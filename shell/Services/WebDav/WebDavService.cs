// 等价移植：rebuild/ai_player/lib/core/services/webdav_service.dart（Dart `WebDavService` + `WebDavEntry`）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §4（WebDAV）——
//   - 目录列举 PROPFIND + `Depth: 1` → 207 Multi-Status
//   - 建目录 MKCOL（已存在会失败，属正常）
//   - 读写 GET / PUT、删除 DELETE（可能返回 204 无响应体）
//   - 鉴权 HTTP Basic（`Authorization: Basic base64(user:pass)`）
//   - 备份产物命名 `/backup_<日期>`
// XML 解析用 System.Xml.Linq：`D:`/`d:`/默认命名空间一律按 **局部名**（Name.LocalName）匹配，
// 避免被不同前缀坑到；元素名比较用 OrdinalIgnoreCase（DAV: 实际全小写，容错）。
// 说明：Dart 用正则分块解析 Multi-Status（不引入 XML 依赖），此处改用 XDocument —— 同为等价语义，
// 但对畸形 XML 的容错更明确（解析失败返回空列表，等价 Dart 无匹配时的空结果）。
// 未实证处（服务端返回非标准字段/畸形 XML/百分号非法转义）一律容错，不抛异常。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.WebDav;

/// <summary>WebDAV 客户端（对应 Dart <c>WebDavService</c>，备份 / 订阅）。</summary>
public sealed class WebDavService
{
    /// <summary>PROPFIND 请求体：只取 displayname / getcontentlength / getlastmodified / resourcetype。</summary>
    private const string PropFindBody =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<d:propfind xmlns:d=\"DAV:\"><d:prop>" +
        "<d:displayname/><d:getcontentlength/><d:getlastmodified/>" +
        "<d:resourcetype/>" +
        "</d:prop></d:propfind>";

    public WebDavService(ShellHttpClient http, ServerConfig server, Action<string> onLog = null)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        OnLog = onLog;
    }

    public ShellHttpClient Http { get; }

    /// <summary>可在运行期替换（Dart 侧 <c>server</c> 亦非 final）。</summary>
    public ServerConfig Server { get; set; }

    public Action<string> OnLog { get; }

    public string BaseUrl => TextUtils.NormalizeBaseUrl(Server.BaseUrl);

    /// <summary>
    /// WebDAV 根路径（<c>extra["rootPath"]</c>）。非字符串或空白 ⇒ <c>null</c>（等价 Dart <c>_rootPath</c>）。
    /// 凭据侧说明：Dart 的 <c>server.password</c> 由 <c>ServerConfig</c> 提供；C# 侧同形（<c>ServerConfig.Password</c>
    /// 读 <c>Extra["password"]</c>），故此处不直接依赖 PasswordStore（本任务不改其它文件）。
    /// </summary>
    public string RootPath
    {
        get
        {
            var root = Server.ExtraString("rootPath");
            if (root == null) return null;
            var trimmed = root.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }

    private void Log(string message) => OnLog?.Invoke(message);

    /// <summary>HTTP Basic（WebDAV 标准鉴权）；User-Agent 与 Dart 一致为 <c>AI Player</c>。</summary>
    public Dictionary<string, string> Headers
    {
        get
        {
            var raw = $"{Server.UserName}:{Server.Password ?? string.Empty}";
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)),
                // t65：UA 走唯一构造点（用户自定义在 WebDAV 面同样生效；此前这里写死 "AI Player"）
                ["User-Agent"] = AIPlayer.Shell.Services.Http.UserAgentPolicy.Current,
            };
        }
    }

    /// <summary>
    /// 拼接请求地址（对应 Dart <c>uri(path)</c>）：<c>baseUrl</c> + <c>rootPath</c> + <c>path</c>，逐段百分号编码
    /// （保留 <c>/</c> 分隔符）。Dart 走 <c>Uri.replace(path:)</c> 编码，语义等价。
    /// </summary>
    public string BuildUrl(string path)
    {
        var root = RootPath;
        var combined = root == null ? (path ?? string.Empty) : root + (path ?? string.Empty);
        var normalized = combined.StartsWith("/", StringComparison.Ordinal) ? combined : "/" + combined;
        return BaseUrl + EncodePath(normalized);
    }

    private static string EncodePath(string path)
    {
        var parts = path.Split('/');
        var builder = new StringBuilder(path.Length + 16);
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) builder.Append('/');
            builder.Append(Uri.EscapeDataString(parts[i]));
        }
        return builder.ToString();
    }

    // ── 探活 / 列举 ──────────────────────────────────────────────────────────

    /// <summary>探活（<c>PROPFIND</c> 根目录 + <c>Depth: 0</c>，10 秒超时）；失败返回 <c>false</c>。</summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = Headers;
            headers["Depth"] = "0";
            await Http.CustomAsync("PROPFIND", BuildUrl("/"), headers, null, TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 连接失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>目录列举（<c>PROPFIND</c> + <c>Depth: 1</c>，解析 207 Multi-Status）；失败返回空列表。</summary>
    /// <remarks>
    /// Dart 侧还有一支「<c>statusCode != 207 &amp;&amp; !isSuccess</c> 打日志」的兜底；C# 的
    /// <see cref="ShellHttpClient"/> 对非 2xx 直接抛 <see cref="ShellHttpException"/>（207 属 2xx 可接受），
    /// 因此该分支不可达，等价行为落到 catch 分支记日志并返回空列表。
    /// </remarks>
    public async Task<List<WebDavEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = Headers;
            headers["Depth"] = "1";
            headers["Content-Type"] = "application/xml; charset=utf-8";
            var result = await Http.CustomAsync("PROPFIND", BuildUrl(path), headers, PropFindBody, null, cancellationToken)
                .ConfigureAwait(false);
            return ParseMultiStatus(result.Body);
        }
        catch (Exception ex)
        {
            Log($"WebDAV 列表失败：{ex.Message}");
            return new List<WebDavEntry>();
        }
    }

    /// <summary>
    /// 极简 Multi-Status 解析（对应 Dart <c>_parseMultiStatus</c>；Dart 为私有静态，此处公开以便自检/测试）。
    /// 逐 <c>response</c> 取 <c>href</c> / <c>resourcetype/collection</c> / <c>getcontentlength</c> / <c>getlastmodified</c>。
    /// </summary>
    public static List<WebDavEntry> ParseMultiStatus(string xml)
    {
        var entries = new List<WebDavEntry>();
        if (string.IsNullOrWhiteSpace(xml)) return entries;

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException)
        {
            // 畸形 XML（部分服务端返回 HTML 错误页）⇒ 视为空目录（未实证处容错）
            return entries;
        }

        var root = document.Root;
        if (root == null) return entries;

        foreach (var response in Descendants(root, "response"))
        {
            var href = FirstValue(response, "href");
            if (href == null) continue;
            href = href.Trim();
            if (href.Length == 0) continue;

            var isDirectory = FirstElement(response, "collection") != null;

            long size = 0L;
            var sizeText = FirstValue(response, "getcontentlength");
            if (!string.IsNullOrEmpty(sizeText))
            {
                long.TryParse(sizeText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
            }

            var modified = WebDavTime.ToLocal(WebDavTime.ParseIso(FirstValue(response, "getlastmodified")));

            entries.Add(new WebDavEntry(DecodeHref(href), isDirectory, size, modified));
        }

        return entries;
    }

    /// <summary>百分号解码（等价 Dart <c>Uri.decodeComponent</c>）；非法转义时原样返回。</summary>
    private static string DecodeHref(string href)
    {
        if (href.IndexOf('%') < 0) return href;
        try
        {
            return Uri.UnescapeDataString(href);
        }
        catch (UriFormatException)
        {
            return href;
        }
    }

    private static IEnumerable<XElement> Descendants(XElement root, string localName)
    {
        foreach (var element in root.Descendants())
        {
            if (Matches(element, localName)) yield return element;
        }
    }

    private static bool Matches(XElement element, string localName)
        => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static XElement FirstElement(XElement root, string localName)
    {
        if (Matches(root, localName)) return root;
        foreach (var element in root.Descendants())
        {
            if (Matches(element, localName)) return element;
        }
        return null;
    }

    private static string FirstValue(XElement root, string localName) => FirstElement(root, localName)?.Value;

    // ── 建目录 ──────────────────────────────────────────────────────────────

    /// <summary>建目录（<c>MKCOL</c>；已存在时会失败，属正常，调用方忽略）。</summary>
    public async Task<bool> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await Http.CustomAsync("MKCOL", BuildUrl(path), Headers, null, null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"MKCOL {path} 失败（可能已存在）：{ex.Message}");
            return false;
        }
    }

    /// <summary>确保目录存在（逐级 <c>MKCOL</c>，等价 Dart <c>ensureDirectory</c>）。</summary>
    public async Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var current = string.Empty;
        foreach (var segment in (path ?? string.Empty).Split('/'))
        {
            if (segment.Length == 0) continue;
            current = current + "/" + segment;
            await CreateDirectoryAsync(current, cancellationToken).ConfigureAwait(false);
        }
    }

    // ── 下载 / 上传 ─────────────────────────────────────────────────────────

    /// <summary>下载文本（<c>GET</c>）；失败返回 <c>null</c>。</summary>
    public async Task<string> ReadStringAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.CustomAsync("GET", BuildUrl(path), Headers, null, null, cancellationToken)
                .ConfigureAwait(false);
            return result.Body;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 读取 {path} 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>下载二进制（<c>GET</c>）；失败返回 <c>null</c>。</summary>
    public async Task<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.CustomAsync("GET", BuildUrl(path), Headers, null, null, cancellationToken)
                .ConfigureAwait(false);
            return result.Bytes;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 下载 {path} 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>下载二进制（<c>GET</c>）—— <see cref="ReadBytesAsync"/> 的同义名（任务接口要求）。</summary>
    public Task<byte[]> DownloadBytesAsync(string path, CancellationToken cancellationToken = default)
        => ReadBytesAsync(path, cancellationToken);

    /// <summary>上传文本（<c>PUT</c>；<c>Content-Type: application/json</c>）；失败返回 <c>false</c>。</summary>
    public async Task<bool> WriteStringAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = Headers;
            headers["Content-Type"] = "application/json; charset=utf-8";
            await Http.PutAsync(BuildUrl(path), headers, Encoding.UTF8.GetBytes(content ?? string.Empty), null, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 上传 {path} 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>上传二进制（<c>PUT</c>；<c>Content-Type: application/octet-stream</c>）；失败返回 <c>false</c>。</summary>
    public async Task<bool> WriteBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = Headers;
            headers["Content-Type"] = "application/octet-stream";
            await Http.PutAsync(BuildUrl(path), headers, content ?? Array.Empty<byte>(), null, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 上传 {path} 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>上传二进制（<c>PUT</c>）—— <see cref="WriteBytesAsync"/> 的同义名（任务接口要求）。</summary>
    public Task<bool> UploadAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        => WriteBytesAsync(path, content, cancellationToken);

    // ── 删除 / 存在性 ───────────────────────────────────────────────────────

    /// <summary>
    /// 删除（<c>DELETE</c>）；失败返回 <c>false</c>。
    /// 语义要点：成功码可能是 200 或 **204（无响应体）**，<see cref="ShellHttpClient"/> 已按 2xx 判定成功并容忍空体。
    /// </summary>
    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await Http.DeleteAsync(BuildUrl(path), Headers, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 删除 {path} 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 存在性探测（新增：Dart <c>WebDavService</c> 无此方法）。
    /// 实现按 SERVICE_API.md §4 的 <c>PROPFIND</c> 语义：`Depth: 0` 命中 2xx ⇒ 存在；404 等异常 ⇒ 不存在。
    /// </summary>
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = Headers;
            headers["Depth"] = "0";
            await Http.CustomAsync("PROPFIND", BuildUrl(path), headers, null, null, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (ShellHttpException ex)
        {
            Log($"PROPFIND {path} 未命中（视为不存在）：{ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Log($"WebDAV 探测 {path} 失败：{ex.Message}");
            return false;
        }
    }
}

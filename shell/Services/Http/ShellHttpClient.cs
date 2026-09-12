// 等价移植：rebuild/ai_player/lib/core/http/http_client.dart（Dart `HttpService`/`HttpResult`/`HttpException`）。
// 依 DESIGN §2 D5：统一一个 ShellHttpClient（超时/重试/代理/二进制），只用 System.Net.Http + System.Text.Json。
// 语义保持：仅 2xx 视为成功，非 2xx 抛 ShellHttpException；4xx 不重试，5xx/网络错误按 defaultRetries 重试（退避 300ms*(n+1)）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Logging;

namespace AIPlayer.Shell.Services.Http;

/// <summary>HTTP 响应载体（对应 Dart <c>HttpResult</c>）。</summary>
public sealed class ShellHttpResult
{
    public ShellHttpResult(int statusCode, byte[] bytes, Dictionary<string, List<string>> headers, string requestUrl)
    {
        StatusCode = statusCode;
        Bytes = bytes ?? Array.Empty<byte>();
        Headers = headers ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        RequestUrl = requestUrl;
    }

    public int StatusCode { get; }

    public byte[] Bytes { get; }

    public Dictionary<string, List<string>> Headers { get; }

    public string RequestUrl { get; }

    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    /// <summary>UTF-8 文本形式（二进制请用 <see cref="Bytes"/>）。</summary>
    public string Body => Encoding.UTF8.GetString(Bytes);

    /// <summary>解析为 JSON；空体或非法 JSON 返回 <c>null</c>（对应 Dart 的容错 json getter）。</summary>
    public JsonElement? Json
    {
        get
        {
            if (Bytes.Length == 0) return null;
            try
            {
                using var doc = JsonDocument.Parse(Bytes);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    public JsonElement? JsonMap
    {
        get
        {
            var v = Json;
            return v.HasValue && v.Value.ValueKind == JsonValueKind.Object ? v : null;
        }
    }

    public JsonElement? JsonList
    {
        get
        {
            var v = Json;
            return v.HasValue && v.Value.ValueKind == JsonValueKind.Array ? v : null;
        }
    }

    /// <summary>按名取首个响应头（大小写不敏感），无则返回 <c>null</c>。</summary>
    public string Header(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var kv in Headers)
        {
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value != null && kv.Value.Count > 0 ? kv.Value[0] : null;
            }
        }
        return null;
    }

    public override string ToString() => $"ShellHttpResult({StatusCode}, {Bytes.Length}B, {RequestUrl})";
}

/// <summary>HTTP 异常（对应 Dart <c>HttpException</c>）。</summary>
public sealed class ShellHttpException : Exception
{
    public ShellHttpException(string message, int? statusCode = null, string url = null)
        : base(message)
    {
        StatusCode = statusCode;
        Url = url;
    }

    public int? StatusCode { get; }

    public string Url { get; }
}

/// <summary>HTTP 客户端（对应 Dart <c>HttpService</c>）。</summary>
public sealed class ShellHttpClient : IDisposable
{
    private readonly Dictionary<string, HttpClient> _clients = new Dictionary<string, HttpClient>(StringComparer.Ordinal);
    private readonly object _gate = new object();
    private bool _disposed;

    public ShellHttpClient(string proxyUrl = null, TimeSpan? defaultTimeout = null, int defaultRetries = 1, string userAgent = null)
    {
        // t61：不传 proxyUrl ⇒ **回落进程级策略** ProxyPolicy.Current（设置一改，下一次请求即生效）；
        // 传了 ⇒ 该实例的显式覆盖（既有行为不变）。
        _proxyOverride = proxyUrl;
        _proxyExplicit = proxyUrl != null;
        // 通用请求默认超时的**唯一构造点**（常量与实现二选一，此处选实现侧，避免"常量写一个值、实现另硬编一个值"的两套口径）。
        // 🔴 这不是图片语义：图片路径的超时另有专门真源 `ImageCacheManager.DefaultImageFetchTimeout`（8 s）；两者语义不同、互不引用。
        DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(20);
        DefaultRetries = defaultRetries;
        // t65：不传 = 用**唯一构造点**的当前值（UserAgentPolicy.Current）；传了 = 该实例的显式覆盖。
        _userAgentOverride = string.IsNullOrEmpty(userAgent) ? null : userAgent;
    }

    private string _proxyOverride;
    private bool _proxyExplicit;

    /// <summary>
    /// 形如 <c>http://127.0.0.1:7890</c>；<c>null</c>/空 = 直连（对应 Dart null/空串）。
    /// 读：本实例**显式设过** ⇒ 用它（把 <c>null</c> 赋进来也算显式直连）；否则回落 <see cref="ProxyPolicy.Current"/>。
    /// 写：只设本实例覆盖（**不**改全局 —— 全局只由 <see cref="ProxyPolicy"/> 改）。
    /// </summary>
    public string ProxyUrl
    {
        get => _proxyExplicit ? _proxyOverride : ProxyPolicy.Current;
        set
        {
            _proxyOverride = value;
            _proxyExplicit = true;   // 显式 null/空 = "本实例直连"，不回落策略（保住 ApplyProxyFromSettings 的既有语义）
        }
    }

    public TimeSpan DefaultTimeout { get; set; }

    public int DefaultRetries { get; set; }

    private string _userAgentOverride;

    /// <summary>
    /// 出站 UA。读：本实例显式覆盖 ?? <see cref="UserAgentPolicy.Current"/>（设置一改，下一次请求即生效，无需重启）；
    /// 写：只设本实例覆盖（**不**改全局默认 —— 全局只由 <see cref="UserAgentPolicy"/> 改）。
    /// </summary>
    public string UserAgent
    {
        get => _userAgentOverride ?? UserAgentPolicy.Current;
        set => _userAgentOverride = string.IsNullOrEmpty(value) ? null : value;
    }

    private HttpClient Client()
    {
        var key = ProxyUrl == null ? string.Empty : ProxyUrl.Trim();
        lock (_gate)
        {
            if (_clients.TryGetValue(key, out var cached))
            {
                EnsureUserAgent(cached);
                return cached;
            }

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
                // Dart 侧仅在显式给了代理串时才设 findProxy；未设即为直连（不走系统代理）。此处保持同一语义。
                UseProxy = false,
            };

            if (key.Length > 0)
            {
                var normalized = key.Contains("://", StringComparison.Ordinal) ? key : "http://" + key;
                if (Uri.TryCreate(normalized, UriKind.Absolute, out var proxyUri))
                {
                    handler.Proxy = new WebProxy(proxyUri);
                    handler.UseProxy = true;
                }
                else
                {
                    // t61 卡面②：非法代理串**不得静默回落直连** ⇒ 落一条可见日志，再按直连继续
                    // （不抛：一个坏设置不该把整个网络面打死；但必须能指认"这次为什么没走代理"）
                    DebugLog.Warn($"PROXY-INVALID url={MaskProxy(key)} ⇒ 本次按**直连**处理（请检查设置里的代理串）");
                }
            }

            // t61 卡面③ 审计面：**每次代理决定变化**（新建 client = key 变了）打一行，可判定"这次请求走没走代理"
            DebugLog.Info($"PROXY-MODE enabled={handler.UseProxy} url={MaskProxy(key)} source={(_proxyExplicit ? "explicit" : "settings")}");

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan, // 超时由 CTS 精确控制（对应 Dart 的 .timeout(timeout)）
                // t107 审计（取数路径必须处处有界）：进程里唯一把 `HttpClient.Timeout` 设成"无界"的就是这一处，
                // 而**每一支**发送都挂了 CTS —— 逐支：
                //   · 便捷方法（GetAsync / PostJsonAsync / PutAsync / DeleteAsync / CustomAsync）⇒ SendAsync（重试循环）
                //     ⇒ SendOnceAsync：**每次尝试**新建 `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter(timeout)`；
                //   · 重试路径 ⇒ 每次 attempt 各建一个 CTS（`300*(attempt+1)ms` 退避本身也传了 ct）；
                //   · 重定向路径 ⇒ 跟随发生在 `Client().SendAsync(request, ResponseHeadersRead, cts.Token)` **之内** ⇒ 同一 CTS；
                //   · 响应体路径 ⇒ `response.Content.ReadAsByteArrayAsync(cts.Token)` 仍在同一 `using cts` 作用域内
                //     ⇒ "先回头部再吊住正文"也被界住；
                //   · 代理路径 ⇒ handler 级 `Proxy`，发送仍是同一调用 ⇒ 同一 CTS。
                // 另有**上层**界：`ImageCacheManager` 的两个 `GetOrFetchAsync` 分别带 `DefaultFetchTimeout`（URL 重载）
                // 与 `WaitAsync(timeout)`（key 重载，t107 补）⇒ 远端吊住最多导致"一次有日志的超时"，不是调用方无限等待。
            };
            EnsureUserAgent(client);

            _clients[key] = client;
            return client;
        }
    }

    /// <summary>
    /// 代理串打码（t61 审计行/告警行用）：<c>http://user:pass@host:port</c> ⇒ <c>http://***:***@host:port</c>。
    /// 代理串可能带 Basic 凭据 ⇒ 日志里**绝不出现明文**。
    /// </summary>
    private static string MaskProxy(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return "(direct)";
        return System.Text.RegularExpressions.Regex.Replace(proxyUrl, "(?i)://[^/@\\s]+@", "://***:***@");
    }

    /// <summary>把当前生效 UA 刷到底层 <see cref="HttpClient"/> 默认头（每次取用前调一次 ⇒ 设置改动立即生效）。</summary>
    private void EnsureUserAgent(HttpClient client)
    {
        var desired = UserAgent ?? string.Empty;
        if (client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            var existing = string.Join(",", client.DefaultRequestHeaders.GetValues("User-Agent"));
            if (string.Equals(existing, desired, StringComparison.Ordinal)) return;
            client.DefaultRequestHeaders.Remove("User-Agent");
        }
        if (desired.Length > 0)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", desired);
        }
    }

    /// <summary>通用请求。body 可为 <c>byte[]</c>（原始）、<c>string</c>（UTF-8 文本）、<c>JsonElement</c>/<c>IDictionary</c>/POCO（自动 JSON）。</summary>
    public async Task<ShellHttpResult> SendAsync(
        string method,
        string url,
        IDictionary<string, string> headers = null,
        object body = null,
        TimeSpan? timeout = null,
        int? retries = null,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(BuildUri(url, null), UriKind.Absolute);
        var attemptLimit = (retries ?? DefaultRetries) + 1;
        Exception lastError = null;

        // t118：走了代理就先看**带 TTL 的健康结论**（不是每个请求各探一次）。
        // 代理不可达 ⇒ **立即明确失败**（选"失败"而非"静默直连"：代理开着多半为变更出口，
        // 悄悄改直连等于改用户的网络语义，还可能把流量漏到本地出口）；
        // 终态在数百毫秒内到达，并留一行点名诊断（reason + 探测耗时 + 是否缓存命中）。
        var proxyForThisCall = ProxyUrl;
        if (!string.IsNullOrEmpty(proxyForThisCall))
        {
            var health = await ProxyPolicy.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (!health.Ok)
            {
                DebugLog.Warn($"PROXY-DEGRADE action=refuse proxy={MaskProxy(proxyForThisCall)} reason={health.Reason} probeMs={health.ElapsedMs} cached={health.FromCache} detail={health.Detail}");
                throw new ShellHttpException($"代理不可达（PROXY-HEALTH {health.Reason}）：{MaskProxy(proxyForThisCall)}", null, url);
            }
        }

        for (var attempt = 0; attempt < attemptLimit; attempt++)
        {
            try
            {
                return await SendOnceAsync(method, uri, headers, body, timeout ?? DefaultTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ShellHttpException ex)
            {
                // 4xx（及 3xx 未跟随）不重试
                if (ex.StatusCode.HasValue && ex.StatusCode.Value < 500) throw;
                lastError = ex;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt < attemptLimit - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }

        if (lastError is ShellHttpException httpEx) throw httpEx;
        throw new ShellHttpException(
            lastError == null ? $"请求失败: {method} {uri}" : $"请求失败: {method} {uri} ({lastError.Message})",
            null,
            uri.ToString());
    }

    private async Task<ShellHttpResult> SendOnceAsync(
        string method,
        Uri url,
        IDictionary<string, string> headers,
        object body,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");

        if (headers != null)
        {
            foreach (var kv in headers)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                // 内容头必须走 Content，否则 HttpRequestMessage 抛异常
                if (!request.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                {
                    // 留待内容头处理
                }
            }
        }

        if (body != null)
        {
            HttpContent content;
            switch (body)
            {
                case byte[] raw:
                    content = new ByteArrayContent(raw);
                    break;
                case string text:
                    content = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
                    break;
                case JsonElement json:
                    content = new ByteArrayContent(Encoding.UTF8.GetBytes(json.GetRawText()));
                    content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                    break;
                default:
                    var payload = JsonSerializer.Serialize(body);
                    content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
                    content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                    break;
            }

            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (kv.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    {
                        content.Headers.Remove(kv.Key);
                        content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }
                }
            }

            request.Content = content;
        }
        else
        {
            request.Content = new ByteArrayContent(Array.Empty<byte>());
            request.Content.Headers.ContentLength = 0;
        }

        HttpResponseMessage response;
        try
        {
            response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShellHttpException($"请求超时: {method} {url}", null, url.ToString());
        }

        using (response)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            var collected = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers)
            {
                collected[h.Key] = h.Value.ToList();
            }
            foreach (var h in response.Content.Headers)
            {
                collected[h.Key] = h.Value.ToList();
            }

            var result = new ShellHttpResult((int)response.StatusCode, bytes, collected, url.ToString());
            if (!result.IsSuccess)
            {
                throw new ShellHttpException(
                    $"HTTP {result.StatusCode} {response.ReasonPhrase}",
                    result.StatusCode,
                    url.ToString());
            }
            return result;
        }
    }

    // ── 便捷方法（与 Dart 侧同名同义）────────────────────────────────────────

    public Task<ShellHttpResult> GetAsync(
        string url,
        IDictionary<string, string> headers = null,
        IDictionary<string, string> query = null,
        TimeSpan? timeout = null,
        int? retries = null,
        CancellationToken cancellationToken = default)
        => SendAsync("GET", BuildUri(url, query), headers, null, timeout, retries, cancellationToken);

    public Task<ShellHttpResult> PostJsonAsync(
        string url,
        object body,
        IDictionary<string, string> headers = null,
        TimeSpan? timeout = null,
        int? retries = null,
        CancellationToken cancellationToken = default)
        => SendAsync("POST", url, headers, body, timeout, retries, cancellationToken);

    public Task<ShellHttpResult> PutAsync(
        string url,
        IDictionary<string, string> headers = null,
        object body = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => SendAsync("PUT", url, headers, body, timeout, 0, cancellationToken);

    public Task<ShellHttpResult> DeleteAsync(
        string url,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
        => SendAsync("DELETE", url, headers, null, null, 0, cancellationToken);

    /// <summary>HTTP 不限制方法名 ⇒ WebDAV 的 <c>PROPFIND</c>/<c>MKCOL</c> 直接透传。</summary>
    public Task<ShellHttpResult> CustomAsync(
        string method,
        string url,
        IDictionary<string, string> headers = null,
        object body = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => SendAsync(method, url, headers, body, timeout, 0, cancellationToken);

    /// <summary>把 query 合并进 URL（覆盖同名键）。</summary>
    public static string BuildUri(string url, IDictionary<string, string> query)
    {
        if (query == null || query.Count == 0) return url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

        var pairs = new List<string>();
        var existing = new Dictionary<string, string>(StringComparer.Ordinal);
        var raw = uri.Query.TrimStart('?');
        if (raw.Length > 0)
        {
            foreach (var part in raw.Split('&'))
            {
                if (part.Length == 0) continue;
                var idx = part.IndexOf('=');
                var k = idx < 0 ? part : part.Substring(0, idx);
                var v = idx < 0 ? string.Empty : part.Substring(idx + 1);
                existing[Uri.UnescapeDataString(k)] = v;
            }
        }
        foreach (var kv in query)
        {
            existing[kv.Key] = Uri.EscapeDataString(kv.Value ?? string.Empty);
        }
        foreach (var kv in existing)
        {
            pairs.Add($"{Uri.EscapeDataString(kv.Key)}={kv.Value}");
        }

        var builder = new UriBuilder(uri) { Query = string.Join("&", pairs) };
        return builder.Uri.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            foreach (var c in _clients.Values)
            {
                try { c.Dispose(); }
                catch (Exception)   // 有意忽略：单个连接释放失败不得阻断整批释放（Dispose 路径幂等）
                {
                }
            }
            _clients.Clear();
        }
    }
}

// 本地 mock WebDAV 服务器。
// 方法/头形态按 reversed/FlutterApp/SERVICE_API.md §4 构造：
//   PROPFIND（Depth: 1）+ 207 Multi-Status、MKCOL、GET/PUT、DELETE、HTTP Basic 鉴权。
// 用途：验证 207 XML 解析、Basic 鉴权、PUT/GET 往返 —— 这些是本层最易出错的部分。
// **mock 通过 ≠ 真服务器通过。**

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Mock;

public sealed class MockWebDavServer : IDisposable
{
    public const string ValidUser = "dav";
    public const string ValidPassword = "dav-secret";

    private readonly MockHttpServer _server;
    private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly HashSet<string> _dirs = new HashSet<string>(StringComparer.Ordinal) { "/" };
    private readonly object _gate = new object();

    public MockWebDavServer()
    {
        _server = new MockHttpServer(Handle);
    }

    public string BaseUrl => _server.BaseUrl;

    public IReadOnlyList<MockRequest> Requests => _server.Requests;

    /// <summary>Basic 鉴权失败次数（应恒为 0）。</summary>
    public int AuthFailureCount { get; private set; }

    public MockWebDavServer Start()
    {
        _server.Start();
        return this;
    }

    public int CountRequests(string method) => Requests.Count(r => string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase));

    private MockResponse Handle(MockRequest request)
    {
        // HTTP Basic 鉴权
        var auth = request.Header("Authorization");
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidUser}:{ValidPassword}"));
        if (auth == null || !string.Equals(auth, expected, StringComparison.Ordinal))
        {
            AuthFailureCount++;
            return new MockResponse { StatusCode = 401, ContentType = "text/plain; charset=utf-8", Body = Encoding.UTF8.GetBytes("unauthorized") };
        }

        var path = request.Path;
        var method = request.Method.ToUpperInvariant();

        switch (method)
        {
            case "PROPFIND":
                return PropFind(path, request.Header("Depth"));

            case "MKCOL":
                lock (_gate)
                {
                    _dirs.Add(path);
                }
                return new MockResponse { StatusCode = 201, ContentType = "text/plain; charset=utf-8", Body = Array.Empty<byte>() };

            case "PUT":
                lock (_gate)
                {
                    _files[path] = request.BodyBytes;
                }
                return new MockResponse { StatusCode = 201, ContentType = "text/plain; charset=utf-8", Body = Array.Empty<byte>() };

            case "GET":
                lock (_gate)
                {
                    if (_files.TryGetValue(path, out var bytes))
                    {
                        return new MockResponse { StatusCode = 200, ContentType = "application/octet-stream", Body = bytes };
                    }
                }
                return new MockResponse { StatusCode = 404, ContentType = "text/plain; charset=utf-8", Body = Encoding.UTF8.GetBytes("not found") };

            case "DELETE":
                lock (_gate)
                {
                    _files.Remove(path);
                    _dirs.Remove(path);
                }
                return new MockResponse { StatusCode = 204, ContentType = "text/plain; charset=utf-8", Body = Array.Empty<byte>() };

            default:
                return new MockResponse { StatusCode = 405, ContentType = "text/plain; charset=utf-8", Body = Array.Empty<byte>() };
        }
    }

    private MockResponse PropFind(string path, string depth)
    {
        var entries = new List<string>();
        var normalized = path.EndsWith("/", StringComparison.Ordinal) || path.Length == 0 ? path : path + "/";

        bool exists;
        lock (_gate)
        {
            exists = _dirs.Contains(path) || _dirs.Contains(normalized) || _files.ContainsKey(path) || _files.ContainsKey(normalized);
        }
        if (!exists)
        {
            // 真 WebDAV 对不存在路径返回 404；这里必须同构，否则 ExistsAsync 永远为 true（自检已抓到该测试替身缺陷）。
            return new MockResponse { StatusCode = 404, ContentType = "text/plain; charset=utf-8", Body = Encoding.UTF8.GetBytes("not found") };
        }

        var selfIsDir = true;
        lock (_gate)
        {
            selfIsDir = _dirs.Contains(path) || _dirs.Contains(normalized);
        }
        entries.Add(ResponseXml(normalized, selfIsDir, 0, DateTime.UtcNow));

        if (!string.Equals(depth, "0", StringComparison.Ordinal))
        {
            lock (_gate)
            {
                foreach (var dir in _dirs.Where(d => d != path && d.StartsWith(normalized, StringComparison.Ordinal) && d != "/").OrderBy(d => d))
                {
                    var rest = dir.Substring(normalized.Length);
                    if (rest.Contains("/") && rest.TrimEnd('/').Contains("/")) continue; // 只列直接子项
                    entries.Add(ResponseXml(dir.EndsWith("/", StringComparison.Ordinal) ? dir : dir + "/", true, 0, DateTime.UtcNow));
                }

                foreach (var file in _files.Where(f => f.Key.StartsWith(normalized, StringComparison.Ordinal) && !f.Key.Substring(normalized.Length).Contains("/")).OrderBy(f => f.Key))
                {
                    entries.Add(ResponseXml(file.Key, false, file.Value.Length, DateTime.UtcNow));
                }
            }
        }

        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                  + "<D:multistatus xmlns:D=\"DAV:\">\n"
                  + string.Join("\n", entries)
                  + "\n</D:multistatus>";

        return new MockResponse
        {
            StatusCode = 207,
            ContentType = "application/xml; charset=utf-8",
            Body = Encoding.UTF8.GetBytes(xml),
        };
    }

    private static string ResponseXml(string href, bool isDirectory, long size, DateTime modified)
    {
        var escaped = System.Security.SecurityElement.Escape(href);
        var resourceType = isDirectory ? "<D:collection/>" : string.Empty;
        var lengthXml = isDirectory ? string.Empty : $"<D:getcontentlength>{size}</D:getcontentlength>";
        var modifiedXml = $"<D:getlastmodified>{modified:R}</D:getlastmodified>";

        return "  <D:response>\n"
               + $"    <D:href>{escaped}</D:href>\n"
               + "    <D:propstat>\n"
               + $"      <D:prop><D:resourcetype>{resourceType}</D:resourcetype>{lengthXml}{modifiedXml}</D:prop>\n"
               + "      <D:status>HTTP/1.1 200 OK</D:status>\n"
               + "    </D:propstat>\n"
               + "  </D:response>";
    }

    public void Dispose() => _server.Dispose();
}

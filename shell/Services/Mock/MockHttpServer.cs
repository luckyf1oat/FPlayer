// 极简回环 HTTP/1.1 服务端（零第三方依赖，走 TcpListener 而非 HttpListener —— 避开 http.sys 的 URL ACL 需管理员问题）。
// 用途：为「无真服务器也能端到端跑通」提供忠实于 reversed/FlutterApp/SERVICE_API.md 端点/头/参数形态的 mock 底座
// （WORKSPACE.md §3：没有真实服务器时一律用服务层自带的本地 mock）。
// 限制（明确标注，勿当成通用服务器）：HTTP/1.1、每连接一请求（Connection: close）、支持 Content-Length 定长体、不支持 chunked/keep-alive/TLS。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.Mock;

public sealed class MockRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string RawQuery { get; set; } = string.Empty;
    public Dictionary<string, string> Query { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
    public byte[] BodyBytes { get; set; } = Array.Empty<byte>();

    public string Header(string name) => Headers.TryGetValue(name, out var v) ? v : null;

    public string QueryValue(string name) => Query.TryGetValue(name, out var v) ? v : null;

    public override string ToString() => $"{Method} {Path}{(RawQuery.Length > 0 ? "?" + RawQuery : string.Empty)}";
}

public sealed class MockResponse
{
    public int StatusCode { get; set; } = 200;
    public string ContentType { get; set; } = "application/json; charset=utf-8";
    public byte[] Body { get; set; } = Array.Empty<byte>();

    public static MockResponse Json(string json, int statusCode = 200) => new MockResponse
    {
        StatusCode = statusCode,
        ContentType = "application/json; charset=utf-8",
        Body = Encoding.UTF8.GetBytes(json),
    };

    public static MockResponse Empty(int statusCode = 204) => new MockResponse
    {
        StatusCode = statusCode,
        ContentType = "text/plain; charset=utf-8",
        Body = Array.Empty<byte>(),
    };

    public static MockResponse Text(string text, int statusCode = 200) => new MockResponse
    {
        StatusCode = statusCode,
        ContentType = "text/plain; charset=utf-8",
        Body = Encoding.UTF8.GetBytes(text),
    };
}

/// <summary>回环 mock HTTP 服务端。</summary>
public sealed class MockHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<MockRequest, MockResponse> _handler;
    private readonly List<MockRequest> _requests = new List<MockRequest>();
    private readonly object _gate = new object();
    private volatile bool _stopped;

    public MockHttpServer(Func<MockRequest, MockResponse> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    public int Port { get; private set; }

    public string BaseUrl { get; private set; }

    /// <summary>按到达顺序记录的全部请求（供断言/证据）。</summary>
    public IReadOnlyList<MockRequest> Requests
    {
        get
        {
            lock (_gate) return _requests.ToList();
        }
    }

    public MockHttpServer Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUrl = $"http://127.0.0.1:{Port}";
        Task.Run(AcceptLoop);
        return this;
    }

    private async Task AcceptLoop()
    {
        while (!_stopped)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or System.Net.Sockets.SocketException or InvalidOperationException)   // 有意忽略：监听器已停止（Dispose）=> 结束接受循环
            {
                break;
            }
            _ = Task.Run(() => Handle(client));
        }
    }

    private void Handle(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 10000;
                stream.WriteTimeout = 10000;

                var headerText = ReadUntilDoubleCrlf(stream, out var leftover);
                if (headerText == null) return;

                var request = ParseRequest(headerText, leftover, stream);
                lock (_gate) _requests.Add(request);

                MockResponse response;
                try
                {
                    response = _handler(request) ?? MockResponse.Empty(404);
                }
                catch (Exception ex)
                {
                    response = MockResponse.Text("mock handler error: " + ex.Message, 500);
                }

                WriteResponse(stream, response);
            }
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or ObjectDisposedException) { /* 有意忽略：mock 侧异常不影响被测方判定 */ }
    }

    private static string ReadUntilDoubleCrlf(NetworkStream stream, out byte[] leftover)
    {
        var buffer = new List<byte>(1024);
        var chunk = new byte[1];
        while (true)
        {
            int read;
            try
            {
                read = stream.Read(chunk, 0, 1);
            }
            catch (IOException)   // 有意忽略：读请求头时对端断开 => 视为「无请求」返回 null（不影响 mock 判定）
            {
                leftover = Array.Empty<byte>();
                return null;
            }
            if (read <= 0)
            {
                leftover = Array.Empty<byte>();
                return null;
            }
            buffer.Add(chunk[0]);
            var n = buffer.Count;
            if (n >= 4 && buffer[n - 4] == 13 && buffer[n - 3] == 10 && buffer[n - 2] == 13 && buffer[n - 1] == 10)
            {
                break;
            }
            if (n > 128 * 1024)
            {
                leftover = Array.Empty<byte>();
                return null;
            }
        }
        leftover = Array.Empty<byte>();
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static MockRequest ParseRequest(string headerText, byte[] leftover, NetworkStream stream)
    {
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        var requestLine = lines.Length > 0 ? lines[0] : string.Empty;
        var parts = requestLine.Split(' ');

        var request = new MockRequest
        {
            Method = parts.Length > 0 ? parts[0] : "GET",
        };

        var target = parts.Length > 1 ? parts[1] : "/";
        var qIndex = target.IndexOf('?');
        if (qIndex >= 0)
        {
            request.Path = target.Substring(0, qIndex);
            request.RawQuery = target.Substring(qIndex + 1);
        }
        else
        {
            request.Path = target;
        }

        foreach (var kv in ParseQuery(request.RawQuery))
        {
            request.Query[kv.Key] = kv.Value;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0) continue;
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var name = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();
            request.Headers[name] = value;
        }

        var contentLength = 0;
        if (request.Headers.TryGetValue("Content-Length", out var cl))
        {
            int.TryParse(cl, out contentLength);
        }

        var body = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            int read;
            try
            {
                read = stream.Read(body, offset, contentLength - offset);
            }
            catch (IOException)   // 有意忽略：读正文时连接被中断 => 跳出循环，按已读长度继续（不抛给调用方）
            {
                break;
            }
            if (read <= 0) break;
            offset += read;
        }
        if (offset < contentLength)
        {
            Array.Resize(ref body, offset);
        }

        request.BodyBytes = body;
        request.Body = Encoding.UTF8.GetString(body);
        return request;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string raw)
    {
        if (string.IsNullOrEmpty(raw)) yield break;
        foreach (var part in raw.Split('&'))
        {
            if (part.Length == 0) continue;
            var idx = part.IndexOf('=');
            var key = idx < 0 ? part : part.Substring(0, idx);
            var value = idx < 0 ? string.Empty : part.Substring(idx + 1);
            yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(key), Uri.UnescapeDataString(value));
        }
    }

    private static void WriteResponse(NetworkStream stream, MockResponse response)
    {
        var reason = response.StatusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            207 => "Multi-Status",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK",
        };

        var header = new StringBuilder();
        header.Append($"HTTP/1.1 {response.StatusCode} {reason}\r\n");
        header.Append($"Content-Type: {response.ContentType}\r\n");
        header.Append($"Content-Length: {response.Body.Length}\r\n");
        header.Append("Connection: close\r\n\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (response.Body.Length > 0)
        {
            stream.Write(response.Body, 0, response.Body.Length);
        }
        stream.Flush();
    }

    public void Dispose()
    {
        _stopped = true;
        try { _listener.Stop(); }
        catch (Exception)   // 有意忽略：Dispose 路径幂等 —— 停止已停止/已释放的监听器不得抛
        {
        }
    }
}

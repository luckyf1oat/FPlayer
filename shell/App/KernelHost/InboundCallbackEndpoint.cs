// t12-A 第 ① 项：外壳侧回环回调端点（内核 → 外壳 的 9 类事件入口）。
//
// 契约来源：shell/docs/T12_CALLBACK_SURFACE.md（9 类事件）/（响应规则）/（空体 ≠ {}）。
// 关键裁决（逐条落到本实现，均来自 WORKSPACE 事实 33/54/61）：
//   1) 端口完全由外壳决定 —— 内核侧没有端口参数，只做 Uri.TryCreate(callbackUrl, UriKind.Absolute)
//      ⇒ 这里绑 IPAddress.Loopback + 端口 0，再把系统分配的真实端口拼成绝对 URL 交给内核。
//   2) **空 body ≠ {}**：内核 `IsNullOrWhiteSpace(text) ? null : Deserialize<HostNavigateOptions>(text)`。
//      ⇒ 「没有内容要说」时一律回 **200 + 空 body**；**绝不回 `{}`** —— 回 `{}` 会让 `progress` 的消费端
//      （PlayerViewModel.cs:5691-5711，对非 null options 无 MediaPath 守卫）无条件写 `_pendingNextEpisodeData`，
//      导致 `alreadyPreloaded` 恒真、**本次播放会话内"下一集预载"被静默禁用**。
//   3) 响应形态分两种（**7 + 2 = 9**，`services` 与 `verifier` 各自独立复核同一文件 `PlaybackReportClient.cs`）：
//        · 要响应体的 **7 条** = `progress`（走 `SendAndReceiveAsync`，**且只接受 HTTP 200**：先 EnsureSuccessStatusCode、再要求 StatusCode==OK 才反序列化）
//          + 6 条导航/换源类（`navigate_previous`/`navigate_next`/`navigate_episode`/`preload_episode`/`switch_version`/`refresh_playback_url`，走 `SendForNavigateAsync`，**任意 2xx**）；
//        · 忽略响应体 **2 条** = `stopped` / `manual_resize`（走 `SendAsync`，任意 2xx，这里回 204）。
//      ⚠️ 本注释原写「6 条」是**逐字继承的错数**：旧汇总句把 `SendAndReceiveAsync`(×1=progress) 与 `SendForNavigateAsync`(×6) 合并计数 ⇒ 少算 1。引用一律用可自证算式 **9 − 2 = 7**。
//   4) 超时预算：上报类 5 s、导航类 90 s（由内核侧决定；本端点只负责尽快应答）。
//
// ⚠️ 为什么用 TcpListener 而不是 HttpListener：`HttpListener` 对 `http://127.0.0.1:<port>/` 通常需要
//    URL ACL（urlacl 预留 / 提权），而 `TcpListener` 无此要求 —— 与 shell/Services/Mock/MockHttpServer.cs:75
//    的既有做法一致（那里也是 `new TcpListener(IPAddress.Loopback, 0)`）。
//
// ⚠️ 本类**不引用内核类型**（S1 落地前内核源码尚未链入），因此用 `Func<CallbackRequest, string>` 作为响应接缝：
//    由 t12-A 第 ② 步的接线处返回「内核自己的 HostNavigateOptions 序列化结果」或 **null（= 空 body）**。
//    这条接缝保证「不手搓 JSON」：序列化内核类型是调用方的事，本类只搬字节。

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.KernelHost;

/// <summary>内核发来的回调请求（已解析的最小面）。</summary>
public sealed class CallbackRequest
{
    public CallbackRequest(string eventName, string body)
    {
        EventName = eventName;
        Body = body;
    }

    /// <summary>9 类事件名（`progress` / `stopped` / `manual_resize` / `navigate_previous` /
    /// `navigate_next` / `navigate_episode` / `preload_episode` / `switch_version` / `refresh_playback_url`）。</summary>
    public string EventName { get; }

    /// <summary>原始请求体（kernel 的 camelCase JSON；含 `event` 键）。</summary>
    public string Body { get; }
}

/// <summary>
/// 外壳侧回环 HTTP 端点。**只负责**：绑定端口、收 POST、解析出事件名、把应答交回调用方决定。
/// 不知道也不序列化任何内核类型；不知道业务语义。
/// </summary>
public sealed class InboundCallbackEndpoint : IDisposable
{
/// <summary>内核固定 POST 到这个路径（见契约：`http://127.0.0.1:&lt;port&gt;/callback`）。</summary>
    public const string CallbackPath = "/callback";

    /// <summary>响应体被内核忽略的两类事件（任意 2xx 即可，这里回 204）。</summary>
    private static readonly HashSet<string> BodyIgnoredEvents = new(StringComparer.Ordinal)
    {
        "stopped",
        "manual_resize",
    };

    // ── 自检观察面（只读；给分帧用例用，不参与生产逻辑）────────────────────────
    //
    // 为什么要暴露它：`stopped` 的线响应是 **204（无正文）** ⇒ 光看 HTTP 层**分不出**
    // "切字节正确" 与 "切歪了但恰好还含 event 键"。观察面把**真正读出来的正文**给自检，
    // 自检就能同时断言 bodyLen / 是否含 U+FFFD / 是否逐字节等值。

    private volatile string _lastEventName = string.Empty;
    private volatile string _lastInnerBody = string.Empty;

    /// <summary>最近一次成功解析出的 `event` 名（空串 = 没解析出来）。</summary>
    public string LastEventName => _lastEventName;

    /// <summary>最近一次**按 `Content-Length` 切出来并解码**的正文原文。</summary>
    public string LastInnerBody => _lastInnerBody;

    private readonly Func<CallbackRequest, string> _respond;
    private readonly Action<string> _log;

    private TcpListener _listener;
    private CancellationTokenSource _cts;

    /// <param name="respond">
/// 响应接缝：返回**要写给内核的 body**；返回 **null 或空串 ⇒ 200 + 空 body**（⚠️ 契约：
    /// 「没有内容要说」必须用空 body，**不得用 `{}`**）。
    /// 抛出的异常由本类兜住并回 500，不会打断监听循环。
    /// </param>
    /// <param name="log">可选日志（外壳侧 Program.Log 或 DebugLog）。</param>
    public InboundCallbackEndpoint(Func<CallbackRequest, string> respond, Action<string> log = null)
    {
        _respond = respond ?? (_ => null);
        _log = log;
    }

    /// <summary>实际监听的绝对 URL（含系统分配的真实端口）；未启动时为 null。</summary>
    public string CallbackUrl { get; private set; }

    public int Port { get; private set; }

    public bool IsRunning => _listener != null;

    /// <summary>绑定 `IPAddress.Loopback` + **端口 0**（由系统分配），并启动接收循环。</summary>
    public void Start() => Start(0);

    /// <summary>
    /// 启动监听；<paramref name="preferredPort"/> &gt; 0 时**先试固定端口**。
    ///
    /// 为什么要固定端口（S5 体验设施，`t61` 项①）：内核**只在启动参数里拿到一次** `callbackUrl`
    /// （`--callback-url=`），端口每次启动都变 ⇒ 端口一旦与上次不同，这套"固定"就没意义。
    /// 端口值的**唯一真相源**是服务层 `ConfigPortService`（含复用/重选/落盘规则）；这里只消费它的裁决。
    ///
    /// 🔴 不可绑时**绝不静默**：先落一行带原因与回落结果的日志，再退到系统随机端口。
    /// </summary>
    public void Start(int preferredPort)
    {
        if (_listener != null)
        {
            return;
        }

        TcpListener listener = null;
        var fallbackReason = string.Empty;

        if (preferredPort > 0)
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, preferredPort);
                listener.Start();
            }
            catch (Exception ex) when (ex is SocketException or ArgumentOutOfRangeException)
            {
                listener = null;
                fallbackReason = ex is SocketException socket ? socket.SocketErrorCode.ToString() : ex.GetType().Name;
            }
        }

        if (listener == null)
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            if (preferredPort > 0)
            {
                _log?.Invoke("CALLBACK-ENDPOINT preferred-port " + preferredPort + " 不可绑（" + fallbackReason
                    + "）⇒ **回落随机端口**（本次内核拿到的是随机端口；下次启动会再试固定端口）");
            }
        }

        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        CallbackUrl = $"http://127.0.0.1:{Port}{CallbackPath}";
        _listener = listener;
        _cts = new CancellationTokenSource();

        _log?.Invoke("CALLBACK-ENDPOINT listening url=" + CallbackUrl
            + " preferred=" + (preferredPort > 0 ? preferredPort.ToString(System.Globalization.CultureInfo.InvariantCulture) : "<none>")
            + " fixed=" + (preferredPort == Port));
        _ = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (Exception ex)
        {
            // Cancel() 会把注册回调里的异常聚合成 AggregateException（CTS 已释放则是 ObjectDisposedException）。
            // 关停路径必须走完 ⇒ 吞掉，但**不静默**：落一行日志（这里没有 await，不涉及吞 OCE）。
            _log?.Invoke("CALLBACK-ENDPOINT cancel-failed " + ex.GetType().Name + ": " + ex.Message);
        }

        try
        {
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            // Stop() 在已停/套接字被回收时会抛 SocketException / ObjectDisposedException；
            // 关停必须幂等且不得抛出 ⇒ 吞掉并留痕（与上一处同口径：不静默）
            _log?.Invoke("CALLBACK-ENDPOINT stop-failed " + ex.GetType().Name + ": " + ex.Message);
        }

        _listener = null;
        _log?.Invoke("CALLBACK-ENDPOINT stopped");
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                await HandleClientAsync(client, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;   // Stop() 关掉了 listener
            }
            catch (Exception ex)
            {
                // ⚠️ 单次请求的任何异常都不得打死监听循环（否则一次坏请求就让回调面永久失效）。
                _log?.Invoke("CALLBACK-ENDPOINT accept-error " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                try
                {
                    client?.Close();
                }
                catch (Exception ex)
                {
                    // 关连接失败只影响这一条请求；监听循环必须继续 ⇒ 吞掉并留痕（不得静默）
                    _log?.Invoke("CALLBACK-ENDPOINT close-failed " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        var (method, path, body) = await ReadRequestAsync(stream, ct).ConfigureAwait(false);

// 路由与结果码（契约 的对照面：内核自己的 PlayerUpdateServer 也是「非 POST→405 / 路径不符→404」）
        // ⚠️ **只按 `?` 之前的路径部分比对**：请求行是 `path[?query] [HTTP/x.y]`，
        //    带 query 的请求（含我们的边界自检用的空格填充）不应被误判成 404。
        if (method != "POST")
        {
            await WriteResponseAsync(stream, 405, null, ct).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(path, CallbackPath, StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 404, null, ct).ConfigureAwait(false);
            return;
        }

        var eventName = ExtractEventName(body);
        _lastEventName = eventName ?? string.Empty;
        _lastInnerBody = body ?? string.Empty;
        if (string.IsNullOrEmpty(eventName))
        {
            // body 为空或没有 event 键 ⇒ 无法归属到 9 类事件之一（内核不会这样发；防御性 400）
            await WriteResponseAsync(stream, 400, null, ct).ConfigureAwait(false);
            _log?.Invoke("CALLBACK-ENDPOINT 400 (no 'event' in body)");
            return;
        }

        if (BodyIgnoredEvents.Contains(eventName))
        {
            // t221：`stopped` 在这里就返回了 ⇒ **观察点必须在此之前**（否则播放中控制会话不知道本轮已结束）
            ShellCallback.NotifyCallbackObserved(eventName, body);
            await WriteResponseAsync(stream, 204, null, ct).ConfigureAwait(false);
            _log?.Invoke($"CALLBACK-ENDPOINT {eventName} -> 204");
            return;
        }

        // t221：`progress` 的观察点（状态回推 + `updateUrl`）——会话据此建唯一控制客户端（SEAM①）
        if (string.Equals(eventName, "progress", StringComparison.Ordinal))
        {
            ShellCallback.NotifyCallbackObserved(eventName, body);
        }

        // E-P1（t38）外壳侧"解析环"：`progress` 上报体里带内核自己的 /segments 基址（`updateUrl`）。
        // 在此**解析并保存到会话状态**（此前全仓 0 处消费）⇒ K1 修好内核端口之后，外壳才真的拿得到地址。
        if (string.Equals(eventName, "progress", StringComparison.Ordinal))
        {
            var updateUrl = ShellCallback.TryRecordUpdateUrlFromBody(body);
            if (!string.IsNullOrEmpty(updateUrl))
            {
                _log?.Invoke($"CALLBACK-ENDPOINT progress: kernel updateUrl = {updateUrl}");
            }
        }

        string responseBody;
        try
        {
            responseBody = _respond(new CallbackRequest(eventName, body));
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(stream, 500, null, ct).ConfigureAwait(false);
            _log?.Invoke($"CALLBACK-ENDPOINT {eventName} -> 500 {ex.GetType().Name}");
            return;
        }

// ⚠️ 契约：null / 空串 ⇒ **空 body**（内核解析为 null = 「没拿到新选项」，安全）；
        //    **不要**把它变成 "{}"（那会被当成非 null 的 options，触发 `progress` 的预载副作用）。
        var payload = string.IsNullOrEmpty(responseBody) ? string.Empty : responseBody;
        await WriteResponseAsync(stream, 200, payload, ct).ConfigureAwait(false);
        _log?.Invoke($"CALLBACK-ENDPOINT {eventName} -> 200 bodyLen={payload.Length}");
    }

    /// <summary>从 camelCase JSON 里取 `event` 值（不引入 JSON 库：只做一次最小、可失败即拒的提取）。</summary>
    private static string ExtractEventName(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        const string Key = "\"event\"";
        var i = body.IndexOf(Key, StringComparison.Ordinal);
        if (i < 0)
        {
            return null;
        }

        i = body.IndexOf(':', i + Key.Length);
        if (i < 0)
        {
            return null;
        }

        i = body.IndexOf('"', i + 1);
        if (i < 0)
        {
            return null;
        }

        var end = body.IndexOf('"', i + 1);
        return end > i ? body.Substring(i + 1, end - i - 1).Trim() : null;
    }

    private const int ReadChunkSize = 8192;
    private const int MaxHeaderBytes = 64 * 1024;
    private const int MaxRequestBytes = 10 * 1024 * 1024;

    /// <summary>
    /// 读一个 HTTP 请求并切出 (method, path, body)。
    ///
    /// <para>🔴 **t27-F-A 第 ③ 项（字节/字符混淆修复）**：本方法原实现把整段读成 <c>string</c> 后再用
    /// <c>Encoding.UTF8.GetByteCount(bodyText)</c> 与 <c>Content-Length</c> 比较、并用
    /// <c>bodyText.Substring(0, contentLength)</c> 截断 —— **两处都是字符下标当字节下标**：
    /// 正文一旦含非 ASCII（**Emby 影片标题就是中文**）⇒ 字符数 &lt; 字节数 ⇒ 补读循环永不满足或
    /// 截断落在字符中间 ⇒ 正文被截断/损坏。修复 = **全程按字节缓冲**（`List&lt;byte&gt;` + 字节级
    /// 找 <c>\r\n\r\n</c> + 按 Content-Length 切字节 + 只在最后解码一次 UTF-8）。</para>
    ///
    /// <para>边界语义：已到字节 **少于** Content-Length ⇒ 返回已解码的部分（交由调用方的路由/解析判失败）；
    /// 超出上限 ⇒ 抛 <see cref="InvalidOperationException"/>（由 accept 循环兜住、不打死监听）。</para>
    /// </summary>
    private static async Task<(string Method, string Path, string Body)> ReadRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[ReadChunkSize];
        var received = new List<byte>(ReadChunkSize);
        var headerEnd = -1;

        // 读头（按**字节**找 \r\n\r\n）
        while (headerEnd < 0)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (n <= 0)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                received.Add(buffer[i]);
            }

            headerEnd = IndexOfHeaderEnd(received);
            if (headerEnd < 0 && received.Count > MaxHeaderBytes)
            {
                throw new InvalidOperationException("请求头超过 " + MaxHeaderBytes + " 字节上限");
            }
        }

        var contentLength = 0;
        if (headerEnd >= 0)
        {
            var headerText = Encoding.UTF8.GetString(received.ToArray(), 0, headerEnd);
            foreach (var line in headerText.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var parsed)
                    && parsed > 0)
                {
                    contentLength = parsed;
                }
            }
        }

        if (contentLength > MaxRequestBytes)
        {
            throw new InvalidOperationException("Content-Length " + contentLength + " 超过 " + MaxRequestBytes + " 字节上限");
        }

        var bodyStart = headerEnd < 0 ? received.Count : headerEnd + 4;
        var haveBody = Math.Max(0, received.Count - bodyStart);

        // 补读剩余 body（**按字节计数**）
        while (haveBody < contentLength)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (n <= 0)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                received.Add(buffer[i]);
            }

            haveBody = received.Count - bodyStart;
            if (received.Count > MaxRequestBytes + MaxHeaderBytes)
            {
                throw new InvalidOperationException("请求体超过上限");
            }
        }

        var bodyLen = Math.Min(haveBody, contentLength > 0 ? contentLength : haveBody);
        var body = bodyLen <= 0
            ? string.Empty
            : Encoding.UTF8.GetString(received.ToArray(), bodyStart, bodyLen);

        var method = string.Empty;
        var path = string.Empty;
        if (headerEnd >= 0)
        {
            var headerText = Encoding.UTF8.GetString(received.ToArray(), 0, headerEnd);
            var firstLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
            var parts = firstLine.Split(' ');
            if (parts.Length >= 2)
            {
                method = parts[0];
                // 请求行 = `path[?query] [HTTP/x.y]` ⇒ 只取 `?` 之前的路径（带 query 的合法请求不该被判 404）
                path = SplitPath(parts[1]);
            }
        }

        return (method, path, body);
    }

    /// <summary>`/callback?x=1` → `/callback`（`?` 缺失时原样返回）。</summary>
    private static string SplitPath(string target)
    {
        if (string.IsNullOrEmpty(target))
        {
            return string.Empty;
        }

        var q = target.IndexOf('?');
        return q < 0 ? target : target.Substring(0, q);
    }

    /// <summary>字节级查找 <c>\r\n\r\n</c>（返回首字节下标；找不到返回 -1）。</summary>
    private static int IndexOfHeaderEnd(List<byte> bytes)
    {
        var limit = bytes.Count - 3;
        for (var i = 0; i < limit; i++)
        {
            if (bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n'
                && bytes[i + 2] == (byte)'\r' && bytes[i + 3] == (byte)'\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string body, CancellationToken ct)
    {
        var reason = status switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            500 => "Internal Server Error",
            _ => "OK",
        };

        var bytes = body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
        var head = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n")
            .Append("Content-Type: application/json; charset=utf-8\r\n")
            .Append("Content-Length: ").Append(bytes.Length).Append("\r\n")
            .Append("Connection: close\r\n\r\n")
            .ToString();

        var headBytes = Encoding.ASCII.GetBytes(head);
        await stream.WriteAsync(headBytes.AsMemory(0, headBytes.Length), ct).ConfigureAwait(false);
        if (bytes.Length > 0)
        {
            await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        }

        await stream.FlushAsync(ct).ConfigureAwait(false);
    }
}

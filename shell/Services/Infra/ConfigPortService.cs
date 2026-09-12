// S5 体验设施（t33）：固定端口 —— 让回调端口**跨启动稳定**，否则每次换端口会让内核手里的
//   callbackUrl 失效（内核只在启动参数里拿到一次）。
// 语义：① 首次 ⇒ 选一个空闲端口并落盘；② 之后 ⇒ 复用同一端口；
//      ③ 端口被别的进程占用 ⇒ 重新选一个并**更新落盘值**（并记日志说明为什么变）。
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIPlayer.Shell.Services.Infra;

public sealed class ConfigPortService
{
    public const string FileName = "callback-port.json";

    private readonly AppDataDir _data;
    private readonly Action<string> _log;

    public ConfigPortService(AppDataDir data, Action<string> log = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _log = log;
    }

    /// <summary>当前端口（未 <see cref="Resolve"/> 过则为 0）。</summary>
    public int Port { get; private set; }

    /// <summary>落盘文件路径（<c>&lt;数据根&gt;\callback-port.json</c>）。</summary>
    public string FilePath => _data.File(FileName);

    /// <summary>回调基址（内核 <c>callbackUrl</c> 用这个）。</summary>
    public string CallbackUrl => Port > 0 ? $"http://127.0.0.1:{Port}/" : null;

    /// <summary>
    /// 解析端口：优先复用落盘值（仍可用时），否则在 [<paramref name="min"/>, <paramref name="max"/>] 内选一个空闲端口并落盘。
    /// </summary>
    public int Resolve(int min = 49152, int max = 65535)
    {
        var saved = ReadSaved();
        if (saved > 0 && IsFree(saved))
        {
            Port = saved;
            _log?.Invoke($"CALLBACK-PORT 复用已存端口 {Port} ⇒ {CallbackUrl}");
            return Port;
        }
        if (saved > 0)
        {
            _log?.Invoke($"CALLBACK-PORT 已存端口 {saved} 不可用（被占用）⇒ 重选");
        }

        var picked = PickFreePort(min, max);
        Port = picked;
        WriteSaved(picked);
        _log?.Invoke($"CALLBACK-PORT 选定 {Port} ⇒ {CallbackUrl}");
        return Port;
    }

    /// <summary>端口此刻是否空闲（真正的占用判定：能 bind 才算空闲）。</summary>
    public static bool IsFree(int port)
    {
        if (port <= 0 || port > 65535) return false;
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int PickFreePort(int min, int max)
    {
        for (var i = 0; i < 64; i++)
        {
            var candidate = 0;
            // 先用系统分配的临时端口探一个（避免顺序扫描总是挑到同一个低号），再校验落在区间内且空闲。
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            candidate = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            if (candidate >= min && candidate <= max && IsFree(candidate)) return candidate;
        }
        for (var p = min; p <= max; p++)
        {
            if (IsFree(p)) return p;
        }
        throw new InvalidOperationException($"在 [{min}, {max}] 内找不到空闲端口");
    }

    private int ReadSaved()
    {
        try
        {
            if (!File.Exists(FilePath)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            return doc.RootElement.TryGetProperty("port", out var p) && p.TryGetInt32(out var v) ? v : 0;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException)
        {
            _log?.Invoke($"CALLBACK-PORT 读取落盘值失败（按未设置处理）：{ex.GetType().Name}");
            return 0;
        }
    }

    private void WriteSaved(int port)
    {
        var obj = new JsonObject
        {
            ["port"] = port,
            ["updatedAtUtc"] = DateTime.UtcNow.ToString("o"),
        };
        AtomicFile.WriteAllText(FilePath, obj.ToJsonString());   // t148：原子写（受保护状态文件不得留半截）
    }
}

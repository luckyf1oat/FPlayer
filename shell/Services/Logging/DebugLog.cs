// 等价移植：rebuild/ai_player/lib/core/services/player_log_service.dart。
// 映射依据：DESIGN §4.1 #16 `debug_log.dart` → `Services/Logging/DebugLog.cs`（同一文件的 Dart 名为 player_log_service）。
// 语义：内存环形缓冲（UI 实时查看，最新在前）+ 落盘 logs/aiplayer.log（排障）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.State;

namespace AIPlayer.Shell.Services.Logging;

public sealed class LogEntry
{
    public LogEntry(DateTime time, string message, string level = "info")
    {
        Time = time;
        Message = message;
        Level = level;
    }

    public DateTime Time { get; }

    public string Message { get; }

    /// <summary><c>info</c> / <c>warn</c> / <c>error</c>。</summary>
    public string Level { get; }

    /// <summary>等价 Dart <c>formatted</c>：<c>HH:mm:ss  message</c>。</summary>
    public string Formatted => $"{Time:HH:mm:ss}  {Message}";

    /// <summary>等价 Dart <c>plainText</c> 使用的完整 ISO 行。</summary>
    public string ToLogLine() => $"{Time:O}  {Message}";
}

/// <summary>运行日志服务（对应 Dart <c>PlayerLogService</c>）。</summary>
public sealed class PlayerLogService : ChangeNotifierBase
{
    public const int MaxEntries = 600;

    private static PlayerLogService _instance;
    private static readonly object Gate = new object();

    private readonly List<LogEntry> _entries = new List<LogEntry>();
    private readonly Action<string> _sink;

    public PlayerLogService(Action<string> fileSink = null)
    {
        _sink = fileSink ?? (message => AppDataDir.Instance.Log(message));
    }

    public static PlayerLogService Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new PlayerLogService();
            }
        }
    }

    /// <summary>测试/多实例。</summary>
    public static PlayerLogService Create(Action<string> fileSink) => new PlayerLogService(fileSink);

    /// <summary>时间倒序（最新在前），便于列表直接渲染。</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.AsEnumerable().Reverse().ToList();
            }
        }
    }

    public string PlainText
    {
        get
        {
            lock (_entries)
            {
                return string.Join("\n", _entries.Select(e => e.ToLogLine()));
            }
        }
    }

    public void Add(string message, string level = "info")
    {
        // t132：**打码下沉到唯一 sink** —— 环形缓冲与文件写盘都从这里出去，所以在入口打一次码，
        // 任何**直接** `DebugLog.Info/Warn/Error` 的调用点（含将来新增、忘了自己打码的点）都自动受益。
        // 实测背景：`KERNEL-LAUNCH … args=` 与 `PLAY-KERNEL-ARGS-RAW` 两条路径各贡献 24 行裸 base64（t89/t99）。
        // 失败面（t135 已把该分支真跑出来；t138 改的是**写出什么**）：打码器不许抛、也不许丢行 ⇒ 失败时这一行**照样写出**，
        // 但内容换成 `LOG-MASK-FAIL <异常类型> ⇒ <masked-failed len=N sha4=XXXX>`：
        //   · **保行**：行数 / 位置 / 时间 / 级别与正常路径逐一相同（"少了哪一行"依旧可判）；
        //   · **不保明文**：环形缓冲与文件**两条出口**都不再落原串（t99 的明文泄露不得在异常路径上回归）。
        // 判定语义未动：仍然是"入口打一次码、失败就换写出内容"，`Mask` 的 5 条规则与返回原串的行为一字未改。
        var masked = SecretMasking.Mask(message, out var maskError);
        if (masked == null) masked = string.Empty;
        if (maskError != null)
        {
            masked = $"{SecretMasking.MaskFailPrefix} {maskError} ⇒ {SecretMasking.DescribeFailure(message)}（本行按占位符写出：不丢行、不落明文）";
        }

        lock (_entries)
        {
            _entries.Add(new LogEntry(DateTime.Now, masked, level));
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
        }
        try
        {
            _sink(masked);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* 有意忽略：日志 sink 抛错不得影响主流程（唯一例外：取消异常不吞） */ }
        NotifyListeners();
    }

    public void Warn(string message) => Add(message, "warn");

    public void Error(string message) => Add(message, "error");

    public void Clear()
    {
        lock (_entries)
        {
            _entries.Clear();
        }
        NotifyListeners();
    }

    /// <summary>对外统一入口名（DESIGN §4.1 #16 的 C# 目标名 <c>DebugLog</c>）。</summary>
    public static void Info(string message) => Instance.Add(message);

    public static void LogWarning(string message) => Instance.Warn(message);

    public static void LogError(string message) => Instance.Error(message);
}

/// <summary>
/// DESIGN §4.1 #16 指定的 C# 门面名（<c>Services/Logging/DebugLog.cs</c>）；
/// 原 Dart 文件名为 <c>player_log_service.dart</c>，实现体即上面的 <see cref="PlayerLogService"/>。
/// </summary>
public static class DebugLog
{
    public static void Info(string message) => PlayerLogService.Instance.Add(message);

    public static void Warn(string message) => PlayerLogService.Instance.Warn(message);

    public static void Error(string message) => PlayerLogService.Instance.Error(message);

    public static string PlainText => PlayerLogService.Instance.PlainText;

    public static IReadOnlyList<LogEntry> Entries => PlayerLogService.Instance.Entries;
}

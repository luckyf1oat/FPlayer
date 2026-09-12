// 等价移植：rebuild/ai_player/lib/core/player/player_controller.dart（154 行 Dart → C#）。
// 契约依据：reversed/FlutterApp/HOST_CONTRACT.md §4.1（九类回调：progress / stopped / manual_resize /
//   navigate_previous / navigate_next / navigate_episode / preload_episode / switch_version /
//   refresh_playback_url）、§4.2（换源续播响应体）、§8/§9 实测（progress 约 0.5 s 一次、updateUrl 恒为 null）。
//
// 移植分层说明（重要，避免与外壳层重复实现）：
//   1. Dart `<c>PlayerController</c>` 的**进程/内核生命周期**部分（`MpvHostService.launch`、`exitCode`、
//      `_events`/`_exitCodes` 流、`dispose` 关闭流）属于内核宿主（外壳 App 侧的 InProcessKernelHost 或
//      进程外 spawn），**不在服务层**（服务层为纯类库，不引用内核程序集）⇒ 此处只保留**抽象**：
//      <see cref="IPlayerController"/> + 导航解析委托 <see cref="NavigationResolver"/>。
//   2. Dart `HostEvent` / `HostEventKind`（host_events.dart）是内核回调的**服务层可见契约**
//      （Emby/Music 会话都要按 kind 分派），服务层此前没有等价物 ⇒ 在此落位为
//      <see cref="HostEventKind"/> / <see cref="HostEvent"/>，字段与 Dart getter 一一对应；
//      另外提供 <see cref="HostEventExtensions"/> 的原始载荷读取帮手，等价 Dart `event.raw['x']`。

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Player;

/// <summary>播放内核状态（外壳 UI 与上报节流共同观察的状态机取值）。</summary>
public enum PlayerState
{
    /// <summary>未启动 / 已停止。</summary>
    Stopped,

    /// <summary>正在播放。</summary>
    Playing,

    /// <summary>暂停（内核 <c>progress.isPaused=true</c>）。</summary>
    Paused,

    /// <summary>起播/换源中（已发请求、内核尚未回报 progress）。</summary>
    Buffering,
}

/// <summary>内核会发给外壳的事件种类（对应 Dart <c>HostEventKind</c>，HOST_CONTRACT §4.1）。</summary>
public enum HostEventKind
{
    Progress,
    Stopped,
    ManualResize,
    NavigatePrevious,
    NavigateNext,
    NavigateEpisode,
    PreloadEpisode,
    SwitchVersion,
    RefreshPlaybackUrl,
    Unknown,
}

/// <summary>
/// 内核 → 外壳的单个回调事件（对应 Dart <c>HostEvent</c>）。
/// <see cref="Raw"/> 保留原始 JSON 载荷：上报需要的 <c>audioStreamIndex</c>/<c>subtitleStreamIndex</c>/
/// <c>volumeLevel</c>/<c>isMuted</c>/<c>playbackRate</c> 只能从原始载荷取（Dart 侧同做法）。
/// </summary>
public sealed class HostEvent
{
    public HostEvent(HostEventKind kind, Dictionary<string, object> raw = null, DateTime? receivedAt = null)
    {
        Kind = kind;
        Raw = raw ?? new Dictionary<string, object>(StringComparer.Ordinal);
        ReceivedAt = receivedAt ?? DateTime.Now;
    }

    public HostEventKind Kind { get; }

    /// <summary>原始载荷（键名为内核 JSON 字段原样，如 <c>positionSeconds</c>）。</summary>
    public Dictionary<string, object> Raw { get; }

    public DateTime ReceivedAt { get; }

    /// <summary><c>progress</c> / <c>stopped</c> / <c>navigate_*</c> 均可能带播放位置（秒）。</summary>
    public double PositionSeconds => HostEventExtensions.DoubleOf(Raw, "positionSeconds") ?? 0;

    /// <summary>仅 <c>progress</c> 带时长（秒）。</summary>
    public double? DurationSeconds => HostEventExtensions.DoubleOf(Raw, "durationSeconds");

    public bool IsPaused => Raw.TryGetValue("isPaused", out var v) && v is bool b && b;

    /// <summary><c>navigate_episode</c> / <c>preload_episode</c> 的目标集号。</summary>
    public string TargetEpisodeId => HostEventExtensions.StringOf(Raw, "targetEpisodeId");

    /// <summary><c>switch_version</c> 的目标版本序号（对应 <c>--version-option=</c> 的顺序）。</summary>
    public int? VersionIndex => HostEventExtensions.IntOf(Raw, "versionIndex");

    /// <summary>内核在发出该事件后会**读取响应体**并当作下一条播放源 ⇒ 必须回 <see cref="HostNavigateOptions"/>。</summary>
    public bool ExpectsSource => Kind is HostEventKind.NavigatePrevious
        or HostEventKind.NavigateNext
        or HostEventKind.NavigateEpisode
        or HostEventKind.PreloadEpisode
        or HostEventKind.SwitchVersion
        or HostEventKind.RefreshPlaybackUrl;

    /// <summary><c>progress</c> 的响应体同样会被采纳（可用于安静的换源）——见 HOST_CONTRACT §9 实测。</summary>
    public bool AcceptsSourceReply => ExpectsSource || Kind == HostEventKind.Progress;

    /// <summary>等价 Dart <c>HostEvent.kindOf</c>：未知/缺失一律 <see cref="HostEventKind.Unknown"/>。</summary>
    public static HostEventKind KindOf(string name) => name switch
    {
        "progress" => HostEventKind.Progress,
        "stopped" => HostEventKind.Stopped,
        "manual_resize" => HostEventKind.ManualResize,
        "navigate_previous" => HostEventKind.NavigatePrevious,
        "navigate_next" => HostEventKind.NavigateNext,
        "navigate_episode" => HostEventKind.NavigateEpisode,
        "preload_episode" => HostEventKind.PreloadEpisode,
        "switch_version" => HostEventKind.SwitchVersion,
        "refresh_playback_url" => HostEventKind.RefreshPlaybackUrl,
        _ => HostEventKind.Unknown,
    };

    /// <summary>等价 Dart <c>HostEvent.parse</c>：从内核回调 JSON 构造（<c>event</c> 字段决定 kind）。</summary>
    public static HostEvent Parse(IDictionary<string, object> json)
    {
        var raw = new Dictionary<string, object>(StringComparer.Ordinal);
        if (json != null)
        {
            foreach (var pair in json) raw[pair.Key] = pair.Value;
        }
        return new HostEvent(KindOf(HostEventExtensions.StringOf(raw, "event")), raw, DateTime.Now);
    }

    /// <summary>等价 Dart <c>toString()</c>。</summary>
    public override string ToString()
        => $"HostEvent({Kind}, pos={PositionSeconds}, paused={IsPaused}"
           + (DurationSeconds.HasValue ? $", dur={DurationSeconds.Value}" : string.Empty)
           + ")";
}

/// <summary>原始载荷读取帮手（等价 Dart 的 <c>(raw['x'] as num?)?.toDouble()</c> 系列，含 JSON 数值的宽松转换）。</summary>
public static class HostEventExtensions
{
    public static double? DoubleOf(IDictionary<string, object> raw, string key)
    {
        if (raw == null || !raw.TryGetValue(key, out var value) || value == null) return null;
        return value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            string text => double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null,
            _ => null,
        };
    }

    public static int? IntOf(IDictionary<string, object> raw, string key)
    {
        var value = DoubleOf(raw, key);
        if (value.HasValue) return (int)value.Value;
        if (raw != null && raw.TryGetValue(key, out var direct) && direct is string text
            && int.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    public static bool BoolOf(IDictionary<string, object> raw, string key)
        => raw != null && raw.TryGetValue(key, out var value) && value is bool b && b;

    public static string StringOf(IDictionary<string, object> raw, string key)
    {
        if (raw == null || !raw.TryGetValue(key, out var value) || value == null) return null;
        return value as string ?? value.ToString();
    }
}

/// <summary>
/// 内核请求导航时，上层据此给出「下一条源」；返回 <c>null</c> 表示保持现状不换源
/// （对应 Dart <c>typedef NavigationResolver</c>）。
/// </summary>
public delegate Task<PlaybackRequest> NavigationResolver(HostEvent hostEvent, CancellationToken cancellationToken);

/// <summary>
/// 播放内核控制器抽象（对应 Dart <c>PlayerController</c> 的**能力面**）。
/// </summary>
/// <remarks>
/// 落地分工（HOST_CONTRACT）：
///   1. 构造 <see cref="PlaybackRequest"/>；
///   2. <c>await controller.StartAsync(request, …)</c>；
///   3. 需要时在 <see cref="ResolveNavigationAsync"/>（或 <see cref="OnNavigation"/> 委托）里返回下一条源。
/// 真实实现 = 外壳侧内核宿主（进程内/进程外）；测试 = 假实现（记录调用、回放事件）。
/// </remarks>
public interface IPlayerController
{
    /// <summary>当前状态（未启动 = <see cref="PlayerState.Stopped"/>）。</summary>
    PlayerState State { get; }

    /// <summary>当前生效的播放请求（换源时会更新）。</summary>
    PlaybackRequest CurrentRequest { get; }

    /// <summary>内核是否在运行。</summary>
    bool IsRunning { get; }

    /// <summary>内核进程号；进程内宿主或未启动时为 <c>null</c>。</summary>
    int? HostPid { get; }

    /// <summary>最近一次回调位置（秒）。</summary>
    double PositionSeconds { get; }

    /// <summary>当前媒体时长（秒；未知为 <c>null</c>）。</summary>
    double? DurationSeconds { get; }

    /// <summary>音量（内核语义 0–<c>MaxVolume</c>）。</summary>
    int VolumeLevel { get; }

    bool IsMuted { get; }

    /// <summary>播放速率（1.0 = 常速）。</summary>
    double PlaybackRate { get; }

    /// <summary>每个内核回调都会触发（UI 展示 + 服务器上报，等价 Dart <c>PlayerController.onEvent</c>）。</summary>
    Action<HostEvent> OnEvent { get; set; }

    /// <summary>导航事件解析器（连播/换版/刷新地址，等价 Dart <c>PlayerController.onNavigation</c>）。</summary>
    NavigationResolver OnNavigation { get; set; }

    /// <summary>启动内核播放（等价 Dart <c>start</c>；实现侧应先停止上一次）。</summary>
    Task StartAsync(PlaybackRequest request, string workingDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>停止内核并释放回调端口（等价 Dart <c>stop</c>）。</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>暂停（内核暂停 ⇒ 下一次 <c>progress</c> 的 <c>isPaused=true</c>）。</summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>继续播放。</summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>跳转到指定秒。</summary>
    Task SeekAsync(double positionSeconds, CancellationToken cancellationToken = default);

    /// <summary>设置音量（0–100）。</summary>
    Task SetVolumeAsync(int level, CancellationToken cancellationToken = default);

    /// <summary>设置静音。</summary>
    Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default);

    /// <summary>设置播放速率。</summary>
    Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 内核请求「下一条源」时的应答（HOST_CONTRACT §4.2，等价 Dart <c>PlayerController._resolveSource</c>）。
    /// 返回 <c>null</c> ⇒ 本轮不换源。
    /// </summary>
    Task<HostNavigateOptions> ResolveNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken = default);
}

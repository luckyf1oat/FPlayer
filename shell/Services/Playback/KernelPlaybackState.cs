// E-P2（t39）：内核回推的**播放状态模型** —— 「单一真相源 = mpv」在外壳侧的载体。
//
// 字段来源（实证，非猜测）：
//   kernel/src/WinUISample.Services/PlaybackReportClient.cs:51-64  ← `progress` 回调体的既有字段
//     updateUrl / positionSeconds / isPaused / durationSeconds / audioStreamIndex / subtitleStreamIndex
//     / subtitleOffset / volumeLevel / isMuted / playbackRate
//   kernel/src/WinUISample.ViewModels/PlayerViewModel.cs:5835-5857 ← 各字段的**取值口径**
//     playbackRate ← mpv `speed`；subtitleOffset ← mpv `sub-delay`；volumeLevel ← mpv `volume`
//     audioStreamIndex/subtitleStreamIndex ← **Emby 流序号**（浮层选中轨的 EmbyStreamIndex）⚠️ 与 mpv `aid`/`sid` 不是一回事
//   t39 新增字段（K3 补口）：`aid` / `sid` ← mpv `aid` / `sid` 真实 track id（原实现只把它们写进了日志 [K3-STATE]）
//
// 纪律（t39 卡面 §3）：**只回显、不缓存、不回写** —— 本类只做「一段回调体 → 一个状态快照」，不持有历史、不产生命令。
// 解析器复用 Util/JsonRead（宽松、永不抛异常；服务端类型漂移不炸）。

using System;
using System.Text;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Playback;

/// <summary>内核回推的播放状态（一次回调体的解析结果）。字段为 null = 该回调体里没有这个字段（**不等于 0**）。</summary>
public sealed class KernelPlaybackState
{
    /// <summary>内核的一次性控制端点（`http://127.0.0.1:{port}/segments`）；缺省 = 本回调体未携带。</summary>
    public string UpdateUrl { get; set; } = string.Empty;

    /// <summary>回调事件名：`progress` / `stopped`。</summary>
    public string EventName { get; set; } = string.Empty;

    public double? PositionSeconds { get; set; }

    public bool? IsPaused { get; set; }

    public double? DurationSeconds { get; set; }

    /// <summary>倍速。来源 = `playbackRate`（内核取 mpv `speed`）。</summary>
    public double? Speed { get; set; }

    /// <summary>字幕延迟（秒）。来源 = `subtitleOffset`（内核读 mpv `sub-delay`）。</summary>
    public double? SubDelay { get; set; }

    /// <summary>音量。来源 = `volumeLevel`（内核优先取 mpv `volume`）。</summary>
    public int? Volume { get; set; }

    public bool? IsMuted { get; set; }

    /// <summary>mpv 音轨 id。来源 = t39 新增字段 `aid`；-1 = mpv 报「无」。</summary>
    public long? Aid { get; set; }

    /// <summary>mpv 字幕轨 id。来源 = t39 新增字段 `sid`；-1 = 无字幕轨。</summary>
    public long? Sid { get; set; }

    /// <summary>Emby 音轨**流序号**（既有字段 `audioStreamIndex`，与 <see cref="Aid"/> 不同源）。</summary>
    public int? EmbyAudioStreamIndex { get; set; }

    /// <summary>Emby 字幕**流序号**（既有字段 `subtitleStreamIndex`）。</summary>
    public int? EmbySubtitleStreamIndex { get; set; }

    public bool HasUpdateUrl => !string.IsNullOrWhiteSpace(UpdateUrl);

    /// <summary>是否有 mpv 侧状态（用于判断「这次回推带没带真实状态」）。</summary>
    public bool HasMpvState => Aid.HasValue || Sid.HasValue || Speed.HasValue || SubDelay.HasValue || Volume.HasValue;

    /// <summary>解析一段回调体。<paramref name="json"/> 为空/非法时返回 null（调用方据此走"未收到"分支，不得假装收到）。</summary>
    public static KernelPlaybackState FromBody(string json)
    {
        var root = JsonRead.FromNode(json);
        if (root == null || root.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }
        return FromElement(root.Value);
    }

    public static KernelPlaybackState FromElement(System.Text.Json.JsonElement root)
    {
        var state = new KernelPlaybackState
        {
            EventName = JsonRead.Str(root, "event"),
            UpdateUrl = JsonRead.Str(root, "updateUrl"),
            PositionSeconds = JsonRead.DoubleOrNull(root, "positionSeconds"),
            IsPaused = JsonRead.Prop(root, "isPaused") == null ? (bool?)null : JsonRead.Bool(root, "isPaused"),
            DurationSeconds = JsonRead.DoubleOrNull(root, "durationSeconds"),
            Speed = JsonRead.DoubleOrNull(root, "playbackRate"),
            SubDelay = JsonRead.DoubleOrNull(root, "subtitleOffset"),
            Volume = JsonRead.IntOrNull(root, "volumeLevel"),
            IsMuted = JsonRead.Prop(root, "isMuted") == null ? (bool?)null : JsonRead.Bool(root, "isMuted"),
            Aid = JsonRead.LongOrNull(root, "aid"),
            Sid = JsonRead.LongOrNull(root, "sid"),
            EmbyAudioStreamIndex = JsonRead.IntOrNull(root, "audioStreamIndex"),
            EmbySubtitleStreamIndex = JsonRead.IntOrNull(root, "subtitleStreamIndex"),
        };
        return state;
    }

    /// <summary>与另一份状态比较，返回**发生变化的**『字段=新值』列表（用于 UI 只更新变动项 / 只打印变化证据）。</summary>
    public System.Collections.Generic.List<string> Changes(KernelPlaybackState previous)
    {
        var changes = new System.Collections.Generic.List<string>();
        if (previous == null)
        {
            return changes;
        }
        if (Aid != previous.Aid) changes.Add($"aid={Aid?.ToString() ?? "-"}");
        if (Sid != previous.Sid) changes.Add($"sid={Sid?.ToString() ?? "-"}");
        if (Speed != previous.Speed) changes.Add($"speed={Speed?.ToString() ?? "-"}");
        if (SubDelay != previous.SubDelay) changes.Add($"subDelay={SubDelay?.ToString() ?? "-"}");
        if (Volume != previous.Volume) changes.Add($"volume={Volume?.ToString() ?? "-"}");
        if (IsPaused != previous.IsPaused) changes.Add($"paused={IsPaused?.ToString() ?? "-"}");
        return changes;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("event=").Append(string.IsNullOrEmpty(EventName) ? "-" : EventName);
        sb.Append(" mpv: aid=").Append(Aid?.ToString() ?? "-");
        sb.Append(" sid=").Append(Sid?.ToString() ?? "-");
        sb.Append(" speed=").Append(Speed?.ToString() ?? "-");
        sb.Append(" subDelay=").Append(SubDelay?.ToString() ?? "-");
        sb.Append(" volume=").Append(Volume?.ToString() ?? "-");
        sb.Append(" | emby: audio=").Append(EmbyAudioStreamIndex?.ToString() ?? "-");
        sb.Append(" sub=").Append(EmbySubtitleStreamIndex?.ToString() ?? "-");
        sb.Append(" | updateUrl=").Append(HasUpdateUrl ? UpdateUrl : "(未携带)");
        return sb.ToString();
    }
}

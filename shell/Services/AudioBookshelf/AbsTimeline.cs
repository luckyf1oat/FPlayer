// 等价移植：rebuild/ai_player/lib/core/models/abs_models.dart 中的 `AbsTimeline` / `AbsLocation`
// （ABS 侧不存在独立的 abs_timeline.dart 源文件；SERVICE_API.md §3 只在文字里以 `abs_timeline.dart`
//  指代这套「多音轨 ↔ 全局进度」换算语义 —— 故此处把该语义**单独成类**，逐行对应 Dart `AbsTimeline`）。
//
// 端点/单位依据：reversed/FlutterApp/SERVICE_API.md §3 Audiobookshelf（`[S][E]`）：
//   - 书籍媒体结构是 `media.audioFiles[]`（多音轨），播放内核一次只播一个文件 ⇒ 必须互转；
//   - `currentTime` / `duration` 单位是**秒**（与内核一致，无 ticks 换算）。
//
// 语义要点（与 Dart 逐行一致）：
//   - `offsetOf(i)`：优先用服务端给的第 i 轨 `startOffset`（>0 才算有效），否则把前 i 轨 duration 累加推算；
//   - `totalDuration`：末轨 `startOffset > 0` 时用 `末轨 endOffset`，否则用 `offsetOf(末轨) + 末轨 duration`；
//   - `locate(g)`：先 clamp 到 [0, total]，逐轨比较 `target < 该轨结束 - 0.001`，命中则轨内秒数再 clamp 到 [0, 该轨时长]；
//     全部落空（或已是末轨）落到末轨；
//   - `toGlobal(i, s)`：`offsetOf(i) + s`（**不**做 clamp，与 Dart 一致）。
// 安全回落：轨道表为空 / 轨时长为 0 / 越界索引 / NaN 输入时不得抛异常，退化为「零偏移 + clamp 后的秒数」。

using System;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.AudioBookshelf;

/// <summary>多轨 ↔ 全书秒数换算（对应 Dart <c>AbsTimeline</c>）。</summary>
public sealed class AbsTimeline
{
    public AbsTimeline(IReadOnlyList<AbsAudioTrack> tracks)
    {
        Tracks = tracks ?? Array.Empty<AbsAudioTrack>();
    }

    /// <summary>音轨表（数组下标即轨序号）。</summary>
    public IReadOnlyList<AbsAudioTrack> Tracks { get; }

    /// <summary>无音轨（对应 Dart <c>isEmpty</c>）——调用方应先判空再播放。</summary>
    public bool IsEmpty => Tracks.Count == 0;

    /// <summary>全书总时长（秒）。</summary>
    public double TotalDuration => TotalDurationOf(Tracks);

    /// <summary>第 <paramref name="index"/> 轨在全书的起始偏移（秒）。</summary>
    public double OffsetOf(int index) => OffsetOf(Tracks, index);

    /// <summary>全局秒数 → （轨序号, 轨内秒数）。</summary>
    public AbsLocation Locate(double globalSeconds) => Locate(Tracks, globalSeconds);

    /// <summary>（轨序号, 轨内秒数）→ 全局秒数。</summary>
    public double GlobalSecondsFromTrack(int trackIndex, double trackSeconds)
        => GlobalSecondsFromTrack(Tracks, trackIndex, trackSeconds);

    /// <summary>（轨序号, 轨内秒数）→ 全局秒数（Dart 侧名 <c>toGlobal</c>，此处保留同义别名）。</summary>
    public double ToGlobal(int trackIndex, double secondsWithinTrack)
        => GlobalSecondsFromTrack(Tracks, trackIndex, secondsWithinTrack);

    /// <summary>按轨序号取轨；越界或空表返回 <c>null</c>（对应 Dart <c>trackAt</c>）。</summary>
    public AbsAudioTrack TrackAt(int index) => TrackAt(Tracks, index);

    // ── 静态形态（无状态，便于服务层与视图层直接调用）─────────────────────────

    /// <summary>音轨表为空（对应 Dart <c>isEmpty</c>）。</summary>
    public static bool IsEmptyTracks(IReadOnlyList<AbsAudioTrack> tracks) => tracks == null || tracks.Count == 0;

    /// <summary>全书总时长（秒）：Dart <c>totalDuration</c> 的逐行等价（静态形态，名字避开实例属性 <see cref="TotalDuration"/>）。</summary>
    public static double TotalDurationOf(IReadOnlyList<AbsAudioTrack> tracks)
    {
        var count = tracks?.Count ?? 0;
        if (count == 0) return 0;
        var last = tracks[count - 1];
        return last.StartOffset > 0
            ? last.EndOffset
            : OffsetOf(tracks, count - 1) + last.Duration;
    }

    /// <summary>
    /// 第 <paramref name="index"/> 轨在全书的起始偏移（秒）。
    /// 优先服务端 <c>startOffset</c>（&gt;0 才算有效），否则累加前序轨时长推算；越界索引被夹到有效范围（Dart <c>clamp</c>）。
    /// </summary>
    public static double OffsetOf(IReadOnlyList<AbsAudioTrack> tracks, int index)
    {
        if (index <= 0) return 0;
        var count = tracks?.Count ?? 0;
        if (count == 0) return 0;

        var bounded = index >= count ? count - 1 : index;
        if (tracks[bounded].StartOffset > 0) return tracks[bounded].StartOffset;

        var sum = 0d;
        for (var i = 0; i < bounded; i++)
        {
            sum += tracks[i].Duration;
        }
        return sum;
    }

    /// <summary>
    /// 全局秒数 → （轨序号, 轨内秒数）。
    /// 空音轨表时退化为 <c>(0, max(0, globalSeconds))</c>；NaN 输入按 0 处理（防 clamp 失效）。
    /// </summary>
    public static AbsLocation Locate(IReadOnlyList<AbsAudioTrack> tracks, double globalSeconds)
    {
        if (double.IsNaN(globalSeconds)) globalSeconds = 0;

        var count = tracks?.Count ?? 0;
        if (count == 0)
        {
            return new AbsLocation(0, globalSeconds < 0 ? 0 : globalSeconds);
        }

        var total = TotalDurationOf(tracks);
        var target = Clamp(globalSeconds, 0, total);

        for (var i = 0; i < count; i++)
        {
            var offset = OffsetOf(tracks, i);
            var end = offset + tracks[i].Duration;
            if (target < end - 0.001 || i == count - 1)
            {
                var within = Clamp(target - offset, 0, tracks[i].Duration);
                return new AbsLocation(i, within);
            }
        }
        return new AbsLocation(count - 1, 0);
    }

    /// <summary>
    /// 静态形态的全局秒数 → （轨序号, 轨内秒数）。语义与 <see cref="Locate(IReadOnlyList{AbsAudioTrack}, double)"/> 完全一致，
    /// 只把结果拆成元组，便于不想引入 <see cref="AbsLocation"/> 的调用方（任务书要求的 <c>LocateGlobalPosition</c> 形态）。
    /// </summary>
    public static (int TrackIndex, double TrackSeconds) LocateGlobalPosition(IReadOnlyList<AbsAudioTrack> tracks, double globalSeconds)
    {
        var location = Locate(tracks, globalSeconds);
        return (location.TrackIndex, location.SecondsWithinTrack);
    }

    /// <summary>实例形态的元组包装（同 <see cref="LocateGlobalPosition(IReadOnlyList{AbsAudioTrack}, double)"/>）。</summary>
    public (int TrackIndex, double TrackSeconds) LocateGlobalPosition(double globalSeconds)
        => LocateGlobalPosition(Tracks, globalSeconds);

    /// <summary>（轨序号, 轨内秒数）→ 全局秒数（元组解构友好形态）。</summary>
    public static double GlobalSecondsFromTrack(IReadOnlyList<AbsAudioTrack> tracks, (int TrackIndex, double TrackSeconds) position)
        => GlobalSecondsFromTrack(tracks, position.TrackIndex, position.TrackSeconds);

    /// <summary>（轨序号, 轨内秒数）→ 全局秒数：<c>offsetOf(trackIndex) + trackSeconds</c>（不 clamp，与 Dart 一致）。</summary>
    public static double GlobalSecondsFromTrack(IReadOnlyList<AbsAudioTrack> tracks, int trackIndex, double trackSeconds)
    {
        if (double.IsNaN(trackSeconds)) trackSeconds = 0;
        return OffsetOf(tracks, trackIndex) + trackSeconds;
    }

    /// <summary>按轨序号取轨；越界或空表返回 <c>null</c>。</summary>
    public static AbsAudioTrack TrackAt(IReadOnlyList<AbsAudioTrack> tracks, int index)
        => tracks != null && index >= 0 && index < tracks.Count ? tracks[index] : null;

    /// <summary>
    /// 服务端给了 <c>audioFiles[].index</c> 时按它找数组下标（ABS 存在 index 从 1 起的版本 ⇒ 轨定位不要用它）。
    /// 找不到返回 -1（未实证字段，容错解析）。
    /// </summary>
    public static int TrackIndexOfServerIndex(IReadOnlyList<AbsAudioTrack> tracks, int serverIndex)
    {
        if (tracks == null) return -1;
        for (var i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Index == serverIndex) return i;
        }
        return -1;
    }

    /// <summary>Dart <c>num.clamp</c> 等价（含 NaN 兜底：NaN 时返回 <paramref name="min"/>）。</summary>
    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value)) return min;
        if (max < min) return min;
        if (value < min) return min;
        return value > max ? max : value;
    }
}

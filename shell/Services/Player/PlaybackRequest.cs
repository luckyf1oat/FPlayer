// 等价移植：rebuild/ai_player/lib/core/player/playback_request.dart（215 行 Dart → C#，逐成员翻译）。
// 契约依据：reversed/FlutterApp/HOST_CONTRACT.md §3（`--open=` 与复合参数 schema）、§4.2（换源响应的
//   `HostNavigateOptions`：内核收到后**原地换源续播**，`startPosition` 决定续播点）。
//
// 移植说明（结构差异，逐条列明，未擅自改语义）：
//   1. Dart `toHostOptions()` 需要 `HostLaunchOptions`（内核启动参数集）。**服务层当前没有该类型**
//      （仅在 shell/Spike/Probe.cs 里以字符串反射探测过 `WinUISample.Models.HostLaunchOptions`），
//      且本次任务限定只创建四个文件 ⇒ 该方法**未移植**，不在此处发明新的启动参数类型。
//      四个内核启动形态字段（settings 驱动的 `--disable-skip-markers`/`--shortcuts=`/代理/弹幕/最大音量…）
//      属于「内核启动参数组装」，应随 `HostLaunchOptions` 一起由外壳层落地。
//   2. Dart `segments` 的类型是 `HostSegmentOption`（`{label,startMs,endMs,emby*}`）。服务层已有
//      **等价物** `MediaSegmentDto`（Services/Models/MediaSegmentDto.cs，字段 `Type/StartMs/EndMs/Source`，
//      自述「形态与内核 `--segment=` 一致」）⇒ 此处用 `MediaSegmentDto`，不新造 HostSegmentOption。
//   3. Dart `fitModeOf` 返回 `HostVideoFitMode`；服务层已有同枚举（Services/Models/HostContractModels.cs）
//      ⇒ 保留为 `FitModeOf`。
//   4. `HostNavigateOptions` 只被本文件的 `ToNavigateOptions()` 产出 ⇒ 与 PlaybackRequest 同文件落位
//      （Dart 侧同在 `host_events.dart`，C# 侧本工程把播放请求契约集中在 Player 命名空间）。

using System;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Player;

/// <summary>播放编排：把「一次播放」需要的一切装进本对象（对应 Dart <c>PlaybackRequest</c>）。</summary>
/// <remarks>
/// 设计要点（对齐 HOST_CONTRACT）：外壳负责**取流与签发 URL**，内核只接收 <c>--open=</c> 或换源响应里的地址；
/// 轨道/版本索引需与内核 UI 对齐 ⇒ <see cref="HostTrackOption.EmbyIndex"/> / <see cref="HostVersionOption.Index"/> 必须填对。
/// </remarks>
public sealed class PlaybackRequest
{
    /// <summary>交给内核 <c>--open=</c> 的播放地址（Emby 静态直连、Navidrome 流、本地文件…）。</summary>
    public string MediaPath { get; set; } = string.Empty;

    /// <summary>内核取流需要的请求头（如 Emby 的 <c>X-Emby-Token</c>）→ <c>--http-header=</c>。</summary>
    public Dictionary<string, string> HttpHeaders { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    /// <summary>起始播放位置（秒）—— 续播/换版/刷新地址都会带它。</summary>
    public double StartPosition { get; set; }

    /// <summary>海报缺图时的首字母占位（<c>--monogram=</c>）。</summary>
    public string Monogram { get; set; } = string.Empty;

    public string Logo { get; set; } = string.Empty;

    public string BackdropUrl { get; set; } = string.Empty;

    /// <summary>角标（4K / HDR / 杜比…）。</summary>
    public List<string> Badges { get; set; } = new List<string>();

    /// <summary>版本（清晰度）选项的顺序**即**内核 <c>switch_version</c> 回调里的 <c>versionIndex</c>。</summary>
    public List<HostVersionOption> VersionOptions { get; set; } = new List<HostVersionOption>();

    public List<HostTrackOption> AudioTracks { get; set; } = new List<HostTrackOption>();

    public List<HostTrackOption> SubtitleTracks { get; set; } = new List<HostTrackOption>();

    /// <summary>跳过片段（形态与内核 <c>--segment=</c> 一致，见文件头说明 2）。</summary>
    public List<MediaSegmentDto> Segments { get; set; } = new List<MediaSegmentDto>();

    /// <summary>章节（<c>--chapter=</c>）。</summary>
    public List<HostChapter> Chapters { get; set; } = new List<HostChapter>();

    /// <summary>进度条缩略图（<c>--sprite=</c>）。</summary>
    public HostSprite Sprite { get; set; }

    /// <summary>内核 <c>--episode-list=</c>：所在季全部集（支持内核侧选集/连播）。</summary>
    public List<HostEpisodeItem> EpisodeList { get; set; } = new List<HostEpisodeItem>();

    public string SubtitleId { get; set; } = string.Empty;

    /// <summary>首选外挂字幕地址（<c>--subtitle-url=</c>）。</summary>
    public string SubtitleUrl { get; set; } = string.Empty;

    /// <summary>弹幕匹配名（<c>--danmaku-match-name=</c>，外壳需 URL 编码后传）。</summary>
    public string DanmakuMatchName { get; set; }

    public string PreviousEpisodeId { get; set; }

    public string NextEpisodeId { get; set; }

    public string SeasonId { get; set; }

    /// <summary>等价 Dart <c>fitModeOf</c>：未知值一律回落 <c>contain</c>。</summary>
    public static HostVideoFitMode FitModeOf(string value) => value switch
    {
        "cover" => HostVideoFitMode.Cover,
        "stretch" => HostVideoFitMode.Stretch,
        _ => HostVideoFitMode.Contain,
    };

    /// <summary>
    /// 等价 Dart <c>toNavigateOptions()</c>：内核换源时回给它的结构（HOST_CONTRACT §4.2）。
    /// <paramref name="resumePosition"/> 为空时沿用 <see cref="StartPosition"/>。
    /// </summary>
    public HostNavigateOptions ToNavigateOptions(
        string callbackUrl = null,
        string newItemId = null,
        string currentEpisodeId = null,
        double? resumePosition = null)
        => new HostNavigateOptions
        {
            MediaPath = MediaPath,
            HttpHeaders = new Dictionary<string, string>(HttpHeaders, StringComparer.Ordinal),
            NewItemId = newItemId,
            Title = string.IsNullOrEmpty(Title) ? null : Title,
            Subtitle = string.IsNullOrEmpty(Subtitle) ? null : Subtitle,
            Monogram = string.IsNullOrEmpty(Monogram) ? null : Monogram,
            Logo = string.IsNullOrEmpty(Logo) ? null : Logo,
            BackdropUrl = string.IsNullOrEmpty(BackdropUrl) ? null : BackdropUrl,
            Badges = new List<string>(Badges),
            VersionOptions = new List<HostVersionOption>(VersionOptions),
            SubtitleId = string.IsNullOrEmpty(SubtitleId) ? null : SubtitleId,
            SubtitleUrl = string.IsNullOrEmpty(SubtitleUrl) ? null : SubtitleUrl,
            CallbackUrl = callbackUrl,
            PreviousEpisodeId = PreviousEpisodeId,
            NextEpisodeId = NextEpisodeId,
            SeasonId = SeasonId,
            StartPosition = resumePosition ?? StartPosition,
            DanmakuMatchName = DanmakuMatchName,
            CurrentEpisodeId = currentEpisodeId,
            AudioTracks = new List<HostTrackOption>(AudioTracks),
            SubtitleTracks = new List<HostTrackOption>(SubtitleTracks),
            Segments = new List<MediaSegmentDto>(Segments),
        };

    /// <summary>
    /// 等价 Dart <c>copyWith()</c>：**未传（null）即沿用原值**；可空字段（<c>sprite</c>/上一集/下一集）
    /// 需显式传 <paramref name="clearSprite"/> 等布尔开关才能清空（与 Dart 同名参数一一对应）。
    /// </summary>
    public PlaybackRequest With(
        string mediaPath = null,
        Dictionary<string, string> httpHeaders = null,
        string title = null,
        string subtitle = null,
        double? startPosition = null,
        string monogram = null,
        string logo = null,
        string backdropUrl = null,
        List<string> badges = null,
        List<HostVersionOption> versionOptions = null,
        List<HostTrackOption> audioTracks = null,
        List<HostTrackOption> subtitleTracks = null,
        List<MediaSegmentDto> segments = null,
        List<HostChapter> chapters = null,
        HostSprite sprite = null,
        bool clearSprite = false,
        List<HostEpisodeItem> episodeList = null,
        string subtitleId = null,
        string subtitleUrl = null,
        string danmakuMatchName = null,
        string previousEpisodeId = null,
        bool clearPreviousEpisodeId = false,
        string nextEpisodeId = null,
        bool clearNextEpisodeId = false,
        string seasonId = null)
        => new PlaybackRequest
        {
            MediaPath = mediaPath ?? MediaPath,
            HttpHeaders = httpHeaders ?? HttpHeaders,
            Title = title ?? Title,
            Subtitle = subtitle ?? Subtitle,
            StartPosition = startPosition ?? StartPosition,
            Monogram = monogram ?? Monogram,
            Logo = logo ?? Logo,
            BackdropUrl = backdropUrl ?? BackdropUrl,
            Badges = badges ?? Badges,
            VersionOptions = versionOptions ?? VersionOptions,
            AudioTracks = audioTracks ?? AudioTracks,
            SubtitleTracks = subtitleTracks ?? SubtitleTracks,
            Segments = segments ?? Segments,
            Chapters = chapters ?? Chapters,
            Sprite = clearSprite ? null : (sprite ?? Sprite),
            EpisodeList = episodeList ?? EpisodeList,
            SubtitleId = subtitleId ?? SubtitleId,
            SubtitleUrl = subtitleUrl ?? SubtitleUrl,
            DanmakuMatchName = danmakuMatchName ?? DanmakuMatchName,
            PreviousEpisodeId = clearPreviousEpisodeId ? null : (previousEpisodeId ?? PreviousEpisodeId),
            NextEpisodeId = clearNextEpisodeId ? null : (nextEpisodeId ?? NextEpisodeId),
            SeasonId = seasonId ?? SeasonId,
        };

    /// <summary>当前选中的音轨 <c>embyIndex</c>；没有选中项返回 <c>-1</c>（上报用）。</summary>
    public int SelectedAudioIndex()
    {
        foreach (var track in AudioTracks)
        {
            if (track.Selected) return track.EmbyIndex;
        }
        return -1;
    }

    /// <summary>当前选中的字幕轨 <c>embyIndex</c>；没有选中项返回 <c>-1</c>（上报用）。</summary>
    public int SelectedSubtitleIndex()
    {
        foreach (var track in SubtitleTracks)
        {
            if (track.Selected) return track.EmbyIndex;
        }
        return -1;
    }
}

/// <summary>
/// 外壳回给内核的「下一条播放源」（对应 Dart <c>HostNavigateOptions</c> / 内核侧同名类型）。
/// 内核收到后**原地换源续播**：<see cref="StartPosition"/> 决定续播点，
/// <see cref="MediaPath"/> + <see cref="HttpHeaders"/> 决定新流，其余字段刷新 UI（标题/角标/轨道/跳过片段…）。
/// </summary>
/// <remarks>JSON 编码（字段名/可选性）依 HOST_CONTRACT §4.2；序列化由外壳层完成（服务层只产出结构）。</remarks>
public sealed class HostNavigateOptions
{
    public string MediaPath { get; set; } = string.Empty;

    public Dictionary<string, string> HttpHeaders { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string NewItemId { get; set; }

    public string Title { get; set; }

    public string Subtitle { get; set; }

    public string Monogram { get; set; }

    public string Logo { get; set; }

    public string BackdropUrl { get; set; }

    public List<string> Badges { get; set; } = new List<string>();

    public List<HostVersionOption> VersionOptions { get; set; } = new List<HostVersionOption>();

    public string SubtitleId { get; set; }

    public string SubtitleUrl { get; set; }

    public string CallbackUrl { get; set; }

    public string PreviousEpisodeId { get; set; }

    public string NextEpisodeId { get; set; }

    public string SeasonId { get; set; }

    public double? StartPosition { get; set; }

    public string DanmakuMatchName { get; set; }

    public string CurrentEpisodeId { get; set; }

    public List<HostTrackOption> AudioTracks { get; set; } = new List<HostTrackOption>();

    public List<HostTrackOption> SubtitleTracks { get; set; } = new List<HostTrackOption>();

    public List<MediaSegmentDto> Segments { get; set; } = new List<MediaSegmentDto>();
}

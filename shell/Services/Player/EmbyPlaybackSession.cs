// 等价移植：rebuild/ai_player/lib/core/player/emby_playback_session.dart（650 行 Dart → C#，逐成员翻译）。
// 契约依据：
//   - reversed/FlutterApp/SERVICE_API.md §1.4 进度上报（POST /Sessions/Playing、/Progress、/Stopped；
//     内核给**秒**、Emby 要 **ticks(1e7/s)**；字段 PositionTicks/IsPaused/VolumeLevel/PlaybackRate/
//     AudioStreamIndex/SubtitleStreamIndex）——本类只传秒，ticks 换算在 EmbyService 内统一完成
//     （`TextUtils.SecondsToTicks`，全程 long，防 3300000000 被截成 2147483647 的历史缺陷）。
//   - reversed/FlutterApp/HOST_CONTRACT.md §4.1 九类回调、§8/§9 实测（progress 约 0.5 s 一次、
//     updateUrl 恒为 null、progress 响应体被采纳可安静换源）。
//
// 端口（成员对照，Dart → C#）：
//   item/episodes/versionIndex/lastPositionSeconds → 同名属性（锁保护）        | get durationSeconds → DurationSeconds
//   get currentSource → CurrentSource                                          | streamUrl() → StreamUrl()
//   buildRequest() → BuildRequestAsync()                                       | reportStarted() → ReportStartedAsync()
//   reportProgress() → ReportProgressAsync()                                   | reportStopped() → ReportStoppedAsync()
//   toggleFavorite()/togglePlayed() → ToggleFavoriteAsync()/TogglePlayedAsync()
//   resolveNavigation() → ResolveNavigationAsync()                             | playSessionId → PlaySessionId
//   （新增能力，见下方 HideFromResumeAsync 说明）
//
// 刻意差异（逐条，未擅自改语义）：
//   1. **进度节流**：Dart 版**没有节流**（每个 progress 回调都发一次上报），0.5 s 粒度来自内核。
//      C# 版按任务要求把节流显式落在会话上：<see cref="ProgressMinInterval"/> 默认 500 ms，
//      与内核实测节奏一致；`isPaused`/位置回退/首帧等边界强制立即上报（对齐 §9 实测「暂停/播完/换源边界会即时补发」）。
//      节流只作用于**上报到 Emby 的网络调用**：本地状态（位置/暂停/速率/音量）每个回调都更新。
//   2. `traktMedia` / `traktMediaOf()` **未移植**：服务层没有 `TraktScrobbleMedia` 与 trakt_service
//      （本次任务限定四个文件；该映射属 t7 之外的 Trakt 通道）。
//   3. `HideFromResumeAsync()` 是**新增能力**：Dart 会话里没有该方法，但 `EmbyService.SetHideFromResumeAsync`
//      已实现且任务要求本阶段给出真实入口 ⇒ 实现为「隐藏/取消隐藏继续观看」并本地同步 userData 语义。
//   4. **续播点落盘**：Dart 会话**不落盘**（续播点由 Emby 服务端在 Stopped 上报后记录；本地只留
//      `lastPositionSeconds` 供换源/换版续播）。此处保持一致，仅额外保留内存中的
//      <see cref="LastPositionSeconds"/>，供外壳在需要时自行持久化。
//   5. 线程安全：Dart 单线程事件循环；C# 下 UI 线程与内核回调线程可能并发访问 ⇒ 全部状态经
//      <c>_stateGate</c> 锁保护；锁内只读写字段，**网络调用（EmbyService/上报）一律在锁外**执行。
//   6. 上报失败容错：与 Dart 一致 —— `EmbyService` 的三个上报方法内部已 try/catch 只记日志，
//      本类不因上报失败中断播放（`PlaybackFailed` 事件用于把失败告知 UI，不抛异常）。

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Segments;
using AIPlayer.Shell.Services.State;
using AIPlayer.Shell.Services.Todb;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Player;

/// <summary>
/// Emby 播放会话：Emby 数据 ↔ 播放内核 的**双向翻译**（对应 Dart <c>EmbyPlaybackSession</c>）。
/// 正向：<see cref="EmbyItem"/> → <see cref="PlaybackRequest"/> → 内核 <c>--open=</c>；
/// 反向：内核回调 → <c>POST /Sessions/Playing*</c> 上报；导航类回调 → 下一条 <see cref="PlaybackRequest"/>。
/// </summary>
public sealed class EmbyPlaybackSession : ChangeNotifierBase
{
    /// <summary>接近片尾的「已看」判定阈值（Dart 原文 0.92）。</summary>
    public const double PlayedThreshold = 0.92;

    /// <summary>进度上报最小间隔；默认 500 ms ↔ HOST_CONTRACT §8 实测「约每 0.5 s 一次」。</summary>
    public static readonly TimeSpan DefaultProgressMinInterval = TimeSpan.FromMilliseconds(500);

    private readonly object _stateGate = new object();
    private readonly Action<string> _onLog;

    private EmbyItem _item;
    private List<EmbyItem> _episodes = new List<EmbyItem>();
    private int _versionIndex;
    private double _lastPositionSeconds;

    private bool _isPaused;
    private bool _isMuted;
    private double _playbackRate = 1.0;
    private int _volumeLevel = -1;
    private int _audioStreamIndex = -1;
    private int _subtitleStreamIndex = -1;

    private long _lastProgressTimestamp;
    private bool _playedNotified;

    public EmbyPlaybackSession(
        EmbyService emby,
        AppSettings settings,
        SegmentService segmentService = null,
        TodbService todbService = null,
        Action<string> onLog = null,
        string playSessionId = null,
        TimeSpan? progressMinInterval = null)
    {
        Emby = emby ?? throw new ArgumentNullException(nameof(emby));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        SegmentService = segmentService;
        TodbService = todbService;
        _onLog = onLog;
        PlaySessionId = string.IsNullOrEmpty(playSessionId) ? NewPlaySessionId() : playSessionId;
        ProgressMinInterval = progressMinInterval ?? DefaultProgressMinInterval;
    }

    public EmbyService Emby { get; }

    /// <summary>可变设置（与 Dart 一致：字幕/跳过片段/弹幕等实时读取）。</summary>
    public AppSettings Settings { get; set; }

    /// <summary>跳过片段聚合服务（可空 —— 为空时只用 Emby 原生段）。</summary>
    public SegmentService SegmentService { get; }

    /// <summary>章节/雪碧图服务（可空）。</summary>
    public TodbService TodbService { get; }

    /// <summary>Emby 播放会话 id（<c>PlaySessionId</c>）。换源续播时保持不变。</summary>
    public string PlaySessionId { get; }

    /// <summary>进度上报节流窗口（见文件头「刻意差异 1」）。</summary>
    public TimeSpan ProgressMinInterval { get; set; }

    /// <summary>状态变化通知（位置/暂停/收藏/已看/换集等；对应 Dart 侧 UI 重建入口）。</summary>
    public Action OnChanged { get; set; }

    /// <summary>上报网络调用失败时的观察点（<c>null</c> 表示不关心）；上报失败**不会**中断播放。</summary>
    public Action<Exception> PlaybackFailed { get; set; }

    // ── 状态读取（全部经锁；Dart 侧为普通字段）─────────────────────────────

    /// <summary>当前条目（未设置时为 <c>null</c>）。</summary>
    public EmbyItem Item
    {
        get { lock (_stateGate) return _item; }
    }

    /// <summary>所在季的集列表（快照副本，外部改动不影响会话）。</summary>
    public IReadOnlyList<EmbyItem> Episodes
    {
        get { lock (_stateGate) return _episodes.ToArray(); }
    }

    /// <summary>当前媒体源序号（<c>--version-option=</c> 的顺序）。</summary>
    public int VersionIndex
    {
        get { lock (_stateGate) return _versionIndex; }
        set
        {
            var count = 0;
            lock (_stateGate)
            {
                count = _item?.MediaSources.Count ?? 0;
                _versionIndex = count == 0 ? 0 : Math.Clamp(value, 0, count - 1);
            }
            OnChanged?.Invoke();
        }
    }

    /// <summary>最近一次已知播放位置（秒）—— 用于刷新地址/换版时续播。</summary>
    public double LastPositionSeconds
    {
        get { lock (_stateGate) return _lastPositionSeconds; }
        set { lock (_stateGate) _lastPositionSeconds = value; }
    }

    /// <summary>最近一次 <c>progress</c> 的暂停状态。</summary>
    public bool IsPaused
    {
        get { lock (_stateGate) return _isPaused; }
    }

    /// <summary>最近一次 <c>progress</c> 的静音状态。</summary>
    public bool IsMuted
    {
        get { lock (_stateGate) return _isMuted; }
    }

    /// <summary>最近一次 <c>progress</c> 的播放速率（缺省 1.0）。</summary>
    public double PlaybackRate
    {
        get { lock (_stateGate) return _playbackRate; }
    }

    /// <summary>最近一次 <c>progress</c> 的音量；未知为 <c>-1</c>（Dart 侧缺省 <c>null</c>）。</summary>
    public int VolumeLevel
    {
        get { lock (_stateGate) return _volumeLevel; }
    }

    /// <summary>内核回传的当前音轨 <c>embyIndex</c>；未知为 <c>-1</c>。</summary>
    public int AudioStreamIndex
    {
        get { lock (_stateGate) return _audioStreamIndex; }
    }

    /// <summary>内核回传的当前字幕轨 <c>embyIndex</c>；未知为 <c>-1</c>。</summary>
    public int SubtitleStreamIndex
    {
        get { lock (_stateGate) return _subtitleStreamIndex; }
    }

    /// <summary>当前条目时长（秒；来自 <c>RunTimeTicks</c>，&lt;=0 时为 <c>null</c>）。</summary>
    public double? DurationSeconds
    {
        get
        {
            var ticks = Item?.RunTimeTicks ?? 0;
            return ticks > 0 ? TextUtils.TicksToSeconds(ticks) : (double?)null;
        }
    }

    /// <summary>当前选中的媒体源；无源时为 <c>null</c>。</summary>
    public EmbyMediaSource CurrentSource
    {
        get
        {
            var sources = Item?.MediaSources;
            if (sources == null || sources.Count == 0) return null;
            var index = VersionIndex;
            if (index < 0) index = 0;
            if (index >= sources.Count) index = sources.Count - 1;
            return sources[index];
        }
    }

    /// <summary>是否应走转码（直连与直放皆不可用）。</summary>
    public bool NeedsTranscode
    {
        get
        {
            var source = CurrentSource;
            if (source == null) return false;
            return !source.SupportsDirectPlay && !source.SupportsDirectStream;
        }
    }

    private void Log(string message)
    {
        _onLog?.Invoke(message);
        DebugLog.Info($"[emby-session] {message}");
    }

    /// <summary>等价 Dart <c>_newPlaySessionId</c>：<c>{epoch毫秒}-{16位随机 hex}</c>。</summary>
    public static string NewPlaySessionId()
    {
        var random = new Random();
        var part = new char[16];
        const string digits = "0123456789abcdef";
        for (var i = 0; i < part.Length; i++) part[i] = digits[random.Next(16)];
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{new string(part)}";
    }

    /// <summary>替换当前条目（含 userData 就地更新时用；等价 Dart <c>item = …</c>）。</summary>
    public void SetItem(EmbyItem item)
    {
        lock (_stateGate)
        {
            _item = item;
            _playedNotified = false;
        }
        OnChanged?.Invoke();
    }

    /// <summary>注入集列表（等价 Dart <c>episodes = …</c>）。</summary>
    public void SetEpisodes(IReadOnlyList<EmbyItem> episodes)
    {
        lock (_stateGate)
        {
            _episodes = episodes == null ? new List<EmbyItem>() : new List<EmbyItem>(episodes);
        }
        OnChanged?.Invoke();
    }

    // ── 地址 / 请求组装 ────────────────────────────────────────────────────

    /// <summary>生成当前条目的播放地址（直连优先，源不支持直连则转码）。</summary>
    public string StreamUrl(double? startSeconds = null)
    {
        var source = CurrentSource;
        var item = Item;
        if (item == null) return string.Empty;
        if (NeedsTranscode)
        {
            // 🔴 t67（超出原版的修复）：HLS 分片必须带 PlaySessionId。优先用**服务端 PlaybackInfo 顶层**下发的那个
            // （它就是转码会话的钥匙；t59 实测补上后 ServerB/ServerA 分片 200 + 真 video/mp2t 字节），
            // 服务端没给才回落外壳自己的会话 id（它已在 /Sessions/Playing 上报里用过）。
            var serverSessionId = EmbyService.ServerPlaySessionIdOf(item);
            var sessionId = string.IsNullOrWhiteSpace(serverSessionId) ? PlaySessionId : serverSessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                // 可见失败：不留"静默 0 进度"这种形态（分片必然 400，日志必须能指认原因）
                DebugLog.Warn($"[hls] HLS-NO-PLAYSESSIONID item={item.Id} ⇒ master.m3u8 将不带 PlaySessionId，分片会被服务端拒（400）");
            }
            Log($"媒体源不支持直连 ⇒ 走转码流（PlaySessionId={(string.IsNullOrWhiteSpace(sessionId) ? "(空!)" : sessionId)}"
                + $"；来源={(string.IsNullOrWhiteSpace(serverSessionId) ? "外壳会话" : "服务端 PlaybackInfo")}）");
            return Emby.TranscodeUrl(item.Id, mediaSourceId: source?.Id, playSessionId: sessionId);
        }
        return Emby.DirectStreamUrl(item.Id, mediaSourceId: source?.Id, startSeconds: startSeconds);
    }

    /// <summary>
    /// 取详情 + 播放信息（含降级路径），并组装内核启动请求（等价 Dart <c>buildRequest</c>）。
    /// <paramref name="startSeconds"/> 为空时用条目自身的续播点。
    /// </summary>
    public async Task<PlaybackRequest> BuildRequestAsync(
        EmbyItem target,
        double? startSeconds = null,
        IReadOnlyList<EmbyItem> episodeList = null,
        EmbyItem previous = null,
        EmbyItem next = null,
        CancellationToken cancellationToken = default)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));

        var detailed = await Emby.PlaybackInfoAsync(target.Id, cancellationToken: cancellationToken).ConfigureAwait(false) ?? target;

        List<EmbyItem> episodeSnapshot = null;
        if (episodeList != null) episodeSnapshot = new List<EmbyItem>(episodeList);

        lock (_stateGate)
        {
            _item = detailed;
            _playedNotified = false;
            if (episodeSnapshot != null) _episodes = episodeSnapshot;
        }

        var resume = startSeconds ?? detailed.UserData.PositionSeconds;
        lock (_stateGate) _lastPositionSeconds = resume;

        var source = CurrentSource;
        var sources = detailed.MediaSources ?? new List<EmbyMediaSource>();

        var versionOptions = new List<HostVersionOption>();
        for (var i = 0; i < sources.Count; i++)
        {
            versionOptions.Add(new HostVersionOption
            {
                Label = sources[i].VersionLabel,
                Id = sources[i].Id,
                Index = i,
                Selected = i == VersionIndex,
            });
        }

        var audioTracks = AudioTracksOf(source);
        var subtitleTracks = SubtitleTracksOf(source);
        var segments = await SegmentsForAsync(detailed, cancellationToken).ConfigureAwait(false);
        var todb = await TodbForAsync(detailed, cancellationToken).ConfigureAwait(false);

        var request = new PlaybackRequest
        {
            MediaPath = StreamUrl(resume > 0 ? resume : (double?)null),
            HttpHeaders = Emby.HostHttpHeaders,
            Title = detailed.Name,
            Subtitle = detailed.SubtitleText,
            StartPosition = resume > 0 ? resume : 0,
            Monogram = TextUtils.MonogramOf(detailed.IsEpisode && detailed.SeriesName.Length > 0
                ? detailed.SeriesName
                : detailed.Name),
            Logo = string.Empty,
            BackdropUrl = BackdropUrlOf(detailed),
            Badges = BadgesOf(detailed, source),
            VersionOptions = versionOptions,
            AudioTracks = audioTracks,
            SubtitleTracks = subtitleTracks,
            Segments = segments,
            Chapters = todb.Chapters,
            Sprite = todb.Sprite,
            EpisodeList = EpisodeListItems(),
            SubtitleId = string.Empty,
            SubtitleUrl = SubtitleUrlOf(source),
            DanmakuMatchName = DanmakuMatchNameOf(detailed),
            PreviousEpisodeId = previous?.Id,
            NextEpisodeId = next?.Id,
            SeasonId = detailed.SeasonId.Length == 0 ? null : detailed.SeasonId,
        };

        OnChanged?.Invoke();
        return request;
    }

    private string BackdropUrlOf(EmbyItem target)
    {
        var backdropId = target.ParentBackdropItemId.Length > 0
            ? target.ParentBackdropItemId
            : (target.IsEpisode ? target.SeriesId : target.Id);
        if (backdropId.Length == 0) return string.Empty;
        return Emby.ImageUrl(backdropId, type: "Backdrop", maxWidth: 1920);
    }

    // ── 轨道 / 角标 ────────────────────────────────────────────────────────

    /// <summary>音轨选项（等价 Dart <c>_audioTracks</c>）：无显式默认轨时选第一条。</summary>
    public List<HostTrackOption> AudioTracksOf(EmbyMediaSource source)
    {
        if (source == null) return new List<HostTrackOption>();
        var list = source.AudioStreams;
        var hasDefault = list.Exists(s => s.IsDefault);
        var tracks = new List<HostTrackOption>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            tracks.Add(new HostTrackOption
            {
                Label = s.Label,
                Id = s.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Url = s.Path,
                Title = s.Title,
                Language = s.Language,
                External = s.IsExternal,
                Selected = s.IsDefault || (!hasDefault && i == 0),
                Special = false,
                EmbyIndex = s.Index,
            });
        }
        return tracks;
    }

    /// <summary>字幕轨选项（等价 Dart <c>_subtitleTracks</c>）：首选语言 → 自动选默认轨；图像字幕标为特殊轨。</summary>
    public List<HostTrackOption> SubtitleTracksOf(EmbyMediaSource source)
    {
        if (source == null) return new List<HostTrackOption>();
        var preferred = (Settings.PreferredSubtitleLanguage ?? string.Empty).Trim().ToLowerInvariant();
        var list = source.SubtitleStreams;

        HostTrackOption selected = null;
        foreach (var s in list)
        {
            if (!s.IsTextSubtitle) continue; // 图像字幕交给内核内部处理
            var lang = (s.Language ?? string.Empty).ToLowerInvariant();
            if (preferred.Length > 0 && lang.Length > 0 && lang.StartsWith(preferred, StringComparison.Ordinal))
            {
                selected = TrackOf(s);
                break;
            }
        }
        if (selected == null && Settings.AutoSelectSubtitle)
        {
            foreach (var s in list)
            {
                if (s.IsDefault)
                {
                    selected = TrackOf(s);
                    break;
                }
            }
        }

        var tracks = new List<HostTrackOption>(list.Count);
        foreach (var s in list)
        {
            tracks.Add(new HostTrackOption
            {
                Label = s.Label,
                Id = s.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Url = TrackUrl(source, s),
                Title = s.Title,
                Language = s.Language,
                External = s.IsExternal,
                Selected = selected != null && selected.EmbyIndex == s.Index,
                Special = !s.IsTextSubtitle, // 图形字幕标为特殊轨
                EmbyIndex = s.Index,
            });
        }
        return tracks;
    }

    private static HostTrackOption TrackOf(EmbyMediaStream s) => new HostTrackOption
    {
        Label = s.Label,
        Id = s.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Title = s.Title,
        Language = s.Language,
        External = s.IsExternal,
        Selected = true,
        EmbyIndex = s.Index,
    };

    private string TrackUrl(EmbyMediaSource source, EmbyMediaStream stream)
    {
        if (stream.DeliveryUrl.Length > 0) return stream.DeliveryUrl;
        if (!stream.IsTextSubtitle) return string.Empty;
        var item = Item;
        return item == null ? string.Empty : Emby.SubtitleUrl(item.Id, source.Id, stream.Index);
    }

    /// <summary>首选外挂字幕（内核 <c>--subtitle-url=</c>）。</summary>
    public string SubtitleUrlOf(EmbyMediaSource source)
    {
        if (source == null || !Settings.AutoSelectSubtitle) return string.Empty;
        var preferred = (Settings.PreferredSubtitleLanguage ?? string.Empty).Trim().ToLowerInvariant();
        if (preferred.Length == 0) return string.Empty;
        var item = Item;
        if (item == null) return string.Empty;
        foreach (var s in source.SubtitleStreams)
        {
            if (!s.IsTextSubtitle) continue;
            var lang = (s.Language ?? string.Empty).ToLowerInvariant();
            if (lang.Length > 0 && lang.StartsWith(preferred, StringComparison.Ordinal))
            {
                return Emby.SubtitleUrl(item.Id, source.Id, s.Index);
            }
        }
        return string.Empty;
    }

    /// <summary>角标（4K / HDR / 杜比…）—— 由媒体流属性推断（等价 Dart <c>_badges</c>）。</summary>
    public List<string> BadgesOf(EmbyItem target, EmbyMediaSource source)
    {
        var badges = new List<string>();
        var streams = source?.MediaStreams ?? new List<EmbyMediaStream>();

        foreach (var v in streams)
        {
            if (!v.IsVideo) continue;
            if (v.Height >= 2160) badges.Add("4K");
            else if (v.Height >= 1080) badges.Add("1080P");
            else if (v.Height >= 720) badges.Add("720P");

            var codec = (v.Codec ?? string.Empty).ToLowerInvariant();
            if (codec == "hevc" || codec == "h265") badges.Add("HEVC");
            if (codec == "av1") badges.Add("AV1");
        }

        foreach (var s in streams)
        {
            if (!s.IsAudio) continue;
            var codec = (s.Codec ?? string.Empty).ToLowerInvariant();
            if (codec == "truehd" || codec == "eac3" || codec == "ac3")
            {
                badges.Add("杜比");
                break;
            }
            if (codec == "dts")
            {
                badges.Add("DTS");
                break;
            }
        }

        if (target?.Raw != null && target.Raw.ContainsKey("Video3DFormat")) badges.Add("3D");

        // 等价 Dart `badges.toSet().toList()`：去重且保留首次出现顺序。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(badges.Count);
        foreach (var badge in badges)
        {
            if (seen.Add(badge)) result.Add(badge);
        }
        return result;
    }

    // ── 跳过片段 / 章节 / 剧集列表 / 弹幕匹配名 ────────────────────────────

    /// <summary>跳过片段：Emby 原生 + 三源聚合（等价 Dart <c>_segmentsFor</c>）。</summary>
    public async Task<List<MediaSegmentDto>> SegmentsForAsync(EmbyItem target, CancellationToken cancellationToken = default)
    {
        var embySegments = await Emby.GetMediaSegmentsAsync(target.Id, cancellationToken).ConfigureAwait(false);
        if (!Settings.SkipIntroEnabled && !Settings.SkipCreditsEnabled) return new List<MediaSegmentDto>();

        var service = SegmentService;
        var all = service == null
            ? embySegments
            : await service.CollectAsync(
                Settings.SkipSources,
                imdbId: target.ImdbId,
                season: target.ParentIndexNumber,
                episode: target.IndexNumber,
                durationSeconds: target.DurationSeconds,
                embySegments: embySegments,
                cancellationToken: cancellationToken).ConfigureAwait(false);

        var filtered = new List<MediaSegmentDto>();
        foreach (var segment in all)
        {
            // 等价 Dart switch：intro/recap 看「跳过片头」，其余（credits/preview）看「跳过片尾」。
            var keep = segment.Type == MediaSegmentType.Intro || segment.Type == MediaSegmentType.Recap
                ? Settings.SkipIntroEnabled
                : Settings.SkipCreditsEnabled;
            if (keep) filtered.Add(segment);
        }
        return filtered;
    }

    /// <summary>章节 + 进度条缩略图（等价 Dart <c>_todbFor</c>；无服务或非电影/剧集时返回空）。</summary>
    public async Task<TodbMetadata> TodbForAsync(EmbyItem target, CancellationToken cancellationToken = default)
    {
        var service = TodbService;
        var imdb = target?.ImdbId;
        if (service == null || string.IsNullOrEmpty(imdb)) return new TodbMetadata();
        return await service.FetchAsync(
            imdb,
            season: target.ParentIndexNumber,
            episode: target.IndexNumber,
            durationSeconds: target.DurationSeconds,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>内核 <c>--episode-list=</c>：所在季全部集的列表（等价 Dart <c>_episodeListItems</c>）。</summary>
    public List<HostEpisodeItem> EpisodeListItems()
    {
        var item = Item;
        var episodes = Episodes;
        var list = new List<HostEpisodeItem>(episodes.Count);
        foreach (var e in episodes)
        {
            list.Add(new HostEpisodeItem
            {
                Id = e.Id,
                Label = e.IndexNumber == null ? e.Name : $"第 {e.IndexNumber.Value} 集 · {e.Name}",
                IsCurrent = e.Id == item?.Id,
                IsPlayed = e.UserData.Played,
            });
        }
        return list;
    }

    /// <summary>
    /// 弹幕匹配名（内核 <c>--danmaku-match-name=</c>，外壳需 URL 编码后传；等价 Dart <c>_danmakuMatchName</c>）。
    /// 模板占位符：<c>{title}</c>（剧名优先）、<c>{episode}</c>（<c>S01E02</c>）、<c>{episodeNumber}</c>、
    /// <c>{season}</c>、<c>{year}</c>。
    /// </summary>
    public string DanmakuMatchNameOf(EmbyItem target)
    {
        var apis = Settings.ActiveDanmakuApis;
        if (apis == null || apis.Count == 0) return null;

        var template = (Settings.DanmakuMatchTemplate ?? string.Empty).Trim();
        if (template.Length == 0) template = "{title} {episode}";

        var showName = target.IsEpisode && target.SeriesName.Length > 0 ? target.SeriesName : target.Name;
        var code = target.EpisodeCodeText;
        var name = template
            .Replace("{title}", showName ?? string.Empty)
            .Replace("{episode}", code ?? string.Empty)
            .Replace("{episodeNumber}", target.IndexNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            .Replace("{season}", target.ParentIndexNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            .Replace("{year}", target.ProductionYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
        return name.Length == 0 ? null : name;
    }

    // ── 进度上报（内核回调 → Emby）─────────────────────────────────────────

    /// <summary>
    /// 开播上报（<c>POST /Sessions/Playing</c>；等价 Dart <c>reportStarted</c>）。
    /// 返回是否真的发出（无当前条目时为 <c>false</c>）。
    /// </summary>
    public async Task<bool> ReportStartedAsync(
        PlaybackRequest request,
        double? position = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var target = Item;
        if (target == null) return false;

        var reportedPosition = position ?? request.StartPosition;
        var audioIndex = request.SelectedAudioIndex();
        var subtitleIndex = request.SelectedSubtitleIndex();
        bool mutedNow;
        double rateNow;
        int? volumeNow;
        lock (_stateGate)
        {
            _lastPositionSeconds = reportedPosition;
            _audioStreamIndex = audioIndex;
            _subtitleStreamIndex = subtitleIndex;
            _isPaused = false;
            mutedNow = _isMuted;
            rateNow = _playbackRate;
            volumeNow = _volumeLevel >= 0 ? _volumeLevel : (int?)null;
        }

        try
        {
            await Emby.ReportPlaybackStartAsync(
                itemId: target.Id,
                playSessionId: PlaySessionId,
                mediaSourceId: CurrentSource?.Id,
                positionSeconds: reportedPosition,
                audioStreamIndex: audioIndex,
                subtitleStreamIndex: subtitleIndex,
                isPaused: false,
                isMuted: mutedNow,
                playbackRate: rateNow,
                volumeLevel: volumeNow,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 上报失败不得中断播放（EmbyService 内部已吞异常；此处兜底并告知 UI）。
            Log($"开播上报失败（不阻塞播放）：{ex.Message}");
            PlaybackFailed?.Invoke(ex);
            OnChanged?.Invoke();
            return false;
        }

        Log($"已上报开播：{target.Name} @{reportedPosition:F1}s（playSession={PlaySessionId}）");
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// <c>progress</c> 回调上报（等价 Dart <c>reportProgress</c>）。
    /// ⚠️ 内核 <c>progress</c> 的 <c>audioStreamIndex</c>/<c>subtitleStreamIndex</c> 直接透传为
    /// <c>--audio-track=</c>/<c>--subtitle-track=</c> 中给的 <c>embyIndex</c>，因此可原样上报 Emby。
    /// </summary>
    /// <param name="hostEvent">内核回调事件（<c>event.raw</c> 里的字段原样使用）。</param>
    /// <param name="force">跳过节流立即上报（停止/换源/状态突变等边界用）。</param>
    /// <returns><c>true</c> = 本次真的发出了上报；<c>false</c> = 被节流或被跳过。</returns>
    public async Task<bool> ReportProgressAsync(HostEvent hostEvent, bool force = false, CancellationToken cancellationToken = default)
    {
        if (hostEvent == null) throw new ArgumentNullException(nameof(hostEvent));
        var target = Item;
        if (target == null) return false;

        var raw = hostEvent.Raw;
        var position = hostEvent.PositionSeconds;
        var paused = hostEvent.IsPaused;
        var muted = HostEventExtensions.BoolOf(raw, "isMuted");
        var rate = HostEventExtensions.DoubleOf(raw, "playbackRate") ?? 1.0;
        var volume = HostEventExtensions.IntOf(raw, "volumeLevel");
        var audioIndex = HostEventExtensions.IntOf(raw, "audioStreamIndex") ?? -1;
        var subtitleIndex = HostEventExtensions.IntOf(raw, "subtitleStreamIndex") ?? -1;

        bool shouldReport;
        lock (_stateGate)
        {
            // 先取旧值再写入新值 —— 边界判定（暂停态突变 / 位置回退）必须与上一帧比较。
            var pausedChanged = paused != _isPaused;
            var positionWentBack = position < _lastPositionSeconds - 0.001;

            _lastPositionSeconds = position;
            _isPaused = paused;
            _isMuted = muted;
            _playbackRate = rate;
            if (volume.HasValue) _volumeLevel = volume.Value;
            _audioStreamIndex = audioIndex;
            _subtitleStreamIndex = subtitleIndex;

            var now = DateTime.UtcNow.Ticks;
            shouldReport = force
                || _lastProgressTimestamp == 0
                || now - _lastProgressTimestamp >= ProgressMinInterval.Ticks
                || pausedChanged             // 状态突变（暂停/继续）立即补发，对齐 §9 实测
                || positionWentBack;         // 跳转/换源边界立即补发
            if (shouldReport) _lastProgressTimestamp = now;
        }
        OnChanged?.Invoke();
        if (!shouldReport) return false;

        try
        {
            await Emby.ReportPlaybackProgressAsync(
                itemId: target.Id,
                playSessionId: PlaySessionId,
                positionSeconds: position,
                isPaused: paused,
                mediaSourceId: CurrentSource?.Id,
                audioStreamIndex: audioIndex,
                subtitleStreamIndex: subtitleIndex,
                isMuted: muted,
                playbackRate: rate,
                volumeLevel: volume,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"进度上报失败（不阻塞播放）：{ex.Message}");
            PlaybackFailed?.Invoke(ex);
            return false;
        }

        // 接近片尾 ⇒ 标记已看（与原版一致的「看完即已看」体验）。
        var duration = hostEvent.DurationSeconds;
        if (duration.HasValue && duration.Value > 0 && position / duration.Value >= PlayedThreshold && !target.UserData.Played)
        {
            await MarkPlayedOnceAsync(target, cancellationToken).ConfigureAwait(false);
        }
        return true;
    }

    private async Task MarkPlayedOnceAsync(EmbyItem target, CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            if (_playedNotified) return;
            _playedNotified = true;
        }

        var ok = false;
        try
        {
            ok = await Emby.SetPlayedAsync(target.Id, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"标记已看失败（不阻塞播放）：{ex.Message}");
            PlaybackFailed?.Invoke(ex);
        }

        if (ok)
        {
            Log($"已标记为已看：{target.Name}");
            lock (_stateGate)
            {
                // 等价 Dart `item = target.withUserData(userData.copyWith(played: true))`：
                // 只在该条目仍是当前条目时就地替换（含换集后的同 id 新实例）。
                if (_item == null || _item.Id == target.Id)
                {
                    _item = target.WithUserData(target.UserData.With(played: true));
                }
            }
            OnChanged?.Invoke();
        }
    }

    /// <summary><c>stopped</c> 回调上报（收尾，通常随后内核即退出；等价 Dart <c>reportStopped</c>）。</summary>
    public async Task<bool> ReportStoppedAsync(HostEvent hostEvent, CancellationToken cancellationToken = default)
    {
        if (hostEvent == null) throw new ArgumentNullException(nameof(hostEvent));
        var target = Item;
        if (target == null) return false;

        var position = hostEvent.PositionSeconds;
        var paused = hostEvent.IsPaused;
        var muted = HostEventExtensions.BoolOf(hostEvent.Raw, "isMuted");
        var rate = HostEventExtensions.DoubleOf(hostEvent.Raw, "playbackRate") ?? 1.0;

        lock (_stateGate)
        {
            _lastPositionSeconds = position;
            _isPaused = paused;
            _isMuted = muted;
            _playbackRate = rate;
            _lastProgressTimestamp = 0; // 下次会话/换源从干净状态开始节流
        }
        OnChanged?.Invoke();

        try
        {
            await Emby.ReportPlaybackStoppedAsync(
                itemId: target.Id,
                playSessionId: PlaySessionId,
                positionSeconds: position,
                mediaSourceId: CurrentSource?.Id,
                isPaused: paused,
                isMuted: muted,
                playbackRate: rate,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"停止上报失败（不阻塞退出）：{ex.Message}");
            PlaybackFailed?.Invoke(ex);
            return false;
        }

        // 续播点：Emby 由服务端依据 Stopped 的 PositionTicks 记录；这里只保留内存值供换源续播。
        Log($"已上报停止：{target.Name} @{position:F1}s");
        return true;
    }

    // ── 收藏 / 已看 / 隐藏（对应 DESIGN §8 S4 的「播放中收藏/标已看」）────────

    /// <summary>收藏开关（详情页/播放中按钮；等价 Dart <c>toggleFavorite</c>）。</summary>
    public async Task<bool> ToggleFavoriteAsync(CancellationToken cancellationToken = default)
    {
        var target = Item;
        if (target == null) return false;

        var next = !target.UserData.IsFavorite;
        var ok = await Emby.SetFavoriteAsync(target.Id, next, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            var updated = target.WithUserData(target.UserData.With(isFavorite: next));
            lock (_stateGate)
            {
                if (_item == null || _item.Id == target.Id) _item = updated;
            }
            OnChanged?.Invoke();
        }
        return ok;
    }

    /// <summary>已看开关（等价 Dart <c>togglePlayed</c>）。</summary>
    public async Task<bool> TogglePlayedAsync(CancellationToken cancellationToken = default)
    {
        var target = Item;
        if (target == null) return false;

        var next = !target.UserData.Played;
        var ok = await Emby.SetPlayedAsync(target.Id, next, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            var updated = target.WithUserData(target.UserData.With(played: next));
            lock (_stateGate)
            {
                if (_item == null || _item.Id == target.Id) _item = updated;
                _playedNotified = next;
            }
            OnChanged?.Invoke();
        }
        return ok;
    }

    /// <summary>
    /// 从「继续观看」隐藏 / 取消隐藏（<c>POST /Users/{userId}/HideFromResume</c>）。
    /// <paramref name="hide"/> 为空时按当前已看状态取反（已看 ⇒ 隐藏）。
    /// </summary>
    public async Task<bool> HideFromResumeAsync(bool? hide = null, CancellationToken cancellationToken = default)
    {
        var target = Item;
        if (target == null) return false;
        var effective = hide ?? !target.UserData.Played;
        var ok = await Emby.SetHideFromResumeAsync(target.Id, effective, cancellationToken).ConfigureAwait(false);
        if (ok) Log($"已{(effective ? "隐藏" : "恢复")}「继续观看」：{target.Name}");
        return ok;
    }

    // ── 导航解析（内核回调 → 下一条源）────────────────────────────────────

    /// <summary>
    /// 处理内核导航回调；返回 <c>null</c> 表示本轮不换源（内核保持现状）。
    /// 对应 HOST_CONTRACT §4.1 的 6 类导航 + <c>progress</c>（可借其顺手切源）。
    /// </summary>
    public async Task<PlaybackRequest> ResolveNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken = default)
    {
        if (hostEvent == null) return null;
        switch (hostEvent.Kind)
        {
            case HostEventKind.NavigateNext:
                return await PlayNeighborAsync(1, cancellationToken).ConfigureAwait(false);
            case HostEventKind.NavigatePrevious:
                return await PlayNeighborAsync(-1, cancellationToken).ConfigureAwait(false);
            case HostEventKind.NavigateEpisode:
            {
                var id = hostEvent.TargetEpisodeId;
                if (string.IsNullOrEmpty(id)) return null;
                return await PlayByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            case HostEventKind.PreloadEpisode:
                await PreloadAsync(hostEvent.TargetEpisodeId, cancellationToken).ConfigureAwait(false);
                return null; // 预加载只是提前取流，不换源
            case HostEventKind.SwitchVersion:
                return await SwitchVersionAsync(hostEvent.VersionIndex, cancellationToken).ConfigureAwait(false);
            case HostEventKind.RefreshPlaybackUrl:
                return await RebuildAsync(hostEvent.PositionSeconds, cancellationToken).ConfigureAwait(false);
            case HostEventKind.Progress:
            case HostEventKind.Stopped:
            case HostEventKind.ManualResize:
            case HostEventKind.Unknown:
            default:
                return null;
        }
    }

    /// <summary>上一集 / 下一集（等价 Dart <c>_playNeighbor</c>）。</summary>
    public async Task<PlaybackRequest> PlayNeighborAsync(int offset, CancellationToken cancellationToken = default)
    {
        var current = Item;
        if (current == null) return null;

        var episodes = Episodes;
        var index = IndexOfEpisode(episodes, current.Id);
        if (index >= 0)
        {
            var target = index + offset;
            if (target >= 0 && target < episodes.Count)
            {
                return await PlayAsync(episodes[target], 0, cancellationToken).ConfigureAwait(false);
            }
        }
        if (offset > 0)
        {
            // 不在本地列表里 ⇒ 问服务端要「下一集」
            var nextUp = await Emby.GetNextUpAsync(
                seriesId: current.SeriesId.Length == 0 ? null : current.SeriesId,
                limit: 1,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (nextUp.Count > 0) return await PlayAsync(nextUp[0], 0, cancellationToken).ConfigureAwait(false);
        }
        Log($"没有可用的{(offset > 0 ? "下" : "上")}一集");
        return null;
    }

    /// <summary>指定集（内核选集/预加载回调；等价 Dart <c>_playById</c>）。</summary>
    public async Task<PlaybackRequest> PlayByIdAsync(string episodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(episodeId)) return null;

        var episodes = Episodes;
        EmbyItem target = null;
        foreach (var e in episodes)
        {
            if (e.Id == episodeId)
            {
                target = e;
                break;
            }
        }
        if (target == null) target = await Emby.GetItemAsync(episodeId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (target == null)
        {
            Log($"取不到条目 {episodeId}");
            return null;
        }
        await EnsureEpisodesLoadedAsync(target, cancellationToken).ConfigureAwait(false);
        return await PlayAsync(target, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>统一入口：切到 <paramref name="target"/> 并生成下一条源（等价 Dart <c>_play</c>）。</summary>
    public async Task<PlaybackRequest> PlayAsync(EmbyItem target, double startSeconds = 0, CancellationToken cancellationToken = default)
    {
        await EnsureEpisodesLoadedAsync(target, cancellationToken).ConfigureAwait(false);

        EmbyItem previous = null;
        EmbyItem next = null;
        var episodes = Episodes;
        var index = IndexOfEpisode(episodes, target.Id);
        if (index >= 0)
        {
            if (index > 0) previous = episodes[index - 1];
            if (index + 1 < episodes.Count) next = episodes[index + 1];
        }

        lock (_stateGate)
        {
            _versionIndex = 0;
            _lastProgressTimestamp = 0; // 换集后立即补发首帧进度
        }
        Log($"切集：{target.Name}{(target.EpisodeCodeText.Length == 0 ? string.Empty : $" ({target.EpisodeCodeText})")}");

        return await BuildRequestAsync(
            target,
            startSeconds: startSeconds,
            previous: previous,
            next: next,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 换清晰度/版本（内核 <c>switch_version</c> 回调，<paramref name="versionIndex"/> 即
    /// <c>--version-option=</c> 的顺序；等价 Dart <c>_switchVersion</c>）。
    /// </summary>
    public async Task<PlaybackRequest> SwitchVersionAsync(int? versionIndex, CancellationToken cancellationToken = default)
    {
        var current = Item;
        if (current == null || !versionIndex.HasValue) return null;
        var count = current.MediaSources?.Count ?? 0;
        if (count == 0) return null;

        var clamped = Math.Clamp(versionIndex.Value, 0, count - 1);
        lock (_stateGate) _versionIndex = clamped;
        Log($"切换版本 → #{clamped}");
        return await RebuildAsync(LastPositionSeconds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>重新签发播放地址（token 过期 / <c>refresh_playback_url</c>；等价 Dart <c>_rebuild</c>）。</summary>
    public async Task<PlaybackRequest> RebuildAsync(double resumeFrom, CancellationToken cancellationToken = default)
    {
        var current = Item;
        if (current == null) return null;
        return await BuildRequestAsync(
            current,
            startSeconds: resumeFrom,
            episodeList: Episodes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>预加载：提前取一次播放信息（内核随后若真要换源可直接命中；等价 Dart <c>_preload</c>）。</summary>
    public async Task PreloadAsync(string episodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(episodeId)) return;
        try
        {
            await Emby.PlaybackInfoAsync(episodeId, cancellationToken: cancellationToken).ConfigureAwait(false);
            Log($"已预加载 {episodeId} 的播放信息");
        }
        catch (Exception ex)
        {
            // Dart `catch (e) { _log('预加载失败：$e'); }` —— 预加载失败不影响播放。
            Log($"预加载失败：{ex.Message}");
        }
    }

    /// <summary>剧集列表按需加载（连播与 <c>--episode-list=</c> 都依赖它；等价 Dart <c>_ensureEpisodesLoaded</c>）。</summary>
    public async Task EnsureEpisodesLoadedAsync(EmbyItem target, CancellationToken cancellationToken = default)
    {
        if (target == null) return;
        if (!target.IsEpisode || target.SeriesId.Length == 0) return;

        var episodes = Episodes;
        if (episodes.Count > 1 && IndexOfEpisode(episodes, target.Id) >= 0) return;

        try
        {
            var list = await Emby.GetEpisodesAsync(
                seriesId: target.SeriesId,
                seasonId: target.SeasonId.Length == 0 ? null : target.SeasonId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (list.Count > 0)
            {
                lock (_stateGate) _episodes = new List<EmbyItem>(list);
            }
        }
        catch (Exception ex)
        {
            // Dart `catch (e) { _log('剧集列表加载失败：$e'); }`
            Log($"剧集列表加载失败：{ex.Message}");
        }
    }

    private static int IndexOfEpisode(IReadOnlyList<EmbyItem> episodes, string id)
    {
        for (var i = 0; i < episodes.Count; i++)
        {
            if (episodes[i].Id == id) return i;
        }
        return -1;
    }
}

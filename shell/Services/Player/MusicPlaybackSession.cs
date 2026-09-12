// 等价移植：rebuild/ai_player/lib/core/player/music_playback_session.dart（183 行 Dart → C#，逐成员翻译）。
// 契约依据：
//   - reversed/FlutterApp/SERVICE_API.md §3（Navidrome / Subsonic）：收听计数规则 = 播满 **50%** 或
//     **≥240 秒**即提交 `scrobble?submission=true`（每曲只提交一次）；`submission=false` 即 now-playing。
//   - reversed/FlutterApp/HOST_CONTRACT.md §4.1 回调（换曲走 navigate_next / navigate_previous /
//     navigate_episode；`refresh_playback_url` 重新签发流地址）。
//
// 端口（Dart → C#）：
//   queue/index/positionSeconds/durationSeconds/isPaused/currentLyrics/lyricsLoading → 同名属性（锁保护）
//   get current → Current                     | requestFor() → RequestFor()
//   begin() → BeginAsync()                    | handleNavigation() → HandleNavigationAsync()
//   onHostEvent() → OnHostEventAsync()        | _maybeSubmitScrobble() 内联于 OnHostEventAsync()
//
// 依赖注入（关键，避免与并行任务耦合）：
//   Dart 直接持有 `NavidromeService` / `LyricsService`；C# 版改为**取数面委托**，不引用任何服务类型
//   （`NavidromeService` / 未来的 `LyricsService` 都可由外壳侧一行 lambda 接上）：
//     <see cref="StreamUrlResolver"/>        ← NavidromeService.StreamUrl(songId)
//     <see cref="CoverArtUrlResolver"/>      ← NavidromeService.CoverArtUrl(coverArtId, 600)
//     <see cref="ScrobbleCallback"/>         ← NavidromeService.ScrobbleAsync(songId, submission, ct)
//     <see cref="LyricsLoader"/>             ← LyricsService.Find(...)（未就绪时可不传，退化为「无歌词」）
//   这样音乐会话可在**无网络**下用假实现单测（与 EmbyPlaybackSession 的假 EmbyService 同一思路）。
//
// 依赖模型：歌曲沿用服务层已落地的 `SubsonicSong`（Services/Models/NavidromeModels.cs，与 Dart 同结构）。
// 歌词结果的**最小形状**（Dart `LyricsResult`）在此以 <see cref="MusicLyricsResult"/> 声明：服务层尚无
// `LyricsService`/`LyricsResult` C# 等价物，本次任务限定只创建四个文件 ⇒ 不新建服务文件，只保留本会话
// 实际读取的字段（source + isEmpty + 歌词文本），由注入的 <see cref="LyricsLoader"/> 产出。

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Player;

/// <summary>
/// 歌词加载结果（Dart `LyricsResult` 中本会话实际使用的字段子集）。
/// <see cref="Source"/> 取值 <c>lrclib</c> / <c>subsonic</c> / <c>cache</c>；<c>null</c> 表示未找到。
/// </summary>
public sealed class MusicLyricsResult
{
    public string TrackName { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string AlbumName { get; set; } = string.Empty;

    public double Duration { get; set; }

    public bool Instrumental { get; set; }

    public string PlainLyrics { get; set; } = string.Empty;

    /// <summary>带时间轴的 LRC（用于歌词高亮）。</summary>
    public string SyncedLyrics { get; set; } = string.Empty;

    /// <summary><c>lrclib</c> / <c>subsonic</c> / <c>cache</c>。</summary>
    public string Source { get; set; } = "lrclib";

    /// <summary>等价 Dart <c>isEmpty</c>：平文本与时间轴皆空且非纯音乐。</summary>
    public bool IsEmpty => (PlainLyrics ?? string.Empty).Trim().Length == 0
        && (SyncedLyrics ?? string.Empty).Trim().Length == 0
        && !Instrumental;
}

/// <summary>流地址解析（等价 Dart <c>navidrome.streamUrl(songId)</c>）。</summary>
public delegate string StreamUrlResolver(string songId);

/// <summary>封面地址解析（等价 Dart <c>navidrome.coverArtUrl(coverArt, size: 600)</c>）。</summary>
public delegate string CoverArtUrlResolver(string coverArtId, int size);

/// <summary>收听计数上报（等价 Dart <c>navidrome.scrobble(songId, submission: …)</c>；返回是否成功）。</summary>
public delegate Task<bool> ScrobbleCallback(string songId, bool submission, CancellationToken cancellationToken);

/// <summary>歌词查找（等价 Dart <c>lyrics.find(trackName:…, subsonic:…)</c>；返回 <c>null</c> = 未找到）。</summary>
public delegate Task<MusicLyricsResult> LyricsLoader(SubsonicSong song, CancellationToken cancellationToken);

/// <summary>
/// 音乐播放会话：Navidrome 队列 ↔ 播放内核 的双向翻译层（对应 Dart <c>MusicPlaybackSession</c>）。
/// 正向：<see cref="SubsonicSong"/> → <see cref="PlaybackRequest"/>（<c>--open=/rest/stream?id=…</c> + 队列导航项）；
/// 反向：内核 <c>progress</c>/<c>stopped</c> → 播放位置（歌词高亮 + 收听计数）。
/// </summary>
public sealed class MusicPlaybackSession
{
    /// <summary>收听计数阈值一：播满 50%。</summary>
    public const double ScrobbleRatio = 0.5;

    /// <summary>收听计数阈值二：已播 ≥ 240 秒（两者取「或」）。</summary>
    public const double ScrobbleMinSeconds = 240;

    private readonly object _gate = new object();
    private readonly StreamUrlResolver _streamUrl;
    private readonly CoverArtUrlResolver _coverArtUrl;
    private readonly ScrobbleCallback _scrobble;
    private readonly LyricsLoader _lyricsLoader;
    private readonly Action<string> _onLog;

    private List<SubsonicSong> _queue = new List<SubsonicSong>();
    private int _index = -1;
    private double _positionSeconds;
    private double? _durationSeconds;
    private bool _isPaused;
    private MusicLyricsResult _currentLyrics;
    private bool _lyricsLoading;
    private bool _submitted;

    /// <param name="streamUrl">流地址解析（必填）。</param>
    /// <param name="coverArtUrl">封面地址解析（可空 —— 为空则 <c>backdropUrl</c> 留空）。</param>
    /// <param name="scrobbleCallback">收听计数上报（可空 —— 为空则不发 now-playing/计数）。</param>
    /// <param name="lyricsLoader">歌词查找（可空 —— 为空则直接置「未找到」）。</param>
    public MusicPlaybackSession(
        StreamUrlResolver streamUrl,
        CoverArtUrlResolver coverArtUrl = null,
        ScrobbleCallback scrobbleCallback = null,
        LyricsLoader lyricsLoader = null,
        Action<string> onLog = null)
    {
        _streamUrl = streamUrl ?? throw new ArgumentNullException(nameof(streamUrl));
        _coverArtUrl = coverArtUrl;
        _scrobble = scrobbleCallback;
        _lyricsLoader = lyricsLoader;
        _onLog = onLog;
    }

    /// <summary>状态变化通知（歌词加载完成 / 换曲 / 进度刷新 ⇒ UI 重建；等价 Dart <c>onChanged</c>）。</summary>
    public Action OnChanged { get; set; }

    /// <summary>当前队列（快照副本）。</summary>
    public IReadOnlyList<SubsonicSong> Queue
    {
        get { lock (_gate) return _queue.ToArray(); }
    }

    /// <summary>当前游标（<c>-1</c> = 无曲目）。</summary>
    public int Index
    {
        get { lock (_gate) return _index; }
    }

    /// <summary>内核回传的播放位置（秒）。</summary>
    public double PositionSeconds
    {
        get { lock (_gate) return _positionSeconds; }
    }

    /// <summary>当前曲目时长（秒；未知为 <c>null</c>）。</summary>
    public double? DurationSeconds
    {
        get { lock (_gate) return _durationSeconds; }
    }

    public bool IsPaused
    {
        get { lock (_gate) return _isPaused; }
    }

    /// <summary>当前曲目歌词（<c>null</c> = 加载中或未找到；与 Dart 语义一致）。</summary>
    public MusicLyricsResult CurrentLyrics
    {
        get { lock (_gate) return _currentLyrics; }
    }

    public bool LyricsLoading
    {
        get { lock (_gate) return _lyricsLoading; }
    }

    /// <summary>当前曲目（越界/空队列为 <c>null</c>；等价 Dart <c>current</c>）。</summary>
    public SubsonicSong Current
    {
        get
        {
            lock (_gate)
            {
                return _index >= 0 && _index < _queue.Count ? _queue[_index] : null;
            }
        }
    }

    private void Log(string message)
    {
        _onLog?.Invoke(message);
        DebugLog.Info($"[music-session] {message}");
    }

    /// <summary>
    /// 开始播放：设置队列、定位到 <paramref name="song"/>、加载歌词并上报 <c>now playing</c>
    /// （等价 Dart <c>begin</c>）。返回 <c>null</c> 表示队列为空且目标曲目缺失。
    /// </summary>
    public Task<PlaybackRequest> BeginAsync(
        IReadOnlyList<SubsonicSong> songs,
        SubsonicSong song,
        CancellationToken cancellationToken = default)
    {
        if (song == null) throw new ArgumentNullException(nameof(song));

        int index;
        lock (_gate)
        {
            _queue = songs == null || songs.Count == 0 ? new List<SubsonicSong> { song } : new List<SubsonicSong>(songs);
            index = _queue.FindIndex(s => s.Id == song.Id);
            if (index < 0) index = 0;
            _index = index;
        }
        return ActivateAsync(Current, cancellationToken);
    }

    /// <summary>
    /// 内核导航回调 → 下一条播放请求（换曲时同步歌词与 <c>now playing</c>；等价 Dart <c>handleNavigation</c>）。
    /// </summary>
    public async Task<PlaybackRequest> HandleNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken = default)
    {
        if (hostEvent == null) return null;
        switch (hostEvent.Kind)
        {
            case HostEventKind.NavigateNext:
                return await StepAsync(1, cancellationToken).ConfigureAwait(false);
            case HostEventKind.NavigatePrevious:
                return await StepAsync(-1, cancellationToken).ConfigureAwait(false);
            case HostEventKind.NavigateEpisode:
            {
                var id = hostEvent.TargetEpisodeId;
                if (string.IsNullOrEmpty(id)) return null;
                SubsonicSong target;
                lock (_gate)
                {
                    var found = _queue.FindIndex(s => s.Id == id);
                    if (found < 0) return null;
                    _index = found;
                    target = _queue[found];
                }
                return await ActivateAsync(target, cancellationToken).ConfigureAwait(false);
            }
            case HostEventKind.RefreshPlaybackUrl:
            {
                var song = Current;
                return song == null ? null : RequestFor(song);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// 内核 <c>progress</c> / <c>stopped</c> 回调：刷新位置、歌词高亮与收听计数（等价 Dart <c>onHostEvent</c>）。
    /// </summary>
    public async Task OnHostEventAsync(HostEvent hostEvent, CancellationToken cancellationToken = default)
    {
        if (hostEvent == null) return;
        if (hostEvent.Kind != HostEventKind.Progress && hostEvent.Kind != HostEventKind.Stopped) return;

        lock (_gate)
        {
            _positionSeconds = hostEvent.PositionSeconds;
            // 等价 Dart `durationSeconds = event.durationSeconds ?? durationSeconds`（回调缺时长时保留旧值）。
            if (hostEvent.DurationSeconds.HasValue) _durationSeconds = hostEvent.DurationSeconds.Value;
            _isPaused = hostEvent.IsPaused || hostEvent.Kind == HostEventKind.Stopped;
        }
        OnChanged?.Invoke();

        await MaybeSubmitScrobbleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>纯播放源：<c>--open=</c> 地址 + 曲目元信息 + 队列导航项（等价 Dart <c>requestFor</c>）。</summary>
    public PlaybackRequest RequestFor(SubsonicSong song)
    {
        if (song == null) throw new ArgumentNullException(nameof(song));

        var queue = Queue;
        var episodeList = new List<HostEpisodeItem>(queue.Count);
        foreach (var s in queue)
        {
            episodeList.Add(new HostEpisodeItem
            {
                Id = s.Id,
                Label = s.Title,
                IsCurrent = s.Id == song.Id,
            });
        }

        return new PlaybackRequest
        {
            MediaPath = _streamUrl(song.Id),
            HttpHeaders = new Dictionary<string, string>(StringComparer.Ordinal),
            Title = song.Title,
            Subtitle = song.Subtitle,
            Monogram = TextUtils.MonogramOf(song.Title),
            BackdropUrl = _coverArtUrl == null ? string.Empty : _coverArtUrl(song.CoverArt, 600),
            EpisodeList = episodeList,
        };
    }

    private async Task<PlaybackRequest> StepAsync(int offset, CancellationToken cancellationToken)
    {
        SubsonicSong next;
        lock (_gate)
        {
            var target = _index + offset;
            if (target < 0 || target >= _queue.Count) return null;
            _index = target;
            next = _queue[target];
        }
        return await ActivateAsync(next, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 切到某曲：重置进度 → 上报 <c>now playing</c> → 异步加载歌词（等价 Dart <c>_activate</c>）。
    /// 歌词加载**不阻塞**返回（Dart 用 <c>unawaited</c>；此处同样不 await）。
    /// </summary>
    private async Task<PlaybackRequest> ActivateAsync(SubsonicSong song, CancellationToken cancellationToken)
    {
        if (song == null) return null;

        lock (_gate)
        {
            _positionSeconds = 0;
            _durationSeconds = song.Duration > 0 ? song.Duration : (double?)null;
            _isPaused = false;
            _submitted = false;
            _currentLyrics = null;
            _lyricsLoading = true;
        }
        OnChanged?.Invoke();

        _ = LoadLyricsAsync(song, cancellationToken);

        if (_scrobble != null)
        {
            try
            {
                await _scrobble(song.Id, false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 上报失败不影响播放（与 NavidromeService.ScrobbleAsync 的「只记日志」一致）。
                Log($"now playing 上报失败（不阻塞播放）：{ex.Message}");
            }
        }

        Log($"正在播放：{song.Title}{(song.Artist.Length == 0 ? string.Empty : $" - {song.Artist}")}");
        return RequestFor(song);
    }

    /// <summary>
    /// 歌词：由注入的 <see cref="LyricsLoader"/> 决定来源（Dart 侧为 lrclib 优先 → Subsonic 回落 → 本地缓存）。
    /// </summary>
    private async Task LoadLyricsAsync(SubsonicSong song, CancellationToken cancellationToken)
    {
        if (_lyricsLoader == null)
        {
            lock (_gate)
            {
                _lyricsLoading = false;
                _currentLyrics = null;
            }
            OnChanged?.Invoke();
            return;
        }

        MusicLyricsResult result = null;
        try
        {
            result = await _lyricsLoader(song, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _currentLyrics = result;
                _lyricsLoading = false;
            }
            Log(result == null || result.IsEmpty
                ? $"未找到歌词：{song.Title}"
                : $"歌词已加载（{result.Source}）：{song.Title}");
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _lyricsLoading = false;
                _currentLyrics = null;
            }
            Log($"歌词加载失败：{ex.Message}");
        }
        OnChanged?.Invoke();
    }

    /// <summary>Subsonic 收听计数：进度 ≥ 50% 或已播 ≥ 240 秒（每曲只提交一次；等价 Dart <c>_maybeSubmitScrobble</c>）。</summary>
    private async Task MaybeSubmitScrobbleAsync(CancellationToken cancellationToken)
    {
        SubsonicSong song;
        lock (_gate)
        {
            if (_submitted) return;
            song = _index >= 0 && _index < _queue.Count ? _queue[_index] : null;
            if (song == null) return;
            var duration = _durationSeconds ?? 0;
            if (duration <= 0) return;
            var played = _positionSeconds;
            if (played / duration < ScrobbleRatio && played < ScrobbleMinSeconds) return;
            _submitted = true;
        }

        if (_scrobble == null) return;
        try
        {
            if (await _scrobble(song.Id, true, cancellationToken).ConfigureAwait(false))
            {
                Log($"已提交收听：{song.Title}");
            }
        }
        catch (Exception ex)
        {
            Log($"收听计数提交失败（不阻塞播放）：{ex.Message}");
        }
    }
}

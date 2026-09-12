# AIPlayer.Shell.Services 公开 API 面（自动生成，勿手改）

> 生成方式：对编译产物 `AIPlayer.Shell.Services.dll` 反射枚举（`Assembly.GetExportedTypes()` + 公开成员）。
> 生成器：临时宿主 `SelfCheckHost.exe api <输出路径>`（宿主源码在 %TEMP%，不入库）。
> 用途：供外壳（t8/t12/t13）与验证（t9/t11）确认「有哪些类、哪些方法可调」，避免凭记忆猜 API。
> 程序集版本：1.0.0.0
> 公开类型数：191

## ⚠️ 两处「内核契约 DTO 面」在本程序集（别去 Models/HostContractModels.cs 找）

| 面 | 落点 | 内容 |
|---|---|---|
| 事件面（内核→外壳） | `AIPlayer.Shell.Services.Player.IPlayerController` / `HostEvent` / `HostEventKind` / `HostEventExtensions` | 9 类事件名逐字映射、`ExpectsSource`(6 类)、`AcceptsSourceReply`(含 `progress`)、`raw['x']` 读帮手 |
| 响应面（外壳→内核） | `AIPlayer.Shell.Services.Player.HostNavigateOptions`（`PlaybackRequest.cs`） | 22 属性，与内核 `WinUISample.Models.HostNavigateOptions` 一一对应 |

`Models/HostContractModels.cs` **只放「出参」编码模型**（`HostValueCodec` + 轨道/版本/剧集/弹幕 API）；
同族出参模型另见 `PlayerShortcuts.cs`(`--shortcuts=`) / `TodbModels.cs`(`--chapter=`/`--sprite=`) / `MediaSegmentDto.cs`(`--segment=`)。

**序列化红线**：`HostNavigateOptions` 是**不带 `[JsonPropertyName]` 的纯 DTO**（序列化归外壳层）。
用默认 `JsonSerializerOptions` 会输出 **PascalCase**，而内核按 `[JsonPropertyName("camelCase")]` **大小写敏感**反序列化
⇒ 结果是内核绑定到「全空 options」（静默）。二选一：**A** 直接用内核自己的 `HostNavigateOptions`（M1 链接内核源码，零风险，推荐）；
**B** 用本程序集 DTO 但必须显式 `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`。


## AIPlayer.Shell.Player

### `sealed class CoverArtUrlResolver`
- 构造：`new CoverArtUrlResolver(Object object, IntPtr method)`
- 方法：`BeginInvoke(String coverArtId, Int32 size, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(String coverArtId, Int32 size)`

### `sealed class EmbyPlaybackSession`
- 构造：`new EmbyPlaybackSession(EmbyService emby, AppSettings settings, SegmentService segmentService=…, TodbService todbService=…, Action<String> onLog=…, String playSessionId=…, Nullable<TimeSpan> progressMinInterval=…)`
- 方法：`AudioTracksOf(EmbyMediaSource source)`；`BadgesOf(EmbyItem target, EmbyMediaSource source)`；`BuildRequestAsync(EmbyItem target, Nullable<Double> startSeconds=…, IReadOnlyList<EmbyItem> episodeList=…, EmbyItem previous=…, EmbyItem next=…, CancellationToken cancellationToken=…)`；`DanmakuMatchNameOf(EmbyItem target)`；`EnsureEpisodesLoadedAsync(EmbyItem target, CancellationToken cancellationToken=…)`；`EpisodeListItems()`；`HideFromResumeAsync(Nullable<Boolean> hide=…, CancellationToken cancellationToken=…)`；`static NewPlaySessionId()`；`PlayAsync(EmbyItem target, Double startSeconds=…, CancellationToken cancellationToken=…)`；`PlayByIdAsync(String episodeId, CancellationToken cancellationToken=…)`；`PlayNeighborAsync(Int32 offset, CancellationToken cancellationToken=…)`；`PreloadAsync(String episodeId, CancellationToken cancellationToken=…)`；`RebuildAsync(Double resumeFrom, CancellationToken cancellationToken=…)`；`ReportProgressAsync(HostEvent hostEvent, Boolean force=…, CancellationToken cancellationToken=…)`；`ReportStartedAsync(PlaybackRequest request, Nullable<Double> position=…, CancellationToken cancellationToken=…)`；`ReportStoppedAsync(HostEvent hostEvent, CancellationToken cancellationToken=…)`；`ResolveNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken=…)`；`SegmentsForAsync(EmbyItem target, CancellationToken cancellationToken=…)`；`SetEpisodes(IReadOnlyList<EmbyItem> episodes)`；`SetItem(EmbyItem item)`；`StreamUrl(Nullable<Double> startSeconds=…)`；`SubtitleTracksOf(EmbyMediaSource source)`；`SubtitleUrlOf(EmbyMediaSource source)`；`SwitchVersionAsync(Nullable<Int32> versionIndex, CancellationToken cancellationToken=…)`；`TodbForAsync(EmbyItem target, CancellationToken cancellationToken=…)`；`ToggleFavoriteAsync(CancellationToken cancellationToken=…)`；`TogglePlayedAsync(CancellationToken cancellationToken=…)`
- 常量：`const PlayedThreshold = 0.92`
- 属性：`AudioStreamIndex{get}`；`CurrentSource{get}`；`DurationSeconds{get}`；`Emby{get}`；`Episodes{get}`；`IsMuted{get}`；`IsPaused{get}`；`Item{get}`；`LastPositionSeconds{get/set}`；`NeedsTranscode{get}`；`OnChanged{get/set}`；`PlaySessionId{get}`；`PlaybackFailed{get/set}`；`PlaybackRate{get}`；`ProgressMinInterval{get/set}`；`SegmentService{get}`；`Settings{get/set}`；`SubtitleStreamIndex{get}`；`TodbService{get}`；`VersionIndex{get/set}`；`VolumeLevel{get}`

### `sealed class HostEvent`
- 构造：`new HostEvent(HostEventKind kind, Dictionary<String,Object> raw=…, Nullable<DateTime> receivedAt=…)`
- 方法：`static KindOf(String name)`；`static Parse(IDictionary<String,Object> json)`；`ToString()`
- 属性：`AcceptsSourceReply{get}`；`DurationSeconds{get}`；`ExpectsSource{get}`；`IsPaused{get}`；`Kind{get}`；`PositionSeconds{get}`；`Raw{get}`；`ReceivedAt{get}`；`TargetEpisodeId{get}`；`VersionIndex{get}`

### `static class HostEventExtensions`
- 方法：`static BoolOf(IDictionary<String,Object> raw, String key)`；`static DoubleOf(IDictionary<String,Object> raw, String key)`；`static IntOf(IDictionary<String,Object> raw, String key)`；`static StringOf(IDictionary<String,Object> raw, String key)`

### `sealed enum HostEventKind`
- 取值：Progress, Stopped, ManualResize, NavigatePrevious, NavigateNext, NavigateEpisode, PreloadEpisode, SwitchVersion, RefreshPlaybackUrl, Unknown

### `sealed class HostNavigateOptions`
- 构造：`new HostNavigateOptions()`
- 属性：`AudioTracks{get/set}`；`BackdropUrl{get/set}`；`Badges{get/set}`；`CallbackUrl{get/set}`；`CurrentEpisodeId{get/set}`；`DanmakuMatchName{get/set}`；`HttpHeaders{get/set}`；`Logo{get/set}`；`MediaPath{get/set}`；`Monogram{get/set}`；`NewItemId{get/set}`；`NextEpisodeId{get/set}`；`PreviousEpisodeId{get/set}`；`SeasonId{get/set}`；`Segments{get/set}`；`StartPosition{get/set}`；`Subtitle{get/set}`；`SubtitleId{get/set}`；`SubtitleTracks{get/set}`；`SubtitleUrl{get/set}`；`Title{get/set}`；`VersionOptions{get/set}`

### `abstract interface IPlayerController`
- 方法：`PauseAsync(CancellationToken cancellationToken=…)`；`ResolveNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken=…)`；`ResumeAsync(CancellationToken cancellationToken=…)`；`SeekAsync(Double positionSeconds, CancellationToken cancellationToken=…)`；`SetMutedAsync(Boolean muted, CancellationToken cancellationToken=…)`；`SetPlaybackRateAsync(Double rate, CancellationToken cancellationToken=…)`；`SetVolumeAsync(Int32 level, CancellationToken cancellationToken=…)`；`StartAsync(PlaybackRequest request, String workingDirectory=…, CancellationToken cancellationToken=…)`；`StopAsync(CancellationToken cancellationToken=…)`
- 属性：`CurrentRequest{get}`；`DurationSeconds{get}`；`HostPid{get}`；`IsMuted{get}`；`IsRunning{get}`；`OnEvent{get/set}`；`OnNavigation{get/set}`；`PlaybackRate{get}`；`PositionSeconds{get}`；`State{get}`；`VolumeLevel{get}`

### `sealed class LyricsLoader`
- 构造：`new LyricsLoader(Object object, IntPtr method)`
- 方法：`BeginInvoke(SubsonicSong song, CancellationToken cancellationToken, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(SubsonicSong song, CancellationToken cancellationToken)`

### `sealed class MusicLyricsResult`
- 构造：`new MusicLyricsResult()`
- 属性：`AlbumName{get/set}`；`ArtistName{get/set}`；`Duration{get/set}`；`Instrumental{get/set}`；`IsEmpty{get}`；`PlainLyrics{get/set}`；`Source{get/set}`；`SyncedLyrics{get/set}`；`TrackName{get/set}`

### `sealed class MusicPlaybackSession`
- 构造：`new MusicPlaybackSession(StreamUrlResolver streamUrl, CoverArtUrlResolver coverArtUrl=…, ScrobbleCallback scrobbleCallback=…, LyricsLoader lyricsLoader=…, Action<String> onLog=…)`
- 方法：`BeginAsync(IReadOnlyList<SubsonicSong> songs, SubsonicSong song, CancellationToken cancellationToken=…)`；`HandleNavigationAsync(HostEvent hostEvent, CancellationToken cancellationToken=…)`；`OnHostEventAsync(HostEvent hostEvent, CancellationToken cancellationToken=…)`；`RequestFor(SubsonicSong song)`
- 常量：`const ScrobbleMinSeconds = 240`；`const ScrobbleRatio = 0.5`
- 属性：`Current{get}`；`CurrentLyrics{get}`；`DurationSeconds{get}`；`Index{get}`；`IsPaused{get}`；`LyricsLoading{get}`；`OnChanged{get/set}`；`PositionSeconds{get}`；`Queue{get}`

### `sealed class NavigationResolver`
- 构造：`new NavigationResolver(Object object, IntPtr method)`
- 方法：`BeginInvoke(HostEvent hostEvent, CancellationToken cancellationToken, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(HostEvent hostEvent, CancellationToken cancellationToken)`

### `sealed class PlaybackRequest`
- 构造：`new PlaybackRequest()`
- 方法：`static FitModeOf(String value)`；`SelectedAudioIndex()`；`SelectedSubtitleIndex()`；`ToNavigateOptions(String callbackUrl=…, String newItemId=…, String currentEpisodeId=…, Nullable<Double> resumePosition=…)`；`With(String mediaPath=…, Dictionary<String,String> httpHeaders=…, String title=…, String subtitle=…, Nullable<Double> startPosition=…, String monogram=…, String logo=…, String backdropUrl=…, List<String> badges=…, List<HostVersionOption> versionOptions=…, List<HostTrackOption> audioTracks=…, List<HostTrackOption> subtitleTracks=…, List<MediaSegmentDto> segments=…, List<HostChapter> chapters=…, HostSprite sprite=…, Boolean clearSprite=…, List<HostEpisodeItem> episodeList=…, String subtitleId=…, String subtitleUrl=…, String danmakuMatchName=…, String previousEpisodeId=…, Boolean clearPreviousEpisodeId=…, String nextEpisodeId=…, Boolean clearNextEpisodeId=…, String seasonId=…)`
- 属性：`AudioTracks{get/set}`；`BackdropUrl{get/set}`；`Badges{get/set}`；`Chapters{get/set}`；`DanmakuMatchName{get/set}`；`EpisodeList{get/set}`；`HttpHeaders{get/set}`；`Logo{get/set}`；`MediaPath{get/set}`；`Monogram{get/set}`；`NextEpisodeId{get/set}`；`PreviousEpisodeId{get/set}`；`SeasonId{get/set}`；`Segments{get/set}`；`Sprite{get/set}`；`StartPosition{get/set}`；`Subtitle{get/set}`；`SubtitleId{get/set}`；`SubtitleTracks{get/set}`；`SubtitleUrl{get/set}`；`Title{get/set}`；`VersionOptions{get/set}`

### `sealed enum PlayerState`
- 取值：Stopped, Playing, Paused, Buffering

### `sealed class ScrobbleCallback`
- 构造：`new ScrobbleCallback(Object object, IntPtr method)`
- 方法：`BeginInvoke(String songId, Boolean submission, CancellationToken cancellationToken, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(String songId, Boolean submission, CancellationToken cancellationToken)`

### `sealed class StreamUrlResolver`
- 构造：`new StreamUrlResolver(Object object, IntPtr method)`
- 方法：`BeginInvoke(String songId, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(String songId)`


## AIPlayer.Shell.Services

### `sealed class ServiceRegistry`
- 构造：`new ServiceRegistry(ShellHttpClient http=…, SecureKvStore credentials=…, JsonStorage settingsStorage=…, JsonStorage serversStorage=…)`
- 方法：`ApplyProxyFromSettings()`；`CreateAudioBookshelf(ServerConfig server, Action<String> onLog=…)`；`CreateAudioBookshelfById(String serverId, Action<String> onLog=…)`；`CreateEmby(ServerConfig server, Action<String> onLog=…)`；`CreateEmbyById(String serverId, Action<String> onLog=…)`；`CreateNavidrome(ServerConfig server, Action<String> onLog=…)`；`CreateNavidromeById(String serverId, Action<String> onLog=…)`；`CreateTrakt(String clientId=…, String clientSecret=…, Action<String> onLog=…)`；`CreateTraktTokenStore()`；`CreateWebDav(ServerConfig server, Action<String> onLog=…)`；`CreateWebDavById(String serverId, Action<String> onLog=…)`；`Dispose()`；`Initialize()`
- 属性：`Credentials{get}`；`Http{get}`；`Icons{get}`；`Segments{get}`；`Servers{get}`；`Settings{get}`；`Todb{get}`


## AIPlayer.Shell.Services.Aggregation

### `sealed class AggregatedSearchEntry`
- 构造：`new AggregatedSearchEntry(IEnumerable<AggregatedSearchHit> hits)`
- 属性：`HasAlternatives{get}`；`Hits{get}`；`Primary{get}`；`SourceLabel{get}`；`Sources{get}`

### `sealed class AggregatedSearchHit`
- 构造：`new AggregatedSearchHit()`
- 方法：`static FromEmbyItem(EmbyItem item, String serverId, String serverName, String imageUrl=…)`；`static KeyOf(AggregatedSearchHit hit)`
- 属性：`AlbumArtist{get/set}`；`AlbumId{get/set}`；`AlbumName{get/set}`；`Artists{get/set}`；`DurationSeconds{get}`；`Id{get/set}`；`ImageUrl{get/set}`；`IndexNumber{get/set}`；`Name{get/set}`；`Overview{get/set}`；`ParentIndexNumber{get/set}`；`PremiereDate{get/set}`；`ProductionYear{get/set}`；`ProviderIds{get/set}`；`Raw{get/set}`；`RunTimeTicks{get/set}`；`SeasonId{get/set}`；`SeasonName{get/set}`；`SeriesId{get/set}`；`SeriesName{get/set}`；`ServerId{get/set}`；`ServerName{get/set}`；`Type{get/set}`

### `sealed class AggregatedSearchProgress`
- 构造：`new AggregatedSearchProgress()`
- 方法：`FindByKey(String key)`；`IndexOfKey(String key)`；`ToString()`
- 属性：`ArrivedHitCount{get/set}`；`ArrivedServerName{get/set}`；`ArrivedSourceCount{get/set}`；`FailedServers{get}`；`FailedThisTime{get/set}`；`IsFinal{get/set}`；`ItemKeys{get}`；`Items{get}`；`MergedCount{get/set}`；`MergedHitCount{get/set}`；`PendingSourceCount{get/set}`；`SourceCount{get/set}`；`Term{get/set}`；`TotalHits{get/set}`

### `sealed class AggregatedSearchResult`
- 构造：`new AggregatedSearchResult()`
- 方法：`static FromItems(IEnumerable<AggregatedSearchHit> hits, String term=…, Int32 limit=…, IEnumerable<String> failedServers=…)`
- 属性：`FailedServers{get}`；`HasFailures{get}`；`IsEmpty{get}`；`Items{get}`；`MergedCount{get/set}`；`MergedHitCount{get/set}`；`SourceCount{get/set}`；`TotalHits{get/set}`

### `sealed class AggregatedSearchService`
- 构造：`new AggregatedSearchService(IEnumerable<IAggregatedSearchSource> sources, Action<String> onLog=…)`
- 方法：`SearchAsync(String term, Int32 limit=…, CancellationToken cancellationToken=…)`；`SearchIncrementalAsync(String term, Int32 limit=…, IProgress<AggregatedSearchProgress> progress=…, CancellationToken cancellationToken=…)`
- 常量：`const DefaultLimit = 60`
- 属性：`Sources{get}`

### `sealed class AggregatedSourceRef`
- 构造：`new AggregatedSourceRef()`
- 属性：`ItemId{get/set}`；`ServerId{get/set}`；`ServerName{get/set}`

### `sealed class EmbyAggregatedSearchSource`
- 构造：`new EmbyAggregatedSearchSource(EmbyService emby, String serverName=…)`
- 方法：`SearchAsync(String term, Int32 limit, CancellationToken cancellationToken)`
- 属性：`ServerId{get}`；`ServerName{get}`

### `abstract interface IAggregatedSearchSource`
- 方法：`SearchAsync(String term, Int32 limit, CancellationToken cancellationToken)`
- 属性：`ServerId{get}`；`ServerName{get}`


## AIPlayer.Shell.Services.AudioBookshelf

### `sealed class AbsBookSkipSettings`
- 构造：`new AbsBookSkipSettings()`
- 方法：`Clone()`；`static FromJson(Nullable<JsonElement> json)`；`static Of(Double introSeconds, Double creditsSeconds)`；`ToJson()`；`ToString()`
- 属性：`CreditsSeconds{get/set}`；`HasCredits{get}`；`HasIntro{get}`；`IntroSeconds{get/set}`；`IsEmpty{get}`；`SkipCredits{get/set}`；`SkipIntro{get/set}`；`UpdatedAt{get/set}`

### `sealed class AbsTimeline`
- 构造：`new AbsTimeline(IReadOnlyList<AbsAudioTrack> tracks)`
- 方法：`GlobalSecondsFromTrack(Int32 trackIndex, Double trackSeconds)`；`static GlobalSecondsFromTrack(IReadOnlyList<AbsAudioTrack> tracks, ValueTuple<Int32,Double> position)`；`static GlobalSecondsFromTrack(IReadOnlyList<AbsAudioTrack> tracks, Int32 trackIndex, Double trackSeconds)`；`static IsEmptyTracks(IReadOnlyList<AbsAudioTrack> tracks)`；`Locate(Double globalSeconds)`；`static Locate(IReadOnlyList<AbsAudioTrack> tracks, Double globalSeconds)`；`static LocateGlobalPosition(IReadOnlyList<AbsAudioTrack> tracks, Double globalSeconds)`；`LocateGlobalPosition(Double globalSeconds)`；`OffsetOf(Int32 index)`；`static OffsetOf(IReadOnlyList<AbsAudioTrack> tracks, Int32 index)`；`ToGlobal(Int32 trackIndex, Double secondsWithinTrack)`；`static TotalDurationOf(IReadOnlyList<AbsAudioTrack> tracks)`；`TrackAt(Int32 index)`；`static TrackAt(IReadOnlyList<AbsAudioTrack> tracks, Int32 index)`；`static TrackIndexOfServerIndex(IReadOnlyList<AbsAudioTrack> tracks, Int32 serverIndex)`
- 属性：`IsEmpty{get}`；`TotalDuration{get}`；`Tracks{get}`

### `sealed class AudioBookshelfService`
- 构造：`new AudioBookshelfService(ShellHttpClient http, ServerConfig server, Action<String> onLog=…, String clientName=…, String deviceName=…)`
- 方法：`CacheTracks(AbsBook book)`；`CloseSessionAsync(String sessionId, Double currentTime, Nullable<Double> duration=…, Nullable<Int32> trackIndex=…, CancellationToken cancellationToken=…)`；`CoverUrl(String itemId)`；`GetAuthorAsync(String authorId, CancellationToken cancellationToken=…)`；`GetAuthorsAsync(CancellationToken cancellationToken=…)`；`GetBackupsAsync(CancellationToken cancellationToken=…)`；`GetItemAsync(String itemId, CancellationToken cancellationToken=…)`；`GetItemsInProgressAsync(CancellationToken cancellationToken=…)`；`GetLibrariesAsync(CancellationToken cancellationToken=…)`；`GetLibraryAsync(String libraryId, CancellationToken cancellationToken=…)`；`GetLibraryItemsAsync(String libraryId, Int32 limit=…, Int32 page=…, String sort=…, Boolean desc=…, CancellationToken cancellationToken=…)`；`GetProgressAsync(String itemId, CancellationToken cancellationToken=…)`；`GetStreamUrl(String itemId, Int32 trackIndex)`；`GetStreamUrl(String itemId, IReadOnlyList<AbsAudioTrack> tracks, Int32 trackIndex)`；`HasCachedTracks(String itemId)`；`static LoginAsync(ShellHttpClient http, String baseUrl, String username, String password, CancellationToken cancellationToken=…)`；`ResolveTrackIndex(String itemId, Int32 serverTrackIndex)`；`SearchAsync(String query, CancellationToken cancellationToken=…)`；`SearchRawAsync(String query, CancellationToken cancellationToken=…)`；`StartPlaybackSessionAsync(String itemId, Nullable<Int32> trackIndex=…, Nullable<Double> startTime=…, CancellationToken cancellationToken=…)`；`SyncSessionAsync(String sessionId, Double currentTime, Nullable<Double> duration=…, Nullable<Int32> trackIndex=…, Boolean isPaused=…, Double playbackRate=…, CancellationToken cancellationToken=…)`；`ToString()`；`TrackStreamUrl(String itemId, AbsAudioTrack track)`；`TracksOf(String itemId)`；`UpdateProgressAsync(String itemId, AbsProgress progress, CancellationToken cancellationToken=…)`；`Uri(String path, IDictionary<String,String> query=…)`
- 常量：`const DefaultClientName = AI Player`；`const DefaultDeviceName = Windows`
- 属性：`BaseUrl{get}`；`CachedTrackCount{get}`；`ClientName{get}`；`DeviceName{get}`；`Headers{get}`；`HostHttpHeaders{get}`；`Http{get}`；`OnLog{get}`；`Server{get/set}`；`Token{get}`

### `sealed class AudiobookSkipStore`
- 构造：`new AudiobookSkipStore(JsonStorage storage)`
- 方法：`All()`；`static At(String filePath)`；`Clear()`；`CreditsStartSeconds(String bookId, Double durationSeconds)`；`Get(String bookId)`；`GetOrDefault(String bookId)`；`IntroSkipSeconds(String bookId)`；`Load()`；`Remove(String bookId)`；`static ResetInstance()`；`Save()`；`Set(String bookId, AbsBookSkipSettings settings)`；`Set(String bookId, Double introSeconds, Double creditsSeconds)`；`ToString()`
- 属性：`Count{get}`；`FilePath{get}`；`Instance{get}`；`IsLoaded{get}`


## AIPlayer.Shell.Services.Constants

### `static class AppConstants`
- 常量：`const AppDisplayName = AI Player`；`const ClientName = AI Player`；`const ClientVersion = 1.0.0`；`const CommunityTelegramUrl = https://t.me/aiemby`；`const DanmakuCacheMaxEntries = 8000`；`const DefaultImageMaxHeight = 480`；`const DefaultLanguage = zh-CN`；`const DefaultProxyUrl = 127.0.0.1:7890`；`const DefaultRetries = 1`；`const DeviceIdPrefix = aiplayer-`；`const DeviceName = Windows`；`const DiskCacheDefaultMaxBytes = 268435456`；`const DiskCacheDefaultMaxEntries = 4000`；`const DirectPlayAudioContainers = mp3,flac,aac,m4a,opus,ogg,wav,wma`；`const DirectPlayContainers = mkv,mp4,ts,m2ts,avi,mov,flv,wmv,webm,mp3,flac,aac,m4a,opus,ogg,wav`；`const HlsContainer = m3u8`；`const IconLibraryExampleUrl = https://example.com/icons.json`；`const ImageCacheDirectoryName = ai_player_images_v1`；`const ImageCacheMaxBytes = 536870912`；`const ImageNegativeCacheMaxEntries = 512`；`const ImageNegativeCacheNoImageSeconds = 600`；`const ImageNegativeCacheTransientSeconds = 30`；`const ImageCacheMaxEntries = 20000`；`const IntroDbUrl = https://api.introdb.app/segments`；`const LrcLibSearchUrl = https://lrclib.net/api/search`；`const MaxSegmentsPerArgument = 32`；`const MillisecondsPerSecond = 1000`；`const PlayedThresholdPercent = 90`；`const ProgressReportIntervalSeconds = 5`；`const ReportPath = /aiplayer/report/`；`const RetryBackoffMs = 300`；`const ScopedCacheDefaultTtlSeconds = 0`；`const ScopedCacheMaxEntries = 512`；`const SegmentOpenEndMs = -1`；`const SegmentTemplateUrl = https://example.com/api?imdb={imdb}&s={season}&e={episode}`；`const ServerIdPrefix = srv_`；`const SponsorUrl = https://afdian.com/a/aiplayer`；`static readonly String[] SubtitleFormats = [srt, ass, ssa]`；`const SwrCacheDefaultMaxBytes = 16777216`；`const SwrCacheDefaultMaxEntries = 200`；`const TheIntroDbUrl = https://api.theintrodb.org/v3/media`；`const TheOtherDbUrl = https://playback.theotherdb.org/api/metadata`；`const TicksPerMillisecond = 10000`；`const TicksPerSecond = 10000000`；`const TraktApiBase = https://api.trakt.tv`；`const TraktApiKeyHeader = trakt-api-key`；`const TraktApiVersion = 2`；`const TraktApiVersionHeader = trakt-api-version`；`const TraktDeviceCodeUrl = https://api.trakt.tv/oauth/device/code`；`const TraktDeviceTokenUrl = https://api.trakt.tv/oauth/device/token`；`const TraktOobUri = urn:ietf:wg:oauth:2.0:oob`；`const TraktRevokeUrl = https://api.trakt.tv/oauth/revoke`；`const TraktTokenUrl = https://api.trakt.tv/oauth/token`；`const UpdateManifestUrl = https://aiplayer.arkhamimp.qzz.io/downloads/version.json`；`static String UserAgent => UserAgentPolicy.Current`（t65 起为转发属性，非 `const`；旧字面量 `AIPlayer/1.0 (rebuilt shell)` 已删）


- **历史快照（已删常量，勿按"当前定义"引用；本行已不再登记它们）**：`DefaultTimeoutSeconds = 20` 与 `ImageTimeoutSeconds = 12` 由
  **t186**（`b482580`）删除；`ListTimeoutSeconds = 30` 由 **t212**（`da67419`）删除；`ImageCacheMaxAgeDays = 30` 由 **t158**（`6099af1`）删除。
  ⇒ 用这四个名字在当刻源码 `shell/Services/Constants/AppConstants.cs` 反查 **0 命中**（"常量还在"是假读数）。
  本节条目的当刻取值以源码为准，复核判据与两列对照见 `shell/Tests/evidence/t235-api-surface-constant-drift.txt`。

## AIPlayer.Shell.Services.Credentials

### `sealed class PasswordStore`
- 构造：`new PasswordStore(SecureKvStore vault)`
- 方法：`AllSecrets()`；`static At(String credentialsFilePath)`；`ClearAll()`；`GetSecret(String key)`；`GetServerPassword(String serverId)`；`GetServerRefreshToken(String serverId)`；`GetServerToken(String serverId)`；`Keys()`；`RemoveSecret(String key)`；`RemoveServer(String serverId)`；`RemoveServerPassword(String serverId)`；`Save()`；`static ServerPasswordKey(String serverId)`；`static ServerRefreshTokenKey(String serverId)`；`static ServerTokenKey(String serverId)`；`SetSecret(String key, String value)`；`SetServerPassword(String serverId, String password)`；`SetServerRefreshToken(String serverId, String token)`；`SetServerToken(String serverId, String token)`
- 属性：`DegradedReason{get}`；`Instance{get}`；`IsEncrypted{get}`；`Vault{get}`

### `sealed class SecureKvStore`
- 构造：`new SecureKvStore(String filePath)`
- 方法：`static At(String filePath)`；`Clear()`；`EnsureLoaded()`；`Flush()`；`static Protect(Byte[] plain)`；`Read(String key)`；`Remove(String key)`；`static Unprotect(Byte[] cipher)`；`Write(String key, String value)`
- 属性：`All{get}`；`DegradedReason{get}`；`DpapiSupported{get}`；`FilePath{get}`；`Instance{get}`；`IsEncrypted{get}`


## AIPlayer.Shell.Services.Emby

### `sealed class EmbyAuthResult`
- 构造：`new EmbyAuthResult()`
- 属性：`AccessToken{get/set}`；`ServerId{get/set}`；`ServerName{get/set}`；`UserId{get/set}`；`UserName{get/set}`

### `static class EmbyItemListMerge`
- 方法：`static DedupeWithinKey(IReadOnlyList<EmbyItemSource> bucket)`；`static KeyOf(EmbyItem item)`；`static KeyOfWith(EmbyItem item, Func<EmbyItem,String> externalIdSelector)`；`static Merge(IEnumerable<EmbyItem> first, IEnumerable<EmbyItem> second=…)`；`static MergeAll(IEnumerable`1[] lists)`；`static MergeSources(IEnumerable<EmbyItemSource> flat)`；`static MergeSourcesWithKey(Func<EmbyItem,String> keyOf, IEnumerable<IEnumerable`1> lists)`；`static MergeWithKey(Func<EmbyItem,String> keyOf, IEnumerable<IEnumerable`1> lists)`；`static PickPrimary(IReadOnlyList<EmbyItemSource> sources)`
- 属性：`KeyComparer{get}`

### `static class EmbyItemNavigation`
- 方法：`static CrossesSeason(EmbyItem from, EmbyItem to)`；`static CrossesSeries(EmbyItem a, EmbyItem b)`；`static IndexOf(EmbyItem current, IReadOnlyList<EmbyItem> sorted)`；`static IsFirst(EmbyItem current, IEnumerable<EmbyItem> episodes)`；`static IsLast(EmbyItem current, IEnumerable<EmbyItem> episodes)`；`static Next(EmbyItem current, IEnumerable<EmbyItem> episodes)`；`static Next(EmbyItem current, IEnumerable<EmbyItem> episodes, Func<EmbyItem,Boolean> predicate)`；`static Previous(EmbyItem current, IEnumerable<EmbyItem> episodes)`；`static Previous(EmbyItem current, IEnumerable<EmbyItem> episodes, Func<EmbyItem,Boolean> predicate)`；`static SameSeason(EmbyItem a, EmbyItem b)`；`static SameSeries(EmbyItem a, EmbyItem b)`；`static SortByEpisode(IEnumerable<EmbyItem> episodes)`；`static Step(EmbyItem current, IEnumerable<EmbyItem> episodes, Boolean forward, Func<EmbyItem,Boolean> predicate=…)`

### `sealed class EmbyItemSource`
- 构造：`new EmbyItemSource()`；`new EmbyItemSource(String source, EmbyItem item)`
- 属性：`Item{get/set}`；`Source{get/set}`

### `sealed class EmbyMergedItem`
- 构造：`new EmbyMergedItem(EmbyItem item, IReadOnlyList<EmbyItemSource> sources)`
- 方法：`ToString()`
- 属性：`HasAlternatives{get}`；`Item{get}`；`SourceLabel{get}`；`Sources{get}`

### `sealed class EmbyServerProfile`
- 构造：`new EmbyServerProfile()`
- 方法：`static FromJson(JsonNode node)`；`static FromPublicInfo(Nullable<JsonElement> json)`；`static ProbeAsync(ShellHttpClient http, String baseUrl, CancellationToken cancellationToken=…)`；`static ProbeAsync(EmbyService emby, CancellationToken cancellationToken=…)`；`ToString()`；`static TryProbeAsync(ShellHttpClient http, String baseUrl, CancellationToken cancellationToken=…)`
- 属性：`DisplayName{get}`；`HasInfo{get}`；`IsEmby{get}`；`IsJellyfin{get}`；`Kind{get}`；`OperatingSystem{get/set}`；`ProductName{get/set}`；`Raw{get/set}`；`ServerId{get/set}`；`ServerName{get/set}`；`SupportsMediaSegments{get}`；`SupportsPlaybackInfo{get}`；`Version{get/set}`；`VersionText{get}`

### `sealed class EmbyService`
- 构造：`new EmbyService(ShellHttpClient http, ServerConfig server, String deviceId=…, String deviceName=…, String clientName=…, String clientVersion=…, String language=…, Action<String> onLog=…)`
- 方法：`DirectStreamUrl(String itemId, String mediaSourceId=…, Nullable<Double> startSeconds=…, Nullable<Int32> audioStreamIndex=…, Nullable<Int32> subtitleStreamIndex=…)`；`GetAlbumsAsync(String artistId=…, String parentId=…, CancellationToken cancellationToken=…)`；`GetArtistsAsync(String parentId=…, Int32 limit=…, CancellationToken cancellationToken=…)`；`GetCountsAsync(CancellationToken cancellationToken=…)`；`GetEpisodesAsync(String seriesId, String seasonId=…, Nullable<Int32> startItemIndex=…, CancellationToken cancellationToken=…)`；`GetItemAsync(String itemId, String fields=…, CancellationToken cancellationToken=…)`；`GetItemsAsync(String parentId=…, String includeItemTypes=…, Boolean recursive=…, Int32 startIndex=…, Int32 limit=…, String sortBy=…, String sortOrder=…, String filters=…, String searchTerm=…, Nullable<Boolean> isPlayed=…, Nullable<Boolean> isFavorite=…, Boolean enableImages=…, Int32 imageTypeLimit=…, String anyProviderIdEquals=…, String fields=…, String genreIds=…, String personIds=…, Nullable<Int32> minCommunityRating=…, String studios=…, String years=…, String albumArtistIds=…, CancellationToken cancellationToken=…)`；`GetLatestAsync(String parentId=…, String includeItemTypes=…, Int32 limit=…, CancellationToken cancellationToken=…)`；`GetMediaSegmentsAsync(String itemId, CancellationToken cancellationToken=…)`；`GetNextUpAsync(String seriesId=…, String seasonId=…, Int32 limit=…, CancellationToken cancellationToken=…)`；`GetResumeAsync(String parentId=…, String mediaTypes=…, Int32 limit=…, CancellationToken cancellationToken=…)`；`GetSeasonsAsync(String seriesId, CancellationToken cancellationToken=…)`；`GetSimilarAsync(String itemId, Int32 limit=…, CancellationToken cancellationToken=…)`；`GetSongsAsync(String albumId=…, CancellationToken cancellationToken=…)`；`GetViewsAsync(CancellationToken cancellationToken=…)`；`ImageUrl(String itemId, String type=…, Nullable<Int32> maxHeight=…, Nullable<Int32> maxWidth=…, Nullable<Int32> index=…, String tag=…)`；`static LoginAsync(ShellHttpClient http, String baseUrl, String username, String password, String deviceId=…, String deviceName=…, String clientName=…, String clientVersion=…, CancellationToken cancellationToken=…)`；`PlaybackInfoAsync(String itemId, String mediaSourceId=…, Nullable<Int32> audioStreamIndex=…, Nullable<Int32> subtitleStreamIndex=…, Nullable<Double> startSeconds=…, Boolean autoOpenLiveStream=…, CancellationToken cancellationToken=…)`；`static PublicInfoAsync(ShellHttpClient http, String baseUrl, CancellationToken cancellationToken=…)`；`ReportPlaybackProgressAsync(String itemId, String playSessionId, Double positionSeconds, Boolean isPaused, String mediaSourceId=…, Int32 audioStreamIndex=…, Int32 subtitleStreamIndex=…, Boolean isMuted=…, Double playbackRate=…, Nullable<Int32> volumeLevel=…, CancellationToken cancellationToken=…)`；`ReportPlaybackStartAsync(String itemId, String playSessionId, String mediaSourceId=…, Double positionSeconds=…, Int32 audioStreamIndex=…, Int32 subtitleStreamIndex=…, Boolean isPaused=…, Boolean isMuted=…, Double playbackRate=…, Nullable<Int32> volumeLevel=…, CancellationToken cancellationToken=…)`；`ReportPlaybackStoppedAsync(String itemId, String playSessionId, Double positionSeconds, String mediaSourceId=…, Boolean isPaused=…, Boolean isMuted=…, Double playbackRate=…, CancellationToken cancellationToken=…)`；`SearchAsync(String term, Int32 limit=…, String includeItemTypes=…, CancellationToken cancellationToken=…)`；`ServerDomainsAsync(CancellationToken cancellationToken=…)`；`SetFavoriteAsync(String itemId, Boolean favorite, CancellationToken cancellationToken=…)`；`SetHideFromResumeAsync(String itemId, Boolean hide, CancellationToken cancellationToken=…)`；`SetPlayedAsync(String itemId, Boolean played, CancellationToken cancellationToken=…)`；`SubtitleUrl(String itemId, String mediaSourceId, Int32 streamIndex, String format=…)`；`TranscodeUrl(String itemId, String mediaSourceId=…)`；`Uri(String path, IDictionary<String,String> query=…)`
- 常量：`const DefaultSearchItemTypes = Movie,Series,Episode,BoxSet,Person,Video,MusicAlbum,Audio`；`const ItemFields = Overview,MediaSources,MediaStreams,ProviderIds,ChildCount,RecursiveItemCount,PrimaryImageAspectRatio,SeriesPrimaryImageTag,DateCreated,Path,Studios,People,Genres,Taglines,ProductionYear,PremiereDate,CommunityRating,OfficialRating,RunTimeTicks,SeriesStudio,SeriesName,SeasonName,IndexNumber,ParentIndexNumber`
- 属性：`AuthorizationHeader{get}`；`BaseUrl{get}`；`ClientName{get}`；`ClientVersion{get}`；`DefaultHeaders{get}`；`DeviceId{get}`；`DeviceName{get}`；`HostHttpHeaders{get}`；`Http{get}`；`Language{get}`；`OnLog{get}`；`Server{get/set}`；`Token{get}`；`UserId{get}`

### `sealed class EpisodeNavigationResult`
- 构造：`new EpisodeNavigationResult()`
- 方法：`ToString()`
- 属性：`CacheKey{get/set}`；`CrossesSeason{get/set}`；`Current{get/set}`；`Episode{get/set}`；`Found{get}`；`SeriesId{get/set}`；`Target{get/set}`

### `sealed class EpisodeOrder`
- 构造：`new EpisodeOrder(Nullable<Int32> season, Nullable<Int32> episode, Boolean isEpisode=…)`
- 方法：`CompareTo(EpisodeOrder other)`；`static Of(EmbyItem item)`；`ToString()`
- 属性：`Episode{get}`；`EpisodeKey{get}`；`IsEpisode{get}`；`Season{get}`；`SeasonKey{get}`


## AIPlayer.Shell.Services.ExternalMpv

### `sealed class ExternalMpvDecision`
- 构造：`new ExternalMpvDecision()`
- 方法：`ToString()`
- 属性：`Argument{get/set}`；`ExecutablePath{get/set}`；`Reason{get/set}`；`UseExternal{get/set}`

### `static class ExternalMpvIpc`
- 方法：`static InputIpcArgument(String instanceName)`；`static PipePath(String instanceName)`；`static SetPropertyCommand(String property, Object value)`；`static TrySendAsync(String pipePath, String commandJson, Int32 timeoutMs=…, CancellationToken cancellationToken=…)`

### `sealed class ExternalMpvService`
- 构造：`new ExternalMpvService(AppDataDir data, Action<String> log=…)`
- 方法：`ReadConfiguredPath()`；`Resolve(String configuredPath=…)`；`ResolveArgument(String configuredPath=…)`；`SaveConfiguredPath(String path)`
- 常量：`const SettingFileName = external-mpv.json`
- 属性：`SettingPath{get}`


## AIPlayer.Shell.Services.Http

### `sealed class ShellHttpClient`
- 构造：`new ShellHttpClient(String proxyUrl=…, Nullable<TimeSpan> defaultTimeout=…, Int32 defaultRetries=…, String userAgent=…)`
- 方法：`static BuildUri(String url, IDictionary<String,String> query)`；`CustomAsync(String method, String url, IDictionary<String,String> headers=…, Object body=…, Nullable<TimeSpan> timeout=…, CancellationToken cancellationToken=…)`；`DeleteAsync(String url, IDictionary<String,String> headers=…, CancellationToken cancellationToken=…)`；`Dispose()`；`GetAsync(String url, IDictionary<String,String> headers=…, IDictionary<String,String> query=…, Nullable<TimeSpan> timeout=…, Nullable<Int32> retries=…, CancellationToken cancellationToken=…)`；`PostJsonAsync(String url, Object body, IDictionary<String,String> headers=…, Nullable<TimeSpan> timeout=…, Nullable<Int32> retries=…, CancellationToken cancellationToken=…)`；`PutAsync(String url, IDictionary<String,String> headers=…, Object body=…, Nullable<TimeSpan> timeout=…, CancellationToken cancellationToken=…)`；`SendAsync(String method, String url, IDictionary<String,String> headers=…, Object body=…, Nullable<TimeSpan> timeout=…, Nullable<Int32> retries=…, CancellationToken cancellationToken=…)`
- 属性：`DefaultRetries{get/set}`；`DefaultTimeout{get/set}`；`ProxyUrl{get/set}`；`UserAgent{get/set}`

### `sealed class ShellHttpException`
- 构造：`new ShellHttpException(String message, Nullable<Int32> statusCode=…, String url=…)`
- 属性：`StatusCode{get}`；`Url{get}`

### `sealed class ShellHttpResult`
- 构造：`new ShellHttpResult(Int32 statusCode, Byte[] bytes, Dictionary<String,List`1> headers, String requestUrl)`
- 方法：`Header(String name)`；`ToString()`
- 属性：`Body{get}`；`Bytes{get}`；`Headers{get}`；`IsSuccess{get}`；`Json{get}`；`JsonList{get}`；`JsonMap{get}`；`RequestUrl{get}`；`StatusCode{get}`


## AIPlayer.Shell.Services.Icons

### `static class IconLibrary`

### `sealed class IconLibraryStore`
- 构造：`new IconLibraryStore(ShellHttpClient http, JsonStorage storage=…, Action<String> onLog=…)`
- 方法：`AddCustom(IconLibraryEntry entry)`；`FetchSourceAsync(String sourceUrl, CancellationToken cancellationToken=…)`；`Load()`；`LoadAsync()`；`RemoveCustom(String id)`；`Save()`；`SaveAsync()`
- 属性：`Custom{get}`；`Sources{get}`


## AIPlayer.Shell.Services.Infra

### `sealed class AppDataDir`
- 构造：`new AppDataDir(String root, Boolean isPortable, String legacyRoot=…, Boolean canAutoMigrate=…)`
- 方法：`BackupPreExistingForeignFiles()`；`ExistingOriginalAccountsFile()`；`File(String name)`；`Log(String message)`；`MigrateLegacyDataIfNeeded()`；`OriginalAccountsFileCandidates()`；`static Override(AppDataDir dir)`；`static Reset()`
- 常量：`const AppFolderName = AIPlayer`；`const MigratedMarker = .migrated_shell`；`const OriginalAccountsFileName = accounts.json`
- 属性：`BackupDir{get}`；`CacheDir{get}`；`CanAutoMigrate{get}`；`CredentialsFile{get}`；`DanmakuCacheDir{get}`；`HostExe{get}`；`IconsFile{get}`；`Instance{get}`；`IsPortable{get}`；`KernelDanmakuCacheDir{get}`；`LegacyRoot{get}`；`LegacyServersFile{get}`；`LibMpvDll{get}`；`LocalRoot{get}`；`LogFile{get}`；`LyricsCacheDir{get}`；`MpvConfigDir{get}`；`PlayerDir{get}`；`Root{get}`；`ServersFile{get}`；`SettingsFile{get}`；`SkipCacheFile{get}`

### `sealed class AudioPlaybackState`
- 构造：`new AudioPlaybackState()`
- 方法：`Clone()`；`ToString()`
- 属性：`AudioLanguage{get/set}`；`AudioStreamIndex{get/set}`；`Muted{get/set}`；`Volume{get/set}`

### `sealed class AudioPlaybackStateStore`
- 构造：`new AudioPlaybackStateStore(AppDataDir data, Action<String> log=…)`
- 方法：`AdjustVolume(String serverId, Double delta)`；`Get(String serverId)`；`PathFor(String serverId)`；`Set(String serverId, AudioPlaybackState state)`；`SetAudioTrack(String serverId, Nullable<Int32> audioStreamIndex, String language=…)`；`SetVolume(String serverId, Double volume)`

### `sealed class CacheGroupStats`
- 构造：`new CacheGroupStats()`
- 方法：`ToString()`
- 属性：`Bytes{get/set}`；`Count{get/set}`；`Directory{get/set}`；`Name{get/set}`

### `sealed class CacheStats`
- 构造：`new CacheStats()`
- 方法：`ToString()`
- 属性：`Bytes{get/set}`；`Count{get/set}`；`Directory{get/set}`；`NewestUtc{get/set}`；`OldestUtc{get/set}`

### `sealed class CacheStatsService`
- 构造：`new CacheStatsService(AppDataDir data)`
- 方法：`All()`；`ClearShellCaches()`；`PruneShellCaches()`；`Report()`

### `sealed class ConfigPortService`
- 构造：`new ConfigPortService(AppDataDir data, Action<String> log=…)`
- 方法：`static IsFree(Int32 port)`；`Resolve(Int32 min=…, Int32 max=…)`
- 常量：`const FileName = callback-port.json`
- 属性：`CallbackUrl{get}`；`FilePath{get}`；`Port{get}`

### `sealed class DanmakuDiskCacheStore`
- 构造：`new DanmakuDiskCacheStore(AppDataDir data, Int64 maxBytes=…, Int32 maxEntries=…, Action<String> log=…)`
- 方法：`Clear()`；`Contains(String matchName)`；`GetOrFetchAsync(String matchName, Func<Task`1> fetch, CancellationToken cancellationToken=…)`；`PathForMatch(String matchName)`；`Prune()`；`Put(String matchName, String json)`；`Stats()`；`TryGet(String matchName)`
- 属性：`Directory{get}`；`FetchCount{get}`；`HitCount{get}`

### `sealed class DataMigrationReport`
- 构造：`new DataMigrationReport()`
- 方法：`ToString()`
- 属性：`Copied{get/set}`；`Failed{get/set}`；`IsIdempotentRun{get}`；`Notes{get}`；`Skipped{get/set}`；`SourceFiles{get/set}`；`TargetFilesAfter{get/set}`；`TargetFilesBefore{get/set}`

### `sealed class DataMigrationService`
- 构造：`new DataMigrationService(String sourceRoot, String targetRoot, Action<String> log=…)`
- 方法：`Run(Boolean overwrite=…)`
- 属性：`SourceRoot{get}`；`TargetRoot{get}`

### `sealed class DeviceIdService`
- 构造：`new DeviceIdService(AppDataDir data, Action<String> log=…)`
- 方法：`BackupTo(String backupPath)`；`GetOrCreate()`；`RestoreFrom(String backupPath)`；`TryRead()`
- 常量：`const FileName = device-id.txt`；`const MinLength = 8`
- 属性：`FilePath{get}`

### `sealed class DiskCacheStore`
- 构造：`new DiskCacheStore(String directory, Int64 maxBytes=…, Int32 maxEntries=…, Action<String> log=…)`
- 方法：`Clear()`；`Contains(String key)`；`PathFor(String key)`；`Prune()`；`Remove(String key)`；`Stats()`；`TryRead(String key)`；`Write(String key, Byte[] data)`
- 属性：`Directory{get}`；`MaxBytes{get}`；`MaxEntries{get}`

### `sealed class ImageCacheManager`
- 构造：`new ImageCacheManager(AppDataDir data, Int64 maxBytes=…, Int32 maxEntries=…, Action<String> log=…)`
- 方法：`Clear()`；`GetOrFetchAsync(String key, Func<Task`1> fetch, CancellationToken cancellationToken=…)`；`Prune()`；`Stats()`；`TryGet(String key)`
- 常量：`const SubDirectory = images`
- 属性：`Store{get}`

### `sealed class ImeStateService`
- 构造：`new ImeStateService()`
- 方法：`BeginComposition(String text=…)`；`EndComposition(String committed=…)`；`Reset()`；`UpdateComposition(String text)`
- 属性：`CompositionText{get}`；`IsComposing{get}`；`LastCommitted{get}`；`ShouldClearInputOnEscape{get}`；`ShouldTriggerSearchOnEnter{get}`

### `sealed class ProxySetting`
- 构造：`new ProxySetting()`
- 方法：`ToString()`
- 属性：`Bypass{get/set}`；`Enabled{get/set}`；`IsUsable{get}`；`Server{get/set}`

### `static class WindowsProxy`
- 方法：`static Read()`；`static ResolveKernelArgument()`；`static ToKernelArgument(ProxySetting setting)`


## AIPlayer.Shell.Services.Logging

### `static class DebugLog`
- 方法：`static Error(String message)`；`static Info(String message)`；`static Warn(String message)`
- 属性：`Entries{get}`；`PlainText{get}`

### `sealed class LogEntry`
- 构造：`new LogEntry(DateTime time, String message, String level=…)`
- 方法：`ToLogLine()`
- 属性：`Formatted{get}`；`Level{get}`；`Message{get}`；`Time{get}`

### `sealed class PlayerLogService`
- 构造：`new PlayerLogService(Action<String> fileSink=…)`
- 方法：`Add(String message, String level=…)`；`Clear()`；`static Create(Action<String> fileSink)`；`Error(String message)`；`static Info(String message)`；`static LogError(String message)`；`static LogWarning(String message)`；`Warn(String message)`
- 常量：`const MaxEntries = 600`
- 属性：`Entries{get}`；`Instance{get}`；`PlainText{get}`


## AIPlayer.Shell.Services.Lyrics

### `sealed class LyricsService`
- 构造：`new LyricsService(ShellHttpClient http, Action<String> onLog=…)`
- 方法：`static CacheFile(String key)`；`static CacheKey(String trackName, String artistName, String albumName)`；`ClearCache()`；`ClearCacheAsync()`；`FindAsync(String trackName, String artistName=…, String albumName=…, Nullable<Double> durationSeconds=…, Func<String,String,CancellationToken,Task`1> subsonicLyricsFetcher=…, String subsonicSongId=…, String subsonicServerId=…, CancellationToken cancellationToken=…)`
- 常量：`const BaseUrl = https://lrclib.net/api`
- 属性：`Http{get}`；`OnLog{get}`


## AIPlayer.Shell.Services.Mock

### `sealed class MockEmbyServer`
- 构造：`new MockEmbyServer()`
- 方法：`CountRequests(String method, String pathPrefix)`；`Dispose()`；`Start()`
- 常量：`const AccessToken = mock-access-token-0001`；`const EpisodeId = episode-1`；`const MovieId = movie-1`；`const MoviesViewId = view-movies`；`const SeriesId = series-1`；`const ServerId = mock-server-id`；`const TvViewId = view-tv`；`const UserId = user-1`；`const ValidPassword = demo-password`；`const ValidUser = demo`
- 属性：`BaseUrl{get}`；`Port{get}`；`Requests{get}`；`UnauthorizedCount{get}`

### `sealed class MockHttpServer`
- 构造：`new MockHttpServer(Func<MockRequest,MockResponse> handler)`
- 方法：`Dispose()`；`Start()`
- 属性：`BaseUrl{get}`；`Port{get}`；`Requests{get}`

### `sealed class MockNavidromeServer`
- 构造：`new MockNavidromeServer()`
- 方法：`Dispose()`；`static Md5Hex(String input)`；`Start()`
- 常量：`const AlbumId = al-1`；`const NativeToken = nd-native-token`；`const SongId = sg-1`；`const UserId = nd-user-1`；`const ValidPassword = nd-secret`；`const ValidUser = demo`
- 属性：`AuthFailureCount{get}`；`BaseUrl{get}`；`Requests{get}`

### `sealed class MockRequest`
- 构造：`new MockRequest()`
- 方法：`Header(String name)`；`QueryValue(String name)`；`ToString()`
- 属性：`Body{get/set}`；`BodyBytes{get/set}`；`Headers{get/set}`；`Method{get/set}`；`Path{get/set}`；`Query{get/set}`；`RawQuery{get/set}`

### `sealed class MockResponse`
- 构造：`new MockResponse()`
- 方法：`static Empty(Int32 statusCode=…)`；`static Json(String json, Int32 statusCode=…)`；`static Text(String text, Int32 statusCode=…)`
- 属性：`Body{get/set}`；`ContentType{get/set}`；`StatusCode{get/set}`

### `sealed class MockWebDavServer`
- 构造：`new MockWebDavServer()`
- 方法：`CountRequests(String method)`；`Dispose()`；`Start()`
- 常量：`const ValidPassword = dav-secret`；`const ValidUser = dav`
- 属性：`AuthFailureCount{get}`；`BaseUrl{get}`；`Requests{get}`


## AIPlayer.Shell.Services.Models

### `sealed class AbsAudioTrack`
- 构造：`new AbsAudioTrack()`
- 方法：`static FromJson(Nullable<JsonElement> json, Int32 index)`；`ToString()`
- 属性：`Duration{get/set}`；`EndOffset{get}`；`Index{get/set}`；`Ino{get/set}`；`MimeType{get/set}`；`StartOffset{get/set}`；`Title{get/set}`

### `sealed class AbsAuth`
- 构造：`new AbsAuth()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Token{get/set}`；`UserId{get/set}`；`UserName{get/set}`

### `sealed class AbsAuthor`
- 构造：`new AbsAuthor()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Id{get/set}`；`Name{get/set}`；`NameLastFirst{get/set}`；`NumBooks{get/set}`

### `sealed class AbsBook`
- 构造：`new AbsBook()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Abridged{get/set}`；`Author{get/set}`；`CoverPath{get/set}`；`Description{get/set}`；`Duration{get/set}`；`EffectiveDuration{get}`；`Explicit{get/set}`；`Genres{get/set}`；`Id{get/set}`；`Isbn{get/set}`；`Language{get/set}`；`Narrator{get/set}`；`PublishedYear{get/set}`；`SeriesName{get/set}`；`SeriesSequence{get/set}`；`Subtitle{get}`；`TimelineTotalDuration{get}`；`Title{get/set}`；`Tracks{get/set}`

### `sealed class AbsLibrary`
- 构造：`new AbsLibrary()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Folders{get/set}`；`Icon{get/set}`；`Id{get/set}`；`MediaType{get/set}`；`Name{get/set}`

### `sealed class AbsLocation`
- 构造：`new AbsLocation(Int32 trackIndex, Double secondsWithinTrack)`
- 方法：`ToString()`
- 属性：`SecondsWithinTrack{get}`；`TrackIndex{get}`

### `sealed class AbsProgress`
- 构造：`new AbsProgress()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToJson()`；`ToString()`
- 属性：`CurrentTime{get/set}`；`Duration{get/set}`；`FinishedAt{get/set}`；`IsFinished{get/set}`；`LastUpdate{get/set}`；`ListenedSeconds{get}`；`Progress{get/set}`；`StartedAt{get/set}`

### `sealed class AbsSearchResult`
- 构造：`new AbsSearchResult()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Authors{get/set}`；`Books{get/set}`；`Podcasts{get/set}`；`Series{get/set}`

### `sealed class AbsSeries`
- 构造：`new AbsSeries()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToString()`
- 属性：`Id{get/set}`；`Name{get/set}`

### `sealed class AppSettings`
- 构造：`new AppSettings()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`；`With(String hostExeOverride=…, Boolean clearHostExeOverride=…, String libMpvOverride=…, Boolean clearLibMpvOverride=…, Nullable<Boolean> useLaunchFile=…, String videoFitMode=…, Nullable<Boolean> fitVideoSize=…, Nullable<Int32> maxVolume=…, Nullable<Boolean> rtxVsr=…, Nullable<Boolean> rtxVideoHdr=…, String animeMode=…, String sharpenMode=…, Nullable<Boolean> autoPlayNextEpisode=…, Nullable<Boolean> skipFeatureEnabled=…, HostShortcuts shortcuts=…, Nullable<Boolean> proxyEnabled=…, String proxyUrl=…, String preferredSubtitleLanguage=…, Nullable<Boolean> autoSelectSubtitle=…, Nullable<Boolean> danmakuEnabled=…, List<DanmakuApiConfig> danmakuApis=…, String danmakuMatchTemplate=…, Nullable<Boolean> skipIntroEnabled=…, Nullable<Boolean> skipCreditsEnabled=…, SkipSourceSettings skipSources=…, Nullable<Boolean> lyricsEnabled=…, Nullable<Boolean> traktEnabled=…, String traktClientId=…, String traktClientSecret=…, Nullable<Boolean> autoBackupEnabled=…, String autoBackupServerId=…, Nullable<Int32> autoBackupIntervalHours=…, Nullable<Int32> autoBackupKeepCount=…, String themeMode=…, Nullable<UInt32> accentColor=…, Nullable<Boolean> acrylicEnabled=…, Nullable<Boolean> rememberWindowBounds=…, Nullable<Boolean> minimizeToTrayOnClose=…, Nullable<Int32> windowX=…, Nullable<Int32> windowY=…, Nullable<Int32> windowWidth=…, Nullable<Int32> windowHeight=…, Nullable<Boolean> windowMaximized=…, Nullable<Boolean> checkUpdateOnStart=…, String lastServerId=…, Nullable<Int32> libraryPageSize=…, Nullable<Boolean> mergeServerLibraries=…)`
- 常量：`const DefaultAccentColor = 4279200957`；`const DefaultProxyUrl = http://127.0.0.1:7890`
- 属性：`AccentColor{get/set}`；`AcrylicEnabled{get/set}`；`ActiveDanmakuApis{get}`；`AnimeMode{get/set}`；`AutoBackupEnabled{get/set}`；`AutoBackupIntervalHours{get/set}`；`AutoBackupKeepCount{get/set}`；`AutoBackupServerId{get/set}`；`AutoPlayNextEpisode{get/set}`；`AutoSelectSubtitle{get/set}`；`CheckUpdateOnStart{get/set}`；`DanmakuApis{get/set}`；`DanmakuEnabled{get/set}`；`DanmakuMatchTemplate{get/set}`；`DisableSkipMarkers{get}`；`FitVideoSize{get/set}`；`HostExeOverride{get/set}`；`LastServerId{get/set}`；`LibMpvOverride{get/set}`；`LibraryPageSize{get/set}`；`LyricsEnabled{get/set}`；`MaxVolume{get/set}`；`MergeServerLibraries{get/set}`；`MinimizeToTrayOnClose{get/set}`；`PreferredSubtitleLanguage{get/set}`；`ProxyEnabled{get/set}`；`ProxyUrl{get/set}`；`RememberWindowBounds{get/set}`；`RtxVideoHdr{get/set}`；`RtxVsr{get/set}`；`SharpenMode{get/set}`；`Shortcuts{get/set}`；`SkipCreditsEnabled{get/set}`；`SkipFeatureEnabled{get/set}`；`SkipIntroEnabled{get/set}`；`SkipSources{get/set}`；`ThemeMode{get/set}`；`TraktClientId{get/set}`；`TraktClientSecret{get/set}`；`TraktEnabled{get/set}`；`UseLaunchFile{get/set}`；`VideoFitMode{get/set}`；`WindowHeight{get/set}`；`WindowMaximized{get/set}`；`WindowWidth{get/set}`；`WindowX{get/set}`；`WindowY{get/set}`

### `sealed class AuthSession`
- 构造：`new AuthSession()`
- 方法：`static FromAbsJson(JsonElement json, String serverId)`；`static FromEmbyJson(JsonElement json, String serverId)`；`static FromNavidromeJson(JsonElement json, String serverId)`
- 属性：`AccessToken{get/set}`；`DeviceId{get/set}`；`IsAuthenticated{get}`；`Raw{get/set}`；`RefreshToken{get/set}`；`ServerId{get/set}`；`UserId{get/set}`；`UserName{get/set}`

### `sealed class BackupBundle`
- 构造：`new BackupBundle()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`；`static ToJsonArray(List<JsonNode> items)`；`static ToJsonObject(Dictionary<String,JsonNode> map)`
- 属性：`CreatedAt{get/set}`；`Icons{get/set}`；`Schema{get/set}`；`Servers{get/set}`；`Settings{get/set}`；`SkipCache{get/set}`

### `sealed class DanmakuApiConfig`
- 构造：`new DanmakuApiConfig()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`
- 属性：`Enabled{get/set}`；`Name{get/set}`；`Url{get/set}`

### `sealed class EmbyItem`
- 构造：`new EmbyItem()`
- 方法：`static FromJson(JsonElement json)`；`WithUserData(EmbyUserData data)`
- 属性：`AlbumArtist{get/set}`；`AlbumId{get/set}`；`Artists{get/set}`；`BackdropImageTags{get/set}`；`ChildCount{get/set}`；`CollectionType{get/set}`；`CommunityRating{get/set}`；`Container{get/set}`；`DurationSeconds{get}`；`EpisodeCodeText{get}`；`Genres{get/set}`；`Id{get/set}`；`ImageTags{get/set}`；`ImdbId{get}`；`IndexNumber{get/set}`；`IsAudio{get}`；`IsBoxSet{get}`；`IsContainer{get}`；`IsEpisode{get}`；`IsFolder{get/set}`；`IsMovie{get}`；`IsPerson{get}`；`IsPlayable{get}`；`IsSeason{get}`；`IsSeries{get}`；`MediaSources{get/set}`；`Name{get/set}`；`OfficialRating{get/set}`；`Overview{get/set}`；`ParentBackdropItemId{get/set}`；`ParentIndexNumber{get/set}`；`Path{get/set}`；`PremiereDate{get/set}`；`PrimaryImageAspectRatio{get/set}`；`PrimaryImageTag{get}`；`ProductionYear{get/set}`；`ProviderIds{get/set}`；`Raw{get/set}`；`RecursiveItemCount{get/set}`；`RunTimeTicks{get/set}`；`SeasonId{get/set}`；`SeasonName{get/set}`；`SeriesId{get/set}`；`SeriesName{get/set}`；`SeriesPrimaryImageTag{get/set}`；`SubtitleText{get}`；`TmdbId{get}`；`TvdbId{get}`；`Type{get/set}`；`UserData{get/set}`

### `sealed class EmbyMediaSource`
- 构造：`new EmbyMediaSource()`
- 方法：`static FromJson(JsonElement json)`
- 属性：`AudioStreams{get}`；`Bitrate{get/set}`；`Container{get/set}`；`DirectStreamUrl{get/set}`；`DurationSeconds{get}`；`ETag{get/set}`；`Id{get/set}`；`MediaStreams{get/set}`；`Name{get/set}`；`Path{get/set}`；`Protocol{get/set}`；`RunTimeTicks{get/set}`；`Size{get/set}`；`SubtitleStreams{get}`；`SupportsDirectPlay{get/set}`；`SupportsDirectStream{get/set}`；`TranscodingUrl{get/set}`；`VersionLabel{get}`

### `sealed class EmbyMediaStream`
- 构造：`new EmbyMediaStream()`
- 方法：`static FromJson(JsonElement json)`
- 属性：`BitRate{get/set}`；`Channels{get/set}`；`Codec{get/set}`；`DeliveryUrl{get/set}`；`DisplayTitle{get/set}`；`Height{get/set}`；`Index{get/set}`；`IsAudio{get}`；`IsDefault{get/set}`；`IsExternal{get/set}`；`IsForced{get/set}`；`IsSubtitle{get}`；`IsTextSubtitle{get/set}`；`IsVideo{get}`；`Label{get}`；`Language{get/set}`；`Path{get/set}`；`Title{get/set}`；`Type{get/set}`；`Width{get/set}`

### `sealed class EmbyQueryResult`
- 构造：`new EmbyQueryResult()`
- 方法：`static FromJson(JsonElement json)`
- 属性：`HasMore{get}`；`Items{get/set}`；`TotalRecordCount{get/set}`

### `sealed class EmbyUserData`
- 构造：`new EmbyUserData()`
- 方法：`static FromJson(Nullable<JsonElement> json)`；`ToJson()`；`With(Nullable<Boolean> played=…, Nullable<Int64> positionTicks=…, Nullable<Int64> playbackPositionTicks=…, Nullable<Boolean> isFavorite=…, Nullable<Double> playedPercentage=…, Nullable<Int32> unplayedItemCount=…)`
- 属性：`IsFavorite{get/set}`；`PlaybackPositionTicks{get/set}`；`Played{get/set}`；`PlayedPercentage{get/set}`；`PositionSeconds{get}`；`PositionTicks{get/set}`；`ResumeTicks{get}`；`UnplayedItemCount{get/set}`

### `sealed class EmbyUserView`
- 构造：`new EmbyUserView()`
- 方法：`static FromJson(JsonElement json)`
- 属性：`CollectionType{get/set}`；`Id{get/set}`；`ImageTags{get/set}`；`Name{get/set}`

### `static class EnhancementModes`

### `sealed class HostChapter`
- 构造：`new HostChapter()`
- 方法：`static FromJson(JsonElement json)`；`ToBase64()`；`ToJson()`
- 属性：`MarkerId{get/set}`；`MarkerType{get/set}`；`TimeEnd{get/set}`；`TimeStart{get/set}`；`Title{get/set}`

### `sealed class HostDanmakuApi`
- 构造：`new HostDanmakuApi()`
- 方法：`ToBase64()`；`ToJson()`
- 属性：`Name{get/set}`；`Url{get/set}`

### `sealed class HostEpisodeItem`
- 构造：`new HostEpisodeItem()`
- 方法：`ToJson()`
- 属性：`Id{get/set}`；`IsCurrent{get/set}`；`IsPlayed{get/set}`；`Label{get/set}`

### `sealed class HostShortcuts`
- 构造：`new HostShortcuts()`
- 方法：`static FromJson(JsonElement json)`；`ToBase64()`；`ToJson()`；`With(Nullable<Int32> playPause=…, Nullable<Int32> seekForward=…, Nullable<Int32> seekBackward=…, Nullable<Int32> volumeUp=…, Nullable<Int32> volumeDown=…, Nullable<Int32> toggleFullscreen=…, Nullable<Int32> toggleMute=…, Nullable<Int32> nextEpisode=…, Nullable<Int32> previousEpisode=…, Nullable<Int32> speedUp=…, Nullable<Int32> speedDown=…, Nullable<Int32> resetSpeed=…, Nullable<Int32> seekForwardSeconds=…, Nullable<Int32> seekBackwardSeconds=…)`
- 属性：`NextEpisode{get/set}`；`PlayPause{get/set}`；`PreviousEpisode{get/set}`；`ResetSpeed{get/set}`；`SeekBackward{get/set}`；`SeekBackwardSeconds{get/set}`；`SeekForward{get/set}`；`SeekForwardSeconds{get/set}`；`SpeedDown{get/set}`；`SpeedUp{get/set}`；`ToggleFullscreen{get/set}`；`ToggleMute{get/set}`；`VolumeDown{get/set}`；`VolumeUp{get/set}`

### `sealed class HostSprite`
- 构造：`new HostSprite()`
- 方法：`static FromJson(JsonElement json)`；`ToBase64()`；`ToJson()`
- 属性：`Height{get/set}`；`SpriteId{get/set}`；`VttUrl{get/set}`；`Width{get/set}`

### `sealed class HostTrackOption`
- 构造：`new HostTrackOption()`
- 方法：`ToBase64()`；`ToJson()`
- 属性：`EmbyIndex{get/set}`；`External{get/set}`；`Id{get/set}`；`Label{get/set}`；`Language{get/set}`；`Selected{get/set}`；`Special{get/set}`；`Title{get/set}`；`Url{get/set}`

### `static class HostValueCodec`
- 方法：`static DecodeHostValue(String encoded)`；`static EncodeHostValue(Object value)`

### `sealed class HostVersionOption`
- 构造：`new HostVersionOption()`
- 方法：`ToBase64()`；`ToJson()`
- 属性：`Id{get/set}`；`Index{get/set}`；`Label{get/set}`；`Selected{get/set}`

### `sealed enum HostVideoFitMode`
- 取值：Contain, Cover, Stretch

### `sealed class IconLibraryEntry`
- 构造：`new IconLibraryEntry()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`；`ToString()`
- 属性：`Id{get/set}`；`IsBuiltin{get}`；`Name{get/set}`；`Source{get/set}`；`Url{get/set}`

### `sealed class LyricLine`
- 构造：`new LyricLine(String text, Int64 startMs)`
- 方法：`ToString()`
- 属性：`StartMs{get/set}`；`Text{get/set}`；`Timestamp{get}`

### `sealed class LyricsResult`
- 构造：`new LyricsResult(String trackName=…, String artistName=…, String albumName=…, Double duration=…, Boolean instrumental=…, String plainLyrics=…, String syncedLyrics=…, String source=…)`
- 方法：`static FromLrclibJson(Nullable<JsonElement> json)`；`static ParseLrc(String raw)`
- 属性：`AlbumName{get/set}`；`ArtistName{get/set}`；`Duration{get/set}`；`Instrumental{get/set}`；`IsEmpty{get}`；`IsSynced{get}`；`Lines{get}`；`Lrc{get}`；`PlainLyrics{get/set}`；`Source{get/set}`；`SyncedLyrics{get/set}`；`TrackName{get/set}`

### `sealed class MediaSegmentDto`
- 构造：`new MediaSegmentDto()`
- 方法：`static FromJson(JsonElement json, String defaultSource=…)`；`static ParseType(String typeText)`；`ToBase64()`；`ToJson()`
- 属性：`End{get}`；`EndMs{get/set}`；`IsValid{get}`；`Source{get/set}`；`Start{get}`；`StartMs{get/set}`；`Type{get/set}`

### `sealed enum MediaSegmentType`
- 取值：Intro, Credits, Recap, Preview

### `static class MediaSegmentTypeExtensions`
- 方法：`static Id(MediaSegmentType type)`

### `sealed class PlaybackItem`
- 构造：`new PlaybackItem()`
- 方法：`static FromEmbyItem(EmbyItem item, ServerConfig server, String mediaSourceId=…)`；`static LocalFile(String path, String title=…)`；`ToJson()`；`ToString()`；`With(String serverId=…, String itemId=…, String name=…, String type=…, String mediaSourceId=…, List<EmbyMediaSource> mediaSources=…, Nullable<Int32> audioStreamIndex=…, Nullable<Int32> subtitleStreamIndex=…, Nullable<Double> startSeconds=…, Nullable<Double> durationSeconds=…, String streamUrl=…, String posterUrl=…, String backdropUrl=…, EmbyUserData userData=…)`
- 属性：`AudioStreamIndex{get/set}`；`BackdropUrl{get/set}`；`CurrentSource{get}`；`DurationSeconds{get/set}`；`EpisodeCodeText{get}`；`EpisodeNumber{get/set}`；`FilePath{get/set}`；`HasMultipleSources{get}`；`IsEpisode{get}`；`IsLocalFile{get/set}`；`ItemId{get/set}`；`MediaSourceId{get/set}`；`MediaSources{get/set}`；`Name{get/set}`；`PosterUrl{get/set}`；`SeasonId{get/set}`；`SeasonNumber{get/set}`；`SeriesId{get/set}`；`SeriesName{get/set}`；`ServerId{get/set}`；`ServerKind{get/set}`；`ServerName{get/set}`；`StartSeconds{get/set}`；`StreamUrl{get/set}`；`SubtitleStreamIndex{get/set}`；`Type{get/set}`；`UserData{get/set}`

### `sealed class ServerConfig`
- 构造：`new ServerConfig()`
- 方法：`ExtraBool(String key, Boolean def=…)`；`ExtraString(String key)`；`static FromJson(JsonElement json)`；`SetCachedLastPlayedAt(Nullable<DateTimeOffset> value)`；`SetExtra(String key, String value)`；`ToJson()`；`ToString()`；`With(String id=…, String name=…, String baseUrl=…, String userName=…, String userId=…, String accessToken=…, String iconUrl=…, String iconData=…, Nullable<Boolean> enabled=…, Nullable<Int32> sortIndex=…, HashSet<String> hiddenLibraryIds=…, Dictionary<String,JsonNode> extra=…)`
- 属性：`AccessToken{get/set}`；`BaseUrl{get/set}`；`CachedLastPlayedAt{get}`；`CachedLastPlayedAtRaw{get}`；`Enabled{get/set}`；`Extra{get/set}`；`HiddenLibraryIds{get/set}`；`IconData{get/set}`；`IconUrl{get/set}`；`Id{get/set}`；`IsOriginalAccountEntry{get}`；`Kind{get/set}`；`Name{get/set}`；`Password{get}`；`PasswordKey{get}`；`RefreshTokenKey{get}`；`SortIndex{get/set}`；`TokenKey{get}`；`UserId{get/set}`；`UserName{get/set}`

### `sealed enum ServerKind`
- 取值：Emby, Jellyfin, Navidrome, Audiobookshelf, WebDav

### `static class ServerKindExtensions`
- 方法：`static DisplayName(ServerKind kind)`；`static Id(ServerKind kind)`；`static IsAudioBook(ServerKind kind)`；`static IsEmbyFamily(ServerKind kind)`；`static IsMusic(ServerKind kind)`；`static ParseKind(Object value)`

### `sealed class SkipSegmentPreference`
- 构造：`new SkipSegmentPreference()`
- 方法：`Allows(MediaSegmentType type)`；`Filter(IEnumerable<MediaSegmentDto> segments)`；`static FromJson(JsonElement json)`；`ToJson()`
- 属性：`Credits{get/set}`；`Intro{get/set}`；`Preview{get/set}`；`Recap{get/set}`

### `sealed class SkipSettings`
- 构造：`new SkipSettings()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`；`ToSegments()`；`With(String itemId=…, Nullable<Boolean> enabled=…, Nullable<Int64> introStartMs=…, Nullable<Int64> introEndMs=…, Nullable<Int64> creditsStartMs=…)`
- 属性：`CreditsStartMs{get/set}`；`Enabled{get/set}`；`HasCredits{get}`；`HasIntro{get}`；`IntroEndMs{get/set}`；`IntroStartMs{get/set}`；`IsEmpty{get}`；`ItemId{get/set}`

### `sealed class SkipSourceSettings`
- 构造：`new SkipSourceSettings()`
- 方法：`static FromJson(JsonElement json)`；`ToJson()`；`With(Nullable<Boolean> emby=…, Nullable<Boolean> theIntroDb=…, Nullable<Boolean> introDbApp=…, Nullable<Boolean> theOtherDb=…, String customTemplate=…)`
- 属性：`CustomTemplate{get/set}`；`Emby{get/set}`；`IntroDbApp{get/set}`；`TheIntroDb{get/set}`；`TheOtherDb{get/set}`

### `sealed class SubsonicAlbum`
- 构造：`new SubsonicAlbum()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`
- 属性：`Artist{get/set}`；`ArtistId{get/set}`；`CoverArt{get/set}`；`Created{get/set}`；`Duration{get/set}`；`Genre{get/set}`；`Id{get/set}`；`Name{get/set}`；`Played{get/set}`；`SongCount{get/set}`；`Starred{get/set}`；`Year{get/set}`

### `sealed class SubsonicAlbumWithSongs`
- 构造：`new SubsonicAlbumWithSongs()`
- 属性：`Album{get/set}`；`Songs{get/set}`

### `sealed class SubsonicArtist`
- 构造：`new SubsonicArtist()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`
- 属性：`AlbumCount{get/set}`；`ArtistImageUrl{get/set}`；`CoverArt{get/set}`；`Id{get/set}`；`Name{get/set}`；`Starred{get/set}`

### `sealed class SubsonicLyricLine`
- 构造：`new SubsonicLyricLine()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`
- 属性：`StartMs{get}`；`Timestamp{get/set}`；`Value{get/set}`

### `sealed class SubsonicLyrics`
- 构造：`new SubsonicLyrics()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`；`ToLrc()`
- 属性：`Artist{get/set}`；`IsEmpty{get}`；`Lines{get/set}`；`Plain{get/set}`；`Title{get/set}`

### `sealed class SubsonicPlaylist`
- 构造：`new SubsonicPlaylist()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`
- 属性：`Changed{get/set}`；`Comment{get/set}`；`CoverArt{get/set}`；`Created{get/set}`；`Duration{get/set}`；`Id{get/set}`；`Name{get/set}`；`Owner{get/set}`；`SongCount{get/set}`

### `sealed class SubsonicSearchResult`
- 构造：`new SubsonicSearchResult()`
- 属性：`Albums{get/set}`；`Artists{get/set}`；`IsEmpty{get}`；`Songs{get/set}`

### `sealed class SubsonicSong`
- 构造：`new SubsonicSong()`
- 方法：`static FromJson(JsonElement json)`；`static FromJson(Nullable<JsonElement> json)`
- 属性：`Album{get/set}`；`AlbumId{get/set}`；`Artist{get/set}`；`ArtistId{get/set}`；`BitRate{get/set}`；`ContentType{get/set}`；`CoverArt{get/set}`；`DiscNumber{get/set}`；`Duration{get/set}`；`Genre{get/set}`；`Id{get/set}`；`Path{get/set}`；`Size{get/set}`；`Starred{get/set}`；`Subtitle{get}`；`Suffix{get/set}`；`Title{get/set}`；`Track{get/set}`；`Year{get/set}`

### `sealed class SubsonicStarred`
- 构造：`new SubsonicStarred()`
- 属性：`Albums{get/set}`；`Artists{get/set}`；`IsEmpty{get}`；`Songs{get/set}`

### `sealed class WebDavEntry`
- 构造：`new WebDavEntry(String path, Boolean isDirectory, Int64 size=…, Nullable<DateTime> modified=…)`
- 方法：`static FromJson(JsonElement json)`；`ToString()`
- 属性：`IsDirectory{get}`；`Modified{get}`；`Name{get}`；`Path{get}`；`Size{get}`

### `static class WebDavTime`
- 方法：`static FromUnixMillis(Int64 millis)`；`static ParseIso(String value)`；`static ToIso(DateTime time)`；`static ToLocal(Nullable<DateTime> time)`


## AIPlayer.Shell.Services.Navidrome

### `sealed class NavidromeAuth`
- 构造：`new NavidromeAuth()`
- 属性：`IsAdmin{get/set}`；`Token{get/set}`；`UserId{get/set}`；`UserName{get/set}`

### `sealed class NavidromeService`
- 构造：`new NavidromeService(ShellHttpClient http, ServerConfig server, String clientName=…, String clientVersion=…, Action<String> onLog=…, String apiVersion=…)`
- 方法：`Api(String method, IDictionary<String,String> query=…)`；`CallAsync(String method, IDictionary<String,String> query=…, CancellationToken cancellationToken=…)`；`CoverArtUrl(String coverArtId, Nullable<Int32> size=…)`；`GetAlbumAsync(String albumId, CancellationToken cancellationToken=…)`；`GetAlbumList2Async(String type=…, Int32 size=…, Int32 offset=…, Nullable<Int32> fromYear=…, Nullable<Int32> toYear=…, String genre=…, CancellationToken cancellationToken=…)`；`GetArtistAlbumsAsync(String artistId, CancellationToken cancellationToken=…)`；`GetArtistsAsync(CancellationToken cancellationToken=…)`；`GetLyricsBySongIdAsync(String songId, CancellationToken cancellationToken=…)`；`GetPlaylistAsync(String playlistId, CancellationToken cancellationToken=…)`；`GetPlaylistsAsync(CancellationToken cancellationToken=…)`；`GetRandomSongsAsync(Int32 size=…, CancellationToken cancellationToken=…)`；`GetStarred2Async(CancellationToken cancellationToken=…)`；`static LoginNativeAsync(ShellHttpClient http, String baseUrl, String username, String password, CancellationToken cancellationToken=…)`；`static Md5Hex(String input)`；`PingAsync(CancellationToken cancellationToken=…)`；`ResetSalt()`；`ScrobbleAsync(String songId, Boolean submission=…, Nullable<Int64> timeSeconds=…, CancellationToken cancellationToken=…)`；`Search3Async(String query, Int32 count=…, CancellationToken cancellationToken=…)`；`SetStarredAsync(String id, Boolean starred, String type=…, CancellationToken cancellationToken=…)`；`StreamUrl(String songId, Nullable<Int32> maxBitRate=…, String format=…, Nullable<Int32> timeOffsetSeconds=…)`；`StreamUrlWithAuth(String songId, Nullable<Int32> maxBitRate=…)`；`ToString()`
- 常量：`const DefaultApiVersion = 1.16.1`
- 属性：`ApiVersion{get}`；`AuthParams{get}`；`BaseUrl{get}`；`ClientName{get}`；`ClientVersion{get}`；`Http{get}`；`NativeHeaders{get}`；`OnLog{get}`；`Password{get}`；`Salt{get}`；`Server{get/set}`；`Token{get}`


## AIPlayer.Shell.Services.Playback

### `sealed class KernelControlClient`
- 构造：`new KernelControlClient(HttpClient http=…, KernelControlSchema schema=…, Nullable<TimeSpan> timeout=…)`
- 方法：`ApplyCallbackBody(String eventName, String body)`；`ApplyProgressBody(String body)`；`Dispose()`；`SendAsync(KernelControlCommand command, CancellationToken cancellationToken=…)`；`SendAsync(IReadOnlyList<KernelControlCommand> commands, CancellationToken cancellationToken=…)`；`SetUpdateUrl(String updateUrl)`；`TryResolveControlUri(String& uri, String& error)`
- 属性：`HasEndpoint{get}`；`LastState{get}`；`Schema{get}`；`UpdateUrl{get}`

### `sealed class KernelControlCommand`
- 方法：`static AddExternalSubtitle(String pathOrUrl)`；`static SeekAbsolute(Double seconds)`；`static SetAudioTrack(Int64 trackId)`；`static SetSpeed(Double rate)`；`static SetSubtitleDelay(Double seconds)`；`static SetSubtitleTrack(Int64 trackId)`；`static SetSubtitleTrackDisabled()`；`static SetSubtitleVisibility(Boolean visible)`；`static SetVolume(Int32 level)`；`ToString()`
- 属性：`Kind{get}`；`TypedField{get}`；`TypedName{get}`；`Value{get}`；`WireName{get}`；`WireValue{get}`

### `sealed enum KernelControlKind`
- 取值：SetAudioTrack, SetSubtitleTrack, SetSubtitleDelay, SetSubtitleVisibility, AddExternalSubtitle, SetSpeed, SetVolume, SeekAbsolute

### `sealed enum KernelControlOutcome`
- 取值：Success, Rejected, NotFound, MethodNotAllowed, Unauthorized, ServerError, UnexpectedStatus, NoEndpoint, EndpointUnreachable

### `static class KernelControlPayload`
- 方法：`static Build(IReadOnlyList<KernelControlCommand> commands, KernelControlSchema schema=…)`；`static Build(KernelControlCommand command, KernelControlSchema schema=…)`
- 常量：`const EnvelopeKey = Commands`

### `sealed class KernelControlResult`
- 构造：`new KernelControlResult()`
- 方法：`ToString()`
- 属性：`Endpoint{get/set}`；`Error{get/set}`；`ErrorCommand{get/set}`；`ErrorIndex{get/set}`；`IsReachable{get}`；`Outcome{get/set}`；`RequestBody{get/set}`；`ResponseBody{get/set}`；`StatusCode{get/set}`；`Success{get}`；`UserFacingFailure{get}`

### `sealed enum KernelControlSchema`
- 取值：TypedKind, NameValue

### `static class KernelPlaybackBridge`
- 方法：`static FromProgressBody(String progressBody, HttpClient http=…, KernelControlSchema schema=…)`；`static UpdateFromProgressBody(KernelControlClient client, String progressBody)`
- 常量：`const SeamLine = SEAM①: AppViewModel 收到 "progress" 回调体 ⇒ KernelPlaybackBridge.FromProgressBody(body, http) ⇒ _live；播放中控制命令 ⇒ _live.SendAsync(...)（失败按 KernelControlResult.UserFacingFailure 显式降级）`

### `sealed class KernelPlaybackState`
- 构造：`new KernelPlaybackState()`
- 方法：`Changes(KernelPlaybackState previous)`；`static FromBody(String json)`；`static FromElement(JsonElement root)`；`ToString()`
- 属性：`Aid{get/set}`；`DurationSeconds{get/set}`；`EmbyAudioStreamIndex{get/set}`；`EmbySubtitleStreamIndex{get/set}`；`EventName{get/set}`；`HasMpvState{get}`；`HasUpdateUrl{get}`；`IsMuted{get/set}`；`IsPaused{get/set}`；`PositionSeconds{get/set}`；`Sid{get/set}`；`Speed{get/set}`；`SubDelay{get/set}`；`UpdateUrl{get/set}`；`Volume{get/set}`

### `sealed enum PlaybackMode`
- 取值：Auto, DirectPlay, DirectStream, Transcode

### `static class PlaybackModeExtensions`
- 方法：`static DisplayName(PlaybackMode mode)`；`static Id(PlaybackMode mode)`；`static ParseId(String value)`

### `sealed class PlaybackResolveOptions`
- 构造：`new PlaybackResolveOptions()`
- 属性：`AllowDirectPlay{get/set}`；`AllowDirectStream{get/set}`；`AllowTranscoding{get/set}`；`AudioStreamIndex{get/set}`；`Default{get}`；`MediaSourceId{get/set}`；`Mode{get/set}`；`StartSeconds{get/set}`；`SubtitleStreamIndex{get/set}`；`TranscodeContainer{get/set}`

### `sealed enum PlaybackStreamKind`
- 取值：Unknown, StaticDirect, DirectStream, Hls, Transcode, Subtitle, Image

### `static class PlaybackStreamUrl`
- 方法：`static AbsoluteUrl(String baseUrl, String url)`；`static CacheKey(String url)`；`static FromUrl(String url)`；`static HlsUrl(EmbyService emby, String itemId, String mediaSourceId=…)`；`static ImageUrl(EmbyService emby, String itemId, String type=…, Nullable<Int32> maxHeight=…, Nullable<Int32> maxWidth=…, Nullable<Int32> index=…, String tag=…)`；`static IsDirectStreamUrl(String url)`；`static IsImageUrl(String url)`；`static IsInternalServiceHostname(String url)`；`static IsPlaybackStreamUrl(String url)`；`static IsSubtitleUrl(String url)`；`static IsTranscodeUrl(String url)`；`static StaticDirectUrl(EmbyService emby, String itemId, String mediaSourceId=…, Nullable<Double> startSeconds=…, Nullable<Int32> audioStreamIndex=…, Nullable<Int32> subtitleStreamIndex=…)`；`static SubtitleUrl(EmbyService emby, String itemId, String mediaSourceId, Int32 streamIndex, String format=…)`；`static TranscodeStreamUrl(EmbyService emby, String itemId, String container, String mediaSourceId=…, Nullable<Double> startSeconds=…, Nullable<Int32> audioStreamIndex=…, Nullable<Int32> subtitleStreamIndex=…)`

### `sealed class PlaybackStreamUrlParts`
- 构造：`new PlaybackStreamUrlParts()`
- 方法：`ToString()`
- 属性：`ApiKey{get/set}`；`AudioStreamIndex{get/set}`；`Container{get/set}`；`Format{get/set}`；`ImageType{get/set}`；`IsPlaybackStream{get}`；`IsStatic{get/set}`；`IsTranscode{get}`；`ItemId{get/set}`；`Kind{get/set}`；`MediaSourceId{get/set}`；`Path{get/set}`；`RawUrl{get/set}`；`StartTicks{get/set}`；`SubtitleStreamIndex{get/set}`；`SubtitleStreamIndexParam{get/set}`

### `sealed class PlaybackUrlResolver`
- 构造：`new PlaybackUrlResolver(EmbyService emby)`
- 方法：`static CanPlay(EmbyItem item)`；`static IsDirectPlayable(EmbyMediaSource source)`；`Resolve(EmbyItem item, PlaybackResolveOptions options=…)`；`static Resolve(EmbyService emby, EmbyItem item, PlaybackResolveOptions options=…)`；`ResolveAsync(EmbyItem item, PlaybackResolveOptions options=…, CancellationToken cancellationToken=…)`；`ResolveSource(EmbyItem item, EmbyMediaSource source, PlaybackResolveOptions options=…)`；`static SelectSource(EmbyItem item, String mediaSourceId)`；`SubtitleUrl(PlaybackUrlResult playback, Int32 subtitleStreamIndex, String format=…)`
- 属性：`Emby{get}`

### `sealed class PlaybackUrlResult`
- 构造：`new PlaybackUrlResult()`
- 方法：`ToString()`
- 属性：`AudioStreamIndex{get/set}`；`Container{get/set}`；`FromServerDirectStreamUrl{get/set}`；`IsDirect{get}`；`ItemId{get/set}`；`MediaSourceId{get/set}`；`Mode{get/set}`；`Source{get/set}`；`StartSeconds{get/set}`；`StartTicks{get/set}`；`SubtitleStreamIndex{get/set}`；`Url{get/set}`


## AIPlayer.Shell.Services.Search

### `sealed class SearchHistoryService`
- 构造：`new SearchHistoryService(JsonStorage storage)`
- 方法：`Add(String query)`；`static At(String filePath)`；`Clear()`；`Contains(String query)`；`Load()`；`LoadAsync()`；`Reload()`；`Remove(String query)`；`Save()`；`SaveAsync()`
- 常量：`const FileName = search_history.json`；`const MaxEntries = 20`
- 属性：`Count{get}`；`Entries{get}`；`Instance{get}`；`IsLoaded{get}`


## AIPlayer.Shell.Services.Segments

### `sealed class SegmentService`
- 构造：`new SegmentService(ShellHttpClient http, JsonStorage cache=…, Action<String> onLog=…, String theIntroDbBase=…, String introDbAppBase=…, String theOtherDbBase=…)`
- 方法：`ClearCache()`；`CollectAsync(SkipSourceSettings settings, String imdbId=…, Nullable<Int32> season=…, Nullable<Int32> episode=…, Nullable<Double> durationSeconds=…, IReadOnlyList<MediaSegmentDto> embySegments=…, CancellationToken cancellationToken=…)`；`static Normalize(IEnumerable<MediaSegmentDto> input, Nullable<Double> durationSeconds=…)`；`static ParseSegments(Nullable<JsonElement> json, String source)`；`static TimeMsOf(Dictionary<String,JsonElement> map, String[] needles)`；`static ToMs(Double value, String fieldName)`；`static TypeOf(String raw)`
- 常量：`const IntroDbAppBase = https://api.introdb.app/segments`；`const TheIntroDbBase = https://api.theintrodb.org/v3/media`；`const TheOtherDbBase = https://playback.theotherdb.org/api/metadata`
- 属性：`HasPendingWrite{get}`


## AIPlayer.Shell.Services.SelfCheck

### `sealed class SelfCheckReport`
- 构造：`new SelfCheckReport()`
- 方法：`ToText()`
- 属性：`AllPassed{get}`；`Failed{get}`；`Passed{get}`；`Steps{get}`

### `sealed class SelfCheckStep`
- 构造：`new SelfCheckStep()`
- 属性：`Detail{get/set}`；`Name{get/set}`；`Passed{get/set}`

### `static class ServiceSelfCheck`
- 方法：`static RunAsync(Action<String> write=…, CancellationToken cancellationToken=…)`


## AIPlayer.Shell.Services.Servers

### `sealed class CrossServerSyncService`
- 方法：`static KeyOf(EmbyItem item)`；`Merge(IEnumerable<MergedSource> sources)`；`MergeByServer(IEnumerable<KeyValuePair`2> byServer)`；`PatchUserData(IEnumerable<MergedItem> items, String serverId, String itemId, EmbyUserData data)`；`static PickPrimary(IReadOnlyList<MergedSource> sources)`；`SortBySourceCount(IEnumerable<MergedItem> items)`
- 属性：`Instance{get}`

### `sealed class MergedItem`
- 构造：`new MergedItem(EmbyItem item, IReadOnlyList<MergedSource> sources)`
- 方法：`ToString()`
- 属性：`HasAlternatives{get}`；`Item{get}`；`PrimarySource{get}`；`SourceLabel{get}`；`Sources{get}`

### `sealed class MergedSource`
- 构造：`new MergedSource()`；`new MergedSource(ServerConfig server, EmbyItem item)`
- 属性：`Item{get/set}`；`Server{get/set}`

### `sealed class ServerConfigStore`
- 构造：`new ServerConfigStore(JsonStorage storage, SecureKvStore credentials, Boolean readOriginalAccounts=…, String originalAccountsFile=…, String legacyServersFile=…)`
- 方法：`Add(ServerConfig server)`；`Adopt(String id)`；`AdoptAndRemove(String id)`；`AdoptAndUpdate(ServerConfig server)`；`static At(String serversFilePath, SecureKvStore credentials)`；`static At(String serversFilePath, SecureKvStore credentials, String originalAccountsFile)`；`static At(String serversFilePath, SecureKvStore credentials, String originalAccountsFile, String legacyServersFile)`；`ById(String id)`；`CanWriteServer(String id)`；`FirstOf(ServerKind kind)`；`static IsAdoptedEntry(ServerConfig server)`；`IsHidden(String id)`；`IsReadOnlyServer(String id)`；`Load()`；`LoadAsync()`；`NewId()`；`PasswordOf(String serverId)`；`Reload()`；`Remove(String id)`；`Reorder(IReadOnlyList<String> idsInOrder)`；`Save()`；`SaveAsync()`；`Update(ServerConfig server)`
- 属性：`EnabledServers{get}`；`HiddenOriginalIds{get}`；`Instance{get}`；`IsLoaded{get}`；`LegacyServersLoaded{get}`；`OriginalAccountsError{get}`；`OriginalAccountsLoaded{get}`；`SaveTargetPath{get}`；`Servers{get}`


## AIPlayer.Shell.Services.Settings

### `sealed class MpvConfService`
- 构造：`new MpvConfService()`
- 方法：`Build()`；`Clear()`；`static FromSettings(AppSettings settings)`；`Has(String key)`；`Remove(String key)`；`Set(String key, String value=…)`；`static WriteFromSettings(AppSettings settings, String directory)`；`WriteTo(String directory)`；`static WriteToDefaultDir(AppSettings settings)`
- 常量：`const FileName = mpv.conf`
- 属性：`Options{get}`

### `sealed class MpvHostPlayerSettingsStore`
- 构造：`new MpvHostPlayerSettingsStore(JsonStorage storage=…)`
- 方法：`All()`；`static At(String filePath)`；`Clear()`；`Load()`；`LoadAsync()`；`ReadBool(String key, Boolean defaultValue=…)`；`ReadDouble(String key, Double defaultValue=…)`；`ReadInt(String key, Int32 defaultValue=…)`；`ReadLocalSetting(String key, String defaultValue=…)`；`Remove(String key)`；`Save()`；`SaveAsync()`；`SetBool(String key, Boolean value)`；`SetDouble(String key, Double value)`；`SetInt(String key, Int32 value)`；`SetLocalSetting(String key, String value)`；`SetLogLevel(String level)`
- 常量：`const DefaultLogLevel = information`；`const LogLevelKey = LogLevel`
- 属性：`FilePath{get}`；`Instance{get}`；`LogLevel{get}`

### `sealed class PlayerShortcutsStore`
- 构造：`new PlayerShortcutsStore(AppDataDir data, Action<String> log=…)`
- 方法：`Load()`；`LoadAndValidate()`；`Save(HostShortcuts shortcuts)`；`static Validate(HostShortcuts shortcuts)`
- 属性：`FilePath{get}`

### `sealed class SettingsService`
- 构造：`new SettingsService(JsonStorage storage=…)`
- 方法：`static At(String settingsFilePath)`；`Load()`；`LoadAsync()`；`Patch(Func<AppSettings,AppSettings> transform)`；`Reload()`；`ResetToDefaults()`；`Save(AppSettings next)`；`SaveAsync(AppSettings next)`
- 属性：`Exists{get}`；`Instance{get}`；`IsLoaded{get}`；`Settings{get}`

### `sealed class ShortcutConflictReport`
- 构造：`new ShortcutConflictReport()`
- 方法：`ToString()`
- 属性：`Conflicts{get}`；`HasConflict{get}`


## AIPlayer.Shell.Services.Skip

### `sealed class MpvHostSegmentEntry`
- 构造：`new MpvHostSegmentEntry()`
- 方法：`static FromJson(String itemId, Nullable<JsonElement> json)`；`ToBase64()`；`ToJson()`；`ToJsonArray()`；`ToString()`
- 属性：`IsEmpty{get}`；`ItemId{get/set}`；`Segments{get/set}`；`UpdatedAt{get/set}`

### `sealed class MpvHostSkipStore`
- 构造：`new MpvHostSkipStore(JsonStorage storage)`
- 方法：`Add(String itemId, MediaSegmentDto segment)`；`All()`；`static At(String filePath)`；`Clear()`；`static Encode(IReadOnlyList<MediaSegmentDto> segments)`；`static Encode(MediaSegmentDto segment)`；`Get(String itemId)`；`Has(String itemId)`；`Load()`；`LoadAsync()`；`Remove(String itemId)`；`static ResetInstance()`；`Save()`；`SaveAsync()`；`Segments(String itemId)`；`Set(String itemId, IEnumerable<MediaSegmentDto> segments)`；`ToBase64(String itemId)`；`ToSegmentArguments(String itemId)`；`static ToSegmentArguments(IReadOnlyList<MediaSegmentDto> segments, Int32 perArgument=…)`；`ToSegmentFlags(String itemId)`；`ToString()`
- 属性：`Count{get}`；`FilePath{get}`；`Instance{get}`；`IsLoaded{get}`

### `sealed class SkipEntry`
- 构造：`new SkipEntry()`
- 方法：`Clone()`；`static FromJson(String itemId, Nullable<JsonElement> json)`；`ToJson()`；`ToString()`
- 属性：`IsEmpty{get}`；`ItemId{get/set}`；`Segments{get/set}`；`UpdatedAt{get/set}`

### `sealed class SkipStore`
- 构造：`new SkipStore(JsonStorage storage)`
- 方法：`All()`；`AllSegments()`；`Append(String itemId, IEnumerable<MediaSegmentDto> segments)`；`static At(String filePath)`；`Clear()`；`Get(String itemId)`；`Has(String itemId)`；`ItemIds()`；`Load()`；`LoadAsync()`；`Remove(String itemId)`；`static ResetInstance()`；`Save()`；`SaveAsync()`；`SegmentsOf(String itemId)`；`Set(String itemId, IEnumerable<MediaSegmentDto> segments)`；`Set(String itemId, MediaSegmentDto segment)`；`ToString()`
- 属性：`Count{get}`；`FilePath{get}`；`Instance{get}`；`IsLoaded{get}`


## AIPlayer.Shell.Services.State

### `abstract class ChangeNotifierBase`

### `sealed class ServerScopedCache`1`
- 构造：`new ServerScopedCache`1()`
- 方法：`Clear()`；`Count(String serverId=…)`；`Get(String serverId, String key, T fallback=…)`；`GetOrAdd(String serverId, String key, Func<T> factory, Nullable<TimeSpan> ttl=…)`；`Has(String serverId, String key)`；`Invalidate(String serverId)`；`Invalidate(String serverId, String key)`；`Items(String serverId)`；`LockFor(String serverId, String key)`；`Remove(String serverId, String key)`；`static ScopeOf(ServerConfig server)`；`static ScopeOf(String serverId)`；`Set(String serverId, String key, T value, Nullable<TimeSpan> ttl=…)`；`TryGet(String serverId, String key, T& value)`
- 常量：`const LocalScope = __local__`
- 属性：`MaxEntries{get/set}`；`Scopes{get}`


## AIPlayer.Shell.Services.Storage

### `sealed class JsonStorage`
- 构造：`new JsonStorage(String filePath)`
- 方法：`Delete()`；`Read()`；`ReadAsync()`；`ReadMap()`；`ReadMapAsync()`；`Write(Object value)`；`WriteAsync(Object value)`
- 属性：`FilePath{get}`

### `sealed enum SwrFreshness`
- 取值：Miss, Stale, Fresh

### `sealed class SwrLoadResult`1`
- 构造：`new SwrLoadResult`1()`
- 方法：`ToString()`
- 属性：`CacheAgeSeconds{get/set}`；`CacheCachedAtUtc{get/set}`；`CacheFreshness{get/set}`；`CacheHit{get/set}`；`CachePreserved{get}`；`Cached{get/set}`；`CachedIds{get}`；`ElapsedMs{get/set}`；`Error{get/set}`；`Evidence{get}`；`Key{get/set}`；`RefreshStarted{get/set}`；`RefreshSucceeded{get/set}`；`RefreshedIds{get}`；`Value{get/set}`

### `sealed class SwrLookup`1`
- 构造：`new SwrLookup`1()`
- 方法：`ToString()`
- 属性：`AgeSeconds{get/set}`；`CachedAtUtc{get/set}`；`Freshness{get/set}`；`HasValue{get}`；`Value{get/set}`

### `sealed class SwrRefreshResult`1`
- 构造：`new SwrRefreshResult`1()`
- 方法：`ToString()`
- 属性：`ElapsedMs{get/set}`；`Error{get/set}`；`PreviousFreshness{get/set}`；`Success{get/set}`；`Value{get/set}`

### `sealed class SwrSnapshotCache`1`
- 构造：`new SwrSnapshotCache`1(String directory, TimeSpan ttl, Int64 maxBytes=…, Int32 maxEntries=…, Action<String> log=…)`
- 方法：`Clear()`；`Invalidate(String serverId)`；`InvalidateKey(String key)`；`LoadAsync(String key, Func<CancellationToken,Task`1> fetch, Action<SwrLookup`1> onCacheValue=…, Func<T,IEnumerable`1> idsOf=…, CancellationToken cancellationToken=…)`；`PathFor(String key)`；`ReadThenRefreshAsync(String key, Func<CancellationToken,Task`1> fetch, Action<SwrLookup`1> onStaleValue=…, CancellationToken cancellationToken=…)`；`RefreshAsync(String key, Func<CancellationToken,Task`1> fetch, CancellationToken cancellationToken=…)`；`SaveSnapshot(String serverId, String key, T value)`；`SnapshotIndex()`；`TryGetSnapshot(String serverId, String key)`；`TryRead(String key)`
- 属性：`Count{get}`；`Directory{get}`；`Ttl{get}`


## AIPlayer.Shell.Services.Todb

### `static class SegmentLike`
- 方法：`static StartMsOf(Dictionary<String,JsonElement> map, Boolean end)`

### `sealed class TodbMetadata`
- 构造：`new TodbMetadata()`
- 属性：`Chapters{get/set}`；`IsEmpty{get}`；`Raw{get/set}`；`Sprite{get/set}`

### `sealed class TodbService`
- 构造：`new TodbService(ShellHttpClient http, Action<String> onLog=…, String metadataBase=…)`
- 方法：`FetchAsync(String imdbId, Nullable<Int32> season=…, Nullable<Int32> episode=…, Nullable<Double> durationSeconds=…, CancellationToken cancellationToken=…)`；`static MsOf(Nullable<JsonElement> value)`
- 常量：`const MetadataBase = https://playback.theotherdb.org/api/metadata`


## AIPlayer.Shell.Services.Trakt

### `sealed class TraktDeviceCode`
- 构造：`new TraktDeviceCode(String deviceCode=…, String userCode=…, String verificationUrl=…, Int32 expiresIn=…, Int32 interval=…)`
- 方法：`static FromJson(Nullable<JsonElement> json)`
- 属性：`DeviceCode{get/set}`；`ExpiresIn{get/set}`；`Interval{get/set}`；`IsUsable{get}`；`UserCode{get/set}`；`VerificationUrl{get/set}`

### `sealed class TraktHistoryItem`
- 构造：`new TraktHistoryItem()`
- 属性：`Action{get/set}`；`Id{get/set}`；`Media{get/set}`；`Type{get/set}`；`WatchedAt{get/set}`

### `sealed class TraktScrobbleMedia`
- 构造：`new TraktScrobbleMedia(String type, String imdb=…, String tmdb=…, String tvdb=…, String title=…, Nullable<Int32> year=…, Nullable<Int32> season=…, Nullable<Int32> episode=…)`
- 方法：`static ForEpisode(Nullable<Int32> season, Nullable<Int32> episode, String imdb=…, String tmdb=…, String tvdb=…, String title=…, Nullable<Int32> year=…)`；`static ForMovie(String imdb=…, String tmdb=…, String tvdb=…, String title=…, Nullable<Int32> year=…)`；`ToJson()`
- 常量：`const TypeEpisode = episode`；`const TypeMovie = movie`；`const TypeSeason = season`；`const TypeShow = show`
- 属性：`Episode{get/set}`；`Ids{get}`；`Imdb{get/set}`；`IsMovie{get}`；`IsUsable{get}`；`Season{get/set}`；`Title{get/set}`；`Tmdb{get/set}`；`Tvdb{get/set}`；`Type{get/set}`；`Year{get/set}`

### `sealed class TraktScrobbleSender`
- 构造：`new TraktScrobbleSender(Object object, IntPtr method)`
- 方法：`BeginInvoke(String action, TraktScrobbleMedia media, Double progressPercent, AsyncCallback callback, Object object)`；`EndInvoke(IAsyncResult result)`；`Invoke(String action, TraktScrobbleMedia media, Double progressPercent)`

### `sealed class TraktScrobbler`
- 构造：`new TraktScrobbler(TraktScrobbleSender send, Action<String> onLog=…)`
- 方法：`static ForService(TraktService service, Action<String> onLog=…)`；`static KeyOf(TraktScrobbleMedia media)`；`OnProgressAsync(TraktScrobbleMedia media, Double positionSeconds, Boolean isPaused, Nullable<Double> durationSeconds=…, Nullable<Double> minimumPercent=…)`；`OnStoppedAsync(TraktScrobbleMedia media, Double positionSeconds, Nullable<Double> durationSeconds=…)`；`static PercentOf(Double positionSeconds, Nullable<Double> durationSeconds)`；`Reset()`
- 常量：`const MinPercent = 1`；`const WatchedPercent = 80`
- 属性：`CurrentKey{get}`；`HasStarted{get}`；`LastAction{get}`；`SentCount{get}`

### `sealed class TraktService`
- 构造：`new TraktService(ShellHttpClient http, PasswordStore credentials, String clientId, String clientSecret=…, Action<String> onLog=…)`
- 方法：`AddToHistoryAsync(TraktScrobbleMedia media, CancellationToken cancellationToken=…)`；`ApiHeaders()`；`ApiUri(String path)`；`AsScrobbleSender()`；`ClearToken()`；`EnsureTokenAsync(CancellationToken cancellationToken=…)`；`GetWatchedHistoryAsync(TraktScrobbleMedia media, Int32 limit=…, CancellationToken cancellationToken=…)`；`LoadToken()`；`PollDeviceTokenAsync(TraktDeviceCode code, Action onPending=…, Nullable<TimeSpan> timeout=…, CancellationToken cancellationToken=…)`；`RefreshTokenAsync(CancellationToken cancellationToken=…)`；`RemoveFromHistoryAsync(TraktScrobbleMedia media, CancellationToken cancellationToken=…)`；`RevokeTokenAsync(CancellationToken cancellationToken=…)`；`SaveToken(TraktToken token)`；`static ScrobbleActionFor(Boolean isPaused, Boolean isFinished)`；`ScrobbleAsync(String action, TraktScrobbleMedia media, Double progressPercent, CancellationToken cancellationToken=…)`；`StartDeviceAuthAsync(CancellationToken cancellationToken=…)`；`TestTokenAsync(CancellationToken cancellationToken=…)`
- 常量：`const ApiBase = https://api.trakt.tv`；`const ClientIdKey = trakt.clientId`；`const ClientSecretKey = trakt.clientSecret`；`const OobRedirect = urn:ietf:wg:oauth:2.0:oob`
- 属性：`ClientId{get/set}`；`ClientSecret{get/set}`；`Credentials{get}`；`HasToken{get}`；`Http{get}`；`LastPollFailure{get}`；`OnLog{get}`；`Token{get/set}`；`TokenStore{get}`

### `sealed class TraktToken`
- 构造：`new TraktToken(String accessToken, String refreshToken, Int64 expiresAt, Int64 createdAt=…, String scope=…, String tokenType=…)`
- 方法：`ExpiresWithin(TimeSpan window)`；`static FromJson(Nullable<JsonElement> json)`；`ToJson()`；`ToString()`
- 常量：`const DefaultScope = public`；`const DefaultTokenType = bearer`
- 属性：`AccessToken{get/set}`；`CreatedAt{get/set}`；`ExpiresAt{get/set}`；`ExpiresAtUtc{get}`；`HasAccessToken{get}`；`IsExpired{get}`；`RefreshToken{get/set}`；`RemainingSeconds{get}`；`Scope{get/set}`；`TokenType{get/set}`

### `sealed class TraktTokenStore`
- 构造：`new TraktTokenStore(PasswordStore credentials, String legacyKey=…)`
- 方法：`Clear()`；`EnsureLoaded()`；`MigrateLegacy()`；`Read()`；`Write(TraktToken token)`
- 常量：`const AccessTokenKey = trakt.accessToken`；`const CreatedAtKey = trakt.createdAt`；`const ExpiresAtKey = trakt.expiresAt`；`const LegacyKey = trakt_token`；`const RefreshTokenKey = trakt.refreshToken`；`const ScopeKey = trakt.scope`；`const TokenTypeKey = trakt.tokenType`
- 属性：`Credentials{get}`；`HasToken{get}`；`LegacyKeyName{get}`


## AIPlayer.Shell.Services.Update

### `sealed class UpdateInfo`
- 构造：`new UpdateInfo()`
- 方法：`static FromJson(JsonElement json)`
- 属性：`DownloadUrl{get/set}`；`FileName{get/set}`；`HasDownload{get}`；`Mandatory{get/set}`；`Notes{get/set}`；`PublishedAt{get/set}`；`Sha256{get/set}`；`SizeBytes{get/set}`；`Version{get/set}`

### `sealed class UpdateService`
- 构造：`new UpdateService(ShellHttpClient http, Action<String> onLog=…, String currentVersion=…)`
- 方法：`ApplyUpdateAsync(String stagingDir, String installDir, String exeName=…, CancellationToken cancellationToken=…)`；`static BuildDiagnostics(String appVersion, IDictionary<String,Object> extra=…)`；`CheckForUpdateAsync(CancellationToken cancellationToken=…)`；`DownloadAsync(UpdateInfo info, String targetDir, Action<Int64,Int64> onProgress=…, CancellationToken cancellationToken=…)`；`ExtractAsync(String archivePath, String targetDir, CancellationToken cancellationToken=…)`；`FetchLatestAsync(CancellationToken cancellationToken=…)`；`static IsNewer(String remote, String local)`；`static NormalizeExtractedRoot(String stagingDir)`；`ReportAsync(String path, IDictionary<String,Object> payload=…, CancellationToken cancellationToken=…)`；`static Segments(String version)`
- 常量：`const ReportUrlPrefix = /aiplayer/report/`；`const SponsorUrl = https://afdian.com/a/aiplayer`；`const TelegramUrl = https://t.me/aiemby`；`const VersionManifestUrl = https://aiplayer.arkhamimp.qzz.io/downloads/version.json`
- 属性：`CurrentVersion{get}`；`Http{get}`；`OnLog{get}`


## AIPlayer.Shell.Services.Util

### `static class FriendlyError`
- 方法：`static Describe(Exception error)`；`static Describe(String message, Nullable<Int32> statusCode)`；`static DescribeStatus(Int32 statusCode)`；`static IsAuthError(Exception error)`；`static IsNetworkError(Exception error)`；`static Short(Exception error, Int32 max=…)`

### `static class JsonRead`
- 方法：`static AsString(JsonElement e, String def=…)`；`static Bool(Nullable<JsonElement> element, String name, Boolean def=…)`；`static DeepClone(JsonElement element)`；`static Double(Nullable<JsonElement> element, String name, Double def=…)`；`static DoubleOrNull(Nullable<JsonElement> element, String name)`；`static From(JsonNode node)`；`static FromNode(String json)`；`static Int(Nullable<JsonElement> element, String name, Int32 def=…)`；`static IntOrNull(Nullable<JsonElement> element, String name)`；`static Items(Nullable<JsonElement> element, String name)`；`static Long(Nullable<JsonElement> element, String name, Int64 def=…)`；`static LongOrNull(Nullable<JsonElement> element, String name)`；`static Members(Nullable<JsonElement> element)`；`static Objects(IEnumerable<JsonElement> source)`；`static Prop(Nullable<JsonElement> element, String name)`；`static Str(Nullable<JsonElement> element, String name, String def=…)`；`static StrList(Nullable<JsonElement> element, String name)`；`static StrMap(Nullable<JsonElement> element, String name)`

### `static class TextUtils`
- 方法：`static BuildUri(String baseUrl, String path, IDictionary<String,String> query=…)`；`static EpisodeCode(Nullable<Int32> season, Nullable<Int32> episode)`；`static FormatBitrate(Nullable<Int64> bitsPerSecond)`；`static FormatDuration(Nullable<Double> seconds)`；`static HumanBytes(Nullable<Int64> bytes)`；`static IsLocalFile(String path)`；`static LooksLikeUrl(String value)`；`static MillisecondsToTicks(Double ms)`；`static MonogramOf(String title)`；`static NormalizeBaseUrl(String input)`；`static ParseIsoDate(String value)`；`static SanitizeFileName(String value)`；`static SecondsToTicks(Double seconds)`；`static TicksToSeconds(Nullable<Int64> ticks)`；`static Truncate(String value, Int32 max=…)`


## AIPlayer.Shell.Services.WebDav

### `sealed class WebDavAutoBackupScheduler`
- 构造：`new WebDavAutoBackupScheduler(WebDavAutoBackupService service, Action<String> onLog=…)`
- 方法：`CancelSchedule()`；`Dispose()`；`RunOnceAsync(CancellationToken cancellationToken=…)`；`Schedule(Func<WebDavService> davProvider, TimeSpan interval, Int32 keepCount=…, String directory=…)`
- 属性：`Interval{get}`；`IsScheduled{get}`

### `sealed class WebDavAutoBackupService`
- 构造：`new WebDavAutoBackupService(Action<String> onLog=…)`
- 方法：`BackupAsync(WebDavService dav, String directory=…, CancellationToken cancellationToken=…)`；`BuildBundleJsonAsync()`；`DownloadAsync(WebDavService dav, String remotePath, CancellationToken cancellationToken=…)`；`ListBackupsAsync(WebDavService dav, String directory=…, CancellationToken cancellationToken=…)`；`PruneAsync(WebDavService dav, String directory=…, Int32 keepCount=…, CancellationToken cancellationToken=…)`；`RestoreAsync(WebDavService dav, String remotePath=…, String directory=…, CancellationToken cancellationToken=…)`；`RestoreLocallyAsync(BackupBundle bundle, CancellationToken cancellationToken=…)`；`static Timestamp(DateTime time)`；`WriteLocalSnapshotAsync()`
- 常量：`const BackupPrefix = backup_`
- 属性：`OnLog{get}`

### `sealed class WebDavService`
- 构造：`new WebDavService(ShellHttpClient http, ServerConfig server, Action<String> onLog=…)`
- 方法：`BuildUrl(String path)`；`CreateDirectoryAsync(String path, CancellationToken cancellationToken=…)`；`DeleteAsync(String path, CancellationToken cancellationToken=…)`；`DownloadBytesAsync(String path, CancellationToken cancellationToken=…)`；`EnsureDirectoryAsync(String path, CancellationToken cancellationToken=…)`；`ExistsAsync(String path, CancellationToken cancellationToken=…)`；`ListAsync(String path, CancellationToken cancellationToken=…)`；`static ParseMultiStatus(String xml)`；`PingAsync(CancellationToken cancellationToken=…)`；`ReadBytesAsync(String path, CancellationToken cancellationToken=…)`；`ReadStringAsync(String path, CancellationToken cancellationToken=…)`；`UploadAsync(String path, Byte[] content, CancellationToken cancellationToken=…)`；`WriteBytesAsync(String path, Byte[] content, CancellationToken cancellationToken=…)`；`WriteStringAsync(String path, String content, CancellationToken cancellationToken=…)`
- 属性：`BaseUrl{get}`；`Headers{get}`；`Http{get}`；`OnLog{get}`；`RootPath{get}`；`Server{get/set}`


// 原版文件（rebuild/ 中不存在 `app_constants.dart`），按 DESIGN §5 归属表 #5
// （`src/core/constants/app_constants.dart` → `Services/Constants/AppConstants.cs`，处置「新写」）新写。
//
// **常量值来源纪律**（任务书硬性要求：未实证的逐条标注「推断」）：
//   [S]/[H]/[E] = reversed/FlutterApp/{strings_all,endpoints,http_headers,urls}.txt 与 SERVICE_API.md 实证
//   「推断」      = 无语料实证，按项目内既有实现（EmbyService/ShellHttpClient/AppDataDir）对齐或取安全默认值
// 注意：本文件**不**定义 Emby 客户端身份头（那属于 EmbyService.AuthorizationHeader）与落盘文件名
// （那属于 Infra/AppDataDir.cs，避免同一真相两处存放）。

namespace AIPlayer.Shell.Services.Constants;

/// <summary>应用级常量（对应原版 `app_constants.dart`）。</summary>
public static class AppConstants
{
    // ── 客户端身份（SERVICE_API §1.1 [S][H]）─────────────────────────────────

    /// <summary>客户端名（SERVICE_API §1.1 的 `X-Emby-Client` 语义位；具体字面量**推断**）。</summary>
    public const string ClientName = "AI Player";

    /// <summary>设备名（`X-Emby-Device-Name`；Dart 侧 ABS 的 `deviceName` 实证为 `Windows`）。</summary>
    public const string DeviceName = "Windows";

    /// <summary>客户端版本（**推断**：与 EmbyService/AppDataDir 既有口径一致）。</summary>
    public const string ClientVersion = "1.0.0";

    /// <summary>设备 id 前缀（Dart 实证键名 `ai_player_device_id`；**字面量形态推断**）。</summary>
    public const string DeviceIdPrefix = "aiplayer-";

    /// <summary>界面/请求语言（EmbyService 既有默认值，非语料实证 ⇒ **推断**）。</summary>
    public const string DefaultLanguage = "zh-CN";

    /// <summary>
    /// 默认 HTTP User-Agent —— **转发到唯一构造点** <c>Http.UserAgentPolicy.Current</c>（t65）。
    /// 为什么改：这里原本还挂着第二份默认串（"AIPlayer/1.0 (rebuilt shell)"），与 <c>ShellHttpClient</c> 的默认值
    /// 同义不同源 ⇒ 同一事实两个构造点。本常量当刻**全仓 0 处引用**；改成转发是为了防止将来有人把它当"第二份默认值"。
    /// </summary>
    public static string UserAgent => AIPlayer.Shell.Services.Http.UserAgentPolicy.Current;

    /// <summary>应用展示名（外层进程默认产品名一致；**推断**）。</summary>
    public const string AppDisplayName = "AI Player";

    // ── 超时 / 重试（均为**推断**：与 ShellHttpClient/EmbyService 既有取值对齐）─────────

    /// <summary>默认重试次数（与 <c>ShellHttpClient(DefaultRetries = 1)</c> 一致）。</summary>
    public const int DefaultRetries = 1;

    /// <summary>退避基准毫秒（与 <c>ShellHttpClient</c> 的 `300ms * (n+1)` 一致）。</summary>
    public const int RetryBackoffMs = 300;

    // ── 图片失败负缓存（t212；**唯一真源** = 本处，ImageCacheManager 只从这里取默认值）────
    // 依据 = t211 当刻复测（证据 `shell/Tests/evidence/t211-image-500-recheck.txt`）：
    //   ① `Primary` 缺图时服务端回 **500**（其余 ImageType 回 404），且换 tag / 换尺寸参数 / 换 index
    //      实测**全部仍 500**（70 行交叉表 + 5/5 定点）⇒ 500 不是"再试一次"能救的；
    //   ② 「同一 URL 每次进屏都重打」正是用户看到的"反复卡"⇒ 必须有界。

    /// <summary>「该 id 上没有这张图」（HTTP 500）的负缓存冷却（秒）。
    /// 取值 600s：这类失败只随**服务端图库**变化、不随网络变化 ⇒ 太短等于重打、太长会让新补的图迟到。
    /// 语义 = 同一会话内同一 key 至多每冷却窗重打一次。</summary>
    public const int ImageNegativeCacheNoImageSeconds = 600;

    /// <summary>「暂时性失败」（超时 / 传输错 / 5xx 但非 500）的负缓存冷却（秒）。
    /// 取值 30s：卡顿恢复通常秒级，短冷却让下一屏有机会自愈。</summary>
    public const int ImageNegativeCacheTransientSeconds = 30;

    /// <summary>负缓存条数上限（**内存**计数，不占磁盘条目配额；超出按最早失败先淘汰）。</summary>
    public const int ImageNegativeCacheMaxEntries = 512;

    // ── 网络代理（SERVICE_API §7 [S]：内置默认 `127.0.0.1:7890`）────────────

    /// <summary>内置默认代理地址（SERVICE_API §7 实证）。</summary>
    public const string DefaultProxyUrl = "127.0.0.1:7890";

    // ── 第三方服务端点（SERVICE_API §5/§6/§7 [S]；urls.txt 逐条印证）──────────

    /// <summary>版本清单（SERVICE_API §7 [S]）。</summary>
    public const string UpdateManifestUrl = "https://aiplayer.arkhamimp.qzz.io/downloads/version.json";

    /// <summary>遥测/错误上报前缀（SERVICE_API §7 [E]；相对路由，非绝对 URL）。</summary>
    public const string ReportPath = "/aiplayer/report/";

    /// <summary>Telegram 社区（SERVICE_API §7 [S]）。</summary>
    public const string CommunityTelegramUrl = "https://t.me/aiemby";

    /// <summary>赞助页（SERVICE_API §7 [S]）。</summary>
    public const string SponsorUrl = "https://afdian.com/a/aiplayer";

    /// <summary>歌词主源（SERVICE_API §6.1 [S]）。</summary>
    public const string LrcLibSearchUrl = "https://lrclib.net/api/search";

    /// <summary>跳过片段源：TheIntroDB（SERVICE_API §6.2 [S]；参数形态 `?imdb=&season=&episode=` **推断**）。</summary>
    public const string TheIntroDbUrl = "https://api.theintrodb.org/v3/media";

    /// <summary>跳过片段源：IntroDB.app（SERVICE_API §6.2 [S]；urls.txt 记为 `…/segmentsr`，尾部 `r` 是拼写噪声）。</summary>
    public const string IntroDbUrl = "https://api.introdb.app/segments";

    /// <summary>跳过片段源：TheOtherDB（SERVICE_API §6.2 [S]）。</summary>
    public const string TheOtherDbUrl = "https://playback.theotherdb.org/api/metadata";

    /// <summary>自定义跳过片段源模板（SERVICE_API §6.2 [S]；占位符 `{imdb}`/`{season}`/`{episode}`）。</summary>
    public const string SegmentTemplateUrl = "https://example.com/api?imdb={imdb}&s={season}&e={episode}";

    /// <summary>自定义图标源示例（SERVICE_API §6.3 [S]）。</summary>
    public const string IconLibraryExampleUrl = "https://example.com/icons.json";

    /// <summary>Trakt API 根（SERVICE_API §5 [S]，urls.txt 实证 `https://api.trakt.tv`）。</summary>
    public const string TraktApiBase = "https://api.trakt.tv";

    /// <summary>Trakt 设备码（SERVICE_API §5 [S]：`POST https://api.trakt.tv/oauth/device/code`）。</summary>
    public const string TraktDeviceCodeUrl = TraktApiBase + "/oauth/device/code";

    /// <summary>Trakt 设备码换 token（SERVICE_API §5 [E]）。</summary>
    public const string TraktDeviceTokenUrl = TraktApiBase + "/oauth/device/token";

    /// <summary>Trakt 刷新 token（SERVICE_API §5 [E]）。</summary>
    public const string TraktTokenUrl = TraktApiBase + "/oauth/token";

    /// <summary>Trakt 撤销 token（SERVICE_API §5 [E]）。</summary>
    public const string TraktRevokeUrl = TraktApiBase + "/oauth/revoke";

    /// <summary>Trakt API 版本头值（SERVICE_API §5 [S][H]：`trakt-api-version: 2`）。</summary>
    public const string TraktApiVersion = "2";

    /// <summary>Trakt API key 头名（SERVICE_API §5 [S][H]）。</summary>
    public const string TraktApiKeyHeader = "trakt-api-key";

    /// <summary>Trakt API 版本头名（SERVICE_API §5 [S][H]）。</summary>
    public const string TraktApiVersionHeader = "trakt-api-version";

    /// <summary>OOB 回退 URI（SERVICE_API §5 [S]）。</summary>
    public const string TraktOobUri = "urn:ietf:wg:oauth:2.0:oob";

    // ── 媒体形态（EmbyService 既有口径；**推断**）───────────────────────────

    /// <summary>直连可播的容器白名单（与 <c>EmbyService.DeviceProfile</c> 的 DirectPlayProfiles 一致）。</summary>
    public const string DirectPlayContainers =
        "mkv,mp4,ts,m2ts,avi,mov,flv,wmv,webm,mp3,flac,aac,m4a,opus,ogg,wav";

    /// <summary>音频容器白名单（同上）。</summary>
    public const string DirectPlayAudioContainers = "mp3,flac,aac,m4a,opus,ogg,wav,wma";

    /// <summary>转码容器（带 `TranscodingProfiles` 的 `Container=ts` + `Protocol=hls`；**推断**：HLS 主播放列表）。</summary>
    public const string HlsContainer = "m3u8";

    /// <summary>字幕格式优先级（与 <c>EmbyService.DeviceProfile</c> 的 SubtitleProfiles 一致）。</summary>
    public static readonly string[] SubtitleFormats = { "srt", "ass", "ssa" };

    // ── Image / 缓存上限（**推断**：DESIGN §2 D7 仅要求「容量与清理策略可配」，具体数值无语料实证）──
    //
    // t148（`t140` review 的 M-2）：此前这里是 **500 条 / 256 MiB**，而 `ImageCacheManager` 的构造函数默认值是
    // **20,000 条 / 512 MiB**，且唯一构造点 `Default` 不传容量 ⇒ 常量是**死常量**，读者按它估算磁盘占用会低估
    // （2 倍字节 / 40 倍条数）。现在**唯一真源 = 本文件这两个常量**（数值取当刻实际生效值，不改变运行时行为），
    // `ImageCacheManager` 的默认参数与 `Default` 都从它们取 ⇒ 全仓只有一处定义图片缓存容量。

    /// <summary>图片默认最大高度（像素）。</summary>
    public const int DefaultImageMaxHeight = 480;

    /// <summary>图片缓存条目上限（磁盘）。**唯一真源**（`ImageCacheManager` 用它作默认参数与 `Default` 的实参）。</summary>
    public const int ImageCacheMaxEntries = 20000;

    /// <summary>图片缓存总字节上限（512 MiB）。**唯一真源**（同上）。</summary>
    public const long ImageCacheMaxBytes = 512L * 1024 * 1024;

    /// <summary>图片缓存子目录名（对应 Dart 实证串 `ai_player_images_v1`）。</summary>
    public const string ImageCacheDirectoryName = "ai_player_images_v1";

    // ── 通用磁盘缓存的默认容量（`t158`：原先是内联在三处构造函数默认值里的字面量，现收敛到唯一归属处；
    //    数值与原内联值逐项相同 ⇒ 行为不变）──────────────────────────────────────────

    /// <summary>通用磁盘缓存默认字节上限（256 MiB）—— <c>DiskCacheStore</c> 与 <c>DanmakuDiskCacheStore</c> 的唯一来源。</summary>
    public const long DiskCacheDefaultMaxBytes = 256L * 1024 * 1024;

    /// <summary>通用磁盘缓存默认条目上限（4000）—— <c>DiskCacheStore</c> 的唯一来源。</summary>
    public const int DiskCacheDefaultMaxEntries = 4000;

    /// <summary>弹幕磁盘缓存默认条目上限（8000）—— <c>DanmakuDiskCacheStore</c> 的唯一来源。</summary>
    public const int DanmakuCacheMaxEntries = 8000;

    /// <summary>SWR 快照缓存默认字节上限（16 MiB）—— <c>SwrSnapshotCache</c> 的唯一来源。</summary>
    public const long SwrCacheDefaultMaxBytes = 16L * 1024 * 1024;

    /// <summary>SWR 快照缓存默认条目上限（200）—— <c>SwrSnapshotCache</c> 的唯一来源。</summary>
    public const int SwrCacheDefaultMaxEntries = 200;

    /// <summary>内存态服务器维度缓存条目上限。</summary>
    public const int ScopedCacheMaxEntries = 512;

    /// <summary>内存态缓存默认存活秒数（0 表示不过期）。</summary>
    public const int ScopedCacheDefaultTtlSeconds = 0;

    // ── 跳过片段（HOST_CONTRACT §3.6 / SERVICE_API §6.2）─────────────────────

    /// <summary>片段的「到片尾」哨兵毫秒值（<c>endMs</c> 缺省即表示到片尾，不写哨兵）——**推断**，仅作展示用。</summary>
    public const long SegmentOpenEndMs = -1;

    /// <summary>单条 `--segment=` 参数的分段数量上限（**推断**：命令行长度保护，超限时分多条参数下发）。</summary>
    public const int MaxSegmentsPerArgument = 32;

    // ── 播放进度（HOST_CONTRACT §4 / SERVICE_API §1.4）──────────────────────

    /// <summary>进度上报节流（秒；**推断**：内核 progress 回调实测约 0.5 s 一次，无需逐条上报）。</summary>
    public const double ProgressReportIntervalSeconds = 5.0;

    /// <summary>判定「看完」的阈值（播放进度百分比；**推断**）。</summary>
    public const double PlayedThresholdPercent = 90.0;

    // ── 时间常数 ───────────────────────────────────────────────────────────

    /// <summary>Emby ticks / 秒（SERVICE_API §1.4 实证：`1e7/s`）。</summary>
    public const long TicksPerSecond = 10000000L;

    /// <summary>Emby ticks / 毫秒（同上换算：10000）。</summary>
    public const long TicksPerMillisecond = 10000L;

    /// <summary>毫秒 / 秒。</summary>
    public const int MillisecondsPerSecond = 1000;

    /// <summary>服务器配置 id 前缀（与 <c>ServerConfigStore.NewId()</c> 的 `srv_` 一致）。</summary>
    public const string ServerIdPrefix = "srv_";
}

// S5 体验设施（t33）：图片磁盘缓存 —— 在通用 DiskCacheStore 之上加"取数回调 + 命中/未命中日志"。
// t70（用户当面要求「同时要求图片也缓存」）：新增 **URL → 字节 的单点入口** `GetOrFetchAsync(url)`。
//
// 🔴 为什么必须单点：App 侧四处图片出口曾各自 `new System.Net.Http.HttpClient`（HomePage:40 / DetailPage:268 /
// PersonPage:277 / AggregatePoster:164）⇒ 绕过 `ShellHttpClient` ⇒ **UA/代理/重试/缓存四件事各写一套**（用户可见：
// 自定义 UA 对图片不生效、图片每次进页面重下）。现在统一经本类：取字节只走 `ShellHttpClient`（UA 读
// `UserAgentPolicy.Current`），缓存只走 `DiskCacheStore`。
//
// 键口径（**取舍已写明**）：键 = URL 规范化后（**去掉 `api_key`**、其余 query **按键名排序保留**）。
//   · 去 `api_key`：token 轮换不该让整库图片 100% miss；也不把凭据留在键材料/日志里。
//   · **保留 `tag`/`maxHeight`/`maxWidth`**：它们改变**字节内容**（tag 是服务端图片版本号，尺寸不同字节不同）
//     ⇒ 必须进键，否则会张冠李戴（拿了 A 版本的图当 B 版本、或用 420 的图冒充原图）。代价 = 同一图不同尺寸各存一份
//     —— 这正是"命中率 vs 正确性"的取舍，这里**优先正确性**（上限由 MaxBytes/MaxEntries 兜底，超了走 LRU）。
//
// t306（负缓存收窄）：t297 结单**自登的代价** —— 服务端**整体**返 500 时也被当"该条目没有图"缓存 **600 s**
//   ⇒ 用户看到"海报一片空白、怎么刷新都不出来（最长 10 分钟）"。本卡只收窄**失败分类**，不改正缓存语义：
//   · **孤立** 500（同 host 其他 key 正常）⇒ "该条目确实无图" ⇒ 仍 600 s（t211 判据不变）；
//   · 同 host **20 s 内 ≥3 个不同 key** 的 5xx ⇒ "服务器整体在坏" ⇒ 一律按 Transient（30 s），
//     并把该窗口内**已误判为"无图"**的同 host 条目就地提升（`NEGCACHE PROMOTE`）——
//     否则突发里的前 1–2 个 key 仍按 600 s 长冷却，用户依旧看不到那张图。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Logging;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>图片取数失败的分类（t212；t306 起"500"不再无条件归入 <see cref="NoImage"/>）。判据取自 t211 的当刻复测。</summary>
public enum ImageFetchFailureKind
{
    /// <summary>HTTP **500 且为孤立失败**（同 host 其他 key 正常）：该 id 上没有这张图（Primary 专有信号；其余 ImageType 缺图是 404）。
    /// t211 实测：换 `tag` / 换尺寸参数 / 换 `index` **全部仍 500** ⇒ 换形态重试没有价值，只记负缓存（600 s）。
    /// 🔴 t306：同 host 出现 5xx 突发时**不判**本类（那时是服务端整体在坏，见 <see cref="Transient"/>）。</summary>
    NoImage,

    /// <summary>暂时性失败：超时 / 传输错 / 5xx（非 500）/ **5xx 突发**（同 host 20 s 内 ≥3 个不同 key 失败）
    /// / 4xx（含 404 —— 语义歧义，见 <c>ImageCacheManager.ClassifyFailure</c> 的文档）
    /// ⇒ 允许**一次**降级重试（放宽到原图档），冷却 30 s 后允许再试。</summary>
    Transient,
}

/// <summary>
/// 一条负缓存记录 —— 这就是「**可辨识标记**」：调用方可以据此画"无图/加载失败"占位，
/// 而不是把 <c>null</c> 当静默空白（App 侧现成契约见 <c>Features/Aggregate/Shared/ImageSourceLoader.LoadAsync</c>：
/// <c>null</c> ⇒ 返回 <c>null</c> ⇒ 调用方**保持占位**）。
/// </summary>
public sealed class ImageFetchFailure
{
    public ImageFetchFailureKind Kind { get; init; }

    /// <summary>HTTP 状态码；传输层失败（超时/无响应）为 <c>null</c>。</summary>
    public int? StatusCode { get; init; }

    public DateTimeOffset FirstSeen { get; init; }

    public DateTimeOffset LastSeen { get; set; }

    /// <summary>本冷却窗内失败累计次数（首次 = 1）。</summary>
    public int Attempts { get; set; }

    /// <summary>本类失败的冷却时长（毫秒）。</summary>
    public long CooldownMs { get; init; }

    public bool IsExpired(DateTimeOffset now) => (now - LastSeen).TotalMilliseconds >= CooldownMs;
}

/// <summary>
/// 图片磁盘缓存：<c>url/键 → 字节</c>。命中不调 <c>fetch</c>；未命中才取数并落盘。
/// 目录 = <c>&lt;数据根&gt;\cache\images</c>（<see cref="AppDataDir.CacheDir"/> 之下，与歌词/弹幕缓存同级）。
/// </summary>
public sealed class ImageCacheManager
{
    public const string SubDirectory = "images";

    /// <summary>每次取图的汇总日志前缀（供 UI/门禁互校：<c>IMG-CACHE hit=…</c>）。</summary>
    public const string LogPrefix = "IMG-CACHE";

    /// <summary>单次取图超时（与 App 侧原来那四个裸 <c>HttpClient</c> 的 20 s 一致）。
    /// t118 起**只作通用/兜底**上限（非图片路径语义不变）；图片路径改用 <see cref="DefaultImageFetchTimeout"/>。
    /// 🔴 与 <c>ShellHttpClient</c> 的通用 20 s 是**并行语义、互不引用**：那个是"客户端默认请求超时"，本项是"URL 重载取图的兜底上界"。</summary>
    public static readonly TimeSpan DefaultFetchTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// t118：**图片类专用 deadline**（与通用 20s 分离）。
    /// 依据 = 本机对 `ServerA` 真图 24 张的成功耗时实测（证据
    /// <c>shell/Tests/evidence/img-deadline-proxy-health.txt</c> §1，两臂各一轮）：
    /// 直连 P50 = 359ms / P95 = 1180ms；**经代理**（用户当刻设置 `enabled=True url=http://127.0.0.1:10808`）
    /// P50 = 706ms / P95 = 2120ms ⇒ 取 <c>4 × max(P95) ≈ 8.5s</c> 向下取整 ⇒ **8s**：对两臂的 P95 有 ~3.8× 余量、
    /// 对实测最大值（2120ms）也远有余量，同时比通用 20s 短 2.5×。
    /// 为什么不更激进（3–4s）：经代理那条路的 P95 已达 2120ms，再短会在"活的慢代理"上产生**假失败**。
    /// 目的：慢源（死代理/黑洞/吊住的远端）时让 UI 在**数秒级**拿到"失败/降级"终态，而不是每张图各付 20s。
    /// 调用方仍可显式传 <c>timeout</c> 覆盖（见 4 参重载）；死代理场景另有 `ProxyPolicy` 的快速降级（~0ms）。
    /// </summary>
    public static readonly TimeSpan DefaultImageFetchTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// 进程级默认实例（数据根 = <see cref="AppDataDir.Instance"/>，目录 <c>&lt;数据根&gt;\cache\images</c>）。
    /// **唯一入口** = <see cref="AIPlayer.Shell.Services.ServiceRegistry.Images"/> 或 <see cref="Default"/>
    /// —— 二者是**同一单例**（<c>ServiceRegistry.Images =&gt; ImageCacheManager.Default</c>），
    /// 外壳侧调用点走 <see cref="Default"/> 与走前者**等价**。外壳各屏**禁止**再
    /// <c>new ImageCacheManager(...)</c>（否则又是第二实例 = 第二容量口径）。隔离根场景（自检/测试）
    /// 请自行 <c>new ImageCacheManager(自己的根)</c> —— 复用本实例会写到真实数据根。
    /// </summary>
    public static ImageCacheManager Default => DefaultLazy.Value;

    private static readonly Lazy<ImageCacheManager> DefaultLazy =
        new Lazy<ImageCacheManager>(() => new ImageCacheManager(
            AppDataDir.Instance,
            Constants.AppConstants.ImageCacheMaxBytes,      // t148：容量只从 AppConstants 取（唯一真源）
            Constants.AppConstants.ImageCacheMaxEntries,
            log: m => DebugLog.Info(m)));

    private readonly Action<string> _log;
    private readonly object _httpGate = new object();
    private ShellHttpClient _http;

    // ── t212 失败负缓存（**进程内内存**；不进 DiskCacheStore ⇒ 不占磁盘条目配额、不改命中/容量语义）──
    private readonly object _negGate = new object();
    private readonly Dictionary<string, ImageFetchFailure> _negative =
        new Dictionary<string, ImageFetchFailure>(StringComparer.Ordinal);
    private long _negBlocked;        // 被负缓存拦下的请求数（= 少发的网络请求数）
    private long _failuresRecorded;  // 落进负缓存的失败次数
    private long _degradeAttempts;   // 降级重试发起次数
    private long _degradeSuccesses;  // 降级重试拿到字节的次数

    // ── t306 host 级 5xx 突发检测（区分「该条目确实无图」与「服务器整体在坏」）──
    // 与 `_negative` **共用 `_negGate` 一把锁**（避免两把锁的嵌套锁序问题；两者都是极短的内存操作）。
    private readonly Dictionary<string, List<HostFailure>> _hostFailures =
        new Dictionary<string, List<HostFailure>>(StringComparer.Ordinal);
    private long _bursts;        // 突发判定命中次数（服务端整体在坏的次数）
    private long _promotions;    // 被就地提升（no-image → transient）的条目数

    /// <summary>一条 host 级 5xx 观测（只保留 <see cref="HostBurstWindow"/> 内的）。</summary>
    private readonly struct HostFailure
    {
        public DateTimeOffset At { get; init; }

        public string Key { get; init; }
    }

    /// <summary>
    /// t306 突发判定窗。依据：一屏图（首页行 / 图墙）是**并发**取数，且同 host 的图片取数耗时实测
    /// P95 = 2120 ms（证据 <c>shell/Tests/evidence/img-deadline-proxy-health.txt</c> §1，经代理臂）
    /// ⇒ 20 s 覆盖"一整屏 + 一次重试"的批次跨度，而远小于 600 s 的长冷却。
    /// </summary>
    public static readonly TimeSpan HostBurstWindow = TimeSpan.FromSeconds(20);

    /// <summary>
    /// 判"服务器整体在坏"所需的**不同 key** 数。取 3 的依据：1 个 key 的 500 无法区分"该条目无图"与"服务端坏了"
    /// （两者在调用点形状完全相同 —— 这正是 t297 那条失败模式的成因）；2 个仍可能是同一张图的兄弟请求
    /// （同图不同尺寸/tag 是两个 key）；**≥3 个不同 key 在同一窗口内同时 5xx** 才是"服务端整体"的形态。
    /// </summary>
    public const int HostBurstDistinctKeys = 3;

    /// <summary>host 桶上限（有界：一次突发事件不会把内存吃穿；超出按"该 host 最新观测最早"淘汰）。</summary>
    public const int HostBurstMaxHosts = 64;

    /// <summary>单个 host 窗口内的观测条数上限（超出丢最早的；判据只用"不同 key 数"，丢掉的是同一批重复观测）。</summary>
    private const int HostBurstMaxObservations = 64;

    /// <summary>突发判定命中次数（= 判过几次"服务端整体在坏"；0 也是读数）。</summary>
    public long BurstDetections => Interlocked.Read(ref _bursts);

    /// <summary>被就地由「无图」提升为「暂时性」的条目数（t306 的关键一步，见 <see cref="PromoteNoImageLocked"/>）。</summary>
    public long Promotions => Interlocked.Read(ref _promotions);

    // ── t256 single-flight（**同 key 并发合流**）────────────────────────────────
    // 冷启图墙 12 张卡请求**同一个 URL** 时，修前各自调用 fetch（12 次网络 + 最多 12 次同文件 STORE 覆盖）。
    // 现在：同 key 的第 2..N 个调用方**不发起取数**，而是等待领跑者的同一个结果 ⇒ 只取一次、只落一次盘。
    private readonly object _flightGate = new object();
    private readonly Dictionary<string, Flight> _inFlight = new Dictionary<string, Flight>(StringComparer.Ordinal);
    private long _sfStarted;     // 领跑次数（= 建单飞条目数）
    private long _sfJoined;      // 合流次数（跟随者数）
    private long _sfFetchCalls;  // **真实取数调用次数**（"N 并发同 key ⇒ 1" 的判据读数）
    private long _sfSuccesses;   // 领跑拿到非空字节的次数
    private long _sfFailures;    // 领跑失败/空结果的次数

    /// <summary>一个在途取数条目：领跑者完成后把**结果或异常**写进 <see cref="Completion"/>，所有跟随者共享同一结果。</summary>
    private sealed class Flight
    {
        public TaskCompletionSource<byte[]> Completion { get; } =
            new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// t269：单飞的**逐调用方**结果 —— 多带回两个字段（本调用方**是否合流**、等了多久），
    /// 供 URL 版打 per-caller 汇总行；**不改变取数语义**（字节、命中/合流判定一字未动）。
    /// </summary>
    private readonly struct FlightOutcome
    {
        public byte[] Bytes { get; init; }

        public bool Joined { get; init; }

        public long WaitMs { get; init; }
    }

    /// <summary>领跑（真正取数）次数 = 单飞条目数。跟随者不计入。</summary>
    public long SingleFlightStarted => Interlocked.Read(ref _sfStarted);

    /// <summary>合流（等待并复用领跑结果）次数。</summary>
    public long SingleFlightJoined => Interlocked.Read(ref _sfJoined);

    /// <summary>**真实取数调用次数**（交给 fetch / HTTP 的次数）——「N 并发同 key ⇒ 必为 1」的判据读数。</summary>
    public long SingleFlightFetchCalls => Interlocked.Read(ref _sfFetchCalls);

    /// <summary>领跑成功（拿到非空字节）次数。</summary>
    public long SingleFlightSuccesses => Interlocked.Read(ref _sfSuccesses);

    /// <summary>领跑失败（异常或空结果）次数。</summary>
    public long SingleFlightFailures => Interlocked.Read(ref _sfFailures);

    /// <summary>当刻在途条目数（收敛到 0 才算"没有悬挂"）。</summary>
    public int SingleFlightInFlight { get { lock (_flightGate) return _inFlight.Count; } }

    /// <summary>
    /// 构造。t148：容量默认值改为直接引用 <see cref="Constants.AppConstants"/> 的常量
    /// （此前这里硬编 512 MiB/20,000，而 `AppConstants` 又写着 256 MiB/500 ⇒ 两套口径，见 M-2）。
    /// </summary>
    public ImageCacheManager(AppDataDir data,
        long maxBytes = Constants.AppConstants.ImageCacheMaxBytes,
        int maxEntries = Constants.AppConstants.ImageCacheMaxEntries,
        Action<string> log = null,
        ShellHttpClient http = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        _log = log;
        _http = http;
        Store = new DiskCacheStore(System.IO.Path.Combine(data.CacheDir, SubDirectory), maxBytes, maxEntries, log);
    }

    public DiskCacheStore Store { get; }

    /// <summary>
    /// 取图用的 HTTP 客户端（**UA/重试/代理的唯一出口**）。
    /// · 不注入 ⇒ 首次用到时惰性 `new ShellHttpClient()`（其 <c>UserAgent</c> 读 <see cref="UserAgentPolicy.Current"/>）；
    /// · 将来代理统一接线时，**注入同一个 client** 即可 —— **不要在图片路径再写第二套客户端**。
    /// </summary>
    public ShellHttpClient Http
    {
        get
        {
            if (_http != null) return _http;
            lock (_httpGate)
            {
                return _http ??= new ShellHttpClient();
            }
        }
        set => _http = value;
    }

    // ── t212 负缓存：公开读数与自检注入点 ─────────────────────────────────────

    /// <summary>「无图」（HTTP 500）冷却时长（秒）。**默认只从 <see cref="Constants.AppConstants"/> 取**（唯一真源）；
    /// 自检/夹具要验证"冷却到期后允许再试一次"时可临时调小，产品路径不得改。</summary>
    public int NoImageCooldownSeconds { get; set; } = Constants.AppConstants.ImageNegativeCacheNoImageSeconds;

    /// <summary>「暂时性失败」冷却时长（秒）。同上：默认单源，仅自检可调小。</summary>
    public int TransientCooldownSeconds { get; set; } = Constants.AppConstants.ImageNegativeCacheTransientSeconds;

    /// <summary>负缓存条数上限（同上）。</summary>
    public int NegativeCacheMaxEntries { get; set; } = Constants.AppConstants.ImageNegativeCacheMaxEntries;

    /// <summary>当前负缓存条数（进程内内存，不占磁盘条目配额）。</summary>
    public int NegativeCacheCount { get { lock (_negGate) return _negative.Count; } }

    /// <summary>被负缓存拦下的请求数（= 本来会发出去的网络请求数）。</summary>
    public long NegativeCacheBlocked => Interlocked.Read(ref _negBlocked);

    /// <summary>累计落进负缓存的失败次数。</summary>
    public long FailureRecords => Interlocked.Read(ref _failuresRecorded);

    /// <summary>降级重试发起次数。</summary>
    public long DegradeAttempts => Interlocked.Read(ref _degradeAttempts);

    /// <summary>降级重试拿到字节的次数（"降级真的救回来了"读数）。</summary>
    public long DegradeSuccesses => Interlocked.Read(ref _degradeSuccesses);

    /// <summary>
    /// 查询某个 URL/键当刻的失败标记 —— 这是「**可辨识占位**」的接口面：
    /// 返回 <c>true</c> ⇒ 调用方应画"无图 / 加载失败"占位，而不是把 <c>null</c> 当静默空白。
    /// </summary>
    public bool TryGetFailure(string url, out ImageFetchFailure failure)
    {
        var key = NormalizeKey(url);
        lock (_negGate)
        {
            if (_negative.TryGetValue(key, out var found) && !found.IsExpired(DateTimeOffset.UtcNow))
            {
                failure = found;
                return true;
            }
        }
        failure = null;
        return false;
    }

    /// <summary>清空负缓存（不影响磁盘条目）。<see cref="Clear"/> 也会调用它 ——「清空本图片缓存」含失败态。
    /// t306：**一并清 host 突发窗**（否则"清理"之后残留的突发史仍会把新失败当突发，用户等了却没换来干净的判定）。</summary>
    public void ClearNegativeCache()
    {
        lock (_negGate)
        {
            _negative.Clear();
            _hostFailures.Clear();
        }
    }

    // ── 键规范化 ────────────────────────────────────────────────────────────

    /// <summary>
    /// URL → 缓存键：去 `api_key`（大小写不敏感），其余 query **按键名排序**保留；**非 http(s) 绝对 URL** 退化为"原文去 api_key"。
    /// 公开出来是为了让调用方/自检能复算"同一图是否同键"。
    ///
    /// 🔴 t82 修正（**回归点**）：判据从"`Uri.TryCreate` 成功即当 URL"收紧为"**scheme ∈ {http,https} 且 Host 非空**"。
    /// 为什么：`Uri.TryCreate("item:123:Primary", Absolute)` **会成功**（scheme=`item`、Host 为空），于是原实现把它
    /// 重建成 `item://123:Primary` —— 即**不透明键被 mangle**，而它本该原样保留（否则同一不透明键在"规范化/未规范化"
    /// 两条路径下又变成两个键，正是本卡要消灭的那类问题）。收紧后：`item:123:Primary`、`ItemId=abc&ImageType=Primary`、
    /// `C:\path\x.png` 一律走文本分支 ⇒ **原样返回**；真正的图片 URL 仍照旧排序+去凭据。
    /// </summary>
    public static string NormalizeKey(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Host.Length == 0
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return StripApiKeyText(trimmed);
        }

        var pairs = new List<KeyValuePair<string, string>>();
        var query = uri.Query;
        if (query.Length > 1)
        {
            foreach (var part in query.Substring(1).Split('&'))
            {
                if (part.Length == 0) continue;
                var eq = part.IndexOf('=');
                var name = eq < 0 ? part : part.Substring(0, eq);
                var value = eq < 0 ? string.Empty : part.Substring(eq + 1);
                if (IsCredentialParam(name)) continue;
                pairs.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        var sb = new StringBuilder();
        sb.Append(uri.Scheme).Append("://").Append(uri.Authority).Append(uri.AbsolutePath);
        if (pairs.Count > 0)
        {
            sb.Append('?');
            sb.Append(string.Join("&", pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)
                .Select(p => p.Key + "=" + p.Value)));
        }
        return sb.ToString();
    }

    private static bool IsCredentialParam(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        switch (name.ToLowerInvariant())
        {
            case "api_key":
            case "apikey":
            case "x-emby-token":
            case "token":
            case "access_token":
                return true;
            default:
                return false;
        }
    }

    private static string StripApiKeyText(string text)
        => System.Text.RegularExpressions.Regex.Replace(text,
            "(?i)(api_key|apikey|x-emby-token|token|access_token)=[^&\\s]*", string.Empty)
            .TrimEnd('&', '?');

    /// <summary>键的稳定短标识（sha1 前 12 位）——日志里只印它，不印 URL/凭据。</summary>
    public static string KeyTag(string key)
    {
        if (string.IsNullOrEmpty(key)) return "-";
        var hash = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(key));
        var sb = new StringBuilder(12);
        for (var i = 0; i < 6; i++) sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    // ── 取数 ────────────────────────────────────────────────────────────────

    // ── t212 负缓存实现（内存；与磁盘条目的命中/容量语义相互独立）──────────────

    /// <summary>冷却窗内该 key 是否已被判定失败（命中则**不发任何请求**）；顺带把过期记录清掉（有界重试）。</summary>
    private bool IsNegativeCached(string key, out ImageFetchFailure failure)
    {
        var now = DateTimeOffset.UtcNow;
        ImageFetchFailure expired = null;
        lock (_negGate)
        {
            if (_negative.TryGetValue(key, out var found))
            {
                if (!found.IsExpired(now))
                {
                    failure = found;
                    return true;
                }
                _negative.Remove(key);   // 冷却到期 ⇒ 允许再试一次（"不无限重打"的另一半：重打必须有界）
                expired = found;
            }
        }
        if (expired != null)
        {
            // t297：**过期也可见** —— 与"写入"（`FAIL … cooldownMs=` 行）、"命中"（`NEGCACHE hit=true …` 行）
            // 凑成三态可区分：写入 / 命中 / 过期。只在锁外打日志（不把 IO 关进 _negGate）。
            _log?.Invoke($"{LogPrefix} NEGCACHE-EXPIRE key={KeyTag(key)}"
                + $" kind={KindLabel(expired.Kind)}"
                + $" ageMs={(long)(now - expired.LastSeen).TotalMilliseconds} cooldownMs={expired.CooldownMs}");
        }
        failure = null;
        return false;
    }

    /// <summary>
    /// 记一条失败（**唯一入口**，t306 起）：分类（host 感知）+ 写负缓存，返回落盘的快照。
    /// 调用方**必须**用返回的快照打日志 —— 突发检测会改写类别（`500` 在突发里是 <c>transient</c>，不是 <c>no-image</c>）。
    /// 分类规则（三条，逐条有实测支撑，见类头注释）：
    /// <list type="number">
    /// <item><c>500</c> 且**孤立**（同 host 无 5xx 突发）⇒ <see cref="ImageFetchFailureKind.NoImage"/>（600 s，t211 判据不变）；</item>
    /// <item>同 host <see cref="HostBurstWindow"/> 内 ≥<see cref="HostBurstDistinctKeys"/> 个不同 key 的 <b>5xx</b>
    ///   ⇒ <see cref="ImageFetchFailureKind.Transient"/>（30 s）+ 就地提升同窗口内已误判的"无图"条目；</item>
    /// <item>其余（非 500 的 5xx / 4xx / 超时传输失败）⇒ <see cref="ImageFetchFailureKind.Transient"/>（30 s，与 t297 一致）。</item>
    /// </list>
    /// </summary>
    private ImageFetchFailure RecordFailure(string key, int? statusCode)
    {
        var now = DateTimeOffset.UtcNow;
        var host = HostOf(key);
        ImageFetchFailure snapshot;
        var burst = false;
        List<string> promoted = null;
        lock (_negGate)
        {
            // ① 名义类别：500 = "该条目没有图"（t211）；其余 5xx / 4xx / 无状态码 = 暂时性。
            var kind = statusCode == 500 ? ImageFetchFailureKind.NoImage : ImageFetchFailureKind.Transient;

            // ② 5xx 先登记进 host 窗口；判到突发 ⇒ 这不是"条目无图"，而是"服务端整体在坏"。
            if (statusCode.HasValue && statusCode.Value >= 500 && RegisterBurstLocked(host, key, now))
            {
                burst = true;
                kind = ImageFetchFailureKind.Transient;
                promoted = PromoteNoImageLocked(host, now);
            }

            var previous = _negative.TryGetValue(key, out var found) ? found : null;
            var attempts = (previous != null && previous.Kind == kind) ? previous.Attempts + 1 : 1;
            var cooldownSeconds = Math.Max(0,
                kind == ImageFetchFailureKind.NoImage ? NoImageCooldownSeconds : TransientCooldownSeconds);

            snapshot = new ImageFetchFailure
            {
                Kind = kind,
                StatusCode = statusCode,
                FirstSeen = previous != null ? previous.FirstSeen : now,
                LastSeen = now,
                Attempts = attempts,
                CooldownMs = (long)TimeSpan.FromSeconds(cooldownSeconds).TotalMilliseconds,
            };
            _negative[key] = snapshot;
            if (_negative.Count > Math.Max(1, NegativeCacheMaxEntries)) TrimNegativeCache(now);
            if (burst) Interlocked.Increment(ref _bursts);
        }

        Interlocked.Increment(ref _failuresRecorded);
        // 新增的三态行都**追加**，不改既有 `FAIL` / `NEGCACHE hit` / `NO-IMAGE` 的形状与字段顺序。
        if (burst)
        {
            _log?.Invoke($"{LogPrefix} NEGCACHE BURST host={host} distinctKeys={HostBurstDistinctKeys}"
                + $" windowMs={(long)HostBurstWindow.TotalMilliseconds} kind=transient promoted={promoted?.Count ?? 0} bursts={BurstDetections}");
        }
        if (promoted != null)
        {
            var promotedMs = (long)TimeSpan.FromSeconds(Math.Max(0, TransientCooldownSeconds)).TotalMilliseconds;
            foreach (var promotedKey in promoted)
                _log?.Invoke($"{LogPrefix} NEGCACHE PROMOTE key={KeyTag(promotedKey)} host={host}"
                    + $" no-image->transient cooldownMs={promotedMs}");
        }
        _log?.Invoke($"{LogPrefix} FAIL key={KeyTag(key)} kind={KindLabel(snapshot.Kind)}"
            + $" status={(statusCode.HasValue ? statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}"
            + $" attempts={snapshot.Attempts} negCache={NegativeCacheCount} cooldownMs={snapshot.CooldownMs} blocked={NegativeCacheBlocked}");
        return snapshot;
    }

    /// <summary>
    /// key → 突发检测桶（t306）：http(s) 绝对 URL ⇒ <c>host</c>（非默认端口写成 <c>host:port</c> —— 不同端口是不同服务端）；
    /// 不透明键（<c>item:123:Primary</c> / 本地路径）⇒ 单一桶 <c>(opaque)</c>。
    /// 取舍已写明：透明键共桶 ⇒ 极端情形下会把"真无图"误判成"暂时性" = 多试一次（方向安全，代价见证据件的放大读数）。
    /// </summary>
    private static string HostOf(string key)
    {
        if (!string.IsNullOrEmpty(key)
            && Uri.TryCreate(key, UriKind.Absolute, out var uri)
            && uri.Host.Length > 0
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return uri.IsDefaultPort
                ? uri.Host
                : uri.Host + ":" + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return "(opaque)";
    }

    /// <summary>把一次 5xx 登记进 host 窗口，并回答"这算不算服务端整体在坏"。**调用方持 <c>_negGate</c>**。</summary>
    private bool RegisterBurstLocked(string host, string key, DateTimeOffset now)
    {
        if (!_hostFailures.TryGetValue(host, out var observed))
        {
            observed = new List<HostFailure>();
            _hostFailures[host] = observed;
        }
        observed.RemoveAll(o => now - o.At > HostBurstWindow);                       // 窗外的丢掉（窗口滑动）
        observed.Add(new HostFailure { At = now, Key = key });
        if (observed.Count > HostBurstMaxObservations) observed.RemoveRange(0, observed.Count - HostBurstMaxObservations);
        if (_hostFailures.Count > HostBurstMaxHosts) TrimHostFailuresLocked(host);
        return observed.Select(o => o.Key).Distinct(StringComparer.Ordinal).Count() >= HostBurstDistinctKeys;
    }

    /// <summary>
    /// t306 的关键一步：把**同一 host、同窗口内已判为「无图」**的条目就地提升为「暂时性」。
    /// 为什么必须做：突发是"第 3 个 key 才判出来"的 —— 前 1–2 个 key 已经按 600 s 写进负缓存了；
    /// 不提升的话，用户那两张图依旧 10 分钟不回来（= 本卡要消灭的失败模式仍在）。**调用方持 <c>_negGate</c>**。
    /// </summary>
    private List<string> PromoteNoImageLocked(string host, DateTimeOffset now)
    {
        List<string> promoted = null;
        var promotedMs = (long)TimeSpan.FromSeconds(Math.Max(0, TransientCooldownSeconds)).TotalMilliseconds;
        foreach (var pair in _negative.ToList())
        {
            var record = pair.Value;
            if (record.Kind != ImageFetchFailureKind.NoImage) continue;
            if (now - record.LastSeen > HostBurstWindow) continue;                   // 只提升同一突发窗内的
            if (!string.Equals(HostOf(pair.Key), host, StringComparison.Ordinal)) continue;
            _negative[pair.Key] = new ImageFetchFailure
            {
                Kind = ImageFetchFailureKind.Transient,
                StatusCode = record.StatusCode,
                FirstSeen = record.FirstSeen,
                LastSeen = now,                                                      // 短窗从"判明突发"的当刻起算
                Attempts = record.Attempts,                                          // 次数**保留**（提升不是重来）
                CooldownMs = promotedMs,
            };
            (promoted ??= new List<string>()).Add(pair.Key);
            Interlocked.Increment(ref _promotions);
        }
        return promoted;
    }

    /// <summary>host 桶上限兜底：按"该 host 最新观测最早"淘汰，**不淘汰本次刚写入的 host**。调用方持锁。</summary>
    private void TrimHostFailuresLocked(string keepHost)
    {
        var drop = _hostFailures
            .Where(p => !string.Equals(p.Key, keepHost, StringComparison.Ordinal))
            .OrderBy(p => p.Value.Count == 0 ? DateTimeOffset.MinValue : p.Value[p.Value.Count - 1].At)
            .Take(_hostFailures.Count - HostBurstMaxHosts)
            .Select(p => p.Key)
            .ToList();
        foreach (var stale in drop) _hostFailures.Remove(stale);
    }

    /// <summary>日志里的类别串（**唯一拼写点**：<c>no-image</c> / <c>transient</c>；既有行格式不变）。</summary>
    private static string KindLabel(ImageFetchFailureKind kind)
        => kind == ImageFetchFailureKind.NoImage ? "no-image" : "transient";

    /// <summary>上限兜底：先清过期，再按 <c>LastSeen</c> 最早先淘汰（与磁盘 LRU 同一取舍方向）。调用方持锁。</summary>
    private void TrimNegativeCache(DateTimeOffset now)
    {
        foreach (var stale in _negative.Where(p => p.Value.IsExpired(now)).Select(p => p.Key).ToList()) _negative.Remove(stale);
        var cap = Math.Max(1, NegativeCacheMaxEntries);
        if (_negative.Count <= cap) return;
        foreach (var oldest in _negative.OrderBy(p => p.Value.LastSeen).Take(_negative.Count - cap).Select(p => p.Key).ToList())
            _negative.Remove(oldest);
    }

    private void ClearFailure(string key)
    {
        lock (_negGate) _negative.Remove(key);
    }

    /// <summary>
    /// t256 **单飞（single-flight）**：同一个 <paramref name="key"/> 上并发的取数**只跑一次** <paramref name="work"/>，
    /// 其余调用方等待并复用同一个结果；**失败（异常）同样广播给所有等待者** ⇒ 不会有调用方永久悬挂。
    /// 🔴 语义边界（写死，改前/改后差异只在这两条）：
    /// · 共享的那一次取数**不绑任一调用方的 token**（否则一个调用方取消会连带毁掉其余等待者的结果）；
    ///   取消语义仍由**每个调用方自己**的 <c>WaitAsync(token)</c> 承担：取消方照样拿到取消，其余人不受影响。
    /// · 合流发生在"磁盘未命中且不在负缓存冷却窗内"**之后** ⇒ 命中路径与负缓存路径的**每调用方**行为与日志一字未变。
    /// </summary>
    private async Task<FlightOutcome> RunSingleFlightAsync(string key, Func<Task<byte[]>> work, CancellationToken cancellationToken)
    {
        Flight flight;
        var leader = false;
        var joiners = 0L;
        var inFlight = 0;
        lock (_flightGate)
        {
            if (_inFlight.TryGetValue(key, out var existing))
            {
                flight = existing;
                joiners = Interlocked.Increment(ref _sfJoined);
            }
            else
            {
                flight = new Flight();
                _inFlight[key] = flight;
                leader = true;
                Interlocked.Increment(ref _sfStarted);
            }
            inFlight = _inFlight.Count;
        }

        _log?.Invoke(leader
            ? $"{LogPrefix} SINGLEFLIGHT start key={KeyTag(key)} started={SingleFlightStarted} inFlight={inFlight}"
            : $"{LogPrefix} SINGLEFLIGHT join key={KeyTag(key)} joiners={joiners} inFlight={inFlight}");

        if (leader) { _ = RunLeaderAsync(key, flight, work); }

        var wait = System.Diagnostics.Stopwatch.StartNew();
        var bytes = await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        wait.Stop();
        return new FlightOutcome { Bytes = bytes, Joined = !leader, WaitMs = wait.ElapsedMilliseconds };
    }

    /// <summary>领跑者：**真跑一次**取数，把结果或异常写进共享条目。任何异常都不逃逸（它是 fire-and-forget 的 Task）。</summary>
    private async Task RunLeaderAsync(string key, Flight flight, Func<Task<byte[]>> work)
    {
        byte[] result = null;
        Exception error = null;
        try
        {
            Interlocked.Increment(ref _sfFetchCalls);   // 真实取数次数（判据读数）
            result = await work().ConfigureAwait(false);
        }
        catch (Exception ex) { error = ex; }

        var ok = error == null && result != null && result.Length > 0;
        if (error == null)
        {
            if (ok) { Interlocked.Increment(ref _sfSuccesses); } else { Interlocked.Increment(ref _sfFailures); }
            // 先完成、再摘除：这个窗口内新到的调用方复用一个**已完成**的结果，而不是再发一次取数。
            flight.Completion.TrySetResult(result);
        }
        else
        {
            Interlocked.Increment(ref _sfFailures);
            flight.Completion.TrySetException(error);
        }
        lock (_flightGate) { _inFlight.Remove(key); }

        _log?.Invoke($"{LogPrefix} SINGLEFLIGHT done key={KeyTag(key)} ok={ok} started={SingleFlightStarted}"
            + $" joined={SingleFlightJoined} fetches={SingleFlightFetchCalls} failures={SingleFlightFailures}"
            + $" inFlight={SingleFlightInFlight}");
    }

    /// <summary>失败类别（t306 起在 <see cref="RecordFailure"/> 内联判定）：见该方法的分类规则。</summary>

    /// <summary>
    /// 降级形态（"放宽到原图档"）：去掉 <c>maxWidth</c> / <c>maxHeight</c> / <c>tag</c> 与路径末尾的 <c>/index</c>；
    /// 保留 <c>api_key</c> 与其余参数。非 http(s) 绝对 URL 无可降级 ⇒ 原样返回（调用方据此跳过重试）。
    /// </summary>
    public static string DegradedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Host.Length == 0
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed;
        }

        var kept = new List<KeyValuePair<string, string>>();
        var query = uri.Query;
        if (query.Length > 1)
        {
            foreach (var part in query.Substring(1).Split('&'))
            {
                if (part.Length == 0) continue;
                var eq = part.IndexOf('=');
                var name = eq < 0 ? part : part.Substring(0, eq);
                if (IsDegradeParam(name)) continue;
                kept.Add(new KeyValuePair<string, string>(name, eq < 0 ? string.Empty : part.Substring(eq + 1)));
            }
        }

        var path = System.Text.RegularExpressions.Regex.Replace(uri.AbsolutePath, @"/\d+$", string.Empty);
        var sb = new StringBuilder();
        sb.Append(uri.Scheme).Append("://").Append(uri.Authority).Append(path);
        if (kept.Count > 0)
        {
            sb.Append('?');
            sb.Append(string.Join("&", kept.Select(p => p.Key + (p.Value.Length > 0 ? "=" + p.Value : string.Empty))));
        }
        return sb.ToString();
    }

    private static bool IsDegradeParam(string name)
    {
        switch ((name ?? string.Empty).ToLowerInvariant())
        {
            case "maxwidth":
            case "maxheight":
            case "tag":
                return true;
            default:
                return false;
        }
    }

    /// <summary>命中/未命中都会写日志（<c>IMAGE-CACHE-HIT</c> / <c>IMAGE-CACHE-MISS</c>）。</summary>
    public byte[] TryGet(string key)
    {
        var data = Store.TryRead(key);
        _log?.Invoke(data == null ? $"IMAGE-CACHE-MISS key={Short(key)}" : $"IMAGE-CACHE-HIT key={Short(key)} bytes={data.Length}");
        return data;
    }

    /// <summary>
    /// 命中 ⇒ 直接返回（**不调用 <paramref name="fetch"/>**）；未命中 ⇒ 取数 + 落盘。
    /// 🔴 t82：**入参先过 <see cref="NormalizeKey"/>**（幂等；不透明键原样保留）—— 这样"传 URL 当键"与
    /// `GetOrFetchAsync(url, …)` 两个重载对**同一 URL 得到同一个键**（修前 key 重载用原文 ⇒ api_key 轮换即 100% miss、
    /// 同图双份缓存）。传不透明键（如 `item:123:Primary`）的调用方行为不变。
    /// </summary>
    public Task<byte[]> GetOrFetchAsync(string key, Func<Task<byte[]>> fetch, CancellationToken cancellationToken = default)
        => GetOrFetchAsync(key, fetch, DefaultImageFetchTimeout, cancellationToken);   // t118：图片路径走**专用** deadline（原为通用 20s）

    /// <summary>
    /// t107（取数路径必须处处有界）：**给调用方传入的 <paramref name="fetch"/> 一个可判定的界**。
    /// 修前是裸 <c>await fetch()</c> ⇒ 远端吊住（或 <paramref name="fetch"/> 永不完成）时调用方**无限等待**——
    /// 这正是"详情页静默挂起"的服务层形态。现在到点抛 <see cref="TimeoutException"/> ⇒ 本方法捕获、
    /// 落一行 <c>IMG-CACHE-FETCH-TIMEOUT key=&lt;sha1 前 12&gt; elapsed=… timeout=… hit=false</c>，并返回 <c>null</c>
    /// （与 URL 重载的失败语义一致：**不抛**，调用方按 null 走占位块）。
    /// <b>其余异常照旧向上抛</b>（不改既有契约）；调用方主动取消（<paramref name="cancellationToken"/> 触发）也照旧上抛。
    /// </summary>
    public async Task<byte[]> GetOrFetchAsync(string key, Func<Task<byte[]>> fetch, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeKey(key);
        var cached = TryGet(normalized);
        if (cached != null) return cached;

        if (fetch == null) return null;

        // t212：冷却窗内直接回 null 且**不调用 fetch** ⇒ 同一 URL 在同一会话内不会被无限重打。
        if (IsNegativeCached(normalized, out var known))
        {
            var blocked = Interlocked.Increment(ref _negBlocked);
            _log?.Invoke($"{LogPrefix} NEGCACHE hit=true key={KeyTag(normalized)}"
                + $" kind={KindLabel(known.Kind)}"
                + $" status={(known.StatusCode.HasValue ? known.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}"
                + $" fails={known.Attempts} ageMs={(long)(DateTimeOffset.UtcNow - known.LastSeen).TotalMilliseconds}"
                + $" cooldownMs={known.CooldownMs} blocked={blocked} fetched=0");
            return null;
        }

        // t256：未命中 ⇒ 同 key 并发**合流为一次取数**（见 RunSingleFlightAsync 的语义边界）。
        return (await RunSingleFlightAsync(normalized, () => FetchViaDelegateAsync(normalized, fetch, timeout), cancellationToken).ConfigureAwait(false)).Bytes;
    }

    /// <summary>单飞共享工作单元（<c>fetch</c> 委托版）：除"不绑调用方 token"外与修前逐字同语义（超时 ⇒ 记账 + 日志 + 回 null；其余异常 ⇒ 记账 + 上抛给**所有**等待者）。</summary>
    private async Task<byte[]> FetchViaDelegateAsync(string key, Func<Task<byte[]>> fetch, TimeSpan timeout)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        byte[] fresh;
        try
        {
            fresh = await fetch().WaitAsync(timeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            RecordFailure(key, null);   // 无状态码 = 传输层失败 ⇒ 暂时性（t306 分类不变）
            _log?.Invoke($"IMG-CACHE-FETCH-TIMEOUT key={KeyTag(key)} elapsed={started.ElapsedMilliseconds}ms timeout={(long)timeout.TotalMilliseconds}ms hit=false fetched=0");
            return null;
        }
        catch (Exception ex)
        {
            // 失败要**记账**（负缓存），但**不改契约**：其余异常由单飞条目广播给所有等待者（每个调用方照旧收到同一个异常）。
            RecordFailure(key, (ex as ShellHttpException)?.StatusCode);
            throw;
        }
        if (fresh != null && fresh.Length > 0)
        {
            ClearFailure(key);
            Store.Write(key, fresh);
            _log?.Invoke($"IMAGE-CACHE-STORE key={Short(key)} bytes={fresh.Length}");
        }
        return fresh;
    }

    /// <summary>
    /// **单点入口（t70）**：URL → 字节。命中不联网；未命中经 <see cref="Http"/> 取字节并落盘。
    /// 每次调用**必打一行**汇总：<c>IMG-CACHE hit=… miss=… status=… fetched=… entries=… bytesTotal=… cap=… key=…</c>
    /// （含失败路径；"命中"由 <c>hit=true</c> 且 <c>fetched=0</c> 可判，UI 侧可观测）。
    /// 🔴 **日志只印 <c>key=&lt;sha1 前 12&gt;</c>，绝不印 URL**（URL 里带 <c>api_key</c>；调用方自己知道传的是哪个 URL，
    /// 需要定位时按 KeyTag 与 `CACHE-MISS/STORE` 行的文件名（SHA1 全量）互校）。
    /// 失败语义：**返回 <c>null</c> + 可见日志**（不抛）—— 调用方按 null 走占位块。
    /// </summary>
    public async Task<byte[]> GetOrFetchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _log?.Invoke($"{LogPrefix} hit=false miss=false skip=empty-url");
            return null;
        }

        var key = NormalizeKey(url);
        var cached = Store.TryRead(key);
        if (cached != null)
        {
            var s = Store.Stats();
            _log?.Invoke($"{LogPrefix} hit=true miss=false status=- fetched=0 bytes={cached.Length} entries={s.Count} bytesTotal={s.Bytes} cap={Store.MaxEntries}/{Store.MaxBytes} key={KeyTag(key)}");
            return cached;
        }

        // t212：冷却窗内 ⇒ 不再发请求（这是"同一 URL 在同会话内不无限重打"的落点）。
        if (IsNegativeCached(key, out var known))
        {
            var blocked = Interlocked.Increment(ref _negBlocked);
            _log?.Invoke($"{LogPrefix} NEGCACHE hit=true key={KeyTag(key)}"
                + $" kind={KindLabel(known.Kind)}"
                + $" status={(known.StatusCode.HasValue ? known.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}"
                + $" fails={known.Attempts} ageMs={(long)(DateTimeOffset.UtcNow - known.LastSeen).TotalMilliseconds}"
                + $" cooldownMs={known.CooldownMs} blocked={blocked} fetched=0");
            return null;
        }

        // t256：同 key 并发**合流为一次取数**（共享工作 = 修前的取数体；取消语义由本方的 WaitAsync 承担）。
        try
        {
            var outcome = await RunSingleFlightAsync(key, () => FetchViaUrlAsync(url, key), cancellationToken).ConfigureAwait(false);
            if (outcome.Joined)
            {
                // t269：**URL 版 per-caller 汇总（合流调用方）**—— 本调用方**没有**发取数（fetched=0），
                // 复用了领跑者的结果；逐 caller 打一行，回答两个问题：① 这次调用是否 joined；② 它的 URL 是否**带版本参数**（`tag=`）。
                // 🔴 只印 `key=<sha1 前 12>` 与布尔，**不印 URL/凭据**（与既有纪律一致）；领跑者的汇总行由 FetchViaUrlAsync 打（含 entries/cap）。
                _log?.Invoke($"{LogPrefix} hit=false miss=true joined=true fetched=0 bytes={outcome.Bytes?.Length ?? 0}"
                    + $" tagged={HasVersionTag(key)} waitMs={outcome.WaitMs} key={KeyTag(key)}");
            }
            return outcome.Bytes;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 取消**不是失败**（不污染负缓存），且**不改既有契约**：修前这里也被通用 catch 吞成 null。
            // （"在带 CancellationToken 的异步路径上应显式区分 OCE" 属 H3b 人类评审项，改它要动 App 侧各调用点的
            //   try/catch ⇒ 不在本卡 inScope，已写进证据件的未验证/边界。）
            _log?.Invoke($"{LogPrefix} hit=false miss=true canceled=true fetched=0 key={KeyTag(key)}");
            return null;
        }
    }

    /// <summary>
    /// 单飞共享工作单元（URL 版）：由 <see cref="RunSingleFlightAsync"/> 领跑者**执行一次**。
    /// 与修前逐字同语义，唯一差别 = **不绑调用方 token**（理由见 <see cref="RunSingleFlightAsync"/> 的语义边界）。
    /// </summary>
    private async Task<byte[]> FetchViaUrlAsync(string url, string key)
    {
        try
        {
            // t107：**retries: 0** —— `ShellHttpClient` 默认重试 1 次 ⇒ 远端吊住时会吃 **2×** 上界
            // （夹具实测 40,336ms ≈ 2×20s）。图片取数"多试一次"没有价值：失败已由本方法的可见日志承担，
            // 目标口径是"远端吊住最多导致**一次**有日志的超时"。
            // t118：上界从通用 20s 换成**图片专用** `DefaultImageFetchTimeout`（更短，见其文档）⇒ 一张慢图最多让 UI 等数秒。
            // t212：重试决策交给本方法（见下三条 catch）—— 500 = 无图终态（不重试），暂时性失败才降级重试一次。
            var res = await Http.GetAsync(url, timeout: DefaultImageFetchTimeout, retries: 0, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            var fresh = res?.Bytes;
            if (fresh != null && fresh.Length > 0)
            {
                ClearFailure(key);
                Store.Write(key, fresh);
            }
            var after = Store.Stats();
            _log?.Invoke($"{LogPrefix} hit=false miss=true status={res?.StatusCode} fetched={fresh?.Length ?? 0} contentType={res?.Header("Content-Type") ?? "-"}"
                + $" entries={after.Count} bytesTotal={after.Bytes} cap={Store.MaxEntries}/{Store.MaxBytes} key={KeyTag(key)}");
            return fresh;
        }
        catch (ShellHttpException ex) when (ex.StatusCode == 500)
        {
            // 🔴 t211 定死的触发条件：HTTP 500 = 「该 id 上没有这张图」（Primary 专有；其余 ImageType 是 404）。
            //    换 tag / 换尺寸参数 / 换 index 实测**全部仍 500**（70 行交叉表 + 5/5 定点）⇒ **不重试**；
            //    这一类失败的"降级"= 降级为**无图终态**（负缓存 + 可辨识标记），让 UI 立刻拿到终态而不是每屏重打。
            //    t306：上面那条只在**孤立** 500 时成立 —— 同 host 5xx 突发时 RecordFailure 会把它记成
            //    `transient`（30 s），故这里必须用**返回的快照**打日志，不能硬编 `kind=no-image`/600000。
            var failure = RecordFailure(key, 500);
            _log?.Invoke($"{LogPrefix} NO-IMAGE key={KeyTag(key)} status=500 kind={KindLabel(failure.Kind)} fetched=0"
                + $" negCache={NegativeCacheCount} cooldownMs={failure.CooldownMs}"
                + $" cap={Store.MaxEntries}/{Store.MaxBytes}");
            return null;
        }
        catch (ShellHttpException ex)
        {
            // 暂时性失败（超时 = 无状态码 / 4xx / 5xx 非 500）⇒ 放宽到原图档**降级重试一次**。
            return await DegradedFetchAsync(url, key, ex, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordFailure(key, (ex as ShellHttpException)?.StatusCode);
            LogFetchFailed(key, ex);
            return null;
        }
    }

    /// <summary>
    /// t212 降级臂：**只对暂时性失败**（超时 / 传输错 / 4xx / 5xx 非 500）放宽到原图档重试**一次**
    /// （去掉 <c>maxWidth</c>/<c>maxHeight</c>/<c>tag</c>、去掉路径末尾 <c>/index</c>）。
    /// 🔴 为什么孤立 500 不走这里：t211 已证 500 = 该 id 上没有这张图，换形态**全都仍 500**
    ///    ⇒ 对那一类再发一次请求只是把用户的等待拉长，价值为零。降级形态的计数由
    ///    <see cref="DegradeAttempts"/> / <see cref="DegradeSuccesses"/> 给出（0/0 也是读数）。
    /// t306：5xx **突发**（服务端整体在坏）同理不走这里 —— 换形态救不了"服务端坏了"，让调用方按 30 s 短窗重试。
    /// </summary>
    private async Task<byte[]> DegradedFetchAsync(string url, string key, ShellHttpException first, CancellationToken cancellationToken)
    {
        var degraded = DegradedUrl(url);
        if (string.IsNullOrEmpty(degraded) || string.Equals(degraded, url, StringComparison.Ordinal))
        {
            // 无可降级形态（例如本来就是原图档，或不是 http(s) 键）⇒ 记失败，不发第二次。
            RecordFailure(key, first.StatusCode);
            LogFetchFailed(key, first);
            return null;
        }

        var attempt = Interlocked.Increment(ref _degradeAttempts);
        _log?.Invoke($"{LogPrefix} DEGRADE key={KeyTag(key)} attempt={attempt} mode=drop-maxwh-tag"
            + $" before={(first.StatusCode.HasValue ? first.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no-status")}");
        try
        {
            var res = await Http.GetAsync(degraded, timeout: DefaultImageFetchTimeout, retries: 0, cancellationToken: cancellationToken).ConfigureAwait(false);
            var fresh = res?.Bytes;
            var ok = fresh != null && fresh.Length > 0;
            if (ok)
            {
                Interlocked.Increment(ref _degradeSuccesses);
                ClearFailure(key);
                Store.Write(key, fresh);
            }
            else
            {
                RecordFailure(key, res?.StatusCode);
            }
            var after = Store.Stats();
            _log?.Invoke($"{LogPrefix} hit=false miss=true degraded=true attempt={attempt} after={res?.StatusCode} fetched={fresh?.Length ?? 0} ok={ok}"
                + $" entries={after.Count} bytesTotal={after.Bytes} cap={Store.MaxEntries}/{Store.MaxBytes} key={KeyTag(key)}");
            return fresh;
        }
        catch (Exception ex)
        {
            RecordFailure(key, (ex as ShellHttpException)?.StatusCode);
            LogFetchFailed(key, ex);
            return null;
        }
    }

    private void LogFetchFailed(string key, Exception ex)
    {
        var after = Store.Stats();
        _log?.Invoke($"{LogPrefix} hit=false miss=true fetch-failed={ex.GetType().Name} fetched=0 entries={after.Count} bytesTotal={after.Bytes}"
            + $" cap={Store.MaxEntries}/{Store.MaxBytes} key={KeyTag(key)}");
    }

    public CacheStats Stats() => Store.Stats();

    /// <summary>按上限淘汰（LRU），并打一行汇总（与 <see cref="Clear"/> 同族的互校装置）。</summary>
    public int Prune()
    {
        var removed = Store.Prune();
        var s = Store.Stats();
        _log?.Invoke($"{LogPrefix} op=prune removed={removed} entries={s.Count} bytesTotal={s.Bytes} cap={Store.MaxEntries}/{Store.MaxBytes}");
        return removed;
    }

    /// <summary>清空本图片缓存，并打一行汇总（<c>IMG-CACHE op=clear removed=… entries=0 bytesTotal=0</c>）。
    /// t212：**一并清负缓存**（"清空本图片缓存"含失败态；否则用户点"清理缓存"后仍被旧的失败标记挡着）。</summary>
    public int Clear()
    {
        var removed = Store.Clear();
        var negativeRemoved = NegativeCacheCount;
        ClearNegativeCache();
        var s = Store.Stats();
        _log?.Invoke($"{LogPrefix} op=clear removed={removed} negCacheRemoved={negativeRemoved} entries={s.Count} bytesTotal={s.Bytes} cap={Store.MaxEntries}/{Store.MaxBytes}");
        return removed;
    }

    /// <summary>t269：键里是否带**版本参数** <c>tag=</c>（App 侧 `ImageUrlIfAvailable` 在有 tag 时会带上它；
    /// 只回布尔串，**不落 URL/值** —— 供 URL 版 per-caller 汇总行回答"这个 caller 的 URL 是否已带版本"）。</summary>
    private static string HasVersionTag(string normalizedKey)
        => normalizedKey != null && normalizedKey.IndexOf("tag=", StringComparison.OrdinalIgnoreCase) >= 0 ? "true" : "false";

    private static string Short(string key)
        => key == null ? "-" : (key.Length <= 48 ? key : key.Substring(0, 48) + "…");
}

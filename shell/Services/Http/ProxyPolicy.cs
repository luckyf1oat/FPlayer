// t61（ui2 请求，2026-09-12）：**进程级代理默认** —— 形态与纪律照抄 UserAgentPolicy。
//
// 🔴 为什么需要：`ShellHttpClient` 只在**显式传 `proxyUrl`** 时才走代理（`UseProxy=false` 是默认值），
// 而设置里的 `proxyEnabled/proxyUrl` 此前只被 `ServiceRegistry.ApplyProxyFromSettings()` 应用到**注册表自己那一个客户端**
// ⇒ 只有搜索/跨服同步/设置自检这些走注册表的路径生效；外壳自己的取数面（首页/媒体库/详情/收藏/聚合视界/人物/合集/服务器/日志）
// 全是 `new ShellHttpClient()`（不传 proxyUrl）⇒ **一律直连**。ui2 的哨兵取证（t61-proxy-effectiveness.txt）实测：
// 代理开时注册表路径 12+ 次 `积极拒绝`（真走代理），而 `HOME IMG-OK 4/8`、`SWR-LOAD … elapsed=1004ms`（直连成功）⇒ 只有一部分生效。
//
// 设计（与 UA 同一族：**一个事实一个构造点**）：
//   · `Current` = 显式 `Set(...)` 过 ⇒ 用它；否则**惰性**读 `SettingsService.Instance.Settings`
//     ⇒ 设置一改，**下一次请求即生效**（无需重启，与 `UserAgentPolicy.Current` 同款）；
//   · `ShellHttpClient(proxyUrl: null)` 回落 `Current`（显式传参仍优先 ⇒ 既有行为不变）；
//   · 显式 `ProxyUrl = null` = **显式直连**（不被回落覆盖）—— 保住 `ApplyProxyFromSettings` 的既有语义。
using System;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Settings;

namespace AIPlayer.Shell.Services.Http;

/// <summary>进程级代理策略：设置 → 所有默认构造的 <see cref="ShellHttpClient"/>。</summary>
public static class ProxyPolicy
{
    private static volatile string _override;
    private static volatile bool _hasOverride;
    private static volatile string _applied;
    private static volatile bool _hasApplied;

    /// <summary>
    /// 当前生效代理串（<c>null</c> = 直连）。
    /// 优先级：显式 <see cref="Set"/> 覆盖 → 由 <see cref="Apply"/>（SettingsService.Load/Save 推）→ 兜底读单例设置。
    /// </summary>
    public static string Current
    {
        get
        {
            if (_hasOverride) return Normalize(_override);
            if (_hasApplied) return Normalize(_applied);
            return FromSettings();
        }
    }

    /// <summary>是否为显式覆盖态（自检/取证用）。</summary>
    public static bool HasOverride => _hasOverride;

    /// <summary>
    /// 由设置喂入（<see cref="AIPlayer.Shell.Services.Settings.SettingsService"/> 在 Load/Save 时调用，与 UserAgentPolicy 同款）。
    /// 🔴 为什么要这条**推送**而不只靠惰性读单例：`SettingsService.At(path)`（测试/多实例/隔离根）永远不是单例，
    /// 只读单例会让"隔离设置里的代理"完全不生效（本卡实测反控 B/D 因此假失败）。
    /// </summary>
    public static void Apply(AppSettings settings)
    {
        _applied = Resolve(settings);
        _hasApplied = true;
        InvalidateHealth();   // t118：代理目标可能变了 ⇒ 旧结论作废（下一请求重探一次）
    }

    /// <summary>
    /// **唯一显式写点**（ui2 请求的形态）：传代理串 ⇒ 显式启用；传 <c>null</c>/空白 ⇒ **显式直连**。
    /// 需要"回到跟随设置"用 <see cref="FollowSettings"/>。
    /// </summary>
    public static void Set(string proxyUrl)
    {
        _override = proxyUrl;
        _hasOverride = true;
        InvalidateHealth();   // t118：目标变了 ⇒ 缓存作废
        DebugLog.Info($"PROXY-POLICY set={Describe(Current)}");
    }

    /// <summary>回到"跟随设置"（取消显式覆盖，仍用最近一次 <see cref="Apply"/> 推入的值）。</summary>
    public static void FollowSettings()
    {
        _hasOverride = false;
        _override = null;
        InvalidateHealth();   // t118：目标变了 ⇒ 缓存作废
        DebugLog.Info($"PROXY-POLICY follow-settings ⇒ {Describe(Current)}");
    }

    /// <summary>设置 → 代理串（<c>proxyEnabled &amp;&amp; 非空 proxyUrl</c> ⇒ 该串；否则直连）。</summary>
    public static string Resolve(AppSettings settings)
        => settings != null && settings.ProxyEnabled && !string.IsNullOrWhiteSpace(settings.ProxyUrl)
            ? settings.ProxyUrl.Trim()
            : null;

    /// <summary>
    /// 读设置里的代理。
    /// 🔴 t86（ui2 目录级复核 → 本卡修）：**设置没被加载过时，自己惰性 <see cref="SettingsService.Load"/> 一次**。
    /// 为什么：`FromSettings()` 原先只在 `IsLoaded` 为真时才读设置，而 App 的默认路径（`HomePage` 只**读属性**
    /// `SettingsService.Instance.Settings.LastServerId`）**从不调用 `Load()`** ⇒ `IsLoaded=false` ⇒ 恒回 <c>null</c>（直连）
    /// ⇒ **设置里配好的代理静默失效**（ui2 的 A/B 两轮不可区分、`refused=0`；t86 夹具复现：读之前 `IsLoaded=False`、
    /// `Current=null`、默认客户端被直连到 DNS 失败、假代理 0 请求）。
    /// 语义边界：只在**没有任何显式覆盖/推送**时才会走到这里（`Current` 的优先级不变），且 `Load()` 是幂等的
    /// ⇒ "设置一改下一次请求即生效"这条原设计不变；`Load()` 里会回调 <see cref="Apply"/>，那只是把同一份设置
    /// 推回来，不构成递归（`Apply` 不读设置）。
    /// </summary>
    private static string FromSettings()
    {
        try
        {
            var service = SettingsService.Instance;
            if (!service.IsLoaded)
            {
                // 线程安全：并发首访只加载一次（Load() 自身是幂等，但仍加锁避免并发重入读文件）
                lock (LazyLoadGate)
                {
                    if (!service.IsLoaded)
                    {
                        service.Load();
                        DebugLog.Info($"PROXY-POLICY lazy-load-settings file={service.Settings?.GetType().Name} loaded=true ⇒ {Describe(Resolve(service.Settings))}");
                    }
                }
            }
            return service.IsLoaded ? Resolve(service.Settings) : null;
        }
        catch (Exception ex)
        {
            // 取设置失败不得让网络面整体不可用：退回直连，但**留可见痕迹**（不静默）
            DebugLog.Warn($"PROXY-POLICY settings-read-failed {ex.GetType().Name} ⇒ 本次按直连处理");
            return null;
        }
    }

    /// <summary>惰性加载设置的并发门（t86）。</summary>
    private static readonly object LazyLoadGate = new object();

    // ── t118：代理健康探测 + 快速降级（带 TTL） ────────────────────────────────
    //
    // 为什么：用户报障现场是 `PROXY-MODE enabled=True url=http://127.0.0.1:10808` + 大量 `IMG-FETCH-FAIL`，
    // 而取数路径此前**只有"每个请求各自付满超时"**这一种终态 ⇒ 若代理已死，N 个请求 = N × 20s 的无表态。
    // 这里把"代理是否可达"变成一个**带 TTL 的判据**：不可达 ⇒ 请求**立即明确失败**（几百毫秒级），并留一行可见诊断。
    //
    // 处置选**明确失败**（而不是静默直连）：代理开着多半是为了绕行/变更出口，悄悄改走直连等于**改了用户的网络语义**
    // （可能把流量从代理隧道漏到本地出口）。失败是诚实的终态，UI 可以据此显示"代理不可达"并让用户处理。
    //
    // 纪律：探测结果**必须缓存**（TTL 内不重探）——否则"每请求各探一次"只是把 N 次超时换成 N 次探测。

    /// <summary>t118：健康结论的 TTL。TTL 内不重复探测。</summary>
    public static readonly TimeSpan HealthTtl = TimeSpan.FromSeconds(30);

    /// <summary>t118：探测自身的超时（TCP connect 与"等响应字节"各一段）。远小于通用取数 20s。</summary>
    public static readonly TimeSpan HealthProbeTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>t118：一次探测的结论（不可变）。</summary>
    public sealed class ProxyHealth
    {
        /// <summary>可达 ⇒ true。</summary>
        public bool Ok { get; init; }

        /// <summary>`ok` / `direct` / `connect-refused` / `connect-timeout` / `invalid-url` / 异常类型名。</summary>
        public string Reason { get; init; } = "ok";

        /// <summary>探测耗时（ms）。</summary>
        public long ElapsedMs { get; init; }

        /// <summary>被探测的 `host:port`（<c>Describe</c> 形态，已去掉 user:pass）。</summary>
        public string Proxy { get; init; } = "（直连）";

        /// <summary>结论产生时刻（TTL 的起点）。</summary>
        public DateTimeOffset At { get; init; }

        /// <summary>本次是否直接命中缓存（= 没有重探）。</summary>
        public bool FromCache { get; init; }

        /// <summary>失败细节（如 SocketErrorCode）。</summary>
        public string Detail { get; init; } = string.Empty;
    }

    private static readonly object HealthGate = new object();
    private static ProxyHealth _health;
    private static int _probeCount;

    /// <summary>累计**真实**探测次数（TTL 命中不计）——判定"缓存生效"的读数。</summary>
    public static int ProbeCount => Volatile.Read(ref _probeCount);

    /// <summary>最近一次结论（<c>null</c> = 本进程还没探测过）。</summary>
    public static ProxyHealth LastHealth
    {
        get { lock (HealthGate) return _health; }
    }

    /// <summary>丢掉缓存（设置变化、显式失效、测试清场时调用）。</summary>
    public static void InvalidateHealth()
    {
        lock (HealthGate) { _health = null; }
    }

    /// <summary>
    /// t118：**带 TTL 的健康探测**。直连（<see cref="Current"/> 为空）恒 <c>Ok</c>。
    /// 形态 = TCP connect 到代理 <c>host:port</c> + 写一段**故意非法的请求行**并要求"回任何字节"
    /// （只连不看会被"接受连接但不回字节"的黑洞伪装成健康）：
    /// 连接被拒 ⇒ <c>connect-refused</c>；连接超时 ⇒ <c>connect-timeout</c>；连上但一个字节都不回 ⇒ <c>probe-silent</c>；
    /// 回了任意内容（真代理对本机先回 400）或连上即关 ⇒ <c>ok</c>。
    /// TTL 内回缓存；TTL 外重探并落一行 <c>PROXY-HEALTH …</c>（探测有成本，必须可见）。
    /// </summary>
    public static async Task<ProxyHealth> CheckAsync(CancellationToken cancellationToken = default)
    {
        var proxy = Current;
        if (string.IsNullOrEmpty(proxy))
        {
            return new ProxyHealth { Ok = true, Reason = "direct", Proxy = "（直连）", At = DateTimeOffset.Now };
        }

        lock (HealthGate)
        {
            if (_health != null && _health.Proxy == HealthTarget(proxy) && DateTimeOffset.Now - _health.At < HealthTtl)
            {
                return new ProxyHealth
                {
                    Ok = _health.Ok,
                    Reason = _health.Reason,
                    ElapsedMs = _health.ElapsedMs,
                    Proxy = _health.Proxy,
                    At = _health.At,
                    Detail = _health.Detail,
                    FromCache = true,
                };
            }
        }

        var health = await ProbeAsync(proxy, cancellationToken).ConfigureAwait(false);
        lock (HealthGate) { _health = health; }
        DebugLog.Info($"PROXY-HEALTH ok={health.Ok} reason={health.Reason} proxy={health.Proxy} elapsed={health.ElapsedMs}ms ttl={(long)HealthTtl.TotalSeconds}s probes={ProbeCount} action={(health.Ok ? "proceed" : "refuse")}");
        return health;
    }

    private static async Task<ProxyHealth> ProbeAsync(string proxy, CancellationToken cancellationToken)
    {
        var at = DateTimeOffset.Now;
        var shown = HealthTarget(proxy);
        var normalized = proxy.Contains("://", StringComparison.Ordinal) ? proxy : "http://" + proxy;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Port <= 0)
        {
            return new ProxyHealth { Ok = false, Reason = "invalid-url", Proxy = shown, At = at };
        }

        var target = $"{uri.Host}:{uri.Port}";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Interlocked.Increment(ref _probeCount);
        var phase = "connect";
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectCts.CancelAfter(HealthProbeTimeout);
                await client.ConnectAsync(uri.Host, uri.Port, connectCts.Token).ConfigureAwait(false);
            }

            // 只连上还不够：**接受连接但不回字节的黑洞**在"纯 connect 探针"下会冒充健康。
            // 因此再写一段**故意非法的请求行**（`XYZ`）并只要求"回任何字节"：
            //   · 真代理   ⇒ 本地立刻回 `400 Bad Request`（**不需要上游** ⇒ 不会因上游被丢包而把活代理误判成死）；
            //   · 黑洞     ⇒ 一个字节都不回 ⇒ probe-silent ⇒ 不可达；
            //   · 连上即关 ⇒ EOF ⇒ **视为活着**（这类对端会让请求立即失败，不会造成 N×20s 的等待）。
            // 先前的写法（请求 `http://127.0.0.1:9/`）会让代理去连上游，本机对已关闭 loopback 端口是**静默丢包**
            // ⇒ 活代理也要等满探测期 ⇒ **假阴性**（实测：真代理被误判 connect-timeout）。
            phase = "read";
            var probeBytes = System.Text.Encoding.ASCII.GetBytes("XYZ\r\n\r\n");
            using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                readCts.CancelAfter(HealthProbeTimeout);
                var stream = client.GetStream();
                await stream.WriteAsync(probeBytes, 0, probeBytes.Length, readCts.Token).ConfigureAwait(false);
                await stream.FlushAsync(readCts.Token).ConfigureAwait(false);
                var buffer = new byte[64];
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token).ConfigureAwait(false);
                sw.Stop();
                if (read <= 0)
                {
                    return new ProxyHealth { Ok = true, Reason = "ok", Proxy = target, ElapsedMs = sw.ElapsedMilliseconds, At = at, Detail = "closed-without-response（连上即关 ⇒ 视为活着）" };
                }

                var firstLine = System.Text.Encoding.ASCII.GetString(buffer, 0, read).Split('\n')[0].Trim();
                return new ProxyHealth { Ok = true, Reason = "ok", Proxy = target, ElapsedMs = sw.ElapsedMilliseconds, At = at, Detail = firstLine };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            var reason = phase == "connect" ? "connect-timeout" : "probe-silent";
            return new ProxyHealth
            {
                Ok = false,
                Reason = reason,
                Proxy = target,
                ElapsedMs = sw.ElapsedMilliseconds,
                At = at,
                Detail = reason == "probe-silent"
                    ? "代理接受连接但不回任何字节（黑洞形态）"
                    : $"probe-timeout={(long)HealthProbeTimeout.TotalMilliseconds}ms",
            };
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            sw.Stop();
            return new ProxyHealth { Ok = false, Reason = "connect-refused", Proxy = target, ElapsedMs = sw.ElapsedMilliseconds, At = at, Detail = ex.SocketErrorCode.ToString() };
        }
        catch (Exception ex)
        {
            // 有意不吞：任何异常类型名都作为 reason 落行（不静默），随后由调用方决定"失败"还是直连
            sw.Stop();
            return new ProxyHealth { Ok = false, Reason = ex.GetType().Name, Proxy = target, ElapsedMs = sw.ElapsedMilliseconds, At = at, Detail = ex.Message };
        }
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// t118：健康探测的**目标标识**（也是缓存键）= <c>host:port</c>。
    /// 与 <see cref="Describe"/> 分开：日志里 `PROXY-POLICY …` 仍打原串（保住既有引用的可复算性），
    /// 而健康结论按 `host:port` 归并 —— 顺带天然去掉了 `user:pass@`（不打印凭据）。
    /// </summary>
    private static string HealthTarget(string proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy)) return "（直连）";
        var normalized = proxy.Contains("://", StringComparison.Ordinal) ? proxy.Trim() : "http://" + proxy.Trim();
        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0
            ? $"{uri.Host}:{uri.Port}"
            : "(invalid)";
    }

    private static string Describe(string url) => string.IsNullOrEmpty(url) ? "direct" : url;
}

// t31 / SEAM③：**收藏 + 聚合视界的「缓存优先 + 并发刷新」取数面**（`Services/AGGREGATION_INCREMENTAL.md`，
// 规格依据 `UI_SPEC_SHELL.md`）。
//
// 一条纪律：本文件是**唯一**读/写这两屏快照的地方；页面不得自己 new 一个 `SwrSnapshotCache`（否则键/作用域各自漂移）。
//
// 语义（ 硬要求，逐条落到代码）：
//   · **T0 同步读缓存 ⇒ 命中就立即回调渲染（零网络），同一时刻并发发起刷新** —— 由 `SwrSnapshotCache.LoadAsync`
//     保证顺序（它先起请求再回调，见 `SwrSnapshotCache.cs:426-452`）；
//   · **T1 成功 ⇒ 就地替换**（不闪屏/不重置滚动/不丢选中 —— 由页面的"先算差异、变了才重绑"实现）；
//   · **失败 ⇒ 保留缓存 + 可见失败提示**（🔴 禁止清空界面或删缓存）—— `SwrLoadResult.CachePreserved` 直接可读；
//   · 🔴 **`Miss` 与"缓存就是空"必须分开**：`HasCache=false` ⇒ 无快照（走骨架屏）；`HasCache=true` 且 `Groups` 为空
//     ⇒ **缓存记录的就是"没有内容"**（应显示空态，而不是骨架屏）。
//
// ⚠️ 缓存里的是 `SnapshotItem` 而不是整条目（白名单，见 `SnapshotItem.cs` 文件头）；**从缓存渲染的卡片不能直接起播**，
//    起播路径必须先拿到刷新后的真条目 —— 页面用 `LiveItemFor(serverId, itemId)` 做这件事。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>快照里的一组（= 一台服务器的一组条目）。**分组必须一起落盘**，否则缓存态还原不出"按服务器分组"。</summary>
public sealed class SnapshotGroup
{
    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public List<SnapshotItem> Items { get; set; } = new List<SnapshotItem>();

    /// <summary>服务端声明的总数（0 = 没给）。t87：收藏族要靠它给出"还有更多"的可见提示。</summary>
    public int TotalCount { get; set; }

    /// <summary>true ⇒ 取数时撞上每源安全硬上限（屏上不是全部）⇒ 必须可见。</summary>
    public bool Truncated { get; set; }
}

/// <summary>一屏的快照载荷（分组 + 失败源 + 取数时刻，全部随信封落盘）。</summary>
public sealed class MediaSnapshot
{
    public List<SnapshotGroup> Groups { get; set; } = new List<SnapshotGroup>();

    public List<string> FailedServers { get; set; } = new List<string>();

    /// <summary>撞上每源安全硬上限的源（非空 ⇒ 屏上不是全部；界面必须可见地说明）。</summary>
    public List<string> TruncatedServers { get; set; } = new List<string>();
}

/// <summary>缓存态的一次取数读数（给页面 + 自检共用）。</summary>
public sealed class SwrOutcome
{
    /// <summary>本次用的**作用域化**缓存键（t93-A：取证"读的是哪个作用域的键"）。</summary>
    public string Key { get; set; } = string.Empty;

    public bool HasCache { get; set; }

    public SwrFreshness Freshness { get; set; }

    public double? AgeSeconds { get; set; }

    /// <summary>T0 缓存内容（`HasCache=false` 时为 null）。</summary>
    public MediaSnapshot Cached { get; set; }

    /// <summary>T1 刷新后的内容（失败 ⇒ null）。</summary>
    public MediaSnapshot Value { get; set; }

    public bool RefreshStarted { get; set; }

    public bool RefreshSucceeded { get; set; }

    public string Error { get; set; } = string.Empty;

    /// <summary>：失败但缓存仍在。</summary>
    public bool CachePreserved { get; set; }

    public double ElapsedMs { get; set; }

    /// <summary>④：两组 id 分别保留（自检要分别打印）。</summary>
    public List<string> CachedIds { get; } = new List<string>();

    public List<string> RefreshedIds { get; } = new List<string>();
}

public static class MediaSnapshotSource
{
    /// <summary>快照 TTL。取 10 分钟：够"重进页面立即有内容"，又不至于长期陈旧。</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 落盘目录 = **数据根下的 `swr`**（默认 `%LOCALAPPDATA%\AIPlayer\swr`）。
    ///
    /// [!] 为什么必须走 <see cref="AIPlayer.Shell.Services.Infra.AppDataDir.Instance"/> 而不能直取
    /// `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`（t232）：后者走 SHGetKnownFolderPath，
    /// **不看 `LOCALAPPDATA` 环境变量、更不看 `AIPLAYER_APPDATA_ROOT`** ⇒ 沙箱化的取证跑（设了覆盖根）仍会把
    /// SWR 快照写进**真实用户根**（实测：真根 `swr\` 的 mtime 在"沙箱"跑里照变）。而 `AppDataDir` 的根推导
    /// 是"环境变量优先"（`AppDataDir.cs:133`）⇒ 覆盖根对 SWR 才真正生效 ⇒ 日志/缓存/SWR **同根 = 单一覆盖点**。
    ///
    /// 取值形状与 `AppDataDir` 里那些 `*Dir` 属性同构（`Root` + 段名 + 按需建目录、建目录失败**有意吞掉**
    /// —— 由调用方随后的写入暴露错误）；本属性是**唯一**给 `SwrSnapshotCache` 的落点（见 `:218` 的构造点）。
    /// </summary>
    public static string Directory
    {
        get
        {
            var dir = System.IO.Path.Combine(AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root, "swr");
            if (!System.IO.Directory.Exists(dir))
            {
                try { System.IO.Directory.CreateDirectory(dir); }
                catch (Exception) { }   // 有意忽略：按需建目录失败 => 由调用方随后的写入暴露错误（与 AppDataDir 同口径）
            }
            return dir;
        }
    }

    /// <summary>每屏一个**基键**（不含作用域）；落盘键一律用 <see cref="KeyFor(AggregateKind, IEnumerable{ServerConfig})"/>。</summary>
    private static string BaseKeyFor(AggregateKind kind) => kind switch
    {
        AggregateKind.ContinueWatching => "aggregate:continue-watching",
        AggregateKind.Favorites => "aggregate:favorites",
        _ => "aggregate:library",
    };

    /// <summary>落盘键 = **基键 + 作用域身份**（t93-A）。作用域不同 ⇒ 键不同 ⇒ 两条快照不可能互相污染。</summary>
    public static string KeyFor(AggregateKind kind, IEnumerable<ServerConfig> servers)
        => BaseKeyFor(kind) + "@" + ScopeKeyFor(servers);

    /// <summary>作用域身份 = 参与取数的服务器 id 集合（**排序去重**后拼接；空集合 = `&lt;none&gt;`）。
    /// 用"集合本身"而不是"台数"：换一台服务器但台数相同（主源切换）也必须换键。</summary>
    public static string ScopeKeyFor(IEnumerable<ServerConfig> servers)
    {
        var ids = (servers ?? Enumerable.Empty<ServerConfig>())
            .Select(s => s?.Id ?? string.Empty)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return ids.Count == 0 ? "<none>" : string.Join("+", ids);
    }

    /// <summary>
    /// t93：**旧的无作用域键**（毒缓存来源）。启动时删一次并留日志行 ——
    /// 新键不会再读它们，但它们会占 LRU 配额、也让取证混淆。
    /// </summary>
    public static readonly string[] LegacyUnscopedKeys =
    {
        "aggregate:continue-watching",
        "aggregate:favorites",
        "aggregate:library",
    };

    private static bool _legacyPurged;

    /// <summary>清掉旧的无作用域键（**一次/进程**）；返回删除条数。可见：日志行 `SWR-LEGACY-PURGE`。</summary>
    public static int PurgeLegacyUnscopedKeys()
    {
        if (_legacyPurged)
        {
            return 0;
        }

        _legacyPurged = true;
        var removed = 0;
        foreach (var legacy in LegacyUnscopedKeys)
        {
            if (CacheInstance.InvalidateKey(legacy))
            {
                removed++;
            }
        }

        Program.Log("SWR-LEGACY-PURGE removed=" + removed + " keys=" + string.Join(",", LegacyUnscopedKeys)
            + "（t93-A：无作用域键不再被任何屏使用）");
        return removed;
    }

    /// <summary>
    /// 自检专用：把"已清过旧键"的标记复位，好让下一次载入**再清一次** ——
    /// t93-A 的反例要在窗口里**真注入一份旧键毒快照**，再证明它被清掉（否则只能靠推理）。
    /// 生产路径永远不调它。
    /// </summary>
    public static void ResetLegacyPurgeForSelfTest()
    {
        _legacyPurged = false;
        Program.Log("SWR-LEGACY-PURGE reset（自检专用：允许再次清理旧键以取证）");
    }

    /// <summary>快照里的条目总数（跨组求和）。</summary>
    public static int CountItems(MediaSnapshot snapshot)
        => snapshot?.Groups?.Sum(g => g.Items?.Count ?? 0) ?? 0;

    /// <summary>
    /// t93-B：**毒结果不得覆盖好缓存**。判据 = 上一份缓存**有内容**、本次刷新**0 条**，
    /// 且（源集合比上次小 或 本次所有源都失败）⇒ 判为退化结果（reject），不写盘。
    /// <para>为什么"源集合缩小"也算退化：实测那次是**收藏屏单服作用域**的结果覆盖了 16 台那份；
    /// 台数缩小意味着"这次根本没问过上一份问过的那些源"，其"0 条"不构成"世界上没有内容"的证据。</para>
    /// </summary>
    public static bool IsDegradedWrite(MediaSnapshot previous, MediaSnapshot fresh)
    {
        var previousItems = CountItems(previous);
        if (previousItems == 0)
        {
            return false;       // 没有"好缓存"需要保护
        }

        if (CountItems(fresh) > 0)
        {
            return false;       // 新结果有内容 ⇒ 正常替换
        }

        var previousServers = previous?.Groups?.Count ?? 0;
        var freshServers = fresh?.Groups?.Count ?? 0;
        var freshAllFailed = freshServers == 0 || (fresh?.FailedServers?.Count ?? 0) >= freshServers;
        return freshServers < previousServers || freshAllFailed;
    }

    /// <summary>退化结果**拒绝写盘**：抛出可读原因 ⇒ SWR 层走"失败保留缓存"（缓存不清、界面显示可见失败）。</summary>
    private static void RejectDegradedWrite(string key, MediaSnapshot previous, MediaSnapshot fresh)
    {
        if (!IsDegradedWrite(previous, fresh))
        {
            return;
        }

        var reason = "degraded-refresh key=" + key
            + " prevItems=" + CountItems(previous) + " freshItems=" + CountItems(fresh)
            + " prevServers=" + (previous?.Groups?.Count ?? 0) + " freshServers=" + (fresh?.Groups?.Count ?? 0)
            + " freshFailed=" + (fresh?.FailedServers?.Count ?? 0);
        Program.Log("SWR-DEGRADED-REJECT " + reason + "（保留上一份缓存，不写空）");
        throw new InvalidOperationException(reason);
    }

    /// <summary>进程内单例（键相同即同一份缓存；避免每屏各建一个实例）。</summary>
    private static readonly Lazy<SwrSnapshotCache<MediaSnapshot>> Cache =
        new Lazy<SwrSnapshotCache<MediaSnapshot>>(() =>
            new SwrSnapshotCache<MediaSnapshot>(Directory, Ttl, log: m => Program.Log("SWR " + m)));

    public static SwrSnapshotCache<MediaSnapshot> CacheInstance => Cache.Value;

    /// <summary>把服务层聚合结果转成快照载荷（**只保留渲染面**）。</summary>
    public static MediaSnapshot ToSnapshot(MediaAggregate aggregate)
    {
        var snapshot = new MediaSnapshot();
        if (aggregate == null)
        {
            return snapshot;
        }

        foreach (var group in aggregate.Groups)
        {
            if (group.Failed)
            {
                snapshot.FailedServers.Add(group.DisplayName);
                continue;
            }

            snapshot.Groups.Add(new SnapshotGroup
            {
                ServerId = group.ServerId ?? string.Empty,
                ServerName = group.DisplayName,
                Items = SnapshotItem.FromEmbyList(group.Items),
                TotalCount = group.TotalCount,
                Truncated = group.Truncated,
            });

            if (group.Truncated)
            {
                // 撞上硬上限的源也要落盘：缓存态渲染时"还有更多"提示必须照样出现（否则 T0 与 T1 观感不一致）
                snapshot.TruncatedServers.Add(group.DisplayName);
            }
        }

        return snapshot;
    }

    /// <summary> 唯一取数入口：T0 缓存（同步回调）→ 并发刷新（T1 替换/失败保留）。</summary>
    public static async Task<SwrOutcome> LoadAsync(
        AggregateKind kind,
        List<ServerConfig> servers,
        Action<SwrLookup<MediaSnapshot>> onCacheValue,
        CancellationToken cancellationToken = default,
        Action<MediaAggregate> onLiveAggregate = null)
    {
        var key = KeyFor(kind, servers);
        PurgeLegacyUnscopedKeys();      // t93-A：旧的无作用域键（毒缓存来源）删一次，留日志行
        Program.Log("SWR-SCOPE key=" + key + " servers=" + (servers?.Count ?? 0) + " scope=" + ScopeKeyFor(servers));

        var fetch = new Func<CancellationToken, Task<MediaSnapshot>>(async ct =>
        {
            if (FailFetchForSelfTest)
            {
                // 反控②：模拟"网络不可用"—— 该屏必须退化为**只用缓存内容**
                throw new InvalidOperationException("SELFTEST-FETCH-DISABLED（反控②：证明内容真来自缓存）");
            }

            if (DegradedFetchForSelfTest)
            {
                // t93-B 反控：**不取数**，直接给出"单服 0 条"的退化结果 —— 本反控只关心"它会不会被写进缓存"
                var degraded = DegradedSnapshotForSelfTest();
                RejectDegradedWrite(key, CacheInstance.TryRead(key).Value, degraded);
                return degraded;
            }

            var aggregate = await MediaAggregator.LoadAsync(kind, servers, ct).ConfigureAwait(false);
            var snapshot = ToSnapshot(aggregate);

            // t93-B：**退化结果不得覆盖好缓存**（上一份有内容 + 本次 0 条 + 源集合缩小/全失败 ⇒ 拒绝写盘）。
            // 拒绝后 SWR 层走"失败保留缓存"分支：缓存内容照旧、界面显示可见失败（不是静默空屏）。
            RejectDegradedWrite(key, CacheInstance.TryRead(key).Value, snapshot);

            // 把**真条目**结果交回调用方（起播要用真条目；快照只是渲染面白名单）——同步回调，避免竞态。
            onLiveAggregate?.Invoke(aggregate);
            return snapshot;
        });

        var load = await CacheInstance.LoadAsync(
            key,
            fetch,
            onCacheValue,
            idsOf: snapshot => SnapshotIds(snapshot),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToOutcome(load);
    }

    /// <summary>
    /// **按自定义键**走**同一个** SWR 快照缓存（人物 / 合集等"单服务器一个列表"的屏，t58）。
    /// 🔴 不新建第二个 `SwrSnapshotCache` 实例：`CacheInstance` 是全工程唯一入口，键不同即不同条目
    /// （键结构仍是 `{scopeServerId}::{key}`）—— 这是 `t54` 定的口径，新屏复用而不是另起一套。
    /// </summary>
    public static async Task<SwrOutcome> LoadListAsync(
        string key,
        ServerConfig server,
        Func<CancellationToken, Task<List<EmbyItem>>> fetchItems,
        Action<SwrLookup<MediaSnapshot>> onCacheValue,
        CancellationToken cancellationToken = default,
        Action<List<EmbyItem>> onLiveItems = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            // 空键会把两屏的快照混成一条（并且 `{scope}::{key}` 结构会被破坏）⇒ 明确拒绝，不静默兜底。
            throw new ArgumentException("快照键不能为空", nameof(key));
        }

        var fetch = new Func<CancellationToken, Task<MediaSnapshot>>(async ct =>
        {
            if (FailFetchForSelfTest)
            {
                throw new InvalidOperationException("SELFTEST-FETCH-DISABLED（反控：证明内容真来自缓存）");
            }

            var items = await fetchItems(ct).ConfigureAwait(false) ?? new List<EmbyItem>();
            onLiveItems?.Invoke(items);      // 同步回调：真条目给起播用（快照只有渲染面字段）
            return ToListSnapshot(server, items);
        });

        var load = await CacheInstance.LoadAsync(
            key,
            fetch,
            onCacheValue,
            idsOf: snapshot => SnapshotIds(snapshot),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToOutcome(load);
    }

    /// <summary>人物屏的快照键（服务器 + 人物 id ⇒ 同一人物在不同服务器上是两条）。</summary>
    public static string KeyForPerson(string serverId, string personId)
        => "person:" + (serverId ?? string.Empty) + ":" + (personId ?? string.Empty);

    /// <summary>合集屏的快照键。</summary>
    public static string KeyForCollection(string serverId, string boxSetId)
        => "collection:" + (serverId ?? string.Empty) + ":" + (boxSetId ?? string.Empty);

    /// <summary>单服务器列表 → 快照（恰好一组；空列表也落盘 ⇒ 缓存能记录"就是没有内容"）。</summary>
    public static MediaSnapshot ToListSnapshot(ServerConfig server, List<EmbyItem> items)
    {
        var snapshot = new MediaSnapshot();
        snapshot.Groups.Add(new SnapshotGroup
        {
            ServerId = server?.Id ?? string.Empty,
            ServerName = server?.Name ?? string.Empty,
            Items = SnapshotItem.FromEmbyList(items ?? new List<EmbyItem>()),
        });
        return snapshot;
    }

    private static SwrOutcome ToOutcome(SwrLoadResult<MediaSnapshot> load)
    {
        var outcome = new SwrOutcome
        {
            Key = load.Key,
            HasCache = load.CacheHit,
            Freshness = load.CacheFreshness,
            AgeSeconds = load.CacheAgeSeconds,
            Cached = load.Cached,
            Value = load.Value,
            RefreshStarted = load.RefreshStarted,
            RefreshSucceeded = load.RefreshSucceeded,
            Error = load.Error,
            CachePreserved = load.CachePreserved,
            ElapsedMs = load.ElapsedMs,
        };
        outcome.CachedIds.AddRange(load.CachedIds);
        outcome.RefreshedIds.AddRange(load.RefreshedIds);
        return outcome;
    }

    /// <summary>④：快照 → id 集合（按组顺序铺平）。</summary>
    public static IEnumerable<string> SnapshotIds(MediaSnapshot snapshot)
        => (snapshot?.Groups ?? new List<SnapshotGroup>())
            .SelectMany(g => g.Items ?? new List<SnapshotItem>())
            .Select(i => i?.Id ?? "<null>");

    /// <summary>
    /// 「还有更多」可见文案（t87）。返回 <c>null</c> = **屏上就是全部**（正常态，不显示任何提示）。
    /// 🔴 只要"屏上条数 &lt; 服务端总数"或"撞上每源硬上限"，就**必须**给出文案 ——
    /// 用户可见的静默截断是本卡要消灭的缺陷（t72 实测：ServerA 34 条只显示 30 条）。
    /// </summary>
    public static string TruncationNotice(MediaSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }

        var shown = snapshot.Groups?.Sum(g => g.Items?.Count ?? 0) ?? 0;
        var total = snapshot.Groups?.Sum(g => Math.Max(g.TotalCount, g.Items?.Count ?? 0)) ?? 0;
        var capped = snapshot.TruncatedServers != null && snapshot.TruncatedServers.Count > 0;

        if (!capped && total <= shown)
        {
            return null;
        }

        var effectiveTotal = Math.Max(total, shown);
        return "还有更多：已显示 " + shown + " / 共 " + effectiveTotal + " 条"
            + (capped
                ? "（已达每台 " + MediaAggregator.FavoritesMaxPerServer + " 条安全上限）"
                : "（重进本页可继续加载）");
    }

    /// <summary>清空本屏全部快照（反控①/②用：清掉后热路径必须退化为冷启动）。</summary>
    public static int ClearAll() => CacheInstance.Clear();

    /// <summary>
    /// 只在**服务层失败**时才真正取数（反控②用）：热路径下让 `fetch` 直接抛 ⇒
    /// 该屏**只能**显示缓存内容。用它证明"内容真来自缓存"，而不是恰好网络也很快。
    /// </summary>
    public static bool FailFetchForSelfTest { get; set; }

    /// <summary>
    /// t93-B 反控：让取数结果**退化成"单服 0 条"**（就是那次毒缓存的形态）。
    /// 用它证明"退化结果不会被写进缓存"（否则只能靠推理，不是读数）。生产路径恒为 false。
    /// </summary>
    public static bool DegradedFetchForSelfTest { get; set; }

    /// <summary>退化结果的合成形态：1 台、0 条、无失败源。</summary>
    private static MediaSnapshot DegradedSnapshotForSelfTest()
    {
        var snapshot = new MediaSnapshot();
        snapshot.Groups.Add(new SnapshotGroup
        {
            ServerId = "t93-degraded",
            ServerName = "（反控）退化单服",
        });
        return snapshot;
    }

    /// <summary>自检观察：当前落盘快照条数。</summary>
    public static int CachedEntryCount => CacheInstance.Count;

    /// <summary>证据一行（给日志/自检）。</summary>
    public static string Describe(SwrOutcome outcome)
        => outcome == null
            ? "<null>"
            : $"key={outcome.Key} cache={(outcome.HasCache ? "hit" : "miss")} freshness={outcome.Freshness}"
              + (outcome.AgeSeconds.HasValue ? $" age={outcome.AgeSeconds.Value:0.0}s" : string.Empty)
              + $" refresh={(outcome.RefreshSucceeded ? "done" : (outcome.RefreshStarted ? "started/failed" : "not-started"))}"
              + $" cachedIds=[{string.Join(",", outcome.CachedIds)}] refreshedIds=[{string.Join(",", outcome.RefreshedIds)}]"
              + (outcome.RefreshSucceeded ? string.Empty : $" error={outcome.Error} cache-preserved={outcome.CachePreserved}");
}

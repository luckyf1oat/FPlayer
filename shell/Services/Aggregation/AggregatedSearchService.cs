// 原版文件，按 SERVICE_API / DESIGN 新写（rebuild/ai_player 树中**不存在** aggregated_search_service.dart）。
// 映射依据：shell/docs/DESIGN.md §4.1 #1 / §5 #30
//   `src/core/services/aggregated_search_service.dart` → `Services/Aggregation/AggregatedSearchService.cs`（新写）；
//   同表关键协议要点：**去重语义对齐 `emby_item_list_merge`（GAP_AUDIT §7-5）**。
// 实证依据（reversed/FlutterApp 字符串 `[S]`）：
//   - 类名 `AggregatedSearchService` / `AggregatedSearchResult`、构造 `AggregatedSearchResult.fromItems`
//     （symbols_classes.txt:221-222、symbols_class_members.txt:15、strings_all.txt:4898-4899）；
//   - provider `aggregatedSearchServiceProvider`（strings_all.txt:23425、29151）。
// 去重键（与 rebuild 侧 `server_merge_service.dart` 的 `keyOf`（原版 emby_item_list_merge 的等价实现）逐条对齐，
//   并已与并行任务产出的 `shell/Services/Emby/EmbyItemListMerge.cs` 的 `KeyOf`/`ProviderId` 交叉核对）：
//   ① `Type + ProviderIds`（Imdb → Tmdb → Tvdb，命中即用，值 trim + 小写；键大小写漂移时忽略大小写回落）；
//   ② 退化：`Type + 名称(trim+小写) + 年份`。
// 排序（**未实证，近似实现**）：相关度（全等 → 前缀 → 包含 → 其他）→ 名称 → 来源服务器名；同分同名的保持
//   「来源顺序」（OrderBy 为稳定排序 = 服务器配置顺序），与 merge 的首次出现顺序语义一致。
// 容错：单源抛异常只记日志并跳过（整体结果照常返回），失败源名进 `FailedServers`。
// 解耦：取数面是 `IAggregatedSearchSource` 委托接口 ⇒ 本文件不引用 NavidromeService（由并行任务编写）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Aggregation;

/// <summary>
/// 跨服聚合搜索的取数面：每台服务器（Emby/Jellyfin/Navidrome…）各实现一个，由外壳按启用服务器注册。
/// </summary>
public interface IAggregatedSearchSource
{
    string ServerId { get; }

    string ServerName { get; }

    /// <summary>在**单台**服务器上搜索；失败请抛出（服务层会捕获、记日志并跳过该源）。</summary>
    Task<IReadOnlyList<AggregatedSearchHit>> SearchAsync(string term, int limit, CancellationToken cancellationToken);
}

/// <summary>聚合搜索的单条命中（一台服务器上的一条媒体）。</summary>
public sealed class AggregatedSearchHit
{
    /// <summary>来源服务器（多源去重后仍保留，UI 可标注「N 个源」并切换播放源）。</summary>
    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Emby 侧条目类型：<c>Movie</c>/<c>Series</c>/<c>Episode</c>/<c>Audio</c>/<c>MusicAlbum</c>…</summary>
    public string Type { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public int? ProductionYear { get; set; }

    public string PremiereDate { get; set; }

    public string SeriesId { get; set; } = string.Empty;

    public string SeriesName { get; set; } = string.Empty;

    public string SeasonId { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;

    public int? IndexNumber { get; set; }

    public int? ParentIndexNumber { get; set; }

    public string AlbumId { get; set; } = string.Empty;

    public string AlbumName { get; set; } = string.Empty;

    /// <summary>音乐路径下的专辑艺术家（Emby 的 <c>AlbumArtist</c>）。</summary>
    public string AlbumArtist { get; set; } = string.Empty;

    public List<string> Artists { get; set; } = new List<string>();

    /// <summary>外部 id（<c>Imdb</c>/<c>Tmdb</c>/<c>Tvdb</c> …）——去重键的首选来源。</summary>
    public Dictionary<string, string> ProviderIds { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public long RunTimeTicks { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>原始 JSON（供未知字段透传；未实证字段的容错出口）。</summary>
    public JsonObject Raw { get; set; } = new JsonObject();

    public double DurationSeconds => TextUtils.TicksToSeconds(RunTimeTicks);

    /// <summary>去重键优先级的候选键（与 Emby ProviderIds 的实际键名一致，大小写敏感）。</summary>
    private static readonly string[] ProviderIdKeys = { "Imdb", "Tmdb", "Tvdb" };

    /// <summary>
    /// 去重键（**与 `shell/Services/Emby/EmbyItemListMerge.cs` 的 <c>KeyOf</c> 逐条对齐**，已按同一实现的
    /// ProviderIdPriority = Imdb → Tmdb → Tvdb、键大小写漂移回落、值 trim + 小写、退化段 `类型|名称|年份` 交叉核对）：
    /// 外部 id 优先，退化到 类型 + 名称 + 年份。
    /// </summary>
    public static string KeyOf(AggregatedSearchHit hit)
    {
        if (hit == null) return string.Empty;

        if (hit.ProviderIds != null)
        {
            foreach (var key in ProviderIdKeys)
            {
                var value = ProviderId(hit.ProviderIds, key);
                if (value.Length > 0)
                {
                    return $"{hit.Type}|{key}:{value.ToLowerInvariant()}";
                }
            }
        }

        var year = hit.ProductionYear.HasValue
            ? hit.ProductionYear.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        return $"{hit.Type}|{(hit.Name ?? string.Empty).Trim().ToLowerInvariant()}|{year}";
    }

    /// <summary>
    /// 外部 id 查找（与 EmbyItemListMerge 的 <c>ProviderId</c> 一致）：先精确键，未命中再忽略大小写扫一遍
    /// （不同服务端的 ProviderIds 键大小写会漂移），值一律 trim。
    /// </summary>
    private static string ProviderId(Dictionary<string, string> ids, string key)
    {
        if (ids == null || ids.Count == 0) return string.Empty;
        if (ids.TryGetValue(key, out var value) && value != null) return value.Trim();

        foreach (var kv in ids)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
            {
                return kv.Value.Trim();
            }
        }
        return string.Empty;
    }

    /// <summary>Emby 条目 → 聚合命中（幂等纯映射；<paramref name="imageUrl"/> 由调用方按图片 tag 决定是否给）。</summary>
    public static AggregatedSearchHit FromEmbyItem(EmbyItem item, string serverId, string serverName, string imageUrl = null)
    {
        if (item == null) return null;
        return new AggregatedSearchHit
        {
            ServerId = serverId ?? string.Empty,
            ServerName = serverName ?? string.Empty,
            Id = item.Id,
            Name = item.Name,
            Type = item.Type,
            Overview = item.Overview,
            ProductionYear = item.ProductionYear,
            PremiereDate = item.PremiereDate,
            SeriesId = item.SeriesId,
            SeriesName = item.SeriesName,
            SeasonId = item.SeasonId,
            SeasonName = item.SeasonName,
            IndexNumber = item.IndexNumber,
            ParentIndexNumber = item.ParentIndexNumber,
            AlbumId = item.AlbumId,
            // EmbyItem 无 AlbumName 字段（ItemFields 未含 Album）⇒ 从未知字段透传里兜底取（未实证，容错解析）。
            AlbumName = JsonRead.Str(JsonRead.From(item.Raw), "Album"),
            AlbumArtist = item.AlbumArtist,
            Artists = item.Artists == null ? new List<string>() : new List<string>(item.Artists),
            ProviderIds = item.ProviderIds == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(item.ProviderIds, StringComparer.Ordinal),
            RunTimeTicks = item.RunTimeTicks,
            ImageUrl = imageUrl ?? string.Empty,
            Raw = item.Raw == null ? new JsonObject() : (JsonObject)item.Raw.DeepClone(),
        };
    }
}

/// <summary>聚合条目的一条来源（服务器 + 该服务器上的条目 id）——对应 Dart/rebuild 侧 <c>MergedSource</c> 的角色。</summary>
public sealed class AggregatedSourceRef
{
    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;
}

/// <summary>
/// 去重后的聚合条目：<see cref="Primary"/> 为默认展示/播放的来源，<see cref="Hits"/> 保留全部来源
/// （「换源续播」用），与 <c>server_merge_service.dart</c> 的 <c>MergedItem</c> 同角色。
/// </summary>
public sealed class AggregatedSearchEntry
{
    public AggregatedSearchEntry(IEnumerable<AggregatedSearchHit> hits)
    {
        Hits = hits == null ? new List<AggregatedSearchHit>() : hits.Where(h => h != null).ToList();
        Primary = Hits.Count > 0 ? Hits[0] : null;
        Sources = Hits
            .Select(h => new AggregatedSourceRef { ServerId = h.ServerId, ServerName = h.ServerName, ItemId = h.Id })
            .ToList();
    }

    public AggregatedSearchHit Primary { get; }

    public List<AggregatedSearchHit> Hits { get; }

    public List<AggregatedSourceRef> Sources { get; }

    /// <summary>是否存在备选源（UI 角标「N 个源」）。</summary>
    public bool HasAlternatives => Sources.Count > 1;

    /// <summary>来源服务器名（<c>A / B</c>）。</summary>
    public string SourceLabel => string.Join(" / ", Sources.Select(s => s.ServerName).Where(n => !string.IsNullOrEmpty(n)));
}

/// <summary>聚合搜索结果（对应 Dart <c>AggregatedSearchResult</c>，含 <c>fromItems</c> 的合并语义）。</summary>
public sealed class AggregatedSearchResult
{
    public List<AggregatedSearchEntry> Items { get; } = new List<AggregatedSearchEntry>();

    /// <summary>失败被跳过的服务器名（单源失败不影响整体）。</summary>
    public List<string> FailedServers { get; } = new List<string>();

    /// <summary>参与本次搜索的源数量。</summary>
    public int SourceCount { get; set; }

    /// <summary>去重前的命中总数。</summary>
    public int TotalHits { get; set; }

    /// <summary>
    /// **去重后条目数**（= 去重键的个数，**在 <c>limit</c> 截断之前**统计）。
    /// <para>与另两个数的关系 —— **三者按设计本来就不相等**，UI 必须各自指名、不得"凑一致"（UI_SPEC_SHELL §P2）：</para>
    /// <list type="bullet">
    /// <item><see cref="TotalHits"/>：各源原始命中数之和，**在跳过空 Id 之前**自增 ⇒ 含空 Id 的命中；</item>
    /// <item><see cref="MergedCount"/>：跨源去重后的**条目数**（同一条目多源命中算 1 条），**不受 <c>limit</c> 影响**；</item>
    /// <item><see cref="Items"/><c>.Count</c>：本次实际返回的条目数，**已被 <c>limit</c> 截断**。</item>
    /// </list>
    /// <para>UI 显示「共 T 条」用本值；<see cref="Items"/><c>.Count</c> 只表示"本页返回多少"。</para>
    /// </summary>
    public int MergedCount { get; set; }

    /// <summary>
    /// **去重后参与分组的命中数**（= 各条目 <c>Hits.Count</c> 之和，即 <c>Id</c> 非空的命中总数，**在 <c>limit</c> 截断之前**）。
    /// **仅作注解/取证**：UI 「共 T 条」的权威口径 = <see cref="MergedCount"/>（`UI_SPEC_SHELL.md` §7.6 裁定，**:411**），
    /// **不是** <c>Items.Sum(e =&gt; e.Hits.Count)</c>；同一条目被多源命中时本值 &gt; <see cref="MergedCount"/>。
    /// </summary>
    public int MergedHitCount { get; set; }

    public bool HasFailures => FailedServers.Count > 0;

    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// 等价 Dart <c>AggregatedSearchResult.fromItems</c>：合并多源命中 → 去重 → 排序 → 截断。
    /// 空 <c>Id</c> 的命中不计入分组（对齐 merge 的 `if (source.item.id.isEmpty) continue`）。
    /// </summary>
    public static AggregatedSearchResult FromItems(
        IEnumerable<AggregatedSearchHit> hits,
        string term = null,
        int limit = 0,
        IEnumerable<string> failedServers = null)
    {
        var result = new AggregatedSearchResult();

        if (failedServers != null)
        {
            result.FailedServers.AddRange(failedServers.Where(n => !string.IsNullOrEmpty(n)));
        }

        var order = new List<string>();
        var grouped = new Dictionary<string, List<AggregatedSearchHit>>(StringComparer.Ordinal);

        if (hits != null)
        {
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                result.TotalHits++;
                if (string.IsNullOrEmpty(hit.Id)) continue;

                var key = AggregatedSearchHit.KeyOf(hit);
                if (!grouped.TryGetValue(key, out var bucket))
                {
                    bucket = new List<AggregatedSearchHit>();
                    grouped[key] = bucket;
                    order.Add(key);
                }
                bucket.Add(hit);
            }
        }

        var entries = order.Select(key => new AggregatedSearchEntry(grouped[key])).ToList();

        // 🔴 t25：去重后计数**必须在 limit 截断之前**取。
        //    `TotalHits`（:281）在跳过空 Id **之前**自增、`Items.Count` 已被 limit 截断 ⇒ 两者都不能当"共 T 条"。
        result.MergedCount = entries.Count;
        result.MergedHitCount = entries.Sum(e => e.Hits.Count);

        // t289：**类型优先级 → 相关度 → 名称 → 来源服务器名**（OrderBy 稳定 ⇒ 同分同名保持来源顺序）。
        //   为什么类型优先级在最前：用户实测「默认一堆小集」⇒ 默认面必须让 Series/Movie 先出（Episode 降到最后）。
        //   与适配器的"两趟取数 + 单集按剧折叠"配套：前者管"取到什么"，这里管"显示成什么序"。
        var sorted = entries
            .OrderBy(e => TypeRank(e.Primary))
            .ThenBy(e => Relevance(e.Primary, term))
            .ThenBy(e => (e.Primary == null ? string.Empty : e.Primary.Name) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => (e.Primary == null ? string.Empty : e.Primary.ServerName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (limit > 0 && sorted.Count > limit)
        {
            sorted = sorted.GetRange(0, limit);
        }

        result.Items.AddRange(sorted);
        return result;
    }

    /// <summary>
    /// t289：默认面的**类型优先级**（小 = 前）：`Series` 0 → `Movie` 1 → `BoxSet` 2 → 其他 3 → `Episode` 4。
    /// 判据出处 = 用户实测「现在搜索怎么默认是集 有很多小集？应该默认是剧」（2026-09-13 00:1x）。
    /// 只影响**显示序**；"能不能搜到集"由显式「集」chip 保证（本函数不禁任何类型）。
    /// </summary>
    private static int TypeRank(AggregatedSearchHit hit)
    {
        var type = (hit == null ? null : hit.Type) ?? string.Empty;
        switch (type)
        {
            case "Series": return 0;
            case "Movie": return 1;
            case "BoxSet": return 2;
            case "Episode": return 4;
            default: return 3;
        }
    }

    /// <summary>未实证的近似相关度：0 全等 &lt; 1 前缀 &lt; 2 包含 &lt; 3 其他（无搜索词时全为 3 ⇒ 退化为名称排序）。</summary>
    private static int Relevance(AggregatedSearchHit hit, string term)
    {
        var needle = (term ?? string.Empty).Trim();
        if (needle.Length == 0) return 3;

        var name = ((hit == null ? null : hit.Name) ?? string.Empty).Trim();
        if (string.Equals(name, needle, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        return 3;
    }
}

/// <summary>
/// t44 增量搜索回调载荷：**某个源返回后**的合并快照（用户诉求「谁先回谁先显示、后到的排在下面」）。
///
/// ⚠️ **语义边界（写死，别再自己造第二套名字）**：
/// <list type="bullet">
/// <item><see cref="Items"/> 顺序 = **条目首次到达的顺序**（后到源的新条目排在下面）；这与
/// <see cref="AggregatedSearchResult"/> 终值的「相关度 → 名称 → 服务器名」排序**不同**，是增量 API 的设计选择；
/// 终值仍由既有合并函数产出（逐字节同 <c>SearchAsync</c>）。</item>
/// <item>四个计数（<see cref="TotalHits"/> / <see cref="MergedCount"/> / <see cref="MergedHitCount"/> / <c>Items.Count</c>）
/// 与 <c>SearchAsync</c> 口径**逐字一致**，但在增量途中是**快照**（边跑边变）；<see cref="IsFinal"/>=true 那次即终值口径。</item>
/// <item>去重键仍是 <see cref="AggregatedSearchHit.KeyOf"/>：后到源命中同一键 ⇒ **就地合并**（同一条目 `Hits` 追加、
/// `HasAlternatives`/`SourceLabel` 随之更新），**不会**多出第二张卡。</item>
/// </list>
/// </summary>
public sealed class AggregatedSearchProgress
{
    public string Term { get; set; } = string.Empty;

    /// <summary>本次返回的源名（失败源也会到达）。</summary>
    public string ArrivedServerName { get; set; } = string.Empty;

    /// <summary>本次返回的源是否失败（失败源仍进 <see cref="FailedServers"/>）。</summary>
    public bool FailedThisTime { get; set; }

    /// <summary>已返回（含失败）的源数。</summary>
    public int ArrivedSourceCount { get; set; }

    /// <summary>尚未返回的源数（0 ⇒ 本次即最后一发）。</summary>
    public int PendingSourceCount { get; set; }

    /// <summary>本次搜索参与的全部源数。</summary>
    public int SourceCount { get; set; }

    /// <summary>**当前**失败源清单（`ServerName`；口径同 <c>SearchAsync</c>）。</summary>
    public List<string> FailedServers { get; } = new List<string>();

    /// <summary>**当前**合并条目快照（按首次到达顺序；已按 <c>limit</c> 截断）。</summary>
    public List<AggregatedSearchEntry> Items { get; } = new List<AggregatedSearchEntry>();

    /// <summary>与 <see cref="Items"/> **同序**的去重键（= <see cref="AggregatedSearchHit.KeyOf"/>），
    /// 供 UI **按去重键定位已有条目**（就地合并/原地更新，而不是追加重复卡）。</summary>
    public List<string> ItemKeys { get; } = new List<string>();

    /// <summary>本次到达源带来的命中条数（失败源为 0）。</summary>
    public int ArrivedHitCount { get; set; }

    /// <summary>按去重键定位已有条目下标（找不到 ⇒ -1）。</summary>
    public int IndexOfKey(string key) => string.IsNullOrEmpty(key) ? -1 : ItemKeys.IndexOf(key);

    /// <summary>按去重键定位已有条目（找不到 ⇒ null）。</summary>
    public AggregatedSearchEntry FindByKey(string key)
    {
        var index = IndexOfKey(key);
        return index < 0 ? null : Items[index];
    }

    /// <summary>当前累计命中数（口径同 <c>AggregatedSearchResult.TotalHits</c>：跳过空 Id **之前**自增）。</summary>
    public int TotalHits { get; set; }

    /// <summary>当前去重后条目数（**不受 limit 影响**）。</summary>
    public int MergedCount { get; set; }

    /// <summary>当前去重后命中总数（仅注解，不参与截断）。</summary>
    public int MergedHitCount { get; set; }

    /// <summary>本次是否最后一发（true ⇒ 上述计数与终值一致）。</summary>
    public bool IsFinal { get; set; }

    public override string ToString()
        => $"{(IsFinal ? "FINAL" : "on")} {ArrivedServerName}{(FailedThisTime ? "(失败)" : string.Empty)}｜到达 {ArrivedSourceCount}/{SourceCount}（待 {PendingSourceCount}）" +
           $"｜条目={Items.Count} 合并数={MergedCount} 命中={TotalHits}/{MergedHitCount}｜失败={FailedServers.Count}";
}

/// <summary>跨服聚合搜索（对应原版 <c>AggregatedSearchService</c>）：并发取数 → 去重 → 排序。</summary>
public sealed class AggregatedSearchService
{
    /// <summary>默认每源取数上限（等价 Emby 搜索默认 <c>limit = 60</c>，见 <c>EmbyService.SearchAsync</c>）。</summary>
    public const int DefaultLimit = 60;

    private readonly List<IAggregatedSearchSource> _sources;
    private readonly Action<string> _onLog;

    public AggregatedSearchService(IEnumerable<IAggregatedSearchSource> sources, Action<string> onLog = null)
    {
        _sources = sources == null
            ? new List<IAggregatedSearchSource>()
            : sources.Where(s => s != null).ToList();
        _onLog = onLog;
    }

    /// <summary>本次参与搜索的源（构造时固定；服务器配置变化时重建本服务）。</summary>
    public IReadOnlyList<IAggregatedSearchSource> Sources => _sources;

    private void Log(string message) => _onLog?.Invoke(message);

    /// <summary>
    /// 并发搜索全部源并合并。<paramref name="term"/> 为空白时直接返回空结果（不打网络）。
    /// 单源失败 ⇒ 记日志 + 记入 <see cref="AggregatedSearchResult.FailedServers"/>，其余源照常出结果。
    /// </summary>
    public async Task<AggregatedSearchResult> SearchAsync(string term, int limit = DefaultLimit, CancellationToken cancellationToken = default)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return new AggregatedSearchResult { SourceCount = _sources.Count };
        }

        var tasks = new List<Task<SourceSearchOutcome>>();
        foreach (var source in _sources)
        {
            tasks.Add(SearchOneAsync(source, query, limit, cancellationToken));
        }

        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

        var hits = new List<AggregatedSearchHit>();
        var failed = new List<string>();
        foreach (var outcome in outcomes)
        {
            if (outcome.Failed) failed.Add(outcome.ServerName);
            else hits.AddRange(outcome.Hits);
        }

        var result = AggregatedSearchResult.FromItems(hits, query, limit, failed);
        result.SourceCount = _sources.Count;
        return result;
    }

    /// <summary>
    /// **t44 增量搜索**：与 <see cref="SearchAsync"/> **完全相同的取数与合并**，但在**每个源返回时**回调一次
    /// <see cref="AggregatedSearchProgress"/>（快源先回 ⇒ UI 先显示，后到的新条目排在下面），
    /// 返回值仍是**终值**（与 <see cref="SearchAsync"/> 逐字节等价 —— 终值走同一个 <see cref="AggregatedSearchResult.FromItems"/>）。
    /// <para>本方法是<b>纯加法</b>：<see cref="SearchAsync"/> 的签名/返回/`Task.WhenAll` 行为一字未改。</para>
    /// <para><paramref name="progress"/>=null 时等价于 <see cref="SearchAsync"/>（不产生回调）。空词不打网络（同 SearchAsync）。</para>
    /// </summary>
    public async Task<AggregatedSearchResult> SearchIncrementalAsync(
        string term,
        int limit = DefaultLimit,
        IProgress<AggregatedSearchProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            progress?.Report(new AggregatedSearchProgress
            {
                Term = query,
                SourceCount = _sources.Count,
                ArrivedSourceCount = 0,
                PendingSourceCount = 0,
                IsFinal = true,
            });
            return new AggregatedSearchResult { SourceCount = _sources.Count };
        }

        var outcomes = new SourceSearchOutcome[_sources.Count];
        var accumulator = new IncrementalAccumulator(query, limit);
        var tasks = new List<Task>(_sources.Count);
        for (var index = 0; index < _sources.Count; index++)
        {
            var i = index;
            var source = _sources[index];
            tasks.Add(Task.Run(async () =>
            {
                var outcome = await SearchOneAsync(source, query, limit, cancellationToken).ConfigureAwait(false);
                outcomes[i] = outcome;
                AggregatedSearchProgress snapshot;
                lock (accumulator)
                {
                    if (outcome.Failed) accumulator.AddFailure(outcome.ServerName);
                    else accumulator.Add(outcome.Hits);
                    var arrived = accumulator.ArrivedSourceCount;
                    snapshot = accumulator.Snapshot(
                        DisplayName(source),
                        outcome.Failed,
                        outcome.Failed ? 0 : outcome.Hits.Count,
                        arrived,
                        _sources.Count - arrived,
                        _sources.Count,
                        isFinal: arrived >= _sources.Count);
                }
                progress?.Report(snapshot);
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // 🔴 终值：**走上游同一个合并函数**（与 SearchAsync 逐字节一致）—— 增量的"到达序快照"只用于过程显示。
        var hits = new List<AggregatedSearchHit>();
        var failed = new List<string>();
        foreach (var outcome in outcomes)
        {
            if (outcome == null) continue;
            if (outcome.Failed) failed.Add(outcome.ServerName);
            else hits.AddRange(outcome.Hits);
        }
        var result = AggregatedSearchResult.FromItems(hits, query, limit, failed);
        result.SourceCount = _sources.Count;
        return result;
    }

    /// <summary>
    /// 增量累加器（内部）：按**首次到达顺序**维护去重分组 —— 去重键仍是 <see cref="AggregatedSearchHit.KeyOf"/>，
    /// 命中同键 ⇒ 就地并入同一组（`Hits` 追加），故不会产生第二张卡；计数口径与 <c>FromItems</c> 逐字一致。
    /// </summary>
    private sealed class IncrementalAccumulator
    {
        private readonly string _term;
        private readonly int _limit;
        private readonly List<string> _arrivalOrder = new List<string>();
        private readonly Dictionary<string, List<AggregatedSearchHit>> _grouped = new Dictionary<string, List<AggregatedSearchHit>>(StringComparer.Ordinal);
        private readonly List<string> _failed = new List<string>();

        public IncrementalAccumulator(string term, int limit)
        {
            _term = term;
            _limit = limit;
        }

        public int ArrivedSourceCount { get; private set; }

        public int TotalHits { get; private set; }

        public void AddFailure(string serverName)
        {
            ArrivedSourceCount++;
            if (!string.IsNullOrEmpty(serverName)) _failed.Add(serverName);
        }

        public void Add(IEnumerable<AggregatedSearchHit> hits)
        {
            ArrivedSourceCount++;
            if (hits == null) return;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                TotalHits++;                                                  // 口径：跳过空 Id 之前自增
                if (string.IsNullOrEmpty(hit.Id)) continue;
                var key = AggregatedSearchHit.KeyOf(hit);
                if (!_grouped.TryGetValue(key, out var bucket))
                {
                    bucket = new List<AggregatedSearchHit>();
                    _grouped[key] = bucket;
                    _arrivalOrder.Add(key);                                   // 首次到达 ⇒ 排在后面（后到源的新条目）
                }
                bucket.Add(hit);                                              // 同键 ⇒ 就地合并
            }
        }

        public AggregatedSearchProgress Snapshot(
            string arrivedServerName,
            bool failedThisTime,
            int arrivedHitCount,
            int arrivedSourceCount,
            int pendingSourceCount,
            int sourceCount,
            bool isFinal)
        {
            var snapshot = new AggregatedSearchProgress
            {
                Term = _term,
                ArrivedServerName = arrivedServerName,
                FailedThisTime = failedThisTime,
                ArrivedHitCount = arrivedHitCount,
                ArrivedSourceCount = arrivedSourceCount,
                PendingSourceCount = pendingSourceCount,
                SourceCount = sourceCount,
                TotalHits = TotalHits,
                MergedCount = _arrivalOrder.Count,                            // = 去重后条目数（不受 limit 影响）
                MergedHitCount = _grouped.Values.Sum(v => v.Count),
                IsFinal = isFinal,
            };
            snapshot.FailedServers.AddRange(_failed);
            foreach (var key in _arrivalOrder)
            {
                if (_limit > 0 && snapshot.Items.Count >= _limit) break;      // Items 已截断；MergedCount 不截断
                snapshot.Items.Add(new AggregatedSearchEntry(_grouped[key]));
                snapshot.ItemKeys.Add(key);                                   // 与 Items 同序 ⇒ UI 能按键定位已有条目
            }
            return snapshot;
        }
    }

    /// <summary>单源取数：任何异常都被降级为「该源无结果 + 记日志」，绝不冒泡打断整体搜索。</summary>
    private async Task<SourceSearchOutcome> SearchOneAsync(
        IAggregatedSearchSource source,
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        var name = DisplayName(source);
        try
        {
            var raw = await source.SearchAsync(term, limit, cancellationToken).ConfigureAwait(false);
            var hits = new List<AggregatedSearchHit>();
            if (raw != null)
            {
                foreach (var hit in raw)
                {
                    if (hit == null) continue;
                    // 兜底补齐来源标注（源实现可能只填了条目字段）。
                    if (string.IsNullOrEmpty(hit.ServerId)) hit.ServerId = source.ServerId ?? string.Empty;
                    if (string.IsNullOrEmpty(hit.ServerName)) hit.ServerName = name;
                    hits.Add(hit);
                }
            }
            return new SourceSearchOutcome { ServerName = name, Hits = hits, Failed = false };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"聚合搜索：{name} 搜索失败（已跳过该源）：{ex.Message}");
            return new SourceSearchOutcome { ServerName = name, Hits = new List<AggregatedSearchHit>(), Failed = true };
        }
    }

    private static string DisplayName(IAggregatedSearchSource source)
    {
        if (!string.IsNullOrEmpty(source.ServerName)) return source.ServerName;
        return string.IsNullOrEmpty(source.ServerId) ? "未知服务器" : source.ServerId;
    }

    /// <summary>单源结果 + 失败标记（内部载体）。</summary>
    private sealed class SourceSearchOutcome
    {
        public string ServerName { get; set; } = string.Empty;

        public List<AggregatedSearchHit> Hits { get; set; } = new List<AggregatedSearchHit>();

        public bool Failed { get; set; }
    }
}

/// <summary>
/// Emby/Jellyfin 取数面适配器：薄封装 <see cref="EmbyService.SearchAsync"/>（Emby 家族共用一套 API）。
/// Navidrome 侧适配器由并行任务实现 <see cref="IAggregatedSearchSource"/> 后注入即可，本文件不引用 NavidromeService。
/// </summary>
public sealed class EmbyAggregatedSearchSource : IAggregatedSearchSource
{
    private readonly EmbyService _emby;
    private readonly string _serverName;

    public EmbyAggregatedSearchSource(EmbyService emby, string serverName = null)
    {
        _emby = emby ?? throw new ArgumentNullException(nameof(emby));
        _serverName = serverName;
    }

    public string ServerId => _emby.Server == null ? string.Empty : _emby.Server.Id;

    public string ServerName => !string.IsNullOrEmpty(_serverName)
        ? _serverName
        : (string.IsNullOrEmpty(ServerId) ? "Emby" : ServerId);

    public async Task<IReadOnlyList<AggregatedSearchHit>> SearchAsync(string term, int limit, CancellationToken cancellationToken)
    {
        // t289：默认（聚合）面的**剧优先两趟取数**。
        // 用户实测「默认搜出来一堆小集」的机制：单趟 8 类（DefaultSearchItemTypes）里 Episode 条数通常压倒 Series
        // ⇒ 结果页看起来就是一串第 1/2/3 集。现改为：
        //   ① 头部面 = Series/Movie/BoxSet（先取，保证「剧」在上）；
        //   ② 尾部面 = Episode/Person/Video/MusicAlbum/Audio，且 **Episode 按剧折叠到每剧至多 1 条**（"剧下挂的单集"）；
        //   ③ 头部在前、尾部在后拼接，按 Id 去重，总量由既有 limit 截断（下游 FromItems 再按类型优先级排序）。
        // ⚠️ 旧行为未删：调用方显式传 DefaultSearchItemTypes 仍是「单趟 8 类」（EmbyService.SearchAsync 一字未改）；
        //    显式点「集」chip 也不经本适配器（App 走 EmbyService.SearchAsync + "Episode"）⇒ 集仍可搜全。
        var head = await _emby.SearchAsync(
            term,
            limit,
            includeItemTypes: EmbyService.DefaultSearchHeadItemTypes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var tail = await _emby.SearchAsync(
            term,
            limit,
            includeItemTypes: EmbyService.DefaultSearchTailItemTypes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var items = new List<EmbyItem>();
        if (head != null && head.Items != null) items.AddRange(head.Items.Where(i => i != null));
        var headIds = new HashSet<string>(items.Select(i => i.Id), StringComparer.Ordinal);
        var tailSeen = 0;
        var foldedEpisodes = 0;
        var episodeSeries = new HashSet<string>(StringComparer.Ordinal);
        if (tail != null && tail.Items != null)
        {
            foreach (var item in tail.Items)
            {
                if (item == null) continue;
                tailSeen++;
                if (headIds.Contains(item.Id)) continue;               // 头部已含（同 id 不重复）
                if (string.Equals(item.Type, "Episode", StringComparison.Ordinal))
                {
                    // 折叠：同一剧集最多进 1 条单集（无 SeriesId 的单集以自身 id 为键，各自保留）
                    var key = string.IsNullOrEmpty(item.SeriesId) ? item.Id : item.SeriesId;
                    if (!episodeSeries.Add(key)) { foldedEpisodes++; continue; }
                }
                items.Add(item);
            }
        }
        var headCount = head == null || head.Items == null ? 0 : head.Items.Count;
        _emby.OnLog?.Invoke($"AGG-SEARCH-FOLD headTypes={EmbyService.DefaultSearchHeadItemTypes} head={headCount}"
            + $" tailTypes={EmbyService.DefaultSearchTailItemTypes} tail={tailSeen} foldedEpisodes={foldedEpisodes} kept={items.Count}");

        var output = new List<AggregatedSearchHit>();
        if (items.Count == 0) return output;

        // t217 判据修正（承接 t211）：**不能**再用「`item.PrimaryImageTag` 非空」当「有自家图」——
        // 那个 tag 可能是**剧集**的（`EmbyItem.PrimaryImageTag` = 自家 tag ⇒ 否则 IsEpisode ⇒ SeriesPrimaryImageTag），
        // 拿它配 `item.Id` 去请求 `/Items/{item.Id}/Images/Primary`，服务端会以 **HTTP 500** 回答
        // （t211 当刻复测：无自家 Primary 的 8 条 Episode 全部 500；**500 = Primary 缺图信号**，其余类型缺图才是 404，
        //  且换 tag / 换尺寸参数 / 换 index 全部仍 500 ⇒ 这不是"再试一次"能救的）。
        // 处置：改走**产品唯一入口** <see cref="EmbyService.ImageUrlIfAvailable"/> —— 自家 tag ⇒ 自家 id；
        // tag 来自剧集 ⇒ 自动换到 `SeriesId` 的 `Primary`（与 `HomePage.SeriesImageStub` 同族的口径，不新造第二套回退）。
        var ownCount = 0;
        var resourcedCount = 0;
        var noneCount = 0;
        var resourcedLogged = false;
        var noneLogged = false;
        foreach (var item in items)
        {
            if (item == null) continue;
            var imageUrl = _emby.ImageUrlIfAvailable(item, "Primary", maxHeight: 300);
            if (string.IsNullOrEmpty(imageUrl))
            {
                // ① 无图（无自家 Primary 也无剧集/父 tag）⇒ **空串 = 不发请求**，绝不造假图。
                noneCount++;
                if (!noneLogged)
                {
                    noneLogged = true;
                    _emby.OnLog?.Invoke($"AGG-IMG-NONE itemId={item.Id} type={item.Type}（无自家 Primary 也无剧集 tag ⇒ 空串，不发请求）");
                }
            }
            else if (IsResourcedToSeries(item, imageUrl))
            {
                // ② 换源（tag 实为剧集的 ⇒ 改到剧集 id 取图）⇒ 这正是修前必 500 的那一类。
                resourcedCount++;
                if (!resourcedLogged)
                {
                    resourcedLogged = true;
                    _emby.OnLog?.Invoke($"AGG-IMG-RESOURCE itemId={item.Id} type={item.Type} seriesId={item.SeriesId}（tag 来自剧集 ⇒ 已换源到剧集 Primary）");
                }
            }
            else
            {
                // ③ 自家图（自家 id + 自家 tag）⇒ 修前修后同路径。
                ownCount++;
            }
            output.Add(AggregatedSearchHit.FromEmbyItem(item, ServerId, ServerName, imageUrl));
        }
        // 每次取数的汇总行（两类情形各自的可读计数；与 ①② 的样例行同族，便于"有没有换源/无图"一眼可判）。
        _emby.OnLog?.Invoke($"AGG-IMG-SUMMARY returned={output.Count} own={ownCount} resourced={resourcedCount} none={noneCount}");
        return output;
    }

    /// <summary>
    /// URL 是否**换源到了剧集 id**（判据 = URL 路径里的 id 是条目的 <see cref="EmbyItem.SeriesId"/> 且不等于自身 id）。
    /// 只用于**日志计数**，不参与"给不给 URL"的决策（决策在 <see cref="EmbyService.ImageUrlIfAvailable"/> 单点）。
    /// </summary>
    private static bool IsResourcedToSeries(EmbyItem item, string imageUrl)
        => !string.IsNullOrEmpty(item.SeriesId)
           && !string.Equals(item.SeriesId, item.Id, StringComparison.Ordinal)
           && imageUrl.IndexOf("/Items/" + item.SeriesId + "/Images/", StringComparison.Ordinal) >= 0;
}

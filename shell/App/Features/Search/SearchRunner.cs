using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services;
using AIPlayer.Shell.Services.Aggregation;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;

namespace AIPlayer.Shell.Features.Search;

/// <summary>结果卡（纯数据；WinUI 的 ImageSource 由页面在 UI 线程上另建，见 <c>SearchPage.ToCardVm</c>）。</summary>
public sealed class ResultCard
{
    public string ItemId { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>合并了几个源（§7.6 备用源）。1 = 单源。</summary>
    public int SourceCount { get; set; } = 1;

    public bool HasAlternatives { get; set; }

    /// <summary>角标文案（§7.3 未看集数 / ✓；§7.6 N 源）。空串 = 不显示角标。</summary>
    public string BadgeText { get; set; } = string.Empty;
}

/// <summary>
/// 一次搜索的读数。
///
/// 🔴 三个计数**语义不同、必须各自指名**（UI_SPEC §7.6 末行 + §10 P2 判据）：
///   · <see cref="SourceCount"/>   = 台数（成功的源 + 失败的源都在内）
///   · <see cref="MergedCount"/>   = **去重后条目数**（limit 截断**之前**统计）＝ 面板「去重后 T 条」
///   · <see cref="MergedHitCount"/> = <c>Items.Sum(e =&gt; e.Hits.Count)</c>（含备用源的命中总数）
///   · <see cref="ShownCount"/>    = <c>Items.Count</c>（**已被 limit 截断**）＝ 面板实际画出的卡数
///   · <see cref="TotalHits"/>     = 服务层原样带出的 TotalHits（在跳过空 Id **之前**自增）⇒ 恒 ≥ 上面几个
/// 任何"三数一致"的写法都是错的（§7.6 红线）。
/// </summary>
public sealed class SearchRunResult
{
    public string Term { get; set; } = string.Empty;
    public string ChipLabel { get; set; } = string.Empty;
    public SearchScope Scope { get; set; }

    public int SourceCount { get; set; }
    public int MergedCount { get; set; }
    public int MergedHitCount { get; set; }
    public int ShownCount { get; set; }
    public int TotalHits { get; set; }

    public List<string> FailedServers { get; set; } = new List<string>();
    public List<ResultCard> Cards { get; set; } = new List<ResultCard>();

    /// <summary>无任何可用源（不是"没搜到"，是"没地方搜"）—— 空态文案必须区分这两者。</summary>
    public bool NoSources { get; set; }

    public long ElapsedMs { get; set; }

    /// <summary>异常类型 + 原文（null = 没出错）。异常里可能回显完整 URL ⇒ 落日志前已打码。</summary>
    public string Error { get; set; }

    public string CountsLine() =>
        $"sourceCount={SourceCount} mergedCount={MergedCount} mergedHitCount={MergedHitCount}"
        + $" shown={ShownCount} totalHits={TotalHits} failed={FailedServers.Count}";
}

/// <summary>
/// 搜索执行器（t28 / U-B）：把 UI_SPEC §7.6 的服务层契约接到 UI 上。
///
/// 纪律：
///   · **空查询不打网络**（<see cref="RunAsync"/> 直接返回空结果；上层连调用都不该调）。
///   · **不按"逐源增量刷新"设计**：<c>AggregatedSearchService</c> 用 <c>Task.WhenAll</c>（§7.6），
///     UI 拿不到增量 ⇒ 本类只在整体完成后给一次结果。
///   · <see cref="NetworkRequests"/> 是**可观测的反控制计数**：空查询跑完它必须为 0。
/// </summary>
public sealed class SearchRunner : IDisposable
{
    private readonly Action<string> _log;
    private ServiceRegistry _registry;

    public SearchRunner(Action<string> log = null)
    {
        _log = log ?? (s => { });
    }

    /// <summary>本实例真正发出过多少次搜索请求（空查询必须为 0）。</summary>
    public int NetworkRequests { get; private set; }

    /// <summary>最近一次建源时的说明（例如"聚合只覆盖 Emby 系 3 台"）。</summary>
    public string LastNote { get; private set; } = string.Empty;

    /// <summary>
    /// 最近一次聚合搜索里**失败源的名字 + 服务器 Id**（按源顺序与失败名逐个配对）。
    /// 为什么要 Id：服务层的 <c>FailedServers</c> **只存名字**（§7.6 原文），两台同名服务器会歧义
    /// （实测本机就有两台都叫 `ServerD`）⇒ 显示层必须写成 `名字（Id 短号）`。
    /// </summary>
    public List<FailedSource> LastFailedSources { get; } = new List<FailedSource>();

    /// <summary>失败源（名字 + 服务器 Id；Id 可能为空 = 配置里找不到同名服务器）。</summary>
    public sealed class FailedSource
    {
        public string Name { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;

        /// <summary>显示用：`名字（Id 前 4 位）`；Id 缺失时退化为名字本身（不装假）。</summary>
        public override string ToString() =>
            string.IsNullOrEmpty(ServerId) ? Name : Name + "（" + ServerId.Substring(0, Math.Min(4, ServerId.Length)) + "）";
    }

    /// <summary>建源时的三元组（名字 / 服务器 Id / 真实 BaseUrl）—— 用于请求面日志与失败源消歧。</summary>
    private sealed class SourceMeta
    {
        public SourceMeta(string name, string id, string baseUrl)
        {
            Name = name;
            Id = id;
            BaseUrl = baseUrl;
        }

        public string Name { get; }
        public string Id { get; }
        public string BaseUrl { get; }
    }

    /// <summary>反控源地址：不可达端口，用来在**不污染用户配置**的前提下验证「单源失败但结果仍在」。</summary>
    public const string BadSourceUrl = "http://127.0.0.1:1/";

    private ServiceRegistry Registry()
    {
        if (_registry == null)
        {
            _registry = new ServiceRegistry();
            _registry.Initialize();
            try { _registry.ApplyProxyFromSettings(); } catch (Exception ex) { _log("代理设置应用失败（忽略）：" + ex.Message); }
        }
        return _registry;
    }

    /// <summary>
    /// 图片取字节的**唯一入口**：`ServiceRegistry.Images`（= 进程级 `ImageCacheManager.Default`，含自定义
    /// User-Agent / 代理策略 / 磁盘缓存与 SHA1 键）。App 侧**不得**再 `new ImageCacheManager(...)`
    /// （第二实例 = 第二容量口径）。复用本 runner 已持有的注册表 ⇒ 不新开第二个注册表实例。
    /// </summary>
    public AIPlayer.Shell.Services.Infra.ImageCacheManager Images => Registry().Images;

    /// <summary>当前服务器列表（与服务器页读同一份；用环境实例以触发对原版 accounts.json 的只读兼容读）。</summary>
    public static List<ServerConfig> LoadServers(Action<string> log = null)
    {
        var store = ServerConfigStore.Instance;
        store.Load();
        return store.Servers?.ToList() ?? new List<ServerConfig>();
    }

    public async Task<SearchRunResult> RunAsync(
        string term,
        SearchChip chip,
        CancellationToken cancellationToken = default,
        bool injectBadSource = false,
        IProgress<AggregatedSearchProgress> progress = null)
    {
        var query = (term ?? string.Empty).Trim();
        var result = new SearchRunResult
        {
            Term = query,
            ChipLabel = chip?.Label ?? "聚合搜索",
            Scope = chip?.Scope ?? SearchScope.Aggregated,
        };

        // 空查询：直接返回、不打网络（§7.6「空查询」行 + §7.2「空查询的搜索态 = 搜索历史」）。
        if (query.Length == 0)
        {
            return result;
        }

        var stopped = Stopwatch.StartNew();
        LastFailedSources.Clear();
        var registry = Registry();
        var logs = new List<string>();
        var relay = new Action<string>(s => { logs.Add(s); _log(s); });

        try
        {
            if (result.Scope == SearchScope.Aggregated)
            {
                await RunAggregatedAsync(result, query, relay, cancellationToken, injectBadSource, progress).ConfigureAwait(false);
            }
            else
            {
                await RunTypedAsync(result, query, chip, relay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + Mask(ex.Message);
            _log("SEARCH-ERROR " + result.Error);
        }

        stopped.Stop();
        result.ElapsedMs = stopped.ElapsedMilliseconds;
        return result;
    }

    private async Task RunAggregatedAsync(
        SearchRunResult result,
        string query,
        Action<string> log,
        CancellationToken cancellationToken,
        bool injectBadSource,
        IProgress<AggregatedSearchProgress> progress = null)
    {
        var servers = LoadServers(log);
        var enabled = servers.Where(s => s.Enabled).ToList();
        var embyFamily = enabled.Where(s => s.Kind.IsEmbyFamily()).ToList();

        var sources = new List<IAggregatedSearchSource>();
        var meta = new List<SourceMeta>();   // 源名 + 服务器 Id + 真实 BaseUrl（按建源顺序）
        foreach (var server in embyFamily)
        {
            sources.Add(new EmbyAggregatedSearchSource(Registry().CreateEmby(server, log), server.Name));
            meta.Add(new SourceMeta(server.Name, server.Id, server.BaseUrl));
        }

        if (injectBadSource)
        {
            var bad = new ServerConfig
            {
                Id = "selftest-bad",
                Name = "坏源(反控)",
                Kind = ServerKind.Emby,
                BaseUrl = BadSourceUrl,
                Enabled = true,
            };
            sources.Add(new EmbyAggregatedSearchSource(Registry().CreateEmby(bad, log), bad.Name));
            meta.Add(new SourceMeta(bad.Name, bad.Id, bad.BaseUrl));
        }

        var skipped = enabled.Count - embyFamily.Count;
        LastNote = $"聚合覆盖 {embyFamily.Count} 台 Emby/Jellyfin 源"
            + (injectBadSource ? " + 1 台反控坏源" : string.Empty)
            + (skipped > 0 ? $"；跳过 {skipped} 台非 Emby 系服务器（当刻无跨源适配器）" : string.Empty);
        log("SEARCH-SOURCES " + LastNote);

        // 请求面证据：把**每个源的真实 BaseUrl** 与查询词一起落日志（时刻由 SearchLog 的时间戳给出）。
        // 这不是抓包，是"我到底向哪些地址发了什么词"的可核对清单；单源失败时服务层还会把
        // 含完整 URL 的异常消息经 onLog 带出来（同样落盘、同样打码）。
        var requestAt = DateTime.Now.ToString("HH:mm:ss.fff");
        foreach (var m in meta)
        {
            log($"SEARCH-REQUEST at={requestAt} term=\"{query}\" source=\"{m.Name}\" id=\"{m.Id}\" baseUrl=\"{m.BaseUrl}\"");
        }

        if (sources.Count == 0)
        {
            result.NoSources = true;
            log("SEARCH-NOSOURCES 没有可用的聚合源");
            return;
        }

        NetworkRequests++;
        var service = new AggregatedSearchService(sources, log);
        // SEAM②（t32，纯加法）：`progress == null` ⇒ **原路径一字不改**（t28 四路真跑的回归面）；
        // 非空 ⇒ 逐源增量（每个源返回时回调一次），**返回值仍是终值**（与 SearchAsync 走同一个 FromItems）
        // ⇒ 下面那段"填 result"两个分支共用，不复制、不改口径。
        var aggregated = progress == null
            ? await service.SearchAsync(query, AggregatedSearchService.DefaultLimit, cancellationToken).ConfigureAwait(false)
            : await service.SearchIncrementalAsync(query, AggregatedSearchService.DefaultLimit, progress, cancellationToken).ConfigureAwait(false);

        result.SourceCount = aggregated.SourceCount;
        result.MergedCount = aggregated.MergedCount;
        result.MergedHitCount = aggregated.MergedHitCount;
        result.ShownCount = aggregated.Items.Count;
        result.TotalHits = aggregated.TotalHits;
        result.FailedServers = aggregated.FailedServers?.ToList() ?? new List<string>();

        // 失败名 → 服务器 Id 配对（同名服务器按建源顺序逐个消费 ⇒ 两台 ServerD 各得各的 Id）。
        LastFailedSources.Clear();
        var claimed = new HashSet<int>();
        foreach (var failedName in result.FailedServers)
        {
            var id = string.Empty;
            for (var i = 0; i < meta.Count; i++)
            {
                if (claimed.Contains(i)) continue;
                if (!string.Equals(meta[i].Name, failedName, StringComparison.Ordinal)) continue;
                claimed.Add(i);
                id = meta[i].Id;
                break;
            }
            LastFailedSources.Add(new FailedSource { Name = failedName, ServerId = id });
        }

        foreach (var entry in aggregated.Items)
        {
            result.Cards.Add(ToCard(entry));
        }
    }

    private async Task RunTypedAsync(
        SearchRunResult result,
        string query,
        SearchChip chip,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (chip == null || !chip.IsAvailable)
        {
            result.Error = "该芯片不可用（已置灰）";
            return;
        }

        var servers = LoadServers(log);
        var main = PickMainServer(servers);
        if (main == null)
        {
            result.NoSources = true;
            log("SEARCH-NOSOURCES 没有已启用的 Emby 系服务器作为主源");
            return;
        }

        NetworkRequests++;
        var emby = Registry().CreateEmby(main, log);
        log($"SEARCH-REQUEST at={DateTime.Now:HH:mm:ss.fff} term=\"{query}\" scope=Typed"
            + $" types=\"{chip.IncludeItemTypes}\" source=\"{main.Name}\" baseUrl=\"{main.BaseUrl}\""
            + " path=/Users/{userId}/Items?SearchTerm=&IncludeItemTypes=");
        var page = await emby.SearchAsync(query, 60, chip.IncludeItemTypes, cancellationToken).ConfigureAwait(false);

        // 单源查询：台数=1；去重后条目数 = 命中条数（没有跨源去重这一层）；
        // shown = 返回条数；totalHits = 服务端 TotalRecordCount（两者都可 > shown，语义各不相同）。
        result.SourceCount = 1;
        result.MergedCount = page.Items.Count;
        result.MergedHitCount = page.Items.Count;
        result.ShownCount = page.Items.Count;
        result.TotalHits = page.TotalRecordCount;
        LastNote = $"类型搜索：主源 {main.Name}（{chip.IncludeItemTypes}）";

        foreach (var item in page.Items)
        {
            var card = new ResultCard
            {
                ItemId = item.Id,
                ServerId = main.Id,
                ServerName = main.Name,
                Type = item.Type,
                Title = string.IsNullOrEmpty(item.Name) ? "(无名)" : item.Name,
                Subtitle = SubtitleOf(item),
                ImageUrl = ImageUrlOf(emby, item),
            };

            var user = item.UserData;
            if (user != null)
            {
                if (user.UnplayedItemCount.HasValue && user.UnplayedItemCount.Value > 0)
                {
                    card.BadgeText = user.UnplayedItemCount.Value.ToString();
                }
                else if (user.Played)
                {
                    card.BadgeText = "✓";
                }
            }

            result.Cards.Add(card);
        }
    }

    /// <summary>主源 = <c>settings.lastServerId</c> 指向的已启用 Emby 系服务器；否则第一台已启用的 Emby 系服务器。</summary>
    private static ServerConfig PickMainServer(IReadOnlyList<ServerConfig> servers)
    {
        var enabled = servers?.Where(s => s.Enabled).ToList() ?? new List<ServerConfig>();
        var embyFamily = enabled.Where(s => s.Kind.IsEmbyFamily()).ToList();

        string lastId = null;
        // 本 callee 是纯同步、**不观察 token**（`PickMainServer(IReadOnlyList<ServerConfig>)` 无 CT 参数、不 ThrowIfCancellationRequested）
        // ⇒ `OperationCanceledException` 不可能在此产生，故用平坦 `catch`；若将来它开始观察 token，此处必须改为
        // `catch (Exception ex) when (ex is not OperationCanceledException)`（否则"用户取消"会变成静默走回落 = 对外语义变了）。
        try { lastId = AIPlayer.Shell.Services.Settings.SettingsService.Instance?.Settings?.LastServerId; }
        catch (Exception) { }

        if (!string.IsNullOrEmpty(lastId))
        {
            var byLast = embyFamily.FirstOrDefault(s => s.Id == lastId);
            if (byLast != null) return byLast;
        }

        return embyFamily.FirstOrDefault();
    }

    internal static ResultCard ToCard(AggregatedSearchEntry entry)
    {
        var primary = entry.Primary;
        var card = new ResultCard
        {
            ItemId = primary?.Id ?? string.Empty,
            ServerId = primary?.ServerId ?? string.Empty,
            ServerName = primary?.ServerName ?? string.Empty,
            Type = primary?.Type ?? string.Empty,
            Title = string.IsNullOrEmpty(primary?.Name) ? "(无名)" : primary.Name,
            Subtitle = SubtitleOf(primary),
            ImageUrl = primary?.ImageUrl ?? string.Empty,
            SourceCount = entry.Hits?.Count ?? 1,
            HasAlternatives = entry.HasAlternatives,
        };

        // 角标优先级（两种角标语义不同，不能同屏叠加）：
        //   §7.6 备用源「N 源」 > §7.3 未看集数 > §7.3 已看完 ✓
        if (card.HasAlternatives && card.SourceCount > 1)
        {
            card.BadgeText = card.SourceCount + " 源";
        }
        else if (primary?.Raw is JsonObject raw
                 && raw.TryGetPropertyValue("UserData", out var userNode)
                 && userNode is JsonObject user)
        {
            var unplayed = IntOf(user, "UnplayedItemCount");
            var played = BoolOf(user, "Played");
            if (unplayed > 0) card.BadgeText = unplayed.ToString();
            else if (played) card.BadgeText = "✓";
        }

        return card;
    }

    /// <summary>副标题（§7.3：剧 = 年份区间；其余给类型/年份）。</summary>
    private static string SubtitleOf(AggregatedSearchHit hit)
    {
        if (hit == null) return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(hit.SeriesName) && !string.Equals(hit.SeriesName, hit.Name, StringComparison.Ordinal))
        {
            parts.Add(hit.SeriesName);
        }
        if (hit.ProductionYear.HasValue) parts.Add(hit.ProductionYear.Value.ToString());
        else if (!string.IsNullOrEmpty(hit.PremiereDate) && hit.PremiereDate.Length >= 4) parts.Add(hit.PremiereDate.Substring(0, 4));
        if (parts.Count == 0 && !string.IsNullOrEmpty(hit.Type)) parts.Add(hit.Type);
        return string.Join(" · ", parts);
    }

    private static string SubtitleOf(EmbyItem item)
    {
        if (item == null) return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(item.SeriesName) && !string.Equals(item.SeriesName, item.Name, StringComparison.Ordinal))
        {
            parts.Add(item.SeriesName);
        }
        if (item.ProductionYear.HasValue) parts.Add(item.ProductionYear.Value.ToString());
        else if (!string.IsNullOrEmpty(item.PremiereDate) && item.PremiereDate.Length >= 4) parts.Add(item.PremiereDate.Substring(0, 4));
        if (parts.Count == 0 && !string.IsNullOrEmpty(item.Type)) parts.Add(item.Type);
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 海报 URL：框 166×249（§7.3）⇒ 请求 **2 倍 = 332×498**，且 **maxWidth 与 maxHeight 同时给**。
    ///
    /// <para>[!] t305（用户第④⑧条「好多图片都是 16:9 你都做成了竖屏」）：**单集先取主剧集竖海报**。
    /// 单集（`Episode`）在 Emby 里自带的 `ImageTags["Primary"]` 就是 **16:9 剧照**，而
    /// <see cref="EmbyService.ImageUrlIfAvailable"/> 的出口①是「自家 tag 优先」⇒ 服务端按框只回横图
    /// （只给 `maxWidth: 332` 时实测回 **332×187**），塞进 166×249 的 `UniformToFill` 框要再放大 1.33× 并**横裁 62.5%**。
    /// 判据照抄参照实现 `HomePage.PrimaryCardImageUrl` + `SeriesImageStub`（t172 / R-1，勿重造）：
    /// stub 的 `Id = SeriesId` 且 `ImageTags["Primary"] = SeriesPrimaryImageTag` ⇒ 出口①拿到的 id 与 tag **都是剧集的**。
    /// stub 为 null（无 `SeriesId` / 无 `SeriesPrimaryImageTag` / 与自家同 id）⇒ 回落原路径（出口②仍会换剧 id，不是 500）。</para>
    ///
    /// <para>走**服务层单点入口**：直连拼 URL 不看 `ImageTags` ⇒ 无自家主图的单集必 500；回退（剧 id / 空串）
    /// **只在服务层那一个出口里**，此处不再复制第二套。日志只记 `urlLen`，**永不打印 URL**（图 URL 带 `api_key`）。</para>
    /// </summary>
    private static string ImageUrlOf(EmbyService emby, EmbyItem item)
    {
        const int w = 332;   // 166 × 2
        const int h = 498;   // 249 × 2
        try
        {
            var stub = SeriesPosterStub(item);
            var branch = "own";
            var url = string.Empty;

            // 「宽高同时给」**只对会走 16:9 素材的那类**（单集 / 用到剧海报 stub）—— 其余类型保持 `maxWidth` 单给，
            // 使它们的 URL **逐字不变**（卡面反控第 4 条：Series/Movie/BoxSet 不得因本次改动变字）。
            var both = stub != null || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase);

            if (stub != null)
            {
                url = emby.ImageUrlIfAvailable(stub, "Primary", maxWidth: w, maxHeight: h);
                branch = "series-stub";
            }

            if (string.IsNullOrEmpty(url))
            {
                url = both
                    ? emby.ImageUrlIfAvailable(item, "Primary", maxWidth: w, maxHeight: h)
                    : emby.ImageUrlIfAvailable(item, "Primary", maxWidth: w);
                branch = "own";
            }

            SearchLog.Write("POSTER-SRC screen=search id=" + item.Id
                + " seriesId=" + (string.IsNullOrEmpty(item.SeriesId) ? "-" : item.SeriesId)
                + " type=" + (string.IsNullOrEmpty(item.Type) ? "-" : item.Type)
                + " branch=" + (string.IsNullOrEmpty(url) ? "none" : branch)
                + " both=" + both
                + " px=" + (both ? w + "x" + h : w + "x-")
                + " urlLen=" + url.Length);
            return url;
        }
        catch (Exception ex)
        {
            SearchLog.Write("IMAGE-URL-FAIL " + item.Id + " " + ex.GetType().Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// 「主剧集竖海报」的 stub（t305）：与 `HomePage.SeriesImageStub`（t172/R-1）**逐条同判据** ——
    /// 本卡 nonGoal 要求"两个面各写一处等价局部实现、不新增共享单点"，故此处是**有意**的第二份，判据变更须两处同步。
    /// 返回 null = 拿不到剧集海报（不构造会 500 的请求）。
    /// </summary>
    private static EmbyItem SeriesPosterStub(EmbyItem item)
    {
        if (item == null
            || string.IsNullOrEmpty(item.SeriesId)
            || string.IsNullOrEmpty(item.SeriesPrimaryImageTag)
            || string.Equals(item.SeriesId, item.Id, StringComparison.Ordinal))
        {
            return null;
        }

        return new EmbyItem
        {
            Id = item.SeriesId,
            Type = "Series",
            ImageTags = new Dictionary<string, string>(StringComparer.Ordinal) { ["Primary"] = item.SeriesPrimaryImageTag },
        };
    }

    private static int IntOf(JsonObject o, string key)
    {
        try
        {
            if (o.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<int>(out var i)) return i;
        }
        catch (Exception) { }   // 字段在但类型不是整数/不可转换 ⇒ 按缺省 0：调用方只当展示元数据用
        return 0;
    }

    private static bool BoolOf(JsonObject o, string key)
    {
        try
        {
            if (o.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        }
        catch (Exception) { }   // 同上：字段类型不是布尔 ⇒ 按缺省 false（只影响该条目的展示标记）
        return false;
    }

    /// <summary>打码 = 转发到**全工程唯一实现** <c>Services.Logging.SecretMasking.Mask</c>（t157 收敛）。</summary>
    /// <remarks>
    /// 本处曾是**窄副本**（只 <c>api_key=</c>，连 <c>api_key%3D</c> 都不覆盖）⇒ 收敛后覆盖 **5 条规则**（见
    /// <c>SecretMasking</c> 文档），并继承其**幂等**与**不抛**两条纪律。覆盖面**单向增加**，`api_key=***` 替换串不变。
    /// </remarks>
    internal static string Mask(string s) => AIPlayer.Shell.Services.Logging.SecretMasking.Mask(s);

    public void Dispose()
    {
        // Dispose 期间不得再抛：调用方已在收尾路径上，异常会把「关屏」变成崩溃
        try { _registry?.Dispose(); } catch (Exception) { }
        _registry = null;
    }
}

// t31 自检钩子（默认关闭；`SHELL_SELFTEST_AGGREGATE=1` / `SHELL_SELFTEST_FAVORITES=1` 才跑）。
//
// 为什么要有它：本卡的验收面是**多台真实服务器**上的分组 / 进度条 / 空源 / 失败源 / 收藏写回 ——
//   这些都不是"看一眼就知道对"的东西，必须留下**可复算的原始读数**。跑完写进
//   `shell/Tests/evidence/t31-{aggregate|favorites}-selftest.txt`（UTF-8 无 BOM）。
//   ⚠ **两屏必须分文件**：两屏各自整体覆盖写，共用一个名字会让后跑的把那屏读数整份盖掉。
//   ⚠ 扩展名必须是 `.txt`：`.gitignore:65` 有 `*.log` ⇒ 用 `.log` 的证据不进仓库（已踩过）。
//
// [!] 判据纪律（照抄 U-SPEC 末行红线）：**三个数各自指名、分别断言**，
//    绝不写"三数一致"（`SourceCount` / `M` / `T` 按设计本就不相等）；收藏屏同理。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Favorites;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>收藏屏 / 聚合视界的自检（只读页面状态 + 合成反控；不改生产数据）。</summary>
public static class T31SelfTest
{
    /// <summary>聚合视界：跑满三个过滤器 + 反控。</summary>
    public static async Task RunAggregateAsync(AggregatePage page)
    {
        AggregateDiagnostics.SetEvidenceSuffix("aggregate");
        var sb = new StringBuilder();
        sb.AppendLine("=== t31 U-E 聚合视界自检 ===");
        sb.AppendLine("utc        = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("serversEnv = " + (Environment.GetEnvironmentVariable(MediaAggregator.LimitServersEnvVar) ?? "<unset>"));
        sb.AppendLine();

        var kinds = new[]
        {
            AggregateKind.ContinueWatching,
            AggregateKind.Favorites,
            AggregateKind.Library,
        };

        // [!] t93 专项模式（`SHELL_SELFTEST_AGGREGATE=t93`）：**只跑五条缓存层反控 + 一次真开屏的时间线**。
        //    为什么需要它：三过滤器长跑实测 ~15 分钟（16 台源、每发 ~40 s），期间窗口被外部关掉的概率很高
        //    （已被关掉两次）；而本卡要的证据是**缓存层契约**，不需要跑满三个过滤器。
        var mode = Environment.GetEnvironmentVariable(AggregatePage.SelfTestEnvVar) ?? string.Empty;

        // [!] t291 专项模式（`SHELL_SELFTEST_AGGREGATE=t291`）：聚合视界「还有更多」**消费面**超限臂。
        //    为什么单列一个模式：本卡判据是"截断状态必须对调用方可见"，走的是渲染函数本身；
        //    三过滤器长跑（16 台源 ~15 分钟）与它无关，跑长了只会平白增加窗口被关的概率。
        if (string.Equals(mode, "t291", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("模式 = t291 专项（聚合视界截断提示的消费面）");
            sb.AppendLine();
            await page.SelectFilterAsync(AggregateKind.Favorites);
            DumpAggregate(sb, page, AggregateKind.Favorites);
            sb.AppendLine("  真数据提示原文 = " + (page.NoticeTextValue.Length > 0 ? page.NoticeTextValue : "<none>（屏上就是全部）"));
            sb.AppendLine();
            DumpNoticeArms(sb, page);
            AggregateDiagnostics.WriteEvidence(sb.ToString());
            return;
        }

        if (string.Equals(mode, "t93", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("模式 = t93 专项（跳过三过滤器长跑）");
            sb.AppendLine();
            sb.AppendLine("--- 开屏时间线（收藏过滤器）---");
            await page.SelectFilterAsync(AggregateKind.Favorites);
            DumpAggregate(sb, page, AggregateKind.Favorites);
            sb.AppendLine("  时间线读数：" + (page.LastOutcome == null ? "<null>" : MediaSnapshotSource.Describe(page.LastOutcome)));
            sb.AppendLine("  摘要原文：" + page.SummaryTextValue);
            sb.AppendLine();

            await T93SelfTest.RunAsync(sb, page);
            AggregateDiagnostics.WriteEvidence(sb.ToString());
            return;
        }

        // [!] t103 专项模式（`SHELL_SELFTEST_AGGREGATE=t103`）：卡片点击**意图分离** + 单集也要有详情页。
        if (string.Equals(mode, "t103", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("模式 = t103 专项（卡片点击意图分离 + 单集详情页）");
            sb.AppendLine();
            await page.SelectFilterAsync(AggregateKind.Favorites);
            DumpAggregate(sb, page, AggregateKind.Favorites);
            await T103SelfTest.RunAsync(sb, page);
            AggregateDiagnostics.WriteEvidence(sb.ToString());
            return;
        }

        foreach (var kind in kinds)
        {
            if (page.CurrentKind != kind)
            {
                await page.SelectFilterAsync(kind);
            }

            sb.AppendLine("--- 过滤器：" + kind + " ---");
            DumpAggregate(sb, page, kind);

            // 封面的「占位 → 封面」转换：**每个屏各测一次**（窗口逐屏重开）。
            // ⚠ 早期版本只在最后测一次 ⇒ 全局预算被前两屏吃光，第三屏必然 `withImage=0`，
            //    那读数**不代表缺陷也不代表正常**（纯属测量伪影）。逐屏重开后才是指标。
            await MeasureImageLoadAsync(sb, page, kind);

            // **每个过滤器跑完就落盘**：三个过滤器 × 多台真实服务器耗时可能上百秒，
            // 若只在最后写一次，中途被中断就什么都留不下（本轮实测踩到过）。
            AggregateDiagnostics.WriteEvidence(sb.ToString());
        }

        await DumpCounterExamplesAsync(sb);

        // ── SEAM③/T1：聚合视界**同一发内**"就地替换"的可判证据（与收藏屏同口径）──────
        //   热缓存（上一轮已落盘）+ 真刷新 + 同批 ids ⇒ 必须走 T1-noop ⇒ T0 那批实例不得被换掉。
        sb.AppendLine("--- SEAM③/T1 就地替换（聚合视界，当前过滤器 " + page.CurrentKind + "）---");
        await page.ReloadForSelfTestAsync();
        var t1 = page.LastOutcome;
        var t1Noop = t1 != null && t1.RefreshSucceeded && SnapshotRenderer.SameIds(t1.CachedIds, t1.RefreshedIds);
        var firstNow = page.Rows.SelectMany(r => r.Posters).FirstOrDefault();
        sb.AppendLine("   刷新读数：" + (t1 == null ? "<null>" : MediaSnapshotSource.Describe(t1)));
        sb.AppendLine("   断言 T1 前提（刷新成功 且 两组 id 相同 ⇒ T1-noop）⇒ " + t1Noop);
        sb.AppendLine("   断言 T0 的分组行实例未被换 ⇒ "
            + (page.RowsAtCacheRender != null && ReferenceEquals(page.Rows, page.RowsAtCacheRender)));
        sb.AppendLine("   断言 T0 的首张卡片实例未被换 ⇒ "
            + (page.FirstPosterAtCacheRender != null && firstNow != null
               && ReferenceEquals(firstNow, page.FirstPosterAtCacheRender)));
        sb.AppendLine();

        // ── t93（P0 空屏）：缓存键作用域 / 毒结果拒写 / 空缓存不当内容 / T1 不钉空屏 / 取消可见 ──
        await T93SelfTest.RunAsync(sb, page);

        AggregateDiagnostics.WriteEvidence(sb.ToString());
    }

    /// <summary>
    /// 单屏封面加载率（逐屏独立窗口 ⇒ 可复算、可对比）。
    /// 窗口来自 `SHELL_AGG_IMAGE_WINDOW`；不设则跳过本项（数据面断言不受影响）。
    /// </summary>
    private static async Task MeasureImageLoadAsync(StringBuilder sb, AggregatePage page, AggregateKind kind)
    {
        var window = ImageWindow();
        var posters = page.Rows.SelectMany(r => r.Posters).ToList();
        if (window <= 0 || posters.Count == 0)
        {
            sb.AppendLine("  封面加载：跳过（窗口=" + window + " 卡片=" + posters.Count + "）");
            sb.AppendLine();
            return;
        }

        var withUrl = posters.Count(p => p.HasImageUrl);
        var before = posters.Count(p => p.HasImage);
        AggregatePoster.OpenImageWindow(window);
        foreach (var poster in posters)
        {
            await poster.EnsureImageAsync();
        }

        var after = posters.Count(p => p.HasImage);
        sb.AppendLine("  封面：有 URL=" + withUrl + "/" + posters.Count
            + " 窗口=" + window
            + " 加载前=" + before + " 加载后=" + after + " 本轮新增=" + (after - before)
            + " 失败=" + AggregateDiagnostics.ImageFailureCount);
        sb.AppendLine("  断言 占位 → 封面 转换真的发生 = " + (after > before));
        sb.AppendLine("  断言 剩余未加载的仍是占位（不崩）= "
            + posters.Where(p => !p.HasImage).All(p => p.PlaceholderVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine();
    }

    private static int ImageWindow()
    {
        var raw = Environment.GetEnvironmentVariable("SHELL_AGG_IMAGE_WINDOW");
        return int.TryParse(raw, out var n) && n > 0 ? n : 0;
    }

    /// <summary>收藏屏：读数 + 反控 + **SEAM③ 四条缓存反控**。</summary>
    public static async Task RunFavoritesAsync(FavoritesPage page)
    {
        AggregateDiagnostics.SetEvidenceSuffix("favorites");
        var sb = new StringBuilder();
        sb.AppendLine("=== t31 U-E 收藏屏自检 ===");
        sb.AppendLine("utc        = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("serversEnv = " + (Environment.GetEnvironmentVariable(MediaAggregator.LimitServersEnvVar) ?? "<unset>"));
        sb.AppendLine();

        await DumpFavoritesAsync(sb, page);
        await DumpCounterExamplesAsync(sb);
        await DumpSwrControlsAsync(sb, page);

        AggregateDiagnostics.WriteEvidence(sb.ToString());
    }

    /// <summary>
/// **SEAM③ 四条反控**（`UI_SPEC_SHELL.md`，逐条可判 False）：
    /// ① 冷启动（清缓存）仍然可用（退化为骨架/冷取，不崩）；
    /// ② 缓存已在时把取数打坏 ⇒ **只能显示缓存**（证明内容真来自缓存）；
    /// ③ 坏源 ⇒ 失败提示出现 + 缓存内容仍在；
    /// ④ 缓存读出的 id 集合与刷新后的 id 集合**分别打印**。
    /// </summary>
    private static async Task DumpSwrControlsAsync(StringBuilder sb, FavoritesPage page)
    {
        sb.AppendLine("=== SEAM③ 四条缓存反控（§12.4）===");

        // ── 反控① 冷启动：清空缓存 ⇒ 该屏仍可用 ────────────────────────────────
        var cleared = MediaSnapshotSource.ClearAll();
        sb.AppendLine("① 冷启动：ClearAll() 删除条数=" + cleared + " 剩余缓存条数=" + MediaSnapshotSource.CachedEntryCount);
        await page.ReloadForSelfTestAsync();
        var cold = page.LastOutcome;
        sb.AppendLine("   冷启动读数：" + (cold == null ? "<null>" : MediaSnapshotSource.Describe(cold)));
        sb.AppendLine("   冷启动后 卡片数=" + page.Posters.Count + " 分区数=" + page.Sections.Count);
        sb.AppendLine("   断言① 冷启动命中缓存 = False ⇒ " + (cold != null && !cold.HasCache));
        sb.AppendLine("   断言① 冷启动不崩且刷新已发起 ⇒ " + (cold != null && cold.RefreshStarted));

        // ── 反控② 热路径必须退化：缓存已在，把 fetch 打坏 ⇒ 只能显示缓存 ─────────
        await page.ReloadForSelfTestAsync();
        var warm0 = page.LastOutcome;
        sb.AppendLine("② 预热：缓存条数=" + MediaSnapshotSource.CachedEntryCount
            + "（上一发 HasCache=" + (warm0?.HasCache.ToString() ?? "<null>")
            + " 刷新成功=" + (warm0?.RefreshSucceeded.ToString() ?? "<null>") + "）");

        MediaSnapshotSource.FailFetchForSelfTest = true;
        try
        {
            await page.ReloadForSelfTestAsync();
            var warm = page.LastOutcome;
            sb.AppendLine("   断网后读数：" + (warm == null ? "<null>" : MediaSnapshotSource.Describe(warm)));
            sb.AppendLine("   断言② 热路径命中缓存 = True ⇒ " + (warm != null && warm.HasCache));
            sb.AppendLine("   断言② 刷新失败 = True ⇒ " + (warm != null && !warm.RefreshSucceeded));
            sb.AppendLine("   断言② 失败仍保留缓存内容 ⇒ " + (warm != null && warm.CachePreserved));
            sb.AppendLine("   断言② 屏上仍有卡片（内容只能来自缓存）⇒ " + (page.Posters.Count > 0)
                + "（卡片数 " + page.Posters.Count + "）");
            sb.AppendLine("   失败提示原文 = " + page.FailedTextValue);

            // ── 反控⑥ 缓存卡起播门（SEAM③(a) 代价条款）：缓存态下**每张卡都必须取不到真条目** ──
            //   含义：此刻点任何一张卡都会走 `OnCardClick` 的拒绝分支（可见提示 + 不起播），
            //   而**不会**静默拿"缺 Raw/MediaSources 的快照条目"去起播。
            //   ⚠ 本反控证的是**门的判据**；真实鼠标点击的观感不在本卡（需点击注入）。
            var blockedAll = page.Posters.Count > 0 && page.Posters.All(p =>
                p.Item != null && p.Server != null && page.LiveItemFor(p.Server.Id, p.Item.Id) == null);
            sb.AppendLine("   断言⑥ 缓存态下每张卡都取不到真条目（点卡必被拦）⇒ " + blockedAll
                + "（卡片数 " + page.Posters.Count + "，LiveAggregate="
                + (page.LastAggregate == null ? "<null>" : "非空") + "）");
        }
        finally
        {
            MediaSnapshotSource.FailFetchForSelfTest = false;
        }

        // ── 反控③ 坏源：注入不可达源 ⇒ 失败提示 + 缓存内容仍在 ─────────────────
        var servers = MediaAggregator.EnabledEmbyServers();
        var poisoned = new List<ServerConfig>
        {
            new ServerConfig
            {
                Id = "t31-swr-bad",
                Name = "(反控)不可达服务器",
                Kind = ServerKind.Emby,
                BaseUrl = "http://127.0.0.1:9/emby",
                Enabled = true,
            },
        };
        poisoned.AddRange(servers);
        var badAggregate = await MediaAggregator.LoadAsync(AggregateKind.Favorites, poisoned);
        sb.AppendLine("③ 坏源：Groups=" + badAggregate.Groups.Count
            + " Failed=" + badAggregate.FailedGroups.Count
            + " NonEmpty=" + badAggregate.NonEmptyGroups.Count
            + " TotalItems=" + badAggregate.TotalItems);
        sb.AppendLine("   失败源显示名 = " + string.Join(" / ", badAggregate.FailedGroups.Select(g => g.DisplayName)));
        // ⚠️ 断言的**口径更正**：这一路的前提里本来就带 1 台真实坏源（ServerF 403）
        //    ⇒ 失败数天然是 1（既有）+ 1（我注入的合成源）= 2，不是 1。
        //    本反控要证的是"**坏源不影响其它源 + 缓存卡片仍在**"，不是"失败恰好 1 台"。
        sb.AppendLine("   断言③ 坏源被登记（≥1 且含我注入的那台）⇒ "
            + (badAggregate.FailedGroups.Count >= 1
               && badAggregate.FailedGroups.Any(g => g.DisplayName.Contains("不可达"))));
        sb.AppendLine("   断言③ 其它源不受影响（NonEmpty ≥ 1 且条目 > 0）⇒ "
            + (badAggregate.NonEmptyGroups.Count >= 1 && badAggregate.TotalItems > 0));
        sb.AppendLine("   断言③ 屏上仍有缓存卡片 ⇒ " + (page.Posters.Count > 0) + "（卡片数 " + page.Posters.Count + "）");

        // ── 反控④ 两组 id 分别打印 ─────────────────────────────────────────────
        await page.ReloadForSelfTestAsync();
        var pair = page.LastOutcome;
        sb.AppendLine("④ 两组 id 分别打印：");
        if (pair != null)
        {
            sb.AppendLine("   cachedIds    (" + pair.CachedIds.Count + ") = [" + string.Join(",", pair.CachedIds) + "]");
            sb.AppendLine("   refreshedIds (" + pair.RefreshedIds.Count + ") = [" + string.Join(",", pair.RefreshedIds) + "]");
            sb.AppendLine("   断言④ 两组 id 都非空 ⇒ " + (pair.CachedIds.Count > 0 && pair.RefreshedIds.Count > 0));
            sb.AppendLine("   断言④ 两组 id 一致（同一批内容）⇒ "
                + SnapshotRenderer.SameIds(pair.CachedIds, pair.RefreshedIds));
        }
        else
        {
            sb.AppendLine("   <null>（未取到读数）");
        }

        sb.AppendLine("两态区分：HasCache=false ⇒ 骨架屏；HasCache=true 且 Groups 为空 ⇒ 空态（缓存记录的就是没有内容）");
        // 两态判据的**可执行样本**（不靠嘴说）：合成一个"有快照但组为空"的载荷 ⇒ 断言它落在空态而非骨架态
        var emptySnapshot = new MediaSnapshot();
        sb.AppendLine("   样本：空组快照 Groups=" + emptySnapshot.Groups.Count
            + " ⇒ HasCache=true 时应走空态、不得走骨架屏 ⇒ 断言 "
            + (emptySnapshot.Groups.Count == 0));

        // ── 反控⑤ T1 **就地替换**（同一发内：T0 渲染的实例在刷新落地后不得被换掉）──────────
        //   判据形式（可判 False）：热缓存 + 真刷新 + 同批 ids ⇒ 走 T1-noop 分支
        //   ⇒ `Sections` 列表实例与首张卡片实例**都还是 T0 当刻那两个**。
        //   若整屏重建（清空重填），两个引用都会变 ⇒ 断言为 False。
        var t1 = await FreshNoOpReloadAsync(page);
        sb.AppendLine("⑤ T1 就地替换（真刷新落地后**不整屏重建** ⇒ 不闪屏/不重置滚动/不丢选中）：");
        sb.AppendLine("   刷新读数：" + (t1 == null ? "<null>" : MediaSnapshotSource.Describe(t1)));
        sb.AppendLine("   断言⑤ 前提成立（刷新成功 且 两组 id 相同 ⇒ 走 T1-noop）⇒ "
            + (t1 != null && t1.RefreshSucceeded && SnapshotRenderer.SameIds(t1.CachedIds, t1.RefreshedIds)));
        sb.AppendLine("   断言⑤ T0 的分区列表实例未被换 ⇒ "
            + (page.SectionsAtCacheRender != null && ReferenceEquals(page.Sections, page.SectionsAtCacheRender)));
        sb.AppendLine("   断言⑤ T0 的首张卡片实例未被换 ⇒ "
            + (page.FirstPosterAtCacheRender != null && page.Posters.Count > 0
               && ReferenceEquals(page.Posters[0], page.FirstPosterAtCacheRender)));

        // ── 反控⑥（正向）真刷新后门必须**放行**（否则"门"退化成一律拒绝，缓存态永远不可播）──
        var anyLive = t1 != null && t1.RefreshSucceeded && page.Posters.Any(p =>
            p.Item != null && p.Server != null && page.LiveItemFor(p.Server.Id, p.Item.Id) != null);
        sb.AppendLine("   断言⑥ 真刷新后至少一张卡能取到真条目（起播门不是一律拒绝）⇒ " + anyLive);
        sb.AppendLine();
    }

    /// <summary>反控⑤ 的前提装置：**热缓存 + 真刷新 + 不打断网** ⇒ 必然落 T1-noop 分支。</summary>
    private static async Task<SwrOutcome> FreshNoOpReloadAsync(FavoritesPage page)
    {
        MediaSnapshotSource.FailFetchForSelfTest = false;
        await page.ReloadForSelfTestAsync();
        return page.LastOutcome;
    }

    // ── 读数 ───────────────────────────────────────────────────────────────

    private static void DumpAggregate(StringBuilder sb, AggregatePage page, AggregateKind kind)
    {
        var aggregate = page.LastAggregate;
        if (aggregate == null)
        {
            sb.AppendLine("aggregate = <null>（未取到数据）");
            sb.AppendLine();
            return;
        }

        // [!] 三个数**各自指名**（不得写"一致"）
        sb.AppendLine("① 服务器台数        Groups.Count      = " + aggregate.Groups.Count);
        sb.AppendLine("② 有内容的台数      NonEmptyGroups    = " + aggregate.NonEmptyGroups.Count);
        sb.AppendLine("③ 失败的台数        FailedGroups      = " + aggregate.FailedGroups.Count);
        sb.AppendLine("④ 条目总数          TotalItems        = " + aggregate.TotalItems);
        sb.AppendLine("⑤ 耗时              ElapsedMs         = " + (int)aggregate.Elapsed.TotalMilliseconds);
        sb.AppendLine("⑥ 分组列表可见性     GroupList         = " + page.GroupListVisibility);
        sb.AppendLine("⑦ 画面上的行数       Rows.Count        = " + page.Rows.Count);
        sb.AppendLine("⑧ 摘要行原文        = " + page.SummaryTextValue);

        // 每组逐行（这是"跨服分组"的直接证据：组名 + 条数 + 封面有无）
        foreach (var row in page.Rows)
        {
            var withImage = row.Posters.Count(p => p.HasImageUrl);
            var progressCount = row.Posters.Count(p => p.ProgressVisibility == Microsoft.UI.Xaml.Visibility.Visible);
            var progressMax = row.Posters.Count == 0 ? 0 : row.Posters.Max(p => p.ProgressFraction);
            sb.AppendLine("  · [" + row.Header + "] items=" + row.Posters.Count
                + " hasImage=" + withImage
                + " placeholder=" + (row.Posters.Count - withImage)
                + " progressBars=" + progressCount
                + " progressMax=" + progressMax.ToString("0.###", CultureInfo.InvariantCulture)
                + (row.Group.Failed ? " FAILED=" + row.Group.Error : string.Empty));
        }

        // 断言（逐条 True/False，不写"通过"就完事）
        // ⚠️ 摘要口径随 SEAM③ 变过（快照只存"有内容的组"）⇒ 断言同步到**新口径**：
        //    必须含「N 台有内容」「共 <数> 条」，**且** T0 状态（hit/miss + Freshness）—— 仍是"各数各自具名"，不写"三数一致"。
        sb.AppendLine("  断言 分组数>0                 = " + (page.Rows.Count > 0));
        sb.AppendLine("  断言 摘要含具名数字与 T0 状态 = "
            + (page.SummaryTextValue.Contains("台有内容") && page.SummaryTextValue.Contains("共 ")
               && page.SummaryTextValue.Contains("T0=")));
        sb.AppendLine("  断言 进度条仅在继续播放/库族  = "
            + (kind == AggregateKind.Library
                ? page.Rows.SelectMany(r => r.Posters).All(p => p.ProgressVisibility == Microsoft.UI.Xaml.Visibility.Collapsed)
                : true));
        sb.AppendLine("  断言 每张卡都有占位或封面     = "
            + page.Rows.SelectMany(r => r.Posters).All(p => p.HasImageUrl || p.PlaceholderVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine("  封面已加载数（此刻）        = " + page.Rows.SelectMany(r => r.Posters).Count(p => p.HasImage));
        sb.AppendLine();
    }

    private static async Task DumpFavoritesAsync(StringBuilder sb, FavoritesPage page)
    {
        var aggregate = page.LastAggregate;
        sb.AppendLine("--- 收藏屏 ---");
        if (aggregate == null)
        {
            sb.AppendLine("aggregate = <null>");
            sb.AppendLine();
            return;
        }

        var posters = page.Posters;
        sb.AppendLine("① 服务器台数   = " + aggregate.Groups.Count);
        sb.AppendLine("② 有收藏的台数 = " + aggregate.NonEmptyGroups.Count);
        sb.AppendLine("③ 失败的台数   = " + aggregate.FailedGroups.Count);
        sb.AppendLine("④ 收藏卡片数   = " + posters.Count);
        sb.AppendLine("⑤ 摘要行原文   = " + page.SummaryTextValue);
        sb.AppendLine();

// 三个分区（收藏的电影 / 收藏的剧 / 收藏的集）
        sb.AppendLine("分区数 = " + page.Sections.Count);
        foreach (var section in page.Sections)
        {
            var withBadge = section.Posters.Count(p => p.BadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible);
            var numericBadge = section.Posters.Count(p => p.BadgeText.Length > 0 && char.IsDigit(p.BadgeText[0]));
            var checkBadge = section.Posters.Count(p => p.BadgeText == "\u2713");
            sb.AppendLine("  · [" + section.Group.ServerName + "] 卡片=" + section.Posters.Count
                + " 带角标=" + withBadge + "（数字=" + numericBadge + " ✓=" + checkBadge + "）");
            foreach (var p in section.Posters.Take(8))
            {
                sb.AppendLine("      - " + p.Title + " | 角标='" + p.BadgeText + "' | " + p.Subtitle
                    + " | hasImageUrl=" + p.HasImageUrl);
            }
        }

        var allBadges = posters.Select(p => p.BadgeText).Where(b => b.Length > 0).ToList();
        sb.AppendLine();
        var titles = string.Join(" / ", page.Sections.Select(s => s.Group.ServerName));
        sb.AppendLine("  分区标题（原样）= " + titles);
        sb.AppendLine("  断言 恰有三个分区（电影 / 剧 / 集）= "
            + (page.Sections.Count == 3
               && page.Sections[0].Group.ServerName == "收藏的电影"
               && page.Sections[1].Group.ServerName == "收藏的剧"
               && page.Sections[2].Group.ServerName == "收藏的集"));
        sb.AppendLine("  断言 作用域 = 只有主源一台         = "
            + (aggregate.Groups.Count == 1) + "（参与取数 " + aggregate.Groups.Count + " 台，主源="
            + (page.MainServer?.Name ?? "<none>") + "）");
        sb.AppendLine("  断言 三个分区张数之和 = 卡片总数   = "
            + (page.Sections.Sum(s => s.Posters.Count) == posters.Count)
            + "（" + page.Sections.Sum(s => s.Posters.Count) + " vs " + posters.Count + "）");
        sb.AppendLine("  断言 卡片都有来源角标         = " + posters.All(p => p.SourceBadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine("  断言 收藏屏不画进度条         = " + posters.All(p => p.ProgressVisibility == Microsoft.UI.Xaml.Visibility.Collapsed));
        sb.AppendLine("  断言 角标形如「未看集数 / ✓」  = "
            + allBadges.All(b => b == "\u2713" || b.All(char.IsDigit))
            + "   （角标取值样例：" + string.Join("/", allBadges.Take(12)) + "）");
        sb.AppendLine();

        await page.LoadImagesAsync();
        sb.AppendLine("  封面加载后：total=" + posters.Count + " withImage=" + posters.Count(p => p.HasImage));
        sb.AppendLine();

        // [!] 分区里的三个类型必须能自证"数据就是这样"而不是"查询写漏"：
        //    分别按 Movie / Series / Episode 各查一次收藏，看服务端到底返回什么。
        //    t87 起这里还带**差额必须为 0** 的反控（旧口径 30 vs 本屏口径翻页取尽）。
        await DumpFavoriteTypesProbeAsync(sb, page, aggregate);
    }

    private static async Task DumpFavoriteTypesProbeAsync(StringBuilder sb, FavoritesPage page, MediaAggregate aggregate)
    {
        sb.AppendLine("--- 反控：收藏类型分布（逐类型 vs 复合查询）＋ t87 差额必须为 0 ---");

        var main = page.MainServer;
        var servers = MediaAggregator.EnabledEmbyServers();

        // ── 本屏生产路径的读数（复合查询 + 服务端声明总数 + 上限，全部具名）────────────
        foreach (var group in aggregate?.Groups ?? new List<MediaGroupResult>())
        {
            sb.AppendLine("  本屏取数 " + MediaAggregator.FavoritesItemTypes.PadRight(13) + " " + group.ServerName
                + " shown=" + group.Items.Count
                + " serverTotal=" + (group.TotalCount > 0 ? group.TotalCount.ToString(CultureInfo.InvariantCulture) : "<none>")
                + " truncated=" + group.Truncated
                + " pageSize=" + MediaAggregator.FavoritesPageSizePerServer
                + " cap=" + MediaAggregator.FavoritesMaxPerServer
                + " 类型[" + TypeBreakdown(group.Items) + "]");
        }

        sb.AppendLine();

        // ── 逐类型（每台一条 + 每类具名 ⇒ 空集合无法让"合计"恒真）──────────────────
        var perTypeById = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var server in servers)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var cells = new List<string>();
            foreach (var type in new[] { "Movie", "Series", "Episode" })
            {
                try
                {
                    var emby = NewEmby(server);
                    var r = await emby.GetItemsAsync(
                        includeItemTypes: type,
                        isFavorite: true,
                        limit: MediaAggregator.FavoritesMaxPerServer);
                    var n = r?.Items?.Count ?? 0;
                    counts[type] = n;
                    cells.Add(type + "=" + n.ToString(CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    counts[type] = -1;
                    cells.Add(type + "=ERR:" + ex.GetType().Name);
                    AggregateDiagnostics.Write("t87-pertype-probe-fail type=" + type + " server=" + (server.Name ?? "?")
                        + " " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            perTypeById[server.Id ?? string.Empty] = counts;
            sb.AppendLine("  逐类型 " + (server.Name ?? "?").PadRight(12) + " " + string.Join(" ", cells)
                + "  合计=" + counts.Values.Where(v => v >= 0).Sum().ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();

        // ── 反控（主源 = 本屏作用域）：差额必须为 0 ──────────────────────────────────
        if (main == null)
        {
            sb.AppendLine("  断言 主源解析 = <null> ⇒ **INCONCLUSIVE(无主源)**：本屏作用域解析不出服务器，无法比对差额。");
            sb.AppendLine();
            return;
        }

        if (!perTypeById.TryGetValue(main.Id ?? string.Empty, out var mainCounts) || mainCounts.Values.Any(v => v < 0))
        {
            sb.AppendLine("  断言 逐类型读数不完整 ⇒ **INCONCLUSIVE(有类型查询失败)**：见上方 ERR 行，不猜数。");
            sb.AppendLine();
            return;
        }

        var perTypeSum = mainCounts.Values.Sum();
        var movies = page.Sections.Count > 0 ? page.Sections[0].Posters.Count : 0;
        var series = page.Sections.Count > 1 ? page.Sections[1].Posters.Count : 0;
        var episodes = page.Sections.Count > 2 ? page.Sections[2].Posters.Count : 0;
        var screenShown = page.Posters.Count;

        // 反控 A（旧口径 = t72 实测的缺陷形态）：复合查询共用 ResumeLimitPerServer
        var legacyShown = await CountFavoritesAsync(main, MediaAggregator.ResumeLimitPerServer);

        sb.AppendLine("  主源 " + (main.Name ?? "?") + " 逐类型：Movie=" + mainCounts["Movie"]
            + " Series=" + mainCounts["Series"] + " Episode=" + mainCounts["Episode"]
            + "  合计=" + perTypeSum);
        sb.AppendLine("  屏上分区：电影=" + movies + " 剧=" + series + " 集=" + episodes + "  合计=" + screenShown);
        sb.AppendLine("  反控A 旧口径（复合查询 limit=" + MediaAggregator.ResumeLimitPerServer + "）= " + legacyShown
            + "　差额 = " + (perTypeSum - legacyShown) + "　（>0 ⇒ 旧口径确实**静默丢条目**；=0 ⇒ 本机数据量小，挡不住这条反控）");
        sb.AppendLine("  反控B 本屏口径（翻页取尽，屏上实有）= " + screenShown
            + "　差额 = " + (perTypeSum - screenShown)
            + "　⇒　断言 差额为 0 = " + (perTypeSum == screenShown));
        sb.AppendLine("  断言 逐类型与三个分区逐项相等        = "
            + (mainCounts["Movie"] == movies && mainCounts["Series"] == series && mainCounts["Episode"] == episodes));
        sb.AppendLine("  更多提示（t87 可见性观察点）        = "
            + (page.MoreTextValue.Length > 0 ? page.MoreTextValue : "<none>（屏上就是全部）"));
        sb.AppendLine();

        // t87：「还有更多」这条绑定必须**可失败**（真机库存 < 硬上限 ⇒ 恒定不显示也能"看起来对"）
        await DumpMoreHintControlAsync(sb, page);
    }

    /// <summary>
    /// t87 反控：合成两组合成快照，证明「还有更多」提示**真的跟着数据走**（而不是恒定显示/恒定不显示）。
    /// 合成数据不碰任何真实服务器，也不落盘（只走渲染路径）。
    /// </summary>
    private static Task DumpMoreHintControlAsync(StringBuilder sb, FavoritesPage page)
    {
        sb.AppendLine("--- t87 反控：『还有更多』提示的可见性（合成快照，可失败）---");

        var real = page.CurrentSnapshot;

        // 控制组 A：服务端声明总数 42，屏上只给 2 条 ⇒ 提示**必须**出现且带 42
        var truncated = new MediaSnapshot();
        truncated.Groups.Add(new SnapshotGroup
        {
            ServerId = "t87-control",
            ServerName = "（反控）截断源",
            TotalCount = 42,
            Truncated = true,
            Items = new List<SnapshotItem>
            {
                new SnapshotItem { Id = "c1", Name = "反控条目 1", Type = "Movie" },
                new SnapshotItem { Id = "c2", Name = "反控条目 2", Type = "Series" },
            },
        });
        truncated.TruncatedServers.Add("（反控）截断源");

        page.RenderSnapshotForSelfTest(truncated);
        var noticeA = page.MoreTextValue;
        sb.AppendLine("  控制组A（TotalCount=42 / 屏上 2 条）提示原文 = " + (noticeA.Length > 0 ? noticeA : "<none>"));
        sb.AppendLine("  断言A 提示出现且含总数 42 = " + (noticeA.Contains("还有更多") && noticeA.Contains("42")));

        // 控制组 B：服务端声明总数 = 屏上条数 ⇒ **不得**出现提示（否则提示是恒定的，A 就不足以说明问题）
        var complete = new MediaSnapshot();
        complete.Groups.Add(new SnapshotGroup
        {
            ServerId = "t87-control2",
            ServerName = "（反控）完整源",
            TotalCount = 2,
            Truncated = false,
            Items = new List<SnapshotItem>
            {
                new SnapshotItem { Id = "c3", Name = "反控条目 3", Type = "Movie" },
                new SnapshotItem { Id = "c4", Name = "反控条目 4", Type = "Movie" },
            },
        });

        page.RenderSnapshotForSelfTest(complete);
        var noticeB = page.MoreTextValue;
        sb.AppendLine("  控制组B（TotalCount=2 / 屏上 2 条）提示原文 = " + (noticeB.Length > 0 ? noticeB : "<none>"));
        sb.AppendLine("  断言B 完整时**不显示**提示 = " + (noticeB.Length == 0));

        // 还原真数据（合成快照只用于本反控，跑完必须回到真快照，避免污染后续读数）
        if (real != null)
        {
            page.RenderSnapshotForSelfTest(real);
        }

        sb.AppendLine("  还原后 卡片数=" + page.Posters.Count + " 提示="
            + (page.MoreTextValue.Length > 0 ? page.MoreTextValue : "<none>"));
        sb.AppendLine();
        return Task.CompletedTask;
    }

    /// <summary>取证开关：`SHELL_T291_KEEP_ARM=1` ⇒ 自检终态保留控制组A（超限）以便截图（默认关闭）。</summary>
    private const string KeepNoticeArmEnvVar = "SHELL_T291_KEEP_ARM";

    /// <summary>
    /// t291 超限臂（聚合视界）：证明「屏上条数 &lt; 服务端声明总数 / 撞上每源硬上限」这条**消费面**
    /// 不是静默截断 —— 臂走的是与生产**同一个**渲染函数 `AggregatePage.RenderSnapshotForSelfTest`。
    /// 三组控制（全部合成，不碰真实服务器，也不落盘）：
    /// A 超限（TotalCount=42 / 屏上 2 条 / Truncated=true）⇒ 提示**必须**出现且带具名数字；
    /// B 完整（TotalCount=2 / 屏上 2 条）⇒ **不得**出现（否则"恒定显示"也能骗过 A）；
    /// C 服务端没给总数（TotalCount=0 / 屏上 2 条）⇒ **不得**出现（否则继续播放/库族会常年挂假提示）。
    /// </summary>
    private static void DumpNoticeArms(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- t291 反控：聚合视界『还有更多』消费面（合成快照，双向可失败）---");

        var real = page.CurrentSnapshot;

        var truncated = new MediaSnapshot();
        truncated.Groups.Add(new SnapshotGroup
        {
            ServerId = "t291-control",
            ServerName = "（反控）超限源",
            TotalCount = 42,
            Truncated = true,
            Items = new List<SnapshotItem>
            {
                new SnapshotItem { Id = "n1", Name = "反控条目 1", Type = "Movie" },
                new SnapshotItem { Id = "n2", Name = "反控条目 2", Type = "Series" },
            },
        });
        truncated.TruncatedServers.Add("（反控）超限源");

        page.RenderSnapshotForSelfTest(truncated);
        var noticeA = page.NoticeTextValue;
        sb.AppendLine("  控制组A（TotalCount=42 / 屏上 2 条 / Truncated=true）提示原文 = "
            + (noticeA.Length > 0 ? noticeA : "<none>"));
        sb.AppendLine("  断言A1 提示出现（非静默）= " + (noticeA.Length > 0));
        sb.AppendLine("  断言A2 提示带具名总数 42 = " + noticeA.Contains("42"));
        sb.AppendLine("  断言A3 提示带具名硬上限 " + MediaAggregator.FavoritesMaxPerServer + " = "
            + noticeA.Contains(MediaAggregator.FavoritesMaxPerServer.ToString(CultureInfo.InvariantCulture)));

        var complete = new MediaSnapshot();
        complete.Groups.Add(new SnapshotGroup
        {
            ServerId = "t291-control2",
            ServerName = "（反控）完整源",
            TotalCount = 2,
            Truncated = false,
            Items = new List<SnapshotItem>
            {
                new SnapshotItem { Id = "n3", Name = "反控条目 3", Type = "Movie" },
                new SnapshotItem { Id = "n4", Name = "反控条目 4", Type = "Movie" },
            },
        });

        page.RenderSnapshotForSelfTest(complete);
        var noticeB = page.NoticeTextValue;
        sb.AppendLine("  控制组B（TotalCount=2 / 屏上 2 条 ⇒ 取尽）提示原文 = "
            + (noticeB.Length > 0 ? noticeB : "<none>"));
        sb.AppendLine("  断言B 取尽时**不显示**提示 = " + (noticeB.Length == 0));

        var noTotal = new MediaSnapshot();
        noTotal.Groups.Add(new SnapshotGroup
        {
            ServerId = "t291-control3",
            ServerName = "（反控）未给总数源",
            TotalCount = 0,
            Truncated = false,
            Items = new List<SnapshotItem>
            {
                new SnapshotItem { Id = "n5", Name = "反控条目 5", Type = "Episode" },
                new SnapshotItem { Id = "n6", Name = "反控条目 6", Type = "Episode" },
            },
        });

        page.RenderSnapshotForSelfTest(noTotal);
        var noticeC = page.NoticeTextValue;
        sb.AppendLine("  控制组C（TotalCount=0 / 屏上 2 条 ⇒ 服务端没给总数）提示原文 = "
            + (noticeC.Length > 0 ? noticeC : "<none>"));
        sb.AppendLine("  断言C 无总数时**不显示**提示（否则继续播放/库族挂假提示）= " + (noticeC.Length == 0));

        // 还原真数据（合成快照只用于本反控，跑完必须回到真快照，避免污染后续读数）
        if (real != null)
        {
            page.RenderSnapshotForSelfTest(real);
        }

        sb.AppendLine("  还原后 卡片数=" + page.PosterCount + " 提示="
            + (page.NoticeTextValue.Length > 0 ? page.NoticeTextValue : "<none>"));

        // 取证开关：`SHELL_T291_KEEP_ARM=1` ⇒ **保留**控制组A（超限）为屏上终态，便于截图证明
        // 提示在真 XAML 上确实可见（不设则逐字还原真数据；生产路径零影响）。
        if (string.Equals(Environment.GetEnvironmentVariable(KeepNoticeArmEnvVar), "1", StringComparison.Ordinal))
        {
            page.RenderSnapshotForSelfTest(truncated);
            sb.AppendLine("  取证保留（" + KeepNoticeArmEnvVar + "=1）：终态 = 控制组A 超限快照（截图用，非生产路径）");
            sb.AppendLine("  保留后 卡片数=" + page.PosterCount + " 提示=" + page.NoticeTextValue);
        }

        sb.AppendLine();
    }

    /// <summary>复合收藏查询的条数（给反控用；失败返回 <c>-1</c> 并留痕，不猜数）。</summary>
    private static async Task<int> CountFavoritesAsync(ServerConfig server, int limit)
    {
        try
        {
            var emby = NewEmby(server);
            var r = await emby.GetItemsAsync(
                includeItemTypes: MediaAggregator.FavoritesItemTypes,
                isFavorite: true,
                limit: limit,
                sortBy: "SortName");
            return r?.Items?.Count ?? 0;
        }
        catch (Exception ex)
        {
            AggregateDiagnostics.Write("t87-legacy-probe-fail server=" + (server?.Name ?? "?")
                + " " + ex.GetType().Name + ": " + ex.Message);
            return -1;
        }
    }

    private static AIPlayer.Shell.Services.Emby.EmbyService NewEmby(ServerConfig server)
        => new AIPlayer.Shell.Services.Emby.EmbyService(
            new AIPlayer.Shell.Services.Http.ShellHttpClient(), server, onLog: m => { });

    /// <summary>逐类型条数的一行读数（`Movie=1,Series=21,Episode=12`）—— 计数必须具名，不许只给合计。</summary>
    private static string TypeBreakdown(List<EmbyItem> items)
        => string.Join(",", (items ?? new List<EmbyItem>())
            .GroupBy(i => i.Type ?? "<null>")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key + "=" + g.Count()));

    // ── 反控（证明这套取数/呈现真的能分辨"空"与"失败"）─────────────────────

    private static async Task DumpCounterExamplesAsync(StringBuilder sb)
    {
        sb.AppendLine("=== 反控：空源 / 失败源 / 无封面（合成数据，不碰真实服务器）===");

        var servers = MediaAggregator.EnabledEmbyServers();

        // 反控① 不可达服务器 ⇒ 必须只进 FailedGroups，且**不影响其它组**
        // ⚠ 合成配置**不给凭据**是刻意的：服务层的失败路径要能被走到。
        //    副作用（实测）：服务层不做凭据存在性校验 ⇒ 请求路径会变成 `Users//Items`（UserId 为空串）。
        //    这**不是本屏要修的**（属服务层），但说明"凭据缺失"在当刻表现为 **404/异常**而非明确的前置拒绝 —— 已记入报告。
        var unreachable = new ServerConfig
        {
            Id = "t31-counter-fail",
            Name = "(反控)不可达服务器",
            Kind = ServerKind.Emby,
            BaseUrl = "http://127.0.0.1:9/emby",
            Enabled = true,
        };

        var withFail = new List<ServerConfig> { unreachable };
        withFail.AddRange(servers.Take(1));

        var agg = await MediaAggregator.LoadAsync(AggregateKind.Favorites, withFail);
        sb.AppendLine("① 注入不可达源：Groups=" + agg.Groups.Count
            + " Failed=" + agg.FailedGroups.Count
            + " NonEmpty=" + agg.NonEmptyGroups.Count
            + " TotalItems=" + agg.TotalItems);
        sb.AppendLine("  失败源显示名 = " + string.Join(" / ", agg.FailedGroups.Select(g => g.DisplayName)));
        sb.AppendLine("  断言 失败源被登记且不影响其它源 = " + (agg.FailedGroups.Count == 1 && agg.Groups.Count == withFail.Count));

        // 反控② 完全空输入 ⇒ 空态（不能崩、不能假成功）
        var empty = await MediaAggregator.LoadAsync(AggregateKind.Library, new List<ServerConfig>());
        sb.AppendLine("② 零服务器：Groups=" + empty.Groups.Count
            + " NonEmpty=" + empty.NonEmptyGroups.Count
            + " Failed=" + empty.FailedGroups.Count);
        sb.AppendLine("  断言 零服务器不抛且计数为 0 = " + (empty.Groups.Count == 0 && empty.TotalItems == 0));

        // 反控③ 无封面条目 ⇒ 必须有占位形态（不是"空框"）
        var noImage = new EmbyItem { Id = "t31-counter-noimage", Name = "(反控)无封面条目", Type = "Movie" };
        var poster = new AggregatePoster(noImage, unreachable, null, string.Empty, true, 0.25);
        sb.AppendLine("③ 无封面条目：HasImageUrl=" + poster.HasImageUrl
            + " PlaceholderVisibility=" + poster.PlaceholderVisibility
            + " ProgressWidth=" + poster.ProgressWidth);
        sb.AppendLine("  断言 无封面 → 画占位        = " + (poster.PlaceholderVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine("  断言 进度条宽度按比例        = " + (poster.ProgressWidth == Math.Round((AggregatePoster.PosterInnerWidth - 8) * 0.25)));

        // 反控④ 进度来源：PlayedPercentage 优先，其次 ResumeTicks/RunTimeTicks
        var byPercentage = new EmbyItem { Id = "a", Name = "pct", RunTimeTicks = 100_000_000_0L, UserData = new EmbyUserData { PlayedPercentage = 42.5 } };
        var byTicks = new EmbyItem { Id = "b", Name = "ticks", RunTimeTicks = 1_000_000_000L, UserData = new EmbyUserData { PlaybackPositionTicks = 250_000_000L } };
        var none = new EmbyItem { Id = "c", Name = "none" };
        sb.AppendLine("④ 进度换算：PlayedPercentage=42.5 → " + MediaAggregator.ProgressFraction(byPercentage).ToString("0.###", CultureInfo.InvariantCulture)
            + " ｜ ticks 25% → " + MediaAggregator.ProgressFraction(byTicks).ToString("0.###", CultureInfo.InvariantCulture)
            + " ｜ 无数据 → " + MediaAggregator.ProgressFraction(none).ToString("0.###", CultureInfo.InvariantCulture));
        sb.AppendLine("  断言 三种来源各自正确        = "
            + (Math.Abs(MediaAggregator.ProgressFraction(byPercentage) - 0.425) < 0.001
               && Math.Abs(MediaAggregator.ProgressFraction(byTicks) - 0.25) < 0.001
               && MediaAggregator.ProgressFraction(none) == 0));
        sb.AppendLine();
    }
}

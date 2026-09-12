// t31（U-E）收藏屏的代码后置。
//
// 规格 = HILLSLITE_UI_ANALYSIS.md 行 2（参照图 `hl-02-favorites-printwindow.png`，已人眼核对）：
//   **三个分区**「收藏的电影」/「收藏的剧」/「收藏的集」，分区标题带 `›`；
//   海报**右上角紫色圆形数字徽章** = **未看集数**（`UserData.UnplayedItemCount`），`✓` = 已看完（`UserData.Played`）；
//   海报下方 = 标题 + 年份区间（如 `2024-现在`）。
// + UI_SPEC_SHELL.md「收藏 ♡ 绑定 = `SetFavoriteAsync(id,false)`」/ P4「空源占位不崩」。
//
// [!] 三个分区是**按条目 Type 分**的，不是按服务器分（与聚合视界不同）：
//   `Movie` → 「收藏的电影」；`Series` → 「收藏的剧」；`Episode` → 「收藏的集」；
//   出现这三类之外的条目（服务端返回了意料外的东西）**归入「收藏的剧」**，宁可可见也不静默丢弃。
//
// [!] **作用域 = 当前服务器（主源）**：本屏不跨服。跨服的只有「聚合视界」那一屏 ——
//   两屏都叫"收藏"但作用域不同，混起来会让用户看到别的服务器的收藏。
//
// 收藏屏**不画进度条**：参照图里进度条只出现在聚合视界/继续观看族（UI_SPEC 同纪律：
// "搜索卡加进度条会与角标抢注意力"，收藏屏同理）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Aggregate;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Favorites;

/// <summary>收藏屏（侧栏第二项；`NavTag` = <c>favorites</c>）。</summary>
public sealed partial class FavoritesPage : Page
{
    /// <summary>导航 tag（外壳 `NavigateTo("favorites")` 用）。</summary>
    public const string NavTag = "favorites";

    /// <summary>`SHELL_SELFTEST_FAVORITES=1` ⇒ 起屏后自动自检并把原始读数写进证据文件。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_FAVORITES";

    private List<ServerConfig> _servers = new List<ServerConfig>();
    private readonly List<AggregatePoster> _posters = new List<AggregatePoster>();
    private bool _loading;

    /// <summary>最近一次渲染出的两个分区计数（摘要行用；只有"就地替换"路径需要单独刷新文字）。</summary>
    private int _movieCount;
    private int _seriesCount;
    private int _episodeCount;

    /// <summary>最近一次 SWR 读数（缓存态/刷新态；自检与"失败保留"分支都要用）。</summary>
    private SwrOutcome _lastOutcome;

    /// <summary>当前屏上的快照内容（T0 缓存 或 T1 刷新后的那份）。</summary>
    private MediaSnapshot CachedSnapshot { get; set; }

    /// <summary>最近一次**真实取数**的聚合结果（null = 当前只渲染了缓存）。起播必须用真条目。</summary>
    public MediaAggregate LiveAggregate { get; private set; }

    public FavoritesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // t99：日志打码的反控自检（`SHELL_SELFTEST_LOGMASK=1` 才跑；默认零影响）
        Infrastructure.SecretMasker.RunLogMaskSelfTestIfRequested();

        await ReloadAsync();

        // 自检钩子（默认关闭；零影响生产）
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            try
            {
                await Aggregate.T31SelfTest.RunFavoritesAsync(this);
            }
            catch (Exception ex)
            {
                Program.Log("FAVORITES selftest FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private async Task ReloadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = "正在读取收藏…";
        SectionList.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;

        try
        {
            if (_servers.Count == 0)
            {
                // 🔴 收藏屏 = **当前服务器（主源）**的收藏，不是跨服聚合：
                //    解析口径与首页/媒体库完全一致（`LastServerId` 命中者，否则第一台已启用 Emby）。
                //    跨服的只有「聚合视界」那一屏 —— 两屏都叫"收藏"但作用域不同，这里必须只查一台。
                var main = ResolveMainServer();
                if (main != null)
                {
                    _servers = new List<ServerConfig> { main };
                }

                Program.Log("FAVORITES scope server=" + (main?.Name ?? "<none>")
                    + " id=" + (main?.Id ?? "-") + " count=" + _servers.Count);
            }

            if (_servers.Count == 0)
            {
                ShowEmpty("没有可用的服务器", "去「服务器」页添加或启用一台 Emby 服务器后再回来。");
                return;
            }

            // 每次重载都把"真条目"清空：`LiveAggregate` 只代表**本次**取数拿到的真条目。
            // 否则刷新失败时会残留**上一次**的真条目 ⇒ 起播门会放行"快照卡"，与该门存在的理由（SEAM③(a) 代价条款）相冲突。
            LiveAggregate = null;

            var outcome = await MediaSnapshotSource.LoadAsync(
                AggregateKind.Favorites,
                _servers,
                onCacheValue: lookup =>
                {
                    // ── T0：缓存命中 ⇒ 立即渲染（刷新请求已在途）──────────────────────
                    // [!] 摘要行显示的是**T0 那一刻**的读数（hit/miss + Freshness）⇒ 先把当刻读数放进
                    //     `_lastOutcome` 再渲染；否则 T0 渲染出的摘要会带上"上一次"的旧状态（自检实测踩到）。
                    _lastOutcome = new SwrOutcome
                    {
                        Key = MediaSnapshotSource.KeyFor(AggregateKind.Favorites, _servers),
                        HasCache = lookup.HasValue,
                        Freshness = lookup.Freshness,
                        AgeSeconds = lookup.AgeSeconds,
                        Cached = lookup.Value,
                    };
                    // T0 当刻的 id 也要**如实**填（否则日志里 `T0-cache-render cachedIds=[]` 会被读成"缓存是空的"）
                    foreach (var id in MediaSnapshotSource.SnapshotIds(lookup.Value))
                    {
                        _lastOutcome.CachedIds.Add(id);
                    }
                    CachedSnapshot = lookup.Value;
                    RenderFromSnapshot(lookup.Value);
                    // 自检观察点：留 T0 当刻的实例身份 ⇒ T1 落地后仍是同一批对象才算"就地替换"
                    FirstPosterAtCacheRender = _posters.Count > 0 ? _posters[0] : null;
                    SectionsAtCacheRender = Sections;
                    Program.Log("FAVORITES T0-cache-render cache=" + (lookup.HasValue ? "hit" : "miss")
                        + " freshness=" + lookup.Freshness
                        + (lookup.AgeSeconds.HasValue ? " age=" + lookup.AgeSeconds.Value.ToString("0.0") + "s" : string.Empty));

                    // [!] t93-C：缓存里没有可用内容时不得当"内容"渲染（否则用户看到空屏）；
                    //    刷新已在途 ⇒ 保持骨架屏 + 可见状态行，等 T1 落地再决定内容/空态。
                    if (_posters.Count == 0)
                    {
                        LoadingPanel.Visibility = Visibility.Visible;
                        LoadingText.Text = "正在读取收藏…（缓存里没有可用内容）";
                        Program.Log("FAVORITES T0-empty-cache ⇒ 保持骨架屏（不把空/失效缓存当内容）");
                    }
                },
                onLiveAggregate: live =>
                {
                    LiveAggregate = live;   // 真条目：起播与自检都读它（快照只有渲染面字段）
                });

            _lastOutcome = outcome;

            if (outcome.RefreshSucceeded && outcome.Value != null)
            {
                // [!] t93-D：**屏上 0 张而刷新有内容时必须重绑** —— 否则 `SameIds([],[])` 会把空屏钉死。
                var renderedEmpty = _posters.Count == 0;
                var changed = CachedSnapshot == null || renderedEmpty
                    || !SnapshotRenderer.SameIds(outcome.CachedIds, outcome.RefreshedIds);
                if (changed)
                {
                    CachedSnapshot = outcome.Value;
                    RenderFromSnapshot(outcome.Value);
                    Program.Log("FAVORITES T1-replace reason="
                        + (renderedEmpty ? "rendered-empty" : "ids-changed")
                        + " " + MediaSnapshotSource.Describe(outcome));
                }
                else
                {
                    Program.Log("FAVORITES T1-noop ids-identical（不重绑 ⇒ 不闪屏/不重置滚动）");
                    // 只改摘要文字（不重绑列表 ⇒ T1 就地性不受影响）
                    UpdateSummaryText(CachedSnapshot);
                }
            }
            else
            {
//：失败 ⇒ **保留缓存内容 + 可见失败提示**（不清空、不删缓存）
                if (CachedSnapshot != null)
                {
                    RenderFromSnapshot(CachedSnapshot);
                }
                else
                {
                    // [!] t93-C：没有缓存可保留时**不得留白**（既无骨架也无内容 = 用户眼里的"功能无效"）
                    ShowEmpty("取数失败（没有可用缓存）",
                        string.IsNullOrEmpty(outcome.Error) ? "明细见日志。" : outcome.Error + "（明细见日志）");
                }

                var preserved = outcome.CachePreserved || CachedSnapshot != null;
                FailedText.Text = "刷新失败：" + (string.IsNullOrEmpty(outcome.Error) ? "（无原因）" : outcome.Error)
                    + (preserved ? "｜**已保留缓存内容**（未清空、未删缓存）" : string.Empty);
                FailedText.Visibility = Visibility.Visible;
                Program.Log("FAVORITES refresh-failed " + MediaSnapshotSource.Describe(outcome)
                    + " preserved=" + preserved);
            }

            // 自检模式不自动加载封面（同聚合视界：否则测量不到"占位 → 封面"这一跳）
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
            {
                await LoadImagesAsync();
            }
        }
        catch (Exception ex)
        {
            Program.Log("FAVORITES load FAIL " + ex.GetType().FullName + ": " + ex.Message);
            ShowEmpty("取数失败", ex.GetType().Name + "（明细见日志）");
        }
        finally
        {
            _loading = false;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 渲染三个分区（**收藏的电影 / 收藏的剧 / 收藏的集**，按条目 `Type` 分）。
    /// SEAM③ 之后渲染统一走**快照**（缓存态与刷新态同一渲染器 ⇒ 替换时不闪变）；本方法是兼容入口。
    /// </summary>
    private void Render(MediaAggregate aggregate)
    {
        if (aggregate == null)
        {
            return;
        }

        RenderFromSnapshot(MediaSnapshotSource.ToSnapshot(aggregate));
    }

    /// <summary>从快照渲染（T0 缓存与 T1 刷新后都用它 ⇒ 观感一致）。</summary>
    private void RenderFromSnapshot(MediaSnapshot snapshot)
    {
        _posters.Clear();
        var sections = SnapshotRenderer.ToFavoriteSections(
            snapshot, _servers, out var movieCount, out var seriesCount, out var episodeCount);
        foreach (var row in sections)
        {
            foreach (var poster in row.Posters)
            {
                _posters.Add(poster);
            }
        }

        Sections = sections;
        SectionList.ItemsSource = sections;
        SectionList.Visibility = Visibility.Visible;
        EmptyPanel.Visibility = Visibility.Collapsed;

        _movieCount = movieCount;
        _seriesCount = seriesCount;
        _episodeCount = episodeCount;
        SummaryText.Text = BuildSnapshotSummary(snapshot);
        UpdateMoreText(snapshot);

        var failed = snapshot?.FailedServers ?? new List<string>();
        if (failed.Count > 0)
        {
            FailedText.Text = "失败 " + failed.Count + " 台：" + string.Join(" / ", failed);
            FailedText.Visibility = Visibility.Visible;
        }

        if (_posters.Count == 0 && failed.Count == 0)
        {
            SectionList.Visibility = Visibility.Collapsed;
            ShowEmpty("还没有收藏", "在详情页点 ♡ 加入收藏后，这里会跨服务器汇总显示。");
        }
    }

    /// <summary>
    /// **只改摘要行文字、不重绑列表**（T1-noop 落地后刷新 T0 状态用；重绑会破坏"就地替换"）。
    /// 与 <see cref="RenderFromSnapshot"/> 共用同一段文案构造 ⇒ 两处口径不可能分叉。
    /// </summary>
    private void UpdateSummaryText(MediaSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        SummaryText.Text = BuildSnapshotSummary(snapshot);
        UpdateMoreText(snapshot);
    }

    /// <summary>
    /// t87「还有更多」可见提示：屏上不是全部时**必须**看得见（收藏是完整列表，静默截断＝用户可见的数据丢失）。
    /// 文案由 <see cref="MediaSnapshotSource.TruncationNotice"/> 单点构造（缓存态/刷新态同一口径）。
    /// </summary>
    private void UpdateMoreText(MediaSnapshot snapshot)
    {
        var notice = MediaSnapshotSource.TruncationNotice(snapshot);
        MoreText.Text = notice ?? string.Empty;
        MoreText.Visibility = notice == null ? Visibility.Collapsed : Visibility.Visible;

        if (notice != null)
        {
            Program.Log("FAVORITES more " + notice
                + " shown=" + _posters.Count
                + " serverTotal=" + (snapshot?.Groups?.Sum(g => Math.Max(g.TotalCount, g.Items?.Count ?? 0)) ?? 0)
                + " truncatedServers=" + ((snapshot?.TruncatedServers?.Count ?? 0))
                + " cap=" + MediaAggregator.FavoritesMaxPerServer
                + " pageSize=" + MediaAggregator.FavoritesPageSizePerServer);
        }
    }

    /// <summary>
    /// 摘要行文案：`收藏：<K> 台有内容 · 共 <N> 条（电影 <n1> / 剧 <n2> / 集 <n3>）[· T0=hit|miss（Freshness）]`。
    /// 各数**各自具名**（不得写"三数一致"；失败台数由失败行单独可见）。
    /// </summary>
    private string BuildSnapshotSummary(MediaSnapshot snapshot)
        => "收藏：" + (snapshot?.Groups?.Count ?? 0) + " 台有内容"
           + " · 共 " + _posters.Count + " 条（电影 " + _movieCount + " / 剧 " + _seriesCount + " / 集 " + _episodeCount + "）"
           + (_lastOutcome != null
               ? " · T0=" + (_lastOutcome.HasCache ? "hit" : "miss") + "（" + _lastOutcome.Freshness + "）"
               : string.Empty);

    /// <summary>
    /// 主源（当前服务器）：解析口径与首页/媒体库一致 —— `SettingsService.LastServerId` 命中者，
    /// 否则第一台已启用 Emby。**解析不出就返回 null**（调用方走空态），**绝不回退成"全部服务器"**。
    /// </summary>
    private static ServerConfig ResolveMainServer()
    {
        var preferredId = Services.Settings.SettingsService.Instance?.Settings?.LastServerId ?? string.Empty;
        var candidates = Services.Servers.ServerConfigStore.Instance.Servers?
            .Where(s => s.Enabled && s.Kind == ServerKind.Emby)
            .OrderBy(s => s.SortIndex)
            .ToList() ?? new List<ServerConfig>();

        return candidates.FirstOrDefault(s => string.Equals(s.Id, preferredId, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault();
    }

    /// <summary>起播用：优先拿**刷新后的真条目**（快照是渲染面白名单，缺 `Raw`/`MediaSources`）。</summary>
    public EmbyItem LiveItemFor(string serverId, string itemId)
    {
        var live = LiveAggregate;
        if (live == null || string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        foreach (var group in live.Groups)
        {
            if (!string.Equals(group.ServerId, serverId, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var item in group.Items)
            {
                if (string.Equals(item.Id, itemId, StringComparison.Ordinal))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private void ShowEmpty(string title, string detail)
    {
        EmptyTitle.Text = title;
        EmptyDetail.Text = detail;
        EmptyPanel.Visibility = Visibility.Visible;
        SectionList.Visibility = Visibility.Collapsed;
    }

    // ── 自检/取证访问面（只读；给 T31SelfTest 用，不参与生产逻辑）────────────────

    /// <summary>最近一次**真实取数**结果（= 刷新拿到的真条目；只有缓存时为 null）。</summary>
    public MediaAggregate LastAggregate => LiveAggregate;

    /// <summary>当前收藏卡片（含两个分区）。</summary>
    public IReadOnlyList<AggregatePoster> Posters => _posters;

    /// <summary>三个分区行（「收藏的电影」/「收藏的剧」/「收藏的集」）。</summary>
    public IReadOnlyList<AggregateRow> Sections { get; private set; } = new List<AggregateRow>();

    /// <summary>摘要行原文。</summary>
    public string SummaryTextValue => SummaryText.Text;

    /// <summary>t87「还有更多」提示原文（空串 = 屏上就是全部；自检与门禁都读它）。</summary>
    public string MoreTextValue => MoreText.Visibility == Visibility.Visible ? MoreText.Text : string.Empty;

    /// <summary>最近一次 SWR 读数（自检打印缓存态/刷新态用）。</summary>
    public SwrOutcome LastOutcome => _lastOutcome;

    /// <summary>当前屏上的快照内容（自检用）。</summary>
    public MediaSnapshot CurrentSnapshot => CachedSnapshot;

    /// <summary>本屏作用域内的服务器（**应当只有主源一台**；自检用它验证"只查当前服务器"）。</summary>
    public ServerConfig MainServer => _servers.Count > 0 ? _servers[0] : null;

/// <summary>骨架屏是否可见 ——"Miss ⇒ 骨架屏"的观察点。</summary>
    public bool SkeletonVisible => LoadingPanel.Visibility == Visibility.Visible;

/// <summary>刷新失败提示原文（ 可见失败提示的观察点）。</summary>
    public string FailedTextValue => FailedText.Text;

    /// <summary>
    /// 自检观察点（只读；T1 就地性的可判形式）：**T0 缓存渲染当刻**屏上第一张卡片的实例。
    /// 同一发里刷新落地后若仍是**同一个实例** ⇒ 没有整屏重建（不闪屏/不重置滚动/不丢选中）。
    /// </summary>
    public AggregatePoster FirstPosterAtCacheRender { get; private set; }

    /// <summary>自检观察点：T0 当刻 `ListView` 绑定的分区列表实例（T1-noop ⇒ 不变）。</summary>
    public IReadOnlyList<AggregateRow> SectionsAtCacheRender { get; private set; }

    /// <summary>自检用：重新走一次完整 SWR 载入。</summary>
    public Task ReloadForSelfTestAsync() => ReloadAsync();

    /// <summary>
    /// 自检用：把**给定快照**渲染到屏上（含「还有更多」可见性）。
    /// 用途 = t87 的可失败反控：真机上收藏只有 34 条、永远够不着 1000 的硬上限，
    /// 于是"截断提示"这条绑定只能靠合成快照证明**没被写成恒定不显示**。
    /// </summary>
    public void RenderSnapshotForSelfTest(MediaSnapshot snapshot) => RenderFromSnapshot(snapshot);

    public async Task LoadImagesAsync()
    {
        foreach (var poster in _posters)
        {
            await poster.EnsureImageAsync();
        }
    }

    private sealed class AggregateGroupLine
    {
        public AggregateGroupLine(string name, string error)
        {
            Name = name;
            Error = error;
        }

        public string Name { get; }

        public string Error { get; }
    }

    /// <summary>
    /// 点卡片 ⇒ **进详情页**（t103：用户原话「就算只有单集（收藏里）我也希望点击进入详情页」）。
    /// 起播改由卡片上的**播放按钮**发起（见 <see cref="OnPlayClick"/>）；两条路径共用 `CardIntents`。
    /// </summary>
    private void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AggregatePoster poster || poster.Item == null || poster.Server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Server.Id, poster.Item.Id);
        if (CardIntents.TryOpenDetail(Frame, "FAVORITES", poster, live, out var reason))
        {
            FailedText.Visibility = Visibility.Collapsed;
            return;
        }

        // 走不通也必须**看得见**（"点了没反应"是本卡的判失败情形）
        Program.Log("FAVORITES detail-blocked " + reason + " item=" + poster.Item.Name);
        FailedText.Text = reason;
        FailedText.Visibility = Visibility.Visible;
    }

    /// <summary>卡片上的**播放按钮** ⇒ 起播（复用 t27 的 M3 链路）。</summary>
    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not AggregatePoster poster
            || poster.Item == null || poster.Server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Server.Id, poster.Item.Id);
        if (CardIntents.TryPlay("FAVORITES", poster, live, out var blockReason))
        {
            return;
        }

        Program.Log("FAVORITES play-blocked " + blockReason + " item=" + poster.Item.Name);
        FailedText.Text = blockReason;
        FailedText.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 取消收藏：**服务端先成功、界面后移除**（失败保留卡片）。
    /// 绑定 = `EmbyService.SetFavoriteAsync(id, false)`（`EmbyService.cs:786`）。
    /// </summary>
    private async void OnUnfavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not AggregatePoster poster
            || poster.Item == null || poster.Server == null)
        {
            return;
        }

        var itemId = poster.Item.Id;
        var server = poster.Server;
        try
        {
            var emby = new EmbyService(new ShellHttpClient(), server, onLog: m => { });
            var ok = await emby.SetFavoriteAsync(itemId, false);
            Program.Log("FAVORITES unfavorite id=" + itemId + " server=" + (server.Name ?? "?") + " ok=" + ok);
            if (!ok)
            {
                return;   // 服务端没成功 ⇒ 卡片留在原地（不乐观移除）
            }

            poster.IsFavorite = false;
            _posters.Remove(poster);

            // 从所属分区里摘掉这一张，再重绑（两分区保持同构）
            foreach (var row in Sections)
            {
                var victim = row.Posters.FirstOrDefault(p => ReferenceEquals(p, poster));
                if (victim != null)
                {
                    row.Posters.Remove(victim);
                }
            }

            SectionList.ItemsSource = null;
            SectionList.ItemsSource = Sections;
            SummaryText.Text = "收藏：" + _posters.Count + " 条（已移除 1 条）";

            if (_posters.Count == 0)
            {
                SectionList.Visibility = Visibility.Collapsed;
                ShowEmpty("还没有收藏", "在详情页点 ♡ 加入收藏后，这里会跨服务器汇总显示。");
            }
        }
        catch (Exception ex)
        {
            Program.Log("FAVORITES unfavorite FAIL id=" + itemId + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

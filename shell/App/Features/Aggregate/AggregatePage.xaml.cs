// t31（U-E）聚合视界屏的代码后置。
//
// 规格 = UI_SPEC_SHELL.md P4「收藏 / 聚合视界：跨服分组 + 进度条；空源显示占位图不崩」
// + HILLSLITE_UI_ANALYSIS.md 行 3 / 行 5（三过滤器 + 按服务器分组 + 每张海报一条进度条）。
//
// 三条实现纪律（都有依据，不是自选）：
//   ① **不做逐源增量刷新**：本屏每台服务器一次取数、`Task.WhenAll` 汇总后一次性出结果
// （依据 = 第 1 行：服务层聚合是 `WhenAll`，UI 拿不到增量；本屏同样不为它编造"逐源出现"的动画）。
//   ② **空源保留分组**：失败源用红字、成功但无条目用灰字，**两行都留在页面上**。
//   ③ **封面异步 + 占位兜底**：封面 URL 带凭据，失败只记类型名；卡片保持 `#333333` + 胶片图标。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Library;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>聚合视界（侧栏第三项；`NavTag` = <c>aggregate</c>）。</summary>
public sealed partial class AggregatePage : Page
{
    /// <summary>导航 tag（外壳 `NavigateTo("aggregate")` 用）。</summary>
    public const string NavTag = "aggregate";

    /// <summary>`SHELL_SELFTEST_AGGREGATE=1` ⇒ 起屏后自动跑三过滤器并把原始读数写进证据文件。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_AGGREGATE";

    private AggregateKind _kind = AggregateKind.ContinueWatching;
    private List<ServerConfig> _servers = new List<ServerConfig>();
    private MediaAggregate _last;
    private bool _loading;

    /// <summary>最近一次 SWR 读数（缓存态/刷新态；自检与"失败保留"分支都要用）。</summary>
    private SwrOutcome _lastOutcome;

    /// <summary>当前屏上的快照内容（T0 缓存 或 T1 刷新后的那份）。</summary>
    private MediaSnapshot CachedSnapshot { get; set; }

    /// <summary>
    /// 最近一次**真实取数**的聚合结果（null = 本屏当前只渲染了缓存）。
    /// ⚠️ 起播必须用**真条目**（快照是白名单 DTO，缺 `Raw`/`MediaSources`）⇒ 见 `LiveItemFor`。
    /// </summary>
    public MediaAggregate LiveAggregate { get; private set; }

    public AggregatePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateFilterVisuals();
        await ReloadAsync();

        // 自检钩子（默认关闭；零影响生产）：跑满三个过滤器 + 四组反控，写证据文件
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            try
            {
                await T31SelfTest.RunAggregateAsync(this);
            }
            catch (Exception ex)
            {
                Program.Log("AGGREGATE selftest FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<AggregateKind>(tag, out var kind) || kind == _kind)
        {
            return;
        }

        _kind = kind;
        UpdateFilterVisuals();
        _ = ReloadAsync();
    }

    private void UpdateFilterVisuals()
    {
        var buttons = new (Button Button, AggregateKind Kind)[]
        {
            (ContinueFilterButton, AggregateKind.ContinueWatching),
            (FavoritesFilterButton, AggregateKind.Favorites),
            (LibraryFilterButton, AggregateKind.Library),
        };

        foreach (var (button, kind) in buttons)
        {
            var active = kind == _kind;
            button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                active ? "AccentBrush" : "ControlBgBrush"];
            button.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"];
        }
    }

    private async Task ReloadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
// 骨架屏（：**Miss ⇒ 骨架**；缓存命中 ⇒ 立即渲染、不闪骨架）
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = "正在跨服务器取数…";
        GroupList.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        FailedText.Visibility = Visibility.Collapsed;

        try
        {
            if (_servers.Count == 0)
            {
                _servers = MediaAggregator.EnabledEmbyServers();
            }

            if (_servers.Count == 0)
            {
                ShowEmpty("没有启用的服务器", "去「服务器」页添加或启用一台 Emby 服务器后再回来。");
                return;
            }

            var showProgress = _kind != AggregateKind.Library;

            // 每次重载都把"真条目"清空：`LiveAggregate` 只代表**本次**取数拿到的真条目。
            // 🔴 必须在 `LoadAsync` **之前**清：`onLiveAggregate` 是在取数途中（LoadAsync 内部）回调的，
            //    之前写成"取完再清"就会把刚填进去的真条目抹掉 ⇒ 聚合视界的**播放按钮永远拿不到真条目**
            //    （t103 实测：断言⑤ 起播入口不可达 = False，根因就在这里）。
            LiveAggregate = null;

            var outcome = await MediaSnapshotSource.LoadAsync(
                _kind,
                _servers,
                onCacheValue: lookup =>
                {
                    // ── T0：同步命中缓存 ⇒ **立即渲染**（此刻刷新请求已在途）────────────
                    // [!] 摘要行要显示**T0 那一刻**的读数（hit/miss + Freshness）⇒ 先把当刻读数放进 `_lastOutcome`
                    //     再渲染（原实现先置 null，导致 T0 渲染出的摘要永远缺 T0 状态 —— 自检实测为 False）。
                    //     真 outcome 到达后只调 `UpdateSummaryText()` 改文字（**不重绑列表** ⇒ 不破坏 T1 就地性）。
                    _lastOutcome = new SwrOutcome
                    {
                        Key = MediaSnapshotSource.KeyFor(_kind, _servers),
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
                    RenderFromSnapshot(lookup.Value, showProgress);
                    // 自检观察点：留 T0 当刻的实例身份 ⇒ T1 落地后仍是同一批对象才算"就地替换"
                    FirstPosterAtCacheRender = Rows.SelectMany(r => r.Posters).FirstOrDefault();
                    RowsAtCacheRender = Rows;

                    // [!] t93-C：缓存里**没有可用内容**时不得把它当"内容"渲染（否则用户看到的是空屏），
                    //    但刷新已在途 ⇒ **保持骨架屏 + 可见状态行**，等 T1 落地再决定内容/空态。
                    //    ⚠ 判据必须是**海报张数**，不是行数：快照里"1 台 0 条"照样会生成一行（空行）。
                    if (PosterCount == 0)
                    {
                        LoadingPanel.Visibility = Visibility.Visible;
                        LoadingText.Text = "正在跨服务器取数…（缓存里没有可用内容）";
                        Program.Log("AGGREGATE T0-empty-cache ⇒ 保持骨架屏（不把空/失效缓存当内容）"
                            + " posters=0 " + MediaSnapshotSource.Describe(_lastOutcome));
                    }

                    Program.Log("AGGREGATE T0-cache-render " + MediaSnapshotSource.Describe(_lastOutcome));
                },
                onLiveAggregate: aggregate =>
                {
                    // 真条目回填：起播与自检都读它（快照只有渲染面字段）
                    LiveAggregate = aggregate;
                    _last = aggregate;
                });

            _lastOutcome = outcome;

            if (outcome.RefreshSucceeded && outcome.Value != null)
            {
                // ── T1：就地替换（id 序列相同 ⇒ 不重绑，保住滚动/选中；不同才换）──────────
                // [!] t93-D：**屏上 0 张而刷新有内容时必须重绑** —— 否则"空 ids vs 空 ids"的 SameIds 会把空屏钉死。
                var renderedEmpty = PosterCount == 0;
                var changed = renderedEmpty || !SnapshotRenderer.SameIds(outcome.CachedIds, outcome.RefreshedIds);
                if (changed || CachedSnapshot == null)
                {
                    // 先定 reason 再赋值（赋值后就"永远有快照"了 ⇒ no-t0 再也判不出来）
                    LastT1Reason = CachedSnapshot == null ? "no-t0" : (renderedEmpty ? "rendered-empty" : "ids-changed");
                    CachedSnapshot = outcome.Value;
                    RenderFromSnapshot(outcome.Value, showProgress);
                    Program.Log("AGGREGATE T1-replace reason=" + LastT1Reason
                        + " " + MediaSnapshotSource.Describe(outcome));
                }
                else
                {
                    LastT1Reason = "noop";
                    Program.Log("AGGREGATE T1-noop ids-identical（不重绑 ⇒ 不闪屏/不重置滚动）");
                }

                // 摘要行跟着**真 outcome** 走（只改文字、不重绑列表 ⇒ T1 就地性不受影响）
                UpdateSummaryText(CachedSnapshot);
                await MaybeLoadImagesAsync();
            }
            else
            {
// ── 失败：**保留缓存内容 + 可见失败提示**（绝不清空、绝不删缓存）─────
                var preserved = outcome.CachePreserved || CachedSnapshot != null;
                if (CachedSnapshot != null)
                {
                    RenderFromSnapshot(CachedSnapshot, showProgress);   // 重画一次，确保内容在
                }
                else
                {
                    // [!] t93-C：没有缓存可保留时**不得留白**（既无骨架也无内容 = 用户眼里的"功能无效"）
                    ShowEmpty("取数失败（没有可用缓存）",
                        string.IsNullOrEmpty(outcome.Error) ? "明细见日志。" : outcome.Error + "（明细见日志）");
                }

                ShowRefreshFailure(outcome.Error, preserved);
                Program.Log("AGGREGATE refresh-failed "
                    + MediaSnapshotSource.Describe(outcome)
                    + " preserved=" + preserved);
            }
        }
        catch (Exception ex)
        {
            Program.Log("AGGREGATE load FAIL " + ex.GetType().FullName + ": " + ex.Message);
            ShowEmpty("取数失败", ex.GetType().Name + "（明细见日志）");
        }
        finally
        {
            _loading = false;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>从快照渲染（缓存态与刷新态**同一渲染器** ⇒ 观感一致，替换时不闪变）。</summary>
    private void RenderFromSnapshot(MediaSnapshot snapshot, bool showProgress)
    {
        if (snapshot == null)
        {
            return;
        }

        var rows = SnapshotRenderer.ToGroupedRows(snapshot, _servers, showProgress);
        Rows = rows;
        GroupList.ItemsSource = rows;
        GroupList.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        // 提示描述的是**数据面**（屏上条数 vs 服务端总数 / 是否撞硬上限），与"渲染出几行"无关 ⇒
        // 必须在空行早退之前先算，否则"有更多但一行没渲染出来"会退化成静默。
        UpdateNoticeText(snapshot);

        if (rows.Count == 0)
        {
            ShowEmpty("没有可显示的内容", FilterName(_kind) + "：所有已启用服务器都返回了 0 条。");
            return;
        }

        SummaryText.Text = BuildSnapshotSummary(snapshot);

        var failed = snapshot.FailedServers ?? new List<string>();
        FailedText.Text = failed.Count == 0 ? string.Empty : "失败 " + failed.Count + " 台：" + string.Join(" / ", failed);
        FailedText.Visibility = failed.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 未取尽/撞硬上限的**可见**提示（t291）：文案单点 = <see cref="MediaSnapshotSource.TruncationNotice"/>
    /// —— 本屏**不另写第二套文案**。返回 null ⇒ 屏上就是全部（正常态不显示任何提示）。
    /// 触发面：① 屏上条数 &lt; 服务端声明总数（取数被限流/被硬上限挡住）② 该源撞上每源硬上限。
    /// </summary>
    private void UpdateNoticeText(MediaSnapshot snapshot)
    {
        var notice = MediaSnapshotSource.TruncationNotice(snapshot);
        NoticeText.Text = notice ?? string.Empty;
        NoticeText.Visibility = string.IsNullOrEmpty(notice) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// **只改摘要行文字、不重绑列表**（T1 落地后刷新 T0 状态用；重绑会破坏"就地替换"）。
    /// 与 <see cref="RenderFromSnapshot"/> 共用同一段文案构造 ⇒ 两处口径不可能分叉。
    /// </summary>
    private void UpdateSummaryText(MediaSnapshot snapshot)
    {
        if (snapshot == null || Rows.Count == 0)
        {
            return;
        }

        SummaryText.Text = BuildSnapshotSummary(snapshot);
        UpdateNoticeText(snapshot);
    }

    /// <summary>
    /// 摘要行文案（快照口径）：`<过滤器>：<K> 台有内容 · 共 <N> 条 [· T0=hit|miss（Freshness）]`。
/// 各数**各自具名**（ 末行红线：不得写"三数一致"；失败台数由 `<see cref="FailedText"/>` 单独可见）。
    /// </summary>
    private string BuildSnapshotSummary(MediaSnapshot snapshot)
        => FilterName(_kind)
           + "：" + snapshot.Groups.Count + " 台有内容"
           + " · 共 " + Rows.Sum(r => r.Posters.Count) + " 条"
           + (_lastOutcome != null
               ? " · T0=" + (_lastOutcome.HasCache ? "hit" : "miss") + "（" + _lastOutcome.Freshness + "）"
               : string.Empty);

/// <summary>：失败提示**可见**，但不覆盖/清空已有内容。</summary>
    private void ShowRefreshFailure(string error, bool cachePreserved)
    {
        var text = "刷新失败：" + (string.IsNullOrEmpty(error) ? "（无原因）" : error);
        if (cachePreserved)
        {
            text += "｜**已保留缓存内容**（未清空、未删缓存）";
        }

        FailedText.Text = text;
        FailedText.Visibility = Visibility.Visible;
    }

    private async Task MaybeLoadImagesAsync()
    {
        // ⚠ 自检模式下**不自动加载封面**：否则"占位 → 封面"这一跳在测量之前就发生了
        //    （实测踩到：加载前=加载后，`本轮新增=0` ⇒ 读数是测量伪影，不是能力）。
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            await LoadImagesAsync();
        }
    }

    private void Render(MediaAggregate aggregate)
    {
        // SEAM③ 之后**渲染只走快照路径**（缓存态与刷新态同一渲染器 ⇒ 替换时不闪变）。
        // 本方法保留为"真条目 → 快照 → 渲染"的兼容入口（自检与过渡期用），不再直接拼卡片。
        if (aggregate == null)
        {
            return;
        }

        RenderFromSnapshot(MediaSnapshotSource.ToSnapshot(aggregate), aggregate.Kind != AggregateKind.Library);
    }

    /// <summary>封面异步补齐（失败保持占位）。**渲染与图片分离**：自检要在"还没加载完图"的一刻看占位态。</summary>
    public async Task LoadImagesAsync()
    {
        foreach (var row in Rows)
        {
            foreach (var poster in row.Posters)
            {
                await poster.EnsureImageAsync();
            }
        }
    }

    // ── 自检/取证访问面（只读；给 T31SelfTest 用，不参与生产逻辑）────────────────

    /// <summary>最近一次渲染出的分组行。</summary>
    public IReadOnlyList<AggregateRow> Rows { get; private set; } = new List<AggregateRow>();

    /// <summary>屏上**海报张数**（t93：判"有没有内容"必须用它，不能用行数 —— 空组也会生成一行）。</summary>
    public int PosterCount => Rows.Sum(r => r.Posters.Count);

    /// <summary>最近一次取数结果（可能为 null = 还没取过）。</summary>
    public MediaAggregate LastAggregate => _last;

    /// <summary>当前过滤器。</summary>
    public AggregateKind CurrentKind => _kind;

    /// <summary>当前屏作用域内的服务器（t93 自检算作用域键用；生产逻辑不读它）。</summary>
    public List<ServerConfig> CurrentServers => _servers;

    /// <summary>供自检切换过滤器（**与用户点按钮走同一条路**：改状态 → 刷新视觉 → 重新取数）。</summary>
    public Task SelectFilterAsync(AggregateKind kind)
    {
        _kind = kind;
        UpdateFilterVisuals();
        return ReloadAsync();
    }

    /// <summary>摘要行原文（自检逐字核对"三个数各自指名"）。</summary>
    public string SummaryTextValue => SummaryText.Text;

    /// <summary>分组列表的可见性（空态断言用）。</summary>
    public Visibility GroupListVisibility => GroupList.Visibility;

    /// <summary>最近一次 SWR 读数（自检打印缓存态/刷新态用）。</summary>
    public SwrOutcome LastOutcome => _lastOutcome;

    /// <summary>当前屏上的快照内容（自检用）。</summary>
    public MediaSnapshot CurrentSnapshot => CachedSnapshot;

/// <summary>骨架屏（LoadingPanel）当前是否可见 ——"Miss ⇒ 骨架屏"的观察点。</summary>
    public bool SkeletonVisible => LoadingPanel.Visibility == Visibility.Visible;

/// <summary>刷新失败提示原文（ 可见失败提示的观察点）。</summary>
    public string FailedTextValue => FailedText.Text;

    /// <summary>
    /// 空态面板可见性（t93-C 的观察点）：**没有可用缓存又没有内容时不得留白** ——
    /// 用户眼里的"功能无效"就是"既没有骨架、也没有内容、也没有提示"。
    /// </summary>
    public bool EmptyPanelVisible => EmptyPanel.Visibility == Visibility.Visible;

    /// <summary>最近一次 T1 的落地方式（`no-t0` / `rendered-empty` / `ids-changed` / `noop`）—— t93-D 的观察点。</summary>
    public string LastT1Reason { get; private set; } = string.Empty;

    /// <summary>
    /// 自检观察点（只读；T1 就地性的可判形式）：**T0 缓存渲染当刻**屏上第一张卡片的实例。
    /// 同一发里刷新落地后若仍是**同一个实例** ⇒ 没有整屏重建（不闪屏/不重置滚动/不丢选中）。
    /// </summary>
    public AggregatePoster FirstPosterAtCacheRender { get; private set; }

    /// <summary>自检观察点：T0 当刻 `GroupList` 绑定的分组行实例（T1-noop ⇒ 不变）。</summary>
    public IReadOnlyList<AggregateRow> RowsAtCacheRender { get; private set; }

    /// <summary>自检用：重新走一次完整 SWR 载入（冷/热/坏源三种前提各调一次）。</summary>
    public Task ReloadForSelfTestAsync() => ReloadAsync();

    /// <summary>自检观察点：屏上「还有更多」提示的**原文**（空串 = 无提示 ⇒ 屏上就是全部）。
    /// 口径照抄收藏屏 `FavoritesPage.MoreTextValue`：**不可见就等同于没有提示**，避免"文本还在但元素已折叠"被读成有提示。</summary>
    public string NoticeTextValue => NoticeText.Visibility == Visibility.Visible ? (NoticeText.Text ?? string.Empty) : string.Empty;

    /// <summary>自检观察点：提示元素当刻是否可见（`Collapsed` 也算"没有提示"）。</summary>
    public bool NoticeVisible => NoticeText.Visibility == Visibility.Visible;

    /// <summary>自检用：把一份（可为合成的）快照走**与生产完全相同**的渲染路径 —— t291 超限臂用。</summary>
    public void RenderSnapshotForSelfTest(MediaSnapshot snapshot)
        => RenderFromSnapshot(snapshot, _kind != AggregateKind.Library);

    // ── t103 自检入口：走**与点击处理同一实现**（`CardIntents`），不模拟输入 ────────────

    /// <summary>副作用：真实发起一次详情导航（用于取证"点完停在详情页"）。</summary>
    public bool OpenDetailForSelfTest(AggregatePoster poster, out string reason)
        => CardIntents.TryOpenDetail(Frame, "AGGREGATE", poster, LiveItemFor(poster?.Server?.Id, poster?.Item?.Id), out reason);

    /// <summary>副作用：按"播放按钮"那条路走一次（`SHELL_SELFTEST_NO_LAUNCH=1` 时不会启内核）。</summary>
    public bool PlayForSelfTest(AggregatePoster poster, out string blockReason)
        => CardIntents.TryPlay("AGGREGATE", poster, LiveItemFor(poster?.Server?.Id, poster?.Item?.Id), out blockReason);

    /// <summary>自检用：本页所属的导航 Frame（"退父剧"那条降级要在真 Frame 上走一遍）。</summary>
    public Frame SelfTestFrame => Frame;

    /// <summary>
/// 摘要行：**三个数各自指名**（ 末行红线的同一条纪律 —— 不得写"三数一致"）：
    /// `<K> 台有内容 · <F> 台失败 · 共 <N> 条 / 最近取数 <ms> ms`。
    /// </summary>
    private static string BuildSummary(MediaAggregate aggregate)
        => FilterName(aggregate.Kind)
           + "：跨 " + aggregate.Groups.Count + " 台服务器"
           + " · 有内容 " + aggregate.NonEmptyGroups.Count + " 台"
           + " · 失败 " + aggregate.FailedGroups.Count + " 台"
           + " · 共 " + aggregate.TotalItems + " 条"
           + " · 耗时 " + (int)aggregate.Elapsed.TotalMilliseconds + " ms";

    private static string FilterName(AggregateKind kind) => kind switch
    {
        AggregateKind.ContinueWatching => "继续播放",
        AggregateKind.Favorites => "收藏",
        _ => "媒体库",
    };

    /// <summary>角标：剧集给集数、其余给类型首字母族；无则空（不画）。</summary>
    private static string BadgeOf(EmbyItem item)
    {
        if (item == null) return string.Empty;
        if (item.IsEpisode) return item.EpisodeCodeText;

        // t168（F4 本体）：剧集角标 = 集数，走**与副标题同一个**决定点（只认 `RecursiveItemCount`；
        // 缺失 ⇒ 空串 ⇒ 不画角标，并落一行 `BADGE-FALLBACK …`）。**不得**再回退印 `ChildCount`。
        return MediaAggregator.EpisodeCountOf(item, "page-badge");
    }

    private void ShowEmpty(string title, string detail)
    {
        EmptyTitle.Text = title;
        EmptyDetail.Text = detail;
        EmptyPanel.Visibility = Visibility.Visible;
        GroupList.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 点卡片 ⇒ **进详情页**（t103：含单集；选季/选集/换源/播放都在详情页里做）。
    /// 起播改由卡片上的**播放按钮**发起（见 <see cref="OnPlayClick"/>），两种意图必须分开。
    /// </summary>
    private void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AggregatePoster poster || poster.Item == null || poster.Server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Server.Id, poster.Item.Id);
        if (CardIntents.TryOpenDetail(Frame, "AGGREGATE", poster, live, out var reason))
        {
            FailedText.Visibility = Visibility.Collapsed;
            return;
        }

        // 走不通也必须**看得见**（"点了没反应"是本卡的判失败情形）
        Program.Log("AGGREGATE detail-blocked " + reason + " item=" + poster.Item.Name);
        FailedText.Text = reason;
        FailedText.Visibility = Visibility.Visible;
    }

    /// <summary>卡片上的**播放按钮** ⇒ 起播（复用 t27 打通的 M3 链路；本屏不重复实现播放逻辑）。</summary>
    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not AggregatePoster poster
            || poster.Item == null || poster.Server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Server.Id, poster.Item.Id);
        if (CardIntents.TryPlay("AGGREGATE", poster, live, out var blockReason))
        {
            return;
        }

        Program.Log("AGGREGATE play-blocked " + blockReason + " item=" + poster.Item.Name);
        FailedText.Text = blockReason;
        FailedText.Visibility = Visibility.Visible;
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
}

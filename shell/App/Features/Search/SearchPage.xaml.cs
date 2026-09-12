using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Search;
using AIPlayer.Shell.Services.Aggregation;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Search;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Search;

/// <summary>
/// 搜索 / 聚合搜索屏（t28 / U-B），规格 = UI_SPEC_SHELL.md §7。
///
/// **对外契约（给外壳挂载用，一行接）**：
///   · 导航：<c>ContentFrame.Navigate(typeof(Features.Search.SearchPage), term)</c>（<see cref="NavTag"/> = "search"）
///   · 顶栏搜索框三个入口：<see cref="SetQuery"/>（输入，300 ms 去抖）/ <see cref="CommitQuery"/>（Enter：立即查 + 落历史）
///     / <see cref="ClearQuery"/>（Esc、×：取消在途请求并回到历史）
///   · 点卡片：<see cref="CardActivated"/>（详情页属 t29；**未接处理者时不静默** —— 落日志）
///
/// **本页只写 Features/Search/**：不碰 MainWindow / Theme / App.xaml。
///
/// 三态纪律（本卡自证到哪一步，任务报告里逐条写）：
///   · 服务层契约存在 —— 已逐条读源码核实（AggregatedSearchService.SearchAsync / SearchAsync(term,limit,types)）。
///   · 本页实现 —— 在盘（本文件 + SearchRunner.cs + SearchChip.cs + SearchPage.xaml）。
///   · 运行验证 —— 由 <see cref="SelfTestEnvVar"/> 钩子在真实窗口里跑脚本化断言后落日志（证据见任务报告）。
/// </summary>
public sealed partial class SearchPage : Page
{
    /// <summary>导航 tag（外壳 <c>NavigateTo("search")</c> 用）。</summary>
    public const string NavTag = "search";

    /// <summary>§7.2：输入 300 ms 去抖后再查询。</summary>
    public const int DebounceMs = 300;

    /// <summary>取证钩子（默认关闭；不设 ⇒ 产品行为零变化）：值为 <c>empty</c> 或搜索词。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_SEARCH";

    /// <summary>取证钩子：<c>1</c> ⇒ 额外注入一台"不可达"的反控源（验证单源失败但结果仍在）。</summary>
    public const string SelfTestBadSourceEnvVar = "SHELL_SELFTEST_SEARCH_BADSRC";

    /// <summary>取证钩子：芯片文案（默认「聚合搜索」）。</summary>
    public const string SelfTestChipEnvVar = "SHELL_SELFTEST_SEARCH_CHIP";

    /// <summary>
    /// 取证钩子（t288）：<c>&lt;序号|itemId&gt;</c> ⇒ 真查完之后**驱动一次点卡**。
    /// 走的是 <see cref="ActivateCard"/>（与真实点击同一动作点），**不是**鼠标事件 ⇒ 见证据件的未实测项。
    /// </summary>
    public const string SelfTestClickEnvVar = "SHELL_SELFTEST_SEARCH_CLICK";

    // 服务层日志直接进本屏的日志文件：单源失败消息里带**真实请求 URL**（api_key 由 SearchLog 统一打码）
    private readonly SearchRunner _runner = new(s => SearchLog.Write("SVC " + s));
    private readonly SearchHistoryService _history = SearchHistoryService.Instance;

    private DispatcherQueueTimer _debounce;
    private string _pending = string.Empty;
    private long _lastInputAt;
    private bool _injectBadSource;

    private List<ServerConfig> _servers = new List<ServerConfig>();
    private List<SearchChip> _chips = new List<SearchChip>();
    private SearchChip _selected;

    private CancellationTokenSource _cts;
    private Task _inFlight;
    private SearchRunResult _last;

    // 去抖取证：输入时刻 → 真发请求时刻 → 拿到结果时刻（全部 TickCount64）
    private long _lastRequestAt;
    private long _lastResultAt;

    // 海报加载诊断（区分"这条没有封面"与"有 URL 但没加载出来"—— 两者在截图里都是空白）
    private int _imagesOpened;
    private int _imagesFailed;
    private int _imagesEmptyUrl;

    // 字节通道（本批改动点）的计数：**取数面**（requested/fetched/null），与上面的控件事件面（opened/failed）分开报。
    // 为什么要分开：`opened` 只在控件真渲染出位图时才 +1，图片"取到了但没渲"与"根本没取到"在这一个数上无法区分。
    private int _imagesByteRequested;
    private int _imagesByteFetched;
    private int _imagesByteNull;

    /// <summary>最近一批取图的规模与"这批里本来就没有封面"的条数（增量模式下一次只取新来的那几张 ⇒
    /// 结构不变量必须按**批**判，不能按整屏判）。</summary>
    private int _imagesLastBatchCards;
    private int _imagesLastBatchEmpty;
    private Task _imagesFill = Task.CompletedTask;

    /// <summary>最近一次渲染的卡片集合：给自检读"位图**交到卡片上了没有**"（见 <c>SELFCHECK-IMAGES</c> 的 boundNow）。</summary>
    private List<CardVm> _lastCardVms = new List<CardVm>();

    // ── SEAM②（聚合搜索逐源增量）状态与读数 ────────────────────────────────────
    // 渲染面：回调次数 + 首次/末次渲染相对"请求起点"的毫秒（反控①要比**时刻**，不能只数次数）。
    // 内容面：同一去重键最多对应几张卡（就地合并则恒为 1）、已到达源数、是否见过终值那一发、终值那一发的四个计数。
    private ObservableCollection<CardVm> _incVms;
    private long _incRunStartMs;
    private int _incRenders;
    private long _incFirstRenderMs = -1;
    private long _incLastRenderMs = -1;
    private int _incMaxCardsPerKey = 1;
    private int _incArrivedSources;
    private bool _incFinalSeen;

    /// <summary>到达序累计是否触到硬上限、以及被上限挡掉的条目数（0 且 capped=False = 常态）。</summary>
    private bool _incCapped;
    private int _incDropped;
    private int _incFinalTotalHits = -1;
    private int _incFinalMergedCount = -1;
    private int _incFinalMergedHitCount = -1;
    private int _incFinalItemsCount = -1;

    /// <summary>整批取图的**总预算**：单请求超时 20 s 由缓存单点负责，这里再给整批封顶，避免 60 张图把页面拖死。</summary>
    private static readonly TimeSpan ImagesFillBudget = TimeSpan.FromSeconds(25);

    /// <summary>增量模式**屏上卡片数的硬上限**（captain 2026-09-12 裁定：到达序累计必须**有界**）。
    /// 取 240：远超本机实测最坏（178 个去重条目全部滚动进屏），真触发就是异常放大，靠 `SEARCH-INCREMENTAL-CAPPED` 留痕。</summary>
    private const int IncMaxRenderedCards = 240;

    /// <summary>点卡片（详情导航由 t29 接线；本例只在这里抛事件，未接处理者时落日志）。</summary>
    public event EventHandler<ResultCard> CardActivated;

    public SearchPage()
    {
        InitializeComponent();

        _debounce = DispatcherQueue.CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(DebounceMs);
        _debounce.IsRepeating = false;
        _debounce.Tick += OnDebounceTick;

        Loaded += OnLoaded;
    }

    // ===================== 对外契约 =====================

    /// <summary>顶栏输入：300 ms 去抖（<paramref name="immediate"/> = true 时立即查，用于 Enter / 取证）。</summary>
    public void SetQuery(string term, bool immediate = false)
    {
        _pending = term ?? string.Empty;
        _lastInputAt = Environment.TickCount64;

        if (_debounce == null) { return; }

        if (immediate)
        {
            _debounce.Stop();
            StartRun(_pending);
            return;
        }

        _debounce.Stop();
        _debounce.Start();
        SearchLog.Write($"SEARCH-INPUT term=\"{_pending.Trim()}\" at={_lastInputAt}");
    }

    /// <summary>Enter：立即查询并把查询词落历史（§7.2）。</summary>
    public void CommitQuery(string term)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length > 0)
        {
            _history.Add(query);
            _history.Save();
        }
        SetQuery(query, immediate: true);
    }

    /// <summary>Esc / ×：取消在途请求，回到搜索历史（§7.2）。</summary>
    public void ClearQuery()
    {
        _pending = string.Empty;
        _debounce?.Stop();
        _cts?.Cancel();
        _last = null;
        ShowHistory();
        SearchLog.Write("SEARCH-CLEAR 退出搜索态，显示历史");
    }

    /// <summary>当前选中的芯片文案（取证/外壳状态栏用）。</summary>
    public string SelectedChipLabel => _selected?.Label ?? string.Empty;

    // ===================== 生命周期 =====================

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_injectBadSource = ReadFlag(SelfTestBadSourceEnvVar))
        {
            SearchLog.Write("SEARCH-BADSRC 已开启反控坏源注入：" + SearchRunner.BadSourceUrl);
        }

        ReloadServers();
        BuildChips();
        ShowHistory();
        _ = RunSelfCheckHookAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e?.Parameter is string term && !string.IsNullOrWhiteSpace(term))
        {
            // 外壳把顶栏当前查询词经导航参数带进来（一行契约）。
            DispatcherQueue.TryEnqueue(() => SetQuery(term, immediate: true));
        }
    }

    // ===================== 服务器与芯片 =====================

    private void ReloadServers()
    {
        try
        {
            _servers = SearchRunner.LoadServers(s => SearchLog.Write("SEARCH-SERVER " + s));
        }
        catch (Exception ex)
        {
            _servers = new List<ServerConfig>();
            SearchLog.Write("SEARCH-SERVERS-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>主源 = settings.lastServerId 指向的已启用服务器；否则第一台已启用服务器。</summary>
    private ServerConfig MainServer()
    {
        var enabled = _servers.Where(s => s.Enabled).ToList();
        string lastId = null;
        // 读「上次用的服务器」失败 ⇒ 只是少一个偏好来源（下面退回第一台已启用服务器）；
        // 但它会改变**主源选择**，故必须留可见痕迹，否则取证时无法解释主源为何不是 lastServerId
        try { lastId = AIPlayer.Shell.Services.Settings.SettingsService.Instance?.Settings?.LastServerId; }
        catch (Exception ex) { SearchLog.Write("SEARCH-LASTID-FAIL " + ex.GetType().Name + ": " + ex.Message); }

        if (!string.IsNullOrEmpty(lastId))
        {
            var byLast = enabled.FirstOrDefault(s => s.Id == lastId);
            if (byLast != null) { return byLast; }
        }
        return enabled.FirstOrDefault();
    }

    private void BuildChips()
    {
        var main = MainServer();
        var kind = main?.Kind ?? ServerKind.Emby;
        _chips = SearchChipCatalog.Build(kind, out var note);

        var label = main == null ? "(无服务器)" : main.Name + " / " + ServerKindExtensions.DisplayName(kind);
        SearchLog.Write($"SEARCH-CHIPS main={label} count={_chips.Count} note={note}");

        ChipRow.Children.Clear();
        foreach (var chip in _chips)
        {
            ChipRow.Children.Add(BuildChipVisual(chip));
        }

        _selected = _chips.FirstOrDefault();
        RefreshChipVisuals();
    }

    private FrameworkElement BuildChipVisual(SearchChip chip)
    {
        var text = new TextBlock
        {
            Text = chip.Label,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // §7.3：高 33、圆角 8、内边距左右各 12
        var border = new Border
        {
            Height = 33,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 0, 12, 0),
            BorderThickness = new Thickness(1),
            Child = text,
            Tag = chip,
        };

        if (!chip.IsAvailable)
        {
            ToolTipService.SetToolTip(border, "待定：" + chip.UnavailableReason);
            border.Opacity = 0.45;
        }
        else
        {
            border.Tapped += OnChipTapped;
            ToolTipService.SetToolTip(border, chip.Scope == SearchScope.Aggregated
                ? "跨全部启用服务器并发查询 → 去重 → 排序"
                : "仅在当前主源（" + (MainServer()?.Name ?? "无") + "）内按类型搜索");
        }

        return border;
    }

    private void RefreshChipVisuals()
    {
        foreach (var child in ChipRow.Children.OfType<Border>())
        {
            if (child.Tag is not SearchChip chip) { continue; }
            var selected = ReferenceEquals(chip, _selected);
            var text = child.Child as TextBlock;

            // 选中 = Accent 实底 + 深色字；未选中 = ControlBg 底 + 1px #434343 边 + 白字（§7.3）
            child.Background = selected
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["ControlBgBrush"];
            child.BorderBrush = selected
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["ChipBorderBrush"];
            if (text != null)
            {
                text.Foreground = selected
                    ? new SolidColorBrush(Microsoft.UI.Colors.Black)
                    : (Brush)Application.Current.Resources["TextPrimaryBrush"];
            }
        }
    }

    private void OnChipTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if ((sender as Border)?.Tag is not SearchChip chip || !chip.IsAvailable) { return; }
        _selected = chip;
        RefreshChipVisuals();
        SearchLog.Write($"SEARCH-CHIP selected={chip.Label} scope={chip.Scope} types={(chip.IncludeItemTypes ?? "(不限)")}");

        if (!string.IsNullOrWhiteSpace(_pending))
        {
            StartRun(_pending);
        }
    }

    private void OnDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        StartRun(_pending);
    }

    // ===================== 查询 =====================

    private void StartRun(string term) => _inFlight = RunAsync(term, _selected);

    private async Task RunAsync(string term, SearchChip chip)
    {
        var query = (term ?? string.Empty).Trim();

        // 空查询：显示历史，**不发网络请求**（§7.6「空查询」行）。
        if (query.Length == 0)
        {
            ShowHistory();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ShowLoading(query, chip);
        var delta = _lastInputAt == 0 ? -1 : Environment.TickCount64 - _lastInputAt;
        _lastRequestAt = Environment.TickCount64;
        SearchLog.Write($"SEARCH-REQ chip=\"{chip?.Label}\" scope={chip?.Scope} term=\"{query}\" debounceMs={DebounceMs} deltaMs={delta}");

        // SEAM②：本跑清零增量状态 + 建回调（`Progress<T>` 在**创建它的线程**（= UI 线程）上排回调 ⇒ 不再包 DispatcherQueue）
        _incVms = null;
        _incRenders = 0;
        _incFirstRenderMs = -1;
        _incLastRenderMs = -1;
        _incMaxCardsPerKey = 1;
        _incArrivedSources = 0;
        _incFinalSeen = false;
        _incFinalTotalHits = _incFinalMergedCount = _incFinalMergedHitCount = _incFinalItemsCount = -1;
        _incRunStartMs = Environment.TickCount64;
        var progress = new Progress<AggregatedSearchProgress>(OnSearchProgress);

        SearchRunResult result;
        try
        {
            result = await _runner.RunAsync(query, chip, token, _injectBadSource, progress);
        }
        catch (OperationCanceledException)
        {
            SearchLog.Write($"SEARCH-CANCELLED term=\"{query}\"");
            return;
        }

        if (token.IsCancellationRequested) { return; }

        _last = result;
        _lastResultAt = Environment.TickCount64;
        RenderResult(result);

        SearchLog.Write($"SEARCH-RESULT term=\"{result.Term}\" chip=\"{result.ChipLabel}\" {result.CountsLine()}"
            + $" cards={result.Cards.Count} elapsedMs={result.ElapsedMs} noSources={result.NoSources}"
            + $" error={(result.Error == null ? "none" : result.Error)}");
    }

    private void RenderResult(SearchRunResult result)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;

        // 空源 / 异常 / 无结果：三态分开（§7.6）
        var failed = FailedDisplayNames(result);

        if (result.NoSources || result.Cards.Count == 0)
        {
            ResultsGrid.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Visible;

            if (result.NoSources)
            {
                EmptyTitle.Text = "没有可搜索的服务器";
                EmptyDetail.Text = "聚合搜索只覆盖已启用的 Emby / Jellyfin 服务器；请先在「服务器」里添加或启用一台。";
            }
            else if (result.Error != null)
            {
                EmptyTitle.Text = "搜索失败";
                EmptyDetail.Text = result.Error;
            }
            else if (failed.Count > 0 && failed.Count >= result.SourceCount)
            {
                EmptyTitle.Text = "全部源都失败了";
                EmptyDetail.Text = "失败来源：" + string.Join("、", failed) + "（结果为空，但失败原因已列出，不是静默失败）";
            }
            else
            {
                EmptyTitle.Text = "没有结果";
                EmptyDetail.Text = $"「{result.Term}」在 {result.SourceCount} 台服务器里没有命中。"
                    + (failed.Count > 0 ? "（部分源失败，见上方摘要行）" : string.Empty);
            }

            // 降级按钮：只在主源是 Emby 系且存在类型芯片时可点（真动作 = 切到第一个类型芯片重查）
            var typed = _chips.FirstOrDefault(c => c.Scope == SearchScope.Typed && c.IsAvailable);
            EmptyFallbackButton.Visibility = typed == null ? Visibility.Collapsed : Visibility.Visible;
            EmptyFallbackButton.Tag = typed;

            RenderSummary(result, failed, cardsVisible: false);
            return;
        }

        EmptyPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        ResultsGrid.Visibility = Visibility.Visible;

        // SEAM② 收口（反控⑤ 的选择：**保持到达序、不切终值序**）：
        // 增量已经渲染过 ⇒ 这里**不重建、不重排**（重建=闪屏；重排=把用户正在看的卡片挪位，也打断就地更新）。
        // 但仍要核对"终值卡片集 == 已渲染卡片集"，缺的**补齐并报数**（不静默）。
        if (_incVms != null)
        {
            var missingVms = new List<CardVm>();
            foreach (var card in result.Cards)
            {
                if (!string.IsNullOrEmpty(card.ItemId) && _incVms.Any(v => v.Card != null && v.Card.ItemId == card.ItemId)) { continue; }
                var vm = ToCardVm(card);
                _incVms.Add(vm);
                missingVms.Add(vm);
            }

            _lastCardVms = _incVms.ToList();
            _imagesEmptyUrl = _lastCardVms.Count(v => string.IsNullOrWhiteSpace(v.Card?.ImageUrl));
            SearchLog.Write($"SEARCH-INCREMENTAL-CLOSE rendered={_incVms.Count} terminalCards={result.Cards.Count} appended={missingVms.Count}");
            if (missingVms.Count > 0) { _imagesFill = FillQueuedAsync(_imagesFill, missingVms); }
            RenderSummary(result, failed, cardsVisible: true);

            // 到达序累计**可能多于**最终窗口（17 台逐个到达时，"当时的 60 条"集合会变 ⇒ 早期进过屏的卡留在屏上）。
            // 这是我们选择的语义（用户要"一个个显示、后到的在下面"，不能把已在看的卡抽走），但**必须在界面上说清**：
            // 否则用户看到 76 张卡而摘要写"面板显示 60 条"，就是一个自相矛盾的界面。
            if (_incVms.Count != result.Cards.Count)
            {
                // 界面话（captain 2026-09-12 裁定）：≤20 字、用用户的话，**不出现**"窗口/截断/抽走"这类我们的术语；
                // 完整理由进 ToolTip + 日志（不进界面正文）。
                SummaryCounts.Text += "（列表里共 " + _incVms.Count + " 条）";
                ToolTipService.SetToolTip(SummaryCounts,
                    "搜索时各台服务器陆续返回结果：先到先显示、后到的排在下面。"
                    + "所以列表里的条数可能比上面那句汇总里说的多几条——多出来的是更早到达、仍在列表里的结果。");
                SearchLog.Write($"SEARCH-INCREMENTAL-OVERFLOW rendered={_incVms.Count} terminal={result.Cards.Count}"
                    + $" capped={_incCapped} dropped={_incDropped}"
                    + " note=到达序累计超出终值窗口(limit)，屏上保留、界面用一句用户话标注，理由在 ToolTip 与注释里");
            }
            return;
        }

        var vms = new List<CardVm>();
        foreach (var card in result.Cards)
        {
            vms.Add(ToCardVm(card));
        }
        _imagesOpened = 0;
        _imagesFailed = 0;
        _imagesEmptyUrl = result.Cards.Count(c => string.IsNullOrWhiteSpace(c.ImageUrl));
        ResultsGrid.ItemsSource = vms;

        // 封面走字节通道（异步、有预算）；**不 await** —— 结果先出、封面陆续到位（自检里会等它跑完再读数）。
        _lastCardVms = vms;
        _imagesFill = FillCardImagesAsync(vms);

        RenderSummary(result, failed, cardsVisible: true);
    }

    /// <summary>
    /// 失败源的**显示名**：`名字（Id 前 4 位）`（§7.6：服务层只存名字，两台同名服务器会歧义 ——
    /// 实测本机确有两台 `ServerD`）。Id 由 <see cref="SearchRunner.LastFailedSources"/> 按建源顺序配回；
    /// 配不上时退化为名字本身（不假装有 Id）。
    /// </summary>
    private List<string> FailedDisplayNames(SearchRunResult result)
    {
        var names = result?.FailedServers ?? new List<string>();
        if (names.Count == 0) { return new List<string>(); }

        var paired = _runner.LastFailedSources;
        if (paired != null && paired.Count == names.Count)
        {
            return paired.Select(f => f.ToString()).ToList();
        }
        return names.ToList();
    }

    /// <summary>
    /// 摘要行：**每个数各自指名**（§7.6 末行红线）。五个数语义各不相同：
    /// 台数 / 去重后条目数（截断前）/ 面板显示条数 / 命中总数 / 服务端 TotalHits。
    /// </summary>
    /// <summary>
    /// SEAM②（t32）：聚合搜索的**逐源增量**回调 —— 某个源一返回就调一次（快源先出、后到的排在下面）。
    /// <para>· 只做「补齐新增 / 更新已有」，**不清屏、不重置 ItemsSource**（清屏 = 闪屏 + 丢滚动位置）。</para>
    /// <para>· 去重键由服务层给（<c>ItemKeys</c>/<c>FindByKey</c>），UI **不自己算键** ⇒ 同名同类型同年只会 1 张卡。</para>
    /// <para>· 新到的卡片封面走**字节通道**（`SearchRunner.Images` → `ImageSourceLoader`），与终值渲染同一条路。</para>
    /// </summary>
    private void OnSearchProgress(AggregatedSearchProgress p)
    {
        if (p == null) { return; }

        _incRenders++;
        var elapsed = Environment.TickCount64 - _incRunStartMs;
        if (_incFirstRenderMs < 0) { _incFirstRenderMs = elapsed; }
        _incLastRenderMs = elapsed;
        _incArrivedSources = p.ArrivedSourceCount;
        if (p.IsFinal)
        {
            _incFinalSeen = true;
            _incFinalTotalHits = p.TotalHits;
            _incFinalMergedCount = p.MergedCount;
            _incFinalMergedHitCount = p.MergedHitCount;
            _incFinalItemsCount = p.Items.Count;
        }

        if (_incVms == null)
        {
            // 第一次增量到达：建集合 + 接 ItemsSource（此后**只改集合内容**）
            _incVms = new ObservableCollection<CardVm>();
            _imagesOpened = 0;
            _imagesFailed = 0;
            ResultsGrid.ItemsSource = _incVms;
            ResultsGrid.Visibility = Visibility.Visible;
            HistoryPanel.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        var added = new List<CardVm>();
        for (var i = 0; i < p.Items.Count; i++)
        {
            var entry = p.Items[i];
            var key = i < p.ItemKeys.Count ? p.ItemKeys[i] : string.Empty;
            var existing = string.IsNullOrEmpty(key) ? null : _incVms.FirstOrDefault(v => v.DedupeKey == key);
            if (existing == null)
            {
                if (_incVms.Count >= IncMaxRenderedCards)
                {
                    _incDropped++;
                    if (!_incCapped)
                    {
                        _incCapped = true;
                        SearchLog.Write($"SEARCH-INCREMENTAL-CAPPED rendered={_incVms.Count} cap={IncMaxRenderedCards}"
                            + " note=屏上卡片数已达硬上限，后续新条目不再追加（界面数字仍是真实条数）");
                    }
                    continue;
                }
                var vm = ToCardVm(SearchRunner.ToCard(entry));
                vm.DedupeKey = key;
                _incVms.Add(vm);
                added.Add(vm);
            }
            else
            {
                existing.UpdateFrom(SearchRunner.ToCard(entry));   // 就地更新（同键又来了新命中）
            }
        }

        // 反控② 的读数：同一去重键最多对应几张卡（就地合并 ⇒ 恒为 1）
        if (_incVms.Count > 0)
        {
            var maxPerKey = _incVms.GroupBy(v => v.DedupeKey ?? string.Empty).Max(g => g.Count());
            if (maxPerKey > _incMaxCardsPerKey) { _incMaxCardsPerKey = maxPerKey; }
        }

        _lastCardVms = _incVms.ToList();
        _imagesEmptyUrl = _lastCardVms.Count(v => string.IsNullOrWhiteSpace(v.Card?.ImageUrl));

        if (added.Count > 0)
        {
            _imagesFill = FillQueuedAsync(_imagesFill, added);   // 只取新来的那几张；**排队**免得两批并发取图
        }

        RenderIncrementalSummary(p);
    }

    /// <summary>
    /// 增量途中的摘要行（边跑边更新）。四个计数**直接取回调字段**（`MergedCount` / `MergedHitCount` /
    /// `Items.Count` / `TotalHits`），各自具名、UI 不重算；**不写"三数一致"**（按设计本就不相等）。
    /// </summary>
    private void RenderIncrementalSummary(AggregatedSearchProgress p)
    {
        SummaryRow.Visibility = Visibility.Visible;

        SummaryCounts.Text = "正在搜索「" + p.Term + "」 · 已到 " + p.ArrivedSourceCount + "/" + p.SourceCount + " 台服务器"
            + " · 去重后条目数 " + p.MergedCount
            + " · 去重后命中总数 " + p.MergedHitCount
            + " · 面板显示 " + p.Items.Count + " 条"
            + " · 命中总数 " + p.TotalHits;

        var failedNow = p.FailedServers?.Count ?? 0;
        SummaryFailed.Visibility = Visibility.Visible;
        SummaryFailed.Text = failedNow == 0
            ? "失败 0 台"
            : "失败 " + failedNow + " 台：" + string.Join("、", p.FailedServers);
    }

    private void RenderSummary(SearchRunResult result, List<string> failed, bool cardsVisible)
    {
        SummaryRow.Visibility = Visibility.Visible;

        var totalHits = result.TotalHits == 0 && result.ShownCount > 0
            ? "未返回(0)"      // 服务端没给 TotalRecordCount 时**不许**显示成"0 条"（会读成"什么都没有"）
            : result.TotalHits.ToString();

        var text = $"跨 {result.SourceCount} 台服务器 · 去重后条目数 {result.MergedCount}"
            + $" · 面板显示 {result.ShownCount} 条 · 命中总数 {result.MergedHitCount}"
            + $" · 服务端 TotalHits {totalHits}";

        if (result.Scope == SearchScope.Typed)
        {
            text = $"主源 1 台 · 返回条目数 {result.MergedCount}"
                + $" · 面板显示 {result.ShownCount} 条 · 服务端 TotalHits {totalHits}";
        }

        if (!string.IsNullOrEmpty(_runner.LastNote))
        {
            text += "｜" + _runner.LastNote;
        }
        text += $"｜耗时 {result.ElapsedMs} ms";
        if (!cardsVisible) { text += "｜当前无结果"; }

        SummaryCounts.Text = text;

        // 失败源：用 ServerFail 色列出（§7.3）；**0 台也照写**，好让"失败列表为空"这条断言看得见。
        SummaryFailed.Visibility = Visibility.Visible;
        SummaryFailed.Text = failed.Count == 0
            ? "失败 0 台"
            : "失败 " + failed.Count + " 台：" + string.Join("、", failed);
    }

    private CardVm ToCardVm(ResultCard card)
    {
        var vm = new CardVm
        {
            Card = card,
            Title = card.Title,
            Subtitle = card.Subtitle,
            BadgeText = card.BadgeText ?? string.Empty,
        };

        // 🔴 封面**不在这里**建图：`new BitmapImage(new Uri(url))` 是"URI 直连"（WinUI 自己的栈去下载 ⇒
        //    绕过我们的 User-Agent / 代理策略 / 磁盘缓存，且非打包 WinUI3 下实测拿不到图）⇒ 改走字节通道，
        //    见 `FillCardImagesAsync`（`SearchRunner.Images` 取字节 → 既有 `ImageSourceLoader`）。

        return vm;
    }

    /// <summary>把一批取图**排在上批之后**（逐源增量会连续来多批；并发取图会打乱计数、也会对服务器压测）。</summary>
    private async Task FillQueuedAsync(Task previous, List<CardVm> vms)
    {
        if (previous != null)
        {
            try { await previous.ConfigureAwait(true); }
            catch (Exception ex)
            {
                // 上一批的失败不该拦住本批：`FillCardImagesAsync` 自己已兜底，这里只是兜住"排队"这一层的意外
                SearchLog.Write("SEARCH-IMG-BYTE-QUEUE-PREV-FAIL " + ex.GetType().Name);
            }
        }
        await FillCardImagesAsync(vms).ConfigureAwait(true);
    }

    /// <summary>
    /// 结果卡封面：**字节通道**。URL → 字节（`SearchRunner.Images` = 进程级缓存单点 + 我们的 `ShellHttpClient`）
    /// → 既有 `ImageSourceLoader`（与首页/详情/聚合同一条路，不新开第二条图片管线）。
    /// <para>· 失败/空字节/解码不出：只记日志并**保持占位底色**（不抛、不整屏崩）。</para>
    /// <para>· 整批有预算封顶（<see cref="ImagesFillBudget"/>）；被预算截断时如实报 <c>abortedByBudget=True</c>。</para>
    /// <para>· 逐张顺序取（与详情页同口径：不对服务器并发压测）；第二次搜索同 URL 命中磁盘缓存。</para>
    /// </summary>
    private async Task FillCardImagesAsync(List<CardVm> vms)
    {
        _imagesByteRequested = 0;
        _imagesByteFetched = 0;
        _imagesByteNull = 0;
        using var budget = new CancellationTokenSource(ImagesFillBudget);

        try
        {
            foreach (var vm in vms)
            {
                if (budget.IsCancellationRequested) { break; }

                var url = vm.Card?.ImageUrl;
                if (string.IsNullOrWhiteSpace(url)) { continue; }

                _imagesByteRequested++;
                byte[] bytes = null;
                try
                {
                    bytes = await _runner.Images.GetOrFetchAsync(url, budget.Token).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    // 缓存单点自身承诺"失败返回 null + 可见日志"（不抛）；HTTP/超时/取消在这里只影响这一张图。
                    SearchLog.Write($"CARD-IMAGE-FETCH-THROW {vm.Card.ItemId} {ex.GetType().Name}");
                }

                if (bytes == null || bytes.Length == 0) { _imagesByteNull++; continue; }

                var bitmap = await AIPlayer.Shell.Features.Aggregate.Shared.ImageSourceLoader.LoadAsync(bytes);
                if (bitmap == null) { _imagesByteNull++; continue; }

                vm.SetImage(bitmap);
                _imagesByteFetched++;
            }
        }
        catch (Exception ex)
        {
            // 顶层兜底：本方法是 fire-and-forget（无人 await 的路径上异常会变成 unobserved）⇒ 只记不抛。
            SearchLog.Write("SEARCH-IMG-BYTE-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        var emptyInBatch = vms.Count(v => string.IsNullOrWhiteSpace(v.Card?.ImageUrl));
        _imagesLastBatchCards = vms.Count;
        _imagesLastBatchEmpty = emptyInBatch;
        SearchLog.Write($"SEARCH-IMG-BYTE requested={_imagesByteRequested} fetched={_imagesByteFetched}"
            + $" null={_imagesByteNull} cards={vms.Count} emptyUrlInBatch={emptyInBatch}"
            + $" partitionOk={_imagesByteRequested + emptyInBatch == vms.Count}"
            + $" abortedByBudget={budget.IsCancellationRequested}");
    }

    private void ShowHistory()
    {
        ResultsGrid.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        SummaryRow.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Visible;

        try { _history.Load(); } catch (Exception ex) { SearchLog.Write("HISTORY-LOAD-FAIL " + ex.GetType().Name); }

        HistoryStack.Children.Clear();
        var entries = _history.Entries?.ToList() ?? new List<string>();
        if (entries.Count == 0)
        {
            HistoryStack.Children.Add(new TextBlock
            {
                Text = "还没有搜索历史。在上方搜索框输入关键词后按 Enter 即会记录（最多 " + SearchHistoryService.MaxEntries + " 条）。",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            });
            return;
        }

        foreach (var entry in entries)
        {
            HistoryStack.Children.Add(BuildHistoryRow(entry));
        }
    }

    private FrameworkElement BuildHistoryRow(string entry)
    {
        var grid = new Grid { Padding = new Thickness(4, 2, 4, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var button = new Button
        {
            Content = entry,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = null,
            BorderThickness = new Thickness(0),
            Tag = entry,
        };
        button.Click += (s, e) =>
        {
            if ((s as Button)?.Tag is string term) { CommitQuery(term); }
        };
        Grid.SetColumn(button, 0);
        grid.Children.Add(button);

        var remove = new Button { Content = "移除", Tag = entry };
        remove.Click += (s, e) =>
        {
            if ((s as Button)?.Tag is string term)
            {
                _history.Remove(term);
                _history.Save();
                SearchLog.Write($"HISTORY-REMOVE \"{term}\" count={_history.Count}");
                ShowHistory();
            }
        };
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);

        return grid;
    }

    private void OnClearHistoryClick(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        _history.Save();
        SearchLog.Write("HISTORY-CLEAR count=0");
        ShowHistory();
    }

    private void OnEmptyFallbackClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SearchChip typed) { return; }
        SearchLog.Write($"SEARCH-FALLBACK chip={typed.Label} types={typed.IncludeItemTypes}");
        OnChipTappedForFallback(typed);
    }

    private void OnChipTappedForFallback(SearchChip chip)
    {
        _selected = chip;
        RefreshChipVisuals();
        StartRun(_pending);
    }

    private void OnCardImageOpened(object sender, RoutedEventArgs e) => _imagesOpened++;

    private void OnCardImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _imagesFailed++;
        var url = ((sender as Image)?.DataContext as CardVm)?.Card?.ImageUrl;
        // URL 里带 api_key ⇒ 落盘前由 SearchLog 统一打码
        SearchLog.Write($"CARD-IMAGE-OPEN-FAIL url=\"{url}\" error={e?.ErrorMessage}");
    }

    private void OnCardClick(object sender, ItemClickEventArgs e)    {
        if (e?.ClickedItem is not CardVm vm || vm.Card == null) { return; }
        ActivateCard(vm.Card);
    }

    /// <summary>
    /// 点卡之后要做的全部事（t288）：<see cref="OnCardClick"/> 与自检驱动**共用同一个动作点** ——
    /// 否则会出现最坏的那种假绿："自检绿了、真实点击仍然没反应"。
    /// <para>**未接处理者时不静默**：落一行 <c>handler=none</c>。这正是 2026-09-13 用户报障
    /// 「现在搜索后 点击结果时无任何反应」当刻唯一能听见的声音 —— 那一行一直在写，只是从没有处理者。</para>
    /// </summary>
    private void ActivateCard(ResultCard card)
    {
        if (card == null || string.IsNullOrEmpty(card.ItemId)) { return; }

        if (CardActivated == null)
        {
            // 不静默：详情导航属 t29（U-C），这里把"点了但没人接"的事实写清楚。
            SearchLog.Write($"CARD-CLICK item={card.ItemId} server={card.ServerName}"
                + " handler=none（详情页导航由 t29/外壳接线）");
            return;
        }

        SearchLog.Write($"CARD-CLICK item={card.ItemId} server={card.ServerName} handler=wired");
        CardActivated.Invoke(this, card);
    }

    private void ShowLoading(string query, SearchChip chip)
    {
        ResultsGrid.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        SummaryRow.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = $"正在搜索「{query}」（{chip?.Label}）…";
    }

    // ===================== 取证钩子（默认关闭） =====================

    private static bool ReadFlag(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(raw)
            && (raw.Trim() == "1" || raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 脚本化自检（**只在设了 <see cref="SelfTestEnvVar"/> 时跑**；产品路径零输出）。
    /// 断言逐条落 <see cref="SearchLog.FilePath"/>，UI 停在最后状态以便按句柄截图。
    /// </summary>
    private async Task RunSelfCheckHookAsync()
    {
        var raw = Environment.GetEnvironmentVariable(SelfTestEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) { return; }

        raw = raw.Trim();
        SearchLog.Clear();
        SearchLog.Write($"SELFCHECK-BEGIN mode=\"{raw}\" badsrc={_injectBadSource} debounceMs={DebounceMs}"
            + $" servers={_servers.Count} enabled={_servers.Count(s => s.Enabled)}"
            + $" embyFamily={_servers.Count(s => s.Enabled && s.Kind.IsEmbyFamily())}");

        // ① 空查询：显示历史 + **零网络请求**（反控制：NetworkRequests 必须还是 0）
        var chipsBefore = ChipRow.Children.Count;
        SetQuery(string.Empty, immediate: true);
        await Task.Delay(400);
        SearchLog.Write($"SELFCHECK-EMPTY networkRequests={_runner.NetworkRequests}"
            + $" historyVisible={HistoryPanel.Visibility} historyEntries={_history.Count}"
            + $" summaryVisible={SummaryRow.Visibility} chipCount={chipsBefore}");

        if (raw.Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            SearchLog.Write("SELFCHECK-END mode=empty");
            return;
        }

        // ② 历史往返（真写盘一次再撤销，不留残留）：Add → Contains → Count → Remove
        var sentinel = "自检哨兵词-t28";
        var beforeCount = _history.Count;
        _history.Add(sentinel);
        var added = _history.Contains(sentinel);
        var afterAdd = _history.Count;
        _history.Remove(sentinel);
        var afterRemove = _history.Count;
        SearchLog.Write($"SELFCHECK-HISTORY add={added} count {beforeCount}->{afterAdd}->{afterRemove}"
            + $" removed={! _history.Contains(sentinel)}");

        // ③ 选芯片（默认第一个 = 聚合搜索）
        var chipLabel = Environment.GetEnvironmentVariable(SelfTestChipEnvVar);
        var chip = string.IsNullOrWhiteSpace(chipLabel)
            ? _chips.FirstOrDefault()
            : _chips.FirstOrDefault(c => c.Label == chipLabel.Trim()) ?? _chips.FirstOrDefault();
        _selected = chip;
        RefreshChipVisuals();
        SearchLog.Write($"SELFCHECK-CHIP selected=\"{chip?.Label}\" available={chip?.IsAvailable}"
            + $" scope={chip?.Scope} types={(chip?.IncludeItemTypes ?? "(不限)")}");

        // ④ 真查一次（immediate：跳过 300 ms 去抖，先把数据面验完）
        var startedAt = Environment.TickCount64;
        _last = null;
        _lastResultAt = 0;
        SetQuery(raw, immediate: true);
        await WaitForResultAsync(TimeSpan.FromSeconds(90));
        var waited = Environment.TickCount64 - startedAt;

        var r = _last;
        SearchLog.Write($"SELFCHECK-RESULT waitedMs={waited} term=\"{r?.Term}\" chip=\"{r?.ChipLabel}\""
            + $" sourceCount={r?.SourceCount} mergedCount={r?.MergedCount} mergedHitCount={r?.MergedHitCount}"
            + $" shown={r?.ShownCount} totalHits={r?.TotalHits} failedCount={r?.FailedServers?.Count}"
            + $" failed=[{string.Join(",", r?.FailedServers ?? new List<string>())}]"
            + $" failedDisplay=[{string.Join(",", FailedDisplayNames(r))}]"
            + $" cards={r?.Cards.Count} networkRequests={_runner.NetworkRequests}"
            + $" gridVisible={ResultsGrid.Visibility} historyVisible={HistoryPanel.Visibility}"
            + $" emptyVisible={EmptyPanel.Visibility} summaryVisible={SummaryRow.Visibility}"
            + $" summaryText=\"{SummaryCounts.Text}\" failedText=\"{SummaryFailed.Text}\"");

        if (r != null && r.Cards.Count > 0)
        {
            var first = r.Cards[0];
            SearchLog.Write($"SELFCHECK-FIRSTCARD title=\"{first.Title}\" subtitle=\"{first.Subtitle}\""
                + $" badge=\"{first.BadgeText}\" server=\"{first.ServerName}\" image=\"{first.ImageUrl}\"");
        }

        // ④b 海报加载读数：区分"这条没有封面"(emptyUrl) 与"有 URL 没加载出来"(failed)。
        //     截图里两者都是空白块 ⇒ 没有这行读数就无法判定是数据面还是加载面。
        // ④b 海报加载读数（本批改动点：`BitmapImage(uri)` 直连 → 字节通道）：**等字节通道跑完**（整批有预算封顶）再读，
        //     否则读到的只是"刚开始"的数。三条**分开**报，因为它们的"能证明什么"不同：
        //       · byteRequested/byteFetched/byteNull = **取数面**（我们的 ShellHttpClient + 磁盘缓存的成绩）——本批的主判据；
        //       · boundNow = **交棒面**（位图真的挂在 CardVm.Image 上了没有）——能失败：填图没跑/没通知就是 0；
        //       · opened/failed = **控件事件面**。⚠️ 实测（2026-09-12）：字节通道喂进去的 `BitmapImage`
        //         **不触发 `Image.ImageOpened`** ⇒ 本通道下 opened 恒为 0，**不能**当"有没有渲染出来"的判据
        //         （同一环境、同一页面：URI 直连的旧代际 opened=23；新代际 opened=0，而窗口级 PrintWindow
        //          截图里 10 张海报清晰可见 ⇒ 渲染是真的，读数不是）。渲染面的判据 = 那张 PNG。
        //     另给两条**可失败**的结构不变量：bytePartitionOk（每张卡"要么有 URL 且取了字节、要么本就是空 URL"）、
        //     openedLEFetched（控件能渲染出来的不可能多于我们交给它的位图数）。
        await _imagesFill.ConfigureAwait(true);

        // 位图**交出去 ≠ 已渲染**：控件的 `ImageOpened` 要等一次布局/合成（实测：紧跟取数面读到的是 opened=0）。
        // 旧版这里是无条件 `Task.Delay(2500)`；现在改成**有条件提前结束**的等待 —— 读数既有判定力，又不白等：
        //   · 事件面追上取数面（全部渲染出来）⇒ 停；
        //   · 连续 1.5 s 计数不再变（稳定的部分渲染，例如虚拟化只实现了可视项）⇒ 停；
        //   · 上限 8 s。
        var renderWaitStart = Environment.TickCount64;
        var lastSeen = -1;
        var lastChangeAt = renderWaitStart;
        while (true)
        {
            var seen = _imagesOpened + _imagesFailed;
            var now = Environment.TickCount64;
            if (_imagesByteFetched > 0 && seen >= _imagesByteFetched) { break; }
            if (seen != lastSeen) { lastSeen = seen; lastChangeAt = now; }
            else if (seen > 0 && now - lastChangeAt >= 1500) { break; }
            if (now - renderWaitStart >= 8000) { break; }
            await Task.Delay(250);
        }
        var renderWaitMs = Environment.TickCount64 - renderWaitStart;

        // ④c SEAM②（逐源增量）读数，三条面分开：
        //     渲染面（renders / firstRenderMs / lastRenderMs / terminalMs）、内容面（cardsRendered / arrived /
        //     isFinalSeen / sameKeyMaxCards）、口径面（终值那一发的四个数 vs 运行结果的四个数，直接可比）。
        //     反控① 的判据 = `renders>=2 && firstRenderMs < terminalMs`（比**时刻**，不只数次数）。
        //     反控⑤ 的选择 = `finalOrderKept=arrival`（终值后**保持到达序**，理由见证据：不切序以免用户正在看的卡片跳位）。
        SearchLog.Write($"SELFCHECK-INCREMENTAL renders={_incRenders} firstRenderMs={_incFirstRenderMs}"
            + $" lastRenderMs={_incLastRenderMs} terminalMs={r?.ElapsedMs} cardsRendered={_incVms?.Count ?? 0}"
            + $" arrived={_incArrivedSources} isFinalSeen={_incFinalSeen} sameKeyMaxCards={_incMaxCardsPerKey}"
            + $" capped={_incCapped} dropped={_incDropped} cap={IncMaxRenderedCards}"
            + $" finalTotalHits={_incFinalTotalHits} finalMergedCount={_incFinalMergedCount}"
            + $" finalMergedHitCount={_incFinalMergedHitCount} finalItemsCount={_incFinalItemsCount}"
            + $" resultTotalHits={r?.TotalHits} resultMergedCount={r?.MergedCount}"
            + $" resultMergedHitCount={r?.MergedHitCount} resultShown={r?.Cards.Count}"
            + $" finalEqualsResult={_incFinalTotalHits == (r?.TotalHits ?? -1) && _incFinalMergedCount == (r?.MergedCount ?? -1) && _incFinalMergedHitCount == (r?.MergedHitCount ?? -1) && _incFinalItemsCount == (r?.Cards.Count ?? -1)}"
            + " finalOrderKept=arrival");

        SearchLog.Write($"SELFCHECK-IMAGES cards={r?.Cards.Count} emptyUrl={_imagesEmptyUrl}"
            + $" opened={_imagesOpened} failed={_imagesFailed}"
            + $" byteRequested={_imagesByteRequested} byteFetched={_imagesByteFetched} byteNull={_imagesByteNull}"
            + $" boundNow={_lastCardVms.Count(v => v.Image != null)}/{_lastCardVms.Count}"
            + $" byteBatchPartitionOk={_imagesByteRequested + _imagesLastBatchEmpty == _imagesLastBatchCards}"
            + $" incrementalMode={_incVms != null}"
            + $" wholeSetPartitionOk={_incVms == null && _imagesByteRequested + _imagesEmptyUrl == (r?.Cards.Count ?? 0)}"
            + $" openedLEFetched={_imagesOpened <= _imagesByteFetched} renderWaitMs={renderWaitMs}");

        // ⑤ 去抖路径：再走一次**非 immediate** 的输入，量"输入 → 发请求 → 出结果"的真实间隔
        //    （第一版这里量出 0 ms：等的是 _inFlight，而去抖还没触发 ⇒ 什么也没等到；改为等结果时刻）
        _last = null;
        _lastRequestAt = 0;
        _lastResultAt = 0;
        var t0 = Environment.TickCount64;
        SetQuery(raw, immediate: false);
        await WaitForResultAsync(TimeSpan.FromSeconds(90));
        SearchLog.Write($"SELFCHECK-DEBOUNCE inputToRequestMs={_lastRequestAt - t0}"
            + $" inputToResultMs={_lastResultAt - t0} debounceMs={DebounceMs}"
            + $" networkRequests={_runner.NetworkRequests}");

        // ⑤ t288 点击接线取证（`SHELL_SELFTEST_SEARCH_CLICK=<序号|itemId>`）：
        //     自检**不模拟鼠标**，而是调**真点击同一个动作点** `ActivateCard`（= `OnCardClick` 的全部内容）
        //     ⇒ 「鼠标事件 → OnCardClick」这一跳不在本驱动的覆盖内（见证据件的未实测项，不许写成实测）。
        var clickTarget = Environment.GetEnvironmentVariable(SelfTestClickEnvVar);
        if (!string.IsNullOrWhiteSpace(clickTarget) && _last?.Cards is { Count: > 0 } cards)
        {
            clickTarget = clickTarget.Trim();
            ResultCard target = null;
            if (int.TryParse(clickTarget, out var clickIndex) && clickIndex >= 0 && clickIndex < cards.Count)
            {
                target = cards[clickIndex];
            }
            else
            {
                target = cards.FirstOrDefault(c => c.ItemId == clickTarget);
            }

            SearchLog.Write($"SELFCHECK-CLICK-DRIVER target=\"{clickTarget}\" resolved={target != null}"
                + $" itemId={(target == null ? "-" : target.ItemId)} server=\"{target?.ServerName}\""
                + " path=ActivateCard（与 OnCardClick 同一动作点）");
            ActivateCard(target);
            await Task.Delay(2500);   // 给详情页导航 + 取数留出落日志的时间
        }

        SearchLog.Write("SELFCHECK-END");
    }

    /// <summary>等"拿到一次搜索结果"（不是等 <c>_inFlight</c> —— 去抖期间它还是 null）。</summary>
    private async Task WaitForResultAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_lastResultAt != 0) { return; }
            await Task.Delay(100);
        }
        SearchLog.Write("SELFCHECK-TIMEOUT 等待搜索结果超时");
    }

    /// <summary>结果卡视图模型（WinUI 侧；<see cref="Card"/> 是纯数据）。</summary>
    public sealed class CardVm : INotifyPropertyChanged
    {
        public ResultCard Card { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public ImageSource Image { get; set; }

        /// <summary>字节通道填图后**通知绑定**：<see cref="Image"/> 是普通属性，不通知的话新位图要等下一次整表刷新才出现。</summary>
        public void SetImage(ImageSource image)
        {
            Image = image;
            Raise(nameof(Image));
        }

        /// <summary>服务层给的去重键（= `AggregatedSearchHit.KeyOf`）。UI **不自己算键**，只用它做就地合并。</summary>
        public string DedupeKey { get; set; } = string.Empty;

        /// <summary>就地更新（同一去重键又来了新命中）：只改内容、**不换实例** ⇒ 不闪、不丢滚动位置、不丢已加载的封面。</summary>
        public void UpdateFrom(ResultCard card)
        {
            Card = card;
            Title = card.Title;
            Subtitle = card.Subtitle;
            BadgeText = card.BadgeText ?? string.Empty;
            Raise(nameof(Title));
            Raise(nameof(Subtitle));
            Raise(nameof(BadgeText));
            Raise(nameof(BadgeVisibility));
        }

        public Visibility BadgeVisibility =>
            string.IsNullOrEmpty(BadgeText) ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

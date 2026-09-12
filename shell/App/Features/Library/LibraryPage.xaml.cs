using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIPlayer.Shell.KernelHost;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Player;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AIPlayer.Shell.Features.Library;

/// <summary>
/// 媒体库页：由服务层真实 Emby 数据驱动，并已接「点开即播」。
///
/// 服务层调用面（依据 <c>shell/Services/API_SURFACE.md</c> 与源码）：
///   · <c>EmbyService(ShellHttpClient, ServerConfig, …)</c> —— 按服务器构造
///   · <c>Task&lt;List&lt;EmbyUserView&gt;&gt; GetViewsAsync(ct)</c>（EmbyService.cs:215）—— 媒体库根视图
///   · <c>Task&lt;EmbyQueryResult&gt; GetItemsAsync(parentId, …, limit, enableImages, imageTypeLimit, …)</c>（EmbyService.cs:223）
///   · <c>ImageUrl(itemId, type, maxHeight, …)</c> —— 封面
///   · <c>EmbyPlaybackSession.BuildRequestAsync(...)</c> → <c>PlaybackRequest</c> —— **唯一合法取流路径**
/// 服务器来源：<c>ServerConfigStore.Instance</c>（**必须**用 Instance：显式 `At(...)` 不触发原版
/// accounts.json 回退 ⇒ 0 台 —— 2026-09-11 实测）。
///
/// 播放链路（**t27-F-A 重做后**）：条目点击 → `EmbyPlaybackSession.BuildRequestAsync`
/// （内含 `NeedsTranscode` 判据，**不用** `PlaybackUrlResolver` —— 它的 Auto 分支永不落转码）
/// → `KernelArgumentBuilder.FromPlayback` 组装 41 参数 → `KernelLauncher` 启动内核（独立进程）
/// → 内核经 <c>--callback-url</c> 回调 ⇒ <see cref="OnKernelNavigationCallback"/> 用
/// `EmbyPlaybackSession.ResolveNavigationAsync` 换源并回 **camelCase 响应体**。
///
/// 自检钩子（默认关闭＝零影响；**都不打印 URL**，避免把 `api_key` 带进日志）：
///   · <c>SHELL_SELFTEST_LIBRARY</c> → `SELFTEST-LIBRARY OK views=… view=… items=… first=…`
///   · <c>SHELL_SELFTEST_PLAY</c>    → 自动挑首个可播条目走完整播放链路并记录解析/启动结果
///   · <c>SHELL_SELFTEST_REPLY=1</c> → 合成导航回调打到**我们的回调端点**，验证响应体逻辑与
///                                     「空体 ≠ {}」的反控（写进 `Tests/evidence/`，不进外壳日志）
/// </summary>
public sealed partial class LibraryPage : Page
{
    public const string SelfTestEnvVar = "SHELL_SELFTEST_LIBRARY";

    public const string PlaySelfTestEnvVar = "SHELL_SELFTEST_PLAY";

    /// <summary>
    /// 详情页自检（默认关闭）：设了它 ⇒ 库视图载入后**程序化打开第一个可播条目的详情页**。
    /// 为什么需要它：详情页是"选集/换源"的落点，但它的完整取数面（条目 + 季 + 集 + 播放源）
    /// **不需要用户点一次才能被验证** —— 这条钩子让验证不依赖鼠标输入（本项目不驱动用户输入）。
    /// 日志锚：`DETAIL-SELFTEST open …` 与详情页自己的 `DETAIL loaded id=… sources=… seasons=… episodes=…`。
    /// </summary>
    public const string DetailSelfTestEnvVar = "SHELL_SELFTEST_DETAIL";

    /// <summary>见 <see cref="DetailSelfTestEnvVar"/>：程序化进详情页（不驱动鼠标、不改配置）。</summary>
    /// <summary>取证钩子（默认关闭）：库名子串或序号，选中该库而不是排序后的第一个。</summary>
    public const string ViewPickEnvVar = "SHELL_SELFTEST_LIBRARY_VIEW";

    /// <summary>
    /// t292（A）：**一页多少条**（改前是"一次性 `limit=200` 的硬顶" ⇒ 617 条的库只看到前 200 条，
    /// 用户截图右下「条目 200 / 617」即此）。改后按页取、滚到近底自动续取，直到服务端给出终态。
    /// </summary>
    private const int PageSize = 100;

    /// <summary>t292（A）：距底还剩几行就触发下一页（判据读数落在 `LIBRARY page-trigger … thresholdPx=`）。</summary>
    private const int LoadMoreRowsRemaining = 2;

    /// <summary>
    /// t292（A）**滚动驱动钩子**（默认关闭＝零影响）：`SHELL_SELFTEST_LIBRARY_SCROLL=&lt;步数&gt;` ⇒
    /// 每 1.5 s 程序化把条目列表滚到底一次，共 N 步（本项目不驱动鼠标 ⇒ 用钩子代替手动滚动，
    /// 与 `SHELL_SELFTEST_*` 系列同一形态）。收尾打印 `LIBRARY scroll-selftest end pages=… cumulative=…`。
    /// </summary>
    public const string ScrollSelfTestEnvVar = "SHELL_SELFTEST_LIBRARY_SCROLL";

    /// <summary>
    /// t292（B）**取证钩子**（默认关闭＝零影响）：`SHELL_SELFTEST_LIBRARY_IMG=1` ⇒ 每张卡落一行**三件并列**读数
    /// <c>LIBRARY img-3way itemId=… type=… src=… displayLogical=… scalePct=… displayPhysical=… request=… cacheKey=… url=…</c>。
    /// <para>**`returnedPx` 不在本进程解**：本进程只落 <c>cacheKey</c>（`ImageCacheManager.NormalizeKey(url)`，公开单点），
    /// 服务端实返尺寸由**外部仪器**在隔离根缓存里按 <c>cacheKey</c> 量（文件名 = SHA1(key) 的十六进制 + `.cache`，
    /// `DiskCacheStore.PathFor`）⇒ 与"本进程怎么解码"无关，是一组**独立读数**。</para>
    /// <para>与文件头纪律一致（"都不打印 URL，避免把 `api_key` 带进日志"）：本行是唯一打印 URL 的钩子，
    /// 且 url 一律过 <see cref="UrlWithoutKey"/> 把 `api_key` 的值抹成 `***`。</para>
    /// </summary>
    public const string ImgProbeEnvVar = "SHELL_SELFTEST_LIBRARY_IMG";

    /// <summary>见 <see cref="ImgProbeEnvVar"/>。</summary>
    private static bool ImgProbeOn
        => string.Equals(Environment.GetEnvironmentVariable(ImgProbeEnvVar), "1", StringComparison.Ordinal);

    /// <summary>把 URL 里 <c>api_key</c> 的**值**抹成 <c>***</c>（日志不出密钥）；没有该参数则原样返回。</summary>
    internal static string UrlWithoutKey(string url)
    {
        if (string.IsNullOrEmpty(url)) { return url ?? string.Empty; }

        var i = url.IndexOf("api_key=", StringComparison.OrdinalIgnoreCase);
        if (i < 0) { return url; }

        var end = url.IndexOf('&', i);
        return url.Substring(0, i) + "api_key=***" + (end < 0 ? string.Empty : url.Substring(end));
    }

    /// <summary>t77 读数：实返条目的类型计数，形如 `Series:45` / `Movie:60,Episode:3`（按类型名排序，可复现）。</summary>
    private static string TypeCounts(IEnumerable<EmbyItem> items)
    {
        return string.Join(",",
            items.Where(i => i != null)
                 .GroupBy(i => string.IsNullOrEmpty(i.Type) ? "<none>" : i.Type)
                 .OrderBy(g => g.Key, StringComparer.Ordinal)
                 .Select(g => g.Key + ":" + g.Count()));
    }

    private void MaybeAutoOpenDetailSelfTest(List<EmbyItem> items)
    {
        var flag = Environment.GetEnvironmentVariable(DetailSelfTestEnvVar);
        if (string.IsNullOrEmpty(flag)) { return; }

        // 取值语义：`1` = 自动挑（**优先剧集**，因为剧集才走得到"季/集"那条取数面）；
        //           其它值 = 当成**条目 id** 直接打开（便于把验证指向任意条目）。
        if (!string.Equals(flag.Trim(), "1", StringComparison.Ordinal))
        {
            var explicitId = flag.Trim();
            Program.Log("DETAIL-SELFTEST open-explicit id=" + explicitId);
            OpenDetail(new LibraryTile { Item = new EmbyItem { Id = explicitId, Name = explicitId }, Title = explicitId });
            return;
        }

        var series = TryPickSeries();
        var candidate = series ?? PickPlayCandidate(items);
        if (candidate == null)
        {
            Program.Log("DETAIL-SELFTEST skip no-playable-item");
            return;
        }

        Program.Log("DETAIL-SELFTEST open id=" + candidate.Id + " name=" + candidate.Name
            + " type=" + candidate.Type + " picked=" + (series != null ? "series" : "fallback"));
        OpenDetail(new LibraryTile { Item = candidate, Title = candidate.Name });
    }

    /// <summary>
    /// 自检专用：向服务端要**一条剧集**（`includeItemTypes=Series`）。
    /// 为什么要它：`PickPlayCandidate` 只会挑电影，而"选集/换源"的季/集取数面**只有剧集走得到** ——
    /// 没有这条，`DETAIL loaded … seasons=0 episodes=0` 会把"没验证到"伪装成"通过"。
    /// 失败只记日志、返回 null（自检不该改变主流程）。
    /// </summary>
    private EmbyItem TryPickSeries()
    {
        try
        {
            var result = _emby?.GetItemsAsync(includeItemTypes: "Series", limit: 1).GetAwaiter().GetResult();
            return result?.Items?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL-SELFTEST series-probe-fail " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>合成导航回调自检（默认关闭）。见 <see cref="RunCallbackReplySelfTestAsync"/>。</summary>
    public const string ReplySelfTestEnvVar = "SHELL_SELFTEST_REPLY";

    /// <summary>`SHELL_SELFTEST_REAL_NAV=1`：播放自检优选**剧集**（电影没有下一集 ⇒ 真回调永远不会到）。</summary>
    public const string RealNavSelfTestEnvVar = "SHELL_SELFTEST_REAL_NAV";

    private EmbyService _emby;
    private List<EmbyUserView> _views = new List<EmbyUserView>();
    private bool _started;

    // ── t292（A）翻页状态（换库即重置；单飞 + 去重 + 显式终态）──────────────────────────────
    /// <summary>条目源：`ObservableCollection` ⇒ **追加一页不重建整表**（改前是 `ItemsSource = tiles` 整表替换）。</summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<LibraryTile> _items =
        new System.Collections.ObjectModel.ObservableCollection<LibraryTile>();

    /// <summary>已落表的卡片（与 <see cref="_items"/> 同源；供只读统计/读数用）。</summary>
    private readonly List<LibraryTile> _tiles = new List<LibraryTile>();

    /// <summary>已见过的条目 id（**去重**用：同一页被请求两次、或服务端跨页重发同一 id 时只落一次）。</summary>
    private readonly System.Collections.Generic.HashSet<string> _seenIds =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

    private string _currentViewId = string.Empty;

    private string _currentViewName = string.Empty;

    private string _currentIncludeTypes = string.Empty;

    /// <summary>下一次请求的 `StartIndex`（只在**取数成功后**推进 ⇒ 失败不会漏页/重页）。</summary>
    private int _nextStartIndex;

    private int _pageCount;

    private int _lastTotal;

    private bool _hasMore = true;

    private bool _ended;

    /// <summary>单飞闸：同一时刻只允许一个取页在飞（防"快速滚到底"把同一页请求两次）。</summary>
    private bool _loadInFlight;

    private ScrollViewer _itemsScroll;

    private double _rowStridePx;

    /// <summary>当前播放会话（换源回调要用它解析"下一条源"）。</summary>
    private EmbyPlaybackSession _session;

    /// <summary>换源串行闸（回调在线程池线程上跑；Emby 会话内部状态不可并发推进）。</summary>
    private readonly System.Threading.SemaphoreSlim _navigateGate =
        new System.Threading.SemaphoreSlim(1, 1);


    public LibraryPage()
    {
        InitializeComponent();
        // 回调接缝注册在**页面构造**时（此时 ShellCallback 已 StartOnce）：
        // 解析器内部判 `_session == null` ⇒ 无可换源目标时回空体，与"没注册"外在行为一致。
        ShellCallback.NavigateHandler = OnKernelNavigationCallback;
        Loaded += LibraryPage_Loaded;

        // t292（A）：条目源**只绑一次**（`ObservableCollection` ⇒ 追加一页不重建整表、不跳滚动位）；
        // 滚动触底检测要等 GridView 模板实例化（`Loaded`）之后才拿得到内部 ScrollViewer。
        ItemGrid.ItemsSource = _items;
        ItemGrid.Loaded += (s, e) => HookItemsScroll();
    }

    private async void LibraryPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await LoadViewsAsync();
    }

    private async Task LoadViewsAsync()
    {
        try
        {
            // 主源选择：① 用户最近选过的那台（`AppSettings.LastServerId` —— 由侧栏点服务器行写入）；
            //           ② 回落 = 第一台启用的 Emby（原行为）。
            // 依据：`UI_SPEC_SHELL.md:142 [实测]`「点侧栏某台服务器 ⇒ 以该服务器为主源」——
            // 没有这一步，"切换服务器"在库视图里就永远停在第一台（用户 2026-09-12 报障）。
            var preferredId = AIPlayer.Shell.Services.Settings.SettingsService.Instance?.Settings?.LastServerId ?? string.Empty;
            var candidates = ServerConfigStore.Instance.Servers?
                .Where(s => s.Enabled && s.Kind == ServerKind.Emby)
                .OrderBy(s => s.SortIndex)
                .ToList() ?? new System.Collections.Generic.List<ServerConfig>();

            var server = candidates.FirstOrDefault(s => string.Equals(s.Id, preferredId, StringComparison.Ordinal))
                ?? candidates.FirstOrDefault();

            if (server == null)
            {
                StatusText.Text = "没有启用的 Emby 服务器（去「服务器」页添加或启用）";
                Program.Log("LibraryPage: no enabled Emby server");
                return;
            }

            _emby = new EmbyService(new ShellHttpClient(), server);
            _views = await _emby.GetViewsAsync();

            // 排序：真实媒体库优先，`playlists`/`folders` 这类容器垫底。
            // 依据（2026-09-11 实测）：真机 ServerA 返回 17 个库，第 1 个恰是「播放列表」且条目为 0，
            // 若直接 SelectedIndex=0 会让人误判「媒体库是空的」。
            ViewList.ItemsSource = _views
                .OrderBy(ViewRank)
                .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(v => new LibraryViewTile { Id = v.Id, Name = v.Name, CollectionType = v.CollectionType })
                .ToList();

            StatusText.Text = "服务器：" + server.Name + "｜媒体库 " + _views.Count + " 个";

            // t292 取证钩子（默认关闭）：把所有库视图的「名字 / collectionType / 解析出的 IncludeItemTypes」落一行，
            // 让"按类型选图"的映射表有**全库面**依据（原只有被选中那一个库的 `LIBRARY rows:` 行）。
            if (ImgProbeOn)
            {
                foreach (var v in _views.OrderBy(ViewRank).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Program.Log("LIBRARY view-probe name=" + v.Name
                        + " collectionType=" + (string.IsNullOrEmpty(v.CollectionType) ? "<none>" : v.CollectionType)
                        + " includeItemTypes=" + EmbyViewTypes.ItemTypesForView(v));
                }
            }

            if (ViewList.Items.Count > 0)
            {
                // 选中会触发 SelectionChanged ⇒ 走 LoadItemsAsync
                ViewList.SelectedIndex = PickViewIndex();
            }
            else
            {
                StatusText.Text += "（该服务器未返回任何库）";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "媒体库读取失败：" + ex.GetType().Name;
            Program.Log("LibraryPage.LoadViews failed: " + ex);
            FailSelfTestIfRequested(ex);
        }
    }

    /// <summary>
    /// 取证钩子（默认关闭）：`SHELL_SELFTEST_LIBRARY_VIEW=<库名子串 | 序号>` ⇒ 选中那个库。
    /// 为什么需要：验收「媒体库类型过滤生效（两种类型对照）」要对**两个不同的库**各截一帧，
    /// 而本项目不驱动鼠标 ⇒ 没有这个钩子就只截得到"排序后的第一个库"。
    /// 找不到匹配时**回落 0 并明确打一行**（不静默降级）。
    /// </summary>
    private int PickViewIndex()
    {
        var raw = (Environment.GetEnvironmentVariable(ViewPickEnvVar) ?? string.Empty).Trim();
        if (raw.Length == 0) { return 0; }

        if (int.TryParse(raw, out var byIndex) && byIndex >= 0 && byIndex < ViewList.Items.Count)
        {
            Program.Log("LIBRARY-VIEW-PICK byIndex=" + byIndex);
            return byIndex;
        }

        for (var i = 0; i < ViewList.Items.Count; i++)
        {
            if (ViewList.Items[i] is LibraryViewTile candidate
                && !string.IsNullOrEmpty(candidate.Name)
                && candidate.Name.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Program.Log("LIBRARY-VIEW-PICK byName='" + raw + "' index=" + i + " name=" + candidate.Name
                    + " type=" + (candidate.CollectionType ?? "<none>"));
                return i;
            }
        }

        Program.Log("LIBRARY-VIEW-PICK fallback raw='" + raw + "' -> 0（没有任何库名匹配）");
        return 0;
    }

    private async void ViewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_emby == null || ViewList.SelectedItem is not LibraryViewTile tile)
        {
            return;
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 按库类型过滤：不加这个，「剧场版」这类库的首屏会混进 Studio/Genre/Folder 条目
            // （2026-09-11 实测：`firstType=Studio`）。排序固定按 SortName 升序，保证列表顺序可复现。
            // 过滤表**委托服务层单点**（`EmbyViewTypes`，未知类型 ⇒ 混合白名单而**不是** null：
            // null 会让 recursive 查询把整库 Episode 泄出来）。本页那份私有 switch 已删。
            var includeItemTypes = EmbyViewTypes.ItemTypesForView(tile.CollectionType);

            // t292（A）：换库 = **翻页状态归零**（去重集合一起清，避免"换个库就少几条"的串味）。
            _currentViewId = tile.Id;
            _currentViewName = tile.Name;
            _currentIncludeTypes = includeItemTypes;
            _seenIds.Clear();
            _tiles.Clear();
            _items.Clear();
            _nextStartIndex = 0;
            _pageCount = 0;
            _lastTotal = 0;
            _ended = false;
            _hasMore = true;

            // 第一页（改前这一发就是 limit=200 的"一次性"取数；改后 limit=PageSize 并允许续取）。
            var items = await LoadNextPageAsync("first");
            if (items.Count == 0 && _items.Count == 0)
            {
                StatusText.Text = "库：" + tile.Name + "｜这个库里没有可显示的条目";
            }

            // t77 读数行（一条日志给全四件）：请求的类型过滤 / 实返类型计数 / 徽标非空条数 / 排序参数。
            Program.Log("LIBRARY rows:view=" + tile.Name
                + " collectionType=" + (tile.CollectionType ?? "<none>")
                + " includeItemTypes=" + (includeItemTypes ?? "<null>")
                + " sortBy=SortName sortOrder=Ascending"
                + " limit=" + PageSize
                + " returned=" + items.Count
                + " types=" + TypeCounts(items)
                + " badgeNonEmpty=" + _tiles.Count(t => !string.IsNullOrEmpty(t.Badge))
                + " elapsed=" + sw.ElapsedMilliseconds + "ms");

            // t292（B）读数：**请求尺寸 vs 显示框**——判"请求是否匹配显示**物理**像素"的那一行。
            // 三件并列：缩放（百分比整数，避免文化差异的小数分隔符）｜显示框逻辑值｜实际请求值。
            var reqSize = PhysicalRequestSize();
            Program.Log("LIBRARY img-req scalePct=" + (int)Math.Round((XamlRoot?.RasterizationScale ?? 1.0) * 100)
                + " logical=" + CardWidth + "x" + CardHeight
                + " request=" + reqSize.W + "x" + reqSize.H);

            // t292（B）分支汇总（**每次载入一行**，取代"每卡一行"的噪声）：四条支 = own-poster / series-poster /
            // own-letterbox / none；letterbox 计数与 `shares` 里的分支互证（两者不一致就是映射写歪了）。
            Program.Log("LIBRARY poster-src shares=" + SourceShares(_tiles)
                + " letterbox=" + _tiles.Count(t => t.ImageStretch == Microsoft.UI.Xaml.Media.Stretch.Uniform));

            // t292（A）：首屏之后就按"滚到近底"续取（`LIBRARY page-trigger` + `LIBRARY page` 两行读数）。
            StartScrollSelfTestIfRequested();

            RunSelfTestIfRequested(tile, items);
            await MaybeAutoPlaySelfTestAsync(items);
            MaybeAutoOpenDetailSelfTest(items);
        }
        catch (Exception ex)
        {
            StatusText.Text = "条目读取失败：" + ex.GetType().Name;
            Program.Log("LibraryPage.LoadItems failed: " + ex);
            FailSelfTestIfRequested(ex);
        }
    }

    /// <summary>
    /// t292（A）：取**一页**并落表 —— 单飞（`_loadInFlight`）+ 去重（`_seenIds` 按条目 id）+ **显式终态**。
    /// <para>终态判据（三条，缺一不可）：① 实返 0 条 ⇒ `empty`；② `TotalRecordCount &gt; 0` 且已取满 ⇒ `total-reached`；
    /// ③ 实返少于 <see cref="PageSize"/> ⇒ `page-short`。**不把 `TotalRecordCount == 0` 当"没有更多"的判据**
    /// （该端在这条查询上恒回 0 ⇒ 那会把第一页就判成到底）。</para>
    /// <para>`StartIndex` 只在**取数成功后**推进 ⇒ 失败/异常不会漏页，也不会把同一页重复请求。</para>
    /// </summary>
    private async Task<List<EmbyItem>> LoadNextPageAsync(string reason)
    {
        if (_loadInFlight || !_hasMore || _ended || _emby == null || string.IsNullOrEmpty(_currentViewId))
        {
            Program.Log("LIBRARY page skip reason=" + reason
                + " why=" + (_loadInFlight ? "in-flight" : (_ended || !_hasMore ? "ended" : "no-view")));
            return new List<EmbyItem>();
        }

        _loadInFlight = true;
        var startIndex = _nextStartIndex;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _emby.GetItemsAsync(
                parentId: _currentViewId,
                includeItemTypes: _currentIncludeTypes,
                // t292（A）：**从第 startIndex 条开始取一页**（服务层 `GetItemsAsync` 本就有 `startIndex` 形参，
                // 只是本页从前没传 ⇒ 永远只取开头 200 条）。
                startIndex: startIndex,
                limit: PageSize,
                // t77②「集数徽标」：`RecursiveItemCount`（集数）**必须显式进 Fields**，
                // 实测（2026-09-12 直连 Emby）：不写进 Fields ⇒ 该字段是空；写进去即得 16/66/16/16/20。
                // `ChildCount` 作回落，`ProductionYear/PremiereDate` 供"年份"副行。
                fields: "RecursiveItemCount,ChildCount,ProductionYear,PremiereDate,ImageTags",
                sortBy: "SortName",
                sortOrder: "Ascending",
                enableImages: true,
                imageTypeLimit: 1);

            var items = result?.Items ?? new List<EmbyItem>();
            var fresh = new List<LibraryTile>();
            var dup = 0;
            foreach (var it in items)
            {
                if (it == null) { continue; }
                if (!string.IsNullOrEmpty(it.Id) && !_seenIds.Add(it.Id)) { dup++; continue; }
                fresh.Add(ToTile(it));
            }

            _tiles.AddRange(fresh);
            foreach (var t in fresh) { _items.Add(t); }
            _pageCount++;
            _nextStartIndex = startIndex + items.Count;

            var total = result?.TotalRecordCount ?? 0;
            string endReason = null;
            if (items.Count == 0) { endReason = "empty"; }
            else if (total > 0 && _nextStartIndex >= total) { endReason = "total-reached"; }
            else if (items.Count < PageSize) { endReason = "page-short"; }

            Program.Log("LIBRARY page reason=" + reason
                + " start=" + startIndex + " limit=" + PageSize
                + " returned=" + items.Count + " fresh=" + fresh.Count + " dup=" + dup
                + " cumulative=" + _items.Count + " pages=" + _pageCount
                + " nextStart=" + _nextStartIndex
                + " totalRecordCount=" + (total > 0 ? total.ToString() : "0(该端不返回)")
                + " elapsed=" + sw.ElapsedMilliseconds + "ms");

            if (endReason != null) { EndPaging(endReason); }
            else { UpdateStatusText(total); }

            // 图片后填（与首页同一条通道）：非打包 WinUI3 里 `BitmapImage(远程 URL)` 会报 E_NETWORK_ERROR，
            // 所以走"自取字节 → ImageSourceLoader"；`LibraryTile` 已实现 INPC ⇒ **按张通知，不重建整表**。
            if (fresh.Count > 0) { _ = FillTileImagesAsync(fresh); }

            return items;
        }
        catch (Exception ex)
        {
            // 取页失败：**不静默**（记一行 + 停掉后续自动翻页），已加载的内容留着。
            Program.Log("LIBRARY page FAIL reason=" + reason + " start=" + startIndex
                + " " + ex.GetType().Name + ": " + ex.Message);
            EndPaging("error");
            return new List<EmbyItem>();
        }
        finally
        {
            _loadInFlight = false;
        }
    }

    /// <summary>
    /// t292（A）：**显式终态** —— 落一行 `LIBRARY END reason=…`、把状态栏改成"已全部加载"、此后不再请求。
    /// 幂等（重复调用只记第一次）。
    /// </summary>
    private void EndPaging(string reason)
    {
        if (_ended) { return; }

        _ended = true;
        _hasMore = false;
        Program.Log("LIBRARY END reason=" + reason + " cumulative=" + _items.Count + " pages=" + _pageCount);
        UpdateStatusText(_lastTotal);
    }

    /// <summary>状态栏读数（用户可见的"条目 N / 总数｜可继续加载 / 已全部加载"）。</summary>
    private void UpdateStatusText(int total)
    {
        if (total > 0) { _lastTotal = total; }

        // 服务端在这条查询上恒回 TotalRecordCount=0（该端实现如此）⇒ 0 一律读作"未返回"，
        // 不写成「25 / 0」那种看着像"25 条里 0 条"的假数量。
        StatusText.Text = "服务器：" + (_emby?.Server?.Name ?? "?")
            + "｜库：" + _currentViewName
            + "｜条目 " + _items.Count + " / " + (_lastTotal > 0 ? _lastTotal.ToString() : "未返回")
            + (_ended ? "｜已全部加载" : "｜滚动继续加载");
    }

    /// <summary>
    /// t292（A）：把 GridView 模板里的 `ScrollViewer` 找出来并挂 `ViewChanged`（近底续取）。
    /// 行高**实测**（首个已实现容器的 `ActualHeight`）而不是猜常量 ⇒ 判据里的 `thresholdPx` 有依据。
    /// </summary>
    private void HookItemsScroll()
    {
        if (_itemsScroll != null) { return; }

        _itemsScroll = FindDescendant<ScrollViewer>(ItemGrid);
        if (_itemsScroll == null)
        {
            Program.Log("LIBRARY page-hook FAIL find=scrollviewer（滚到底不会续取）");
            return;
        }

        _rowStridePx = (ItemGrid.ContainerFromIndex(0) as FrameworkElement)?.ActualHeight ?? (CardHeight + 16.0);
        _itemsScroll.ViewChanged += OnItemsViewChanged;
        Program.Log("LIBRARY page-hook ok rowStridePx=" + (int)Math.Round(_rowStridePx)
            + " thresholdRows=" + LoadMoreRowsRemaining
            + " thresholdPx=" + (int)Math.Round(LoadMoreRowsRemaining * _rowStridePx)
            + " scrollable=" + (int)Math.Round(_itemsScroll.ScrollableHeight)
            + " viewport=" + (int)Math.Round(_itemsScroll.ViewportHeight));
    }

    private void OnItemsViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_itemsScroll == null || _loadInFlight || !_hasMore || _ended) { return; }

        var remaining = _itemsScroll.ScrollableHeight - _itemsScroll.VerticalOffset;
        var threshold = LoadMoreRowsRemaining * _rowStridePx;
        if (remaining > threshold) { return; }

        Program.Log("LIBRARY page-trigger where=scroll remainingPx=" + (int)Math.Round(remaining)
            + " thresholdPx=" + (int)Math.Round(threshold)
            + " offset=" + (int)Math.Round(_itemsScroll.VerticalOffset)
            + " scrollable=" + (int)Math.Round(_itemsScroll.ScrollableHeight)
            + " cumulative=" + _items.Count);
        _ = LoadNextPageAsync("scroll");
    }

    /// <summary>可视化树里找第一个 <typeparamref name="T"/>（GridView 内部 ScrollViewer 只在模板实例化后存在）。</summary>
    private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) { return null; }

        var n = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) { return hit; }

            var deeper = FindDescendant<T>(child);
            if (deeper != null) { return deeper; }
        }

        return null;
    }

    /// <summary>
    /// t292（A）滚动驱动（默认关闭）：`SHELL_SELFTEST_LIBRARY_SCROLL=&lt;步数&gt;` ⇒ 每 1.5 s 程序化滚到底一次。
    /// 本项目不驱动鼠标/键盘 ⇒ 用钩子驱动滚动，让"滚动续取"在无人值守的臂里也能被跑出来。
    /// </summary>
    private void StartScrollSelfTestIfRequested()
    {
        var raw = Environment.GetEnvironmentVariable(ScrollSelfTestEnvVar);
        if (!int.TryParse(raw, out var steps) || steps <= 0)
        {
            // 非自检路径：首屏若没填满视口（scrollable=0）也要能续取 —— 首次布局后再查一次。
            _ = Task.Delay(800).ContinueWith(_ => DispatcherQueue.TryEnqueue(CheckViewportFill), TaskScheduler.Default);
            return;
        }

        Program.Log("LIBRARY scroll-selftest begin steps=" + steps);
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < steps; i++)
            {
                await Task.Delay(1500);
                var idx = i + 1;
                DispatcherQueue.TryEnqueue(() =>
                {
                    var sv = _itemsScroll;
                    if (sv == null)
                    {
                        Program.Log("LIBRARY scroll-selftest step=" + idx + " no-scrollviewer");
                        return;
                    }

                    var before = sv.VerticalOffset;
                    sv.ChangeView(null, sv.ScrollableHeight, null);
                    Program.Log("LIBRARY scroll-selftest step=" + idx
                        + " from=" + (int)Math.Round(before)
                        + " to=" + (int)Math.Round(sv.ScrollableHeight)
                        + " cumulative=" + _items.Count + " pages=" + _pageCount + " hasMore=" + _hasMore);
                });
            }

            await Task.Delay(1500);
            DispatcherQueue.TryEnqueue(() =>
                Program.Log("LIBRARY scroll-selftest end pages=" + _pageCount
                    + " cumulative=" + _items.Count + " hasMore=" + _hasMore + " ended=" + _ended));
        });
    }

    /// <summary>视口没被第一页填满时（`ScrollableHeight == 0`）也续取 —— 否则短视口/大屏上"没得滚"就永远只有一页。</summary>
    private void CheckViewportFill()
    {
        if (_itemsScroll == null || _loadInFlight || !_hasMore || _ended) { return; }
        if (_itemsScroll.ScrollableHeight > 0) { return; }

        Program.Log("LIBRARY page-trigger where=viewport-unfilled scrollable=0 cumulative=" + _items.Count);
        _ = LoadNextPageAsync("viewport");
    }

    private void ItemGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LibraryTile tile)
        {
            // 用户 2026-09-12 报障「没有媒体详情页 没有地方选集和换源」⇒ 点卡片 = **进详情页**，
            // 播放动作移到详情页发起（选集 / 换源都在那里）。失败才回落到旧的"直接播"。
            OpenDetail(tile);
        }
    }

    /// <summary>点卡片 → 进详情页（`Features/Detail`）；详情页不可用时回落到直接起播（不把用户卡住）。</summary>
    private void OpenDetail(LibraryTile tile)
    {
        if (tile?.Item == null) { return; }

        try
        {
            AIPlayer.Shell.Shell.ShellState.Current.CurrentTag = Features.Detail.DetailPage.NavTag;
            Program.Log("Nav -> detail id=" + tile.Item.Id + " name=" + tile.Item.Name);
            Frame?.Navigate(typeof(Features.Detail.DetailPage), tile.Item);
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL NAV-FAIL " + ex.GetType().Name + ": " + ex.Message);
            _ = PlayAsync(tile);
        }
    }

    /// <summary>「点开即播」：走 `EmbyPlaybackSession.BuildRequestAsync`（含转码判据）→ 41 参数 → 启动内核（独立进程）。</summary>
    private async Task PlayAsync(LibraryTile tile)
    {
        if (_emby == null || tile?.Item == null)
        {
            return;
        }

        try
        {
            // 设置必须已加载：外壳目前不构造 ServiceRegistry（`Initialize()` 才 Load）。
            // 不加载 ⇒ AppSettings 全是默认值 ⇒ 代理/快捷键/画面/音量/连播都会静默用错值，故此处显式兜一下。
            if (!SettingsService.Instance.IsLoaded)
            {
                SettingsService.Instance.Load();
            }

            var settings = SettingsService.Instance.Settings;
            var session = CreateSession(settings);
            session.SetItem(tile.Item);

            // [!] 唯一合法取流路径（`PlaybackUrlResolver` 的 Auto 分支永不落 Transcode，会静默丢转码）
            var request = await session.BuildRequestAsync(tile.Item);
            if (request == null || string.IsNullOrEmpty(request.MediaPath))
            {
                Program.Log("PLAY FAIL no-url item=" + tile.Item.Name + " type=" + tile.Item.Type);
                StatusText.Text = "无法解析播放地址：" + tile.Title;
                return;
            }

            _session = session;

            var launch = KernelArgumentBuilder.FromPlayback(
                request,
                settings,
                callbackUrl: ShellCallback.Url,
                libMpvPath: KernelLauncher.LibMpvPath,
                parentPid: Environment.ProcessId);

            // **参数面读数**（验收面）：`DescribeParameterSurface` 给出逐类计数（params=/badges=/headers=…），
            // 完整参数串由紧随其后的 `KernelLauncher.Launch` 打（`KERNEL-LAUNCH … args=`，走唯一打码真源）——
            // 此前那行"再打一份完整参数"（`PLAY-KERNEL-ARGS-RAW`）已按 t247 删除，理由见下。
            Program.Log("PLAY resolved item=" + tile.Item.Name
                + " needTranscode=" + session.NeedsTranscode
                + " urlLen=" + request.MediaPath.Length
                + " " + KernelArgumentBuilder.DescribeParameterSurface(launch));
            // [!] 这里**曾经**再打一行完整参数（`PLAY-KERNEL-ARGS-RAW <args>`）—— 已删除，不再打印（t247）。
            // 理由（与 `DetailPage.xaml.cs:1140` 同形处置）：
            //   ① **冗余**：紧随其后的 `KernelLauncher.Launch` 会打 `KERNEL-LAUNCH exe=… args=<masked>`，
            //      同一串参数在同一秒出现两份；
            //   ② **诊断信息不丢**：参数面读数在上一条 `PLAY resolved … DescribeParameterSurface`（params=NN …）里，
            //      完整参数在 `KERNEL-LAUNCH … args=` 里（走唯一打码真源，`api_key=***` / `--http-header=<masked …>`）；
            //   ③ 本行**当刻并不是明文出口**（`Program.Log` 已下沉到打码 sink）—— 删它是为去重，不是为堵漏，
            //      历史代际的明文读数见证据件（`Features/Library/` 侧，t247）。

            var process = KernelLauncher.Launch(launch, Program.Log);

            StatusText.Text = process == null
                ? "内核启动失败（见日志）"
                : "已启动内核 PID " + process.Id + "：" + tile.Title;

            // ⚡ ④ 凭据面语义修正：内核 `AppViewModel.cs:296` 会把 `--open=` 原样写进 `LastLocalVideoPath`。
            //    给它一点落盘时间再修正（同步等一下比"猜时间"确定；内核是独立进程，不占我们的线程）。
            await Task.Delay(1500);
            LocalVideoPathSemantic.RestoreAfterPlayback(Program.Log);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReplySelfTestEnvVar)))
            {
                await RunCallbackReplySelfTestAsync();
            }

            // ⑤ 内核日志打码收尾自检（默认零影响）：在"内核在跑"与"内核已停"两种情形下各取一次实测
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHELL_SELFTEST_MASK")))
            {
                _ = KernelLogMasker.RunExitMaskSelfTestIfRequestedAsync();
            }
        }
        catch (Exception ex)
        {
            Program.Log("PLAY FAIL " + ex.GetType().FullName + ": " + ex.Message);
            StatusText.Text = "播放失败：" + ex.GetType().Name;
        }
    }

    /// <summary>按当前服务器 + 设置构造播放会话（TODB/跳过片段服务由会话内部按需用）。</summary>
    private EmbyPlaybackSession CreateSession(AppSettings settings)
    {
        var http = new ShellHttpClient();
        return new EmbyPlaybackSession(
            _emby,
            settings,
            segmentService: new AIPlayer.Shell.Services.Segments.SegmentService(http),
            todbService: new AIPlayer.Shell.Services.Todb.TodbService(http),
            onLog: m => Program.Log("SESSION " + m));
    }

    /// <summary>
    /// **回调接缝（t27-F-A ①）**：内核发来导航类事件时解析"下一条源"并回 camelCase 响应体。
    /// 返回 <c>null</c> ⇒ 端点回**空体**（内核保持现状，不换源）。
    /// ⚠️ 本方法在**端点线程**上执行（见 `InboundCallbackEndpoint.AcceptLoopAsync`）⇒ 不得碰 UI；
    /// 会话状态用信号量串行化。
    /// </summary>
    private HostNavigateOptions OnKernelNavigationCallback(NavigateReplyRequest reply)
    {
        var session = _session;
        if (session == null)
        {
            Program.Log("CALLBACK-NAV no-session event=" + reply.EventName + " -> empty-body");
            return null;
        }

        if (!reply.Event.AcceptsSourceReply)
        {
            return null;
        }

        var gateTaken = false;
        try
        {
            gateTaken = _navigateGate.Wait(TimeSpan.FromSeconds(30));
            if (!gateTaken)
            {
                Program.Log("CALLBACK-NAV busy event=" + reply.EventName + " -> empty-body");
                return null;
            }

            // ⚠️ 内核导航回调的 HTTP 超时预算是 90 s（T12_CALLBACK_SURFACE §6.1）⇒ 这里给足 60 s。
            var next = session.ResolveNavigationAsync(reply.Event).GetAwaiter().GetResult();
            if (next == null || string.IsNullOrWhiteSpace(next.MediaPath))
            {
                Program.Log("CALLBACK-NAV " + reply.EventName + " resolved-none -> empty-body");
                return null;
            }

            // 换源应答必须带 startPosition（原版 `toNavigateOptions` 就是带的，规格 §1.1 末段）
            var options = next.ToNavigateOptions(
                callbackUrl: ShellCallback.Url,
                currentEpisodeId: session.Item?.Id,
                resumePosition: reply.Event.PositionSeconds);

            Program.Log("CALLBACK-NAV " + reply.EventName + " resolved=ok mediaLen="
                + (options.MediaPath?.Length ?? 0) + " startPosition=" + (options.StartPosition?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"));

            // 下一条源里含新签发的 token ⇒ 会话换到新条目，槽语义修正的时机也更新
            LocalVideoPathSemantic.GuardBeforeLaunch(Program.Log);
            return options;
        }
        catch (Exception ex)
        {
            Program.Log("CALLBACK-NAV FAIL " + reply.EventName + " " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
        finally
        {
            if (gateTaken)
            {
                _navigateGate.Release();
            }
        }
    }

    /// <summary>
    /// **回调响应体自检（默认关闭）**：合成导航回调（`navigate_next` 与 `progress`）**打到我们自己的端点**，
    /// 逐条记录状态码与**原始响应体**，并把「空体 ≠ `{}`」的反控一并测掉。
    ///
    /// 为什么要"打自己的端点"而不是直接调解析器：只有走完
    /// `<c>POST /callback</c> → 路由 → 响应体 → HTTP 状态行` 才证明**线上形态**是对的
    /// （直接调函数会绕过端点把 `205`/空体/`{}` 的事实全部跳过）。
    ///
    /// 证据落到 <c>shell/Tests/evidence/t27-callback-reply.txt</c>（UTF-8 无 BOM）：
    /// ⚠️ **扩展名必须是 `.txt`** —— `.gitignore:65` 有 `*.log` 规则，用 `.log` 写的证据**不进仓库**
    /// （本队实测踩过两次）。
    /// 响应体里含**新签发的 token**，按纪律**只存证据文件、不进外壳日志**。
    /// </summary>
    private async Task RunCallbackReplySelfTestAsync()
    {
        var url = ShellCallback.Url;
        if (string.IsNullOrEmpty(url))
        {
            Program.Log("SELFTEST-REPLY FAIL no-endpoint");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== t27-F-A ① 导航回调响应体自检 ===");
        sb.AppendLine("endpoint  = " + url);
        sb.AppendLine("utc       = " + DateTime.UtcNow.ToString("O"));
        sb.AppendLine("item      = " + (_session?.Item?.Name ?? "<none>") + " (" + (_session?.Item?.Id ?? "?") + ")");
        sb.AppendLine("episodes  = " + (_session?.Episodes?.Count ?? 0));
        sb.AppendLine();

        // ①-a **真会话解析**：直接走 `_session.ResolveNavigationAsync`，证明"解析器是通的、能产出下一条源"。
        //     （端点那一侧用的是同一个解析器 ⇒ 两条证据合起来才是完整的"回调 → 响应体"。）
        sb.AppendLine("=== 真会话解析（EmbyPlaybackSession.ResolveNavigationAsync，不经端点）===");
        sb.AppendLine("resolved  = " + ResolveForProbe());
        sb.AppendLine();

        // ①-b **线上形态**：把合成回调打到**我们自己的端点**（走完路由/序列化/状态行），记原始响应体。
        await PostAndRecordAsync(sb, url,
            "navigate_next",
            "{\"event\":\"navigate_next\",\"positionSeconds\":42.5}");
        await PostAndRecordAsync(sb, url,
            "progress",
            "{\"event\":\"progress\",\"positionSeconds\":100.25,\"durationSeconds\":1434,\"isPaused\":false}");
        await PostAndRecordAsync(sb, url,
            "navigate_previous",
            "{\"event\":\"navigate_previous\",\"positionSeconds\":7}");

        // 反控 ①：**空体**是正确形态（内核读成 null = 没有新选项）
        // 反控 ②：**非空但无 MediaPath** 也必须回空体（回 `{}` 会让 progress 侧把空对象当预载数据占住）
        // 反控 ③：**形状错**（camelCase 缺失 / segments.type 是数字）⇒ 必须能被人眼在下面断言里看出为 False
        var real = ShellCallback.NavigateHandler;
        try
        {
            ShellCallback.NavigateHandler = _ => null;
            await PostAndRecordAsync(sb, url, "navigate_next[anti1-handler-null]",
                "{\"event\":\"navigate_next\",\"positionSeconds\":1}");

            ShellCallback.NavigateHandler = _ => new HostNavigateOptions();
            await PostAndRecordAsync(sb, url, "navigate_next[anti2-no-mediaPath]",
                "{\"event\":\"navigate_next\",\"positionSeconds\":1}");

            ShellCallback.NavigateHandler = _ => new HostNavigateOptions
            {
                MediaPath = "https://example.invalid/anti3?api_key=<redacted>",
                Title = "反控③：形状必须可判",
            };
            await PostAndRecordAsync(sb, url, "navigate_next[anti3-valid-shape]",
                "{\"event\":\"navigate_next\",\"positionSeconds\":1}");
        }
        finally
        {
            ShellCallback.NavigateHandler = real;
        }

        sb.AppendLine();
        sb.AppendLine("=== 线载荷形状断言（正控：形状探针）===");
        var probe = new HostNavigateOptions
        {
            MediaPath = "https://example.invalid/stream?api_key=<redacted>",
            Title = "形状探针",
            StartPosition = 12.5,
            HttpHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Emby-Token"] = "<redacted>" },
            Segments = new List<MediaSegmentDto> { new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 12000, EndMs = 102000, Source = "emby" } },
            AudioTracks = new List<HostTrackOption> { new HostTrackOption { Label = "日语", Id = "1", EmbyIndex = 1, Selected = true } },
        };
        var json = ShellCallback.BuildNavigateJson(probe);
        sb.AppendLine("json      = " + json);
        sb.AppendLine("A camelCase mediaPath          = " + json.Contains("\"mediaPath\""));
        sb.AppendLine("A camelCase startPosition      = " + json.Contains("\"startPosition\""));
        sb.AppendLine("A camelCase httpHeaders        = " + json.Contains("\"httpHeaders\""));
        sb.AppendLine("A camelCase audioTracks        = " + json.Contains("\"audioTracks\""));
        sb.AppendLine("A segments[].type 是字符串      = " + json.Contains("\"type\":\"intro\"") + "   （反控：若为 \"type\":0 ⇒ 内核反序列化抛异常 ⇒ 整条应答变 null）");
        sb.AppendLine("A 计算属性未上线（start/end/isValid）= " + (!json.Contains("\"isValid\"") && !json.Contains("\"\\\"start\\\":")));

        // ③ `ReadRequestAsync` 字节/字符混淆：**8190 / 8191 / 8192 三种偏移**的字节级实测。
        //    三种偏移的必要性：读块 = 8192 字节 ⇒
        //      0 ≤ 8190：`\r\n\r\n` 与 Body 的**前两字节**同在第一块尾、`Content-Length` 剩余字节在**最后一次 read 才到**；
        //      8191    ：`\r\n\r\n` **跨块**（前 3 字节在第一块尾、`\n` 在第二块首）；
        //      8192    ：头恰好占满一整块（`\r\n\r\n` 是块内最后 4 字节）。
        //    反控：正文特意含中文（字符数≠字节数，正是原缺陷的受害面）。
        sb.AppendLine();
        sb.AppendLine("=== ③ ReadRequestAsync 字节边界（8190/8191/8192 头长偏移）===");
        await RunReadRequestBoundarySelfTestAsync(sb, url);

        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "evidence");
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, "t27-callback-reply.txt");
            System.IO.File.WriteAllText(file, sb.ToString(), new System.Text.UTF8Encoding(false));
            Program.Log("SELFTEST-REPLY OK evidence=" + System.IO.Path.GetFullPath(file));
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-REPLY evidence-write FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        StatusText.Text += "｜SELFTEST-REPLY 见 shell/Tests/evidence/t27-callback-reply.txt";
    }

    /// <summary>真会话解析探针（只回**形态摘要**，不回 URL/token）。</summary>
    private string ResolveForProbe()
    {
        var session = _session;
        if (session == null)
        {
            return "<no-session>";
        }

        try
        {
            var next = session
                .ResolveNavigationAsync(new HostEvent(HostEventKind.NavigateNext))
                .GetAwaiter().GetResult();

            if (next == null || string.IsNullOrWhiteSpace(next.MediaPath))
            {
                return "<null-or-empty-mediaPath>（本条目无下一集 ⇒ 端点按「空体」应答，属正确形态）";
            }

            return "ok mediaLen=" + next.MediaPath.Length
                + " startPosition=" + next.StartPosition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " audio=" + next.AudioTracks.Count
                + " subtitle=" + next.SubtitleTracks.Count
                + " segments=" + next.Segments.Count
                + " httpHeaders=" + next.HttpHeaders.Count;
        }
        catch (Exception ex)
        {
            return "<EXCEPTION " + ex.GetType().Name + ": " + ex.Message + ">";
        }
    }

    /// <summary>对端点发一条真实 HTTP POST，把**状态行 + 原始响应体**记进证据。</summary>
    private static async Task PostAndRecordAsync(StringBuilder sb, string url, string label, string body)
    {
        try
        {
            // 自检专用（非生产取数路径）：它是**发 POST 给本机回调端点**，而 `ShellHttpClient` 当刻只有
            // `GetAsync`（无 POST）⇒ 换过去编译不过（CS1061）。要清零需服务层补 POST，不属本卡范围。
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            using var content = new System.Net.Http.StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, content);
            var text = await response.Content.ReadAsStringAsync();
            sb.AppendLine("--- " + label + " ---");
            sb.AppendLine("request   = " + body);
            sb.AppendLine("status    = " + (int)response.StatusCode + " " + response.ReasonPhrase);
            sb.AppendLine("bodyLen   = " + text.Length);
            sb.AppendLine("body      = " + (text.Length == 0 ? "<EMPTY>" : text));
            sb.AppendLine();
            Program.Log("SELFTEST-REPLY " + label + " status=" + (int)response.StatusCode + " bodyLen=" + text.Length);
        }
        catch (Exception ex)
        {
            sb.AppendLine("--- " + label + " ---");
            sb.AppendLine("EXCEPTION = " + ex.GetType().FullName + ": " + ex.Message);
            sb.AppendLine();
            Program.Log("SELFTEST-REPLY " + label + " FAIL " + ex.GetType().Name);
        }
    }

    /// <summary>
    /// **③ `ReadRequestAsync` 字节/字符缺陷自检** —— 按 `shell/docs/VERIFY_PLAN_T35_T41.md` **第 8 行**重做。
    ///
    /// <para>判据原文：**`Content-Length=8400`**、**3 字节 CJK 起于 8190 / 8191 / 8192** ⇒ 解码正确；
    /// **ASCII 对照必须同过**；[!] **ASCII 与 CJK 都过 ⇒ 用例没打在分帧上 ⇒ 判"用例无效"，不是判通过**。</para>
    ///
    /// <para>**为什么第一版被这条判据否掉**（自我记录，避免重犯）：第一版把 `\r\n\r\n` 顶到 8190/8191/8192，
    /// 打的是**头边界**；读块 = 8192 ⇒ 头边界贴近读边界时**根本没有多字节字符被拆开** ⇒
    /// ASCII 版与 CJK 版都会"通过" —— 正是判据要防的"用例无效"形态。
    /// 本版把分帧点真正压到**正文里的 CJK 字节**上：头用填充顶到 3×8192−2 = **24574**，
    /// ⇒ 首个读块（8192）吃掉"头 8190 + `\r`"，第二块开头是 `\n\r\n`，正文整段始于 **3×8192**，
    /// CJK/ASCII 标记位于正文偏移 8190/8191/8192 ⇒ **块边界 8192 落在正文偏移 8192 处，正切标记**。</para>
    ///
    /// <para>**两种正文都发**（CJK 与 ASCII），并用**可判别差异**证明用例真的打在分帧上：
    /// 同一偏移下 CJK 版正文 92 字符 / 94 字节，ASCII 版 94 字符 / 94 字节（同字节数、不同字符数）
    /// ⇒ 端点读到的是**同样的字节数**、解码出的**字符数不同** ⇒ 差异必然来自"字节被正确按长度切、再按 UTF-8 解码"。</para>
    /// </summary>
    private async Task RunReadRequestBoundarySelfTestAsync(StringBuilder sb, string url)
    {
        var uri = new Uri(url);

        // 头长：让正文整段始于 3 × 8192（首个读块 + 首个补读块之后）
        var headerLen = 3 * 8192 - 2;

        sb.AppendLine("判据（VERIFY_PLAN_T35_T41 第 8 行）：Content-Length=" + FramingProbeClient.ContentLengthPerCriterion
            + "；CJK 起于 8190/8191/8192；ASCII 对照必须同过");
        sb.AppendLine("头长 = " + headerLen + "（= 3×8192−2 ⇒ 正文整段始于字节 " + (3 * 8192) + "，读块边界 8192 落在正文偏移 8192）");
        sb.AppendLine();

        var rows = new List<(int Offset, FramingProbeClient.ProbeResult Cjk, FramingProbeClient.ProbeResult Ascii)>();

        foreach (var offset in new[] { 8190, 8191, 8192 })
        {
            var cjk = FramingProbeClient.Send(uri.Host, uri.Port, uri.AbsolutePath, offset, withCjk: true, headerLen);
            var ascii = FramingProbeClient.Send(uri.Host, uri.Port, uri.AbsolutePath, offset, withCjk: false, headerLen);
            rows.Add((offset, cjk, ascii));

            sb.AppendLine("--- 标记起于正文偏移 " + offset + " ---");
            sb.AppendLine("  CJK   : status='" + cjk.StatusLine + "' 发送字节=" + cjk.BodyByteLen
                + " 端点读到字符=" + (cjk.BodyText?.Length ?? 0)
                + " 事件名='" + _endpointEventName(cjk) + "' U+FFFD=" + cjk.HasReplacementChar
                + (cjk.Error == null ? string.Empty : " ERR=" + cjk.Error));
            sb.AppendLine("  ASCII : status='" + ascii.StatusLine + "' 发送字节=" + ascii.BodyByteLen
                + " 端点读到字符=" + (ascii.BodyText?.Length ?? 0)
                + " 事件名='" + _endpointEventName(ascii) + "' U+FFFD=" + ascii.HasReplacementChar
                + (ascii.Error == null ? string.Empty : " ERR=" + ascii.Error));
            sb.AppendLine("  端点最近一次解析的正文长度(字符) = " + (ShellCallback.Endpoint?.LastInnerBody?.Length ?? -1)
                + "  含 U+FFFD = " + ((ShellCallback.Endpoint?.LastInnerBody ?? string.Empty).IndexOf('\uFFFD') >= 0));
            sb.AppendLine();
        }

        // 断言（逐条 True/False，不写"通过"就完事）
        var allOk = rows.All(r => r.Cjk.StatusLine.Contains("204") && r.Ascii.StatusLine.Contains("204")
            && !r.Cjk.HasReplacementChar && r.Cjk.BodyByteLen == FramingProbeClient.ContentLengthPerCriterion);
        sb.AppendLine("断言 三偏移 × (CJK,ASCII) 全部 204 = " + allOk);
        sb.AppendLine("断言 Content-Length 恒为 " + FramingProbeClient.ContentLengthPerCriterion + " = "
            + rows.All(r => r.Cjk.BodyByteLen == FramingProbeClient.ContentLengthPerCriterion
                && r.Ascii.BodyByteLen == FramingProbeClient.ContentLengthPerCriterion));
        sb.AppendLine("断言 CJK 正文无 U+FFFD（未被按字符拆坏）= " + rows.All(r => !r.Cjk.HasReplacementChar));

        // [!] 判据的"用例有效性"反控：同偏移下 CJK/ASCII **字符数必须不同**（字节数相同）
        var distinguishable = rows.All(r => r.Cjk.BodyText.Length != r.Ascii.BodyText.Length);
        sb.AppendLine("断言 用例打在分帧上（同偏移 CJK 与 ASCII 字符数不同）= " + distinguishable
            + "   ← False 则按判据判「用例无效」，不得判通过");
        sb.AppendLine();
    }

    private static string _endpointEventName(FramingProbeClient.ProbeResult r)
        => r.Error == null ? (ShellCallback.Endpoint?.LastEventName ?? "<null>") : "<err>";

    private static int ViewRank(EmbyUserView view)
    {
        switch ((view.CollectionType ?? string.Empty).ToLowerInvariant())
        {
            case "movies":
                return 0;
            case "tvshows":
                return 1;
            case "boxsets":
                return 2;
            case "music":
                return 3;
            case "homevideos":
                return 4;
            case "books":
                return 5;
            case "audiobooks":
                return 6;
            case "playlists":
                return 98;
            case "folders":
                return 99;
            default:
                return 50;
        }
    }

    /// <summary>
    /// 图片后填：并发度由**全壳共享单点** <see cref="AIPlayer.Shell.Shell.ImageFillBudget"/> 决定
    /// （t292 归一：本屏原来是自留的 `new SemaphoreSlim(6)` —— 那是**第三份**取图闸门；现改走单点，
    /// 与首页/详情同一口径，默认预算 8、`SHELL_HOME_IMG_BUDGET` 可覆盖）。单张 8s 有界，
    /// 任何一张失败只少一张图；填完再刷一次 ItemsSource。
    /// 与首页 <c>HomePage.FillTileImagesAsync</c> 同一形态（同一字节通道、同一超时口径、**同一预算源**）。
    /// </summary>
    private async Task FillTileImagesAsync(List<LibraryTile> tiles)
    {
        if (tiles.Count == 0) { return; }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ok = 0;
        await Task.WhenAll(tiles.Select(async tile =>
        {
            // t292：闸门租约在**每张**体内获取（共享单点是"进闸 → 用完释放"的一次性租约，
            // 不是旧的 `SemaphoreSlim.WaitAsync/Release` 那种可反复借还的形态）。
            using (await AIPlayer.Shell.Shell.ImageFillBudget.AcquireAsync())
            {
                tile.Image = await LoadImageAsync(tile.ImageUrl, tile);
                if (tile.Image != null) { System.Threading.Interlocked.Increment(ref ok); }
            }
        }));

        Program.Log("LIBRARY IMG-OK " + ok + "/" + tiles.Count + " elapsed=" + sw.ElapsedMilliseconds + "ms"
            // t292：归一后把共享单点的四读数一并落盘（与首页 `HOME IMG-OK … budget=/inFlightMax=/gateWaits=/completed=` 同口径）
            + " budget=" + AIPlayer.Shell.Shell.ImageFillBudget.Budget
            + " inFlightMax=" + AIPlayer.Shell.Shell.ImageFillBudget.InFlightMax
            + " gateWaits=" + AIPlayer.Shell.Shell.ImageFillBudget.GateWaits
            + " completed=" + AIPlayer.Shell.Shell.ImageFillBudget.Completed);
        // t292（A）：不再 `ItemsSource = null; = tiles;` 整表重建 —— `LibraryTile` 已实现 INPC，
        // 图到达时只通知**这一张卡**（与首页 `PosterTile` 同一形态；也避免追加一页时滚动位被重置）。
    }

    /// <summary>图片：自己取字节 → 既有 <c>ImageSourceLoader</c>（控件直连远程 URL 会 E_NETWORK_ERROR）。</summary>
    private async Task<ImageSource> LoadImageAsync(string url, LibraryTile tile = null)
    {
        if (string.IsNullOrEmpty(url)) { return null; }

        try
        {
            var bytes = await AIPlayer.Shell.Services.Infra.ImageCacheManager.Default.GetOrFetchAsync(
                AIPlayer.Shell.Services.Infra.ImageCacheManager.NormalizeKey(url),
                async () =>
                {
                    var http = new AIPlayer.Shell.Services.Http.ShellHttpClient();
                    var r = await http.GetAsync(url, timeout: TimeSpan.FromSeconds(8));
                    return r.Bytes;
                });
            if (bytes == null || bytes.Length == 0) { return null; }
            if (ImgProbeOn && tile != null) { LogImageTripleReading(tile, url, bytes.Length); }
            return await AIPlayer.Shell.Features.Aggregate.Shared.ImageSourceLoader.LoadAsync(bytes);
        }
        catch (Exception ex)
        {
            Program.Log("LIBRARY IMG-FETCH-FAIL " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// t292（B）三件并列读数（只在 <see cref="ImgProbeEnvVar"/>=1 时打印）：**显示框**（逻辑 150×225 → 物理 ×scale）
    /// ｜**请求**（同一组物理值，即实际发出去的 `maxWidth/maxHeight`）｜**服务端实返**（本行只给 `cacheKey` + 字节数，
    /// 像素由外部仪器按 `cacheKey` 在缓存里量，见 <see cref="ImgProbeEnvVar"/> 注释）。
    /// </summary>
    private void LogImageTripleReading(LibraryTile tile, string url, int byteCount)
    {
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        var req = PhysicalRequestSize();
        var physicalW = (int)Math.Round(CardWidth * scale, MidpointRounding.AwayFromZero);
        var physicalH = (int)Math.Round(CardHeight * scale, MidpointRounding.AwayFromZero);
        var key = AIPlayer.Shell.Services.Infra.ImageCacheManager.NormalizeKey(url);

        Program.Log("LIBRARY img-3way itemId=" + (tile.Item?.Id ?? "-")
            + " type=" + (string.IsNullOrEmpty(tile.Item?.Type) ? "-" : tile.Item.Type)
            + " src=" + (string.IsNullOrEmpty(tile.ImageSourceKind) ? "-" : tile.ImageSourceKind)
            + " displayLogical=" + CardWidth + "x" + CardHeight
            + " scalePct=" + (int)Math.Round(scale * 100)
            + " displayPhysical=" + physicalW + "x" + physicalH
            + " request=" + req.W + "x" + req.H
            + " bytes=" + byteCount
            + " cacheKey=" + key
            + " url=" + UrlWithoutKey(url));
    }

    /// <summary>
    /// t292（B）**按 item 类型选图种 + 选框内摆法**（2:3 竖框本身不动 —— 几何归 t287/t271）。
    ///
    /// <para>**为什么按类型分**（Emby 的素材比例是类型的函数）：`Movie` / `Series` / `BoxSet` / `Season` 的 `Primary`
    /// 就是 **2:3 竖海报**（框同比例 ⇒ 填满且几乎零裁切）；`Episode` / `Video` / `MusicVideo` 的 `Primary` 是
    /// **16:9 截帧**（`UniformToFill` 进 2:3 框 ⇒ 横裁 ≈62% —— 用户报的"16:9 做成竖屏"）；`MusicAlbum` / `Audio`
    /// 是 **1:1 方图**（横裁 ≈33%）。</para>
    ///
    /// <para>**选图链**（`Episode` 那一支照抄 `HomePage.SeriesImageStub` 的判据，不重造）：<br/>
    /// ① 2:3 原生类型 ⇒ 自家 `Primary`，摆法 `UniformToFill`（填满）；<br/>
    /// ② 其它类型且**主剧集有竖海报**（`SeriesId` + `SeriesPrimaryImageTag`，且与原 id 不同）⇒ **换 id/tag 取剧集海报**，
    ///    摆法 `UniformToFill`；<br/>
    /// ③ 其余（16:9 / 1:1 素材）⇒ 仍用自家 `Primary`，但摆法 **`Uniform`（留边不裁）** —— 这就是
    ///    "**不得把 16:9 硬塞进 2:3 框**"的落点：素材照放，但不 `UniformToFill` 硬裁 62%（与 t263 对首页 16:9 行的处置同向）；<br/>
    /// ④ 连自家 `Primary` 都没有 ⇒ **空串**（不发请求、不造 500），摆法同 `Uniform`。</para>
    ///
    /// <para>摆法落在 <see cref="LibraryTile.ImageStretch"/>（XAML 的 `Image.Stretch` 绑它）；分支名（`own-poster` /
    /// `series-poster` / `own-letterbox` / `none`）落在 <see cref="LibraryTile.ImageSourceKind"/> 并**每次载入汇总一行**
    /// `LIBRARY poster-src shares=…`。</para>
    /// </summary>
    private (string Url, string Src, Microsoft.UI.Xaml.Media.Stretch Fit) CardImageFor(EmbyItem item, int reqW, int reqH)
    {
        var type = item?.Type ?? string.Empty;

        if (PosterNativeTypes.Contains(type))
        {
            var ownPoster = _emby.ImageUrlIfAvailable(item, "Primary", maxWidth: reqW, maxHeight: reqH);
            return string.IsNullOrEmpty(ownPoster)
                ? (string.Empty, "none", Microsoft.UI.Xaml.Media.Stretch.Uniform)
                : (ownPoster, "own-poster", Microsoft.UI.Xaml.Media.Stretch.UniformToFill);
        }

        var series = SeriesPosterStub(item);
        if (series != null)
        {
            var seriesUrl = _emby.ImageUrlIfAvailable(series, "Primary", maxWidth: reqW, maxHeight: reqH);
            if (!string.IsNullOrEmpty(seriesUrl))
            {
                return (seriesUrl, "series-poster", Microsoft.UI.Xaml.Media.Stretch.UniformToFill);
            }
        }

        var own = _emby.ImageUrlIfAvailable(item, "Primary", maxWidth: reqW, maxHeight: reqH);
        return string.IsNullOrEmpty(own)
            ? (string.Empty, "none", Microsoft.UI.Xaml.Media.Stretch.Uniform)
            : (own, "own-letterbox", Microsoft.UI.Xaml.Media.Stretch.Uniform);
    }

    /// <summary>2:3 原生（竖海报）类型白名单；不在表内的一律按"可能不是 2:3"处理（留边，不硬裁）。</summary>
    private static readonly System.Collections.Generic.HashSet<string> PosterNativeTypes =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Movie", "Series", "BoxSet", "Season" };

    /// <summary>
    /// 「主剧集竖海报」的 stub（**与 `HomePage.SeriesImageStub`（t172/R-1）逐条同判据**：`SeriesId` 与
    /// `SeriesPrimaryImageTag` 都非空、且 `SeriesId != 自家 id`；否则返回 null ⇒ 不构造"同 id 原地重试"的请求）。
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

    /// <summary>t292（B）分支汇总（`own-poster:142,none:4,own-letterbox:54`；按分支名排序 ⇒ 可复现）。</summary>
    private static string SourceShares(IEnumerable<LibraryTile> tiles)
    {
        return string.Join(",",
            tiles.Where(t => t != null)
                 .GroupBy(t => string.IsNullOrEmpty(t.ImageSourceKind) ? "-" : t.ImageSourceKind)
                 .OrderBy(g => g.Key, StringComparer.Ordinal)
                 .Select(g => g.Key + ":" + g.Count()));
    }

    /// <summary>
    /// 卡片（t77②③）。
    /// <para>· <c>Badge</c> = 集数：`RecursiveItemCount`（回落 `ChildCount`）⇒「共 N 集」；两者都没有 ⇒ 显示年份；
    /// 连年份都没有 ⇒ 空串（**不画角标**，也不画一个假的"0 集"）。</para>
    /// <para>· <c>Subtitle</c> = `类型 · 年份`。</para>
    /// <para>· **尺寸契约**：卡片显示区 150x225（2:3，**逻辑**像素），请求**同时**给 `maxWidth/maxHeight` ⇒ 服务端按该框出图
    /// （实测剧集 2:3 海报恰好回 150x225，零裁切；只给 `maxHeight` 会拿回 200x300，**多下 2.7 倍字节**）。
    /// 两处尺寸必须同值：本文件 `CardWidth/CardHeight` 与 `LibraryPage.xaml` 的 `Grid`。</para>
    /// <para>t292（B）**请求改按物理像素**：显示框是逻辑 150×225，而请求原先也用逻辑值 ⇒ 缩放 ≠ 100% 时服务端出图
    /// **小于实际显示像素**，画面必然偏软（用户报"图糊"）。现改为请求 `round(150×scale) × round(225×scale)`；
    /// **框不动**（几何契约归 `t287`/`t271`），只改请求。scale 取 `XamlRoot.RasterizationScale`，
    /// 页未挂载时回落 `1.0` ⇒ 与旧行为逐字一致（不会平白放大）。</para>
    /// </summary>
    private LibraryTile ToTile(EmbyItem item)
    {
        var req = PhysicalRequestSize();
        var pick = CardImageFor(item, req.W, req.H);
        return new LibraryTile
        {
            Item = item,
            Title = string.IsNullOrEmpty(item.Name) ? "(无名)" : item.Name,
            Subtitle = SubtitleLine(item),
            Badge = BadgeOf(item),
            // t292（B）：图种 + 摆法按类型选（见 `CardImageFor`）；空串 = 这条没有可取的图 ⇒ 不发请求。
            ImageUrl = pick.Url,
            ImageSourceKind = pick.Src,
            ImageStretch = pick.Fit,
        };
    }

    /// <summary>
    /// t292（B）：请求用的**物理**像素 = 显示框逻辑尺寸 × `XamlRoot.RasterizationScale`
    /// （`1.0` = 100% 缩放 ⇒ 与旧的 `150×225` 逐字相同）。调用方另落一行 `LIBRARY img-req …` 读数。
    /// </summary>
    private (int W, int H) PhysicalRequestSize()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        return ((int)Math.Round(CardWidth * scale, MidpointRounding.AwayFromZero),
                (int)Math.Round(CardHeight * scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>卡片显示区尺寸（**逻辑**像素；与 `LibraryPage.xaml` 里那个 `Grid` 同值；改一处必须同时改另一处）。</summary>
    private const int CardWidth = 150;
    private const int CardHeight = 225;

    /// <summary>副行：`类型 · 年份`（年份缺失就不写，不补占位符）。</summary>
    private static string SubtitleLine(EmbyItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(item.Type)) { parts.Add(item.Type); }
        var year = YearOf(item);
        if (year.Length > 0) { parts.Add(year); }
        return string.Join(" · ", parts);
    }

    /// <summary>年份：优先 `ProductionYear`，否则取 `PremiereDate` 前 4 位，都没有 ⇒ 空串。</summary>
    private static string YearOf(EmbyItem item)
    {
        if (item.ProductionYear is int year && year > 0) { return year.ToString(); }
        var premiere = item.PremiereDate;
        if (!string.IsNullOrEmpty(premiere) && premiere.Length >= 4) { return premiere.Substring(0, 4); }
        return string.Empty;
    }

    /// <summary>
    /// 角标：剧集「N 集」（**只认 `RecursiveItemCount`**）→ 否则年份 → 否则空串。
    /// <para>[!] 为什么不回落 `ChildCount`：对剧集它是**季数**（不是集数）、对文件夹是子项数 ——
    /// 拿它当"集数"印出来就是**假数**（t149 的 F3）。宁可只显示年份，也不印一个错的集数。</para>
    /// </summary>
    private static string BadgeOf(EmbyItem item)
    {
        if (item.RecursiveItemCount is int recursive && recursive > 0) { return recursive + " 集"; }
        return YearOf(item);
    }

    private void RunSelfTestIfRequested(LibraryViewTile view, List<EmbyItem> items)
    {
        var flag = Environment.GetEnvironmentVariable(SelfTestEnvVar);
        if (string.IsNullOrEmpty(flag))
        {
            return;
        }

        Program.Log("SELFTEST-LIBRARY BEGIN value=" + flag
            + " server=" + (_emby.Server?.Name ?? "?")
            + " views=" + _views.Count
            + " view=" + view.Name);

        if (items.Count == 0)
        {
            Program.Log("SELFTEST-LIBRARY FAIL items=0 view=" + view.Name);
            return;
        }

        Program.Log("SELFTEST-LIBRARY OK views=" + _views.Count
            + " view=" + view.Name
            + " items=" + items.Count
            + " first=" + items[0].Name
            + " firstType=" + items[0].Type);
    }

    /// <summary>自动挑首个可播条目走完整播放链路（判据：解析出 URL + 内核进程起来 + 端点收到回调）。</summary>
    private async Task MaybeAutoPlaySelfTestAsync(List<EmbyItem> items)
    {
        var flag = Environment.GetEnvironmentVariable(PlaySelfTestEnvVar);
        if (string.IsNullOrEmpty(flag))
        {
            return;
        }

        var candidate = PickPlayCandidate(items);

        // 真回调自检：当前库没有剧集时，**跨库**取一集来播（`navigate_next` 只有剧集形态才会真的到）
        if (candidate == null || (IsRealNavSelfTest()
            && !candidate.IsEpisode
            && !string.Equals(candidate.Type, "Episode", StringComparison.OrdinalIgnoreCase)))
        {
            var episode = await TryPickEpisodeAcrossViewsAsync();
            if (episode != null)
            {
                candidate = episode;
            }
        }

        if (candidate == null)
        {
            Program.Log("SELFTEST-PLAY FAIL no-playable-item items=" + items.Count);
            return;
        }

        Program.Log("SELFTEST-PLAY BEGIN value=" + flag
            + " item=" + candidate.Name
            + " type=" + candidate.Type
            + " isPlayable=" + candidate.IsPlayable
            + " realNav=" + (Environment.GetEnvironmentVariable(RealNavSelfTestEnvVar) ?? "0")
            + " callback=" + (ShellCallback.Url ?? "<null>"));

        await PlayAsync(new LibraryTile { Item = candidate, Title = candidate.Name });
    }

    /// <summary>
    /// 播什么：
    ///   · 默认 = 首个 `IsPlayable` 条目（原行为，电影库即命中）；
    ///   · `SHELL_SELFTEST_REAL_NAV=1` 且当前库里**有剧集条目** ⇒ **优选剧集** ——
    ///     只有它是「真的会触发内核 `navigate_next`」的形态（电影没有下一集，回调永远不会到）。
    /// </summary>
    private static EmbyItem PickPlayCandidate(List<EmbyItem> items)
    {
        var realNav = IsRealNavSelfTest();
        if (realNav)
        {
            var episode = items.FirstOrDefault(i => i.IsEpisode)
                ?? items.FirstOrDefault(i => string.Equals(i.Type, "Episode", StringComparison.OrdinalIgnoreCase));
            if (episode != null)
            {
                return episode;
            }
        }

        return items.FirstOrDefault(i => i.IsPlayable)
            ?? items.FirstOrDefault(i => i.IsMovie || i.IsEpisode);
    }

    private static bool IsRealNavSelfTest()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RealNavSelfTestEnvVar));

    /// <summary>
    /// `SHELL_SELFTEST_REAL_NAV=1` 时，跨库找一集来播：
    /// **只有剧集形态**才有 `--episode-list=`（连播/选集依赖）与内核 `navigate_next` 的真实触发路径；
    /// 电影库永远拿不到这条证据（本机实测：movie 库 `episodes=0` ⇒ 真回调永不到）。
    /// 本机 `ServerA` 首个库是电影库，故必须跨库找。
    /// </summary>
    private async Task<EmbyItem> TryPickEpisodeAcrossViewsAsync()
    {
        try
        {
            for (var i = 0; i < _views.Count && i < 6; i++)
            {
                var view = _views[i];
                if (!string.Equals(view.CollectionType, "tvshows", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var result = await _emby.GetItemsAsync(
                    parentId: view.Id,
                    includeItemTypes: "Episode",
                    limit: 5,
                    sortBy: "SortName",
                    sortOrder: "Ascending");

                var episode = (result?.Items ?? new List<EmbyItem>())
                    .FirstOrDefault(x => x.IsEpisode || string.Equals(x.Type, "Episode", StringComparison.OrdinalIgnoreCase));

                if (episode != null)
                {
                    Program.Log("SELFTEST-PLAY realNav picked-episode view=" + view.Name + " item=" + episode.Name);
                    return episode;
                }
            }
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-PLAY realNav pick-episode FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        Program.Log("SELFTEST-PLAY realNav no-episode-view-found views=" + _views.Count);
        return null;
    }

    private void FailSelfTestIfRequested(Exception ex)
    {
        var hasLibraryHook = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar));
        var hasPlayHook = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PlaySelfTestEnvVar));
        if (!hasLibraryHook && !hasPlayHook)
        {
            return;
        }

        Program.Log("SELFTEST-LIBRARY FAIL " + ex.GetType().FullName + ": " + ex.Message);
    }

    /// <summary>左侧库列表项。</summary>
    public sealed class LibraryViewTile
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string CollectionType { get; set; }
    }

    /// <summary>右侧条目卡片（Image 用 ImageSource 而非字符串：避免 {Binding} 对 Image.Source 的隐式转换问题）。
    /// <para>t292（A）：实现 `INotifyPropertyChanged` ⇒ 图片后填时**只通知这一张卡**（改前靠 `ItemsSource = null; = tiles;`
    /// 整表重建，那会让"追加一页"把滚动位顶回去）。</para></summary>
    public sealed class LibraryTile : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        /// <summary>原始条目：播放链路需要它（`PlaybackUrlResolver` 要 `EmbyItem`）。</summary>
        public EmbyItem Item { get; set; }

        public string Title { get; set; }

        public string Subtitle { get; set; }

        /// <summary>服务层给的图片 URL（空串 = 没有可取的图，不请求）；由后填流程消费。</summary>
        public string ImageUrl { get; set; }

        /// <summary>t292（B）：这张卡的图**是按哪条支选出来的**（`own-poster` / `series-poster` / `own-letterbox` / `none`）。
        /// 只进读数行，不参与渲染。</summary>
        public string ImageSourceKind { get; set; }

        /// <summary>t292（B）：框内摆法 —— 2:3 原生素材 `UniformToFill`（填满）；16:9 / 1:1 素材 `Uniform`（留边不裁）。
        /// XAML 里 `Image.Stretch` 绑这个属性（默认 `UniformToFill` 与旧行为一致）。</summary>
        public Microsoft.UI.Xaml.Media.Stretch ImageStretch { get; set; } = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;

        /// <summary>角标文本（t77②）：剧集「共 N 集」/ 否则年份 / 都没有 ⇒ 空串。</summary>
        public string Badge { get; set; } = string.Empty;

        /// <summary>角标显隐：空串 ⇒ `Collapsed`（**不画一个空的黑框**）；用属性而不是转换器，少一个运行期失败点。</summary>
        public Microsoft.UI.Xaml.Visibility BadgeVisibility => string.IsNullOrEmpty(Badge)
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

        private ImageSource _image;

        public ImageSource Image
        {
            get => _image;
            set
            {
                _image = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Image)));
            }
        }
    }
}

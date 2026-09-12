using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Home;

/// <summary>
/// 首页 = **默认落点**（`MainWindow.StartTag()` 无环境变量时进这里）。
///
/// 结构（对着参照物 `refs/original-ui/hl-home-*`）：Hero 大图 + 「继续观看」横排 + 每个媒体库一行。
/// 取数：`GetResumeAsync`（继续观看）→ `GetViewsAsync`（媒体库列表）→ 每库 `GetItemsAsync(parentId)`
/// ⇒ **全部走既有服务层**，不自造通道；点卡片**进详情页**（不在首页直接起播，与媒体库页一致）。
/// 图片走**字节通道**（`ImageSourceLoader`）—— 非打包 WinUI3 里 `BitmapImage(远程 URL)` 会报
/// `E_NETWORK_ERROR`（详情页实测），所以这里从第一行就用对通道。
///
/// 未做（如实）：类型芯片行、Hero 轮播（参照物有「继续观看 + 最新」混合 ≤5 张轮播）、
/// 库内分页（这里每库取 20 条）。这些留给后续批次，不在本页假装已有。
/// </summary>
public sealed partial class HomePage : Page
{
    /// <summary>导航 tag（与 `MainWindow.NavigateTo` 的 `case` 对齐）。</summary>
    public const string NavTag = "home";

    private const int RowSize = 20;      // 每库取多少条（首屏够用；分页未做）
    private const int MaxLibraryRows = 4; // 最多铺几个库的行（首屏速度优先）

    /// <summary>
    /// 图片填充闸门**已归一到共享单点**（t260）：本页不再自留预算 / 信号量 / 计数器，
    /// 一律走 <see cref="AIPlayer.Shell.Shell.ImageFillBudget"/> —— 预算默认 8、`SHELL_HOME_IMG_BUDGET`
    /// 可覆盖（0/非法 ⇒ 回落 8）、四读数 `InFlight / InFlightMax / GateWaits / Completed` 全取自那**一个**
    /// 实现（与 `DetailPage` 同一份）⇒ 同一语义不再有两处实现、行为不可能漂移。
    /// <para>为什么必须**全局**（t152 ④，不是审美问题，是实测缺陷）：① 真实代理下 N 路并发会互相排队，单图变慢；
    /// ② 更硬的：同一张图在不同行重复出现时，两个 writer 同时写同一个缓存文件 ⇒ 实测
    /// `HOME IMG-FETCH-FAIL IOException: The process cannot access the file '…\cache\images\&lt;sha1&gt;.cache'`，
    /// 那张图当场丢失（基线读数 `HOME IMG-OK 19/20`）。
    /// 旧实现是"每次 `FillTileImagesAsync` 各 new 一个 `SemaphoreSlim(6)`" ⇒ 4 行 = 最多 24 张在飞。</para>
    /// </summary>

    /// <summary>
    /// 行集合：**只绑一次**，每行到达就 `Add`（t152 ⑤：旧实现每行都 `ItemsSource = null; = rows;`
    /// ⇒ 整表重建 O(N²)，且把滚动位置与选中态一起重置）。
    /// </summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<HomeRow> _rows = new();

    private bool _rowsBound;

    /// <summary>
    /// 本页**同 URL 只取一次**（t152 ④ 的另一半）：同一张图可能在多行/同一行重复出现，
    /// 重复取 = 白花一次请求，且是上面那条缓存写冲突的直接来源。
    /// </summary>
    private readonly Dictionary<string, ImageSource> _imageByUrl = new(StringComparer.Ordinal);

    private EmbyService _emby;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private void OnReloadClick(object sender, RoutedEventArgs e) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        ReloadButton.IsEnabled = false;
        StatusText.Text = "正在加载…";
        try
        {
            var server = ResolveServer();
            if (server == null)
            {
                StatusText.Text = "没有启用的 Emby 服务器（去「服务器」页添加或启用）";
                FooterText.Text = string.Empty;
                return;
            }

            _emby = new EmbyService(new ShellHttpClient(), server);
            Program.Log("HOME load server=" + server.Name);

            // ① 继续观看（Hero 取第一条，其余铺成横排）
            var resume = await SafeAsync(() => _emby.GetResumeAsync(limit: RowSize), "resume") ?? new List<EmbyItem>();
            if (resume.Count > 0)
            {
                var hero = resume[0];
                HeroTitle.Text = hero.Name;
                // 图片 URL 只认服务层唯一入口 `ImageUrlIfAvailable`（自家 tag → 剧集回退 → 父级 backdrop → 空串），
                // 这里不再手写 ParentBackdropItemId 三元回退（手写版会把「没有 backdrop tag」当成有图，取回 404）。
                var heroUrl = _emby.ImageUrlIfAvailable(hero, "Backdrop", maxWidth: 1600);
                if (!string.IsNullOrEmpty(heroUrl)) { HeroImage.Source = await LoadImageAsync(heroUrl); }
                else { Program.Log("HOME hero-noimage id=" + hero.Id); }

                // t152 ②：继续观看行的条目仍是**单集**，但卡片要**同时**给出「主剧集名（`SeriesName`）」与
                // 「是哪一集（`S01E02 · 集名`，副行）」，图片用**主剧集的图**。
                // t172（R-1）：这里曾只把"意图"写在注释里 —— 实际传的是**原始单集**，而 `ImageUrlIfAvailable`
                // 的出口①「自家 tag 优先」先命中 ⇒ 拿到的是**单集 16:9 截帧**（166×93）塞进 2:3 卡框（166×249）
                // ⇒ 横裁 62.7%（reviewer 实测 4/8 条）。现在由 `SeriesImageStub` 显式换 id/tag，见 `BuildTiles`。
                var tiles = BuildTiles(resume, resumeRow: true);
                ContinueList.ItemsSource = tiles;
                ContinueSection.Visibility = tiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                Program.Log("HOME resume cards=" + tiles.Count + " seriesFallback=" + SeriesFallbackCount(tiles)
                    + " " + ResumeReading(tiles));
                _ = FillTileImagesAsync(tiles);
            }
            else
            {
                // Hero 兜底：没有"继续观看"时用**最新入库**的一条，避免首屏顶部一块空黑
                // （实测：主源 `resume=0` 时 Hero 是空的，用户看到的就是"顶部什么都没有"）。
                var latest = await SafeAsync(() => _emby.GetLatestAsync(limit: 1), "latest") ?? new List<EmbyItem>();
                if (latest.Count > 0)
                {
                    var hero = latest[0];
                    HeroTitle.Text = hero.Name;
                    // 同上：图片 URL 走服务层唯一入口，不手写 backdrop 三元回退。
                    var heroUrl = _emby.ImageUrlIfAvailable(hero, "Backdrop", maxWidth: 1600);
                    if (!string.IsNullOrEmpty(heroUrl)) { HeroImage.Source = await LoadImageAsync(heroUrl); }
                    else { Program.Log("HOME hero-noimage id=" + hero.Id); }
                    Program.Log("HOME hero-fallback latest=" + hero.Name);
                }
                else
                {
                    HeroTitle.Text = string.Empty;
                    Program.Log("HOME hero-empty (no resume / no latest)");
                }
            }

            StatusText.Text = server.Name + "｜继续观看 " + resume.Count + " 条";

            // ② 媒体库行（最多 MaxLibraryRows 个库）
            //    t152 ③：**各行并发发起**（旧实现是 `foreach + await` 串行 ⇒ 16 个库排队，是"太慢"的主因）；
            //            谁先回来谁先绑（`BindRow`），不等其它行、不等图片。
            var views = await SafeAsync(() => _emby.GetViewsAsync(), "views") ?? new List<EmbyUserView>();
            var picked = views.Take(MaxLibraryRows).ToList();
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            var firstRowMs = -1;
            Program.Log("HOME rows:begin views=" + picked.Count + " budget=" + AIPlayer.Shell.Shell.ImageFillBudget.Budget);

            var rowTasks = picked.Select(async view =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Program.Log("HOME rows:view=" + view.Name + " begin");

                // ① 行内类型**走服务层单点** `EmbyViewTypes.ItemTypesForView(view)`（t82 F1）：
                //    旧写法 `isSeriesLib ? "Series" : null` 只对 `tvshows` 库恰好与单点同值，其它库类型传 null（= 无过滤）
                //    ⇒ t77 要防的"同一个库在不同屏上取到不同种类的东西"在首页路径上仍成立。
                var itemTypes = EmbyViewTypes.ItemTypesForView(view);
                Program.Log("HOME row-source view=" + view.Name + " collectionType=" + (view?.CollectionType ?? "-")
                    + " types=" + (string.IsNullOrEmpty(itemTypes) ? "<null=no-filter>" : itemTypes));
                var items = await SafeAsync(
                    () => _emby.GetItemsAsync(
                        parentId: view.Id,
                        includeItemTypes: itemTypes,
                        limit: RowSize,
                        enableImages: true,
                        imageTypeLimit: 1),
                    "items:" + view.Name);
                var list = items?.Items ?? new List<EmbyItem>();
                if (list.Count == 0)
                {
                    Program.Log("HOME rows:view=" + view.Name + " end tiles=0 elapsed=" + sw.ElapsedMilliseconds + "ms");
                    return;
                }

                // 先建卡片、立刻绑行（不等任何图片）⇒ 行先出现，图片随后按**全局预算**并行补齐。
                var tiles = BuildTiles(list);
                BindRow(new HomeRow { Title = view.Name, Tiles = tiles });
                if (firstRowMs < 0)
                {
                    firstRowMs = (int)swAll.ElapsedMilliseconds;
                    Program.Log("HOME row-first ms=" + firstRowMs + " view=" + view.Name);
                }

                Program.Log("HOME bind rows=" + _rows.Count + " view=" + view.Name + " tiles=" + tiles.Count
                    + " elapsed=" + sw.ElapsedMilliseconds + "ms types=" + TypeCounts(list)
                    + " badgeNonEmpty=" + tiles.Count(t => !string.IsNullOrEmpty(t.Badge))
                    + " seriesFallback=" + SeriesFallbackCount(tiles)
                    + " yearSamples=" + YearSamples(list));
                _ = FillTileImagesAsync(tiles);
            }).ToList();

            await Task.WhenAll(rowTasks);

            FooterText.Text = "媒体库 " + _rows.Count + " 行｜共 " + views.Count + " 个库";
            Program.Log("HOME rows:all done rows=" + _rows.Count + " elapsedMs=" + swAll.ElapsedMilliseconds
                + " firstRowMs=" + firstRowMs + " imgInFlightMax=" + AIPlayer.Shell.Shell.ImageFillBudget.InFlightMax);
            Program.Log("HOME loaded resume=" + resume.Count + " views=" + views.Count + " rows=" + _rows.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = "首页加载失败：" + ex.GetType().Name;
            Program.Log("HOME LOAD-FAIL " + ex.GetType().FullName + ": " + ex.Message);
        }
        finally
        {
            ReloadButton.IsEnabled = true;
        }
    }

    /// <summary>单个取数动作失败只记日志、返回 null —— 首页不该因为某个库/某次请求失败整页空白。</summary>
    private async Task<T> SafeAsync<T>(Func<Task<T>> action, string what) where T : class
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Program.Log("HOME STEP-FAIL " + what + " " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>把一行接到界面上：`ItemsSource` **只绑一次**，之后只 `Add`（t152 ⑤）。</summary>
    private void BindRow(HomeRow row)
    {
        if (!_rowsBound)
        {
            LibraryHost.ItemsSource = _rows;
            _rowsBound = true;
        }

        _rows.Add(row);
    }

    /// <summary>类型计数（t152 ① 的读数）：形如 `Series:20` / `Movie:20,Episode:3`。</summary>
    private static string TypeCounts(List<EmbyItem> items)
    {
        var groups = items.Where(i => i != null)
            .GroupBy(i => string.IsNullOrEmpty(i.Type) ? "?" : i.Type)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key + ":" + g.Count());
        return string.Join(",", groups);
    }

    /// <summary>
    /// t152 ② 的读数：逐卡给出 `seriesId / seriesName / 集标识 / 图片 URL 里的 ItemId`
    /// ⇒ 证明"**图 = 主剧集的图**"（URL 的 ItemId == 主剧集 id）且"卡片文字能区分主剧集与是哪一集"。
    /// <para>t294：**逐卡打印上限放开**（原为 `Take(4)` + `+N more`）。理由 = 改前基线必须能逐卡核：
    /// 旧形态只印前 4 张，而"**4 张对、4 张走了小集图**"这个断言恰好落在**没印出来的那 4 张**上
    /// ⇒ 用旧形态永远验证不了它（这是我上一张卡回给 captain 的仪器缺口，本卡负责关掉）。</para>
    /// </summary>
    private static string ResumeReading(List<PosterTile> tiles)
    {
        var parts = new List<string>();
        foreach (var tile in tiles)
        {
            var item = tile.Item;
            parts.Add("[" + (string.IsNullOrEmpty(item.SeriesId) ? "-" : item.SeriesId) + "/"
                + (string.IsNullOrEmpty(item.SeriesName) ? "-" : item.SeriesName) + "/"
                + (string.IsNullOrEmpty(Identity(item)) ? "-" : Identity(item))
                + "/imgItemId=" + ImageItemIdOf(tile.ImageUrl)
                // t196 读数（t207 判据）：本行素材应为 **Backdrop**（16:9 原生），且 ItemId == 主剧集 id。
                + "/imgType=" + ImageTypeOf(tile.ImageUrl)
                // t172 反控①读数：`own=1`（单集自带 Primary tag）且 `imgItemId=SeriesId` ⇒ 证明出口①的自家 tag 优先级被绕过。
                + "/own=" + (HasOwnPrimaryTag(item) ? "1" : "0")
                + "/serTag=" + (string.IsNullOrEmpty(item.SeriesPrimaryImageTag) ? "0" : "1") + "]");
        }

        // t294：原来这里会补一行 `+N more`（配合上游 `Take(4)`）。上限放开后**不再截断**，
        // 故整行必须自述张数，否则"读到的行是不是全量"要靠外部猜（读数件里我给的就是这一行）。
        parts.Add("|printed=" + tiles.Count + "/" + tiles.Count + " cap=none");
        return string.Join(" ", parts);
    }

    /// <summary>条目**自家**是否有 Primary tag（t172 反控①的读数，见 <see cref="ResumeReading"/>）。</summary>
    private static bool HasOwnPrimaryTag(EmbyItem item)
        => item?.ImageTags != null
           && item.ImageTags.TryGetValue("Primary", out var ownTag)
           && !string.IsNullOrEmpty(ownTag);

    /// <summary>从图片 URL 里取 `/Items/{id}/Images/…` 的那个 id（只用于读数；取不到给 `-`）。</summary>
    private static string ImageItemIdOf(string url)
    {
        if (string.IsNullOrEmpty(url)) { return "-"; }

        var m = System.Text.RegularExpressions.Regex.Match(url, @"/Items/([0-9a-zA-Z]+)/Images/");
        return m.Success ? m.Groups[1].Value : "-";
    }

    /// <summary>
    /// 从图片 URL 里取 `/Items/{id}/Images/{type}` 的 type（t196/t207 读数：继续观看行应恒为 `Backdrop`）。
    /// 取不到给 `-`（**不猜**：拿不到类型就是拿不到，读数里显式可见）。
    /// </summary>
    private static string ImageTypeOf(string url)
    {
        if (string.IsNullOrEmpty(url)) { return "-"; }

        var m = System.Text.RegularExpressions.Regex.Match(url, @"/Items/[0-9a-zA-Z]+/Images/([0-9a-zA-Z]+)");
        return m.Success ? m.Groups[1].Value : "-";
    }

    /// <summary>
    /// 本行里"图片 URL 的 ItemId **不是**条目自身 id"的条数（= 借用了主剧集的图）。
    /// 这是 t152 ② 的**反控读数**：它必须"回退机制为真、但不是整行同图"——ui 复算的基线是 `distinctUrls=20 / seriesFallback=1`。
    /// </summary>
    private static int SeriesFallbackCount(List<PosterTile> tiles)
        => tiles.Count(t =>
        {
            if (t.Item == null) { return false; }

            var urlId = ImageItemIdOf(t.ImageUrl);
            return !string.IsNullOrEmpty(urlId) && !string.Equals(urlId, "-", StringComparison.Ordinal)
                && !string.Equals(urlId, t.Item.Id, StringComparison.Ordinal);
        });

    private List<PosterTile> BuildTiles(IEnumerable<EmbyItem> items, bool resumeRow = false)
    {
        var tiles = new List<PosterTile>();
        foreach (var item in items.Where(i => i != null))
        {
            // t308（用户第④⑧条在首页媒体行的根因）：**按行分流取图** ——
            //   继续观看行 = 16:9 横卡（`HomePage.xaml:91` 的 216×121）⇒ `ResumeCardImageUrl` 四级 pin（t294，本次不动）；
            //   媒体行     = 2:3 竖卡（`HomePage.xaml:149` 的 166×249 + `UniformToFill`）⇒ `PrimaryCardImageUrl`
            //                （t172/R-1：单集换主剧集竖海报 + 双上限 166×249）。
            // 旧形态两行共用 `ResumeCardImageUrl` ⇒ 16:9 Backdrop 进 2:3 框，横裁 ≈62.4%、或 2:3 素材被放大 2.06×。
            var row = resumeRow ? "cw" : "media";
            var branch = string.Empty;
            var url = resumeRow ? ResumeCardImageUrl(item) : MediaRowImageUrl(item, out branch);
            LogTileImage(row, item, branch, url);

            tiles.Add(new PosterTile
            {
                Item = item,
                Caption = Caption(item),
                SubCaption = SubCaption(item, resumeRow),
                Badge = Badge(item),
                // 尺寸契约（Captain 验收 B）：卡片显示区 = 166×249（2:3），所以**两个上限都给**。
                // 只给 maxHeight=420 的后果（ui 用直连 Emby 解码实测，`shell/Tests/evidence/ui-image-contract-forensics.txt` §4）：
                //   剧集 → 280×420 / 104,482 B（比例对但体积 2.66×）；单集 → 747×420 / 125,369 B（16:9 塞进 2:3，横向裁 ~62% ⇒ 用户说的"怪"）。
                // 给 maxWidth=166 + maxHeight=249 后：剧集 166×249 / 39,307 B（与卡片完全吻合）；单集 166×93 / 9,516 B（体积降一个量级）。
                // t172（R-1）：**换素材**才是这道题的答案 —— 2:3 竖框里只能放 2:3 的海报。单集自带 Primary（16:9 截帧）
                // 时必须显式改取**主剧集的海报**（`SeriesImageStub`），否则出口①仍会优先发自家 tag ⇒ 横裁 62.7%。
                // 拿不到剧集海报（无 SeriesId / 无 SeriesPrimaryImageTag）时才回落到原行为（自家 tag → 剧集 tag → 空串，不造 500）。
                Row = row,
                ImageUrl = url,
            });
        }

        return tiles;
    }

    /// <summary>
    /// 「继续观看」行的卡片图。**框是 16:9，本行因此只取 16:9 原生素材**。
    ///
    /// <para>**卡框**：`HomePage.xaml:91` 的 `Grid` = **216×121**（t287 由 166×93 提上来，参考帧实测 216 宽 /
    /// 间距 16 / 图区含 4 px 进度条 121）⇒ 常量 <see cref="ResumeCardWidth"/>/<see cref="ResumeCardHeight"/> 同值。
    /// （旧注释里的"166×93"是 t287 之前的几何，已失效。）</para>
    ///
    /// <para>**取法（t294 起）**：**不再借道服务层出口** —— `ResumeCardImageUrl` 直接调
    /// <see cref="EmbyService.ImageUrl"/> 构造 URL。原因是 `ImageUrlIfAvailable` 的 Primary 分支内部为
    /// "**自家 tag 优先于父级**"，那正是"剧集明明有背景图、卡片却显示小集自家图"的机制。改为**四级显式 pin**：</para>
    /// <list type="number">
    /// <item>① 主剧集（父级）Backdrop —— <see cref="SeriesBackdropUrl"/>：必须**换 id 且带父的 tag**，同 id 等于原地重试；</item>
    /// <item>② 单集自家 Backdrop —— <see cref="OwnBackdropUrl"/>：真源是 `BackdropImageTags` 列表；</item>
    /// <item>③ 单集自家 Primary still（Emby 的 Episode Primary **本身就是 16:9 still**）—— <see cref="OwnPrimaryStillUrl"/>，
    /// **仅在自家 tag 存在时**取；</item>
    /// <item>④ 剧集竖海报 2:3 —— <see cref="SeriesPosterUrl"/>。框已 `Uniform` ⇒ **留边**，不再横裁 60%+（t263）。</item>
    /// </list>
    ///
    /// <para>[!] **③ 的口径**（旧注释在这里写宽了）："自家 Primary ⇒ ≈0 裁切"**只在单集自带 tag 时成立**，
    /// 而且它现在是**第③级**，不是 16:9 改造时代的"回落支"；它缺席时并不把 2:3 海报塞进 16:9，而是继续落到 ④。</para>
    ///
    /// <para>**③④ 都缺**（无 `SeriesId` / 无剧集海报 tag）⇒ 返回**空串**：宁留空框，也不拿 2:3 竖海报去填 16:9，
    /// 也不造 500。本行**不要**回落 <see cref="PrimaryCardImageUrl"/> —— 那是 t172/R-1 给 **2:3 竖框**定的
    /// "换主剧集海报"口径，两处框比例不同，混用会自己制造裁切。</para>
    ///
    /// <para>**分支可观测**：每张卡一行 `HOME resume-source … source=`（<see cref="LogResumeSource"/>），
    /// 取值恰为 `series-backdrop` / `own-backdrop` / `own-primary-still` / `series-poster-uniform` / `none`。</para>
    ///
    /// <para>[!] **本方法不只服务继续观看行**：媒体行分支（`BuildTiles(list)`，`HomePage.xaml:149` 的框是
    /// **166×249 = 2:3**）同样调到这里。上面这套取向是**按 16:9 框定的**（① / ② 都是 16:9 原生 Backdrop），
    /// 于是 2:3 框拿到的也是 16:9 素材 —— 真起窗读数（t302 隔离根 68 张卡图缓存）：
    /// `215×121 n=62`（16:9 素材被压到高 121）＋ `81×121 n=5`（2:3 素材缩到高 121）
    /// ＋ `1600×900 n=1`（首页 hero）⇒ 2:3 框里 `UniformToFill` 横裁 ≈62%、或反向被放大 2.05×。
    /// **t302 只做注释对齐、不改实现**（本卡 nonGoals）；这条"框比例 vs 素材取向"的口径差按发现登记待裁。</para>
    /// </summary>
    private string ResumeCardImageUrl(EmbyItem item)
    {
        // t294：**来源选择显式化**，不再借道服务层出口② —— 出口②（`EmbyService.ImageUrlIfAvailable`
        // 的 Primary 分支）内部是"**自家 tag 优先于父级**"，那正是"剧集明明有背景图、卡片却显示小集自家图"
        // 的机制。本行框是 16:9（`HomePage.xaml` 216×121）⇒ 目标 = **只拿 16:9 原生素材**。优先级写死：
        //   ① 主剧集（父级）Backdrop ② 单集自家 Backdrop ③ 单集自家 Primary still（Emby 的 Episode Primary
        //     本身就是 16:9 still）④ 剧集竖海报 2:3 —— 框已 `Uniform` ⇒ **留边、不再横裁 60%+**（t263）。
        // ③ 之后仍无素材才落 ④；④ 也取不到（无 SeriesId/无剧集海报 tag）⇒ 空串（不造 500、不空卡硬撑）。
        var seriesBackdrop = SeriesBackdropUrl(item);
        if (!string.IsNullOrEmpty(seriesBackdrop)) { LogResumeSource(item, "series-backdrop"); return seriesBackdrop; }

        var ownBackdrop = OwnBackdropUrl(item);
        if (!string.IsNullOrEmpty(ownBackdrop)) { LogResumeSource(item, "own-backdrop"); return ownBackdrop; }

        var ownStill = OwnPrimaryStillUrl(item);
        if (!string.IsNullOrEmpty(ownStill)) { LogResumeSource(item, "own-primary-still"); return ownStill; }

        var seriesPoster = SeriesPosterUrl(item);
        LogResumeSource(item, string.IsNullOrEmpty(seriesPoster) ? "none" : "series-poster-uniform");
        return seriesPoster;
    }

    /// <summary>① 主剧集（父级）Backdrop：必须**换 id 且带父的 tag**（同 id 等于原地重试）。</summary>
    private string SeriesBackdropUrl(EmbyItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.ParentBackdropItemId)
            || string.Equals(item.ParentBackdropItemId, item.Id, StringComparison.Ordinal)) { return string.Empty; }
        var tag = FirstNonEmptyTag(item.ParentBackdropImageTags);
        if (string.IsNullOrEmpty(tag)) { return string.Empty; }
        return _emby.ImageUrl(item.ParentBackdropItemId, "Backdrop", maxHeight: ResumeCardHeight, maxWidth: ResumeCardWidth, tag: tag);
    }

    /// <summary>② 单集自家 Backdrop（`ImageTags["Backdrop"]` 命中 0 ⇒ 真源是 `BackdropImageTags` 列表）。</summary>
    private string OwnBackdropUrl(EmbyItem item)
    {
        var tag = FirstNonEmptyTag(item?.BackdropImageTags);
        if (string.IsNullOrEmpty(tag)) { return string.Empty; }
        return _emby.ImageUrl(item.Id, "Backdrop", maxHeight: ResumeCardHeight, maxWidth: ResumeCardWidth, tag: tag);
    }

    /// <summary>③ 单集自家 Primary still（16:9 原生）。**只在自家 tag 存在时取** ⇒ 结构上不可能落到出口②。</summary>
    private string OwnPrimaryStillUrl(EmbyItem item)
    {
        var ownTag = item?.ImageTags != null && item.ImageTags.TryGetValue("Primary", out var t) ? t : null;
        if (string.IsNullOrEmpty(ownTag)) { return string.Empty; }
        return _emby.ImageUrl(item.Id, "Primary", maxHeight: ResumeCardHeight, maxWidth: ResumeCardWidth, tag: ownTag);
    }

    /// <summary>④ 剧集竖海报（2:3）：**直接构造** `ItemId = SeriesId` + 剧集海报 tag ⇒ 不经过任何服务层出口。</summary>
    private string SeriesPosterUrl(EmbyItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.SeriesId) || string.IsNullOrEmpty(item.SeriesPrimaryImageTag)
            || string.Equals(item.SeriesId, item.Id, StringComparison.Ordinal)) { return string.Empty; }
        return _emby.ImageUrl(item.SeriesId, "Primary", maxHeight: ResumeCardHeight, maxWidth: ResumeCardWidth,
            tag: item.SeriesPrimaryImageTag);
    }

    private static string FirstNonEmptyTag(System.Collections.Generic.List<string> values)
    {
        if (values == null) { return string.Empty; }
        foreach (var v in values) { if (!string.IsNullOrEmpty(v)) { return v; } }
        return string.Empty;
    }

    /// <summary>
    /// t294 验收④：**逐卡来源**读数（一张一行）。取代旧的 `resume-image-fallback` 行 ——
    /// 旧行的文案（"回落单集自家 Primary"）在来源显式化之后已不成立，留着会让复核者读到错的机制。
    /// </summary>
    private static void LogResumeSource(EmbyItem item, string source)
    {
        Program.Log("HOME resume-source itemId=" + (item?.Id ?? "-")
            + " seriesId=" + (string.IsNullOrEmpty(item?.SeriesId) ? "-" : item.SeriesId)
            + " source=" + source);
    }

    /// <summary>继续观看行卡框的像素尺寸（**必须与 `HomePage.xaml` 的那个 `Grid` 同值**；改一处要同时改另一处）。
    /// t287：由 166×93 提到 **216×121**（参考帧实测 216 宽 / 间距 16 / 行首 272 / 图区含 4 px 进度条 121）。</summary>
    private const int ResumeCardWidth = 216;

    private const int ResumeCardHeight = 121;

    /// <summary>
    /// 卡片的 Primary 图 URL（t172 / R-1）。**单集在 2:3 竖框里必须用主剧集的海报**：直接传原始单集时
    /// <see cref="EmbyService.ImageUrlIfAvailable"/> 的出口①「自家 tag 优先」（`EmbyService.cs:577-581`）会先命中
    /// ⇒ 服务端按**单集 16:9 截帧**回图（实测 166×93）⇒ 塞进 `HomePage.xaml:87` 的 166×249 框，`UniformToFill` 下横裁 62.7%。
    /// 这里先试 <see cref="SeriesImageStub"/>（显式换 id/tag 去取剧集海报 ⇒ 实测 166×235，横裁 5.6%）；
    /// stub 为 null（无 SeriesId / 无 SeriesPrimaryImageTag / 与自家同 id）时回落原路径 —— 单集无自家 tag 的那种由
    /// 出口②（`EmbyService.cs:585-595`）换剧集 id，仍不是 500；两条都没有则出口③给空串（**不发请求**）。
    /// </summary>
    private string PrimaryCardImageUrl(EmbyItem item) => PrimaryCardImageUrl(item, out _);

    /// <summary>t308：与无参重载同实现，额外给出**分支名**（`series-stub` / `own` / `none`）供逐卡读数用。</summary>
    private string PrimaryCardImageUrl(EmbyItem item, out string branch)
    {
        var series = SeriesImageStub(item);
        if (series != null)
        {
            var seriesUrl = _emby.ImageUrlIfAvailable(series, "Primary", maxWidth: 166, maxHeight: 249);
            if (!string.IsNullOrEmpty(seriesUrl)) { branch = "series-stub"; return seriesUrl; }
        }

        var url = _emby.ImageUrlIfAvailable(item, "Primary", maxWidth: 166, maxHeight: 249);
        branch = string.IsNullOrEmpty(url) ? "none" : "own";
        return url;
    }

    /// <summary>
    /// **媒体行**的卡片图（t308）：媒体行框是 2:3（`HomePage.xaml:149` 的 166×249 + `UniformToFill`）⇒
    /// 必须走 2:3 取向 —— 复用 t172/R-1 的 <see cref="PrimaryCardImageUrl"/>（单集换主剧集竖海报 + 双上限 166×249），
    /// **不再**借道 <see cref="ResumeCardImageUrl"/>（那是 t294 给 16:9 横卡定的四级 pin、Backdrop 优先 ⇒ 16:9 素材进 2:3 框）。
    /// t302 真起窗读数里媒体行的 `215×121 n=62` 就是这个混用造成的（`UniformToFill` 横裁 ≈62.4% / 反向放大 2.06×）。
    /// </summary>
    private string MediaRowImageUrl(EmbyItem item, out string branch) => PrimaryCardImageUrl(item, out branch);

    /// <summary>t308 逐卡读数：`HOME tile-img row=media|cw itemId=… type=… branch=… reqPx=… urlLen=…`（**不打印 URL**）。</summary>
    private static void LogTileImage(string row, EmbyItem item, string branch, string url)
    {
        Program.Log("HOME tile-img row=" + row
            + " itemId=" + (item?.Id ?? "-")
            + " type=" + (string.IsNullOrEmpty(item?.Type) ? "-" : item.Type)
            + " branch=" + (string.IsNullOrEmpty(branch) ? "-" : branch)
            + " reqPx=" + (row == "cw" ? ResumeCardWidth + "x" + ResumeCardHeight : "166x249")
            + " urlLen=" + (url == null ? 0 : url.Length));
    }

    /// <summary>t308 到达读数：解码后的真实像素（`ImageSourceLoader` 回的是 `BitmapImage` ⇒ 加载后 `PixelWidth/Height` 可用）。</summary>
    private static void LogTileLoaded(PosterTile tile, ImageSource img)
    {
        var bmp = img as Microsoft.UI.Xaml.Media.Imaging.BitmapImage;
        Program.Log("HOME tile-img-loaded row=" + (tile?.Row ?? "-")
            + " itemId=" + (tile?.Item?.Id ?? "-")
            + " px=" + (bmp == null ? "-" : bmp.PixelWidth + "x" + bmp.PixelHeight));
    }

    /// <summary>
    /// 「主剧集海报」的 stub（t172 / R-1）：<c>Id = SeriesId</c> + <c>ImageTags["Primary"] = SeriesPrimaryImageTag</c>。
    /// 这样出口①拿到的 id 与 tag **都是剧集的** ⇒ 不被单集自己的 tag 抢走（用户原话「用主剧集的图片」），
    /// 也不需要新增服务层 API。判据与 `EmbyModels.cs:279-285` 的 <see cref="EmbyItem.PrimaryImageTag"/> 同向：
    /// 只有在"这一集**没有**自家 Primary tag"时，那个决定点才会给出剧集 tag；本 stub 是**无条件**优先剧集海报
    /// （卡片是 2:3 竖框，单集截帧在这里永远是错的）。
    /// 返回 null = 拿不到剧集海报（不构造会 500 的请求）。
    /// </summary>
    private static EmbyItem SeriesImageStub(EmbyItem item)
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

    /// <summary>
    /// 图片后填：并发度由**共享单点** `ImageFillBudget` 决定（t260 归一；t152 ④ 的"全局"语义一字不变），
    /// 同 URL 只取一次（②：重复取既白花请求，也是缓存写冲突的来源），单张 8 s 有界（在 `LoadImageAsync` 里）。
    /// <para>图片到达时只通知**这一张卡**（`PosterTile` 已实现 INPC）⇒ 不再整表重建（t152 ⑤）。</para>
    /// </summary>
    private async Task FillTileImagesAsync(List<PosterTile> tiles)
    {
        if (tiles.Count == 0) { return; }

        var ok = 0;
        var elapsed = new List<long>();
        await Task.WhenAll(tiles.Select(async tile =>
        {
            if (string.IsNullOrEmpty(tile.ImageUrl))
            {
                return;   // 判定走服务层单点 ⇒ 无图就是不请求（保持占位）
            }

            lock (_imageByUrl)
            {
                if (_imageByUrl.TryGetValue(tile.ImageUrl, out var seen))
                {
                    tile.Image = seen;   // 本页已经取过这张 ⇒ 直接复用，不再发请求
                    if (seen != null) { System.Threading.Interlocked.Increment(ref ok); LogTileLoaded(tile, seen); }
                    return;
                }
            }

            using var gate = await AIPlayer.Shell.Shell.ImageFillBudget.AcquireAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var img = await LoadImageAsync(tile.ImageUrl);
            sw.Stop();
            lock (elapsed) { elapsed.Add(sw.ElapsedMilliseconds); }
            lock (_imageByUrl) { _imageByUrl[tile.ImageUrl] = img; }
            tile.Image = img;
            if (img != null) { System.Threading.Interlocked.Increment(ref ok); LogTileLoaded(tile, img); }
        }));

        Program.Log("HOME IMG-OK " + ok + "/" + tiles.Count + " " + Summarize(elapsed)
            + " budget=" + AIPlayer.Shell.Shell.ImageFillBudget.Budget
            + " inFlightMax=" + AIPlayer.Shell.Shell.ImageFillBudget.InFlightMax
            + " gateWaits=" + AIPlayer.Shell.Shell.ImageFillBudget.GateWaits
            + " completed=" + AIPlayer.Shell.Shell.ImageFillBudget.Completed);
    }

    /// <summary>单图耗时分布（t152 ⑥）：p50 / p95 / max（毫秒）。</summary>
    private static string Summarize(List<long> values)
    {
        if (values == null || values.Count == 0) { return "p50=-ms p95=-ms max=-ms"; }

        var sorted = values.OrderBy(v => v).ToList();
        var p50 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.50))];
        var p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
        return "p50=" + p50 + "ms p95=" + p95 + "ms max=" + sorted[sorted.Count - 1] + "ms";
    }

    /// <summary>
    /// 主行文字。t152 ②（用户口径）：**继续观看行**的卡片必须是"主剧集名 + 是哪一集"两行都看得见 ⇒
    /// 剧集条目的主行给**主剧集名**（`SeriesName`），"是哪一集"放副行（见 <see cref="SubCaption"/>）；
    /// 没有 `SeriesName` 时才回落集名（不静默把集名当剧名）。
    /// </summary>
    private static string Caption(EmbyItem item)
    {
        if (IsEpisode(item) && !string.IsNullOrEmpty(item.SeriesName))
        {
            return item.SeriesName;
        }

        return item.Name;
    }

    /// <summary>
    /// 副行：继续观看行显式写"是哪一集"（`S02E01 · 集名`，②）；**剧集行写年份三态**（t77 卡面 ⑥ / t82 F2）；
    /// 其它类型留空不画。
    /// </summary>
    private static string SubCaption(EmbyItem item, bool resumeRow)
    {
        if (IsEpisode(item))
        {
            var ident = Identity(item);
            if (!resumeRow) { return ident; }
            return string.IsNullOrEmpty(item.Name) ? ident : ident + " · " + item.Name;
        }

        return string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase) ? YearRange(item) : string.Empty;
    }

    /// <summary>
    /// 年份三态（语义见 `EmbyModels.cs:228-237`）：**连载中** ⇒ `2014-现在`；**跨年完结** ⇒ `2014-2026`；
    /// **同年完结 / 缺字段** ⇒ 单年 `2026`；连首播年都没有 ⇒ 空串（不画）。
    /// <para>`EndDate` 是**服务端原文**（形如 `2011-05-08`，可能带时刻）⇒ 按 ISO 解析取年；解析不了 ⇒ 当缺失（不猜）。</para>
    /// </summary>
    private static string YearRange(EmbyItem item)
    {
        var start = item.ProductionYear is int y && y > 0 ? y : (int?)null;
        var end = ParseYear(item.EndDate);
        var continuing = string.Equals(item.SeriesStatus, "Continuing", StringComparison.OrdinalIgnoreCase);

        if (start == null) { return end?.ToString() ?? string.Empty; }
        if (continuing) { return start.Value + "-现在"; }
        return end != null && end.Value != start.Value ? start.Value + "-" + end.Value : start.Value.ToString();
    }

    /// <summary>`EndDate`（ISO，可能带时刻）⇒ 年份；解析不了 ⇒ `null`（不猜）。</summary>
    private static int? ParseYear(string endDate)
        => DateTime.TryParse(endDate, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var dt) ? dt.Year : (int?)null;

    /// <summary>年份三态的**原始字段 → 渲染文本**样本（t82 F2 点名要三态各一例）。</summary>
    private static string YearSamples(List<EmbyItem> items, int take = 3)
    {
        var parts = new List<string>();
        foreach (var item in items.Where(i => i != null
            && string.Equals(i.Type, "Series", StringComparison.OrdinalIgnoreCase)).Take(take))
        {
            var rendered = YearRange(item);
            parts.Add("[year=" + (item.ProductionYear?.ToString() ?? "-")
                + " status=" + (string.IsNullOrEmpty(item.SeriesStatus) ? "-" : item.SeriesStatus)
                + " endDate=" + (string.IsNullOrEmpty(item.EndDate) ? "-" : item.EndDate)
                + " -> " + (string.IsNullOrEmpty(rendered) ? "<empty>" : rendered) + "]");
        }

        return parts.Count == 0 ? "none" : string.Join(" ", parts);
    }

    /// <summary>
    /// 角标（①）：**集数只取 `RecursiveItemCount`**；取不到 ⇒ **不印集数**（t82 F3：TV 库的 `ChildCount` = 直属子项，
    /// 多半是"季"，印成"集"是错口径）—— 但**不静默**：落一行 `BADGE-FALLBACK` 说明为什么没有。
    /// 剧集条目其余情况给年份（`ProductionYear`）。
    /// </summary>
    private string Badge(EmbyItem item)
    {
        if (item.RecursiveItemCount is int recursive && recursive > 0) { return recursive + " 集"; }

        if (string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase))
        {
            Program.Log("BADGE-FALLBACK itemId=" + item.Id + " reason=recursive-item-count-missing type=Series"
                + " childCount=" + (item.ChildCount?.ToString() ?? "-") + "（不回退印 ChildCount：语义是「直属子项」、多半为季）");
        }

        if (item.ProductionYear is int year && year > 0) { return year.ToString(); }
        return string.Empty;
    }

    private static bool IsEpisode(EmbyItem item)
        => string.Equals(item?.Type, "Episode", StringComparison.OrdinalIgnoreCase);

    /// <summary>`S02E01`（有集/季号时）。</summary>
    private static string Identity(EmbyItem item)
        => item.IndexNumber is int ep && item.ParentIndexNumber is int season ? $"S{season:00}E{ep:00}" : string.Empty;

    /// <summary>图片：自己取字节 → 既有 `ImageSourceLoader`（见类注释：控件直连远程 URL 会失败）。</summary>
    private async Task<ImageSource> LoadImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) { return null; }

        try
        {
            var bytes = await AIPlayer.Shell.Services.Infra.ImageCacheManager.Default.GetOrFetchAsync(
                AIPlayer.Shell.Services.Infra.ImageCacheManager.NormalizeKey(url),
                async () =>
                {
                    // 统一走外壳 HTTP（UA/代理/重试一致）+ 单张 8s 上限：行不再被"每张 20s、逐张串行"拖住。
                    var http = new ShellHttpClient();
                    var r = await http.GetAsync(url, timeout: TimeSpan.FromSeconds(8));
                    return r.Bytes;
                });
            if (bytes == null || bytes.Length == 0) { return null; }
            return await AIPlayer.Shell.Features.Aggregate.Shared.ImageSourceLoader.LoadAsync(bytes);
        }
        catch (Exception ex)
        {
            Program.Log("HOME IMG-FETCH-FAIL " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    private static ServerConfig ResolveServer()
    {
        var preferredId = SettingsService.Instance?.Settings?.LastServerId ?? string.Empty;
        var candidates = ServerConfigStore.Instance.Servers?
            .Where(s => s.Enabled && s.Kind == ServerKind.Emby)
            .OrderBy(s => s.SortIndex)
            .ToList() ?? new List<ServerConfig>();

        return candidates.FirstOrDefault(s => string.Equals(s.Id, preferredId, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault();
    }

    private void OnTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PosterTile tile && tile.Item != null)
        {
            Program.Log("Nav -> detail id=" + tile.Item.Id + " name=" + tile.Item.Name);
            Frame?.Navigate(typeof(Detail.DetailPage), tile.Item);
        }
    }

    // 滚轮已收敛到 `MainWindow.OnAnyPointerWheel`（挂在根 Grid 上 + `handledEventsToo: true`）——
    // 本页**不再自挂** `PointerWheelChanged`：内层 ScrollViewer 会把滚轮标成 Handled，挂在 ListView 上的
    // 处理器根本收不到（用户报障「滚轮在横排/图标上整页滚不动」）。

    // 横排左右箭头（用户 2026-09-12：「增加左右箭头 同时要横条」）：
    // 悬停时出现 ◀ ▶，点一次翻**一屏**（改内层 ScrollViewer 的 HorizontalOffset；ChangeView 自己钳位，到头即停）。
    // 箭头与它要滚的那个 ListView 在**同一个 Grid** 里 ⇒ 不需要名字/Tag 绑定，模板行（媒体库行）也能工作。
    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e) => SetRowArrows(sender, Visibility.Visible);

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e) => SetRowArrows(sender, Visibility.Collapsed);

    private static void SetRowArrows(object sender, Visibility visibility)
    {
        if (sender is not DependencyObject host) { return; }

        foreach (var button in Descendants<Button>(host))
        {
            if (button.Tag is string tag && (tag == "left" || tag == "right")) { button.Visibility = visibility; }
        }
    }

    private void OnRowArrowClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag || button.Parent is not DependencyObject row)
        {
            return;
        }

        var viewer = Descendants<ScrollViewer>(row).FirstOrDefault();
        if (viewer == null || viewer.ScrollableWidth <= 0) { return; }

        var step = Math.Max(240, viewer.ViewportWidth - 80);
        var target = Math.Clamp(tag == "left" ? viewer.HorizontalOffset - step : viewer.HorizontalOffset + step,
                                0, viewer.ScrollableWidth);
        viewer.ChangeView(target, null, null, disableAnimation: true);
        Program.Log("HOME ROW-ARROW dir=" + tag + " target=" + (int)target + " scrollable=" + (int)viewer.ScrollableWidth);
    }

    /// <summary>深度优先枚举后代（箭头与目标滚动区同在模板里 ⇒ 只能按可视树找，不能按名字找）。</summary>
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) { yield return typed; }
            foreach (var nested in Descendants<T>(child)) { yield return nested; }
        }
    }

    /// <summary>一个媒体库一行。</summary>
    public sealed class HomeRow
    {
        public string Title { get; set; } = string.Empty;

        public List<PosterTile> Tiles { get; set; } = new List<PosterTile>();
    }

    /// <summary>
    /// 海报卡：占位底色先出，图片异步填。
    /// <para>t152 ⑤：**实现 `INotifyPropertyChanged`** —— 图片到达时只通知这一张卡（旧实现靠
    /// "整行 `ItemsSource = null; = rows;`"来强制刷新 ⇒ 每行一次整表重建，还会重置滚动与选中态）。</para>
    /// </summary>
    public sealed class PosterTile : System.ComponentModel.INotifyPropertyChanged
    {
        private ImageSource _image;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public ImageSource Image
        {
            get => _image;
            set
            {
                _image = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Image)));
            }
        }

        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>t308：这枚卡属于哪一行（`media` 媒体行 / `cw` 继续观看行）—— 按行分流取图的逐卡读数用。</summary>
        public string Row { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        /// <summary>副行（t152 ②）：继续观看行**显式**写出"主剧集名 · 是哪一集"（不许静默借用）。</summary>
        public string SubCaption { get; set; } = string.Empty;

        /// <summary>角标（t152 ①）：剧集给"集数"（`RecursiveItemCount`），否则给年份；两者都没有 ⇒ 空串（不画）。</summary>
        public string Badge { get; set; } = string.Empty;

        /// <summary>角标有没有（XAML 里 `x:Bind` 直连 `Visibility`，省掉一个转换器）。</summary>
        public Visibility BadgeVisibility
            => string.IsNullOrEmpty(Badge) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>副行有没有（同上）。</summary>
        public Visibility SubVisibility
            => string.IsNullOrEmpty(SubCaption) ? Visibility.Collapsed : Visibility.Visible;

        public EmbyItem Item { get; set; }
    }
}

/// <summary>
/// 非空字符串 ⇒ <c>Visible</c>（XAML 里用来"没有角标 / 没有副行就整块不画"）。
/// <para>为什么不用 <c>x:Bind</c> + <c>x:DataType</c>：`PosterTile` 是 **`HomePage` 的嵌套类**，
/// 而 `x:DataType="feat:PosterTile"` 解析不到嵌套类型 ⇒ XamlCompiler 直接失败（实测 `MSB3073`）。
/// 经典 `{Binding}` + 转换器走反射绑定，不需要 `x:DataType`，嵌套类也能绑。</para>
/// </summary>
public sealed class NonEmptyToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

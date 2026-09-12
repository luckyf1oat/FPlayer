using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Detail;

/// <summary>
/// 详情页：按用户当面给的参照图（`refs/original-ui/req-detail-0*.png`）复现 ——
/// **Hero 大背景 + 主操作行（播放 + 三圆形图标）+ 右下角三下拉（版本/音轨/字幕）+ 元信息 + 简介
/// + 季 + 更多来自第 N 季 + 更多类似 + 媒体信息分卡 + 外部链接**。
///
/// 取数面**全部走既有服务层**（不自造通道）：
///   · 明细 `GetItemAsync(fields: …People/ImageTags/Genres…)`、季 `GetSeasonsAsync`、集 `GetEpisodesAsync`、
///     相似 `GetSimilarAsync`、图片 `ImageUrl(...)`
///   · 播放：`BuildRequestAsync` → `KernelArgumentBuilder.FromPlayback` → `KernelLauncher.Launch`（唯一合法链路）
///   · [!] 三下拉的落点（**都在会话上，公开可写**）：
///       `VersionIndex`（换源，值域被夹紧）· `AudioStreamIndex` · `SubtitleStreamIndex`
///     它们同时影响取流 URL 与 `--version-option=` / `--audio-track=` / `--subtitle-id=`，故"选了哪条"在命令行里可查。
///
/// 导航参数 = `EmbyItem`（由 `LibraryPage` 的卡片点击传入）。
/// </summary>
public sealed partial class DetailPage : Page
{
    /// <summary>导航 tag（与 `MainWindow.NavigateTo` 的 `case` 对齐）。</summary>
    public const string NavTag = "detail";

    /// <summary>自检钩子（默认关闭）：载入完自动起播一次，用于读"选集是否真的进了内核命令行"。</summary>
    public const string DetailAutoPlayEnvVar = "SHELL_SELFTEST_DETAIL_AUTOPLAY";

    /// <summary>取证钩子（默认关闭）：载入完把页面滚到该像素位置，用于截到折线以下的段落。</summary>
    public const string DetailScrollEnvVar = "SHELL_SELFTEST_DETAIL_SCROLL";

    /// <summary>取证钩子（默认关闭）：把剧集行的选中项切到该序号（等价于点那一行）。</summary>
    public const string DetailEpisodeEnvVar = "SHELL_SELFTEST_DETAIL_EPISODE";

    /// <summary>t105 硬超时：整段载入超过它 ⇒ 落可见失败态（默认 20 s；可用 `SHELL_DETAIL_TIMEOUT_MS` 覆盖，便于取证时缩短）。</summary>
    private static int LoadTimeoutMs
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("SHELL_DETAIL_TIMEOUT_MS");
            return int.TryParse(raw, out var ms) && ms > 0 ? ms : 20000;
        }
    }

    /// <summary>t105 可见三态。</summary>
    private enum DetailState
    {
        Loading,
        Content,
        Failed,
    }

    private EmbyItem _item;                       // 当前播放目标（电影 / 单集）
    private EmbyItem _navItem;                    // 导航带进来的那条目（续播点回落用，见 FillHeader）
    private EmbyItem _series;                     // 剧集条目（若 `_item` 属于某剧）
    private EmbyService _emby;
    private EmbyPlaybackSession _session;         // 懒构造：播放/收藏/标记已看共用

    private readonly List<EmbyItem> _seasons = new();
    private IReadOnlyList<EmbyItem> _episodes = Array.Empty<EmbyItem>();
    private IReadOnlyList<EmbyMediaSource> _sources = Array.Empty<EmbyMediaSource>();

    private int _seasonIndex;
    private int _versionIndex;
    private int _audioIndex = -1;
    private int _subtitleIndex = -1;
    private bool _suppress;

    public DetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not EmbyItem item)
        {
            StatusText.Text = "没有可显示的条目（请从媒体库点进来）";
            PlayButton.IsEnabled = false;
            Program.Log("DETAIL no-parameter");
            return;
        }

        _item = item;
        Program.Log("DETAIL open id=" + item.Id + " type=" + item.Type + " name=" + item.Name);
        await LoadAsync();
    }

    // ------------------------------------------------------------------ 载入

    /// <summary>
    /// 载入入口（t105）：**硬超时 + 可见三态 + 分段埋点**。
    /// 用户 2026-09-12 报障「详情页加载不出东西」= `DETAIL open` 之后**既无 `loaded` 也无 `LOAD-FAIL`**，页面就那么挂着。
    /// 现在：① 起手立刻进「加载中」（转圈 + 「正在加载…」，主按钮禁用）；
    ///      ② 整段载入有 <see cref="LoadTimeoutMs"/> 硬超时 ⇒ 超时落**可见失败态**（带重试）+ 一行 `DETAIL LOAD-TIMEOUT`；
    ///      ③ 成功 ⇒ 「有内容」态；异常 ⇒ 可见失败态 + `DETAIL LOAD-FAIL`。三态各自落一行 `DETAIL STATE …`（可判绿/判红）。
    /// </summary>
    private async Task LoadAsync()
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        SetState(DetailState.Loading);
        Program.Log("DETAIL load-begin id=" + (_item?.Id ?? "<null>") + " name=" + (_item?.Name ?? "")
            + " timeoutMs=" + LoadTimeoutMs);

        var core = LoadCoreAsync(total);
        var winner = await Task.WhenAny(core, Task.Delay(LoadTimeoutMs));
        if (winner != core)
        {
            SetState(DetailState.Failed, "加载超时（" + TimeoutLabel() + "）—— 点「重试」再来一次");
            Program.Log("DETAIL LOAD-TIMEOUT elapsed=" + total.ElapsedMilliseconds + "ms timeout=" + LoadTimeoutMs
                + "ms id=" + (_item?.Id ?? "?"));
            // 后台那条链若之后自己**成功**收尾 ⇒ 回到"有内容"态（内容已经填进页面了，标签不该还停在"失败"）；
            // 失败/取消只留一行痕。`ContinueWith` 跑在线程池 ⇒ 触碰 XAML 必须回 UI 线程。
            _ = core.ContinueWith(t =>
            {
                Program.Log("DETAIL late-completion state=" + t.Status);
                if (t.Status == TaskStatus.RanToCompletion && t.Result)
                {
                    DispatcherQueue.TryEnqueue(() => SetState(DetailState.Content));
                }
            }, TaskScheduler.Default);
            return;
        }

        try
        {
            // `core` 返回 false = LoadCoreAsync **自己已经把状态置成 Failed**（例如条目详情读不到）⇒
            // 这里绝不能无条件置"有内容"：实测（2026-09-12 17:16:49.356/.358）原来就是无条件写的，
            // 结果刚打出来的失败态被下一行抹成"有内容"，用户看到一屏空白且没有任何提示。
            if (await core) { SetState(DetailState.Content); }
        }
        catch (Exception ex)
        {
            SetState(DetailState.Failed, "载入失败：" + ex.GetType().Name + " —— 点「重试」再来一次");
            Program.Log("DETAIL LOAD-FAIL " + ex.GetType().FullName + ": " + ex.Message);
        }
    }

    /// <summary>「重试」：从头再走一遍 <see cref="LoadAsync"/>（失败态是**可恢复**的，不是死屏）。</summary>
    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        Program.Log("DETAIL retry");
        _ = LoadAsync();
    }

    /// <summary>
    /// 三段式状态机（t105 的「可见三态」）。文案与可见性都在这一处改，避免三处各写一半。
    /// </summary>
    private void SetState(DetailState state, string message = null)
    {
        switch (state)
        {
            case DetailState.Loading:
                LoadingPanel.Visibility = Visibility.Visible;
                RetryButton.Visibility = Visibility.Collapsed;
                PlayButton.IsEnabled = false;
                StatusText.Text = message ?? "正在加载…";
                break;
            case DetailState.Content:
                LoadingPanel.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Collapsed;
                PlayButton.IsEnabled = true;
                StatusText.Text = message ?? string.Empty;
                break;
            default:
                LoadingPanel.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Visible;   // 失败必须看得见"能重试"
                PlayButton.IsEnabled = false;
                StatusText.Text = message ?? "加载失败";
                break;
        }

        Program.Log("DETAIL STATE " + state + " text=" + (StatusText.Text ?? string.Empty));
    }

    /// <summary>分段埋点（有返回值）：`DETAIL segment=&lt;名&gt; begin|end elapsed=|fail`；异常时按 `<paramref name="fallback"/>` 继续。</summary>
    private async Task<T> SegmentAsync<T>(string name, Func<Task<T>> body, T fallback)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Program.Log("DETAIL segment=" + name + " begin");
        try
        {
            var value = await body();
            Program.Log("DETAIL segment=" + name + " end elapsed=" + sw.ElapsedMilliseconds + "ms");
            return value;
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL segment=" + name + " fail elapsed=" + sw.ElapsedMilliseconds + "ms "
                + ex.GetType().Name + ": " + ex.Message);
            return fallback;
        }
    }

    /// <summary>分段埋点（无返回值，同上）。</summary>
    private async Task SegmentAsync(string name, Func<Task> body)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Program.Log("DETAIL segment=" + name + " begin");
        try
        {
            await body();
            Program.Log("DETAIL segment=" + name + " end elapsed=" + sw.ElapsedMilliseconds + "ms");
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL segment=" + name + " fail elapsed=" + sw.ElapsedMilliseconds + "ms "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>超时文案里的时限（&lt;1 s 用毫秒，否则用秒；取证时把时限压到 1 ms 也不会出现"0 秒"）。</summary>
    private static string TimeoutLabel()
        => LoadTimeoutMs < 1000 ? LoadTimeoutMs + " 毫秒" : (LoadTimeoutMs / 1000) + " 秒";

    /// <summary>
    /// 真正干活的那条链。返回 <c>true</c> = 走到正常收尾；<c>false</c> = **自己已经落过失败态**
    /// （调用方不得再覆盖成"有内容"，见 <see cref="LoadAsync"/> 里的实测注释）。
    /// </summary>
    private async Task<bool> LoadCoreAsync(System.Diagnostics.Stopwatch total)
    {
        try
        {
            // 导航带进来的那条目（首页「继续观看」过来的条目自带续播点）—— 取数会把 `_item` 换成
            // 重新取的明细，续播点要留一份原值做回落（明细的 UserData 若为空，不能把续播点读没了）。
            _navItem = _item;

            var server = ResolveServer();
            if (server == null)
            {
                SetState(DetailState.Failed, "没有启用的 Emby 服务器（去「服务器」页添加或启用）");
                Program.Log("DETAIL no-enabled-emby-server");
                return false;
            }

            _emby = new EmbyService(new ShellHttpClient(), server);

            var fetched = await SegmentAsync("item", () => _emby.GetItemAsync(_item.Id,
                fields: "Overview,MediaSources,MediaStreams,ProviderIds,ImageTags,BackdropImageTags,Genres," +
                        "CommunityRating,OfficialRating,ProductionYear,PremiereDate,Studios,People,Taglines"),
                (EmbyItem)null);

            // [!] 连条目详情都读不到 ⇒ 这就是**失败**，绝不允许往下走成"有内容"态：
            // 否则不可达源会给出一屏**空壳**（无标题无图）却被判成成功 —— 那还是"用户看不懂"的那种屏。
            if (fetched == null)
            {
                SetState(DetailState.Failed, "读不到条目详情（源不可达 / 超时）—— 点「重试」再来一次");
                Program.Log("DETAIL item-unavailable id=" + (_item?.Id ?? "?"));
                return false;
            }

            var detailed = fetched;
            _item = detailed;
            await SegmentAsync("hero", () => SetHeroImageAsync(detailed));

            // 文案（标题 / 季集行 / 元信息 / 简介 / 主按钮文案）**先落**，再进季·集那条取数面。
            // 实测（2026-09-12 16:47:31.14 `DETAIL open` → 16:48:04.04 才打出 `DETAIL play-label`）：
            // 原来 FillHeader 排在 LoadSeasonsAsync 之后，于是"下一集列表的 24 张图"没回来之前，
            // 用户看到的是**标题和播放按钮都不在**的一屏 —— 这就是用户说的"加载有点慢"。
            // 这里只调顺序（不改成并发，并发/超时/骨架屏归 t105），标题与按钮立刻可见。
            FillHeader(detailed);

            // 剧集面：Series 自身、或"某一集"（用它的 SeriesId 反查同剧的季/集）。
            var seriesId = !string.IsNullOrEmpty(detailed.SeriesId)
                ? detailed.SeriesId
                : (string.Equals(detailed.Type, "Series", StringComparison.OrdinalIgnoreCase) ? detailed.Id : string.Empty);

            if (!string.IsNullOrEmpty(seriesId))
            {
                _series = string.Equals(seriesId, detailed.Id, StringComparison.OrdinalIgnoreCase)
                    ? detailed
                    : await _emby.GetItemAsync(seriesId) ?? detailed;

                await SegmentAsync("seasons", () => LoadSeasonsAsync(seriesId, detailed));
            }

            // FillHeader 已在上面（进季/集之前）先落一次；这里不再重复调它：
            // 季/集那条链里 `LoadEpisodesAsync` 结尾自己会按"选中的那一集"再刷一次抬头，
            // 重复调只会把主按钮文案再算一遍（同一判定，无新信息）。
            FillSources(detailed);
            await SegmentAsync("sources", () => RefreshSourcesForAsync(_item));
            await SegmentAsync("similar", () => LoadSimilarAsync(detailed));
            await SegmentAsync("cast", () => LoadCastAsync(detailed));

            Program.Log("DETAIL loaded id=" + detailed.Id + " sources=" + _sources.Count
                + " seasons=" + _seasons.Count + " episodes=" + _episodes.Count
                + " elapsed=" + total.ElapsedMilliseconds + "ms");

            // 自检钩子（默认关闭）：`SHELL_SELFTEST_DETAIL_AUTOPLAY=1` ⇒ 载入完直接走一次播放。
            // 为什么需要它：**"选集有没有真的进到内核命令行"只有起播才能读到**（`PLAY resolved … episodes=N`），
            // 而本项目不驱动鼠标 ⇒ 用钩子代替点击（与 `SHELL_SELFTEST_*` 系列同一形态）。
            // 取证钩子（默认关闭）：`SHELL_SELFTEST_DETAIL_EPISODE=<剧集行序号>` ⇒ 切到该集。
            // 走的**还是 ListView 的 SelectionChanged**（= 点那一行走的那条路：`_item` 换成该集、
            // 抬头/主按钮重算、播放源重取），不是另造一条起播路径；随后若同时设了 AUTOPLAY，
            // 起播的就是**切换后的那一集**（验收⑤的"切集再起播"）。
            var episodePicked = false;
            if (int.TryParse(Environment.GetEnvironmentVariable(DetailEpisodeEnvVar), out var epIdx)
                && epIdx >= 0 && epIdx < _episodes.Count)
            {
                EpisodeList.SelectedIndex = epIdx;
                episodePicked = true;
                Program.Log("DETAIL-SELFTEST episode index=" + epIdx + " count=" + _episodes.Count
                    + " picked=" + (_episodes[epIdx].Name ?? ""));
            }

            if (Environment.GetEnvironmentVariable(DetailAutoPlayEnvVar) == "1")
            {
                // 刚切过集时先让 SelectionChanged 那条异步链把 `_item` 落定，否则起播的可能还是上一集。
                if (episodePicked) { await Task.Delay(1200); }
                Program.Log("DETAIL-AUTOPLAY begin episodes=" + _episodes.Count + " sources=" + _sources.Count
                    + " item=" + (_item?.Name ?? ""));
                OnPlayClick(this, null);
            }

            // 取证钩子（默认关闭）：`SHELL_SELFTEST_DETAIL_SCROLL=<像素>` ⇒ 载入完把页面滚到该位置。
            // 为什么需要：详情页比一屏长（演职人员 / 媒体信息 / 外部链接都在折线以下），而本项目
            // 不驱动鼠标输入 ⇒ 没有这个钩子，能进截图的就只剩"最上面那一屏"。
            if (double.TryParse(Environment.GetEnvironmentVariable(DetailScrollEnvVar), out var scrollY))
            {
                await Task.Delay(1200);   // 先让上面几段图片落位，再滚（否则滚下去还是占位底）
                PageScroll.ChangeView(null, scrollY, null);
                Program.Log("DETAIL-SELFTEST scroll y=" + scrollY);
            }

            return true;        // 走到正常收尾 ⇒ 调用方置「有内容」态
        }
        catch (Exception ex)
        {
            SetState(DetailState.Failed, "载入失败：" + ex.GetType().Name + " —— 点「重试」再来一次");
            Program.Log("DETAIL LOAD-FAIL " + ex.GetType().FullName + ": " + ex.Message);
            return false;       // 失败态已落 ⇒ 调用方不得再覆盖成「有内容」
        }
    }

    private async Task SetHeroImageAsync(EmbyItem item)
    {
        try
        {
            // 单集通常没有自家背景图（背景图挂在所属剧上），回退与 tag 都由服务层单点决定：
            // 空串 = "这条没有可取的背景图" ⇒ 不设图、不发请求。
            // [t257] 降档 1600→1200：Hero 显示区的**内容宽度实测 1168 px**（t177/t198 的几何读数）
            // ⇒ 1200 ≥ 1168，**不产生画质损失**（1600 是过剩请求）；而冷启读数显示 hero 是**单请求瓶颈**
            // （`Backdrop:maxWidth=1600` → 584,860 B / 6,067 ms ≈ 96 KB/s 有效）。
            // 降档按面积比 (1200*675)/(1600*900)=0.5625 估 ≈329 KB ⇒ ≈3.4 s（目标 hero 段 ≤3,500 ms）。
            var url = _emby.ImageUrlIfAvailable(item, "Backdrop", maxWidth: 1200);
            if (!string.IsNullOrEmpty(url))
            {
                HeroImage.Source = await LoadImageAsync(url);
                // 阳性读数：Hero 有没有真的拿到图，之前**没有任何一行日志**（只有失败才打），
                // 于是截图上半幅是黑的时候分不清"没图"和"图是暗色的"。
                // 三栏读数（Captain 追加验收①）：请求 = `Backdrop` maxWidth:1200（16:9）；实返回像素 = `returnedPx`；
                // 显示区 = Hero Grid 高 300 + `UniformToFill`（**有意 cover 裁切**，参照图二也是"一整张横版大图 + 标题浮在上面"）。
                // `IMG-MISS` 就是对"顶部纯色底"的正面回答：图没取回来时它是显式的，不再和"暗色图"混为一谈。
                var heroPx = HeroImage.Source is Microsoft.UI.Xaml.Media.Imaging.BitmapImage heroBmp
                    ? heroBmp.PixelWidth + "x" + heroBmp.PixelHeight
                    : "<none>";
                Program.Log("DETAIL hero " + (HeroImage.Source != null ? "ok" : "IMG-MISS")
                    + " id=" + item.Id + " request=Backdrop:maxWidth=1200 returnedPx=" + heroPx);
            }
            else
            {
                Program.Log("DETAIL hero-noimage id=" + item.Id
                    + " type=" + item.Type
                    + " ownBackdropTags=" + (item.BackdropImageTags?.Count ?? 0)
                    + " parentBackdropId=" + (string.IsNullOrEmpty(item.ParentBackdropItemId) ? "none" : "set"));
            }
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL HERO-IMG-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task LoadSeasonsAsync(string seriesId, EmbyItem detailed)
    {
        try
        {
            var seasons = await _emby.GetSeasonsAsync(seriesId);
            _seasons.Clear();
            if (seasons != null) { _seasons.AddRange(seasons); }

            if (_seasons.Count > 1)
            {
                _suppress = true;
                SeasonList.ItemsSource = _seasons.Select((s, i) => new SeasonTile
                {
                    Caption = string.IsNullOrEmpty(s.Name) ? "第 " + (i + 1) + " 季" : s.Name,
                    Count = s.ChildCount ?? 0,
                    // 季卡 = 竖版 2:3（420x630 请求 ↔ 140x210 显示区，同一比例）：
                    // **同时**给 maxWidth/maxHeight，服务端按这个框出图，避免"只限高"导致的长边浪费。
                    ImageUrl = _emby.ImageUrlIfAvailable(s, "Primary", maxWidth: 280, maxHeight: 420),
                }).ToList();
                await FillImagesAsync((SeasonList.ItemsSource as System.Collections.IEnumerable)?.OfType<ITileImage>() ?? System.Linq.Enumerable.Empty<ITileImage>(), "seasons-2x3");

                var wanted = _seasons.FindIndex(s => string.Equals(s.Id, detailed.SeasonId, StringComparison.OrdinalIgnoreCase));
                _seasonIndex = wanted >= 0 ? wanted : 0;
                SeasonList.SelectedIndex = _seasonIndex;
                _suppress = false;
                SeasonSection.Visibility = Visibility.Visible;
            }

            var seasonId = _seasons.Count > 0
                ? _seasons[Math.Clamp(_seasonIndex, 0, _seasons.Count - 1)].Id
                : detailed.SeasonId;

            await LoadEpisodesAsync(seriesId, seasonId);
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL SEASONS-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task LoadEpisodesAsync(string seriesId, string seasonId)
    {
        try
        {
            var episodes = await _emby.GetEpisodesAsync(seriesId, string.IsNullOrEmpty(seasonId) ? null : seasonId);
            _episodes = episodes ?? new List<EmbyItem>();

            _suppress = true;
            EpisodeList.ItemsSource = _episodes.Select((ep, i) => new PosterTile
            {
                Caption = (ep.IndexNumber is int n && n > 0 ? n : i + 1) + ". " + ep.Name,
                // 集**多数没有自己的封面** ⇒ 回退由服务层单点决定（自家 tag → 剧集回退 → 空串）；
                // 手写三元版把「没有 tag」也当成有图（自家 id 无 tag ⇒ 服务端 500，t78 实测 11 张里 6 张挂）。
                // 剧集行 = **横版 16:9**（用户参照图二）：Episode 的 `Primary` 就是 16:9 截帧，
                // 请求按 480x270（16:9）出图，与 `DetailPage.xaml` 的 300x169 卡位同比例 ⇒ 不裁不变形。
                // 改前是 420 只限高 + 166x249 竖卡 ⇒ 横图被裁成"粉色一团/半张脸"（用户截图图一）。
                ImageUrl = _emby.ImageUrlIfAvailable(ep, "Primary", maxWidth: 480, maxHeight: 270),
                Item = ep,
            }).ToList();
                await FillImagesAsync((EpisodeList.ItemsSource as System.Collections.IEnumerable)?.OfType<ITileImage>() ?? System.Linq.Enumerable.Empty<ITileImage>(), "episodes-16x9");

            var current = _episodes.Select((ep, i) => (ep, i))
                .FirstOrDefault(t => string.Equals(t.ep.Id, _item.Id, StringComparison.OrdinalIgnoreCase));
            var selected = current.ep != null ? current.i : (_episodes.Count > 0 ? 0 : -1);
            EpisodeList.SelectedIndex = selected;
            if (selected >= 0) { _item = _episodes[selected]; }
            _suppress = false;

            if (_episodes.Count > 0)
            {
                var seasonLabel = _seasons.Count > 0 && _seasonIndex < _seasons.Count
                    ? (_seasons[_seasonIndex].Name ?? ("第 " + (_seasonIndex + 1) + " 季"))
                    : "本季";
                EpisodeSectionTitle.Text = "更多来自 " + seasonLabel;
                EpisodeSection.Visibility = Visibility.Visible;
            }

            FillHeader(_item);
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL EPISODES-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task LoadSimilarAsync(EmbyItem item)
    {
        try
        {
            var similar = await _emby.GetSimilarAsync(item.Id, limit: 12);
            if (similar == null || similar.Count == 0) { return; }

            SimilarList.ItemsSource = similar.Select(s => new PosterTile
            {
                Caption = s.Name,
                // 相似行 = 竖版 2:3 海报（420x630 请求 ↔ 140x210 显示区，同比例）—— 这一行**本来就是对的**，
                // 本卡只把请求补成"宽高都限"，保证"不一致处 = 0"这条判据在整页成立。
                ImageUrl = _emby.ImageUrlIfAvailable(s, "Primary", maxWidth: 280, maxHeight: 420),
                Item = s,
            }).ToList();
                await FillImagesAsync((SimilarList.ItemsSource as System.Collections.IEnumerable)?.OfType<ITileImage>() ?? System.Linq.Enumerable.Empty<ITileImage>(), "similar-2x3");
            SimilarSection.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL SIMILAR-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// 图片走**字节通道**（不是 `BitmapImage(uri)`）。
    /// 为什么：本工程是**非打包** WinUI3，`BitmapImage` 直接吃远程 URL 实测报 `E_NETWORK_ERROR`
    /// （2026-09-12 实测：`DETAIL IMG-FAIL type=Primary id=250617 err=E_NETWORK_ERROR`）⇒ 用户看到的
    /// 就是"图片全空"。改用**自己取字节 + 既有 `ImageSourceLoader`**（`Features/Aggregate/Shared/`，
    /// 聚合页一直在用这条通道）⇒ 与"一个能力一个实现者"一致，不新开第二条图片管线。
    /// 失败只记日志、返回 null（占位底色保留），**不抛**。
    /// </summary>
    /// <summary>
    /// 演职人员（§6）：服务层 DTO **没有** <c>People</c> 字段（与人物页同一事实，见 `PersonPage.cs:465`），
    /// 故读条目原始 JSON 的 <c>People[]</c>（`Id` / `Name` / `Role` / `PrimaryImageTag`）。
    /// 图片 URL **仍只由服务层出口产出**：把 People 里的 `PrimaryImageTag` 塞进一个最小 <c>EmbyItem</c>，
    /// 交给 <see cref="EmbyService.ImageUrlIfAvailable"/>（其出口①的判据就是"自家 tag 存在"），
    /// 本页**不手拼任何图片地址**（没有 tag 就是空串 ⇒ 不发请求，卡片留在 `#333333` 占位底上）。
    /// 点卡片 ⇒ 该人作品列表（`Features/Person`，`personIds` 过滤），复用既有导航参数形态（`EmbyItem`）。
    /// </summary>
    private async Task LoadCastAsync(EmbyItem item)
    {
        try
        {
            if (item?.Raw == null) { return; }

            var people = AIPlayer.Shell.Services.Util.JsonRead.Items(
                AIPlayer.Shell.Services.Util.JsonRead.From(item.Raw), "People");

            var tiles = new List<PersonTile>();
            foreach (var person in people)
            {
                var id = AIPlayer.Shell.Services.Util.JsonRead.Str(person, "Id");
                var name = AIPlayer.Shell.Services.Util.JsonRead.Str(person, "Name");
                if (id.Length == 0 || name.Length == 0) { continue; }

                var tag = AIPlayer.Shell.Services.Util.JsonRead.Str(person, "PrimaryImageTag");
                var stub = new EmbyItem { Id = id };
                if (tag.Length > 0) { stub.ImageTags["Primary"] = tag; }

                tiles.Add(new PersonTile
                {
                    PersonId = id,
                    Name = name,
                    Role = AIPlayer.Shell.Services.Util.JsonRead.Str(person, "Role"),
                    ImageUrl = _emby.ImageUrlIfAvailable(stub, "Primary", maxHeight: 300),
                });
            }

            if (tiles.Count == 0)
            {
                CastSection.Visibility = Visibility.Collapsed;
                Program.Log("DETAIL cast people=" + people.Count + " tiles=0");
                return;
            }

            CastList.ItemsSource = tiles;
            CastSection.Visibility = Visibility.Visible;
            await FillImagesAsync(tiles, "cast-1x1");
            Program.Log("DETAIL cast people=" + people.Count + " tiles=" + tiles.Count
                + " withUrl=" + tiles.Count(t => !string.IsNullOrEmpty(t.ImageUrl)));
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL CAST-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>演职人员卡：一位人 = 头像 + 姓名 + 角色；点击进该人作品列表。</summary>
    private void OnCastSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) { return; }
        if (sender is not ListView list || list.SelectedItem is not PersonTile tile) { return; }

        list.SelectedIndex = -1;   // 立即清选中：ListView 的选中底色会盖住头像，且便于再次点同一人
        Program.Log("Nav -> person id=" + tile.PersonId + " name=" + tile.Name + " from=detail-cast");
        Frame?.Navigate(typeof(Features.Person.PersonPage), new EmbyItem { Id = tile.PersonId, Name = tile.Name });
    }

    /// <summary>
    /// t203 ③：**同一次会话内不重打同一个 URL**。判据 = 取字节/解码失败过的 URL 记进本集合，后续再遇到直接跳过
    /// （只记一次 skip 日志）。为什么需要：一次载入里同一张图可能被多行引用（季/集/相似/演职各有自己的行），
    /// 失败后每次重进详情页都会再打一遍 ⇒ 用户点几次就是几倍的无效请求，且每次都要等满 8 s 超时。
    /// </summary>
    private readonly HashSet<string> _imgFailedKeys = new(StringComparer.Ordinal);

    private int _imgFailCount;
    private int _imgSkipCount;

    /// <summary>
    /// t203 取证钩子（默认关闭）：`SHELL_SELFTEST_DETAIL_BADIMG=&lt;n&gt;` ⇒ 把该行**前 n 张**卡的 URL 全换成
    /// **同一个**必然失败的地址（本机 9 号端口没人监听 ⇒ 连接被拒，快且确定）。
    /// <para>为什么要它：验收 ④（失败态可见 + 同会话同 URL 不重打）若靠"等真实 500 碰巧出现"就无法复现 ⇒
    /// 用它把两条都变成**确定性读数**：第 1 张 fetch 失败（`IMG-FAIL`），第 2..n 张命中"不重打"闸门
    /// （`IMG-SKIP-REFETCH`），行末计数 `failed=n skipRefetch=n-1`。</para>
    /// 与同文件的其它 `SHELL_SELFTEST_*` 钩子同形态：只在显式设了环境变量时生效。
    /// </summary>
    public const string BadImageEnvVar = "SHELL_SELFTEST_DETAIL_BADIMG";

    private const string BadImageUrl = "http://127.0.0.1:9/t203-bad-image.jpg";

    private static int ResolveBadImageCount()
    {
        var raw = Environment.GetEnvironmentVariable(BadImageEnvVar);
        return int.TryParse(raw, out var v) && v > 0 ? v : 0;
    }

    private static void InjectBadImagesForSelfTest(System.Collections.Generic.IList<ITileImage> tiles)
    {
        var n = ResolveBadImageCount();
        if (n <= 0 || tiles == null) { return; }

        n = Math.Min(n, tiles.Count);
        for (var i = 0; i < n; i++)
        {
            tiles[i].ImageUrl = BadImageUrl;
        }

        Program.Log("DETAIL BADIMG-INJECT n=" + n + " ofRow=" + tiles.Count + " url=" + BadImageUrl);
    }

    private async Task<ImageSource> LoadImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) { return null; }

        var key = AIPlayer.Shell.Services.Infra.ImageCacheManager.NormalizeKey(url);
        if (_imgFailedKeys.Contains(key))
        {
            _imgSkipCount++;
            Program.Log("DETAIL IMG-SKIP-REFETCH key=" + key + " skippedTotal=" + _imgSkipCount);
            return null;
        }

        try
        {
            // 与首页同源：统一缓存单点 + 单张 8s 上限（裸 HttpClient 会绕过 UA/代理策略、磁盘缓存与 8s 界）。
            var bytes = await AIPlayer.Shell.Services.Infra.ImageCacheManager.Default.GetOrFetchAsync(
                AIPlayer.Shell.Services.Infra.ImageCacheManager.NormalizeKey(url),
                async () =>
                {
                    var http = new ShellHttpClient();
                    var r = await http.GetAsync(url, timeout: TimeSpan.FromSeconds(8));
                    return r.Bytes;
                });
            if (bytes == null || bytes.Length == 0)
            {
                _imgFailedKeys.Add(key);
                _imgFailCount++;
                Program.Log("DETAIL IMG-FAIL kind=empty key=" + key + " failTotal=" + _imgFailCount);
                return null;
            }

            return await AIPlayer.Shell.Features.Aggregate.Shared.ImageSourceLoader.LoadAsync(bytes);
        }
        catch (Exception ex)
        {
            _imgFailedKeys.Add(key);
            _imgFailCount++;
            Program.Log("DETAIL IMG-FAIL kind=" + ex.GetType().Name + " key=" + key + " failTotal=" + _imgFailCount
                + " url=" + Program.MaskSecrets(url));
            return null;
        }
    }

    /// <summary>
    /// 按 `ImageUrl` **并发**取回（t215：并发度走全壳共享单点 <see cref="AIPlayer.Shell.Shell.ImageFillBudget"/>；
    /// 改前是 `foreach { await … }` 纯串行，12 张一排 ≈1.5 s）。
    /// <paramref name="row"/> 只用于**形状契约的三栏读数**：把这一行**首个成功**图的真实像素打出来，
    /// 与"请求参数/控件显示区宽高比"对齐（改前后对照就靠它，不靠肉眼猜）。
    /// <para>并发不改"每张到达即通知"的语义：`tile.Image` 的赋值仍在 **UI 线程**（本方法从 UI 线程进入、
    /// `await` 的续体回到同一 `SynchronizationContext`），因此 `TileNotifyBase` 的 INPC 通知照旧安全。</para>
    /// </summary>
    private async Task FillImagesAsync(IEnumerable<ITileImage> tiles, string row = null)
    {
        var list = tiles as System.Collections.Generic.IList<ITileImage> ?? tiles.ToList();
        InjectBadImagesForSelfTest(list);

        var all = list.Count;
        var ok = 0;
        var failed = 0;
        // t171 要求的机器读数：**逐张**卡的 `Source` 是否非空（形如 `perTile=1111111`，第 i 位 = 第 i 张）。
        // 只有它能把"某一张是占位底"从"整行都失败"里分出来（`IMG-OK 7/7` 只给总数）。
        var presence = new char[all];
        var inFlightPeakBefore = AIPlayer.Shell.Shell.ImageFillBudget.InFlightMax;

        await Task.WhenAll(list.Select(async (tile, idx) =>
        {
            var swWait = System.Diagnostics.Stopwatch.StartNew();
            using (await AIPlayer.Shell.Shell.ImageFillBudget.AcquireAsync())
            {
                swWait.Stop();
                var swFetch = System.Diagnostics.Stopwatch.StartNew();
                tile.Image = await LoadImageAsync(tile.ImageUrl);
                swFetch.Stop();
                // t215 取证：每张的**等闸门**与**取字节+解码**两段时间 —— 用来把"并发没提速"归因到
                // 「闸门等待」还是「服务端/缓存/解码本身串行」（只看总耗时无法区分这两者）。
                Program.Log("DETAIL IMG-TILE idx=" + idx + " waitMs=" + swWait.ElapsedMilliseconds
                    + " fetchMs=" + swFetch.ElapsedMilliseconds + " ok=" + (tile.Image != null ? 1 : 0));
            }

            presence[idx] = tile.Image != null ? '1' : '0';
            if (tile.Image != null)
            {
                System.Threading.Interlocked.Increment(ref ok);
            }
            else if (!string.IsNullOrEmpty(tile.ImageUrl))
            {
                // t203 ③：有 URL 却取不回来 ⇒ 该格显示可辨识失败态（不再与"还没填"同形）。
                if (tile is TileNotifyBase nb) { nb.Failed = true; }
                System.Threading.Interlocked.Increment(ref failed);
            }
        }));

        // 首张成功图的像素（按**最小下标**取，保证并发下读数仍可复现）
        var firstPx = "<none>";
        for (var i = 0; i < all; i++)
        {
            if (list[i].Image is Microsoft.UI.Xaml.Media.Imaging.BitmapImage bmp)
            {
                firstPx = bmp.PixelWidth + "x" + bmp.PixelHeight;
                break;
            }
        }

        if (all > 0)
        {
            Program.Log("DETAIL IMG-OK " + ok + "/" + all
                + (string.IsNullOrEmpty(row) ? string.Empty : " row=" + row + " firstTilePx=" + firstPx)
                + " perTile=" + new string(presence)
                + " failed=" + failed + " skipRefetch=" + _imgSkipCount
                + " budget=" + AIPlayer.Shell.Shell.ImageFillBudget.Budget
                + " inFlightMax=" + AIPlayer.Shell.Shell.ImageFillBudget.InFlightMax
                + " gateWaits=" + AIPlayer.Shell.Shell.ImageFillBudget.GateWaits
                + " completed=" + AIPlayer.Shell.Shell.ImageFillBudget.Completed
                + " peakBeforeRow=" + inFlightPeakBefore);
        }
    }

    /// <summary>
    // 滚轮已收敛到 `MainWindow.OnAnyPointerWheel`（挂根 Grid + `handledEventsToo: true`）—— 本页**不再自挂**：
    // 横向 `ListView` 的内层 ScrollViewer 会把滚轮标成 Handled（它被设成 VerticalScrollMode=Enabled 以免
    // 被映射成横滚），挂在 ListView 上的处理器收不到 ⇒ 曾经的观感就是"鼠标停在选集/季/类似上整页滚不动"。

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

    // ------------------------------------------------------------------ 头部/元信息

    private void FillHeader(EmbyItem item)
    {
        TitleText.Text = string.IsNullOrEmpty(item.SeriesName) ? item.Name : item.SeriesName;

        if (item.ParentIndexNumber is int season && item.IndexNumber is int ep)
        {
            EpisodeLineText.Text = $"S{season:00}E{ep:00} - {item.Name}";
            EpisodeLineText.Visibility = Visibility.Visible;
        }
        else
        {
            EpisodeLineText.Text = string.Empty;
            EpisodeLineText.Visibility = Visibility.Collapsed;
        }

        var bits = new List<string>();
        if (item.CommunityRating is double rating and > 0) { bits.Add("★ " + rating.ToString("0.0")); }
        if (item.ProductionYear is int year) { bits.Add(year.ToString()); }
        if (item.DurationSeconds > 0)
        {
            var span = TimeSpan.FromSeconds(item.DurationSeconds);
            bits.Add(((int)span.TotalMinutes) + "分" + span.Seconds + "秒");
        }

        if (item.Genres.Count > 0) { bits.Add(string.Join(" / ", item.Genres.Take(3))); }
        if (!string.IsNullOrEmpty(item.OfficialRating)) { bits.Add(item.OfficialRating); }

        // 分隔符一律"普通空格 + 中点"：此前用 U+3000（全角空格）⇒ 用户报障「字中间有奇怪空格」（2026-09-12）。
        MetaText.Text = string.Join(" · ", bits);

        // 主按钮文案（§6 实测形态）：**有续播点** ⇒「继续播放 H:MM:SS」，没有 ⇒「播放」。
        // 判据只认模型层 `EmbyItem.UserData.ResumeTicks`（`PlaybackPositionTicks` > 0 取它，否则回落 `PositionTicks`）。
        // 明细取数若没带回 UserData，回落**仅限同一条 id**的导航条目 —— 否则"从继续观看点进来"会显示成「播放」，
        // 而不加 id 判定的话，切到别的集也会顶着上一集的续播点。
        var resumeTicks = item.UserData?.ResumeTicks ?? 0;
        if (resumeTicks <= 0
            && string.Equals(_navItem?.Id, item.Id, StringComparison.OrdinalIgnoreCase))
        {
            resumeTicks = _navItem?.UserData?.ResumeTicks ?? 0;
        }

        var resume = resumeTicks > 0 ? TimeSpan.FromTicks(resumeTicks) : TimeSpan.Zero;
        PlayButtonText.Text = resume > TimeSpan.Zero ? ("继续播放 " + FormatClock(resume)) : "播放";
        Program.Log("DETAIL play-label item=" + item.Id + " resumeSec=" + (long)resume.TotalSeconds
            + " label=" + PlayButtonText.Text);

        OverviewText.Text = item.Overview?.Trim() ?? string.Empty;
        OverviewText.Visibility = string.IsNullOrWhiteSpace(item.Overview) ? Visibility.Collapsed : Visibility.Visible;
        LinksSection.Visibility = BuildLinks(item) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>外部链接胶囊：只显示**确实有 id** 的那几个（IMDb / TMDB / TVDB）。</summary>
    private bool BuildLinks(EmbyItem item)
    {
        var any = false;

        ImdbButton.Visibility = string.IsNullOrEmpty(item.ImdbId) ? Visibility.Collapsed : Visibility.Visible;
        TmdbButton.Visibility = string.IsNullOrEmpty(item.TmdbId) ? Visibility.Collapsed : Visibility.Visible;
        TvdbButton.Visibility = string.IsNullOrEmpty(item.TvdbId) ? Visibility.Collapsed : Visibility.Visible;
        any |= ImdbButton.Visibility == Visibility.Visible
            || TmdbButton.Visibility == Visibility.Visible
            || TvdbButton.Visibility == Visibility.Visible;

        return any;
    }

    // ------------------------------------------------------------------ 三下拉（版本 / 音轨 / 字幕）

    private void FillSources(EmbyItem item)
    {
        _sources = item.MediaSources ?? new List<EmbyMediaSource>();

        // 版本：参照图第二行 = 容器/体积/码率（用户话，不写内部字段名）
        var versions = _sources.Select((s, i) =>
        {
            var label = string.IsNullOrWhiteSpace(s.Name) ? ("版本 " + (i + 1)) : s.Name;
            var bits = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Container)) { bits.Add(s.Container.ToUpperInvariant()); }
            if (s.Size > 0) { bits.Add(FormatSize(s.Size)); }
            if (s.Bitrate > 0) { bits.Add((s.Bitrate / 1_000_000.0).ToString("0.0") + " Mbps"); }
            return bits.Count == 0 ? label : label + " · " + string.Join(" / ", bits);
        }).ToList();

        _suppress = true;
        VersionBox.ItemsSource = versions;
        _versionIndex = 0;
        if (versions.Count > 0) { VersionBox.SelectedIndex = 0; }
        VersionBox.Visibility = versions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _suppress = false;
    }

    /// <summary>
    /// 把"当前选中条目"的播放源与轨道重新取一遍（剧集场景必需：`Series` 自身 `MediaSources=0`，源在各集上）。
    /// **失败只记日志并保留上一份列表**，不清空 —— 否则选集会顺手把"换源/选轨道"变成空白。
    /// </summary>
    private async Task RefreshSourcesForAsync(EmbyItem item)
    {
        try
        {
            var detailed = await _emby.GetItemAsync(item.Id, fields: "MediaSources,MediaStreams") ?? item;
            var sources = detailed.MediaSources ?? new List<EmbyMediaSource>();
            Program.Log("DETAIL sources-for id=" + item.Id + " n=" + sources.Count);
            if (sources.Count > 0) { _sources = sources; }

            FillSources(_sources.Count > 0 ? new EmbyItem { MediaSources = _sources.ToList() } : detailed);
            FillTracks();
            FillMediaInfo();
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL SOURCES-FAIL id=" + item.Id + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private EmbyMediaSource CurrentSource
        => _versionIndex >= 0 && _versionIndex < _sources.Count ? _sources[_versionIndex] : _sources.FirstOrDefault();

    /// <summary>
    /// 音轨 / 字幕下拉：数据来自**当前选中源**的 `MediaStreams`。
    /// 选中项经 `AudioStreamIndex` / `SubtitleStreamIndex` 影响起播（内核按 id 应用）。
    /// </summary>
    private void FillTracks()
    {
        var source = CurrentSource;
        var audio = source?.AudioStreams ?? new List<EmbyMediaStream>();
        var subs = source?.SubtitleStreams ?? new List<EmbyMediaStream>();

        _suppress = true;

        AudioBox.ItemsSource = audio.Select(s => TrackLabel(s)).ToList();
        _audioIndex = audio.Count > 0 ? Math.Max(0, audio.FindIndex(s => s.IsDefault)) : -1;
        AudioBox.SelectedIndex = _audioIndex;
        AudioBox.Visibility = audio.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var subOptions = new List<string> { "关闭字幕" };
        subOptions.AddRange(subs.Select(s => TrackLabel(s)));
        SubtitleBox.ItemsSource = subOptions;
        var subDefault = subs.FindIndex(s => s.IsDefault);
        _subtitleIndex = subs.Count > 0 ? (subDefault >= 0 ? subDefault : 0) : -1;
        SubtitleBox.SelectedIndex = _subtitleIndex + 1;   // +1：第 0 项是"关闭字幕"
        SubtitleBox.Visibility = subOptions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        _suppress = false;
    }

    private static string TrackLabel(EmbyMediaStream s)
    {
        // ① 分隔符用普通空格（此前是 U+3000 全角空格 ⇒ 用户报障「字中间有奇怪空格」）；
        // ② 去重：服务端 `DisplayTitle` 里常已含 "(默认)"/"（默认）"或语言码 ⇒ 再追加就会出现
        //    "(默认)  (默认)" 这种重复（用户截图里就有）。
        var title = (string.IsNullOrWhiteSpace(s.DisplayTitle) ? (s.Language + " " + s.Codec) : s.DisplayTitle).Trim();
        var bits = new List<string> { title };

        bool TitleHas(string token) => !string.IsNullOrEmpty(token) && title.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        if (s.IsDefault && !TitleHas("默认") && !TitleHas("default")) { bits.Add("(默认)"); }
        if (s.Channels > 0) { bits.Add(s.Channels + " 声道"); }
        if (!string.IsNullOrWhiteSpace(s.Language) && !TitleHas(s.Language)) { bits.Add(s.Language); }

        return string.Join(" · ", bits.Where(b => !string.IsNullOrWhiteSpace(b)));
    }

    /// <summary>续播点时钟：≥1 小时用 <c>H:MM:SS</c>，否则 <c>M:SS</c>（不写毫秒）。</summary>
    private static string FormatClock(TimeSpan t)
        => t.TotalHours >= 1
            ? ((int)t.TotalHours) + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00")
            : ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00");

    private static string FormatSize(long bytes)
    {
        double v = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return v.ToString(u >= 3 ? "0.0" : "0") + " " + units[u];
    }

    private void FillMediaInfo()
    {
        var source = CurrentSource;
        if (source == null)
        {
            MediaInfoSection.Visibility = Visibility.Collapsed;
            return;
        }

        var cards = new List<InfoCard>();

        var fileRows = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Name)) { fileRows.Add("名称：" + source.Name); }
        if (!string.IsNullOrWhiteSpace(source.Container)) { fileRows.Add("容器：" + source.Container.ToUpperInvariant()); }
        if (source.Size > 0) { fileRows.Add("体积：" + FormatSize(source.Size)); }
        if (source.Bitrate > 0) { fileRows.Add("总码率：" + (source.Bitrate / 1_000_000.0).ToString("0.0") + " Mbps"); }
        if (source.DurationSeconds > 0) { fileRows.Add("时长：" + TimeSpan.FromSeconds(source.DurationSeconds).ToString(@"hh\:mm\:ss")); }
        cards.Add(new InfoCard { Title = "文件", Rows = fileRows });

        var video = source.MediaStreams.Where(s => s.IsVideo).ToList();
        if (video.Count > 0) { cards.Add(new InfoCard { Title = "视频", Rows = video.Select(StreamRows).SelectMany(x => x).ToList() }); }
        if (source.AudioStreams.Count > 0) { cards.Add(new InfoCard { Title = "音频", Rows = source.AudioStreams.Select(StreamRows).SelectMany(x => x).ToList() }); }

        var subs = source.SubtitleStreams;
        for (var i = 0; i < subs.Count; i++)
        {
            cards.Add(new InfoCard { Title = "字幕 " + (i + 1), Rows = StreamRows(subs[i]) });
        }

        MediaInfoCards.ItemsSource = cards;
        MediaInfoSection.Visibility = Visibility.Visible;
    }

    private static List<string> StreamRows(EmbyMediaStream s)
    {
        var rows = new List<string>
        {
            "类型：" + s.Type,
            "索引：" + s.Index,
        };
        if (!string.IsNullOrWhiteSpace(s.DisplayTitle)) { rows.Add("显示标题：" + s.DisplayTitle); }
        if (!string.IsNullOrWhiteSpace(s.Language)) { rows.Add("语言：" + s.Language); }
        if (!string.IsNullOrWhiteSpace(s.Codec)) { rows.Add("编解码器：" + s.Codec); }
        if (s.BitRate > 0) { rows.Add("码率：" + (s.BitRate / 1000) + " Kbps"); }
        if (s.Width > 0 && s.Height > 0) { rows.Add("宽高：" + s.Width + " × " + s.Height); }
        if (s.Channels > 0) { rows.Add("声道：" + s.Channels); }
        rows.Add("默认：" + (s.IsDefault ? "true" : "false"));
        rows.Add("外部：" + (s.IsExternal ? "true" : "false"));
        return rows;
    }

    // ------------------------------------------------------------------ 交互

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true) { Frame.GoBack(); }
    }

    private async void OnSeasonSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _emby == null || _series == null) { return; }
        var i = SeasonList.SelectedIndex;
        if (i < 0 || i >= _seasons.Count) { return; }

        _seasonIndex = i;
        await LoadEpisodesAsync(_series.Id, _seasons[i].Id);
    }

    private async void OnEpisodeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _emby == null) { return; }
        var i = EpisodeList.SelectedIndex;
        if (i < 0 || i >= _episodes.Count) { return; }

        _item = _episodes[i];
        FillHeader(_item);
        await SetHeroImageAsync(_item);
        await RefreshSourcesForAsync(_item);
    }

    private void OnVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) { return; }
        if (VersionBox.SelectedIndex >= 0)
        {
            _versionIndex = VersionBox.SelectedIndex;
            FillTracks();        // 源变了 ⇒ 音轨/字幕列表跟着换
            FillMediaInfo();
            Program.Log("DETAIL version-pick index=" + _versionIndex
                + " sourceId=" + (CurrentSource?.Id ?? "<none>"));
        }
    }

    private void OnAudioChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) { return; }
        _audioIndex = AudioBox.SelectedIndex;
        Program.Log("DETAIL audio-pick index=" + _audioIndex);
    }

    private void OnSubtitleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) { return; }
        _subtitleIndex = SubtitleBox.SelectedIndex - 1;   // 第 0 项 = 关闭字幕
        Program.Log("DETAIL subtitle-pick index=" + _subtitleIndex);
    }

    private async void OnSimilarSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _emby == null) { return; }
        if (SimilarList.SelectedItem is PosterTile tile && tile.Item != null)
        {
            _suppress = true;
            SimilarList.SelectedIndex = -1;
            _suppress = false;
            Frame?.Navigate(typeof(DetailPage), tile.Item);
        }
    }

    /// <summary>当前集在 `_episodes` 里的相邻项（`-1` = 上一集，`+1` = 下一集）；找不到返回 null。</summary>
    private EmbyItem NeighbourEpisode(int delta)
    {
        if (_episodes.Count == 0 || _item == null) { return null; }

        var idx = -1;
        for (var i = 0; i < _episodes.Count; i++)
        {
            if (string.Equals(_episodes[i].Id, _item.Id, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
        }

        if (idx < 0) { return null; }
        var target = idx + delta;
        return target >= 0 && target < _episodes.Count ? _episodes[target] : null;
    }

    /// <summary>
    /// 把"三下拉"的选择落到**请求**上（会话的两个轨道索引是只读的内核回传值，不能预写）：
    /// · 音轨：把选中的那条 `Selected = true`（其余清掉）—— 内核按 `Selected` 应用，命令行里是 `--audio-track=`；
    /// · 字幕：同理；另把 `SubtitleId` 设为该轨的 Emby 索引，命令行里是 `--subtitle-id=`；选"关闭字幕"则清空。
    /// </summary>
    private void ApplyTrackSelection(PlaybackRequest request)
    {
        try
        {
            var source = CurrentSource;
            var audio = source?.AudioStreams ?? new List<EmbyMediaStream>();
            var subs = source?.SubtitleStreams ?? new List<EmbyMediaStream>();

            var wantedAudio = _audioIndex >= 0 && _audioIndex < audio.Count ? audio[_audioIndex].Index : -1;
            foreach (var t in request.AudioTracks) { t.Selected = t.EmbyIndex == wantedAudio; }

            var wantedSub = _subtitleIndex >= 0 && _subtitleIndex < subs.Count ? subs[_subtitleIndex].Index : -1;
            foreach (var t in request.SubtitleTracks) { t.Selected = t.EmbyIndex == wantedSub; }
            request.SubtitleId = wantedSub >= 0 ? wantedSub.ToString() : string.Empty;

            Program.Log("DETAIL track-apply audio=" + wantedAudio + " subtitle=" + wantedSub
                + " audioTracks=" + request.AudioTracks.Count + " subtitleTracks=" + request.SubtitleTracks.Count);
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL TRACK-APPLY-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async void OnPlayClick(object sender, RoutedEventArgs e)
    {
        PlayButton.IsEnabled = false;
        try
        {
            if (_emby == null || _item == null)
            {
                StatusText.Text = "还没有可播放的条目";
                return;
            }

            if (!SettingsService.Instance.IsLoaded) { SettingsService.Instance.Load(); }
            var settings = SettingsService.Instance.Settings;
            var session = EnsureSession(settings);

            session.SetItem(_item);
            // [!] 三下拉的落点：**版本**在会话上（`VersionIndex`，公开可写、值域夹紧）；
            // **音轨/字幕**不在会话上 —— 会话的 `AudioStreamIndex`/`SubtitleStreamIndex` 是**只读**的
            // （由内核回传），预选只能落在 `PlaybackRequest` 的轨道表上（见下方 `Selected = true` 与 `SubtitleId`）。
            session.VersionIndex = _versionIndex;

            var request = await session.BuildRequestAsync(
                _item,
                // [!] **选集进播放器**：`episodeList` 是内核 `--episode-list=` 的唯一来源。
                // 不传 ⇒ 内核浮层里**没有集列表**（用户报障「现在选集都没有」的直接原因）。
                episodeList: _episodes.Count > 0 ? _episodes : null,
                previous: NeighbourEpisode(-1),
                next: NeighbourEpisode(+1));
            if (request == null || string.IsNullOrEmpty(request.MediaPath))
            {
                Program.Log("DETAIL PLAY FAIL no-url item=" + _item.Name);
                StatusText.Text = "无法解析播放地址";
                return;
            }

            ApplyTrackSelection(request);
            var launch = KernelArgumentBuilder.FromPlayback(
                request,
                settings,
                callbackUrl: ShellCallback.Url,
                libMpvPath: KernelLauncher.LibMpvPath,
                parentPid: Environment.ProcessId);

            Program.Log("PLAY-VERSION-SELECT index=" + session.VersionIndex
                + " sourceId=" + (session.CurrentSource?.Id ?? "<none>")
                + " audio=" + _audioIndex + " subtitle=" + _subtitleIndex
                + " name=" + _item.Name);
            Program.Log("PLAY resolved item=" + _item.Name
                + " needTranscode=" + session.NeedsTranscode
                + " urlLen=" + request.MediaPath.Length
                + " " + KernelArgumentBuilder.DescribeParameterSurface(launch));
            // [!] 这里**曾经**再打一行未打码的完整参数（`PLAY-KERNEL-ARGS-RAW <args>`）—— 已删除，不再打印。
            //   原因（2026-09-12 实测，非推测）：该行**没过** `SecretMasker`，于是同一批参数在日志里出现两份
            //   —— `KernelLauncher.cs:202` 的 `KERNEL-LAUNCH … args=<masked>` 是打码的，而这一行把
            //   `--http-header=<base64 X-Emby-Token>` **明文**写进日志（真实根 `aiplayer.log` 里该行曾出现
            //   26 次、单条 1,805 字符、含 3 处 `--http-header=`）。删除它**不丢信息**：同一时刻的
            //   `KERNEL-LAUNCH … args=` 给出同一串（已打码），上一行的 `DescribeParameterSurface` 给出参数面。
            //  证据：`shell/App/Features/Detail/evidence/mask-gap-kernel-args-raw.txt`（含出货版打码器实测：
            //   `--subtitle-track=<base64>` **不在** 5 条规则内 ⇒ 该残留属打码器面，另报）。

            var process = KernelLauncher.Launch(launch, Program.Log);
            StatusText.Text = process == null
                ? "内核启动失败（见日志）"
                : "已启动内核 PID " + process.Id + "：" + _item.Name;

            await Task.Delay(1500);
            LocalVideoPathSemantic.RestoreAfterPlayback(Program.Log);
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL PLAY FAIL " + ex.GetType().FullName + ": " + ex.Message);
            StatusText.Text = "播放失败：" + ex.GetType().Name;
        }
        finally
        {
            PlayButton.IsEnabled = true;
        }
    }

    /// <summary>播放/收藏/标记已看共用同一个会话（构造一次；`SetItem` 每次播放前重设）。</summary>
    private EmbyPlaybackSession EnsureSession(AppSettings settings)
    {
        if (_session != null) { return _session; }

        var http = new ShellHttpClient();
        _session = new EmbyPlaybackSession(
            _emby,
            settings,
            segmentService: new AIPlayer.Shell.Services.Segments.SegmentService(http),
            todbService: new AIPlayer.Shell.Services.Todb.TodbService(http),
            onLog: m => Program.Log("SESSION " + m));
        return _session;
    }

    private async void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!SettingsService.Instance.IsLoaded) { SettingsService.Instance.Load(); }
            var session = EnsureSession(SettingsService.Instance.Settings);
            session.SetItem(_item);
            var ok = await session.ToggleFavoriteAsync();
            StatusText.Text = ok ? "已切换收藏状态" : "收藏操作未成功（见日志）";
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL FAVORITE-FAIL " + ex.GetType().Name + ": " + ex.Message);
            StatusText.Text = "收藏失败：" + ex.GetType().Name;
        }
    }

    private async void OnWatchedClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!SettingsService.Instance.IsLoaded) { SettingsService.Instance.Load(); }
            var session = EnsureSession(SettingsService.Instance.Settings);
            session.SetItem(_item);
            var ok = await session.TogglePlayedAsync();
            StatusText.Text = ok ? "已切换已看状态" : "标记未成功（见日志）";
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL WATCHED-FAIL " + ex.GetType().Name + ": " + ex.Message);
            StatusText.Text = "标记失败：" + ex.GetType().Name;
        }
    }

    private void OnCopyLinkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = string.IsNullOrEmpty(_emby?.BaseUrl)
                ? string.Empty
                : _emby.BaseUrl + "/web/index.html#!/item?id=" + _item.Id;
            if (string.IsNullOrEmpty(url))
            {
                StatusText.Text = "该服务器没有可复制的条目链接";
                return;
            }

            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(url);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            StatusText.Text = "链接已复制";
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL COPYLINK-FAIL " + ex.GetType().Name + ": " + ex.Message);
            StatusText.Text = "复制失败：" + ex.GetType().Name;
        }
    }

    private void OnImdbClick(object sender, RoutedEventArgs e) => OpenExternal("https://www.imdb.com/title/" + _item?.ImdbId);

    private void OnTmdbClick(object sender, RoutedEventArgs e) => OpenExternal("https://www.themoviedb.org/movie/" + _item?.TmdbId);

    private void OnTvdbClick(object sender, RoutedEventArgs e) => OpenExternal("https://thetvdb.com/?tab=series&id=" + _item?.TvdbId);

    private void OpenExternal(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || url.EndsWith("/", StringComparison.Ordinal)) { return; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL OPEN-LINK-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ 视图模型

    /// <summary>海报/季卡共用的"可后填图片"契约：先建卡（占位底色），再异步把图填进去。</summary>
    public interface ITileImage
    {
        ImageSource Image { get; set; }

        string ImageUrl { get; set; }
    }

    /// <summary>
    /// 可后填图片的卡基类：<c>Image</c> 变化时**通知绑定**。
    /// <para>[!] 这是"演职人员第 2–6 张显示为占位底而日志却是 `IMG-OK 7/7`"的**真因**（不是截图时机）：
    /// 三个卡类原本是裸自动属性，而 `ListView` 的容器在**图填进去之前**就可能已经生成 ⇒
    /// `{Binding Image}` 只在那一次求值过，之后 `tile.Image = …` 不会触发任何刷新 ⇒ 那一格永远是占位底。
    /// 与首页 `PosterTile` 的做法对齐（同一族缺陷，那边已用 INPC 修过）。</para>
    /// </summary>
    public abstract class TileNotifyBase : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private ImageSource _image;
        private bool _failed;

        public ImageSource Image
        {
            get => _image;
            set
            {
                _image = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Image)));
            }
        }

        /// <summary>
        /// t203 ③：**取图失败要看得见**。判据 = 有 URL 但取不回来（`Image == null` 且 `ImageUrl` 非空）；
        /// “本来就没图”（URL 为空）不算失败 —— 那种格子留着占位底是正确的。
        /// <para>为什么必须可见：旧行为下失败格子只有一行日志，界面上与"还没填"完全同形 ⇒ 用户看到的是
        /// “图没对应上 / 某几格永远空着”而没有任何可归因的信号（这正是用户报障的那一类观感）。</para>
        /// </summary>
        public bool Failed
        {
            get => _failed;
            set
            {
                if (_failed == value) { return; }
                _failed = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Failed)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FailedVisibility)));
            }
        }

        /// <summary>失败态覆盖层的可见性（XAML 里四个卡模板共用这一条绑定）。</summary>
        public Microsoft.UI.Xaml.Visibility FailedVisibility =>
            _failed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public string ImageUrl { get; set; } = string.Empty;
    }

    public sealed class PosterTile : TileNotifyBase, ITileImage
    {
        public string Caption { get; set; } = string.Empty;

        public EmbyItem Item { get; set; }
    }

    /// <summary>季卡：海报 + 名称（角标数字用 `Count`）。</summary>
    public sealed class SeasonTile : TileNotifyBase, ITileImage
    {
        public string Caption { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    /// <summary>演职人员卡：头像 + 姓名 + 角色（点卡片进该人作品列表）。</summary>
    public sealed class PersonTile : TileNotifyBase, ITileImage
    {
        public string PersonId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }

    /// <summary>媒体信息分卡：`Title` + 若干 `Rows`（每行一句"字段：值"）。</summary>
    public sealed class InfoCard
    {
        public string Title { get; set; } = string.Empty;

        public List<string> Rows { get; set; } = new List<string>();
    }
}

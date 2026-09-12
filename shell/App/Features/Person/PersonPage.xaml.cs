// t58（U-L 分片一）人物屏的代码后置。
//
// 三态（**不许混说**）：
//   · [逆向到了] 头像卡（头像 + 姓名白 + 角色灰、卡宽 144 / 间距 16、无照片 `#FF333333`）与海报几何
// （166×249、列间距 24、右上角紫色圆形角标）—— 均取自 `HILLSLITE_UI_ANALYSIS.md` / `UI_SPEC_SHELL.md`· 的**实测值**；
//   · [重建实现了] 本屏是"人物页"，**参照图里没有这一屏** ⇒ 整体构图维度一律 `INCONCLUSIVE(无参照)`
//     （见 `evidence/t58-person-selftest.txt` 的差异表），只有"复用了实测几何"的子元素才有实测依据；
//   · [运行验证过了] 自检（`SHELL_SELFTEST_PERSON=1`）在**真实 Emby** 上取到真人物 + 真作品，读数落证据文件。
//
// 取数与缓存：一律经 `EmbyService`（`GetItemsAsync(personIds: …)`）+ **同一个** `MediaSnapshotSource`
// 的 SWR 入口（`LoadListAsync`）—— 规格依据 `Services/AGGREGATION_INCREMENTAL.md` 与 `UI_SPEC_SHELL.md`。
//
// 起播：复用 t27 打通的 M3 链路（`AggregatePlayback.Play`）。**缓存态拿不到真条目 ⇒ 明确拒绝 + 可见提示**
// （快照是渲染面白名单，缺 `Raw`/`MediaSources`），绝不静默用快照条目起播。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Aggregate;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Person;

/// <summary>人物屏：某位演职人员的作品列表。`NavTag` = <c>person</c>。</summary>
public sealed partial class PersonPage : Page
{
    /// <summary>导航 tag（外壳 `NavigateTo("person")` 用；case 归 `ui`，见本卡报告）。</summary>
    public const string NavTag = "person";

    /// <summary>`SHELL_SELFTEST_PERSON=1` ⇒ 起屏后自动自检并写证据。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_PERSON";

    /// <summary>自检/手工取证时指定人物（留空 ⇒ 从主源真实数据里**自动取一位**）。</summary>
    public const string PersonIdEnvVar = "SHELL_PERSON_ID";

    public const string PersonNameEnvVar = "SHELL_PERSON_NAME";

    /// <summary>指定服务器（按 Id 或名称包含匹配；留空 ⇒ 主源 = 第一台已启用的 Emby 系服务器）。</summary>
    public const string ServerEnvVar = "SHELL_PERSON_SERVER";

    /// <summary>人物作品的类型集合（与 芯片同一套 Emby 类型名）。</summary>
    public const string WorkItemTypes = "Movie,Series,Episode,BoxSet,Video";

    private readonly ObservableCollection<AggregatePoster> _works = new ObservableCollection<AggregatePoster>();

    private ServerConfig _server;
    private string _personId = string.Empty;
    private string _personName = string.Empty;

    /// <summary>
    /// 导航带过来的**人物条目本身**（Id = 人物 Id）。带着它就带着 <c>ImageTags</c> ⇒ 能直接判"有没有头像"；
    /// 为 null 时（env 起屏 / 从剧集推人）才去取一次，见 <see cref="EnsureAvatarAsync"/>。
    /// </summary>
    private EmbyItem _personItem;

    /// <summary>本次取数拿到的**真条目**（起播与自检读它；只渲染缓存时为 null）。</summary>
    private List<EmbyItem> _liveItems;

    private SwrOutcome _lastOutcome;
    private MediaSnapshot _snapshot;
    private bool _loading;
    private bool _avatarRequested;

    public PersonPage()
    {
        InitializeComponent();
        WorkList.ItemsSource = _works;
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // 导航参数约定与 `Features/Detail/DetailPage` 一致：直接传 `EmbyItem`（Id = 人物 Id、Name = 姓名）。
        if (e?.Parameter is EmbyItem item && !string.IsNullOrEmpty(item.Id))
        {
            _personId = item.Id;
            _personName = item.Name ?? string.Empty;
            _personItem = item;      // 留整条：头像要不要发请求，判据（ImageTags）就在它身上
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_personId))
        {
            _personId = (Environment.GetEnvironmentVariable(PersonIdEnvVar) ?? string.Empty).Trim();
            _personName = (Environment.GetEnvironmentVariable(PersonNameEnvVar) ?? string.Empty).Trim();
        }

        // 人物/合集屏复用聚合卡的渲染器 ⇒ 把"封面失败/无图"这类**卡片级**日志改道到本屏自己的证据文件，
        // 不污染 t31 的证据（`AggregateDiagnostics` 是那一族的落点）。
        AggregateDiagnostics.Redirect = PersonEvidence.AppendLine;

        await ReloadAsync();

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            try
            {
                await PersonSelfTest.RunAsync(this);
            }
            catch (Exception ex)
            {
                Program.Log("PERSON selftest FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    /// <summary>取数（缓存优先 + 并发刷新；）：T0 缓存渲染 → T1 就地替换 → 失败保留 + 可见提示。</summary>
    public async Task ReloadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = "正在读取这位演职人员的作品…";
        WorkList.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;

        try
        {
            _server = _server ?? ResolveServer();
            if (_server == null)
            {
                ShowEmpty("没有可用的服务器", "去「服务器」页添加或启用一台 Emby 服务器后再回来。");
                return;
            }

            if (string.IsNullOrEmpty(_personId))
            {
                ShowEmpty("未指定演职人员", "本屏需要从详情页的《演职人员》里点某个人进来（或由自检注入人物 Id）。");
                return;
            }

            NameText.Text = string.IsNullOrEmpty(_personName) ? "（未知名）" : _personName;
            UpdateSubtitleText();

            _liveItems = null;      // 「真条目」只代表**本次**取数（否则刷新失败时会残留上一次的）

            var outcome = await MediaSnapshotSource.LoadListAsync(
                MediaSnapshotSource.KeyForPerson(_server.Id, _personId),
                _server,
                FetchWorksAsync,
                onCacheValue: lookup =>
                {
                    _lastOutcome = new SwrOutcome
                    {
                        HasCache = lookup.HasValue,
                        Freshness = lookup.Freshness,
                        AgeSeconds = lookup.AgeSeconds,
                        Cached = lookup.Value,
                    };
                    _snapshot = lookup.Value;
                    RenderFromSnapshot(lookup.Value);
                    FirstPosterAtCacheRender = _works.Count > 0 ? _works[0] : null;
                    WorksAtCacheRender = _works.Count;
                    Program.Log("PERSON T0-cache-render " + MediaSnapshotSource.Describe(_lastOutcome));
                },
                onLiveItems: items => _liveItems = items);

            _lastOutcome = outcome;

            if (outcome.RefreshSucceeded && outcome.Value != null)
            {
                var changed = _snapshot == null || !SnapshotRenderer.SameIds(outcome.CachedIds, outcome.RefreshedIds);
                if (changed)
                {
                    _snapshot = outcome.Value;
                    RenderFromSnapshot(outcome.Value);
                    Program.Log("PERSON T1-replace " + MediaSnapshotSource.Describe(outcome));
                }
                else
                {
                    Program.Log("PERSON T1-noop ids-identical（不重绑 ⇒ 不闪屏/不重置滚动）");
                    UpdateSummaryText();
                }

                await MaybeLoadImagesAsync();
            }
            else
            {
                if (_snapshot != null)
                {
                    RenderFromSnapshot(_snapshot);
                }

                var preserved = outcome.CachePreserved || _snapshot != null;
                FailedText.Text = "刷新失败：" + (string.IsNullOrEmpty(outcome.Error) ? "（无原因）" : outcome.Error)
                    + (preserved ? "｜**已保留缓存内容**（未清空、未删缓存）" : string.Empty);
                FailedText.Visibility = Visibility.Visible;
                Program.Log("PERSON refresh-failed " + MediaSnapshotSource.Describe(outcome));
            }

            await EnsureAvatarAsync();
        }
        catch (Exception ex)
        {
            Program.Log("PERSON load FAIL " + ex.GetType().FullName + ": " + ex.Message);
            ShowEmpty("取数失败", ex.GetType().Name + "（明细见日志）");
        }
        finally
        {
            _loading = false;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>真取数：`GET /Users/{userId}/Items?PersonIds={personId}`（经既有 `EmbyService`，不自造通道）。</summary>
    private async Task<List<EmbyItem>> FetchWorksAsync(CancellationToken ct)
    {
        var emby = new EmbyService(new ShellHttpClient(), _server, onLog: m => Program.Log("PERSON " + m));
        var result = await emby.GetItemsAsync(
            includeItemTypes: WorkItemTypes,
            personIds: _personId,
            limit: 200,
            cancellationToken: ct).ConfigureAwait(false);
        return result?.Items ?? new List<EmbyItem>();
    }

    private void RenderFromSnapshot(MediaSnapshot snapshot)
    {
        _works.Clear();
        foreach (var group in snapshot?.Groups ?? new List<SnapshotGroup>())
        {
            foreach (var item in group.Items ?? new List<SnapshotItem>())
            {
                _works.Add(SnapshotRenderer.ToPoster(item, _server, showProgress: false));
            }
        }

        WorkList.Visibility = _works.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        UpdateSummaryText();
        // [!] 副标题带计数 ⇒ **必须跟着渲染走**：早先把赋值放在 `MaybeLoadImagesAsync()` 之后，
        //     结果 171 张封面串行加载期间屏上一直显示「作品 0 部」（截图实测抓到，不只是理论问题）。
        UpdateSubtitleText();

        var failed = snapshot?.FailedServers ?? new List<string>();
        if (failed.Count > 0)
        {
            FailedText.Text = "失败 " + failed.Count + " 台：" + string.Join(" / ", failed);
            FailedText.Visibility = Visibility.Visible;
        }

        if (_works.Count == 0 && failed.Count == 0)
        {
            ShowEmpty("这位演职人员暂无作品",
                "主源返回 0 条 —— 这是**数据面**的 0（该服务器上该人物的作品列表为空），不是取数失败。");
        }
    }

    /// <summary>摘要行：**两个数各自具名**（作品数 + 缓存态），不写"一致"这类合并表述。</summary>
    private void UpdateSummaryText()
        => SummaryText.Text = "作品 " + _works.Count + " 部"
            + (_lastOutcome != null
                ? " · 缓存=" + (_lastOutcome.HasCache ? "命中" : "未命中") + "（" + _lastOutcome.Freshness + "）"
                : string.Empty);

    /// <summary>副标题（服务器 + 作品数）；与摘要行同源，避免两处计数打架。</summary>
    private void UpdateSubtitleText()
        => SubtitleText.Text = "演职人员 · " + (_server?.Name ?? "?") + " · 作品 " + _works.Count + " 部";

    /// <summary>头像：人物图 `Items/{id}/Images/Primary`；取不到 ⇒ 保持 `#FF333333` 占位（ 实测值）。</summary>
    private async Task EnsureAvatarAsync()
    {
        if (_avatarRequested || _server == null || string.IsNullOrEmpty(_personId))
        {
            return;
        }

        _avatarRequested = true;
        try
        {
            var emby = new EmbyService(new ShellHttpClient(), _server, onLog: m => { });

            // 取图前先判"到底有没有头像"：判据只认服务层唯一入口 `ImageUrlIfAvailable` ——
            // 无 `ImageTags.Primary` 的条目请求 `/Items/{id}/Images/Primary` 会被服务端以 **HTTP 500** 回答（t70/t78 实测），
            // 所以**无图 ⇒ 空串 ⇒ 不发请求**，保持 `#FF333333` 占位。
            // 该判据要 tag：优先用导航带过来的条目；env 起屏 / 从剧集推人时手里只有 id
            // ⇒ 取一次该人物条目（带字段集，含 ImageTags）。这是**一条元数据**请求，换掉那条必然 500 的图片请求。
            var personItem = _personItem;
            if (personItem == null || personItem.ImageTags == null || personItem.ImageTags.Count == 0)
            {
                personItem = (await emby.GetItemAsync(_personId).ConfigureAwait(true)) ?? personItem;
            }

            var url = emby.ImageUrlIfAvailable(personItem, "Primary", maxHeight: 300);
            if (string.IsNullOrEmpty(url))
            {
                // 与下面的 catch 分开：这里是"确无图"（证据可区分），那边是"取图出事"（带异常类型）
                PersonEvidence.AppendLine("avatar-no-image person=" + _personId
                    + " itemAvailable=" + (personItem != null)
                    + " ownPrimary=" + (personItem?.ImageTags != null && personItem.ImageTags.ContainsKey("Primary")));
                return;
            }

            // [!] 取字节走**服务层单点**（t145 / UI_MAP §4）：`ImageCacheManager.GetOrFetchAsync(url)`
            //     —— 命中**不联网**（本地磁盘缓存）；未命中才经它内部的 `ShellHttpClient`（统一 UA/代理/重试）
            //     取回并落盘；失败返回 **null + 一行可见 `IMG-CACHE …` 日志**（不抛）。
            //     这样头像与首页/详情/搜索用**同一份缓存与同一套 UA/代理口径**，不新增第二个取图栈，
            //     也不再有"裸 `HttpClient` 绕过 UA/代理"这一类出口（`ShellHttpClient` 手工 new 一个也不行：无缓存）。
            var bytes = await AIPlayer.Shell.Services.Infra.ImageCacheManager.Default
                .GetOrFetchAsync(url)
                .ConfigureAwait(true);
            var bitmap = await Aggregate.Shared.ImageSourceLoader.LoadAsync(bytes);
            if (bitmap != null)
            {
                AvatarImage.Source = bitmap;
                AvatarImage.Visibility = Visibility.Visible;
                AvatarPlaceholder.Visibility = Visibility.Collapsed;
                PersonEvidence.AppendLine("avatar-loaded bytes=" + (bytes?.Length ?? 0));
            }
            else
            {
                PersonEvidence.AppendLine("avatar-decode-null bytes=" + (bytes?.Length ?? 0));
            }
        }
        catch (Exception ex)
        {
            // 头像取不到**不影响本屏**（占位照常显示）；但**不静默**：落一行证据（含异常类型，不含 URL）。
            PersonEvidence.AppendLine("avatar-fail " + ex.GetType().Name);
        }
    }

    private async Task MaybeLoadImagesAsync()
    {
        // 自检模式不自动加载封面（否则测不到"占位 → 封面"这一跳；与 t31 同一条仪器纪律）
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            return;
        }

        foreach (var poster in _works)
        {
            await poster.EnsureImageAsync();
        }
    }

    /// <summary>起播用：拿**本次取数**的真条目（快照缺 `Raw`/`MediaSources`，不能直接起播）。</summary>
    public EmbyItem LiveItemFor(string itemId)
    {
        if (_liveItems == null || string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        foreach (var item in _liveItems)
        {
            if (string.Equals(item.Id, itemId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 主源解析（t191 / U-T）：走**服务层唯一实现** `PrimaryServerResolver` ——
    /// 口径 = 「`LastServerId` 指向的**可用**台 ⇒ 否则第一台可用台」（可用 = `Enabled && Kind == Emby`），
    /// 与首页 / 详情 / 收藏 / 媒体库 / 搜索**逐字一致**；`SHELL_PERSON_SERVER` 的显式覆盖保留并**优先于主源**。
    /// <para>改前本屏走家族 B：**完全不读 `LastServerId`**、候选面是 Emby∪Jellyfin ⇒ 用户换主源后仍看第一台。
    /// 读数由解析器落一行 `PRIMARY-SERVER screen=person id=… name=… source=env|lastServerId|fallback …`。</para>
    /// </summary>
    private ServerConfig ResolveServer()
        => AIPlayer.Shell.Services.Infra.PrimaryServerResolver.Resolve("person", ServerEnvVar, log: Program.Log);

    private void ShowEmpty(string title, string detail)
    {
        EmptyTitle.Text = title;
        EmptyDetail.Text = detail;
        EmptyPanel.Visibility = Visibility.Visible;
        WorkList.Visibility = Visibility.Collapsed;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        // 由外壳框架的回退栈决定能不能回退（本屏不自己造回退栈）。
        if (Frame != null && Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        Program.Log("PERSON back-ignored canGoBack=false");
        FailedText.Text = "没有可返回的上一屏";
        FailedText.Visibility = Visibility.Visible;
    }

    /// <summary>点作品 → 起播（复用 t27 的 M3 链路；缓存态必须拦住，见 `LiveItemFor`）。</summary>
    private void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AggregatePoster poster || poster.Item == null || _server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Item.Id);
        if (live == null)
        {
            Program.Log("PERSON play-blocked no-live-item（当前只有缓存快照）item=" + poster.Item.Name);
            FailedText.Text = "该条目当前来自缓存快照，尚未取到可播放的完整条目｜请稍候重试（或重新进入本页触发刷新）";
            FailedText.Visibility = Visibility.Visible;
            return;
        }

        Program.Log("PERSON play-click item=" + live.Name + " server=" + (_server.Name ?? "?"));
        AggregatePlayback.Play(_server, live);
    }

    // ── 自检/取证访问面（只读；不参与生产逻辑）────────────────────────────────

    /// <summary>
    /// 自检用：没有注入人物 Id 时，**从真实数据里取一位**（取一部剧的 `People[0]`）。
    /// 不做任何硬编码 id —— 硬编码会让证据变成"这台机器上恰好有一个 id"的一次性读数。
    /// </summary>
    public async Task<string> DerivePersonFromRealDataAsync(CancellationToken ct = default)
    {
        _server = _server ?? ResolveServer();
        if (_server == null)
        {
            return string.Empty;
        }

        var emby = new EmbyService(new ShellHttpClient(), _server, onLog: m => Program.Log("PERSON " + m));
        var series = await emby.GetItemsAsync(
            includeItemTypes: "Series", limit: 8, fields: "People", cancellationToken: ct).ConfigureAwait(false);

        foreach (var candidate in series?.Items ?? new List<EmbyItem>())
        {
            var full = await emby.GetItemAsync(candidate.Id, fields: "People", cancellationToken: ct).ConfigureAwait(false);
            var person = FirstPersonOf(full);
            if (person.Id.Length > 0)
            {
                _personId = person.Id;
                _personName = person.Name;
                Program.Log("PERSON derived from real data series=" + candidate.Name + " person=" + person.Name);
                return _personId;
            }
        }

        return string.Empty;
    }

    /// <summary>从条目原始 JSON 的 `People[0]` 取 (Id, Name, Role) —— 服务层 DTO 没有 `People` 字段，故读 Raw。</summary>
    public static (string Id, string Name, string Role) FirstPersonOf(EmbyItem item)
    {
        if (item?.Raw == null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var people = Services.Util.JsonRead.Items(Services.Util.JsonRead.From(item.Raw), "People");
        foreach (var person in people)
        {
            var id = Services.Util.JsonRead.Str(person, "Id");
            if (id.Length == 0)
            {
                continue;
            }

            return (id, Services.Util.JsonRead.Str(person, "Name"), Services.Util.JsonRead.Str(person, "Role"));
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    public IReadOnlyList<AggregatePoster> Works => _works;

    public ServerConfig CurrentServer => _server;

    public string PersonId => _personId;

    public string PersonNameText => NameText.Text;

    public string SubtitleTextValue => SubtitleText.Text;

    public string SummaryTextValue => SummaryText.Text;

    public string FailedTextValue => FailedText.Text;

    public SwrOutcome LastOutcome => _lastOutcome;

    public MediaSnapshot CurrentSnapshot => _snapshot;

    public bool SkeletonVisible => LoadingPanel.Visibility == Visibility.Visible;

    public bool AvatarLoaded => AvatarImage.Visibility == Visibility.Visible;

    /// <summary>T0 缓存渲染当刻的首张卡片实例（T1 就地性判据：刷新落地后不得被换）。</summary>
    public AggregatePoster FirstPosterAtCacheRender { get; private set; }

    public int WorksAtCacheRender { get; private set; }

    public Task ReloadForSelfTestAsync() => ReloadAsync();

    public async Task LoadImagesForSelfTestAsync()
    {
        foreach (var poster in _works)
        {
            await poster.EnsureImageAsync();
        }
    }
}

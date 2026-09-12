// t58（U-L 分片二）合集屏的代码后置。
//
// 三态（不许混说）：
// · [逆向到了] 海报几何（166×249、列间距 24、右上角紫色圆形角标、标题白 + 副标题灰）=/ 实测值；
//   · [重建实现了] "合集页"这一屏在参照图里**没有对应物** ⇒ 整体构图 `INCONCLUSIVE(无参照)`；
//   · [运行验证过了] 自检在真实 Emby 上取到真合集（`IncludeItemTypes=BoxSet`）与其条目。
//
// 取数：一律经 `EmbyService.GetItemsAsync(parentId: boxSetId, …)`（**不自造取数通道**）；
//       缓存优先复用**同一个** `MediaSnapshotSource` 的 SWR 入口（`LoadListAsync`，键 = `collection:{serverId}:{boxSetId}`）。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Aggregate;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Collection;

/// <summary>合集屏：一个合集（BoxSet）里的条目。`NavTag` = <c>collection</c>。</summary>
public sealed partial class CollectionPage : Page
{
    /// <summary>导航 tag（外壳 `NavigateTo("collection")` 用；case 归 `ui`，见本卡报告）。</summary>
    public const string NavTag = "collection";

    public const string SelfTestEnvVar = "SHELL_SELFTEST_COLLECTION";

    public const string CollectionIdEnvVar = "SHELL_COLLECTION_ID";

    public const string CollectionNameEnvVar = "SHELL_COLLECTION_NAME";

    public const string ServerEnvVar = "SHELL_COLLECTION_SERVER";

    /// <summary>合集内的条目类型（与 芯片同一套 Emby 类型名）。</summary>
    public const string ChildItemTypes = "Movie,Series,Episode,Video,BoxSet";

    private readonly ObservableCollection<AggregatePoster> _items = new ObservableCollection<AggregatePoster>();

    private ServerConfig _server;
    private string _boxSetId = string.Empty;
    private string _boxSetName = string.Empty;
    private List<EmbyItem> _liveItems;
    private SwrOutcome _lastOutcome;
    private MediaSnapshot _snapshot;
    private bool _loading;

    public CollectionPage()
    {
        InitializeComponent();
        ItemList.ItemsSource = _items;
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e?.Parameter is EmbyItem item && !string.IsNullOrEmpty(item.Id))
        {
            _boxSetId = item.Id;
            _boxSetName = item.Name ?? string.Empty;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_boxSetId))
        {
            _boxSetId = (Environment.GetEnvironmentVariable(CollectionIdEnvVar) ?? string.Empty).Trim();
            _boxSetName = (Environment.GetEnvironmentVariable(CollectionNameEnvVar) ?? string.Empty).Trim();
        }

        AggregateDiagnostics.Redirect = CollectionEvidence.AppendLine;
        await ReloadAsync();

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            try
            {
                await CollectionSelfTest.RunAsync(this);
            }
            catch (Exception ex)
            {
                Program.Log("COLLECTION selftest FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    /// <summary>缓存优先取数（ 同一条语义：T0 渲染 → T1 就地替换 → 失败保留 + 可见提示）。</summary>
    public async Task ReloadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = "正在读取合集条目…";
        ItemList.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;

        try
        {
            _server = _server ?? ResolveServer();
            if (_server == null)
            {
                ShowEmpty("没有可用的服务器", "去「服务器」页添加或启用一台 Emby 服务器后再回来。");
                return;
            }

            if (string.IsNullOrEmpty(_boxSetId))
            {
                ShowEmpty("未指定合集", "本屏需要从搜索结果里点一个《合集》条目进来（或由自检注入合集 Id）。");
                return;
            }

            SubtitleText.Text = string.Empty;
            NameText.Text = string.IsNullOrEmpty(_boxSetName) ? "（未命名合集）" : _boxSetName;
            _liveItems = null;

            var outcome = await MediaSnapshotSource.LoadListAsync(
                MediaSnapshotSource.KeyForCollection(_server.Id, _boxSetId),
                _server,
                FetchChildrenAsync,
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
                    FirstPosterAtCacheRender = _items.Count > 0 ? _items[0] : null;
                    Program.Log("COLLECTION T0-cache-render " + MediaSnapshotSource.Describe(_lastOutcome));
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
                    Program.Log("COLLECTION T1-replace " + MediaSnapshotSource.Describe(outcome));
                }
                else
                {
                    Program.Log("COLLECTION T1-noop ids-identical（不重绑 ⇒ 不闪屏/不重置滚动）");
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
                Program.Log("COLLECTION refresh-failed " + MediaSnapshotSource.Describe(outcome));
            }

            UpdateSubtitleText();
        }
        catch (Exception ex)
        {
            Program.Log("COLLECTION load FAIL " + ex.GetType().FullName + ": " + ex.Message);
            ShowEmpty("取数失败", ex.GetType().Name + "（明细见日志）");
        }
        finally
        {
            _loading = false;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>真取数：`GET /Users/{userId}/Items?ParentId={boxSetId}`（合集内条目）。</summary>
    private async Task<List<EmbyItem>> FetchChildrenAsync(CancellationToken ct)
    {
        var emby = new EmbyService(new ShellHttpClient(), _server, onLog: m => Program.Log("COLLECTION " + m));
        var result = await emby.GetItemsAsync(
            parentId: _boxSetId,
            includeItemTypes: ChildItemTypes,
            limit: 200,
            cancellationToken: ct).ConfigureAwait(false);
        return result?.Items ?? new List<EmbyItem>();
    }

    private void RenderFromSnapshot(MediaSnapshot snapshot)
    {
        _items.Clear();
        foreach (var group in snapshot?.Groups ?? new List<SnapshotGroup>())
        {
            foreach (var item in group.Items ?? new List<SnapshotItem>())
            {
                _items.Add(SnapshotRenderer.ToPoster(item, _server, showProgress: false));
            }
        }

        ItemList.Visibility = _items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        UpdateSummaryText();
        // [!] 副标题带计数 ⇒ **必须跟着渲染走**：否则封面串行加载期间屏上会一直显示「条目 0 个」
        //     （人物屏截图实测抓到过这个形态，不只是理论问题）。
        UpdateSubtitleText();

        var failed = snapshot?.FailedServers ?? new List<string>();
        if (failed.Count > 0)
        {
            FailedText.Text = "失败 " + failed.Count + " 台：" + string.Join(" / ", failed);
            FailedText.Visibility = Visibility.Visible;
        }

        if (_items.Count == 0 && failed.Count == 0)
        {
            ShowEmpty("这个合集里暂无条目",
                "主源返回 0 条 —— 这是**数据面**的 0（该合集本身为空），不是取数失败。");
        }
    }

    private void UpdateSummaryText()
        => SummaryText.Text = "条目 " + _items.Count + " 个"
            + (_lastOutcome != null
                ? " · 缓存=" + (_lastOutcome.HasCache ? "命中" : "未命中") + "（" + _lastOutcome.Freshness + "）"
                : string.Empty);

    /// <summary>副标题（服务器 + 条目数）；与摘要行同源，避免两处计数打架。</summary>
    private void UpdateSubtitleText()
        => SubtitleText.Text = "合集 · " + (_server?.Name ?? "?") + " · 条目 " + _items.Count + " 个";

    private async Task MaybeLoadImagesAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SelfTestEnvVar)))
        {
            return;   // 自检模式不自动加载封面（否则测不到"占位 → 封面"这一跳）
        }

        foreach (var poster in _items)
        {
            await poster.EnsureImageAsync();
        }
    }

    /// <summary>
    /// 自检用**反控**：同一合集、**不加类型过滤**再取一次。
    /// 目的：把"这个合集是空的"与"我的类型过滤太窄"分开 —— 少了这一发，
    /// 一个 0 条读数**无法归因**（两者都能产生 0），那不是证据。
    /// </summary>
    public async Task<(int Filtered, int Unfiltered)> ProbeChildrenCountsAsync(CancellationToken ct = default)
    {
        var emby = new EmbyService(new ShellHttpClient(), _server, onLog: m => { });
        var filtered = await emby.GetItemsAsync(
            parentId: _boxSetId, includeItemTypes: ChildItemTypes, limit: 200,
            cancellationToken: ct).ConfigureAwait(false);
        var unfiltered = await emby.GetItemsAsync(
            parentId: _boxSetId, limit: 200,
            cancellationToken: ct).ConfigureAwait(false);
        return (filtered?.Items.Count ?? 0, unfiltered?.Items.Count ?? 0);
    }

    /// <summary>起播用：本次取数的真条目（快照缺 `Raw`/`MediaSources`，不能直接起播）。</summary>
    public EmbyItem LiveItemFor(string itemId)    {
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
    /// 自检用：没有注入合集 Id 时，从真实数据里取一个。**遍历多台服务器 × 两种查询形态**
    /// （全局 `IncludeItemTypes=BoxSet` + 逐个库 `ParentId={viewId}`），并返回尝试矩阵 ——
    /// 否则"找不到合集"这个结论**无法归因**（是这台没有？还是我的查询形态不对？）。
    /// </summary>
    public async Task<(string Id, string Name, List<string> Attempts)> DeriveCollectionDetailedAsync(CancellationToken ct = default)
    {
        var attempts = new List<string>();
        var servers = MediaAggregator.EnabledEmbyServers();
        var wanted = (Environment.GetEnvironmentVariable(ServerEnvVar) ?? string.Empty).Trim();
        if (wanted.Length > 0)
        {
            servers = servers.Where(s => string.Equals(s.Id, wanted, StringComparison.OrdinalIgnoreCase)
                || (s.Name ?? string.Empty).IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        var limit = 3;
        var raw = Environment.GetEnvironmentVariable(MediaAggregator.LimitServersEnvVar);
        if (int.TryParse(raw, out var n) && n > 0)
        {
            limit = n;
        }

        foreach (var server in servers.Take(limit))
        {
            var emby = new EmbyService(new ShellHttpClient(), server, onLog: m => { });
            try
            {
                var global = await emby.GetItemsAsync(
                    includeItemTypes: "BoxSet", limit: 5, fields: "ImageTags", cancellationToken: ct).ConfigureAwait(false);
                attempts.Add(server.Name + "/global=" + (global?.Items.Count ?? 0));
                var hit = global?.Items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Id));
                if (hit != null)
                {
                    _server = server;
                    _boxSetId = hit.Id;
                    _boxSetName = hit.Name ?? string.Empty;
                    return (_boxSetId, _boxSetName, attempts);
                }

                var views = await emby.GetViewsAsync(ct).ConfigureAwait(false);
                foreach (var view in views ?? new List<EmbyUserView>())
                {
                    var perView = await emby.GetItemsAsync(
                        parentId: view.Id, includeItemTypes: "BoxSet", limit: 5,
                        fields: "ImageTags", cancellationToken: ct).ConfigureAwait(false);
                    attempts.Add(server.Name + "/view:" + view.Name + "=" + (perView?.Items.Count ?? 0));
                    var found = perView?.Items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Id));
                    if (found != null)
                    {
                        _server = server;
                        _boxSetId = found.Id;
                        _boxSetName = found.Name ?? string.Empty;
                        return (_boxSetId, _boxSetName, attempts);
                    }
                }
            }
            catch (Exception ex)
            {
                // 单台失败不拖垮探测：记下形态与异常类型（不记 URL）
                attempts.Add(server.Name + "/error=" + ex.GetType().Name);
            }
        }

        return (string.Empty, string.Empty, attempts);
    }

    /// <summary>自检用：没有注入合集 Id 时，从真实数据里取一个（`IncludeItemTypes=BoxSet`）。</summary>
    public async Task<string> DeriveCollectionFromRealDataAsync(CancellationToken ct = default)
    {
        var (id, _, _) = await DeriveCollectionDetailedAsync(ct).ConfigureAwait(false);
        if (id.Length > 0)
        {
            Program.Log("COLLECTION derived from real data boxset=" + _boxSetName);
        }

        return id;
    }

    /// <summary>
    /// 主源解析（t191 / U-T）：走**服务层唯一实现** `PrimaryServerResolver`（与人物屏、以及首页/详情/收藏/
    /// 媒体库/搜索同一口径）；`SHELL_COLLECTION_SERVER` 的显式覆盖保留并**优先于主源**。
    /// <para>改前本屏走家族 B（不读 `LastServerId`、候选面 Emby∪Jellyfin）。
    /// 读数由解析器落一行 `PRIMARY-SERVER screen=collection id=… name=… source=…`。</para>
    /// </summary>
    private ServerConfig ResolveServer()
        => AIPlayer.Shell.Services.Infra.PrimaryServerResolver.Resolve("collection", ServerEnvVar, log: Program.Log);

    private void ShowEmpty(string title, string detail)
    {
        EmptyTitle.Text = title;
        EmptyDetail.Text = detail;
        EmptyPanel.Visibility = Visibility.Visible;
        ItemList.Visibility = Visibility.Collapsed;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Frame != null && Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        Program.Log("COLLECTION back-ignored canGoBack=false");
        FailedText.Text = "没有可返回的上一屏";
        FailedText.Visibility = Visibility.Visible;
    }

    private void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AggregatePoster poster || poster.Item == null || _server == null)
        {
            return;
        }

        var live = LiveItemFor(poster.Item.Id);
        if (live == null)
        {
            Program.Log("COLLECTION play-blocked no-live-item（当前只有缓存快照）item=" + poster.Item.Name);
            FailedText.Text = "该条目当前来自缓存快照，尚未取到可播放的完整条目｜请稍候重试（或重新进入本页触发刷新）";
            FailedText.Visibility = Visibility.Visible;
            return;
        }

        Program.Log("COLLECTION play-click item=" + live.Name + " server=" + (_server.Name ?? "?"));
        AggregatePlayback.Play(_server, live);
    }

    // ── 自检/取证访问面（只读）──────────────────────────────────────────────

    public IReadOnlyList<AggregatePoster> Items => _items;

    public ServerConfig CurrentServer => _server;

    public string BoxSetId => _boxSetId;

    public string NameTextValue => NameText.Text;

    public string SubtitleTextValue => SubtitleText.Text;

    public string SummaryTextValue => SummaryText.Text;

    public string FailedTextValue => FailedText.Text;

    public SwrOutcome LastOutcome => _lastOutcome;

    public MediaSnapshot CurrentSnapshot => _snapshot;

    public bool SkeletonVisible => LoadingPanel.Visibility == Visibility.Visible;

    public AggregatePoster FirstPosterAtCacheRender { get; private set; }

    public Task ReloadForSelfTestAsync() => ReloadAsync();

    public async Task LoadImagesForSelfTestAsync()
    {
        foreach (var poster in _items)
        {
            await poster.EnsureImageAsync();
        }
    }
}

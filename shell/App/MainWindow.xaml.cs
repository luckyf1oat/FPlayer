using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Servers;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AIPlayer.Shell;

/// <summary>
/// 外壳主窗（t26 / U-A 新 UI 全局框架，HillsLite 风格）。
/// 权威：shell/docs/UI_SPEC_SHELL.md §2（布局）/§3（侧栏）；令牌：Theme/Tokens.xaml（t26 独占维护）。
///
/// 三件事都接真实数据（用户硬约束「功能和 UI 绑定」）：
///   ① 侧栏服务器行 ← `ServerConfigStore.Instance`（生产唯一构造点）；
///   ② 失败态名称 `#FFADB6` ← **真实连通探测**（`EmbyService.GetViewsAsync`，8 s 超时），不是装饰；
///   ③ 拖拽排序 → `ServerConfig.SortIndex` + `Save()` ⇒ 落盘并能在重启后保持。
/// </summary>
public sealed partial class MainWindow : Window
{
    public const string StartPageEnvVar = "SHELL_START_PAGE";

    /// <summary>反控钩子：给绝对 URL ⇒ **临时构造一行（不落盘、不入库）**制造**真实**连通失败，用于取失败态颜色。</summary>
    public const string FailProbeEnvVar = "SHELL_SELFTEST_RAIL_FAIL";

    /// <summary>
    /// t304 自检钩子：逗号分隔的步骤脚本 `items / tap-server / nav-library / nav-home / dump`。
    /// 只做**只读枚举**与"等价用户手势"的导航调用（`tap-server` 走的正是 `ServerList_Tapped` 这个处理器），
    /// 不写任何数据；与 `SHELL_START_PAGE` 正交（本钩子不改落点，只在同一进程里依次触发以供取运行期读数）。
    /// </summary>
    public const string RailSelfTestEnvVar = "SHELL_SELFTEST_RAIL";

    private const string LastPlayedKey = "cachedLastPlayedAt";

    private const string FailProbeId = "rail-failprobe";

    private ServerConfigStore _store;

    public ObservableCollection<ServerRow> ServerRows { get; } = new ObservableCollection<ServerRow>();

    public MainWindow()
    {
        InitializeComponent();

        // 滚轮的**唯一权威**：挂在根 Grid 上 + `handledEventsToo: true`（内层 ScrollViewer 会把滚轮标成
        // Handled=true ⇒ 挂在 ListView / 页面上的处理器**收不到**）。判据与理由见 OnAnyPointerWheel。
        RootGrid.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnAnyPointerWheel), true);
        Title = "AI Player";
        ApplyDarkCaption();
        ServerList.ItemsSource = ServerRows;

        // t183：三态 tooltip 的挂载点（挂在行容器上，不改 XAML 模板 —— 本卡 inScope 只有本文件）。
        ServerList.ContainerContentChanging += OnServerContainerChanging;

        // 退出侧收尾的挂点**只在 `App` 注册一次**（`App.OnLaunched` 里 `_window.Closed += …`）——
        // 这里**不重复注册**（早前我在两处都挂了，实测日志出现两条 `EXIT-CTX window:MainWindow.Closed` ⇒ 已收敛为一处）。

        LoadServers();

        // t183 取证锚：此刻**每一行**都还是"未探测"（`ProbeAllAsync` 已发出，但一行都还没返回）。
        // 这一行与首个 `RAIL-ROW-STATE … state=` 之间的时长就是**未探测窗口的实测长度**。
        Program.Log("RAIL state-initial unknown rows=" + ServerRows.Count);

        // 启动时 ListView 会**自动选中首行** ⇒ 侧栏看起来"选中了 ServerA"，但主区在首页（用户报障：
        // "看似选中了第一个服务器 但页面不是"）。⇒ 显式清掉这个"假选中"，让选中态只在用户真的点了之后出现。
        ServerList.SelectedItem = null;
        ShellState.Current.Refresh();
        UpdateStatusBar();

        // 返回按钮的可用性跟随 Frame 的真实回退栈（旧实现里它恒可点却常常无反应 ⇒ 观感 = "回不去"）。
        // t288：搜索页的「卡片点击 → 详情页」也挂在这一处（见 WireSearchCardNavigation 的理由）。
        // 顺带落一行**内容区真实身份**（页面类型 + tag + 能否回退）：t288 的验收① 就是这一条，
        // 没有它就只能拿"目标页自己的 OnNavigatedTo 跑到了"间接论证。
        ContentFrame.Navigated += (_, __) =>
        {
            UpdateBackButton();
            WireSearchCardNavigation();
            Program.Log("NAV content=" + (ContentFrame.Content?.GetType().Name ?? "-")
                + " tag=" + ShellState.Current.CurrentTag
                + " canGoBack=" + ContentFrame.CanGoBack);
        };
        UpdateBackButton();

        NavigateTo(StartTag());
        MaybeOpenDetailSelfTest();
        MaybeRunRailSelfTest();

        // 主源切换**只认用户手势**（`ServerList_Tapped`）—— 不再需要"启动初期先收闸"这类时序开关：
        // 那个闸门（`_userSelectionArmed`）只在 ctor 末尾置位，挡不住"异步填行后 ListView 自动选中"
        // 这类**迟到**的程序性选中（实测会在 `MainWindow ready` 后 ~2 s 触发 `SRV-SWITCH` + 跳库视图）。
        Program.Log("MainWindow ready (new shell frame); title=" + Title);
    }

    /// <summary>
    /// t183：把「三态 tip」挂到**行容器**上（`ListViewItem`）。tip 的 `Content` 用 `Source=row` 的绑定指向
    /// <see cref="ServerRow.StateToolTip"/> ⇒ 探测返回后 tip 会**跟着状态变**，不会留下"未探测"的过期文案。
    /// 只在代码里挂、不改 `MainWindow.xaml`：本卡 inScope 只有本文件（XAML 属他人面）。
    /// 同一行可能被回收复用（`InRecycleQueue`）⇒ 回收队列里的容器不重挂；重新实体化时会带上新行的绑定。
    /// </summary>
    private static void OnServerContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not ServerRow row) { return; }

        try
        {
            var tip = new ToolTip();
            tip.SetBinding(ContentControl.ContentProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(nameof(ServerRow.StateToolTip)),
                Source = row,
                Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay,
            });
            ToolTipService.SetToolTip(args.ItemContainer, tip);
            Program.Log("RAIL-ROW-TIP attached row=" + row.Name + " state=" + row.StateTag + " tip=" + row.StateToolTip);
        }
        catch (Exception ex)
        {
            // tip 挂不上不该影响侧栏可用性：留一行审计即可（与 RAIL-ICON-LOAD-FAIL 同族处置）。
            Program.Log("RAIL-ROW-TIP-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ==================================================================================
    // t26 补遗②-b（captain 2026-09-11 授权修「浅色系统下浅色面」这一缺陷）：
    //   根节点 `RequestedTheme="Dark"` 只能管 XAML 内容 —— 实测标题栏区域（bitmap y=4..30）
    //   仍是 #FFFFFF，因为系统标题栏由 AppWindow/DWM 画、不在 XAML 树里。
    //   这里用 DWM 的 DWMWA_USE_IMMERSIVE_DARK_MODE(20) 让标题栏走暗色（Win10 2004+ / Win11）。
    //   机制照旧：**不改主题、不改系统设置**，只对**本窗口**生效；失败只留审计行、不抛。
    // ==================================================================================
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void ApplyDarkCaption()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var on = 1;
            var hr = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
            Program.Log("CAPTION dark-mode hwnd=" + hwnd + " attr=" + DwmwaUseImmersiveDarkMode + " hr=" + hr);
        }
        catch (Exception ex)
        {
            Program.Log("CAPTION dark-mode FAILED " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ===================== 侧栏数据 =====================

    private void LoadServers()
    {
        try
        {
            // 🔴 生产唯一构造点：Instance（显式 At(...) 不触发原版 accounts.json 回落 ⇒ 0 台）
            _store = ServerConfigStore.Instance;
            _store.Load();

            ServerRows.Clear();
            var servers = _store.Servers?.OrderBy(s => s.SortIndex).ToList() ?? new List<ServerConfig>();
            foreach (var s in servers)
            {
                var row = new ServerRow(s, _store.CanWriteServer(s.Id));
                ServerRows.Add(row);
                // 手柄外观与说明都取决于"当下是否可写"，日志同时打出两个集合的期望色，便于像素自检对照。
                Program.Log("RAIL row name=" + row.Name + " canWrite=" + row.CanWrite + " readOnly=" + row.IsReadOnly
                    + " handleColor=" + row.HandleColorHex + " handleToolTip=" + row.HandleToolTip);
            }

            Program.Log("RAIL loaded " + ServerRows.Count + " servers; originalAccounts="
                + _store.OriginalAccountsLoaded + "; legacy=" + _store.LegacyServersLoaded);

            var bad = Environment.GetEnvironmentVariable(FailProbeEnvVar);
            if (!string.IsNullOrWhiteSpace(bad))
            {
                // 插到**第 0 行**：与第 1 行形成"失败 #FFADB6 vs 正常 白"的可见对照（供像素自检取坐标）
                ServerRows.Insert(0, new ServerRow(new ServerConfig
                {
                    Id = FailProbeId,
                    Name = "(反控)不可达服务器",
                    Kind = ServerKind.Emby,
                    BaseUrl = bad.Trim(),
                    Enabled = true,
                }, canWrite: false));
                Program.Log("RAIL fail-probe row added url=" + Program.MaskSecrets(bad.Trim()));
            }

            _ = ProbeAllAsync();

            if (ServerRows.Count > 0)
            {
                ServerList.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Program.Log("MainWindow.LoadServers failed: " + ex);
        }
    }

    /// <summary>真实连通探测（决定失败态颜色）。语义与服务器页「测试连接」一致。</summary>
    /// <remarks>
    /// t296：改走**服务层并发探测** <see cref="ServerProbeRunner.ProbeAllAsync"/>（cap = <see cref="ServerProbeRunner.DefaultMaxConcurrency"/>）。
    /// 三点刻意不变：① 文案仍由 <see cref="ServerRow.SetStatus"/> 决定（成功 `连通 · N 库` / 失败 `连接失败：&lt;异常类型名&gt;`）；
    /// ② 每台结果**一到就回填该行**（`onResult`）⇒ `SetStatus` 内既有那行 `RAIL-ROW-STATE …` 照旧**逐台**打出（不合并、不吞）；
    /// ③ 调用点仍是 fire-and-forget（`_ = ProbeAllAsync();`，见 <c>LoadServers</c>）。
    /// 与旧实现的差异只有两处：**同时最多 N 台在飞**（旧：严格逐台）+ **每台各自 8 s 超时**（旧：同一阈值，但串行叠加）。
    /// <para>🔴 回填**按对象引用**建映射、**不按 Id**：`ServerConfigStore` 合并 accounts.json + servers.json，
    /// Id 不保证唯一（实测同名已有实例：`ServerD` 两台）；按 Id 建字典一旦撞键就会**静默漏掉一行**的状态行，
    /// 而"失败台必须逐台可见"正是本路径的硬要求（t296 反控：同 Id 两台的合成件在 Id 映射下 `byId.Count=1`）。</para>
    /// </remarks>
    private async Task ProbeAllAsync()
    {
        var rows = ServerRows.ToList();
        var byServer = new Dictionary<ServerConfig, ServerRow>();
        foreach (var row in rows)
        {
            if (row?.Server != null)
            {
                byServer[row.Server] = row;   // 类默认比较器 = **引用相等**（不按 Id）
            }
        }

        await ServerProbeRunner.ProbeAllAsync(
            rows.Where(r => r?.Server != null).Select(r => r.Server),
            timeout: TimeSpan.FromSeconds(8),                          // 与旧实现同一阈值
            maxConcurrency: ServerProbeRunner.DefaultMaxConcurrency,   // 常量（经验值、可调），不散落魔数
            onResult: r =>
            {
                if (r?.Server == null)
                {
                    return;
                }

                if (!byServer.TryGetValue(r.Server, out var row))
                {
                    Program.Log("RAIL-PROBE NOROW name=" + r.Name + " ok=" + r.Ok + " detail=" + r.Detail);
                    return;
                }

                // ① **审计行在工作线程当场打出**（同一格式、逐台一行）：可见性不押在 dispatcher 上
                //    （t296 实测过"入队被丢 ⇒ 少一行"那一幕，见 ServerRow.SetStatusCore 的注释）。
                row.SetStatusCore(r.Ok, r.Detail);

                // ② 绑定通知回 UI 线程（有界重试）；逐台入队 ⇒ 保留"边测边点亮"的既有观感。
                _ = RaiseRowStatusAsync(row, r);
            },
            onLog: line => Program.Log(line)).ConfigureAwait(true);
    }

    /// <summary>t296：把某一行的绑定通知送回 UI 线程（有界重试；失败只留诊断，**不影响已打出的审计行**）。</summary>
    private async Task RaiseRowStatusAsync(ServerRow row, ServerProbeResult r)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (DispatcherQueue.TryEnqueue(row.RaiseStatusChanged))
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        Program.Log("RAIL-PROBE ENQUEUE-FAIL name=" + r.Name + " attempts=20（状态行已出，仅 UI 未刷新）");
    }

    // ===================== 交互 =====================

    /// <summary>
    /// 自检钩子（默认关闭）：`SHELL_DETAIL_ITEM=<itemId>` ⇒ 起窗后**直接进该条目的详情页**。
    /// 为什么需要它：详情页的取数面（季/集/源/轨道）**不该依赖"媒体库先加载成功"** ——
    /// 库里任何一台服务器抽风都会让"经库进详情"的路径失效，从而让自检变成"没验到"而不是"失败"。
    /// 详情页自己会 `GetItemAsync(id)` 取全量，所以这里只给一个带 Id 的壳条目即可。
    /// </summary>
    private void MaybeOpenDetailSelfTest()
    {
        var itemId = Environment.GetEnvironmentVariable(DetailItemEnvVar);
        if (string.IsNullOrWhiteSpace(itemId)) { return; }

        try
        {
            Program.Log("DETAIL-DIRECT open id=" + itemId);
            ShellState.Current.CurrentTag = Features.Detail.DetailPage.NavTag;
            ContentFrame.Navigate(typeof(Features.Detail.DetailPage), new Services.Models.EmbyItem { Id = itemId.Trim(), Name = itemId.Trim() });
            SyncRailSelection(Features.Detail.DetailPage.NavTag);
            UpdateBackButton();
        }
        catch (Exception ex)
        {
            Program.Log("DETAIL-DIRECT FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>见 <see cref="MaybeOpenDetailSelfTest"/>。</summary>
    public const string DetailItemEnvVar = "SHELL_DETAIL_ITEM";

    /// <summary>
    /// t304 自检执行体：按 <see cref="RailSelfTestEnvVar"/> 的步骤脚本驱动 rail，步骤间留间隔让日志可读。
    /// 取的就是卡面 acceptance ①–⑤ 那几格运行期读数：rail 项序列 / 冷启动落点 / 点服务器行落点 /
    /// rail「媒体库」往返 / `dump` 现态。**不改落点、不写数据**。
    /// </summary>
    private async void MaybeRunRailSelfTest()
    {
        var script = Environment.GetEnvironmentVariable(RailSelfTestEnvVar);
        if (string.IsNullOrWhiteSpace(script)) { return; }

        // 等异步填行（LoadServers → ProbeAllAsync）完成，否则 `tap-server` 没有可点的行。
        await Task.Delay(4000);
        Program.Log("RAIL-SELFTEST begin steps=" + script);

        foreach (var raw in script.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var step = raw.Trim().ToLowerInvariant();
            switch (step)
            {
                case "items":
                {
                    var seq = string.Join(",", RailNavList.Items.OfType<ListBoxItem>()
                        .Select(i => i.Tag as string ?? "<none>"));
                    var sel = (RailNavList.SelectedItem as ListBoxItem)?.Tag as string ?? "<none>";
                    Program.Log("RAIL-ITEMS count=" + RailNavList.Items.Count + " seq=" + seq + " selected=" + sel);
                    break;
                }
                case "tap-server":
                {
                    if (ServerList.Items.Count == 0)
                    {
                        Program.Log("RAIL-SELFTEST tap-server skipped rows=0");
                        break;
                    }
                    ServerList.SelectedItem = ServerList.Items[0];
                    ServerList_Tapped(this, null);   // 与真实点击走**同一个**处理器
                    Program.Log("RAIL-SELFTEST tap-server row-index=0");
                    break;
                }
                case "nav-library":
                case "nav-home":
                {
                    var tag = step.Substring(4);     // nav-library ⇒ library ｜ nav-home ⇒ home
                    var hit = RailNavList.Items.OfType<ListBoxItem>()
                        .FirstOrDefault(i => string.Equals(i.Tag as string, tag, StringComparison.Ordinal));
                    if (hit is null)
                    {
                        Program.Log("RAIL-SELFTEST nav tag=" + tag + " rail-item-missing");
                        break;
                    }
                    RailNavList.SelectedItem = hit;  // 选中 ⇒ SelectionChanged ⇒ NavigateTo(tag)
                    Program.Log("RAIL-SELFTEST nav tag=" + tag + " rail-item-found=True");
                    break;
                }
                case "dump":
                    Program.Log("RAIL-SELFTEST dump tag=" + ShellState.Current.CurrentTag
                        + " content=" + (ContentFrame.Content?.GetType().Name ?? "-"));
                    break;
                case "retap":
                {
                    // 等价"点已选中的那一项"：走真实处理器；按设计**不重复导航**（tag 与当前页相同 ⇒ 不压回退栈），
                    // 但必须**不崩、不回归**（旧缺陷正是这条路 no-op）。
                    var sel = (RailNavList.SelectedItem as ListBoxItem)?.Tag as string ?? "<none>";
                    Program.Log("RAIL-SELFTEST retap before tag=" + ShellState.Current.CurrentTag + " selected=" + sel);
                    RailNav_Tapped(this, null);
                    Program.Log("RAIL-SELFTEST retap after tag=" + ShellState.Current.CurrentTag
                        + " canGoBack=" + ContentFrame.CanGoBack);
                    break;
                }
                default:
                    Program.Log("RAIL-SELFTEST unknown-step=" + step);
                    break;
            }

            await Task.Delay(900);
        }

        Program.Log("RAIL-SELFTEST done");
    }

    /// <summary>由 `NavigateTo` 反向同步 rail 选中态时置位 ⇒ 抑制 `SelectionChanged` 造成的递归导航。</summary>
    private bool _navSyncing;

    private void RailNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navSyncing) { return; }

        if (RailNavList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    /// <summary>
    /// rail 项被**点击**（含"点已选中的那一项"）。`SelectionChanged` 只在选中项**变化**时触发 ⇒
    /// 「进设置页后回点首页」在旧实现里是 no-op（用户 2026-09-12 报障：切换无反应）。
    /// 这里按"当前页不是目标页"补一次导航；重复导航由 `NavigateTo` 自己去重，不会压两次回退栈。
    /// </summary>
    private void RailNav_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (RailNavList.SelectedItem is ListBoxItem item && item.Tag is string tag
            && !string.Equals(ShellState.Current.CurrentTag, tag, StringComparison.Ordinal))
        {
            NavigateTo(tag);
        }
    }

    private void ServerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只同步"行是否选中"的视觉状态；**不再由它切换主源**（见下）。
        foreach (var row in ServerRows)
        {
            row.IsSelected = ReferenceEquals(row, ServerList.SelectedItem);
        }

        // 🔴 2026-09-12 修「启动后自发跳页」缺陷（实测日志：`MainWindow ready` 后 ~2 s 出现
        //   `SRV-SWITCH id=… name=ServerA` + `Nav -> library`，且**同一毫秒出现两次**）：
        //   `_userSelectionArmed` 只在 ctor 末尾置位 ⇒ 任何**迟到的** `SelectionChanged` 都会被当成用户选择；
        //   而 `LoadServers()` 是异步的（服务器状态探测完成后才填行），ListView 在**行出现那一刻**会把首行
        //   自动选中 ⇒ 触发本事件 ⇒ 静默切主源 + 强制跳库视图（并覆盖 `SHELL_START_PAGE`）。
        //   时序相关的缺陷没法靠"再收一道闸"根治 ⇒ **主源切换只认用户手势**（`ServerList_Tapped` /
        //   后续如加键盘导航，也要显式走 SwitchMainSource，绝不复用"选中项变化"这个信号）。
        if (_navSyncing) { return; }
    }

    /// <summary>
    /// 点侧栏服务器行的**点击**（含"点已经选中的那一行"）—— 这是**唯一**的主源切换入口。
    /// 为什么不靠 `SelectionChanged`：① 它只在选中项**变化**时触发 ⇒ 点"看起来已选中"的那台是 no-op
    /// （用户报障「点第一个服务器也没反应」）；② 它会**被程序性选中**触发（异步填行 / 键盘 / 焦点）⇒
    /// 启动后会自发切主源并跳到库视图（实测缺陷）。用户手势语义明确，用它就没有这两类问题。
    /// </summary>
    private void ServerList_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ServerList.SelectedItem is ServerRow row)
        {
            SwitchMainSource(row);
        }
    }

    /// <summary>
    /// 把"当前主源"落到设置里（`AppSettings.LastServerId` = 全仓唯一的主源字段，搜索面已在读它），
    /// 然后进入库视图。落盘失败只记日志、**不阻断导航** —— 否则一次写盘失败会让整个侧栏点不动。
    /// </summary>
    private void SwitchMainSource(ServerRow row)
    {
        try
        {
            var svc = AIPlayer.Shell.Services.Settings.SettingsService.Instance;
            if (svc?.Settings is { } settings)
            {
                settings.LastServerId = row.Server.Id;
                svc.Save(settings);
                Program.Log("SRV-SWITCH id=" + row.Server.Id + " name=" + row.Name + " persisted=True");
            }
            else
            {
                Program.Log("SRV-SWITCH id=" + row.Server.Id + " name=" + row.Name + " persisted=False (no settings service)");
            }
        }
        catch (Exception ex)
        {
            Program.Log("SRV-SWITCH-PERSIST-FAIL id=" + row.Server.Id + " " + ex.GetType().Name + ": " + ex.Message);
        }

        // 主源变了 ⇒ 用 `force` 重进**首页**（绕过"同页不重导航"的去重）：首页的 Hero/继续观看/媒体库行
        // 都是按"当前主源"取数的 ⇒ 换源后必须重建，才能看到新主源的首页；媒体库改由 rail「媒体库」主动进入
        // （用户 2026-09-12 报障第⑤条原话：「点服务器默认是首页」；改前此处强跳 library，:-1 那版会在换源后
        //  把用户丢进库视图，与"点服务器 = 看这台服务器的首页"的语义不符）。
        NavigateTo("home", force: true);
    }

    /// <summary>拖拽排序 → 写回 SortIndex 并落盘（§3 [契约]：拖拽排序 → ServerConfig.SortIndex）。</summary>
    private void ServerList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        try
        {
            for (var i = 0; i < ServerRows.Count; i++)
            {
                var row = ServerRows[i];
                if (row.Server.Id == FailProbeId)
                {
                    continue; // 反控行不入库
                }

                if (row.Server.SortIndex == i)
                {
                    continue;
                }

                row.Server.SortIndex = i;
                _store.Update(row.Server.With(sortIndex: i));
            }

            _store.Save();
            Program.Log("RAIL-REORDER saved " + string.Join(",", ServerRows.Select(r => r.Name)));
        }
        catch (Exception ex)
        {
            Program.Log("RAIL-REORDER FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ==================================================================================
    // t26 挂载点 ①（ui3 / t30）：侧栏服务器行**右键菜单**
    //   ui3 交付 `Features/Servers/ServerRowContextMenu`，公开
    //     AttachTo(FrameworkElement row, ServerConfig server, Action onChanged)
    //   接入 = 把 SEAM① 那一行打开即可（菜单本体 + §3.1 的 11 个 handler 全归 ui3，避免两套）。
    //   下面这段已把「被点的是哪一行」解析好并留审计行，ui3 不必再自己找 DataContext。
    //   状态：重建实现了（未运行验证过 —— 右键属用户输入，按 §14 不驱动）。
    // ==================================================================================
    private void ServerList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        var row = RowOf(args.OriginalSource as DependencyObject);
        if (row == null)
        {
            return;
        }

        Program.Log("RAIL-CONTEXT row=" + row.Name + " readOnly=" + row.IsReadOnly + " source=" + (args.OriginalSource?.GetType().Name ?? "null"));

        // SEAM①（ui3 / t30 已接，2026-09-12）：菜单本体与 11 个 handler 归 ui3；刷新回调落点 = OnServersChanged（LoadServers）。
        ServerRowContextMenu.AttachTo(args.OriginalSource as FrameworkElement, row.Server, OnServersChanged);
    }

    /// <summary>从被点元素沿可视树回溯到承载该行的 ServerRow（容器 DataContext）。</summary>
    private static ServerRow RowOf(DependencyObject start)
    {
        var node = start;
        while (node != null)
        {
            if (node is FrameworkElement fe && fe.DataContext is ServerRow r)
            {
                return r;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    /// <summary>侧栏服务器数据变更后的统一刷新点 = ui3 的 `onChanged` 回调落点。</summary>
    private void OnServersChanged() => LoadServers();

    private void RailBack_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
            Program.Log("Nav back; canGoBack=" + ContentFrame.CanGoBack);
            UpdateBackButton();
        }
    }

    private void ServerManage_Click(object sender, RoutedEventArgs e) => NavigateTo("servers");

    // t26 挂载点 ②-a（ui3 / t30）：设置页。
    //   ui3 交付 `Features.Settings.SettingsPage` 后，把下面这行换成
    //     ContentFrame.Navigate(typeof(Features.Settings.SettingsPage));
    private void Settings_Click(object sender, RoutedEventArgs e) => NavigateTo("settings");

    // t26 挂载点 ②-b（ui3 / t30 已接，2026-09-12）：添加服务器对话框本体归 ui3。
    //   XamlRoot 非空的前提由点击路径保证（ContentFrame 此时必已 Loaded）。
    private async void AddServer_Click(object sender, RoutedEventArgs e)
    {
        var ok = await Features.Servers.AddServerDialog.ShowAsync(ContentFrame.XamlRoot);
        if (ok)
        {
            OnServersChanged();
        }
    }

    private void SearchInput_GotFocus(object sender, RoutedEventArgs e) => Program.Log("SEARCH focus");

    // ==================================================================================
    // t28 挂载（ui3 的 SearchPage）：顶栏常驻搜索框 → 搜索态（§7.2 入口 = 顶栏搜索框，
    //   「不跳屏、内容区就地切换」= 把内容区导航到 SearchPage 并把当前词交出去）。
    //   300 ms 去抖由 SearchPage.SetQuery 内部自己做（外壳不重复实现）。
    //   状态：重建实现了 —— 输入驱动属用户输入，按 §14 不在自检里模拟；ui3 用自己的
    //   SHELL_SELFTEST_SEARCH 钩子取运行证据。
    // ==================================================================================
    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var term = SearchInput.Text ?? string.Empty;
        if (ContentFrame.Content is Features.Search.SearchPage current)
        {
            current.SetQuery(term);
            return;
        }

        if (term.Length == 0)
        {
            return; // 空词且不在搜索态 ⇒ 什么都不做（不为了清空而跳屏）
        }

        ContentFrame.Navigate(typeof(Features.Search.SearchPage), term);
        ShellState.Current.CurrentTag = Features.Search.SearchPage.NavTag;
        UpdateStatusBar();
        Program.Log("SEARCH enter term-length=" + term.Length);
    }

    /// <summary>
    /// t288 反控钩子（默认关闭）：非空 ⇒ **故意不接** `SearchPage.CardActivated`，
    /// 复现"点了没反应"的改前语义（点击应落到 SearchPage 自己的 `handler=none` 行，不许静默）。
    /// </summary>
    public const string SearchUnwireEnvVar = "SHELL_SEARCH_UNWIRE";

    /// <summary>
    /// t288：把搜索页的「卡片点击」接到详情页。**唯一接线点** —— 挂在 `ContentFrame.Navigated`（而不是散在
    /// 两处导航分支上：`:459` 顶栏输入、`:517` 路由表），因为每次 Navigate 都会新建/复用页面实例，
    /// 只有"内容区真的变成 SearchPage 之后"这一处能保证**无论从哪个入口进搜索态都接上**。
    /// <para>幂等：先退订再订阅。Frame 复用页面实例时重复订阅会让**一次点击弹两次详情**。</para>
    /// </summary>
    private void WireSearchCardNavigation()
    {
        if (ContentFrame.Content is not Features.Search.SearchPage page) { return; }

        page.CardActivated -= OnSearchCardActivated;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SearchUnwireEnvVar)))
        {
            Program.Log("SEARCH nav-wire disabled reason=" + SearchUnwireEnvVar);
            return;
        }

        page.CardActivated += OnSearchCardActivated;
        Program.Log("SEARCH nav-wire wired page=" + page.GetType().Name);
    }

    /// <summary>
    /// t288：搜索卡 → 详情页。搜索是全仓**唯一跨服务器**的面，而详情页取数只认"主源"
    /// （`DetailPage.ResolveServer` → `AppSettings.LastServerId`）。所以卡片来自别的源时，
    /// **必须先把主源切到它** —— 否则详情页会拿主源去查这个 id，用户看到的是"读不到条目详情"的空壳，
    /// 比"点了没反应"更难懂。切源语义与侧栏点服务器行一致（都是用户手势），只记日志、不阻断导航。
    /// </summary>
    private void OnSearchCardActivated(object sender, Features.Search.ResultCard card)
    {
        if (card == null || string.IsNullOrEmpty(card.ItemId))
        {
            Program.Log("SEARCH-NAV skip reason=no-item-id");
            return;
        }

        var switched = SwitchMainSourceForCard(card);

        var item = new AIPlayer.Shell.Services.Models.EmbyItem
        {
            Id = card.ItemId,
            Name = card.Title,
            Type = card.Type,
        };

        ShellState.Current.CurrentTag = Features.Detail.DetailPage.NavTag;
        Program.Log("SEARCH-NAV detail id=" + card.ItemId + " type=" + card.Type
            + " server=\"" + card.ServerName + "\" serverId=" + (string.IsNullOrEmpty(card.ServerId) ? "-" : card.ServerId)
            + " mainSourceSwitched=" + (switched ? "1" : "0"));
        ContentFrame.Navigate(typeof(Features.Detail.DetailPage), item);
        UpdateStatusBar();
    }

    /// <summary>
    /// 主源跟随卡片所属服务器（口径与 <see cref="SwitchMainSource"/> 一致：写 `LastServerId` + 落盘）。
    /// 返回 <c>true</c> = 真的换了。写盘失败只记日志、**不阻断导航**。
    /// </summary>
    private bool SwitchMainSourceForCard(Features.Search.ResultCard card)
    {
        if (string.IsNullOrEmpty(card.ServerId)) { return false; }

        var svc = AIPlayer.Shell.Services.Settings.SettingsService.Instance;
        if (svc?.Settings is not { } settings)
        {
            Program.Log("SEARCH-NAV source-switch skipped reason=no-settings-service to=" + card.ServerId);
            return false;
        }

        var from = settings.LastServerId ?? string.Empty;
        if (string.Equals(from, card.ServerId, StringComparison.Ordinal)) { return false; }

        settings.LastServerId = card.ServerId;
        svc.Save(settings);
        Program.Log("SEARCH-NAV source-switch from=" + (from.Length == 0 ? "-" : from) + " to=" + card.ServerId
            + " name=\"" + card.ServerName + "\"");
        return true;
    }

    private void SearchInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var page = ContentFrame.Content as Features.Search.SearchPage;
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            page?.CommitQuery(SearchInput.Text ?? string.Empty);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            page?.ClearQuery();
            SearchInput.Text = string.Empty;
            e.Handled = true;
        }
    }

    private void SearchClear_Click(object sender, RoutedEventArgs e) => SearchInput.Text = string.Empty;

    // ===================== 导航与状态 =====================

    private static string StartTag()
    {
        var raw = Environment.GetEnvironmentVariable(StartPageEnvVar);
        // 默认落点 = **首页**（`Features/Home` 已落地：Hero + 继续观看 + 媒体库行）。
        // 历史：首页未做时这里临时指向 "library"（否则一进来就掉 `PlaceholderPage`）；现已恢复为 "home"。
        return string.IsNullOrWhiteSpace(raw) ? "home" : raw.Trim();
    }

    private void NavigateTo(string tag, bool force = false)
    {
        // 同一个 tag 的重复导航（`SelectionChanged` 与 `Tapped` 都会来）⇒ 只同步状态，不再压一次回退栈。
        // `force: true` 用于"主源已变"这类**必须重建页面**的场景（切服务器后库视图要按新主源重取）。
        if (!force
            && string.Equals(ShellState.Current.CurrentTag, tag, StringComparison.Ordinal)
            && ContentFrame.Content?.GetType() == PageTypeFor(tag))
        {
            SyncRailSelection(tag);
            UpdateBackButton();
            return;
        }

        Program.Log("Nav -> " + tag);
        ShellState.Current.CurrentTag = tag;

        switch (tag)
        {
            case "home":
                // 首页（`Features/Home`）：Hero + 继续观看 + 媒体库行；**默认落点**（见 `StartTag()`）。
                ContentFrame.Navigate(typeof(Features.Home.HomePage));
                break;
            case "search":
                // t28 挂载（ui3 的 Features/Search/SearchPage）：导航参数 = 初查词，由 SearchPage 自己读。
                ContentFrame.Navigate(typeof(Features.Search.SearchPage), SearchInput.Text);
                break;
            case "settings":
                // t30 挂载（ui3 的 Features/Settings/SettingsPage）2026-09-12。
                ContentFrame.Navigate(typeof(Features.Settings.SettingsPage));
                break;
            case "servers":
                ContentFrame.Navigate(typeof(Features.Servers.ServersPage));
                break;
            case "logs":
                ContentFrame.Navigate(typeof(Features.Logs.LogsPage));
                break;
            case "library":
                ContentFrame.Navigate(typeof(Features.Library.LibraryPage));
                break;
            case Features.Favorites.FavoritesPage.NavTag:
                // t31（U-E）：侧栏第二项「收藏」
                ContentFrame.Navigate(typeof(Features.Favorites.FavoritesPage));
                break;
            case Features.Aggregate.AggregatePage.NavTag:
                // t31（U-E）：侧栏第四项「聚合视界」（三过滤器 + 按服务器分组 + 进度条；t304 起为第四项，
                // 第三项让位给「媒体库」——用户报障第⑤条要求 rail = 首页/收藏/媒体库/聚合视界）
                ContentFrame.Navigate(typeof(Features.Aggregate.AggregatePage));
                break;
            case Features.Person.PersonPage.NavTag:
                // t58：某位演职人员的作品列表（屏自己读 SHELL_PERSON_ID，不经导航参数）
                ContentFrame.Navigate(typeof(Features.Person.PersonPage));
                break;
            case Features.Collection.CollectionPage.NavTag:
                // t58：一个合集（BoxSet）里的条目（屏自己读 SHELL_COLLECTION_ID）
                ContentFrame.Navigate(typeof(Features.Collection.CollectionPage));
                break;
            default:
                // U-A 只落全局框架：其余屏由 ui2/ui3 与后续卡补齐 ⇒ 占位页带 tag，便于取证
                ContentFrame.Navigate(typeof(PlaceholderPage), tag);
                break;
        }

        SyncRailSelection(tag);
        UpdateBackButton();
        UpdateStatusBar();
    }

    /// <summary>
    /// tag → 页面类型。**唯一映射点**：`NavigateTo` 的分支与"重复导航"判据共用一份，
    /// 避免"两处各写一份映射"这种迟早会不一致的形态。
    /// </summary>
    private static Type PageTypeFor(string tag) => tag switch
    {
        "search" => typeof(Features.Search.SearchPage),
        "settings" => typeof(Features.Settings.SettingsPage),
        "servers" => typeof(Features.Servers.ServersPage),
        "logs" => typeof(Features.Logs.LogsPage),
        "library" => typeof(Features.Library.LibraryPage),
        "home" => typeof(Features.Home.HomePage),
        "detail" => typeof(Features.Detail.DetailPage),
        Features.Favorites.FavoritesPage.NavTag => typeof(Features.Favorites.FavoritesPage),
        Features.Aggregate.AggregatePage.NavTag => typeof(Features.Aggregate.AggregatePage),
        Features.Person.PersonPage.NavTag => typeof(Features.Person.PersonPage),
        Features.Collection.CollectionPage.NavTag => typeof(Features.Collection.CollectionPage),
        _ => typeof(PlaceholderPage),
    };

    /// <summary>
    /// rail 选中态与当前页**保持一致**：rail 项 ⇒ 选中它；非 rail 项（servers/settings/logs/search…）⇒ **清空选中**，
    /// 杜绝"停在设置页却显示首页被选中"的假选中态（用户 2026-09-12 报障：切换无反应）。
    /// </summary>
    private void SyncRailSelection(string tag)
    {
        _navSyncing = true;
        try
        {
            ListBoxItem match = null;
            foreach (var obj in RailNavList.Items)
            {
                if (obj is ListBoxItem it && it.Tag is string t && string.Equals(t, tag, StringComparison.Ordinal))
                {
                    match = it;
                    break;
                }
            }

            if (match is null)
            {
                if (RailNavList.SelectedItem is not null) { RailNavList.SelectedItem = null; }
            }
            else if (!ReferenceEquals(RailNavList.SelectedItem, match))
            {
                RailNavList.SelectedItem = match;
            }

            Program.Log("NAV-RAIL-SYNC tag=" + tag + " selected=" + (match is null ? "<none>" : tag));
        }
        finally
        {
            _navSyncing = false;
        }
    }

    /// <summary>返回按钮的可用性 = Frame 的真实回退栈（旧实现恒可点 ⇒ 观感是"能点但没反应"）。</summary>
    private void UpdateBackButton()
    {
        RailBackButton.IsEnabled = ContentFrame.CanGoBack;
    }

    private void UpdateStatusBar()
    {
        var state = ShellState.Current;
        RouteText.Text = "当前页：" + state.CurrentTag;
        ServersText.Text = state.ServerSummary;
        LogsText.Text = state.LogSummary;
    }

    /// <summary>
    /// 侧栏服务器行（§3）。名称颜色由**真实探测结果**驱动。
    ///
    /// **t183：状态三态化（`bool` → `bool?`）** —— 判据 = `_probeOk`：`null` 未探测 / `true` 在线 / `false` 离线。
    /// 为什么必须三态（修的是 ui3 转来的缺陷）：`ProbeAllAsync` 是 fire-and-forget，窗口起来后有一段时间
    /// **一行都还没探测过**；旧字段 `bool _ok` 默认 `false`，而 `false` 被三处消费点读成「离线」
    /// ⇒ 未探测期就画成离线红，与同一时刻 `_detail = "未探测"` 的文案**互相矛盾**。
    /// 三态落地口径：未探测 = **不表态**（图标空 + 描边灰 + 名灰 + tip 明说"尚未返回"），
    /// 探测完成后绿/红两向与之前逐字一致（只加一支，不改既有两态）。
    /// </summary>
    public sealed class ServerRow : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>三态判据：`null`=未探测（默认，窗口刚起来 `ProbeAllAsync` 还没返回该行）/ `true`=在线 / `false`=离线。</summary>
        private bool? _probeOk;
        private string _detail = "未探测";

        public ServerRow(ServerConfig server, bool canWrite)
        {
            Server = server;
            CanWrite = canWrite;
            AgeText = FormatAge(server);
        }

        /// <summary>
        /// 该行当前是否**可写**（自己添加的 / 已接管的）。取 `ServerConfigStore.CanWriteServer` —— 与
        /// 右键菜单是否给出可写项用的是**同一个判据**，这样手柄外观、tooltip 与菜单永远不可能互相矛盾。
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>仅用于取证日志与旧判据对照；语义 = `!CanWrite`。</summary>
        public bool IsReadOnly => !CanWrite;

        /// <summary>手柄期望像素色（供取证日志与像素自检对照，取值同 Theme/Tokens.xaml）。</summary>
        public string HandleColorHex => CanWrite ? "#9E9E9E" : "#6A6A6A";

        public Brush HandleBrush => (Brush)Application.Current.Resources[CanWrite ? "TextSecondaryBrush" : "TextDisabledBrush"];

        private static ImageSource _iconCacheOk;
        private static ImageSource _iconCacheFail;

        /// <summary>
        /// 服务器图标（**用户 2026-09-12 裁定**：显示 Emby 图标，不要纯色大方块）。
        /// 来源 = 原版资产 `data/flutter_assets/assets/icons/emby.png`（512×423）**降采样 64×64** ⇒
        /// `shell/App/Assets/Icons/emby.png`（2,094 B / sha12 `3B716E0B523B`）。
        /// 用绝对路径加载是因为本工程**非打包**（`WindowsPackageType=None`）⇒ 不依赖 `ms-appx:///` 的包根解析；
        /// 加载失败返回 `null`（XAML 侧只剩状态描边），**不抛**（一个图标不该让侧栏起不来）。
        ///
        /// **2026-09-12 第二次裁定（本方法）**：图标**本体**也要分状态 —— 用户原话「我希望绿色 emby 图标也是红色的」。
        /// 离线/探测失败态改用 `Assets/Icons/emby-offline.png`（红色菱形 Emby，2,012 B / sha12 `0FDDB4448041`）。
        /// 判据取 `_probeOk` —— 与同文件的 `StatusBrush`/`NameBrush` 两个 getter **同一个字段、同一时刻**，
        /// 故图标不可能与名称颜色/状态描边互相矛盾（三处同源，不是三套判据）。
        ///
        /// **t183：第三态（未探测）不借任何一个图标** —— 绿/红两版本身就是**状态断言**，`null` 时返回 `null`
        /// 让 `Image` 空白，配合中性灰描边 ⇒ 未探测期"什么都没说"，不会冒充离线（旧行为见下方 [!] 的更正）。
        ///
        /// 两态**各自缓存**：只留一份缓存会让先到的那态永久占位（探测返回后另一态拿到的还是旧图）。
        /// [!] 形态禁令：**不得**改成 `{StaticResource ServerIconSource}` 或在构造里 `Resources[...]` 赋值 ——
        /// 该形态在场且 >=1 行被实体化时发生 0xC000027B，与图片好坏无关（探针 B 2x2 矩阵，见 PROBE_B_PROTOCOL.md）。
        /// [x] t183 更正：上一版此处写「未探测期间（`_ok` 默认 false）显示离线红图标，非新增闪变」——
        ///     那句话把缺陷说成了设计。**未探测不是离线**，实现在这条注释之下已改为"无图标 + 灰描边"。
        /// </summary>
        public ImageSource IconSource
        {
            get
            {
                if (_probeOk is null) { return null; }

                var ok = _probeOk.Value;
                var cached = ok ? _iconCacheOk : _iconCacheFail;
                if (cached is not null) { return cached; }

                try
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", ok ? "emby.png" : "emby-offline.png");
                    if (System.IO.File.Exists(path))
                    {
                        var image = new BitmapImage(new Uri(path));
                        if (ok) { _iconCacheOk = image; }
                        else { _iconCacheFail = image; }
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("RAIL-ICON-LOAD-FAIL " + ex.GetType().Name + ": " + ex.Message);
                }

                return ok ? _iconCacheOk : _iconCacheFail;
            }
        }

        /// <summary>
        /// 手柄说明。不可写时**必须点出可用的出路**（右键接管），否则就成了"能点却与说明相反"的假说明；
        /// 因此这里不出现"不会写回"这类绝对句 —— 接管之后它就成立了。
        /// </summary>
        public string HandleToolTip => CanWrite
            ? "拖动可调整顺序（写回 SortIndex 并落盘）"
            : "只读（来自原版 accounts.json）；右键可接管为可写，接管后即可排序";

        public ServerConfig Server { get; }

        public string Name => string.IsNullOrWhiteSpace(Server.Name) ? "(未命名)" : Server.Name;

        /// <summary>§3：真实值；**无数据显示占位，绝不显示「0 天」**。</summary>
        public string AgeText { get; }

        public Visibility AccentBarVisibility => _isSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 状态描边（t183 **三态**）：绿=`ServerOkBrush`（在线）/ 粉=`ServerFailBrush`（离线）/
        /// **灰=`TextDisabledBrush`（未探测）**。中性一支刻意复用**既有令牌**（本卡 inScope 只有本文件 ⇒
        /// 不动 `Theme/Tokens.xaml`，不新增画刷键）。
        /// </summary>
        public Brush StatusBrush => (Brush)Application.Current.Resources[
            _probeOk is null ? "TextDisabledBrush" : (_probeOk.Value ? "ServerOkBrush" : "ServerFailBrush")];

        /// <summary>
        /// 名称色（t183 **三态**）：白=`TextPrimaryBrush`（在线）/ 粉=`ServerFailBrush`（离线）/
        /// **灰=`TextSecondaryBrush`（未探测）** —— 与"离线红"在同帧里必须**一眼可分**。
        /// </summary>
        public Brush NameBrush => (Brush)Application.Current.Resources[
            _probeOk is null ? "TextSecondaryBrush" : (_probeOk.Value ? "TextPrimaryBrush" : "ServerFailBrush")];

        /// <summary>三态标签（本类内部与取证日志用）：`unknown` / `ok` / `fail`。</summary>
        public string StateTag => _probeOk is null ? "unknown" : (_probeOk.Value ? "ok" : "fail");

        /// <summary>
        /// 行 tooltip（t183 三态各一句，**中性态必须明说"还没探测完"** —— 否则"灰图标 + 灰名称"会被读成
        /// 第三种状态，例如"已禁用"）。为什么在代码里给：本卡 inScope 只有本文件 ⇒ 不改 XAML 模板，
        /// 容器级 tooltip 由 <see cref="OnServerContainerChanging"/> 挂到 `ListViewItem` 上，
        /// 并用 `Source=row` 的绑定**跟随状态变化**（不留过期的"未探测"文案）。
        /// </summary>
        public string StateToolTip => _probeOk is null
            ? "未探测：连通探测尚未返回（启动后逐台探测，单台最长 8 秒）"
            : (_probeOk.Value ? "在线：" + _detail : "离线：" + _detail);

        public string Detail => _detail;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                Raise(nameof(AccentBarVisibility));
            }
        }

        public void SetStatus(bool ok, string detail)
        {
            SetStatusCore(ok, detail);
            RaiseStatusChanged();
        }

        /// <summary>
        /// t296：**工作线程安全**的状态写入 —— 只做"落字段 + **当场打审计行**"，不碰绑定通知。
        /// <para>为什么必须拆出来：并发探测的 <c>onResult</c> 在 runner 的工作线程上跑，而**"失败逐台可见"是本路径的硬要求**；
        /// 若把审计行也押在 `DispatcherQueue` 上，一旦入队被丢（t296 实测：16 台里有 1 台的首个 `TryEnqueue` 未落地 ⇒
        /// 少一行 `RAIL-ROW-STATE`），可见性就无声丢了。⇒ 审计行在工作线程**当场**打出（格式不变、逐台一行），
        /// 绑定通知再由调用方回 UI 线程（见 <see cref="RaiseStatusChanged"/>）。</para>
        /// </summary>
        public void SetStatusCore(bool ok, string detail)
        {
            _probeOk = ok;
            _detail = detail;

            // 取证锚（t183）：三态窗口只能从日志里量 —— 未探测期**没有**任何一行日志可读，
            // 所以"未探测持续了多久" = `RAIL state-initial unknown` 到本行首个 `state=` 之间的时长。
            Program.Log("RAIL-ROW-STATE name=" + Name + " state=" + StateTag + " detail=" + detail);
        }

        /// <summary>t296：绑定通知（**只能**在 UI 线程调用）。</summary>
        public void RaiseStatusChanged()
        {
            Raise(nameof(StatusBrush));
            Raise(nameof(NameBrush));
            Raise(nameof(IconSource));
            Raise(nameof(StateToolTip));
            Raise(nameof(Detail));
            Raise(nameof(StateTag));
        }

        /// <summary>
        /// 「N 天前看过」← 兼容读保留在 `Extra` 的 `cachedLastPlayedAt`（原版遗留值）。
        /// 🔴 accounts.json 只读、绝不回写 ⇒ 我们无权更新该值，只显示原版遗留值；无值 ⇒ 占位（不是「0 天」）。
        /// </summary>
        private static string FormatAge(ServerConfig server)
        {
            try
            {
                var raw = server.ExtraString(LastPlayedKey);
                if (string.IsNullOrWhiteSpace(raw) || !DateTimeOffset.TryParse(raw, out var when))
                {
                    return "— 无观看记录";
                }

                var days = (int)Math.Floor((DateTimeOffset.Now - when).TotalDays);
                return days <= 0 ? "今天看过" : days + " 天前看过";
            }
            catch (Exception ex) // 遗留时间值可能是任意脏字符串：解析/换算失败就退化成"无观看记录"，不让一行脏数据打断整列渲染
            {
                Program.Log("RAIL-AGE-FALLBACK raw=" + server.ExtraString(LastPlayedKey) + " -> " + ex.GetType().Name);
                return "— 无观看记录";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 滚轮的**唯一权威**（挂在根 `RootGrid` 上，且 `handledEventsToo: true`）。
    /// 为什么必须挂在根上（用户报障「在图标上/横排上滚轮上下滚不动」）：WinUI 的内层 `ScrollViewer` 处理完滚轮
    /// 会把它标成 `Handled = true`；横向列表为避免"滚轮被映射成横滚"已被设成
    /// `ScrollViewer.VerticalScrollMode="Enabled"` ⇒ 它**吃掉**了滚轮、自己却没有纵向可滚高度 ⇒ 页面一动不动，
    /// 而挂在 `ListView` / 页面上的 `PointerWheelChanged` 因为事件已被标记而**根本收不到**。
    /// 判据（可判、不猜）：从事件源往上找**最近的** `ScrollViewer`——
    ///   ① 它能沿该方向滚（`ScrollableHeight &gt; 0` 且未到尽头）⇒ 直接返回，交给它（避免双重滚动）；
    ///   ② 它不能（横排高度=内容高，或侧栏列表已滚到尽头）⇒ 把这次滚轮转给**当前页面**的 `ScrollViewer`。
    /// 有意副作用：侧栏列表滚到尽头后继续滚会带动主区页面（与常见阅读器一致）。
    /// </summary>
    private void OnAnyPointerWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(RootGrid).Properties.MouseWheelDelta;
        if (delta == 0) { return; }

        var inner = NearestScrollViewer(e.OriginalSource as DependencyObject);
        if (inner != null && CanScrollBy(inner, delta)) { return; }

        var page = FirstScrollViewerIn(ContentFrame);
        var target = (page != null && !ReferenceEquals(page, inner)) ? page : inner;
        if (target == null || !CanScrollBy(target, delta)) { return; }

        target.ChangeView(null, target.VerticalOffset - delta, null, disableAnimation: true);
        e.Handled = true;
    }

    /// <summary>该 `ScrollViewer` 能否沿本次滚轮方向继续滚（`MouseWheelDelta &gt; 0` = 向前 = 内容上移）。</summary>
    private static bool CanScrollBy(ScrollViewer viewer, int delta)
    {
        if (viewer.ScrollableHeight <= 0.5) { return false; }
        return delta < 0
            ? viewer.VerticalOffset < viewer.ScrollableHeight - 0.5
            : viewer.VerticalOffset > 0.5;
    }

    /// <summary>从事件源往**上**找最近的 `ScrollViewer`（事件源可能不是视觉元素 ⇒ 返回 null，由调用方兜底）。</summary>
    private static ScrollViewer NearestScrollViewer(DependencyObject start)
    {
        for (var node = start; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer viewer) { return viewer; }
        }

        return null;
    }

    /// <summary>当前页面里**最外层**的 `ScrollViewer`（深度优先先遇到的必是最外层：内层是它的后代）。</summary>
    private static ScrollViewer FirstScrollViewerIn(DependencyObject root)
    {
        if (root == null) { return null; }
        if (root is ScrollViewer viewer) { return viewer; }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FirstScrollViewerIn(VisualTreeHelper.GetChild(root, i));
            if (found != null) { return found; }
        }

        return null;
    }
}

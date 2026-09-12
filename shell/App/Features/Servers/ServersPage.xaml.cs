using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.SelfCheck;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Servers;

/// <summary>
/// 服务器管理页（t8 第 ① 项）：接 shell/Services。
///
/// 接线（签名取自 shell/Services/API_SURFACE.md，逐条核对）：
///   · AppDataDir.Instance → ServersFile / CredentialsFile / SettingsFile
///   · SecureKvStore.At(credentialsFile)  ← 凭据经 DPAPI 加密，**不落明文到 JsonStorage**
///   · ServerConfigStore.At(serversFile, credentials) → Load/Save/Add/Update/Remove/Servers
///   · ServerConfig.With(...) 做不可变式更新；ServerKind / ServerKindExtensions.DisplayName
///   · ServiceSelfCheck.RunAsync(onLog) 作「测试连接/自检」入口
///
/// 凭据纪律（§4 第 15 条 + 本工程硬要求）：密码/Token 一律经 SecureKvStore；
/// **不把明文写进 JsonStorage**，日志里也不打印凭据。
///
/// ⚠️ **验证用钩子（默认关闭；`WORKSPACE` 事实 40 / 58）**
///   · 环境变量 `SHELL_SELFTEST_SERVERS`：**不设 ⇒ 钩子完全不出现**（产品行为零变化）；
///     `=1` ⇒ 对**首台已启用服务器**跑一次连接测试（0 台时记「跳过」）；
///     `=<绝对 URL>` ⇒ 就测这个 URL（**临时构造配置、不落盘、不进 store**，反控制靠这条，不必污染用户数据）。
///   · 结果与异常原文**只写日志**（经 `Program.Log` → `shell-startup.log`）；
///     **不加按钮、不改默认路径、不改那 6 个按钮的现有行为**。
/// </summary>
public sealed partial class ServersPage : Page
{
    private ServerConfigStore _store;
    private int _menuSelfTestTicks;

    /// <summary>验证用钩子的环境变量名（默认关闭；语义见类注释）。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_SERVERS";

    public ServersPage()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            Reload();
            QueueMenuSelfTestIfRequested();
            await RunSelfTestHookIfRequestedAsync();
        };
    }

    /// <summary>
    /// t64：行**最右列**的状态读数（把 `ServersPage.xaml:41` 注释里"预留"的那一列填成真实值）。
    /// 数据源 = 当刻已有的 `ServerConfig.Enabled` ⇒ 文案 `启用` / `停用`（用户词，不是 `True/False`、不是字段名）。
    /// [!] 走 code-behind `Loaded` 赋值**而不是** `{Binding …, Converter={StaticResource …}}`：
    /// `DataTemplate` 里的资源查找正是崩溃代际的形态（探针 B：该形态在场即崩 `0xC000027B`）⇒ 本批不引入该形态。
    /// 取不到值 ⇒ 写 `—`（**绝不留空**）。
    /// </summary>
    private void OnRowStatusLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock block) { return; }

        if (block.DataContext is AIPlayer.Shell.Services.Models.ServerConfig cfg)
        {
            block.Text = cfg.Enabled ? "启用" : "停用";
        }
        else
        {
            block.Text = "—";   // 取不到值也不留空（本批硬口径）
        }
    }

    /// <summary>
    /// 行头像：**与侧栏同一份资产**（一个图标一个来源）。
    /// ⚠️ 之所以**不在 `DataTemplate` 里写 `{StaticResource …}`**：模板实例化时若资源键未就绪，XAML 资源查找会抛
    /// **stowed 异常**（实测 `0xC000027B`：`ServersPage` 装载后进程即死 ⇒ "编译过 ≠ 能跑"）。
    /// 改为控件自身 `Loaded` 时赋值 ⇒ **解析期不查资源**；加载失败为 `null`（空白，**不退化成色块**）。
    /// </summary>
    private void OnRowIconLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image img && img.Source == null)
        {
            img.Source = LoadServerIcon();
        }
    }

    /// <summary>
    /// 管理页行头像的**唯一加载点**。加载方式与侧栏一致：`AppContext.BaseDirectory` 绝对路径（本工程 `WindowsPackageType=None`，不走 `ms-appx:///`）。
    /// **失败返回 null**（渲染为空白）——**不退化成品红色块**：那正是用户报的"大方块"，宁可空白。
    /// </summary>
    private static Microsoft.UI.Xaml.Media.ImageSource LoadServerIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "emby.png");
            if (!System.IO.File.Exists(path))
            {
                Program.Log("SERVERS icon-missing（资产未随输出复制）：" + path);
                return null;
            }
            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
        }
        catch (Exception ex)
        {
            Program.Log("SERVERS icon-load-fail " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 菜单取证钩子（`SHELL_SELFTEST_SERVERMENU=1`）：等一次布局后**编程式**弹出首行右键菜单，
    /// 供窗口级单抓弹窗（`shell/tools/popup-capture.ps1 -List` → `-Hwnd`）取图。
    /// **为什么必须等布局**：`ShowAt` 需要一个已完成测量、已挂进可视树的目标元素，否则弹窗没有落点。
    /// **为什么轮询而不是只等一次**：`LayoutUpdated` 可能在行容器实体化之前就触发（实测会拿到 null）。
    /// 本钩子不驱动鼠标输入、不抢前台、不写任何配置 —— 只额外打日志。
    /// </summary>
    private void QueueMenuSelfTestIfRequested()
    {
        var raw = Environment.GetEnvironmentVariable(ServerRowContextMenu.SelfTestEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;                     // 未设 ⇒ 产品路径，零输出零副作用
        }

        if (raw.Trim() != "1")
        {
            Program.Log($"MENU-SELFTEST SKIP 未知取值 \"{raw}\"（当刻只认 1 = 弹出首行菜单；按项直接执行尚未实现）");
            return;
        }

        // 🔴 可诊断性（`verifier` 要求）：**变量一到就落一行** —— 否则"变量没到 app"与"`LayoutUpdated` 从未触发"
        // 在日志里**分不清**（他两次运行只看到 `ServersPage loaded`，一条 `MENU-SELFTEST` 都没有 ⇒ 无法归因）。
        // ⚠️ 变量**未设时仍零输出** ⇒ "日志里没有 `ARMED` 行"就等价于"变量没到 process"，两种失败因此可区分。
        Program.Log($"MENU-SELFTEST ARMED env={ServerRowContextMenu.SelfTestEnvVar} rows={ServerList.Items.Count}");

        if (ServerList.Items.Count == 0)
        {
            Program.Log("MENU-SELFTEST SKIP 列表 0 台 ⇒ 无行可弹（先添加/导入一台服务器再抓）");
            return;
        }

        ServerList.LayoutUpdated += OnMenuSelfTestLayoutUpdated;
    }

    private void OnMenuSelfTestLayoutUpdated(object sender, object e)
    {
        _menuSelfTestTicks++;
        if (ServerList.ContainerFromIndex(0) is not FrameworkElement row
            || ServerList.Items[0] is not ServerConfig cfg)
        {
            if (_menuSelfTestTicks >= 60)
            {
                ServerList.LayoutUpdated -= OnMenuSelfTestLayoutUpdated;
                Program.Log($"MENU-SELFTEST FAIL 60 次布局后首行容器仍未实体化（rows={ServerList.Items.Count}）");
            }
            return;
        }

        ServerList.LayoutUpdated -= OnMenuSelfTestLayoutUpdated;

        var anchor = row.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
        Program.Log($"MENU-SELFTEST BEGIN rows={ServerList.Items.Count} id={cfg.Id}"
            + $" anchor={anchor.X:F0},{anchor.Y:F0} {row.ActualWidth:F0}x{row.ActualHeight:F0}");

        // 复用产品路径同一个入口（唯一行为源）：它内部 Build + ShowAt + 打 MENU-OPEN。
        ServerRowContextMenu.AttachTo(row, cfg, Reload);
        _ = ServerRowContextMenu.DescribeInventory(cfg);        // 该方法内部已落 MENU-INVENTORY 日志，返回值此处不需要
        Program.Log("MENU-SELFTEST TIP 弹窗是独立 HWND ⇒ popup-capture.ps1 -Pid <外壳 PID> -List 找到它再 -Hwnd 单抓；"
            + "本钩子**不会**关掉菜单（不抢前台），抓完由取证方关外壳进程");
    }

    private void SetButtons(bool hasSelection)
    {
        var selected = Selected;
        var readOnly = selected != null && _store != null && _store.IsReadOnlyServer(selected.Id);

        // 只读条目（来自原版 `accounts.json` 的兼容读）：**允许「测试连接」**（纯只读 HTTP，不写任何文件），
        // **禁用「启用/停用」「删除」** —— 我们对这类条目永不回写原文件（`Save()` 明确排除它们），
        // 放行只会让用户「看起来生效、重启还原」= 本项目最忌讳的**静默失败**。
        // 判据 = `ServerConfigStore.IsReadOnlyServer(id)`（`ServerConfigStore.cs:120`，
        // 语义 = `_fromOriginalAccounts.Contains(id)`），**不靠猜**（猜的判据会在服务层改内部表示后静默失效）。
        // captain 裁定(a) + 事实 176 一般规则（**凡置灰某入口，必须同时存在另一条使它能用的路径；否则置灰 = 功能缺失**）：
        // 本机 16 台导入服务器（readOnly）全都停不了，而「删除」有菜单接管路径 ⇒ 本开关**恒可点**，
        // 未接管时走与菜单三项**同一个**接管确认源（点「取消」⇒ 不触碰任何写 API）。
        ToggleButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection && !readOnly;
        TestButton.IsEnabled = hasSelection;
    }

    private ServerConfig Selected => ServerList.SelectedItem as ServerConfig;

    private void Reload()
    {
        try
        {
            var dataDir = AppDataDir.Instance;

            // 🔴 事实 70：必须用**环境解析出的实例**（`ServerConfigStore.Instance`），才能触发对原版
            //    `<Local>\AIPlayer\accounts.json` 的**只读兼容读**。
            //    反例（21:28 实测）：`ServerConfigStore.At(dataDir.ServersFile, credentials)` —— **显式路径**构造
            //    会被 `services` 的「候选范围按实例类型收紧」规则排除（只有 `CanAutoMigrate=true` 的环境实例才回落），
            //    ⇒ 只读到我们自己的 `servers.json`（21 B 空文件）⇒ 服务器页显示 **0 台**（期望 16）。
            _store = ServerConfigStore.Instance;
            _store.Load();

            var servers = _store.Servers?.ToList() ?? new List<ServerConfig>();
            ServerList.ItemsSource = servers;
            SetButtons(false);

            // §9.2：可见文案**不出现绝对路径**（任何位置都不许，ToolTip 也不行），也不写"原版兼容读 / 旧根合并"这类过程语；
            // 来源计数与文件路径已由下面那条日志留档（ServersPage loaded: … originalAccounts=… legacy=… file=…）⇒ 取证不受影响。
            SummaryText.Text = $"共 {servers.Count} 台（启用 {servers.Count(s => s.Enabled)}）";
            // 生产界面不留常驻调试/自检文案：`StatusText` 只承载**操作结果与失败**（成功后置空）。
            StatusText.Text = string.Empty;

            // 四态证据要求 ②：光看「共 16 台」无法区分「16 = 原版 16」还是「16 = 原版 12 + 旧根 4」，
            // 故把两个来源计数与只读条目数一并落日志（`OriginalAccountsLoaded`/`LegacyServersLoaded`
            // 来自 `ServerConfigStore.cs:114/117`；只读判据 `IsReadOnlyServer` 见 `:120`）。
            var readOnlyCount = servers.Count(s => _store.IsReadOnlyServer(s.Id));
            Program.Log($"ServersPage loaded: {servers.Count} servers; enabled={servers.Count(s => s.Enabled)}"
                + $"; originalAccounts={_store.OriginalAccountsLoaded}; legacy={_store.LegacyServersLoaded}"
                + $"; readOnly={readOnlyCount}; file={dataDir.ServersFile}");

            // 状态栏（ShellState）与本页读同一份配置，增删改后重算，避免两处数字漂移。
            ShellState.Current.Refresh();
        }
        catch (Exception ex)
        {
            SummaryText.Text = "加载失败";
            StatusText.Text = ex.GetType().Name + ": " + ex.Message;
            Program.Log("ServersPage FAILED " + ex);
        }
    }

    /// <summary>
    /// 第二个宿主（captain 裁决①）：**同一个** <see cref="ServerRowContextMenu"/> 也挂在本页列表行上。
    /// 项集与侧栏那处**逐项相同**（唯一数据源/唯一行为源，不许各写一份）。
    /// </summary>
    private void OnServerListContextRequested(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs args)
    {
        var cfg = RowServerOf(args.OriginalSource as DependencyObject);
        if (cfg == null)
        {
            Program.Log("SERVERS-CONTEXT row=null（没解析到服务器行）");
            return;
        }

        Program.Log("SERVERS-CONTEXT row=" + cfg.Name + " id=" + cfg.Id);
        ServerRowContextMenu.AttachTo(args.OriginalSource as FrameworkElement, cfg, Reload);
    }

    /// <summary>沿可视树回溯到 DataContext 为 <see cref="ServerConfig"/> 的元素（与 MainWindow 的 RowOf 同法）。</summary>
    private static ServerConfig RowServerOf(DependencyObject start)
    {
        var node = start;
        while (node != null)
        {
            if (node is FrameworkElement fe && fe.DataContext is ServerConfig cfg) { return cfg; }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)    {
        SetButtons(Selected != null);

        // 只读条目的可见说明（captain 裁决 ③：不要做得更"聪明"去偷偷"接管"它们 ——
        // 那会破坏事实 70 裁决 2「原版文件永不回写」）。
        var cfg = Selected;
        if (cfg != null && _store != null && _store.IsReadOnlyServer(cfg.Id))
        {
            StatusText.Text = "「" + cfg.Name + "」来自原版配置，只读；如需管理请重新添加。";
        }
    }

    private void OnReloadClick(object sender, RoutedEventArgs e) => Reload();

    /// <summary>
    /// 「添加」= 走 §8 的五字段对话框（t30 / U-D）。
    /// 旧实现（t8）是直接塞一台占位 Emby 服务器，既不校验也不认证 ⇒ 本轮换掉：
    /// 对话框内部「提交即认证」（Emby 系真换 token、能列库才算成功），失败不落盘。
    /// </summary>
    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var ok = await AddServerDialog.ShowAsync(XamlRoot);
            if (ok)
            {
                Reload();
                StatusText.Text = "已添加服务器（配置与凭据均已落盘）";
            }
            else
            {
                StatusText.Text = "已取消添加（未写入任何文件）";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "添加失败 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("ServersPage add-dialog FAILED " + ex);
        }
    }

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        var cfg = Selected;
        if (cfg == null) { return; }

        var readOnly = _store.IsReadOnlyServer(cfg.Id);
        try
        {
            // 与菜单三项**共用同一个**接管确认源（`EnsureAdoptedAsync`，唯一行为源）：
            // 未接管 ⇒ 显式接管确认；点「取消」⇒ 返回 false ⇒ 这里直接 return，**不触碰任何写 API**。
            if (!await ServerRowContextMenu.EnsureAdoptedAsync(ToggleButton, cfg))
            {
                StatusText.Text = "已取消：未接管，未做任何写入";
                Program.Log($"TOGGLE-ENABLE readOnly={readOnly} before={cfg.Enabled} confirmed=False after={cfg.Enabled} adopted=False");
                return;
            }

            var target = cfg.With(enabled: !cfg.Enabled);
            if (readOnly) { _store.AdoptAndUpdate(target); }   // 接管 + 翻转一步到位（accounts.json 不动）
            else { _store.Update(target); _store.Save(); }

            Reload();
            StatusText.Text = $"已{(cfg.Enabled ? "停用" : "启用")}：{cfg.Name}";

            var after = _store.ById(cfg.Id) ?? target;
            Program.Log($"TOGGLE-ENABLE readOnly={readOnly} before={cfg.Enabled} confirmed=True after={after.Enabled}"
                + $" adopted={ServerConfigStore.IsAdoptedEntry(after)}");
        }
        catch (InvalidOperationException ex)
        {
            // 服务层兜底拒绝（未接管的写操作）⇒ **可见提示，不静默**；也证明"绕过确认写不进去"。
            StatusText.Text = "未接管，操作被拒绝：" + ex.Message;
            Program.Log($"TOGGLE-ENABLE readOnly={readOnly} confirmed=False rejected={ex.GetType().Name}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "切换失败 " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var cfg = Selected;
        if (cfg == null) { return; }
        try
        {
            _store.Remove(cfg.Id);
            _store.Save();
            Reload();
            StatusText.Text = $"已删除：{cfg.Name}";
            Program.Log($"ServersPage removed {cfg.Id}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "删除失败 " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>「测试连接」按钮：真实网络往返；核心逻辑与验证钩子**共用** <see cref="ProbeConnectAsync"/>（保证"验的是同一条路"）。</summary>
    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        var cfg = Selected;
        if (cfg == null) { return; }

        TestButton.IsEnabled = false;
        StatusText.Text = "正在连接 " + cfg.BaseUrl + " …";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var detail = await ProbeConnectAsync(cfg, cts.Token);
            StatusText.Text = $"连接成功：{cfg.Name}（{ServerKindExtensions.DisplayName(cfg.Kind)} @{cfg.BaseUrl}）" + detail;
            Program.Log($"ServersPage test OK {cfg.Id} @{cfg.BaseUrl}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "连接超时（8 s 内无响应）：" + cfg.BaseUrl;
            Program.Log($"ServersPage test TIMEOUT {cfg.Id}");
        }
        catch (Exception ex)
        {
            // 原样带出异常类型与消息：401/403 表示「可达但认证失败」，与「连不上」是两回事。
            StatusText.Text = $"连接失败 {ex.GetType().Name}: {ex.Message}";
            Program.Log($"ServersPage test FAILED {cfg.Id} {ex.GetType().Name}");
        }
        finally
        {
            TestButton.IsEnabled = Selected != null;
        }
    }

    /// <summary>
    /// 真实连接探测（按钮与验证钩子共用）。逐类型选一个**必然发请求**的实例方法
    /// （签名取自 `shell/Services/API_SURFACE.md`）：
    ///   Emby/Jellyfin → `GetViewsAsync`   Navidrome → `PingAsync`
    ///   Audiobookshelf → `GetLibrariesAsync`   WebDAV → `PingAsync`
    /// 成功返回附加说明串；失败**向上抛**（由调用方决定怎么呈现/记录）。
    /// 凭据纪律：本方法**不读也不打印**密码/Token（由服务层内部经 SecureKvStore 取用）。
    /// </summary>
    internal static Task<string> ProbeConnectAsync(ServerConfig cfg, CancellationToken ct)
        => ServerProbe.ProbeAsync(cfg, ct);   // 唯一实现见 Features/Servers/ServerProbe.cs（captain 裁决③：不许第二条探测路径）

    /// <summary>
    /// 写日志前的凭据打码（`WORKSPACE §4` 第 15 条）：URL 里的 `api_key=<值>` 一律替换为 `***`。
    /// ⚠️ 钩子允许传入任意绝对 URL ⇒ **入参本身可能带 token**，不打码就会把凭据写进 `shell-startup.log`。
    /// 与 `shell/Spike/Probe.cs` 的 `MaskSecrets` 同一条正则（不改动那边的实现）。
    /// </summary>
    internal static string MaskSecrets(string s) => Program.MaskSecrets(s);

    /// <summary>
    /// 验证用钩子（**默认关闭**；`WORKSPACE` 事实 40 / 58）。三态：
    /// 不设 ⇒ 直接返回（产品路径零输出）；`=1` ⇒ 首台已启用服务器；`=&lt;绝对 URL&gt;` ⇒ 临时构造、不落盘。
    /// **只写日志**，异常一律吞进日志，绝不影响页面加载。所有 URL 先经 <see cref="MaskSecrets"/> 再落盘。
    /// </summary>
    private async Task RunSelfTestHookIfRequestedAsync()
    {
        var raw = Environment.GetEnvironmentVariable(SelfTestEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        raw = raw.Trim();
        try
        {
            ServerConfig target;
            if (raw == "1")
            {
                var first = _store?.Servers?.FirstOrDefault(s => s.Enabled);
                if (first == null)
                {
                    Program.Log("SELFTEST-SERVERS SKIP 没有已启用的服务器（0 台）");
                    return;
                }
                target = first;
            }
            else
            {
                // 临时构造、**不落盘、不进 store**：反控制（坏 URL）靠这条，不必污染用户数据。
                target = new ServerConfig
                {
                    Id = "selftest",
                    Name = "(selftest)",
                    Kind = ServerKind.Emby,
                    BaseUrl = raw,
                    Enabled = true,
                };
            }

            Program.Log($"SELFTEST-SERVERS BEGIN value={MaskSecrets(raw)} url={MaskSecrets(target.BaseUrl)}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var detail = await ProbeConnectAsync(target, cts.Token);
            Program.Log($"SELFTEST-SERVERS OK {MaskSecrets(target.BaseUrl)}" + MaskSecrets(detail));

            // 只读语义的运行时断言（captain 裁决 ③）。**为什么必须选中一条再读**：
            // 「无选择时三条按钮本就都禁用」，那不能证明只读分支生效 —— 必须选中一条**只读**条目，
            // 再直接读三个按钮的 IsEnabled（而不是断言"我写了 if"）。
            if (ServerList.Items.Count > 0)
            {
                ServerList.SelectedIndex = 0;
                var sel = Selected;
                var readOnly = sel != null && _store != null && _store.IsReadOnlyServer(sel.Id);
                Program.Log($"SELFTEST-SERVERS readonly-assert id={sel?.Id} IsReadOnlyServer={readOnly}"
                    + $" toggle={ToggleButton.IsEnabled} delete={DeleteButton.IsEnabled} test={TestButton.IsEnabled}"
                    + $" note={StatusText.Text}");
            }
        }
        catch (OperationCanceledException)
        {
            Program.Log($"SELFTEST-SERVERS FAIL OperationCanceledException: 8 s 内无响应（{MaskSecrets(raw)}）");
        }
        catch (Exception ex)
        {
            // 反控制判据：坏 URL 必须落到这里，且带**可区分**的异常类型与原文。
            // ⚠️ 异常原文里可能回显**完整 URL**（服务层把请求 URL 写进了消息）⇒ 同样必须打码。
            Program.Log($"SELFTEST-SERVERS FAIL {ex.GetType().Name}: {MaskSecrets(ex.Message)}");
        }
    }

    private async void OnSelfCheckClick(object sender, RoutedEventArgs e)
    {
        SelfCheckButton.IsEnabled = false;
        StatusText.Text = "自检中…";
        try
        {
            var lines = new List<string>();
            await Task.Run(async () => { await ServiceSelfCheck.RunAsync(line => lines.Add(line)); });
            StatusText.Text = $"自检完成（{lines.Count} 步）：" + string.Join(" / ", lines.Take(2));
            Program.Log("ServiceSelfCheck done: " + lines.Count + " steps");
        }
        catch (Exception ex)
        {
            StatusText.Text = "自检异常 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("ServiceSelfCheck FAILED " + ex);
        }
        finally
        {
            SelfCheckButton.IsEnabled = true;
        }
    }
}

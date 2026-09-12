using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;
using AIPlayer.Shell.Services.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AIPlayer.Shell.Features.Settings;

/// <summary>
/// 屏 C：设置页（t30 / U-D），规格 = `UI_SPEC_SHELL.md` §9（只做非账号项）。
///
/// **逐行的绑定真实面**（纪律：能点的必须有真绑定；绑不了的一律置灰 + 写明原因）
/// <list type="bullet">
/// <item>语言 —— 🔴 全服务层**没有 i18n 服务**（检索 0 命中）⇒ 置灰标「待定」，不装样子。</item>
/// <item>主题 —— `AppSettings.ThemeMode` **真写盘**；当刻只有一套暗色令牌 ⇒ 副说明如实写明「全窗口生效待 t34」。</item>
/// <item>媒体库 —— `AppSettings.LibraryPageSize` / `MergeServerLibraries` 真读写。</item>
/// <item>备份与还原 —— 真导出/真还原（落 `%LOCALAPPDATA%\AIPlayer\backups\`，**不弹选择器** ⇒ 可被自检复现）。</item>
/// <item>同步 —— Trakt 账号绑定属被排除范围（§6.1「不做 Trakt 账号绑定」）⇒ 置灰标「待定」。</item>
/// <item>网络 —— `ProxyEnabled/ProxyUrl`（+ t33 的 `WindowsProxy`）与 `ConfigPortService` 固定端口，真读写。</item>
/// <item>关闭时最小化到托盘 —— `AppSettings.MinimizeToTrayOnClose` 真读写（托盘图标本体归 t32）。</item>
/// </list>
/// **不做的行**：Hills Lite Pro、账号（用户硬约束 1）。
///
/// 落盘一律走 <see cref="SettingsService.Patch"/>（行级更新，避免整对象覆盖丢字段）。
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>取证钩子（默认关闭）：设了才跑脚本化断言并把读数写进 `shell-startup.log`。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_SETTINGS";

    /// <summary>
    /// t75 可失败反控（默认关闭）：`SHELL_SELFTEST_SETTINGS_NEG=1` ⇒ 自检**故意不执行导入**，
    /// 于是"临时根读回 == 哨兵值"这条断言**必须报 `match=False`**（证明它不是恒真）。
    /// 生产路径零影响；反控跑同样不写真实数据根（隔离在两种跑法下都成立）。
    /// </summary>
    public const string SelfTestNegEnvVar = "SHELL_SELFTEST_SETTINGS_NEG";

    /// <summary>导航 tag（ui 的 `MainWindow` 用 `NavigateTo` 的 tag 名）。</summary>
    public const string NavTag = "settings";

    private readonly SettingsService _settings = SettingsService.Instance;
    private bool _loading;

    /// <summary>提示行的"正常"前景色（首帧从 XAML 取一次）——错误态与它互相替换，行高不变。</summary>
    private Brush _uaHintBrush;

    /// <summary>错误前景色 = 原版"登录失败"用的粉红 `#FFB6AD`（项目既有实测色值，见 HILLSLITE 分析）。</summary>
    private static readonly SolidColorBrush UaErrorBrush = new(Windows.UI.Color.FromArgb(255, 0xFF, 0xB6, 0xAD));

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Load();
            _ = RunSelfCheckHookAsync();
        };
    }

    // ===================== 加载与呈现 =====================

    private void Load()
    {
        _loading = true;
        try
        {
            _settings.Load();
            var s = _settings.Settings;
            var data = AppDataDir.Instance;

            // 语言（§9.2：可见面只写用户能懂的一句话；为什么放 ToolTip）
            LanguageBox.ItemsSource = new List<string> { "Auto（跟随系统）" };
            LanguageBox.SelectedIndex = 0;
            LanguageDesc.Text = "暂不支持切换语言";

            // 主题：**置灰标待定**（captain 裁决 E2：参照物只测到暗色，凭空造一套浅色 = 编数据）。
            // 值仍**原样显示**（不改写），并挂 tooltip —— 用户看得到当刻是什么，也知道为什么点不动。
            ThemeBox.ItemsSource = new List<string> { "仅暗色" };
            ThemeBox.SelectedIndex = 0;
            ThemeDesc.Text = "暂不支持切换主题";
            ToolTipService.SetToolTip(ThemeRow, "待定：仅暗色主题（参照物只有暗色实测值，浅色未设计）");

            // 媒体库
            PageSizeBox.Value = s.LibraryPageSize;
            MergeLibsSwitch.IsOn = s.MergeServerLibraries;

            // 备份与还原 = 配置包（captain 裁决 E5）
            BackupDesc.Text = "导出或导入配置备份";

            // 同步 = 跨服同步（captain 裁决 E5-同步）；Trakt 仍属被排除的账号面（不做）
            SyncDesc.Text = "合并多台服务器的同一部影片";

            // 网络
            ProxySwitch.IsOn = s.ProxyEnabled;
            ProxyBox.Text = s.ProxyUrl ?? string.Empty;
            var port = SafeCallbackPort(out var portNote);
            NetworkDesc.Text = "代理与回调端口";
            ToolTipService.SetToolTip(ProxyBox,
                "固定回调端口：" + (port > 0 ? port + "（" + portNote + "）" : "未分配（" + portNote + "）"));

            // 浏览器标识（User-Agent）：输入框显示**落盘形态**（留空 = 空串），提示行显示**当刻生效值**
            UaBox.Text = s.UserAgent ?? string.Empty;
            UaHint.Text = "生效：" + _settings.EffectiveUserAgent;
            ToolTipService.SetToolTip(ExportButton,
                "配置包含服务器/设置/图标/跳过缓存四段，凭据不打包（口令留在 DPAPI 凭据库）；"
                + "落点与最近一次结果见导出后的结果提示。");

            // 托盘
            TraySwitch.IsOn = s.MinimizeToTrayOnClose;

            // §9.2⑥：页头不显示配置文件路径
            SummaryText.Text = File.Exists(data.SettingsFile) ? "配置已加载" : "配置尚未创建";

            // §9.2③：实现理由挪进 ToolTip（悬停可查），不占版面
            ToolTipService.SetToolTip(LanguageRow, "服务层当刻没有任何语言资源（全仓检索 0 命中）⇒ 只展示、不改写。");
            ToolTipService.SetToolTip(ThemeRow, "参照物只有暗色实测值，浅色未设计；当刻固定暗色，不改写 themeMode。");
            ToolTipService.SetToolTip(LibraryRow, "对应 LibraryPageSize（每页条数）与 MergeServerLibraries（多服合并）。");
            ToolTipService.SetToolTip(BackupRow, "备份包含服务器、设置、图标、跳过标记；凭据不入包（留在本机凭据库）。");
            ToolTipService.SetToolTip(SyncRow, "对全部已启用服务器抓「继续观看」，按去重键合并（CrossServerSyncService）。");
            ToolTipService.SetToolTip(NetworkRow, "走系统代理时填代理地址；回调端口首次播放时自动分配并落盘。");
            ToolTipService.SetToolTip(TrayRow, "开启后关窗不退出进程（MinimizeToTrayOnClose）；托盘图标由补屏卡交付。");
            ToolTipService.SetToolTip(ResetRow, "只重置设置文件（ResetToDefaults），不影响服务器与凭据。");
            StatusText.Text = string.Empty;   // §9.3②：不留调试文案
        }
        catch (Exception ex)
        {
            StatusText.Text = "加载失败 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("SETTINGS load-fail " + ex);
        }
        finally
        {
            _loading = false;
        }
    }

    // ===================== 逐行事件 =====================
    // 主题行**没有事件**：captain 裁决 E2 = 置灰标待定（不做第二套令牌、不改写 themeMode）。

    private void OnPageSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) { return; }

        // 限制补全（用户报障「限制不全」）：键入可能绕过 Min/Max ⇒ 越界就**钳回合法值并回写控件**
        var size = (int)Math.Round(args.NewValue);
        var clamped = Math.Clamp(size, 10, 500);
        if (clamped != size)
        {
            _loading = true;
            sender.Value = clamped;
            _loading = false;
            StatusText.Text = "每页条数已限制在 10–500";
            Program.Log("SETTINGS libraryPageSize clamped from=" + size + " to=" + clamped);
            size = clamped;
        }

        _settings.Patch(s => s.With(libraryPageSize: size));
        StatusText.Text = "每页条数已保存：" + size;
        Program.Log("SETTINGS libraryPageSize=" + size);
    }

    private void OnMergeLibsToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) { return; }
        var on = MergeLibsSwitch.IsOn;
        _settings.Patch(s => s.With(mergeServerLibraries: on));
        StatusText.Text = "合并媒体库：" + (on ? "开" : "关");
        Program.Log("SETTINGS mergeServerLibraries=" + on);
    }

    private void OnProxyToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) { return; }
        var on = ProxySwitch.IsOn;
        _settings.Patch(s => s.With(proxyEnabled: on));
        StatusText.Text = "系统代理：" + (on ? "开" : "关");
        Program.Log("SETTINGS proxyEnabled=" + on);
    }

    private void OnProxyLostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) { return; }

        // 限制补全（用户报障「限制不全」）：非空必须是 http/https 绝对地址，否则**不写盘**并回退到已保存值。
        var url = (ProxyBox.Text ?? string.Empty).Trim();
        var valid = url.Length == 0
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
        if (!valid)
        {
            ProxyBox.Text = _settings.Settings.ProxyUrl ?? string.Empty;
            NetworkDesc.Text = "代理地址要形如 http://主机:端口";
            StatusText.Text = "代理地址未保存（格式不合法）";
            Program.Log("SETTINGS proxyUrl rejected (needs http/https absolute)");
            return;
        }

        _settings.Patch(s => s.With(proxyUrl: url));
        NetworkDesc.Text = "代理与回调端口";
        StatusText.Text = url.Length == 0 ? "代理地址已清空" : "代理地址已保存：" + url;
        Program.Log("SETTINGS proxyUrl=" + Program.MaskSecrets(url));
    }

    /// <summary>
    /// 浏览器标识（User-Agent）—— 用户点名「ua 支持自定义」。空 = 用默认（盘上落空串，输入框保存后仍为空）；
    /// 非法值（控制字符/超长）由服务层 `TrySetUserAgent` 拒绝 ⇒ **不写盘**、当场在固定高度的提示行回显原因。
    /// </summary>
    private void OnUaLostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) { return; }

        _uaHintBrush ??= UaHint.Foreground;
        var raw = UaBox.Text ?? string.Empty;
        if (!_settings.TrySetUserAgent(raw, out var error))
        {
            UaHint.Foreground = UaErrorBrush;
            UaHint.Text = "未保存：" + error;
            StatusText.Text = "浏览器标识未保存（" + error + "）";
            Program.Log("SETTINGS ua rejected reason=" + error);
            return;
        }

        UaBox.Text = _settings.Settings.UserAgent ?? string.Empty;   // 落盘形态（留空 = 空串）
        UaHint.Foreground = _uaHintBrush;
        UaHint.Text = "生效：" + _settings.EffectiveUserAgent;
        StatusText.Text = "浏览器标识已保存";
        Program.Log("SETTINGS ua saved persistedLen=" + (_settings.Settings.UserAgent?.Length ?? 0)
            + " effective=" + _settings.EffectiveUserAgent);
    }

    private void OnTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) { return; }
        var on = TraySwitch.IsOn;
        _settings.Patch(s => s.With(minimizeToTrayOnClose: on));
        StatusText.Text = "关闭时最小化到托盘：" + (on ? "开" : "关");
        Program.Log("SETTINGS minimizeToTrayOnClose=" + on);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _ = ResetAsync();
    }

    private async Task ResetAsync()
    {
        var ok = await ConfirmAsync("重置设置", "把设置文件恢复成默认值？服务器配置与凭据不受影响。", "重置");
        if (!ok) { return; }

        _settings.ResetToDefaults();
        Load();
        StatusText.Text = "设置已重置为默认值";
        Program.Log("SETTINGS reset done");
    }

    // ===================== 备份与还原（真文件操作） =====================

    /// <summary>配置包目录：`AppDataDir.BackupDir`（= `%LOCALAPPDATA%\AIPlayer\backup\`，服务层既有路径）。</summary>
    internal static string BackupDirPath()
    {
        try { return AppDataDir.Instance.BackupDir; }
        catch (Exception) { return Path.Combine(Path.GetDirectoryName(AppDataDir.Instance.SettingsFile) ?? AppContext.BaseDirectory, "backup"); }   // 备份目录属性取不到就退到配置目录旁，不让页面崩
    }

    /// <summary>最近一个配置包（`bundle-*.json`）。</summary>
    private static string LatestBackup()
    {
        try
        {
            var dir = BackupDirPath();
            if (!Directory.Exists(dir)) { return null; }
            return Directory.GetFiles(dir, "bundle-*.json").OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault();
        }
        catch (Exception) { return null; }   // 读备份目录失败 ⇒ 当作"没有可用配置包"，由调用方显示占位
    }

    private void OnExportClick(object sender, RoutedEventArgs e) => _ = ExportAsync();

    /// <summary>
    /// 导出**配置包**（captain 裁决 E5）：用既有 <see cref="BackupBundle"/> 模型打
    /// **Servers + Settings + Icons + SkipCache** 四段，落 <see cref="AppDataDir.BackupDir"/>。
    /// **凭据不进包**（密钥材料留在 DPAPI 凭据库；导入后需重新输入口令）。
    /// </summary>
    /// <param name="dataOverride">**隔离根**（自检/取证用；`null` ⇒ 产品路径用 `AppDataDir.Instance`）——产品行为零变化。</param>
    private async Task<string> ExportAsync(AppDataDir dataOverride = null)
    {
        try
        {
            var data = dataOverride ?? AppDataDir.Instance;
            var bundle = BuildBundle(data);
            var dir = dataOverride == null ? BackupDirPath() : data.BackupDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "bundle-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");

            var storage = new JsonStorage(path);
            storage.Write(bundle.ToJson());

            // §9.2⑤：结果提示里**不放绝对路径**（路径对用户无意义且属隐私面），段名也用用户词而非内部段名
            BackupDesc.Text = "已导出配置包：" + Path.GetFileName(path) + "（服务器 " + bundle.Servers.Count + " 条 / 设置 "
                + bundle.Settings.Count + " 项 / 图标 " + bundle.Icons.Count + " 项 / 跳过缓存 " + bundle.SkipCache.Count + " 项）";
            StatusText.Text = "配置包已导出：" + Path.GetFileName(path) + "（" + new FileInfo(path).Length + " B）";
            Program.Log($"SETTINGS bundle-export path={path} bytes={new FileInfo(path).Length}"
                + $" servers={bundle.Servers.Count} settings={bundle.Settings.Count} icons={bundle.Icons.Count} skip={bundle.SkipCache.Count}");
            await Task.CompletedTask;
            return path;
        }
        catch (Exception ex)
        {
            StatusText.Text = "导出失败 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("SETTINGS bundle-export-fail " + ex);
            return null;
        }
    }

    /// <summary>按 BackupBundle 的四段把当前磁盘状态收进一个包（缺文件 = 空段，不编数据）。</summary>
    private static BackupBundle BuildBundle() => BuildBundle(AppDataDir.Instance);

    /// <summary>
    /// 同上的**可注入数据根**版本：隔离取证（临时数据根）复用**同一条**构包路径，不开第二套实现。
    /// ⚠️ 四段都取自**磁盘文件**（不是内存 store）⇒ 调用方必须让 store 先落盘再调本方法。
    /// </summary>
    internal static BackupBundle BuildBundle(AppDataDir data)
    {
        var bundle = new BackupBundle { Schema = 1, CreatedAt = DateTime.Now };

        bundle.Servers = NodesOfArray(data.ServersFile);
        bundle.Settings = NodesOfObject(data.SettingsFile);
        bundle.Icons = NodesOfObject(data.IconsFile);
        bundle.SkipCache = NodesOfObject(data.SkipCacheFile);
        return bundle;
    }

    private static List<System.Text.Json.Nodes.JsonNode> NodesOfArray(string path)
    {
        var list = new List<System.Text.Json.Nodes.JsonNode>();
        try
        {
            if (!File.Exists(path)) { return list; }
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));

            // 两种形态都要吃：裸数组 `[...]`，或服务层写出来的对象 `{"version":1,"servers":[...]}`
            //（实测：servers.json 是后者 ⇒ 只认裸数组会得到 0 条，包就成空壳）。
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                foreach (var key in new[] { "servers", "Servers", "items", "Items" })
                {
                    if (obj.TryGetPropertyValue(key, out var inner) && inner is System.Text.Json.Nodes.JsonArray innerArray)
                    {
                        foreach (var item in innerArray) { list.Add(item?.DeepClone()); }
                        return list;
                    }
                }
                return list;
            }

            if (node is System.Text.Json.Nodes.JsonArray array)
            {
                foreach (var item in array) { list.Add(item?.DeepClone()); }
            }
        }
        catch (Exception ex) { Program.Log("SETTINGS bundle-read-array-fail " + Path.GetFileName(path) + " " + ex.GetType().Name); }
        return list;
    }

    private static Dictionary<string, System.Text.Json.Nodes.JsonNode> NodesOfObject(string path)
    {
        var map = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(path)) { return map; }
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                foreach (var kv in obj) { map[kv.Key] = kv.Value?.DeepClone(); }
            }
        }
        catch (Exception ex) { Program.Log("SETTINGS bundle-read-object-fail " + Path.GetFileName(path) + " " + ex.GetType().Name); }
        return map;
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e) => _ = RestoreAsync();

    /// <summary>导入最近配置包：把四段写回各自文件（服务器 = 数组、其余 = 对象），再 Reload 各 store。</summary>
    /// <param name="dataOverride">**隔离根**（自检用；`null` ⇒ 产品路径用 `AppDataDir.Instance`）——产品行为零变化。</param>
    /// <param name="bundlePathOverride">**指定包路径**（自检用；`null` ⇒ 产品路径取"最近一次配置包"）。</param>
    private async Task<bool> RestoreAsync(AppDataDir dataOverride = null, string bundlePathOverride = null)
    {
        var path = bundlePathOverride ?? LatestBackup();
        if (path == null)
        {
            StatusText.Text = "没有可导入的配置包（先点「导出配置包」）";
            return false;
        }

        try
        {
            var bundle = BackupBundle.FromJson(
                System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement);
            var data = dataOverride ?? AppDataDir.Instance;

            var written = new List<string>();
            if (bundle.Servers.Count > 0)
            {
                var array = new System.Text.Json.Nodes.JsonArray();
                foreach (var item in bundle.Servers) { array.Add(item?.DeepClone()); }
                new JsonStorage(data.ServersFile).Write(array);
                written.Add("servers=" + bundle.Servers.Count);
            }
            if (bundle.Settings.Count > 0)
            {
                new JsonStorage(data.SettingsFile).Write(ToObject(bundle.Settings));
                written.Add("settings=" + bundle.Settings.Count);
            }
            if (bundle.Icons.Count > 0)
            {
                new JsonStorage(data.IconsFile).Write(ToObject(bundle.Icons));
                written.Add("icons=" + bundle.Icons.Count);
            }
            if (bundle.SkipCache.Count > 0)
            {
                new JsonStorage(data.SkipCacheFile).Write(ToObject(bundle.SkipCache));
                written.Add("skip=" + bundle.SkipCache.Count);
            }

            ServerConfigStore.Instance.Reload();
            _settings.Reload();
            Load();
            StatusText.Text = "已从 " + Path.GetFileName(path) + " 导入：" + string.Join(" / ", written);
            Program.Log($"SETTINGS bundle-restore path={path} parts=[{string.Join(",", written)}]");
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "导入失败 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("SETTINGS bundle-restore-fail " + ex);
            return false;
        }
    }

    private static System.Text.Json.Nodes.JsonObject ToObject(Dictionary<string, System.Text.Json.Nodes.JsonNode> map)
    {
        var obj = new System.Text.Json.Nodes.JsonObject();
        foreach (var kv in map) { obj[kv.Key] = kv.Value?.DeepClone(); }
        return obj;
    }

    // ===================== 同步（跨服同步，captain 裁决 E5-同步） =====================

    private void OnSyncClick(object sender, RoutedEventArgs e) => _ = RunSyncAsync();

    /// <summary>
    /// 跨服同步：对**全部已启用服务器**抓「继续观看」，按去重键合并（`CrossServerSyncService`），
    /// 报「台数 / 合并后条目数 / 多源条目数」三个各自具名的数。**不写任何配置**（纯读 + 合并）。
    /// </summary>
    private async Task<string> RunSyncAsync()
    {
        SyncButton.IsEnabled = false;
        try
        {
            var result = await SyncCoreAsync();
            StatusText.Text = result;
            return result;
        }
        catch (Exception ex)
        {
            StatusText.Text = "跨服同步失败 " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("SETTINGS sync-fail " + ex);
            return null;
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    private async Task<string> SyncCoreAsync()
    {
        var servers = ServerConfigStore.Instance.Servers?.Where(x => x.Enabled && x.Kind.IsEmbyFamily()).ToList()
            ?? new List<Services.Models.ServerConfig>();

        if (servers.Count == 0)
        {
            Program.Log("SETTINGS-SYNC noservers");
            return "跨服同步：没有已启用的 Emby/Jellyfin 服务器";
        }

        using var registry = new Services.ServiceRegistry();
        registry.Initialize();

        var byServer = new List<KeyValuePair<Services.Models.ServerConfig, IEnumerable<Services.Models.EmbyItem>>>();
        var failed = new List<string>();

        foreach (var server in servers)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var items = await registry.CreateEmby(server).GetResumeAsync(limit: 50, cancellationToken: cts.Token);
                byServer.Add(new KeyValuePair<Services.Models.ServerConfig, IEnumerable<Services.Models.EmbyItem>>(server, items));
            }
            catch (Exception ex)
            {
                failed.Add(server.Name + "（" + Program.MaskSecrets(ex.Message) + "）");
                Program.Log($"SETTINGS-SYNC source-fail {server.Name} {ex.GetType().Name}: {Program.MaskSecrets(ex.Message)}");
            }
        }

        var merged = Services.Servers.CrossServerSyncService.Instance.MergeByServer(byServer);
        var multi = merged.Count(m => m.HasAlternatives);

        var line = $"跨服同步：{byServer.Count} 台成功 / {failed.Count} 台失败 · 合并后条目数 {merged.Count} · 多源条目数 {multi}";
        Program.Log($"SETTINGS-SYNC servers={byServer.Count} failed={failed.Count} merged={merged.Count} multiSource={multi}"
            + (failed.Count > 0 ? " failedList=[" + string.Join(",", failed) + "]" : string.Empty));
        if (byServer.Count == 0) { return line + "（全部源失败，未编造结果）"; }
        return line;
    }

    // ===================== 小工具 =====================

    private async Task<bool> ConfirmAsync(string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Dark,   // 弹窗自带根，不继承页面主题（实测过）
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            PrimaryButtonText = primaryText,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static int SafeCallbackPort(out string note)
    {
        try
        {
            var service = new Services.Infra.ConfigPortService(AppDataDir.Instance);
            var port = service.Port;
            if (port <= 0)
            {
                note = "尚未分配（首次播放时分配并落盘 callback-port.json）";
                return 0;
            }
            note = service.FilePath;
            return port;
        }
        catch (Exception ex)
        {
            note = "读取失败 " + ex.GetType().Name;
            return 0;
        }
    }

    // ===================== 取证钩子（默认关闭） =====================

    /// <summary>
    /// 脚本化自检：**不驱动用户输入**（§14），直接走"读 → 改 → 读回 → 还原"的闭环，并把每一步读数落日志。
    /// 反控制：全程对 `servers.json` 做哈希，要求**字节不变**（设置页不得碰服务器配置）。
    /// </summary>
    private async Task RunSelfCheckHookAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SelfTestEnvVar))) { return; }

        try
        {
            var data = AppDataDir.Instance;
            var serversHashBefore = HashOf(data.ServersFile);
            // 🔴 captain 要求：自检对设置的三次改动（① 哨兵写 / ③ 基线还原 / 包往返的 settings 段）**仍会写真实 settings.json**
            // ⇒ 本批不改它的写面，但**必须证明"内容是还原的"**（sha 相等；mtime 必然变，如实标注而不是假装"没被写"）。
            var settingsHashBefore = HashOf(data.SettingsFile);
            // 规范化内容哈希（忽略缩进/空格差异）：**"内容已还原"的证明读这个**，不读字节 sha。
            // 动机（实测）：settings.json 可能被外部工具（本机实测 Windows PowerShell 的 ConvertTo-Json）写过
            // ⇒ 我们的写回会按自己的序列化重排 ⇒ 字节 sha 必然不等，但那不是"内容变了"。
            var settingsCanonBefore = CanonicalHashOf(data.SettingsFile);

            Program.Log($"SETTINGS-SELFCHECK-BEGIN settingsFile={data.SettingsFile}"
                + $" exists={File.Exists(data.SettingsFile)} serversHash12={serversHashBefore}");

            // t75：真实四件套的**写前**戳（sha256_12 + mtime）—— 验收 1 要的就是"这四个文件全程不变"。
            string Stamp(string p) => File.Exists(p) ? HashOf(p) + "@" + File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm:ss.fff") : "<missing>";
            var realServersBefore = Stamp(data.ServersFile);
            var realSettingsBefore = Stamp(data.SettingsFile);
            var realIconsBefore = Stamp(data.IconsFile);
            var realSkipCacheBefore = Stamp(data.SkipCacheFile);
            Program.Log($"SETTINGS-SELFCHECK-REAL-BEFORE servers={realServersBefore} settings={realSettingsBefore}"
                + $" icons={realIconsBefore} skipCache={realSkipCacheBefore}");

            var s0 = _settings.Settings;
            Program.Log($"SETTINGS-SELFCHECK-BASELINE themeMode=\"{s0.ThemeMode}\" libraryPageSize={s0.LibraryPageSize}"
                + $" mergeServerLibraries={s0.MergeServerLibraries} proxyEnabled={s0.ProxyEnabled}"
                + $" proxyUrl=\"{Program.MaskSecrets(s0.ProxyUrl)}\" minimizeToTrayOnClose={s0.MinimizeToTrayOnClose}");

            // ① 行级写：**改到隔离根上**（见下面 ②-pre-2 的 `sc`）—— t75 前这里是写真实 `settings.json` 再还原，
            //    结果是"内容还原了但 mtime 变了"。现在真实文件只被读。
            var sentinelSize = s0.LibraryPageSize == 77 ? 78 : 77;

            // 隔离根（captain 裁定 (甲)）：自检的"导出 → 改动 → 导入"**全部**落在这个一次性根里
            // ⇒ 真实 servers.json / settings.json / icons / skipCache **只被读、不被写**（原缺陷：
            //   自检直接跑产品导入路径并把 Servers 段写回真实文件 ⇒ SETTINGS-SELFCHECK-SERVERS-UNTOUCHED identical=False）。
            var selfCheckRoot = new AIPlayer.Shell.Services.Infra.AppDataDir(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t30-setcheck-" + Guid.NewGuid().ToString("N").Substring(0, 8)),
                isPortable: true);
            // 存在性**三读**（captain 要求：建前 False / 用中 True / 删后 False）。
            // 另加一条"真的能用"的判据：光看"目录在"不等于"能写"（权限/路径长度都可能让它不可用）
            // ⇒ 建后写一个探针文件再删掉，把结果并进同一行读数。
            var tempRootExistsBefore = Directory.Exists(selfCheckRoot.Root);
            System.IO.Directory.CreateDirectory(selfCheckRoot.Root);
            var tempRootWritable = false;
            try
            {
                var probeFile = System.IO.Path.Combine(selfCheckRoot.Root, ".write-probe");
                System.IO.File.WriteAllText(probeFile, "ok");
                tempRootWritable = System.IO.File.Exists(probeFile);
                System.IO.File.Delete(probeFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 探针失败 = 这个临时根此刻不可写：如实记一行，下面那条 TEMPROOT 读数里 writable=False 会直接暴露
                Program.Log("SETTINGS-SELFCHECK-TEMPROOT-PROBE-FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
            Program.Log($"SETTINGS-SELFCHECK-TEMPROOT exists-before={tempRootExistsBefore}"
                + $" exists-during={Directory.Exists(selfCheckRoot.Root)} writable={tempRootWritable} path={selfCheckRoot.Root}");

            // ②-pre 把真实四段**复制**进隔离根（只读来源、只写副本）。
            // 为什么必须有这一步：隔离到位之后，"配置包四段"这条断言如果从**空的**临时根构包，就会得到
            // `parts=[Servers=0,Settings=0,Icons=0,SkipCache=0]` ⇒ 断言退化成恒真（实测踩过：第一次隔离后就是这组读数）。
            // 复制真实文件进临时根后，包里就是**真实内容**，而真实根全程只被读。
            var seeded = new List<string>();
            foreach (var (src, dst) in new[]
            {
                (src: data.ServersFile, dst: selfCheckRoot.ServersFile),
                (src: data.SettingsFile, dst: selfCheckRoot.SettingsFile),
                (src: data.IconsFile, dst: selfCheckRoot.IconsFile),
                (src: data.SkipCacheFile, dst: selfCheckRoot.SkipCacheFile),
            })
            {
                try
                {
                    if (File.Exists(src)) { File.Copy(src, dst, overwrite: true); seeded.Add(Path.GetFileName(src)); }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 单个文件复制失败：这一段在包内计数会是 0，读数的 seeded=[...] 里缺哪一个能直接看出来
                }
            }
            Program.Log($"SETTINGS-SELFCHECK-SEED seeded=[{string.Join(",", seeded)}] root={selfCheckRoot.Root}");

            // ②-pre-2（t75 结构性收口）：行级哨兵写改到**根作用域**的 `SettingsService` 上。
            // 为什么不是 `_settings`：`_settings` = `SettingsService.Instance`，绑的是**真实** settings.json
            //   ⇒ 哨兵"写后还原"必然改掉它的 mtime（旧读数：settings 内容还原了、mtime 变了）。
            // 用什么：`SettingsService.At(path)` —— **服务层既有 public API**（`Settings/SettingsService.cs:46`），
            //   本卡不碰 `shell/Services/**`，只是换一个实例来写 ⇒ 真实文件从"写后还原"变成"只被读"。
            // 顺序要求：必须在 SEED 之后（`sc` 要读的是**真实内容的副本**），且在构包之前（包要带哨兵值，
            //   下面 `restoredToBackupValue == sentinelSize` 才是有意义的判据）。
            var sc = SettingsService.At(selfCheckRoot.SettingsFile);
            sc.Load();
            var sc0 = sc.Settings;
            Program.Log($"SETTINGS-SELFCHECK-SCOPE root={selfCheckRoot.Root}"
                + $" settingsFile={Path.GetFileName(selfCheckRoot.SettingsFile)}"
                + $" baselinePageSize={sc0.LibraryPageSize} baselineTray={sc0.MinimizeToTrayOnClose}");

            sc.Patch(s => s.With(libraryPageSize: sentinelSize));
            sc.Reload();
            var afterSize = sc.Settings.LibraryPageSize;
            Program.Log($"SETTINGS-SELFCHECK-WRITE libraryPageSize {sc0.LibraryPageSize}->{sentinelSize}"
                + $" readBack={afterSize} match={afterSize == sentinelSize} scope=temp-root");

            sc.Patch(s => s.With(minimizeToTrayOnClose: !sc0.MinimizeToTrayOnClose));
            sc.Reload();
            var afterTray = sc.Settings.MinimizeToTrayOnClose;
            Program.Log($"SETTINGS-SELFCHECK-WRITE minimizeToTrayOnClose {sc0.MinimizeToTrayOnClose}->{!sc0.MinimizeToTrayOnClose}"
                + $" readBack={afterTray} match={afterTray == !sc0.MinimizeToTrayOnClose} scope=temp-root");

            var sentinelProxy = (sc0.ProxyUrl == "http://127.0.0.1:9/") ? "http://127.0.0.1:8/" : "http://127.0.0.1:9/";
            sc.Patch(s => s.With(proxyUrl: sentinelProxy, proxyEnabled: true));
            sc.Reload();
            var afterProxyUrl = sc.Settings.ProxyUrl;
            var afterProxyOn = sc.Settings.ProxyEnabled;
            Program.Log($"SETTINGS-SELFCHECK-WRITE proxyUrl->{sentinelProxy} readBack=\"{afterProxyUrl}\""
                + $" match={afterProxyUrl == sentinelProxy} proxyEnabled=true readBack={afterProxyOn} scope=temp-root");

            // ② 配置包：导出（BackupBundle 四段）→ 改动 → 导入 → 读数回到导出时点
            var bundlePath = await ExportAsync(selfCheckRoot);
            var bundleInfo = bundlePath != null && File.Exists(bundlePath)
                ? $"{Path.GetFileName(bundlePath)}={new FileInfo(bundlePath).Length}B"
                : "(none)";
            var bundleParts = "(none)";
            try
            {
                if (bundlePath != null && File.Exists(bundlePath))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(bundlePath)).RootElement;
                    var counts = new List<string>();
                    foreach (var name in new[] { "Servers", "Settings", "Icons", "SkipCache" })
                    {
                        // 键名大小写两种都试（服务层 ToJson 用的是 camelCase，实测踩过）
                        var alt = char.ToLowerInvariant(name[0]) + name.Substring(1);
                        var part = doc.TryGetProperty(name, out var p1) ? p1
                            : (doc.TryGetProperty(alt, out var p2) ? p2 : default);
                        var n = part.ValueKind == System.Text.Json.JsonValueKind.Array
                            ? part.GetArrayLength()
                            : (part.ValueKind == System.Text.Json.JsonValueKind.Object ? part.EnumerateObject().Count() : -1);
                        counts.Add(name + "(" + alt + ")=" + n);
                    }
                    bundleParts = string.Join(",", counts);
                }
            }
            catch (Exception ex) { bundleParts = "parse-fail:" + ex.GetType().Name; }
            Program.Log($"SETTINGS-SELFCHECK-BUNDLE file={bundleInfo} parts=[{bundleParts}]");

            sc.Patch(s => s.With(libraryPageSize: 123));
            sc.Reload();
            var dirty = sc.Settings.LibraryPageSize;
            // t75 可失败反控：`SHELL_SELFTEST_SETTINGS_NEG=1` ⇒ **故意不导入** ⇒ 下面"临时根读回 == 哨兵值"必须报 False。
            var negativeArm = string.Equals(Environment.GetEnvironmentVariable(SelfTestNegEnvVar), "1", StringComparison.Ordinal);
            var restored = negativeArm ? false : await RestoreAsync(selfCheckRoot, bundlePath);
            var afterRestore = sc.Settings.LibraryPageSize;
            Program.Log($"SETTINGS-SELFCHECK-RESTORE dirty={dirty} restored={restored} readBack={afterRestore}"
                + $" restoredToBackupValue={afterRestore == sentinelSize} negativeArm={negativeArm}");

            // ②-c 隔离根侧的判据：导入到底有没有把包里的值写回去 —— **读临时根的文件**（不读真实文件）。
            // 读真实根得到的是"内存单例 _settings 的值"（② 步刚把它改成 123），在隔离后**本来就该**与包值不等
            // ⇒ 只看 restoredToBackupValue 会把"隔离生效"误读成"导入没生效"。两条读数分开报。
            var tempReadBack = ReadLibraryPageSize(selfCheckRoot.SettingsFile);
            Program.Log($"SETTINGS-SELFCHECK-RESTORE-TEMP tempRootReadBack={tempReadBack} expected={sentinelSize}"
                + $" match={tempReadBack == sentinelSize} tempFile={Path.GetFileName(selfCheckRoot.SettingsFile)}");

            // ②b 跨服同步（captain 裁决 E5-同步）——**必须放在第 ③ 步还原基线之后**：
            //     否则会带着第 ① 步的哨兵代理（http://127.0.0.1:9/）去联网，16 台全被本机拒绝
            //     （实测踩过：失败原文里 "(由于目标计算机积极拒绝，无法连接。 (127.0.0.1:9))"）。

            // ③ 基线核对（t75 前这里 = "把 ① 的哨兵写回真实文件"，那一步本身就是一次真实写）。
            //    现在真实文件**从未被本自检写过** ⇒ 这里退化成一次**读**；判据反而更强：
            //    "内存值与基线相同" + 下面 ④-a 的"真实四件套 sha256 与 mtime 都不变"。
            _settings.Reload();
            var s2 = _settings.Settings;
            Program.Log($"SETTINGS-SELFCHECK-BASELINE-UNTOUCHED libraryPageSize={s2.LibraryPageSize}"
                + $" tray={s2.MinimizeToTrayOnClose} proxyEnabled={s2.ProxyEnabled} proxyUrl=\"{Program.MaskSecrets(s2.ProxyUrl)}\""
                + $" identical={s2.LibraryPageSize == s0.LibraryPageSize && s2.MinimizeToTrayOnClose == s0.MinimizeToTrayOnClose && s2.ProxyEnabled == s0.ProxyEnabled && s2.ProxyUrl == s0.ProxyUrl}");

            // ④ 反控制：设置页不得碰 servers.json
            var serversHashAfter = HashOf(data.ServersFile);
            Program.Log($"SETTINGS-SELFCHECK-SERVERS-UNTOUCHED before={serversHashBefore} after={serversHashAfter}"
                + $" identical={serversHashBefore == serversHashAfter}");

            // ④-a（t75 验收 1）：真实四件套**逐文件**给前后戳（sha256_12 + mtime）。
            //     判据从"内容还原（bytesEqual/contentEqual）"升格为"**sha256 与 mtime 都不变**"——
            //     因为哨兵写已搬到隔离根，真实文件全程只被读。
            var realServersAfter = Stamp(data.ServersFile);
            var realSettingsAfter = Stamp(data.SettingsFile);
            var realIconsAfter = Stamp(data.IconsFile);
            var realSkipCacheAfter = Stamp(data.SkipCacheFile);
            Program.Log($"SETTINGS-SELFCHECK-REAL-AFTER servers={realServersAfter} settings={realSettingsAfter}"
                + $" icons={realIconsAfter} skipCache={realSkipCacheAfter}");
            var settingsHashAfter = HashOf(data.SettingsFile);
            var settingsCanonAfter = CanonicalHashOf(data.SettingsFile);
            Program.Log($"SETTINGS-SELFCHECK-REAL-UNTOUCHED"
                + $" servers_same={realServersBefore == realServersAfter}"
                + $" settings_same={realSettingsBefore == realSettingsAfter}"
                + $" icons_same={realIconsBefore == realIconsAfter}"
                + $" skipCache_same={realSkipCacheBefore == realSkipCacheAfter}"
                + $" allSame={realServersBefore == realServersAfter && realSettingsBefore == realSettingsAfter && realIconsBefore == realIconsAfter && realSkipCacheBefore == realSkipCacheAfter}"
                + $" settingsBytesEqual={settingsHashBefore == settingsHashAfter}"
                + $" settingsContentEqual={settingsCanonBefore == settingsCanonAfter}"
                + " note=戳 = sha256_12@mtime（yyyy-MM-dd HH:mm:ss.fff）；mtime 不变是本卡的核心判据");

            // ④-a2 断言面：正控必须 True；`SHELL_SELFTEST_SETTINGS_NEG=1` 的负控（故意不导入）必须 False。
            var tempAssertOk = tempReadBack == sentinelSize;
            if (negativeArm)
            {
                Program.Log($"SETTINGS-SELFCHECK-ASSERT arm=negative expected=False actual={tempAssertOk}"
                    + $" tempReadBack={tempReadBack} expectedValue={sentinelSize} reason=skip-import"
                    + $" verdict={(tempAssertOk ? "FAIL(断言恒真！)" : "PASS(断言可失败)")}");
            }
            else
            {
                Program.Log($"SETTINGS-SELFCHECK-ASSERT arm=positive expected=True actual={tempAssertOk}"
                    + $" tempReadBack={tempReadBack} expectedValue={sentinelSize}"
                    + $" verdict={(tempAssertOk ? "PASS" : "FAIL")}");
            }

            // ④-b 临时根清理（captain 补要求 2）：给"删后是否还在"的存在性读数
            try
            {
                Directory.Delete(selfCheckRoot.Root, recursive: true);
            }
            catch (Exception ex)
            {
                // 有意忽略：清理失败只影响 %TEMP% 卫生，不影响上面两条断言；下面会把"删后是否还在"如实打出来
                Program.Log("SETTINGS-SELFCHECK-TEMPROOT-DELETE-FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
            Program.Log($"SETTINGS-SELFCHECK-TEMPROOT exists-after={Directory.Exists(selfCheckRoot.Root)} path={selfCheckRoot.Root}");

            // ⑤ 跨服同步（真打网络 + 真合并，三个数各自具名）——此刻代理已还原成基线
            var syncLine = await RunSyncAsync();
            Program.Log($"SETTINGS-SELFCHECK-SYNC line=\"{syncLine}\"");

            // ⑤ 行状态读数（哪几行真绑、哪几行待定）
            Program.Log($"SETTINGS-SELFCHECK-ROWS languageEnabled={LanguageBox.IsEnabled} themeEnabled={ThemeBox.IsEnabled}"
                + $" pageSizeEnabled={PageSizeBox.IsEnabled} mergeEnabled={MergeLibsSwitch.IsEnabled}"
                + $" exportEnabled={ExportButton.IsEnabled} restoreEnabled={RestoreButton.IsEnabled}"
                + $" syncEnabled={SyncButton.IsEnabled} proxyEnabled={ProxySwitch.IsEnabled}"
                + $" trayEnabled={TraySwitch.IsEnabled} resetEnabled={ResetButton.IsEnabled}"
                + $" themeValue=\"{_settings.Settings.ThemeMode}\"");

            Program.Log("SETTINGS-SELFCHECK-END");

            // 自检改过控件以外的值 ⇒ 收尾刷一次 UI，保证"截图里的数与文件里的数一致"
            //（否则截图上留着哨兵值 77 / 哨兵代理，看图的人会以为配置被改了）。
            Load();
        }
        catch (Exception ex)
        {
            Program.Log("SETTINGS-SELFCHECK-FAIL " + ex);
        }

        await Task.CompletedTask;
    }

    /// <summary>从一份 settings JSON 里读 <c>libraryPageSize</c>（读不到/不是整数 ⇒ -1）。
    /// 用途：隔离根的导入断言要读**临时根**的文件，不能读真实根（那条路径读的是内存单例的值）。</summary>
    private static int ReadLibraryPageSize(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return -1; }
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            return node?["libraryPageSize"]?.GetValue<int>() ?? -1;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException
                                      or FormatException or InvalidOperationException)
        {
            return -1;   // 解析/读取/取值失败：调用方按"读不到"处理（判据会显式报 match=False，不静默当成 0）
        }
    }

    private static string HashOf(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return "(missing)"; }
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).Substring(0, 12);
        }
        catch (Exception) { return "(error)"; }   // 序列化失败只影响自检读数可读性，不打断页面
    }

    /// <summary>
    /// **规范化内容哈希**（sha256_12 / 大写）：把 JSON 解析后按 `JsonNode.ToJsonString()` 重排再哈希
    /// ⇒ 缩进、空格、"键顺序"以外的格式差异不影响读数。
    /// 用途：`settings.json` 会被自检自己写，而**外部工具也可能写它**（本机实测 Windows PowerShell 的
    /// `ConvertTo-Json` 会写出另一种缩进）⇒ 字节 sha 不等可能只是"格式不是我写的"，不足以证明"内容变了"。
    /// </summary>
    private static string CanonicalHashOf(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return "(missing)"; }
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            var canonical = node?.ToJsonString() ?? "(null)";
            return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).Substring(0, 12);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            return "(bad:" + ex.GetType().Name + ")";   // 解析/读取失败只让读数退化为"不可比"，不影响自检其余部分
        }
    }
}

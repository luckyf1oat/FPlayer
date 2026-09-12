using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Servers;

/// <summary>
/// 屏 B：侧栏服务器行的**右键菜单**（t30 / U-D），裁定表 = `UI_SPEC_SHELL.md` §3.1
/// （外观实测值 = `HILLSLITE_UI_ANALYSIS.md` §4.5：底 `#1F1F1F`、悬停 `#2C2C2C`、无分隔线、11 项）。
///
/// **对外契约（ui 已在 `MainWindow.ServerList_ContextRequested` 留好 SEAM① 那一行）**
/// <code>ServerRowContextMenu.AttachTo(args.OriginalSource as FrameworkElement, row.Server, OnServersChanged);</code>
/// 菜单本体 + 11 个 handler 全在本类 ⇒ 外壳不再写第二套。
///
/// **逐项状态（用户硬约束：能点的必须有真实绑定；不能绑的一律置灰标「待定」，不静默省略）**
/// 做 7 项：修改图标 / 修改备注 / 媒体库 / 禁用预加载 / 修改密码 / 编辑 / 删除
/// 待定 4 项：服务器线路 / 设为私密 / 媒体库统计 / strm直链播放（**可见但置灰** + 理由 tooltip）
/// 只读条目（原版 `accounts.json` 兼容读）额外禁用 修改密码 / 编辑 / 删除 —— 我们对这类条目**永不回写**。
/// </summary>
public static class ServerRowContextMenu
{
    /// <summary>
    /// 菜单取证钩子的环境变量名（**默认关闭**，不设 ⇒ 产品路径零输出）。
    /// 当刻只实现 `=1`：起屏后**编程式弹出首行菜单**（不驱动鼠标、不抢前台），供窗口级单抓弹窗取图。
    /// 「`=&lt;菜单项&gt;` ⇒ 直接执行该项」是**预留语义、尚未实现** ⇒ 其它取值由 <c>ServersPage</c> 记一条 SKIP 日志（不静默吞掉）。
    /// </summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_SERVERMENU";

    private static readonly ServerConfigStore Store = ServerConfigStore.Instance;
    private static readonly PasswordStore Passwords = PasswordStore.Instance;

    // ==================================================================================
    // 入口
    // ==================================================================================

    /// <summary>给一行挂上右键菜单（幂等：重复调用只是重建菜单，不会叠加）。</summary>
    public static void AttachTo(FrameworkElement row, ServerConfig server, Action onChanged)
    {
        if (row == null) { return; }
        if (server == null)
        {
            Program.Log("MENU-ATTACH skipped: server=null");
            return;
        }

        var flyout = Build(row, server, onChanged);
        row.ContextFlyout = flyout;
        flyout.ShowAt(row);
        Program.Log($"MENU-OPEN id={server.Id} name=\"{server.Name}\" readOnly={IsReadOnly(server)}");
    }

    /// <summary>
    /// 只读判定：语义 = 服务层的 <c>IsReadOnlyServer</c>（不靠猜，猜的判据会静默失效）。
    /// ⚠️ captain 2026-09-12 裁定① 起，**只读 ≠ 禁用**：只读行仍可点「编辑/删除/修改密码」，
    /// 点击路径先走**显式接管确认**（adopt-on-write）—— 见 <see cref="EnsureAdoptedAsync"/>。
    /// </summary>
    internal static bool IsReadOnly(ServerConfig server)
    {
        try { return Store.IsReadOnlyServer(server.Id); }
        catch (Exception) { return false; }   // 判据取不到时按"可写"兜底：宁可多弹一次确认，也不误灰用户入口
    }

    /// <summary>是否**尚未接管**的导入条目（= 需要先走接管确认的那一档）。</summary>
    internal static bool IsUnadopted(ServerConfig server)
    {
        try { return Store.IsReadOnlyServer(server.Id) && !ServerConfigStore.IsAdoptedEntry(server); }
        catch (Exception) { return false; }   // 同上：取不到就当未接管，走"确认式接管"而非静默放行
    }

    /// <summary>
    /// 🔴 captain 裁定① 的落地：**adopt-on-write（首次编辑即接管）**。
    /// 未接管的导入条目点「编辑/删除/修改密码」时先弹确认，确认后 `Adopt(id)` 把它移出只读集合
    /// ⇒ 下一次 `Save()` 写进我们自己的 `servers.json`（**`accounts.json` 全程零写入**）。
    /// 反控意义：接管**必须显式发生**（用户点了确认），不是全局放开 —— 未确认时操作即被拦下。
    /// </summary>
    internal static async Task<bool> EnsureAdoptedAsync(FrameworkElement row, ServerConfig server)
    {
        if (!IsReadOnly(server)) { return true; }                       // 本来就可写
        if (ServerConfigStore.IsAdoptedEntry(server)) { return true; }   // 已接管过

        var ok = await ServerPrompts.ConfirmAsync(
            row.XamlRoot,
            "接管这台服务器？",
            "「" + server.Name + "」来自原版 accounts.json（只读）。\n\n"
            + "继续会把它的副本接管到外壳自己的 servers.json —— **原版文件一个字节都不改动** —— "
            + "此后这台服务器就可以自由编辑 / 删除。",
            "接管并继续");

        if (!ok)
        {
            Program.Log($"MENU-ADOPT id={server.Id} cancelled（未接管 ⇒ 操作被拦）");
            return false;
        }

        var adopted = Store.Adopt(server.Id);
        if (adopted)
        {
            Store.Save();   // 真正落进我们自己的 servers.json
            Program.Log($"MENU-ADOPT id={server.Id} ok=True saved=True");
        }
        else
        {
            Program.Log($"MENU-ADOPT id={server.Id} ok=False（服务层拒绝接管）");
        }
        return adopted;
    }

    // ==================================================================================
    // 菜单构造
    // ==================================================================================

    private static MenuFlyout Build(FrameworkElement row, ServerConfig server, Action onChanged)
    {
        var flyout = new MenuFlyout();
        var readOnly = IsReadOnly(server);

        MenuFlyoutItem Item(string label, bool enabled, string pendingReason, Func<Task> action)
        {
            var item = new MenuFlyoutItem { Text = label, IsEnabled = enabled, Tag = label };
            if (!string.IsNullOrEmpty(pendingReason))
            {
                // 说明**与是否可点无关**：未接管的只读行是可点的（点了先确认接管），
                // 所以 tooltip 必须在"可点"时也挂上（captain 裁定① 第 (e) 条）。
                ToolTipService.SetToolTip(item, pendingReason);
            }
            if (!enabled)
            {
                // 禁用态文字色用**既有令牌** TextDisabledBrush（ui 新增，勿自造色值）；
                // 取不到就退回 WinUI 默认禁用色（不硬编 #6A6A6A）。
                if (Application.Current?.Resources != null
                    && Application.Current.Resources.TryGetValue("TextDisabledBrush", out var brush)
                    && brush is Microsoft.UI.Xaml.Media.Brush disabledBrush)
                {
                    item.Foreground = disabledBrush;
                }
            }
            else
            {
                item.Click += async (_, _) =>
                {
                    try
                    {
                        Program.Log($"MENU-CLICK id={server.Id} item={label}");
                        await action();
                        onChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"MENU-FAIL id={server.Id} item={label} {ex.GetType().Name}: {ex.Message}");
                    }
                };
            }
            return item;
        }

        // 1 服务器线路 [待定]
        flyout.Items.Add(Item("服务器线路", false, Pending.ServerRoute, null));

        // 2 修改图标（做）
        flyout.Items.Add(Item("修改图标", true, null, () => ChangeIconAsync(row, server)));

        // 3 修改备注（做）
        flyout.Items.Add(Item("修改备注", true, null, () => ChangeRemarkAsync(row, server)));

        // 4 设为私密 [待定]
        flyout.Items.Add(Item("设为私密", false, Pending.Private, null));

        // 5 媒体库（做）
        flyout.Items.Add(Item("媒体库", true, null, () => ChooseLibrariesAsync(row, server)));

        // 6 媒体库统计 [待定]
        flyout.Items.Add(Item("媒体库统计", false, Pending.LibraryStats, null));

        // 7 strm直链播放 [待定]
        flyout.Items.Add(Item("strm直链播放", false, Pending.StrmDirect, null));

        // 8 禁用预加载（做）：开关态用 ToggleMenuFlyoutItem，状态就是真实字段
        var preload = new ToggleMenuFlyoutItem
        {
            Text = "禁用预加载",
            IsChecked = server.ExtraBool("disablePreload"),
        };
        preload.Click += async (_, _) =>
        {
            try
            {
                server.SetExtra("disablePreload", preload.IsChecked ? "1" : null);
                Store.Update(server);
                Store.Save();
                Program.Log($"MENU-TOGGLE id={server.Id} disablePreload={preload.IsChecked}");
                onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Program.Log($"MENU-FAIL id={server.Id} item=禁用预加载 {ex.GetType().Name}: {ex.Message}");
            }
        };
        flyout.Items.Add(preload);

        // 9 修改密码（做；**只读也可用** —— 点击路径先走接管确认，见 captain 裁定①）
        flyout.Items.Add(Item("修改密码", true, UnadoptedReason(server), () => ChangePasswordAsync(row, server)));

        // 10 编辑（做；同上）
        flyout.Items.Add(Item("编辑", true, UnadoptedReason(server), () => EditAsync(row, server)));

        // 11 删除（做；同上）
        flyout.Items.Add(Item("删除", true, UnadoptedReason(server), () => DeleteAsync(row, server)));

        return flyout;
    }

    /// <summary>
    /// 菜单清单自检（取证用；**不驱动右键**）：把某项服务器实际会得到的 11 项逐条打印
    /// 「文案 + 是否可点 + 置灰理由」，直接对上 §3.1 的 7 做 / 4 待定 与只读条目纪律。
    /// </summary>
    public static string DescribeInventory(ServerConfig server)
    {
        var readOnly = IsReadOnly(server);
        var host = new Grid();
        var flyout = Build(host, server, null);
        var parts = new List<string>();

        foreach (var item in flyout.Items)
        {
            string text;
            bool enabled;
            string reason = null;

            switch (item)
            {
                // ⚠️ 顺序要紧：ToggleMenuFlyoutItem **派生自** MenuFlyoutItem（实测 CS8120 撞过），
                //    必须先判派生类型，否则开关项会被当普通项、IsChecked 也读不到。
                case ToggleMenuFlyoutItem t:
                    text = t.Text;
                    enabled = t.IsEnabled;
                    reason = "checked=" + t.IsChecked;
                    break;
                case MenuFlyoutItem m:
                    text = m.Text;
                    enabled = m.IsEnabled;
                    reason = ToolTipService.GetToolTip(m) as string;
                    break;
                default:
                    text = item.GetType().Name;
                    enabled = false;
                    reason = "未知项类型（不该出现）";
                    break;
            }

            parts.Add($"{(enabled ? "ON " : "OFF")}|{text}|{(reason == null ? "-" : reason)}");
        }

        var line = $"MENU-INVENTORY id={server.Id} name=\"{server.Name}\" readOnly={readOnly}"
            + $" total={flyout.Items.Count} on={flyout.Items.OfType<MenuFlyoutItem>().Count(i => i.IsEnabled) + flyout.Items.OfType<ToggleMenuFlyoutItem>().Count()}"
            + $" :: " + string.Join(" ;; ", parts);
        Program.Log(line);
        return line;
    }

    /// <summary>三项写入口的 tooltip 文案：**未接管 / 已接管两版**（服务层 SERVERS_ADOPT_ON_WRITE.md §3 的最终口径）。</summary>
    private static string UnadoptedReason(ServerConfig server)
        => IsUnadopted(server) ? Pending.AdoptOnWrite
            : (IsReadOnly(server) ? Pending.Adopted : null);

    /// <summary>
    /// 4 个待定项的**理由**（置灰时挂 tooltip —— 只说"待定"不说为什么等于没交代）。
    /// <para>依据 = `shell/docs/UI_SPEC_SHELL.md` **§3.1**（裁定表：7 做 / 4 待定）。</para>
    /// <para>[!] **规格编号只写在这里，不写进字符串**：这四条会经 `MenuFlyoutItem.Text` +
    /// `ToolTipService.SetToolTip` 变成**用户可见文案**，而 `CODE_STANDARD.md §8.5` 的豁免**只给两类**
    /// （①写入目标是日志/证据文件 ②判据正则的字面量）；UI 面文案**禁止出现规格编号**（2026-09-12 收口）。</para>
    /// </summary>
    private static class Pending
    {
        public const string ServerRoute = "待定：当刻 ServerConfig 只有一个 BaseUrl，要做同服多地址回落需先扩模型";
        public const string Private = "待定：语义未定 —— 是「不参与聚合视界」还是「隐藏名称」尚未裁定，不猜";
        public const string LibraryStats = "待定：依赖统计接口，收益低";
        public const string StrmDirect = "待定：原版特性；与「播放走内核」的关系需先核";
        public const string ReadOnly = "该条目来自原版 accounts.json（只读）：我们对它永不回写，如需管理请重新添加";
        /// <summary>未接管的导入条目：**不是禁用**，而是"点下去会先问你是否接管"（captain 裁定①）。</summary>
        public const string AdoptOnWrite = "该条目来自原版 accounts.json（导入）：点下去会先确认「接管到外壳管理」——原文件不会被改动，接管后即可自由编辑/删除";
        /// <summary>已接管的导入条目：血缘仍在，但已归外壳管理（服务层文档 §3 要求的**第二版 tooltip**）。</summary>
        public const string Adopted = "已接管到外壳管理（原版 accounts.json 条目未被改动）——可自由修改/删除，改动重启后仍在";
    }

    // ==================================================================================
    // 7 个实现项
    // ==================================================================================

    private static async Task ChangeIconAsync(FrameworkElement row, ServerConfig server)
    {
        // 内置图标 = 真写入 ServerConfig.IconData；自定义 = 写 ServerConfig.IconUrl。
        // 说明：侧栏把这些值**画出来**属 ui 的行渲染（t26/t34）—— 本项只负责"改得进去、存得下来"。
        // 🔴 图标一律走 **Segoe Fluent Icons 字形**（规格 §1.1：禁止 emoji / 文本符号当图标）。
        //    码位（十六进制）与渲染实测记录见本文件末尾「图标字形表」。
        var builtin = new List<(string Key, string Label, string Glyph)>
        {
            ("builtin:film", "影片", "E8B2"),
            ("builtin:tv", "剧集", "E7F4"),
            ("builtin:music", "音乐", "E8D6"),
            ("builtin:book", "有声书", "E736"),
            ("builtin:cloud", "WebDAV", "E753"),
            ("builtin:star", "常用", "E734"),
        };

        var current = server.IconData ?? string.Empty;
        var picked = await ServerPrompts.AskSingleSelectAsync(
            row.XamlRoot, "修改图标", "内置图标（写入 IconData）；自定义 URL 请用下面的输入框。", builtin, current);

        if (picked != null)
        {
            var iconUrl = await ServerPrompts.AskTextAsync(
                row.XamlRoot, "自定义图标 URL（可留空）", "图标地址", server.IconUrl, "https://…/icon.png");
            if (iconUrl == null) { return; }

            server.IconData = picked;
            server.IconUrl = iconUrl.Trim();
            Store.Update(server);
            Store.Save();
            Program.Log($"MENU-ICON id={server.Id} iconData={picked} iconUrlLen={server.IconUrl.Length}");
            return;
        }

        // 取消内置选择时允许只改 URL（保持"能改"这条路径存在）
        var onlyUrl = await ServerPrompts.AskTextAsync(
            row.XamlRoot, "自定义图标 URL", "图标地址（留空 = 清除）", server.IconUrl, "https://…/icon.png");
        if (onlyUrl == null) { return; }

        server.IconUrl = onlyUrl.Trim();
        Store.Update(server);
        Store.Save();
        Program.Log($"MENU-ICON id={server.Id} iconData=(unchanged) iconUrlLen={server.IconUrl.Length}");
    }

    private static async Task ChangeRemarkAsync(FrameworkElement row, ServerConfig server)
    {
        var current = server.ExtraString("remark") ?? string.Empty;
        var text = await ServerPrompts.AskTextAsync(row.XamlRoot, "修改备注", "备注", current, "给这台服务器起个备注");
        if (text == null) { return; }

        server.SetExtra("remark", string.IsNullOrWhiteSpace(text) ? null : text.Trim());
        Store.Update(server);
        Store.Save();
        Program.Log($"MENU-REMARK id={server.Id} len={(server.ExtraString("remark") ?? string.Empty).Length}");
    }

    private static async Task ChooseLibrariesAsync(FrameworkElement row, ServerConfig server)
    {
        var libs = new List<(string Id, string Name)>();
        var note = string.Empty;

        try
        {
            using var registry = new ServiceRegistry();
            registry.Initialize();

            switch (server.Kind)
            {
                case ServerKind.Emby:
                case ServerKind.Jellyfin:
                    foreach (var view in await registry.CreateEmby(server).GetViewsAsync())
                    {
                        libs.Add((view.Id, view.Name));
                    }
                    break;

                case ServerKind.Audiobookshelf:
                    foreach (var lib in await registry.CreateAudioBookshelf(server).GetLibrariesAsync())
                    {
                        libs.Add((lib.Id, lib.Name));
                    }
                    break;

                default:
                    note = "该类型（" + ServerKindExtensions.DisplayName(server.Kind) + "）没有媒体库概念 ⇒ 本项无可用库。";
                    break;
            }
        }
        catch (Exception ex)
        {
            note = "拉取媒体库失败（" + ex.GetType().Name + "：" + Program.MaskSecrets(ex.Message) + "）";
            Program.Log($"MENU-LIBS-FAIL id={server.Id} {ex.GetType().Name}");
        }

        if (libs.Count == 0)
        {
            await ServerPrompts.ConfirmAsync(row.XamlRoot, "媒体库", string.IsNullOrEmpty(note) ? "没有可选的库。" : note, "知道了");
            return;
        }

        // 勾选 = **要显示**的库；落盘的是它的补集 HiddenLibraryIds（字段语义如此）
        var visible = libs.Select(l => l.Id).Where(id => !server.HiddenLibraryIds.Contains(id)).ToList();
        var chosen = await ServerPrompts.AskMultiSelectAsync(
            row.XamlRoot, "媒体库（勾选要显示的）", "未勾选的库会被隐藏（HiddenLibraryIds）。", libs, visible);
        if (chosen == null) { return; }

        var hidden = new HashSet<string>(libs.Select(l => l.Id).Where(id => !chosen.Contains(id)), StringComparer.Ordinal);
        server.HiddenLibraryIds = hidden;
        Store.Update(server);
        Store.Save();
        Program.Log($"MENU-LIBS id={server.Id} total={libs.Count} hidden={hidden.Count}");
    }

    private static async Task ChangePasswordAsync(FrameworkElement row, ServerConfig server)
    {
        if (!await EnsureAdoptedAsync(row, server)) { return; }

        var old = Passwords.GetServerPassword(server.Id) ?? string.Empty;
        var next = await ServerPrompts.AskPasswordAsync(row.XamlRoot, "修改密码", "新密码");
        if (next == null) { return; }

        // 先写进内存态试连；失败回滚 —— 不留"看着改了其实没生效"
        try
        {
            if (server.Kind == ServerKind.Navidrome)
            {
                server.SetExtra("password", next);
            }
            else
            {
                Passwords.SetServerPassword(server.Id, next);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await ProbeAsync(server, cts.Token);

            // 需要换 token 的类型：用新密码重新登录一次，成功才落盘
            if (server.Kind == ServerKind.Emby || server.Kind == ServerKind.Jellyfin)
            {
                using var registry = new ServiceRegistry();
                registry.Initialize();
                var auth = await EmbyServiceLoginAsync(registry, server, next);
                server.AccessToken = auth.AccessToken;
                server.UserId = auth.UserId;
                Passwords.SetServerToken(server.Id, auth.AccessToken);
            }

            Store.Update(server);
            Store.Save();
            Passwords.Save();
            Program.Log($"MENU-PASSWORD id={server.Id} ok kind={server.Kind}");
        }
        catch (Exception ex)
        {
            // 回滚：内存态与凭据库都退回旧值
            Passwords.SetServerPassword(server.Id, old);
            if (server.Kind == ServerKind.Navidrome) { server.SetExtra("password", old); }
            Passwords.Save();
            Program.Log($"MENU-PASSWORD id={server.Id} FAIL {ex.GetType().Name}: {Program.MaskSecrets(ex.Message)}");
            await ServerPrompts.ConfirmAsync(
                row.XamlRoot, "修改密码失败（已回滚）",
                "新密码没能通过认证，已恢复原密码：\n" + Program.MaskSecrets(ex.Message), "知道了");
        }
    }

    private static async Task EditAsync(FrameworkElement row, ServerConfig server)
    {
        if (!await EnsureAdoptedAsync(row, server)) { return; }

        var ok = await AddServerDialog.ShowAsync(row.XamlRoot, server);
        Program.Log($"MENU-EDIT id={server.Id} saved={ok}");

        if (ok)
        {
            // captain SEAM⑤：写路径一律走"接管 + 更新"（服务层对未接管条目抛 InvalidOperationException 拦下）
            try
            {
                Store.AdoptAndUpdate(server);
                Store.Save();
            }
            catch (InvalidOperationException ex)
            {
                Program.Log($"MENU-EDIT-BLOCKED id={server.Id} {ex.Message}");
                await ServerPrompts.ConfirmAsync(row.XamlRoot, "这条暂时不能修改", ex.Message, "知道了");
            }
        }
    }

    private static async Task DeleteAsync(FrameworkElement row, ServerConfig server)
    {
        // 删除 = **外壳侧墓碑**（captain 裁定①）：先接管（显式确认），再用 AdoptAndRemove —— **绝不删原版条目**
        var wasReadOnly = IsReadOnly(server);
        if (!await EnsureAdoptedAsync(row, server)) { return; }

        var confirmed = await ServerPrompts.ConfirmAsync(
            row.XamlRoot, "删除服务器",
            "删除「" + server.Name + "」？会同时清掉它的凭据。此操作不可撤销。\n"
            + (wasReadOnly ? "（原版 accounts.json 里的原始条目**不会被改动**，只在外壳侧记录一条墓碑。）" : string.Empty),
            "删除");
        if (!confirmed) { return; }

        try
        {
            // captain SEAM⑤：删除一律走 **AdoptAndRemove**（接管 + 外壳侧墓碑；未接管条目由服务层抛异常拦下）
            Store.AdoptAndRemove(server.Id);
        }
        catch (InvalidOperationException ex)
        {
            Program.Log($"MENU-DELETE-BLOCKED id={server.Id} {ex.Message}");
            await ServerPrompts.ConfirmAsync(row.XamlRoot, "这条暂时不能删除", ex.Message, "知道了");
            return;
        }
        Passwords.RemoveServer(server.Id);
        Store.Save();
        Passwords.Save();
        Program.Log($"MENU-DELETE id={server.Id} name=\"{server.Name}\" wasReadOnly={wasReadOnly} tombstone={wasReadOnly}");
    }

    // ==================================================================================
    // 连通探测（与 ServersPage / AddServerDialog 同一条路：逐 Kind 选一个必然发请求的方法）
    // ==================================================================================

    internal static async Task<string> ProbeAsync(ServerConfig cfg, CancellationToken ct)
    {
        using var registry = new ServiceRegistry();
        registry.Initialize();

        switch (cfg.Kind)
        {
            case ServerKind.Emby:
            case ServerKind.Jellyfin:
                await registry.CreateEmby(cfg).GetViewsAsync(ct);
                break;
            case ServerKind.Navidrome:
                await registry.CreateNavidrome(cfg).PingAsync(ct);
                break;
            case ServerKind.Audiobookshelf:
                await registry.CreateAudioBookshelf(cfg).GetLibrariesAsync(ct);
                break;
            case ServerKind.WebDav:
                await registry.CreateWebDav(cfg).PingAsync(ct);
                break;
            default:
                throw new NotSupportedException("未知服务器类型：" + cfg.Kind);
        }

        return "ok";
    }

    private static Task<EmbyAuthResult> EmbyServiceLoginAsync(ServiceRegistry registry, ServerConfig cfg, string password)
    {
        string deviceId;
        try { deviceId = new Services.Infra.DeviceIdService(Services.Infra.AppDataDir.Instance).GetOrCreate(); }
        catch (Exception) { deviceId = "aiplayer-rebuild"; }   // 设备 ID 取不到不影响探测，用固定占位

        return EmbyService.LoginAsync(registry.Http, cfg.BaseUrl, cfg.UserName, password,
            deviceId, "AI Player", "AI Player", "1.0.0", CancellationToken.None);
    }
}

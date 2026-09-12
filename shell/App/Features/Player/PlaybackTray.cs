// t221（重建 t40 / E-P3）：**托盘图标 + 右键菜单**（Windows 侧 Win32 面）。
//
// [逆向到了] 原版外壳是 Flutter（`tray_manager`，见 `reversed/FlutterApp/SERVICE_API.md:196`），
//           本项目外壳是 WinUI 3 ⇒ 没有现成的托盘 API，用 Win32 `Shell_NotifyIcon`（= `DESIGN.md:972` 的 U7 风险项）。
//           菜单内容/降级口径 = `shell/docs/PLAYBACK_LIVE_CONTROL.md:79` + §4（见 `PlaybackControlSession` 文件头）。
// [重建实现了] 本文件：隐藏的**顶层**窗口（不是 message-only —— `SetForegroundWindow` 对 message-only 窗口无效，
//           菜单会点外面不消失）+ `Shell_NotifyIcon` + `HMENU`/`TrackPopupMenu` + 派发到会话。
// [运行验证过了] `shell/Tests/evidence/t221-inplayback-controls.txt`（含菜单结构/两态/像素抓图）。
//
// **为什么菜单从"模型"生成而不是硬编 HMENU**：可用性/文案的唯一真相源在
//    `PlaybackControlSession.BuildMenu()`（纯数据、可被自检逐行断言）⇒ 这里只做"模型 → HMENU"的机械映射，
//    避免出现两份可用性判据（本队踩过"两份真相"的坑）。
//
// **纪律**：只有本进程自己拉起的窗口与图标被操作；`WM_DESTROY` 前先 `NIM_DELETE`（否则托盘留幽灵图标）。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AIPlayer.Shell.Features.Player;

/// <summary>托盘图标 + 右键菜单的宿主（进程级单点；`StartOnce()` 幂等）。</summary>
public sealed class PlaybackTray
{
    /// <summary>进程级单点。</summary>
    public static PlaybackTray Current { get; } = new PlaybackTray();

    /// <summary>自检用：托盘图标/窗口创建失败时的原因（空 = 正常）。</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>图标是否已挂上（`Shell_NotifyIcon(NIM_ADD)` 的真实返回值）。</summary>
    public bool IconAdded { get; private set; }

    /// <summary>最近一次菜单显示的结果（自检打印；未显示过 ⇒ 空）。</summary>
    public string LastShowResult { get; private set; } = string.Empty;

    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 1;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int WM_TIMER = 0x0113;
    private const int WM_CLOSE = 0x0010;
    private const int WM_DESTROY = 0x0002;
    private const int WM_QUERYENDSESSION = 0x0011;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_POPUP = 0x00000010;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_LEFTALIGN = 0x0000;

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int IDI_APPLICATION = 32512;
    private const int TRAY_ICON_ID = 0x4150;          // 'AP'
    private const int AUTOCLOSE_TIMER_ID = 0x7A21;    // 自检用的一次性自动关菜单定时器

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private WndProcDelegate _wndProc;                 // **必须持有引用**：委托被 GC 掉 ⇒ 窗口过程变野指针
    private ushort _classAtom;
    private uint _taskbarCreatedMessage;
    private int _showCount;

    private PlaybackTray()
    {
    }

    /// <summary>本进程的托盘窗口句柄（未建 ⇒ <see cref="IntPtr.Zero"/>）。</summary>
    public IntPtr Handle => _hwnd;

    /// <summary>菜单显示次数（自检断言"真的弹出过"）。</summary>
    public int ShowCount => _showCount;

    /// <summary>
    /// 建隐藏窗口 + 挂托盘图标。**幂等**；任何失败都只留痕（不得因托盘起不来影响外壳启动）。
    /// </summary>
    public void StartOnce()
    {
        if (_hwnd != IntPtr.Zero)
        {
            return;
        }

        try
        {
            _wndProc = WndProcImpl;
            var hInstance = GetModuleHandleW(null);
            var className = "AIPlayerShellTrayWnd";

            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEXW)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInstance,
                lpszClassName = className,
            };
            _classAtom = RegisterClassExW(ref wc);
            if (_classAtom == 0)
            {
                var err = Marshal.GetLastWin32Error();
                // 类已注册（同一进程二次调用）不算失败
                if (err != 1410 /*ERROR_CLASS_ALREADY_EXISTS*/)
                {
                    LastError = "RegisterClassExW failed win32=" + err;
                    Program.Log("PLAYER-TRAY FAIL " + LastError);
                    return;
                }
            }

            // 顶层窗口（WS_POPUP + 从不 ShowWindow）：`SetForegroundWindow` 对它有效 ⇒ 菜单能正常消失。
            //    message-only 窗口（HWND_MESSAGE）**做不到**这一点（这是托盘菜单的经典坑）。
            _hwnd = CreateWindowExW(0, className, "AIPlayerShellTray", WS_POPUP,
                0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                LastError = "CreateWindowExW failed win32=" + Marshal.GetLastWin32Error();
                Program.Log("PLAYER-TRAY FAIL " + LastError);
                return;
            }

            _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
            _hIcon = ResolveIcon();
            IconAdded = AddOrModifyIcon(NIM_ADD);
            LastError = IconAdded ? string.Empty : "Shell_NotifyIcon(NIM_ADD) failed win32=" + Marshal.GetLastWin32Error();

            // 退出收尾：先摘图标再销毁窗口（否则 Windows 会留一枚"幽灵图标"直到鼠标划过）。
            // 用 `AppDomain.ProcessExit`（覆盖 `Application.Exit()` 与窗口关闭两条退出路径；
            // [!] WinAppSDK 1.8 的 `Microsoft.UI.Xaml.Application` **没有** `Exiting` 事件 —— 实测 CS1061，
            // 所以这里不能照 App.xaml.cs 头注那句话说"挂在 Application.Exiting"）。幂等由 Stop() 保证。
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
                Program.Log("PLAYER-TRAY exit-hook attached (AppDomain.ProcessExit -> Stop)");
            }
            catch (Exception ex)
            {
                Program.Log("PLAYER-TRAY exit-hook FAIL " + ex.GetType().Name + ": " + ex.Message);
            }

            Program.Log("PLAYER-TRAY start hwnd=0x" + _hwnd.ToInt64().ToString("X")
                + " classAtom=" + _classAtom
                + " iconAdded=" + IconAdded
                + " hIcon=0x" + _hIcon.ToInt64().ToString("X")
                + " taskbarCreatedMsg=" + _taskbarCreatedMessage
                + (IconAdded ? string.Empty : " err=" + LastError));
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            Program.Log("PLAYER-TRAY FAIL " + LastError);
        }
    }

    /// <summary>取图标：优先外壳 exe 自带图标（用户认得出是我们），失败回落系统默认图标（保证托盘不空）。</summary>
    private static IntPtr ResolveIcon()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var large = new IntPtr[1];
                var small = new IntPtr[1];
                var count = ExtractIconExW(exe, 0, large, small, 1);
                if (count > 0 && small[0] != IntPtr.Zero)
                {
                    return small[0];
                }

                if (count > 0 && large[0] != IntPtr.Zero)
                {
                    return large[0];
                }
            }
        }
        catch (Exception ex)
        {
            Program.Log("PLAYER-TRAY icon-from-exe FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        return LoadIconW(IntPtr.Zero, new IntPtr(IDI_APPLICATION));
    }

    private bool AddOrModifyIcon(uint message)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
            hWnd = _hwnd,
            uID = TRAY_ICON_ID,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "AIPlayer 外壳（播放中控制：右键菜单）",
        };
        return Shell_NotifyIconW(message, ref data);
    }

    /// <summary>卸图标 + 销毁窗口（退出收尾；幂等）。</summary>
    public void Stop()
    {
        try
        {
            if (_hwnd != IntPtr.Zero && IconAdded)
            {
                AddOrModifyIcon(NIM_DELETE);
                IconAdded = false;
            }

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Program.Log("PLAYER-TRAY stop FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // explorer 重启后托盘被清空 ⇒ 重新挂图标（标准做法，判据是注册消息相等）
            if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
            {
                IconAdded = AddOrModifyIcon(NIM_ADD);
                Program.Log("PLAYER-TRAY taskbar-created re-add iconAdded=" + IconAdded);
                return IntPtr.Zero;
            }

            if (msg == WM_TRAYICON)
            {
                var mouse = (int)(lParam.ToInt64() & 0xFFFF);
                if (mouse == WM_LBUTTONUP || mouse == WM_RBUTTONUP)
                {
                    // 左右键都给菜单（左键不另做"切换暂停"——内核没有 pause 命令，做成假动作就是静默无效）
                    ShowMenuAtCursor();
                }

                return IntPtr.Zero;
            }

            if (msg == WM_CONTEXTMENU)
            {
                ShowMenuAtCursor();
                return IntPtr.Zero;
            }

            if (msg == WM_TIMER && wParam.ToInt64() == AUTOCLOSE_TIMER_ID)
            {
                // 自检用：菜单在**同一线程**的模态循环里 ⇒ 这里 `EndMenu()` 关掉的就是它的活动菜单
                KillTimer(_hwnd, (IntPtr)AUTOCLOSE_TIMER_ID);
                var ended = EndMenu();
                Program.Log("PLAYER-TRAY menu auto-close timer fired endMenu=" + ended);
                return IntPtr.Zero;
            }

            if (msg == WM_QUERYENDSESSION || msg == WM_CLOSE || msg == WM_DESTROY)
            {
                if (_hwnd != IntPtr.Zero && IconAdded)
                {
                    AddOrModifyIcon(NIM_DELETE);
                    IconAdded = false;
                }

                if (msg == WM_DESTROY)
                {
                    _hwnd = IntPtr.Zero;
                }
            }
        }
        catch (Exception ex)
        {
            // 窗口过程里绝不让异常逃逸（会打死整个 UI 线程）
            try
            {
                Program.Log("PLAYER-TRAY wndproc FAIL msg=0x" + msg.ToString("X") + " " + ex.GetType().Name + ": " + ex.Message);
            }
            catch (Exception logEx)
            {
                // 日志本身失败也只能吞掉；不能再调 Program.Log（会递归），落 Debug 面（不落盘、不抛）
                System.Diagnostics.Debug.WriteLine("PLAYER-TRAY wndproc log-fail " + logEx.GetType().Name);
            }
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// 在光标处弹出托盘菜单（模态；`TPM_RETURNCMD` ⇒ 返回被选项的整数 id）。
    /// <paramref name="autoCloseMs"/> &gt; 0 时挂一次性定时器自动关闭（**自检专用**，正常路径传 0）。
    /// </summary>
    public string ShowMenuAtCursor(int autoCloseMs = 0)
    {
        if (_hwnd == IntPtr.Zero)
        {
            LastShowResult = "no-window (StartOnce 未成功或已 Stop)";
            Program.Log("PLAYER-TRAY menu skipped reason=" + LastShowResult);
            return LastShowResult;
        }

        var items = PlaybackControlSession.Current.BuildMenu();
        var byId = new Dictionary<int, PlaybackMenuEntry>();
        var hMenu = BuildHMenu(items, byId, 0x1000);
        if (hMenu == IntPtr.Zero)
        {
            LastShowResult = "BuildHMenu returned null";
            Program.Log("PLAYER-TRAY menu FAIL " + LastShowResult);
            return LastShowResult;
        }

        try
        {
            if (!GetCursorPos(out var pt))
            {
                pt.X = 0;
                pt.Y = 0;
            }

            // 契约（Win32）：菜单要能"点外面就消失"，宿主窗口必须是前台窗口
            SetForegroundWindow(_hwnd);
            if (autoCloseMs > 0)
            {
                SetTimer(_hwnd, (IntPtr)AUTOCLOSE_TIMER_ID, (uint)autoCloseMs, IntPtr.Zero);
            }

            _showCount++;
            var picked = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON | TPM_LEFTALIGN,
                pt.X, pt.Y, _hwnd, IntPtr.Zero);

            // 标准收尾：让菜单的"下一次点击"不落在我们窗口上
            PostMessageW(_hwnd, 0x0000 /*WM_NULL*/, IntPtr.Zero, IntPtr.Zero);

            string line;
            if (picked == 0)
            {
                line = "menu dismissed without command (items=" + byId.Count + " at=" + pt.X + "," + pt.Y + ")";
                Program.Log("PLAYER-TRAY " + line);
            }
            else if (byId.TryGetValue(picked, out var entry))
            {
                line = "picked id=" + picked + " action=" + entry.ActionId + " label=" + entry.Label;
                Program.Log("PLAYER-TRAY " + line);
                Dispatch(entry);
            }
            else
            {
                line = "picked unknown id=" + picked;
                Program.Log("PLAYER-TRAY " + line);
            }

            LastShowResult = line;
            return line;
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    /// <summary>
    /// **自检用**：把"模型 → `HMENU`"这一步单独暴露出来，供**枚举真实的 Win32 菜单结构**
    /// （`GetMenuItemCount` / `GetMenuStringW` / `GetMenuState` 逐项读，含灰化位）。
    /// 调用方负责 `DestroyMenu(hMenu)`。产品路径不走这里（走 <see cref="ShowMenuAtCursor"/>）。
    /// </summary>
    public IntPtr BuildMenuForInspection(out Dictionary<int, PlaybackMenuEntry> byId)
    {
        byId = new Dictionary<int, PlaybackMenuEntry>();
        return BuildHMenu(PlaybackControlSession.Current.BuildMenu(), byId, 0x1000);
    }

    /// <summary>模型 → `HMENU`（命令 id 从 <paramref name="nextId"/> 起递增；子菜单用 `MF_POPUP`）。</summary>
    private static IntPtr BuildHMenu(List<PlaybackMenuEntry> items, Dictionary<int, PlaybackMenuEntry> byId, int nextId)
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var id = nextId;
        foreach (var item in items)
        {
            if (item.Separator)
            {
                AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
                continue;
            }

            if (item.Children != null)
            {
                var sub = BuildHMenu(item.Children, byId, id + 1000);
                var label = item.Enabled ? item.Label : item.Label + "（" + item.DisabledReason + "）";
                var flags = MF_POPUP | MF_STRING;
                if (!item.Enabled || item.Children.Count == 0)
                {
                    // 父项自身不可点：把它的"为什么不能点"写进标题（Win32 的父项不响应灰化后的展开）
                    flags |= MF_GRAYED | MF_DISABLED;
                }

                AppendMenuW(hMenu, flags, sub, label);
                continue;
            }

            var leafFlags = MF_STRING;
            if (!item.Enabled)
            {
                leafFlags |= MF_GRAYED | MF_DISABLED;
            }

            var leafLabel = item.Enabled ? item.Label : item.Label + "（" + item.DisabledReason + "）";
            AppendMenuW(hMenu, leafFlags, new IntPtr(id), leafLabel);
            byId[id] = item;
            id++;
        }

        return hMenu;
    }

    /// <summary>执行被选项（叶子动作）：控制命令走会话单点；`StopPlayback` 走进程面。</summary>
    public void Dispatch(PlaybackMenuEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (!entry.Enabled)
        {
            // 理论上不可达（Win32 已灰化），但**不静默**：留一行"试图执行不可用项"
            Program.Log("PLAYER-TRAY dispatch BLOCKED action=" + entry.ActionId + " reason=" + entry.DisabledReason);
            return;
        }

        switch (entry.Action)
        {
            case PlaybackMenuAction.StopPlayback:
                PlaybackControlSession.Current.StopPlayback();
                return;
            case PlaybackMenuAction.Info:
                return;
            default:
                if (entry.Command == null)
                {
                    Program.Log("PLAYER-TRAY dispatch NO-COMMAND action=" + entry.ActionId);
                    return;
                }

                // 菜单在 UI 线程上 ⇒ 这里为了让"点击即发出"不阻塞 UI，用后台任务发（端点调用是 IO）
                var command = entry.Command;
                var actionId = entry.ActionId;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await PlaybackControlSession.Current.SendAsync(command, actionId).ConfigureAwait(false);
                });
                return;
        }
    }

    // ── Win32 面 ─────────────────────────────────────────────────────────────

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EndMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);
}

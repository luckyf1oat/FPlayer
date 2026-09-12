using System;
using Microsoft.UI.Xaml;

namespace AIPlayer.Shell;

public partial class App : Application
{
    private Window _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Program.Log("OnLaunched begin");

        // 内核 → 外壳的回调端点（绑 Loopback 端口 0，生命周期随外壳）。必须在可能触发播放之前启动。
        KernelHost.ShellCallback.StartOnce();

        // 内核日志面不打码，一开播就会被写入明文 api_key ⇒ 启动时就地打码。
        // 纪律：只打码、绝不删除；打码点全工程唯一（复用 Program.MaskSecrets）。
        KernelHost.KernelLogMasker.MaskKernelLogsAtStartup();

        try
        {
            _window = new MainWindow();
            _window.Activate();
            Program.Log("MainWindow activated");

            // 退出侧收尾挂在**主窗口 Closed**：它是唯一既能拿到"用户点了关闭"、
            // 又早于 `Program.Main` 兜底的一次回调（`Program.Main` 的兜底保留，用于非窗口路径的退出）。
            //
            // ⚠️ **订阅必须排在 `Activate()` 之后**：`Activate()` 之前窗口尚未展示，
            //    若那一刻就被关闭，事件会早于订阅发生 ⇒ 漏掉本轮收尾（只能靠 `Program.Main` 兜底救）。
            // 依据（含三次试错的原始读数）：`shell/Tests/evidence/t27-exitapi-probe.txt`。
            _window.Closed += (_, _) =>
            {
                ExitProbe.MarkContext("window:MainWindow.Closed");
                ExitProbe.RunExitSideWork("Window.Closed");
            };

            // 程序化退出路径的自检驱动（未设 `SHELL_SELFTEST_EXIT_API` 时为空操作）
            ExitProbe.ScheduleAutoExitIfRequested();
            // 退出挂点反射枚举（未设 `SHELL_SELFTEST_PROBE_EXIT_API` 时为空操作）
            KernelHost.ExitApiProbe.RunIfRequested();

            // 组件装载闸门（未设 `SHELL_SELFTEST_LOADCOMPONENT` 时为空操作）
            KernelHost.KernelLoadGate.RunIfRequested();
        }
        catch (Exception ex)
        {
            // 不吞异常：磁盘留证后继续抛，便于用 FirstChanceException 追栈
            Program.Log("OnLaunched FAILED " + ex);
            throw;
        }
    }
}

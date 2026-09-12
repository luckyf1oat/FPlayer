// t27-F-A ⑤ 退出侧的**取证/自检支撑**（默认零影响）。
//
// 背景（captain 2026-09-12 裁示 ①）：退出打码的挂点从 `Program.cs`（`Application.Start` 返回后）
// **迁到** `App` 的 `Application.Exiting` 事件 —— 理由：① 消掉一处越界改动；② `Program.cs` 是共享文件；
// ③ `Exiting` 是 WinUI 3 惯用位置，语义与前者**等价**（两者都在"进程退出前一跳"）。
//
// 🔴 captain 的两个前置条件（本文件就是为它们写的）：
//   ① **必须实测两条退出路径**，不能只测一条；
//   ② 若某条路径不触发 `Exiting` ⇒ **不要凭猜补第二个钩子**，把"哪条路径不触发 + 原始日志"报回去。
// ⇒ 这里提供两样东西：
//   · **退出原因留痕**：每条路径进入时写一行 `EXIT-CTX …`，让证据里能看出走的是哪条；
//   · **路径②的驱动钩子**：`SHELL_SELFTEST_EXIT_API=1` 时延时调用 `Application.Current.Exit()`
//     （这是"程序化退出"那条路径，人手点窗口关不出来）。默认不设该变量 ⇒ 生产零影响。

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace AIPlayer.Shell;

/// <summary>退出路径的取支撑与自检驱动。</summary>
public static class ExitProbe
{
    /// <summary>`SHELL_SELFTEST_EXIT_API=1` ⇒ 起窗后自动走"程序化退出"路径（`Application.Exit()`）。</summary>
    public const string AutoExitEnvVar = "SHELL_SELFTEST_EXIT_API";

    /// <summary>兜底退出延时（秒）：窗口起来后等这么久再调 `Exit()`。</summary>
    public const string AutoExitDelayEnvVar = "SHELL_SELFTEST_EXIT_DELAY";

    private static int _scheduled;

    /// <summary>写入"本条退出路径"的上下文（供证据核对是哪条路径触发了 `Exiting`）。</summary>
    public static void MarkContext(string reason) => Program.Log("EXIT-CTX " + reason);

    /// <summary>退出侧收尾（**唯一实现**：由 `App.Exiting` 调用；`Program.Main` 不再挂）。</summary>
    public static void RunExitSideWork(string trigger)
    {
        Program.Log("APP-EXITING trigger=" + trigger);
        KernelHost.KernelLogMasker.MaskKernelLogsAtExit();
        KernelHost.LocalVideoPathSemantic.RestoreAfterPlayback(Program.Log);
        Program.Log("APP-EXITING done");
    }

    /// <summary>
    /// 路径②的自检驱动：`SHELL_SELFTEST_EXIT_API=1` 时，延时调用 `Application.Current.Exit()`。
    /// **幂等**（只排一次）；默认不设变量 ⇒ 不排任何任务。
    /// </summary>
    public static void ScheduleAutoExitIfRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(AutoExitEnvVar), "1", StringComparison.Ordinal))
        {
            return;
        }

        if (Interlocked.Exchange(ref _scheduled, 1) != 0)
        {
            return;
        }

        var seconds = 8;
        if (int.TryParse(Environment.GetEnvironmentVariable(AutoExitDelayEnvVar), out var parsed) && parsed > 0)
        {
            seconds = parsed;
        }

        Program.Log("SELFTEST-EXIT-API scheduled in " + seconds + "s（程序化退出路径）");
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
            try
            {
                MarkContext("api:Application.Current.Exit()");
                Program.Log("SELFTEST-EXIT-API invoking Application.Current.Exit()");
                Application.Current.Exit();
                Program.Log("SELFTEST-EXIT-API Exit() returned");
            }
            catch (Exception ex)
            {
                Program.Log("SELFTEST-EXIT-API FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        });
    }
}

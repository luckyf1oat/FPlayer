using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// 内核日志面的**就地打码**（t12 尾项 b'）。
///
/// <para>**为什么必须做**（2026-09-11 实测，不是假设）：内核自己写的
/// <c>%LOCALAPPDATA%\AIPlayer\logs\log-player-&lt;date&gt;.txt</c> **完全不打码**。我方一接通播放，
/// 内核就把带凭据的完整直连 URL 写进去 —— 实测我的播放验证那几次让
/// <c>log-player-2026-09-11.txt</c>（mtime 22:05:36）出现 <b>15 行</b>明文
/// <c>api_key=&lt;32 hex&gt;</c>，而打码形态 <c>api_key=***</c> 为 <b>0</b>。
/// 即：**这条曝露面是我方验证动作亲手激活的**，不是历史遗留。</para>
///
/// <para>**处置纪律**（逐条照 captain 口径）：
/// ① **只打码、绝不删除**文件，也不改行数；
/// ② 复用**唯一打码点** <see cref="Program.MaskSecrets"/>（与 <c>Log()</c> 同一套正则）；
/// ③ 只在**确有命中**时才回写（无命中的文件一个字节都不动）；
/// ④ 每次打码留**一条审计行**（文件数 / 改动数 / 命中处数），审计行自身也经打码；
/// ⑤ 写回用 **UTF-8 无 BOM**（本队踩过的编码坑）。</para>
///
/// <para>**反控制**：往日志目录放一个含**合成假 token** 的 <c>.txt</c> ⇒ 启动后该值必须变成 <c>***</c>，
/// 且审计行处数 +1（假 token 一律只留 ≤2 位引用，见团队纪律）。</para>
/// </summary>
public static class KernelLogMasker
{
    /// <summary>
    /// 内核日志里**凭据形态**的识别式（t99 扩面）：除原有的 `api_key=&lt;32 hex&gt;`，还覆盖
    /// `--http-header=&lt;base64&gt;`（内含 `X-Emby-Token`）与认证头 —— 这三类都实测出现在内核/外壳日志里。
    /// 只识别"形态"，具体替换一律交给唯一打码器 <see cref="Infrastructure.SecretMasker"/>。
    /// </summary>
    private static readonly Regex SecretPattern = new Regex(
        @"api_key=[0-9a-fA-F]{16,}|--http-header=\S+|(?:X-Emby-Token|X-Emby-Authorization)\s*:\s*\S+",
        RegexOptions.Compiled);

    public static void MaskKernelLogsAtStartup()
    {
        try
        {
            var result = MaskLogsUnder(KernelLogRoot);
            if (result.Changed > 0)
            {
                Program.Log("KERNEL-LOG-MASK done kind=startup files=" + result.Files
                    + " changed=" + result.Changed + " hits=" + result.Occurrences);
            }
            else
            {
                Program.Log("KERNEL-LOG-MASK audit kind=startup files=" + result.Files
                    + " changed=0 hits=0 skipped=" + result.Skipped);
            }
        }
        catch (Exception ex)
        {
            // 打码失败绝不影响启动（与 Program.Log 的容错口径一致）
            Program.Log("KERNEL-LOG-MASK FAILED kind=startup " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// **退出时再跑一遍**（t27-F-A 第 ⑤ 项）。与启动版的两点差异：
    /// ① **并发保护**：若 <c>AIPlayer.MpvHost</c> 仍在跑 ⇒ **跳过整轮并留审计行**
    ///    （内核全程持有同一个 Serilog 文件 sink，`shared: true`；我们与它同时写同一个文件必然互相截断）。
    ///    判据用 <see cref="KernelLauncher.CountRunningKernels"/>；<c>-1</c>（探测失败）**按"在跑"处理**（保守）。
    /// ② **有命中也必留审计行**（启动版只在有改动时才汇总）：退出是最后一跳，必须能从日志证明"跑过没有"。
    /// </summary>
    public static void MaskKernelLogsAtExit()
    {
        try
        {
            var running = KernelLauncher.CountRunningKernels();
            if (running != 0)
            {
                // 反控制：内核在跑时必须留**跳过**审计行（不能静默什么都不做）
                Program.Log("KERNEL-LOG-MASK audit kind=exit result=SKIPPED reason=kernel-running"
                    + " running=" + (running < 0 ? "<probe-failed>" : running.ToString())
                    + " logsRoot=" + KernelLogRoot);
                return;
            }

            var result = MaskLogsUnder(KernelLogRoot);
            Program.Log("KERNEL-LOG-MASK audit kind=exit result=RAN files=" + result.Files
                + " changed=" + result.Changed + " hits=" + result.Occurrences + " skipped=" + result.Skipped);
        }
        catch (Exception ex)
        {
            Program.Log("KERNEL-LOG-MASK FAILED kind=exit " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// 日志根 = **跟数据根走**（t221 收口 t226 报出的同族硬编码）。
    ///
    /// <para>改前：`Path.Combine(Environment.GetFolderPath(LocalApplicationData), "AIPlayer", "logs")`
    /// —— 环境变量 `AIPLAYER_APPDATA_ROOT` 覆盖数据根时**这条推导不跟随**（`GetFolderPath` 走
    /// `SHGetKnownFolderPath`，不看环境变量）⇒ 沙箱/便携形态下打码扫的是**真根**：
    /// ① 沙箱里内核写在沙箱根、扫描扫真根 ⇒ **该掩的没掩**；
    /// ② 反向还有一层：真根被别人的内核写入时，这里会去改**真根**（越界写）。</para>
    ///
    /// <para>改后：与 `<c>AppDataDir.LogFile</c>`（`AppDataDir.cs:467`）**同一个推导**（`Root` + `logs`），
    /// 不新建第二套根解析 —— 与 `SearchLog.cs`（t226）保持同一口径。</para>
    /// </summary>
    public static string KernelLogRoot => Path.Combine(
        AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root,
        "logs");

    /// <summary>一轮打码的结果计数（四要素可复算：文件数 / 改动文件数 / 命中处数 / 跳过数）。</summary>
    public sealed class MaskResult
    {
        public int Files { get; set; }

        public int Changed { get; set; }

        public int Occurrences { get; set; }

        public int Skipped { get; set; }
    }

    /// <summary>扫描一个根目录下的 <c>*.txt</c> 就地打码（**只打码、绝不删除、不改行数**）。</summary>
    public static MaskResult MaskLogsUnder(string root)
    {
        var result = new MaskResult();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
        {
            result.Files++;
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                result.Skipped++;
                Program.Log("KERNEL-LOG-MASK skip " + Path.GetFileName(path) + " " + ex.GetType().Name);
                continue;
            }

            var matches = SecretPattern.Matches(text);
            if (matches.Count == 0)
            {
                continue;
            }

            result.Occurrences += matches.Count;
            result.Changed++;

            // 只替换匹配片段本身 ⇒ 行数、其余内容逐字不变；
            // 替换值交给**唯一打码器** `SecretMasker`（t99：全工程一份规则，不再各写窄正则）。
            var masked = SecretPattern.Replace(text, match => Infrastructure.SecretMasker.Mask(match.Value));
            File.WriteAllText(path, masked, new UTF8Encoding(false));
            Program.Log("KERNEL-LOG-MASK file=" + Path.GetFileName(path) + " hits=" + matches.Count);
        }

        return result;
    }

    /// <summary>
    /// **自检钩子（默认零影响）**：`SHELL_SELFTEST_MASK=exit` 时自动在"内核在跑"与"内核已停"两种情形下
    /// 各跑一次 <see cref="MaskKernelLogsAtExit"/>，各留一条审计行 —— 否则这两条实测证据只能靠人手动关窗，
    /// 无法自动化取证（本队纪律：完成必须带运行证据）。
    ///
    /// <para>时序（都在后台线程）：等内核起来（最多 60 s）→ **第 1 次**（预期 <c>result=SKIPPED</c>）
    /// → 结束内核进程 → **第 2 次**（预期 <c>result=RAN</c>）。</para>
    /// </summary>
    public static async System.Threading.Tasks.Task RunExitMaskSelfTestIfRequestedAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SHELL_SELFTEST_MASK"), "exit",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Program.Log("SELFTEST-MASK BEGIN kind=exit-two-cases env=" + Environment.GetEnvironmentVariable("SHELL_SELFTEST_MASK"));

        // ── 情形 ①：内核在跑 ⇒ 必须跳过并留审计行 ──────────────────────────
        var running = 0;
        for (var i = 0; i < 60; i++)
        {
            running = KernelLauncher.CountRunningKernels();
            if (running > 0) break;
            await System.Threading.Tasks.Task.Delay(1000);
        }

        Program.Log("SELFTEST-MASK case1 kernelRunning=" + running);
        MaskKernelLogsAtExit();

        // ── 情形 ②：内核已停 ⇒ 必须真跑打码并留审计行 ──────────────────────
        Program.Log("SELFTEST-MASK case2 terminating-kernel (self-test only)");

        // 🔴 H6 纪律：**只停本进程自己 `Process.Start` 返回的那个内核**（`KernelLauncher.LastLaunchedKernel`），
        //    绝不按进程名批量结束 —— 按名结束会连带杀掉队友/用户正在跑的内核，且不可逆。
        //    没有"自己拉起的"内核时**只报告、不动手**（此时内核本来就没在跑，情形 ② 依旧成立）。
        var launched = KernelLauncher.LastLaunchedKernel;
        if (launched == null)
        {
            Program.Log("SELFTEST-MASK case2 no-self-launched-kernel (nothing to stop; 不按名结束他人进程)");
        }
        else
        {
            try
            {
                var pid = launched.Id;
                launched.Kill(entireProcessTree: true);
                Program.Log("SELFTEST-MASK case2 stopped self-launched kernel pid=" + pid);
            }
            catch (Exception ex)
            {
                Program.Log("SELFTEST-MASK kill FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        await System.Threading.Tasks.Task.Delay(3000);
        Program.Log("SELFTEST-MASK case2 kernelRunning=" + KernelLauncher.CountRunningKernels());
        MaskKernelLogsAtExit();

        Program.Log("SELFTEST-MASK END kind=exit-two-cases");
    }
}


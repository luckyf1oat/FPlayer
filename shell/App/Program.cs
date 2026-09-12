using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using AIPlayer.Shell.Services.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AIPlayer.Shell;

/// <summary>
/// 自管入口（csproj 设 DISABLE_XAML_GENERATED_MAIN）。
/// 为什么自写 Main：
///   1) 挂 FirstChanceException —— 内核/WinUI 的 stowed 异常只有它能拿到真栈（t6 的 KERNEL-INJECT 就是这样定位的）；
///   2) 挂 AssemblyLoadContext.Resolving —— 内核依赖闭包里有不走 deps.json 的程序集
///      （见 shell/docs/KERNEL_DEPS.md：必须显式搬运的两项 + 47 项托管闭包）。
/// </summary>
public static class Program
{
    private const string LogDirName = "logs";

    private const string LogFileName = "shell-startup.log";

    /// <summary>解析完成前的**有界**暂存：既不写回 exe 目录，也不静默丢弃；落点可用后按序冲刷。</summary>
    private static readonly List<string> PendingLogLines = new List<string>();

    private const int PendingCap = 2000;

    private static readonly object SinkLock = new object();

    private static string _logPath;              // null = 落点暂不可用（解析失败 / 目录不可写）
    private static bool _sinkResolved;           // 进程内只解析一次
    private static string _sinkFailReason = "<none>";

    /// <summary>
    /// 把日志落点解析到**数据根**下 <c>logs\shell-startup.log</c>（t146），不再写 exe 所在目录。
    /// 为什么必须改（两条都是实测后果）：① **污染交付面** —— `dist\AIPlayer\shell-startup.log` 让交付树递归
    /// 989 → 990；② **多写者撞锁** —— 同一目录两个实例争同一文件 ⇒
    /// `IOException: The process cannot access the file '…\dist\AIPlayer\shell-startup.log' because it is being used by another process`。
    /// 落点跟**数据根**走（`AIPLAYER_APPDATA_ROOT` 覆盖 / 便携 `data\` / 默认 `%LOCALAPPDATA%\AIPlayer`），
    /// 与内核日志、缓存、凭据同根 ⇒ 从副本 smoke 也不会再写进交付树。
    /// 幂等；返回 false = 落点不可用（已写一行 stderr，进程继续跑）。
    /// </summary>
    private static bool PrepareLogSink()
    {
        lock (SinkLock)
        {
            if (_sinkResolved) { return _logPath != null; }
            _sinkResolved = true;

            try
            {
                var root = AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root;
                var dir = Path.Combine(root, LogDirName);
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, LogFileName);
            }
            catch (Exception ex)
            {
                _logPath = null;
                _sinkFailReason = ex.GetType().Name + ": " + ex.Message;
            }

            if (_logPath == null)
            {
                // **可见失败**：不崩、不静默；行仍进有界暂存，落点恢复后还能冲刷出去。
                SafeStderr("STARTUP-LOG-SINK-UNAVAILABLE " + _sinkFailReason);
            }

            return _logPath != null;
        }
    }

    private static void SafeStderr(string line)
    {
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (IOException) { }              // 有意忽略：stderr 不可写不应让启动失败
        catch (ObjectDisposedException) { }  // 有意忽略：同上，写端已释放
    }

    /// <summary>
    /// 追加一行，**带短重试**：两个实例同根时仍会争同一文件（AppendAllText 的写窗口很短，但不是零），
    /// 重试把"撞上就丢一行"变成"让一下再写"。这才真正闭合 t146 的 ⑤（多实例不撞锁）。
    /// </summary>
    private static void AppendWithRetry(string path, string text)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.AppendAllText(path, text);
                return;
            }
            catch (IOException) when (attempt < 8)
            {
                Thread.Sleep(15);
            }
        }
    }

    internal static void Log(string line)
    {
        // ⚠️ **唯一写盘点**：集中打码。执行到这里之前的所有路径（FIRSTCHANCE 回显的异常原文、
        //    服务层抛出的 URL、自检钩子入参…）都可能带 `api_key=<token>`，逐处打码必然漏。
        //    与 shell/Spike/Probe.cs 的 MaskSecrets 同一条正则（那条是 Spike 侧的写盘点）。
        line = MaskSecrets(line);
        var stamped = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line;

        try
        {
            lock (SinkLock)
            {
                if (!_sinkResolved) { PrepareLogSink(); }

                if (_logPath == null)
                {
                    // 落点不可用：**暂存**（有界），绝不回退到 exe 目录。
                    if (PendingLogLines.Count < PendingCap) { PendingLogLines.Add(stamped); }
                    else { PendingLogLines[PendingCap - 1] = "<early lines dropped at cap=" + PendingCap + ">"; }
                    return;
                }

                if (PendingLogLines.Count > 0)
                {
                    // 早期行**按发生顺序**补在最前（它们才是本次启动最早的诊断），写完即清空。
                    AppendWithRetry(_logPath, string.Join(Environment.NewLine, PendingLogLines) + Environment.NewLine);
                    PendingLogLines.Clear();
                }

                AppendWithRetry(_logPath, stamped + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // 写不进去（盘满/权限/被独占）时降级到 stderr，并把失败原因回填；
            // 行本身仍进有界暂存 ⇒ "写不进去"不会变成"这段历史消失"。
            _sinkFailReason = ex.GetType().Name + ": " + ex.Message;
            lock (SinkLock)
            {
                if (PendingLogLines.Count < PendingCap) { PendingLogLines.Add(stamped); }
            }

            SafeStderr("STARTUP-LOG-FAIL " + _sinkFailReason);
        }

        Mirror(line);
    }

    /// <summary>
    /// 凭据打码：转发到全工程唯一实现 <see cref="Infrastructure.SecretMasker.Mask"/>。
    /// 保留本方法只为不动既有调用点；单一实现意味着规则只有一处会漂。
    /// </summary>
    internal static string MaskSecrets(string s) => Infrastructure.SecretMasker.Mask(s);

    /// <summary>
    /// 把启动诊断镜像进应用日志（日志页数据源 = <c>PlayerLogService</c>，等价 Dart <c>PlayerLogService</c>）。
    /// FIRSTCHANCE 是逐异常噪声（内核/WinUI 预期内异常也命中，会冲掉环形缓冲），不入 UI 日志；
    /// 解析失败与主崩溃按 warn 入，便于在日志页按级别筛出。
    /// </summary>
    private static void Mirror(string line)
    {
        if (line.StartsWith("FIRSTCHANCE", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (line.StartsWith("MAIN-CRASH", StringComparison.Ordinal)
                || line.StartsWith("RESOLVE-FAIL", StringComparison.Ordinal)
                || line.StartsWith("RESOLVE-NOFILE", StringComparison.Ordinal))
            {
                DebugLog.Warn(line);
            }
            else
            {
                DebugLog.Info(line);
            }
        }
        catch (Exception ex)
        {
            // 镜像进应用日志失败同样不得影响启动；这里同样降级到 stderr，避免"启动日志没写进去"被静默。
            try
            {
                Console.Error.WriteLine("STARTUP-MIRROR-FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
            catch (IOException) { }              // 有意忽略：stderr 不可写不应让启动失败
            catch (ObjectDisposedException) { }  // 有意忽略：同上
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        // t146：落点**先行** —— 在任何诊断行（含紧接着的 FirstChance 回显）之前把日志落点解析到**数据根**，
        // 让热路径只做一次"带短重试的追加"，且解析失败时**不回退到 exe 目录**（处置见 PrepareLogSink / Log）。
        PrepareLogSink();

        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            Log("FIRSTCHANCE " + e.Exception.GetType().FullName + ": " + e.Exception.Message);

        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    var asm = ctx.LoadFromAssemblyPath(candidate);
                    Log("RESOLVED " + name.Name + " <- app dir");
                    return asm;
                }
                catch (Exception ex)
                {
                    Log("RESOLVE-FAIL " + name.Name + " -> " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            else
            {
                Log("RESOLVE-NOFILE " + name.Name);
            }
            return null;
        };

        Log("Main entered; args=" + args.Length);

        // ── t227（乙）：**启动即把两个策略加载一次** ────────────────────────────────
        // 目的：消灭"只读 `UserAgentPolicy.Current` 且此前从未构造过 `ShellHttpClient`"路径上的默认 UA 残留
        // （`services` 点名的首例 = "直接起内核"那条 `EmbyService.HostHttpHeaders` 读 UA，`EmbyService.cs:126`）。
        //
        // [!] **为什么选 `Program.Main` 早段，而不是 `App` 构造 / `OnLaunched`**（卡面要求二选一 + 写明理由）：
        //   ① `Main` 是**最先**执行的一跳（`App` 构造发生在 `Application.Start` 回调里，`:253`）；
        //   ② 此刻日志落点已解析（`PrepareLogSink()`，见文件头 t146 段）⇒ 读数直接落到**数据根**日志，
        //      不依赖 WinUI 是否起来；
        //   ③ 本卡要消灭的路径（起内核/取流）全部发生在 `OnLaunched` 之后 ⇒ 放在 `Main` 早段即"任何读点之前"。
        // 位置：排在日志落点与两个异常钩子**之后**（这样加载失败也会被 FIRSTCHANCE/日志捕获），但在 COM/WinUI 之前。
        RunStartupPolicyProbeIfRequested("before-load");   // 改前语义读数（此刻 IsLoaded 必为 false）
        RunStartupPolicyLoad();                            // 启动即 Load 一次（`SHELL_UA_SKIP_STARTUP_LOAD=1` 时跳过）
        RunStartupPolicyProbeIfRequested("after-load");    // 改后读数（含 ④ 显式语义边界两跳）

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("InitializeComWrappers OK");

            // 单实例闸门：已有实例 ⇒ 留一行审计并按非 0 退出码结束。第二个实例不该把窗口建起来，
            // 也不该去改写 callback-port.json —— 它会把第一个实例正监听的端口判成"被占用"。
            // 用 Environment.ExitCode 而非 `return 3`：Main 是 void，这样也不改变任何既有返回路径。
            if (!AIPlayer.Shell.Infrastructure.SingleInstance.TryAcquire(out var existing))
            {
                Log("SINGLE-INSTANCE-REJECTED existing=" + (existing ?? "-"));
                AIPlayer.Shell.Infrastructure.SingleInstance.NotifyAlreadyRunning(existing, Log);   // 可见提示：此刻 WinUI 未启动（无窗口/XamlRoot）只能用原生弹框；SHELL_SINGLEINSTANCE_SILENT=1 时只留日志
                Environment.ExitCode = 3;
                return;
            }

            // ⚠️ 回调参数**不能**叫 `_`：叫 `_` 时下面那句 `_ = new App()` 会被编译成
            //    「给形参赋值」而不是丢弃，得到 CS0029（无法把 App 转成 ApplicationInitializationCallbackParams）。
            Application.Start(p =>
            {
                var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(ctx);
                Log("sync context set; constructing App");
                var app = new App();
                Log("App constructed: " + app.GetType().FullName);
            });

            Log("Application.Start returned");

            // t27-F-A ⑤ 的**幂等兜底**：真正的挂点是 `App` 里的主窗口 `Closed`（`Window.Closed` 实测存在且可用），
            // 这里再跑一次只为防"存在不经 `Closed` 的退出路径" —— 打码器**幂等**（无命中 ⇒ `changed=0 hits=0`），
            // 故重复调用安全。依据与三次试错的原始反射输出见 `shell/App/KernelHost/t27-FA-EVIDENCE.md` §5.2
            // 与 `shell/Tests/evidence/t27-exitapi-probe.txt`。
            KernelHost.KernelLogMasker.MaskKernelLogsAtExit();
            KernelHost.LocalVideoPathSemantic.RestoreAfterPlayback(Log);   // ④ 语义修正兜底
            Log("Main exiting after exit-mask (idempotent fallback)");
        }
        catch (Exception ex)
        {
            Log("MAIN-CRASH " + ex);
            throw;
        }
    }

    // ── t227：启动即加载策略 + 自检读数（默认零影响）──────────────────────────────

    /// <summary>t227 自检钩子（默认零影响）：`SHELL_SELFTEST_UA=&lt;tag&gt;` ⇒ 启动早段打印策略读数。</summary>
    public const string UaProbeEnvVar = "SHELL_SELFTEST_UA";

    /// <summary>t227 反控开关：`SHELL_UA_SKIP_STARTUP_LOAD=1` ⇒ 跳过"启动即 Load 一次"（复现**改前**语义）。</summary>
    public const string UaSkipLoadEnvVar = "SHELL_UA_SKIP_STARTUP_LOAD";

    /// <summary>
    /// 启动即调用两个策略的加载入口**一次**。
    ///
    /// <para>加载入口 = <c>SettingsService.Instance.Load()</c> —— 它内部回调
    /// <c>UserAgentPolicy.Apply(_settings)</c> 与 <c>ProxyPolicy.Apply(_settings)</c>
    /// （`shell/Services/Settings/SettingsService.cs:68/:70`）⇒ 调它一次即"两个策略都被真实设置覆盖"。
    /// `Load()` 幂等（`:50 IsLoaded` 守卫）。</para>
    ///
    /// <para>失败**不阻断启动**（与既有惰性路径同口径：读点会回落默认），但**必须留痕**（不静默）。</para>
    /// </summary>
    private static void RunStartupPolicyLoad()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(UaSkipLoadEnvVar), "1", StringComparison.Ordinal))
        {
            Log("UA-STARTUP-LOAD skipped reason=" + UaSkipLoadEnvVar + "=1（反控臂：复现改前语义）");
            return;
        }

        try
        {
            var settings = AIPlayer.Shell.Services.Settings.SettingsService.Instance;
            settings.Load();
            Log("UA-STARTUP-LOAD done isLoaded=" + settings.IsLoaded
                + " userAgent=" + SafeValue(AIPlayer.Shell.Services.Http.UserAgentPolicy.Current)
                + " proxy=" + SafeValue(AIPlayer.Shell.Services.Http.ProxyPolicy.Current));
        }
        catch (Exception ex)
        {
            Log("UA-STARTUP-LOAD FAIL " + ex.GetType().Name + ": " + ex.Message + " ⇒ 读点仍走惰性/默认");
        }
    }

    /// <summary>
    /// 打印一次策略读数（只在 `SHELL_SELFTEST_UA` 非空时执行）。
    /// `after-load` 这一跳额外做两步**语义边界**读数（卡面验收④）：
    /// ① `SetCurrent("aip-explicit/t227")` ⇒ 显式写入必须生效；② `FollowSettings()` ⇒ 回到设置值。
    /// </summary>
    private static void RunStartupPolicyProbeIfRequested(string stage)
    {
        var tag = Environment.GetEnvironmentVariable(UaProbeEnvVar);
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        LogPolicyReading(tag, stage);
        if (!string.Equals(stage, "after-load", StringComparison.Ordinal))
        {
            return;
        }

        AIPlayer.Shell.Services.Http.UserAgentPolicy.SetCurrent("aip-explicit/t227");
        LogPolicyReading(tag, "explicit-set");
        AIPlayer.Shell.Services.Http.UserAgentPolicy.FollowSettings();
        LogPolicyReading(tag, "explicit-followed");
    }

    /// <summary>
    /// 一行读数，覆盖 UA/代理的**全部读面**（都指向同一单点，行号见证据件）：
    /// `SettingsService.IsLoaded` / `SettingsService.EffectiveUserAgent`(`SettingsService.cs:162`) /
    /// `UserAgentPolicy.Current`(`UserAgentPolicy.cs:55`) / `AppConstants.UserAgent`(`AppConstants.cs:37`) /
    /// `ProxyPolicy.Current`(`ProxyPolicy.cs:35`) —— 且**不构造任何 `ShellHttpClient`**（卡面验收③ 的前提）。
    /// </summary>
    private static void LogPolicyReading(string tag, string stage)
    {
        var settings = AIPlayer.Shell.Services.Settings.SettingsService.Instance;
        Log("UA-PROBE tag=" + tag + " stage=" + stage
            + " settingsIsLoaded=" + settings.IsLoaded
            + " settingsEffectiveUserAgent=" + SafeValue(settings.EffectiveUserAgent)
            + " userAgentPolicyCurrent=" + SafeValue(AIPlayer.Shell.Services.Http.UserAgentPolicy.Current)
            + " appConstantsUserAgent=" + SafeValue(AIPlayer.Shell.Services.Constants.AppConstants.UserAgent)
            + " proxyPolicyCurrent=" + SafeValue(AIPlayer.Shell.Services.Http.ProxyPolicy.Current)
            + " isPinned=" + AIPlayer.Shell.Services.Http.UserAgentPolicy.IsPinned
            + " hasExplicit=" + AIPlayer.Shell.Services.Http.UserAgentPolicy.HasExplicit);
    }

    private static string SafeValue(string value)
        => value == null ? "<null>" : (value.Length == 0 ? "<empty>" : value);
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// 内核启动器（**M3 = 进程外拉起内核 exe = 原版架构**，captain 2026-09-11 裁决）。
///
/// <para>**M3 为什么是终选**：原版自己就是进程外（Dart 侧 `MpvHostService.launch` + 41 参数 + 9 类 HTTP 回调），
/// 且它一次性消掉进程内方案的一整类结构问题 —— 不需要引用内核 DLL、不需要把内核 XBF 折进**宿主** PRI、
/// 不需要复刻 DI 与反射注入 `GlobalDependencies`、不需要 `ActivateXamlRoot` 轮询；
/// 那三个把 M1 逼死的结构冲突（同名 `App`/`MainWindow`、XBF 程序集身份、元数据手工串接）**全部消失**。</para>
///
/// <para>**参数组装已分离到 <see cref="KernelArgumentBuilder"/>**（t27-F-A ②：41 参数补齐）。
/// 本类只保留"进程怎么起"这件事。</para>
///
/// <para>**最小可用的三条纪律**（都踩过或有权威依据）：
/// ① **显式传 `--libmpv=`**（`data\player\libmpv-2.dll`）⇒ 内核 `AppViewModel.cs:281-285` 自己写回并过闸
///    ⇒ **绕开对用户真实 `player\settings.json` 的高危读-改-写**；
/// ② **`--title=` 绝不能含 URL/凭据**（事实 18/28）—— 传的是媒体标题；
/// ③ **首播 `StartPosition` 留空**（= 不传 `--start=`）：续播点已烘进 `MediaPath` 的 `StartTimeTicks`，
///    再传 `--start=N` 会**两次 seek** ⇒ 位置错位（规格）。</para>
///
/// <para>🔴 **取流不得用 `PlaybackUrlResolver`**：其 `Auto` 分支在 `AllowDirectStream` 默认 true 时
/// **永不落 Transcode** ⇒ 静默丢转码分支（`PlaybackUrlResolver.cs:205-208`）。权威判据是
/// `EmbyPlaybackSession.NeedsTranscode` ⇒ 播放必须走 **`EmbyPlaybackSession.BuildRequestAsync`**
/// → `PlaybackRequest` → <see cref="KernelArgumentBuilder.FromPlayback"/>。</para>
/// </summary>
public static class KernelLauncher
{
    /// <summary>覆盖内核 exe 路径（最高优先级）。</summary>
    public const string ExeEnvVar = "SHELL_KERNEL_EXE";

    /// <summary>覆盖 libmpv 路径（最高优先级）。</summary>
    public const string LibMpvEnvVar = "SHELL_KERNEL_LIBMPV";

    /// <summary>
    /// `SHELL_KERNEL_LEGACY=off` ⇒ **第三级 legacy 视为不可达**（默认 on）。
    /// 为什么需要它（t126）：legacy 绝对路径在本机**真实存在** ⇒ 若 player\ 落空就静默起原版内核，
    /// 反控格会被读成"回落正常" ⇒ 必须有个开关能让第三级真的落空，否则"三条全不满足"这条反控无法成立。
    /// </summary>
    public const string LegacySwitchEnvVar = "SHELL_KERNEL_LEGACY";

    /// <summary>产物自带目录名（`&lt;BaseDir&gt;\player\`，与 dist 发布形态一致）。</summary>
    private const string ProductPlayerDir = "player";

    private const string KernelExeName = "AIPlayer.MpvHost.exe";
    private const string LibMpvName = "libmpv-2.dll";

    /// <summary>
    /// t250：内核**托管主体**的文件名 —— exe 只是同名的宿主外壳，**fork 与原版的差别在它旁边的这个 dll**
    /// （原版 = 979,456 B / `8D73526C2C07`；fork = 1,026,560 B 级）。启动读数里带上它的 bytes/sha12，
    /// "当刻起的是 fork 还是原版"才能**一眼判定**，不用事后外扫目录。
    /// </summary>
    private const string KernelDllName = "AIPlayer.MpvHost.dll";

    private const string DefaultExe = @"E:\AI Player\data\player\AIPlayer.MpvHost.exe";
    private const string DefaultLibMpv = @"E:\AI Player\data\player\libmpv-2.dll";

    /// <summary>内核进程名（`GetProcessesByName` 用；与 exe 同源，避免两处写死不一致）。</summary>
    public const string KernelProcessName = "AIPlayer.MpvHost";

    /// <summary>
    /// **本进程自己拉起的最后一个内核进程**（H6 纪律：只停自己 <see cref="Process.Start(ProcessStartInfo)"/>
    /// 返回的那个对象，**绝不按进程名批量结束** —— 按名结束会连带杀掉队友/用户正在跑的内核）。
    /// 未拉起过时为 <c>null</c>。
    /// </summary>
    private static Process _lastLaunched;

    /// <summary>
    /// 内核 exe 的解析结果（t126）：`&lt;BaseDir&gt;\player\` **产物自带件优先于 legacy 绝对路径** ——
    /// 这样 dist 发布形态（自带 player\ 的 fork 内核）才真的跑到 fork，而不是永远起 `data\player\` 的原版。
    /// </summary>
    public readonly struct ResolvedImage
    {
        public ResolvedImage(string path, string source, bool forceMissing = false)
        {
            Path = path ?? string.Empty;
            Source = source ?? "none";
            // forceMissing：`SHELL_KERNEL_LEGACY=off` 时 legacy 段**逻辑上不可达** —— 文件在盘上也不算数，
            // 否则反控会"以为落空了、实际仍起原版"（本轮实测踩到：source=none 但 exists=True 且真的启动了）。
            Exists = !forceMissing && Path.Length > 0 && File.Exists(Path);
        }

        /// <summary>解析出的绝对路径（不存在时也给出"本该在哪"，便于报错定位）。</summary>
        public string Path { get; }

        /// <summary>`env` / `player` / `legacy` / `none`（`none` = legacy 被开关置为不可达）。</summary>
        public string Source { get; }

        public bool Exists { get; }
    }

    /// <summary>三段解析（env &gt; `&lt;BaseDir&gt;\player\` &gt; legacy 绝对路径）；`SHELL_KERNEL_LEGACY=off` 时第三段不可达。</summary>
    public static ResolvedImage ResolveKernelExe() => Resolve(ExeEnvVar, KernelExeName, DefaultExe);

    /// <summary>libmpv 同口径解析。</summary>
    public static ResolvedImage ResolveLibMpv() => Resolve(LibMpvEnvVar, LibMpvName, DefaultLibMpv);

    /// <summary>legacy（第三段）当刻是否可达（默认 true；`SHELL_KERNEL_LEGACY=off` ⇒ false）。</summary>
    public static bool LegacyEnabled
        => !string.Equals(Environment.GetEnvironmentVariable(LegacySwitchEnvVar), "off", StringComparison.OrdinalIgnoreCase);

    private static ResolvedImage Resolve(string envVar, string fileName, string legacyPath)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return new ResolvedImage(fromEnv.Trim(), "env");
        }

        var product = Path.Combine(AppContext.BaseDirectory ?? string.Empty, ProductPlayerDir, fileName);
        if (File.Exists(product))
        {
            return new ResolvedImage(product, "player");
        }

        return LegacyEnabled ? new ResolvedImage(legacyPath, "legacy") : new ResolvedImage(legacyPath, "none", forceMissing: true);
    }

    /// <summary>兼容既有调用点：只要路径（exe）。</summary>
    public static string KernelExePath => ResolveKernelExe().Path;

    /// <summary>兼容既有调用点：只要路径（libmpv）。</summary>
    public static string LibMpvPath => ResolveLibMpv().Path;

    /// <summary>
    /// 落一行**逐字段**解析读数（t126 硬要求；t250 追加三组字段）：`KERNEL-IMAGE resolved=… bytes=… sha12=… source=… exists=… legacyEnabled=…`
    /// ⇒ 只读日志就能判定走了哪一级、起的是哪个镜像（"起原版"这类回归必须当场可见，不靠事后外扫）。
    /// <para>t250 追加：① `bytes=`（文件字节数，读不到 ⇒ `&lt;none&gt;`）；② 解析**落空**时追加 `reason=`（取值可枚举，
    /// 见 <see cref="ReasonFor"/>）；③ `withSiblingDll=true` 时追加 `dllBytes=` / `dllSha12=`（exe 同目录的
    /// <see cref="KernelDllName"/>，缺失 ⇒ `dllBytes=absent` / `dllSha12=dllAbsent`）—— fork 与原版的判别字段就在这里。</para>
    /// </summary>
    public static void LogResolvedImage(Action<string> log, string prefix, ResolvedImage image, bool withSiblingDll = false)
    {
        var line = prefix + " resolved=" + image.Path
            + " bytes=" + BytesOf(image.Path)
            + " sha12=" + Sha12Of(image.Path)
            + " source=" + image.Source
            + " exists=" + image.Exists
            + " legacyEnabled=" + LegacyEnabled;

        if (!image.Exists)
        {
            // 落空才给 reason：成立时不打，避免每行多一个恒真字段（判据纪律：恒真字段不得单独作判据）。
            line += " reason=" + ReasonFor(image);
        }

        if (withSiblingDll)
        {
            var dll = SiblingDllOf(image.Path);
            line += " dllBytes=" + (dll == null ? "absent" : BytesOf(dll))
                + " dllSha12=" + (dll == null ? "dllAbsent" : Sha12Of(dll));
        }

        log?.Invoke(line);
    }

    /// <summary>
    /// t250：解析**落空**时的可枚举理由（只在这四个值里取，便于机器判定；不要新造自由文本）：
    /// <list type="bullet">
    /// <item><c>env-path-absent</c> —— `SHELL_KERNEL_EXE` / `SHELL_KERNEL_LIBMPV` 指定了路径，但盘上无该文件；</item>
    /// <item><c>player-path-absent</c> —— 第②级 `&lt;BaseDir&gt;\player\` 下无该文件；</item>
    /// <item><c>legacy-path-absent</c> —— 第③级 legacy 绝对路径下无该文件；</item>
    /// <item><c>legacy-disabled</c> —— `SHELL_KERNEL_LEGACY=off` 把第③级置为**逻辑不可达**（文件可能仍在盘上）。</item>
    /// </list>
    /// </summary>
    private static string ReasonFor(ResolvedImage image)
    {
        if (string.Equals(image.Source, "none", StringComparison.Ordinal))
        {
            return "legacy-disabled";
        }

        if (string.Equals(image.Source, "env", StringComparison.Ordinal))
        {
            return "env-path-absent";
        }

        if (string.Equals(image.Source, "player", StringComparison.Ordinal))
        {
            return "player-path-absent";
        }

        return "legacy-path-absent";
    }

    /// <summary>t250：文件字节数（不存在 ⇒ `&lt;none&gt;`；不可读 ⇒ `&lt;err:类型&gt;`）；不抛。</summary>
    public static string BytesOf(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return "<none>";
        }

        try
        {
            return new FileInfo(path).Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "<err:" + ex.GetType().Name + ">";
        }
    }

    /// <summary>t250：exe 同目录的托管主体 dll（<see cref="KernelDllName"/>）；不存在或目录取不到 ⇒ `null`（调用方打 `dllAbsent`）。</summary>
    private static string SiblingDllOf(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }

        var dll = Path.Combine(dir, KernelDllName);
        return File.Exists(dll) ? dll : null;
    }

    private static readonly Dictionary<string, string> Sha12Cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>文件 sha12（不存在 ⇒ `&lt;none&gt;`）；按路径缓存（libmpv 有 117 MB，不能每次起播都重算）。</summary>
    public static string Sha12Of(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return "<none>";
        }

        lock (Sha12Cache)
        {
            if (Sha12Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hex = Convert.ToHexString(sha.ComputeHash(stream)).Substring(0, 12);
            lock (Sha12Cache)
            {
                Sha12Cache[path] = hex;
            }

            return hex;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "<err:" + ex.GetType().Name + ">";
        }
    }

    /// <summary>参数组装（**唯一写点**，委托给 <see cref="KernelArgumentBuilder"/>）。</summary>
    public static string BuildArguments(KernelLaunchRequest request) => KernelArgumentBuilder.BuildArguments(request);

    /// <summary>
    /// 本进程自己拉起的最后一个内核进程（**唯一可停对象**）；未拉起过时为 <c>null</c>。
    /// </summary>
    public static Process LastLaunchedKernel => _lastLaunched;

    public static Process Launch(KernelLaunchRequest request, Action<string> log)
    {
        // t126：**先落两行解析读数**（逐字段）⇒ 起的是产物自带件还是 legacy 原版，当场可见（不靠事后外扫）。
        var exeImage = ResolveKernelExe();
        var libMpvImage = ResolveLibMpv();
        LogResolvedImage(log, "KERNEL-IMAGE", exeImage, withSiblingDll: true);
        LogResolvedImage(log, "KERNEL-LIBMPV", libMpvImage);

        var exe = exeImage.Path;
        if (!exeImage.Exists)
        {
            // 三条全不满足（或 legacy 被 `SHELL_KERNEL_LEGACY=off` 置为不可达）⇒ 明确报出"本该在哪"，且**不启动任何进程**
            log?.Invoke("KERNEL-LAUNCH FAIL exe-missing=" + exe + " source=" + exeImage.Source);
            return null;
        }

        // ① `LastLocalVideoPath` 语义闸门（t27-F-A ④）：把"要押后恢复的本地路径"先收好。
        //    必须在**内核起来之前**取，因为内核 `HandleLaunchAsync`（AppViewModel.cs:296）会立刻覆盖该键。
        LocalVideoPathSemantic.GuardBeforeLaunch();

        var arguments = BuildArguments(request);
        // t99：`args` 里有 `--http-header=<base64>`（内含 X-Emby-Token）⇒ **落日志前先打码**
        // （旧实现直接把原样 args 写进 aiplayer.log ⇒ 实测 24 行裸 base64）。
        log?.Invoke("KERNEL-LAUNCH exe=" + exe + " args=" + Infrastructure.SecretMasker.Mask(arguments));

        try
        {
            var startInfo = new ProcessStartInfo(exe)
            {
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? ".",
                UseShellExecute = false,
            };

            var process = Process.Start(startInfo);
            log?.Invoke("KERNEL-LAUNCH OK pid=" + (process == null ? -1 : process.Id));
            _lastLaunched = process;

            // t221（E-P3）：把"本次起播的上下文"发布给播放中控制会话（轨道清单用于托盘菜单标签；
            // pid 用于 `退出播放` 的**只停自己**判据）。三个起播调用点（Library/Detail/Aggregate）
            // 都走本方法 ⇒ 这里是**唯一**发布点，不需要在各页面各写一份。
            try
            {
                Features.Player.PlaybackControlSession.Current.OnKernelLaunched(request, process);
            }
            catch (Exception ex)
            {
                log?.Invoke("PLAYER-SESSION publish FAIL " + ex.GetType().Name + ": " + ex.Message);
            }

            return process;
        }
        catch (Exception ex)
        {
            log?.Invoke("KERNEL-LAUNCH FAIL " + ex.GetType().FullName + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>当前是否已有内核在跑（运维/自检/退出打码的并发闸门用；按进程名匹配，避免误杀）。</summary>
    public static int RunningKernelCount() => CountRunningKernels();

    /// <summary>按进程名统计内核进程数；失败返回 <c>-1</c>（**不是 0** —— 0 会被误读成"确无内核"）。</summary>
    public static int CountRunningKernels()
    {
        try
        {
            return Process.GetProcessesByName(KernelProcessName).Length;
        }
        catch (Exception)
        {
            // 受限环境可能拒绝枚举进程（权限/WMI 关机）；**必须与"确无内核"区分** ⇒ 返回 -1 而不是 0
            return -1;
        }
    }
}

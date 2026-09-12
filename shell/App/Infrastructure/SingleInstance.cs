// 单实例闸门：第二个进程**提示并退出**，而不是再起一个外壳。
//
// 为什么要它（两条都不只是"体验问题"）：
//   · 同一份用户数据目录被两个进程同时写（`settings.json` / `servers.json` / `swr` 快照）会出现互相覆盖；
//   · 回调端口是**落盘固定**的：第二个实例发现该端口"正被第一个实例监听" ⇒ 会判它不可用、**另选一个并改写
//     `callback-port.json`** ⇒ 把第一个实例的固定端口顶掉（这个文件是跨启动复用的唯一依据）。
//     ⇒ 闸门必须**早于**任何端口解析/取数发生（放在 `Application.Start` 之前），被拒绝的那一支**不得触碰任何落盘文件**。
//
// 用法（调用方在 `Program.Main` 里，早于 `Application.Start`）：
//   if (!SingleInstance.TryAcquire(out var existing))
//   { Program.Log("SINGLE-INSTANCE-REJECTED existing=" + (existing ?? "-")); Environment.ExitCode = 3; return; }
//   ↑ 用 `Environment.ExitCode = 3; return;`，**不要写 `return 3;`** —— `Program.Main` 是 `private static void`
//     （captain 裁定 (b)：改 `int Main` 要逐条核所有出口，含 `Application.Start` 之后的打码路径）。

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AIPlayer.Shell.Infrastructure;

/// <summary>
/// **同一份数据根**上只允许一个外壳实例（t104：粒度从"整台机一个"改成"每个数据根一个"）。
///
/// <para>为什么粒度必须是**数据根**：闸门要保护的是"同一份数据目录不被两个进程同时写"
/// （`settings.json` / `servers.json` / `swr` 快照 / 落盘的回调端口）。原实现用**固定互斥体名**
/// ⇒ 重定向 `LOCALAPPDATA`/`APPDATA` 或 `AIPLAYER_APPDATA_ROOT` 都撞同一个名字：
/// 测试/沙箱实例被用户那份 `dist\AIPlayer\` 挡住（实测 `t93` 取证退 3），而按纪律**不能**去杀别人的实例。</para>
/// </summary>
public static class SingleInstance
{
    /// <summary>互斥体名前缀（`Local\` 前缀 = 每个登录会话一个；换会话各占一个，符合"看自己的窗口"的直觉）。</summary>
    private const string MutexNamePrefix = @"Local\AIPlayer.Shell.SingleInstance.v1";

    /// <summary>数据根覆盖环境变量（**与 `Services.Infra.AppDataDir.RootOverrideEnvVar` 同名同义**）。</summary>
    public const string RootOverrideEnvVar = "AIPLAYER_APPDATA_ROOT";

    private static readonly Lazy<string> LazyDataRoot = new Lazy<string>(ResolveDataRootReadOnly);

    /// <summary>本进程判重所依据的**数据根**（只读解析，**绝不落盘**）。</summary>
    public static string DataRoot => LazyDataRoot.Value;

    private static readonly Lazy<string> LazyFingerprint = new Lazy<string>(() =>
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        // 大写规范化：Windows 路径大小写不敏感 ⇒ 否则 `C:\Users` 与 `c:\users` 会得到两个指纹（闸门失效）
        var hex = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(DataRoot.ToUpperInvariant())));
        return hex.Substring(0, 8);
    });

    /// <summary>数据根指纹 = `sha256_8(规范化根路径)`（小写 hex，便于人读）。</summary>
    public static string DataRootFingerprint => LazyFingerprint.Value.ToLowerInvariant();

    private static readonly Lazy<string> LazyMutexName = new Lazy<string>(
        () => MutexNamePrefix + "." + DataRootFingerprint);

    /// <summary>
    /// **实际使用的互斥体名** = `Local\AIPlayer.Shell.SingleInstance.v1.&lt;sha8(数据根)&gt;`（可观测：取得/被拒各落日志）。
    /// 同一个根 ⇒ 同名 ⇒ 第二个实例仍被拒（原保护一条不减）；不同根 ⇒ 不同名 ⇒ 互不干扰。
    /// </summary>
    public static string MutexName => LazyMutexName.Value;

    /// <summary>
    /// 只读解析数据根（闸门在 `Application.Start` 之前运行）。
    /// <para>**t246：根推导同源到 `Services.Infra.AppDataDir.Instance.Root`** —— 与数据面**一份根解析**，
    /// 不再在本类里自己拼第三段（旧实现直接用**系统级 LOCALAPPDATA 查询 API**（`SpecialFolder` 形态）自己拼，
    /// 与 `AppDataDir` 的"环境变量优先"口径存在<b>可观测差异</b>）。</para>
    /// <para>[!] **代价（实测并写入证据）**：`AppDataDir.Resolve()` 在①覆盖根分支会做**可写探针**
    /// （`Directory.CreateDirectory(value)` + `.write-probe-&lt;pid&gt;` 写后即删），在③默认根分支会
    /// `Directory.CreateDirectory(root)`（已存在则无实际写入）。⇒ 闸门比旧实现**多一次"根自证"的可写动作**；
    /// 它**不触碰受保护状态文件**（`settings.json` / `servers.json` / `credentials.bin` / `callback-port.json`），
    /// 与 ⑤ 的现行口径（"不得触碰受保护状态文件；自诊断行除外"）相容。旧的"偏宽一格"差异**随之消失**：
    /// 覆盖根非法时，闸门与数据面一起回落到默认根。</para>
    /// </summary>
    private static string ResolveDataRootReadOnly()
    {
        var root = AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root;
        return NormalizeForFingerprint(root);
    }

    /// <summary>规范化（去尾部分隔符；`GetFullPath` 失败时原样返回 —— 指纹只要求**同输入同输出**）。</summary>
    private static string NormalizeForFingerprint(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.TrimEnd('\\', '/');
        }
    }

    private static Mutex _mutex;

    /// <summary>闸门是否已被本进程取得（只读；自检/日志用）。</summary>
    public static bool Acquired { get; private set; }

    /// <summary>
    /// 自动化取证的**显式旁路**：`SHELL_SINGLEINSTANCE_ALLOW_MULTI=1` ⇒ 跳过闸门（放行并留日志行）。
    /// <para>为什么需要：用户/他人当刻开着外壳（例如 `dist\AIPlayer\` 那份发布版）时，
    /// 取证用的第二个实例会拿不到互斥体而 **exit 3**，导致"想取证却起不了窗"——
    /// 而按纪律**不能**去杀别人的实例（H6 / 事实 221）。</para>
    /// <para>它是**显式、可见、默认关闭**的：不设该环境变量时行为逐字不变；设了也一定落一行
    /// `SINGLE-INSTANCE-BYPASS` 日志（不允许静默放行）。</para>
    /// </summary>
    public const string AllowMultiEnvVar = "SHELL_SINGLEINSTANCE_ALLOW_MULTI";

    /// <summary>
    /// 尝试取得闸门。
    /// 返回 <c>true</c> = 本进程是第一个实例（互斥体由本进程持有，进程退出时释放）；
    /// 返回 <c>false</c> = 已有实例在跑，调用方应**提示并退出**，且**不得**再写任何落盘文件。
    /// </summary>
    /// <param name="existingInfo">**非断言**的现场描述（同名进程**数量** + 本根 / 互斥体名）；**不含任何具体 pid** —— 互斥体名不携带持有者，点名必错（t229）。取不到时给不出信息也不抛。</param>
    public static bool TryAcquire(out string existingInfo)
    {
        existingInfo = string.Empty;
        if (Acquired)
        {
            return true;
        }

        // 取证旁路（默认关闭）：设了 `SHELL_SINGLEINSTANCE_ALLOW_MULTI=1` ⇒ 放行，但**必须留日志**
        // （调用方拿到 true 就继续起窗；"为什么会有两个实例"这件事在日志里有据可查）。
        if (string.Equals(Environment.GetEnvironmentVariable(AllowMultiEnvVar), "1", StringComparison.Ordinal))
        {
            Acquired = true;
            Program.Log("SINGLE-INSTANCE-BYPASS " + AllowMultiEnvVar + "=1 ⇒ 跳过单实例闸门（取证用；同一数据目录可能出现两个实例）"
                + " existing=" + DescribeSameNameCandidates()
                + " mutex=" + MutexName + " root=" + DataRoot);
            return true;
        }

        try
        {
            _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);

            // 可观测（t104 验收 4）：**起步就落一行**说明本进程用的是哪个互斥体名 / 哪个根 / 哪个指纹 ——
            // 否则"两个实例互不干扰"与"闸门根本没生效"在日志上长得一模一样（判据必须能证伪）。
            Program.Log("SINGLE-INSTANCE acquire mutex=" + MutexName + " root=" + DataRoot
                + " fingerprint=" + DataRootFingerprint + " createdNew=" + createdNew);

            if (createdNew)
            {
                Acquired = true;
                return true;
            }

            // 互斥体已存在：可能是"别的实例正持着"，也可能是"上个实例刚崩、句柄还没回收"。
            // 这里**只做一次**极短等待，避免把"残留句柄"误判成"有实例在跑"。
            var taken = false;
            try
            {
                taken = _mutex.WaitOne(TimeSpan.FromMilliseconds(300));
            }
            catch (AbandonedMutexException)
            {
                // 前一个持有者异常退出 ⇒ 内核把所有权交给我们 ⇒ 视为"拿到"
                taken = true;
            }

            if (taken)
            {
                Acquired = true;
                Program.Log("SINGLE-INSTANCE acquire-after-wait mutex=" + MutexName + " root=" + DataRoot
                    + "（同名互斥体存在但 300ms 内可取 ⇒ 前持有者已退出/句柄残留，视为取得）");
                return true;
            }

            // 被拒时把"已有实例 + 所用根 + 所用互斥体名"一起交出去：调用方的
            // `SINGLE-INSTANCE-REJECTED existing=<这串>` 因此自带根 ⇒ 排查"是同一个根真撞了、还是闸门粒度不对"
            // 不需要再猜（t104 验收 4）。
            existingInfo = DescribeSameNameCandidates() + " mutex=" + MutexName + " root=" + DataRoot;
            return false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            // 拿不到互斥体（受限环境/名字冲突）不该把外壳挡死：**放行但留痕**，由调用方决定是否提示。
            existingInfo = "singleton-check-unavailable: " + ex.GetType().Name;
            return true;
        }
    }

    /// <summary>
    /// **同名进程的数量**（t229：**不点名任何 pid**）。失败**不抛**，返回可读串。
    ///
    /// [!] 为什么不再点名（2026-09-12 真机实测 + Captain 裁定）：原实现取**第一个**同名进程，
    /// 把它的 `pid/path/started` 当作"已有实例"报出去。但**互斥体名不携带持有者信息**，而**同名进程
    /// 可能属于别的数据根** ⇒ 三连复现的"不可能组合"：互斥体 `…SingleInstance.v1.34139158` 绑定根
    /// `…\t61b-port`，报出的却是 `pid=13896`（`dist\AIPlayer`、**默认根**、18:28:15）——
    /// 那个进程**结构上不可能**持有该根指纹下的互斥体；真持锁者是**同根** `createdNew=True` 的那一支。
    /// ⇒ 后果是用户照这条去关进程会**关错**（多实例/多根场景下系统性不可信）。
    /// 判据：本方法**不得**输出任何具体 pid。要"按真正持锁进程报"需枚举句柄（本仓不做）⇒ 只报候选数 + 本根。
    /// </summary>
    private static string DescribeSameNameCandidates()
    {
        try
        {
            var self = Environment.ProcessPath ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(self);
            if (string.IsNullOrEmpty(name)) { return "same-name-instances=unknown（取不到本进程名）"; }

            var count = 0;
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process) { count++; }
            }

            return count == 0
                ? "same-name-instances=0（互斥体被占但进程枚举为空）"
                : "same-name-instances=" + count
                  + "（持锁者 pid 不可判定：互斥体名不携带持有者，且同名进程可能属于其它数据根）";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return "same-name-instances-unavailable: " + ex.GetType().Name;
        }
    }

    /// <summary>
    /// 给用户看的一行提示（第二个实例被拒绝时弹一个原生消息框）。
    /// 为什么用原生 `MessageBoxW`：此刻 WinUI 还没启动（没有窗口/XamlRoot），`ContentDialog` 用不了；
    /// 纯控制台不可见（外壳是 GUI 子系统）⇒ 这是唯一"用户真的看得见"的做法。
    ///
    /// [!] `SHELL_SINGLEINSTANCE_SILENT=1` ⇒ **不弹框、只留日志**：自动化取证里模态框会把进程挂住
    /// （第二实例不退出、锁着自己目录里的 dll），所以必须有一个"安静模式"开关，**而不是让调用方各写一套**。
    /// **失败不抛**：提示失败也不能把退出流程挡住（但仍写日志留痕）。
    /// </summary>
    public static void NotifyAlreadyRunning(string existingInfo, Action<string> log = null)
    {
        log?.Invoke("SINGLE-INSTANCE notify: " + existingInfo);

        var silent = string.Equals(
            Environment.GetEnvironmentVariable("SHELL_SINGLEINSTANCE_SILENT"), "1", StringComparison.Ordinal);
        if (silent)
        {
            log?.Invoke("SINGLE-INSTANCE notify-suppressed（SHELL_SINGLEINSTANCE_SILENT=1）");
            return;
        }

        // 界面话（用户语言，**不出现** pid / 互斥体 / 根 / 路径这类字段名）：技术细节留在上面那行日志里。
        // 原实现把 `existing=pid=… path=… mutex=…` 直接拼进弹框 ⇒ 既暴露内部字段，又可能点名错实例（t229）。
        var text = "AI Player 已经在运行。\r\n\r\n本窗口不会再打开第二个实例。\r\n如果要再开一个，请先关闭正在运行的那个窗口。";
        try
        {
            _ = MessageBoxW(IntPtr.Zero, text, "AI Player", 0x00000000 /* MB_OK */);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            log?.Invoke("SINGLE-INSTANCE notify-failed " + ex.GetType().Name + "（提示失败不影响退出）");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}

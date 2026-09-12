// t31（U-E 收藏 + 聚合视界）的**诊断日志写盘点**。
//
// 为什么单独开一个文件而不是复用 `shell-startup.log`：
//   ① 它要**无 BOM UTF-8** 写到一个稳定路径（`shell/Tests/evidence/`），便于 verifier 直接读原始输出；
//   ② 本屏的取数面是**多台真实服务器**（本机 accounts.json 16 台），自检输出会比启动日志长得多，
//      混进启动日志会淹没其它卡的取证；
//   ③ 它与 t27 的 `t27-callback-reply.log` 同族（自检写机器证据），路径规则一致。
//
// ⚠ 纪律：**只写形态与计数，不写 URL**（图片/取流地址都带 `api_key`）。
//    凡是必须落进证据的凭据形态，一律先过 `Program.MaskSecrets`。

using System;
using System.IO;
using System.Text;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>把聚合自检的输出同时写进外壳日志与 <c>shell/Tests/evidence/</c> 下的证据文件。</summary>
public static class AggregateDiagnostics
{
    /// <summary>证据文件名（t31）。
    ///
    /// [!] **扩展名必须是 `.txt` / `.md`，永远不用 `.log`**：仓库 `.gitignore:65` 有 `*.log` 规则
    /// ⇒ 用 `.log` 写的证据**根本没进仓库**（本队实测踩过两次：`s1-build-zero.log`、以及本卡
    /// `t27-callback-reply.log`）。写盘前一律用 `git check-ignore` 自证。
    /// </summary>
    public const string DefaultEvidenceFileName = "t31-aggregate-selftest.txt";

    /// <summary>
    /// 证据文件名（报告）的**显式覆盖**：`SHELL_EVIDENCE_NAME`（如 `t87-favorites-limit.txt`）。
    /// 为什么要它：新卡的读数必须落**新文件名** —— 覆盖旧文件名会与已入库的读数身份（sha12/行数）对不上，
    /// 而"同一次运行不得有两个文件名"又不允许事后改名拷贝。只保留安全字符，并强制 `.txt`/`.md`。
    /// </summary>
    public const string EvidenceNameEnvVar = "SHELL_EVIDENCE_NAME";

    private static string _evidenceFileName = DefaultEvidenceFileName;

    /// <summary>落点只报一次（见 <see cref="AIPlayer.Shell.Features.Evidence.EvidenceDirReporter.LogOnce"/>）。</summary>
    private static int _dirLogged;

    /// <summary>当前证据文件名。缺省 = <see cref="DefaultEvidenceFileName"/>。
    ///
    /// [!] **为什么要有它**：本卡两屏（聚合视界 / 收藏屏）各自跑自检，两者**各自一次性整体覆盖写**。
    /// 早期版本两屏共用一个文件名 ⇒ 后跑的那屏把先跑的**整份读数覆盖掉**（实测：先聚合后收藏，
    /// 聚合的 3 个过滤器读数全没了）。证据文件按屏分开，才有"两屏读数同时在场"这回事。
    /// </summary>
    public static string EvidenceFileName => _evidenceFileName;

    /// <summary>把证据文件名切成 `t31-{screen}-selftest.txt`（screen 只保留 `[a-z0-9-]`，防路径注入）。</summary>
    public static void SetEvidenceSuffix(string screen)
    {
        var over = Environment.GetEnvironmentVariable(EvidenceNameEnvVar);
        if (!string.IsNullOrWhiteSpace(over))
        {
            _evidenceFileName = SafeEvidenceName(over.Trim());
            return;
        }

        var sb = new StringBuilder("t31-");
        foreach (var ch in screen ?? string.Empty)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-')
            {
                sb.Append(ch);
            }
        }

        sb.Append("-selftest.txt");
        _evidenceFileName = sb.ToString();
    }

    /// <summary>`SHELL_EVIDENCE_NAME` 的安全化：只留 `[A-Za-z0-9._-]`，并强制 `.txt`/`.md`（`.log` 会被 `.gitignore` 吞掉 ⇒ H2a 的坑）。</summary>
    private static string SafeEvidenceName(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name ?? string.Empty)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')
                || ch == '-' || ch == '_' || ch == '.')
            {
                sb.Append(ch);
            }
        }

        var clean = sb.ToString();
        if (!clean.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && !clean.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            clean += ".txt";
        }

        return clean.Length <= 4 ? DefaultEvidenceFileName : clean;
    }

    /// <summary>`bin\Debug\<tfm>\<rid>\` → 上溯 5 级到 `shell\`。
    ///
    /// [!] `SHELL_EVIDENCE_DIR`（绝对路径）优先：**隔离输出**（`-p:OutputPath=E:\ui2-bin\`）时
    /// `AppContext.BaseDirectory` 不在 `shell\App\bin\…` 里，上溯 5 级会落到 `E:\` 去 —— 显式指定才能把证据写回仓内。
    /// </summary>
    public static string EvidenceDir
    {
        get
        {
            var over = Environment.GetEnvironmentVariable("SHELL_EVIDENCE_DIR");
            return string.IsNullOrWhiteSpace(over)
                ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "evidence"))
                : Path.GetFullPath(over.Trim());
        }
    }

    /// <summary>自检**报告**文件（整体覆盖写）。</summary>
    public static string EvidencePath => Path.Combine(EvidenceDir, EvidenceFileName);

    /// <summary>
    /// 卡片级日志文件（**追加写**）。
    ///
    /// [!] 为什么要跟报告分家：两者写盘模式不同（报告 = 整体覆盖；卡片日志 = 逐行追加）。
    /// 早期两者共用一个文件 ⇒ 一个长驻的聚合视界实例会把 `poster-image-skip-budget` 这类行
    /// **一路 append 进报告**（实测：报告从 83 行涨到 217 行、18,901 B，`H5`/`H2b` 的"身份"因此对不上）。
    /// </summary>
    public static string CardLogPath => Path.Combine(EvidenceDir, CardLogFileName);

    /// <summary>
    /// 卡片日志文件名（与报告**必须**不同名，否则"覆盖写"的报告会被"追加写"的卡片日志灌长）。
    /// 默认命名保持历史上的 `t31-{screen}-cardlog.txt` 逐字不变；自定义报告名则派生 `{名}-cardlog.txt`。
    /// </summary>
    private static string CardLogFileName
    {
        get
        {
            const string suffix = "-selftest.txt";
            var name = EvidenceFileName;
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - suffix.Length) + "-cardlog.txt"
                : Path.GetFileNameWithoutExtension(name) + "-cardlog" + Path.GetExtension(name);
        }
    }

    // t281：本屏原先自己实现的 `LogEvidenceDirOnce()` 已收敛到唯一实现点
    // `Evidence.EvidenceDirReporter.LogOnce`（三处逐句同源的副本 ⇒ 现只剩一处）；
    // 本屏只提供**自己的**前缀 `EVIDENCE-DIR` 与落点参数，守卫仍是本类的 `_dirLogged`（每屏一次）。

    /// <summary>写一行（同时进外壳日志 + 证据文件）；**不抛异常**。</summary>
    public static void Write(string line)
    {
        line = Sanitize(line);
        Program.Log("AGG " + line);

        // t58：人物/合集屏**复用聚合的卡片与渲染器**，但证据必须落在各自屏的目录 ⇒ 允许临时改道。
        // 不设改道时行为与从前逐字一致（写 `t31-*.txt`）。
        var redirect = Redirect;
        if (redirect != null)
        {
            redirect(line);
            return;
        }

        AppendToFile(line);
    }

    /// <summary>
    /// 证据行改道（t58 的人物/合集屏用）。**只影响写盘落点**，不影响 `Program.Log` 与脱敏。
    /// 页面在进入时设置；同一时刻只有一个屏在前台，故不做引用计数。
    /// </summary>
    public static Action<string> Redirect { get; set; }

    private static int _imageFailures;

    /// <summary>
    /// 封面失败**只记前几条**：本机实测一台服务器上 9 张封面取不到 ⇒ 逐张记会把日志刷满，
    /// 而"多张失败"这件事的**信息量在第一条之后就不再增长**（形态相同、只是不同 item）。
    /// 计数仍会出现在最后一条里，便于复算。
    /// </summary>
    public static void WriteImageFailure(string line)
    {
        _imageFailures++;
        if (_imageFailures <= 3)
        {
            Write(line + " [#" + _imageFailures + "]");
        }
        else if (_imageFailures == 4)
        {
            Write("poster-image-fail 后续同类失败已折叠（cap=3）");
        }
    }

    /// <summary>统计：本轮封面失败总数（自检/收尾用）。</summary>
    public static int ImageFailureCount => _imageFailures;

    private static int _noUrlCount;

    /// <summary>
    /// "没有封面可用"的条目：**只记前 5 条**（一条服务器上可能上百条都没图 ——
    /// 逐条打印会把日志刷爆，而形态相同的信息量在第一条之后不再增长；总数仍会给出）。
    /// </summary>
    public static void WriteNoImageUrl(string title)
    {
        _noUrlCount++;
        if (_noUrlCount <= 5)
        {
            Write("poster-no-image-url title=" + title);
        }
        else if (_noUrlCount == 6)
        {
            Write("poster-no-image-url 后续同类已折叠（cap=5）");
        }
    }

    /// <summary>本轮"无封面可用"的条目总数（可复算）。</summary>
    public static int NoImageUrlCount => _noUrlCount;

    /// <summary>
    /// [!] **日志脱敏**：本文件是"证据"通道，极易被把**整条异常消息**贴进来 ——
    /// 而服务层抛的消息里带**完整 URL**（实测：`GET http://127.0.0.1:9/emby/Users//Items?…`）。
    /// 服务层当前不校验凭据存在性，`UserId` 可为空串 ⇒ 会出现 `Users//Items` 这种畸形路径被原样打印。
    /// 处置：先过唯一打码咽喉 <see cref="Program.MaskSecrets"/>（t99 起它已转发到
    /// <see cref="AIPlayer.Shell.Infrastructure.SecretMasker"/>，覆盖 `api_key=` / `--http-header=` / 认证头），
    /// 再把**查询串整体截掉**（查询串里才可能有凭据；路径保留便于定位）。
    /// </summary>
    public static string Sanitize(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        line = Program.MaskSecrets(line);

        var q = line.IndexOf('?');
        if (q < 0)
        {
            return line;
        }

        var end = line.IndexOf(' ', q);
        return end < 0
            ? line.Substring(0, q) + "?<query-elided>"
            : line.Substring(0, q) + "?<query-elided>" + line.Substring(end);
    }

    /// <summary>把整段自检输出一次性落盘（覆盖写；UTF-8 无 BOM）。</summary>
    public static void WriteEvidence(string content)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "EVIDENCE-DIR", EvidenceDir, EvidenceFileName, CardLogFileName);
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(EvidencePath, content, new UTF8Encoding(false));
            Program.Log("AGG evidence written " + EvidencePath + " bytes=" + new FileInfo(EvidencePath).Length);
        }
        catch (Exception ex)
        {
            Program.Log("AGG evidence FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void AppendToFile(string line)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "EVIDENCE-DIR", EvidenceDir, EvidenceFileName, CardLogFileName);
            Directory.CreateDirectory(EvidenceDir);
            // 卡片级日志走 **cardlog** 文件（追加），与"报告"（整体覆盖）分家 —— 见 CardLogPath 的注释。
            File.AppendAllText(CardLogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            // 证据落盘失败**不得影响界面**（本方法在渲染路径上被逐行调用）。
            // 但**不许静默**：证据文件写不进去本身就是需要诊断的状况 ⇒ 至少进外壳日志。
            // （`Program.Log` 自己也有内层 typed catch，不会因此递归。）
            Program.Log("AGG evidence-append FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

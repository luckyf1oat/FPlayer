// t58（U-L 分片一）人物屏的证据落点。
//
// 为什么独立于 t31 的 `AggregateDiagnostics`：本卡验收要求证据落在**本屏自己的目录**
// （`Features/Person/evidence/*.txt`）。卡片级日志（封面失败/无图）走 `AggregateDiagnostics.Redirect`
// 改道到这里（渲染器复用聚合的卡片 ⇒ 那一族的日志咽喉也在那边）。

using System;
using System.IO;
using System.Text;

namespace AIPlayer.Shell.Features.Person;

/// <summary>人物屏证据写盘（UTF-8 无 BOM；`.txt`，绝不用 `.log`）。</summary>
public static class PersonEvidence
{
    /// <summary>证据文件名。</summary>
    public const string FileName = "t58-person-selftest.txt";

    /// <summary>`bin\Debug\<tfm>\<rid>\` → 上溯 5 级到 `shell\`，再进 `App\Features\Person\evidence`。
    /// `SHELL_EVIDENCE_DIR`（绝对路径）优先 —— 隔离输出（`-p:OutputPath=E:\ui2-bin\`）时必须显式把证据指回仓内。
    /// </summary>
    public static string EvidenceDir
    {
        get
        {
            var over = Environment.GetEnvironmentVariable("SHELL_EVIDENCE_DIR");
            if (!string.IsNullOrWhiteSpace(over))
            {
                // 语义与 `AggregateDiagnostics` 一致：**该值就是最终证据目录**（不再拼子目录）。
                return Path.GetFullPath(over.Trim());
            }

            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "App", "Features", "Person", "evidence"));
        }
    }

    public static string EvidencePath => Path.Combine(EvidenceDir, FileName);

    /// <summary>卡片级日志（追加写）——与报告（整体覆盖写）分家，避免长驻实例把日志灌进报告。</summary>
    public static string CardLogPath => Path.Combine(EvidenceDir, FileName.Replace("-selftest.txt", "-cardlog.txt"));

    /// <summary>落点只报一次（见 <see cref="AIPlayer.Shell.Features.Evidence.EvidenceDirReporter.LogOnce"/>）。</summary>
    private static int _dirLogged;

    // t281：本屏原先自己实现的 `LogEvidenceDirOnce()` 已收敛到唯一实现点
    // `Evidence.EvidenceDirReporter.LogOnce`；本屏只提供自己的前缀 `PERSON EVIDENCE-DIR` 与落点参数。

    /// <summary>追加一行（带时刻）；**不抛异常**（证据落盘失败不得影响界面）。</summary>
    public static void AppendLine(string line)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "PERSON EVIDENCE-DIR", EvidenceDir, FileName, Path.GetFileName(CardLogPath));
            Directory.CreateDirectory(EvidenceDir);
            File.AppendAllText(
                CardLogPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            // 不静默：证据写不进去本身就是要诊断的状况，至少进外壳日志。
            Program.Log("PERSON evidence-append FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>整段覆盖写（自检开始/结束各写一次）。</summary>
    public static void WriteEvidence(string content)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "PERSON EVIDENCE-DIR", EvidenceDir, FileName, Path.GetFileName(CardLogPath));
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(EvidencePath, content, new UTF8Encoding(false));
            Program.Log("PERSON evidence written " + EvidencePath + " bytes=" + new FileInfo(EvidencePath).Length);
        }
        catch (Exception ex)
        {
            Program.Log("PERSON evidence FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

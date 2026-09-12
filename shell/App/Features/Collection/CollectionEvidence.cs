// t58（U-L 分片二）合集屏的证据落点。
//
// 与人物屏同一条纪律：证据落在**本屏自己的目录**（`Features/Collection/evidence/*.txt`），
// 卡片级日志（封面失败/无图）经 `AggregateDiagnostics.Redirect` 改道到这里，不污染 t31 的证据文件。

using System;
using System.IO;
using System.Text;

namespace AIPlayer.Shell.Features.Collection;

/// <summary>合集屏证据写盘（UTF-8 无 BOM；`.txt`，绝不用 `.log`）。</summary>
public static class CollectionEvidence
{
    public const string FileName = "t58-collection-selftest.txt";

    /// <summary>`bin\Debug\<tfm>\<rid>\` → 上溯 5 级到 `shell\`，再进 `App\Features\Collection\evidence`。
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
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "App", "Features", "Collection", "evidence"));
        }
    }

    public static string EvidencePath => Path.Combine(EvidenceDir, FileName);

    /// <summary>卡片级日志（追加写）——与报告（整体覆盖写）分家，避免长驻实例把日志灌进报告。</summary>
    public static string CardLogPath => Path.Combine(EvidenceDir, FileName.Replace("-selftest.txt", "-cardlog.txt"));

    /// <summary>落点只报一次（见 <see cref="AIPlayer.Shell.Features.Evidence.EvidenceDirReporter.LogOnce"/>）。</summary>
    private static int _dirLogged;

    // t281：本屏原先自己实现的 `LogEvidenceDirOnce()` 已收敛到唯一实现点
    // `Evidence.EvidenceDirReporter.LogOnce`；本屏只提供自己的前缀 `COLLECTION EVIDENCE-DIR` 与落点参数。

    /// <summary>追加一行（带时刻）；**不抛异常**。</summary>
    public static void AppendLine(string line)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "COLLECTION EVIDENCE-DIR", EvidenceDir, FileName, Path.GetFileName(CardLogPath));
            Directory.CreateDirectory(EvidenceDir);
            File.AppendAllText(
                CardLogPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            Program.Log("COLLECTION evidence-append FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>整段覆盖写（自检结束时写一次）。</summary>
    public static void WriteEvidence(string content)
    {
        try
        {
            Evidence.EvidenceDirReporter.LogOnce(ref _dirLogged, "COLLECTION EVIDENCE-DIR", EvidenceDir, FileName, Path.GetFileName(CardLogPath));
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(EvidencePath, content, new UTF8Encoding(false));
            Program.Log("COLLECTION evidence written " + EvidencePath + " bytes=" + new FileInfo(EvidencePath).Length);
        }
        catch (Exception ex)
        {
            Program.Log("COLLECTION evidence FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

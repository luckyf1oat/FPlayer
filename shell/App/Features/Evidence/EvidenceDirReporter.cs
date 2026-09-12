// t281：证据落点「每运行只报一次」的**唯一实现点**。
//
// 为什么要有它：同一条裁定（captain 2026-09-12：`SHELL_EVIDENCE_DIR` 会让落点随环境变化，
// 隔离输出（`-p:OutputPath=…`）时 `AppContext.BaseDirectory` 上溯会跑出仓外，光看运行命令
// **判不出证据写到哪**）原本在 `AggregateDiagnostics` / `PersonEvidence` / `CollectionEvidence`
// 三处**各写一份、逐句同源** ⇒ 「改一处、忘两处」的漂移风险。
// 现在：格式与 `source=` 判定只在本文件一份；各屏只提供**自己的**前缀与落点参数。
//
// [!] 守卫（`Interlocked`）由**调用方按屏**持有（`ref int guard`）：三屏各自「每运行一次」，
//     不是全进程一次 —— 否则后两屏的落点行会被第一屏吃掉（本卡验收 [3] 要的正是三屏各一行）。
// [!] 未设 `SHELL_EVIDENCE_DIR` ⇒ 走各屏默认的仓内目录，行为与收敛前逐字相同。

using System;
using System.Threading;

namespace AIPlayer.Shell.Features.Evidence;

/// <summary>证据落点行（`… EVIDENCE-DIR path=… report=… cardlog=… source=…`）的唯一实现点。</summary>
public static class EvidenceDirReporter
{
    /// <summary>
    /// 每次运行的**第一笔**证据写盘前，把落点打一行到外壳日志；同一屏再次调用为空操作。
    /// </summary>
    /// <param name="guard">调用方**按屏**持有的守卫字段（`Interlocked.Exchange` 保证每屏只报一次）。</param>
    /// <param name="prefix">逐屏前缀，逐字保留历史形态：`EVIDENCE-DIR` / `PERSON EVIDENCE-DIR` / `COLLECTION EVIDENCE-DIR`。</param>
    /// <param name="evidenceDir">该屏当刻的证据目录（`SHELL_EVIDENCE_DIR` 是否生效由本方法统一判定并写进 `source=`）。</param>
    public static void LogOnce(ref int guard, string prefix, string evidenceDir, string reportFile, string cardLogFile)
    {
        if (Interlocked.Exchange(ref guard, 1) != 0) { return; }

        Program.Log(prefix + " path=" + evidenceDir + " report=" + reportFile + " cardlog=" + cardLogFile
            + " source=" + (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHELL_EVIDENCE_DIR")) ? "default" : "SHELL_EVIDENCE_DIR"));
    }
}

// t92（captain 2026-09-12 裁定，源自 t78 的 F1）：**设置兼容层（只读）**。
//
// 背景：迁移前原件 58 键 vs 现行已建模 44 键 ⇒ 53 个旧键回到盘上但**不被读**（t80 只做了"存盘不丢 + 回填"）。
// 本层负责把其中**语义等价**的旧名折算成现行键，让老用户的设置真的生效；**只读**：不改盘、不改调用方对象。
//
// 🔴 两条不生效（只登记待定）：值语义会变的那两条 —— 打开就等于替用户做决定
//    · `traktScrobbleEnabled → traktEnabled`：打开会**往外推观看记录**（Trakt 服务端可见）；
//    · `segmentSources → skipSources`：旧值形态与新结构不同，直接套会把"源列表"读成"别的含义"。
//    另两条同列待定（理由不同）：`webdavAutoBackupIntervalValue/Unit` 需要**单位换算**、
//    `seekForward/BackwardSeconds` 是**折进 shortcuts 子结构**而不是顶层 1:1。
//    待 captain/用户对"要不要恢复成他原来的值"表态后再打开 —— 在那之前一律只落日志。
//
// 映射表来源 = `shell/Tests/settings-key-reconcile.ps1:33-64`（t78 的 1:1 rename 表 + merge 表），
// **不另造一份口径**；那边的 `-DropMapKey` 反控继续作为"分类器不是恒真"的证据。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Settings;

/// <summary>旧键名 → 现行键名的只读兼容层（只折算、不写盘；风险项只登记不生效）。</summary>
public static class SettingsCompat
{
    /// <summary>语义等价、可直接折算的旧键（旧名 → 现行名）。</summary>
    public static readonly (string Legacy, string Target)[] SafeRenames =
    {
        ("proxyMode", "proxyEnabled"),
        ("customProxyAddress", "proxyUrl"),
        ("defaultRtxVsrEnabled", "rtxVsr"),
        ("defaultRtxVideoHdrEnabled", "rtxVideoHdr"),
        ("defaultAnimeMode", "animeMode"),
        ("defaultSharpenMode", "sharpenMode"),
        ("danmuApis", "danmakuApis"),
        ("playerShortcuts", "shortcuts"),
        ("preferredSubtitleLang", "preferredSubtitleLanguage"),
        ("autoSkipEnabled", "skipFeatureEnabled"),
        ("autoSkipIntroEnabled", "skipIntroEnabled"),
        ("autoSkipOutroEnabled", "skipCreditsEnabled"),
        ("closeToTray", "minimizeToTrayOnClose"),
        ("webdavAutoBackupEnabled", "autoBackupEnabled"),
        ("webdavAutoBackupServerId", "autoBackupServerId"),
    };

    /// <summary>**只登记、不生效**的旧键（旧名, 现行名, 理由）。理由分三类，便于将来逐条开口子。</summary>
    public static readonly (string Legacy, string Target, string Reason)[] PendingRenames =
    {
        ("traktScrobbleEnabled", "traktEnabled", "value-semantics-differs:打开会往外推观看记录"),
        ("traktScrobbling", "traktEnabled", "value-semantics-differs:同上（旧版别名）"),
        ("segmentSources", "skipSources", "value-semantics-differs:旧值形态与新结构不同"),
        ("webdavAutoBackupIntervalValue", "autoBackupIntervalHours", "needs-unit-conversion:值要走单位换算"),
        ("webdavAutoBackupIntervalUnit", "autoBackupIntervalHours", "needs-unit-conversion:只给单位"),
        ("seekForwardSeconds", "shortcuts.seekForwardSeconds", "folded-into-structure:折进 shortcuts"),
        ("seekBackwardSeconds", "shortcuts.seekBackwardSeconds", "folded-into-structure:折进 shortcuts"),
    };

    /// <summary>一次加载的兼容层读数（供日志/自检/证据读取）。</summary>
    public sealed class Report
    {
        public List<string> MappedKeys { get; } = new List<string>();
        public List<string> SkippedBecauseModeled { get; } = new List<string>();
        public List<string> PendingKeys { get; } = new List<string>();
        public List<string> UnmappedKeys { get; } = new List<string>();

        public int UnmappedCount => UnmappedKeys.Count;

        public string Summary =>
            $"mapped={MappedKeys.Count} already-modeled={SkippedBecauseModeled.Count} pending={PendingKeys.Count} unmapped={UnmappedCount}";
    }

    /// <summary>
    /// 产出"折算后的**内存副本**"：语义等价的旧键在**现行键缺失**时折算过来；盘上已有的现行键**一律优先**
    /// （用户在新界面里改过的值不得被旧名盖掉）。**不写盘、不改入参。**
    /// </summary>
    public static JsonObject BuildEffective(JsonObject disk, out Report report)
    {
        report = new Report();
        var effective = disk == null ? new JsonObject() : (JsonObject)disk.DeepClone();
        if (disk == null) return effective;

        foreach (var (legacy, target) in SafeRenames)
        {
            if (!disk.ContainsKey(legacy)) continue;
            if (effective.ContainsKey(target))
            {
                // 盘上已有现行键 ⇒ 现行键赢（"新界面的值"优先于"旧名遗留值"）
                report.SkippedBecauseModeled.Add(legacy);
                continue;
            }
            effective[target] = disk[legacy]?.DeepClone();
            report.MappedKeys.Add(legacy);
        }

        foreach (var (legacy, _, _) in PendingRenames)
        {
            if (disk.ContainsKey(legacy)) report.PendingKeys.Add(legacy);
        }

        var handled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (legacy, _) in SafeRenames) handled.Add(legacy);
        foreach (var (legacy, _, _) in PendingRenames) handled.Add(legacy);
        foreach (var kv in disk)
        {
            if (AppSettings.ModeledKeys.Contains(kv.Key)) continue; // 已建模：不是"未映射"
            if (handled.Contains(kv.Key)) continue;                 // 已在本层表里（折算/待定）
            report.UnmappedKeys.Add(kv.Key);
        }
        report.UnmappedKeys.Sort(StringComparer.Ordinal);
        return effective;
    }

    /// <summary>把读数落成可见日志（每行都可被证据直接引用）。</summary>
    public static void LogReport(Report report)
    {
        if (report == null) return;
        foreach (var legacy in report.MappedKeys)
        {
            var target = SafeRenames.First(x => x.Legacy == legacy).Target;
            DebugLog.Info($"SETTINGS-COMPAT-MAPPED legacy={legacy} → key={target}（盘上无现行键 ⇒ 按旧名折算）");
        }
        foreach (var legacy in report.SkippedBecauseModeled)
        {
            var target = SafeRenames.First(x => x.Legacy == legacy).Target;
            DebugLog.Info($"SETTINGS-COMPAT-SKIPPED legacy={legacy} target={target}（盘上已有现行键 ⇒ 现行键优先）");
        }
        foreach (var legacy in report.PendingKeys)
        {
            var entry = PendingRenames.First(x => x.Legacy == legacy);
            DebugLog.Warn($"COMPAT-PENDING legacy={legacy} target={entry.Target} reason={entry.Reason}（**只登记不生效**，等用户表态）");
        }
        var shown = report.UnmappedKeys.Count <= 12
            ? string.Join(",", report.UnmappedKeys)
            : string.Join(",", report.UnmappedKeys.Take(12)) + " …";
        DebugLog.Info($"SETTINGS-UNMAPPED-KEYS n={report.UnmappedCount}{(report.UnmappedCount == 0 ? string.Empty : " keys=" + shown)}");
        DebugLog.Info($"SETTINGS-COMPAT-SUMMARY {report.Summary}");
    }
}

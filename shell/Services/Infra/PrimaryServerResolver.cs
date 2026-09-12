// t191（U-T 主源收敛）：全仓**唯一**的「主源（当前服务器）」解析实现。
//
// 为什么要有它：`UI_MAP §1` 的语义是「首页 / 收藏 / 媒体库 / 搜索 = 主源屏」，但解析口径曾被抓成两份：
//   · 家族 A（HomePage / DetailPage / Favorites / Library / Search / SearchRunner）—— 读 `AppSettings.LastServerId`，
//     未命中或该台不可用 ⇒ 回落第一台 `Enabled && Kind == Emby`（按 `SortIndex`）；
//   · 家族 B（PersonPage / CollectionPage）—— **完全不读 `LastServerId`**，且 kind 判据是 Emby∪Jellyfin
//     ⇒ 用户在侧栏换了主源后，进「人物 / 合集」**仍然看第一台**（用户可见缺陷）。
//
// 收敛后只有本文件一份实现。家族 A 的 5 处调用点本轮**不动**（口径本来就对，去重留给其属主）。
// 本文件**不依赖 App 层**：读数是注入的 `Action<string>` sink（App 传 `Program.Log`）。

using System;
using System.Collections.Generic;
using System.Linq;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>主源（当前服务器）解析 —— 全仓唯一实现。</summary>
public static class PrimaryServerResolver
{
    /// <summary>人物屏的自检覆盖变量（语义保留：Id 或 Name 子串匹配，**优先于主源**）。</summary>
    public const string PersonEnvVar = "SHELL_PERSON_SERVER";

    /// <summary>合集屏的自检覆盖变量（同上）。</summary>
    public const string CollectionEnvVar = "SHELL_COLLECTION_SERVER";

    /// <summary>读数里的来源标记：env 覆盖生效。</summary>
    public const string SourceEnv = "env";

    /// <summary>读数里的来源标记：`LastServerId` 命中的可用台。</summary>
    public const string SourceLastServerId = "lastServerId";

    /// <summary>读数里的来源标记：回落第一台可用台（含"`LastServerId` 指向的台不可用"）。</summary>
    public const string SourceFallback = "fallback";

    /// <summary>
    /// 可用台 = `Enabled && Kind == Emby`，按 `SortIndex` 升序 —— **与家族 A 逐字一致**（这是本卡要收敛的 kind 判据）。
    /// </summary>
    public static List<ServerConfig> AvailableEmbyServers(ServerConfigStore store = null)
        => (store ?? ServerConfigStore.Instance).Servers?
            .Where(s => s.Enabled && s.Kind == ServerKind.Emby)
            .OrderBy(s => s.SortIndex)
            .ToList() ?? new List<ServerConfig>();

    /// <summary>
    /// 解析主源：① `envVarName` 显式覆盖（**优先**，自检依赖）→ ② `SettingsService.Settings.LastServerId`
    /// 指向的**可用**台 → ③ 第一台可用台。
    /// <para><paramref name="log"/> 非空时**由本函数**落一行可读读数（全仓同一形状，逐屏可判）：
    /// <c>PRIMARY-SERVER screen=… id=… name=… source=env|lastServerId|fallback env=… candidates=N</c>。</para>
    /// <para><paramref name="store"/> 只为自检注入（生产传 null = <see cref="ServerConfigStore.Instance"/>）。</para>
    /// </summary>
    public static ServerConfig Resolve(string screen, string envVarName, Action<string> log = null, ServerConfigStore store = null)
    {
        var available = AvailableEmbyServers(store);
        var server = ResolveCore(available, envVarName, out var source);

        log?.Invoke("PRIMARY-SERVER screen=" + (string.IsNullOrEmpty(screen) ? "-" : screen)
            + " id=" + (string.IsNullOrEmpty(server?.Id) ? "-" : server.Id)
            + " name=" + (string.IsNullOrEmpty(server?.Name) ? "-" : server.Name)
            + " source=" + source
            + " env=" + (string.IsNullOrEmpty(envVarName) ? "-" : envVarName)
            + " candidates=" + available.Count
            // 候选**顺序**也打出来（最多 8 个）：反控要证明"用户选的那台不是第一台"，光有 count 证不了。
            + " order=" + string.Join(",", available.Take(8).Select(s => s.Id)));

        return server;
    }

    private static ServerConfig ResolveCore(List<ServerConfig> available, string envVarName, out string source)
    {
        // ① env 覆盖：自检要靠它指台 ⇒ **优先于主源**。匹配口径与原家族 B 一致（Id 精确 或 Name 子串，忽略大小写），
        //    候选面按本卡要求的 kind 判据收窄到"可用 Emby 台"（收窄是刻意的：读数里的 candidates= 会让它可见）。
        var wanted = string.IsNullOrEmpty(envVarName)
            ? string.Empty
            : (Environment.GetEnvironmentVariable(envVarName) ?? string.Empty).Trim();
        if (wanted.Length > 0)
        {
            foreach (var s in available)
            {
                if (string.Equals(s.Id, wanted, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(s.Name) && s.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    source = SourceEnv;
                    return s;
                }
            }
        }

        // ② 主源：用户最近在侧栏选过的那台（`MainWindow` 写入 `settings.lastServerId`）
        var preferredId = SettingsService.Instance?.Settings?.LastServerId ?? string.Empty;
        if (preferredId.Length > 0)
        {
            var preferred = available.FirstOrDefault(s => string.Equals(s.Id, preferredId, StringComparison.Ordinal));
            if (preferred != null)
            {
                source = SourceLastServerId;
                return preferred;
            }
        }

        // ③ 回落第一台可用台（`LastServerId` 指向的台被禁用/删掉/不是 Emby 时也走这里）
        source = SourceFallback;
        return available.FirstOrDefault();
    }
}

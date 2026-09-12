// t93（P0 用户报障「聚合视界功能无效」）的**取证面**：五条反例各自的原始读数。
//
// 为什么独立一个文件：t31 自检写的是"屏能显示什么"，本文件写的是"**缓存与作用域**这一层的五条契约"，
// 两者失败时的定位路径完全不同（一个是渲染，一个是缓存键/写盘/取消）。
//
// 五条（对应卡面 A–E，每条都必须能 False）：
//   A 缓存键带作用域身份 ⇒ 收藏屏（主源单服）与聚合视界（跨服）不可能共用；旧的无作用域键必须被清除。
//   B 毒结果不得覆盖好缓存 ⇒ "上一份有内容 + 本次 0 条 + 源集合缩小/全失败"必须**拒绝写盘**。
//   C T0 不得把空/失效缓存当内容渲染 ⇒ 保持骨架屏；连骨架都没有时必须有可见提示（不留白）。
//   D T1-noop 不得把空屏钉死 ⇒ 已渲染 0 张而刷新有内容 ⇒ 必须重绑（reason=rendered-empty）。
//   E 取消不得被记成 `refresh=done` ⇒ 必须是 `refresh=cancelled` 且缓存字节不变。
//
// [!] 本文件只跑在自检钩子下（`SHELL_SELFTEST_AGGREGATE=1`）；生产路径零影响。
// [!] **不要在异步调用上用 `ConfigureAwait(false)`**：自检从页面 `Loaded`（UI 线程）进来，
//     一旦切到线程池，后续 `page.*` 的 XAML 操作会抛 `COMException`（本轮实测踩到：最后一个恢复步骤就是这么丢的）。
//     所有"注入"都走 `SwrSnapshotCache` 的公开入口，用完**恢复**真实状态（最后再真刷新一次）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>t93 的五条反例读数（缓存作用域 / 毒写 / 空缓存渲染 / T1 钉死 / 取消可见）。</summary>
public static class T93SelfTest
{
    /// <summary>跑完五条并把原始读数追加进 <paramref name="sb"/>（不抛异常：任何一条失败只记 False）。</summary>
    public static async Task RunAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("=== t93（P0 空屏）五条反例读数 ===");
        sb.AppendLine("utc = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine();

        await RunOneAsync(sb, "A", () => ControlAAsync(sb, page));
        await RunOneAsync(sb, "B", () => ControlBAsync(sb, page));
        await RunOneAsync(sb, "C", () => ControlCAsync(sb, page));
        await RunOneAsync(sb, "D", () => ControlDAsync(sb, page));
        await RunOneAsync(sb, "E", () => ControlEAsync(sb, page));

        // 收尾：**无论前面哪条出错**都要把屏恢复到真实数据（否则后续读数/截图是脏的）
        try
        {
            await page.ReloadForSelfTestAsync();
            sb.AppendLine("收尾恢复：海报 = " + page.PosterCount + " 张｜摘要 = " + page.SummaryTextValue);
        }
        catch (Exception ex)
        {
            sb.AppendLine("收尾恢复失败 " + ex.GetType().Name + ": " + ex.Message);
        }

        sb.AppendLine();
    }

    /// <summary>单条反控的护栏：它抛了也只记一行（其余四条与"收尾恢复"照跑）。</summary>
    private static async Task RunOneAsync(StringBuilder sb, string tag, Func<Task> control)
    {
        try
        {
            await control();
        }
        catch (Exception ex)
        {
            sb.AppendLine("  " + tag + " 反控异常 " + ex.GetType().Name + ": " + ex.Message);
            Program.Log("T93 control-" + tag + " FAIL " + ex.GetType().FullName + ": " + ex.Message);
        }
    }

    /// <summary>A：键带作用域 + 旧键被清除（两屏不可能共用一条快照）。</summary>
    private static async Task ControlAAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- A 缓存键必须带作用域身份 ---");

        var all = MediaAggregator.EnabledEmbyServers();
        var main = page.CurrentServers.FirstOrDefault();
        var aggregateKey = MediaSnapshotSource.KeyFor(AggregateKind.Favorites, all);
        var singleServerKeys = new List<string>();
        if (main != null)
        {
            singleServerKeys.Add(MediaSnapshotSource.KeyFor(AggregateKind.Favorites, new List<ServerConfig> { main }));
        }

        sb.AppendLine("  跨服键   = " + aggregateKey + "（服务器 " + all.Count + " 台）");
        foreach (var key in singleServerKeys)
        {
            sb.AppendLine("  单服键   = " + key);
        }

        var distinct = singleServerKeys.Count == 0 || singleServerKeys.All(k => !string.Equals(k, aggregateKey, StringComparison.Ordinal));
        sb.AppendLine("  断言A1 单服键 ≠ 跨服键 = " + distinct);

        // 真实反例：把**旧的无作用域键**用毒快照灌满，然后走一次真实载入 —— 它必须读不到（用作用域键），
        // 且旧键必须被 PurgeLegacyUnscopedKeys 删掉（磁盘上不再存在）。
        var legacyPath = MediaSnapshotSource.CacheInstance.PathFor(MediaSnapshotSource.LegacyUnscopedKeys[1]);
        var legacyWritten = await MediaSnapshotSource.CacheInstance.RefreshAsync(
            MediaSnapshotSource.LegacyUnscopedKeys[1],
            _ => Task.FromResult(SyntheticSnapshot(groups: 1, itemsPerGroup: 0)));
        var legacyBefore = File.Exists(legacyPath);
        var legacyBytes = legacyBefore ? new FileInfo(legacyPath).Length : 0;
        sb.AppendLine("  注入毒快照到旧键 '" + MediaSnapshotSource.LegacyUnscopedKeys[1] + "' ⇒ 落盘=" + legacyWritten.Success
            + " 文件存在=" + legacyBefore + " bytes=" + legacyBytes);

        // 清键标记复位（自检专用）⇒ 下一次载入会**再清一次**，从而证明"旧键被清掉"这条契约真的成立
        MediaSnapshotSource.ResetLegacyPurgeForSelfTest();
        await page.ReloadForSelfTestAsync();

        var outcome = page.LastOutcome;
        var legacyAfter = File.Exists(legacyPath);
        sb.AppendLine("  载入后读数：" + (outcome == null ? "<null>" : MediaSnapshotSource.Describe(outcome)));
        sb.AppendLine("  断言A2 载入时用的是**作用域键**（不是旧键）= "
            + (outcome != null && !string.Equals(outcome.Key, MediaSnapshotSource.LegacyUnscopedKeys[1], StringComparison.Ordinal)
               ? "True（key=" + outcome.Key + "）" : "False"));
        sb.AppendLine("  断言A3 旧键文件已被清除（存在 " + legacyBefore + " ⇒ " + legacyAfter + "）= " + (!legacyAfter));
        sb.AppendLine("  断言A4 屏上仍有内容（毒快照没被当内容）= " + (page.PosterCount > 0)
            + "（海报 " + page.PosterCount + " 张 / 行数 " + page.Rows.Count + "）");
        sb.AppendLine();
    }

    /// <summary>B：毒结果不得覆盖好缓存（判据四分支 + 真落地一次）。</summary>
    private static async Task ControlBAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- B 毒结果不得覆盖好缓存 ---");

        // "好缓存"必须**源集合比毒结果大**，否则"0 条"完全可能是"世界上真没有内容"（那种情况**不该**拦）。
        var good = SyntheticSnapshot(groups: 2, itemsPerGroup: 2);
        var shrunken = SyntheticSnapshot(groups: 1, itemsPerGroup: 0);
        var allFailed = new MediaSnapshot { FailedServers = { "甲", "乙" } };

        var b1 = MediaSnapshotSource.IsDegradedWrite(good, shrunken);        // 源集合缩小 + 0 条 ⇒ 退化
        var b2 = MediaSnapshotSource.IsDegradedWrite(good, allFailed);       // 全失败 ⇒ 退化
        var b3 = MediaSnapshotSource.IsDegradedWrite(good, good);            // 有内容 ⇒ 不是退化
        var b4 = MediaSnapshotSource.IsDegradedWrite(shrunken, shrunken);    // 本来就没内容 ⇒ 不是退化（不拦真空）
        sb.AppendLine("  判据（上一份 2 台/4 条）：");
        sb.AppendLine("    源集合缩小 + 0 条 ⇒ 退化 = " + b1);
        sb.AppendLine("    全部源失败       ⇒ 退化 = " + b2);
        sb.AppendLine("    新结果有内容     ⇒ 退化 = " + b3);
        sb.AppendLine("    上一份本来就空   ⇒ 退化 = " + b4);
        sb.AppendLine("  断言B1 前两条为真、后两条为假 = " + (b1 && b2 && !b3 && !b4));

        // 真落地：先在**当前作用域键**上放一份"2 台/4 条"的好快照，
        // 再让下一次取数退化成"1 台/0 条"（源集合缩小）⇒ 必须**拒绝写盘**（缓存字节不变）。
        var key = MediaSnapshotSource.KeyFor(page.CurrentKind, page.CurrentServers);
        var path = MediaSnapshotSource.CacheInstance.PathFor(key);
        await MediaSnapshotSource.CacheInstance.RefreshAsync(key, _ => Task.FromResult(good));
        var beforeSha = Sha12(path);
        var beforeBytes = File.Exists(path) ? new FileInfo(path).Length : 0;

        MediaSnapshotSource.DegradedFetchForSelfTest = true;
        try
        {
            await page.ReloadForSelfTestAsync();
        }
        finally
        {
            MediaSnapshotSource.DegradedFetchForSelfTest = false;
        }

        var outcome = page.LastOutcome;
        var afterSha = Sha12(path);
        var afterBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        sb.AppendLine("  好缓存文件 " + Path.GetFileName(path) + "：before " + beforeBytes + " B/" + beforeSha
            + " ⇒ after " + afterBytes + " B/" + afterSha);
        sb.AppendLine("  退化刷新读数：" + (outcome == null ? "<null>" : MediaSnapshotSource.Describe(outcome)));
        sb.AppendLine("  断言B2 刷新被判失败（不是 done）= " + (outcome != null && !outcome.RefreshSucceeded));
        sb.AppendLine("  断言B3 缓存**字节未变**（sha12 相同）= " + string.Equals(beforeSha, afterSha, StringComparison.Ordinal));
        sb.AppendLine("  断言B4 屏上仍显示缓存内容（海报 " + page.PosterCount + " 张 > 0）= " + (page.PosterCount > 0));
        sb.AppendLine("  断言B5 失败可见（提示行非空）= " + (page.FailedTextValue.Length > 0));
        sb.AppendLine();
    }

    /// <summary>C：空/失效缓存不得当内容渲染（骨架可见；没有缓存又没有内容时必须有可见提示）。</summary>
    private static async Task ControlCAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- C T0 不得把空缓存当内容渲染 ---");

        var key = MediaSnapshotSource.KeyFor(page.CurrentKind, page.CurrentServers);
        // 把**当前作用域键**写成"一服 0 条"（正是 2026-09-12 05:43:08 那份毒缓存的形态）
        await MediaSnapshotSource.CacheInstance.RefreshAsync(key, _ => Task.FromResult(SyntheticSnapshot(groups: 1, itemsPerGroup: 0)));

        // ① 刷新在途（取数打坏）⇒ T0 渲染 0 张时必须**保持骨架屏 / 有可见提示**，而不是留白
        MediaSnapshotSource.FailFetchForSelfTest = true;
        try
        {
            await page.ReloadForSelfTestAsync();
        }
        finally
        {
            MediaSnapshotSource.FailFetchForSelfTest = false;
        }

        sb.AppendLine("  前提：当前作用域键 = " + key + "，缓存内容 = 1 台/0 条");
        sb.AppendLine("  T0 海报张数 = " + page.PosterCount + "（行数 " + page.Rows.Count + "）｜骨架可见 = " + page.SkeletonVisible
            + "｜空态可见 = " + page.EmptyPanelVisible + "｜失败提示非空 = " + (page.FailedTextValue.Length > 0));
        sb.AppendLine("  断言C1 空缓存未被当成内容（海报 = " + page.PosterCount + " 张，或至少有可见提示）= "
            + (page.PosterCount == 0 ? (page.EmptyPanelVisible || page.FailedTextValue.Length > 0 || page.SkeletonVisible) : true));
        sb.AppendLine("  断言C2 没有留白（骨架 或 空态 或 失败提示 至少一个可见）= "
            + (page.SkeletonVisible || page.EmptyPanelVisible || page.FailedTextValue.Length > 0));

        // ② 恢复正常取数：真内容必须回来（证明上面不是"屏坏了"）
        await page.ReloadForSelfTestAsync();
        sb.AppendLine("  恢复正常后 海报 = " + page.PosterCount + " 张｜摘要 = " + page.SummaryTextValue);
        sb.AppendLine("  断言C3 真内容回来（海报 > 0）= " + (page.PosterCount > 0));
        sb.AppendLine();
    }

    /// <summary>D：T1-noop 不得把空屏钉死（0 张 + 刷新有内容 ⇒ 必须重绑）。</summary>
    private static async Task ControlDAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- D T1-noop 不得把空屏钉死 ---");

        var key = MediaSnapshotSource.KeyFor(page.CurrentKind, page.CurrentServers);
        await MediaSnapshotSource.CacheInstance.RefreshAsync(key, _ => Task.FromResult(SyntheticSnapshot(groups: 1, itemsPerGroup: 0)));

        await page.ReloadForSelfTestAsync();      // T0 = 1 台/0 条；T1 = 真数据
        var outcome = page.LastOutcome;
        sb.AppendLine("  T0 缓存 id 数 = " + (outcome?.CachedIds.Count ?? -1)
            + "｜T1 刷新 id 数 = " + (outcome?.RefreshedIds.Count ?? -1)
            + "｜T1 落地方式 = " + (page.LastT1Reason.Length > 0 ? page.LastT1Reason : "<none>"));
        sb.AppendLine("  断言D1 刷新成功 = " + (outcome != null && outcome.RefreshSucceeded));
        sb.AppendLine("  断言D2 屏上重绑出了内容（海报 " + page.PosterCount + " 张 > 0）= " + (page.PosterCount > 0));
        sb.AppendLine("  断言D3 落地方式不是 noop = " + (!string.Equals(page.LastT1Reason, "noop", StringComparison.Ordinal))
            + "（reason=" + page.LastT1Reason + "）");
        sb.AppendLine();
    }

    /// <summary>E：取消必须可见（`refresh=cancelled` + 缓存字节不变），不得记成 `refresh=done ids=[]`。</summary>
    private static async Task ControlEAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("--- E 取消必须可见（不得记成 refresh=done ids=[]）---");

        var key = MediaSnapshotSource.KeyFor(page.CurrentKind, page.CurrentServers);
        var path = MediaSnapshotSource.CacheInstance.PathFor(key);
        var beforeSha = Sha12(path);

        using var cts = new CancellationTokenSource();
        var task = MediaSnapshotSource.LoadAsync(
            page.CurrentKind,
            page.CurrentServers,
            onCacheValue: null,
            cancellationToken: cts.Token);
        await Task.Delay(400);       // 让真实取数跑起来（16 台，实测一发约 40 s）
        cts.Cancel();

        SwrOutcome cancelled = null;
        try
        {
            cancelled = await task;
        }
        catch (OperationCanceledException)
        {
            sb.AppendLine("  LoadAsync 把取消**抛了**（不是吞成成功）");
        }

        var afterSha = Sha12(path);
        sb.AppendLine("  取消后读数：" + (cancelled == null ? "<null>" : MediaSnapshotSource.Describe(cancelled)));
        sb.AppendLine("  缓存文件 sha12：before " + beforeSha + " ⇒ after " + afterSha);
        sb.AppendLine("  断言E1 刷新**未**被记成成功 = " + (cancelled == null || !cancelled.RefreshSucceeded));
        sb.AppendLine("  断言E2 失败原因显式为 cancelled = "
            + (cancelled != null && string.Equals(cancelled.Error, "cancelled", StringComparison.Ordinal))
            + "（error=" + (cancelled?.Error ?? "<null>") + "）");
        sb.AppendLine("  断言E3 缓存字节未变 = " + string.Equals(beforeSha, afterSha, StringComparison.Ordinal));
        sb.AppendLine();
    }

    /// <summary>
    /// 合成快照（**只用于反控**）：<paramref name="groups"/> 台、每台 <paramref name="itemsPerGroup"/> 条。
    /// 用它把"源集合缩小"做成可判形式 —— 退化判据比较的是**组数**。
    /// </summary>
    private static MediaSnapshot SyntheticSnapshot(int groups, int itemsPerGroup)
    {
        var snapshot = new MediaSnapshot();
        for (var g = 0; g < groups; g++)
        {
            var items = new List<SnapshotItem>();
            for (var i = 0; i < itemsPerGroup; i++)
            {
                items.Add(new SnapshotItem
                {
                    Id = "t93-g" + g + "-i" + i,
                    Name = "反控条目 " + g + "-" + i,
                    Type = i % 2 == 0 ? "Movie" : "Series",
                });
            }

            snapshot.Groups.Add(new SnapshotGroup
            {
                ServerId = "t93-control-" + g,
                ServerName = "（反控）第 " + g + " 台",
                TotalCount = items.Count,
                Items = items,
            });
        }

        return snapshot;
    }

    private static string Sha12(string path)
    {
        try
        {
            if (!File.Exists(path)) return "<absent>";
            using var stream = File.OpenRead(path);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).Substring(0, 12);
        }
        catch (Exception ex)
        {
            Program.Log("T93 sha12 FAIL " + ex.GetType().Name + ": " + ex.Message);
            return "<err:" + ex.GetType().Name + ">";
        }
    }
}

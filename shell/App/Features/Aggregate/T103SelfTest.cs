// t103 自检：**卡片点击的意图分离**（点卡片=进详情页 / 播放按钮=起播）+ 「单集也要有详情页」。
//
// 为什么用"调同一条实现"而不是"模拟鼠标事件"：本卡要证的是**意图绑定**，不是输入子系统。
//   `CardIntents` 是两屏唯一的意图实现（`FavoritesPage.OnCardClick`/`OnPlayClick` 与
//   `AggregatePage.OnCardClick`/`OnPlayClick` 都走它），所以自检直接走同一入口即可等价取证。
//
// [!] 本文件只跑在 `SHELL_SELFTEST_AGGREGATE=t103` 下；生产路径零影响。
// [!] 起播**不真启内核**：跑之前把 `SHELL_SELFTEST_NO_LAUNCH=1` 打开（`AggregatePlayback` 会落一行
//     `AGG PLAY selftest-suppressed`）—— 这样"入口被走到"仍被证明，而不会把 mpv 拉起来。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>t103 的两条意图 + 单集详情页降级的原始读数。</summary>
public static class T103SelfTest
{
    public static async Task RunAsync(StringBuilder sb, AggregatePage page)
    {
        sb.AppendLine("=== t103（卡片点击意图分离 + 单集详情页）读数 ===");
        sb.AppendLine("utc = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("no-launch = " + (Environment.GetEnvironmentVariable(AggregatePlayback.NoLaunchEnvVar) ?? "<unset>")
            + "（=1 ⇒ 起播只证明入口被走到，不启内核）");
        sb.AppendLine();

        var posters = page.Rows.SelectMany(r => r.Posters).ToList();
        var episodes = posters.Where(p => string.Equals(p.Item?.Type, "Episode", StringComparison.OrdinalIgnoreCase)).ToList();
        sb.AppendLine("  屏上卡片 = " + posters.Count + " 张｜其中 Episode = " + episodes.Count + " 张"
            + "（类型分布：" + string.Join(",", posters.GroupBy(p => p.Item?.Type ?? "<null>").Select(g => g.Key + "=" + g.Count())) + "）");

        var episode = episodes.FirstOrDefault(p => p.Item != null && !string.IsNullOrEmpty(p.Item.Id));
        if (episode == null)
        {
            sb.AppendLine("  断言 找到一条带 Id 的真实 Episode 收藏 ⇒ **INCONCLUSIVE（本发屏上没有 Episode）**");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("  取用样本：type=" + episode.Item.Type + " id=" + episode.Item.Id
            + " name=" + episode.Item.Name + " serverId=" + (episode.Server?.Id ?? "?"));
        sb.AppendLine("  断言① 样本是 Episode 且 Id 非空 = "
            + (string.Equals(episode.Item.Type, "Episode", StringComparison.OrdinalIgnoreCase) && episode.Item.Id.Length > 0));
        sb.AppendLine();

        // ── 意图①：点卡片 ⇒ 进详情页（含 Episode）────────────────────────────────
        var opened = page.OpenDetailForSelfTest(episode, out var reason);
        var tag = AIPlayer.Shell.Shell.ShellState.Current.CurrentTag;
        sb.AppendLine("--- 意图① 点卡片 ⇒ 进详情页 ---");
        sb.AppendLine("  导航返回 = " + opened + "｜失败原因 = " + (reason.Length == 0 ? "<none>" : reason));
        sb.AppendLine("  导航读数（CardIntents.LastDetailReading）= " + (CardIntents.LastDetailReading.Length == 0 ? "<none>" : CardIntents.LastDetailReading));
        sb.AppendLine("  当前页面 tag（ShellState.CurrentTag）= " + tag);
        sb.AppendLine("  断言② 点卡片成功且落在详情页 tag = " + (opened && string.Equals(tag, Features.Detail.DetailPage.NavTag, StringComparison.Ordinal)));
        sb.AppendLine("  断言③ 导航读数含 tag/itemId/serverId = "
            + (CardIntents.LastDetailReading.Contains("tag=" + Features.Detail.DetailPage.NavTag)
               && CardIntents.LastDetailReading.Contains("itemId=" + episode.Item.Id)
               && CardIntents.LastDetailReading.Contains("serverId=" + (episode.Server?.Id ?? "?"))));
        sb.AppendLine("  断言④ 最近意图 = detail = " + string.Equals(CardIntents.LastIntent, CardIntents.IntentDetail, StringComparison.Ordinal));
        await Task.Yield();

        // ── 意图②：播放按钮 ⇒ 起播（不启内核，但必须真的走到入口）────────────────
        sb.AppendLine();
        sb.AppendLine("--- 意图② 播放按钮 ⇒ 起播（no-launch 模式）---");
        var played = page.PlayForSelfTest(episode, out var blockReason);
        sb.AppendLine("  起播调用返回 = " + played + "｜拦因 = " + (blockReason.Length == 0 ? "<none>" : blockReason));
        sb.AppendLine("  断言⑤ 起播入口仍可达（返回 true）= " + played);
        sb.AppendLine("  断言⑥ 最近意图 = play = " + string.Equals(CardIntents.LastIntent, CardIntents.IntentPlay, StringComparison.Ordinal));
        sb.AppendLine("  断言⑦ 播放按钮有字形与 tooltip = "
            + (episode.PlayGlyph.Length > 0 && episode.PlayTooltip.Length > 0)
            + "（glyph='" + episode.PlayGlyph + "' tooltip='" + episode.PlayTooltip + "'）");

        // ── 意图分离的**可失败**一面：快照卡（拿不到真条目）不许静默 ──────────────
        sb.AppendLine();
        sb.AppendLine("--- 意图分离的反例：快照卡点播放 ⇒ 必须给可见拦因（不许静默 no-op）---");
        var snapshotPoster = new AggregatePoster(
            new EmbyItem { Id = "t103-snapshot-only", Name = "（反控）只有快照的卡", Type = "Episode" },
            episode.Server,
            imageUrl: null,
            badgeText: string.Empty,
            showProgress: false,
            progressFraction: 0);
        var blockedPlay = CardIntents.TryPlay("SELFTEST-T103", snapshotPoster, live: null, out var noLiveReason);
        sb.AppendLine("  TryPlay(live=null) 返回 = " + blockedPlay + "｜拦因 = " + (noLiveReason.Length == 0 ? "<none>" : noLiveReason));
        sb.AppendLine("  断言⑧ 无真条目时**拒绝起播**且给出可见理由 = " + (!blockedPlay && noLiveReason.Length > 0));

        // ── 单集详情页降级：自身 Id 不可用 ⇒ 退父剧；连父剧都没有 ⇒ 可见失败 ────────
        sb.AppendLine();
        sb.AppendLine("--- 单集降级：自身 Id 不可用 ⇒ 退父剧（可见降级，不是『点了没反应』）---");
        var brokenSelf = new AggregatePoster(
            new EmbyItem { Id = string.Empty, SeriesId = "t103-parent-series", SeriesName = "（反控）父剧", Name = "（反控）无 Id 的单集", Type = "Episode" },
            episode.Server, null, string.Empty, false, 0);
        var fallbackOk = CardIntents.TryOpenDetail(page.SelfTestFrame, "SELFTEST-T103", brokenSelf, live: null, out var fallbackReason);
        sb.AppendLine("  TryOpenDetail(自身 Id 空 / 有父剧) 返回 = " + fallbackOk + "｜失败原因 = " + (fallbackReason.Length == 0 ? "<none>" : fallbackReason));
        sb.AppendLine("  断言⑨ 退到父剧（读数含 degraded=parent-series）= "
            + (fallbackOk && CardIntents.LastDetailReading.Contains("degraded=parent-series")
               && CardIntents.LastDetailReading.Contains("itemId=t103-parent-series")));

        var hopeless = new AggregatePoster(
            new EmbyItem { Id = string.Empty, SeriesId = string.Empty, Name = "（反控）什么都没有", Type = "Episode" },
            episode.Server, null, string.Empty, false, 0);
        var hopelessOk = CardIntents.TryOpenDetail(page.SelfTestFrame, "SELFTEST-T103", hopeless, live: null, out var hopelessReason);
        sb.AppendLine("  TryOpenDetail(自身 Id 空 / 无父剧) 返回 = " + hopelessOk + "｜失败原因 = " + (hopelessReason.Length == 0 ? "<none>" : hopelessReason));
        sb.AppendLine("  断言⑩ 连父剧都没有 ⇒ 返回 false 且理由可见 = " + (!hopelessOk && hopelessReason.Length > 0));

        // ── 收尾：把窗口停在**详情页**（本卡要的取证画面就是"点完停在详情页"）─────
        sb.AppendLine();
        var finalOpen = page.OpenDetailForSelfTest(episode, out var finalReason);
        sb.AppendLine("  收尾：重新导航到该 Episode 的详情页 = " + finalOpen + "（reason=" + (finalReason.Length == 0 ? "<none>" : finalReason) + "）");
        sb.AppendLine("  收尾读数：" + CardIntents.LastDetailReading);
        sb.AppendLine("  收尾 tag = " + AIPlayer.Shell.Shell.ShellState.Current.CurrentTag);
        sb.AppendLine();
    }
}

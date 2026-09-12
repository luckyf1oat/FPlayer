// t103：卡片点击的**两种意图**（详情 / 播放）—— 收藏屏与聚合视界**共用同一实现**。
//
// 为什么抽出来而不是两屏各写一遍：用户报障「就算只有单集（收藏里）我也希望点击进入详情页」。
// 两屏若各写一份"点卡片"的判定，必然漂移（今晚已多次踩到同类漂移），而"意图"这件事本身只有两种：
//   · **点卡片 ⇒ 进详情页**（含 `Type=Episode`）—— 选季/选集/换源/播放都在详情页里做；
//   · **播放按钮 ⇒ 起播**（走既有 M3 参数面，不在这里重造）。
// 两条都不许"点了没反应"：任何走不通的情形都必须给出**可见**理由（页面把理由写进失败提示行）。
//
// [!] 单集（Episode）的详情页降级：本屏只有渲染面快照时，条目自身 Id 可能不可用 ⇒
//     退到**父剧**（`SeriesId`）的详情页，并在日志里标 `degraded=parent-series`；连父剧都没有 ⇒ 返回失败原因。

using System;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>卡片点击的两种意图（详情 / 播放）与它们的可见失败面。</summary>
public static class CardIntents
{
    /// <summary>意图常量：进详情页。</summary>
    public const string IntentDetail = "detail";

    /// <summary>意图常量：起播。</summary>
    public const string IntentPlay = "play";

    /// <summary>最近一次被走到的意图（自检/取证观察点）：`detail` / `play` / `none`。</summary>
    public static string LastIntent { get; private set; } = "none";

    /// <summary>最近一次详情导航的载荷读数（`tag=… itemId=… serverId=… type=…`），给证据用。</summary>
    public static string LastDetailReading { get; private set; } = string.Empty;

    /// <summary>
    /// **点卡片 ⇒ 进详情页**（含 Episode）。
    /// 条目优先级：刷新后的**真条目** &gt; 快照条目 &gt;（单集自身不可用 ⇒ **父剧**条目，标记降级）。
    /// 返回 <c>true</c> = 已发起导航（调用方清掉旧提示）；<c>false</c> = <paramref name="failureReason"/> 必须被显示。
    /// </summary>
    public static bool TryOpenDetail(Frame frame, string screen, AggregatePoster poster, EmbyItem live, out string failureReason)
    {
        failureReason = string.Empty;
        var server = poster?.Server;
        var item = live ?? poster?.Item;
        if (server == null || item == null)
        {
            failureReason = "该条目数据不完整，无法打开详情页（明细见日志）";
            return false;
        }

        var degraded = false;
        if (string.IsNullOrEmpty(item.Id))
        {
            // 单集自身 Id 不可用 ⇒ 退到父剧（可见降级，而不是"点了没反应"）
            if (!string.IsNullOrEmpty(item.SeriesId))
            {
                item = new EmbyItem
                {
                    Id = item.SeriesId,
                    Name = string.IsNullOrEmpty(item.SeriesName) ? item.Name : item.SeriesName,
                    Type = "Series",
                    SeriesId = item.SeriesId,
                };
                degraded = true;
            }
            else
            {
                failureReason = "该集详情不可用（条目缺少 Id 且没有父剧可退）";
                return false;
            }
        }

        try
        {
            AIPlayer.Shell.Shell.ShellState.Current.CurrentTag = Features.Detail.DetailPage.NavTag;
            LastIntent = IntentDetail;
            LastDetailReading = "tag=" + Features.Detail.DetailPage.NavTag
                + " itemId=" + item.Id
                + " serverId=" + server.Id
                + " type=" + (item.Type ?? "?")
                + (degraded ? " degraded=parent-series" : string.Empty);
            Program.Log(screen + " Nav -> detail " + LastDetailReading);
            frame?.Navigate(typeof(Features.Detail.DetailPage), item);
            return true;
        }
        catch (Exception ex)
        {
            Program.Log(screen + " DETAIL NAV-FAIL " + ex.GetType().Name + ": " + ex.Message);
            failureReason = "打不开详情页：" + ex.GetType().Name + "（明细见日志）";
            return false;
        }
    }

    /// <summary>
    /// **播放按钮 ⇒ 起播**（走既有 M3 参数面）。返回 <c>true</c> = 已发起起播；
    /// <c>false</c> = <paramref name="blockReason"/> 必须被显示（快照卡没有可播放的真条目时不允许静默失败）。
    /// </summary>
    public static bool TryPlay(string screen, AggregatePoster poster, EmbyItem live, out string blockReason)
    {
        blockReason = string.Empty;
        var server = poster?.Server;
        if (server == null || poster?.Item == null)
        {
            blockReason = "该条目数据不完整，无法起播（明细见日志）";
            return false;
        }

        if (live == null)
        {
            // SEAM③ 之后卡片可能来自**快照**（渲染面白名单，缺 `Raw`/`MediaSources`）⇒ 不许静默起播残缺数据
            blockReason = "该条目当前来自缓存快照，尚未取到可播放的完整条目｜请稍候重试（或重新进入本页触发刷新）";
            return false;
        }

        LastIntent = IntentPlay;
        Program.Log(screen + " play-click item=" + live.Name + " server=" + (server.Name ?? "?"));
        AggregatePlayback.Play(server, live);
        return true;
    }
}

// t31 / SEAM③（`shell/Services/AGGREGATION_INCREMENTAL.md` + `UI_SPEC_SHELL.md`）：
// **缓存优先 + 并发刷新**的本地快照模型。
//
// 🔴 为什么缓存的是**本 DTO**而不是服务层 `List<EmbyItem>`（这是对文档示例的**有意偏离**，理由可查）：
//   ① `EmbyItem.Raw` 是 `JsonObject`（`shell/Services/Models/EmbyModels.cs:221`）——把任意原始 JSON 整包写进缓存，
//      既**昂贵**又**易碎**（反序列化路径要把 `JsonElement` 重新挂回 `JsonObject`）；
//   ② 缓存只服务于**渲染**（标题/副标题/封面标签/时长/观看状态），`Raw` 与 `MediaSources` 之外的字段一个都不需要；
// ③ 因此**白名单式**落盘：只存渲染面字段 ⇒ 快照小、形态稳定、坏缓存影响面窄（反序列化失败 ⇒ Miss，按 不算 Fresh）。
//   ⚠️ 代价：快照**不是**完整 `EmbyItem` ⇒ 从快照渲染出来的卡片**不能**直接进播放链
//      （播放需要完整条目）。⇒ 本类的约定是：**点卡片起播前先拿刷新后的真条目**；详情页动作（播放/收藏切换）只用真条目。

using System;
using System.Collections.Generic;
using System.Linq;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>落盘用的条目快照（渲染面白名单）。</summary>
public sealed class SnapshotItem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string SeriesId { get; set; } = string.Empty;

    public string SeriesName { get; set; } = string.Empty;

    public int? IndexNumber { get; set; }

    public int? ParentIndexNumber { get; set; }

    public int? ProductionYear { get; set; }

    public long RunTimeTicks { get; set; }

    public int? ChildCount { get; set; }

    /// <summary>
    /// 「递归子项数」= **集数**（t188 / F4 的第三处载体）。
    /// 为什么必须跟着快照走：本屏渲染走 `SnapshotRenderer.ToGroupedRow` 的 `item.ToEmbyItem()` 还原，
    /// 而集数的唯一决定点 `MediaAggregator.EpisodeCountOf` **只认 `RecursiveItemCount`**（`ChildCount` 是
    /// "直属子项"、剧下多半是季）⇒ 载体漏了它，整屏每条剧都会判"缺失"（实测 27 条标记 / 0 条有集数）。
    /// </summary>
    public int? RecursiveItemCount { get; set; }

    public bool HasPrimaryImageTag { get; set; }

    public bool HasSeriesPrimaryImageTag { get; set; }

    /// <summary>观看状态（角标：未看集数 / ✓；进度条；只用于显示）。</summary>
    public bool Played { get; set; }

    public bool IsFavorite { get; set; }

    public double PlayedPercentage { get; set; }

    public int? UnplayedItemCount { get; set; }

    public long ResumeTicks { get; set; }

    /// <summary>从真条目取渲染面字段。</summary>
    public static SnapshotItem FromEmby(EmbyItem item) => item == null ? null : new SnapshotItem
    {
        Id = item.Id ?? string.Empty,
        Name = item.Name ?? string.Empty,
        Type = item.Type ?? string.Empty,
        SeriesId = item.SeriesId ?? string.Empty,
        SeriesName = item.SeriesName ?? string.Empty,
        IndexNumber = item.IndexNumber,
        ParentIndexNumber = item.ParentIndexNumber,
        ProductionYear = item.ProductionYear,
        RunTimeTicks = item.RunTimeTicks,
        ChildCount = item.ChildCount,
        RecursiveItemCount = item.RecursiveItemCount,
        HasPrimaryImageTag = item.ImageTags != null && item.ImageTags.ContainsKey("Primary"),
        HasSeriesPrimaryImageTag = !string.IsNullOrEmpty(item.SeriesPrimaryImageTag),
        Played = item.UserData?.Played ?? false,
        IsFavorite = item.UserData?.IsFavorite ?? false,
        PlayedPercentage = item.UserData?.PlayedPercentage ?? 0,
        UnplayedItemCount = item.UserData?.UnplayedItemCount,
        ResumeTicks = item.UserData?.ResumeTicks ?? 0,
    };

    /// <summary>
    /// 还原成 `EmbyItem`（**只填渲染面**；`Raw`/`MediaSources`/`Path` 等留空）。
    /// ⚠️ 据此渲染的卡片**不可**直接起播（见文件头"代价"）—— 起播前必须拿到刷新后的真条目。
    /// </summary>
    public EmbyItem ToEmbyItem()
    {
        var item = new EmbyItem
        {
            Id = Id ?? string.Empty,
            Name = Name ?? string.Empty,
            Type = Type ?? string.Empty,
            SeriesId = SeriesId ?? string.Empty,
            SeriesName = SeriesName ?? string.Empty,
            IndexNumber = IndexNumber,
            ParentIndexNumber = ParentIndexNumber,
            ProductionYear = ProductionYear,
            RunTimeTicks = RunTimeTicks,
            ChildCount = ChildCount,
            RecursiveItemCount = RecursiveItemCount,
            UserData = new EmbyUserData
            {
                Played = Played,
                IsFavorite = IsFavorite,
                PlayedPercentage = PlayedPercentage,
                UnplayedItemCount = UnplayedItemCount,
                PlaybackPositionTicks = ResumeTicks,
            },
        };

        if (HasPrimaryImageTag)
        {
            item.ImageTags["Primary"] = "cached";     // 只为让 `ImageTags.ContainsKey("Primary")` 成立（URL 由 EmbyService 现算）
        }

        if (HasSeriesPrimaryImageTag)
        {
            item.SeriesPrimaryImageTag = "cached";
        }

        return item;
    }

    public static List<SnapshotItem> FromEmbyList(IEnumerable<EmbyItem> items)
        => (items ?? Enumerable.Empty<EmbyItem>()).Where(i => i != null).Select(FromEmby).ToList();

    public static List<EmbyItem> ToEmbyList(IEnumerable<SnapshotItem> items)
        => (items ?? Enumerable.Empty<SnapshotItem>()).Where(i => i != null).Select(i => i.ToEmbyItem()).ToList();

    /// <summary>④：id 集合（两组分别打印用）。</summary>
    public static IEnumerable<string> IdsOf(IEnumerable<EmbyItem> items)
        => (items ?? Enumerable.Empty<EmbyItem>()).Select(i => i?.Id ?? "<null>");

    public static IEnumerable<string> IdsOf(IEnumerable<SnapshotItem> items)
        => (items ?? Enumerable.Empty<SnapshotItem>()).Select(i => i?.Id ?? "<null>");
}

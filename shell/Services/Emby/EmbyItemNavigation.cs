// 原版文件（rebuild/ 中不存在 `emby_item_navigation.dart`），按 DESIGN §4.2 映射表
// （`src/core/util/emby_item_navigation.dart` → `Services/Emby/EmbyItemNavigation.cs`）新写。
//
// 依据：DESIGN §4.1 #19（`EmbyService` 的 `/Shows/{id}/Episodes` 是内核 `--episode-list=` 的来源、
// `/Shows/NextUp` 对应 `nextEpisodeId`）+ SERVICE_API §1.2「下一集 `GET /Shows/NextUp`；季/集 `/Shows/{id}/Seasons`、`/Shows/{id}/Episodes`」。
// 字段口径：Emby 的 `IndexNumber`（集号）/`ParentIndexNumber`（季号）/`SeriesId`/`SeasonId`（EmbyModels 已解析）。
// **纯计算**：不联网 —— 调用方先取回剧集列表（`GetEpisodesAsync` 或 `GetNextUpAsync`），再由本类排期。

using System;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Emby;

/// <summary>集号排序依据（季号 → 集号；非剧集排在最后）。</summary>
public sealed class EpisodeOrder : IComparable<EpisodeOrder>
{
    public EpisodeOrder(int? season, int? episode, bool isEpisode = true)
    {
        Season = season;
        Episode = episode;
        IsEpisode = isEpisode;
    }

    public int? Season { get; }

    public int? Episode { get; }

    /// <summary>非剧集条目（如整季/花絮）为 false，排序时排在剧集之后。</summary>
    public bool IsEpisode { get; }

    /// <summary>比较用哨兵：缺号按 <see cref="int.MaxValue"/> 处理（未编号的排在同季末尾）。</summary>
    public int SeasonKey => IsEpisode ? (Season ?? int.MaxValue) : int.MaxValue;

    public int EpisodeKey => IsEpisode ? (Episode ?? int.MaxValue) : int.MaxValue;

    public int CompareTo(EpisodeOrder other)
    {
        if (other == null) return 1;
        var bySeason = SeasonKey.CompareTo(other.SeasonKey);
        return bySeason != 0 ? bySeason : EpisodeKey.CompareTo(other.EpisodeKey);
    }

    public static EpisodeOrder Of(EmbyItem item)
        => item == null ? new EpisodeOrder(null, null, false) : new EpisodeOrder(item.ParentIndexNumber, item.IndexNumber, item.IsEpisode);

    public override string ToString() => IsEpisode ? $"S{SeasonKey}E{EpisodeKey}" : "非剧集";
}

/// <summary>导航结果。缓存键可用 <see cref="CacheKey"/>，避免 `--next-episode-id=` 每次重查。</summary>
public sealed class EpisodeNavigationResult
{
    public EmbyItem Episode { get; set; }

    /// <summary>false ⇒ 没有下一集/上一集（剧终或剧首）。</summary>
    public bool Found => Episode != null;

    /// <summary>true ⇒ 该次导航跨越季边界（UI 可提示「进入第 N 季」）。</summary>
    public bool CrossesSeason { get; set; }

    public EpisodeOrder Current { get; set; }

    public EpisodeOrder Target { get; set; }

    /// <summary>来源剧集（<c>SeriesId</c>）；同一剧内导航时非空。</summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>稳定缓存键（如 <c>series-1|s1e2|next</c>）。</summary>
    public string CacheKey { get; set; } = string.Empty;

    public override string ToString() => $"EpisodeNavigationResult({(Found ? Episode.Id : "无")}, {CacheKey})";
}

/// <summary>下一集 / 上一集导航（含跨季边界）。</summary>
public static class EmbyItemNavigation
{
    /// <summary>下一集（严格大于当前集）。</summary>
    public static EpisodeNavigationResult Next(EmbyItem current, IEnumerable<EmbyItem> episodes)
        => Step(current, episodes, forward: true);

    /// <summary>上一集（严格小于当前集）。</summary>
    public static EpisodeNavigationResult Previous(EmbyItem current, IEnumerable<EmbyItem> episodes)
        => Step(current, episodes, forward: false);

    /// <summary>
    /// 下一集（可带条件，如「跳过已看」）。<paramref name="predicate"/> 为 null 时等价 <see cref="Next"/>。
    /// </summary>
    public static EpisodeNavigationResult Next(EmbyItem current, IEnumerable<EmbyItem> episodes, Func<EmbyItem, bool> predicate)
        => Step(current, episodes, forward: true, predicate);

    /// <summary>上一集（可带条件）。</summary>
    public static EpisodeNavigationResult Previous(EmbyItem current, IEnumerable<EmbyItem> episodes, Func<EmbyItem, bool> predicate)
        => Step(current, episodes, forward: false, predicate);

    /// <summary>按季号→集号排序后的副本（未编号条目稳定保持相对顺序）。</summary>
    public static List<EmbyItem> SortByEpisode(IEnumerable<EmbyItem> episodes)
    {
        var list = new List<EmbyItem>();
        if (episodes != null)
        {
            foreach (var item in episodes)
            {
                if (item != null) list.Add(item);
            }
        }

        var indexed = new List<KeyValuePair<int, EmbyItem>>(list.Count);
        for (var i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, EmbyItem>(i, list[i]));
        indexed.Sort((a, b) =>
        {
            var byOrder = EpisodeOrder.Of(a.Value).CompareTo(EpisodeOrder.Of(b.Value));
            return byOrder != 0 ? byOrder : a.Key.CompareTo(b.Key);
        });

        var sorted = new List<EmbyItem>(indexed.Count);
        foreach (var entry in indexed) sorted.Add(entry.Value);
        return sorted;
    }

    /// <summary>当前项在（已排序）列表中的下标；找不到返回 -1。</summary>
    public static int IndexOf(EmbyItem current, IReadOnlyList<EmbyItem> sorted)
    {
        if (current == null || sorted == null) return -1;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (ReferenceEquals(sorted[i], current)) return i;
        }
        if (string.IsNullOrEmpty(current.Id)) return -1;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (sorted[i] != null && string.Equals(sorted[i].Id, current.Id, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    /// <summary>两个条目是否同季（季号都已知且相等）。</summary>
    public static bool SameSeason(EmbyItem a, EmbyItem b)
        => a?.ParentIndexNumber.HasValue == true && b?.ParentIndexNumber.HasValue == true
           && a.ParentIndexNumber.Value == b.ParentIndexNumber.Value;

    /// <summary>是否跨季（两边都有季号且不相等）。</summary>
    public static bool CrossesSeason(EmbyItem from, EmbyItem to)
    {
        if (from == null || to == null) return false;
        return from.ParentIndexNumber.HasValue && to.ParentIndexNumber.HasValue
            && from.ParentIndexNumber.Value != to.ParentIndexNumber.Value;
    }

    /// <summary>同剧判断（<c>SeriesId</c> 相等；缺失时退化为「都在同一批列表里」）。</summary>
    public static bool SameSeries(EmbyItem a, EmbyItem b)
    {
        if (a == null || b == null) return false;
        if (string.IsNullOrEmpty(a.SeriesId) || string.IsNullOrEmpty(b.SeriesId)) return true;
        return string.Equals(a.SeriesId, b.SeriesId, StringComparison.Ordinal);
    }

    /// <summary>是否跨剧（两边 SeriesId 都非空且不等）。</summary>
    public static bool CrossesSeries(EmbyItem a, EmbyItem b)
        => !string.IsNullOrEmpty(a?.SeriesId) && !string.IsNullOrEmpty(b?.SeriesId)
           && !string.Equals(a.SeriesId, b.SeriesId, StringComparison.Ordinal);

    /// <summary>当前集是否是该（已排序）列表的最后一集。</summary>
    public static bool IsLast(EmbyItem current, IEnumerable<EmbyItem> episodes)
    {
        var sorted = SortByEpisode(episodes);
        var index = IndexOf(current, sorted);
        return index >= 0 && index == sorted.Count - 1;
    }

    /// <summary>当前集是否是该（已排序）列表的第一集。</summary>
    public static bool IsFirst(EmbyItem current, IEnumerable<EmbyItem> episodes)
    {
        var sorted = SortByEpisode(episodes);
        return IndexOf(current, sorted) == 0;
    }

    /// <summary>
    /// 选下一个候选：优先**同季**的相邻集；同季无候选时跨到下一季/上一季的边界集。
    /// 两处都没找到 ⇒ 返回不可用结果（剧终/剧首），调用方可再调 `/Shows/NextUp` 兜底。
    /// </summary>
    public static EpisodeNavigationResult Step(EmbyItem current, IEnumerable<EmbyItem> episodes, bool forward, Func<EmbyItem, bool> predicate = null)
    {
        var result = new EpisodeNavigationResult
        {
            SeriesId = current?.SeriesId ?? string.Empty,
        };

        if (current == null) return result;

        var all = SortByEpisode(episodes);

        // ① 同季相邻集（主路径）
        EmbyItem target = null;
        foreach (var item in all)
        {
            if (!SameSeries(current, item) || !SameSeason(current, item)) continue;
            if (predicate != null && !predicate(item)) continue;

            var byEpisode = EpisodeOrder.Of(item).CompareTo(EpisodeOrder.Of(current));
            if (forward ? byEpisode <= 0 : byEpisode >= 0) continue;

            if (target == null) target = item;
            else if (forward
                ? EpisodeOrder.Of(item).CompareTo(EpisodeOrder.Of(target)) < 0
                : EpisodeOrder.Of(item).CompareTo(EpisodeOrder.Of(target)) > 0)
            {
                target = item;
            }
        }

        // ② 跨季边界（同季无候选时：下一季的**首集** / 上一季的**末集**）
        if (target == null && current.ParentIndexNumber.HasValue)
        {
            var currentSeason = current.ParentIndexNumber.Value;
            foreach (var item in all)
            {
                if (!SameSeries(current, item)) continue;
                if (predicate != null && !predicate(item)) continue;

                var season = item.ParentIndexNumber;
                if (!season.HasValue) continue;
                var isCandidate = forward ? season.Value > currentSeason : season.Value < currentSeason;
                if (!isCandidate) continue;

                if (target == null)
                {
                    target = item;
                    continue;
                }

                var targetSeason = target.ParentIndexNumber.Value;
                // 跨季时取**最近的季**；同季内 forward 取最小集号、backward 取最大集号
                var bySeason = forward ? season.Value.CompareTo(targetSeason) : targetSeason.CompareTo(season.Value);
                if (bySeason < 0) target = item;
                else if (bySeason == 0)
                {
                    var byEpisode = EpisodeOrder.Of(item).CompareTo(EpisodeOrder.Of(target));
                    if (forward ? byEpisode < 0 : byEpisode > 0) target = item;
                }
            }
        }

        if (target == null) return result;

        result.Episode = target;
        result.CrossesSeason = CrossesSeason(current, target);
        result.Current = EpisodeOrder.Of(current);
        result.Target = EpisodeOrder.Of(target);
        result.SeriesId = !string.IsNullOrEmpty(target.SeriesId) ? target.SeriesId : (current.SeriesId ?? string.Empty);
        result.CacheKey = string.Join("|",
            result.SeriesId,
            result.Current.ToString(),
            forward ? "next" : "prev");
        return result;
    }
}

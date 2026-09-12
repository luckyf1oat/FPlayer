// 原版文件（rebuild/ 中不存在 `emby_item_list_merge.dart`），按 DESIGN §4.2 映射表
// （`src/core/util/emby_item_list_merge.dart` → `Services/Emby/EmbyItemListMerge.cs`）
// 新写；去重键口径与 DESIGN §4.1 #1/#13（`aggregated_search_service` 对齐 `emby_item_list_merge`、
// `cross_server_sync_service` 与 `server_merge` 去重键一致）逐字对齐。
//
// 去重键优先级（对应 Emby `ProviderIds` 语义，见 reversed/FlutterApp/SERVICE_API.md §1.2 的 `AnyProviderIdEquals`）：
//   1. `Type + ProviderIds`（Imdb → Tmdb → Tvdb，命中即用；值 trim + 小写）
//   2. 退化：`Type + 名称(trim + 小写) + ProductionYear`
// 诚实标注：原版合并 UI 的角标文案/排序无字符串实证 ⇒ 本文件只保证「去重键 + 首见顺序 + primary 取舍」的语义等价。
//
// API 说明（**刻意不做泛型推断**）：`MergeSources` 家族一律显式标注类型，避免
// 「方法组 → Func<T,string>」与「IEnumerable<T>[] params」两个候选互相干扰（实测会触发 CS1503/CS0019）。

using System;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Emby;

/// <summary>带来源的条目（来源标识可为 serverId、服务器名或库名，由调用方决定）。</summary>
public sealed class EmbyItemSource
{
    public EmbyItemSource()
    {
    }

    public EmbyItemSource(string source, EmbyItem item)
    {
        Source = source;
        Item = item;
    }

    /// <summary>来源标识（建议填 serverId，便于换源续播回到正确服务器）。</summary>
    public string Source { get; set; } = string.Empty;

    public EmbyItem Item { get; set; }
}

/// <summary>合并结果：<see cref="Item"/> 为默认展示/播放的版本，<see cref="Sources"/> 为全部来源（保持首见顺序）。</summary>
public sealed class EmbyMergedItem
{
    public EmbyMergedItem(EmbyItem item, IReadOnlyList<EmbyItemSource> sources)
    {
        Item = item;
        Sources = sources ?? Array.Empty<EmbyItemSource>();
    }

    public EmbyItem Item { get; }

    public IReadOnlyList<EmbyItemSource> Sources { get; }

    /// <summary>是否存在备选源（UI 角标「N 个源」）。</summary>
    public bool HasAlternatives => Sources.Count > 1;

    /// <summary>来源标签（`A / B`）。</summary>
    public string SourceLabel
    {
        get
        {
            var labels = new List<string>();
            foreach (var source in Sources)
            {
                if (!string.IsNullOrEmpty(source.Source)) labels.Add(source.Source);
            }
            return string.Join(" / ", labels);
        }
    }

    public override string ToString() => $"EmbyMergedItem({Item?.Name}, {Sources.Count} 源)";
}

/// <summary>跨服条目去重合并。</summary>
public static class EmbyItemListMerge
{
    /// <summary>外部 id 的命中优先级（与 Emby `ProviderIds` 的键名一致）。</summary>
    public static readonly string[] ProviderIdPriority = { "Imdb", "Tmdb", "Tvdb" };

    /// <summary>
    /// 去重键：外部 id 优先，退化到 `类型 + 名称 + 年份`。
    /// **刻意只有单参数**（带可选参数的版本会失去「方法组 → Func&lt;T,string&gt;」转换能力，实测 CS1503）。
    /// </summary>
    public static string KeyOf(EmbyItem item) => KeyOfWith(item, null);

    /// <summary>去重键（可指定外部 id 口径）；<paramref name="externalIdSelector"/> 命中时优先采用其返回值。</summary>
    public static string KeyOfWith(EmbyItem item, Func<EmbyItem, string> externalIdSelector)
    {
        if (item == null) return string.Empty;

        if (externalIdSelector != null)
        {
            var custom = externalIdSelector(item);
            if (!string.IsNullOrEmpty(custom)) return item.Type + "|custom:" + custom;
        }

        var ids = item.ProviderIds;
        if (ids != null)
        {
            foreach (var key in ProviderIdPriority)
            {
                var value = ProviderId(ids, key);
                if (value.Length > 0)
                {
                    return $"{item.Type}|{key}:{value.ToLowerInvariant()}";
                }
            }
        }

        return $"{item.Type}|{Normalize(item.Name)}|{YearText(item)}";
    }

    /// <summary>
    /// 合并多服条目：保持**首次出现顺序**（即服务器配置顺序）。
    /// 空 <c>Id</c> 的条目被丢弃（无 Id 无法播放/换源）。
    /// </summary>
    public static List<EmbyItem> Merge(IEnumerable<EmbyItem> first, IEnumerable<EmbyItem> second = null)
    {
        var lists = new List<IEnumerable<EmbyItem>>();
        if (first != null) lists.Add(first);
        if (second != null) lists.Add(second);
        return MergeWithKey(KeyOf, lists);
    }

    /// <summary>合并 n 个列表（显式数组，避免 params 与方法组推断互相干扰）。</summary>
    public static List<EmbyItem> MergeAll(params IEnumerable<EmbyItem>[] lists)
        => MergeWithKey(KeyOf, lists);

    /// <summary>合并（可指定键函数，供搜索聚合等场景微调去重口径）。</summary>
    public static List<EmbyItem> MergeWithKey(Func<EmbyItem, string> keyOf, IEnumerable<IEnumerable<EmbyItem>> lists)
    {
        var groups = MergeSourcesWithKey(keyOf, lists);
        var result = new List<EmbyItem>(groups.Count);
        foreach (var group in groups) result.Add(group.Item);
        return result;
    }

    /// <summary>
    /// 合并为「分组 + primary 取舍」结构（跨服合并的主入口）。
    /// 语义与 <c>ServerMergeService.merge</c> 一致：首见顺序 + primary = 已看 &gt; 有进度 &gt; 首个来源。
    /// </summary>
    public static List<EmbyMergedItem> MergeSources(IEnumerable<EmbyItemSource> flat)
    {
        var lists = new List<IEnumerable<EmbyItem>>();
        if (flat != null)
        {
            var items = new List<EmbyItem>();
            foreach (var entry in flat)
            {
                if (entry?.Item != null) items.Add(entry.Item);
            }
            lists.Add(items);
        }
        return MergeSourcesWithKey(KeyOf, lists);
    }

    /// <summary>合并（显式键函数；<paramref name="lists"/> 为数组，避免 params 推断歧义）。</summary>
    public static List<EmbyMergedItem> MergeSourcesWithKey(Func<EmbyItem, string> keyOf, IEnumerable<IEnumerable<EmbyItem>> lists)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<EmbyItemSource>>(StringComparer.Ordinal);
        var compare = keyOf ?? (item => KeyOf(item));

        if (lists != null)
        {
            foreach (var list in lists)
            {
                if (list == null) continue;
                foreach (var item in list)
                {
                    if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                    var key = compare(item) ?? string.Empty;
                    if (!grouped.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<EmbyItemSource>();
                        grouped[key] = bucket;
                        order.Add(key);
                    }
                    bucket.Add(new EmbyItemSource(string.Empty, item));
                }
            }
        }

        return Build(order, grouped);
    }

    /// <summary>同一去重键内、同一 itemId 只保留首见（同一服务器的重复响应不再是两个「源」）。</summary>
    public static List<EmbyItemSource> DedupeWithinKey(IReadOnlyList<EmbyItemSource> bucket)
    {
        var result = new List<EmbyItemSource>();
        if (bucket == null) return result;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in bucket)
        {
            if (entry?.Item == null) continue;
            if (seen.Add(entry.Item.Id)) result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// primary 取舍：**已看 &gt; 有进度 &gt; 首个来源**。
    /// 这样默认播放的是「用户真正在看的那一份」，与续播体验一致。
    /// </summary>
    public static EmbyItem PickPrimary(IReadOnlyList<EmbyItemSource> sources)
    {
        if (sources == null || sources.Count == 0) return null;

        foreach (var source in sources)
        {
            if (source?.Item?.UserData != null && source.Item.UserData.Played) return source.Item;
        }
        foreach (var source in sources)
        {
            if (source?.Item?.UserData != null && source.Item.UserData.PlaybackPositionTicks > 0) return source.Item;
        }
        return sources[0].Item;
    }

    /// <summary>键比较器（序号比较，与 Dart 的字符串 == 口径一致）。</summary>
    public static IEqualityComparer<string> KeyComparer => StringComparer.Ordinal;

    private static List<EmbyMergedItem> Build(List<string> order, Dictionary<string, List<EmbyItemSource>> grouped)
    {
        var merged = new List<EmbyMergedItem>(order.Count);
        foreach (var key in order)
        {
            var bucket = DedupeWithinKey(grouped[key]);
            merged.Add(new EmbyMergedItem(PickPrimary(bucket), bucket));
        }
        return merged;
    }

    private static string ProviderId(IDictionary<string, string> ids, string key)
    {
        if (ids.Count == 0) return string.Empty;
        if (ids.TryGetValue(key, out var value) && value != null) return value.Trim();

        // ProviderIds 的键大小写在不同服务端可能漂移 ⇒ 退化为忽略大小写查找。
        foreach (var kv in ids)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
            {
                return kv.Value.Trim();
            }
        }
        return string.Empty;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string YearText(EmbyItem item)
        => item.ProductionYear.HasValue ? item.ProductionYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
}

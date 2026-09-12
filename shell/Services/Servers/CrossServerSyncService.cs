// 等价移植：rebuild/ai_player/lib/core/services/server_merge_service.dart（90 行 Dart → C#）。
// 映射依据：DESIGN §4.1 #13（`cross_server_sync_service.dart` → `Services/Servers/CrossServerSyncService.cs`，
// 关键协议要点「与 `server_merge` 去重键一致」）+ §4.1 #1（`aggregated_search_service` 的去重语义对齐 `emby_item_list_merge`）。
//
// 逐行等价点（与 Dart 一致，不自行加戏）：
//   - `keyOf`：`ProviderIds` 按 Imdb → Tmdb → Tvdb 命中即用（值 trim + 小写）；否则退化 `Type|名称(trim+小写)|ProductionYear`
//   - `merge`：**保持首次出现顺序**（即服务器配置顺序）；`item.id` 为空的来源直接跳过
//   - `_pickPrimary`：**已看 > 有进度 > 首个来源**
//   - `hasAlternatives` = sources.length > 1；`sourceLabel` = 服务器名以 ` / ` 连接
// ⚠️ 诚实标注（与 Dart 原文相同）：原版合并 UI 的排序/角标文案/冲突取舍无字符串实证，
//    这里是与 `server_merge_service.dart` 等价的实现；去重键与 EmbyItemListMerge 共用同一口径。

using System;
using System.Collections.Generic;
using System.Text;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Servers;

/// <summary>合并条目的一条来源（服务器 + 该服务器上的条目，对应 Dart <c>MergedSource</c>）。</summary>
public sealed class MergedSource
{
    public MergedSource()
    {
    }

    public MergedSource(ServerConfig server, EmbyItem item)
    {
        Server = server;
        Item = item;
    }

    public ServerConfig Server { get; set; }

    public EmbyItem Item { get; set; }
}

/// <summary>合并后的条目（对应 Dart <c>MergedItem</c>）：<see cref="Item"/> 为默认展示/播放的版本。</summary>
public sealed class MergedItem
{
    public MergedItem(EmbyItem item, IReadOnlyList<MergedSource> sources)
    {
        Item = item;
        Sources = sources ?? Array.Empty<MergedSource>();
    }

    public EmbyItem Item { get; }

    /// <summary>全部来源（不可变视图语义：构造后不再改写）。</summary>
    public IReadOnlyList<MergedSource> Sources { get; }

    /// <summary>是否存在备选源（UI 角标「N 个源」）。</summary>
    public bool HasAlternatives => Sources.Count > 1;

    /// <summary>来源服务器名（`A / B`）。</summary>
    public string SourceLabel
    {
        get
        {
            var builder = new StringBuilder();
            foreach (var source in Sources)
            {
                if (builder.Length > 0) builder.Append(" / ");
                builder.Append(source.Server?.Name ?? string.Empty);
            }
            return builder.ToString();
        }
    }

    /// <summary>primary 来源（<see cref="Item"/> 所属的那条来源）。</summary>
    public MergedSource PrimarySource
    {
        get
        {
            foreach (var source in Sources)
            {
                if (ReferenceEquals(source.Item, Item)) return source;
            }
            return Sources.Count > 0 ? Sources[0] : null;
        }
    }

    public override string ToString()
        => $"MergedItem({Item?.Name}, {Sources.Count} 源)";
}

/// <summary>跨服状态同步 / 媒体库合并（对应 Dart <c>ServerMergeService</c>）。</summary>
public sealed class CrossServerSyncService
{
    private CrossServerSyncService()
    {
    }

    /// <summary>无状态服务（与原 Dart 的私有构造 + 静态方法一致）。</summary>
    public static CrossServerSyncService Instance { get; } = new CrossServerSyncService();

    /// <summary>去重键：外部 id 优先，退化到 `名称 + 年份 + 类型`（与 <see cref="EmbyItemListMerge.KeyOf"/> 同口径）。</summary>
    public static string KeyOf(EmbyItem item) => EmbyItemListMerge.KeyOf(item);

    /// <summary>合并多服务器结果：保持**首次出现顺序**（即服务器配置顺序）。</summary>
    public List<MergedItem> Merge(IEnumerable<MergedSource> sources)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<MergedSource>>(StringComparer.Ordinal);

        if (sources != null)
        {
            foreach (var source in sources)
            {
                if (source?.Item == null || string.IsNullOrEmpty(source.Item.Id)) continue;
                var key = KeyOf(source.Item);
                if (!grouped.TryGetValue(key, out var bucket))
                {
                    bucket = new List<MergedSource>();
                    grouped[key] = bucket;
                    order.Add(key);
                }
                bucket.Add(source);
            }
        }

        var merged = new List<MergedItem>(order.Count);
        foreach (var key in order)
        {
            var bucket = grouped[key];
            merged.Add(new MergedItem(PickPrimary(bucket), bucket));
        }
        return merged;
    }

    /// <summary>
    /// primary 取舍：**已看 &gt; 有进度 &gt; 首个来源**。
    /// 这样默认播放的是「用户真正在看的那一份」，与续播体验一致。
    /// </summary>
    public static EmbyItem PickPrimary(IReadOnlyList<MergedSource> sources)
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

    // ── 便于外壳调用的便捷面（保持同一去重键）───────────────────────────────

    /// <summary>按「服务器 + 该服条目列表」批量合并（多服聚合页的主入口）。</summary>
    public List<MergedItem> MergeByServer(IEnumerable<KeyValuePair<ServerConfig, IEnumerable<EmbyItem>>> byServer)
    {
        var flat = new List<MergedSource>();
        if (byServer != null)
        {
            foreach (var pair in byServer)
            {
                if (pair.Value == null) continue;
                foreach (var item in pair.Value)
                {
                    if (item == null) continue;
                    flat.Add(new MergedSource(pair.Key, item));
                }
            }
        }
        return Merge(flat);
    }

    /// <summary>合并后按来源数降序、首次出现顺序稳定的排序副本（多源优先展示）。</summary>
    public List<MergedItem> SortBySourceCount(IEnumerable<MergedItem> items)
    {
        var list = new List<MergedItem>();
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null) list.Add(item);
            }
        }

        var indexed = new List<KeyValuePair<int, MergedItem>>(list.Count);
        for (var i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, MergedItem>(i, list[i]));
        indexed.Sort((a, b) =>
        {
            var byCount = b.Value.Sources.Count.CompareTo(a.Value.Sources.Count);
            return byCount != 0 ? byCount : a.Key.CompareTo(b.Key);
        });

        var sorted = new List<MergedItem>(indexed.Count);
        foreach (var entry in indexed) sorted.Add(entry.Value);
        return sorted;
    }

    /// <summary>本地乐观更新：把某个 (itemId, serverId) 的 UserData 替换掉（跨服同步后的即时反馈）。</summary>
    public List<MergedItem> PatchUserData(IEnumerable<MergedItem> items, string serverId, string itemId, EmbyUserData data)
    {
        var result = new List<MergedItem>();
        if (items == null) return result;

        foreach (var merged in items)
        {
            if (merged == null) continue;
            var changed = false;
            var sources = new List<MergedSource>(merged.Sources.Count);
            foreach (var source in merged.Sources)
            {
                var matchesServer = string.IsNullOrEmpty(serverId) || source.Server?.Id == serverId;
                if (matchesServer && source.Item != null && string.Equals(source.Item.Id, itemId, StringComparison.Ordinal))
                {
                    sources.Add(new MergedSource(source.Server, source.Item.WithUserData(data)));
                    changed = true;
                }
                else
                {
                    sources.Add(source);
                }
            }
            result.Add(changed ? new MergedItem(PickPrimary(sources), sources) : merged);
        }
        return result;
    }
}

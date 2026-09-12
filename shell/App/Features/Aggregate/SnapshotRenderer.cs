// t31 / SEAM③：把**快照**渲染成行与卡片（收藏屏与聚合视界共用）。
//
// 为什么独立一个文件：两屏的行/卡片构造逻辑必须**同构**，否则"缓存态"与"刷新态"会长出两套观感
// （ 要求 T1 就地把 T0 的内容替换掉，两套渲染必然导致闪变）。
//
// ⚠️ 本渲染器接受 `SnapshotItem`（白名单快照）⇒ 据此渲染的卡片**不能直接起播**（缺 `Raw`/`MediaSources`）。
//    起播由页面用 `LiveItemFor(serverId, itemId)` 拿**刷新后的真条目**完成（见各页 PlayAsync 入口）。

using System;
using System.Collections.Generic;
using System.Linq;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>快照 → 屏幕行/卡片。</summary>
public static class SnapshotRenderer
{
    /// <summary>按服务器分组渲染（聚合视界）。</summary>
    public static List<AggregateRow> ToGroupedRows(
        MediaSnapshot snapshot,
        List<ServerConfig> servers,
        bool showProgress)
    {
        var rows = new List<AggregateRow>();
        if (snapshot == null)
        {
            return rows;
        }

        foreach (var group in snapshot.Groups ?? new List<SnapshotGroup>())
        {
            var server = Resolve(servers, group.ServerId);
            var posters = new List<AggregatePoster>();
            foreach (var item in group.Items ?? new List<SnapshotItem>())
            {
                posters.Add(ToPoster(item, server, showProgress));
            }

            // 行头沿用快照里存的显示名（含 ServerId 短号）⇒ 缓存态与刷新态的行头一致
            rows.Add(new AggregateRow(
                new MediaGroupResult(group.ServerId, group.ServerName ?? group.ServerId, new List<EmbyItem>(), null),
                posters));
        }

        return rows;
    }

    /// <summary>
    /// 收藏屏的三个分区（**收藏的电影 / 收藏的剧 / 收藏的集**，按条目 `Type` 分；不是按服务器分）。
    /// 三个分区**恒定存在**：某一类为 0 张时也保留该分区（显示 0），用户才能分清"这一类没有"与"这屏坏了"。
    /// 未知类型（取数只请求三类，出现别的说明服务端返回了意料外的东西）**归入「收藏的剧」而不是丢弃** ——
    /// 宁可让它在界面上可见，也不能静默吞掉。
    /// </summary>
    public static List<AggregateRow> ToFavoriteSections(
        MediaSnapshot snapshot,
        List<ServerConfig> servers,
        out int movieCount,
        out int seriesCount,
        out int episodeCount)
    {
        var movies = new List<AggregatePoster>();
        var series = new List<AggregatePoster>();
        var episodes = new List<AggregatePoster>();

        foreach (var group in snapshot?.Groups ?? new List<SnapshotGroup>())
        {
            var server = Resolve(servers, group.ServerId);
            foreach (var item in group.Items ?? new List<SnapshotItem>())
            {
                var poster = ToPoster(item, server, showProgress: false);
                if (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase))
                {
                    movies.Add(poster);
                }
                else if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
                {
                    episodes.Add(poster);
                }
                else
                {
                    series.Add(poster);
                }
            }
        }

        movieCount = movies.Count;
        seriesCount = series.Count;
        episodeCount = episodes.Count;

        return new List<AggregateRow>
        {
            new AggregateRow(new MediaGroupResult("section-movies", "收藏的电影", new List<EmbyItem>(), null), movies),
            new AggregateRow(new MediaGroupResult("section-series", "收藏的剧", new List<EmbyItem>(), null), series),
            new AggregateRow(new MediaGroupResult("section-episodes", "收藏的集", new List<EmbyItem>(), null), episodes),
        };
    }

    /// <summary>一个快照条目 → 一张卡（封面 URL 由 `EmbyService.ImageUrl` 现算，快照只存"有没有图"）。</summary>
    public static AggregatePoster ToPoster(SnapshotItem item, ServerConfig server, bool showProgress)
    {
        var embyItem = item.ToEmbyItem();
        var emby = server == null
            ? null
            : new EmbyService(new ShellHttpClient(), server, onLog: m => { });

        return new AggregatePoster(
            embyItem,
            server,
            MediaAggregator.PrimaryImageUrl(emby, embyItem),
            badgeText: AggregatePoster.UnplayedBadgeOf(embyItem),
            showProgress: showProgress,
            progressFraction: MediaAggregator.ProgressFraction(embyItem));
    }

    private static ServerConfig Resolve(List<ServerConfig> servers, string serverId)
    {
        if (servers == null || string.IsNullOrEmpty(serverId))
        {
            return null;
        }

        foreach (var server in servers)
        {
            if (string.Equals(server.Id, serverId, StringComparison.Ordinal))
            {
                return server;
            }
        }

        return null;
    }

    /// <summary>从快照算"内容是否变化"（比较 id 序列）—— 变了才重绑，避免 T1 替换时闪屏/重置滚动。</summary>
    public static bool SameIds(IEnumerable<string> a, IEnumerable<string> b)
    {
        var left = (a ?? Enumerable.Empty<string>()).ToList();
        var right = (b ?? Enumerable.Empty<string>()).ToList();
        return left.Count == right.Count && left.SequenceEqual(right, StringComparer.Ordinal);
    }
}

// 对应 DESIGN §4.2 `core/models/playback_item.dart` → `Services/Models/PlaybackItem.cs`。
// ⚠️ 三态标注：`rebuild/ai_player/lib/core/models/playback_item.dart` **在 rebuild 树中不存在**（原版文件），
// 故本文件属**重建实现**：字段依据 SERVICE_API §1.2/§1.3（Emby 条目、MediaSources、取流）与
// HostView/播放会话实际消费的字段面（`Services/Player/EmbyPlaybackSession.cs`）归纳而来，未逐字段实证处标「未实证」。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>「要播放的一条」的载体：把服务端条目 + 选定的媒体源/轨道 + 起步位置收敛为一个对象。</summary>
public sealed class PlaybackItem
{
    /// <summary>来源服务器（用于回传进度、收藏、跳过等）。</summary>
    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public ServerKind ServerKind { get; set; } = ServerKind.Emby;

    /// <summary>服务端条目 id。</summary>
    public string ItemId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>条目类型（<c>Movie</c>/<c>Episode</c>/<c>Audio</c>…）。</summary>
    public string Type { get; set; } = string.Empty;

    // ── 剧集上下文（内核 --episode-list=/--next-episode-id= 等参数用）────────
    public string SeriesId { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }

    // ── 媒体源与轨道 ───────────────────────────────────────────────────────
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>可选：条目的全部媒体源（多版本 ⇒ 内核 <c>--version-option=</c>）。</summary>
    public List<EmbyMediaSource> MediaSources { get; set; } = new List<EmbyMediaSource>();

    public int AudioStreamIndex { get; set; } = -1;

    public int SubtitleStreamIndex { get; set; } = -1;

    // ── 位置 ───────────────────────────────────────────────────────────────
    /// <summary>起步位置（秒）；续播时来自 <see cref="EmbyUserData.ResumeTicks"/>。</summary>
    public double StartSeconds { get; set; }

    public double DurationSeconds { get; set; }

    // ── 本地文件 vs 远端流 ─────────────────────────────────────────────────
    /// <summary>本地视频（不经服务器，直接喂内核 <c>--open=</c>）。</summary>
    public bool IsLocalFile { get; set; }

    public string FilePath { get; set; } = string.Empty;

    /// <summary>已解析好的播放地址（由 <c>PlaybackUrlResolver</c> 产出）。</summary>
    public string StreamUrl { get; set; } = string.Empty;

    public string PosterUrl { get; set; } = string.Empty;

    public string BackdropUrl { get; set; } = string.Empty;

    public EmbyUserData UserData { get; set; } = new EmbyUserData();

    public bool IsEpisode => Type == "Episode";

    public bool HasMultipleSources => MediaSources != null && MediaSources.Count > 1;

    /// <summary>当前选定的媒体源；未指定时取第一个（未实证：原版可能另有偏好规则）。</summary>
    public EmbyMediaSource CurrentSource
    {
        get
        {
            if (MediaSources == null || MediaSources.Count == 0) return null;
            if (!string.IsNullOrEmpty(MediaSourceId))
            {
                var found = MediaSources.FirstOrDefault(s => s.Id == MediaSourceId);
                if (found != null) return found;
            }
            return MediaSources[0];
        }
    }

    /// <summary>等价 Dart 的 <c>S01E02</c> 显示串。</summary>
    public string EpisodeCodeText => IsEpisode ? TextUtils.EpisodeCode(SeasonNumber, EpisodeNumber) : string.Empty;

    /// <summary>由 Emby 条目 + 服务器配置构造（最常用路径）。</summary>
    public static PlaybackItem FromEmbyItem(EmbyItem item, ServerConfig server, string mediaSourceId = null)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        return new PlaybackItem
        {
            ServerId = server?.Id ?? string.Empty,
            ServerName = server?.Name ?? string.Empty,
            ServerKind = server?.Kind ?? ServerKind.Emby,
            ItemId = item.Id,
            Name = item.Name,
            Type = item.Type,
            SeriesId = item.SeriesId,
            SeriesName = item.SeriesName,
            SeasonId = item.SeasonId,
            SeasonNumber = item.ParentIndexNumber,
            EpisodeNumber = item.IndexNumber,
            MediaSourceId = mediaSourceId ?? string.Empty,
            MediaSources = item.MediaSources ?? new List<EmbyMediaSource>(),
            StartSeconds = item.UserData.PositionSeconds,
            DurationSeconds = item.DurationSeconds,
            FilePath = item.Path,
            UserData = item.UserData,
        };
    }

    /// <summary>本地视频（不经服务器）。</summary>
    public static PlaybackItem LocalFile(string path, string title = null)
    {
        return new PlaybackItem
        {
            ServerKind = ServerKind.Emby,
            ItemId = path,
            Name = title ?? System.IO.Path.GetFileName(path ?? string.Empty),
            Type = "Video",
            IsLocalFile = true,
            FilePath = path,
            StreamUrl = path,
        };
    }

    public PlaybackItem With(
        string serverId = null,
        string itemId = null,
        string name = null,
        string type = null,
        string mediaSourceId = null,
        List<EmbyMediaSource> mediaSources = null,
        int? audioStreamIndex = null,
        int? subtitleStreamIndex = null,
        double? startSeconds = null,
        double? durationSeconds = null,
        string streamUrl = null,
        string posterUrl = null,
        string backdropUrl = null,
        EmbyUserData userData = null)
    {
        var c = (PlaybackItem)MemberwiseClone();
        if (serverId != null) c.ServerId = serverId;
        if (itemId != null) c.ItemId = itemId;
        if (name != null) c.Name = name;
        if (type != null) c.Type = type;
        if (mediaSourceId != null) c.MediaSourceId = mediaSourceId;
        if (mediaSources != null) c.MediaSources = mediaSources;
        if (audioStreamIndex.HasValue) c.AudioStreamIndex = audioStreamIndex.Value;
        if (subtitleStreamIndex.HasValue) c.SubtitleStreamIndex = subtitleStreamIndex.Value;
        if (startSeconds.HasValue) c.StartSeconds = startSeconds.Value;
        if (durationSeconds.HasValue) c.DurationSeconds = durationSeconds.Value;
        if (streamUrl != null) c.StreamUrl = streamUrl;
        if (posterUrl != null) c.PosterUrl = posterUrl;
        if (backdropUrl != null) c.BackdropUrl = backdropUrl;
        if (userData != null) c.UserData = userData;
        return c;
    }

    /// <summary>落盘形态（用于「继续观看」本地缓存；Emby 侧真值仍在服务端）。</summary>
    public System.Text.Json.Nodes.JsonObject ToJson()
    {
        var o = new System.Text.Json.Nodes.JsonObject
        {
            ["serverId"] = ServerId,
            ["serverName"] = ServerName,
            ["serverKind"] = ServerKind.Id(),
            ["itemId"] = ItemId,
            ["name"] = Name,
            ["type"] = Type,
            ["seriesId"] = SeriesId,
            ["seasonId"] = SeasonId,
            ["mediaSourceId"] = MediaSourceId,
            ["startSeconds"] = StartSeconds,
            ["durationSeconds"] = DurationSeconds,
            ["isLocalFile"] = IsLocalFile,
            ["filePath"] = FilePath,
            ["streamUrl"] = StreamUrl,
        };
        if (SeasonNumber.HasValue) o["seasonNumber"] = SeasonNumber.Value;
        if (EpisodeNumber.HasValue) o["episodeNumber"] = EpisodeNumber.Value;
        return o;
    }

    public override string ToString() => $"PlaybackItem({Type} {Name} @{ServerName}, {StartSeconds:F1}/{DurationSeconds:F1}s)";
}

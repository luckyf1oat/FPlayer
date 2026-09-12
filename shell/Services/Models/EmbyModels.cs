// 等价移植：rebuild/ai_player/lib/core/models/emby_models.dart（字段名忠实于服务端 JSON）。
// 端点与参数依据：reversed/FlutterApp/SERVICE_API.md §1（[E]/[S] 实证）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

public sealed class EmbyUserData
{
    public bool Played { get; set; }
    public long PositionTicks { get; set; }
    public long PlaybackPositionTicks { get; set; }
    public bool IsFavorite { get; set; }
    public double PlayedPercentage { get; set; }
    public int? UnplayedItemCount { get; set; }

    /// <summary>续播点：优先 <c>PlaybackPositionTicks</c>，回落 <c>PositionTicks</c>。</summary>
    public long ResumeTicks => PlaybackPositionTicks > 0 ? PlaybackPositionTicks : PositionTicks;

    public double PositionSeconds => TextUtils.TicksToSeconds(ResumeTicks);

    public static EmbyUserData FromJson(JsonElement? json) => new EmbyUserData
    {
        Played = JsonRead.Bool(json, "Played"),
        PositionTicks = JsonRead.LongOrNull(json, "PositionTicks") ?? 0,
        PlaybackPositionTicks = JsonRead.LongOrNull(json, "PlaybackPositionTicks") ?? 0,
        IsFavorite = JsonRead.Bool(json, "IsFavorite"),
        PlayedPercentage = JsonRead.Double(json, "PlayedPercentage"),
        UnplayedItemCount = JsonRead.IntOrNull(json, "UnplayedItemCount"),
    };

    public EmbyUserData With(bool? played = null, long? positionTicks = null, long? playbackPositionTicks = null,
        bool? isFavorite = null, double? playedPercentage = null, int? unplayedItemCount = null)
        => new EmbyUserData
        {
            Played = played ?? Played,
            PositionTicks = positionTicks ?? PositionTicks,
            PlaybackPositionTicks = playbackPositionTicks ?? PlaybackPositionTicks,
            IsFavorite = isFavorite ?? IsFavorite,
            PlayedPercentage = playedPercentage ?? PlayedPercentage,
            UnplayedItemCount = unplayedItemCount ?? UnplayedItemCount,
        };

    /// <summary>等价 Dart 的 <c>toJson()</c>（UnplayedItemCount 为 null 时不下发）。</summary>
    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["Played"] = Played,
            ["PositionTicks"] = PositionTicks,
            ["PlaybackPositionTicks"] = PlaybackPositionTicks,
            ["IsFavorite"] = IsFavorite,
            ["PlayedPercentage"] = PlayedPercentage,
        };
        if (UnplayedItemCount.HasValue) o["UnplayedItemCount"] = UnplayedItemCount.Value;
        return o;
    }
}

public sealed class EmbyMediaStream
{
    public int Index { get; set; } = -1;

    /// <summary><c>Video</c> / <c>Audio</c> / <c>Subtitle</c> / <c>EmbeddedImage</c>。</summary>
    public string Type { get; set; } = string.Empty;

    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsForced { get; set; }
    public bool IsExternal { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Height { get; set; }
    public int Width { get; set; }
    public int Channels { get; set; }
    public long BitRate { get; set; }
    public string DeliveryUrl { get; set; } = string.Empty;
    public bool IsTextSubtitle { get; set; } = true;

    public bool IsAudio => Type == "Audio";
    public bool IsSubtitle => Type == "Subtitle" || Type == "EmbeddedImage";
    public bool IsVideo => Type == "Video";

    /// <summary>轨道显示名（优先 <c>DisplayTitle</c>，回落「语言 · 标题 · 编码 · 声道」）。</summary>
    public string Label
    {
        get
        {
            if ((DisplayTitle ?? string.Empty).Trim().Length > 0) return DisplayTitle.Trim();
            var parts = new List<string>();
            if ((Language ?? string.Empty).Trim().Length > 0) parts.Add(Language.Trim());
            if ((Title ?? string.Empty).Trim().Length > 0 && Title != "Undefined") parts.Add(Title.Trim());
            if ((Codec ?? string.Empty).Trim().Length > 0) parts.Add(Codec.ToUpperInvariant());
            if (Channels > 0) parts.Add($"{Channels} 声道");
            return parts.Count == 0 ? $"轨道 {Index + 1}" : string.Join(" · ", parts);
        }
    }

    public static EmbyMediaStream FromJson(JsonElement json) => new EmbyMediaStream
    {
        Index = JsonRead.IntOrNull(json, "Index") ?? -1,
        Type = JsonRead.Str(json, "Type"),
        Codec = JsonRead.Str(json, "Codec"),
        Language = JsonRead.Str(json, "Language"),
        DisplayTitle = JsonRead.Str(json, "DisplayTitle"),
        Title = JsonRead.Str(json, "Title"),
        IsDefault = JsonRead.Bool(json, "IsDefault"),
        IsForced = JsonRead.Bool(json, "IsForced"),
        IsExternal = JsonRead.Bool(json, "IsExternal"),
        Path = JsonRead.Str(json, "Path"),
        Height = JsonRead.Int(json, "Height"),
        Width = JsonRead.Int(json, "Width"),
        Channels = JsonRead.Int(json, "Channels"),
        BitRate = JsonRead.LongOrNull(json, "BitRate") ?? 0,
        DeliveryUrl = JsonRead.Str(json, "DeliveryUrl"),
        IsTextSubtitle = JsonRead.Bool(json, "IsTextSubtitle", true),
    };
}

public sealed class EmbyMediaSource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public long Size { get; set; }
    public long Bitrate { get; set; }
    public long RunTimeTicks { get; set; }
    public bool SupportsDirectPlay { get; set; }
    public bool SupportsDirectStream { get; set; }
    public string DirectStreamUrl { get; set; } = string.Empty;
    public string TranscodingUrl { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public string Protocol { get; set; } = "File";
    public List<EmbyMediaStream> MediaStreams { get; set; } = new List<EmbyMediaStream>();

    public List<EmbyMediaStream> AudioStreams => MediaStreams.Where(s => s.IsAudio).ToList();

    public List<EmbyMediaStream> SubtitleStreams => MediaStreams.Where(s => s.IsSubtitle).ToList();

    public double DurationSeconds => TextUtils.TicksToSeconds(RunTimeTicks);

    /// <summary>版本展示名 —— 多 <c>MediaSource</c> 即内核 <c>--version-option=</c> 的来源。</summary>
    public string VersionLabel
    {
        get
        {
            var parts = new List<string>();
            if ((Name ?? string.Empty).Trim().Length > 0) parts.Add(Name.Trim());
            if ((Container ?? string.Empty).Trim().Length > 0) parts.Add(Container.ToUpperInvariant());
            if (Bitrate > 0) parts.Add(TextUtils.FormatBitrate(Bitrate));
            return parts.Count == 0 ? $"版本 {Id}" : string.Join(" · ", parts);
        }
    }

    public static EmbyMediaSource FromJson(JsonElement json) => new EmbyMediaSource
    {
        Id = JsonRead.Str(json, "Id"),
        Name = JsonRead.Str(json, "Name"),
        Path = JsonRead.Str(json, "Path"),
        Container = JsonRead.Str(json, "Container"),
        Size = JsonRead.LongOrNull(json, "Size") ?? 0,
        Bitrate = JsonRead.LongOrNull(json, "Bitrate") ?? 0,
        RunTimeTicks = JsonRead.LongOrNull(json, "RunTimeTicks") ?? 0,
        SupportsDirectPlay = JsonRead.Bool(json, "SupportsDirectPlay"),
        SupportsDirectStream = JsonRead.Bool(json, "SupportsDirectStream"),
        DirectStreamUrl = JsonRead.Str(json, "DirectStreamUrl"),
        TranscodingUrl = JsonRead.Str(json, "TranscodingUrl"),
        ETag = JsonRead.Str(json, "ETag"),
        Protocol = JsonRead.Str(json, "Protocol", "File"),
        MediaStreams = JsonRead.Objects(JsonRead.Items(json, "MediaStreams"))
            .Select(EmbyMediaStream.FromJson).ToList(),
    };
}

/// <summary>条目（电影/剧集/季/集/专辑/音频/文件夹…）。</summary>
public sealed class EmbyItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SeriesId { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public int? IndexNumber { get; set; }
    public int? ParentIndexNumber { get; set; }
    public string Overview { get; set; } = string.Empty;
    public int? ProductionYear { get; set; }
    public string PremiereDate { get; set; }
    public long RunTimeTicks { get; set; }
    public string Container { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public int? ChildCount { get; set; }
    public int? RecursiveItemCount { get; set; }
    public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public List<string> BackdropImageTags { get; set; } = new List<string>();
    public string ParentBackdropItemId { get; set; } = string.Empty;

    /// <summary>
    /// 父条目的**背景图 tag 列表**（Emby <c>ParentBackdropImageTags</c>，t76 同批追加）。自家没有 Backdrop 时，
    /// 背景图常挂在父条目上（`DetailPage` 用 <see cref="ParentBackdropItemId"/> 取 backdrop）⇒ 取图要连 tag 一起换 id，
    /// 否则就是"自家 id + 别人的 tag"或不带 tag（实测会 404/500）。空列表 ⇒ 视为"父也没有可取的背景图"。
    /// </summary>
    public List<string> ParentBackdropImageTags { get; set; } = new List<string>();
    public double PrimaryImageAspectRatio { get; set; }
    public EmbyUserData UserData { get; set; } = new EmbyUserData();
    public List<EmbyMediaSource> MediaSources { get; set; } = new List<EmbyMediaSource>();
    public Dictionary<string, string> ProviderIds { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string AlbumId { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new List<string>();
    public string CollectionType { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new List<string>();
    public double? CommunityRating { get; set; }
    public string OfficialRating { get; set; } = string.Empty;
    public string SeriesPrimaryImageTag { get; set; } = string.Empty;

    /// <summary>
    /// 剧的**完结日期**（Emby <c>EndDate</c>，t76 新增）：**服务端原文**（形如 <c>2011-05-08</c>），没有该字段 ⇒ <c>null</c>。
    /// 用途（ui 的 t79）：首页卡片第二行"年份区间" —— 连载中画 `<首播年>-现在`、跨年完结画 `<首播年>-<结束年>`、同年/缺失画单年。
    /// </summary>
    public string EndDate { get; set; }

    /// <summary>
    /// 剧的**播出状态**（Emby <c>Status</c>，t76 新增；实测值 <c>Continuing</c> / <c>Ended</c>），没有 ⇒ <c>null</c>。
    /// 与 <see cref="EndDate"/> 配合判定上面那三态。
    /// </summary>
    public string SeriesStatus { get; set; }

    /// <summary>原始 JSON（供未知字段透传）。</summary>
    public JsonObject Raw { get; set; } = new JsonObject();

    public bool IsEpisode => Type == "Episode";
    public bool IsSeries => Type == "Series";
    public bool IsSeason => Type == "Season";
    public bool IsMovie => Type == "Movie";
    public bool IsAudio => Type == "Audio";
    public bool IsContainer => IsFolder || Type == "Folder" || Type == "CollectionFolder";
    public bool IsBoxSet => Type == "BoxSet";
    public bool IsPerson => Type == "Person";

    private static readonly HashSet<string> PlayableTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Movie", "Episode", "Video", "Audio", "MusicVideo",
    };

    public bool IsPlayable => PlayableTypes.Contains(Type);

    public double DurationSeconds => TextUtils.TicksToSeconds(RunTimeTicks);

    /// <summary><c>S01E02</c>（非剧集返回空串）。</summary>
    public string EpisodeCodeText => IsEpisode ? TextUtils.EpisodeCode(ParentIndexNumber, IndexNumber) : string.Empty;

    /// <summary>副标题（映射内核 <c>--subtitle=</c>）：剧集 <c>S01E02</c>，电影为年份。</summary>
    public string SubtitleText
    {
        get
        {
            if (IsEpisode)
            {
                var code = EpisodeCodeText;
                return code.Length == 0 ? SeriesName : $"{SeriesName}  {code}";
            }
            if (ProductionYear.HasValue) return ProductionYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.Empty;
        }
    }

    /// <summary>海报 tag（剧集回落到剧的海报 —— 原版同样打法）。</summary>
    public string PrimaryImageTag
    {
        get
        {
            if (ImageTags.TryGetValue("Primary", out var tag) && !string.IsNullOrEmpty(tag)) return tag;
            return IsEpisode ? SeriesPrimaryImageTag : string.Empty;
        }
    }

    public string ImdbId => ProviderIds.TryGetValue("Imdb", out var v) ? v : null;
    public string TmdbId => ProviderIds.TryGetValue("Tmdb", out var v) ? v : null;
    public string TvdbId => ProviderIds.TryGetValue("Tvdb", out var v) ? v : null;

    /// <summary>仅 <c>userData</c> 变化的新实例（收藏/已看就地更新，避免整表重取）。</summary>
    public EmbyItem WithUserData(EmbyUserData data)
    {
        var clone = (EmbyItem)MemberwiseClone();
        clone.UserData = data;
        var raw = Raw == null ? new JsonObject() : (JsonObject)Raw.DeepClone();
        raw["UserData"] = data.ToJson();
        clone.Raw = raw;
        return clone;
    }

    public static EmbyItem FromJson(JsonElement json) => new EmbyItem
    {
        Id = JsonRead.Str(json, "Id"),
        Name = JsonRead.Str(json, "Name"),
        Type = JsonRead.Str(json, "Type"),
        SeriesId = JsonRead.Str(json, "SeriesId"),
        SeriesName = JsonRead.Str(json, "SeriesName"),
        SeasonId = JsonRead.Str(json, "SeasonId"),
        SeasonName = JsonRead.Str(json, "SeasonName"),
        IndexNumber = JsonRead.IntOrNull(json, "IndexNumber"),
        ParentIndexNumber = JsonRead.IntOrNull(json, "ParentIndexNumber"),
        Overview = JsonRead.Str(json, "Overview"),
        ProductionYear = JsonRead.IntOrNull(json, "ProductionYear"),
        PremiereDate = JsonRead.Prop(json, "PremiereDate")?.ValueKind == JsonValueKind.String
            ? JsonRead.Str(json, "PremiereDate")
            : null,
        RunTimeTicks = JsonRead.LongOrNull(json, "RunTimeTicks") ?? 0,
        Container = JsonRead.Str(json, "Container"),
        Path = JsonRead.Str(json, "Path"),
        IsFolder = JsonRead.Bool(json, "IsFolder"),
        ChildCount = JsonRead.IntOrNull(json, "ChildCount"),
        RecursiveItemCount = JsonRead.IntOrNull(json, "RecursiveItemCount"),
        ImageTags = JsonRead.StrMap(json, "ImageTags"),
        BackdropImageTags = JsonRead.StrList(json, "BackdropImageTags"),
        ParentBackdropItemId = JsonRead.Str(json, "ParentBackdropItemId"),
        ParentBackdropImageTags = JsonRead.StrList(json, "ParentBackdropImageTags"),
        PrimaryImageAspectRatio = JsonRead.Double(json, "PrimaryImageAspectRatio"),
        UserData = EmbyUserData.FromJson(JsonRead.Prop(json, "UserData")),
        MediaSources = JsonRead.Objects(JsonRead.Items(json, "MediaSources"))
            .Select(EmbyMediaSource.FromJson).ToList(),
        ProviderIds = JsonRead.StrMap(json, "ProviderIds"),
        AlbumId = JsonRead.Str(json, "AlbumId"),
        AlbumArtist = JsonRead.Str(json, "AlbumArtist"),
        Artists = JsonRead.StrList(json, "Artists"),
        CollectionType = JsonRead.Str(json, "CollectionType"),
        Genres = JsonRead.StrList(json, "Genres"),
        CommunityRating = JsonRead.DoubleOrNull(json, "CommunityRating"),
        OfficialRating = JsonRead.Str(json, "OfficialRating"),
        SeriesPrimaryImageTag = JsonRead.Str(json, "SeriesPrimaryImageTag"),
        // t76：服务端没有该键/空串 ⇒ 保留 null（"没有这个信息" ≠ "空字符串"）
        EndDate = JsonRead.Prop(json, "EndDate")?.ValueKind == JsonValueKind.String ? EmbyTextNormalize.NullIfEmpty(JsonRead.Str(json, "EndDate")) : null,
        SeriesStatus = JsonRead.Prop(json, "Status")?.ValueKind == JsonValueKind.String ? EmbyTextNormalize.NullIfEmpty(JsonRead.Str(json, "Status")) : null,
        Raw = JsonRead.DeepClone(json),
    };
}

/// <summary>空串 ⇒ <c>null</c>（t76：`EndDate`/`SeriesStatus` 的"没有该信息"用 null 表达）。</summary>
internal static class EmbyTextNormalize
{
    internal static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;
}

/// <summary><c>GET /Users/{userId}/Items</c> 等列表型响应。</summary>
public sealed class EmbyQueryResult
{
    public List<EmbyItem> Items { get; set; } = new List<EmbyItem>();
    public int TotalRecordCount { get; set; }

    /// <summary>
    /// ⚠️ **旧判据，勿用**（t140 review 的 I-2 / t148 收口）：`Items.Count &lt; TotalRecordCount` 在**搜索路径上恒为 false**
    /// （该服务端在任何带 `SearchTerm` 的查询上一律回 `TotalRecordCount=0`，见 t131 的 9 条真端点读数）。
    /// 请改用单点判据 <c>EmbyService.HasMore(result, requestedLimit)</c>（t131 收敛点）。
    /// 保留属性是因为它在 `t131` 之前已公开；**调用点当刻为 0**（Services 与 App 实测），加 `[Obsolete]` 只为防将来引错。
    /// </summary>
    [System.Obsolete("旧判据在搜索路径恒 false；请用 EmbyService.HasMore(result, requestedLimit)（t131 单点）")]
    public bool HasMore => Items.Count < TotalRecordCount;

    public static EmbyQueryResult FromJson(JsonElement json) => new EmbyQueryResult
    {
        Items = JsonRead.Objects(JsonRead.Items(json, "Items")).Select(EmbyItem.FromJson).ToList(),
        TotalRecordCount = JsonRead.IntOrNull(json, "TotalRecordCount") ?? 0,
    };
}

/// <summary><c>GET /Users/{userId}/Views</c> 里的库。</summary>
public sealed class EmbyUserView
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary><c>movies</c> / <c>tvshows</c> / <c>music</c> / <c>books</c> / <c>boxsets</c> / <c>playlists</c>…</summary>
    public string CollectionType { get; set; } = string.Empty;

    public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public static EmbyUserView FromJson(JsonElement json) => new EmbyUserView
    {
        Id = JsonRead.Str(json, "Id"),
        Name = JsonRead.Str(json, "Name"),
        CollectionType = JsonRead.Str(json, "CollectionType"),
        ImageTags = JsonRead.StrMap(json, "ImageTags"),
    };
}

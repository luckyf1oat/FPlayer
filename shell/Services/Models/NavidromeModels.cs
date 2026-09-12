// 等价移植：rebuild/ai_player/lib/core/models/navidrome_models.dart（298 行 Dart → C#，逐字段翻译）。
// 端点与参数依据：reversed/FlutterApp/SERVICE_API.md §2「Navidrome / Subsonic」（[S]/[E]/[H] 实证）：
//   兼容层 /rest/{method}、getAlbumList2、getStarred2、getPlaylists、getPlaylist、getCoverArt、
//   getLyricsBySongId、search3、star、scrobble、stream；参数 u/token/salt/f=json/v=1.16.x/c。
// 命名约定：Dart 类名直接沿用（`Subsonic*` 前缀即 Dart 侧原名，对应服务端 Subsonic 兼容层，
//   不是「Navidrome 原生 API」的模型）。
// 容错策略（依 JsonRead 的宽松语义，等价 Dart 的 `'${json['X'] ?? ''}'` / `(json['X'] as num?)?.toInt()`）：
//   字段缺失/类型漂移（string↔number）不抛异常；未在 SERVICE_API.md §2 逐字段实证 ⇒ 一律容错解析。
// 数值宽度：Subsonic 的 duration/size/bitRate/year 用 64 位读取（JsonRead.LongOrNull），
//   防止大文件 size（>2^31）静默截断。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>艺术家（对应 Dart <c>SubsonicArtist</c>）。</summary>
public sealed class SubsonicArtist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long AlbumCount { get; set; }

    /// <summary>封面图 id（喂 <c>/rest/getCoverArt?id=…</c>）。</summary>
    public string CoverArt { get; set; } = string.Empty;

    /// <summary>艺术家头像 URL（Navidrome 扩展字段；未实证，容错解析）。</summary>
    public string ArtistImageUrl { get; set; } = string.Empty;

    /// <summary>等价 Dart <c>json['starred'] != null</c>：只看存在性，不解析值。</summary>
    public bool Starred { get; set; }

    /// <summary>非空重载：便于 <c>.Select(SubsonicArtist.FromJson)</c> 的方法组转换（JsonElement? 不能匹配 Func&lt;JsonElement,T&gt;）。</summary>
    public static SubsonicArtist FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicArtist FromJson(JsonElement? json) => new SubsonicArtist
    {
        Id = JsonRead.Str(json, "id"),
        Name = JsonRead.Str(json, "name"),
        AlbumCount = JsonRead.LongOrNull(json, "albumCount") ?? 0,
        CoverArt = JsonRead.Str(json, "coverArt"),
        ArtistImageUrl = JsonRead.Str(json, "artistImageUrl"),
        Starred = HasKey(json, "starred"),
    };

    internal static bool HasKey(JsonElement? json, string name)
        => json.HasValue && json.Value.ValueKind == JsonValueKind.Object && json.Value.TryGetProperty(name, out _);
}

/// <summary>专辑（对应 Dart <c>SubsonicAlbum</c>）。</summary>
public sealed class SubsonicAlbum
{
    public string Id { get; set; } = string.Empty;

    /// <summary>等价 Dart：<c>json['name'] ?? json['album']</c>（getAlbumList2 用 name，getAlbum 用 album）。</summary>
    public string Name { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;
    public string ArtistId { get; set; } = string.Empty;
    public string CoverArt { get; set; } = string.Empty;
    public long SongCount { get; set; }

    /// <summary>总时长（秒）。</summary>
    public long Duration { get; set; }

    public long? Year { get; set; }
    public string Genre { get; set; } = string.Empty;

    /// <summary>Subsonic 的 <c>played</c> 是时间戳串（未实证具体格式，原样保留）。</summary>
    public string Played { get; set; } = string.Empty;

    public string Created { get; set; } = string.Empty;
    public bool Starred { get; set; }

    /// <summary>非空重载：便于 <c>.Select(SubsonicAlbum.FromJson)</c>。</summary>
    public static SubsonicAlbum FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicAlbum FromJson(JsonElement? json)
    {
        var name = JsonRead.Str(json, "name");
        if (name.Length == 0) name = JsonRead.Str(json, "album");
        return new SubsonicAlbum
        {
            Id = JsonRead.Str(json, "id"),
            Name = name,
            Artist = JsonRead.Str(json, "artist"),
            ArtistId = JsonRead.Str(json, "artistId"),
            CoverArt = JsonRead.Str(json, "coverArt"),
            SongCount = JsonRead.LongOrNull(json, "songCount") ?? 0,
            Duration = JsonRead.LongOrNull(json, "duration") ?? 0,
            Year = JsonRead.LongOrNull(json, "year"),
            Genre = JsonRead.Str(json, "genre"),
            Played = JsonRead.Str(json, "played"),
            Created = JsonRead.Str(json, "created"),
            Starred = SubsonicArtist.HasKey(json, "starred"),
        };
    }
}

/// <summary>歌曲（对应 Dart <c>SubsonicSong</c>）。</summary>
public sealed class SubsonicSong
{
    public string Id { get; set; } = string.Empty;

    /// <summary>等价 Dart：<c>json['title'] ?? json['name']</c>（getPlaylist 的 entry 用 title，部分端用 name）。</summary>
    public string Title { get; set; } = string.Empty;

    public string Album { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ArtistId { get; set; } = string.Empty;
    public string CoverArt { get; set; } = string.Empty;

    /// <summary>时长（秒）。</summary>
    public long Duration { get; set; }

    public int? Track { get; set; }
    public int? DiscNumber { get; set; }
    public long? Year { get; set; }
    public string Genre { get; set; } = string.Empty;

    /// <summary>容器后缀（<c>mp3</c>/<c>flac</c>/…）。</summary>
    public string Suffix { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>文件字节数 —— 等价 Dart <c>int size</c>，必须 64 位读取以免截断。</summary>
    public long Size { get; set; }

    /// <summary>码率（kbps，Subsonic 原义）。</summary>
    public long BitRate { get; set; }

    /// <summary>服务端文件路径。</summary>
    public string Path { get; set; } = string.Empty;

    public bool Starred { get; set; }

    /// <summary>等价 Dart <c>subtitle</c>：<c>artist · album</c>（空项跳过）。</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Artist.Length > 0) parts.Add(Artist);
            if (Album.Length > 0) parts.Add(Album);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>非空重载：便于 <c>.Select(SubsonicSong.FromJson)</c>。</summary>
    public static SubsonicSong FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicSong FromJson(JsonElement? json)
    {
        var title = JsonRead.Str(json, "title");
        if (title.Length == 0) title = JsonRead.Str(json, "name");
        return new SubsonicSong
        {
            Id = JsonRead.Str(json, "id"),
            Title = title,
            Album = JsonRead.Str(json, "album"),
            AlbumId = JsonRead.Str(json, "albumId"),
            Artist = JsonRead.Str(json, "artist"),
            ArtistId = JsonRead.Str(json, "artistId"),
            CoverArt = JsonRead.Str(json, "coverArt"),
            Duration = JsonRead.LongOrNull(json, "duration") ?? 0,
            Track = JsonRead.IntOrNull(json, "track"),
            DiscNumber = JsonRead.IntOrNull(json, "discNumber"),
            Year = JsonRead.LongOrNull(json, "year"),
            Genre = JsonRead.Str(json, "genre"),
            Suffix = JsonRead.Str(json, "suffix"),
            ContentType = JsonRead.Str(json, "contentType"),
            Size = JsonRead.LongOrNull(json, "size") ?? 0,
            BitRate = JsonRead.LongOrNull(json, "bitRate") ?? 0,
            Path = JsonRead.Str(json, "path"),
            Starred = SubsonicArtist.HasKey(json, "starred"),
        };
    }
}

/// <summary>播放列表（对应 Dart <c>SubsonicPlaylist</c>）。</summary>
public sealed class SubsonicPlaylist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public long SongCount { get; set; }

    /// <summary>总时长（秒）。</summary>
    public long Duration { get; set; }

    public string Owner { get; set; } = string.Empty;
    public string CoverArt { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;

    /// <summary>Subsonic 的 <c>changed</c>（最后修改时间）。</summary>
    public string Changed { get; set; } = string.Empty;

    /// <summary>非空重载：便于 <c>.Select(SubsonicPlaylist.FromJson)</c>。</summary>
    public static SubsonicPlaylist FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicPlaylist FromJson(JsonElement? json) => new SubsonicPlaylist
    {
        Id = JsonRead.Str(json, "id"),
        Name = JsonRead.Str(json, "name"),
        Comment = JsonRead.Str(json, "comment"),
        SongCount = JsonRead.LongOrNull(json, "songCount") ?? 0,
        Duration = JsonRead.LongOrNull(json, "duration") ?? 0,
        Owner = JsonRead.Str(json, "owner"),
        CoverArt = JsonRead.Str(json, "coverArt"),
        Created = JsonRead.Str(json, "created"),
        Changed = JsonRead.Str(json, "changed"),
    };
}

/// <summary>结构化歌词（对应 Dart <c>SubsonicLyrics</c>）。</summary>
/// <remarks>
/// <c>getLyricsBySongId</c> 返回带时间轴的 <c>structuredLyrics[].line[]</c>（<c>value</c>+<c>start</c>）；
/// 无结构化数据时回落平文本（<c>value</c>）。响应外层包裹体（<c>lyricsList</c>）由调用方
/// <c>NavidromeService</c> 拆开后再喂 <see cref="FromJson"/>，与 Dart 侧同一分工。
/// </remarks>
public sealed class SubsonicLyrics
{
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>无时间轴的平文本歌词。</summary>
    public string Plain { get; set; } = string.Empty;

    public List<SubsonicLyricLine> Lines { get; set; } = new List<SubsonicLyricLine>();

    /// <summary>等价 Dart <c>isEmpty</c>：平文本空白且无时间轴行。</summary>
    public bool IsEmpty => Plain.Trim().Length == 0 && Lines.Count == 0;

    /// <summary>等价 Dart <c>toLrc()</c>：转标准 LRC（<c>[mm:ss.xx]文本</c>，行尾 <c>\n</c>，等价 Dart <c>writeln</c>）。</summary>
    public string ToLrc()
    {
        if (Lines.Count == 0) return Plain;
        var sb = new System.Text.StringBuilder();
        foreach (var line in Lines)
        {
            sb.Append('[').Append(line.Timestamp).Append(']').Append(line.Value).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// 等价 Dart <c>SubsonicLyrics.fromJson</c>：有 <c>structuredLyrics</c> 数组且非空 ⇒ 取**首个**元素，
    /// 读其 <c>line[]</c>；否则按 <c>{artist,title,value}</c> 平文本解析。
    /// </summary>
    /// <summary>非空重载：便于 <c>.Select(SubsonicLyrics.FromJson)</c>。</summary>
    public static SubsonicLyrics FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicLyrics FromJson(JsonElement? json)
    {
        var structuredFirst = JsonRead.Items(json, "structuredLyrics").FirstOrDefault();
        if (structuredFirst.ValueKind == JsonValueKind.Object)
        {
            var first = structuredFirst;
            var lines = new List<SubsonicLyricLine>();
            foreach (var item in JsonRead.Objects(JsonRead.Items(first, "line")))
            {
                lines.Add(SubsonicLyricLine.FromJson(item));
            }
            return new SubsonicLyrics
            {
                Artist = JsonRead.Str(first, "artist"),
                Title = JsonRead.Str(first, "title"),
                Lines = lines,
            };
        }

        return new SubsonicLyrics
        {
            Artist = JsonRead.Str(json, "artist"),
            Title = JsonRead.Str(json, "title"),
            Plain = JsonRead.Str(json, "value"),
        };
    }
}

/// <summary>一行带时间轴的歌词（对应 Dart <c>SubsonicLyricLine</c>）。</summary>
public sealed class SubsonicLyricLine
{
    public string Value { get; set; } = string.Empty;

    /// <summary>等价 Dart：来自 JSON 的 <c>start</c> 字段，形如 <c>mm:ss.xx</c>（原样保留，不归一化）。</summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>等价 Dart <c>startMs</c>：把 <c>mm:ss.xx</c> 解析成毫秒；不足两段或非法数字按 0。</summary>
    public int StartMs
    {
        get
        {
            var parts = (Timestamp ?? string.Empty).Split(':');
            if (parts.Length < 2) return 0;
            var minutes = int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 0;
            var seconds = double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0d;
            // 等价 Dart `(seconds * 1000).round()`（银行家舍入 → MidpointRounding.AwayFromZero 更贴近 round()）
            return (int)(minutes * 60000L + (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>非空重载：便于 <c>.Select(SubsonicLyricLine.FromJson)</c>。</summary>
    public static SubsonicLyricLine FromJson(JsonElement json) => FromJson((JsonElement?)json);

    public static SubsonicLyricLine FromJson(JsonElement? json) => new SubsonicLyricLine
    {
        Value = JsonRead.Str(json, "value"),
        Timestamp = JsonRead.Str(json, "start"),
    };
}

/// <summary><c>getAlbum</c> 的结果（专辑元数据 + 曲目）——对应 Dart <c>SubsonicAlbumWithSongs</c>。</summary>
public sealed class SubsonicAlbumWithSongs
{
    /// <summary>无 <c>album</c> 节点时为 <c>null</c>（等价 Dart 的 nullable <c>album</c>）。</summary>
    public SubsonicAlbum Album { get; set; }

    public List<SubsonicSong> Songs { get; set; } = new List<SubsonicSong>();
}

/// <summary><c>getStarred2</c> 的结果（对应 Dart <c>SubsonicStarred</c>）。</summary>
public sealed class SubsonicStarred
{
    public List<SubsonicArtist> Artists { get; set; } = new List<SubsonicArtist>();
    public List<SubsonicAlbum> Albums { get; set; } = new List<SubsonicAlbum>();
    public List<SubsonicSong> Songs { get; set; } = new List<SubsonicSong>();

    public bool IsEmpty => Artists.Count == 0 && Albums.Count == 0 && Songs.Count == 0;
}

/// <summary><c>search3</c> 的结果（对应 Dart <c>SubsonicSearchResult</c>）。</summary>
public sealed class SubsonicSearchResult
{
    public List<SubsonicArtist> Artists { get; set; } = new List<SubsonicArtist>();
    public List<SubsonicAlbum> Albums { get; set; } = new List<SubsonicAlbum>();
    public List<SubsonicSong> Songs { get; set; } = new List<SubsonicSong>();

    public bool IsEmpty => Artists.Count == 0 && Albums.Count == 0 && Songs.Count == 0;
}

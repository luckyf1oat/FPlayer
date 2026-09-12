// 等价移植：rebuild/ai_player/lib/core/models/abs_models.dart（Audiobookshelf 数据模型）。
// 端点与字段依据：reversed/FlutterApp/SERVICE_API.md §3 Audiobookshelf（`[S][E]` 实证）：
//   - 登录 POST /login 换 Bearer；GET /api/libraries、/api/libraries/{id}、/api/items/…、
//     /api/authors、/api/authors/{id}、/api/search、GET/PATCH /api/me/progress/{itemId}、
//     GET /api/me/items-in-progress、POST /api/session/{id}/play|sync|close。
//   - 进度换算：ABS 的 currentTime/duration 单位是**秒**（与内核一致，无 ticks 换算）；
//     但书籍是**多音轨**结构（media.audioFiles[]）⇒ 轨内秒数 ↔ 全书秒数的互转见 AbsTimeline.cs。
// 容错：服务端字段缺失/类型漂移（string↔number）一律走 JsonRead 宽松读取，见同目录 JsonRead.cs。
// 时间戳类字段（lastUpdate/startedAt/finishedAt）一律走 JsonRead.LongOrNull —— 64 位毫秒时间戳用 IntOrNull 会静默截断。

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.AudioBookshelf;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>登录结果（对应 Dart <c>AbsAuth</c>；token 来自 <c>POST /login</c> 的 <c>user.token</c>）。</summary>
public sealed class AbsAuth
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public static AbsAuth FromJson(JsonElement? json) => new AbsAuth
    {
        Token = JsonRead.Str(json, "token"),
        UserId = JsonRead.Str(json, "id"),
        UserName = JsonRead.Str(json, "username"),
    };

    public override string ToString() => $"AbsAuth({UserName}, user={UserId}, tokenLen={Token.Length})";
}

/// <summary>库（对应 Dart <c>AbsLibrary</c>；<c>GET /api/libraries</c>）。</summary>
public sealed class AbsLibrary
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary><c>book</c> / <c>podcast</c>；缺省 <c>book</c>（与 Dart 默认值一致）。</summary>
    public string MediaType { get; set; } = "book";

    /// <summary>库目录（<c>folders[].fullPath</c>）；缺省为空表。</summary>
    public List<string> Folders { get; set; } = new List<string>();

    /// <summary>ABS 库图标标识（未实证字段，容错解析）。</summary>
    public string Icon { get; set; } = string.Empty;

    public static AbsLibrary FromJson(JsonElement? json)
    {
        var library = new AbsLibrary
        {
            Id = JsonRead.Str(json, "id"),
            Name = JsonRead.Str(json, "name"),
            MediaType = JsonRead.Str(json, "mediaType", "book"),
            Icon = JsonRead.Str(json, "icon"),
        };

        // Dart: (json['folders'] as List?)?.whereType<Map>().map((e) => '${e['fullPath'] ?? ''}')
        foreach (var folder in JsonRead.Objects(JsonRead.Items(json, "folders")))
        {
            library.Folders.Add(JsonRead.Str(folder, "fullPath"));
        }
        return library;
    }

    public override string ToString() => $"AbsLibrary({Id}, {Name}, {MediaType})";
}

/// <summary>音频轨（对应 Dart <c>AbsAudioTrack</c>；<c>media.audioFiles[]</c>）。</summary>
public sealed class AbsAudioTrack
{
    /// <summary>轨序号（从 0 开始，与 ABS <c>audioFiles</c> 顺序一致）。</summary>
    public int Index { get; set; }

    /// <summary>本轨时长（秒）。</summary>
    public double Duration { get; set; }

    /// <summary>全书起点偏移（秒）。ABS 多数版本会给 <c>startOffset</c>，缺省由 <c>AbsTimeline</c> 累计推算。</summary>
    public double StartOffset { get; set; }

    /// <summary>ABS 的音频文件 inode 串（取流地址 <c>/api/items/{id}/file/{ino}</c> 直接用它）。</summary>
    public string Ino { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    /// <summary>轨标题（来自 <c>metadata.title</c>）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>本轨在全书的结束偏移（秒）。</summary>
    public double EndOffset => StartOffset + Duration;

    /// <summary>
    /// 解析单轨。<paramref name="index"/> 为数组下标：服务端未给 <c>index</c> 时回落到它（与 Dart 一致）。
    /// 注意：ABS 的 <c>audioFiles[].index</c> 存在**从 1 开始**的版本 ⇒ 轨定位不要用 <c>Index</c>，
    /// 一律用数组下标（见 <c>AbsTimeline</c>）。
    /// </summary>
    public static AbsAudioTrack FromJson(JsonElement? json, int index) => new AbsAudioTrack
    {
        Index = JsonRead.IntOrNull(json, "index") ?? index,
        Duration = JsonRead.Double(json, "duration"),
        StartOffset = JsonRead.Double(json, "startOffset"),
        Ino = JsonRead.Str(json, "ino"),
        MimeType = JsonRead.Str(json, "mimeType"),
        Title = JsonRead.Str(JsonRead.Prop(json, "metadata"), "title"),
    };

    public override string ToString() => $"AbsAudioTrack(#{Index}, {Duration:F1}s @{StartOffset:F1}s, {Title})";
}

/// <summary>图书（对应 Dart <c>AbsBook</c>；<c>media.metadata</c> + <c>media.audioFiles</c>）。</summary>
public sealed class AbsBook
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary><c>metadata.authorName</c>，回落 <c>metadata.author</c>。</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary><c>metadata.narratorName</c>（未实证字段，容错解析）。</summary>
    public string Narrator { get; set; } = string.Empty;

    public string SeriesName { get; set; } = string.Empty;

    /// <summary>系列内序号（服务端可能是字符串或数字 ⇒ 容错读为字符串）。</summary>
    public string SeriesSequence { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PublishedYear { get; set; } = string.Empty;

    public List<string> Genres { get; set; } = new List<string>();

    public string Language { get; set; } = string.Empty;

    public string Isbn { get; set; } = string.Empty;

    /// <summary>全书时长（秒，<c>media.duration</c>）；为 0 时可用 <see cref="TimelineTotalDuration"/>。</summary>
    public double Duration { get; set; }

    /// <summary>封面相对路径（<c>media.coverPath</c>；实际取图走 <c>/api/items/{id}/cover</c>）。</summary>
    public string CoverPath { get; set; } = string.Empty;

    /// <summary>多轨清单（数组下标即轨序号）。</summary>
    public List<AbsAudioTrack> Tracks { get; set; } = new List<AbsAudioTrack>();

    /// <summary><c>metadata.explicit</c>（未实证字段，容错解析）。</summary>
    public bool Explicit { get; set; }

    /// <summary><c>metadata.abridged</c>（未实证字段，容错解析）。</summary>
    public bool Abridged { get; set; }

    /// <summary>同名于 Dart <c>subtitle</c>：<c>作者 · 旁白 X · 系列 #n</c>（跳过空项）。</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Author.Length > 0) parts.Add(Author);
            if (Narrator.Length > 0) parts.Add("旁白 " + Narrator);
            if (SeriesName.Length > 0)
            {
                parts.Add(SeriesSequence.Length == 0 ? SeriesName : $"{SeriesName} #{SeriesSequence}");
            }
            return string.Join(" · ", parts);
        }
    }

    /// <summary>多轨时长合计（秒）—— <c>media.duration</c> 缺失时的回落量（对应 Dart 侧 <c>timeline.totalDuration</c> 用法）。</summary>
    public double TimelineTotalDuration => AbsTimeline.TotalDurationOf(Tracks);

    /// <summary>上报进度/显示用的有效总时长：优先服务端 <see cref="Duration"/>，为 0 时回落多轨合计。</summary>
    public double EffectiveDuration => Duration > 0 ? Duration : TimelineTotalDuration;

    public static AbsBook FromJson(JsonElement? json)
    {
        var media = JsonRead.Prop(json, "media");
        var meta = JsonRead.Prop(media, "metadata");

        var book = new AbsBook
        {
            Id = JsonRead.Str(json, "id"),
            Title = JsonRead.Str(meta, "title"),
            Author = JsonRead.Str(meta, "authorName"),
            Narrator = JsonRead.Str(meta, "narratorName"),
            SeriesName = JsonRead.Str(meta, "seriesName"),
            SeriesSequence = JsonRead.Str(meta, "seriesSequence"),
            Description = JsonRead.Str(meta, "description"),
            PublishedYear = JsonRead.Str(meta, "publishedYear"),
            Genres = JsonRead.StrList(meta, "genres"),
            Language = JsonRead.Str(meta, "language"),
            Isbn = JsonRead.Str(meta, "isbn"),
            Duration = JsonRead.Double(media, "duration"),
            CoverPath = JsonRead.Str(media, "coverPath"),
            Explicit = JsonRead.Bool(meta, "explicit"),
            Abridged = JsonRead.Bool(meta, "abridged"),
        };

        // Dart: id = '${json['id'] ?? media['id'] ?? ''}' —— 条目嵌套在库条目响应里时 id 在 media 上。
        if (book.Id.Length == 0) book.Id = JsonRead.Str(media, "id");

        // Dart: author = '${meta['authorName'] ?? meta['author'] ?? ''}'
        if (book.Author.Length == 0) book.Author = JsonRead.Str(meta, "author");

        // Dart: 逐元素 AbsAudioTrack.fromJson(e, i)（非对象元素被 whereType<Map> 过滤掉）
        var index = 0;
        foreach (var track in JsonRead.Objects(JsonRead.Items(media, "audioFiles")))
        {
            book.Tracks.Add(AbsAudioTrack.FromJson(track, index));
            index++;
        }
        return book;
    }

    public override string ToString() => $"AbsBook({Id}, {Title}, tracks={Tracks.Count}, {Duration:F1}s)";
}

/// <summary>播放进度（对应 Dart <c>AbsProgress</c>；<c>GET/PATCH /api/me/progress/{itemId}</c>）。</summary>
public sealed class AbsProgress
{
    /// <summary>全书秒数（ABS 与内核一致，均为**秒**，无需 ticks 换算）。</summary>
    public double CurrentTime { get; set; }

    public double Duration { get; set; }

    /// <summary>0–1 比例。</summary>
    public double Progress { get; set; }

    public bool IsFinished { get; set; }

    /// <summary>最后更新时间戳（毫秒；64 位 ⇒ 走 LongOrNull）。</summary>
    public long LastUpdate { get; set; }

    /// <summary>开听时间戳（毫秒；64 位 ⇒ 走 LongOrNull）。</summary>
    public long StartedAt { get; set; }

    /// <summary>听完时间戳（毫秒；可能缺省 ⇒ 可空）。</summary>
    public long? FinishedAt { get; set; }

    /// <summary>已听多久（秒）—— 等价 Dart <c>listenedSeconds</c>。</summary>
    public double ListenedSeconds => CurrentTime;

    public static AbsProgress FromJson(JsonElement? json) => new AbsProgress
    {
        CurrentTime = JsonRead.Double(json, "currentTime"),
        Duration = JsonRead.Double(json, "duration"),
        Progress = JsonRead.Double(json, "progress"),
        IsFinished = JsonRead.Bool(json, "isFinished"),
        LastUpdate = JsonRead.LongOrNull(json, "lastUpdate") ?? 0L,
        StartedAt = JsonRead.LongOrNull(json, "startedAt") ?? 0L,
        FinishedAt = JsonRead.LongOrNull(json, "finishedAt"),
    };

    /// <summary>
    /// 等价 Dart <c>toJson()</c>：**只**下发 currentTime/duration/progress/isFinished 四个键
    /// （PATCH <c>/api/me/progress/{itemId}</c> 的请求体；ABS 服务端不接受扩展字段）。
    /// </summary>
    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["currentTime"] = CurrentTime,
            ["duration"] = Duration,
            ["progress"] = Progress,
            ["isFinished"] = IsFinished,
        };
    }

    public override string ToString() => $"AbsProgress({CurrentTime:F1}/{Duration:F1}s, {Progress:P0}, finished={IsFinished})";
}

/// <summary>「轨序号 + 轨内秒数」定位结果（对应 Dart <c>AbsLocation</c>）。</summary>
public sealed class AbsLocation
{
    public AbsLocation(int trackIndex, double secondsWithinTrack)
    {
        TrackIndex = trackIndex;
        SecondsWithinTrack = secondsWithinTrack;
    }

    public int TrackIndex { get; }

    public double SecondsWithinTrack { get; }

    public override string ToString() => $"AbsLocation(track={TrackIndex}, inTrack={SecondsWithinTrack:F1}s)";
}

/// <summary>
/// 作者（<c>GET /api/authors</c> / <c>GET /api/authors/{id}</c>）。
/// Dart 侧把作者原样当 <c>Map&lt;String, dynamic&gt;</c> 透传，未建模；此处按 SERVICE_API.md §3 的实证端点建最小模型
/// （字段名 <c>id</c>/<c>name</c> 为 ABS 通用形态，<c>name</c> 缺失时回落 <c>nameLastFirst</c> —— 未实证，容错解析）。
/// </summary>
public sealed class AbsAuthor
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>ABS 的 <c>nameLastFirst</c>（“姓, 名”形态；未实证，容错解析）。</summary>
    public string NameLastFirst { get; set; } = string.Empty;

    /// <summary>服务端给的条目数（<c>numBooks</c>，缺省 -1 表示未提供）。</summary>
    public int NumBooks { get; set; } = -1;

    public static AbsAuthor FromJson(JsonElement? json)
    {
        var author = new AbsAuthor
        {
            Id = JsonRead.Str(json, "id"),
            Name = JsonRead.Str(json, "name"),
            NameLastFirst = JsonRead.Str(json, "nameLastFirst"),
            NumBooks = JsonRead.IntOrNull(json, "numBooks") ?? -1,
        };
        if (author.Name.Length == 0) author.Name = author.NameLastFirst;
        return author;
    }

    public override string ToString() => $"AbsAuthor({Id}, {Name})";
}

/// <summary>搜索命中的系列（<c>GET /api/search</c> 的 <c>series</c> 分桶；未实证，容错解析）。</summary>
public sealed class AbsSeries
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public static AbsSeries FromJson(JsonElement? json) => new AbsSeries
    {
        Id = JsonRead.Str(json, "id"),
        Name = JsonRead.Str(json, "name"),
    };

    public override string ToString() => $"AbsSeries({Id}, {Name})";
}

/// <summary>
/// 搜索结果（<c>GET /api/search?q=</c>）。
/// Dart 侧直接返回整张 JSON map，未建模；此处按 ABS 实证响应形状给出强类型视图，
/// 字段名 <c>book</c>/<c>podcast</c>/<c>authors</c>/<c>series</c> 为实证形态，数组元素结构未实证 ⇒ 一律容错解析。
/// 原始 JSON 仍可由 <c>AudioBookshelfService.SearchRawAsync</c> 取得。
/// </summary>
public sealed class AbsSearchResult
{
    public List<AbsBook> Books { get; set; } = new List<AbsBook>();

    public List<AbsBook> Podcasts { get; set; } = new List<AbsBook>();

    public List<AbsAuthor> Authors { get; set; } = new List<AbsAuthor>();

    public List<AbsSeries> Series { get; set; } = new List<AbsSeries>();

    public static AbsSearchResult FromJson(JsonElement? json)
    {
        var result = new AbsSearchResult();
        if (json == null) return result;

        foreach (var element in JsonRead.Objects(JsonRead.Items(json, "book")))
        {
            result.Books.Add(AbsBook.FromJson(element));
        }
        foreach (var element in JsonRead.Objects(JsonRead.Items(json, "podcast")))
        {
            result.Podcasts.Add(AbsBook.FromJson(element));
        }
        foreach (var element in JsonRead.Objects(JsonRead.Items(json, "authors")))
        {
            result.Authors.Add(AbsAuthor.FromJson(element));
        }
        foreach (var element in JsonRead.Objects(JsonRead.Items(json, "series")))
        {
            result.Series.Add(AbsSeries.FromJson(element));
        }
        return result;
    }

    public override string ToString() =>
        $"AbsSearchResult(books={Books.Count}, podcasts={Podcasts.Count}, authors={Authors.Count}, series={Series.Count})";
}

// 对应：rebuild/ai_player/lib/core/models/media_segment.dart → DESIGN §4.2 `Services/Models/MediaSegmentDto.cs`。
// 字段形态以 reversed/FlutterApp/HOST_CONTRACT.md §3.6 为实证（内核 --segment= 吃 Base64(JSON)）：
//   { "type": "intro", "startMs": 12000, "endMs": 102000, "source": "emby" }
//   `startMs <= 0` 且无 `endMs` ⇒ 丢弃；`endMs` 可缺省（表示「到片尾」）。
// 注：服务层不引用内核程序集；由外壳把本 DTO 序列化后映射到 WinUISample.Models.MediaSegment（DESIGN §4.2）。

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIPlayer.Shell.Services.Models;

public enum MediaSegmentType
{
    Intro,
    Credits,
    Recap,
    Preview,
}

public static class MediaSegmentTypeExtensions
{
    /// <summary>内核契约里的小写标识（HOST_CONTRACT §3.6 例：<c>intro</c>）。</summary>
    public static string Id(this MediaSegmentType type) => type switch
    {
        MediaSegmentType.Intro => "intro",
        MediaSegmentType.Credits => "credits",
        MediaSegmentType.Recap => "recap",
        MediaSegmentType.Preview => "preview",
        _ => "intro",
    };
}

/// <summary>跳过片段（片头/片尾/回顾/预告），形态与内核 <c>--segment=</c> 一致。</summary>
public sealed class MediaSegmentDto
{
    public MediaSegmentType Type { get; set; } = MediaSegmentType.Intro;

    public long StartMs { get; set; }

    /// <summary>可空：缺省表示「到片尾」。</summary>
    public long? EndMs { get; set; }

    /// <summary>来源标识（<c>emby</c> / <c>theintrodb</c> / <c>introdb</c> / <c>theotherdb</c> / <c>custom</c> / <c>local</c>）。</summary>
    public string Source { get; set; } = string.Empty;

    public TimeSpan Start => TimeSpan.FromMilliseconds(StartMs);

    public TimeSpan? End => EndMs.HasValue ? TimeSpan.FromMilliseconds(EndMs.Value) : (TimeSpan?)null;

    /// <summary>等价内核丢弃规则：<c>startMs &lt;= 0 且无 endMs</c> ⇒ 无效。</summary>
    public bool IsValid => StartMs > 0 || EndMs.HasValue;

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["type"] = Type.Id(),
            ["startMs"] = StartMs,
        };
        if (EndMs.HasValue) o["endMs"] = EndMs.Value;
        o["source"] = Source;
        return o;
    }

    /// <summary>内核 <c>--segment=</c> 的 Base64(JSON) 载荷。</summary>
    public string ToBase64() => Convert.ToBase64String(Encoding.UTF8.GetBytes(ToJson().ToJsonString()));

    public static MediaSegmentDto FromJson(JsonElement json, string defaultSource = "")
    {
        var typeText = (Util.JsonRead.Str(json, "Type").Length > 0
            ? Util.JsonRead.Str(json, "Type")
            : Util.JsonRead.Str(json, "type")).ToLowerInvariant();

        var start = Util.JsonRead.LongOrNull(json, "StartMs") ?? Util.JsonRead.LongOrNull(json, "startMs") ?? 0;
        var end = Util.JsonRead.LongOrNull(json, "EndMs") ?? Util.JsonRead.LongOrNull(json, "endMs");

        var source = Util.JsonRead.Str(json, "Source");
        if (source.Length == 0) source = Util.JsonRead.Str(json, "source");
        if (source.Length == 0) source = defaultSource;

        return new MediaSegmentDto
        {
            Type = ParseType(typeText),
            StartMs = start,
            EndMs = end,
            Source = source,
        };
    }

    /// <summary>Emby 的 <c>Type</c> ∈ Intro | Outro | Recap | Preview | Commercial → 内核四类。</summary>
    public static MediaSegmentType ParseType(string typeText) => (typeText ?? string.Empty).ToLowerInvariant() switch
    {
        "recap" => MediaSegmentType.Recap,
        "outro" => MediaSegmentType.Credits,
        "credits" => MediaSegmentType.Credits,
        "ending" => MediaSegmentType.Credits,
        "preview" => MediaSegmentType.Preview,
        "commercial" => MediaSegmentType.Preview,
        "trailer" => MediaSegmentType.Preview,
        _ => MediaSegmentType.Intro,
    };
}

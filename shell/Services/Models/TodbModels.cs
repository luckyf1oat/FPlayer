// 对应 DESIGN §4.2 `core/models/todb_models.dart` → `Services/Models/TodbModels.cs`。
// 规格依据（DESIGN 修订 r20 §4.2 + 硬规则 S-1）：服务层**自建同形 DTO**（字段名 snake_case，与内核
// `--chapter=` / `--sprite=` 直吃的形态一致），App 侧再映射内核 `WinUISample.Models.TodbChapter/TodbSprite`；
// 服务层不得使用任何 `WinUISample.*` 类型。
//
// 形态实证：reversed/FlutterApp/HOST_CONTRACT.md §3.7（章节）与 §3.8（雪碧图）。

using System;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>TODB 章节标记（<c>--chapter=</c>，字段名 snake_case）。</summary>
public sealed class HostChapter
{
    public int MarkerId { get; set; }

    /// <summary>缺省 <c>chapter</c>。</summary>
    public string MarkerType { get; set; } = "chapter";

    public string Title { get; set; }

    /// <summary>毫秒（与内核 <c>time_start</c> 单位一致）。</summary>
    public long TimeStart { get; set; }

    /// <summary>毫秒；可缺省。</summary>
    public long? TimeEnd { get; set; }

    public System.Text.Json.Nodes.JsonObject ToJson()
    {
        var o = new System.Text.Json.Nodes.JsonObject
        {
            ["marker_id"] = MarkerId,
            ["marker_type"] = MarkerType,
            ["title"] = Title,
            ["time_start"] = TimeStart,
        };
        if (TimeEnd.HasValue) o["time_end"] = TimeEnd.Value;
        return o;
    }

    /// <summary>内核 <c>--chapter=</c> 的 Base64 载荷。</summary>
    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());

    /// <summary>容错解析（snake_case 为主，兼容 camelCase）。</summary>
    public static HostChapter FromJson(System.Text.Json.JsonElement json)
    {
        var markerId = JsonRead.IntOrNull(json, "marker_id");
        if (!markerId.HasValue) markerId = JsonRead.IntOrNull(json, "markerId");

        var start = JsonRead.LongOrNull(json, "time_start");
        if (!start.HasValue) start = JsonRead.LongOrNull(json, "timeStart");

        var end = JsonRead.LongOrNull(json, "time_end");
        if (!end.HasValue) end = JsonRead.LongOrNull(json, "timeEnd");

        var type = JsonRead.Str(json, "marker_type");
        if (type.Length == 0) type = JsonRead.Str(json, "markerType");

        var title = JsonRead.Prop(json, "title")?.ValueKind == System.Text.Json.JsonValueKind.String
            ? JsonRead.Str(json, "title")
            : null;

        return new HostChapter
        {
            MarkerId = markerId ?? 0,
            MarkerType = type.Length == 0 ? "chapter" : type,
            Title = title,
            TimeStart = start ?? 0,
            TimeEnd = end,
        };
    }
}

/// <summary>进度条缩略图雪碧图（<c>--sprite=</c>）。</summary>
public sealed class HostSprite
{
    public int SpriteId { get; set; } = 1;

    public int Width { get; set; }

    public int Height { get; set; }

    public string VttUrl { get; set; } = string.Empty;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["sprite_id"] = SpriteId,
        ["width"] = Width,
        ["height"] = Height,
        ["vtt_url"] = VttUrl,
    };

    /// <summary>内核 <c>--sprite=</c> 的 Base64 载荷。</summary>
    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());

    public static HostSprite FromJson(System.Text.Json.JsonElement json)
    {
        var vtt = JsonRead.Str(json, "vtt_url");
        if (vtt.Length == 0) vtt = JsonRead.Str(json, "vttUrl");

        var id = JsonRead.IntOrNull(json, "sprite_id");
        if (!id.HasValue) id = JsonRead.IntOrNull(json, "spriteId");

        return new HostSprite
        {
            SpriteId = id ?? 1,
            Width = JsonRead.IntOrNull(json, "width") ?? 0,
            Height = JsonRead.IntOrNull(json, "height") ?? 0,
            VttUrl = vtt,
        };
    }
}

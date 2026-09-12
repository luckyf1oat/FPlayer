// 对应 DESIGN §4.2 `core/models/icon_library.dart` → `Services/Models/IconLibraryEntry.cs`。
// 等价移植：rebuild/ai_player/lib/core/services/icon_library_service.dart 的 `IconEntry`
//         + 原版 `core/models/icon_library.dart` 的语义（内置图标 id → 显示名映射）。
// 说明：本类型是**唯一**的图标条目模型；`Icons/IconLibraryStore.cs` 通过 using 别名引用它（不再自行定义同名类型）。

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>图标条目（内置或自定义）。</summary>
public sealed class IconLibraryEntry
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>图片 URL（内置图标为空）。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary><c>builtin</c> / <c>custom</c>，或远程图标源 URL。</summary>
    public string Source { get; set; } = "custom";

    /// <summary>内置图标：有 <c>source=builtin</c> 且无 URL（UI 侧映射到 FluentIcons）。</summary>
    public bool IsBuiltin => Source == "builtin" && Url.Length == 0;

    public JsonObject ToJson() => new JsonObject
    {
        ["id"] = Id,
        ["name"] = Name,
        ["url"] = Url,
        ["source"] = Source,
    };

    /// <summary>容错解析：<c>id</c> 缺失回落 <c>name</c>；<c>url</c> 缺失回落 <c>icon</c>（原版字符串实证两种命名）。</summary>
    public static IconLibraryEntry FromJson(JsonElement json)
    {
        var id = JsonRead.Str(json, "id");
        if (id.Length == 0) id = JsonRead.Str(json, "name");
        var url = JsonRead.Str(json, "url");
        if (url.Length == 0) url = JsonRead.Str(json, "icon");

        return new IconLibraryEntry
        {
            Id = id,
            Name = JsonRead.Str(json, "name"),
            Url = url,
            Source = JsonRead.Str(json, "source", "custom"),
        };
    }

    public override string ToString() => $"IconLibraryEntry({Id}, {Name}, {Source}, {(Url.Length > 0 ? Url : "-")})";
}

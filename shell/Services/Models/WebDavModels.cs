// 等价移植：rebuild/ai_player/lib/core/models/webdav_models.dart。
// 说明：重建版的 `webdav_models.dart` 实为空壳（models 目录下无该文件，DESIGN §4.1 #19 标为「新写」），
// 因此本文件按 Dart 实际类型归属落位，逐行对应：
//   - WebDavEntry   ← rebuild/ai_player/lib/core/services/webdav_service.dart:13-33
//   - BackupBundle  ← rebuild/ai_player/lib/core/services/webdav_backup_service.dart:16-57
// 端点依据：reversed/FlutterApp/SERVICE_API.md §4（WebDAV：PROPFIND/MKCOL/GET/PUT/DELETE、
// HTTP Basic、备份产物命名 `/backup_<日期>`）；备份内容面依据同节「设置 / 服务器配置 / 播放进度 / 图标库」推断。
// 容错：SERVICE_API.md 未实证字段一律宽松解析（缺字段 / 类型漂移不抛异常）。
// 命名：WebDavEntry / BackupBundle / WebDavTime 与 EmbyModels、ABS、Navidrome 侧类型无重名。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>WebDAV 目录项（对应 Dart <c>WebDavEntry</c>，webdav_service.dart:13）。</summary>
public sealed class WebDavEntry
{
    public WebDavEntry(string path, bool isDirectory, long size = 0L, DateTime? modified = null)
    {
        Path = path ?? string.Empty;
        IsDirectory = isDirectory;
        Size = size;
        Modified = modified;
    }

    /// <summary>远端路径（已做百分号解码；如 <c>/dav/backup/backup_20260911_191405.json</c>）。</summary>
    public string Path { get; }

    /// <summary><c>resourcetype/collection</c> 命中即为目录。</summary>
    public bool IsDirectory { get; }

    /// <summary>文件字节数（<c>getcontentlength</c>）。大整数一律 64 位，勿降为 int。</summary>
    public long Size { get; }

    /// <summary>最后修改时间（已转本地时区；解析失败为 <c>null</c>）。</summary>
    public DateTime? Modified { get; }

    /// <summary>路径末段（对应 Dart <c>name</c> getter）；路径为空时回落为整串。</summary>
    public string Name
    {
        get
        {
            var segments = new List<string>();
            foreach (var segment in Path.Split('/'))
            {
                if (segment.Length > 0) segments.Add(segment);
            }
            return segments.Count == 0 ? Path : segments[segments.Count - 1];
        }
    }

    /// <summary>
    /// JSON 形态的目录项（等价 Dart 字段集：path / isDirectory / size / modified）。
    /// Dart 侧 <c>WebDavEntry</c> 只由 XML Multi-Status 构造、无 <c>fromJson</c>；
    /// 此处按服务层统一约定提供 <c>FromJson</c>，供 JSON 列表输入使用（未实证处容错）。
    /// </summary>
    public static WebDavEntry FromJson(JsonElement json)
    {
        var modified = WebDavTime.ParseIso(JsonRead.Str(json, "modified"));
        if (modified == null)
        {
            // 容忍 epoch 毫秒数值形态（Dart 仅接受 ISO 串）。
            var epochMillis = JsonRead.LongOrNull(json, "modified");
            if (epochMillis.HasValue) modified = WebDavTime.FromUnixMillis(epochMillis.Value);
        }
        return new WebDavEntry(
            JsonRead.Str(json, "path"),
            JsonRead.Bool(json, "isDirectory"),
            JsonRead.LongOrNull(json, "size") ?? 0L,
            modified);
    }

    public override string ToString() => $"WebDavEntry({Path}, dir={IsDirectory}, {Size} B)";
}

/// <summary>WebDAV 备份包（对应 Dart <c>BackupBundle</c>，webdav_backup_service.dart:16）。</summary>
public sealed class BackupBundle
{
    /// <summary>备份格式版本（Dart 默认 1）。</summary>
    public int Schema { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>设置（settings.json）。</summary>
    public Dictionary<string, JsonNode> Settings { get; set; } = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>服务器配置（servers.json 的 <c>servers</c> 数组；**已剥离密码/令牌**）。</summary>
    public List<JsonNode> Servers { get; set; } = new List<JsonNode>();

    /// <summary>跳过片段缓存（skip_segments.json）。</summary>
    public Dictionary<string, JsonNode> SkipCache { get; set; } = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>自定义图标库（icons.json）。</summary>
    public Dictionary<string, JsonNode> Icons { get; set; } = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>序列化为上传用对象（字段名与 Dart <c>toJson</c> 一致）。</summary>
    public JsonObject ToJson() => new JsonObject
    {
        ["schema"] = Schema,
        ["createdAt"] = WebDavTime.ToIso(CreatedAt),
        ["app"] = "AI Player (rebuilt shell)",
        ["settings"] = ToJsonObject(Settings),
        ["servers"] = ToJsonArray(Servers),
        ["skipCache"] = ToJsonObject(SkipCache),
        ["icons"] = ToJsonObject(Icons),
    };

    /// <summary>容错反序列化（对应 Dart <c>BackupBundle.fromJson</c>；缺字段回落默认值）。</summary>
    public static BackupBundle FromJson(JsonElement json)
    {
        var createdAt = WebDavTime.ParseIso(JsonRead.Str(json, "createdAt"));
        if (createdAt == null)
        {
            var epochMillis = JsonRead.LongOrNull(json, "createdAt");
            if (epochMillis.HasValue) createdAt = WebDavTime.FromUnixMillis(epochMillis.Value);
        }

        return new BackupBundle
        {
            Schema = JsonRead.IntOrNull(json, "schema") ?? 1,
            CreatedAt = createdAt ?? DateTime.Now,
            Settings = ReadMap(json, "settings"),
            Servers = ReadList(json, "servers"),
            SkipCache = ReadMap(json, "skipCache"),
            Icons = ReadMap(json, "icons"),
        };
    }

    /// <summary>把字典挂进 <see cref="JsonObject"/>（始终深拷贝，避免节点多父级）。</summary>
    public static JsonObject ToJsonObject(Dictionary<string, JsonNode> map)
    {
        var result = new JsonObject();
        if (map == null) return result;
        foreach (var kv in map)
        {
            result[kv.Key] = kv.Value == null ? (JsonNode)null : kv.Value.DeepClone();
        }
        return result;
    }

    /// <summary>把列表挂进 <see cref="JsonArray"/>（始终深拷贝）。</summary>
    public static JsonArray ToJsonArray(List<JsonNode> items)
    {
        var result = new JsonArray();
        if (items == null) return result;
        foreach (var item in items)
        {
            result.Add(item == null ? (JsonNode)null : item.DeepClone());
        }
        return result;
    }

    private static Dictionary<string, JsonNode> ReadMap(JsonElement json, string name)
    {
        var map = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var element = JsonRead.Prop(json, name);
        if (element == null || element.Value.ValueKind != JsonValueKind.Object) return map;
        foreach (var property in element.Value.EnumerateObject())
        {
            map[property.Name] = ParseNode(property.Value);
        }
        return map;
    }

    private static List<JsonNode> ReadList(JsonElement json, string name)
    {
        var list = new List<JsonNode>();
        var element = JsonRead.Prop(json, name);
        if (element == null || element.Value.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            list.Add(ParseNode(item));
        }
        return list;
    }

    /// <summary>JSON 元素 → JsonNode（<c>null</c> 字面量返回 <c>null</c> 节点）。</summary>
    private static JsonNode ParseNode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined) return null;
        try
        {
            return JsonNode.Parse(element.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// 时间戳助手（Dart 无对应类；<c>DateTime.tryParse</c> / <c>toIso8601String</c> 的等价物）。
/// 输出与 Dart 一致：<c>Kind=Utc</c> 带 <c>Z</c> 后缀，本地/未指定不带后缀；毫秒为 0 时省略小数部分。
/// </summary>
public static class WebDavTime
{
    /// <summary>容错解析 ISO 8601 / RFC 1123 时间串；失败返回 <c>null</c>（等价 Dart <c>DateTime.tryParse</c>）。</summary>
    public static DateTime? ParseIso(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed
            : (DateTime?)null;
    }

    /// <summary>转本地时区（等价 Dart <c>?.toLocal()</c>）。</summary>
    public static DateTime? ToLocal(DateTime? time)
    {
        if (time == null) return null;
        var value = time.Value;
        if (value.Kind == DateTimeKind.Utc) return value.ToLocalTime();
        return value.Kind == DateTimeKind.Local ? value : DateTime.SpecifyKind(value, DateTimeKind.Local);
    }

    /// <summary>ISO 8601 输出（等价 Dart <c>toIso8601String()</c>，毫秒精度）。</summary>
    public static string ToIso(DateTime time)
    {
        var text = time.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        if (time.Millisecond != 0)
        {
            text += "." + time.Millisecond.ToString("000", CultureInfo.InvariantCulture);
        }
        return time.Kind == DateTimeKind.Utc ? text + "Z" : text;
    }

    /// <summary>epoch 毫秒 → 本地时间（未实证形态的容错入口）。</summary>
    public static DateTime FromUnixMillis(long millis)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(millis).ToLocalTime().DateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.Now;
        }
    }
}

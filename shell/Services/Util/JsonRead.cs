// 服务层 JSON 容错读取助手。
// 目的：等价 Dart 侧 `'${json['X'] ?? ''}'` / `(json['X'] as num?)?.toInt()` 的宽松语义 —— 服务端字段缺失、
// 类型漂移（string↔number）时不抛异常，按 SERVICE_API.md 未实证处一律容错解析（DESIGN §4 / t7 验收⑥）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIPlayer.Shell.Services.Util;

/// <summary>JSON 宽松读取（永不抛异常）。</summary>
public static class JsonRead
{
    public static JsonElement? Prop(JsonElement? element, string name)
    {
        if (element == null) return null;
        var e = element.Value;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v))
        {
            return v;
        }
        return null;
    }

    /// <summary>等价 Dart 的字符串插值：数值/布尔也转成字符串；缺省返回 <paramref name="def"/>。</summary>
    public static string Str(JsonElement? element, string name, string def = "")
    {
        var v = Prop(element, name);
        return v == null ? def : AsString(v.Value, def);
    }

    public static string AsString(JsonElement e, string def = "")
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                return e.GetString() ?? def;
            case JsonValueKind.Number:
                return e.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return def;
            default:
                return e.GetRawText();
        }
    }

    public static int Int(JsonElement? element, string name, int def = 0)
        => IntOrNull(element, name) ?? def;

    /// <summary>
    /// 64 位整数读取。**ticks / 文件大小 / 码率必须走这里**：Emby 的 ticks 极易超过 int 上限
    /// （100 分钟 = 6e10 ticks &gt; int.MaxValue 2.1e9），用 int 读会静默截断。
    /// </summary>
    public static long? LongOrNull(JsonElement? element, string name)
    {
        var v = Prop(element, name);
        if (v == null) return null;
        var e = v.Value;
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                if (e.TryGetInt64(out var l)) return l;
                if (e.TryGetDouble(out var d)) return double.IsNaN(d) || double.IsInfinity(d) ? null : (long)Math.Round(d);
                return null;
            case JsonValueKind.String:
                if (long.TryParse(e.GetString(), out var parsed)) return parsed;
                if (double.TryParse(e.GetString(), out var pd)) return (long)Math.Round(pd);
                return null;
            default:
                return null;
        }
    }

    public static long Long(JsonElement? element, string name, long def = 0)
        => LongOrNull(element, name) ?? def;

    public static int? IntOrNull(JsonElement? element, string name)
    {
        var v = Prop(element, name);
        if (v == null) return null;
        var e = v.Value;
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                if (e.TryGetInt64(out var l)) return l > int.MaxValue ? int.MaxValue : (l < int.MinValue ? int.MinValue : (int)l);
                if (e.TryGetDouble(out var d)) return double.IsNaN(d) || double.IsInfinity(d) ? null : (int)Math.Round(d);
                return null;
            case JsonValueKind.String:
                if (long.TryParse(e.GetString(), out var parsed)) return parsed > int.MaxValue ? int.MaxValue : (int)parsed;
                if (double.TryParse(e.GetString(), out var pd)) return (int)Math.Round(pd);
                return null;
            default:
                return null;
        }
    }

    public static double Double(JsonElement? element, string name, double def = 0)
        => DoubleOrNull(element, name) ?? def;

    public static double? DoubleOrNull(JsonElement? element, string name)
    {
        var v = Prop(element, name);
        if (v == null) return null;
        var e = v.Value;
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                return e.TryGetDouble(out var d) ? d : null;
            case JsonValueKind.String:
                return double.TryParse(e.GetString(), out var pd) ? pd : null;
            default:
                return null;
        }
    }

    public static bool Bool(JsonElement? element, string name, bool def = false)
    {
        var v = Prop(element, name);
        if (v == null) return def;
        var e = v.Value;
        switch (e.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                var s = e.GetString();
                if (bool.TryParse(s, out var b)) return b;
                if (s == "1") return true;
                if (s == "0") return false;
                return def;
            case JsonValueKind.Number:
                return e.TryGetDouble(out var d) && Math.Abs(d) > double.Epsilon;
            default:
                return def;
        }
    }

    /// <summary>对象内所有键的字符串值（对应 Dart 的 <c>(json['X'] as Map?)?.map((k,v) =&gt; MapEntry('$k','$v'))</c>）。</summary>
    public static Dictionary<string, string> StrMap(JsonElement? element, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var v = Prop(element, name);
        if (v == null || v.Value.ValueKind != JsonValueKind.Object) return result;
        foreach (var p in v.Value.EnumerateObject())
        {
            result[p.Name] = AsString(p.Value);
        }
        return result;
    }

    public static List<string> StrList(JsonElement? element, string name)
    {
        var result = new List<string>();
        var v = Prop(element, name);
        if (v == null || v.Value.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in v.Value.EnumerateArray())
        {
            result.Add(AsString(item));
        }
        return result;
    }

    /// <summary>数组元素（仅对象/值元素，原样返回）。</summary>
    public static List<JsonElement> Items(JsonElement? element, string name)
    {
        var result = new List<JsonElement>();
        var v = Prop(element, name);
        if (v == null || v.Value.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in v.Value.EnumerateArray())
        {
            result.Add(item);
        }
        return result;
    }

    public static JsonElement? FromNode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>JsonNode（如 <c>JsonStorage.ReadMap()</c> 的返回值）→ JsonElement 视图，便于复用上面的读取器。</summary>
    public static JsonElement? From(JsonNode node) => node == null ? null : FromNode(node.ToJsonString());

    public static JsonObject DeepClone(JsonElement element)
    {
        try
        {
            return JsonNode.Parse(element.GetRawText()) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>取对象的全部成员为字典（用于「未知字段」透传，如 ABS 的 progress）。</summary>
    public static Dictionary<string, JsonElement> Members(JsonElement? element)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (element == null || element.Value.ValueKind != JsonValueKind.Object) return result;
        foreach (var p in element.Value.EnumerateObject())
        {
            result[p.Name] = p.Value.Clone();
        }
        return result;
    }

    /// <summary>JsonElement 数组 → 可枚举对象视图（过滤非对象元素）。</summary>
    public static IEnumerable<JsonElement> Objects(IEnumerable<JsonElement> source)
        => source.Where(e => e.ValueKind == JsonValueKind.Object);
}

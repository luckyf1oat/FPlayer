// 等价移植：rebuild/ai_player/lib/core/services/todb_service.dart。
// 映射依据：DESIGN §4.1 #36 `todb_service.dart` → `Services/Todb/TodbService.cs`。
// SERVICE_API.md §6.3：内核 --chapter=（snake_case：marker_id/time_start…）与 --sprite=（vtt_url）
// **直接吃 todb 结构** ⇒ 外壳拉取后原样透传（本实现的 HostChapter/HostSprite 即该形态）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Todb;

/// <summary>一次拉取的结果。</summary>
public sealed class TodbMetadata
{
    public List<HostChapter> Chapters { get; set; } = new List<HostChapter>();

    public HostSprite Sprite { get; set; }

    public JsonObject Raw { get; set; } = new JsonObject();

    public bool IsEmpty => Chapters.Count == 0 && Sprite == null;
}

public sealed class TodbService
{
    public const string MetadataBase = "https://playback.theotherdb.org/api/metadata";

    private readonly ShellHttpClient _http;
    private readonly Action<string> _onLog;
    private readonly string _metadataBase;

    /// <param name="metadataBase">可注入基地址（DESIGN §2 D5：便于 mock 端到端）；默认 TheOtherDB 官方地址。</param>
    public TodbService(ShellHttpClient http, Action<string> onLog = null, string metadataBase = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _onLog = onLog;
        _metadataBase = string.IsNullOrEmpty(metadataBase) ? MetadataBase : metadataBase;
    }

    /// <summary>拉取条目元数据（因 §6.3 说明内核直吃 todb 结构，这里只做最小规范化）。</summary>
    public async Task<TodbMetadata> FetchAsync(
        string imdbId,
        int? season = null,
        int? episode = null,
        double? durationSeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["imdb_id"] = imdbId };
            if (season.HasValue) query["season"] = season.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (episode.HasValue) query["episode"] = episode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (durationSeconds.HasValue) query["duration"] = ((long)Math.Round(durationSeconds.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var result = await _http.GetAsync(ShellHttpClient.BuildUri(_metadataBase, query),
                timeout: TimeSpan.FromSeconds(8), retries: 0, cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (json == null) return new TodbMetadata();

            return new TodbMetadata
            {
                Chapters = ChaptersOf(json.Value),
                Sprite = SpriteOf(json.Value),
                Raw = JsonRead.DeepClone(json.Value),
            };
        }
        catch (Exception ex)
        {
            _onLog?.Invoke($"TheOtherDB 元数据获取失败: {ex.Message}");
            return new TodbMetadata();
        }
    }

    private static List<HostChapter> ChaptersOf(JsonElement json)
    {
        var raw = JsonRead.Items(json, "chapters");
        if (raw.Count == 0) raw = JsonRead.Items(json, "markers");
        if (raw.Count == 0) raw = JsonRead.Items(json, "items");

        var output = new List<HostChapter>();
        for (var i = 0; i < raw.Count; i++)
        {
            var item = raw[i];
            if (item.ValueKind != JsonValueKind.Object) continue;

            // 直接是内核结构（snake_case）⇒ 原样透传
            if (JsonRead.Prop(item, "marker_id") != null
                && (JsonRead.Prop(item, "time_start") != null || JsonRead.Prop(item, "timeStart") != null))
            {
                output.Add(new HostChapter
                {
                    MarkerId = JsonRead.IntOrNull(item, "marker_id") ?? i + 1,
                    MarkerType = JsonRead.Str(item, "marker_type", "chapter"),
                    Title = JsonRead.Prop(item, "title")?.ValueKind == JsonValueKind.String ? JsonRead.Str(item, "title") : null,
                    TimeStart = JsonRead.LongOrNull(item, "time_start") ?? JsonRead.LongOrNull(item, "timeStart") ?? 0,
                    TimeEnd = JsonRead.Prop(item, "time_end") == null && JsonRead.Prop(item, "timeEnd") == null
                        ? (long?)null
                        : (JsonRead.LongOrNull(item, "time_end") ?? JsonRead.LongOrNull(item, "timeEnd")),
                });
                continue;
            }

            // 其它命名 ⇒ 归一化
            var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var p in item.EnumerateObject()) map[p.Name.ToLowerInvariant()] = p.Value;

            var start = SegmentLike.StartMsOf(map, false);
            if (start == null) continue;

            var titleElement = JsonRead.Prop(item, "title");
            if (titleElement == null) titleElement = JsonRead.Prop(item, "name");
            if (titleElement == null) titleElement = JsonRead.Prop(item, "label");

            output.Add(new HostChapter
            {
                MarkerId = JsonRead.IntOrNull(item, "id") ?? i + 1,
                MarkerType = JsonRead.Str(item, "type", "chapter"),
                Title = titleElement?.ValueKind == JsonValueKind.String ? JsonRead.AsString(titleElement.Value) : null,
                TimeStart = start.Value,
                TimeEnd = SegmentLike.StartMsOf(map, true),
            });
        }
        return output;
    }

    private static HostSprite SpriteOf(JsonElement json)
    {
        var raw = JsonRead.Prop(json, "sprite");
        if (raw == null) raw = JsonRead.Prop(json, "thumbnails");
        if (raw == null) raw = JsonRead.Prop(json, "thumbs");
        if (raw == null || raw.Value.ValueKind != JsonValueKind.Object) return null;

        var vtt = JsonRead.Str(raw, "vtt_url");
        if (vtt.Length == 0) vtt = JsonRead.Str(raw, "vttUrl");
        if (vtt.Length == 0) vtt = JsonRead.Str(raw, "vtt");

        var width = JsonRead.IntOrNull(raw, "width") ?? 0;
        var height = JsonRead.IntOrNull(raw, "height") ?? 0;
        if (vtt.Length == 0 || width <= 0 || height <= 0) return null;

        return new HostSprite
        {
            SpriteId = JsonRead.IntOrNull(raw, "sprite_id") ?? 1,
            Width = width,
            Height = height,
            VttUrl = vtt,
        };
    }

    /// <summary>todb 时间字段单位是**毫秒**（内核 <c>time_start</c> 亦为毫秒）。</summary>
    public static long MsOf(JsonElement? value)
    {
        if (value == null) return 0;
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var num))
        {
            return (long)Math.Round(num, MidpointRounding.AwayFromZero);
        }
        if (value.Value.ValueKind == JsonValueKind.String && double.TryParse(value.Value.GetString(), out var parsed))
        {
            return (long)Math.Round(parsed, MidpointRounding.AwayFromZero);
        }
        return 0;
    }
}

/// <summary>供 <c>SegmentService</c> 与 <c>TodbService</c> 共用的极简时间字段探测（避免相互依赖）。</summary>
public static class SegmentLike
{
    public static long? StartMsOf(Dictionary<string, JsonElement> map, bool end)
    {
        var needles = end ? new[] { "end", "to", "finish" } : new[] { "start", "from", "begin" };
        var keys = map.Keys
            .Where(k => needles.Any(n => k.Contains(n, StringComparison.Ordinal)))
            .OrderBy(k => k.Length)
            .ToList();

        foreach (var key in keys)
        {
            var value = map[key];
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var num)) return Normalize(num, key);
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)) return Normalize(parsed, key);
        }
        return null;
    }

    private static long Normalize(double value, string fieldName)
    {
        var key = (fieldName ?? string.Empty).ToLowerInvariant();
        var msHint = key.Contains("ms", StringComparison.Ordinal) || key.Contains("milli", StringComparison.Ordinal);
        var secHint = key.Contains("sec", StringComparison.Ordinal) || key.Contains("second", StringComparison.Ordinal);
        if (msHint && !secHint) return (long)Math.Round(value, MidpointRounding.AwayFromZero);
        if (secHint) return (long)Math.Round(value * 1000, MidpointRounding.AwayFromZero);
        return value >= 100000
            ? (long)Math.Round(value, MidpointRounding.AwayFromZero)
            : (long)Math.Round(value * 1000, MidpointRounding.AwayFromZero);
    }
}

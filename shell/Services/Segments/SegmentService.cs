// 等价移植：rebuild/ai_player/lib/core/services/segment_service.dart。
// 映射依据：DESIGN §4.1 #32 `segment_service.dart` → `Services/Segments/SegmentService.cs`。
// 来源（SERVICE_API.md §6.2，均有 [S] 字符串实证）：
//   - Emby 原生 /MediaSegments/{itemId}（source: emby）
//   - TheIntroDB https://api.theintrodb.org/v3/media
//   - IntroDB.app https://api.introdb.app/segments
//   - TheOtherDB https://playback.theotherdb.org/api/metadata
//   - 自定义模板（占位符 {imdb} / {season} / {episode}）
// ⚠️ 重建说明：第三方三源的**响应字段命名未从 AOT 恢复**，此处沿用 Dart 侧同样的容错解析器
// （同时识别 start/startMs/start_ms/startSeconds 等常见写法），并把识别到的 type 映射到内核四类。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Segments;

/// <summary>跳过片头/片尾：多源聚合 → 内核 <c>--segment=</c>。</summary>
public sealed class SegmentService
{
    public const string TheIntroDbBase = "https://api.theintrodb.org/v3/media";
    public const string IntroDbAppBase = "https://api.introdb.app/segments";
    public const string TheOtherDbBase = "https://playback.theotherdb.org/api/metadata";

    private readonly ShellHttpClient _http;
    private readonly JsonStorage _cache;
    private readonly Action<string> _onLog;
    private readonly string _theIntroDbBase;
    private readonly string _introDbAppBase;
    private readonly string _theOtherDbBase;

    private JsonObject _cacheData;
    private bool _cacheDirty;

    /// <param name="theIntroDbBase">可注入基地址（DESIGN §2 D5：便于 mock 端到端）；为 null 时用官方地址。</param>
    public SegmentService(
        ShellHttpClient http,
        JsonStorage cache = null,
        Action<string> onLog = null,
        string theIntroDbBase = null,
        string introDbAppBase = null,
        string theOtherDbBase = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _cache = cache ?? new JsonStorage(AppDataDir.Instance.SkipCacheFile);
        _onLog = onLog;
        _theIntroDbBase = string.IsNullOrEmpty(theIntroDbBase) ? TheIntroDbBase : theIntroDbBase;
        _introDbAppBase = string.IsNullOrEmpty(introDbAppBase) ? IntroDbAppBase : introDbAppBase;
        _theOtherDbBase = string.IsNullOrEmpty(theOtherDbBase) ? TheOtherDbBase : theOtherDbBase;
    }

    private void Log(string message) => _onLog?.Invoke(message);

    public bool HasPendingWrite => _cacheDirty;

    /// <summary>
    /// 收集某个条目所有可用片段。
    /// <paramref name="imdbId"/> 缺失时第三方源会跳过（这是它们的匹配键）；
    /// <paramref name="embySegments"/> 由 <c>EmbyService.GetMediaSegmentsAsync</c> 预先取到，直接并入。
    /// </summary>
    public async Task<List<MediaSegmentDto>> CollectAsync(
        SkipSourceSettings settings,
        string imdbId = null,
        int? season = null,
        int? episode = null,
        double? durationSeconds = null,
        IReadOnlyList<MediaSegmentDto> embySegments = null,
        CancellationToken cancellationToken = default)
    {
        settings ??= new SkipSourceSettings();
        var results = new List<MediaSegmentDto>();

        if (settings.Emby && embySegments != null) results.AddRange(embySegments);

        var cacheKey = $"{imdbId ?? string.Empty}|{(season?.ToString() ?? string.Empty)}|{(episode?.ToString() ?? string.Empty)}";

        if (settings.TheIntroDb || settings.IntroDbApp || settings.TheOtherDb)
        {
            if (string.IsNullOrEmpty(imdbId))
            {
                Log("跳过片段：缺少 IMDb id，第三方源跳过");
            }
            else
            {
                var cached = ReadCache(cacheKey);
                if (cached != null)
                {
                    Log($"跳过片段：命中本地缓存（{cached.Count} 条）");
                    results.AddRange(cached);
                }
                else
                {
                    var tasks = new List<Task<List<MediaSegmentDto>>>();
                    if (settings.TheIntroDb) tasks.Add(FetchJsonAsync(_theIntroDbBase, imdbId, season, episode, "theintrodb", cancellationToken));
                    if (settings.IntroDbApp) tasks.Add(FetchJsonAsync(_introDbAppBase, imdbId, season, episode, "introdb.app", cancellationToken));
                    if (settings.TheOtherDb) tasks.Add(FetchJsonAsync(_theOtherDbBase, imdbId, season, episode, "theotherdb", cancellationToken));

                    var fetched = await Task.WhenAll(tasks).ConfigureAwait(false);
                    foreach (var list in fetched) results.AddRange(list);

                    if (fetched.Any(l => l.Count > 0)) WriteCache(cacheKey, results);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.CustomTemplate) && !string.IsNullOrEmpty(imdbId))
        {
            results.AddRange(await FetchCustomAsync(settings.CustomTemplate, imdbId, season, episode, cancellationToken).ConfigureAwait(false));
        }

        return Normalize(results, durationSeconds);
    }

    /// <summary>
    /// 合并去重 + 合理性过滤 + 排序。
    /// 规则：同 <c>type</c> 取并集（最早 start / 最晚 end）；<c>start &gt;= 0</c>；
    /// <c>end</c> 缺失（表示「直到片尾」）保留；<c>end &lt;= start</c> 丢弃。
    /// </summary>
    public static List<MediaSegmentDto> Normalize(IEnumerable<MediaSegmentDto> input, double? durationSeconds = null)
    {
        var byType = new Dictionary<MediaSegmentType, MediaSegmentDto>();
        if (input == null) return new List<MediaSegmentDto>();

        foreach (var seg in input)
        {
            if (seg == null || seg.StartMs < 0) continue;
            var end = seg.EndMs;
            if (end.HasValue && end.Value <= seg.StartMs) continue;
            if (durationSeconds.HasValue && durationSeconds.Value > 0 && seg.StartMs > durationSeconds.Value * 1000 + 5000)
            {
                continue; // 明显越界（源数据单位错误时兜底）
            }

            if (!byType.TryGetValue(seg.Type, out var existing))
            {
                byType[seg.Type] = seg;
                continue;
            }

            var start = Math.Min(seg.StartMs, existing.StartMs);
            long? mergedEnd;
            if (!existing.EndMs.HasValue || !end.HasValue)
            {
                mergedEnd = null; // 任一方「到片尾」⇒ 结果到片尾
            }
            else
            {
                mergedEnd = Math.Max(existing.EndMs.Value, end.Value);
            }

            byType[seg.Type] = new MediaSegmentDto
            {
                Type = seg.Type,
                StartMs = start,
                EndMs = mergedEnd,
                Source = existing.Source == seg.Source ? seg.Source : $"{existing.Source}+{seg.Source}",
            };
        }

        return byType.Values.OrderBy(s => s.StartMs).ToList();
    }

    // ── 各源抓取 ───────────────────────────────────────────────────────────

    private Task<List<MediaSegmentDto>> FetchCustomAsync(string template, string imdbId, int? season, int? episode, CancellationToken cancellationToken)
    {
        var url = template
            .Replace("{imdb}", imdbId)
            .Replace("{season}", season?.ToString() ?? string.Empty)
            .Replace("{episode}", episode?.ToString() ?? string.Empty);

        return FetchUrlAsync(url, "custom", cancellationToken);
    }

    private Task<List<MediaSegmentDto>> FetchJsonAsync(string baseUrl, string imdbId, int? season, int? episode, string source, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["imdb_id"] = imdbId };
        if (season.HasValue) query["season"] = season.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (episode.HasValue) query["episode"] = episode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return FetchUrlAsync(ShellHttpClient.BuildUri(baseUrl, query), source, cancellationToken);
    }

    private async Task<List<MediaSegmentDto>> FetchUrlAsync(string url, string source, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _http.GetAsync(url, timeout: TimeSpan.FromSeconds(8), retries: 0, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var segments = ParseSegments(result.Json, source);
            if (segments.Count > 0) Log($"跳过片段：{source} 返回 {segments.Count} 条");
            return segments;
        }
        catch (Exception ex)
        {
            Log($"跳过片段：{source} 获取失败（{ex.Message}）");
            return new List<MediaSegmentDto>();
        }
    }

    // ── 容错解析（第三方字段命名不可知，故按语义匹配）──────────────────────

    private static readonly Dictionary<string, MediaSegmentType> TypeAliases = new Dictionary<string, MediaSegmentType>(StringComparer.Ordinal)
    {
        ["intro"] = MediaSegmentType.Intro,
        ["opening"] = MediaSegmentType.Intro,
        ["title"] = MediaSegmentType.Intro,
        ["recap"] = MediaSegmentType.Recap,
        ["previously"] = MediaSegmentType.Recap,
        ["credits"] = MediaSegmentType.Credits,
        ["outro"] = MediaSegmentType.Credits,
        ["ending"] = MediaSegmentType.Credits,
        ["preview"] = MediaSegmentType.Preview,
        ["commercial"] = MediaSegmentType.Preview,
        ["advert"] = MediaSegmentType.Preview,
    };

    /// <summary>任意 <c>type</c>/键名字符串 → 内核片段类型；识别不到返回 <c>null</c>。</summary>
    public static MediaSegmentType? TypeOf(string raw)
    {
        var key = (raw ?? string.Empty).ToLowerInvariant();
        key = new string(key.Where(char.IsLetter).ToArray());
        if (key.Length == 0) return null;
        foreach (var kv in TypeAliases)
        {
            if (key.Contains(kv.Key, StringComparison.Ordinal)) return kv.Value;
        }
        return null;
    }

    /// <summary>
    /// 解析任意源的响应体为片段列表。支持三种常见形态：
    ///   1. <c>[{"type":"intro","start_ms":12000,"end_ms":102000}, …]</c>
    ///   2. <c>{"intro":{"start":12,"end":102},"outro":{"start":1380}}</c>
    ///   3. 任意嵌套（自动下钻 <c>data</c>/<c>results</c>/<c>items</c>/<c>segments</c> 等包装层）
    /// </summary>
    public static List<MediaSegmentDto> ParseSegments(JsonElement? json, string source)
    {
        var output = new List<MediaSegmentDto>();
        if (json == null) return output;
        Visit(json.Value, null, output, source);
        return output;
    }

    private static void Visit(JsonElement node, MediaSegmentType? hint, List<MediaSegmentDto> output, string source)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray()) Visit(item, hint, output, source);
            return;
        }
        if (node.ValueKind != JsonValueKind.Object) return;

        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in node.EnumerateObject())
        {
            map[p.Name.ToLowerInvariant()] = p.Value;
        }

        string RawOf(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out var v))
                {
                    var s = JsonRead.AsString(v);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return string.Empty;
        }

        var explicitType = TypeOf(RawOf("type", "segment_type", "kind", "category")) ?? hint;

        var start = TimeMsOf(map, new[] { "start", "from", "begin" });
        if (start.HasValue)
        {
            output.Add(new MediaSegmentDto
            {
                Type = explicitType ?? MediaSegmentType.Intro,
                StartMs = start.Value,
                EndMs = TimeMsOf(map, new[] { "end", "to", "finish" }),
                Source = source,
            });
            return;
        }

        // 形态 2：键名本身即类型
        var matchedByKey = false;
        foreach (var entry in map)
        {
            var kind = TypeOf(entry.Key);
            if (kind == null) continue;
            matchedByKey = true;

            var value = entry.Value;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var num))
            {
                output.Add(new MediaSegmentDto { Type = kind.Value, StartMs = ToMs(num, entry.Key), Source = source });
            }
            else if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                output.Add(new MediaSegmentDto { Type = kind.Value, StartMs = ToMs(parsed, entry.Key), Source = source });
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                Visit(value, kind, output, source);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray()) Visit(item, kind, output, source);
            }
        }
        if (matchedByKey) return;

        // 兜底：下钻包装层
        foreach (var entry in map)
        {
            var value = entry.Value;
            if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
            {
                Visit(value, null, output, source);
            }
        }
    }

    /// <summary>从 map 中挑出第一个「语义匹配」的时间字段（键名最短者优先，减少误命中）。</summary>
    public static long? TimeMsOf(Dictionary<string, JsonElement> map, string[] needles)
    {
        var candidates = map.Keys
            .Where(k => needles.Any(n => k.Contains(n, StringComparison.Ordinal)))
            .OrderBy(k => k.Length)
            .ToList();

        foreach (var key in candidates)
        {
            var value = map[key];
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var num)) return ToMs(num, key);
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)) return ToMs(parsed, key);
        }
        return null;
    }

    /// <summary>单位判定：字段名含 <c>ms</c>/<c>milli</c> ⇒ 毫秒；含 <c>sec</c> ⇒ 秒；否则按大小猜。</summary>
    public static long ToMs(double value, string fieldName)
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

    // ── 本地缓存 ───────────────────────────────────────────────────────────

    private JsonObject LoadCache()
    {
        if (_cacheData != null) return _cacheData;
        var read = _cache.ReadMap();
        _cacheData = read ?? new JsonObject();
        return _cacheData;
    }

    private List<MediaSegmentDto> ReadCache(string key)
    {
        var data = LoadCache();
        if (!data.TryGetPropertyValue(key, out var entry) || entry is not JsonObject entryObject) return null;
        if (!entryObject.TryGetPropertyValue("segments", out var items) || items is not JsonArray array) return null;

        var result = new List<MediaSegmentDto>();
        foreach (var node in array)
        {
            if (node is not JsonObject o) continue;
            var element = JsonRead.From(o);
            if (!element.HasValue) continue;
            result.Add(new MediaSegmentDto
            {
                Type = TypeOf(JsonRead.Str(element, "type")) ?? MediaSegmentType.Intro,
                StartMs = JsonRead.LongOrNull(element, "startMs") ?? 0,
                EndMs = JsonRead.LongOrNull(element, "endMs"),
                Source = JsonRead.Str(element, "source"),
            });
        }
        return result;
    }

    private void WriteCache(string key, IEnumerable<MediaSegmentDto> segments)
    {
        var data = LoadCache();
        var array = new JsonArray();
        foreach (var s in segments) array.Add(s.ToJson());

        data[key] = new JsonObject
        {
            ["updatedAt"] = DateTime.Now.ToString("O"),
            ["segments"] = array,
        };
        _cacheDirty = true;
        _cache.Write(data);
    }

    /// <summary>清空片段缓存（设置页「清除缓存」）。</summary>
    public void ClearCache()
    {
        _cacheData = new JsonObject();
        _cacheDirty = false;
        _cache.Delete();
    }
}

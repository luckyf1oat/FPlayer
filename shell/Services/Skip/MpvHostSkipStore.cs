// 原版文件（rebuild/ 中不存在 `mpv_host_skip_store.dart`），按 DESIGN §4.1 #28 与 §5 #57 归属表
// （`src/core/services/mpv_host_skip_store.dart` → `Services/Skip/MpvHostSkipStore.cs`，处置「新写」）新写。
//
// 语义：**喂给内核 `--segment=` 的那份**跳过片段（与 SkipStore 的分工：那份是用户可见/可编辑的本地片段集，
// 这份是内核参数载荷的持久化与编码）。形态依据：
//   - HOST_CONTRACT §3.6：`--segment=`（可重复），值为 **Base64(JSON)**，元素 `{type,startMs,endMs,source}`
//     例：`{ "type": "intro", "startMs": 12000, "endMs": 102000, "source": "emby" }`
//   - SERVICE_API §6.2：多源聚合结果**统一转成内核 `{type,startMs,endMs,source}`**
// 落盘：`<AppDataDir>/mpv_host_skip_segments.json`（默认），读写走 JsonStorage（原子写 + 坏文件隔离）。
// 序列化直接复用 `Models/MediaSegmentDto.ToJson()` / `ToBase64()`，不另写一份字段名。

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Skip;

/// <summary>单个条目的内核片段缓存。</summary>
public sealed class MpvHostSegmentEntry
{
    public string ItemId { get; set; } = string.Empty;

    public List<MediaSegmentDto> Segments { get; set; } = new List<MediaSegmentDto>();

    /// <summary>最后写入时间（Unix 毫秒；**未实证字段**，容错解析）。</summary>
    public long UpdatedAt { get; set; }

    public bool IsEmpty => Segments == null || Segments.Count == 0;

    /// <summary>可直接喂内核的参数载荷（Base64(JSON)）。</summary>
    public string ToBase64()
    {
        var array = ToJsonArray();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(array.ToJsonString()));
    }

    /// <summary>内核形态 JSON 数组：`[{type,startMs,endMs,source}, …]`（HOST_CONTRACT §3.6）。</summary>
    public JsonArray ToJsonArray()
    {
        var array = new JsonArray();
        if (Segments == null) return array;
        foreach (var segment in Segments)
        {
            if (segment != null) array.Add(segment.ToJson());
        }
        return array;
    }

    public JsonObject ToJson() => new JsonObject
    {
        ["updatedAt"] = UpdatedAt,
        ["segments"] = ToJsonArray(),
    };

    public static MpvHostSegmentEntry FromJson(string itemId, JsonElement? json)
    {
        var entry = new MpvHostSegmentEntry
        {
            ItemId = itemId ?? string.Empty,
            UpdatedAt = JsonRead.LongOrNull(json, "updatedAt") ?? 0L,
        };

        var segments = JsonRead.Prop(json, "segments");
        if (segments.HasValue && segments.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in segments.Value.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var segment = MediaSegmentDto.FromJson(element, "local");
                if (segment != null) entry.Segments.Add(segment);
            }
        }
        return entry;
    }

    public override string ToString() => $"MpvHostSegmentEntry({ItemId}, {Segments?.Count ?? 0} 段)";
}

/// <summary>
/// 内核跳过片段持久化（`--segment=` 的本地来源）。
/// 默认文件 <c>&lt;AppDataDir&gt;/mpv_host_skip_segments.json</c>；缺文件/坏文件一律按空表继续。
/// </summary>
public sealed class MpvHostSkipStore
{
    private const int CurrentVersion = 1;

    private static MpvHostSkipStore _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly Dictionary<string, MpvHostSegmentEntry> _entries =
        new Dictionary<string, MpvHostSegmentEntry>(StringComparer.Ordinal);
    private readonly object _sync = new object();
    private bool _loaded;

    public MpvHostSkipStore(JsonStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public static MpvHostSkipStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new MpvHostSkipStore(
                    new JsonStorage(AppDataDir.Instance.File("mpv_host_skip_segments.json")));
            }
        }
    }

    /// <summary>测试/多实例：显式指定存储文件。</summary>
    public static MpvHostSkipStore At(string filePath) => new MpvHostSkipStore(new JsonStorage(filePath));

    /// <summary>测试辅助：丢弃单例缓存。</summary>
    public static void ResetInstance()
    {
        lock (Gate)
        {
            _instance = null;
        }
    }

    public string FilePath => _storage.FilePath;

    public bool IsLoaded
    {
        get { lock (_sync) { return _loaded; } }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                EnsureLoadedLocked();
                return _entries.Count;
            }
        }
    }

    private void EnsureLoadedLocked()
    {
        if (!_loaded) LoadLocked();
    }

    public void Load()
    {
        lock (_sync)
        {
            LoadLocked();
        }
    }

    public Task LoadAsync() => Task.Run(Load);

    private void LoadLocked()
    {
        if (_loaded) return;
        _loaded = true;
        _entries.Clear();

        try
        {
            var root = _storage.ReadMap();
            if (root == null) return;

            var element = JsonRead.From(root);
            var items = JsonRead.Prop(element, "items");
            if (items == null || items.Value.ValueKind != JsonValueKind.Object) return;

            foreach (var property in items.Value.EnumerateObject())
            {
                if (property.Name.Length == 0) continue;
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                _entries[property.Name] = MpvHostSegmentEntry.FromJson(property.Name, property.Value);
            }
        }
        catch (Exception ex)
        {
            AppDataDir.Instance.Log($"内核跳过片段读取失败（按空表继续）：{FilePath}（{ex.Message}）");
        }
    }

    public void Save()
    {
        lock (_sync)
        {
            SaveLocked();
        }
    }

    public Task SaveAsync() => Task.Run(Save);

    private void SaveLocked()
    {
        EnsureLoadedLocked();

        var items = new JsonObject();
        foreach (var kv in _entries)
        {
            items[kv.Key] = kv.Value == null ? new JsonObject() : kv.Value.ToJson();
        }

        _storage.Write(new JsonObject
        {
            ["version"] = CurrentVersion,
            ["items"] = items,
        });
    }

    // ── 按 itemId 的 get / set / remove ─────────────────────────────────────

    /// <summary>取片段副本；不存在返回 <c>null</c>。</summary>
    public MpvHostSegmentEntry Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        lock (_sync)
        {
            EnsureLoadedLocked();
            if (!_entries.TryGetValue(itemId, out var entry)) return null;
            return Clone(entry);
        }
    }

    /// <summary>取片段列表（不存在返回空表）。</summary>
    public List<MediaSegmentDto> Segments(string itemId) => Get(itemId)?.Segments ?? new List<MediaSegmentDto>();

    public bool Has(string itemId) => Get(itemId) != null;

    /// <summary>写入并落盘；<c>null</c>/空表等价于删除。</summary>
    public void Set(string itemId, IEnumerable<MediaSegmentDto> segments)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        var list = new List<MediaSegmentDto>();
        if (segments != null)
        {
            foreach (var segment in segments)
            {
                if (segment != null) list.Add(segment);
            }
        }
        if (list.Count == 0)
        {
            Remove(itemId);
            return;
        }

        lock (_sync)
        {
            EnsureLoadedLocked();
            _entries[itemId] = new MpvHostSegmentEntry
            {
                ItemId = itemId,
                Segments = list,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            SaveLocked();
        }
    }

    /// <summary>已存在的条目 append（同 type 替换）；不存在则新建。</summary>
    public void Add(string itemId, MediaSegmentDto segment)
    {
        if (segment == null) return;
        var current = Segments(itemId);
        current.RemoveAll(existing => existing.Type == segment.Type);
        current.Add(segment);
        current.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
        Set(itemId, current);
    }

    /// <summary>删除并落盘（不存在返回 false，且不写盘）。</summary>
    public bool Remove(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        lock (_sync)
        {
            EnsureLoadedLocked();
            if (!_entries.Remove(itemId)) return false;
            SaveLocked();
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            EnsureLoadedLocked();
            if (_entries.Count == 0) return;
            _entries.Clear();
            SaveLocked();
        }
    }

    /// <summary>全部条目副本（itemId → 条目）。</summary>
    public Dictionary<string, MpvHostSegmentEntry> All()
    {
        lock (_sync)
        {
            EnsureLoadedLocked();
            var copy = new Dictionary<string, MpvHostSegmentEntry>(StringComparer.Ordinal);
            foreach (var kv in _entries)
            {
                copy[kv.Key] = kv.Value == null
                    ? new MpvHostSegmentEntry { ItemId = kv.Key }
                    : Clone(kv.Value);
            }
            return copy;
        }
    }

    // ── 内核参数编码（HOST_CONTRACT §3.6：`--segment=` 可重复，值为 Base64(JSON)）──

    /// <summary>单条 `--segment=` 的载荷（Base64(JSON)）；无片段返回空串。</summary>
    public string ToBase64(string itemId)
    {
        var segments = Segments(itemId);
        if (segments.Count == 0) return string.Empty;
        return Encode(segments);
    }

    /// <summary>
    /// 全部 `--segment=` 参数（**不含**前缀，调用方自行拼 `--segment=`）。
    /// 超过 <c>AppConstants.MaxSegmentsPerArgument</c> 时按批拆分，避免单条命令行过长。
    /// </summary>
    public List<string> ToSegmentArguments(string itemId)
        => ToSegmentArguments(Segments(itemId));

    /// <summary>按批编码（每批 ≤ <paramref name="perArgument"/> 条，缺省取 <c>AppConstants.MaxSegmentsPerArgument</c>）。</summary>
    public static List<string> ToSegmentArguments(IReadOnlyList<MediaSegmentDto> segments, int perArgument = 0)
    {
        var result = new List<string>();
        if (segments == null || segments.Count == 0) return result;

        var size = perArgument > 0 ? perArgument : Constants.AppConstants.MaxSegmentsPerArgument;
        if (size <= 0) size = segments.Count;

        for (var offset = 0; offset < segments.Count; offset += size)
        {
            var batch = new List<MediaSegmentDto>();
            for (var i = offset; i < segments.Count && i < offset + size; i++)
            {
                if (segments[i] != null) batch.Add(segments[i]);
            }
            if (batch.Count > 0) result.Add(Encode(batch));
        }
        return result;
    }

    /// <summary>把一批片段编码为 Base64(JSON)（字段名与 <see cref="MediaSegmentDto.ToJson"/> 一致：`type/startMs/endMs/source`）。</summary>
    public static string Encode(IReadOnlyList<MediaSegmentDto> segments)
    {
        var array = new JsonArray();
        if (segments != null)
        {
            foreach (var segment in segments)
            {
                if (segment != null) array.Add(segment.ToJson());
            }
        }
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(array.ToJsonString()));
    }

    /// <summary>单条片段的 `--segment=` 载荷（等价 <c>MediaSegmentDto.ToBase64()</c> 的数组形态）。</summary>
    public static string Encode(MediaSegmentDto segment)
        => segment == null ? string.Empty : Encode(new[] { segment });

    /// <summary>整批的 `--segment=` 参数（含前缀，便于直接 concat 进命令行）。</summary>
    public List<string> ToSegmentFlags(string itemId)
    {
        var flags = new List<string>();
        foreach (var payload in ToSegmentArguments(itemId))
        {
            flags.Add("--segment=" + payload);
        }
        return flags;
    }

    private static MpvHostSegmentEntry Clone(MpvHostSegmentEntry entry)
    {
        var copy = new MpvHostSegmentEntry
        {
            ItemId = entry.ItemId,
            UpdatedAt = entry.UpdatedAt,
            Segments = new List<MediaSegmentDto>(),
        };
        if (entry.Segments == null) return copy;
        foreach (var segment in entry.Segments)
        {
            copy.Segments.Add(new MediaSegmentDto
            {
                Type = segment.Type,
                StartMs = segment.StartMs,
                EndMs = segment.EndMs,
                Source = segment.Source,
            });
        }
        return copy;
    }

    public override string ToString() => $"MpvHostSkipStore({FilePath}, items={Count}, loaded={IsLoaded})";
}

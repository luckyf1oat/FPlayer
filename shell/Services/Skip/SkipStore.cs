// 原版文件（rebuild/ 中不存在 `skip_store.dart`），按 DESIGN §4.1 #35 与 §5 #64 归属表
// （`src/core/services/skip_store.dart` → `Services/Skip/SkipStore.cs`，处置「新写」）新写。
//
// 语义（**重建实现**，判定三态：重建实现了 —— 原版无源码可逐行对照）：
//   通用本地跳过片段存储：按 itemId 保存一组 `MediaSegmentDto`（`{type,startMs,endMs,source}`，
//   形态依据 HOST_CONTRACT §3.6 与 SERVICE_API §6.2「结果统一转成内核 --segment= 的 {type,startMs,endMs,source}」）。
//   落盘位置：`AppDataDir.Instance.SkipCacheFile`（= `skip_segments.json`，见 Infra/AppDataDir.cs）。
//   读写走 JsonStorage（原子写 + 坏文件隔离），符合 DESIGN §2 D5「JSON 一律走 System.Text.Json」。
//
// 与 MpvHostSkipStore 的分工：本类面向「用户可见/可编辑的本地片段集」；
//   MpvHostSkipStore 面向「喂给内核 --segment= 的那份」（额外负责 Base64/分段参数编码）。

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Skip;

/// <summary>单个条目的跳过片段集合（含写入时间戳，便于「最近写入优先」与排障）。</summary>
public sealed class SkipEntry
{
    public string ItemId { get; set; } = string.Empty;

    public List<MediaSegmentDto> Segments { get; set; } = new List<MediaSegmentDto>();

    /// <summary>最后写入时间（Unix 毫秒；**未实证字段**，容错解析，0 表示未知）。</summary>
    public long UpdatedAt { get; set; }

    public bool IsEmpty => Segments == null || Segments.Count == 0;

    public SkipEntry Clone()
    {
        var copy = new SkipEntry { ItemId = ItemId, UpdatedAt = UpdatedAt, Segments = new List<MediaSegmentDto>() };
        if (Segments == null) return copy;
        foreach (var segment in Segments)
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

    public JsonObject ToJson()
    {
        var array = new JsonArray();
        if (Segments != null)
        {
            foreach (var segment in Segments)
            {
                if (segment != null) array.Add(segment.ToJson());
            }
        }
        return new JsonObject
        {
            ["updatedAt"] = UpdatedAt,
            ["segments"] = array,
        };
    }

    /// <summary>容错反序列化：<c>segments</c> 数组元素非法时跳过，绝不抛异常。</summary>
    public static SkipEntry FromJson(string itemId, JsonElement? json)
    {
        var entry = new SkipEntry
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
                var segment = MediaSegmentDto.FromJson(element);
                if (segment != null) entry.Segments.Add(segment);
            }
        }
        return entry;
    }

    public override string ToString() => $"SkipEntry({ItemId}, {Segments?.Count ?? 0} 段)";
}

/// <summary>
/// 通用跳过片段本地存储（按 <c>itemId</c> 读写）。
/// 默认文件 <c>&lt;AppDataDir&gt;/skip_segments.json</c>；缺文件/坏文件/字段缺失一律按空表继续。
/// </summary>
public sealed class SkipStore
{
    private const int CurrentVersion = 1;

    private static SkipStore _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly Dictionary<string, SkipEntry> _entries = new Dictionary<string, SkipEntry>(StringComparer.Ordinal);
    private readonly object _sync = new object();
    private bool _loaded;

    public SkipStore(JsonStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public static SkipStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new SkipStore(new JsonStorage(AppDataDir.Instance.SkipCacheFile));
            }
        }
    }

    /// <summary>测试/多实例：显式指定存储文件（写法同 <c>ServerConfigStore.At</c>）。</summary>
    public static SkipStore At(string filePath) => new SkipStore(new JsonStorage(filePath));

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

    public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.Run(Load);

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
            var entries = JsonRead.Prop(element, "items");
            if (entries == null || entries.Value.ValueKind != JsonValueKind.Object) return;

            foreach (var property in entries.Value.EnumerateObject())
            {
                if (property.Name.Length == 0) continue;
                if (property.Value.ValueKind != JsonValueKind.Object) continue; // 容错：跳过非对象
                _entries[property.Name] = SkipEntry.FromJson(property.Name, property.Value);
            }
        }
        catch (Exception ex)
        {
            AppDataDir.Instance.Log($"跳过片段存储读取失败（按空表继续）：{FilePath}（{ex.Message}）");
        }
    }

    public void Save()
    {
        lock (_sync)
        {
            SaveLocked();
        }
    }

    public System.Threading.Tasks.Task SaveAsync() => System.Threading.Tasks.Task.Run(Save);

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

    /// <summary>按 itemId 取片段副本；不存在返回 <c>null</c>（**不**隐式写入空条目）。</summary>
    public SkipEntry Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        lock (_sync)
        {
            EnsureLoadedLocked();
            return _entries.TryGetValue(itemId, out var entry) ? entry.Clone() : null;
        }
    }

    /// <summary>按 itemId 取片段；不存在返回空表（调用方无需判 null）。</summary>
    public List<MediaSegmentDto> SegmentsOf(string itemId)
        => Get(itemId)?.Segments ?? new List<MediaSegmentDto>();

    /// <summary>是否存在该条目的片段。</summary>
    public bool Has(string itemId) => Get(itemId) != null;

    /// <summary>写入片段并落盘；传入 <c>null</c> 或空表等价于删除该条目。</summary>
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
            _entries[itemId] = new SkipEntry
            {
                ItemId = itemId,
                Segments = list,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            SaveLocked();
        }
    }

    /// <summary>写入单条片段。</summary>
    public void Set(string itemId, MediaSegmentDto segment)
        => Set(itemId, segment == null ? null : new[] { segment });

    /// <summary>追加片段（同 type 去重：新片段替换旧的同类型片段；`source` 不同也视为同一类）。</summary>
    public void Append(string itemId, IEnumerable<MediaSegmentDto> segments)
    {
        if (string.IsNullOrEmpty(itemId) || segments == null) return;

        var current = SegmentsOf(itemId);
        foreach (var segment in segments)
        {
            if (segment == null) continue;
            current.RemoveAll(existing => existing.Type == segment.Type);
            current.Add(segment);
        }
        current.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
        Set(itemId, current);
    }

    /// <summary>删除某条目的片段（不存在返回 false，且不落盘）。</summary>
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

    /// <summary>清空全部并落盘。</summary>
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

    /// <summary>全部条目副本（itemId → 片段），按 itemId 序号排序便于展示与对比。</summary>
    public Dictionary<string, SkipEntry> All()
    {
        lock (_sync)
        {
            EnsureLoadedLocked();
            var copy = new Dictionary<string, SkipEntry>(StringComparer.Ordinal);
            foreach (var kv in _entries)
            {
                copy[kv.Key] = kv.Value == null ? new SkipEntry { ItemId = kv.Key } : kv.Value.Clone();
            }
            return copy;
        }
    }

    /// <summary>全部 itemId 列表（序号排序）。</summary>
    public List<string> ItemIds()
    {
        var list = new List<string>(All().Keys);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>全部片段（扁平化；诊断/全量喂内核时用）。</summary>
    public List<MediaSegmentDto> AllSegments()
    {
        var result = new List<MediaSegmentDto>();
        foreach (var entry in All().Values)
        {
            if (entry.Segments == null) continue;
            foreach (var segment in entry.Segments)
            {
                if (segment != null) result.Add(segment);
            }
        }
        return result;
    }

    public override string ToString() => $"SkipStore({FilePath}, items={Count}, loaded={IsLoaded})";
}

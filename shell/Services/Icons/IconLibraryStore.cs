// 等价移植：rebuild/ai_player/lib/core/services/icon_library_service.dart。
// 映射依据：DESIGN §4.1 #23 `icon_library_store.dart` → `Services/Icons/IconLibraryStore.cs`。
// 协议依据：SERVICE_API.md §6.3（自定义源示例 `https://example.com/icons.json`）。
// 内置图标：语义 id（emby/jellyfin/navidrome/audiobookshelf/webdav/cloud/nas/live/music/movie/book/star），UI 侧映射到 FluentIcons。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.State;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

// 图标条目模型已统一到 Models/IconLibraryEntry.cs（DESIGN §4.2 的映射落点）；
// 这里用别名保持本文件既有调用点不变，且**不再重复定义**同名类型。
using IconEntry = AIPlayer.Shell.Services.Models.IconLibraryEntry;

namespace AIPlayer.Shell.Services.Icons;

/// <summary>内置图标 id → 显示名。</summary>
public static class IconLibrary
{
    public static readonly IReadOnlyDictionary<string, string> Builtin = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["emby"] = "Emby",
        ["jellyfin"] = "Jellyfin",
        ["navidrome"] = "Navidrome",
        ["audiobookshelf"] = "有声书",
        ["webdav"] = "WebDAV",
        ["cloud"] = "云端",
        ["nas"] = "NAS",
        ["live"] = "直播",
        ["music"] = "音乐",
        ["movie"] = "影视",
        ["book"] = "图书",
        ["star"] = "收藏",
    };
}

/// <summary>服务器图标库（对应 Dart <c>IconLibraryService</c>）。</summary>
public sealed class IconLibraryStore : ChangeNotifierBase
{
    private readonly JsonStorage _storage;
    private readonly ShellHttpClient _http;
    private readonly Action<string> _onLog;

    private readonly List<IconEntry> _custom = new List<IconEntry>();
    private readonly List<string> _sources = new List<string>();
    private bool _loaded;

    public IconLibraryStore(ShellHttpClient http, JsonStorage storage = null, Action<string> onLog = null)
    {
        _http = http;
        _storage = storage ?? new JsonStorage(AppDataDir.Instance.IconsFile);
        _onLog = onLog;
    }

    public IReadOnlyList<IconEntry> Custom
    {
        get
        {
            lock (_custom) return _custom.ToList();
        }
    }

    public IReadOnlyList<string> Sources
    {
        get
        {
            lock (_sources) return _sources.ToList();
        }
    }

    private void Log(string message) => _onLog?.Invoke(message);

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        var json = _storage.ReadMap();
        if (json == null) return;

        lock (_custom)
        {
            _custom.Clear();
            foreach (var element in JsonRead.Objects(JsonRead.Items(JsonRead.From(json), "custom")))
            {
                _custom.Add(IconEntry.FromJson(element));
            }
        }

        lock (_sources)
        {
            _sources.Clear();
            foreach (var s in JsonRead.StrList(JsonRead.From(json), "sources"))
            {
                _sources.Add(s);
            }
        }

        NotifyListeners();
    }

    public Task LoadAsync() => Task.Run(Load);

    public void Save()
    {
        var root = new JsonObject();
        var custom = new JsonArray();
        foreach (var entry in Custom) custom.Add(entry.ToJson());
        root["custom"] = custom;

        var sources = new JsonArray();
        foreach (var s in Sources) sources.Add(s);
        root["sources"] = sources;

        _storage.Write(root);
    }

    public Task SaveAsync() => Task.Run(Save);

    public void AddCustom(IconEntry entry)
    {
        if (entry == null) return;
        lock (_custom)
        {
            _custom.RemoveAll(e => e.Id == entry.Id);
            _custom.Add(entry);
        }
        Save();
        NotifyListeners();
    }

    public void RemoveCustom(string id)
    {
        lock (_custom)
        {
            _custom.RemoveAll(e => e.Id == id);
        }
        Save();
        NotifyListeners();
    }

    /// <summary>拉取远程图标源（JSON 数组或 <c>{icons:[...]}</c> / <c>{items:[...]}</c>）并合并。</summary>
    public async Task<int> FetchSourceAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _http.GetAsync(sourceUrl, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var raw = new List<JsonElement>();
            var json = result.Json;
            if (json.HasValue && json.Value.ValueKind == JsonValueKind.Array)
            {
                raw.AddRange(json.Value.EnumerateArray());
            }
            else
            {
                var map = result.JsonMap;
                var icons = JsonRead.Items(map, "icons");
                if (icons.Count > 0) raw.AddRange(icons);
                else raw.AddRange(JsonRead.Items(map, "items"));
            }

            var added = 0;
            foreach (var item in JsonRead.Objects(raw))
            {
                var merged = JsonRead.DeepClone(item);
                merged["source"] = sourceUrl;
                var entryElement = JsonRead.FromNode(merged.ToJsonString());
                if (!entryElement.HasValue) continue;
                var entry = IconEntry.FromJson(entryElement.Value);
                if (entry.Id.Length == 0 || entry.Url.Length == 0) continue;
                AddCustom(entry);
                added++;
            }

            lock (_sources)
            {
                if (!_sources.Contains(sourceUrl)) _sources.Add(sourceUrl);
            }
            Save();

            Log($"图标源 {sourceUrl} 载入 {added} 个图标");
            NotifyListeners();
            return added;
        }
        catch (Exception ex)
        {
            Log($"图标源载入失败：{ex.Message}");
            return 0;
        }
    }
}

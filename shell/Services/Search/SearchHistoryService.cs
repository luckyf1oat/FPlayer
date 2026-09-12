// 原版文件，按 SERVICE_API / DESIGN 新写（rebuild/ai_player 树中**不存在** search_history_service.dart）。
// 映射依据：shell/docs/DESIGN.md §5 #135
//   `src/features/search/application/search_history_service.dart` → `Services/Search/SearchHistoryService.cs`（新写）。
// 实证依据（reversed/FlutterApp 字符串 `[S]`）：
//   - 类名 `SearchHistoryService` / `SearchHistoryNotifier`、provider `searchHistoryProvider` / `searchHistoryServiceProvider`
//     （symbols_classes.txt:5803-5804；strings_all.txt:12734-12736、29363-29364、34024-34025）；
//   - 落盘文件名 `search_history.json`（strings_all.txt:34033）。
// 未实证处（容错解析 + 显式标注）：
//   1) JSON 结构取 `{ "entries": [ ... ] }`（与 servers.json 的 `{ "servers": [...] }` 同风格），
//      **同时兼容裸数组**形态，避免结构猜测错误导致历史全丢；
//   2) 历史上限取 20（无可实证数字）；
//   3) 去重按大小写不敏感比较（OrdinalIgnoreCase），避免 "Movie"/"movie" 重复占位。
// 写法参考：Services/Servers/ServerConfigStore.cs（JsonStorage + AppDataDir + ChangeNotifierBase 通知）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.State;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Search;

/// <summary>搜索历史持久化（等价原版 <c>SearchHistoryService</c>：去重 + 上限 + 最近优先）。</summary>
public sealed class SearchHistoryService : ChangeNotifierBase
{
    /// <summary>历史条目上限（未实证，取 20）。</summary>
    public const int MaxEntries = 20;

    /// <summary>落盘文件名（字符串实证：`search_history.json`）。</summary>
    public const string FileName = "search_history.json";

    private static SearchHistoryService _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly List<string> _entries = new List<string>();
    private bool _loaded;

    public SearchHistoryService(JsonStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>进程级单例（落盘于 <c>AppDataDir</c> 根目录的 <c>search_history.json</c>）。</summary>
    public static SearchHistoryService Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new SearchHistoryService(new JsonStorage(AppDataDir.Instance.File(FileName)));
            }
        }
    }

    /// <summary>测试/多实例（独立文件），与 <c>ServerConfigStore.At</c> 同风格。</summary>
    public static SearchHistoryService At(string filePath) => new SearchHistoryService(new JsonStorage(filePath));

    /// <summary>最近优先的历史条目（副本，避免调用方直接改内部列表）。访问时自动加载。</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            Load();
            return _entries.ToList();
        }
    }

    public int Count
    {
        get
        {
            Load();
            return _entries.Count;
        }
    }

    public bool IsLoaded => _loaded;

    /// <summary>从磁盘加载（幂等；等价 ServerConfigStore.Load）。损坏文件由 JsonStorage 隔离为 <c>*.corrupt</c> 并返回空历史。</summary>
    public void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _entries.Clear();

        var root = JsonRead.From(_storage.Read());
        var raw = JsonRead.StrList(root, "entries");

        // 未实证，容错解析：文件是裸数组（["a","b"]）时同样接受。
        if (raw.Count == 0 && root.HasValue && root.Value.ValueKind == JsonValueKind.Array)
        {
            raw = new List<string>();
            foreach (var item in root.Value.EnumerateArray())
            {
                raw.Add(JsonRead.AsString(item));
            }
        }

        foreach (var entry in raw)
        {
            var text = (entry ?? string.Empty).Trim();
            if (text.Length == 0) continue;
            if (_entries.Any(e => string.Equals(e, text, StringComparison.OrdinalIgnoreCase))) continue;
            _entries.Add(text);
            if (_entries.Count >= MaxEntries) break;
        }

        NotifyListeners();
    }

    public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.Run(Load);

    /// <summary>落盘（<c>{ "entries": [...] }</c>）。</summary>
    public void Save()
    {
        var array = new JsonArray();
        foreach (var entry in _entries) array.Add(entry);

        var root = new JsonObject { ["entries"] = array };
        _storage.Write(root);
        NotifyListeners();
    }

    public System.Threading.Tasks.Task SaveAsync() => System.Threading.Tasks.Task.Run(Save);

    /// <summary>追加一条（去重 → 插到最前 → 截断到上限 → 落盘）。空白查询直接忽略。</summary>
    public void Add(string query)
    {
        Load();
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0) return;

        _entries.RemoveAll(e => string.Equals(e, text, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, text); // 最近优先
        if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        Save();
    }

    /// <summary>删除一条（大小写不敏感全匹配）。</summary>
    public void Remove(string query)
    {
        Load();
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0) return;

        if (_entries.RemoveAll(e => string.Equals(e, text, StringComparison.OrdinalIgnoreCase)) > 0) Save();
    }

    /// <summary>清空历史。</summary>
    public void Clear()
    {
        Load();
        if (_entries.Count == 0) return;
        _entries.Clear();
        Save();
    }

    /// <summary>从磁盘重新读取（备份恢复后使用）。</summary>
    public void Reload()
    {
        _loaded = false;
        Load();
    }

    /// <summary>是否已包含某条（大小写不敏感）。</summary>
    public bool Contains(string query)
    {
        Load();
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        return _entries.Any(e => string.Equals(e, text, StringComparison.OrdinalIgnoreCase));
    }
}

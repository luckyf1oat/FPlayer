// 等价移植：**Dart 侧不存在 audiobook_skip_store.dart**（已用 glob/grep 全树确认：rebuild/ai_player/lib 下无该文件，
// 也无同名类）。任务书上给出的这份 Dart 路径在当前重建源里缺失 ⇒ 本文件是**重建实现**（判定三态：重建实现了，不是逆向到了）。
//
// 语义来源（均为 Dart 侧实证，逐条对应）：
//   1. 全局开关：rebuild/ai_player/lib/core/models/app_settings.dart
//        - `skipFeatureEnabled`（正向语义；传给内核 `--disable-skip-markers` 时取反，见 `disableSkipMarkers`）
//        - `skipIntroEnabled` / `skipCreditsEnabled`
//   2. 片段单位与形态：rebuild/ai_player/lib/core/services/segment_service.dart
//        - 片段以毫秒承载（`start_ms`/`end_ms`），单位判定逻辑见该文件 `_toMs`
//        - 缓存文件 `AppPaths.skipCacheFile`（= `skip_segments.json`，见 shell/Services/Infra/AppDataDir.cs SkipCacheFile）
//   3. 有声书时长单位：reversed/FlutterApp/SERVICE_API.md §3 —— ABS 的 `currentTime`/`duration` 是**秒**，
//      多音轨换算见 AbsTimeline.cs。故本存储的片头/片尾时长一律以**秒**落盘，与 ABS 进度同一单位，避免二次换算。
//
// 落盘（JsonStorage 原子写 + 容错读，写法同 shell/Services/Servers/ServerConfigStore.cs）：
//   { "version": 1, "books": { "<bookId>": { "introSeconds": 0, "creditsSeconds": 0,
//                                            "skipIntro": true, "skipCredits": true,
//                                            "updatedAt": 0 } } }
// 说明：`skipIntro`/`skipCredits` 缺省 true，配合全局 `skipIntroEnabled`/`skipCreditsEnabled` 生效
//      （全局关 → 本书这两项不生效；与 app_settings.dart 的组合语义一致）。
// 未实证字段（`updatedAt`、`version`）按容错解析，缺省不抛异常。

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.AudioBookshelf;

/// <summary>单本书的跳过设置（片头/片尾时长以**秒**承载，与 ABS 进度同单位）。</summary>
public sealed class AbsBookSkipSettings
{
    /// <summary>片头时长（秒；0 表示无边听边跳）。</summary>
    public double IntroSeconds { get; set; }

    /// <summary>片尾时长（秒；0 表示不跳片尾）。</summary>
    public double CreditsSeconds { get; set; }

    /// <summary>本书是否启用片头跳过（缺省 true，仍需全局 <c>skipIntroEnabled</c> 为真）。</summary>
    public bool SkipIntro { get; set; } = true;

    /// <summary>本书是否启用片尾跳过（缺省 true，仍需全局 <c>skipCreditsEnabled</c> 为真）。</summary>
    public bool SkipCredits { get; set; } = true;

    /// <summary>最后修改时间戳（毫秒；未实证字段，容错解析；0 表示未提供）。</summary>
    public long UpdatedAt { get; set; }

    /// <summary>是否是一条「空设置」（两项时长都为 0）——调用方据此决定要不要落盘。</summary>
    public bool IsEmpty => IntroSeconds <= 0 && CreditsSeconds <= 0;

    /// <summary>片头跳过是否实际生效：本书开关 ∧ 时长有效。</summary>
    public bool HasIntro => SkipIntro && IntroSeconds > 0;

    /// <summary>片尾跳过是否实际生效：本书开关 ∧ 时长有效。</summary>
    public bool HasCredits => SkipCredits && CreditsSeconds > 0;

    public AbsBookSkipSettings Clone() => new AbsBookSkipSettings
    {
        IntroSeconds = IntroSeconds,
        CreditsSeconds = CreditsSeconds,
        SkipIntro = SkipIntro,
        SkipCredits = SkipCredits,
        UpdatedAt = UpdatedAt,
    };

    public static AbsBookSkipSettings FromJson(JsonElement? json) => new AbsBookSkipSettings
    {
        IntroSeconds = JsonRead.Double(json, "introSeconds"),
        CreditsSeconds = JsonRead.Double(json, "creditsSeconds"),
        SkipIntro = JsonRead.Bool(json, "skipIntro", true),
        SkipCredits = JsonRead.Bool(json, "skipCredits", true),
        UpdatedAt = JsonRead.LongOrNull(json, "updatedAt") ?? 0L,
    };

    public JsonObject ToJson() => new JsonObject
    {
        ["introSeconds"] = IntroSeconds,
        ["creditsSeconds"] = CreditsSeconds,
        ["skipIntro"] = SkipIntro,
        ["skipCredits"] = SkipCredits,
        ["updatedAt"] = UpdatedAt,
    };

    public static AbsBookSkipSettings Of(double introSeconds, double creditsSeconds)
        => new AbsBookSkipSettings { IntroSeconds = introSeconds, CreditsSeconds = creditsSeconds };

    public override string ToString() =>
        $"AbsBookSkipSettings(intro={IntroSeconds:F1}s/{SkipIntro}, credits={CreditsSeconds:F1}s/{SkipCredits})";
}

/// <summary>
/// 有声书跳过设置存储（按 <c>bookId</c> 读写）。
/// 文件默认 <c>&lt;AppDataDir&gt;/audiobook_skip.json</c>；读写走 <see cref="JsonStorage"/>（原子写 + 坏文件隔离）。
/// 缺少既有文件/坏文件/字段缺失时一律返回默认值，绝不因坏文件导致启动或播放失败。
/// </summary>
public sealed class AudiobookSkipStore
{
    private const int CurrentVersion = 1;

    private static AudiobookSkipStore _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly Dictionary<string, AbsBookSkipSettings> _books =
        new Dictionary<string, AbsBookSkipSettings>(StringComparer.Ordinal);
    private bool _loaded;

    public AudiobookSkipStore(JsonStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>全局单例（落在 AppDataDir 根目录；AppDataDir 已按「便携 data/ → %APPDATA%\AIPlayer」解析）。</summary>
    public static AudiobookSkipStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new AudiobookSkipStore(new JsonStorage(AppDataDir.Instance.File("audiobook_skip.json")));
            }
        }
    }

    /// <summary>测试/多实例：显式指定存储文件（写法同 <c>ServerConfigStore.At</c>）。</summary>
    public static AudiobookSkipStore At(string filePath)
        => new AudiobookSkipStore(new JsonStorage(filePath));

    /// <summary>测试辅助：丢弃单例缓存。</summary>
    public static void ResetInstance()
    {
        lock (Gate)
        {
            _instance = null;
        }
    }

    public string FilePath => _storage.FilePath;

    public bool IsLoaded => _loaded;

    /// <summary>已登记的 bookId 数量（未加载时为 0）。</summary>
    public int Count
    {
        get
        {
            EnsureLoaded();
            return _books.Count;
        }
    }

    /// <summary>惰性加载：任何读写入口先确保已从磁盘读过一次（幂等）。</summary>
    private void EnsureLoaded()
    {
        if (!_loaded) Load();
    }

    /// <summary>从磁盘加载（幂等；<c>Load()</c> 已加载则直接返回）。坏文件由 JsonStorage 隔离为 <c>*.corrupt</c> 并返回空表。</summary>
    public void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _books.Clear();

        try
        {
            var root = _storage.ReadMap();
            if (root == null) return;

            var element = JsonRead.From(root);
            var books = JsonRead.Prop(element, "books");
            if (books == null || books.Value.ValueKind != JsonValueKind.Object) return;

            foreach (var property in books.Value.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object) continue; // 容错：跳过非对象值
                var settings = AbsBookSkipSettings.FromJson(property.Value);
                if (property.Name.Length > 0) _books[property.Name] = settings;
            }
        }
        catch (Exception ex)
        {
            AppDataDir.Instance.Log($"有声书跳过设置读取失败（按空表继续）：{FilePath}（{ex.Message}）");
        }
    }

    /// <summary>落盘（原子写；版本号便于后续迁移）。</summary>
    public void Save()
    {
        EnsureLoaded();

        var books = new JsonObject();
        foreach (var kv in _books)
        {
            books[kv.Key] = kv.Value == null ? new JsonObject() : kv.Value.ToJson();
        }

        var root = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["books"] = books,
        };
        _storage.Write(root);
    }

    /// <summary>按 bookId 取设置；不存在返回 <c>null</c>（**不**隐式写入空条目）。</summary>
    public AbsBookSkipSettings Get(string bookId)
    {
        if (string.IsNullOrEmpty(bookId)) return null;
        EnsureLoaded();
        return _books.TryGetValue(bookId, out var settings) ? settings : null;
    }

    /// <summary>按 bookId 取设置；不存在时返回默认值（两项时长 0、开关 true）。</summary>
    public AbsBookSkipSettings GetOrDefault(string bookId)
        => Get(bookId) ?? new AbsBookSkipSettings();

    /// <summary>按 bookId 写入设置并落盘（传入 <c>null</c> 等价于删除该书条目）；打上当前时间戳。</summary>
    public void Set(string bookId, AbsBookSkipSettings settings)
    {
        if (string.IsNullOrEmpty(bookId)) return;
        EnsureLoaded();

        if (settings == null)
        {
            Remove(bookId);
            return;
        }

        var stored = settings.Clone();
        stored.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _books[bookId] = stored;
        Save();
    }

    /// <summary>便捷写入：直接给片头/片尾秒数（其余开关保持默认 true）。</summary>
    public void Set(string bookId, double introSeconds, double creditsSeconds)
    {
        if (string.IsNullOrEmpty(bookId)) return;
        EnsureLoaded();

        var existing = GetOrDefault(bookId);
        existing.IntroSeconds = introSeconds;
        existing.CreditsSeconds = creditsSeconds;
        Set(bookId, existing);
    }

    /// <summary>删除某本书的设置并落盘；不存在时不做任何写入（返回 false）。</summary>
    public bool Remove(string bookId)
    {
        if (string.IsNullOrEmpty(bookId)) return false;
        EnsureLoaded();
        if (!_books.Remove(bookId)) return false;
        Save();
        return true;
    }

    /// <summary>清空全部条目并落盘。</summary>
    public void Clear()
    {
        EnsureLoaded();
        if (_books.Count == 0) return;
        _books.Clear();
        Save();
    }

    /// <summary>全部条目的副本（bookId → 设置），按 key 排序便于展示/对比。</summary>
    public Dictionary<string, AbsBookSkipSettings> All()
    {
        EnsureLoaded();
        var copy = new Dictionary<string, AbsBookSkipSettings>(StringComparer.Ordinal);
        foreach (var kv in _books)
        {
            copy[kv.Key] = kv.Value == null ? new AbsBookSkipSettings() : kv.Value.Clone();
        }
        return copy;
    }

    /// <summary>
    /// 片头跳过点（秒）：<c>skipIntro && introSeconds &gt; 0</c> 时返回该秒数，否则 <c>null</c>
    /// （等价 app_settings.dart 中 <c>skipIntroEnabled</c> 与本书开关的组合判断）。
    /// </summary>
    public double? IntroSkipSeconds(string bookId)
    {
        var settings = Get(bookId);
        return settings != null && settings.HasIntro ? settings.IntroSeconds : (double?)null;
    }

    /// <summary>
    /// 片尾起点（秒）：给全书时长 <paramref name="durationSeconds"/> 与本书片尾时长，返回「应从此处开始跳过」的秒数；
    /// 片尾长度 &lt;= 0 或时长不足时返回 <c>null</c>（不误跳）。
    /// </summary>
    public double? CreditsStartSeconds(string bookId, double durationSeconds)
    {
        var settings = Get(bookId);
        if (settings == null || !settings.HasCredits) return null;
        if (durationSeconds <= 0 || settings.CreditsSeconds <= 0) return null;
        var start = durationSeconds - settings.CreditsSeconds;
        return start < 0 ? 0 : start;
    }

    public override string ToString() => $"AudiobookSkipStore({FilePath}, books={_books.Count}, loaded={_loaded})";
}

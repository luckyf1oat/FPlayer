// 对应 DESIGN §4.1 #27 `mpv_host_player_settings_store.dart` → `Services/Settings/MpvHostPlayerSettingsStore.cs`
// （修订 r20 把该条从 `Services/Kernel/` 移到 `Services/Settings/`：纯 KV 持久化，无内核依赖）。
// ⚠️ 三态标注：该 Dart 文件 **在 rebuild 树中不存在**（原版文件）⇒ 本文件属**重建实现**。
//
// 机制实证（内核侧，非推断）：
//   - `reversed/MpvHost/WinUISample/GlobalDependencies.cs:37`
//     `Kernel.GetRequiredService<ISettingsToolkit>().ReadLocalSetting("LogLevel", "information")`
//     ⇒ 内核播放器确实从 **本地设置** 读 `LogLevel`（默认 `information`），键名大小写为 `LogLevel`。
//   - `ISettingsToolkit` / `DesktopSettingsToolkit`（`reversed/MpvHost/WinUISample.Toolkits/DesktopSettingsToolkit.cs:9`）
//     是内核自己的键值工具；S-1 禁止服务层引用它，故本类提供**同名的外壳侧镜像存储**，
//     由 App 层在启动时把需要的键喂给内核（或写入内核自己的设置文件），服务层不做内核类型映射。
//
// 存储位置：与其它服务一致，落 `AppDataDir` 下的 JSON（原子写 + 坏文件隔离），**不读不写内核私有格式**。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Settings;

/// <summary>内核播放器设置的外壳侧镜像存储（对应原版 <c>mpv_host_player_settings_store</c>；重建实现）。</summary>
public sealed class MpvHostPlayerSettingsStore
{
    /// <summary>内核读取的日志级别键（实证：`GlobalDependencies.cs:37`）。</summary>
    public const string LogLevelKey = "LogLevel";

    public const string DefaultLogLevel = "information";

    private static MpvHostPlayerSettingsStore _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _loaded;

    public MpvHostPlayerSettingsStore(JsonStorage storage = null)
    {
        _storage = storage ?? new JsonStorage(System.IO.Path.Combine(AppDataDir.Instance.Root, "player_settings.json"));
    }

    public static MpvHostPlayerSettingsStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new MpvHostPlayerSettingsStore();
            }
        }
    }

    /// <summary>测试/多实例。</summary>
    public static MpvHostPlayerSettingsStore At(string filePath) => new MpvHostPlayerSettingsStore(new JsonStorage(filePath));

    public string FilePath => _storage.FilePath;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _values.Clear();

        var json = _storage.ReadMap();
        if (json == null) return;
        foreach (var kv in json)
        {
            _values[kv.Key] = kv.Value == null ? null : kv.Value.ToJsonString().Trim('"');
        }
    }

    public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.Run(Load);

    /// <summary>等价内核 <c>ISettingsToolkit.ReadLocalSetting(key, default)</c> 的读取语义（缺省回落默认值）。</summary>
    public string ReadLocalSetting(string key, string defaultValue = null)
    {
        Load();
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return _values.TryGetValue(key, out var v) && v != null ? v : defaultValue;
    }

    public void SetLocalSetting(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        Load();
        if (value == null) _values.Remove(key);
        else _values[key] = value;
        Save();
    }

    public bool Remove(string key)
    {
        Load();
        var removed = _values.Remove(key);
        if (removed) Save();
        return removed;
    }

    public void Clear()
    {
        Load();
        _values.Clear();
        Save();
    }

    public IReadOnlyDictionary<string, string> All()
    {
        Load();
        return new Dictionary<string, string>(_values, StringComparer.Ordinal);
    }

    // ── 强类型便捷（内核侧确实按字符串读，这里只做外壳侧的易用封装）──────────

    public int ReadInt(string key, int defaultValue = 0)
    {
        var raw = ReadLocalSetting(key, null);
        return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    public double ReadDouble(string key, double defaultValue = 0)
    {
        var raw = ReadLocalSetting(key, null);
        return raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    public bool ReadBool(string key, bool defaultValue = false)
    {
        var raw = ReadLocalSetting(key, null);
        return raw != null && bool.TryParse(raw, out var v) ? v : defaultValue;
    }

    public void SetInt(string key, int value) => SetLocalSetting(key, value.ToString(CultureInfo.InvariantCulture));

    public void SetDouble(string key, double value) => SetLocalSetting(key, value.ToString("R", CultureInfo.InvariantCulture));

    public void SetBool(string key, bool value) => SetLocalSetting(key, value ? "true" : "false");

    // ── 日志级别（内核实证键）──────────────────────────────────────────────

    /// <summary>读日志级别；缺省 <c>information</c>（与内核 `GlobalDependencies.cs:37` 的默认值一致）。</summary>
    public string LogLevel => ReadLocalSetting(LogLevelKey, DefaultLogLevel);

    /// <summary>设置日志级别。取值建议与内核 <c>LogEventLevel</c> 小写名一致：verbose/debug/information/warning/error/fatal。</summary>
    public void SetLogLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level)) level = DefaultLogLevel;
        SetLocalSetting(LogLevelKey, level.Trim().ToLowerInvariant());
    }

    public void Save()
    {
        var root = new System.Text.Json.Nodes.JsonObject();
        foreach (var kv in _values) root[kv.Key] = kv.Value;
        _storage.Write(root);
    }

    public System.Threading.Tasks.Task SaveAsync() => System.Threading.Tasks.Task.Run(Save);
}

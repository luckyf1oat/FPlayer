// 等价移植：rebuild/ai_player/lib/core/services/settings_service.dart。
// 映射依据：DESIGN §4.1 #34 `settings_service.dart` → `Services/Settings/SettingsService.cs`。
// 语义：settings.json 持久化 + 变更通知（原 ChangeNotifier → ChangeNotifierBase）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.State;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Settings;

public sealed class SettingsService : ChangeNotifierBase
{
    private static SettingsService _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private AppSettings _settings = new AppSettings();
    private bool _loaded;

    public SettingsService(JsonStorage storage = null)
    {
        _storage = storage ?? new JsonStorage(AppDataDir.Instance.SettingsFile);
    }

    public static SettingsService Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new SettingsService();
            }
        }
    }

    /// <summary>测试/多实例。</summary>
    public static SettingsService At(string settingsFilePath) => new SettingsService(new JsonStorage(settingsFilePath));

    public AppSettings Settings => _settings;

    public bool IsLoaded => _loaded;

    /// <summary>启动时加载一次（幂等）。</summary>
    public void Load()
    {
        if (_loaded) return;
        var json = _storage.ReadMap();
        if (json != null)
        {
            // t92（t78 的 F1）：只读兼容层 —— 语义等价的旧名折算进来（现行键优先），风险项只登记不生效。
            // 折算只发生在**内存副本**上：盘上原文一个字节都不动（Save 仍走 t80 的合并写盘）。
            var effective = SettingsCompat.BuildEffective(json, out var compat);
            var element = JsonRead.From(effective);
            if (element.HasValue) _settings = AppSettings.FromJson(element.Value);
            SettingsCompat.LogReport(compat);
        }
        _loaded = true;
        // t65：把盘上的 UA 喂给唯一构造点（脏值由 UserAgentPolicy 回落默认，启动路径不抛）
        UserAgentPolicy.Apply(_settings);
        // t61：代理同理推给进程级策略（否则 15 处 `new ShellHttpClient()` 仍直连）
        ProxyPolicy.Apply(_settings);
        NotifyListeners();
    }

    public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.Run(Load);

    /// <summary>t80：上一次 <see cref="Save"/> 是否因"盘上文件不可解析/非对象/空文件"而**拒绝写盘**（内存态仍生效）。</summary>
    public bool LastSaveRejected { get; private set; }

    /// <summary>t80：拒绝原因（供日志/自检/证据读取）。</summary>
    public string LastSaveRejectReason { get; private set; } = string.Empty;

    /// <summary>
    /// 整体替换并落盘。
    /// <para>t80 **存盘保底**：以盘上现有 JSON 为底，只覆盖**已建模键** ⇒ 未建模/他人写入的键原样留存；
    /// 盘上文件**不可解析 / 根不是对象 / 空文件** ⇒ **拒绝写盘**（绝不把用户文件写成空对象），内存态仍生效并留可见日志。</para>
    /// </summary>
    public void Save(AppSettings next)
    {
        // t80-F1 加固（captain 2026-09-12 裁定）：让"Save 发生在 Load 之前"当场可见 —— **只加日志、不改行为**。
        var wasLoaded = _loaded;
        var diskExistedBefore = File.Exists(_storage.FilePath);
        if (!wasLoaded)
        {
            DebugLog.Warn($"SETTINGS-SAVE-BEFORE-LOAD file={_storage.FilePath} loadedBefore={wasLoaded} diskExists={diskExistedBefore}");
        }

        _settings = next ?? new AppSettings();
        _loaded = true;

        JsonObject baseline = null;
        LastSaveRejected = false;
        LastSaveRejectReason = string.Empty;
        var quarantinedPath = _storage.FilePath + ".corrupt";
        if (!File.Exists(_storage.FilePath) && File.Exists(quarantinedPath))
        {
            // t80-F1（甲）：文件已被 JsonStorage 隔离成 *.corrupt ⇒ **隔离是用户唯一的恢复路径**，
            // 若紧接着写一份全新默认文件，"原文还在"就会变成"看起来没救"（与"失败面不得静默覆盖"自相矛盾）
            // ⇒ 拒绝写盘，并在日志里显式点名隔离文件。
            LastSaveRejected = true;
            LastSaveRejectReason = "quarantined";
        }
        else if (File.Exists(_storage.FilePath))
        {
            try
            {
                var text = File.ReadAllText(_storage.FilePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    LastSaveRejected = true;
                    LastSaveRejectReason = "empty-file";
                }
                else
                {
                    baseline = JsonNode.Parse(text) as JsonObject;
                    if (baseline == null)
                    {
                        LastSaveRejected = true;
                        LastSaveRejectReason = "non-object-root";
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or ArgumentException)
            {
                LastSaveRejected = true;
                LastSaveRejectReason = ex.GetType().Name;
            }
        }

        if (LastSaveRejected)
        {
            // 保留盘上原文：不得覆盖（否则用户数据被写成空对象/被截断）
            var hint = LastSaveRejectReason == "quarantined"
                ? $"（原文仍在隔离文件 {quarantinedPath}，本函数**不新建** settings.json；修好原文或人工恢复后再保存）"
                : "（盘上原文保留，内存态仍生效）";
            DebugLog.Warn($"SETTINGS-SAVE-REJECTED reason={LastSaveRejectReason} file={_storage.FilePath}{hint}");
        }
        else
        {
            _storage.Write(AppSettings.MergeModeledKeys(baseline, _settings));
        }

        // t65：落盘即生效（盘上脏值回落默认）
        UserAgentPolicy.Apply(_settings);
        // t61：代理同理（改设置 ⇒ 下一次请求即生效，免重启）
        ProxyPolicy.Apply(_settings);
        NotifyListeners();
    }

    public System.Threading.Tasks.Task SaveAsync(AppSettings next) => System.Threading.Tasks.Task.Run(() => Save(next));

    /// <summary>当前生效的 User-Agent（= <see cref="UserAgentPolicy.Current"/>，供 UI/自检读取）。</summary>
    public string EffectiveUserAgent => UserAgentPolicy.Current;

    /// <summary>
    /// t65 设置 User-Agent：**校验 → 落盘 → 立即生效**（顺序不可换：先校验，非法值绝不写盘）。
    /// · 空/纯空白 ⇒ **盘上落空串**（用户意图 = "用默认"），生效值由 <see cref="UserAgentPolicy"/> 解析为默认 ⇒ 不抛、不发空 UA；
    ///   落空串而不是默认字面量，是为了让设置页输入框保存后**仍是空**（"留空回落默认"这条反控才可反复验证，UI 不会把默认值渲染成实体文本）；
    /// · 非法（控制字符/超长）⇒ 返回 false + 可读 <paramref name="error"/>，并留一条**可见**日志，**不写盘**。
    /// </summary>
    public bool TrySetUserAgent(string raw, out string error)
    {
        if (!UserAgentPolicy.TryNormalize(raw, out var normalized, out error))
        {
            DebugLog.Warn($"UA-REJECT reason={error} rawLen={(raw ?? string.Empty).Length} file={_storage.FilePath}");
            return false;
        }

        var persisted = string.IsNullOrWhiteSpace(raw) ? string.Empty : normalized;
        Patch(s =>
        {
            s.UserAgent = persisted;
            return s;
        });
        DebugLog.Info($"UA-SET persistedLen={persisted.Length} effective={UserAgentPolicy.Current} custom={UserAgentPolicy.Current != UserAgentPolicy.Default}");
        error = null;
        return true;
    }

    /// <summary>局部更新（推荐：避免遗漏字段）。</summary>
    public void Patch(Func<AppSettings, AppSettings> transform)
    {
        if (transform == null) return;
        Save(transform(_settings));
    }

    /// <summary>恢复默认（设置页「重置设置」）。**t80-F1 加固**：调用即留痕（**只加日志、不改行为**）。</summary>
    public void ResetToDefaults()
    {
        DebugLog.Warn($"SETTINGS-RESET-TO-DEFAULTS file={_storage.FilePath} loaded={_loaded} diskExists={File.Exists(_storage.FilePath)}");
        Save(new AppSettings());
    }

    /// <summary>t80 一次性回填的结果（供证据/自检读取）。</summary>
    public sealed class BackfillResult
    {
        public string BackupPath { get; set; } = string.Empty;
        public int BackupKeyCount { get; set; }
        public int CurrentKeyCount { get; set; }
        public List<string> AddedKeys { get; } = new List<string>();
        public List<string> AlreadyPresent { get; } = new List<string>();
        public int ModeledKeysSkipped { get; set; }
        public bool Written { get; set; }
        public string BackupOfCurrent { get; set; } = string.Empty;
        public string BeforeSha12 { get; set; } = string.Empty;
        public string AfterSha12 { get; set; } = string.Empty;

        public string Summary =>
            $"added={AddedKeys.Count} already={AlreadyPresent.Count} skippedModeled={ModeledKeysSkipped} " +
            $"written={Written} before={BeforeSha12} after={AfterSha12} backupOfCurrent={BackupOfCurrent}";
    }

    /// <summary>
    /// t80 **一次性回填**：把迁移前备份里**我们未建模**的键并回真实 <c>settings.json</c> —— **只增不改**。
    /// <list type="bullet">
    /// <item>已建模键（<see cref="AppSettings.ModeledKeys"/>）**一个都不碰**；盘上已存在的键也不碰；</item>
    /// <item>无新增 ⇒ **不写盘**（纯只读读数不得写盘）；</item>
    /// <item>要写盘 ⇒ 先建带时刻的备份 <c>settings.json.pre-backfill-&lt;yyyyMMdd-HHmmss&gt;.bak</c>。</item>
    /// </list>
    /// 备份不存在 / 不可解析 ⇒ 返回 null（不抛、不写）。
    /// </summary>
    public BackfillResult BackfillUnmodeledKeys(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath)) return null;

        JsonObject backup;
        try
        {
            backup = JsonNode.Parse(File.ReadAllText(backupPath)) as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or ArgumentException)
        {
            DebugLog.Warn($"SETTINGS-BACKFILL-REJECTED reason={ex.GetType().Name} backup={backupPath}");
            return null;
        }
        if (backup == null) return null;

        var result = new BackfillResult { BackupPath = backupPath, BackupKeyCount = backup.Count };

        JsonObject current = null;
        if (File.Exists(_storage.FilePath))
        {
            result.BeforeSha12 = Sha12(_storage.FilePath);
            try
            {
                current = JsonNode.Parse(File.ReadAllText(_storage.FilePath)) as JsonObject;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or ArgumentException)
            {
                // 目标文件不可解析 ⇒ 拒绝回填（不得把它写成"只剩我们懂的键"的新文件）
                DebugLog.Warn($"SETTINGS-BACKFILL-REJECTED reason={ex.GetType().Name} file={_storage.FilePath}");
                return null;
            }
        }
        current ??= new JsonObject();
        result.CurrentKeyCount = current.Count;

        foreach (var kv in backup)
        {
            if (AppSettings.ModeledKeys.Contains(kv.Key)) { result.ModeledKeysSkipped++; continue; }
            if (current.ContainsKey(kv.Key)) { result.AlreadyPresent.Add(kv.Key); continue; }
            current[kv.Key] = kv.Value?.DeepClone();
            result.AddedKeys.Add(kv.Key);
        }

        if (result.AddedKeys.Count == 0)
        {
            result.Written = false;
            result.AfterSha12 = result.BeforeSha12;
            DebugLog.Info($"SETTINGS-BACKFILL no-op（无新增未建模键）file={_storage.FilePath} skippedModeled={result.ModeledKeysSkipped}");
            return result;
        }

        if (File.Exists(_storage.FilePath))
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            result.BackupOfCurrent = _storage.FilePath + ".pre-backfill-" + stamp + ".bak";
            File.Copy(_storage.FilePath, result.BackupOfCurrent, overwrite: true);
        }

        _storage.Write(current);
        result.Written = true;
        result.AfterSha12 = Sha12(_storage.FilePath);
        DebugLog.Info($"SETTINGS-BACKFILL done added={result.AddedKeys.Count} skippedModeled={result.ModeledKeysSkipped} write={result.Written} before={result.BeforeSha12} after={result.AfterSha12} backup={result.BackupOfCurrent}");
        return result;
    }

    /// <summary>文件内容的 sha256 前 12 位（大写；文件不存在 ⇒ 空串）。</summary>
    public static string Sha12(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).Substring(0, 12);
    }

    /// <summary>从磁盘重新读取（备份恢复后使用）。</summary>
    public void Reload()
    {
        _loaded = false;
        Load();
    }

    /// <summary>设置文件是否已存在（UI 首次启动提示用）。</summary>
    public bool Exists => File.Exists(_storage.FilePath);
}

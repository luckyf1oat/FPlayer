// S5 体验设施（t33）：快捷键组的**落盘 + 冲突校验**。
// 模型复用既有 `Models/PlayerShortcuts.cs` 的 HostShortcuts（12 个键码 + 两个秒数），本类只做持久化与校验，
// **不新造第二份键位模型**。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;

namespace AIPlayer.Shell.Services.Settings;

/// <summary>一次冲突校验的结果。</summary>
public sealed class ShortcutConflictReport
{
    public List<string> Conflicts { get; } = new List<string>();

    public bool HasConflict => Conflicts.Count > 0;

    public override string ToString()
        => HasConflict ? "冲突：" + string.Join("；", Conflicts) : "无冲突";
}

/// <summary>快捷键组存储（<c>&lt;数据根&gt;\shortcuts.json</c>）。</summary>
public sealed class PlayerShortcutsStore
{
    private readonly JsonStorage _storage;
    private readonly Action<string> _log;

    public PlayerShortcutsStore(AppDataDir data, Action<string> log = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        _log = log;
        _storage = new JsonStorage(data.File("shortcuts.json"));
    }

    public string FilePath => _storage.FilePath;

    /// <summary>读；文件缺失/损坏 ⇒ 默认键位（**不抛**）。</summary>
    public HostShortcuts Load()
    {
        try
        {
            var node = _storage.Read() as JsonObject;
            if (node == null) return new HostShortcuts();
            var json = System.Text.Json.JsonDocument.Parse(node.ToJsonString());
            return HostShortcuts.FromJson(json.RootElement);
        }
        catch (Exception ex) when (ex is IOException || ex is System.Text.Json.JsonException)
        {
            _log?.Invoke($"SHORTCUTS 读取失败（用默认键位）：{ex.GetType().Name}");
            return new HostShortcuts();
        }
    }

    /// <summary>写（整对象覆盖）。</summary>
    public void Save(HostShortcuts shortcuts)
    {
        if (shortcuts == null) return;
        _storage.Write(shortcuts.ToJson());
        _log?.Invoke($"SHORTCUTS 落盘 {shortcuts.ToBase64()}");
    }

    /// <summary>
    /// 冲突校验：**同一键码被两个不同动作占用**即冲突（秒数项不参与）。
    /// 返回逐条命中：<c>PlayPause 与 ToggleMute 同为 77</c>。
    /// </summary>
    public static ShortcutConflictReport Validate(HostShortcuts shortcuts)
    {
        var report = new ShortcutConflictReport();
        if (shortcuts == null)
        {
            report.Conflicts.Add("键位对象为空");
            return report;
        }

        var actions = new (string Name, int Code)[]
        {
            ("PlayPause", shortcuts.PlayPause),
            ("SeekForward", shortcuts.SeekForward),
            ("SeekBackward", shortcuts.SeekBackward),
            ("VolumeUp", shortcuts.VolumeUp),
            ("VolumeDown", shortcuts.VolumeDown),
            ("ToggleFullscreen", shortcuts.ToggleFullscreen),
            ("ToggleMute", shortcuts.ToggleMute),
            ("NextEpisode", shortcuts.NextEpisode),
            ("PreviousEpisode", shortcuts.PreviousEpisode),
            ("SpeedUp", shortcuts.SpeedUp),
            ("SpeedDown", shortcuts.SpeedDown),
            ("ResetSpeed", shortcuts.ResetSpeed),
        };

        foreach (var group in actions.GroupBy(a => a.Code).Where(g => g.Count() > 1))
        {
            report.Conflicts.Add($"{string.Join(" 与 ", group.Select(a => a.Name))} 同为 {group.Key.ToString(CultureInfo.InvariantCulture)}");
        }
        return report;
    }

    /// <summary>读 + 校验一步到位。</summary>
    public ShortcutConflictReport LoadAndValidate() => Validate(Load());
}

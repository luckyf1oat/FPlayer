using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Richasy.WinUIKernel.Share;
using Richasy.WinUIKernel.Share.Toolkits;
using WinUISample.ViewModels;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// `ISettingsToolkit` 的外壳实现。
/// 内核的 `DesktopSettingsToolkit` 是 `internal sealed`（`reversed/MpvHost/WinUISample.Toolkits/DesktopSettingsToolkit.cs:9`），
/// 外壳**无法复用**，故按源码 1:1 复刻语义：一个 `Dictionary&lt;string,string&gt;` 的 JSON 文件，
/// 落盘位置与内核对齐 —— `&lt;AppDataRoot&gt;\player\settings.json`。
///
/// ⚠️ **这是用户真实内核配置文件**（事实 91/80）：本类**只做读-改-写**（`LoadSettings` 先读盘、
/// `SaveSettings` 写回合并后的字典，**不是**凭空重建），并且**首次覆盖写入前自动备份** `settings.json.bak-&lt;yyyyMMdd-HHmmss&gt;`。
/// 禁止任何"整文件覆盖"式写法。
///
/// 来源：自 `shell/Spike/ShellToolkits.cs` 搬迁（captain 裁决 ⑤）。与 Spike 版的差异：命名空间、
/// `KernelHost.AppDataRoot` → `KernelBridge.AppDataRoot`、以及上面这条**新增的备份**。
/// </summary>
public sealed class ShellSettingsToolkit : ISettingsToolkit
{
    private readonly string _settingsFilePath;
    private readonly object _lock = new object();
    private Dictionary<string, string> _settings;
    private bool _backedUp;

    public ShellSettingsToolkit()
    {
        _settingsFilePath = Path.Combine(KernelBridge.AppDataRoot, "player", "settings.json");
    }

    public bool IsSettingKeyExist(string settingName)
    {
        lock (_lock) { return LoadSettings().ContainsKey(settingName); }
    }

    public T ReadLocalSetting<T>(string settingName, T defaultValue)
    {
        lock (_lock)
        {
            if (!LoadSettings().TryGetValue(settingName, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }
            try
            {
                var parsed = JsonSerializer.Deserialize<T>(value);
                return parsed != null ? parsed : defaultValue;
            }
            catch (Exception)
            {
                // 存量值不是本类型的合法 JSON（类型改过/被手改）⇒ 回落调用方默认值，不让读设置失败升级成启动失败
                return defaultValue;
            }
        }
    }

    public void WriteLocalSetting<T>(string settingName, T value)
    {
        lock (_lock)
        {
            var settings = LoadSettings();
            settings[settingName] = JsonSerializer.Serialize(value);
            SaveSettings(settings);
        }
    }

    public void DeleteLocalSetting(string settingName)
    {
        lock (_lock)
        {
            var settings = LoadSettings();
            if (settings.Remove(settingName))
            {
                SaveSettings(settings);
            }
        }
    }

    private Dictionary<string, string> LoadSettings()
    {
        if (_settings != null)
        {
            return _settings;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
        if (!File.Exists(_settingsFilePath))
        {
            _settings = new Dictionary<string, string>(StringComparer.Ordinal);
            return _settings;
        }
        try
        {
            _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_settingsFilePath))
                        ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // 设置文件损坏/被并发写截断 ⇒ 当"空设置"继续（下次写入会重建）；**不删文件、下次启动仍可复核**
            _settings = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        return _settings;
    }

    private void SaveSettings(Dictionary<string, string> settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));

        // 首次覆盖写前备份用户真实配置（事实 80/91 纪律：动用户数据前先留证、绝不整文件覆盖）
        if (!_backedUp && File.Exists(_settingsFilePath))
        {
            try
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var backup = _settingsFilePath + ".bak-" + stamp;
                File.Copy(_settingsFilePath, backup, overwrite: false);
                Program.Log("SETTINGS-BACKUP " + Path.GetFileName(backup)
                    + " bytes=" + new FileInfo(backup).Length);
            }
            catch (Exception ex)
            {
                // 备份失败 ⇒ 宁可不写，也不要用未备份的写入碰用户配置
                Program.Log("SETTINGS-BACKUP FAIL " + ex.GetType().Name + ": " + ex.Message
                    + " ⇒ 放弃本次写入");
                return;
            }
        }

        _backedUp = true;
        File.WriteAllText(_settingsFilePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>
/// `IXamlRootProvider` 的外壳实现。内核同名类 `internal sealed`，无法复用。
/// 内核实现只有一行：`XamlRoot => this.Get&lt;AppViewModel&gt;().ActivateXamlRoot`（`XamlRootProvider.cs:9`），故此处等价复刻。
/// </summary>
public sealed class ShellXamlRootProvider : IXamlRootProvider
{
    public XamlRoot XamlRoot =>
        KernelBridge.Current?.Services?.GetService<AppViewModel>()?.ActivateXamlRoot;
}

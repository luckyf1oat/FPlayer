using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Richasy.WinUIKernel.Share.Toolkits;

namespace WinUISample.Toolkits;

internal sealed class DesktopSettingsToolkit : ISettingsToolkit
{
	private readonly string _settingsFilePath;

	private readonly object _lock = new object();

	private Dictionary<string, string>? _settings;

	public DesktopSettingsToolkit()
	{
		_settingsFilePath = Path.Combine(App.AppDataRoot, "player", "settings.json");
	}

	public bool IsSettingKeyExist(string settingName)
	{
		lock (_lock)
		{
			return LoadSettings().ContainsKey(settingName);
		}
	}

	public T ReadLocalSetting<T>(string settingName, T defaultValue)
	{
		lock (_lock)
		{
			if (!LoadSettings().TryGetValue(settingName, out string value) || string.IsNullOrWhiteSpace(value))
			{
				return defaultValue;
			}
			try
			{
				T val = JsonSerializer.Deserialize<T>(value);
				return (T)((val != null) ? val : ((object)defaultValue));
			}
			// t235：命名类型 = `Exception`，**不能再窄** —— `JsonSerializer.Deserialize<T>` 可抛 `JsonException` /
			// `NotSupportedException` / `ArgumentException`，类型不符另见 `InvalidCastException` ⇒ 共同祖先只有
			// `Exception`（`T` 是本方法泛型参数，异常集合随 `T` 变）⇒ 契约 = 坏值一律回落 `defaultValue`。
			catch (Exception)
			{
				return defaultValue;
			}
		}
	}

	public void WriteLocalSetting<T>(string settingName, T value)
	{
		lock (_lock)
		{
			Dictionary<string, string> dictionary = LoadSettings();
			dictionary[settingName] = JsonSerializer.Serialize(value);
			SaveSettings(dictionary);
		}
	}

	public void DeleteLocalSetting(string settingName)
	{
		lock (_lock)
		{
			Dictionary<string, string> dictionary = LoadSettings();
			if (dictionary.Remove(settingName))
			{
				SaveSettings(dictionary);
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
			string json = File.ReadAllText(_settingsFilePath);
			_settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.Ordinal);
		}
		// t235：命名类型 = `Exception`，**不能再窄** —— `File.ReadAllText`（`IOException` / `UnauthorizedAccessException` /
		// `DirectoryNotFoundException`）与 `JsonSerializer.Deserialize`（`JsonException` / `NotSupportedException`）
		// 的可抛集合不同 ⇒ 共同祖先只有 `Exception`；契约 = 设置档不可读/坏档 ⇒ 用空表继续（不崩、不丢会话）。
		catch (Exception)
		{
			_settings = new Dictionary<string, string>(StringComparer.Ordinal);
		}
		return _settings;
	}

	private void SaveSettings(Dictionary<string, string> settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
		File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}
}

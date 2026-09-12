using System;
using System.Text;
using System.Text.Json;
using Windows.System;

namespace WinUISample.Models;

public sealed class PlayerShortcuts
{
	public VirtualKey PlayPause { get; init; } = VirtualKey.Space;

	public VirtualKey SeekForward { get; init; } = VirtualKey.Right;

	public VirtualKey SeekBackward { get; init; } = VirtualKey.Left;

	public VirtualKey VolumeUp { get; init; } = VirtualKey.Up;

	public VirtualKey VolumeDown { get; init; } = VirtualKey.Down;

	public VirtualKey ToggleFullscreen { get; init; } = VirtualKey.F;

	public VirtualKey ToggleMute { get; init; } = VirtualKey.M;

	public VirtualKey NextEpisode { get; init; } = (VirtualKey)190;

	public VirtualKey PreviousEpisode { get; init; } = (VirtualKey)188;

	public VirtualKey SpeedUp { get; init; } = (VirtualKey)221;

	public VirtualKey SpeedDown { get; init; } = (VirtualKey)219;

	public VirtualKey ResetSpeed { get; init; } = VirtualKey.Back;

	public int SeekForwardSeconds { get; init; } = 10;

	public int SeekBackwardSeconds { get; init; } = 10;

	public static PlayerShortcuts Default { get; } = new PlayerShortcuts();

	public static PlayerShortcuts FromBase64(string encoded)
	{
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			byte[] bytes = Convert.FromBase64String(text);
			using JsonDocument jsonDocument = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
			JsonElement rootElement = jsonDocument.RootElement;
			return new PlayerShortcuts
			{
				PlayPause = GetVKey(rootElement, "playPause", VirtualKey.Space),
				SeekForward = GetVKey(rootElement, "seekForward", VirtualKey.Right),
				SeekBackward = GetVKey(rootElement, "seekBackward", VirtualKey.Left),
				VolumeUp = GetVKey(rootElement, "volumeUp", VirtualKey.Up),
				VolumeDown = GetVKey(rootElement, "volumeDown", VirtualKey.Down),
				ToggleFullscreen = GetVKey(rootElement, "toggleFullscreen", VirtualKey.F),
				ToggleMute = GetVKey(rootElement, "toggleMute", VirtualKey.M),
				NextEpisode = GetVKey(rootElement, "nextEpisode", (VirtualKey)190),
				PreviousEpisode = GetVKey(rootElement, "previousEpisode", (VirtualKey)188),
				SpeedUp = GetVKey(rootElement, "speedUp", (VirtualKey)221),
				SpeedDown = GetVKey(rootElement, "speedDown", (VirtualKey)219),
				ResetSpeed = GetVKey(rootElement, "resetSpeed", VirtualKey.Back),
				SeekForwardSeconds = GetInt(rootElement, "seekForwardSeconds", 10),
				SeekBackwardSeconds = GetInt(rootElement, "seekBackwardSeconds", 10)
			};
		}
		// t235：命名类型 = `Exception`，**不能再窄** —— 本 try 内是 `JsonElement` 取值链（`InvalidOperationException`
		// （节点缺失/类型不符时的 `GetProperty`/`GetString`）、`FormatException`/`OverflowException`（数值解析）、
		// `KeyNotFoundException`）各自可抛 ⇒ 共同祖先只有 `Exception`；契约 = 快捷键配置坏了一律回落 `Default`。
		catch (Exception)
		{
			return Default;
		}
	}

	private static VirtualKey GetVKey(JsonElement root, string key, VirtualKey fallback)
	{
		if (root.TryGetProperty(key, out var value) && value.TryGetInt32(out var value2))
		{
			return (VirtualKey)value2;
		}
		return fallback;
	}

	private static int GetInt(JsonElement root, string key, int fallback)
	{
		if (root.TryGetProperty(key, out var value) && value.TryGetInt32(out var value2))
		{
			return value2;
		}
		return fallback;
	}
}

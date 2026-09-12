using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinUISample.Models;

public static class VttSpriteParser
{
	private static readonly Regex TimeRegex = new Regex("(\\d{2}):(\\d{2}):(\\d{2})\\.(\\d{3})\\s*-->\\s*(\\d{2}):(\\d{2}):(\\d{2})\\.(\\d{3})", RegexOptions.Compiled);

	private static readonly Regex UrlRegex = new Regex("(https?://[^\\s]+)#xywh=(\\d+),(\\d+),(\\d+),(\\d+)", RegexOptions.Compiled);

	public static List<VttSpriteEntry> Parse(string vttContent)
	{
		List<VttSpriteEntry> list = new List<VttSpriteEntry>();
		if (string.IsNullOrWhiteSpace(vttContent))
		{
			return list;
		}
		string[] array = vttContent.Split('\n');
		VttSpriteEntry vttSpriteEntry = null;
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (string.IsNullOrEmpty(text) || text.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			Match match = TimeRegex.Match(text);
			if (match.Success)
			{
				vttSpriteEntry = new VttSpriteEntry
				{
					StartSeconds = ParseTime(match, 1),
					EndSeconds = ParseTime(match, 5)
				};
				continue;
			}
			Match match2 = UrlRegex.Match(text);
			if (match2.Success && vttSpriteEntry != null)
			{
				vttSpriteEntry = new VttSpriteEntry
				{
					StartSeconds = vttSpriteEntry.StartSeconds,
					EndSeconds = vttSpriteEntry.EndSeconds,
					ImageUrl = match2.Groups[1].Value,
					X = int.Parse(match2.Groups[2].Value),
					Y = int.Parse(match2.Groups[3].Value),
					Width = int.Parse(match2.Groups[4].Value),
					Height = int.Parse(match2.Groups[5].Value)
				};
				list.Add(vttSpriteEntry);
				vttSpriteEntry = null;
			}
		}
		return list;
	}

	private static double ParseTime(Match match, int groupOffset)
	{
		int num = int.Parse(match.Groups[groupOffset].Value);
		int num2 = int.Parse(match.Groups[groupOffset + 1].Value);
		int num3 = int.Parse(match.Groups[groupOffset + 2].Value);
		int num4 = int.Parse(match.Groups[groupOffset + 3].Value);
		return num * 3600 + num2 * 60 + num3 + num4 / 1000.0;
	}

	public static VttSpriteEntry? FindEntry(List<VttSpriteEntry> entries, double seconds)
	{
		if (entries.Count == 0)
		{
			return null;
		}
		int num = 0;
		int num2 = entries.Count - 1;
		while (num <= num2)
		{
			int num3 = (num + num2) / 2;
			VttSpriteEntry vttSpriteEntry = entries[num3];
			if (seconds >= vttSpriteEntry.StartSeconds && seconds < vttSpriteEntry.EndSeconds)
			{
				return vttSpriteEntry;
			}
			if (seconds < vttSpriteEntry.StartSeconds)
			{
				num2 = num3 - 1;
			}
			else
			{
				num = num3 + 1;
			}
		}
		if (num > 0 && num <= entries.Count)
		{
			return entries[num - 1];
		}
		if (entries.Count <= 0)
		{
			return null;
		}
		return entries[0];
	}
}

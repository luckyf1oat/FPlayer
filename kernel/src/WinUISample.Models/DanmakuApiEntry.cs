using System;

namespace WinUISample.Models;

public sealed class DanmakuApiEntry
{
	public string Name { get; init; } = string.Empty;

	public string Url { get; init; } = string.Empty;

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Name))
			{
				return Name;
			}
			if (Uri.TryCreate(Url, UriKind.Absolute, out Uri result))
			{
				return result.Authority;
			}
			if (Url.Length <= 40)
			{
				return Url;
			}
			return Url.Substring(0, 40) + "…";
		}
	}
}

using System.Collections.Generic;
using System.IO;

namespace WinUISample.Controls;

public sealed class SubtitleResult
{
	public string PackageId { get; set; } = string.Empty;

	public string Filename { get; set; } = string.Empty;

	public string DownloadUrl { get; set; } = string.Empty;

	public string Language { get; set; } = string.Empty;

	public double? Rating { get; set; }

	public string Detail
	{
		get
		{
			List<string> list = new List<string>();
			if (!string.IsNullOrWhiteSpace(Language))
			{
				list.Add(Language);
			}
			string text = Path.GetExtension(Filename).TrimStart('.').ToUpperInvariant();
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(text);
			}
			string text2 = string.Join(" · ", list);
			if (!Rating.HasValue || !(Rating.Value > 0.0))
			{
				return text2;
			}
			return $"{text2} · ★ {Rating.Value:0.0}";
		}
	}
}

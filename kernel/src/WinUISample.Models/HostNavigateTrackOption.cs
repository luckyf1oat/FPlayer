using System.Text.Json.Serialization;

namespace WinUISample.Models;

internal sealed class HostNavigateTrackOption
{
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("label")]
	public string? Label { get; init; }

	[JsonPropertyName("url")]
	public string? Url { get; init; }

	[JsonPropertyName("title")]
	public string? Title { get; init; }

	[JsonPropertyName("language")]
	public string? Language { get; init; }

	[JsonPropertyName("external")]
	public bool IsExternal { get; init; }

	[JsonPropertyName("selected")]
	public bool IsSelected { get; init; }

	[JsonPropertyName("special")]
	public bool IsSpecial { get; init; }

	internal OverlayTrackOption ToTrackOption()
	{
		return new OverlayTrackOption
		{
			Id = (Id ?? string.Empty),
			Label = (Label ?? string.Empty),
			Url = (Url ?? string.Empty),
			Title = (Title ?? string.Empty),
			Language = (Language ?? string.Empty),
			IsExternal = IsExternal,
			IsSelected = IsSelected,
			IsSpecial = IsSpecial
		};
	}
}

using System.Text.Json.Serialization;

namespace WinUISample.Models;

internal sealed class HostNavigateVersionOption
{
	[JsonPropertyName("index")]
	public int Index { get; init; }

	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("label")]
	public string? Label { get; init; }

	[JsonPropertyName("selected")]
	public bool IsSelected { get; init; }

	internal OverlayVersionOption ToVersionOption()
	{
		return new OverlayVersionOption
		{
			Index = Index,
			Id = (Id ?? string.Empty),
			Label = (Label ?? string.Empty),
			IsSelected = IsSelected
		};
	}
}

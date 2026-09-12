namespace WinUISample.Models;

public sealed class OverlayTrackOption
{
	public string Id { get; init; } = string.Empty;

	public string Label { get; init; } = string.Empty;

	public string Url { get; init; } = string.Empty;

	public string Title { get; init; } = string.Empty;

	public string Language { get; init; } = string.Empty;

	public bool IsExternal { get; init; }

	public bool IsSelected { get; set; }

	public string ResolvedId { get; set; } = string.Empty;

	public bool IsSpecial { get; init; }

	public int EmbyStreamIndex { get; init; } = -1;
}

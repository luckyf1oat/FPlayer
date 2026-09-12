namespace WinUISample.Models;

public sealed class OverlayVersionOption
{
	public int Index { get; init; }

	public string Id { get; init; } = string.Empty;

	public string Label { get; init; } = string.Empty;

	public bool IsSelected { get; set; }
}

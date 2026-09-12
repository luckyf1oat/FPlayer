namespace WinUISample.Models;

public sealed class EpisodeListItem
{
	public string Id { get; init; } = string.Empty;

	public string Label { get; init; } = string.Empty;

	public bool IsCurrent { get; set; }

	public bool IsPlayed { get; init; }
}

namespace WinUISample.Models;

public sealed class TodbChapter
{
	public int MarkerId { get; init; }

	public string MarkerType { get; init; } = "chapter";

	public string? Title { get; init; }

	public int TimeStart { get; init; }

	public int? TimeEnd { get; init; }
}

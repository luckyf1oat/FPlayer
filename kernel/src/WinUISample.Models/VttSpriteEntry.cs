namespace WinUISample.Models;

public sealed class VttSpriteEntry
{
	public double StartSeconds { get; init; }

	public double EndSeconds { get; init; }

	public string ImageUrl { get; init; } = string.Empty;

	public int X { get; init; }

	public int Y { get; init; }

	public int Width { get; init; }

	public int Height { get; init; }
}

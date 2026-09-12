namespace WinUISample.Models;

public sealed class TodbSprite
{
	public int SpriteId { get; init; }

	public int Width { get; init; }

	public int Height { get; init; }

	public string VttUrl { get; init; } = string.Empty;
}

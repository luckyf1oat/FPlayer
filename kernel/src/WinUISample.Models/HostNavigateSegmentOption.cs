using System.Text.Json.Serialization;

namespace WinUISample.Models;

internal sealed class HostNavigateSegmentOption
{
	[JsonPropertyName("type")]
	public string? Type { get; init; }

	[JsonPropertyName("startMs")]
	public long StartMs { get; init; }

	[JsonPropertyName("endMs")]
	public long? EndMs { get; init; }

	[JsonPropertyName("source")]
	public string? Source { get; init; }

	internal MediaSegment? ToMediaSegment()
	{
		MediaSegmentType mediaSegmentType;
		switch ((Type ?? "intro").ToLowerInvariant())
		{
			case "recap":
				mediaSegmentType = MediaSegmentType.Recap;
				break;
			case "credits":
			case "outro":
				mediaSegmentType = MediaSegmentType.Credits;
				break;
			case "preview":
				mediaSegmentType = MediaSegmentType.Preview;
				break;
			default:
				mediaSegmentType = MediaSegmentType.Intro;
				break;
		}
		MediaSegmentType type = mediaSegmentType;
		if (StartMs <= 0 && !EndMs.HasValue)
		{
			return null;
		}
		return new MediaSegment
		{
			Type = type,
			StartMs = StartMs,
			EndMs = EndMs,
			Source = (Source ?? string.Empty)
		};
	}
}

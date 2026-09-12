namespace WinUISample.Models;

public sealed class MediaSegment
{
	public MediaSegmentType Type { get; init; }

	public long StartMs { get; init; }

	public long? EndMs { get; init; }

	public string Source { get; init; } = string.Empty;

	public double StartSeconds => StartMs / 1000.0;

	public double? EndSeconds
	{
		get
		{
			if (!EndMs.HasValue)
			{
				return null;
			}
			return EndMs.Value / 1000.0;
		}
	}
}

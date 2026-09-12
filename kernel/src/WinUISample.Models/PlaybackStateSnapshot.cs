namespace WinUISample.Models;

public readonly record struct PlaybackStateSnapshot(int AudioStreamIndex, int SubtitleStreamIndex, double SubtitleOffset, int VolumeLevel, bool IsMuted, double PlaybackRate)
{
	public double DurationSeconds { get; init; } = 0.0;

	/// <summary>
	/// mpv 的 <c>aid</c> **真实 track id**（既有 <c>AudioStreamIndex</c> 是 Emby 流序号，两者不同源）。
	/// <c>-1</c> = mpv 报「无」/读不到。
	/// </summary>
	public long MpvAudioTrackId { get; init; } = -1L;

	/// <summary>mpv 的 <c>sid</c> 真实 track id（<c>-1</c> = 无字幕轨）。</summary>
	public long MpvSubtitleTrackId { get; init; } = -1L;

	/// <summary>
	/// mpv 的 <c>sub-visibility</c>（字幕显示开关）真实值。
	/// <c>null</c> = **读不到**（不做假值：不冒充 true/false）—— 外壳须把 null 当"未知"。
	/// 为什么必须回推：字幕开关现在是可逆的 `sub-visibility` no/yes，
	/// 「关了字幕」若外壳看不到，用户会以为点了没反应 —— 那就是又一处**静默**。
	/// </summary>
	public bool? MpvSubtitleVisible { get; init; } = null;

	public static PlaybackStateSnapshot Default => new PlaybackStateSnapshot(-1, -1, 0.0, 100, IsMuted: false, 1.0);
}

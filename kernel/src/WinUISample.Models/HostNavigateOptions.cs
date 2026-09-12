using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinUISample.Models;

internal sealed class HostNavigateOptions
{
	[JsonPropertyName("mediaPath")]
	public string? MediaPath { get; init; }

	[JsonPropertyName("httpHeaders")]
	public IReadOnlyDictionary<string, string> HttpHeaders { get; init; } = new Dictionary<string, string>();

	[JsonPropertyName("newItemId")]
	public string? NewItemId { get; init; }

	[JsonPropertyName("title")]
	public string? Title { get; init; }

	[JsonPropertyName("subtitle")]
	public string? Subtitle { get; init; }

	[JsonPropertyName("monogram")]
	public string? Monogram { get; init; }

	[JsonPropertyName("logo")]
	public string? Logo { get; init; }

	[JsonPropertyName("backdropUrl")]
	public string? BackdropUrl { get; init; }

	[JsonPropertyName("badges")]
	public IReadOnlyList<string> Badges { get; init; } = Array.Empty<string>();

	[JsonPropertyName("versionOptions")]
	public IReadOnlyList<HostNavigateVersionOption> VersionOptions { get; init; } = Array.Empty<HostNavigateVersionOption>();

	[JsonPropertyName("subtitleId")]
	public string? SubtitleId { get; init; }

	[JsonPropertyName("subtitleUrl")]
	public string? SubtitleUrl { get; init; }

	[JsonPropertyName("callbackUrl")]
	public string? CallbackUrl { get; init; }

	[JsonPropertyName("previousEpisodeId")]
	public string? PreviousEpisodeId { get; init; }

	[JsonPropertyName("nextEpisodeId")]
	public string? NextEpisodeId { get; init; }

	[JsonPropertyName("seasonId")]
	public string? SeasonId { get; init; }

	[JsonPropertyName("startPosition")]
	public double? StartPosition { get; init; }

	[JsonPropertyName("danmakuMatchName")]
	public string? DanmakuMatchName { get; init; }

	[JsonPropertyName("currentEpisodeId")]
	public string? CurrentEpisodeId { get; init; }

	[JsonPropertyName("audioTracks")]
	public IReadOnlyList<HostNavigateTrackOption> AudioTracks { get; init; } = Array.Empty<HostNavigateTrackOption>();

	[JsonPropertyName("subtitleTracks")]
	public IReadOnlyList<HostNavigateTrackOption> SubtitleTracks { get; init; } = Array.Empty<HostNavigateTrackOption>();

	[JsonPropertyName("segments")]
	public IReadOnlyList<HostNavigateSegmentOption> Segments { get; init; } = Array.Empty<HostNavigateSegmentOption>();
}

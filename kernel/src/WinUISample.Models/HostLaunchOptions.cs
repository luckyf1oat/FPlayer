using System;
using System.Collections.Generic;
using WinUISample.Models.Constants;

namespace WinUISample.Models;

public sealed class HostLaunchOptions
{
	public string? LibMpvPath { get; init; }

	public string? MediaPath { get; init; }

	public double? StartPosition { get; init; }

	public string? Title { get; init; }

	public string? Subtitle { get; init; }

	public string? Monogram { get; init; }

	public string? Logo { get; init; }

	public string? BackdropUrl { get; init; }

	public IReadOnlyList<string> Badges { get; init; } = Array.Empty<string>();

	public IReadOnlyList<OverlayVersionOption> VersionOptions { get; init; } = Array.Empty<OverlayVersionOption>();

	public string? SubtitleId { get; init; }

	public string? SubtitleUrl { get; init; }

	public string? CallbackUrl { get; init; }

	public string? PreviousEpisodeId { get; init; }

	public string? NextEpisodeId { get; init; }

	public string? SeasonId { get; init; }

	public IReadOnlyList<OverlayTrackOption> AudioTracks { get; init; } = Array.Empty<OverlayTrackOption>();

	public IReadOnlyList<OverlayTrackOption> SubtitleTracks { get; init; } = Array.Empty<OverlayTrackOption>();

	public IReadOnlyDictionary<string, string> HttpHeaders { get; init; } = new Dictionary<string, string>();

	public string? UserAgent { get; init; }

	public bool DisableSkipMarkers { get; init; }

	public bool HasOpenRequest => !string.IsNullOrWhiteSpace(MediaPath);

	public PlayerShortcuts Shortcuts { get; init; } = PlayerShortcuts.Default;

	public string? HttpProxy { get; init; }

	public IReadOnlyList<DanmakuApiEntry> DanmakuApis { get; init; } = Array.Empty<DanmakuApiEntry>();

	public string? DanmakuMatchName { get; init; }

	/// <summary>
	/// 由命令行解析器置位：<see cref="DanmakuMatchName"/> 的编码/解码配对**已在命令行边界**完成
	/// （命令行通道向外壳约定"传 URL 编码后的名字"，解析时解码回原始名）。
	/// 置 false 表示名字来自进程内宿主（如 `AIPlayer.Shell` 直接构造本对象）——那条通道**必须**直接给原始名。
	/// 有这个开关，下游才能给出**可判别**的来源标签，而不是靠猜；也是"同一事实只有一个判断点"的落点。
	/// </summary>
	public bool DanmakuMatchNameDecodedFromCli { get; init; }

	public IReadOnlyList<EpisodeListItem> EpisodeList { get; init; } = Array.Empty<EpisodeListItem>();

	public IReadOnlyList<MediaSegment> Segments { get; init; } = Array.Empty<MediaSegment>();

	public IReadOnlyList<TodbChapter> Chapters { get; init; } = Array.Empty<TodbChapter>();

	public TodbSprite? Sprite { get; init; }

	public string? MpvConfigDir { get; init; }

	public bool FitVideoSize { get; init; } = true;

	public VideoFitMode VideoFitMode { get; init; }

	public int? ParentPid { get; init; }

	public bool DefaultRtxVsr { get; init; }

	public bool DefaultRtxVideoHdr { get; init; }

	public string DefaultAnimeMode { get; init; } = "none";

	public string DefaultSharpenMode { get; init; } = "none";

	public bool AutoPlayNextEpisode { get; init; } = true;

	public int MaxVolume { get; init; } = 100;
}

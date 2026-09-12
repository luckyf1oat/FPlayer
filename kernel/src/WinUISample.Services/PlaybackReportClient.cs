using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinUISample.Models;

namespace WinUISample.Services;

internal sealed class PlaybackReportClient
{
	private readonly HttpClient _httpClient;

	private readonly HttpClient _navigateHttpClient;

	private readonly Uri? _callbackUri;

	private readonly ILogger<PlaybackReportClient> _logger;

	public bool IsEnabled => _callbackUri is not null;

	public PlaybackReportClient(string? callbackUrl, ILogger<PlaybackReportClient> logger)
	{
		_logger = logger;
		HttpClientHandler handler = new HttpClientHandler
		{
			UseProxy = false
		};
		_httpClient = new HttpClient(handler)
		{
			Timeout = TimeSpan.FromSeconds(5L)
		};
		HttpClientHandler handler2 = new HttpClientHandler
		{
			UseProxy = false
		};
		_navigateHttpClient = new HttpClient(handler2)
		{
			Timeout = TimeSpan.FromSeconds(90L)
		};
		if (Uri.TryCreate(callbackUrl, UriKind.Absolute, out Uri result))
		{
			_callbackUri = result;
		}
	}

	public Task<HostNavigateOptions?> ReportProgressAsync(double positionSeconds, bool isPaused, PlaybackStateSnapshot state, string? updateUrl = null)
	{
		return SendAndReceiveAsync(new
		{
			@event = "progress",
			positionSeconds = positionSeconds,
			isPaused = isPaused,
			durationSeconds = state.DurationSeconds,
			audioStreamIndex = state.AudioStreamIndex,
			subtitleStreamIndex = state.SubtitleStreamIndex,
			subtitleOffset = state.SubtitleOffset,
			volumeLevel = state.VolumeLevel,
			isMuted = state.IsMuted,
			playbackRate = state.PlaybackRate,
			// t39（E-P2）：新增字段（**既有字段语义不变**）—— mpv 的真实 track id，外壳据此实现「单一真相源 = mpv」。
			aid = state.MpvAudioTrackId,
			sid = state.MpvSubtitleTrackId,
			// mpv 的 sub-visibility 真实值（null = 读不到）。外壳据此看出"字幕被关了"。
			subVisible = state.MpvSubtitleVisible,
			updateUrl = updateUrl
		});
	}

	public Task ReportStoppedAsync(double positionSeconds, PlaybackStateSnapshot state)
	{
		return SendAsync(new
		{
			@event = "stopped",
			positionSeconds = positionSeconds,
			audioStreamIndex = state.AudioStreamIndex,
			subtitleStreamIndex = state.SubtitleStreamIndex,
			subtitleOffset = state.SubtitleOffset,
			volumeLevel = state.VolumeLevel,
			isMuted = state.IsMuted,
			playbackRate = state.PlaybackRate
		});
	}

	public Task ReportManualResizeAsync()
	{
		return SendAsync(new
		{
			@event = "manual_resize"
		});
	}

	public Task<HostNavigateOptions?> ReportNavigatePreviousAsync(double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "navigate_previous",
			positionSeconds = positionSeconds
		});
	}

	public Task<HostNavigateOptions?> ReportNavigateNextAsync(double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "navigate_next",
			positionSeconds = positionSeconds
		});
	}

	public Task<HostNavigateOptions?> ReportNavigateEpisodeAsync(string episodeId, double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "navigate_episode",
			targetEpisodeId = episodeId,
			positionSeconds = positionSeconds
		});
	}

	public Task<HostNavigateOptions?> ReportPreloadEpisodeAsync(string episodeId, double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "preload_episode",
			targetEpisodeId = episodeId,
			positionSeconds = positionSeconds
		});
	}

	public Task<HostNavigateOptions?> ReportSwitchVersionAsync(int versionIndex, double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "switch_version",
			versionIndex = versionIndex,
			positionSeconds = positionSeconds
		});
	}

	public Task<HostNavigateOptions?> ReportRefreshPlaybackUrlAsync(double positionSeconds)
	{
		return SendForNavigateAsync(new
		{
			@event = "refresh_playback_url",
			positionSeconds = positionSeconds
		});
	}

	private async Task SendAsync(object payload)
	{
		if (_callbackUri is null)
		{
			return;
		}
		try
		{
			using StringContent content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			using HttpResponseMessage httpResponseMessage = await _httpClient.PostAsync(_callbackUri, content);
			httpResponseMessage.EnsureSuccessStatusCode();
			_logger.LogDebug("PlaybackReportClient.SendAsync ok status={StatusCode}", (int)httpResponseMessage.StatusCode);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "PlaybackReportClient.SendAsync failed");
		}
	}

	private async Task<HostNavigateOptions?> SendAndReceiveAsync(object payload)
	{
		if (_callbackUri is null)
		{
			return null;
		}
		try
		{
			using StringContent content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await _navigateHttpClient.PostAsync(_callbackUri, content);
			response.EnsureSuccessStatusCode();
			_logger.LogDebug("PlaybackReportClient.SendAndReceiveAsync ok status={StatusCode}", (int)response.StatusCode);
			if (response.StatusCode == HttpStatusCode.OK)
			{
				string text = await response.Content.ReadAsStringAsync();
				if (!string.IsNullOrWhiteSpace(text))
				{
					return JsonSerializer.Deserialize<HostNavigateOptions>(text);
				}
			}
			return null;
		}
		catch (TaskCanceledException exception)
		{
			_logger.LogWarning(exception, "PlaybackReportClient.SendAndReceiveAsync timed out after {TimeoutSeconds}s", _navigateHttpClient.Timeout.TotalSeconds);
			return null;
		}
		catch (Exception exception2)
		{
			_logger.LogWarning(exception2, "PlaybackReportClient.SendAndReceiveAsync failed");
			return null;
		}
	}

	private async Task<HostNavigateOptions?> SendForNavigateAsync(object payload)
	{
		if (_callbackUri is null)
		{
			return null;
		}
		try
		{
			using StringContent content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await _navigateHttpClient.PostAsync(_callbackUri, content);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("PlaybackReportClient.SendForNavigateAsync failed: {StatusCode} ({ReasonPhrase})", (int)response.StatusCode, response.ReasonPhrase);
				return null;
			}
			string text = await response.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			return JsonSerializer.Deserialize<HostNavigateOptions>(text);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "PlaybackReportClient.SendForNavigateAsync failed");
			return null;
		}
	}
}

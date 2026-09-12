using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Richasy.Danmaku.Enums;
using Richasy.Danmaku.Models;
using Serilog;
using Windows.UI;
using WinUISample.Models;
using WinUISample.Models.Constants;

namespace WinUISample.Services;

internal static class DanmakuParser
{
	private static readonly ILogger _log = Log.ForContext(typeof(DanmakuParser));

	private static HttpClient? _http;

	private static readonly object _httpLock = new object();

	private static readonly object _cacheLock = new object();

	private static readonly Dictionary<string, DanmakuFetchAttempt> _commentCache = new Dictionary<string, DanmakuFetchAttempt>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Task<DanmakuFetchAttempt>> _inFlight = new Dictionary<string, Task<DanmakuFetchAttempt>>(StringComparer.Ordinal);

	private static readonly Queue<string> _evictionQueue = new Queue<string>();

	private static readonly HashSet<string> _cachedUrls = new HashSet<string>(StringComparer.Ordinal);
	private static readonly object _matchLock = new object();

	private static readonly Dictionary<string, IReadOnlyList<DanmakuMatchResult>> _matchResultCache = new Dictionary<string, IReadOnlyList<DanmakuMatchResult>>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Task<DanmakuMatchAttempt>> _matchInFlight = new Dictionary<string, Task<DanmakuMatchAttempt>>(StringComparer.Ordinal);

	private static readonly object _manualSelectionLock = new object();

	private static readonly Dictionary<string, DanmakuMatchResult> _manualSelectionCache = new Dictionary<string, DanmakuMatchResult>(StringComparer.Ordinal);

	private static HttpClient Http
	{
		get
		{
			if (_http == null)
			{
				lock (_httpLock)
				{
					if (_http == null)
					{
						_http = new HttpClient
						{
							DefaultRequestHeaders = {
							{
								"User-Agent",
								ClientIdentity.HttpUserAgent
							} },
							Timeout = TimeSpan.FromSeconds(60L)
						};
					}
				}
			}
			return _http;
		}
	}

	public static string BuildCommentUrl(string apiBase, int episodeId)
	{
		return $"{apiBase.TrimEnd('/')}/api/v2/comment/{episodeId}?format=xml";
	}

	public static async Task<DanmakuMatchResult?> MatchAsync(string apiBase, string matchName)
	{
		return (await MatchWithStatusAsync(apiBase, matchName).ConfigureAwait(continueOnCapturedContext: false)).Result;
	}

	public static async Task<DanmakuMatchAttempt> MatchWithStatusAsync(string apiBase, string matchName)
	{
		string key = BuildMatchCacheKey(apiBase, matchName);
		Task<DanmakuMatchAttempt> value2;
		lock (_matchLock)
		{
			if (_matchResultCache.TryGetValue(key, out IReadOnlyList<DanmakuMatchResult> value))
			{
				_log.Debug($"DanmakuParser.MatchAsync cache_hit matchName={matchName} count={value.Count}");
				return DanmakuMatchAttempt.Success(value);
			}
			if (_matchInFlight.TryGetValue(key, out value2))
			{
				_log.Debug("DanmakuParser.MatchAsync dedup_wait matchName=" + matchName);
			}
			else
			{
				value2 = MatchInternalAsync(apiBase, matchName, key);
				_matchInFlight[key] = value2;
			}
		}
		try
		{
			return await value2.ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			lock (_matchLock)
			{
				_matchInFlight.Remove(key);
			}
		}
	}

	public static string BuildMatchCacheKey(string apiBase, string matchName)
	{
		return apiBase.TrimEnd('/') + "\0" + matchName;
	}

	public static Task PersistManualSelectionAsync(string apiBase, string matchName, string displayTitle, string commentUrl, string? animeTitle = null)
	{
		if (string.IsNullOrWhiteSpace(apiBase) || string.IsNullOrWhiteSpace(matchName) || string.IsNullOrWhiteSpace(commentUrl))
		{
			return Task.CompletedTask;
		}
		string text = BuildMatchCacheKey(apiBase, matchName);
		DanmakuMatchResult danmakuMatchResult = new DanmakuMatchResult(displayTitle, commentUrl, animeTitle);
		lock (_manualSelectionLock)
		{
			_manualSelectionCache[text] = danmakuMatchResult;
		}
		DanmakuDiskCache.SaveManualSelection(text, danmakuMatchResult);
		_log.Information("DanmakuParser manual selection persisted matchName={MatchName} url={Url}", matchName, commentUrl);
		return Task.CompletedTask;
	}

	public static async Task<DanmakuMatchResult?> TryRestoreManualSelectionAsync(string apiBase, string matchName)
	{
		if (string.IsNullOrWhiteSpace(apiBase) || string.IsNullOrWhiteSpace(matchName))
		{
			return null;
		}
		string key = BuildMatchCacheKey(apiBase, matchName);
		lock (_manualSelectionLock)
		{
			if (_manualSelectionCache.TryGetValue(key, out DanmakuMatchResult value))
			{
				_log.Debug("DanmakuParser manual selection memory_hit matchName={MatchName}", matchName);
				return value;
			}
		}
		DanmakuMatchResult danmakuMatchResult = await DanmakuDiskCache.TryReadManualSelectionAsync(key).ConfigureAwait(continueOnCapturedContext: false);
		if (danmakuMatchResult is null)
		{
			return null;
		}
		lock (_manualSelectionLock)
		{
			_manualSelectionCache[key] = danmakuMatchResult;
		}
		_log.Debug("DanmakuParser manual selection disk_hit matchName={MatchName} url={Url}", matchName, danmakuMatchResult.CommentUrl);
		return danmakuMatchResult;
	}

	private static async Task<DanmakuMatchAttempt> MatchInternalAsync(string apiBase, string matchName, string cacheKey)
	{
		IReadOnlyList<DanmakuMatchResult> readOnlyList = await DanmakuDiskCache.TryReadMatchesAsync(cacheKey).ConfigureAwait(continueOnCapturedContext: false);
		if (readOnlyList.Count > 0)
		{
			lock (_matchLock)
			{
				_matchResultCache[cacheKey] = readOnlyList;
			}
			return DanmakuMatchAttempt.Success(readOnlyList);
		}
		_log.Debug("DanmakuParser.MatchAsync apiBase=" + apiBase + " matchName=" + matchName);
		try
		{
			string base_ = apiBase.TrimEnd('/');
			string requestUri = base_ + "/api/v2/match";
			string content = JsonSerializer.Serialize(new
			{
				fileName = matchName
			});
			using StringContent content2 = new StringContent(content, Encoding.UTF8, "application/json");
			HttpResponseMessage httpResponseMessage = await Http.PostAsync(requestUri, content2).ConfigureAwait(continueOnCapturedContext: false);
			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				string message = $"弹幕匹配请求失败（HTTP {(int)httpResponseMessage.StatusCode}）";
				_log.Warning("DanmakuParser.MatchAsync match_failed status={StatusCode} matchName={MatchName}", (int)httpResponseMessage.StatusCode, matchName);
				return DanmakuMatchAttempt.Failure(message);
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false));
			if (!jsonDocument.RootElement.TryGetProperty("matches", out var value) || value.GetArrayLength() == 0)
			{
				_log.Warning("DanmakuParser.MatchAsync no_matches matchName={MatchName}", matchName);
				return DanmakuMatchAttempt.Failure("未找到匹配的弹幕，可在弹幕菜单中手动搜索");
			}
			List<DanmakuMatchResult> list = new List<DanmakuMatchResult>();
			foreach (JsonElement item in value.EnumerateArray())
			{
				string text = null;
				JsonElement value3;
				if (item.TryGetProperty("episodeId", out var value2))
				{
					text = value2.GetRawText().Trim('"');
				}
				else if (item.TryGetProperty("id", out value3))
				{
					text = value3.GetRawText().Trim('"');
				}
				if (!string.IsNullOrWhiteSpace(text) && int.TryParse(text, out var result))
				{
					string episodeTitle = (item.TryGetProperty("episodeTitle", out var value4) ? (value4.GetString() ?? string.Empty) : string.Empty);
					string animeTitle = (item.TryGetProperty("animeTitle", out var value5) ? value5.GetString() : null);
					string commentUrl = BuildCommentUrl(base_, result);
					list.Add(new DanmakuMatchResult(episodeTitle, commentUrl, animeTitle));
				}
			}
			if (list.Count == 0)
			{
				_log.Warning("DanmakuParser.MatchAsync no_episode_id matchName={MatchName}", matchName);
				return DanmakuMatchAttempt.Failure("弹幕接口返回数据无效");
			}
			_log.Information("DanmakuParser.MatchAsync success count={Count} matchName={MatchName}", list.Count, matchName);
			lock (_matchLock)
			{
				_matchResultCache[cacheKey] = list;
			}
			DanmakuDiskCache.SaveMatches(cacheKey, list);
			return DanmakuMatchAttempt.Success(list);
		}
		catch (TaskCanceledException)
		{
			_log.Warning("DanmakuParser.MatchAsync timeout matchName={MatchName}", matchName);
			return DanmakuMatchAttempt.Failure("弹幕匹配请求超时，请检查网络或弹幕 API");
		}
		catch (Exception ex2)
		{
			_log.Warning(ex2, "DanmakuParser.MatchAsync error matchName={MatchName}", matchName);
			return DanmakuMatchAttempt.Failure("弹幕匹配网络错误：" + ex2.Message);
		}
	}

	public static async Task<List<DanmakuSearchAnime>> SearchEpisodesAsync(string apiBase, string anime)
	{
		_log.Debug("DanmakuParser.SearchEpisodesAsync apiBase=" + apiBase + " anime=" + anime);
		try
		{
			string requestUri = apiBase.TrimEnd('/') + "/api/v2/search/episodes?anime=" + Uri.EscapeDataString(anime);
			using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync(requestUri).ConfigureAwait(continueOnCapturedContext: false));
			JsonElement rootElement = jsonDocument.RootElement;
			List<DanmakuSearchAnime> list = new List<DanmakuSearchAnime>();
			if (!rootElement.TryGetProperty("animes", out var value))
			{
				return list;
			}
			foreach (JsonElement item in value.EnumerateArray())
			{
				int num = (item.TryGetProperty("animeId", out var value2) ? value2.GetInt32() : 0);
				string animeTitle = (item.TryGetProperty("animeTitle", out var value3) ? (value3.GetString() ?? string.Empty) : string.Empty);
				List<DanmakuSearchEpisode> list2 = new List<DanmakuSearchEpisode>();
				if (item.TryGetProperty("episodes", out var value4))
				{
					foreach (JsonElement item2 in value4.EnumerateArray())
					{
						int num2 = (item2.TryGetProperty("episodeId", out var value5) ? value5.GetInt32() : 0);
						string episodeTitle = (item2.TryGetProperty("episodeTitle", out var value6) ? (value6.GetString() ?? string.Empty) : string.Empty);
						if (num2 > 0)
						{
							list2.Add(new DanmakuSearchEpisode(num2, episodeTitle));
						}
					}
				}
				if (num > 0)
				{
					list.Add(new DanmakuSearchAnime(num, animeTitle, list2));
				}
			}
			_log.Debug($"DanmakuParser.SearchEpisodesAsync found={list.Count} animes");
			return list;
		}
		catch (Exception ex)
		{
			// 默认日志级别是 information ⇒ 这里过去用 Debug 等于**没有失败信号**：
			// 用户看到的是"搜不到番剧"，日志里却什么都没有（返回空列表是对的，静默不是）。
			_log.Warning(ex, "DanmakuParser.SearchEpisodesAsync error apiBase={ApiBase} anime={Anime}", apiBase, anime);
			return new List<DanmakuSearchAnime>();
		}
	}

	public static async Task<List<DanmakuItem>> FetchAndParseAsync(string url)
	{
		DanmakuFetchAttempt danmakuFetchAttempt = await FetchCommentAsync(url).ConfigureAwait(continueOnCapturedContext: false);
		return (danmakuFetchAttempt.Items is List<DanmakuItem> list) ? list : danmakuFetchAttempt.Items.ToList();
	}

	public static async Task<IReadOnlyList<DanmakuApiMatchResult>> MatchAllWithStatusAsync(IReadOnlyList<DanmakuApiEntry> apis, string matchName)
	{
		if (apis.Count == 0)
		{
			return Array.Empty<DanmakuApiMatchResult>();
		}
		Task<DanmakuApiMatchResult>[] array = new Task<DanmakuApiMatchResult>[apis.Count];
		for (int i = 0; i < apis.Count; i++)
		{
			int apiIndex = i;
			DanmakuApiEntry api = apis[i];
			array[i] = MatchApiWithStatusAsync(apiIndex, api, matchName);
		}
		return await Task.WhenAll(array).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<DanmakuApiMatchResult> MatchApiWithStatusAsync(int apiIndex, DanmakuApiEntry api, string matchName)
	{
		string apiBase = api.Url?.Trim();
		if (string.IsNullOrWhiteSpace(apiBase))
		{
			return new DanmakuApiMatchResult(apiIndex, string.Empty, DanmakuMatchAttempt.Failure("API 地址无效"));
		}
		return new DanmakuApiMatchResult(apiIndex, apiBase, await MatchWithStatusAsync(apiBase, matchName).ConfigureAwait(continueOnCapturedContext: false));
	}

	public static async Task PrefetchAsync(string apiBase, string matchName)
	{
		DanmakuMatchResult danmakuMatchResult = await TryRestoreManualSelectionAsync(apiBase, matchName).ConfigureAwait(continueOnCapturedContext: false);
		if (danmakuMatchResult is not null)
		{
			FetchCommentAsync(danmakuMatchResult.CommentUrl);
			return;
		}
		DanmakuMatchAttempt danmakuMatchAttempt = await MatchWithStatusAsync(apiBase, matchName).ConfigureAwait(continueOnCapturedContext: false);
		if (danmakuMatchAttempt.IsSuccess)
		{
			FetchCommentAsync(danmakuMatchAttempt.Result.CommentUrl);
		}
	}

	public static Task PrefetchAllAsync(IReadOnlyList<DanmakuApiEntry> apis, string matchName)
	{
		if (apis.Count == 0)
		{
			return Task.CompletedTask;
		}
		return Task.WhenAll(from api in apis
							where !string.IsNullOrWhiteSpace(api.Url)
							select PrefetchAsync(api.Url.Trim(), matchName));
	}

	public static Task<DanmakuFetchAttempt> FetchCommentAsync(string url, bool forceRefresh = false)
	{
		lock (_cacheLock)
		{
			if (!forceRefresh && _commentCache.TryGetValue(url, out DanmakuFetchAttempt value))
			{
				_log.Debug($"DanmakuParser.FetchAndParseAsync cache_hit items={value.Items.Count} url={url}");
				return Task.FromResult(value);
			}
			if (forceRefresh)
			{
				_commentCache.Remove(url);
				_inFlight.Remove(url);
				_log.Debug("DanmakuParser.FetchAndParseAsync force_refresh url=" + url);
			}
			if (!_inFlight.TryGetValue(url, out Task<DanmakuFetchAttempt> value2))
			{
				_log.Debug($"DanmakuParser.FetchAndParseAsync fetch_start url={url} forceRefresh={forceRefresh}");
				value2 = FetchInternalAsync(url, forceRefresh);
				_inFlight[url] = value2;
			}
			else if (!forceRefresh)
			{
				_log.Debug("DanmakuParser.FetchAndParseAsync dedup_wait url=" + url);
			}
			else
			{
				_log.Debug("DanmakuParser.FetchAndParseAsync force_refresh_wait url=" + url);
			}
			return value2;
		}
	}

	private static async Task<DanmakuFetchAttempt> FetchInternalAsync(string url, bool forceRefresh = false)
	{
		_ = 3;
		try
		{
			string url2;
			if (!forceRefresh)
			{
				string text = await DanmakuDiskCache.TryReadCommentXmlAsync(url).ConfigureAwait(continueOnCapturedContext: false);
				if (text != null)
				{
					url2 = url;
					return CacheFetchResult(url2, await ParseXmlAsync(text).ConfigureAwait(continueOnCapturedContext: false), "disk");
				}
			}
			string xml = await Http.GetStringAsync(url).ConfigureAwait(continueOnCapturedContext: false);
			DanmakuDiskCache.SaveCommentXml(url, xml);
			url2 = url;
			return CacheFetchResult(url2, await ParseXmlAsync(xml).ConfigureAwait(continueOnCapturedContext: false), forceRefresh ? "network-forced" : "network");
		}
		catch (TaskCanceledException)
		{
			_log.Warning("DanmakuParser.FetchInternalAsync timeout url={Url}", url);
			return new DanmakuFetchAttempt(Array.Empty<DanmakuItem>(), "弹幕下载超时，请检查网络或弹幕 API");
		}
		catch (Exception ex2)
		{
			_log.Warning(ex2, "DanmakuParser.FetchInternalAsync error url={Url}", url);
			return new DanmakuFetchAttempt(Array.Empty<DanmakuItem>(), "弹幕下载失败：" + ex2.Message);
		}
		finally
		{
			lock (_cacheLock)
			{
				_inFlight.Remove(url);
			}
		}
	}

	private static DanmakuFetchAttempt CacheFetchResult(string url, List<DanmakuItem> result, string source)
	{
		object obj;
		if (result.Count <= 0)
		{
			obj = "empty";
		}
		else
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(11, 2);
			defaultInterpolatedStringHandler.AppendLiteral("range=");
			defaultInterpolatedStringHandler.AppendFormatted(result[0].StartMs);
			defaultInterpolatedStringHandler.AppendLiteral("ms~");
			defaultInterpolatedStringHandler.AppendFormatted(result[result.Count - 1].StartMs);
			defaultInterpolatedStringHandler.AppendLiteral("ms");
			obj = defaultInterpolatedStringHandler.ToStringAndClear();
		}
		string text = (string)obj;
		DanmakuFetchAttempt danmakuFetchAttempt;
		if (result.Count == 0)
		{
			_log.Warning("DanmakuParser.FetchInternalAsync empty source={Source} url={Url}", source, url);
			danmakuFetchAttempt = new DanmakuFetchAttempt(result, "弹幕内容为空");
		}
		else
		{
			_log.Information("DanmakuParser.FetchInternalAsync parsed={Count} items {Range} source={Source} url={Url}", result.Count, text, source, url);
			danmakuFetchAttempt = new DanmakuFetchAttempt(result, null);
		}
		lock (_cacheLock)
		{
			if (!_cachedUrls.Contains(url))
			{
				while (_evictionQueue.Count >= 64)
				{
					string text2 = _evictionQueue.Dequeue();
					_commentCache.Remove(text2);
					_cachedUrls.Remove(text2);
				}
				_evictionQueue.Enqueue(url);
				_cachedUrls.Add(url);
			}
			_commentCache[url] = danmakuFetchAttempt;
			return danmakuFetchAttempt;
		}
	}

	private static Task<List<DanmakuItem>> ParseXmlAsync(string xml)
	{
		return Task.Run(() => ParseXml(xml));
	}

	private static string SanitizeXmlContent(string xml)
	{
		if (string.IsNullOrEmpty(xml))
		{
			return xml;
		}
		StringBuilder stringBuilder = new StringBuilder(xml.Length);
		foreach (char c in xml)
		{
			if (IsLegalXmlChar(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static bool IsLegalXmlChar(char c)
	{
		bool flag;
		switch (c)
		{
			case '\t':
			case '\n':
			case '\r':
				flag = true;
				break;
			default:
				flag = false;
				break;
		}
		if (!flag && (c < ' ' || c > '\ud7ff'))
		{
			if (c >= '\ue000')
			{
				return c <= '\ufffd';
			}
			return false;
		}
		return true;
	}

	private static string SanitizeDanmakuText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (IsLegalXmlChar(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static List<DanmakuItem> ParseXml(string xml)
	{
		try
		{
			return ParseXmlCore(xml);
		}
		catch (Exception ex) when (((ex is XmlException || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			_log.Debug(ex, "DanmakuParser.ParseXml retry after sanitization");
			return ParseXmlCore(SanitizeXmlContent(xml));
		}
	}

	private static List<DanmakuItem> ParseXmlCore(string xml)
	{
		List<DanmakuItem> list = new List<DanmakuItem>();
		XmlReaderSettings settings = new XmlReaderSettings
		{
			IgnoreComments = true,
			IgnoreWhitespace = true,
			DtdProcessing = DtdProcessing.Prohibit
		};
		using XmlReader xmlReader = XmlReader.Create(new StringReader(xml), settings);
		while (xmlReader.Read())
		{
			if (xmlReader.NodeType != XmlNodeType.Element || !string.Equals(xmlReader.LocalName, "d", StringComparison.Ordinal))
			{
				continue;
			}
			string attribute = xmlReader.GetAttribute("p");
			if (string.IsNullOrWhiteSpace(attribute))
			{
				xmlReader.Skip();
				continue;
			}
			string text = SanitizeDanmakuText(xmlReader.ReadElementContentAsString());
			if (!string.IsNullOrWhiteSpace(text))
			{
				string[] array = attribute.Split(',');
				if (array.Length >= 4 && double.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && !(result < 0.0) && !(result > 86400.0) && int.TryParse(array[1], out var result2) && float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) && uint.TryParse(array[3], out var result4))
				{
					DanmakuMode mode = result2 switch
					{
						4 => DanmakuMode.Bottom,
						5 => DanmakuMode.Top,
						6 => DanmakuMode.ReverseRolling,
						_ => DanmakuMode.Rolling,
					};
					byte r = (byte)((result4 >> 16) & 0xFF);
					byte g = (byte)((result4 >> 8) & 0xFF);
					byte b = (byte)(result4 & 0xFF);
					uint num = (uint)(result * 1000.0);
					list.Add(new DanmakuItem
					{
						Id = ((array.Length >= 8) ? array[7] : $"{num}:{result2}:{text.GetHashCode(StringComparison.Ordinal):X8}"),
						StartMs = num,
						Text = text,
						Mode = mode,
						TextColor = Color.FromArgb(byte.MaxValue, r, g, b),
						BaseFontSize = result3,
						HasOutline = true,
						OutlineColor = Color.FromArgb(byte.MaxValue, 0, 0, 0),
						OutlineSize = 1.5f
					});
				}
			}
		}
		if (list.Count > 1)
		{
			list.Sort((DanmakuItem a, DanmakuItem danmakuItem) => a.StartMs.CompareTo(danmakuItem.StartMs));
		}
		return list;
	}
}

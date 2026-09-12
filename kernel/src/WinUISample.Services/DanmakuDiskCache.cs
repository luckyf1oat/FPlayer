using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace WinUISample.Services;

/// <summary>
/// 弹幕匹配/评论的**纯加速器**磁盘缓存（三个根：<c>danmaku-cache/{matches,comments,manual-selections}</c>）。
/// </summary>
/// <remarks>
/// <b>契约：这六条失败路径有意吞掉，只记 <c>Debug</c></b>，语义一律回退成"没有缓存"
/// （<c>null</c> / 空列表）或直接放弃写入 —— 不是"忘了记日志"：
/// <list type="bullet">
///   <item>读失败（文件损坏 / 被占用 / 权限）⇒ 调用方按"未命中"处理，重新走网络取一份即可**自愈**；</item>
///   <item>写失败 ⇒ 只损失下次的加速，不影响本次结果（写入是后台 <c>Task.Run</c>，不阻塞播放）。</item>
/// </list>
/// 因此不把它们升级成 Warning：损坏的缓存会每次播放都刷一条日志，而正确处置与"未命中"**完全相同**。
/// 需要排查缓存行为时把日志级别调到 <c>debug</c>（见 <c>GlobalDependencies</c> 的 <c>LogLevel</c> 读取）。
/// <para>⚠️ 与"真正的失败"的边界：本类**只**负责缓存 I/O；网络/解析失败发生在 <c>DanmakuParser</c> 里，
/// 那些路径必须是 Warning 以上（例如 <c>DanmakuParser.SearchEpisodesAsync</c>）。</para>
/// </remarks>
internal static class DanmakuDiskCache
{
	private sealed record CachedMatchEntry(string EpisodeTitle, string CommentUrl, string? AnimeTitle = null);

	private static readonly ILogger _log = Log.ForContext(typeof(DanmakuDiskCache));

	private static readonly string CommentCacheRoot = Path.Combine(App.AppDataRoot, "danmaku-cache", "comments");

	private static readonly string MatchCacheRoot = Path.Combine(App.AppDataRoot, "danmaku-cache", "matches");

	private static readonly string ManualSelectionCacheRoot = Path.Combine(App.AppDataRoot, "danmaku-cache", "manual-selections");

	private static string HashKey(string key)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
	}

	public static async Task<string?> TryReadCommentXmlAsync(string url)
	{
		string path = Path.Combine(CommentCacheRoot, HashKey(url) + ".xml");
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			string text = await File.ReadAllTextAsync(path).ConfigureAwait(continueOnCapturedContext: false);
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			_log.Debug("DanmakuDiskCache comment hit url={Url}", url);
			return text;
		}
		catch (Exception exception)
		{
			_log.Debug(exception, "DanmakuDiskCache comment read failed url={Url}", url);
			return null;
		}
	}

	public static void SaveCommentXml(string url, string xml)
	{
		if (string.IsNullOrWhiteSpace(xml))
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				Directory.CreateDirectory(CommentCacheRoot);
				await File.WriteAllTextAsync(Path.Combine(CommentCacheRoot, HashKey(url) + ".xml"), xml).ConfigureAwait(continueOnCapturedContext: false);
				_log.Debug("DanmakuDiskCache comment saved url={Url}", url);
			}
			catch (Exception exception)
			{
				_log.Debug(exception, "DanmakuDiskCache comment save failed url={Url}", url);
			}
		});
	}

	public static async Task<IReadOnlyList<DanmakuMatchResult>> TryReadMatchesAsync(string cacheKey)
	{
		string path = Path.Combine(MatchCacheRoot, HashKey(cacheKey) + ".json");
		if (!File.Exists(path))
		{
			return Array.Empty<DanmakuMatchResult>();
		}
		try
		{
			string json = await File.ReadAllTextAsync(path).ConfigureAwait(continueOnCapturedContext: false);
			List<CachedMatchEntry> list = JsonSerializer.Deserialize<List<CachedMatchEntry>>(json);
			if (list != null && list.Count > 0)
			{
				_log.Debug("DanmakuDiskCache match hit key={Key} count={Count}", cacheKey, list.Count);
				return list.Select(ToMatchResult).ToList();
			}
			CachedMatchEntry cachedMatchEntry = JsonSerializer.Deserialize<CachedMatchEntry>(json);
			if (cachedMatchEntry is not null && !string.IsNullOrWhiteSpace(cachedMatchEntry.EpisodeTitle) && !string.IsNullOrWhiteSpace(cachedMatchEntry.CommentUrl))
			{
				_log.Debug("DanmakuDiskCache match hit key={Key} count=1", cacheKey);
				return new global::_003C_003Ez__ReadOnlySingleElementList<DanmakuMatchResult>(ToMatchResult(cachedMatchEntry));
			}
			return Array.Empty<DanmakuMatchResult>();
		}
		catch (Exception exception)
		{
			_log.Debug(exception, "DanmakuDiskCache match read failed key={Key}", cacheKey);
			return Array.Empty<DanmakuMatchResult>();
		}
	}

	public static void SaveMatches(string cacheKey, IReadOnlyList<DanmakuMatchResult> results)
	{
		if (results.Count == 0)
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				Directory.CreateDirectory(MatchCacheRoot);
				string path = Path.Combine(MatchCacheRoot, HashKey(cacheKey) + ".json");
				string contents = JsonSerializer.Serialize(results.Select((DanmakuMatchResult r) => new CachedMatchEntry(r.EpisodeTitle, r.CommentUrl, r.AnimeTitle)).ToList());
				await File.WriteAllTextAsync(path, contents).ConfigureAwait(continueOnCapturedContext: false);
				_log.Debug("DanmakuDiskCache match saved key={Key} count={Count}", cacheKey, results.Count);
			}
			catch (Exception exception)
			{
				_log.Debug(exception, "DanmakuDiskCache match save failed key={Key}", cacheKey);
			}
		});
	}

	public static async Task<DanmakuMatchResult?> TryReadManualSelectionAsync(string cacheKey)
	{
		string path = Path.Combine(ManualSelectionCacheRoot, HashKey(cacheKey) + ".json");
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			CachedMatchEntry cachedMatchEntry = JsonSerializer.Deserialize<CachedMatchEntry>(await File.ReadAllTextAsync(path).ConfigureAwait(continueOnCapturedContext: false));
			if (cachedMatchEntry is null || string.IsNullOrWhiteSpace(cachedMatchEntry.EpisodeTitle) || string.IsNullOrWhiteSpace(cachedMatchEntry.CommentUrl))
			{
				return null;
			}
			_log.Debug("DanmakuDiskCache manual selection hit key={Key}", cacheKey);
			return ToMatchResult(cachedMatchEntry);
		}
		catch (Exception exception)
		{
			_log.Debug(exception, "DanmakuDiskCache manual selection read failed key={Key}", cacheKey);
			return null;
		}
	}

	public static void SaveManualSelection(string cacheKey, DanmakuMatchResult result)
	{
		if (string.IsNullOrWhiteSpace(result.CommentUrl))
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				Directory.CreateDirectory(ManualSelectionCacheRoot);
				string path = Path.Combine(ManualSelectionCacheRoot, HashKey(cacheKey) + ".json");
				string contents = JsonSerializer.Serialize(new CachedMatchEntry(result.EpisodeTitle, result.CommentUrl, result.AnimeTitle));
				await File.WriteAllTextAsync(path, contents).ConfigureAwait(continueOnCapturedContext: false);
				_log.Debug("DanmakuDiskCache manual selection saved key={Key} url={Url}", cacheKey, result.CommentUrl);
			}
			catch (Exception exception)
			{
				_log.Debug(exception, "DanmakuDiskCache manual selection save failed key={Key}", cacheKey);
			}
		});
	}

	private static DanmakuMatchResult ToMatchResult(CachedMatchEntry entry)
	{
		return new DanmakuMatchResult(entry.EpisodeTitle, entry.CommentUrl, entry.AnimeTitle);
	}
}

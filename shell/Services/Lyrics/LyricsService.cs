// 等价移植：rebuild/ai_player/lib/core/services/lyrics_service.dart（Dart `LyricsService`，289 行）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §6.1「歌词」——
//   主源 GET https://lrclib.net/api/search?q=…；精确匹配形态 GET /api/get?track_name=&artist_name=&album_name=&duration=；
//   响应 syncedLyrics（LRC）/ plainLyrics；失败文案前缀 `lrclib search failed: `（字符串实证）。
//   Subsonic 回落依据 §2「Navidrome / Subsonic」的 `getLyricsBySongId`（/rest/{method}）。
// 语义保持：
//   - 查找顺序 = 内存缓存 → 磁盘缓存 → lrclib 精确匹配 → lrclib 搜索 → Subsonic 回落（与 Dart 逐行一致）；
//   - 内存缓存**缓存 null**（等价 Dart `Map<String, LyricsResult?>`：查过的“无歌词”不再打网络）；
//   - 任何网络/IO/解析失败只记日志并返回 null（等价 Dart 的可空返回值 = 空结果）——**绝不抛出打断播放**；
//   - 只有非空结果才落盘（`!result.isEmpty`）。
// 与 Dart 的差异（均为 C# 侧适配，见文件末「适配说明」）：
//   ① Subsonic 回落改成注入委托（Func<songId, serverId, ct, Task<lrc>>），不硬引用 NavidromeService；
//   ② 委托面只能携带 LRC 文本 ⇒ 结果的 trackName/artistName 用调用方传入值（Dart 用服务端返回的 title/artist）；
//   ③ MD5 实现内联在本文件（不引用并行任务写的 NavidromeService.Md5Hex），语义与 Dart `Md5.hex` 一致。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

// Subsonic 歌词回落面（等价 Dart 构造参数 `NavidromeService? subsonic` + `subsonic.getLyricsBySongId(songId)`）：
//   Func<songId, serverId, cancellationToken, Task<string>>；
//   返回**标准 LRC 文本**（等价 Dart `SubsonicLyrics.toLrc()`：有结构化行时拼 `[mm:ss.xx]文本\n`，否则原样返回平文本），
//   无歌词时返回 null/空串；`serverId` 供多服务器场景选路，单实例调用方可忽略。
// 别名只是可读性包装，公开签名里的类型仍是规范要求的 Func<string, string, CancellationToken, Task<string>>。
using SubsonicLyricsFetcher = System.Func<string, string, System.Threading.CancellationToken, System.Threading.Tasks.Task<string>>;

namespace AIPlayer.Shell.Services.Lyrics;

/// <summary>歌词服务（对应 Dart <c>LyricsService</c>）：lrclib + 本地缓存 + Subsonic 回落。</summary>
public sealed class LyricsService
{
    /// <summary>lrclib API 根（等价 Dart <c>LyricsService.baseUrl</c>）。</summary>
    public const string BaseUrl = "https://lrclib.net/api";

    /// <summary>Dart 侧统一 10 秒超时（见 `lyrics_service.dart` 的两处 `timeout: Duration(seconds: 10)`）。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>写盘用 UTF-8 无 BOM（等价 Dart <c>writeAsString</c>：utf8、无 BOM）。</summary>
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly Dictionary<string, LyricsResult> _memory = new Dictionary<string, LyricsResult>(StringComparer.Ordinal);

    public LyricsService(ShellHttpClient http, Action<string> onLog = null)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        OnLog = onLog;
    }

    public ShellHttpClient Http { get; }

    public Action<string> OnLog { get; }

    private void Log(string message) => OnLog?.Invoke(message);

    /// <summary>
    /// 主入口（等价 Dart <c>find()</c>）：内存/磁盘缓存 → lrclib 精确匹配 → lrclib 搜索 → Subsonic 歌词。
    /// 找不到或全部失败 ⇒ 返回 <c>null</c>（等价 Dart 可空返回；调用方据此回落纯文本/隐藏歌词页）。
    /// </summary>
    public async Task<LyricsResult> FindAsync(
        string trackName,
        string artistName = null,
        string albumName = null,
        double? durationSeconds = null,
        SubsonicLyricsFetcher subsonicLyricsFetcher = null,
        string subsonicSongId = null,
        string subsonicServerId = null,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(trackName, artistName, albumName);

        // 等价 Dart：`if (_memory.containsKey(key)) return _memory[key];`
        // 刻意用 ContainsKey 而非 TryGetValue —— 负结果（null）也是有效缓存值。
        if (_memory.ContainsKey(key)) return _memory[key];

        var cached = await ReadDiskCacheAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached != null)
        {
            _memory[key] = cached;
            return cached;
        }

        var result = await FromLrclibGetAsync(trackName, artistName, albumName, durationSeconds, cancellationToken)
            .ConfigureAwait(false);
        if (result == null)
        {
            result = await FromLrclibSearchAsync(trackName, artistName, cancellationToken).ConfigureAwait(false);
        }
        if (result == null)
        {
            result = await FromSubsonicAsync(subsonicLyricsFetcher, subsonicSongId, subsonicServerId, trackName, artistName, cancellationToken)
                .ConfigureAwait(false);
        }

        if (result != null && !result.IsEmpty)
        {
            await WriteDiskCacheAsync(key, result, cancellationToken).ConfigureAwait(false);
        }
        _memory[key] = result;
        return result;
    }

    // ── lrclib ─────────────────────────────────────────────────────────────

    /// <summary>精确匹配 <c>GET /api/get?track_name=&amp;artist_name=&amp;album_name=&amp;duration=</c>（Dart <c>_fromLrclibGet</c>）。</summary>
    private async Task<LyricsResult> FromLrclibGetAsync(
        string trackName,
        string artistName,
        string albumName,
        double? durationSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            // 等价 Dart：track_name 恒带（哪怕空串），其余仅在非空/正数时带上。
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["track_name"] = trackName ?? string.Empty,
            };
            if (!string.IsNullOrEmpty(artistName)) query["artist_name"] = artistName;
            if (!string.IsNullOrEmpty(albumName)) query["album_name"] = albumName;
            if (durationSeconds.HasValue && durationSeconds.Value > 0)
            {
                // 等价 Dart `'${durationSeconds.round()}'`（round = 四舍五入、远离零）。
                var rounded = (long)Math.Round(durationSeconds.Value, MidpointRounding.AwayFromZero);
                query["duration"] = rounded.ToString(CultureInfo.InvariantCulture);
            }

            var result = await Http.GetAsync(
                $"{BaseUrl}/get",
                query: query,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (json == null) return null;
            return LyricsResult.FromLrclibJson(json);
        }
        catch (Exception ex)
        {
            // 失败文案与 Dart 一致（前缀实证）。
            Log($"lrclib get failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>搜索 <c>GET /api/search?q=track artist</c>（Dart <c>_fromLrclibSearch</c>）；优先带时间轴的结果。</summary>
    private async Task<LyricsResult> FromLrclibSearchAsync(
        string trackName,
        string artistName,
        CancellationToken cancellationToken)
    {
        try
        {
            var parts = new List<string> { trackName ?? string.Empty };
            if (!string.IsNullOrEmpty(artistName)) parts.Add(artistName);
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["q"] = string.Join(" ", parts),
            };

            var result = await Http.GetAsync(
                $"{BaseUrl}/search",
                query: query,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var list = result.JsonList;
            if (list == null) return null;

            var candidates = new List<LyricsResult>();
            foreach (var element in JsonRead.Objects(list.Value.EnumerateArray()))
            {
                candidates.Add(LyricsResult.FromLrclibJson(element));
            }
            if (candidates.Count == 0) return null;

            // 等价 Dart `candidates.firstWhere((c) => c.isSynced, orElse: () => candidates.first)`
            foreach (var candidate in candidates)
            {
                if (candidate.IsSynced) return candidate;
            }
            return candidates[0];
        }
        catch (Exception ex)
        {
            Log($"lrclib search failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Subsonic 回落（Dart <c>_fromSubsonic</c>；用注入委托替代硬引用 NavidromeService）。</summary>
    private async Task<LyricsResult> FromSubsonicAsync(
        SubsonicLyricsFetcher fetcher,
        string songId,
        string serverId,
        string trackName,
        string artistName,
        CancellationToken cancellationToken)
    {
        if (fetcher == null || string.IsNullOrEmpty(songId)) return null;
        try
        {
            var lrc = await fetcher(songId, serverId, cancellationToken).ConfigureAwait(false);
            // 等价 Dart `lyrics == null || lyrics.isEmpty`（plain.trim().isEmpty && lines.isEmpty）。
            if (string.IsNullOrWhiteSpace(lrc)) return null;

            return new LyricsResult(
                trackName: trackName ?? string.Empty,
                artistName: artistName ?? string.Empty,
                syncedLyrics: lrc,
                source: "subsonic");
        }
        catch (Exception ex)
        {
            Log($"Subsonic 歌词回落失败（忽略）：{ex.Message}");
            return null;
        }
    }

    // ── 磁盘缓存 ───────────────────────────────────────────────────────────

    /// <summary>缓存键（等价 Dart <c>_cacheKey</c>）：三段小写后用 <c>|</c> 拼接再取 MD5 十六进制。</summary>
    public static string CacheKey(string trackName, string artistName, string albumName)
        => Md5Hex(
            (trackName ?? string.Empty).ToLowerInvariant()
            + "|" + (artistName ?? string.Empty).ToLowerInvariant()
            + "|" + (albumName ?? string.Empty).ToLowerInvariant());

    /// <summary>缓存文件 <c>&lt;lyricsCacheDir&gt;\&lt;key&gt;.lrc</c>（Dart <c>_cacheFile</c>；文件名过 SanitizeFileName）。</summary>
    public static string CacheFile(string key)
        => Path.Combine(AppDataDir.Instance.LyricsCacheDir, TextUtils.SanitizeFileName($"{key}.lrc"));

    /// <summary>
    /// 读缓存（Dart <c>_readDiskCache</c>）：首行为元信息 <c>track|artist|source</c>，其余整体作为 syncedLyrics。
    /// </summary>
    private async Task<LyricsResult> ReadDiskCacheAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var file = CacheFile(key);
            if (!File.Exists(file)) return null;
            var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return null;

            var split = text.IndexOf('\n');
            var metaLine = split < 0 ? string.Empty : text.Substring(0, split);
            var body = split < 0 ? text : text.Substring(split + 1);
            var parts = metaLine.Split('|');

            return new LyricsResult(
                trackName: parts.Length > 0 ? parts[0] : string.Empty,
                artistName: parts.Length > 1 ? parts[1] : string.Empty,
                syncedLyrics: body,
                source: parts.Length > 2 ? parts[2] : "cache");
        }
        catch (Exception ex)
        {
            Log($"歌词缓存读取失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>写缓存（Dart <c>_writeDiskCache</c>）：<c>track|artist|source\n&lt;lrc&gt;</c>。</summary>
    private async Task WriteDiskCacheAsync(string key, LyricsResult result, CancellationToken cancellationToken)
    {
        try
        {
            var file = CacheFile(key);
            var payload = $"{result.TrackName}|{result.ArtistName}|{result.Source}\n{result.Lrc}";
            await File.WriteAllTextAsync(file, payload, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"歌词缓存写入失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空歌词缓存（Dart <c>clearCache</c>；设置页「清除缓存」）。
    /// 目录内每个实体逐一删除，单个失败忽略 —— 与 Dart 的清缓存语义一致（其实现同样是「删不掉就跳过」）。
    /// </summary>
    public void ClearCache()
    {
        _memory.Clear();
        try
        {
            var dir = AppDataDir.Instance.LyricsCacheDir;
            if (!Directory.Exists(dir)) return;
            foreach (var entry in Directory.GetFileSystemEntries(dir))
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        // Dart 的 Directory.deleteSync() 默认非递归（非空目录会失败并被忽略）——保持同语义。
                        Directory.Delete(entry);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* 有意忽略：单个缓存实体删不掉不阻断整目录清理（与 Dart 清缓存同语义） */ }
            }
        }
        catch (Exception ex)
        {
            Log($"歌词缓存清理失败：{ex.Message}");
        }
    }

    /// <summary>异步清理（保持 Dart <c>Future&lt;void&gt; clearCache()</c> 的调用形状）。</summary>
    public Task ClearCacheAsync() => Task.Run(ClearCache);

    // ── 适配说明 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 等价 Dart `Md5.hex`（rebuild/ai_player/lib/core/util/md5.dart）：UTF-8 编码 → MD5 → 十六进制**小写**。
    /// 刻意内联实现，避免与并行任务编写的 NavidromeService.Md5Hex 产生文件级耦合。
    /// </summary>
    private static string Md5Hex(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}

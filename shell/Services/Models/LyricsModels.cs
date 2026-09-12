// 等价移植：rebuild/ai_player/lib/core/services/lyrics_service.dart 的**模型部分**
//   （Dart `LyricLine` / `LyricsResult`；原版树另有 `lyrics_models.dart`，重建版把类型并入了该服务文件）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §6.1「歌词」——
//   主源 GET https://lrclib.net/api/search?q=…；响应字段 syncedLyrics（LRC 带时间轴）/ plainLyrics。
// 分工：本文件只有模型与 LRC 解析（纯函数）；网络/磁盘缓存/Subsonic 回落见 Services/Lyrics/LyricsService.cs。
// 未实证，容错解析：lrclib 字段缺失/类型漂移一律走 JsonRead 的宽松取值（等价 Dart 的 `'${json['x'] ?? ''}'`）。
// 命名避让：本命名空间已有 SubsonicLyrics/SubsonicLyricLine（NavidromeModels.cs，Subsonic 结构化歌词），
//   故此处沿用 Dart 的 `LyricLine`/`LyricsResult`，两者语义不同、互不重名。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>一行歌词（对应 Dart <c>LyricLine</c>）。</summary>
public sealed class LyricLine
{
    public LyricLine(string text, long startMs)
    {
        Text = text ?? string.Empty;
        StartMs = startMs;
    }

    public string Text { get; set; }

    /// <summary>行起始时间（毫秒）。</summary>
    public long StartMs { get; set; }

    /// <summary>等价 Dart <c>timestamp</c>：<c>mm:ss.xx</c>（分钟两位、秒两位小数再左补零到 5 位）。</summary>
    public string Timestamp
    {
        get
        {
            var minutes = StartMs / 60000;
            var seconds = (StartMs % 60000) / 1000d;
            return minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')
                + ":"
                + seconds.ToString("F2", CultureInfo.InvariantCulture).PadLeft(5, '0');
        }
    }

    /// <summary>等价 Dart <c>toString()</c> ⇒ <c>[mm:ss.xx]文本</c>。</summary>
    public override string ToString() => $"[{Timestamp}]{Text}";
}

/// <summary>一次查询的歌词结果（对应 Dart <c>LyricsResult</c>）。</summary>
public sealed class LyricsResult
{
    /// <summary>等价 Dart 的命名可选参数构造（默认值取自 Dart 侧）。</summary>
    public LyricsResult(
        string trackName = "",
        string artistName = "",
        string albumName = "",
        double duration = 0,
        bool instrumental = false,
        string plainLyrics = "",
        string syncedLyrics = "",
        string source = "lrclib")
    {
        TrackName = trackName ?? string.Empty;
        ArtistName = artistName ?? string.Empty;
        AlbumName = albumName ?? string.Empty;
        Duration = duration;
        Instrumental = instrumental;
        PlainLyrics = plainLyrics ?? string.Empty;
        SyncedLyrics = syncedLyrics ?? string.Empty;
        Source = source ?? string.Empty;
    }

    public string TrackName { get; set; }

    public string ArtistName { get; set; }

    public string AlbumName { get; set; }

    /// <summary>时长（秒）；lrclib 给的 <c>duration</c> 为浮点。</summary>
    public double Duration { get; set; }

    public bool Instrumental { get; set; }

    public string PlainLyrics { get; set; }

    public string SyncedLyrics { get; set; }

    /// <summary>来源：<c>lrclib</c> / <c>subsonic</c> / <c>cache</c>（等价 Dart 注释里的三态）。</summary>
    public string Source { get; set; } = "lrclib";

    /// <summary>等价 Dart <c>isEmpty</c>：纯文本与时间轴都为空且非纯音乐。</summary>
    public bool IsEmpty =>
        (PlainLyrics ?? string.Empty).Trim().Length == 0
        && (SyncedLyrics ?? string.Empty).Trim().Length == 0
        && !Instrumental;

    /// <summary>等价 Dart <c>isSynced</c>：有带时间轴的歌词。</summary>
    public bool IsSynced => (SyncedLyrics ?? string.Empty).Trim().Length > 0;

    /// <summary>等价 Dart <c>lines</c>：解析 <c>syncedLyrics</c>（无时间轴 ⇒ 空列表，调用方回落纯文本）。</summary>
    public List<LyricLine> Lines => ParseLrc(SyncedLyrics);

    /// <summary>等价 Dart <c>lrc</c>：可直接展示/落盘的 LRC。</summary>
    public string Lrc
    {
        get
        {
            if (IsSynced) return SyncedLyrics;
            var parsed = Lines;
            return parsed.Count == 0 ? (PlainLyrics ?? string.Empty) : string.Join("\n", parsed);
        }
    }

    /// <summary>
    /// 等价 Dart <c>LyricsResult.fromLrclibJson</c>（字段名 <c>trackName</c>/<c>artistName</c>/<c>albumName</c>/
    /// <c>duration</c>/<c>instrumental</c>/<c>plainLyrics</c>/<c>syncedLyrics</c>）。
    /// </summary>
    public static LyricsResult FromLrclibJson(JsonElement? json) => new LyricsResult(
        trackName: JsonRead.Str(json, "trackName"),
        artistName: JsonRead.Str(json, "artistName"),
        albumName: JsonRead.Str(json, "albumName"),
        duration: JsonRead.Double(json, "duration"),
        instrumental: JsonRead.Bool(json, "instrumental"),
        plainLyrics: JsonRead.Str(json, "plainLyrics"),
        syncedLyrics: JsonRead.Str(json, "syncedLyrics"));

    /// <summary>
    /// 等价 Dart <c>LyricsResult.parseLrc</c>：兼容 <c>[mm:ss.xx]</c> / <c>[mm:ss]</c> / 一行多时间戳。
    /// 小数位规则同 Dart：3 位按毫秒直取，1~2 位右补零到 3 位（<c>[mm:ss.5]</c> = 500ms）。
    /// 正则用 <c>[0-9]</c> 而非 <c>\d</c>：Dart 的 <c>\d</c> 只匹配 ASCII 数字，.NET 的 <c>\d</c> 会匹配 Unicode 数字。
    /// 排序同 Dart <c>List.sort</c>（按 startMs 升序；相等时次序未定义）。
    /// </summary>
    public static List<LyricLine> ParseLrc(string raw)
    {
        var output = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(raw)) return output;

        foreach (var line in raw.Split('\n'))
        {
            var matches = LrcPattern.Matches(line);
            if (matches.Count == 0) continue;

            var text = LrcPattern.Replace(line, string.Empty).Trim();
            if (text.Length == 0) continue;

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var fractionRaw = match.Groups[3].Success ? match.Groups[3].Value : "0";
                var fraction = fractionRaw.Length == 3
                    ? int.Parse(fractionRaw, CultureInfo.InvariantCulture)
                    : int.Parse(fractionRaw.PadRight(3, '0'), CultureInfo.InvariantCulture);

                output.Add(new LyricLine(text, minutes * 60000L + seconds * 1000L + fraction));
            }
        }

        output.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
        return output;
    }

    /// <summary>等价 Dart <c>RegExp(r'\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]')</c>。</summary>
    private static readonly Regex LrcPattern =
        new Regex(@"\[([0-9]{1,3}):([0-9]{1,2})(?:[.:]([0-9]{1,3}))?\]", RegexOptions.Compiled);
}

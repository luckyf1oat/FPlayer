// 等价移植：rebuild/ai_player/lib/core/util/text_utils.dart。
// 文案与原版保持一致（时长 `h:mm:ss`、未知值 `-`）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AIPlayer.Shell.Services.Util;

/// <summary>通用格式化/转换工具（对应 Dart <c>text_utils.dart</c>）。</summary>
public static class TextUtils
{
    /// <summary><c>1:23:45</c> / <c>23:45</c>；负数或 NaN 显示 <c>-</c>。</summary>
    public static string FormatDuration(double? seconds)
    {
        if (seconds == null || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value) || seconds.Value < 0)
        {
            return "-";
        }
        var total = (long)Math.Floor(seconds.Value);
        var h = total / 3600;
        var m = (total % 3600) / 60;
        var s = total % 60;
        var mm = m.ToString(CultureInfo.InvariantCulture).PadLeft(h > 0 ? 2 : 1, '0');
        var ss = s.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        return h > 0
            ? $"{h.ToString(CultureInfo.InvariantCulture)}:{mm}:{ss}"
            : $"{mm}:{ss}";
    }

    /// <summary>Emby 使用 ticks（1 秒 = 10,000,000 ticks）。</summary>
    public static long SecondsToTicks(double seconds) => (long)Math.Round(seconds * 10000000d, MidpointRounding.AwayFromZero);

    public static double TicksToSeconds(long? ticks) => (ticks ?? 0) / 10000000d;

    /// <summary>毫秒 → ticks（跳过片段用毫秒）。</summary>
    public static long MillisecondsToTicks(double ms) => (long)Math.Round(ms * 10000d, MidpointRounding.AwayFromZero);

    /// <summary>从标题取首字母作为海报占位（原版 <c>--monogram=</c>）。</summary>
    public static string MonogramOf(string title)
    {
        var t = (title ?? string.Empty).Trim();
        if (t.Length == 0) return "?";
        var first = t.Substring(0, 1);
        return first.ToUpperInvariant();
    }

    public static string HumanBytes(long? bytes)
    {
        if (bytes == null || bytes.Value <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var digits = value >= 100 || unit == 0 ? 0 : 1;
        return $"{value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)} {units[unit]}";
    }

    /// <summary>码率（bps → <c>12.3 Mbps</c>）。</summary>
    public static string FormatBitrate(long? bitsPerSecond)
    {
        if (bitsPerSecond == null || bitsPerSecond.Value <= 0) return "-";
        if (bitsPerSecond.Value >= 1000000)
        {
            return (bitsPerSecond.Value / 1000000d).ToString("F1", CultureInfo.InvariantCulture) + " Mbps";
        }
        return (bitsPerSecond.Value / 1000d).ToString("F0", CultureInfo.InvariantCulture) + " kbps";
    }

    /// <summary>去掉末尾 <c>/</c>，补全 scheme。用户常输入 <c>192.168.1.2:8096</c>。</summary>
    public static string NormalizeBaseUrl(string input)
    {
        var url = (input ?? string.Empty).Trim();
        if (url.Length == 0) return url;
        if (!url.Contains("://", StringComparison.Ordinal)) url = "http://" + url;
        while (url.EndsWith("/", StringComparison.Ordinal))
        {
            url = url.Substring(0, url.Length - 1);
        }
        return url;
    }

    /// <summary><c>S01E02</c>（缺项按 0 补位；两项皆空返回空串）。</summary>
    public static string EpisodeCode(int? season, int? episode)
    {
        if (season == null && episode == null) return string.Empty;
        var s = (season ?? 0).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        var e = (episode ?? 0).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        return $"S{s}E{e}";
    }

    /// <summary>拼 URL（Emby 需要 <c>api_key</c>）。</summary>
    public static string BuildUri(string baseUrl, string path, IDictionary<string, string> query = null)
    {
        var normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        var root = NormalizeBaseUrl(baseUrl);
        var full = root + normalizedPath;
        if (query == null || query.Count == 0) return full;
        return Http.ShellHttpClient.BuildUri(full, query);
    }

    public static DateTime? ParseIsoDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Local)
            : (DateTime?)null;
    }

    public static bool LooksLikeUrl(string value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v.StartsWith("http://", StringComparison.Ordinal) || v.StartsWith("https://", StringComparison.Ordinal);
    }

    public static string Truncate(string value, int max = 80)
    {
        value ??= string.Empty;
        return value.Length <= max ? value : value.Substring(0, Math.Max(0, max - 1)) + "…";
    }

    private static readonly Regex InvalidFileNameChars = new Regex("[\\\\/:*?\"<>|\\x00-\\x1f]", RegexOptions.Compiled);

    /// <summary>过滤 Windows 文件名非法字符（备份文件名会用到）。</summary>
    public static string SanitizeFileName(string value)
    {
        var cleaned = InvalidFileNameChars.Replace(value ?? string.Empty, "_");
        cleaned = cleaned.Trim();
        return cleaned.Length == 0 ? "unnamed" : cleaned;
    }

    public static bool IsLocalFile(string path)
    {
        if (LooksLikeUrl(path)) return false;
        try
        {
            return File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)   // 有意忽略：路径非法/权限不足 => 视为「不是本地文件」，由调用方走 URL 分支
        {
            return false;
        }
    }
}

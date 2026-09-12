// 原版文件（rebuild/ 中不存在 `playback_stream_url.dart`），按 DESIGN §4.2 映射表
// （`src/core/utils/playback_stream_url.dart` → `Services/Playback/PlaybackStreamUrl.cs`）
// + reversed/FlutterApp/SERVICE_API.md §1.3「取流与播放」新写。
//
// 与 EmbyService 的分工（**不复制粘贴它的拼接逻辑**）：
//   - 直连 `?static=true&api_key=` → 委托 `EmbyService.DirectStreamUrl`
//   - HLS 转码 `/videos/{id}/master.m3u8` → 委托 `EmbyService.TranscodeUrl`
//   - 外挂字幕 `/Videos/{id}/{msId}/Subtitles/{index}/Stream.{format}` → 委托 `EmbyService.SubtitleUrl`
//   - 图片 `/Items/{id}/Images/{type}` → 委托 `EmbyService.ImageUrl`
// 本类只补 EmbyService 未覆盖的两件事：
//   ① 转码流形态 `/Videos/{id}/stream.{container}`（SERVICE_API §1.3 实证的第二条转码路径）；
//   ② 从 URL **反解** itemId / mediaSourceId / 流类型（判断「这个地址是不是本服务的取流地址」，
//      对应原版文件名的 `playback_stream_url` 语义），以及 `MediaSources[].DirectStreamUrl` 的绝对化。

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Playback;

/// <summary>流的形态（由路径与查询串反解而来）。</summary>
public enum PlaybackStreamKind
{
    Unknown,
    /// <summary>`/Videos/{id}/stream?static=true`：静态直连（首选形态）。</summary>
    StaticDirect,
    /// <summary>`/Videos/{id}/stream[.{container}]`：直连/直出流（静态标记缺省）。</summary>
    DirectStream,
    /// <summary>`/videos/{id}/master.m3u8`：HLS 转码主播放列表。</summary>
    Hls,
    /// <summary>`/Videos/{id}/stream.{container}`：转码流（容器由扩展名给出）。</summary>
    Transcode,
    /// <summary>`/Videos/{id}/{msId}/Subtitles/{index}/Stream.{format}`：外挂字幕。</summary>
    Subtitle,
    /// <summary>`/Items/{id}/Images/{type}`：图片（海报/背景）。</summary>
    Image,
}

/// <summary>URL 反解结果（供缓存键、日志、换源判断复用）。</summary>
public sealed class PlaybackStreamUrlParts
{
    public string RawUrl { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public PlaybackStreamKind Kind { get; set; } = PlaybackStreamKind.Unknown;

    public string ItemId { get; set; } = string.Empty;

    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>容器（转码流由扩展名给出；HLS 为 <c>m3u8</c>）。</summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>字幕序号（仅 <see cref="PlaybackStreamKind.Subtitle"/>）。</summary>
    public int? SubtitleStreamIndex { get; set; }

    /// <summary>字幕格式（<c>srt</c> / <c>ass</c> …，仅字幕 URL）。</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>图片类型（<c>Primary</c> / <c>Backdrop</c> …，仅图片 URL），含可选的 index。</summary>
    public string ImageType { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public bool IsStatic { get; set; }

    /// <summary>`StartTimeTicks`（**64 位**：超过 int 上限的时长不截断）。</summary>
    public long? StartTicks { get; set; }

    public int? AudioStreamIndex { get; set; }

    public int? SubtitleStreamIndexParam { get; set; }

    public bool IsPlaybackStream => Kind == PlaybackStreamKind.StaticDirect
        || Kind == PlaybackStreamKind.DirectStream
        || Kind == PlaybackStreamKind.Hls
        || Kind == PlaybackStreamKind.Transcode;

    public bool IsTranscode => Kind == PlaybackStreamKind.Hls || Kind == PlaybackStreamKind.Transcode;

    public override string ToString() => $"PlaybackStreamUrlParts({Kind}, item={ItemId}, ms={MediaSourceId})";
}

/// <summary>播放流 URL 的组装 / 反解工具（与 EmbyService 的构造保持同一形态）。</summary>
public static class PlaybackStreamUrl
{
    // t76（captain 2026-09-12 裁定「选甲」）：兄弟正则补**部署前缀**容错，与 `HlsPath`（t67，本文件内紧随其后）同款 `(?:[^/]+/)*`。
    // 为什么：base path 部署（`/emby/videos/…`）下这三条恒不匹配 ⇒ 反解得到 `Unknown`（**静默**：不报错、只给出错的分类，
    // 在缓存键/换源判断/日志上表现为"识别不出自己的取流地址"）。t67 已在同一族栽过一次（HlsPath）。
    // 语义不变：`(?:[^/]+/)*` 对**无前缀**形态匹配 0 次 ⇒ 既有结果逐例不变（判据 = 夹具 `classify` 改前/改后对照，
    // 见 `shell/Tests/evidence/t67-stream-path-tolerance.txt`）；只**新增**带前缀形态的识别能力。
    // ⚠️ 同一文件里 `ImagePath`（紧随 `HlsPath` 之后）仍是 `^/Items/…` 锚死 ⇒ 同类第四条，**未改**（不在本次裁定范围，已上报）。
    private static readonly Regex SubtitlePath = new Regex(
        "^/(?:[^/]+/)*Videos/(?<item>[^/]+)/(?<ms>[^/]+)/Subtitles/(?<index>\\d+)/Stream\\.(?<format>[^/]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TranscodeStreamPath = new Regex(
        "^/(?:[^/]+/)*Videos/(?<item>[^/]+)/stream\\.(?<container>[A-Za-z0-9]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DirectStreamPath = new Regex(
        "^/(?:[^/]+/)*Videos/(?<item>[^/]+)/stream$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // t67：允许**部署前缀**（`/emby/videos/{id}/master.m3u8` 这类形态）。此前正则锚死 `^/videos/…` ⇒ 带前缀的服务器上
    // `IsTranscodeUrl(master.m3u8)` 恒 False（t59 实测）。`(?:[^/]+/)*` 对无前缀形态匹配 0 次 ⇒ 行为不变。
    private static readonly Regex HlsPath = new Regex(
        "^/(?:[^/]+/)*videos/(?<item>[^/]+)/master\\.m3u8$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // t79（第四条兄弟正则，captain 2026-09-12 批准"同族一起改"）：`ImagePath` 也补**部署前缀**容错。
    // 与 `HlsPath`（t67）及上面三条（t76）同款 `(?:[^/]+/)*`；理由同上：base path 部署下恒不匹配 ⇒ 反解 Unknown（静默）。
    // 语义不变：对**无前缀**形态匹配 0 次 ⇒ 既有结果逐例不变（判据 = 夹具 `classify` 改前/改后对照，
    // 见 `shell/Tests/evidence/t67-stream-path-tolerance.txt` §8）。
    private static readonly Regex ImagePath = new Regex(
        "^/(?:[^/]+/)*Items/(?<item>[^/]+)/Images/(?<type>[^/]+)(?:/(?<index>\\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── 组装（委托 EmbyService 的既有实现）──────────────────────────────────

    /// <summary>静态直连（首选）：`/Videos/{id}/stream?static=true&api_key=`（委托 EmbyService）。</summary>
    public static string StaticDirectUrl(
        EmbyService emby,
        string itemId,
        string mediaSourceId = null,
        double? startSeconds = null,
        int? audioStreamIndex = null,
        int? subtitleStreamIndex = null)
        => emby == null ? string.Empty : emby.DirectStreamUrl(itemId, mediaSourceId, startSeconds, audioStreamIndex, subtitleStreamIndex);

    /// <summary>HLS 转码：`/videos/{id}/master.m3u8`（委托 EmbyService）。</summary>
    public static string HlsUrl(EmbyService emby, string itemId, string mediaSourceId = null)
        => emby == null ? string.Empty : emby.TranscodeUrl(itemId, mediaSourceId);

    /// <summary>
    /// 转码流：`/Videos/{id}/stream.{container}`（SERVICE_API §1.3 实证的第二条转码路径）。
    /// EmbyService 未覆盖此形态 ⇒ 在此补齐；参数口径与 <c>DirectStreamUrl</c> 完全一致（`static` 不下发）。
    /// </summary>
    public static string TranscodeStreamUrl(
        EmbyService emby,
        string itemId,
        string container,
        string mediaSourceId = null,
        double? startSeconds = null,
        int? audioStreamIndex = null,
        int? subtitleStreamIndex = null)
    {
        if (emby == null || string.IsNullOrEmpty(itemId)) return string.Empty;
        var ext = NormalizeContainer(container);
        if (ext.Length == 0) return string.Empty;

        var query = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["api_key"] = emby.Token,
        };
        if (!string.IsNullOrEmpty(mediaSourceId)) query["MediaSourceId"] = mediaSourceId;
        if (startSeconds.HasValue && startSeconds.Value > 0)
        {
            query["StartTimeTicks"] = TextUtils.SecondsToTicks(startSeconds.Value).ToString(CultureInfo.InvariantCulture);
        }
        if (audioStreamIndex.HasValue) query["AudioStreamIndex"] = audioStreamIndex.Value.ToString(CultureInfo.InvariantCulture);
        if (subtitleStreamIndex.HasValue) query["SubtitleStreamIndex"] = subtitleStreamIndex.Value.ToString(CultureInfo.InvariantCulture);

        return emby.Uri($"/Videos/{itemId}/stream.{ext}", query);
    }

    /// <summary>外挂字幕（委托 EmbyService）。</summary>
    public static string SubtitleUrl(EmbyService emby, string itemId, string mediaSourceId, int streamIndex, string format = "srt")
        => emby == null ? string.Empty : emby.SubtitleUrl(itemId, mediaSourceId, streamIndex, format);

    /// <summary>图片（委托 EmbyService）。</summary>
    public static string ImageUrl(EmbyService emby, string itemId, string type = "Primary", int? maxHeight = null, int? maxWidth = null, int? index = null, string tag = null)
        => emby == null ? string.Empty : emby.ImageUrl(itemId, type, maxHeight, maxWidth, index, tag);

    /// <summary>把服务端给的（可能相对的）`DirectStreamUrl` 绝对化。</summary>
    public static string AbsoluteUrl(string baseUrl, string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        if (TextUtils.LooksLikeUrl(url)) return url;
        var root = TextUtils.NormalizeBaseUrl(baseUrl);
        return url.StartsWith("/", StringComparison.Ordinal) ? root + url : root + "/" + url;
    }

    // ── 反解 ───────────────────────────────────────────────────────────────

    /// <summary>从 URL 反解 itemId / mediaSourceId / 流类型；无法识别时 <c>Kind = Unknown</c>（不抛异常）。</summary>
    public static PlaybackStreamUrlParts FromUrl(string url)
    {
        var parts = new PlaybackStreamUrlParts { RawUrl = url ?? string.Empty };
        if (string.IsNullOrWhiteSpace(url)) return parts;

        string path;
        string query;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var index = url.IndexOf('?');
                parts.Path = index < 0 ? url : url.Substring(0, index);
                query = index < 0 ? string.Empty : url.Substring(index + 1);
                ReadQuery(parts, query);
                return MatchPath(parts);
            }
            path = uri.AbsolutePath;
            query = uri.Query.TrimStart('?');
        }
        catch (UriFormatException)
        {
            return parts;
        }

        parts.Path = path;
        ReadQuery(parts, query);
        return MatchPath(parts);
    }

    /// <summary>是否为 Emby 取流地址（直连/转码/HLS）。</summary>
    public static bool IsPlaybackStreamUrl(string url) => FromUrl(url).IsPlaybackStream;

    /// <summary>是否为直连地址（`static=true` 或 `/Videos/{id}/stream` 无扩展名）。</summary>
    public static bool IsDirectStreamUrl(string url)
    {
        var kind = FromUrl(url).Kind;
        return kind == PlaybackStreamKind.StaticDirect || kind == PlaybackStreamKind.DirectStream;
    }

    /// <summary>是否为转码地址（HLS 或 `/Videos/{id}/stream.{container}`）。</summary>
    public static bool IsTranscodeUrl(string url) => FromUrl(url).IsTranscode;

    /// <summary>是否为字幕地址。</summary>
    public static bool IsSubtitleUrl(string url) => FromUrl(url).Kind == PlaybackStreamKind.Subtitle;

    /// <summary>是否为图片地址。</summary>
    public static bool IsImageUrl(string url) => FromUrl(url).Kind == PlaybackStreamKind.Image;

    /// <summary>
    /// 「内部服务地址」判断（localhost / 回环 / 私有网段）。
    /// **推断项**：原版仅有同名符号 `isInternalServiceHostname`，无字符串实证，此处给语义合理的近似实现。
    /// </summary>
    public static bool IsInternalServiceHostname(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "::1" || host.EndsWith(".local", StringComparison.Ordinal)) return true;
            if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;
            if (System.Net.IPAddress.IsLoopback(ip)) return true;

            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return bytes[0] == 10
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
            }
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>把 URL 收敛为稳定的缓存键（去掉 `api_key`，避免 token 轮换导致缓存穿透）。</summary>
    public static string CacheKey(string url)
    {
        var parts = FromUrl(url);
        if (parts.Kind == PlaybackStreamKind.Unknown) return url ?? string.Empty;
        return string.Join("|",
            parts.Kind.ToString(),
            parts.ItemId,
            parts.MediaSourceId,
            parts.SubtitleStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            parts.AudioStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            parts.StartTicks?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    // ── 内部 ───────────────────────────────────────────────────────────────

    private static string NormalizeContainer(string container)
    {
        var value = (container ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        if (value.Length == 0) return string.Empty;
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch)) return string.Empty;
        }
        return value;
    }

    private static void ReadQuery(PlaybackStreamUrlParts parts, string query)
    {
        if (string.IsNullOrEmpty(query)) return;
        foreach (var pair in query.Split('&'))
        {
            if (pair.Length == 0) continue;
            var index = pair.IndexOf('=');
            var key = index < 0 ? pair : pair.Substring(0, index);
            var raw = index < 0 ? string.Empty : pair.Substring(index + 1);
            string value;
            try
            {
                value = Uri.UnescapeDataString(raw);
            }
            catch (UriFormatException)
            {
                value = raw;
            }

            switch (key.ToLowerInvariant())
            {
                case "api_key":
                    parts.ApiKey = value;
                    break;
                case "static":
                    parts.IsStatic = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    break;
                case "mediasourceid":
                    parts.MediaSourceId = value;
                    break;
                case "starttimetticks":
                    // 大整数/时间戳一律走 JsonRead 的 64 位口径解析（不经过 int）
                    parts.StartTicks = ParseLong(value);
                    break;
                case "audiostreamindex":
                    parts.AudioStreamIndex = ParseInt(value);
                    break;
                case "subtitlestreamindex":
                    parts.SubtitleStreamIndexParam = ParseInt(value);
                    break;
            }
        }
    }

    private static PlaybackStreamUrlParts MatchPath(PlaybackStreamUrlParts parts)
    {
        var path = parts.Path ?? string.Empty;

        var subtitle = SubtitlePath.Match(path);
        if (subtitle.Success)
        {
            parts.Kind = PlaybackStreamKind.Subtitle;
            parts.ItemId = subtitle.Groups["item"].Value;
            parts.MediaSourceId = subtitle.Groups["ms"].Value;
            parts.SubtitleStreamIndex = ParseInt(subtitle.Groups["index"].Value);
            parts.Format = subtitle.Groups["format"].Value;
            return parts;
        }

        var transcodeStream = TranscodeStreamPath.Match(path);
        if (transcodeStream.Success)
        {
            parts.Kind = PlaybackStreamKind.Transcode;
            parts.ItemId = transcodeStream.Groups["item"].Value;
            parts.Container = transcodeStream.Groups["container"].Value.ToLowerInvariant();
            return parts;
        }

        var direct = DirectStreamPath.Match(path);
        if (direct.Success)
        {
            parts.Kind = parts.IsStatic ? PlaybackStreamKind.StaticDirect : PlaybackStreamKind.DirectStream;
            parts.ItemId = direct.Groups["item"].Value;
            return parts;
        }

        var hls = HlsPath.Match(path);
        if (hls.Success)
        {
            parts.Kind = PlaybackStreamKind.Hls;
            parts.ItemId = hls.Groups["item"].Value;
            parts.Container = "m3u8";
            return parts;
        }

        var image = ImagePath.Match(path);
        if (image.Success)
        {
            parts.Kind = PlaybackStreamKind.Image;
            parts.ItemId = image.Groups["item"].Value;
            parts.ImageType = image.Groups["index"].Success
                ? image.Groups["type"].Value + "/" + image.Groups["index"].Value
                : image.Groups["type"].Value;
            return parts;
        }

        return parts;
    }

    /// <summary>64 位解析（`ticks` 会超过 int 上限，绝不截断）。</summary>
    private static long? ParseLong(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? (long)Math.Round(d, MidpointRounding.AwayFromZero)
            : (long?)null;
    }

    private static int? ParseInt(string value)
    {
        var l = ParseLong(value);
        if (!l.HasValue) return null;
        return l.Value > int.MaxValue ? int.MaxValue : (l.Value < int.MinValue ? int.MinValue : (int)l.Value);
    }
}

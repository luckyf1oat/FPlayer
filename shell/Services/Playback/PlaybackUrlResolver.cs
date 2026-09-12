// 原版文件（rebuild/ 中不存在 `playback_url_resolver.dart`），按 DESIGN §4.1 #31 与 §4.2 的映射表
// （`src/core/services/playback_url_resolver.dart` → `Services/Playback/PlaybackUrlResolver.cs`）
// + reversed/FlutterApp/SERVICE_API.md §1.3「取流与播放」新写。
//
// 实证依据（SERVICE_API §1.3）：
//   - **静态直连（首选，喂给内核 --open=）**：`GET {base}/Videos/{id}/stream?static=true&api_key={token}` [S]
//   - 播放信息：`POST /Items/{id}/PlaybackInfo` → `MediaSources[].DirectPlayUrl` / `TranscodingUrl` [S]
//   - 转码：`/Videos/{id}/stream.{container}` 或 `/videos/{id}/master.m3u8`（含 `/transcode/`）；
//     外壳字符串里另有 `directPlay=`、`mediaSource=`、`transcoding=` **三个选择开关** [S]
//     （strings_all.txt:210 `directPlay=` / :307 `mediaSource=` / :405 `transcoding=` 命中）
//   - 字幕：`/Videos/{id}/{mediaSourceId}/Subtitles/{index}/Stream.{format}`
//
// 设计口径：解析器**委托/组合** EmbyService 既有的 URL 构造（DirectStreamUrl / TranscodeUrl / SubtitleUrl /
// ImageUrl），不复制粘贴其查询串拼接；EmbyService 未覆盖的形态（`/Videos/{id}/stream.{container}` 直连转码流、
// `MediaSources[].DirectStreamUrl` 原样透传）由同目录的 PlaybackStreamUrl 补齐，本类按模式分派。

using System;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Playback;

/// <summary>取流方式（三个选择开关的语义载体，SERVICE_API §1.3）。</summary>
public enum PlaybackMode
{
    /// <summary>未指定 ⇒ 由解析器按「直连优先 → 转码回落」自动决定。</summary>
    Auto,

    /// <summary>`directPlay=`：文件直出（不经服务端封装转换）。</summary>
    DirectPlay,

    /// <summary>`directStream=`：服务端直出（此处与 DirectPlay 走同一直连端点，仅在开关允许性上区分）。</summary>
    DirectStream,

    /// <summary>`transcoding=`：服务端转码（HLS `/videos/{id}/master.m3u8` 或 `/Videos/{id}/stream.{container}`）。</summary>
    Transcode,
}

public static class PlaybackModeExtensions
{
    /// <summary>落盘/日志用的稳定小写标识。</summary>
    public static string Id(this PlaybackMode mode) => mode switch
    {
        PlaybackMode.DirectPlay => "directplay",
        PlaybackMode.DirectStream => "directstream",
        PlaybackMode.Transcode => "transcode",
        _ => "auto",
    };

    public static string DisplayName(this PlaybackMode mode) => mode switch
    {
        PlaybackMode.DirectPlay => "直连播放",
        PlaybackMode.DirectStream => "直连流",
        PlaybackMode.Transcode => "服务端转码",
        _ => "自动",
    };

    /// <summary>解析模式标识；无法识别时返回 <see cref="PlaybackMode.Auto"/>。</summary>
    public static PlaybackMode ParseId(string value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v switch
        {
            "directplay" => PlaybackMode.DirectPlay,
            "direct" => PlaybackMode.DirectPlay,
            "directstream" => PlaybackMode.DirectStream,
            "transcode" => PlaybackMode.Transcode,
            "transcoding" => PlaybackMode.Transcode,
            "hls" => PlaybackMode.Transcode,
            _ => PlaybackMode.Auto,
        };
    }
}

/// <summary>解析出的播放地址及其上下文（供内核 `--open=` 与进度上报复用）。</summary>
public sealed class PlaybackUrlResult
{
    public string Url { get; set; } = string.Empty;

    public PlaybackMode Mode { get; set; } = PlaybackMode.Auto;

    public string ItemId { get; set; } = string.Empty;

    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>Emby 的 ticks（1 秒 = 10,000,000），**64 位**（超过 int 上限的时长不截断）。</summary>
    public long? StartTicks { get; set; }

    public double? StartSeconds { get; set; }

    public int? AudioStreamIndex { get; set; }

    public int? SubtitleStreamIndex { get; set; }

    /// <summary>转码容器（`/Videos/{id}/stream.{container}` 形态；HLS 主播放列表为 `m3u8`）。</summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>命中的媒体源（可为 null：条目未带 MediaSources 时的直连回落）。</summary>
    public EmbyMediaSource Source { get; set; }

    /// <summary>直连地址取自 `MediaSources[].DirectStreamUrl` 原样透传（而非本地拼接）。</summary>
    public bool FromServerDirectStreamUrl { get; set; }

    public bool IsDirect => Mode == PlaybackMode.DirectPlay || Mode == PlaybackMode.DirectStream;

    public override string ToString() => $"PlaybackUrlResult({Mode.Id()}, item={ItemId}, ms={MediaSourceId})";
}

/// <summary>解析选项（对应 SERVICE_API §1.3 的三个选择开关）。</summary>
public sealed class PlaybackResolveOptions
{
    /// <summary>`directPlay=` 开关（false ⇒ 不允许直连）。默认 true。</summary>
    public bool AllowDirectPlay { get; set; } = true;

    /// <summary>`directStream=` 开关（false ⇒ 不允许服务端直出）。默认 true。</summary>
    public bool AllowDirectStream { get; set; } = true;

    /// <summary>`transcoding=` 开关（false ⇒ 不允许转码）。默认 true。</summary>
    public bool AllowTranscoding { get; set; } = true;

    /// <summary>`mediaSource=` 选择开关：指定后只在该 MediaSource 内取流。</summary>
    public string MediaSourceId { get; set; }

    /// <summary>强制模式（`directPlay` / `directStream` / `transcoding`）；Auto 表示自动决策。</summary>
    public PlaybackMode Mode { get; set; } = PlaybackMode.Auto;

    public double? StartSeconds { get; set; }

    public int? AudioStreamIndex { get; set; }

    public int? SubtitleStreamIndex { get; set; }

    /// <summary>转码容器；空表示走 HLS 主播放列表（`/videos/{id}/master.m3u8`）。</summary>
    public string TranscodeContainer { get; set; }

    public static PlaybackResolveOptions Default => new PlaybackResolveOptions();
}

/// <summary>
/// 播放地址解析（直连优先 → 转码回落）。
/// <see cref="Resolve(EmbyService, EmbyItem, PlaybackResolveOptions)"/> 是纯解析、不联网；
/// <see cref="ResolveAsync"/> 只在条目缺 <c>MediaSources</c> 时才补一次 `POST /Items/{id}/PlaybackInfo`。
/// </summary>
public sealed class PlaybackUrlResolver
{
    public PlaybackUrlResolver(EmbyService emby)
    {
        Emby = emby ?? throw new ArgumentNullException(nameof(emby));
    }

    public EmbyService Emby { get; }

    /// <summary>直连优先 → 转码回落（SERVICE_API §1.3 首选形态）。</summary>
    public PlaybackUrlResult Resolve(EmbyItem item, PlaybackResolveOptions options = null)
        => Resolve(Emby, item, options);

    /// <summary>缺 <c>MediaSources</c> 时补取播放信息后解析（`POST /Items/{id}/PlaybackInfo`，含 GET 降级）。</summary>
    public async Task<PlaybackUrlResult> ResolveAsync(
        EmbyItem item,
        PlaybackResolveOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var result = Resolve(item, options);
        if (result != null) return result;
        if (item == null || string.IsNullOrEmpty(item.Id)) return null;

        var opts = options ?? PlaybackResolveOptions.Default;
        var info = await Emby.PlaybackInfoAsync(
            item.Id,
            opts.MediaSourceId,
            opts.AudioStreamIndex,
            opts.SubtitleStreamIndex,
            opts.StartSeconds,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return info == null ? null : Resolve(info, options);
    }

    /// <summary>直接按给定 <see cref="EmbyMediaSource"/> 解析（跳过选源逻辑）。</summary>
    public PlaybackUrlResult ResolveSource(EmbyItem item, EmbyMediaSource source, PlaybackResolveOptions options = null)
    {
        if (item == null || string.IsNullOrEmpty(item.Id)) return null;
        var opts = options ?? PlaybackResolveOptions.Default;
        if (string.IsNullOrEmpty(opts.MediaSourceId) && source != null) opts.MediaSourceId = source.Id;
        return Resolve(Emby, item, opts);
    }

    /// <summary>
    /// 主解析：`mediaSource=` 选源 → `directPlay=`/`transcoding=` 决策 → 构造地址。
    /// 返回 <c>null</c> 表示「无可用取流路径」（条目不合法或三个开关全关）。
    /// </summary>
    public static PlaybackUrlResult Resolve(EmbyService emby, EmbyItem item, PlaybackResolveOptions options = null)
    {
        if (emby == null || item == null || string.IsNullOrEmpty(item.Id)) return null;

        var opts = options ?? PlaybackResolveOptions.Default;
        var source = SelectSource(item, opts.MediaSourceId);

        var mode = opts.Mode;
        if (mode == PlaybackMode.Auto)
        {
            if (opts.AllowDirectPlay && IsDirectPlayable(source)) mode = PlaybackMode.DirectPlay;
            else if (opts.AllowDirectStream) mode = PlaybackMode.DirectStream;
            else if (opts.AllowTranscoding) mode = PlaybackMode.Transcode;
            else return null;
        }

        if (mode == PlaybackMode.DirectPlay && !opts.AllowDirectPlay) return null;
        if (mode == PlaybackMode.DirectStream && !opts.AllowDirectStream) return null;
        if (mode == PlaybackMode.Transcode && !opts.AllowTranscoding) return null;

        var mediaSourceId = source != null && !string.IsNullOrEmpty(source.Id) ? source.Id : opts.MediaSourceId;

        string url;
        var container = string.Empty;
        var fromServerDirectStreamUrl = false;

        if (mode == PlaybackMode.Transcode)
        {
            if (!string.IsNullOrEmpty(opts.TranscodeContainer))
            {
                container = opts.TranscodeContainer;
                url = PlaybackStreamUrl.TranscodeStreamUrl(emby, item.Id, container, mediaSourceId,
                    opts.StartSeconds, opts.AudioStreamIndex, opts.SubtitleStreamIndex);
            }
            else
            {
                container = "m3u8";
                url = emby.TranscodeUrl(item.Id, mediaSourceId);
            }
        }
        else
        {
            // 首选：服务端在 PlaybackInfo 里给出的 DirectStreamUrl（原样透传，可为相对路径）
            var serverUrl = source?.DirectStreamUrl;
            if (opts.AllowDirectStream && !string.IsNullOrEmpty(serverUrl)
                && (source == null || source.SupportsDirectStream))
            {
                url = PlaybackStreamUrl.AbsoluteUrl(emby.BaseUrl, serverUrl);
                fromServerDirectStreamUrl = true;
            }
            else
            {
                container = source?.Container ?? string.Empty;
                url = emby.DirectStreamUrl(item.Id, mediaSourceId, opts.StartSeconds,
                    opts.AudioStreamIndex, opts.SubtitleStreamIndex);
            }
        }

        if (string.IsNullOrEmpty(url)) return null;

        return new PlaybackUrlResult
        {
            Url = url,
            Mode = mode,
            ItemId = item.Id,
            MediaSourceId = mediaSourceId ?? string.Empty,
            StartSeconds = opts.StartSeconds,
            StartTicks = opts.StartSeconds.HasValue ? TextUtils.SecondsToTicks(opts.StartSeconds.Value) : (long?)null,
            AudioStreamIndex = opts.AudioStreamIndex,
            SubtitleStreamIndex = opts.SubtitleStreamIndex,
            Container = container,
            Source = source,
            FromServerDirectStreamUrl = fromServerDirectStreamUrl,
        };
    }

    /// <summary>`mediaSource=` 选源：命中 Id 优先；无 Id 时取首个 MediaSource（播放信息接口的默认排序）。</summary>
    public static EmbyMediaSource SelectSource(EmbyItem item, string mediaSourceId)
    {
        var sources = item?.MediaSources;
        if (sources == null || sources.Count == 0) return null;

        if (!string.IsNullOrEmpty(mediaSourceId))
        {
            foreach (var s in sources)
            {
                if (string.Equals(s.Id, mediaSourceId, StringComparison.Ordinal)) return s;
            }
        }
        return sources[0];
    }

    /// <summary>
    /// 该媒体源是否可「直连播放」。
    /// 判据只用实证字段：`SupportsDirectPlay`（服务端声明）与 `Protocol == "File"`（本地文件）。
    /// **不**臆测客户端解码能力 —— 内核 mpv 的解码面不在服务层判定。
    /// </summary>
    public static bool IsDirectPlayable(EmbyMediaSource source)
    {
        if (source == null) return true; // 无 MediaSources 信息 ⇒ 直连仍是首选形态（SERVICE_API §1.3）
        if (!source.SupportsDirectPlay) return false;
        return string.IsNullOrEmpty(source.Protocol)
            || string.Equals(source.Protocol, "File", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>条目是否有可用取流路径。</summary>
    public static bool CanPlay(EmbyItem item) => item != null && !string.IsNullOrEmpty(item.Id);

    /// <summary>字幕地址（`/Videos/{id}/{mediaSourceId}/Subtitles/{index}/Stream.{format}`，委托 EmbyService）。</summary>
    public string SubtitleUrl(PlaybackUrlResult playback, int subtitleStreamIndex, string format = "srt")
    {
        if (playback == null || string.IsNullOrEmpty(playback.ItemId)) return string.Empty;
        return Emby.SubtitleUrl(playback.ItemId, playback.MediaSourceId, subtitleStreamIndex, format);
    }
}

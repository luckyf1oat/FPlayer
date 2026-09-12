// t27-F-A 第 ② 项：**41 参数组装器**（命令行通道）。
//
// 定位：`PlaybackRequest`（服务层产出的"一次播放"）→ `KernelLaunchRequest`（37 个 CLI 字段）
//       → **参数原文**（内核 `App.cs:55-321` 的前缀解析循环能逐条吃回去）。
//
// 三条硬纪律（都来自 reversed 内核源码，不是从二手清单抄的）：
//   ① **参数权威 = 内核自己的前缀解析循环**（`App.cs:55-321`）；本文件的参数名/形态逐条对齐它的 else-if 链。
//   ② **`--danmaku-match-name=` 走命令行必须 URL 编码** —— `App.cs:235` 有 `Uri.UnescapeDataString` **会解码**；
//      而进程内形态（M1）必须传未编码原名（`AppViewModel.cs:251` 原样赋值）。这是唯一语义相反的字段。
//   ③ **复合值一律 URL-safe Base64(UTF-8 JSON)**（`App.cs:406-798` 的 ParseTrackOption 系全部先 `Replace('-','+')`、
//      `Replace('_','/')` 再补 padding）⇒ 统一走服务层 DTO 的 `ToBase64()`（`HostValueCodec`），
//      **不手搓 JSON**，避免出现第二套编码规则。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Player;

namespace AIPlayer.Shell.KernelHost;

/// <summary>播放请求 → 内核启动参数（**命令行通道 = M3 = 唯一验收路线**）。</summary>
public static class KernelArgumentBuilder
{
    /// <summary>
    /// 一次播放的 CLI 参数**全量**映射。
    /// <paramref name="request"/> 提供随条目变化的部分；<paramref name="settings"/> 提供行为开关（代理/快捷键/画面/音量/连播）。
    /// </summary>
    public static KernelLaunchRequest FromPlayback(
        PlaybackRequest request,
        AppSettings settings,
        string callbackUrl,
        string libMpvPath,
        int? parentPid)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        settings ??= new AppSettings();

        var launch = new KernelLaunchRequest
        {
            // ① 核心
            OpenUrl = request.MediaPath,
            // t314：标题/副标题**必须先过"用户可见文本"闸门** —— 上游为空或值本身是 URL/凭据形状时，
            // 一律换成非 URL 占位（内核覆盖层在无 `--title` 时会回落 `media-title` = `--open` 原始 URL，
            // 那正是用户 2026-09-13 看到的 `stream?static=true&api_key=…`）。
            Title = SanitizedTitle(request),
            Subtitle = SanitizedSubtitle(request?.Subtitle),
            CallbackUrl = callbackUrl,
            LibMpvPath = libMpvPath,
            // t78-A（2026-09-12 修正）：**必须显式传** `--user-agent=`。
            // 旧注释写的是「刻意不传：内核 `HandleLaunchAsync` 自己写死 `ClientIdentity.HttpUserAgent`
            // （`AppViewModel.cs:303`）⇒ 我方设了也是被覆盖（T12_PLAYBACK_BRIDGE.md 该行）」——
            // 该前提**已被 `kernel2` 的三层闭环（只读反编译 + 读码 + 线上裸 `TcpListener` 夹具）证伪**：
            //   `--user-agent=` → `App.cs:194-199` → `:342-343` → `:841-843` 才是 AppViewModel 的 UA 来源；
            //   而 `--http-header=` 落在**另一个字段**（`AppViewModel.cs:339` vs `:340`）⇒ 两条通道不互通，
            //   不传 `--user-agent=` 时内核取 `ClientIdentity.HttpUserAgent` 的**默认值 `ai-player`**
            //   （与外壳唯一 UA 构造点 `UserAgentPolicy.Default`（`UserAgentPolicy.cs:38`）同值）
            //   ⇒ 用户改 UA 只影响外壳的 API/图片，**播放流量永远是 `ai-player`**（原缺陷）。
            // 取值 = 设置里的原始 UA；空白 ⇒ `null` = 不传该参数 ⇒ 内核维持默认（行为不变，也是内核
            // 「非空才生效」`App.cs:194` 的语义）。
            UserAgent = string.IsNullOrWhiteSpace(settings.UserAgent) ? null : settings.UserAgent.Trim(),
            ParentPid = parentPid,

            // ② 剧集标识 / 徽标
            PreviousEpisodeId = request.PreviousEpisodeId,
            NextEpisodeId = request.NextEpisodeId,
            SeasonId = request.SeasonId,
            Monogram = request.Monogram,
            Logo = request.Logo,
            BackdropUrl = request.BackdropUrl,
            Badges = new List<string>(request.Badges ?? new List<string>()),

            // ③ 轨道 / 版本 / 请求头（Emby 取流靠 X-Emby-Token）
            AudioTracks = new List<HostTrackOption>(request.AudioTracks ?? new List<HostTrackOption>()),
            SubtitleTracks = new List<HostTrackOption>(request.SubtitleTracks ?? new List<HostTrackOption>()),
            VersionOptions = new List<HostVersionOption>(request.VersionOptions ?? new List<HostVersionOption>()),
            HttpHeaders = new Dictionary<string, string>(request.HttpHeaders ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),

            // ④ 字幕 / 弹幕
            SubtitleId = request.SubtitleId,
            SubtitleUrl = request.SubtitleUrl,
            DanmakuMatchName = request.DanmakuMatchName,
            DanmakuApis = ActiveDanmakuApis(settings),

            // ⑤ 跳过 / 章节 / 雪碧图 / 剧集列表
            Segments = new List<MediaSegmentDto>(request.Segments ?? new List<MediaSegmentDto>()),
            Chapters = new List<HostChapter>(request.Chapters ?? new List<HostChapter>()),
            Sprite = request.Sprite,
            EpisodeList = new List<HostEpisodeItem>(request.EpisodeList ?? new List<HostEpisodeItem>()),

// ⑥ 行为开关（设置驱动；对齐 表 "toHostOptions"）
            DisableSkipMarkers = settings.DisableSkipMarkers,
            Shortcuts = settings.Shortcuts,
            HttpProxy = settings.ProxyEnabled && !string.IsNullOrWhiteSpace(settings.ProxyUrl) ? settings.ProxyUrl : null,

            // ⑦ 画面
            // ⚠️ `--mpv-config-dir=` **刻意不传**：内核只在"显式给了目录"时才 `UseConfig=true`
            //    （`reversed/MpvHost/WinUISample.Models/HostLaunchOptions.cs:69` 契约入参；
            //     服务层无"用户自选 mpv 配置目录"设置项，`AppDataDir.MpvConfigDir` 是**内核自己的**目录）
            //    ⇒ 传了等于把内核的配置发现路径改写，属于本任务范围外的行为变更。
            MpvConfigDir = null,
            FitVideoSize = settings.FitVideoSize,
            VideoFitMode = NormalizeFitMode(settings.VideoFitMode),
            DefaultRtxVsr = settings.RtxVsr,
            DefaultRtxVideoHdr = settings.RtxVideoHdr,
            DefaultAnimeMode = settings.AnimeMode,
            DefaultSharpenMode = settings.SharpenMode,

            // ⑧ 连播 / 音量
            AutoPlayNextEpisode = settings.AutoPlayNextEpisode,
            MaxVolume = settings.MaxVolume,
        };

// ⑨ StartPosition 专项：**首播必须留空**（T12_PLAYBACK_BRIDGE.md）
        //    续播点已经在 MediaPath 的 StartTimeTicks 里；再传 --start=N 会两次 seek ⇒ 位置错位。
        launch.StartSeconds = null;

        return launch;
    }

    /// <summary>
    /// 生效的弹幕 API（**照抄设置的三条规则**：`danmakuEnabled=false ⇒ 空表` / `enabled &amp;&amp; url 非空白` / **取前 5**）。
    /// 规则来源：`rebuild` 侧 `app_settings.dart:239-243`（[重建版参照]）+ 内核 CLI 上限（`App.cs:225` **计数式** `list.Count &lt; 5`）。
    /// </summary>
    private static List<HostDanmakuApi> ActiveDanmakuApis(AppSettings settings)
    {
        var list = new List<HostDanmakuApi>();
        foreach (var api in settings.ActiveDanmakuApis)
        {
            list.Add(new HostDanmakuApi { Name = api.Name ?? string.Empty, Url = api.Url });
        }
        return list;
    }

    /// <summary>内核 <c>--video-fit-mode=</c> 的取值域（`App.cs:250-266`）：未知值**静默落 contain**。</summary>
    private static string NormalizeFitMode(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "cover":
            case "fill":
                return "cover";
            case "stretch":
                return "stretch";
            default:
                return "contain";
        }
    }

    /// <summary>t314：上游标题为空 / URL 形状时用的**非 URL 占位**（会出现在播放器覆盖层，必须是可读文本）。</summary>
    internal const string UnknownTitlePlaceholder = "(未知)";

    /// <summary>
    /// t314：判"URL / 凭据形状"的标记集（任一命中即**不采用**该值）。后两项是为"值本身是媒体路径/图片地址"留的保险。
    /// </summary>
    internal static readonly string[] UrlLikeMarkers =
    {
        "://", "stream?", "api_key=", "MediaSourceId=", "X-Emby-Token", "/Videos/", "/Items/",
    };

    /// <summary>t314：值是否含 URL/凭据形状标记（空值 ⇒ false）。</summary>
    internal static bool LooksUrlLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return false; }

        foreach (var marker in UrlLikeMarkers)
        {
            if (value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
        }

        return false;
    }

    /// <summary>
    /// t314：标题闸门 —— 空 / URL 形状 ⇒ <see cref="UnknownTitlePlaceholder"/>；
    /// 并落一行可观测读数 `PLAY-TITLE …`（改前是"空 ⇒ 不传 `--title=` ⇒ 内核回落 `media-title` = `--open` URL"）。
    /// </summary>
    private static string SanitizedTitle(PlaybackRequest request)
    {
        var rawTitle = request?.Title;
        var rawSubtitle = request?.Subtitle;
        var title = SanitizedDisplayText(rawTitle, UnknownTitlePlaceholder, out var reason);

        Program.Log("PLAY-TITLE t314 raw_title_len=" + (rawTitle?.Length ?? 0)
            + " raw_url_like=" + (LooksUrlLike(rawTitle) ? "true" : "false")
            + " title=\"" + title + "\""
            + " subtitle_len=" + (rawSubtitle?.Length ?? 0)
            + " subtitle_url_like=" + (LooksUrlLike(rawSubtitle) ? "true" : "false")
            + " reason=" + reason
            + " url_fallback_blocked=true");

        return title;
    }

    /// <summary>t314：副标题同闸门（空 ⇒ **空串**，不塞占位；只保证"不含 URL/凭据"）。</summary>
    private static string SanitizedSubtitle(string subtitle)
        => SanitizedDisplayText(subtitle, string.Empty, out _);

    /// <summary>
    /// t314：通用闸门 —— 空白 ⇒ <paramref name="fallback"/>；URL/凭据形状 ⇒ <paramref name="fallback"/>（理由经 out 回传，可观测）；
    /// 其余原样保留（**不做**任何同义改写/截断，避免影响既有观感）。
    /// </summary>
    internal static string SanitizedDisplayText(string value, string fallback, out string reason)
    {
        if (string.IsNullOrWhiteSpace(value)) { reason = "empty"; return fallback; }

        var trimmed = value.Trim();
        if (LooksUrlLike(trimmed)) { reason = "url-like"; return fallback; }

        reason = "kept";
        return trimmed;
    }

    /// <summary>
    /// 把请求渲染成**参数原文**（与 <c>ProcessStartInfo.Arguments</c> 完全一致，便于日志留证：
    /// 验收要求"命令行原文可见"）。⚠️ 调用方负责打码（<c>Program.Log</c> 是唯一打码咽喉 ——
    /// Emby 直连 URL 含 <c>api_key=</c>）。
    /// </summary>
    public static string BuildArguments(KernelLaunchRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var parts = new List<string>();

        // ── 顺序与内核 else-if 链无关（它是前缀解析，与顺序无关）；此处按可读性分组 ──
        AddIf(parts, "--libmpv=", request.LibMpvPath);
        AddIf(parts, "--callback-url=", request.CallbackUrl);

        // t78-A：`--user-agent=` 与 `--http-header=` 在内核侧是**两个不同字段**
        //（`AppViewModel.cs:340` vs `:339`）⇒ header 通道（含 `X-Emby-Token`）一字不动，UA 另走本行。
        // `AddIf` 自带空白判定（空 ⇒ 不传），并对含空格/引号的值加引号（`Quote`）。
        AddIf(parts, "--user-agent=", request.UserAgent);
        AddIf(parts, "--title=", request.Title);
        AddIf(parts, "--subtitle=", request.Subtitle);
        AddIf(parts, "--subtitle-id=", request.SubtitleId);
        AddIf(parts, "--subtitle-url=", request.SubtitleUrl);
        AddIf(parts, "--previous-episode-id=", request.PreviousEpisodeId);
        AddIf(parts, "--next-episode-id=", request.NextEpisodeId);
        AddIf(parts, "--season-id=", request.SeasonId);
        AddIf(parts, "--monogram=", request.Monogram);
        AddIf(parts, "--logo=", request.Logo);
        AddIf(parts, "--backdrop=", request.BackdropUrl);

        foreach (var badge in request.Badges ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(badge)) parts.Add("--badge=" + Quote(badge.Trim()));
        }

        foreach (var track in request.AudioTracks ?? new List<HostTrackOption>())
        {
            if (!string.IsNullOrWhiteSpace(track.Label)) parts.Add("--audio-track=" + track.ToBase64());
        }

        foreach (var track in request.SubtitleTracks ?? new List<HostTrackOption>())
        {
            if (!string.IsNullOrWhiteSpace(track.Label)) parts.Add("--subtitle-track=" + track.ToBase64());
        }

        foreach (var version in request.VersionOptions ?? new List<HostVersionOption>())
        {
            if (version.Index >= 0) parts.Add("--version-option=" + version.ToBase64());
        }

        foreach (var header in request.HttpHeaders ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value)) continue;
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = header.Key,
                ["value"] = header.Value,
            };
            parts.Add("--http-header=" + HostValueCodec.EncodeHostValue(payload.ToJsonString()));
        }

        foreach (var api in request.DanmakuApis ?? new List<HostDanmakuApi>())
        {
            // 内核 `App.cs:222-229`：url 空白 ⇒ Parse 返回 null ⇒ 丢弃；此处在源头就按同一判据过滤。
            if (string.IsNullOrWhiteSpace(api.Url)) continue;
            parts.Add("--danmaku-api=" + api.ToBase64());
        }

        // 🔴 命令行通道必须 URL 编码（`App.cs:235` 的 Uri.UnescapeDataString 会解码）
        if (!string.IsNullOrWhiteSpace(request.DanmakuMatchName))
        {
            parts.Add("--danmaku-match-name=" + Quote(Uri.EscapeDataString(request.DanmakuMatchName)));
        }

        foreach (var segment in request.Segments ?? new List<MediaSegmentDto>())
        {
            if (!segment.IsValid) continue;      // 内核同判据：startMs<=0 且无 endMs ⇒ 丢弃
            parts.Add("--segment=" + segment.ToBase64());
        }

        foreach (var chapter in request.Chapters ?? new List<HostChapter>())
        {
            parts.Add("--chapter=" + chapter.ToBase64());
        }

        if (request.Sprite != null && request.Sprite.Width > 0 && request.Sprite.Height > 0
            && !string.IsNullOrWhiteSpace(request.Sprite.VttUrl))
        {
            parts.Add("--sprite=" + request.Sprite.ToBase64());
        }

        if ((request.EpisodeList?.Count ?? 0) > 0)
        {
            var array = new System.Text.Json.Nodes.JsonArray();
            foreach (var item in request.EpisodeList)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) continue;   // 内核同判据
                array.Add(item.ToJson());
            }
            if (array.Count > 0)
            {
                parts.Add("--episode-list=" + HostValueCodec.EncodeHostValue(array.ToJsonString()));
            }
        }

        if (request.DisableSkipMarkers) parts.Add("--disable-skip-markers");
        if (request.Shortcuts != null) parts.Add("--shortcuts=" + request.Shortcuts.ToBase64());
        AddIf(parts, "--http-proxy=", request.HttpProxy);
        AddIf(parts, "--mpv-config-dir=", request.MpvConfigDir);

        parts.Add("--fit-video-size=" + (request.FitVideoSize ? "true" : "false"));
        parts.Add("--video-fit-mode=" + NormalizeFitMode(request.VideoFitMode));

        // ⚠️ 内核只认 `--rtx-vsr=true` / `--rtx-video-hdr=true` 两个字面量（`App.cs:297-304`，无 false 分支）
        if (request.DefaultRtxVsr) parts.Add("--rtx-vsr=true");
        if (request.DefaultRtxVideoHdr) parts.Add("--rtx-video-hdr=true");

        AddIf(parts, "--anime-mode=", request.DefaultAnimeMode);
        AddIf(parts, "--sharpen-mode=", request.DefaultSharpenMode);
        parts.Add("--auto-play-next-episode=" + (request.AutoPlayNextEpisode ? "true" : "false"));
        parts.Add("--max-volume=" + request.MaxVolume.ToString(CultureInfo.InvariantCulture));

        if (request.StartSeconds.HasValue && request.StartSeconds.Value > 0)
        {
            parts.Add("--start=" + request.StartSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (request.ParentPid.HasValue && request.ParentPid.Value > 0)
        {
            parts.Add("--parent-pid=" + request.ParentPid.Value.ToString(CultureInfo.InvariantCulture));
        }

        // --open 放最后：契约「有值即视为打开请求」
        AddIf(parts, "--open=", request.OpenUrl);

        return string.Join(" ", parts);
    }

    /// <summary>参数面统计（自检/验收用：证明"接了几个"可复算，而不是自答）。</summary>
    public static string DescribeParameterSurface(KernelLaunchRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("params=").Append(BuildArguments(request).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        sb.Append(" badges=").Append(request.Badges?.Count ?? 0);
        sb.Append(" audioTracks=").Append(request.AudioTracks?.Count ?? 0);
        sb.Append(" subtitleTracks=").Append(request.SubtitleTracks?.Count ?? 0);
        sb.Append(" versions=").Append(request.VersionOptions?.Count ?? 0);
        sb.Append(" headers=").Append(request.HttpHeaders?.Count ?? 0);
        sb.Append(" danmakuApis=").Append(request.DanmakuApis?.Count ?? 0);
        sb.Append(" segments=").Append(request.Segments?.Count ?? 0);
        sb.Append(" chapters=").Append(request.Chapters?.Count ?? 0);
        sb.Append(" episodes=").Append(request.EpisodeList?.Count ?? 0);
        sb.Append(" sprite=").Append(request.Sprite != null);
        return sb.ToString();
    }

    private static void AddIf(List<string> parts, string prefix, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) parts.Add(prefix + Quote(value));
    }

    private static string Quote(string value)
    {
        if (value.IndexOf(' ') < 0 && value.IndexOf('"') < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

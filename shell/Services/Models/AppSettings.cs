// 等价移植：rebuild/ai_player/lib/core/models/app_settings.dart。
// 落盘 settings.json（**不含明文密码**，密码走 SecureKvStore/DPAPI）。
// 覆盖原版设置页全部可见项，并直接决定发给播放内核的参数（映射见 HOST_CONTRACT.md §2）。

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>弹幕 API 条目（最多 5 条会被内核接受，见 HOST_CONTRACT §3.4）。</summary>
public sealed class DanmakuApiConfig
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["url"] = Url,
        ["name"] = Name,
        ["enabled"] = Enabled,
    };

    public static DanmakuApiConfig FromJson(JsonElement json) => new DanmakuApiConfig
    {
        Url = JsonRead.Str(json, "url"),
        Name = JsonRead.Str(json, "name"),
        Enabled = JsonRead.Bool(json, "enabled", true),
    };
}

/// <summary>跳过片头/片尾的多源开关（对应 SERVICE_API §6.2 的五个来源）。</summary>
public sealed class SkipSourceSettings
{
    /// <summary>Emby / <c>/MediaSegments/{itemId}</c>。</summary>
    public bool Emby { get; set; } = true;

    /// <summary><c>api.theintrodb.org/v3/media</c>。</summary>
    public bool TheIntroDb { get; set; } = true;

    /// <summary><c>api.introdb.app/segments</c>。</summary>
    public bool IntroDbApp { get; set; }

    /// <summary><c>playback.theotherdb.org/api/metadata</c>。</summary>
    public bool TheOtherDb { get; set; }

    /// <summary>含 <c>{imdb}</c>/<c>{season}</c>/<c>{episode}</c> 占位符。</summary>
    public string CustomTemplate { get; set; } = string.Empty;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["emby"] = Emby,
        ["theIntroDb"] = TheIntroDb,
        ["introDbApp"] = IntroDbApp,
        ["theOtherDb"] = TheOtherDb,
        ["customTemplate"] = CustomTemplate,
    };

    public static SkipSourceSettings FromJson(JsonElement json) => new SkipSourceSettings
    {
        Emby = JsonRead.Bool(json, "emby", true),
        TheIntroDb = JsonRead.Bool(json, "theIntroDb", true),
        IntroDbApp = JsonRead.Bool(json, "introDbApp"),
        TheOtherDb = JsonRead.Bool(json, "theOtherDb"),
        CustomTemplate = JsonRead.Str(json, "customTemplate"),
    };

    public SkipSourceSettings With(bool? emby = null, bool? theIntroDb = null, bool? introDbApp = null,
        bool? theOtherDb = null, string customTemplate = null)
    {
        var c = (SkipSourceSettings)MemberwiseClone();
        if (emby.HasValue) c.Emby = emby.Value;
        if (theIntroDb.HasValue) c.TheIntroDb = theIntroDb.Value;
        if (introDbApp.HasValue) c.IntroDbApp = introDbApp.Value;
        if (theOtherDb.HasValue) c.TheOtherDb = theOtherDb.Value;
        if (customTemplate != null) c.CustomTemplate = customTemplate;
        return c;
    }
}

/// <summary>内核增强模式的**确切取值域**（来源：内核 <c>PlayerOverlay.cs</c> 右键菜单条目）。</summary>
public static class EnhancementModes
{
    public static readonly IReadOnlyDictionary<string, string> Anime = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["none"] = "未设置",
        ["standard"] = "标准",
        ["soft"] = "柔和",
        ["denoise"] = "降噪",
    };

    public static readonly IReadOnlyDictionary<string, string> Sharpen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["none"] = "未设置",
        ["light"] = "轻度",
        ["medium"] = "中度",
        ["strong"] = "强度",
    };

    public static readonly IReadOnlyDictionary<string, string> FitMode = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["contain"] = "适应",
        ["cover"] = "填充",
        ["stretch"] = "拉伸",
    };
}

/// <summary>应用设置（持久化到 <c>settings.json</c>）。</summary>
public sealed class AppSettings
{
    public const string DefaultProxyUrl = "http://127.0.0.1:7890";
    public const uint DefaultAccentColor = 0xFF0F6CBD;

    // ── 内核路径 ────────────────────────────────────────────────────────────
    /// <summary>为空 ⇒ 用 <c>AppDataDir.Instance.HostExe</c>。</summary>
    public string HostExeOverride { get; set; }

    public string LibMpvOverride { get; set; }

    /// <summary>参数写入 <c>--launch-file=</c> 传递（参数多/URL 超长时更稳）。</summary>
    public bool UseLaunchFile { get; set; } = true;

    // ── 画面 / 内核开关 ────────────────────────────────────────────────────
    public string VideoFitMode { get; set; } = "contain";
    public bool FitVideoSize { get; set; } = true;
    public int MaxVolume { get; set; } = 100;
    public bool RtxVsr { get; set; }
    public bool RtxVideoHdr { get; set; }
    public string AnimeMode { get; set; } = "none";
    public string SharpenMode { get; set; } = "none";

    // ── 播放行为 ───────────────────────────────────────────────────────────
    public bool AutoPlayNextEpisode { get; set; } = true;

    /// <summary>对应内核 <c>--disable-skip-markers</c>（此处为**正向**语义）。</summary>
    public bool SkipFeatureEnabled { get; set; } = true;

    public HostShortcuts Shortcuts { get; set; } = new HostShortcuts();

    // ── 网络 ───────────────────────────────────────────────────────────────
    public bool ProxyEnabled { get; set; }
    public string ProxyUrl { get; set; } = DefaultProxyUrl;

    /// <summary>
    /// t65 自定义 User-Agent；**空/纯空白 ⇒ 回落默认**（<c>Http.UserAgentPolicy.Default</c>，原版实测值），
    /// 任何情况下都不发空 UA。校验（控制字符/超长）在写入侧做，见 <c>SettingsService.TrySetUserAgent</c>。
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    // ── 字幕 ───────────────────────────────────────────────────────────────
    public string PreferredSubtitleLanguage { get; set; } = string.Empty;
    public bool AutoSelectSubtitle { get; set; } = true;

    // ── 弹幕 ───────────────────────────────────────────────────────────────
    public bool DanmakuEnabled { get; set; } = true;
    public List<DanmakuApiConfig> DanmakuApis { get; set; } = new List<DanmakuApiConfig>();

    /// <summary>匹配名模板；<c>{title}</c> <c>{episode}</c> <c>{season}</c> <c>{year}</c> 会被替换。</summary>
    public string DanmakuMatchTemplate { get; set; } = "{title} {episode}";

    // ── 跳过片头/片尾 ──────────────────────────────────────────────────────
    public bool SkipIntroEnabled { get; set; } = true;
    public bool SkipCreditsEnabled { get; set; } = true;
    public SkipSourceSettings SkipSources { get; set; } = new SkipSourceSettings();

    // ── 歌词 ───────────────────────────────────────────────────────────────
    public bool LyricsEnabled { get; set; } = true;

    // ── Trakt ──────────────────────────────────────────────────────────────
    public bool TraktEnabled { get; set; }
    public string TraktClientId { get; set; } = string.Empty;
    public string TraktClientSecret { get; set; } = string.Empty;

    // ── WebDAV 自动备份 ────────────────────────────────────────────────────
    public bool AutoBackupEnabled { get; set; }
    public string AutoBackupServerId { get; set; } = string.Empty;
    public int AutoBackupIntervalHours { get; set; } = 24;
    public int AutoBackupKeepCount { get; set; } = 7;

    // ── 界面 ───────────────────────────────────────────────────────────────
    /// <summary><c>system</c> | <c>light</c> | <c>dark</c>。</summary>
    public string ThemeMode { get; set; } = "system";

    public uint AccentColor { get; set; } = DefaultAccentColor;
    public bool AcrylicEnabled { get; set; } = true;

    /// <summary>
    /// 关闭主窗口时**最小化到托盘**（而不是退出程序）。默认 <c>false</c>（= 关闭即退出，与原版一致）。
    /// 供设置屏「关闭时最小化到托盘」一行使用（t30/ui3）；读取走 <see cref="SettingsService.Patch"/>，不新开写路径。
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; }

    public bool RememberWindowBounds { get; set; } = true;
    public int WindowX { get; set; }
    public int WindowY { get; set; }

    /// <summary><c>0</c> = 未记录（首次启动沿用系统默认尺寸）。</summary>
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public bool CheckUpdateOnStart { get; set; } = true;
    public string LastServerId { get; set; } = string.Empty;
    public int LibraryPageSize { get; set; } = 60;

    /// <summary>跨服务器合并媒体库（原版 <c>emby_item_list_merge</c>）。</summary>
    public bool MergeServerLibraries { get; set; }

    /// <summary>传给内核 <c>--disable-skip-markers</c> 的值。</summary>
    public bool DisableSkipMarkers => !SkipFeatureEnabled;

    /// <summary>生效的弹幕 API（启用且非空、最多 5 条 —— 内核上限）。</summary>
    public List<DanmakuApiConfig> ActiveDanmakuApis
    {
        get
        {
            var result = new List<DanmakuApiConfig>();
            if (!DanmakuEnabled) return result;
            foreach (var api in DanmakuApis)
            {
                if (result.Count >= 5) break;
                if (api.Enabled && api.Url.Trim().Length > 0) result.Add(api);
            }
            return result;
        }
    }

    public System.Text.Json.Nodes.JsonObject ToJson()
    {
        var o = new System.Text.Json.Nodes.JsonObject();
        if (HostExeOverride != null) o["hostExeOverride"] = HostExeOverride;
        if (LibMpvOverride != null) o["libMpvOverride"] = LibMpvOverride;
        o["useLaunchFile"] = UseLaunchFile;
        o["videoFitMode"] = VideoFitMode;
        o["fitVideoSize"] = FitVideoSize;
        o["maxVolume"] = MaxVolume;
        o["rtxVsr"] = RtxVsr;
        o["rtxVideoHdr"] = RtxVideoHdr;
        o["animeMode"] = AnimeMode;
        o["sharpenMode"] = SharpenMode;
        o["autoPlayNextEpisode"] = AutoPlayNextEpisode;
        o["skipFeatureEnabled"] = SkipFeatureEnabled;
        o["shortcuts"] = Shortcuts.ToJson();
        o["proxyEnabled"] = ProxyEnabled;
        o["proxyUrl"] = ProxyUrl;
        o["userAgent"] = UserAgent;
        o["preferredSubtitleLanguage"] = PreferredSubtitleLanguage;
        o["autoSelectSubtitle"] = AutoSelectSubtitle;
        o["danmakuEnabled"] = DanmakuEnabled;

        var apis = new System.Text.Json.Nodes.JsonArray();
        foreach (var api in DanmakuApis) apis.Add(api.ToJson());
        o["danmakuApis"] = apis;

        o["danmakuMatchTemplate"] = DanmakuMatchTemplate;
        o["skipIntroEnabled"] = SkipIntroEnabled;
        o["skipCreditsEnabled"] = SkipCreditsEnabled;
        o["skipSources"] = SkipSources.ToJson();
        o["lyricsEnabled"] = LyricsEnabled;
        o["traktEnabled"] = TraktEnabled;
        o["traktClientId"] = TraktClientId;
        o["traktClientSecret"] = TraktClientSecret;
        o["autoBackupEnabled"] = AutoBackupEnabled;
        o["autoBackupServerId"] = AutoBackupServerId;
        o["autoBackupIntervalHours"] = AutoBackupIntervalHours;
        o["autoBackupKeepCount"] = AutoBackupKeepCount;
        o["themeMode"] = ThemeMode;
        o["accentColor"] = AccentColor;
        o["acrylicEnabled"] = AcrylicEnabled;
        o["minimizeToTrayOnClose"] = MinimizeToTrayOnClose;
        o["rememberWindowBounds"] = RememberWindowBounds;
        o["windowX"] = WindowX;
        o["windowY"] = WindowY;
        o["windowWidth"] = WindowWidth;
        o["windowHeight"] = WindowHeight;
        o["windowMaximized"] = WindowMaximized;
        o["checkUpdateOnStart"] = CheckUpdateOnStart;
        o["lastServerId"] = LastServerId;
        o["libraryPageSize"] = LibraryPageSize;
        o["mergeServerLibraries"] = MergeServerLibraries;
        return o;
    }

    /// <summary>
    /// t80：我们**已建模**的键集合（= <see cref="ToJson"/> 会写出的键 ∪ 两个可空的 override 键）。
    /// 用途：存盘保底时"只覆盖我们懂的键"，一次性回填时"只增、绝不碰已建模键"。
    /// </summary>
    public static IReadOnlySet<string> ModeledKeys { get; } = BuildModeledKeys();

    private static IReadOnlySet<string> BuildModeledKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kv in new AppSettings().ToJson()) keys.Add(kv.Key);
        keys.Add("hostExeOverride");
        keys.Add("libMpvOverride");
        return keys;
    }

    /// <summary>
    /// t80 **存盘保底**：以盘上现有 JSON 对象为底，只把我们已建模的键覆盖上去，其余键**原样留存**。
    /// 语义 = "我们只改自己懂的键"；盘上没有的对象（null）⇒ 从空对象起（等价于旧行为）。
    /// </summary>
    public static JsonObject MergeModeledKeys(JsonObject baseline, AppSettings settings)
    {
        var result = baseline == null ? new JsonObject() : (JsonObject)baseline.DeepClone();
        if (settings == null) return result;
        foreach (var kv in settings.ToJson())
        {
            result[kv.Key] = kv.Value?.DeepClone();
        }
        return result;
    }

    public static AppSettings FromJson(JsonElement json)
    {
        var f = new AppSettings();
        var settings = new AppSettings
        {
            HostExeOverride = JsonRead.Prop(json, "hostExeOverride")?.ValueKind == JsonValueKind.String ? JsonRead.Str(json, "hostExeOverride") : null,
            LibMpvOverride = JsonRead.Prop(json, "libMpvOverride")?.ValueKind == JsonValueKind.String ? JsonRead.Str(json, "libMpvOverride") : null,
            UseLaunchFile = JsonRead.Bool(json, "useLaunchFile", f.UseLaunchFile),
            VideoFitMode = JsonRead.Str(json, "videoFitMode", f.VideoFitMode),
            FitVideoSize = JsonRead.Bool(json, "fitVideoSize", f.FitVideoSize),
            MaxVolume = JsonRead.IntOrNull(json, "maxVolume") ?? f.MaxVolume,
            RtxVsr = JsonRead.Bool(json, "rtxVsr", f.RtxVsr),
            RtxVideoHdr = JsonRead.Bool(json, "rtxVideoHdr", f.RtxVideoHdr),
            AnimeMode = JsonRead.Str(json, "animeMode", f.AnimeMode),
            SharpenMode = JsonRead.Str(json, "sharpenMode", f.SharpenMode),
            AutoPlayNextEpisode = JsonRead.Bool(json, "autoPlayNextEpisode", f.AutoPlayNextEpisode),
            SkipFeatureEnabled = JsonRead.Bool(json, "skipFeatureEnabled", f.SkipFeatureEnabled),
            ProxyEnabled = JsonRead.Bool(json, "proxyEnabled", f.ProxyEnabled),
            ProxyUrl = JsonRead.Str(json, "proxyUrl", f.ProxyUrl),
            UserAgent = JsonRead.Str(json, "userAgent", f.UserAgent),
            PreferredSubtitleLanguage = JsonRead.Str(json, "preferredSubtitleLanguage", f.PreferredSubtitleLanguage),
            AutoSelectSubtitle = JsonRead.Bool(json, "autoSelectSubtitle", f.AutoSelectSubtitle),
            DanmakuEnabled = JsonRead.Bool(json, "danmakuEnabled", f.DanmakuEnabled),
            DanmakuMatchTemplate = JsonRead.Str(json, "danmakuMatchTemplate", f.DanmakuMatchTemplate),
            SkipIntroEnabled = JsonRead.Bool(json, "skipIntroEnabled", f.SkipIntroEnabled),
            SkipCreditsEnabled = JsonRead.Bool(json, "skipCreditsEnabled", f.SkipCreditsEnabled),
            LyricsEnabled = JsonRead.Bool(json, "lyricsEnabled", f.LyricsEnabled),
            TraktEnabled = JsonRead.Bool(json, "traktEnabled", f.TraktEnabled),
            TraktClientId = JsonRead.Str(json, "traktClientId", f.TraktClientId),
            TraktClientSecret = JsonRead.Str(json, "traktClientSecret", f.TraktClientSecret),
            AutoBackupEnabled = JsonRead.Bool(json, "autoBackupEnabled", f.AutoBackupEnabled),
            AutoBackupServerId = JsonRead.Str(json, "autoBackupServerId", f.AutoBackupServerId),
            AutoBackupIntervalHours = JsonRead.IntOrNull(json, "autoBackupIntervalHours") ?? f.AutoBackupIntervalHours,
            AutoBackupKeepCount = JsonRead.IntOrNull(json, "autoBackupKeepCount") ?? f.AutoBackupKeepCount,
            ThemeMode = JsonRead.Str(json, "themeMode", f.ThemeMode),
            AccentColor = (uint)(JsonRead.LongOrNull(json, "accentColor") ?? f.AccentColor),
            AcrylicEnabled = JsonRead.Bool(json, "acrylicEnabled", f.AcrylicEnabled),
            MinimizeToTrayOnClose = JsonRead.Bool(json, "minimizeToTrayOnClose", f.MinimizeToTrayOnClose),
            RememberWindowBounds = JsonRead.Bool(json, "rememberWindowBounds", f.RememberWindowBounds),
            WindowX = JsonRead.IntOrNull(json, "windowX") ?? f.WindowX,
            WindowY = JsonRead.IntOrNull(json, "windowY") ?? f.WindowY,
            WindowWidth = JsonRead.IntOrNull(json, "windowWidth") ?? f.WindowWidth,
            WindowHeight = JsonRead.IntOrNull(json, "windowHeight") ?? f.WindowHeight,
            WindowMaximized = JsonRead.Bool(json, "windowMaximized", f.WindowMaximized),
            CheckUpdateOnStart = JsonRead.Bool(json, "checkUpdateOnStart", f.CheckUpdateOnStart),
            LastServerId = JsonRead.Str(json, "lastServerId", f.LastServerId),
            LibraryPageSize = JsonRead.IntOrNull(json, "libraryPageSize") ?? f.LibraryPageSize,
            MergeServerLibraries = JsonRead.Bool(json, "mergeServerLibraries", f.MergeServerLibraries),
        };

        var shortcuts = JsonRead.Prop(json, "shortcuts");
        settings.Shortcuts = shortcuts.HasValue && shortcuts.Value.ValueKind == JsonValueKind.Object
            ? HostShortcuts.FromJson(shortcuts.Value)
            : f.Shortcuts;

        var skipSources = JsonRead.Prop(json, "skipSources");
        settings.SkipSources = skipSources.HasValue && skipSources.Value.ValueKind == JsonValueKind.Object
            ? SkipSourceSettings.FromJson(skipSources.Value)
            : f.SkipSources;

        var apis = JsonRead.Prop(json, "danmakuApis");
        if (apis.HasValue && apis.Value.ValueKind == JsonValueKind.Array)
        {
            settings.DanmakuApis = new List<DanmakuApiConfig>();
            foreach (var element in JsonRead.Objects(apis.Value.EnumerateArray()))
            {
                settings.DanmakuApis.Add(DanmakuApiConfig.FromJson(element));
            }
        }

        return settings;
    }

    public AppSettings With(
        string hostExeOverride = null, bool clearHostExeOverride = false,
        string libMpvOverride = null, bool clearLibMpvOverride = false,
        bool? useLaunchFile = null, string videoFitMode = null, bool? fitVideoSize = null,
        int? maxVolume = null, bool? rtxVsr = null, bool? rtxVideoHdr = null,
        string animeMode = null, string sharpenMode = null, bool? autoPlayNextEpisode = null,
        bool? skipFeatureEnabled = null, HostShortcuts shortcuts = null,
        bool? proxyEnabled = null, string proxyUrl = null, string preferredSubtitleLanguage = null,
        bool? autoSelectSubtitle = null, bool? danmakuEnabled = null,
        List<DanmakuApiConfig> danmakuApis = null, string danmakuMatchTemplate = null,
        bool? skipIntroEnabled = null, bool? skipCreditsEnabled = null, SkipSourceSettings skipSources = null,
        bool? lyricsEnabled = null, bool? traktEnabled = null, string traktClientId = null,
        string traktClientSecret = null, bool? autoBackupEnabled = null, string autoBackupServerId = null,
        int? autoBackupIntervalHours = null, int? autoBackupKeepCount = null, string themeMode = null,
        uint? accentColor = null, bool? acrylicEnabled = null, bool? rememberWindowBounds = null,
        bool? minimizeToTrayOnClose = null,
        int? windowX = null, int? windowY = null, int? windowWidth = null, int? windowHeight = null,
        bool? windowMaximized = null, bool? checkUpdateOnStart = null, string lastServerId = null,
        int? libraryPageSize = null, bool? mergeServerLibraries = null)
    {
        var c = (AppSettings)MemberwiseClone();
        c.HostExeOverride = clearHostExeOverride ? null : (hostExeOverride ?? HostExeOverride);
        c.LibMpvOverride = clearLibMpvOverride ? null : (libMpvOverride ?? LibMpvOverride);
        if (useLaunchFile.HasValue) c.UseLaunchFile = useLaunchFile.Value;
        if (videoFitMode != null) c.VideoFitMode = videoFitMode;
        if (fitVideoSize.HasValue) c.FitVideoSize = fitVideoSize.Value;
        if (maxVolume.HasValue) c.MaxVolume = maxVolume.Value;
        if (rtxVsr.HasValue) c.RtxVsr = rtxVsr.Value;
        if (rtxVideoHdr.HasValue) c.RtxVideoHdr = rtxVideoHdr.Value;
        if (animeMode != null) c.AnimeMode = animeMode;
        if (sharpenMode != null) c.SharpenMode = sharpenMode;
        if (autoPlayNextEpisode.HasValue) c.AutoPlayNextEpisode = autoPlayNextEpisode.Value;
        if (skipFeatureEnabled.HasValue) c.SkipFeatureEnabled = skipFeatureEnabled.Value;
        if (shortcuts != null) c.Shortcuts = shortcuts;
        if (proxyEnabled.HasValue) c.ProxyEnabled = proxyEnabled.Value;
        if (proxyUrl != null) c.ProxyUrl = proxyUrl;
        if (preferredSubtitleLanguage != null) c.PreferredSubtitleLanguage = preferredSubtitleLanguage;
        if (autoSelectSubtitle.HasValue) c.AutoSelectSubtitle = autoSelectSubtitle.Value;
        if (danmakuEnabled.HasValue) c.DanmakuEnabled = danmakuEnabled.Value;
        if (danmakuApis != null) c.DanmakuApis = danmakuApis;
        if (danmakuMatchTemplate != null) c.DanmakuMatchTemplate = danmakuMatchTemplate;
        if (skipIntroEnabled.HasValue) c.SkipIntroEnabled = skipIntroEnabled.Value;
        if (skipCreditsEnabled.HasValue) c.SkipCreditsEnabled = skipCreditsEnabled.Value;
        if (skipSources != null) c.SkipSources = skipSources;
        if (lyricsEnabled.HasValue) c.LyricsEnabled = lyricsEnabled.Value;
        if (traktEnabled.HasValue) c.TraktEnabled = traktEnabled.Value;
        if (traktClientId != null) c.TraktClientId = traktClientId;
        if (traktClientSecret != null) c.TraktClientSecret = traktClientSecret;
        if (autoBackupEnabled.HasValue) c.AutoBackupEnabled = autoBackupEnabled.Value;
        if (autoBackupServerId != null) c.AutoBackupServerId = autoBackupServerId;
        if (autoBackupIntervalHours.HasValue) c.AutoBackupIntervalHours = autoBackupIntervalHours.Value;
        if (autoBackupKeepCount.HasValue) c.AutoBackupKeepCount = autoBackupKeepCount.Value;
        if (themeMode != null) c.ThemeMode = themeMode;
        if (accentColor.HasValue) c.AccentColor = accentColor.Value;
        if (acrylicEnabled.HasValue) c.AcrylicEnabled = acrylicEnabled.Value;
        if (minimizeToTrayOnClose.HasValue) c.MinimizeToTrayOnClose = minimizeToTrayOnClose.Value;
        if (rememberWindowBounds.HasValue) c.RememberWindowBounds = rememberWindowBounds.Value;
        if (windowX.HasValue) c.WindowX = windowX.Value;
        if (windowY.HasValue) c.WindowY = windowY.Value;
        if (windowWidth.HasValue) c.WindowWidth = windowWidth.Value;
        if (windowHeight.HasValue) c.WindowHeight = windowHeight.Value;
        if (windowMaximized.HasValue) c.WindowMaximized = windowMaximized.Value;
        if (checkUpdateOnStart.HasValue) c.CheckUpdateOnStart = checkUpdateOnStart.Value;
        if (lastServerId != null) c.LastServerId = lastServerId;
        if (libraryPageSize.HasValue) c.LibraryPageSize = libraryPageSize.Value;
        if (mergeServerLibraries.HasValue) c.MergeServerLibraries = mergeServerLibraries.Value;
        return c;
    }
}

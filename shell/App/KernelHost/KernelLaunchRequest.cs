using System;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// 一次内核启动的请求。字段与 **内核自己的解析循环** 一一对应
/// （权威 = <c>reversed/MpvHost/WinUISample/App.cs:55-321</c> 的 <c>ParseLaunchOptions</c>，
/// **按前缀循环解析**；<c>reversed/FlutterApp/HOST_CONTRACT.md</c> 仅作交叉核对）。
///
/// <para>⚠️ **不要照抄任何二手参数表**。本类 37 个字段的排序与命名以 <c>App.cs</c> 的 else-if 链为准，
/// 逐条注明内核行号；<c>--launch-file=</c>（<c>App.cs:366-403</c>）是**展开器**不是选项，故不建模。</para>
///
/// <para>**默认值 = 内核默认值**：只有"内核默认值与外壳默认值不同"的字段才需要外壳显式传
/// （例：内核 <c>fitVideoSize</c> 默认 <c>true</c>，外壳设置**也是** <c>true</c>
/// （<c>AppSettings.cs:128</c> 与 <c>AppSettings.Typed</c> 的 <c>fitVideoSize</c>、以及本类 <c>:132</c>）
/// ⇒ 无需显式传 —— 原文写"外壳设置默认 <c>false</c>"是**错的口径**，2026-09-12 按盘上实测订正
/// （<c>t78</c> 的 F2；此前那句被当成"必须显式传"的依据）。</para>
/// </summary>
public sealed class KernelLaunchRequest
{
    // ── 核心（既有面，t12 已实测）────────────────────────────────────────────

    /// <summary><c>--open=&lt;url|path&gt;</c>（<c>App.cs:106</c>）：媒体地址；**有值即视为打开请求**。</summary>
    public string OpenUrl { get; set; }

    /// <summary><c>--title=</c>（<c>App.cs:110</c>）：标题（覆盖媒体元数据）。</summary>
    public string Title { get; set; }

    /// <summary><c>--subtitle=</c>（<c>App.cs:114</c>）：副标题（一般是集号/年份）。</summary>
    public string Subtitle { get; set; }

    /// <summary><c>--callback-url=</c>（<c>App.cs:126</c>）：外壳 HTTP 端点；为空则内核独立工作（无进度上报）。</summary>
    public string CallbackUrl { get; set; }

    /// <summary><c>--libmpv=&lt;path&gt;</c>（<c>App.cs:102</c>）：libmpv-2.dll 路径。</summary>
    public string LibMpvPath { get; set; }

    /// <summary><c>--start=&lt;double&gt;</c>（<c>App.cs:206</c>）：起始秒；**InvariantCulture**（小数点用 <c>.</c>）。</summary>
    public double? StartSeconds { get; set; }

    /// <summary><c>--user-agent=</c>（<c>App.cs:194</c>）：非空才生效。**必须由外壳显式传**（t78-A 修正）：
    /// 不传时内核取 <c>ClientIdentity.HttpUserAgent</c> 的默认 <c>ai-player</c>，而 <c>--http-header=</c> 是
    /// **另一个字段**（<c>AppViewModel.cs:339</c> vs <c>:340</c>）⇒ 原文"内核自己写死 UA ⇒ 一般留空"的前提是错的
    /// （<c>kernel2</c> 三层闭环证伪）。</summary>
    public string UserAgent { get; set; }

    /// <summary><c>--parent-pid=&lt;int&gt;</c>（<c>App.cs:268</c>）：**需 &gt; 0 才生效**；内核随父进程退出。</summary>
    public int? ParentPid { get; set; }

    // ── 徽标 / 剧集标识（App.cs:130-153）────────────────────────────────────

    /// <summary><c>--previous-episode-id=</c>（<c>App.cs:130</c>）：上一集（内核按钮/连播依赖）。</summary>
    public string PreviousEpisodeId { get; set; }

    /// <summary><c>--next-episode-id=</c>（<c>App.cs:134</c>）。</summary>
    public string NextEpisodeId { get; set; }

    /// <summary><c>--season-id=</c>（<c>App.cs:138</c>）。</summary>
    public string SeasonId { get; set; }

    /// <summary><c>--monogram=</c>（<c>App.cs:142</c>）：海报缺图时的首字母占位。</summary>
    public string Monogram { get; set; }

    /// <summary><c>--logo=</c>（<c>App.cs:146</c>）。</summary>
    public string Logo { get; set; }

    /// <summary><c>--backdrop=</c>（<c>App.cs:150</c>）。⚠️ 参数名是 <c>--backdrop=</c>，不是 <c>--backdrop-url=</c>。</summary>
    public string BackdropUrl { get; set; }

    /// <summary><c>--badge=</c>（<c>App.cs:154</c>）：可重复；空白项被内核丢弃。</summary>
    public List<string> Badges { get; set; } = new List<string>();

    // ── 轨道 / 版本 / 请求头（App.cs:162-193）───────────────────────────────

    /// <summary><c>--audio-track=</c>（<c>App.cs:162</c>）：Base64(JSON)，可重复；<c>label</c> 空白整条丢弃。</summary>
    public List<HostTrackOption> AudioTracks { get; set; } = new List<HostTrackOption>();

    /// <summary><c>--subtitle-track=</c>（<c>App.cs:178</c>）：Base64(JSON)，可重复。</summary>
    public List<HostTrackOption> SubtitleTracks { get; set; } = new List<HostTrackOption>();

    /// <summary><c>--version-option=</c>（<c>App.cs:170</c>）：Base64(JSON)，可重复；<c>index &lt; 0</c> 丢弃。</summary>
    public List<HostVersionOption> VersionOptions { get; set; } = new List<HostVersionOption>();

    /// <summary><c>--http-header=</c>（<c>App.cs:186</c>）：<c>{"name":…,"value":…}</c> 的 Base64；**Emby 取流靠 <c>X-Emby-Token</c>**。</summary>
    public Dictionary<string, string> HttpHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // ── 字幕 / 弹幕（App.cs:118-236）───────────────────────────────────────

    /// <summary><c>--subtitle-id=</c>（<c>App.cs:118</c>）：首选字幕轨 id（进 <c>MpvPlayOptions.InitialSubtitleId</c>）。</summary>
    public string SubtitleId { get; set; }

    /// <summary><c>--subtitle-url=</c>（<c>App.cs:122</c>）：首选外挂字幕地址。</summary>
    public string SubtitleUrl { get; set; }

    /// <summary><c>--danmaku-api=</c>（<c>App.cs:222</c>）：<c>{"name":…,"url":…}</c> 的 Base64，可重复；**内核 CLI 路径上限 5 条**（<c>App.cs:225</c> 计数式）。</summary>
    public List<HostDanmakuApi> DanmakuApis { get; set; } = new List<HostDanmakuApi>();

    /// <summary>
    /// <c>--danmaku-match-name=</c>（<c>App.cs:230-237</c>）：**必须是 URL 编码后的值**。
    /// 🔴 唯一"进程内/外语义相反"的字段：命令行通道有 <c>Uri.UnescapeDataString</c>（<c>App.cs:235</c>）**会解码**，
    /// 而进程内形态必须传未编码原名（<c>AppViewModel.cs:251</c> 原样赋值）。本字段由 <c>KernelArgumentBuilder</c> 统一编码。
    /// </summary>
    public string DanmakuMatchName { get; set; }

    // ── 跳过 / 章节 / 雪碧图 / 剧集列表（App.cs:272-296）───────────────────

    /// <summary><c>--segment=</c>（<c>App.cs:277</c>）：Base64(JSON)，可重复；<c>startMs&lt;=0 且无 endMs</c> 丢弃。</summary>
    public List<MediaSegmentDto> Segments { get; set; } = new List<MediaSegmentDto>();

    /// <summary><c>--chapter=</c>（<c>App.cs:285</c>）：Base64(JSON, snake_case)，可重复。</summary>
    public List<HostChapter> Chapters { get; set; } = new List<HostChapter>();

    /// <summary><c>--sprite=</c>（<c>App.cs:293</c>）：Base64(JSON)；宽/高/URL 任一缺失即整条丢弃。</summary>
    public HostSprite Sprite { get; set; }

    /// <summary><c>--episode-list=</c>（<c>App.cs:272</c>）：**一个 JSON 数组**的 Base64，可重复（多段会累加）。</summary>
    public List<HostEpisodeItem> EpisodeList { get; set; } = new List<HostEpisodeItem>();

    // ── 行为开关（App.cs:202-221）─────────────────────────────────────────

    /// <summary><c>--disable-skip-markers</c>（<c>App.cs:202</c>）：无值布尔开关，出现即 true。</summary>
    public bool DisableSkipMarkers { get; set; }

    /// <summary><c>--shortcuts=</c>（<c>App.cs:210</c>）：Base64(JSON)；空 ⇒ 不传（内核回落 <c>PlayerShortcuts.Default</c>）。</summary>
    public HostShortcuts Shortcuts { get; set; }

    /// <summary><c>--http-proxy=</c>（<c>App.cs:214</c>）：非空白才生效。**关代理时必须不传**（不是传空串）。</summary>
    public string HttpProxy { get; set; }

    // ── 画面（App.cs:238-266 / 297-312）───────────────────────────────────

    /// <summary><c>--mpv-config-dir=</c>（<c>App.cs:238</c>）。</summary>
    public string MpvConfigDir { get; set; }

    /// <summary><c>--fit-video-size=false</c>（<c>App.cs:246</c>）：除字面 <c>false</c> 外都是 true。**内核默认 true**。</summary>
    public bool FitVideoSize { get; set; } = true;

    /// <summary><c>--video-fit-mode=</c>（<c>App.cs:250-266</c>）：<c>cover|fill|stretch|其余→contain</c>。**内核默认 contain**。</summary>
    public string VideoFitMode { get; set; } = "contain";

    /// <summary><c>--rtx-vsr=true</c>（<c>App.cs:297</c>）：⚠️ 参数**只认 <c>--rtx-vsr=true</c> 这一个字面量**（无 false 分支）。</summary>
    public bool DefaultRtxVsr { get; set; }

    /// <summary><c>--rtx-video-hdr=true</c>（<c>App.cs:301</c>）：同上，只认 true 字面量。</summary>
    public bool DefaultRtxVideoHdr { get; set; }

    /// <summary><c>--anime-mode=</c>（<c>App.cs:305</c>）：内核小写化；**内核默认 <c>none</c>**。</summary>
    public string DefaultAnimeMode { get; set; } = "none";

    /// <summary><c>--sharpen-mode=</c>（<c>App.cs:309</c>）：同上。</summary>
    public string DefaultSharpenMode { get; set; } = "none";

    // ── 连播 / 音量（App.cs:313-320）──────────────────────────────────────

    /// <summary><c>--auto-play-next-episode=</c>（<c>App.cs:313</c>）：**内核默认 true**（解析失败也落 true）。</summary>
    public bool AutoPlayNextEpisode { get; set; } = true;

    /// <summary><c>--max-volume=</c>（<c>App.cs:317-320</c>）：内核二次夹取 <c>[100,200]</c>。**内核默认 100**。</summary>
    public int MaxVolume { get; set; } = 100;
}

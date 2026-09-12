// t27-F-A 第 ① 项：**导航类回调的响应体**（内核 ← 外壳的"下一条源"）。
//
// 缺陷（当刻已知，可复现）：`ShellCallback.Respond` 一律返回 `null` ⇒ 端点回 **200 + 空体** ⇒
// 内核读到的 options 为 `null` ⇒ `navigate_*` / `switch_version` / `refresh_playback_url` **不会换源续播**。
//
// 修复路线 = **B（服务层 DTO + 显式 camelCase）**，依据 `T12_PLAYBACK_BRIDGE.md` 第 0 条：
//   内核响应侧 DTO 全是 `internal`（`HostNavigateOptions.cs:7` 等）且全仓无 `InternalsVisibleTo`；
//   而"能否命名内核类型"是**接线形态的函数**（M1 同程序集可见 / M2 不可见）⇒ 一律走服务层 DTO，
//   两种形态下都可用，且不把工程形态变成编译期隐式依赖。
//
// [!] **空体 ≠ `{}`**（事实 54）：没有内容要说时必须回**空体**。
//   回 `{}`（或任何 `.MediaPath` 为空的非 null 对象）会让 `progress` 的消费端
//   （`PlayerViewModel.cs:5691-5720`，对非 null options 无 `MediaPath` 守卫）无条件写 `_pendingNextEpisodeData`
//   ⇒ `alreadyPreloaded` 恒真 ⇒ **本次会话内"下一集预载"被静默禁用**。
//
// [!] **为什么线上形态用一个专用 DTO 而不是直接序列化服务层 `HostNavigateOptions`**（实测教训，2026-09-11）：
//   服务层 DTO 上有**计算属性**（`MediaSegmentDto.Start/End/IsValid`、`HostSprite` 系…），
//   默认序列化会把它们一并写进线载荷；更致命的是 **`segments[].type` 是枚举** ⇒ 输出 `<c>0</c>`，
//   而内核 `HostNavigateSegmentOption.cs:7-8` 是 **`string`** + `(Type ?? "intro").ToLowerInvariant()` 分派
//   ⇒ 反序列化**抛异常** ⇒ **整条换源应答变 `null`**（症状＝"点了没反应"）。
//   ⇒ 故此处**显式投影**成 `[JsonPropertyName]` 逐字段钉死的线形态，并让自检断言 `segments[].type` 是字符串。

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIPlayer.Shell.Player;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.KernelHost;

/// <summary>一次导航回调的原始请求（服务层 `HostEvent` + 原始体），交给外壳的换源解析器。</summary>
public sealed class NavigateReplyRequest
{
    public NavigateReplyRequest(string eventName, string body, HostEvent hostEvent)
    {
        EventName = eventName;
        Body = body;
        Event = hostEvent;
    }

    public string EventName { get; }

    /// <summary>内核发来的 **camelCase 原始体**（含 `event` 键）。</summary>
    public string Body { get; }

    /// <summary>解析后的服务层事件对象（`progress` / `navigate_*` …）。</summary>
    public HostEvent Event { get; }
}

/// <summary>
/// 外壳回调端点（内核 → 外壳的唯一入站面）的持有者：启动一次、暴露 URL、**按事件类型给响应体**。
///
/// <para>响应形态（`T12_CALLBACK_SURFACE.md`，实测自 `PlaybackReportClient.cs`）：
/// 要响应体 **7** 条 = `progress`（走 `SendAndReceiveAsync`，只接受 HTTP 200）+ 6 条导航/换源类
/// （`SendForNavigateAsync`，任意 2xx）；忽略响应体 **2** 条 = `stopped` / `manual_resize`（端点回 204）。</para>
/// </summary>
public static class ShellCallback
{
    /// <summary>线载荷序列化选项：显式 camelCase + 跳过 null（字段名另有 `[JsonPropertyName]` 逐条钉死）。</summary>
    private static readonly JsonSerializerOptions WireJsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static InboundCallbackEndpoint Endpoint { get; private set; }

    public static string Url => Endpoint?.CallbackUrl;

    /// <summary>
    /// 换源解析器（外壳播放接线注册）。**返回 `null` 表示"本轮不换源"** ⇒ 端点回空体。
    /// 抛出的异常由 <see cref="Respond"/> 兜住 ⇒ 回空体（内核保持现状），绝不让一次解析失败打死回调面。
    /// </summary>
    public static Func<NavigateReplyRequest, HostNavigateOptions> NavigateHandler { get; set; }

    public static void StartOnce()
    {
        if (Endpoint != null)
        {
            return;
        }

        // t221（重建 t40 / E-P3）：**播放中控制**的用户可见面 —— 会话单点 + 托盘 + 自检驱动。
        // 落在这里的理由：本方法由 `App.OnLaunched` 调一次（早于任何可能触发播放的路径），
        // 而 `App.xaml.cs` 不在 t221 的 inScope ⇒ 接线点必须在 KernelHost 侧。
        // 任何失败都不许影响外壳启动（托盘起不来顶多少一个菜单，不能把回调面/窗口带崩）。
        try
        {
            Features.Player.PlaybackControlSession.Current.StartOnce();

            // `SHELL_TRAY=off` ⇒ **不建托盘**（t221 的反控开关：用来判"托盘窗口是否影响退出/启动"这类
            // 归因问题；默认 on，产品形态有托盘）。
            if (!string.Equals(Environment.GetEnvironmentVariable("SHELL_TRAY"), "off", StringComparison.OrdinalIgnoreCase))
            {
                Features.Player.PlaybackTray.Current.StartOnce();
            }
            else
            {
                Program.Log("PLAYER-TRAY skipped reason=SHELL_TRAY=off（反控臂）");
            }

            Features.Player.PlaybackControlSelfTest.RunIfRequested();
        }
        catch (Exception ex)
        {
            Program.Log("PLAYER-SESSION/TRAY start FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        // t61 项①「固定端口」：端口值的**唯一真相源** = 服务层 `ConfigPortService`（复用落盘值 / 被占则重选并落盘）。
        // 这里只消费它的裁决，并把裁决结果交给端点；端点不可绑时会**可见地**回落（见 `InboundCallbackEndpoint.Start`）。
        var preferredPort = 0;
        try
        {
            var portService = new Services.Infra.ConfigPortService(Services.Infra.AppDataDir.Instance, Program.Log);
            preferredPort = portService.Resolve();
        }
        catch (Exception ex)
        {
            // 解析失败 ⇒ 不静默：记一行，然后照旧用系统随机端口（回调面不能因此起不来）。
            Program.Log("SHELL-CALLBACK port-resolve FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        var endpoint = new InboundCallbackEndpoint(Respond, Program.Log);
        endpoint.Start(preferredPort);
        Endpoint = endpoint;
        Program.Log("SHELL-CALLBACK listening url=" + endpoint.CallbackUrl + " preferredPort=" + preferredPort);
    }

    private static string Respond(CallbackRequest request)
    {
        // 只记事件名与体长：体里可能出现标题/URL 等用户内容，且日志是全工程唯一打码咽喉之前的一道
        Program.Log("CALLBACK event=" + request.EventName + " bodyLen=" + (request.Body?.Length ?? 0));

        // 忽略响应体的 2 条在端点上就回 204 了，走不到这里；此处只处理「要体」的 7 条。
        if (HostEvent.KindOf(request.EventName) == HostEventKind.Unknown)
        {
            return null;
        }

        var handler = NavigateHandler;
        if (handler == null)
        {
            return null;
        }

        try
        {
            var hostEvent = HostEvent.Parse(ParseFlatJson(request.Body));
            var options = handler(new NavigateReplyRequest(request.EventName, request.Body, hostEvent));

            // **必须**有非空 MediaPath 才回体：空对象/空 MediaPath 一律回空体（见文件头"空体 ≠ {}"）
            if (options == null || string.IsNullOrWhiteSpace(options.MediaPath))
            {
                return null;
            }

            var json = BuildNavigateJson(options);
            Program.Log("CALLBACK-REPLY " + request.EventName + " bodyLen=" + json.Length);
            return json;
        }
        catch (Exception ex)
        {
            Program.Log("CALLBACK-REPLY-FAIL " + request.EventName + " " + ex.GetType().Name + ": " + ex.Message);
            return null;    // 异常 ⇒ 空体，内核保持现状
        }
    }

    /// <summary>
    /// 把服务层 <see cref="HostNavigateOptions"/> **显式投影**成线形态并序列化（**验收要的"原始响应体"**）。
    /// 投影规则：
    ///   · `segments[].type` 由 <see cref="MediaSegmentType"/> **映射成字符串**（内核侧是 `string`，枚举会炸）；
    ///   · 只投影内核 `HostNavigateOptions.cs:9-72` 认识的 22 个字段 ⇒ 计算属性（`start`/`end`/`isValid`…）
    ///     不再混进线载荷。
    /// </summary>
    public static string BuildNavigateJson(HostNavigateOptions options)
        => options == null ? null : JsonSerializer.Serialize(ToWire(options), WireJsonOptions);

    /// <summary>服务层 DTO → 线载荷（**唯一投影点**）。</summary>
    public static HostNavigateWire ToWire(HostNavigateOptions o)
    {
        var wire = new HostNavigateWire
        {
            MediaPath = o.MediaPath,
            NewItemId = o.NewItemId,
            Title = o.Title,
            Subtitle = o.Subtitle,
            Monogram = o.Monogram,
            Logo = o.Logo,
            BackdropUrl = o.BackdropUrl,
            Badges = o.Badges,
            VersionOptions = o.VersionOptions,
            SubtitleId = o.SubtitleId,
            SubtitleUrl = o.SubtitleUrl,
            CallbackUrl = o.CallbackUrl,
            PreviousEpisodeId = o.PreviousEpisodeId,
            NextEpisodeId = o.NextEpisodeId,
            SeasonId = o.SeasonId,
            StartPosition = o.StartPosition,
            DanmakuMatchName = o.DanmakuMatchName,
            CurrentEpisodeId = o.CurrentEpisodeId,
            AudioTracks = o.AudioTracks,
            SubtitleTracks = o.SubtitleTracks,
        };

        if (o.HttpHeaders != null && o.HttpHeaders.Count > 0)
        {
            wire.HttpHeaders = new Dictionary<string, string>(o.HttpHeaders, StringComparer.Ordinal);
        }

        if (o.Segments != null && o.Segments.Count > 0)
        {
            wire.Segments = new List<HostNavigateSegment>(o.Segments.Count);
            foreach (var segment in o.Segments)
            {
                wire.Segments.Add(new HostNavigateSegment
                {
                    // [!] 枚举 → 字符串（内核 `HostNavigateSegmentOption.Type` 是 string）
                    Type = SegmentTypeId(segment.Type),
                    StartMs = segment.StartMs,
                    EndMs = segment.EndMs,
                    Source = segment.Source,
                });
            }
        }

        return wire;
    }

    /// <summary>服务层 <see cref="MediaSegmentType"/> → 内核字符串 id（`App.cs:680-695` 的同一组字面量）。</summary>
    public static string SegmentTypeId(MediaSegmentType type) => type switch
    {
        MediaSegmentType.Recap => "recap",
        MediaSegmentType.Credits => "credits",
        MediaSegmentType.Preview => "preview",
        _ => "intro",
    };

    /// <summary>序列化选项（自检/对照用）。</summary>
    public static JsonSerializerOptions SerializerOptions => WireJsonOptions;

    /// <summary>内核回调体 → <see cref="HostEvent"/>（与 `HostEvent.Parse` 同一入口，避免两套解析）。</summary>
    public static HostEvent ParseEvent(string body) => HostEvent.Parse(ParseFlatJson(body));

    // ── E-P1（t38）外壳侧"解析环"：内核在 `progress` 上报体里带的 /segments 基址（`updateUrl`）────────────
    //   背景（P0 实测）：该字段由**内核自己生成**（PlayerUpdateServer，loopback + 临时端口 + 固定路径 /segments），
    //   随每次 `progress` 上报体送到外壳；而**此前外壳全仓 0 处消费它**（grep `updateUrl|UpdateUrl` ⇒ shell/App 0 命中）
    //   ⇒ 即使内核端口修好，外壳也拿不到地址。这里补的就是"**解析并保存到会话状态**"这一环。
    /// <summary>内核最近一次上报的 `/segments` 基址（**会话状态**；未收到过 ⇒ <c>null</c>）。</summary>
    public static string KernelUpdateUrl { get; private set; }

    // ── t221（E-P3）：回调体的**观察点** ────────────────────────────────────────
    //   播放中控制会话需要两类体：`progress`（状态 + `updateUrl`）与 `stopped`（本轮结束 ⇒ 控制项回未播放态）。
    //   [!] `stopped` 属于"忽略响应体"的 2 条之一，端点在它上面**提前 204 返回**
    //   （`InboundCallbackEndpoint.cs:267-272`）⇒ 通知必须在那个分支**之前**发出，
    //   否则会话永远不知道该把菜单灰回去（这正是"看着还在播放、其实已经停了"的来路）。

    /// <summary>回调体观察者（`(eventName, body)`）。**唯一订阅者** = `Features/Player` 的播放中控制会话。</summary>
    public static event Action<string, string> CallbackObserved;

    /// <summary>把一次回调体交给观察者（异常**不得**回流到端点线程）。</summary>
    public static void NotifyCallbackObserved(string eventName, string body)
    {
        var handler = CallbackObserved;
        if (handler == null)
        {
            return;
        }

        try
        {
            handler(eventName, body);
        }
        catch (Exception ex)
        {
            Program.Log("CALLBACK-OBSERVER-FAIL " + eventName + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>记录一条 <c>updateUrl</c>（**非空才覆盖**）。返回是否发生了变化（用于只在变化时打日志）。</summary>
    public static bool RecordUpdateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (string.Equals(KernelUpdateUrl, url, StringComparison.Ordinal)) return false;
        KernelUpdateUrl = url;
        return true;
    }

    /// <summary>
    /// 从回调体里取 `updateUrl` 并保存；取到 ⇒ 返回它，否则 <c>null</c>。
    /// 走的就是 <see cref="ParseFlatJson"/>（扁平解析，`updateUrl` 是标量键，不需要新建第二套解析）。
    /// </summary>
    public static string TryRecordUpdateUrlFromBody(string body)
    {
        var flat = ParseFlatJson(body);
        if (flat != null && flat.TryGetValue("updateUrl", out var value) && value is string url && !string.IsNullOrWhiteSpace(url))
        {
            RecordUpdateUrl(url);
            return url;
        }
        return null;
    }

    /// <summary>
    /// 极简扁平 JSON → 字典（**只取标量与字符串**；回调体是 `PlaybackReportClient` 产生的扁平 camelCase 对象）。
    /// 不建对象图：`HostEvent` 只读标量键（`event`/`positionSeconds`/`versionIndex`/`targetEpisodeId`…）。
    /// </summary>
    public static Dictionary<string, object> ParseFlatJson(string body)
    {
        var raw = new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(body))
        {
            return raw;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return raw;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        raw[prop.Name] = prop.Value.GetString();
                        break;
                    case JsonValueKind.Number:
                        if (prop.Value.TryGetInt64(out var l)) raw[prop.Name] = l;
                        else if (prop.Value.TryGetDouble(out var d)) raw[prop.Name] = d;
                        break;
                    case JsonValueKind.True:
                        raw[prop.Name] = true;
                        break;
                    case JsonValueKind.False:
                        raw[prop.Name] = false;
                        break;
                    case JsonValueKind.Null:
                        break;
                    default:
                        raw[prop.Name] = prop.Value.GetRawText();
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            Program.Log("CALLBACK-PARSE-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }

        return raw;
    }
}

/// <summary>
/// **线载荷**：字段名逐条对应内核 `HostNavigateOptions.cs:9-72`（22 条）。
/// 刻意不用服务层 DTO 直接序列化 —— 避免计算属性上线、避免枚举落成数字。
/// </summary>
public sealed class HostNavigateWire
{
    [JsonPropertyName("mediaPath")] public string MediaPath { get; set; }

    [JsonPropertyName("httpHeaders")] public Dictionary<string, string> HttpHeaders { get; set; }

    [JsonPropertyName("newItemId")] public string NewItemId { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("subtitle")] public string Subtitle { get; set; }

    [JsonPropertyName("monogram")] public string Monogram { get; set; }

    [JsonPropertyName("logo")] public string Logo { get; set; }

    [JsonPropertyName("backdropUrl")] public string BackdropUrl { get; set; }

    [JsonPropertyName("badges")] public List<string> Badges { get; set; }

    [JsonPropertyName("versionOptions")] public List<HostVersionOption> VersionOptions { get; set; }

    [JsonPropertyName("subtitleId")] public string SubtitleId { get; set; }

    [JsonPropertyName("subtitleUrl")] public string SubtitleUrl { get; set; }

    [JsonPropertyName("callbackUrl")] public string CallbackUrl { get; set; }

    [JsonPropertyName("previousEpisodeId")] public string PreviousEpisodeId { get; set; }

    [JsonPropertyName("nextEpisodeId")] public string NextEpisodeId { get; set; }

    [JsonPropertyName("seasonId")] public string SeasonId { get; set; }

    [JsonPropertyName("startPosition")] public double? StartPosition { get; set; }

    [JsonPropertyName("danmakuMatchName")] public string DanmakuMatchName { get; set; }

    [JsonPropertyName("currentEpisodeId")] public string CurrentEpisodeId { get; set; }

    [JsonPropertyName("audioTracks")] public List<HostTrackOption> AudioTracks { get; set; }

    [JsonPropertyName("subtitleTracks")] public List<HostTrackOption> SubtitleTracks { get; set; }

    [JsonPropertyName("segments")] public List<HostNavigateSegment> Segments { get; set; }
}

/// <summary>线载荷里的跳过片段（内核 `HostNavigateSegmentOption.cs`：`type` 是 **string**）。</summary>
public sealed class HostNavigateSegment
{
    [JsonPropertyName("type")] public string Type { get; set; }

    [JsonPropertyName("startMs")] public long StartMs { get; set; }

    [JsonPropertyName("endMs")] public long? EndMs { get; set; }

    [JsonPropertyName("source")] public string Source { get; set; }
}

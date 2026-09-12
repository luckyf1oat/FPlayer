// 原版文件（rebuild/ 中不存在 `emby_server_profile.dart`），按 DESIGN §4.2 映射表
// （`src/core/util/emby_server_profile.dart` → `Services/Emby/EmbyServerProfile.cs`）新写。
//
// 依据：SERVICE_API §1.1「公共信息 GET /System/Info/Public」（免鉴权，新建服务器时先探活），
// 以及原版符号实证 `isJellyfin`（strings_all.txt:29606）与图标 `assets/icons/jellyfin.png`（:23679）。
// 字段名兼容性：Jellyfin 与 Emby 的 `/System/Info/Public` 返回键一致（`ServerName`/`Version`/`Id`），
// 但不同版本存在 ProductName 缺省或多形态 ⇒ 一律走 JsonRead 容错读取，并同时尝试大小写变体。

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Emby;

/// <summary>服务器画像/能力探测结果（对应原版 `emby_server_profile.dart`）。</summary>
public sealed class EmbyServerProfile
{
    public string ServerName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    /// <summary>服务端自报的产品名（Emby 为 `Emby Server`，Jellyfin 为 `Jellyfin Server`）。</summary>
    public string ProductName { get; set; } = string.Empty;

    public string ServerId { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>`GET /System/Info/Public` 的原始响应（未知字段透传）。</summary>
    public JsonObject Raw { get; set; } = new JsonObject();

    public bool HasInfo => Raw != null && Raw.Count > 0;

    /// <summary>
    /// 是否 Jellyfin。判据（任一命中，均为**推断**口径）：
    ///   ① 产品名/服务器名含 `jellyfin`；
    ///   ② 产品名含 `Jellyfin`（同一判断的大小写不敏感形态）。
    /// 原版仅实证了符号 <c>isJellyfin</c> 与 jellyfin 图标资源，判定字符串无直接实证。
    /// </summary>
    public bool IsJellyfin
        => Contains(ProductName, "jellyfin") || Contains(ServerName, "jellyfin");

    /// <summary>是否 Emby（产品名含 `emby`，或名字既不含 emby 也不含 jellyfin 时的默认值）。</summary>
    public bool IsEmby => !IsJellyfin;

    /// <summary>展示用类型名（`Jellyfin` / `Emby`）。</summary>
    public string DisplayName => IsJellyfin ? "Jellyfin" : "Emby";

    /// <summary>探测到的对应 <see cref="ServerKind"/>（供新建服务器时预选类型）。</summary>
    public ServerKind Kind => IsJellyfin ? ServerKind.Jellyfin : ServerKind.Emby;

    /// <summary>版本号（解析失败返回空串）。</summary>
    public string VersionText => Version ?? string.Empty;

    /// <summary>
    /// 媒体段接口 `/MediaSegments/{itemId}` 是否需要插件支持。
    /// **推断**：Emby 侧该端点由插件提供，Jellyfin 10.10+ 原生提供 ⇒ 恒返回 true 由调用方容错（`EmbyService` 已 try/catch）。
    /// </summary>
    public bool SupportsMediaSegments => true;

    /// <summary>`POST /Items/{id}/PlaybackInfo` 支持性（Emby/Jellyfin 均有；此处恒 true，仅作语义占位）。</summary>
    public bool SupportsPlaybackInfo => true;

    public static EmbyServerProfile FromPublicInfo(JsonElement? json)
    {
        var profile = new EmbyServerProfile();
        if (json == null || json.Value.ValueKind != JsonValueKind.Object) return profile;

        profile.Raw = JsonRead.DeepClone(json.Value);
        profile.ServerName = FirstString(json, "ServerName", "serverName", "Name", "name");
        profile.Version = FirstString(json, "Version", "version");
        profile.ProductName = FirstString(json, "ProductName", "productName");
        profile.ServerId = FirstString(json, "Id", "id", "ServerId", "serverId");
        profile.OperatingSystem = FirstString(json, "OperatingSystem", "operatingSystem");
        return profile;
    }

    public static EmbyServerProfile FromJson(JsonNode node) => FromPublicInfo(JsonRead.From(node));

    /// <summary>探测（免鉴权）：`GET /System/Info/Public`；失败抛 <see cref="ShellHttpException"/>（由调用方决定是否降级）。</summary>
    public static async Task<EmbyServerProfile> ProbeAsync(
        ShellHttpClient http,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var json = await EmbyService.PublicInfoAsync(http, baseUrl, cancellationToken).ConfigureAwait(false);
        return FromJson(json);
    }

    /// <summary>用现有 <see cref="EmbyService"/> 的连接信息探测同一台服务器。</summary>
    public static Task<EmbyServerProfile> ProbeAsync(EmbyService emby, CancellationToken cancellationToken = default)
    {
        if (emby == null) return Task.FromResult(new EmbyServerProfile());
        return ProbeAsync(emby.Http, emby.BaseUrl, cancellationToken);
    }

    /// <summary>
    /// 可达性探测（**不抛异常**：不可达返回 <c>null</c>）。
    /// t148：两个 catch 分支各留**一行可见日志**（此前是彻底静默 —— `t140` review 的 L-1，56 条候选里唯一确认的真静默）。
    /// 语义未变：**仍然不向上抛**，只是把"为什么返回 null"写出来（原因类型 + 状态码）。
    /// <paramref name="log"/> 为 null 时落到进程级 sink（<c>Logging.DebugLog</c>）。
    /// </summary>
    public static async Task<EmbyServerProfile> TryProbeAsync(
        ShellHttpClient http,
        string baseUrl,
        CancellationToken cancellationToken = default,
        Action<string> log = null)
    {
        var sink = log ?? (m => AIPlayer.Shell.Services.Logging.DebugLog.Info(m));
        try
        {
            return await ProbeAsync(http, baseUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (ShellHttpException ex)
        {
            sink($"PROBE-FAIL baseUrl={baseUrl} status={(ex.StatusCode.HasValue ? ex.StatusCode.Value.ToString() : "(none)")} ⇒ 不可达，返回 null");
            return null;
        }
        catch (Exception ex)
        {
            sink($"PROBE-FAIL baseUrl={baseUrl} reason={ex.GetType().Name}: {ex.Message} ⇒ 不可达，返回 null");
            return null;
        }
    }

    public override string ToString()
        => $"EmbyServerProfile({DisplayName} {VersionText}, name={ServerName}, id={ServerId})";

    private static string FirstString(JsonElement? json, params string[] names)
    {
        foreach (var name in names)
        {
            var value = JsonRead.Str(json, name);
            if (value.Length > 0) return value;
        }
        return string.Empty;
    }

    private static bool Contains(string value, string needle)
        => !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(needle);
}

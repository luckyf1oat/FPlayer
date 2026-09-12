// 等价移植：rebuild/ai_player/lib/core/models/server_config.dart。
// kind 决定走哪套协议；extra 存放服务特有字段（Navidrome 密码、ABS 的 libraryId、WebDAV 的 rootPath），避免为每个服务建表。
// 凭据不落 servers.json：password/token 存 SecureKvStore（密钥见 PasswordKey/TokenKey）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

public enum ServerKind
{
    Emby,
    Jellyfin,
    Navidrome,
    Audiobookshelf,
    WebDav,
}

public static class ServerKindExtensions
{
    /// <summary>落盘用的稳定标识（勿随意改，会影响既有配置读取）。</summary>
    public static string Id(this ServerKind kind) => kind switch
    {
        ServerKind.Emby => "emby",
        ServerKind.Jellyfin => "jellyfin",
        ServerKind.Navidrome => "navidrome",
        ServerKind.Audiobookshelf => "audiobookshelf",
        ServerKind.WebDav => "webdav",
        _ => "emby",
    };

    public static string DisplayName(this ServerKind kind) => kind switch
    {
        ServerKind.Emby => "Emby",
        ServerKind.Jellyfin => "Jellyfin",
        ServerKind.Navidrome => "Navidrome",
        ServerKind.Audiobookshelf => "Audiobookshelf",
        ServerKind.WebDav => "WebDAV",
        _ => "Emby",
    };

    /// <summary>Emby 与 Jellyfin 共用一套 API（<c>/Users/...</c>、<c>X-Emby-Token</c>）。</summary>
    public static bool IsEmbyFamily(this ServerKind kind) => kind == ServerKind.Emby || kind == ServerKind.Jellyfin;

    public static bool IsMusic(this ServerKind kind) => kind == ServerKind.Navidrome;

    public static bool IsAudioBook(this ServerKind kind) => kind == ServerKind.Audiobookshelf;

    /// <summary>等价 Dart <c>ServerKindX.parse</c>：不识别时回落 Emby。</summary>
    public static ServerKind ParseKind(object value)
    {
        var v = (value?.ToString() ?? string.Empty).ToLowerInvariant();
        foreach (ServerKind k in Enum.GetValues(typeof(ServerKind)))
        {
            if (k.Id() == v || k.ToString().ToLowerInvariant() == v) return k;
        }
        return ServerKind.Emby;
    }
}

public sealed class ServerConfig
{
    public string Id { get; set; } = string.Empty;
    public ServerKind Kind { get; set; } = ServerKind.Emby;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>自定义图标 URL（原版 <c>icon_library</c> 支持内置图标码与 URL）。</summary>
    public string IconUrl { get; set; } = string.Empty;

    /// <summary>自定义图标（内置图标库的资源码/字符），可为空。</summary>
    public string IconData { get; set; }

    public bool Enabled { get; set; } = true;
    public int SortIndex { get; set; }

    /// <summary>库过滤：被用户隐藏的库 id（原版 <c>library_filter_dialog</c>）。</summary>
    public HashSet<string> HiddenLibraryIds { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>服务特有字段：<c>password</c>（Navidrome）、<c>libraryId</c>（ABS）、<c>rootPath</c>（WebDAV）等。</summary>
    public Dictionary<string, JsonNode> Extra { get; set; } = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>Navidrome 密码（等价 Dart <c>extra['password'] as String?</c>）。</summary>
    public string Password => ExtraString("password");

    // ── t25-③ 「N 天前看过」的时间（原版字段 cachedLastPlayedAt）────────────────────────────
    //   数据面早已存在：兼容读把原版单条**除两个秘密字段外的全部属性**原样塞进 Extra
    //   （ServerConfigStore.FromOriginalAccount；实测注释见 ServerConfigStore.cs:16）。
    //   ⇒ 缺的只是**类型化入口**，不是数据。

    /// <summary>
    /// 「上次观看时间」的**原始串**（原版 <c>accounts.json</c> 字段 <c>cachedLastPlayedAt</c>，
    /// 本机实测形态 <c>2026-09-10T16:53:00</c>）；无该字段 ⇒ <c>null</c>。
    /// </summary>
    public string CachedLastPlayedAtRaw => ExtraString("cachedLastPlayedAt");

    /// <summary>
    /// 类型化的「上次观看时间」。**缺失 / 空白 / 无法解析 ⇒ <c>null</c>，绝不抛**。
    /// 解析用 <see cref="DateTimeOffset.TryParse(string, IFormatProvider, DateTimeStyles, out DateTimeOffset)"/> +
    /// <see cref="DateTimeStyles.RoundtripKind"/>；原版串不带时区时按**本地时间**解释（与原版 Dart <c>DateTime.parse</c> 同义）。
    /// <para><b>语义边界</b>：该值只对**从原版 <c>accounts.json</c> 兼容读进来**的服务器存在；外壳自建服务器没有它，
    /// 且原版语义里它由**原版播放**更新 ⇒ 新外壳若要维护它，必须先定"维护方"（见
    /// <see cref="SetCachedLastPlayedAt"/> 与 <see cref="IsOriginalAccountEntry"/>）。</para>
    /// </summary>
    public DateTimeOffset? CachedLastPlayedAt
    {
        get
        {
            var raw = CachedLastPlayedAtRaw;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;
        }
    }

    /// <summary>
    /// 写「上次观看时间」到**本配置的内存态** <see cref="Extra"/>（<c>null</c> ⇒ 移除该键）。
    /// <para><b>红线</b>（<c>ServerConfigStore.cs:17-18</c>）：原版 <c>accounts.json</c> <b>永不被写</b>。
    /// 本方法只改内存对象；落盘与否取决于条目来源 —— 原版兼容读进来的条目在 <c>ServerConfigStore.Save()</c> 里被**跳过**
    /// （既不会写进 <c>servers.json</c>，更不会回写 <c>accounts.json</c>），只有外壳自有条目会落盘。
    /// ⇒ **调用方若要为"原版只读服务器"维护该时间，必须另立外壳自有的存储**，不能指望本方法把它写进原版文件。</para>
    /// </summary>
    public void SetCachedLastPlayedAt(DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            SetExtra("cachedLastPlayedAt", value.Value.ToString("o", CultureInfo.InvariantCulture));
        }
        else
        {
            SetExtra("cachedLastPlayedAt", null);
        }
    }

    /// <summary>
    /// 本配置是否来自**原版 <c>accounts.json</c> 的兼容读**（只读投影）——由兼容读写下的
    /// <c>Extra["accountsOrigin"] = true</c> 标记判定（<c>ServerConfigStore.FromOriginalAccount</c>）。
    /// <para>写路径（含 <see cref="SetCachedLastPlayedAt"/> 与"N 天前看过"维护方选择）**必须**据此分支：
    /// 对原版条目只改内存、不落盘，或改为写到外壳自有的记录里。</para>
    /// </summary>
    public bool IsOriginalAccountEntry => ExtraBool("accountsOrigin");

    public string ExtraString(string key)
    {
        if (Extra == null || !Extra.TryGetValue(key, out var node) || node == null) return null;
        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return node.ToJsonString();
        }
    }

    public bool ExtraBool(string key, bool def = false)
    {
        if (Extra == null || !Extra.TryGetValue(key, out var node) || node == null) return def;
        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            return bool.TryParse(node.ToJsonString().Trim('"'), out var b) ? b : def;
        }
    }

    public void SetExtra(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value == null)
        {
            Extra.Remove(key);
            return;
        }
        Extra[key] = JsonValue.Create(value);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["kind"] = Kind.Id(),
            ["name"] = Name,
            ["baseUrl"] = BaseUrl,
            ["userName"] = UserName,
            ["userId"] = UserId,
            ["accessToken"] = AccessToken,
            ["iconUrl"] = IconUrl,
            ["enabled"] = Enabled,
            ["sortIndex"] = SortIndex,
        };
        if (IconData != null) o["iconData"] = IconData;

        var hidden = new JsonArray();
        foreach (var id in HiddenLibraryIds) hidden.Add(id);
        o["hiddenLibraryIds"] = hidden;

        var extra = new JsonObject();
        if (Extra != null)
        {
            foreach (var kv in Extra)
            {
                extra[kv.Key] = kv.Value == null ? null : kv.Value.DeepClone();
            }
        }
        o["extra"] = extra;
        return o;
    }

    public static ServerConfig FromJson(JsonElement json)
    {
        var cfg = new ServerConfig
        {
            Id = JsonRead.Str(json, "id"),
            Kind = ServerKindExtensions.ParseKind(JsonRead.Str(json, "kind")),
            Name = JsonRead.Str(json, "name"),
            BaseUrl = JsonRead.Str(json, "baseUrl"),
            UserName = JsonRead.Str(json, "userName"),
            UserId = JsonRead.Str(json, "userId"),
            AccessToken = JsonRead.Str(json, "accessToken"),
            IconUrl = JsonRead.Str(json, "iconUrl"),
            IconData = JsonRead.Prop(json, "iconData")?.ValueKind == JsonValueKind.String
                ? JsonRead.Str(json, "iconData")
                : null,
            Enabled = JsonRead.Bool(json, "enabled", true),
            SortIndex = JsonRead.IntOrNull(json, "sortIndex") ?? 0,
        };

        foreach (var id in JsonRead.StrList(json, "hiddenLibraryIds"))
        {
            cfg.HiddenLibraryIds.Add(id);
        }

        var extraElement = JsonRead.Prop(json, "extra");
        if (extraElement.HasValue && extraElement.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in extraElement.Value.EnumerateObject())
            {
                cfg.Extra[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? JsonValue.Create(p.Value.GetString())
                    : JsonNode.Parse(p.Value.GetRawText());
            }
        }
        return cfg;
    }

    public ServerConfig With(
        string id = null,
        string name = null,
        string baseUrl = null,
        string userName = null,
        string userId = null,
        string accessToken = null,
        string iconUrl = null,
        string iconData = null,
        bool? enabled = null,
        int? sortIndex = null,
        HashSet<string> hiddenLibraryIds = null,
        Dictionary<string, JsonNode> extra = null)
    {
        var clone = (ServerConfig)MemberwiseClone();
        if (id != null) clone.Id = id;
        if (name != null) clone.Name = name;
        if (baseUrl != null) clone.BaseUrl = baseUrl;
        if (userName != null) clone.UserName = userName;
        if (userId != null) clone.UserId = userId;
        if (accessToken != null) clone.AccessToken = accessToken;
        if (iconUrl != null) clone.IconUrl = iconUrl;
        if (iconData != null) clone.IconData = iconData;
        if (enabled.HasValue) clone.Enabled = enabled.Value;
        if (sortIndex.HasValue) clone.SortIndex = sortIndex.Value;
        if (hiddenLibraryIds != null) clone.HiddenLibraryIds = hiddenLibraryIds;
        if (extra != null) clone.Extra = extra;
        return clone;
    }

    /// <summary>凭据在 <c>SecureKvStore</c> 中的键（密码不落在 <c>servers.json</c>）。</summary>
    public string PasswordKey => $"server.{Id}.password";

    public string TokenKey => $"server.{Id}.token";

    public string RefreshTokenKey => $"server.{Id}.refreshToken";

    public override string ToString() => $"ServerConfig({Kind.Id()}, {Name}, {BaseUrl})";
}

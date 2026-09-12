// 新写（对应原版 `src/core/models/auth_session.dart`，DESIGN §4.2 → Services/Models/AuthSession.cs）。
// 字段依据 SERVICE_API.md §1.1（Emby 登录返回含 `AccessToken` / `User.Id`）、§2（Navidrome 登录）、§3（ABS POST /login → Bearer）。
// 证据等级：端点/[S] 实证；**字段组合属重建**（SERVICE_API 未逐字段实证），故一律容错解析并保留 Raw 供透传。

using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>一次登录会话（跨三端共用载体）。</summary>
public sealed class AuthSession
{
    public string ServerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    /// <summary>原始响应（未实证字段透传）。</summary>
    public JsonObject Raw { get; set; } = new JsonObject();

    /// <summary>Emby：<c>POST /Users/AuthenticateByName</c> → <c>{AccessToken, User:{Id,Name}}</c>。</summary>
    public static AuthSession FromEmbyJson(JsonElement json, string serverId)
    {
        var user = JsonRead.Prop(json, "User");
        return new AuthSession
        {
            ServerId = serverId,
            AccessToken = JsonRead.Str(json, "AccessToken"),
            UserId = JsonRead.Str(user, "Id"),
            UserName = JsonRead.Str(user, "Name"),
            Raw = JsonRead.DeepClone(json),
        };
    }

    /// <summary>Navidrome：<c>POST /auth/login</c> → <c>{token, ...}</c>（字段组合属重建，容错取多种命名）。</summary>
    public static AuthSession FromNavidromeJson(JsonElement json, string serverId)
    {
        var token = JsonRead.Str(json, "token");
        if (string.IsNullOrEmpty(token)) token = JsonRead.Str(json, "accessToken");
        if (string.IsNullOrEmpty(token)) token = JsonRead.Str(json, "subsonicToken");

        var userName = JsonRead.Str(json, "username");
        if (string.IsNullOrEmpty(userName)) userName = JsonRead.Str(json, "userName");
        if (string.IsNullOrEmpty(userName)) userName = JsonRead.Str(json, "name");

        return new AuthSession
        {
            ServerId = serverId,
            AccessToken = token,
            UserId = JsonRead.Str(json, "userId"),
            UserName = userName,
            Raw = JsonRead.DeepClone(json),
        };
    }

    /// <summary>Audiobookshelf：<c>POST /login</c> → <c>{user:{...}, userDefaultLibraryId, ...}</c>。</summary>
    public static AuthSession FromAbsJson(JsonElement json, string serverId)
    {
        var user = JsonRead.Prop(json, "user");
        var token = JsonRead.Str(json, "token");
        if (string.IsNullOrEmpty(token)) token = JsonRead.Str(user, "token");

        return new AuthSession
        {
            ServerId = serverId,
            AccessToken = token,
            UserId = JsonRead.Str(user, "id"),
            UserName = JsonRead.Str(user, "username"),
            Raw = JsonRead.DeepClone(json),
        };
    }
}

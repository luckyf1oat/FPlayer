// 等价移植：rebuild/ai_player/lib/core/services/trakt_service.dart（395 行 Dart → C#）。
// 端点依据 reversed/FlutterApp/SERVICE_API.md §5「Trakt（观看记录同步）」：
//   - 设备码授权 POST https://api.trakt.tv/oauth/device/code → device_code / user_code / verification_url（+ expires_in / interval）
//   - 换/刷/撤销令牌 POST /oauth/device/token、POST /oauth/token、POST /oauth/revoke
//   - 必需头 trakt-api-key: <client_id>、trakt-api-version: 2、Authorization: Bearer <token>、Content-Type: application/json
//   - 打卡 POST /scrobble/start|pause|stop；历史 POST /sync/history、POST /sync/history/remove
//   - 类型 TraktScrobbleMedia.{movie,episode,season,show}；OOB 回退 urn:ietf:wg:oauth:2.0:oob
//   - 凭据 `trakt_token_store` → 本工程 PasswordStore（DPAPI，见 TraktTokenStore.cs）
// 语义保持（与 Dart 逐条对齐）：
//   1. 失败一律「记日志 + 返回 null/false」，不向调用方抛异常（仅调用方主动取消时抛 OperationCanceledException）。
//   2. `/oauth/*` 端点**不带** trakt-api-key/trakt-api-version —— Dart 侧实证如此（postJson 未传 headers，
//      仅 device/code 与 device/token 的 client_id 走请求体）；§5 的「必需头」用于 scrobble/sync/users-me，本实现一致。
//   3. 内存中的 `Token` 与持久化**解耦**：Dart 的 `token` 字段同样只活在内存里，落盘由其调用方经 TraktTokenStore 完成。
//      外壳接线：启动时 `LoadToken()`；授权/刷新成功后 `SaveToken(token)`；撤销/登出后 `ClearToken()`。
// 未实证处（容错解析并标注）：刷新响应缺 refresh_token 时沿用旧值；scrobble 进度 NaN 归零；
//   Trakt 客户端凭据未注入时从 PasswordStore 的 `trakt.clientId`/`trakt.clientSecret` 读取（允许为空）。
// 客户端凭据**不写死在代码里**（构造参数注入，或由凭据库提供）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Trakt;

/// <summary>
/// 设备码授权第一步的响应（对应 Dart <c>TraktDeviceCode</c>，trakt_service.dart:17-40）。
/// 依据 SERVICE_API §5：<c>device_code</c>/<c>user_code</c>/<c>verification_url</c>（另有 <c>expires_in</c>/<c>interval</c>）。
/// </summary>
public sealed class TraktDeviceCode
{
    public TraktDeviceCode(
        string deviceCode = "",
        string userCode = "",
        string verificationUrl = "",
        int expiresIn = 600,
        int interval = 5)
    {
        DeviceCode = deviceCode ?? string.Empty;
        UserCode = userCode ?? string.Empty;
        VerificationUrl = verificationUrl ?? string.Empty;
        ExpiresIn = expiresIn;
        Interval = interval;
    }

    public string DeviceCode { get; set; }

    public string UserCode { get; set; }

    /// <summary>用户在浏览器打开的授权页（Trakt 也会给 <c>verification_url</c>；缺省时外壳可自行拼 <c>https://trakt.tv/activate</c>）。</summary>
    public string VerificationUrl { get; set; }

    /// <summary>设备码有效期（秒），Dart 缺省 600。</summary>
    public int ExpiresIn { get; set; }

    /// <summary>轮询建议间隔（秒），Dart 缺省 5。</summary>
    public int Interval { get; set; }

    public bool IsUsable => !string.IsNullOrEmpty(DeviceCode);

    /// <summary>容错反序列化（对应 Dart <c>TraktDeviceCode.fromJson</c>，trakt_service.dart:32-39）。</summary>
    public static TraktDeviceCode FromJson(JsonElement? json)
    {
        if (json == null) return new TraktDeviceCode();
        return new TraktDeviceCode(
            JsonRead.Str(json, "device_code"),
            JsonRead.Str(json, "user_code"),
            JsonRead.Str(json, "verification_url"),
            IntOf(json, "expires_in", 600),
            IntOf(json, "interval", 5));
    }

    /// <summary>64 位读取 + int 夹取（不整型截断；时间/时长类字段一律走 JsonRead.LongOrNull）。</summary>
    internal static int IntOf(JsonElement? json, string name, int def)
    {
        var value = JsonRead.LongOrNull(json, name);
        if (value == null) return def;
        if (value.Value > int.MaxValue) return int.MaxValue;
        if (value.Value < int.MinValue) return int.MinValue;
        return (int)value.Value;
    }
}

/// <summary>
/// 打卡/历史条目（对应 Dart <c>TraktScrobbleMedia</c>，trakt_service.dart:89-128）。
/// <see cref="Type"/> ∈ <c>movie</c> / <c>episode</c> / <c>season</c> / <c>show</c>（SERVICE_API §5）。
/// </summary>
public sealed class TraktScrobbleMedia
{
    public const string TypeMovie = "movie";

    public const string TypeEpisode = "episode";

    public const string TypeSeason = "season";

    public const string TypeShow = "show";

    public TraktScrobbleMedia(
        string type,
        string imdb = "",
        string tmdb = "",
        string tvdb = "",
        string title = "",
        int? year = null,
        int? season = null,
        int? episode = null)
    {
        Type = string.IsNullOrEmpty(type) ? TypeMovie : type;
        Imdb = imdb ?? string.Empty;
        Tmdb = tmdb ?? string.Empty;
        Tvdb = tvdb ?? string.Empty;
        Title = title ?? string.Empty;
        Year = year;
        Season = season;
        Episode = episode;
    }

    public string Type { get; set; }

    public string Imdb { get; set; }

    public string Tmdb { get; set; }

    public string Tvdb { get; set; }

    public string Title { get; set; }

    public int? Year { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public bool IsMovie => string.Equals(Type, TypeMovie, StringComparison.Ordinal);

    /// <summary>
    /// 外部 id 映射（对应 Dart <c>ids</c>，trakt_service.dart:111-115）：
    /// <c>imdb</c> 原样、<c>tmdb</c>/<c>tvdb</c> 能解析成整数就发数字（Trakt 接受两种形态）。
    /// </summary>
    public Dictionary<string, object> Ids
    {
        get
        {
            var ids = new Dictionary<string, object>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(Imdb)) ids["imdb"] = Imdb;
            if (!string.IsNullOrEmpty(Tmdb)) ids["tmdb"] = NumericOrText(Tmdb);
            if (!string.IsNullOrEmpty(Tvdb)) ids["tvdb"] = NumericOrText(Tvdb);
            return ids;
        }
    }

    /// <summary>等价 Dart <c>isUsable</c>：至少要有一个外部 id 才可打卡/写历史。</summary>
    public bool IsUsable => Ids.Count > 0;

    /// <summary>对应 Dart <c>toJson()</c>（trakt_service.dart:119-127）：<c>{ &lt;type&gt;: { title?, year?, ids, [season], [number] } }</c>。</summary>
    public Dictionary<string, object> ToJson()
    {
        var type = string.IsNullOrEmpty(Type) ? TypeMovie : Type;
        var body = new Dictionary<string, object>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(Title)) body["title"] = Title;
        if (Year.HasValue) body["year"] = Year.Value;
        body["ids"] = Ids;
        if (string.Equals(type, TypeEpisode, StringComparison.Ordinal))
        {
            // Dart 对 episode 类型**恒写** season/number 两键（值允许为 null），非 episode 类型不写 ⇒ 保持一致。
            body["season"] = Season;
            body["number"] = Episode;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal) { [type] = body };
    }

    /// <summary>新增便利（Dart 侧无工厂）：电影条目。</summary>
    public static TraktScrobbleMedia ForMovie(string imdb = "", string tmdb = "", string tvdb = "", string title = "", int? year = null)
        => new TraktScrobbleMedia(TypeMovie, imdb, tmdb, tvdb, title, year);

    /// <summary>
    /// 新增便利（Dart 侧无工厂）：剧集条目（Trakt 的 <c>episode</c> 需要 season + number）。
    /// 命名说明：不能叫 <c>Episode</c> —— 与同类的 <see cref="Episode"/> 属性同名会触发 CS0102（构建实测）。
    /// </summary>
    public static TraktScrobbleMedia ForEpisode(int? season, int? episode, string imdb = "", string tmdb = "", string tvdb = "", string title = "", int? year = null)
        => new TraktScrobbleMedia(TypeEpisode, imdb, tmdb, tvdb, title, year, season, episode);

    private static object NumericOrText(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object)value;
    }
}

/// <summary>
/// 观看历史条目（**新增**：Dart <c>trakt_service.dart</c> 无此模型；父任务要求提供「查询一类」能力，见
/// <see cref="TraktService.GetWatchedHistoryAsync"/> 的端点标注）。
/// </summary>
public sealed class TraktHistoryItem
{
    /// <summary>Trakt 历史记录 id（非媒体 id）。</summary>
    public long Id { get; set; }

    /// <summary><c>watch</c> / <c>scrobble</c> / <c>checkin</c>。</summary>
    public string Action { get; set; } = "watch";

    /// <summary><c>movie</c> / <c>episode</c> / <c>season</c> / <c>show</c>。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>观看时刻（UTC）；<c>watched_at</c> 缺失或无法解析时为 <c>null</c>。</summary>
    public DateTimeOffset? WatchedAt { get; set; }

    /// <summary>从条目内嵌对象解析出的媒体身份（可能因字段缺失而不可用，由 <see cref="TraktScrobbleMedia.IsUsable"/> 判定）。</summary>
    public TraktScrobbleMedia Media { get; set; }
}

/// <summary>
/// Trakt 客户端（对应 Dart <c>TraktService</c>，trakt_service.dart:161-395）。
/// 构造签名按父任务固定：<c>(ShellHttpClient, PasswordStore, clientId, clientSecret = null, onLog = null)</c>。
/// </summary>
public sealed class TraktService
{
    /// <summary>Dart <c>apiBase</c>（trakt_service.dart:174）。</summary>
    public const string ApiBase = "https://api.trakt.tv";

    /// <summary>Dart <c>oobRedirect</c>（trakt_service.dart:177，原版字符串实证 <c>[S]</c>）。</summary>
    public const string OobRedirect = "urn:ietf:wg:oauth:2.0:oob";

    /// <summary>客户端 id 在凭据库中的键（构造参数未提供时读取；允许为空）。</summary>
    public const string ClientIdKey = "trakt.clientId";

    /// <summary>客户端密钥在凭据库中的键。</summary>
    public const string ClientSecretKey = "trakt.clientSecret";

    /// <summary>Dart 轮询默认超时 5 分钟（trakt_service.dart:214）。</summary>
    public static readonly TimeSpan DefaultPollTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Dart 轮询单次请求超时 15 秒且不重试（trakt_service.dart:228-229）。</summary>
    public static readonly TimeSpan PollRequestTimeout = TimeSpan.FromSeconds(15);

    private readonly Action<string> _onLog;

    public TraktService(
        ShellHttpClient http,
        PasswordStore credentials,
        string clientId,
        string clientSecret = null,
        Action<string> onLog = null)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _onLog = onLog;
        TokenStore = new TraktTokenStore(credentials);

        // 凭据不硬编码：优先构造参数，未给（null/空白）时回落到凭据库；两者皆无则为空串（请求会由 Trakt 拒绝并记日志）。
        ClientId = string.IsNullOrWhiteSpace(clientId) ? (credentials.GetSecret(ClientIdKey) ?? string.Empty) : clientId.Trim();
        ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? credentials.GetSecret(ClientSecretKey) : clientSecret;
    }

    public ShellHttpClient Http { get; }

    /// <summary>凭据库（DPAPI）。</summary>
    public PasswordStore Credentials { get; }

    /// <summary>令牌持久化（对应原版 `trakt_token_store`）。</summary>
    public TraktTokenStore TokenStore { get; }

    /// <summary><c>trakt-api-key</c> 的取值（对应 Dart <c>clientId</c>）。</summary>
    public string ClientId { get; set; }

    /// <summary>OAuth 客户端密钥（对应 Dart <c>clientSecret</c>）。</summary>
    public string ClientSecret { get; set; }

    public Action<string> OnLog => _onLog;

    /// <summary>当前令牌（对应 Dart 可变字段 <c>token</c>，trakt_service.dart:179）。仅内存；持久化见 <see cref="LoadToken"/>/<see cref="SaveToken"/>/<see cref="ClearToken"/>。</summary>
    public TraktToken Token { get; set; }

    /// <summary>最近一次设备码轮询的失败原因（新增：便于外壳把「等待/慢下来/过期」直接展示给用户）。</summary>
    public string LastPollFailure { get; private set; }

    public bool HasToken => Token != null;

    private void Log(string message) => _onLog?.Invoke(message);

    /// <summary>API 绝对地址（对应 Dart <c>_uri</c>，trakt_service.dart:191）。</summary>
    public string ApiUri(string path) => ApiBase + path;

    /// <summary>
    /// 必需头（对应 Dart <c>_headers</c>，trakt_service.dart:183-189）：
    /// <c>trakt-api-key</c> + <c>trakt-api-version: 2</c> + <c>Content-Type</c>，有令牌则附 <c>Authorization: Bearer</c>。
    /// </summary>
    public Dictionary<string, string> ApiHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trakt-api-key"] = ClientId ?? string.Empty,
            ["trakt-api-version"] = "2",
            ["Content-Type"] = "application/json",
        };

        var token = Token;
        if (token != null && !string.IsNullOrEmpty(token.AccessToken))
        {
            headers["Authorization"] = "Bearer " + token.AccessToken;
        }

        return headers;
    }

    // ── 令牌持久化（外壳接线；Dart 侧由调用方经 trakt_token_store 完成）─────────

    /// <summary>从凭据库载入令牌到内存（外壳启动时调用一次）。无令牌返回 <c>null</c>。</summary>
    public TraktToken LoadToken()
    {
        var stored = TokenStore.Read();
        Token = stored;
        if (stored != null) Log("Trakt token 已从凭据库载入");
        return stored;
    }

    /// <summary>把令牌写入凭据库并同步到内存（授权/刷新成功后调用）。<paramref name="token"/> 为 <c>null</c> 时等价 <see cref="ClearToken"/>。</summary>
    public void SaveToken(TraktToken token)
    {
        if (token == null)
        {
            ClearToken();
            return;
        }

        Token = token;
        TokenStore.Write(token);
        Log("Trakt token 已写入凭据库");
    }

    /// <summary>清除内存与凭据库中的令牌（撤销授权/登出）。</summary>
    public void ClearToken()
    {
        Token = null;
        TokenStore.Clear();
    }

    // ── 设备码授权 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 设备码授权第一步（对应 Dart <c>requestDeviceCode()</c>，trakt_service.dart:193-206）：
    /// <c>POST /oauth/device/code</c>，体 <c>{client_id}</c>。失败记日志并返回 <c>null</c>。
    /// </summary>
    public async Task<TraktDeviceCode> StartDeviceAuthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["client_id"] = ClientId,
            };
            var result = await Http.PostJsonAsync(
                ApiUri("/oauth/device/code"),
                body,
                null,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            return TraktDeviceCode.FromJson(result.JsonMap);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt 设备码申请失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设备码授权第二步（对应 Dart <c>pollDeviceToken()</c>，trakt_service.dart:208-265）：
    /// 先等 <c>interval</c> 秒，再 <c>POST /oauth/device/token</c>（体 <c>{code, client_id, client_secret}</c>，单次 15s、不重试）。
    /// 状态码语义按 Dart 逐条处理：400 等待授权 / 404 设备码无效 / 409 已被使用 / 410 已过期 / 418 用户拒绝 / 429 间隔 +1 秒。
    /// 返回 <c>null</c> 表示未拿到令牌（终止原因见 <see cref="LastPollFailure"/> 与日志）；<paramref name="onPending"/> 用于 UI 提示「等待授权」。
    /// </summary>
    public async Task<TraktToken> PollDeviceTokenAsync(
        TraktDeviceCode code,
        Action onPending = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        LastPollFailure = null;

        if (code == null || !code.IsUsable)
        {
            LastPollFailure = "设备码为空";
            Log("Trakt 轮询跳过：设备码为空");
            return null;
        }

        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultPollTimeout);
        var intervalSeconds = Math.Clamp(code.Interval <= 0 ? 5 : code.Interval, 1, 30); // Dart 的 clamp(1, 30)
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

            try
            {
                var body = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["code"] = code.DeviceCode,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                };
                var result = await Http.PostJsonAsync(
                    ApiUri("/oauth/device/token"),
                    body,
                    null,
                    PollRequestTimeout,
                    0,
                    cancellationToken).ConfigureAwait(false);

                var json = result.JsonMap;
                if (HasNonNull(json, "access_token"))
                {
                    var token = TraktToken.FromJson(json);
                    Token = token;
                    return token;
                }
            }
            catch (ShellHttpException ex)
            {
                switch (ex.StatusCode)
                {
                    case 400:
                        LastPollFailure = "等待用户在浏览器完成授权";
                        onPending?.Invoke();
                        break;
                    case 404:
                        LastPollFailure = "设备码无效";
                        Log("Trakt 设备码无效");
                        return null;
                    case 409:
                        LastPollFailure = "设备码已被使用";
                        Log("Trakt 设备码已被使用");
                        return null;
                    case 410:
                        LastPollFailure = "设备码已过期";
                        Log("Trakt 设备码已过期");
                        return null;
                    case 418:
                        LastPollFailure = "用户拒绝授权";
                        Log("Trakt 用户拒绝授权");
                        return null;
                    case 429:
                        // Dart：interval = interval + 1 秒（Trakt 要求放慢轮询）
                        interval = TimeSpan.FromSeconds(interval.TotalSeconds + 1);
                        LastPollFailure = "轮询过快，已放慢（429）";
                        Log($"Trakt 轮询过快（429），间隔调整为 {interval.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} 秒");
                        break;
                    default:
                        LastPollFailure = $"轮询返回 {ex.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
                        Log($"Trakt 轮询返回 {ex.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "?"}");
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LastPollFailure = ex.Message;
                Log($"Trakt 轮询异常：{ex.Message}");
            }
        }

        LastPollFailure ??= "授权超时";
        Log("Trakt 授权超时");
        return null;
    }

    /// <summary>
    /// 刷新令牌（对应 Dart <c>refresh()</c>，trakt_service.dart:267-287）：<c>POST /oauth/token</c>，
    /// 体 <c>{refresh_token, client_id, client_secret, grant_type: refresh_token, redirect_uri: OOB}</c>。
    /// 未实证处的容错：响应缺 <c>refresh_token</c> 时沿用旧刷新令牌（否则后续刷新会因空令牌永久失效）。
    /// </summary>
    public async Task<TraktToken> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var current = Token;
        if (current == null || string.IsNullOrEmpty(current.RefreshToken)) return null;

        try
        {
            var body = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["refresh_token"] = current.RefreshToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["grant_type"] = "refresh_token",
                ["redirect_uri"] = OobRedirect,
            };
            var result = await Http.PostJsonAsync(
                ApiUri("/oauth/token"),
                body,
                null,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (!HasNonNull(json, "access_token")) return null;

            var token = TraktToken.FromJson(json);
            if (string.IsNullOrEmpty(token.RefreshToken)) token.RefreshToken = current.RefreshToken;
            if (token.CreatedAt <= 0) token.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Token = token;
            return token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt 刷新 token 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 撤销授权（对应 Dart <c>revoke()</c>，trakt_service.dart:289-305）：<c>POST /oauth/revoke</c>，
    /// 体 <c>{token, client_id, client_secret}</c>。成功仅清空**内存**令牌（与 Dart 一致）；
    /// 需要同时清凭据库时由外壳调用 <see cref="ClearToken"/>。
    /// </summary>
    public async Task<bool> RevokeTokenAsync(CancellationToken cancellationToken = default)
    {
        var current = Token;
        if (current == null) return false;

        try
        {
            var body = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["token"] = current.AccessToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
            };
            await Http.PostJsonAsync(
                ApiUri("/oauth/revoke"),
                body,
                null,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            Token = null;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt 撤销失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 确保令牌可用（对应 Dart <c>ensureToken()</c>，trakt_service.dart:307-313）：无令牌 ⇒ <c>false</c>；已过期 ⇒ 尝试刷新。
    /// 注：与 Dart 一致，此处**不**隐式从凭据库载入；外壳须先 <see cref="LoadToken"/>。
    /// </summary>
    public async Task<bool> EnsureTokenAsync(CancellationToken cancellationToken = default)
    {
        var current = Token;
        if (current == null) return false;
        if (!current.IsExpired) return true;
        return await RefreshTokenAsync(cancellationToken).ConfigureAwait(false) != null;
    }

    // ── 打卡 / 历史 ────────────────────────────────────────────────────────

    /// <summary>
    /// 打卡（对应 Dart <c>scrobble()</c>，trakt_service.dart:315-342）：<c>POST /scrobble/{action}</c>，
    /// 体为媒体 JSON + <c>progress</c>（0–100，Dart 侧由调用方把秒换算成百分比）。
    /// </summary>
    public async Task<bool> ScrobbleAsync(
        string action,
        TraktScrobbleMedia media,
        double progressPercent,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureTokenAsync(cancellationToken).ConfigureAwait(false)) return false;
        if (media == null || !media.IsUsable)
        {
            Log("Trakt 打卡跳过：缺少外部 id（imdb/tmdb/tvdb）");
            return false;
        }

        try
        {
            var body = media.ToJson();
            body["progress"] = ClampPercent(progressPercent);
            await Http.PostJsonAsync(
                ApiUri("/scrobble/" + (action ?? string.Empty)),
                body,
                ApiHeaders(),
                null,
                null,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt scrobble/{action} 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 加入观看历史（**P1 零调用项**，对应 Dart <c>addToHistory()</c>，trakt_service.dart:344-346）：
    /// <c>POST /sync/history</c>。
    /// </summary>
    public Task<bool> AddToHistoryAsync(TraktScrobbleMedia media, CancellationToken cancellationToken = default)
        => HistoryAsync("/sync/history", media, false, cancellationToken);

    /// <summary>
    /// 从观看历史移除（**P1 零调用项**，对应 Dart <c>removeFromHistory()</c>，trakt_service.dart:348-350）：
    /// <c>POST /sync/history/remove</c>。
    /// </summary>
    public Task<bool> RemoveFromHistoryAsync(TraktScrobbleMedia media, CancellationToken cancellationToken = default)
        => HistoryAsync("/sync/history/remove", media, true, cancellationToken);

    /// <summary>
    /// 历史写入/移除共用实现（对应 Dart <c>_history()</c>，trakt_service.dart:352-372）：
    /// 移除时体为 <c>{ids}</c>；写入时电影进 <c>movies</c>、其余类型进 <c>episodes</c>。
    /// 注（未实证）：Dart 的移除体是**扁平** <c>{ids}</c>，而 Trakt 官方文档的 <c>/sync/history/remove</c> 期望
    /// <c>{movies|shows|episodes: [{ids}]}</c>；按「逐行翻译、不自行发明行为」保持 Dart 原样，是否被 Trakt 接受待实测。
    /// </summary>
    private async Task<bool> HistoryAsync(
        string path,
        TraktScrobbleMedia media,
        bool remove,
        CancellationToken cancellationToken)
    {
        if (!await EnsureTokenAsync(cancellationToken).ConfigureAwait(false)) return false;
        if (media == null || !media.IsUsable) return false;

        try
        {
            Dictionary<string, object> body;
            if (remove)
            {
                body = new Dictionary<string, object>(StringComparer.Ordinal) { ["ids"] = media.Ids };
            }
            else
            {
                var item = media.ToJson();
                body = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [media.IsMovie ? "movies" : "episodes"] = new List<object> { item },
                };
            }

            await Http.PostJsonAsync(
                ApiUri(path),
                body,
                ApiHeaders(),
                null,
                null,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt {path} 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 校验令牌（对应 Dart <c>testToken()</c>，trakt_service.dart:374-384）：<c>GET /users/me</c>。
    /// 这是 Dart 侧唯一的「查询」调用。
    /// </summary>
    public async Task<bool> TestTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureTokenAsync(cancellationToken).ConfigureAwait(false)) return false;

        try
        {
            await Http.GetAsync(
                ApiUri("/users/me"),
                ApiHeaders(),
                null,
                null,
                null,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt token 校验失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 观看历史查询（**新增**：Dart <c>trakt_service.dart</c> **无**此方法，SERVICE_API §5 也只列了
    /// <c>/sync/history</c> 与 <c>/sync/history/remove</c> 两个 POST；本方法按父任务「提供查询一类能力」实现，
    /// 走 Trakt v2 公开路由 <c>GET /sync/history/{movies|shows|seasons|episodes}/{id}</c>（<c>id</c> 优先取 imdb）。
    /// 未实证处：Trakt 路由对 <c>imdb</c> 形如 <c>ttXXXX</c> 可直接作路径 id；仅有 tmdb/tvdb 数字时可能 404
    /// ⇒ 失败一律返回空列表并记日志（不抛异常）。
    /// </summary>
    public async Task<List<TraktHistoryItem>> GetWatchedHistoryAsync(
        TraktScrobbleMedia media,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var items = new List<TraktHistoryItem>();
        if (!await EnsureTokenAsync(cancellationToken).ConfigureAwait(false)) return items;

        if (media == null || !media.IsUsable)
        {
            Log("Trakt 历史查询跳过：缺少外部 id（imdb/tmdb/tvdb）");
            return items;
        }

        var segment = HistorySegment(media.Type);
        if (segment == null)
        {
            Log($"Trakt 历史查询跳过：未知类型 {media.Type}");
            return items;
        }

        var externalId = !string.IsNullOrEmpty(media.Imdb)
            ? media.Imdb
            : (!string.IsNullOrEmpty(media.Tmdb) ? media.Tmdb : media.Tvdb);

        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["limit"] = (limit <= 0 ? 10 : limit).ToString(CultureInfo.InvariantCulture),
            ["page"] = "1",
        };

        try
        {
            var result = await Http.GetAsync(
                ApiUri($"/sync/history/{segment}/{Uri.EscapeDataString(externalId)}"),
                ApiHeaders(),
                query,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            var list = result.JsonList;
            if (list == null) return items;

            foreach (var entry in list.Value.EnumerateArray())
            {
                var parsed = ParseHistoryItem(entry);
                if (parsed != null) items.Add(parsed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Trakt /sync/history 查询失败：{ex.Message}");
        }

        return items;
    }

    /// <summary>
    /// 由内核回调驱动的打卡动作判定（对应 Dart <c>scrobbleActionFor</c>，trakt_service.dart:386-394）：
    /// Trakt 只有 <c>start</c>/<c>pause</c>/<c>stop</c> 三个动作。
    /// </summary>
    public static string ScrobbleActionFor(bool isPaused, bool isFinished)
    {
        if (isFinished) return "stop";
        if (isPaused) return "pause";
        return "start";
    }

    /// <summary>供 <see cref="TraktScrobbler"/> 直接使用的发送函数（等价 Dart 传 <c>TraktService.scrobble</c> 的做法）。</summary>
    public TraktScrobbleSender AsScrobbleSender()
        => (action, media, progressPercent) => ScrobbleAsync(action, media, progressPercent);

    // ── 内部 ───────────────────────────────────────────────────────────────

    /// <summary>Trakt 历史路由的类型段（单数 → 复数）。</summary>
    private static string HistorySegment(string type)
    {
        switch ((type ?? string.Empty).ToLowerInvariant())
        {
            case TraktScrobbleMedia.TypeMovie:
                return "movies";
            case TraktScrobbleMedia.TypeShow:
                return "shows";
            case TraktScrobbleMedia.TypeSeason:
                return "seasons";
            case TraktScrobbleMedia.TypeEpisode:
                return "episodes";
            default:
                return null;
        }
    }

    /// <summary>容错解析单条历史（字段缺失/类型漂移一律取缺省，不抛异常）。</summary>
    private static TraktHistoryItem ParseHistoryItem(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;

        var type = JsonRead.Str(entry, "type");
        if (type.Length == 0) type = TraktScrobbleMedia.TypeMovie;

        var node = JsonRead.Prop(entry, type);
        var ids = JsonRead.Prop(node, "ids");

        var media = new TraktScrobbleMedia(
            type,
            JsonRead.Str(ids, "imdb"),
            JsonRead.Str(ids, "tmdb"),
            JsonRead.Str(ids, "tvdb"),
            JsonRead.Str(node, "title"),
            IntOrNull(node, "year"),
            IntOrNull(node, "season"),
            IntOrNull(node, "number"));

        DateTimeOffset? watchedAt = null;
        var rawWatchedAt = JsonRead.Str(entry, "watched_at");
        if (rawWatchedAt.Length > 0 &&
            DateTimeOffset.TryParse(rawWatchedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            watchedAt = parsed;
        }

        var action = JsonRead.Str(entry, "action");
        if (action.Length == 0) action = "watch";

        return new TraktHistoryItem
        {
            Id = JsonRead.LongOrNull(entry, "id") ?? 0,
            Action = action,
            Type = type,
            WatchedAt = watchedAt,
            Media = media,
        };
    }

    private static int? IntOrNull(JsonElement? json, string name)
    {
        var value = JsonRead.LongOrNull(json, name);
        if (value == null) return null;
        if (value.Value > int.MaxValue) return int.MaxValue;
        if (value.Value < int.MinValue) return int.MinValue;
        return (int)value.Value;
    }

    /// <summary>进度百分比夹取（Dart <c>progressPercent.clamp(0, 100)</c>；NaN 归 0 —— JSON 不允许 NaN，否则序列化抛异常）。</summary>
    private static double ClampPercent(double percent)
    {
        if (double.IsNaN(percent)) return 0;
        return Math.Clamp(percent, 0, 100);
    }

    /// <summary>Dart 的 <c>json['x'] != null</c>：属性存在且不是 JSON <c>null</c>。</summary>
    private static bool HasNonNull(JsonElement? json, string name)
    {
        var prop = JsonRead.Prop(json, name);
        return prop.HasValue && prop.Value.ValueKind != JsonValueKind.Null;
    }
}

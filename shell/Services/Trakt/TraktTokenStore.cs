// 等价移植：rebuild/ai_player/lib/core/services/trakt_service.dart（其中的 `TraktToken` + `TraktTokenStore` 两段）。
// 端点依据：无（纯本地持久化，本文件不发起任何 HTTP）。存储机制依据 reversed/FlutterApp/SERVICE_API.md §5「凭据」行
//   —— 原版 `trakt_token_store` → `flutter_secure_storage(_windows)`（Windows 凭据管理器）；
//   本工程对应 shell/Services/Credentials/PasswordStore.cs（DPAPI / CryptProtectData，CurrentUser 作用域）。
// 与 Dart 的差异（逐条标注，未实证处一律容错解析）：
//   1. Dart 用**单键 JSON** 落盘（键 `trakt_token`）。本实现按父任务要求拆成 `trakt.*` 分键（便于外壳分别回填/清理），
//      同时保留对 Dart 单键 JSON 的**读取兼容**（`Read()` 回退 + `MigrateLegacy()` 迁移），保证从原版升级不丢授权。
//   2. Dart `TraktToken.toJson()` 把**绝对**过期戳写进 `expires_in` 字段，而 `fromJson` 又做 `created_at + expires_in`
//      ⇒ 每轮「读→写」都把过期时间再推后一个 `created_at`（原版缺陷，实测语义如此）。本实现改为同时写
//      `expires_at`（绝对，新增字段）与 `expires_in`（剩余秒），读取时**优先 `expires_at`**；
//      读到「`expires_in` 明显是纪元秒」的旧数据时按绝对值解释（见 TraktToken.FromJson）。
//   3. 时间戳一律 64 位（`long`）：JSON 路径走 JsonRead.LongOrNull，分键路径走 InvariantCulture long.TryParse，
//      不用 int，避免 2038 截断与脏数据溢出。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Trakt;

/// <summary>
/// Trakt OAuth 令牌（对应 Dart <c>TraktToken</c>，见 trakt_service.dart:42-86）。
/// 字段与 Dart 一一对应；<see cref="ExpiresAt"/>/<see cref="CreatedAt"/> 为 **Unix 秒（UTC）**。
/// </summary>
public sealed class TraktToken
{
    /// <summary>Dart 默认 <c>scope</c>（trakt_service.dart:48）。</summary>
    public const string DefaultScope = "public";

    /// <summary>Dart 默认 <c>token_type</c>（trakt_service.dart:49）。</summary>
    public const string DefaultTokenType = "bearer";

    /// <summary>2001-09-09 前后的纪元秒阈值：`expires_in` 大于它就说明该字段装的是绝对戳而非剩余秒。</summary>
    private const long AbsoluteEpochThreshold = 1000000000L;

    /// <summary>DateTimeOffset.FromUnixTimeSeconds 的合法区间（越界会抛异常，脏数据必须挡在这里）。</summary>
    private const long MinUnixSeconds = -62135596800L;

    private const long MaxUnixSeconds = 253402300799L;

    public TraktToken(
        string accessToken,
        string refreshToken,
        long expiresAt,
        long createdAt = 0,
        string scope = DefaultScope,
        string tokenType = DefaultTokenType)
    {
        AccessToken = accessToken ?? string.Empty;
        RefreshToken = refreshToken ?? string.Empty;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        Scope = string.IsNullOrEmpty(scope) ? DefaultScope : scope;
        TokenType = string.IsNullOrEmpty(tokenType) ? DefaultTokenType : tokenType;
    }

    public string AccessToken { get; set; }

    public string RefreshToken { get; set; }

    /// <summary>绝对过期时刻，Unix 秒（UTC）。Dart 名 <c>expiresAt</c>。</summary>
    public long ExpiresAt { get; set; }

    /// <summary>签发时刻，Unix 秒（UTC）。</summary>
    public long CreatedAt { get; set; }

    public string Scope { get; set; }

    public string TokenType { get; set; }

    public bool HasAccessToken => !string.IsNullOrEmpty(AccessToken);

    /// <summary>
    /// 等价 Dart <c>isExpired</c>（trakt_service.dart:61-62）：
    /// <c>expiresAt &gt; 0 &amp;&amp; DateTime.now().millisecondsSinceEpoch ~/ 1000 &gt;= expiresAt</c>。
    /// 两侧都是「纪元秒」，本地 now 与 UtcNow 的纪元秒相同 ⇒ 与 Dart 一致。
    /// </summary>
    public bool IsExpired => ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;

    /// <summary>过期时刻的 UTC 表达（<see cref="ExpiresAt"/> ≤ 0 或越界时返回 <c>null</c>）。新增：便于外壳展示。</summary>
    public DateTimeOffset? ExpiresAtUtc
    {
        get
        {
            if (ExpiresAt <= 0 || ExpiresAt < MinUnixSeconds || ExpiresAt > MaxUnixSeconds) return null;
            return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt);
        }
    }

    /// <summary>剩余有效秒数（已过期或未知过期返回 0）。新增：便于外壳展示。</summary>
    public long RemainingSeconds
    {
        get
        {
            if (ExpiresAt <= 0) return 0;
            var remaining = ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return remaining < 0 ? 0 : remaining;
        }
    }

    /// <summary>是否在 <paramref name="window"/> 内即将过期（含已过期）。新增：便于外壳提前刷新。</summary>
    public bool ExpiresWithin(TimeSpan window)
    {
        if (ExpiresAt <= 0) return false;
        var seconds = (long)Math.Ceiling(window.TotalSeconds);
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds >= ExpiresAt;
    }

    /// <summary>
    /// 序列化（对应 Dart <c>toJson</c>，trakt_service.dart:64-71，但修正了 expires_in 语义 —— 见文件头差异 2）。
    /// 额外字段 <c>expires_at</c> 只被本实现读取；Dart 读本结构时仍按 <c>created_at + expires_in</c> 得到同一绝对时刻。
    /// </summary>
    public Dictionary<string, object> ToJson()
    {
        var ttl = ExpiresAt > CreatedAt ? ExpiresAt - CreatedAt : 0;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["access_token"] = AccessToken,
            ["refresh_token"] = RefreshToken,
            ["created_at"] = CreatedAt,
            ["expires_at"] = ExpiresAt,
            ["expires_in"] = ttl,
            ["scope"] = Scope,
            ["token_type"] = TokenType,
        };
    }

    /// <summary>
    /// 容错反序列化（对应 Dart <c>TraktToken.fromJson</c>，trakt_service.dart:73-85）。
    /// 缺字段不抛异常：字符串缺省 <c>""</c>、<c>created_at</c> 缺省取当前时间、<c>expires_in</c> 缺省 0。
    /// </summary>
    public static TraktToken FromJson(JsonElement? json)
    {
        if (json == null) return null;

        var createdAt = JsonRead.LongOrNull(json, "created_at") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var accessToken = JsonRead.Str(json, "access_token");
        var refreshToken = JsonRead.Str(json, "refresh_token");

        long expiresAt;
        var absolute = JsonRead.LongOrNull(json, "expires_at");
        if (absolute.HasValue)
        {
            expiresAt = absolute.Value;
        }
        else
        {
            var expiresIn = JsonRead.LongOrNull(json, "expires_in") ?? 0;
            // 容错（文件头差异 2）：旧数据里 expires_in 可能装的是绝对纪元秒（Dart toJson 的写法）。
            // 阈值 1e9 ≈ 2001-09-09；正常剩余秒（Trakt 为 7,776,000 ≈ 90 天）远小于它。
            expiresAt = expiresIn >= AbsoluteEpochThreshold ? expiresIn : createdAt + expiresIn;
        }

        var scope = JsonRead.Str(json, "scope");
        if (scope.Length == 0) scope = DefaultScope;

        var tokenType = JsonRead.Str(json, "token_type");
        if (tokenType.Length == 0) tokenType = DefaultTokenType;

        return new TraktToken(accessToken, refreshToken, expiresAt, createdAt, scope, tokenType);
    }

    /// <summary>诊断串（**不打印任何令牌内容**）。</summary>
    public override string ToString() =>
        $"TraktToken(expiresAt={ExpiresAt.ToString(CultureInfo.InvariantCulture)}, expired={IsExpired})";
}

/// <summary>
/// Trakt 令牌存取（对应 Dart <c>TraktTokenStore</c>，trakt_service.dart:130-159；原版模块名 `trakt_token_store`）。
/// 底层为 <see cref="PasswordStore"/>（DPAPI），**不自建加密、不落明文**。
/// </summary>
public sealed class TraktTokenStore
{
    /// <summary>Dart 默认单键（trakt_service.dart:134）；仅用于**读取兼容**与迁移。</summary>
    public const string LegacyKey = "trakt_token";

    /// <summary>访问令牌键（父任务指定）。</summary>
    public const string AccessTokenKey = "trakt.accessToken";

    /// <summary>刷新令牌键（父任务指定）。</summary>
    public const string RefreshTokenKey = "trakt.refreshToken";

    /// <summary>绝对过期时刻（Unix 秒，UTC）键（父任务指定）。</summary>
    public const string ExpiresAtKey = "trakt.expiresAt";

    /// <summary>签发时刻（Unix 秒，UTC）键。</summary>
    public const string CreatedAtKey = "trakt.createdAt";

    /// <summary>OAuth scope 键（Trakt 实际恒为 <c>public</c>，仍落盘以求无损往返）。</summary>
    public const string ScopeKey = "trakt.scope";

    /// <summary>令牌类型键（Trakt 实际恒为 <c>bearer</c>）。</summary>
    public const string TokenTypeKey = "trakt.tokenType";

    private readonly PasswordStore _credentials;

    public TraktTokenStore(PasswordStore credentials, string legacyKey = LegacyKey)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        LegacyKeyName = string.IsNullOrEmpty(legacyKey) ? LegacyKey : legacyKey;
    }

    /// <summary>凭据库（DPAPI 存储）。</summary>
    public PasswordStore Credentials => _credentials;

    /// <summary>Dart 单键名（默认 <c>trakt_token</c>）。</summary>
    public string LegacyKeyName { get; }

    /// <summary>对应 Dart <c>TraktTokenStore.open()</c> 里的 <c>await store.ensureLoaded()</c>（C# 侧为同步）。</summary>
    public void EnsureLoaded() => _credentials.Vault.EnsureLoaded();

    /// <summary>
    /// 是否已存有可用令牌（等价 Dart <c>read() != null</c>）。
    /// </summary>
    public bool HasToken => Read() != null;

    /// <summary>
    /// 读取令牌（对应 Dart <c>read()</c>，trakt_service.dart:145-153）。
    /// 先读分键；无分键则回退读 Dart 单键 JSON。脏数据一律视为「无令牌」（Dart 侧 catch(_) ⇒ null），不抛异常。
    /// </summary>
    public TraktToken Read()
    {
        EnsureLoaded();

        var accessToken = Secret(AccessTokenKey);
        if (!string.IsNullOrEmpty(accessToken))
        {
            return new TraktToken(
                accessToken,
                Secret(RefreshTokenKey) ?? string.Empty,
                LongSecret(ExpiresAtKey, 0),
                LongSecret(CreatedAtKey, 0),
                Secret(ScopeKey),
                Secret(TokenTypeKey));
        }

        // 分键为空但 Dart 单键存在 ⇒ 兼容读取（空访问令牌等同于「无令牌」，与 Dart 的 trim().isEmpty ⇒ null 同义）。
        return ReadLegacy();
    }

    /// <summary>写入令牌（对应 Dart <c>write()</c>，trakt_service.dart:155-156）；<paramref name="token"/> 为 <c>null</c> 时等价 <see cref="Clear"/>。</summary>
    public void Write(TraktToken token)
    {
        if (token == null)
        {
            Clear();
            return;
        }

        EnsureLoaded();
        _credentials.SetSecret(AccessTokenKey, token.AccessToken ?? string.Empty);
        _credentials.SetSecret(RefreshTokenKey, token.RefreshToken ?? string.Empty);
        _credentials.SetSecret(ExpiresAtKey, token.ExpiresAt.ToString(CultureInfo.InvariantCulture));
        _credentials.SetSecret(CreatedAtKey, token.CreatedAt.ToString(CultureInfo.InvariantCulture));
        _credentials.SetSecret(ScopeKey, string.IsNullOrEmpty(token.Scope) ? TraktToken.DefaultScope : token.Scope);
        _credentials.SetSecret(TokenTypeKey, string.IsNullOrEmpty(token.TokenType) ? TraktToken.DefaultTokenType : token.TokenType);
    }

    /// <summary>
    /// 清除令牌（对应 Dart <c>clear()</c>，trakt_service.dart:158）。
    /// 比 Dart 多清 Dart 单键：否则 <see cref="Read"/> 的分键回退会「清了又读回来」。
    /// </summary>
    public void Clear()
    {
        EnsureLoaded();
        _credentials.RemoveSecret(AccessTokenKey);
        _credentials.RemoveSecret(RefreshTokenKey);
        _credentials.RemoveSecret(ExpiresAtKey);
        _credentials.RemoveSecret(CreatedAtKey);
        _credentials.RemoveSecret(ScopeKey);
        _credentials.RemoveSecret(TokenTypeKey);
        _credentials.RemoveSecret(LegacyKeyName);
    }

    /// <summary>
    /// 新增（原版升级路径，Dart 无对应方法）：把 Dart 时代的单键 JSON 迁到分键并删除旧键。
    /// 无旧数据时返回 <c>null</c>；已有分键时直接返回 <see cref="Read"/>（不覆盖）。
    /// </summary>
    public TraktToken MigrateLegacy()
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(Secret(AccessTokenKey))) return Read();

        var legacy = ReadLegacy();
        if (legacy == null) return null;

        Write(legacy);
        _credentials.RemoveSecret(LegacyKeyName);
        return legacy;
    }

    // ── 内部 ───────────────────────────────────────────────────────────────

    /// <summary>Dart 单键 JSON → 令牌（对应 dart:convert + TraktToken.fromJson）。</summary>
    private TraktToken ReadLegacy()
    {
        var raw = _credentials.GetSecret(LegacyKeyName);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var json = JsonRead.FromNode(raw);
        // 对应 Dart 的 `jsonDecode(raw) as Map<String, dynamic>` + catch(_) ⇒ null：非对象/非法 JSON 一律当无令牌。
        if (json == null || json.Value.ValueKind != JsonValueKind.Object) return null;
        return TraktToken.FromJson(json);
    }

    private string Secret(string key)
    {
        var raw = _credentials.GetSecret(key);
        return raw == null ? null : raw.Trim();
    }

    /// <summary>分键里的 64 位整数（时间戳）。不用 int，避免截断。</summary>
    private long LongSecret(string key, long def)
    {
        var raw = Secret(key);
        if (string.IsNullOrEmpty(raw)) return def;
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : def;
    }
}

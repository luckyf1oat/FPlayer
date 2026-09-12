// 等价移植：rebuild/ai_player/lib/core/services/navidrome_service.dart（360 行 Dart → C#）。
// 端点、参数、鉴权依据 reversed/FlutterApp/SERVICE_API.md §2「Navidrome / Subsonic」（[S]/[E]/[H] 实证）：
//   - 兼容层：Subsonic REST `/rest/{method}`，`/rest/ping` 探活；响应包裹体 `subsonic-response`
//   - 鉴权：**token+salt**（`t = md5(password + s)`，十六进制小写；`s` 为随机盐）—— 字符串同时含 `token` 与 `salt`
//   - 原生登录：`POST /auth/login`（Navidrome 0.5x+），头 `X-ND-Authorization`；token 写回 `ServerConfig.AccessToken`
//   - 方法名实证 [S]：getAlbumList2 / getStarred2 / getPlaylists / getPlaylist / getCoverArt /
//     getLyricsBySongId / search3 / star（unstar）/ scrobble / stream
//   - 参数实证 [S]：u= / token / salt / f=json / v=1.16.x / c=
//   - 播放：`/rest/stream?id=…` 直连（纯音频，无转码协商），URL 直接喂内核 `--open=`
// 容错策略：未在 SERVICE_API.md §2 逐字段/逐方法实证的部分（getArtists / getArtist / getRandomSongs /
//   ping 之外的普通方法、scrobble 的 time、stream 的 maxBitRate/format/timeOffset）一律容错解析（不抛异常），
//   与原 Dart 的宽松取值语义一致。
// 语义保持：上报/收藏/歌词类失败**只记日志、不抛异常**（返回 false / null），与 Dart 侧完全一致。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Navidrome;

/// <summary>Navidrome 原生登录结果（对应 Dart <c>NavidromeAuth</c>）。</summary>
public sealed class NavidromeAuth
{
    /// <summary><c>POST /auth/login</c> 返回的 <c>token</c>（Navidrome 原生 token，非 Subsonic salt 哈希）。</summary>
    public string Token { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}

/// <summary>Navidrome / Subsonic 客户端（对应 Dart <c>NavidromeService</c>）。</summary>
public sealed class NavidromeService
{
    /// <summary>Subsonic 协议版本（`v=` 参数；字符串实证含 `v=1.16.x`）。</summary>
    public const string DefaultApiVersion = "1.16.1";

    /// <summary>随机盐长度（字符数；等价 Dart 的 <c>List.generate(12, …)</c>）。</summary>
    private const int SaltLength = 12;

    private string _salt;
    private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

    public NavidromeService(
        ShellHttpClient http,
        ServerConfig server,
        string clientName = "AI Player",
        string clientVersion = "1.0.0",
        Action<string> onLog = null,
        string apiVersion = DefaultApiVersion)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        ClientName = string.IsNullOrEmpty(clientName) ? "AI Player" : clientName;
        ClientVersion = clientVersion ?? "1.0.0";
        OnLog = onLog;
        ApiVersion = string.IsNullOrEmpty(apiVersion) ? DefaultApiVersion : apiVersion;
    }

    public ShellHttpClient Http { get; }

    /// <summary>服务配置；登录成功后由调用方把 <c>AccessToken</c> 落盘（等价 Dart 侧写回 ServerConfig）。</summary>
    public ServerConfig Server { get; set; }

    /// <summary>客户端名（`c=` 参数）。</summary>
    public string ClientName { get; }

    public string ClientVersion { get; }

    public string ApiVersion { get; }

    public Action<string> OnLog { get; }

    public string BaseUrl => TextUtils.NormalizeBaseUrl(Server.BaseUrl);

    /// <summary>密码来自 <c>ServerConfig.Password</c>（即 <c>extra["password"]</c>）；无则空串。</summary>
    public string Password => Server.Password ?? string.Empty;

    private void Log(string message) => OnLog?.Invoke(message);

    /// <summary>
    /// 每次会话随机盐（Subsonic 规范要求盐值不可复用）。
    /// 等价 Dart <c>_salt ??= 12 位 base36 随机串</c>：会话内缓存一次（Dart 侧 <c>_salt</c> 同样只生成一次）。
    /// </summary>
    public string Salt
    {
        get
        {
            if (_salt == null) _salt = NewSalt();
            return _salt;
        }
    }

    /// <summary>`t = md5(密码 + 盐)` —— 十六进制小写（Subsonic token+salt 校验）。</summary>
    public string Token => Md5Hex(Password + Salt);

    /// <summary>鉴权与协议参数（等价 Dart <c>_authParams</c>）：<c>u/t/s/v/c/f</c>。</summary>
    public Dictionary<string, string> AuthParams => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["u"] = Server.UserName ?? string.Empty,
        ["t"] = Token,
        ["s"] = Salt,
        ["v"] = ApiVersion,
        ["c"] = ClientName,
        ["f"] = "json",
    };

    /// <summary>重置盐（下一请求换新盐；Subsonic 规范建议每次请求新盐）。</summary>
    public void ResetSalt() => _salt = null;

    /// <summary>随机 12 位 base36 盐（不改全局随机源；用加密级随机数）。</summary>
    private string NewSalt()
    {
        var bytes = new byte[SaltLength];
        _random.GetBytes(bytes);
        var sb = new StringBuilder(SaltLength);
        for (var i = 0; i < SaltLength; i++)
        {
            sb.Append(Base36Alphabet[bytes[i] % 36]);
        }
        return sb.ToString();
    }

    private const string Base36Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>等价 Dart <c>Md5.hex</c>：UTF-8 编码 → MD5 → 十六进制**小写**。</summary>
    public static string Md5Hex(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>原生登录（<c>POST /auth/login</c>，Navidrome 0.5x+；头 <c>X-ND-Authorization</c> 用于后续原生 API）。</summary>
    public static async Task<NavidromeAuth> LoginNativeAsync(
        ShellHttpClient http,
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (http == null) throw new ArgumentNullException(nameof(http));

        var result = await http.PostJsonAsync(
            TextUtils.BuildUri(TextUtils.NormalizeBaseUrl(baseUrl), "/auth/login"),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["username"] = username ?? string.Empty,
                ["password"] = password ?? string.Empty,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = result.JsonMap;
        return new NavidromeAuth
        {
            Token = JsonRead.Str(json, "token"),
            UserId = JsonRead.Str(json, "id"),
            // 等价 Dart：json['name'] ?? username
            UserName = NonEmpty(JsonRead.Str(json, "name"), username ?? string.Empty),
            // 等价 Dart `json['isAdmin'] as bool? ?? false`（字符串 "true" 也容错接受，未实证，容错解析）
            IsAdmin = JsonRead.Bool(json, "isAdmin"),
        };
    }

    /// <summary>Navidrome 原生 API 的鉴权头（<c>X-ND-Authorization</c>），token 取 <c>ServerConfig.AccessToken</c>。</summary>
    public Dictionary<string, string> NativeHeaders
    {
        get
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            var token = Server.AccessToken;
            if (!string.IsNullOrEmpty(token)) headers["X-ND-Authorization"] = "Bearer " + token;
            return headers;
        }
    }

    /// <summary>拼 <c>/rest/{method}</c> 完整 URL（含鉴权参数，query 覆盖同名键）。</summary>
    public string Api(string method, IDictionary<string, string> query = null)
    {
        var all = AuthParams;
        if (query != null)
        {
            foreach (var kv in query) all[kv.Key] = kv.Value;
        }
        return TextUtils.BuildUri(BaseUrl, "/rest/" + (method ?? string.Empty), all);
    }

    /// <summary>
    /// 统一调用：GET <c>/rest/{method}</c>，拆 <c>subsonic-response</c> 包裹体，
    /// <c>status == "failed"</c> 时抛 <see cref="ShellHttpException"/>（等价 Dart <c>call</c>）。
    /// </summary>
    public async Task<JsonElement?> CallAsync(
        string method,
        IDictionary<string, string> query = null,
        CancellationToken cancellationToken = default)
    {
        var url = Api(method, query);
        var result = await Http.GetAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        var body = JsonRead.Prop(result.JsonMap, "subsonic-response");
        if (body == null || body.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ShellHttpException($"{method} 响应格式异常", null, result.RequestUrl);
        }

        var map = body.Value;
        if (JsonRead.Str(map, "status") == "failed")
        {
            var error = JsonRead.Prop(map, "error");
            // 等价 Dart：error.message ?? map['error'] ?? '未知错误'（map['error'] 可能是对象 ⇒ 原样序列化）
            var message = JsonRead.Str(error, "message");
            if (message.Length == 0)
            {
                var rawError = JsonRead.Prop(map, "error");
                message = rawError.HasValue ? rawError.Value.GetRawText() : string.Empty;
            }
            if (message.Length == 0) message = "未知错误";

            throw new ShellHttpException(
                $"{method} 失败：{message}",
                ToInt(JsonRead.LongOrNull(error, "code")),
                result.RequestUrl);
        }
        return map;
    }

    /// <summary>探活（<c>/rest/ping</c>）；失败只记日志并返回 <c>false</c>（等价 Dart <c>ping()</c>）。</summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CallAsync("ping", null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Navidrome 连接失败：{ex}");
            return false;
        }
    }

    // ── 浏览 ───────────────────────────────────────────────────────────────

    /// <summary>等价 Dart <c>_listOf(body, container, key)</c>：取 <c>body[container][key]</c> 的对象数组。</summary>
    private static List<JsonElement> ListOf(JsonElement? body, string container, string key)
        => JsonRead.Objects(JsonRead.Items(JsonRead.Prop(body, container), key)).ToList();

    /// <summary>所有艺术家（<c>getArtists</c>，索引分组结构：<c>artists.index[].artist[]</c>）。</summary>
    public async Task<List<SubsonicArtist>> GetArtistsAsync(CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getArtists", null, cancellationToken).ConfigureAwait(false);
        var outList = new List<SubsonicArtist>();
        var index = JsonRead.Prop(body, "artists");
        if (index == null || index.Value.ValueKind != JsonValueKind.Object)
        {
            return outList;
        }
        foreach (var group in JsonRead.Objects(JsonRead.Items(index, "index")))
        {
            foreach (var artist in JsonRead.Objects(JsonRead.Items(group, "artist")))
            {
                outList.Add(SubsonicArtist.FromJson(artist));
            }
        }
        return outList;
    }

    /// <summary>某艺术家的专辑（<c>getArtist</c> → <c>artist.album[]</c>）。</summary>
    public async Task<List<SubsonicAlbum>> GetArtistAlbumsAsync(
        string artistId,
        CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getArtist", Q(("id", artistId)), cancellationToken).ConfigureAwait(false);
        // 等价 Dart：_listOf({'wrapper': body['artist']}, 'wrapper', 'album')
        var wrapper = new JsonObject();
        var artist = JsonRead.Prop(body, "artist");
        if (artist.HasValue) wrapper["artist"] = JsonNode.Parse(artist.Value.GetRawText());
        return ListOf(JsonRead.From(wrapper), "wrapper", "album").Select(SubsonicAlbum.FromJson).ToList();
    }

    /// <summary>
    /// 专辑列表（<c>getAlbumList2</c>）。<paramref name="type"/> 取 Subsonic 规定值：
    /// <c>newest</c>/<c>alphabeticalByName</c>/<c>alphabeticalByArtist</c>/<c>recent</c>/<c>frequent</c>/
    /// <c>random</c>/<c>starred</c>/<c>byYear</c>/<c>byGenre</c>。
    /// </summary>
    public async Task<List<SubsonicAlbum>> GetAlbumList2Async(
        string type = "alphabeticalByArtist",
        int size = 100,
        int offset = 0,
        int? fromYear = null,
        int? toYear = null,
        string genre = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = type ?? "alphabeticalByArtist",
            ["size"] = size.ToString(CultureInfo.InvariantCulture),
            ["offset"] = offset.ToString(CultureInfo.InvariantCulture),
        };
        if (fromYear.HasValue) query["fromYear"] = fromYear.Value.ToString(CultureInfo.InvariantCulture);
        if (toYear.HasValue) query["toYear"] = toYear.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(genre)) query["genre"] = genre;

        var body = await CallAsync("getAlbumList2", query, cancellationToken).ConfigureAwait(false);
        return ListOf(body, "albumList2", "album").Select(SubsonicAlbum.FromJson).ToList();
    }

    /// <summary>专辑详情（<c>getAlbum</c> → <c>album.song[]</c>）。无 <c>album</c> 节点时返回空结果。</summary>
    public async Task<SubsonicAlbumWithSongs> GetAlbumAsync(
        string albumId,
        CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getAlbum", Q(("id", albumId)), cancellationToken).ConfigureAwait(false);
        var holder = JsonRead.Prop(body, "album");
        if (holder == null || holder.Value.ValueKind != JsonValueKind.Object)
        {
            return new SubsonicAlbumWithSongs();
        }
        var songs = JsonRead.Objects(JsonRead.Items(holder, "song")).Select(SubsonicSong.FromJson).ToList();
        return new SubsonicAlbumWithSongs
        {
            Album = SubsonicAlbum.FromJson(holder),
            Songs = songs,
        };
    }

    /// <summary>收藏（<c>getStarred2</c> → <c>starred2.artist/album/song[]</c>）。</summary>
    public async Task<SubsonicStarred> GetStarred2Async(CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getStarred2", null, cancellationToken).ConfigureAwait(false);
        return new SubsonicStarred
        {
            Artists = ListOf(body, "starred2", "artist").Select(SubsonicArtist.FromJson).ToList(),
            Albums = ListOf(body, "starred2", "album").Select(SubsonicAlbum.FromJson).ToList(),
            Songs = ListOf(body, "starred2", "song").Select(SubsonicSong.FromJson).ToList(),
        };
    }

    /// <summary>播放列表（<c>getPlaylists</c> → <c>playlists.playlist[]</c>）。</summary>
    public async Task<List<SubsonicPlaylist>> GetPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getPlaylists", null, cancellationToken).ConfigureAwait(false);
        return ListOf(body, "playlists", "playlist").Select(SubsonicPlaylist.FromJson).ToList();
    }

    /// <summary>播放列表内容（<c>getPlaylist</c> → <c>playlist.entry[]</c>）。</summary>
    public async Task<List<SubsonicSong>> GetPlaylistAsync(
        string playlistId,
        CancellationToken cancellationToken = default)
    {
        var body = await CallAsync("getPlaylist", Q(("id", playlistId)), cancellationToken).ConfigureAwait(false);
        return ListOf(body, "playlist", "entry").Select(SubsonicSong.FromJson).ToList();
    }

    /// <summary>随机歌曲（<c>getRandomSongs</c> → <c>randomSongs.song[]</c>；方法名未在 §2 的 [S] 清单内，容错解析）。</summary>
    public async Task<List<SubsonicSong>> GetRandomSongsAsync(
        int size = 50,
        CancellationToken cancellationToken = default)
    {
        var body = await CallAsync(
            "getRandomSongs",
            Q(("size", size.ToString(CultureInfo.InvariantCulture))),
            cancellationToken).ConfigureAwait(false);
        return ListOf(body, "randomSongs", "song").Select(SubsonicSong.FromJson).ToList();
    }

    /// <summary>搜索（<c>search3</c> → <c>searchResult3.artist/album/song[]</c>）。</summary>
    public async Task<SubsonicSearchResult> Search3Async(
        string query,
        int count = 30,
        CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["query"] = query ?? string.Empty,
            ["artistCount"] = count.ToString(CultureInfo.InvariantCulture),
            ["albumCount"] = count.ToString(CultureInfo.InvariantCulture),
            ["songCount"] = count.ToString(CultureInfo.InvariantCulture),
        };
        var body = await CallAsync("search3", q, cancellationToken).ConfigureAwait(false);
        return new SubsonicSearchResult
        {
            Artists = ListOf(body, "searchResult3", "artist").Select(SubsonicArtist.FromJson).ToList(),
            Albums = ListOf(body, "searchResult3", "album").Select(SubsonicAlbum.FromJson).ToList(),
            Songs = ListOf(body, "searchResult3", "song").Select(SubsonicSong.FromJson).ToList(),
        };
    }

    // ── 收藏 / 打卡 / 歌词 / 地址 ──────────────────────────────────────────

    /// <summary>
    /// 收藏 / 取消收藏（<c>star</c>、<c>unstar</c>）。
    /// 等价 Dart：同时下发 <c>id=</c> 与按 <paramref name="type"/> 命名的键（<c>song=</c>/<c>album=</c>/<c>artist=</c>）。
    /// 失败只记日志并返回 <c>false</c>。
    /// </summary>
    public async Task<bool> SetStarredAsync(
        string id,
        bool starred,
        string type = "song",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = string.IsNullOrEmpty(type) ? "song" : type;
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = id ?? string.Empty,
                [key] = id ?? string.Empty,
            };
            await CallAsync(starred ? "star" : "unstar", query, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"{(starred ? "star" : "unstar")} 失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 打卡上报（<c>scrobble</c>）：<c>submission=false</c> 即 now-playing。
    /// <paramref name="timeSeconds"/> 为 Unix 秒时间戳（Subsonic <c>time</c> 参数原义，未在 §2 逐参数实证 ⇒ 原样透传）。
    /// 失败只记日志并返回 <c>false</c>。
    /// </summary>
    public async Task<bool> ScrobbleAsync(
        string songId,
        bool submission = true,
        long? timeSeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = songId ?? string.Empty,
                ["submission"] = submission ? "true" : "false",
            };
            if (timeSeconds.HasValue)
            {
                query["time"] = timeSeconds.Value.ToString(CultureInfo.InvariantCulture);
            }
            await CallAsync("scrobble", query, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"scrobble 失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 歌词（<c>getLyricsBySongId</c>）：拆 <c>lyricsList.structuredLyrics[0]</c> 后交给
    /// <see cref="SubsonicLyrics.FromJson"/>（等价 Dart 侧同一分工）。无歌词或失败返回 <c>null</c>。
    /// </summary>
    public async Task<SubsonicLyrics> GetLyricsBySongIdAsync(
        string songId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = await CallAsync("getLyricsBySongId", Q(("id", songId)), cancellationToken).ConfigureAwait(false);
            var holder = JsonRead.Prop(body, "lyricsList");
            if (holder == null || holder.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var list = JsonRead.Items(holder, "structuredLyrics");
            if (list.Count > 0 && list[0].ValueKind == JsonValueKind.Object)
            {
                // 等价 Dart：SubsonicLyrics.fromJson({'structuredLyrics': [first]})
                // 注意必须是**数组**：[first] —— 模型侧按 structuredLyrics 数组取首个元素，
                // 直接塞单对象会解析出空歌词（本工程自检 Navidrome 用例实测抓到的缺陷）。
                var wrapper = new JsonObject
                {
                    ["structuredLyrics"] = new JsonArray { JsonNode.Parse(list[0].GetRawText()) },
                };
                var wrapperElement = JsonRead.From(wrapper);
                return wrapperElement.HasValue ? SubsonicLyrics.FromJson(wrapperElement) : null;
            }
            return null;
        }
        catch (Exception ex)
        {
            Log($"getLyricsBySongId 失败：{ex}");
            return null;
        }
    }

    /// <summary>
    /// 流地址（<c>/rest/stream</c>；音频直连，无需转码协商）。
    /// 该 URL 直接喂给内核 <c>--open=</c>（内核负责解码与渲染）。
    /// </summary>
    public string StreamUrl(
        string songId,
        int? maxBitRate = null,
        string format = null,
        int? timeOffsetSeconds = null)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = songId ?? string.Empty,
        };
        if (maxBitRate.HasValue) query["maxBitRate"] = maxBitRate.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(format)) query["format"] = format;
        if (timeOffsetSeconds.HasValue) query["timeOffset"] = timeOffsetSeconds.Value.ToString(CultureInfo.InvariantCulture);
        return Api("stream", query);
    }

    /// <summary>封面地址（<c>/rest/getCoverArt</c>）；<paramref name="coverArtId"/> 为空返回空串（需先判空）。</summary>
    public string CoverArtUrl(string coverArtId, int? size = null)
    {
        if (string.IsNullOrEmpty(coverArtId)) return string.Empty;
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = coverArtId,
        };
        if (size.HasValue) query["size"] = size.Value.ToString(CultureInfo.InvariantCulture);
        return Api("getCoverArt", query);
    }

    /// <summary>
    /// 免鉴权直链（供内核/外部播放器使用）：把 <c>u/t/s</c> 换成 <c>api_key</c> 之外的等价形式，
    /// 即保留 token+salt 参数。URL 与 <see cref="StreamUrl"/> 同构，仅便于调用方表达意图。
    /// </summary>
    public string StreamUrlWithAuth(string songId, int? maxBitRate = null)
        => StreamUrl(songId, maxBitRate);

    /// <summary>等价 Dart <c>toString()</c>：<c>NavidromeService(名称 @ baseUrl)</c>。</summary>
    public override string ToString() => $"NavidromeService({Server.Name} @ {BaseUrl})";

    // ── 小工具 ─────────────────────────────────────────────────────────────

    /// <summary>构造单键 query（等价 Dart 的字面量 Map；值 null ⇒ 空串）。</summary>
    private static Dictionary<string, string> Q(params (string Key, string Value)[] pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pairs) map[p.Key] = p.Value ?? string.Empty;
        return map;
    }

    private static string NonEmpty(string value, string fallback)
        => string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;

    private static int? ToInt(long? value)
    {
        if (!value.HasValue) return null;
        if (value.Value > int.MaxValue) return int.MaxValue;
        if (value.Value < int.MinValue) return int.MinValue;
        return (int)value.Value;
    }
}

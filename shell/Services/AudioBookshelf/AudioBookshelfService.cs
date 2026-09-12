// 等价移植：rebuild/ai_player/lib/core/services/audiobookshelf_service.dart（Audiobookshelf 客户端）。
//
// 端点与鉴权依据：reversed/FlutterApp/SERVICE_API.md §3 Audiobookshelf（`[S][E]` 实证）：
//   - 登录 POST /login 换 Bearer token（Dart 侧读 user.token，回落顶层 token）
//   - 媒体库 GET /api/libraries、GET /api/libraries/{id}（含 /items）
//   - 条目   GET /api/items/{id}?expanded=1、取流 /api/items/{id}/file[/{ino}]、封面 /api/items/{id}/cover
//   - 作者   GET /api/authors、GET /api/authors/{id}
//   - 搜索   GET /api/search?q=
//   - 进度   GET /api/me/progress/{itemId}、PATCH /api/me/progress/{itemId}、GET /api/me/items-in-progress
//   - 播放会话 POST /api/session/{itemId}/play、POST /api/session/{sessionId}/sync、POST /api/session/{sessionId}/close
//   - 备份   GET /api/backups（SERVICE_API.md §3 记为 `/backup_` 前缀；Dart 实现实证为 /api/backups，
//            此处沿用 Dart 的可运行端点，并把两种形态都记在注释里）
//
// Bearer 鉴权：请求头 `Authorization: Bearer <token>`（token 存 ServerConfig.AccessToken）。
// 语义保持：**查询类**沿用 Dart（有 try/catch 的只记日志返回 null/空表，无 try/catch 的照旧抛出 ShellHttpException）；
//           **上报类**（进度 / 会话）失败只记日志、绝不上抛，避免打断播放。
// 多音轨：本书的轨内秒数 ↔ 全书秒数换算见 AbsTimeline.cs（`/api/me/progress` 的 currentTime 是全书秒数）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.AudioBookshelf;

/// <summary>Audiobookshelf 客户端（对应 Dart <c>AudiobookshelfService</c>）。</summary>
public sealed class AudioBookshelfService
{
    /// <summary>等价 Dart <c>static const clientName</c>。</summary>
    public const string DefaultClientName = "AI Player";

    /// <summary>等价 Dart <c>static const deviceName</c>。</summary>
    public const string DefaultDeviceName = "Windows";

    private readonly Action<string> _onLog;

    public AudioBookshelfService(
        ShellHttpClient http,
        ServerConfig server,
        Action<string> onLog = null,
        string clientName = DefaultClientName,
        string deviceName = DefaultDeviceName)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        _onLog = onLog;
        ClientName = string.IsNullOrEmpty(clientName) ? DefaultClientName : clientName;
        DeviceName = string.IsNullOrEmpty(deviceName) ? DefaultDeviceName : deviceName;
    }

    public ShellHttpClient Http { get; }

    /// <summary>对应 Dart 的可变字段 <c>server</c>（token 从 <c>AccessToken</c> 取）。</summary>
    public ServerConfig Server { get; set; }

    public string ClientName { get; }

    public string DeviceName { get; }

    public Action<string> OnLog => _onLog;

    public string BaseUrl => TextUtils.NormalizeBaseUrl(Server.BaseUrl);

    public string Token => Server.AccessToken;

    private void Log(string message) => _onLog?.Invoke(message);

    /// <summary>业务请求头：仅在有 token 时带 Bearer（对应 Dart <c>_headers</c>）。</summary>
    public Dictionary<string, string> Headers
    {
        get
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(Token)) headers["Authorization"] = $"Bearer {Token}";
            return headers;
        }
    }

    /// <summary>供内核取流用（ABS 走 Bearer 头，内核通过 <c>--http-header=</c> 携带）——对应 Dart <c>hostHttpHeaders</c>。</summary>
    public Dictionary<string, string> HostHttpHeaders => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Authorization"] = $"Bearer {Token}",
        // t65：UA 走唯一构造点（此前这里用的是 ABS 的 ClientName ⇒ 又一处独立 UA 来源）
        ["User-Agent"] = AIPlayer.Shell.Services.Http.UserAgentPolicy.Current,
    };

    public string Uri(string path, IDictionary<string, string> query = null) => TextUtils.BuildUri(BaseUrl, path, query);

    // ── 认证 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 登录（<c>POST /login</c>）——等价 Dart <c>static Future&lt;AbsAuth&gt; login({...})</c>。
    /// token 取 <c>user.token</c>，回落响应顶层 <c>token</c>；userId 取 <c>user.id</c>（回落顶层 <c>userId</c>，
    /// 后者「未实证，容错解析」）；userName 取 <c>user.username</c>，回落入参 username。
    /// 失败按 Dart 语义上抛 <see cref="ShellHttpException"/>（登录失败必须让调用方看见）。
    /// </summary>
    public static async Task<AbsAuth> LoginAsync(
        ShellHttpClient http,
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (http == null) throw new ArgumentNullException(nameof(http));

        var normalized = TextUtils.NormalizeBaseUrl(baseUrl);
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["username"] = username ?? string.Empty,
            ["password"] = password ?? string.Empty,
        };

        var result = await http.PostJsonAsync(
            TextUtils.BuildUri(normalized, "/login"),
            body,
            null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = result.JsonMap;
        var user = JsonRead.Prop(json, "user");

        var token = JsonRead.Str(user, "token");
        if (token.Length == 0) token = JsonRead.Str(json, "token");

        var userId = JsonRead.Str(user, "id");
        if (userId.Length == 0) userId = JsonRead.Str(json, "userId"); // 未实证，容错解析

        var userName = JsonRead.Str(user, "username");
        if (userName.Length == 0) userName = username ?? string.Empty;

        return new AbsAuth
        {
            Token = token,
            UserId = userId,
            UserName = userName,
        };
    }

    // ── 媒体库 ─────────────────────────────────────────────────────────────

    /// <summary>把「顶层即数组」的响应取成元素表（Dart <c>result.jsonList</c> 的等价写法）。</summary>
    private static List<JsonElement> TopLevelItems(ShellHttpResult result)
    {
        var raw = result.JsonList;
        if (!raw.HasValue) return new List<JsonElement>();
        var list = new List<JsonElement>();
        foreach (var item in raw.Value.EnumerateArray())
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>库列表（<c>GET /api/libraries</c>）：取 <c>libraries</c> 数组，回落顶层数组。</summary>
    public async Task<List<AbsLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri("/api/libraries"), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var list = JsonRead.Items(result.JsonMap, "libraries");
        if (list.Count == 0) list = TopLevelItems(result);

        var libraries = new List<AbsLibrary>();
        foreach (var element in JsonRead.Objects(list))
        {
            libraries.Add(AbsLibrary.FromJson(element));
        }
        return libraries;
    }

    /// <summary>
    /// 单个库详情（<c>GET /api/libraries/{id}</c>，SERVICE_API.md §3 实证端点；Dart 侧未单独实现，此处补齐）。
    /// 服务端把库对象放在顶层或 <c>library</c> 字段下，两种形态都容错。
    /// </summary>
    public async Task<AbsLibrary> GetLibraryAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri($"/api/libraries/{libraryId}"), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var json = result.JsonMap;
        var nested = JsonRead.Prop(json, "library");
        var target = nested ?? json;
        return target.HasValue ? AbsLibrary.FromJson(target.Value) : null;
    }

    /// <summary>
    /// 库内条目（<c>GET /api/libraries/{id}/items</c>）；结果取 <c>results</c>，回落 <c>items</c>。
    /// Dart 的查询参数原样保留：limit/page/sort/desc(1|0)/expanded=1。
    /// </summary>
    public async Task<List<AbsBook>> GetLibraryItemsAsync(
        string libraryId,
        int limit = 50,
        int page = 0,
        string sort = "media.metadata.title",
        bool desc = false,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["sort"] = sort ?? string.Empty,
            ["desc"] = desc ? "1" : "0",
            ["expanded"] = "1",
        };

        var result = await Http.GetAsync(
            Uri($"/api/libraries/{libraryId}/items", query),
            Headers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = result.JsonMap;
        var list = JsonRead.Items(json, "results");
        if (list.Count == 0) list = JsonRead.Items(json, "items");

        var books = new List<AbsBook>();
        foreach (var element in JsonRead.Objects(list))
        {
            books.Add(AbsBook.FromJson(element));
        }
        return books;
    }

    // ── 条目 ───────────────────────────────────────────────────────────────

    /// <summary>条目详情（<c>GET /api/items/{id}?expanded=1</c>）—— 含多轨清单。失败记日志返回 <c>null</c>。</summary>
    public async Task<AbsBook> GetItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["expanded"] = "1" };
            var result = await Http.GetAsync(
                Uri($"/api/items/{itemId}", query),
                Headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (!json.HasValue) return null;

            var book = AbsBook.FromJson(json.Value);
            CacheTracks(book); // 登记轨表：供 GetStreamUrl(itemId, trackIndex) 使用
            return book;
        }
        catch (Exception ex)
        {
            Log($"ABS getItem 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 在听条目（<c>GET /api/me/items-in-progress</c>）：顶层数组 → <c>items</c> → <c>libraryItems</c> 依次回落。
    /// </summary>
    public async Task<List<AbsBook>> GetItemsInProgressAsync(CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri("/api/me/items-in-progress"), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var list = TopLevelItems(result);
        if (list.Count == 0) list = JsonRead.Items(result.JsonMap, "items");
        if (list.Count == 0) list = JsonRead.Items(result.JsonMap, "libraryItems");

        var books = new List<AbsBook>();
        foreach (var element in JsonRead.Objects(list))
        {
            books.Add(AbsBook.FromJson(element));
        }
        return books;
    }

    // ── 作者 ───────────────────────────────────────────────────────────────

    /// <summary>作者列表（<c>GET /api/authors</c>）：取 <c>authors</c> 数组，回落顶层数组。</summary>
    public async Task<List<AbsAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri("/api/authors"), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var list = JsonRead.Items(result.JsonMap, "authors");
        if (list.Count == 0) list = TopLevelItems(result);

        var authors = new List<AbsAuthor>();
        foreach (var element in JsonRead.Objects(list))
        {
            authors.Add(AbsAuthor.FromJson(element));
        }
        return authors;
    }

    /// <summary>
    /// 单个作者（<c>GET /api/authors/{id}</c>，SERVICE_API.md §3 实证端点；Dart 侧未单独实现，此处补齐）。
    /// ABS 该端点返回 <c>{ author: {...}, libraryItems: [...] }</c> ⇒ 作者对象优先取 <c>author</c> 字段（未实证，容错解析）。
    /// </summary>
    public async Task<AbsAuthor> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri($"/api/authors/{authorId}"), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var json = result.JsonMap;
        var nested = JsonRead.Prop(json, "author");
        var target = nested ?? json;
        return target.HasValue ? AbsAuthor.FromJson(target.Value) : null;
    }

    // ── 搜索 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 搜索（<c>GET /api/search?q=</c>）。
    /// Dart 原样返回整张 JSON map（调用方按 <c>book</c>/<c>podcast</c>/<c>authors</c>/<c>series</c> 分桶自取），
    /// 故这里保留 <see cref="JsonObject"/> 形态；同时提供 <see cref="SearchAsync(string, CancellationToken)"/> 的强类型视图。
    /// </summary>
    public async Task<JsonObject> SearchRawAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = new Dictionary<string, string>(StringComparer.Ordinal) { ["q"] = query ?? string.Empty };
        var result = await Http.GetAsync(Uri("/api/search", q), Headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var json = result.JsonMap;
        return json.HasValue ? JsonRead.DeepClone(json.Value) : new JsonObject();
    }

    /// <summary>搜索的强类型视图：按 ABS 返回的 <c>book</c> / <c>podcast</c> / <c>authors</c> / <c>series</c> 分桶。</summary>
    public async Task<AbsSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var json = await SearchRawAsync(query, cancellationToken).ConfigureAwait(false);
        var element = JsonRead.From(json);
        return AbsSearchResult.FromJson(element);
    }

    // ── 进度 ───────────────────────────────────────────────────────────────

    /// <summary>读取进度（<c>GET /api/me/progress/{itemId}</c>）；失败记日志返回 <c>null</c>。</summary>
    public async Task<AbsProgress> GetProgressAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.GetAsync(
                Uri($"/api/me/progress/{itemId}"),
                Headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            return json.HasValue ? AbsProgress.FromJson(json.Value) : null;
        }
        catch (Exception ex)
        {
            Log($"ABS 进度读取失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 上报进度（<c>PATCH /api/me/progress/{itemId}</c>；<paramref name="progress"/> 的 <c>currentTime</c> 必须是**全书秒数**，
    /// 换算见 <see cref="AbsTimeline.GlobalSecondsFromTrack(IReadOnlyList{AbsAudioTrack}, int, double)"/>）。
    /// 请求体只含 currentTime/duration/progress/isFinished（等价 Dart <c>AbsProgress.toJson()</c>）；失败记日志返回 <c>false</c>。
    /// </summary>
    public async Task<bool> UpdateProgressAsync(string itemId, AbsProgress progress, CancellationToken cancellationToken = default)
    {
        try
        {
            // 用 JsonObject 作 body：ShellHttpClient 会序列化为 JSON 并自动带 Content-Type（等价 Dart 的 map body）。
            var body = progress == null ? new JsonObject() : progress.ToJson();
            await Http.CustomAsync(
                "PATCH",
                Uri($"/api/me/progress/{itemId}"),
                Headers,
                body,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"ABS 进度上报失败：{ex.Message}");
            return false;
        }
    }

    // ── 播放会话 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 开启播放会话（<c>POST /api/session/{itemId}/play</c>），返回 <c>sessionId</c>（无则 <c>null</c>）。
    /// body 与 Dart 一致：deviceInfo + mediaPlayer，<paramref name="trackIndex"/> / <paramref name="startTime"/> 仅在给定时下发
    /// （<c>startIndex</c> 用数组下标；<c>startTime</c> 是**轨内秒数**）。
    /// </summary>
    public async Task<string> StartPlaybackSessionAsync(
        string itemId,
        int? trackIndex = null,
        double? startTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceInfo = new JsonObject
            {
                ["clientName"] = ClientName,
                ["deviceName"] = DeviceName,
                ["deviceId"] = Server.Id ?? string.Empty,
                ["manufacturer"] = "AI Player",
                ["model"] = "Rebuilt Shell",
            };

            var body = new JsonObject
            {
                ["deviceInfo"] = deviceInfo,
                ["mediaPlayer"] = "AI Player",
            };
            if (trackIndex.HasValue) body["startIndex"] = trackIndex.Value;
            if (startTime.HasValue) body["startTime"] = startTime.Value;

            var result = await Http.PostJsonAsync(
                Uri($"/api/session/{itemId}/play"),
                body,
                Headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var id = JsonRead.Str(result.JsonMap, "id");
            return id.Length == 0 ? null : id;
        }
        catch (Exception ex)
        {
            Log($"ABS 会话创建失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 会话同步（<c>POST /api/session/{sessionId}/sync</c>）。<paramref name="currentTime"/> 是**全书秒数**；
    /// body 含 currentTime/（可选 duration）/（可选 startIndex = 轨下标）/isPaused/playbackRate。失败只记日志。
    /// </summary>
    public async Task SyncSessionAsync(
        string sessionId,
        double currentTime,
        double? duration = null,
        int? trackIndex = null,
        bool isPaused = false,
        double playbackRate = 1.0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new JsonObject
            {
                ["currentTime"] = currentTime,
                ["isPaused"] = isPaused,
                ["playbackRate"] = playbackRate,
            };
            if (duration.HasValue) body["duration"] = duration.Value;
            if (trackIndex.HasValue) body["startIndex"] = trackIndex.Value;

            await Http.PostJsonAsync(
                Uri($"/api/session/{sessionId}/sync"),
                body,
                Headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"ABS 会话同步失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 关闭会话（<c>POST /api/session/{sessionId}/close</c>）。<paramref name="currentTime"/> 是**全书秒数**；
    /// body 含 currentTime/（可选 duration）/（可选 startIndex）。失败只记日志。
    /// </summary>
    public async Task CloseSessionAsync(
        string sessionId,
        double currentTime,
        double? duration = null,
        int? trackIndex = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new JsonObject { ["currentTime"] = currentTime };
            if (duration.HasValue) body["duration"] = duration.Value;
            if (trackIndex.HasValue) body["startIndex"] = trackIndex.Value;

            await Http.PostJsonAsync(
                Uri($"/api/session/{sessionId}/close"),
                body,
                Headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"ABS 会话关闭失败：{ex.Message}");
        }
    }

    // ── 地址 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 单轨播放地址：<c>/api/items/{itemId}/file/{ino}</c>；<c>ino</c> 为空时回落 <c>/api/items/{itemId}/file?index={index}</c>。
    /// 与 Dart <c>trackStreamUrl</c> 一致：<c>?index=</c> 回落分支用的是轨对象自带的 <c>index</c> 字段（服务端原值，
    /// 存在从 1 开始的版本 ⇒ 与 <see cref="GetStreamUrl"/> 的数组下标语义**不同**，此处保持 Dart 原样）。
    /// </summary>
    public string TrackStreamUrl(string itemId, AbsAudioTrack track)
    {
        if (track != null && track.Ino.Length > 0)
        {
            return Uri($"/api/items/{itemId}/file/{track.Ino}");
        }

        var index = track == null ? 0 : track.Index;
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["index"] = index.ToString(CultureInfo.InvariantCulture),
        };
        return Uri($"/api/items/{itemId}/file", query);
    }

    /// <summary>
    /// 取流地址（等价 Dart <c>trackStreamUrl</c>，按**轨下标**取）：索引越界或该条目未登记轨表时返回
    /// <c>string.Empty</c>（安全回落，不抛异常）；传入的轨存在 <c>ino</c> 时优先用 <c>/file/{ino}</c>，
    /// 否则回落 <c>/file?index={数组下标}</c>（服务端未实证 index 语义，容错）。
    /// </summary>
    public string GetStreamUrl(string itemId, int trackIndex)
    {
        var tracks = TracksOf(itemId);
        var track = AbsTimeline.TrackAt(tracks, trackIndex);
        return track == null ? string.Empty : TrackStreamUrl(itemId, track);
    }

    /// <summary>取流地址（按**轨道表**下标取，避免依赖内部缓存）。</summary>
    public string GetStreamUrl(string itemId, IReadOnlyList<AbsAudioTrack> tracks, int trackIndex)
    {
        var track = AbsTimeline.TrackAt(tracks, trackIndex);
        return track == null ? string.Empty : TrackStreamUrl(itemId, track);
    }

    /// <summary>
    /// 轨表缓存：<c>GetItemAsync</c> 成功时写入，供按 <c>(itemId, trackIndex)</c> 生成取流地址
    /// （内核取流只需要地址 + Bearer 头，缓存让调用方不必自己保存轨表）。
    /// </summary>
    private readonly Dictionary<string, IReadOnlyList<AbsAudioTrack>> _tracks =
        new Dictionary<string, IReadOnlyList<AbsAudioTrack>>(StringComparer.Ordinal);

    /// <summary>已登记的 bookId 数（缓存只增不减，条目数上限=本次会话打开过的书）。</summary>
    public int CachedTrackCount => _tracks.Count;

    /// <summary>取某条目已登记的轨表；未登记返回空表（不抛异常）。</summary>
    public IReadOnlyList<AbsAudioTrack> TracksOf(string itemId)
        => !string.IsNullOrEmpty(itemId) && _tracks.TryGetValue(itemId, out var tracks)
            ? tracks
            : (IReadOnlyList<AbsAudioTrack>)Array.Empty<AbsAudioTrack>();

    /// <summary>把条目轨表登记进缓存（<c>GetItemAsync</c> 内部调用；也可由调用方在别处拿到条目后手动登记）。</summary>
    public void CacheTracks(AbsBook book)
    {
        if (book == null || string.IsNullOrEmpty(book.Id)) return;
        if (book.Tracks != null) _tracks[book.Id] = book.Tracks;
    }

    /// <summary>服务端原始轨下标 → 本地数组下标（ABS 存在 index 从 1 起的版本；不在缓存中时返回 -1）。</summary>
    public int ResolveTrackIndex(string itemId, int serverTrackIndex)
    {
        if (string.IsNullOrEmpty(itemId) || !_tracks.TryGetValue(itemId, out var tracks)) return -1;
        return AbsTimeline.TrackIndexOfServerIndex(tracks, serverTrackIndex);
    }

    /// <summary>目录里已有该 bookId 的轨表缓存。</summary>
    public bool HasCachedTracks(string itemId)
        => !string.IsNullOrEmpty(itemId) && _tracks.ContainsKey(itemId);

    /// <summary>封面地址（<c>/api/items/{id}/cover</c>）。</summary>
    public string CoverUrl(string itemId) => Uri($"/api/items/{itemId}/cover");

    // ── 备份 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 备份列表（<c>GET /api/backups</c>；SERVICE_API.md §3 记作 <c>/backup_</c> 前缀，Dart 实现实证为 <c>/api/backups</c>）。
    /// 返回原始对象表（Dart 同样返回 <c>Map</c> 列表）；失败记日志返回空表。
    /// </summary>
    public async Task<List<JsonObject>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        var backups = new List<JsonObject>();
        try
        {
            var result = await Http.GetAsync(Uri("/api/backups"), Headers, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var list = JsonRead.Items(result.JsonMap, "backups");
            if (list.Count == 0) list = TopLevelItems(result);

            foreach (var element in JsonRead.Objects(list))
            {
                backups.Add(JsonRead.DeepClone(element));
            }
        }
        catch (Exception ex)
        {
            Log($"ABS 备份列表失败：{ex.Message}");
        }
        return backups;
    }

    public override string ToString() => $"AudioBookshelfService({Server.Name} @ {BaseUrl})";
}

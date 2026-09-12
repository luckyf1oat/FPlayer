// 等价移植：rebuild/ai_player/lib/core/services/emby_service.dart（802 行 Dart → C#）。
// 端点、参数、请求头依据 reversed/FlutterApp/SERVICE_API.md §1（[E]/[S]/[H] 实证）：
//   - 认证 POST /Users/AuthenticateByName + X-Emby-Token / X-Emby-Authorization
//   - 浏览 /Users/{userId}/Views、/Users/{userId}/Items、/Items/Counts、/Shows/NextUp …
//   - 取流 /Videos/{id}/stream?static=true&api_key=、POST /Items/{id}/PlaybackInfo
//   - 上报 /Sessions/Playing、/Sessions/Playing/Progress、/Sessions/Playing/Stopped
//   - 媒体段 /MediaSegments/{itemId}（跳过片头片尾的原生来源）
// 语义保持：上报类失败只记日志、**不阻塞播放**（与原 Dart 一致）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Emby;

/// <summary>登录结果（对应 Dart <c>EmbyAuthResult</c>）。</summary>
public sealed class EmbyAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
}

/// <summary>Emby / Jellyfin 客户端（对应 Dart <c>EmbyService</c>）。</summary>
public sealed class EmbyService
{
    /// <summary>列表查询统一请求的字段集（含取流与跳过片段需要的全部信息）。</summary>
    /// <remarks>
    /// 🔴 t48 根治（`ui2` 在 t31 抓到）：**必须含 `ImageTags`**。它给出 `ImageTags.Primary/Backdrop/Logo…` 的 tag，
    /// 外壳据此拼封面 URL（`EmbyItem.PrimaryImageTag` → `ImageUrl(...)`）；缺它 ⇒ 响应里没有封面标签
    /// ⇒ 外层整屏一张封面都没有（他实测单轮 **47 条** `poster-no-image-url`）。
    /// `GetItemsAsync`（`:265`）有 `fields:` 入参可以绕开，但 **`GetResumeAsync`（`:297`）/ `GetLatestAsync`（`:314`）没有该入参**
    /// ⇒ 只能在这里根治。
    /// </remarks>
    public const string ItemFields =
        "Overview,MediaSources,MediaStreams,ProviderIds,ChildCount,RecursiveItemCount,PrimaryImageAspectRatio," +
        "SeriesPrimaryImageTag,ImageTags,DateCreated,Path,Studios,People,Genres,Taglines,ProductionYear,PremiereDate," +
        "CommunityRating,OfficialRating,RunTimeTicks,SeriesStudio,SeriesName,SeasonName,IndexNumber,ParentIndexNumber," +
        // t76：首页要画"年份区间"（ProductionYear–EndDate.Year）与"连载中/已完结"字幕 ⇒ 必须把这两个字段要回来。
        // t76 同批追加：Backdrop 的父级回退要 `ParentBackdropImageTags`（`ImageUrlIfAvailable` 第 ④ 出口）。
        "EndDate,Status,ParentBackdropImageTags";

    public EmbyService(
        ShellHttpClient http,
        ServerConfig server,
        string deviceId = "aiplayer-rebuild",
        string deviceName = "AI Player",
        string clientName = "AI Player",
        string clientVersion = "1.0.0",
        string language = "zh-CN",
        Action<string> onLog = null)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        DeviceId = deviceId;
        DeviceName = deviceName;
        ClientName = clientName;
        ClientVersion = clientVersion;
        Language = language;
        OnLog = onLog;
    }

    public ShellHttpClient Http { get; }

    public ServerConfig Server { get; set; }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public string ClientName { get; }

    public string ClientVersion { get; }

    public string Language { get; }

    public Action<string> OnLog { get; }

    public string BaseUrl => TextUtils.NormalizeBaseUrl(Server.BaseUrl);

    public string Token => Server.AccessToken;

    public string UserId => Server.UserId;

    private void Log(string message) => OnLog?.Invoke(message);

    /// <summary>Emby 客户端身份串（<c>X-Emby-Authorization</c>）。</summary>
    public string AuthorizationHeader =>
        $"MediaBrowser Client=\"{ClientName}\", Device=\"{DeviceName}\", DeviceId=\"{DeviceId}\", Version=\"{ClientVersion}\"";

    public Dictionary<string, string> DefaultHeaders
    {
        get
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["X-Emby-Authorization"] = AuthorizationHeader,
                ["X-Emby-Client"] = ClientName,
                ["X-Emby-Client-Version"] = ClientVersion,
                ["X-Emby-Device-Id"] = DeviceId,
                ["X-Emby-Device-Name"] = DeviceName,
                ["X-Emby-Language"] = Language,
            };
            if (!string.IsNullOrEmpty(Token)) headers["X-Emby-Token"] = Token;
            return headers;
        }
    }

    /// <summary>供内核 <c>--http-header=</c> 使用的头（内核取流/字幕需要 Token）。
    /// 🔴 t65：<c>User-Agent</c> 取自**唯一构造点** <see cref="UserAgentPolicy.Current"/> —— 此前这里写死
    /// <c>$"{ClientName}/{ClientVersion}"</c>，与 <c>ShellHttpClient</c> 的默认串是**两份默认值**，
    /// 改一处另一处会静默留旧值（同一个 UA 影响 Emby 侧识别与内核取流两处）。</summary>
    public Dictionary<string, string> HostHttpHeaders => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["X-Emby-Token"] = Token,
        ["X-Emby-Authorization"] = AuthorizationHeader,
        ["User-Agent"] = UserAgentPolicy.Current,
    };

    public string Uri(string path, IDictionary<string, string> query = null)
    {
        // t48 健壮性（`ui2` 报）：缺凭据时 `UserId` 为空串 ⇒ 路径退化成 `/Users//Items` ⇒ 服务端只回 404/异常，
        // 调用方看不出根因是"没登录/没配凭据"。这里做**前置拒绝**，给可指认的错误（不改任何合法路径的行为）。
        if (path != null && path.StartsWith("/Users//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "缺少用户凭据（UserId 为空）：请先登录或为该服务器配置账号凭据 —— 否则请求路径会退化为 /Users//…（服务端只会回 404）");
        }
        return TextUtils.BuildUri(BaseUrl, path, query);
    }

    // ── 认证 ───────────────────────────────────────────────────────────────

    /// <summary>登录（<c>POST /Users/AuthenticateByName</c>）。</summary>
    public static async Task<EmbyAuthResult> LoginAsync(
        ShellHttpClient http,
        string baseUrl,
        string username,
        string password,
        string deviceId = "aiplayer-rebuild",
        string deviceName = "AI Player",
        string clientName = "AI Player",
        string clientVersion = "1.0.0",
        CancellationToken cancellationToken = default)
    {
        var normalized = TextUtils.NormalizeBaseUrl(baseUrl);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Emby-Authorization"] = $"MediaBrowser Client=\"{clientName}\", Device=\"{deviceName}\", DeviceId=\"{deviceId}\", Version=\"{clientVersion}\"",
            ["X-Emby-Client"] = clientName,
            ["X-Emby-Client-Version"] = clientVersion,
            ["X-Emby-Device-Id"] = deviceId,
            ["X-Emby-Device-Name"] = deviceName,
            ["X-Emby-Language"] = "zh-CN",
        };

        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Username"] = username,
            ["Pw"] = password,
        };

        var result = await http.PostJsonAsync(
            TextUtils.BuildUri(normalized, "/Users/AuthenticateByName"),
            body,
            headers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = result.JsonMap;
        var user = JsonRead.Prop(json, "User");
        var accessToken = JsonRead.Str(json, "AccessToken");
        if (accessToken.Length == 0)
        {
            throw new ShellHttpException("登录失败：服务端未返回 AccessToken", null, result.RequestUrl);
        }

        var userName = JsonRead.Str(user, "Name");
        if (userName.Length == 0) userName = username;

        return new EmbyAuthResult
        {
            AccessToken = accessToken,
            UserId = JsonRead.Str(user, "Id"),
            UserName = userName,
            ServerId = JsonRead.Str(json, "ServerId"),
        };
    }

    /// <summary>公共信息（免鉴权）：<c>GET /System/Info/Public</c> —— 新建服务器时先探活。</summary>
    public static async Task<JsonObject> PublicInfoAsync(
        ShellHttpClient http,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var result = await http.GetAsync(
            TextUtils.BuildUri(TextUtils.NormalizeBaseUrl(baseUrl), "/System/Info/Public"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = result.JsonMap;
        return json.HasValue ? JsonRead.DeepClone(json.Value) : new JsonObject();
    }

    /// <summary>服务端域名的额外地址（<c>GET /System/Ext/ServerDomains</c>，原版字符串实证）；失败返回空表。</summary>
    public async Task<List<string>> ServerDomainsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.GetAsync(Uri("/System/Ext/ServerDomains"), DefaultHeaders, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var json = result.Json;
            if (json == null || json.Value.ValueKind != JsonValueKind.Array) return new List<string>();
            return json.Value.EnumerateArray()
                .Select(e => JsonRead.AsString(e))
                .Where(s => s.Length > 0)
                .ToList();
        }
        catch (Exception ex)
        {
            Log($"ServerDomains 获取失败: {ex.Message}");
            return new List<string>();
        }
    }

    // ── 浏览 ───────────────────────────────────────────────────────────────

    private static List<EmbyItem> ItemsOf(ShellHttpResult result)
        => JsonRead.Objects(JsonRead.Items(result.JsonMap, "Items")).Select(EmbyItem.FromJson).ToList();

    /// <summary>用户可见的库列表（<c>GET /Users/{userId}/Views</c>）。</summary>
    public async Task<List<EmbyUserView>> GetViewsAsync(CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Views"), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return JsonRead.Objects(JsonRead.Items(result.JsonMap, "Items")).Select(EmbyUserView.FromJson).ToList();
    }

    /// <summary>通用条目查询（<c>GET /Users/{userId}/Items</c>）。</summary>
    public async Task<EmbyQueryResult> GetItemsAsync(
        string parentId = null,
        string includeItemTypes = null,
        bool recursive = true,
        int startIndex = 0,
        int limit = 100,
        string sortBy = null,
        string sortOrder = "Ascending",
        string filters = null,
        string searchTerm = null,
        bool? isPlayed = null,
        bool? isFavorite = null,
        bool enableImages = true,
        int imageTypeLimit = 1,
        string anyProviderIdEquals = null,
        string fields = null,
        string genreIds = null,
        string personIds = null,
        int? minCommunityRating = null,
        string studios = null,
        string years = null,
        string albumArtistIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parentId != null) query["ParentId"] = parentId;
        if (includeItemTypes != null) query["IncludeItemTypes"] = includeItemTypes;
        query["Recursive"] = recursive ? "true" : "false";
        query["StartIndex"] = startIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (sortBy != null)
        {
            query["SortBy"] = sortBy;
            query["SortOrder"] = sortOrder;
        }
        if (filters != null) query["Filters"] = filters;
        if (searchTerm != null) query["SearchTerm"] = searchTerm;
        if (isPlayed.HasValue) query["IsPlayed"] = isPlayed.Value ? "true" : "false";
        if (isFavorite.HasValue) query["IsFavorite"] = isFavorite.Value ? "true" : "false";
        query["EnableImages"] = enableImages ? "true" : "false";
        query["ImageTypeLimit"] = imageTypeLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (anyProviderIdEquals != null) query["AnyProviderIdEquals"] = anyProviderIdEquals;
        query["Fields"] = fields ?? ItemFields;
        if (genreIds != null) query["GenreIds"] = genreIds;
        if (personIds != null) query["PersonIds"] = personIds;
        if (minCommunityRating.HasValue) query["MinCommunityRating"] = minCommunityRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (studios != null) query["Studios"] = studios;
        if (years != null) query["Years"] = years;
        if (albumArtistIds != null) query["AlbumArtistIds"] = albumArtistIds;

        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Items", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var json = result.JsonMap;
        return json.HasValue ? EmbyQueryResult.FromJson(json.Value) : new EmbyQueryResult();
    }

    /// <summary>
    /// t131：`/Items` **分页判据的单点**（消费方一律用它，不要在各自调用点散补）。
    /// 为什么需要：本机实测该 Emby 服务端在**带 `SearchTerm` 的查询**上一律回 `TotalRecordCount = 0`
    /// （显式加 `EnableTotalRecordCount=true` 也一样 —— 两次响应体逐字节相同），而
    /// <see cref="EmbyQueryResult.HasMore"/>（`Items.Count &lt; TotalRecordCount`）在这条路上**恒为 false**
    /// ⇒ 将来做"翻页/加载更多"的消费方会**在满页面面前静默停住**（静默少取）。
    /// 判据：
    ///   · 服务端给了**正**计数 ⇒ 用旧口径 `Items.Count &lt; TotalRecordCount`（**逐字不变**）；
    ///   · 计数为 0 / 字段缺失（search 路径、Latest 这类裸数组端点）⇒ 退回"**是否满页**"
    ///     `Items.Count &gt;= requestedLimit`（未知 ⇒ 保守地认为可能还有，交给下一次请求去证伪）。
    /// 反控（必须能失败）：把 <paramref name="requestedLimit"/> 调到**大于**实际返回条数 ⇒ 必须是 <c>false</c>
    /// （证明这个退回**不是恒真**，也不会把"最后一页"读成"还有更多"）。
    /// </summary>
    public static bool HasMore(EmbyQueryResult result, int requestedLimit)
    {
        if (result == null) return false;
        if (result.TotalRecordCount > 0) return result.Items.Count < result.TotalRecordCount;
        return requestedLimit > 0 && result.Items.Count >= requestedLimit;
    }

    /// <summary>单条详情（<c>GET /Users/{userId}/Items/{id}</c>）。</summary>
    public async Task<EmbyItem> GetItemAsync(string itemId, string fields = null, CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(
            Uri($"/Users/{UserId}/Items/{itemId}", new Dictionary<string, string>(StringComparer.Ordinal) { ["Fields"] = fields ?? ItemFields }),
            DefaultHeaders,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = result.JsonMap;
        return json.HasValue ? EmbyItem.FromJson(json.Value) : null;
    }

    /// <summary>
    /// 「继续观看」默认 <c>MediaTypes</c>（t70 实测选定，**超出原版的修复**）。
    /// 为什么必须有：实测 4 台真实服务器上，<c>GET /Users/{userId}/Items/Resume</c> **不给 MediaTypes 时一律返回 0 条**
    /// （响应体恒为 33 字节的空列表）；给 <c>MediaTypes=Video</c> 才有：ServerA 8 / ServerN 23 / ServerB 1 / ServerG 3。
    /// 为什么不是 <c>Video,Audio</c>：该值在 ServerA 上反而回 **0 条**（该服务端只认单值），而 ServerB 上又是 3 条 ⇒ **不可预测**，故默认取单值 <c>Video</c>。
    /// 原版 Dart 同样不带该参数（共同缺陷、非移植回归）；调用方仍可用 <c>mediaTypes</c> 覆盖。
    /// </summary>
    public const string DefaultResumeMediaTypes = "Video";

    /// <summary>继续观看（<c>GET /Users/{userId}/Items/Resume</c>）。</summary>
    public async Task<List<EmbyItem>> GetResumeAsync(string parentId = null, string mediaTypes = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parentId != null) query["ParentId"] = parentId;
        // t70：不给就补默认（否则该端点在真实服务器上恒回 0 条 ⇒ 首页「继续观看」永远空行）
        query["MediaTypes"] = string.IsNullOrEmpty(mediaTypes) ? DefaultResumeMediaTypes : mediaTypes;
        query["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["Fields"] = ItemFields;
        query["EnableImages"] = "true";
        query["ImageTypeLimit"] = "1";
        query["Recursive"] = "true";

        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Items/Resume", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>
    /// 最新添加（<c>GET /Users/{userId}/Items/Latest</c>；该接口直接返回**数组**）。
    /// <paramref name="groupItems"/>（t76 新增）：Emby 的 <c>GroupItems</c> —— 把**单集归到它的剧**下。
    /// **只在 <c>true</c> 时下发该参数**；缺省 <c>false</c> ⇒ **请求里完全不含该参数**（默认路径逐字节不变，t72 依赖这一点）。
    /// 实测（t83，真机 ServerA，电视库）：`GroupItems=false`/缺省形态仍会返回 **Episode**；`GroupItems=true` ⇒ 返回 **Series**。
    /// </summary>
    public async Task<List<EmbyItem>> GetLatestAsync(string parentId = null, string includeItemTypes = null, int limit = 20,
        bool groupItems = false, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parentId != null) query["ParentId"] = parentId;
        if (includeItemTypes != null) query["IncludeItemTypes"] = includeItemTypes;
        if (groupItems) query["GroupItems"] = "true";
        query["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["Fields"] = ItemFields;
        query["EnableImages"] = "true";
        query["ImageTypeLimit"] = "1";

        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Items/Latest", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var json = result.Json;
        if (json == null || json.Value.ValueKind != JsonValueKind.Array) return new List<EmbyItem>();
        return JsonRead.Objects(json.Value.EnumerateArray()).Select(EmbyItem.FromJson).ToList();
    }

    /// <summary>下一集（<c>GET /Shows/NextUp</c>）—— 内核 <c>--next-episode-id=</c> 的来源。</summary>
    public async Task<List<EmbyItem>> GetNextUpAsync(string seriesId = null, string seasonId = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["UserId"] = UserId };
        if (seriesId != null) query["SeriesId"] = seriesId;
        if (seasonId != null) query["SeasonId"] = seasonId;
        query["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["Fields"] = ItemFields;
        query["EnableImages"] = "true";
        query["ImageTypeLimit"] = "1";

        var result = await Http.GetAsync(Uri("/Shows/NextUp", query), DefaultHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>季列表（<c>/Shows/{id}/Seasons</c>）。</summary>
    public async Task<List<EmbyItem>> GetSeasonsAsync(string seriesId, CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(
            Uri($"/Shows/{seriesId}/Seasons", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserId"] = UserId,
                ["Fields"] = ItemFields,
                ["EnableImages"] = "true",
            }),
            DefaultHeaders,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>集列表（<c>/Shows/{id}/Episodes</c>）—— 内核 <c>--episode-list=</c> 的来源。</summary>
    public async Task<List<EmbyItem>> GetEpisodesAsync(string seriesId, string seasonId = null, int? startItemIndex = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["UserId"] = UserId };
        if (seasonId != null) query["SeasonId"] = seasonId;
        if (startItemIndex.HasValue) query["StartItemIndex"] = startItemIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["Fields"] = ItemFields;
        query["EnableImages"] = "true";

        var result = await Http.GetAsync(Uri($"/Shows/{seriesId}/Episodes", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>相似条目（<c>/Items/{id}/Similar</c>）。</summary>
    public async Task<List<EmbyItem>> GetSimilarAsync(string itemId, int limit = 12, CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(
            Uri($"/Items/{itemId}/Similar", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserId"] = UserId,
                ["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Fields"] = ItemFields,
                ["EnableImages"] = "true",
            }),
            DefaultHeaders,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ItemsOf(result);
    }

    // ── 音乐 ───────────────────────────────────────────────────────────────

    /// <summary>艺术家（<c>/Artists</c>）。</summary>
    public async Task<List<EmbyItem>> GetArtistsAsync(string parentId = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UserId"] = UserId,
            ["Limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Recursive"] = "true",
            ["Fields"] = ItemFields,
            ["EnableImages"] = "true",
            ["SortBy"] = "SortName",
        };
        if (parentId != null) query["ParentId"] = parentId;

        var result = await Http.GetAsync(Uri("/Artists", query), DefaultHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>专辑（<c>/Items?IncludeItemTypes=MusicAlbum</c>）。</summary>
    public async Task<List<EmbyItem>> GetAlbumsAsync(string artistId = null, string parentId = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IncludeItemTypes"] = "MusicAlbum",
            ["Recursive"] = "true",
            ["Fields"] = ItemFields,
            ["EnableImages"] = "true",
            ["SortBy"] = "ProductionYear,PremiereDate,SortName",
        };
        if (parentId != null) query["ParentId"] = parentId;
        if (artistId != null) query["AlbumArtistIds"] = artistId;

        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Items", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>单曲（<c>/Items?IncludeItemTypes=Audio</c>）。</summary>
    public async Task<List<EmbyItem>> GetSongsAsync(string albumId = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IncludeItemTypes"] = "Audio",
            ["Recursive"] = "true",
            ["Fields"] = ItemFields,
            ["EnableImages"] = "true",
            ["SortBy"] = "ParentIndexNumber,IndexNumber,SortName",
        };
        if (albumId != null) query["ParentId"] = albumId;

        var result = await Http.GetAsync(Uri($"/Users/{UserId}/Items", query), DefaultHeaders, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ItemsOf(result);
    }

    /// <summary>
    /// 搜索的**历史默认类型集合**（8 类）。t25 之前 <see cref="SearchAsync"/> 把它硬编在方法体里；
    /// 现在它是**公开常量**：调用方要复现历史行为就显式传它，不传则不加类型过滤（见 <see cref="SearchAsync"/>）。
    /// </summary>
    public const string DefaultSearchItemTypes = "Movie,Series,Episode,BoxSet,Person,Video,MusicAlbum,Audio";

    /// <summary>
    /// t289：**默认（聚合）搜索的剧优先「头部」类型集合** —— `Series` 最前，其次 `Movie` / `BoxSet`。
    /// <para>与 <see cref="DefaultSearchTailItemTypes"/> 成对使用：默认面（聚合搜索）先取头部、再取尾部
    /// （<c>EmbyAggregatedSearchSource.SearchAsync</c>）⇒ 根治用户实测的「默认搜出来一堆小集」。</para>
    /// <para>旧的单趟 8 类行为可由调用方显式传 <see cref="DefaultSearchItemTypes"/> 复现（<see cref="SearchAsync"/> 语义一字未改）。</para>
    /// </summary>
    public const string DefaultSearchHeadItemTypes = "Series,Movie,BoxSet";

    /// <summary>
    /// t289：默认（聚合）搜索的**「尾部」类型集合** —— `Episode`（在默认面按剧折叠为每剧至多 1 条）与其余非剧集类型。
    /// <para>显式点「集」chip 走的是 <see cref="SearchAsync"/> 的 <c>includeItemTypes: "Episode"</c>，
    /// **不经过本常量** ⇒ 集仍然可以搜全（不得用"禁掉 Episode"实现剧优先）。</para>
    /// </summary>
    public const string DefaultSearchTailItemTypes = "Episode,Person,Video,MusicAlbum,Audio";

    /// <summary>
    /// 搜索（<c>SearchTerm</c> 打在 <c>/Users/{userId}/Items</c>）。
    /// <para><b><paramref name="includeItemTypes"/> = <c>null</c>（默认）⇒ 请求里<b>不带</b> <c>IncludeItemTypes</c> query 参数</b>
    /// —— 与 <see cref="GetItemsAsync"/>（形参 <c>:225</c> / 判空 <c>:249</c>）和 <see cref="GetLatestAsync"/>（<c>:308</c> / <c>:312</c>）**同形**，
    /// 供 UI 的搜索类型芯片表达任意类型集合（如 <c>"Movie"</c>、<c>"Series,Episode"</c>）。</para>
    /// <para>要复现 t25 之前那条硬编 8 类的行为，请显式传 <see cref="DefaultSearchItemTypes"/>；
    /// 聚合搜索的 Emby 取数面（<c>EmbyAggregatedSearchSource</c>）正是这么做的 ⇒ **旧行为零变化**。</para>
    /// </summary>
    public Task<EmbyQueryResult> SearchAsync(
        string term,
        int limit = 60,
        string includeItemTypes = null,
        CancellationToken cancellationToken = default)
        => GetItemsAsync(
            searchTerm: term,
            recursive: true,
            limit: limit,
            includeItemTypes: includeItemTypes,
            sortBy: "SortName",
            cancellationToken: cancellationToken);

    /// <summary>计数（<c>GET /Items/Counts</c>）。</summary>
    public async Task<Dictionary<string, long>> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        var result = await Http.GetAsync(
            Uri("/Items/Counts", new Dictionary<string, string>(StringComparer.Ordinal) { ["UserId"] = UserId }),
            DefaultHeaders,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var json = result.JsonMap;
        if (json.HasValue && json.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in json.Value.EnumerateObject())
            {
                counts[p.Name] = JsonRead.IntOrNull(json, p.Name) ?? 0;
            }
        }
        return counts;
    }

    // ── 图片 / 字幕 / 取流 ─────────────────────────────────────────────────

    /// <summary>图片地址（<c>/Items/{id}/Images/{type}</c>，带 <c>api_key</c>）。</summary>
    /// <summary>
    /// 该条目**有图可拿**时才给 URL，否则回 <see cref="string.Empty"/>（t70 加性 API；t78 补第二级回退）。
    /// 两级口径（**唯一入口**，App 侧不要再各写一套回退）：
    ///   ① 自家 <c>ImageTags[type]</c>（有 tag 才算有图）⇒ <c>/Items/{id}/Images/{type}?tag=…</c>；
    ///   ② <c>type == Primary</c> 且条目挂在剧下：**判据直接取自模型层的唯一决定点**
    ///      <see cref="EmbyItem.PrimaryImageTag"/>（<c>EmbyModels.cs:259-267</c>：自家 Primary tag ⇒ 否则
    ///      <c>IsEpisode</c> ⇒ <c>SeriesPrimaryImageTag</c> ⇒ 否则空串）——当它给出的 tag **不是**自家 tag 时，
    ///      说明 tag 来自剧 ⇒ **必须换 id**（实测：自家 id + 剧 tag 仍然 500）⇒ <c>/Items/{SeriesId}/Images/Primary?tag=…</c>；
    ///   ③ 都没有 ⇒ 空串（**不发请求**）。
    /// 这样"用哪个 tag"只有模型层一个答案，本方法只负责"用哪个 id 去取"。
    ///
    /// 为什么需要 ①：实测无 <c>ImageTags.Primary</c> 的条目请求 <c>/Items/{id}/Images/Primary</c> 会被服务端
    /// 以 **HTTP 500**（不是 404）回答（t70）。
    /// 为什么需要 ②（t78 扩样复核，用户当面报「全部图片都有问题」）：t70 的抽样只见 3/20 的规模，
    /// 实测真机上**整行 11/20 挂**。逐条分类（ServerA，4 个媒体库共 80 条）得到：无自家 Primary 的 **13 条全部是
    /// <c>Episode</c>**，且 13/13 都有 <c>SeriesId</c>+<c>SeriesPrimaryImageTag</c>；形态对照 =
    /// 自家 id 不带 tag ⇒ **500**、自家 id 带剧 tag ⇒ **500**（tag 不是原因）、
    /// **剧 id（带/不带 tag）⇒ 200｜109,790 B 等**；控制组（自家有图）⇒ 200。
    /// 证据 = <c>shell/Tests/evidence/t78-image-500-mechanism.txt</c>。
    /// </summary>
    public string ImageUrlIfAvailable(EmbyItem item, string type = "Primary", int? maxHeight = null, int? maxWidth = null, string tag = null)
    {
        if (item == null || string.IsNullOrEmpty(item.Id)) return string.Empty;
        var key = string.IsNullOrEmpty(type) ? "Primary" : type;

        var ownTag = item.ImageTags != null && item.ImageTags.TryGetValue(key, out var availableTag) ? availableTag : null;
        if (!string.IsNullOrEmpty(ownTag))
        {
            return ImageUrl(item.Id, key, maxHeight: maxHeight, maxWidth: maxWidth, tag: string.IsNullOrEmpty(tag) ? ownTag : tag);
        }

        // ② 只有当模型层判据说"这个条目有主图 tag"、且它给的不是自家 tag 时，才换 id 去剧上取。
        //    额外只加两个构造 URL 必需/自保的守卫：SeriesId 非空、且必须**换 id**（SeriesId == item.Id 时回退等于原地重试 ⇒ 仍会 500）。
        if (string.Equals(key, "Primary", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(item.SeriesId)
            && !string.Equals(item.SeriesId, item.Id, StringComparison.Ordinal))
        {
            var modelTag = item.PrimaryImageTag;
            if (!string.IsNullOrEmpty(modelTag))
            {
                return ImageUrl(item.SeriesId, "Primary", maxHeight: maxHeight, maxWidth: maxWidth,
                    tag: string.IsNullOrEmpty(tag) ? modelTag : tag);
            }
        }

        // ④ Backdrop（t76 同批追加）：背景图这条链**不是** `ImageTags` 里的键 —— 实测（t84，真机 180 条）
        //    **172 条有自家背景图，全部藏在 `BackdropImageTags`（列表）里**，`ImageTags["Backdrop"]` 命中 0
        //    ⇒ 若只认 `ImageTags`，`ui` 一旦改用本方法取 backdrop 就会**丢掉 172/180 的背景图**（真回归）。
        //    故这里补：自家 `BackdropImageTags` → 否则父级（`ParentBackdropItemId` + `ParentBackdropImageTags`，
        //    必须**换 id 且带父的 tag**）→ 否则空串。
        //    ⚠️ 刻意**不**走上面 Primary 的 `IsEpisode` 分支：模型 `EmbyModels.cs:260-267` 只对 Primary 定义回退。
        if (string.Equals(key, "Backdrop", StringComparison.OrdinalIgnoreCase))
        {
            var ownBackdropTag = FirstNonEmpty(item.BackdropImageTags);
            if (!string.IsNullOrEmpty(ownBackdropTag))
            {
                return ImageUrl(item.Id, "Backdrop", maxHeight: maxHeight, maxWidth: maxWidth,
                    tag: string.IsNullOrEmpty(tag) ? ownBackdropTag : tag);
            }

            if (!string.IsNullOrEmpty(item.ParentBackdropItemId)
                && !string.Equals(item.ParentBackdropItemId, item.Id, StringComparison.Ordinal))
            {
                var parentTag = FirstNonEmpty(item.ParentBackdropImageTags);
                if (!string.IsNullOrEmpty(parentTag))
                {
                    return ImageUrl(item.ParentBackdropItemId, "Backdrop", maxHeight: maxHeight, maxWidth: maxWidth,
                        tag: string.IsNullOrEmpty(tag) ? parentTag : tag);
                }
            }
        }

        // ③ 都没有 ⇒ 空串（**不发请求**）。t78-a（captain 要求）：留一行**可观测的缺口计数**，
        //    以便"要不要第四级回退"将来用**数字**判而不是靠猜。限速：同一"类别"只落一条（避免整屏刷屏）。
        LogNoImageOnce(item, key);
        return string.Empty;
    }

    /// <summary>取列表里第一个非空串（t76：`ParentBackdropImageTags` 可能有多个候选 tag）。</summary>
    private static string FirstNonEmpty(System.Collections.Generic.List<string> values)
    {
        if (values == null) return null;
        foreach (var v in values)
        {
            if (!string.IsNullOrEmpty(v)) return v;
        }
        return null;
    }

    /// <summary>已落过 `IMG-NO-IMAGE` 的"类别"桶（进程级；键 = type|有无 SeriesId|有无 SeriesTag|itemType）。</summary>
    private static readonly System.Collections.Generic.HashSet<string> NoImageLogged =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

    private void LogNoImageOnce(EmbyItem item, string type)
    {
        var hasSeriesId = !string.IsNullOrEmpty(item.SeriesId);
        var hasSeriesTag = !string.IsNullOrEmpty(item.SeriesPrimaryImageTag);
        var bucket = type + "|" + (hasSeriesId ? "1" : "0") + "|" + (hasSeriesTag ? "1" : "0") + "|" + (item.Type ?? string.Empty);
        bool first;
        lock (NoImageLogged) first = NoImageLogged.Add(bucket);
        if (!first) return;
        Log($"IMG-NO-IMAGE itemId={item.Id} type={type} hasSeriesId={hasSeriesId} hasSeriesTag={hasSeriesTag} itemType={item.Type}");
    }

    /// <summary>逐条变体的 500 根因在 <c>t70</c>：见 <see cref="ImageUrlIfAvailable"/>。</summary>
    public string ImageUrl(string itemId, string type = "Primary", int? maxHeight = null, int? maxWidth = null, int? index = null, string tag = null)
    {
        var path = index == null
            ? $"/Items/{itemId}/Images/{type}"
            : $"/Items/{itemId}/Images/{type}/{index.Value}";

        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (maxHeight.HasValue) query["maxHeight"] = maxHeight.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (maxWidth.HasValue) query["maxWidth"] = maxWidth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(tag)) query["tag"] = tag;
        query["api_key"] = Token;

        return Uri(path, query);
    }

    /// <summary>外挂字幕地址（<c>/Videos/{id}/{mediaSourceId}/Subtitles/{index}/Stream.{format}</c>）。</summary>
    public string SubtitleUrl(string itemId, string mediaSourceId, int streamIndex, string format = "srt")
        => Uri($"/Videos/{itemId}/{mediaSourceId}/Subtitles/{streamIndex}/Stream.{format}",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["api_key"] = Token });

    /// <summary>静态直连流地址 —— **首选**喂给内核 <c>--open=</c>（SERVICE_API §1.3 [S] 实证）。</summary>
    public string DirectStreamUrl(string itemId, string mediaSourceId = null, double? startSeconds = null,
        int? audioStreamIndex = null, int? subtitleStreamIndex = null)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["static"] = "true",
            ["api_key"] = Token,
        };
        if (!string.IsNullOrEmpty(mediaSourceId)) query["MediaSourceId"] = mediaSourceId;
        if (startSeconds.HasValue && startSeconds.Value > 0)
            query["StartTimeTicks"] = TextUtils.SecondsToTicks(startSeconds.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (audioStreamIndex.HasValue) query["AudioStreamIndex"] = audioStreamIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (subtitleStreamIndex.HasValue) query["SubtitleStreamIndex"] = subtitleStreamIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Uri($"/Videos/{itemId}/stream", query);
    }

    /// <summary>
    /// HLS 转码地址（<c>/videos/{id}/master.m3u8</c>）。
    /// 🔴 t67（**超出原版的修复**）：必须带 <c>PlaySessionId</c>，否则服务端把子清单里的分片 URL 回显成
    /// <c>hls1/main/0.ts?PlaySessionId=</c>（空）⇒ 分片请求 **400**，整条转码链路播不了。
    /// 证据：t59 实测产品自拼 URL 在 6/6 台可连服务器上分片全 400；补上该参数后 ServerB/ServerA 立刻取到真
    /// <c>video/mp2t</c> 字节。原版 Dart <c>transcodeUrl</c> 同样不带该参数（**共同缺陷，非移植回归**）。
    /// </summary>
    /// <param name="playSessionId">
    /// 取值优先级由调用方（<see cref="AIPlayer.Shell.Player.EmbyPlaybackSession"/>）决定：**服务端 PlaybackInfo
    /// 顶层下发的那个**（最稳，它就是转码会话的钥匙）→ 否则用外壳自己的会话 id（它已在 <c>/Sessions/Playing</c> 上报里用过）。
    /// 为空则不加参数（= 旧行为；此时分片必然 400，调用方应留可见日志）。
    /// </param>
    public string TranscodeUrl(string itemId, string mediaSourceId = null, string playSessionId = null)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["api_key"] = Token };
        if (!string.IsNullOrEmpty(mediaSourceId)) query["MediaSourceId"] = mediaSourceId;
        if (!string.IsNullOrWhiteSpace(playSessionId)) query["PlaySessionId"] = playSessionId.Trim();
        return Uri($"/videos/{itemId}/master.m3u8", query);
    }

    /// <summary>
    /// 取 PlaybackInfo 响应**顶层**的服务端 <c>PlaySessionId</c>（t67）。
    /// 实测口径（t67 探针）：`PlaybackInfoAsync` 会把整个响应体深克隆进 <see cref="EmbyItem.Raw"/> ⇒ 该字段
    /// **在 Raw 里是有的**，此前的问题是「没有类型化出口 + <c>TranscodeUrl</c> 从不发它」，而不是数据被丢掉。
    /// 返回空串表示服务端未下发（调用方回落外壳自身的会话 id）。
    /// </summary>
    public static string ServerPlaySessionIdOf(EmbyItem item)
    {
        var node = item?.Raw?["PlaySessionId"];
        if (node == null) return string.Empty;
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text ?? string.Empty;
        return node.ToString().Trim('"');
    }

    /// <summary>极简 <c>DeviceProfile</c>：声明可直接播放，避免服务端无谓转码。</summary>
    private static JsonObject DeviceProfile()
    {
        var o = new JsonObject
        {
            ["Name"] = "AI Player",
            ["MaxStreamingBitrate"] = 200000000,
        };

        o["DirectPlayProfiles"] = new JsonArray
        {
            new JsonObject
            {
                ["Container"] = "mkv,mp4,ts,m2ts,avi,mov,flv,wmv,webm,mp3,flac,aac,m4a,opus,ogg,wav",
                ["Type"] = "Video",
                ["VideoCodec"] = "h264,hevc,av1,vp9,mpeg2video,vc1",
                ["AudioCodec"] = "aac,ac3,eac3,dts,truehd,flac,mp3,opus,vorbis,pcm",
            },
            new JsonObject
            {
                ["Container"] = "mp3,flac,aac,m4a,opus,ogg,wav,wma",
                ["Type"] = "Audio",
                ["AudioCodec"] = "mp3,flac,aac,opus,vorbis,pcm",
            },
        };

        o["TranscodingProfiles"] = new JsonArray
        {
            new JsonObject
            {
                ["Container"] = "ts",
                ["Type"] = "Video",
                ["VideoCodec"] = "h264",
                ["AudioCodec"] = "aac,mp3",
                ["Protocol"] = "hls",
                ["Context"] = "Streaming",
            },
        };

        o["SubtitleProfiles"] = new JsonArray
        {
            new JsonObject { ["Format"] = "srt", ["Method"] = "External" },
            new JsonObject { ["Format"] = "ass", ["Method"] = "External" },
            new JsonObject { ["Format"] = "ssa", ["Method"] = "External" },
        };

        return o;
    }

    /// <summary>
    /// 播放信息：优先 <c>POST /Items/{id}/PlaybackInfo</c>；失败自动降级 <c>GET /Items/{id}?Fields=MediaSources</c>。
    /// 降级并非可选优化 —— 原版字符串明确记录了该分支：
    /// <i>"Proxy may be stripping the POST body. Retrying via GET /Items/{id}?Fields=MediaSources."</i>
    /// </summary>
    public async Task<EmbyItem> PlaybackInfoAsync(
        string itemId,
        string mediaSourceId = null,
        int? audioStreamIndex = null,
        int? subtitleStreamIndex = null,
        double? startSeconds = null,
        bool autoOpenLiveStream = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserId"] = UserId,
                ["AutoOpenLiveStream"] = autoOpenLiveStream ? "true" : "false",
                ["IsPlayback"] = "true",
            };
            if (startSeconds.HasValue)
                query["StartTimeTicks"] = TextUtils.SecondsToTicks(startSeconds.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var body = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["UserId"] = UserId,
                ["EnableDirectPlay"] = true,
                ["EnableDirectStream"] = true,
                ["EnableTranscoding"] = true,
                ["AutoOpenLiveStream"] = autoOpenLiveStream,
            };
            if (!string.IsNullOrEmpty(mediaSourceId)) body["MediaSourceId"] = mediaSourceId;
            if (audioStreamIndex.HasValue) body["AudioStreamIndex"] = audioStreamIndex.Value;
            if (subtitleStreamIndex.HasValue) body["SubtitleStreamIndex"] = subtitleStreamIndex.Value;
            if (startSeconds.HasValue) body["StartTimeTicks"] = TextUtils.SecondsToTicks(startSeconds.Value);

            var result = await Http.PostJsonAsync(Uri($"/Items/{itemId}/PlaybackInfo", query), body, DefaultHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (json.HasValue && JsonRead.Prop(json, "MediaSources")?.ValueKind == JsonValueKind.Array)
            {
                var merged = JsonRead.DeepClone(json.Value);
                if (!merged.ContainsKey("Id")) merged["Id"] = itemId;
                var mergedElement = JsonRead.FromNode(merged.ToJsonString());
                return mergedElement.HasValue ? EmbyItem.FromJson(mergedElement.Value) : null;
            }
        }
        catch (Exception ex)
        {
            Log($"PlaybackInfo 失败，降级 GET /Items/{{id}}?Fields=MediaSources：{ex.Message}");
        }

        try
        {
            var fallback = await Http.GetAsync(
                Uri($"/Items/{itemId}", new Dictionary<string, string>(StringComparer.Ordinal) { ["Fields"] = "MediaSources,MediaStreams" }),
                DefaultHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var json = fallback.JsonMap;
            return json.HasValue ? EmbyItem.FromJson(json.Value) : null;
        }
        catch (Exception ex)
        {
            Log($"降级取 MediaSources 亦失败：{ex.Message}");
            return null;
        }
    }

    // ── 播放会话上报 ───────────────────────────────────────────────────────

    /// <summary><c>POST /Sessions/Playing</c> —— 内核成功开播后调用。</summary>
    public async Task ReportPlaybackStartAsync(
        string itemId,
        string playSessionId,
        string mediaSourceId = null,
        double positionSeconds = 0,
        int audioStreamIndex = -1,
        int subtitleStreamIndex = -1,
        bool isPaused = false,
        bool isMuted = false,
        double playbackRate = 1.0,
        int? volumeLevel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = PlaybackPayload(itemId, playSessionId, positionSeconds, audioStreamIndex, subtitleStreamIndex,
                isPaused, isMuted, playbackRate, mediaSourceId, volumeLevel, "DirectStream");
            payload["CanSeek"] = true;

            await Http.PostJsonAsync(
                Uri("/Sessions/Playing", new Dictionary<string, string>(StringComparer.Ordinal) { ["api_key"] = Token }),
                payload,
                DefaultHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Sessions/Playing 上报失败（不阻塞播放）：{ex.Message}");
        }
    }

    /// <summary><c>POST /Sessions/Playing/Progress</c> —— 内核 <c>progress</c> 回调（实测约 0.5 s 一次）。</summary>
    public async Task ReportPlaybackProgressAsync(
        string itemId,
        string playSessionId,
        double positionSeconds,
        bool isPaused,
        string mediaSourceId = null,
        int audioStreamIndex = -1,
        int subtitleStreamIndex = -1,
        bool isMuted = false,
        double playbackRate = 1.0,
        int? volumeLevel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = PlaybackPayload(itemId, playSessionId, positionSeconds, audioStreamIndex, subtitleStreamIndex,
                isPaused, isMuted, playbackRate, mediaSourceId, volumeLevel);
            payload["EventName"] = "timeupdate";

            await Http.PostJsonAsync(
                Uri("/Sessions/Playing/Progress", new Dictionary<string, string>(StringComparer.Ordinal) { ["api_key"] = Token }),
                payload,
                DefaultHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Sessions/Playing/Progress 上报失败（不阻塞播放）：{ex.Message}");
        }
    }

    /// <summary><c>POST /Sessions/Playing/Stopped</c> —— 内核 <c>stopped</c> 回调。</summary>
    public async Task ReportPlaybackStoppedAsync(
        string itemId,
        string playSessionId,
        double positionSeconds,
        string mediaSourceId = null,
        bool isPaused = false,
        bool isMuted = false,
        double playbackRate = 1.0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = PlaybackPayload(itemId, playSessionId, positionSeconds, -1, -1,
                isPaused, isMuted, playbackRate, mediaSourceId, null);

            await Http.PostJsonAsync(
                Uri("/Sessions/Playing/Stopped", new Dictionary<string, string>(StringComparer.Ordinal) { ["api_key"] = Token }),
                payload,
                DefaultHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Sessions/Playing/Stopped 上报失败（不阻塞播放）：{ex.Message}");
        }
    }

    /// <summary>秒 → ticks 的换算集中在此处（内核给**秒**，Emby 要 **ticks**）。</summary>
    private static JsonObject PlaybackPayload(
        string itemId,
        string playSessionId,
        double positionSeconds,
        int audioStreamIndex,
        int subtitleStreamIndex,
        bool isPaused,
        bool isMuted,
        double playbackRate,
        string mediaSourceId,
        int? volumeLevel,
        string playMethod = "")
    {
        var o = new JsonObject
        {
            ["ItemId"] = itemId,
            ["PlaySessionId"] = playSessionId,
            ["PositionTicks"] = TextUtils.SecondsToTicks(positionSeconds),
            ["IsPaused"] = isPaused,
            ["IsMuted"] = isMuted,
            ["PlaybackRate"] = playbackRate,
        };
        if (!string.IsNullOrEmpty(mediaSourceId)) o["MediaSourceId"] = mediaSourceId;
        if (playMethod.Length > 0) o["PlayMethod"] = playMethod;
        if (volumeLevel.HasValue) o["VolumeLevel"] = volumeLevel.Value;
        if (audioStreamIndex >= 0) o["AudioStreamIndex"] = audioStreamIndex;
        if (subtitleStreamIndex >= 0) o["SubtitleStreamIndex"] = subtitleStreamIndex;
        return o;
    }

    // ── 收藏 / 已看 / 隐藏 ─────────────────────────────────────────────────

    /// <summary>收藏（<c>POST|DELETE /Users/{userId}/FavoriteItems/{id}</c>）。</summary>
    public Task<bool> SetFavoriteAsync(string itemId, bool favorite, CancellationToken cancellationToken = default)
        => ToggleAsync($"/Users/{UserId}/FavoriteItems/{itemId}", favorite, cancellationToken);

    /// <summary>已看（<c>POST|DELETE /Users/{userId}/PlayedItems/{id}</c>）。</summary>
    public Task<bool> SetPlayedAsync(string itemId, bool played, CancellationToken cancellationToken = default)
        => ToggleAsync($"/Users/{UserId}/PlayedItems/{itemId}", played, cancellationToken);

    /// <summary>从「继续观看」隐藏（<c>POST /Users/{userId}/HideFromResume</c>，原版字符串实证）。</summary>
    public async Task<bool> SetHideFromResumeAsync(string itemId, bool hide, CancellationToken cancellationToken = default)
    {
        try
        {
            await Http.SendAsync(
                "POST",
                Uri("/Users/" + UserId + "/HideFromResume", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ItemId"] = itemId,
                    ["Hide"] = hide ? "true" : "false",
                }),
                DefaultHeaders,
                new Dictionary<string, object>(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"HideFromResume 失败: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ToggleAsync(string path, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            var url = Uri(path);
            if (enabled)
            {
                await Http.PostJsonAsync(url, new Dictionary<string, object>(), DefaultHeaders, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await Http.DeleteAsync(url, DefaultHeaders, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Log($"{path} 操作失败: {ex.Message}");
            return false;
        }
    }

    // ── 媒体段（跳过片头/片尾的原生来源）───────────────────────────────────

    /// <summary>
    /// <c>GET /MediaSegments/{itemId}</c> → 内核 <c>--segment=</c> 结构。
    /// Emby 的 <c>Type</c> ∈ Intro | Outro | Recap | Preview | Commercial，时间单位 **ticks**，内核要 **毫秒**（HOST_CONTRACT §3.6）。
    /// </summary>
    public async Task<List<MediaSegmentDto>> GetMediaSegmentsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.GetAsync(Uri($"/MediaSegments/{itemId}"), DefaultHeaders, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var raw = JsonRead.Items(result.JsonMap, "Items");
            if (raw.Count == 0)
            {
                var list = result.Json;
                if (list.HasValue && list.Value.ValueKind == JsonValueKind.Array)
                {
                    raw = list.Value.EnumerateArray().ToList();
                }
            }

            var segments = new List<MediaSegmentDto>();
            foreach (var element in JsonRead.Objects(raw))
            {
                var segment = SegmentFromJson(element);
                if (segment != null) segments.Add(segment);
            }
            return segments;
        }
        catch (Exception ex)
        {
            Log($"MediaSegments 获取失败（服务端可能不支持该插件）: {ex.Message}");
            return new List<MediaSegmentDto>();
        }
    }

    private static MediaSegmentDto SegmentFromJson(JsonElement json)
    {
        var type = JsonRead.Str(json, "Type").ToLowerInvariant();
        var startTicks = JsonRead.LongOrNull(json, "StartTicks") ?? 0;
        var endTicks = JsonRead.LongOrNull(json, "EndTicks");
        if (startTicks <= 0 && !endTicks.HasValue) return null;

        return new MediaSegmentDto
        {
            Type = MediaSegmentDto.ParseType(type),
            StartMs = (long)Math.Round(startTicks / 10000d, MidpointRounding.AwayFromZero),
            EndMs = endTicks.HasValue ? (long)Math.Round(endTicks.Value / 10000d, MidpointRounding.AwayFromZero) : (long?)null,
            Source = "emby",
        };
    }
}

// 本地 mock Emby 服务器：端点/头/参数形态严格按 reversed/FlutterApp/SERVICE_API.md §1 构造。
// 用途：无真实服务器时端到端验证（WORKSPACE.md §3）；**mock 通过 ≠ 真服务器通过**，任何报告须如实标注。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Mock;

/// <summary>Emby 端点的内存实现（含 X-Emby-Token 校验与媒体段）。</summary>
public sealed class MockEmbyServer : IDisposable
{
    public const string ValidUser = "demo";
    public const string ValidPassword = "demo-password";
    public const string AccessToken = "mock-access-token-0001";
    public const string UserId = "user-1";
    public const string ServerId = "mock-server-id";
    public const string MovieId = "movie-1";
    public const string SeriesId = "series-1";
    public const string EpisodeId = "episode-1";
    public const string MoviesViewId = "view-movies";
    public const string TvViewId = "view-tv";

    private readonly MockHttpServer _server;

    public MockEmbyServer()
    {
        _server = new MockHttpServer(Handle);
    }

    public string BaseUrl => _server.BaseUrl;

    public int Port => _server.Port;

    public IReadOnlyList<MockRequest> Requests => _server.Requests;

    /// <summary>鉴权失败的请求数（用于断言 Token 头确实被带上）。</summary>
    public int UnauthorizedCount { get; private set; }

    public MockEmbyServer Start()
    {
        _server.Start();
        return this;
    }

    public int CountRequests(string method, string pathPrefix)
        => Requests.Count(r => string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase)
                               && r.Path.StartsWith(pathPrefix, StringComparison.Ordinal));

    private MockResponse Handle(MockRequest request)
    {
        var path = request.Path;
        var method = request.Method.ToUpperInvariant();

        // 免鉴权端点
        if (method == "POST" && path == "/Users/AuthenticateByName") return Authenticate(request);
        if (method == "GET" && path == "/System/Info/Public") return PublicInfo();
        if (method == "GET" && path == "/System/Ext/ServerDomains") return MockResponse.Json("[\"http://lan.example:8096\"]");

        // 余下端点都需要 X-Emby-Token（SERVICE_API §1.1）
        var token = request.Header("X-Emby-Token");
        if (token == null) token = request.QueryValue("api_key");
        if (!string.Equals(token, AccessToken, StringComparison.Ordinal))
        {
            UnauthorizedCount++;
            return MockResponse.Json("{\"error\":\"unauthorized\"}", 401);
        }

        if (path.StartsWith("/Videos/", StringComparison.Ordinal) && path.EndsWith("/stream", StringComparison.Ordinal))
        {
            return new MockResponse
            {
                StatusCode = 200,
                ContentType = "video/mp4",
                Body = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, // 伪流（仅证明地址可取）
            };
        }

        if (path.StartsWith("/MediaSegments/", StringComparison.Ordinal)) return MediaSegments();

        if (method == "POST" && path.StartsWith("/Items/", StringComparison.Ordinal) && path.EndsWith("/PlaybackInfo", StringComparison.Ordinal))
        {
            return PlaybackInfo();
        }

        if (path == $"/Users/{UserId}/Views") return Views();
        if (path == $"/Users/{UserId}/Items") return Items(request);
        if (path == $"/Users/{UserId}/Items/Resume") return Items(request);
        if (path == "/Items/Counts") return MockResponse.Json("{\"MovieCount\":1,\"SeriesCount\":1,\"EpisodeCount\":1}");

        if (path.StartsWith($"/Users/{UserId}/Items/", StringComparison.Ordinal)) return SingleItem(path);

        if (path.StartsWith($"/Users/{UserId}/FavoriteItems/", StringComparison.Ordinal)
            || path.StartsWith($"/Users/{UserId}/PlayedItems/", StringComparison.Ordinal))
        {
            return MockResponse.Json("{}");
        }

        if (path == $"/Users/{UserId}/HideFromResume") return MockResponse.Json("{}");

        if (path.StartsWith("/Sessions/Playing", StringComparison.Ordinal)) return MockResponse.Empty(204);

        return MockResponse.Json("{\"error\":\"not found\"}", 404);
    }

    private MockResponse Authenticate(MockRequest request)
    {
        var body = JsonRead.FromNode(request.Body);
        var username = JsonRead.Str(body, "Username");
        var password = JsonRead.Str(body, "Pw");

        if (username != ValidUser || password != ValidPassword)
        {
            return MockResponse.Json("{\"error\":\"invalid credentials\"}", 401);
        }

        var json = new JsonObject
        {
            ["AccessToken"] = AccessToken,
            ["ServerId"] = ServerId,
            ["User"] = new JsonObject
            {
                ["Id"] = UserId,
                ["Name"] = username,
            },
        };
        return MockResponse.Json(json.ToJsonString());
    }

    private static MockResponse PublicInfo() => MockResponse.Json(new JsonObject
    {
        ["ServerName"] = "Mock Emby",
        ["Version"] = "4.8.0.0",
        ["Id"] = ServerId,
    }.ToJsonString());

    private static MockResponse Views() => MockResponse.Json(new JsonObject
    {
        ["Items"] = new JsonArray
        {
            new JsonObject { ["Id"] = MoviesViewId, ["Name"] = "电影", ["CollectionType"] = "movies" },
            new JsonObject { ["Id"] = TvViewId, ["Name"] = "剧集", ["CollectionType"] = "tvshows" },
        },
        ["TotalRecordCount"] = 2,
    }.ToJsonString());

    private static MockResponse Items(MockRequest request)
    {
        var parentId = request.QueryValue("ParentId");
        var includeTypes = request.QueryValue("IncludeItemTypes") ?? string.Empty;
        var search = request.QueryValue("SearchTerm");

        var all = new List<JsonObject> { Movie(), series(), episode() };

        IEnumerable<JsonObject> filtered = all;
        if (!string.IsNullOrEmpty(parentId))
        {
            filtered = filtered.Where(i => i["Id"]?.GetValue<string>() == MovieId
                                           || i["SeriesId"]?.GetValue<string>() == parentId
                                           || i["Id"]?.GetValue<string>() == parentId);
        }
        if (includeTypes.Length > 0)
        {
            var types = includeTypes.Split(',').Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(i => types.Contains(JsonRead.Str(JsonRead.From(i), "Type")));
        }
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(i => (JsonRead.Str(JsonRead.From(i), "Name") ?? string.Empty)
                .Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        var arr = new JsonArray();
        foreach (var item in list) arr.Add(item);

        return MockResponse.Json(new JsonObject
        {
            ["Items"] = arr,
            ["TotalRecordCount"] = list.Count,
        }.ToJsonString());
    }

    private static MockResponse SingleItem(string path)
    {
        var id = path.Substring(path.LastIndexOf('/') + 1);
        var item = id switch
        {
            MovieId => Movie(),
            SeriesId => series(),
            EpisodeId => episode(),
            _ => null,
        };
        return item == null
            ? MockResponse.Json("{\"error\":\"not found\"}", 404)
            : MockResponse.Json(item.ToJsonString());
    }

    private static MockResponse PlaybackInfo() => MockResponse.Json(new JsonObject
    {
        ["MediaSources"] = new JsonArray
        {
            MediaSource("ms-1", "1080p", "mkv", 8000000),
            MediaSource("ms-2", "720p", "mp4", 3000000),
        },
    }.ToJsonString());

    private static MockResponse MediaSegments() => MockResponse.Json(new JsonObject
    {
        ["Items"] = new JsonArray
        {
            new JsonObject { ["Type"] = "Intro", ["StartTicks"] = 120000000, ["EndTicks"] = 1020000000 },
            new JsonObject { ["Type"] = "Outro", ["StartTicks"] = 3300000000L },
        },
    }.ToJsonString());

    // ── 数据集 ─────────────────────────────────────────────────────────────

    private static JsonObject Movie() => new JsonObject
    {
        ["Id"] = MovieId,
        ["Name"] = "示例电影",
        ["Type"] = "Movie",
        ["ProductionYear"] = 2024,
        ["RunTimeTicks"] = 6000000000L,
        ["Container"] = "mkv",
        ["Path"] = @"D:\media\demo.mkv",
        ["IsFolder"] = false,
        ["ImageTags"] = new JsonObject { ["Primary"] = "tag-primary" },
        ["ProviderIds"] = new JsonObject { ["Imdb"] = "tt0000001", ["Tmdb"] = "1" },
        ["UserData"] = new JsonObject
        {
            ["Played"] = false,
            ["PlaybackPositionTicks"] = 3000000000L, // > int.MaxValue：专门用于暴露 int 截断类缺陷
            ["IsFavorite"] = false,
            ["PlayedPercentage"] = 5.0,
        },
        ["MediaSources"] = new JsonArray
        {
            MediaSource("ms-1", "1080p", "mkv", 8000000),
            MediaSource("ms-2", "720p", "mp4", 3000000),
        },
    };

    private static JsonObject series() => new JsonObject
    {
        ["Id"] = SeriesId,
        ["Name"] = "示例剧集",
        ["Type"] = "Series",
        ["ImageTags"] = new JsonObject { ["Primary"] = "tag-series" },
        ["UserData"] = new JsonObject { ["Played"] = false },
    };

    private static JsonObject episode() => new JsonObject
    {
        ["Id"] = EpisodeId,
        ["Name"] = "第一集",
        ["Type"] = "Episode",
        ["SeriesId"] = SeriesId,
        ["SeriesName"] = "示例剧集",
        ["SeasonId"] = "season-1",
        ["SeasonName"] = "第 1 季",
        ["IndexNumber"] = 1,
        ["ParentIndexNumber"] = 1,
        ["RunTimeTicks"] = 2400000000L,
        ["SeriesPrimaryImageTag"] = "tag-series",
        ["UserData"] = new JsonObject { ["Played"] = false },
        ["MediaSources"] = new JsonArray { MediaSource("ms-ep1", "1080p", "mkv", 6000000) },
    };

    private static JsonObject MediaSource(string id, string name, string container, long bitrate) => new JsonObject
    {
        ["Id"] = id,
        ["Name"] = name,
        ["Container"] = container,
        ["Bitrate"] = bitrate,
        ["RunTimeTicks"] = 6000000000L,
        ["SupportsDirectPlay"] = true,
        ["Protocol"] = "File",
        ["MediaStreams"] = new JsonArray
        {
            new JsonObject { ["Index"] = 0, ["Type"] = "Video", ["Codec"] = "h264", ["Height"] = 1080, ["Width"] = 1920, ["IsDefault"] = true },
            new JsonObject { ["Index"] = 1, ["Type"] = "Audio", ["Codec"] = "aac", ["Language"] = "jpn", ["Channels"] = 2, ["IsDefault"] = true },
            new JsonObject { ["Index"] = 2, ["Type"] = "Subtitle", ["Codec"] = "srt", ["Language"] = "chi", ["IsDefault"] = false },
        },
    };

    public void Dispose() => _server.Dispose();
}

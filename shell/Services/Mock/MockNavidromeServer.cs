// 本地 mock Navidrome / Subsonic 服务器。
// 端点与参数形态按 reversed/FlutterApp/SERVICE_API.md §2 构造：
//   /rest/{method} + 参数 u/token/salt/f=json/v/c；token = md5(password + salt)（十六进制小写）
//   /auth/login 走 Navidrome 原生登录（X-ND-Authorization）
// 用途：无真实 Navidrome 时验证「Subsonic token+salt 鉴权」这条协议关键路径。
// **mock 通过 ≠ 真服务器通过。**

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Mock;

public sealed class MockNavidromeServer : IDisposable
{
    public const string ValidUser = "demo";
    public const string ValidPassword = "nd-secret";
    public const string NativeToken = "nd-native-token";
    public const string UserId = "nd-user-1";
    public const string AlbumId = "al-1";
    public const string SongId = "sg-1";

    private readonly MockHttpServer _server;

    public MockNavidromeServer()
    {
        _server = new MockHttpServer(Handle);
    }

    public string BaseUrl => _server.BaseUrl;

    public IReadOnlyList<MockRequest> Requests => _server.Requests;

    /// <summary>token/salt 校验失败的次数（应恒为 0 —— 非 0 即证明 token+salt 实现有误）。</summary>
    public int AuthFailureCount { get; private set; }

    public MockNavidromeServer Start()
    {
        _server.Start();
        return this;
    }

    private MockResponse Handle(MockRequest request)
    {
        var path = request.Path;

        if (request.Method == "POST" && path == "/auth/login")
        {
            var body = JsonRead.FromNode(request.Body);
            var user = JsonRead.Str(body, "username");
            if (user.Length == 0) user = JsonRead.Str(body, "userName");
            var password = JsonRead.Str(body, "password");
            if (user == ValidUser && password == ValidPassword)
            {
                return MockResponse.Json(new JsonObject
                {
                    ["token"] = NativeToken,
                    ["userId"] = UserId,
                    ["username"] = ValidUser,
                    ["isAdmin"] = false,
                }.ToJsonString());
            }
            return MockResponse.Json("{\"error\":\"invalid credentials\"}", 401);
        }

        if (!path.StartsWith("/rest/", StringComparison.Ordinal))
        {
            return MockResponse.Json("{\"error\":\"not found\"}", 404);
        }

        var method = path.Substring("/rest/".Length);

        // Subsonic token+salt 校验（除 ping 外全部要求；ping 也校验以便暴露实现错误）
        var u = request.QueryValue("u");
        var token = request.QueryValue("t") ?? request.QueryValue("token");
        var salt = request.QueryValue("s") ?? request.QueryValue("salt");
        var expected = Md5Hex(ValidPassword + salt);
        if (u != ValidUser || string.IsNullOrEmpty(salt) || !string.Equals(token, expected, StringComparison.OrdinalIgnoreCase))
        {
            AuthFailureCount++;
            return MockResponse.Json(new JsonObject
            {
                ["subsonic-response"] = new JsonObject
                {
                    ["status"] = "failed",
                    ["version"] = "1.16.1",
                    ["error"] = new JsonObject { ["code"] = 40, ["message"] = "Wrong username or password" },
                },
            }.ToJsonString());
        }

        switch (method)
        {
            case "ping":
                return Ok(new JsonObject { ["version"] = "1.16.1" });

            case "getAlbumList2":
                return Ok(new JsonObject
                {
                    ["albumList2"] = new JsonObject
                    {
                        ["album"] = new JsonArray
                        {
                            Album("al-1", "示例专辑一", 2024),
                            Album("al-2", "示例专辑二", 2023),
                        },
                    },
                });

            case "getAlbum":
                return Ok(new JsonObject
                {
                    ["album"] = new JsonObject
                    {
                        ["id"] = AlbumId,
                        ["name"] = "示例专辑一",
                        ["artist"] = "示例艺术家",
                        ["year"] = 2024,
                        ["songCount"] = 1,
                        ["duration"] = 245,
                        ["song"] = new JsonArray { Song() },
                    },
                });

            case "getArtists":
                return Ok(new JsonObject
                {
                    ["artists"] = new JsonObject
                    {
                        ["index"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["name"] = "S",
                                ["artist"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "ar-1", ["name"] = "示例艺术家", ["albumCount"] = 2 },
                                },
                            },
                        },
                    },
                });

            case "getStarred2":
                return Ok(new JsonObject
                {
                    ["starred2"] = new JsonObject
                    {
                        ["song"] = new JsonArray { Song() },
                        ["album"] = new JsonArray { Album(AlbumId, "示例专辑一", 2024) },
                    },
                });

            case "getPlaylists":
                return Ok(new JsonObject
                {
                    ["playlists"] = new JsonObject
                    {
                        ["playlist"] = new JsonArray
                        {
                            new JsonObject { ["id"] = "pl-1", ["name"] = "示例歌单", ["songCount"] = 12, ["duration"] = 3600 },
                        },
                    },
                });

            case "getPlaylist":
                return Ok(new JsonObject
                {
                    ["playlist"] = new JsonObject
                    {
                        ["id"] = "pl-1",
                        ["name"] = "示例歌单",
                        ["entry"] = new JsonArray { Song() },
                    },
                });

            case "getRandomSongs":
                return Ok(new JsonObject
                {
                    ["randomSongs"] = new JsonObject { ["song"] = new JsonArray { Song() } },
                });

            case "search3":
                return Ok(new JsonObject
                {
                    ["searchResult3"] = new JsonObject
                    {
                        ["artist"] = new JsonArray { new JsonObject { ["id"] = "ar-1", ["name"] = "示例艺术家" } },
                        ["album"] = new JsonArray { Album(AlbumId, "示例专辑一", 2024) },
                        ["song"] = new JsonArray { Song() },
                    },
                });

            case "star":
            case "unstar":
            case "scrobble":
                return Ok(new JsonObject());

            case "getLyricsBySongId":
                return Ok(new JsonObject
                {
                    ["lyricsList"] = new JsonObject
                    {
                        ["structuredLyrics"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["lang"] = "zho",
                                ["synced"] = true,
                                ["line"] = new JsonArray
                                {
                                    new JsonObject { ["start"] = 0L, ["value"] = "第一行" },
                                    new JsonObject { ["start"] = 5000L, ["value"] = "第二行" },
                                },
                            },
                        },
                    },
                });

            case "stream":
                return new MockResponse { StatusCode = 200, ContentType = "audio/mpeg", Body = new byte[] { 0xFF, 0xFB, 0x90, 0x00 } };

            default:
                return MockResponse.Json(new JsonObject
                {
                    ["subsonic-response"] = new JsonObject
                    {
                        ["status"] = "failed",
                        ["version"] = "1.16.1",
                        ["error"] = new JsonObject { ["code"] = 70, ["message"] = $"unknown method {method}" },
                    },
                }.ToJsonString());
        }
    }

    private static MockResponse Ok(JsonObject payload)
        => MockResponse.Json(new JsonObject
        {
            ["subsonic-response"] = Merge(new JsonObject { ["status"] = "ok", ["version"] = "1.16.1" }, payload),
        }.ToJsonString());

    private static JsonObject Merge(JsonObject head, JsonObject tail)
    {
        foreach (var kv in tail.ToList())
        {
            tail.Remove(kv.Key);
            head[kv.Key] = kv.Value;
        }
        return head;
    }

    private static JsonObject Album(string id, string name, int year) => new JsonObject
    {
        ["id"] = id,
        ["name"] = name,
        ["artist"] = "示例艺术家",
        ["artistId"] = "ar-1",
        ["year"] = year,
        ["songCount"] = 1,
        ["duration"] = 245,
        ["coverArt"] = "cover-" + id,
        ["created"] = "2024-01-01T00:00:00.000Z",
    };

    private static JsonObject Song() => new JsonObject
    {
        ["id"] = SongId,
        ["title"] = "示例歌曲",
        ["album"] = "示例专辑一",
        ["albumId"] = AlbumId,
        ["artist"] = "示例艺术家",
        ["duration"] = 245,
        ["track"] = 1,
        ["discNumber"] = 1,
        ["suffix"] = "mp3",
        ["bitRate"] = 320,
        ["size"] = 9840000L,
        ["contentType"] = "audio/mpeg",
        ["coverArt"] = "cover-" + AlbumId,
        ["path"] = "Music/示例/01 示例歌曲.mp3",
    };

    /// <summary>独立实现（不调用被测方的 Md5Hex），避免「用被测代码验证被测代码」。</summary>
    public static string Md5Hex(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public void Dispose() => _server.Dispose();
}

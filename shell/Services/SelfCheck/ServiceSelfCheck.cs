// 服务层端到端自检（t7 验收⑤）：无真服务器时用内置 mock 复现
//   认证 → 列库 → 取详情 → 生成可播放流地址 → 播放信息/媒体段 → 进度上报 → 收藏切换 → 凭据与配置持久化。
// 运行方式：由测试工程或临时宿主调用 ServiceSelfCheck.RunAsync()（服务层本身是类库，不承担进程入口）。
// 诚实边界：本自检走的是 **mock 服务器**（mock 通过 ≠ 真服务器通过，报告须如实标注）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Aggregation;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.ExternalMpv;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Mock;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Navidrome;
using AIPlayer.Shell.Services.Playback;
using AIPlayer.Shell.Services.Segments;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Todb;
using AIPlayer.Shell.Services.Util;
using AIPlayer.Shell.Services.WebDav;

namespace AIPlayer.Shell.Services.SelfCheck;

public sealed class SelfCheckStep
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class SelfCheckReport
{
    public List<SelfCheckStep> Steps { get; } = new List<SelfCheckStep>();

    public int Passed => Steps.Count(s => s.Passed);

    public int Failed => Steps.Count(s => !s.Passed);

    public bool AllPassed => Failed == 0;

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"self-check: {Passed} passed / {Failed} failed / {Steps.Count} total  => {(AllPassed ? "PASS" : "FAIL")}");
        foreach (var step in Steps)
        {
            sb.AppendLine($"  [{(step.Passed ? "PASS" : "FAIL")}] {step.Name} :: {step.Detail}");
        }
        return sb.ToString();
    }
}

/// <summary>服务层自检驱动器。</summary>
public static class ServiceSelfCheck
{
    public static async Task<SelfCheckReport> RunAsync(Action<string> write = null, CancellationToken cancellationToken = default)
    {
        var report = new SelfCheckReport();
        var log = new List<string>();

        void Say(string message)
        {
            log.Add(message);
            write?.Invoke(message);
        }

        void Step(string name, bool passed, string detail)
        {
            report.Steps.Add(new SelfCheckStep { Name = name, Passed = passed, Detail = detail });
            Say($"{(passed ? "PASS" : "FAIL")}  {name}  ::  {detail}");
        }

        using var mock = new MockEmbyServer().Start();
        Say($"mock Emby 已启动：{mock.BaseUrl}（端点形态依据 SERVICE_API.md §1）");

        using var http = new ShellHttpClient();

        // ① 认证
        try
        {
            var auth = await EmbyService.LoginAsync(http, mock.BaseUrl, MockEmbyServer.ValidUser, MockEmbyServer.ValidPassword, cancellationToken: cancellationToken);
            Step("认证 POST /Users/AuthenticateByName", auth.AccessToken == MockEmbyServer.AccessToken && auth.UserId == MockEmbyServer.UserId,
                MaskAccessTokenPair(auth.AccessToken) + $", userId={auth.UserId}, userName={auth.UserName}");
        }
        catch (Exception ex)
        {
            Step("认证 POST /Users/AuthenticateByName", false, ex.Message);
            return report;
        }

        // 错误口令必须被拒（证明 mock 真的在验，不是无脑 200）
        try
        {
            await EmbyService.LoginAsync(http, mock.BaseUrl, MockEmbyServer.ValidUser, "wrong", cancellationToken: cancellationToken);
            Step("错误口令被拒", false, "预期抛 ShellHttpException，但登录成功了");
        }
        catch (ShellHttpException ex)
        {
            Step("错误口令被拒", ex.StatusCode == 401, $"status={ex.StatusCode}");
        }

        var server = new ServerConfig
        {
            Id = "srv-mock",
            Kind = ServerKind.Emby,
            Name = "Mock Emby",
            BaseUrl = mock.BaseUrl,
            UserId = MockEmbyServer.UserId,
            UserName = MockEmbyServer.ValidUser,
            AccessToken = MockEmbyServer.AccessToken,
        };

        var emby = new EmbyService(http, server, onLog: m => Say("    EmbyService: " + m));

        // ② 列库
        List<EmbyUserView> views;
        try
        {
            views = await emby.GetViewsAsync(cancellationToken);
            Step("列库 GET /Users/{userId}/Views", views.Count == 2 && views.Any(v => v.CollectionType == "movies"),
                string.Join(", ", views.Select(v => $"{v.Name}({v.CollectionType})")));
        }
        catch (Exception ex)
        {
            Step("列库 GET /Users/{userId}/Views", false, ex.Message);
            return report;
        }

        // ③ 列条目
        try
        {
            var items = await emby.GetItemsAsync(parentId: MockEmbyServer.MoviesViewId, includeItemTypes: "Movie", cancellationToken: cancellationToken);
            var first = items.Items.FirstOrDefault();
            Step("列条目 GET /Users/{userId}/Items", first != null && first.Id == MockEmbyServer.MovieId,
                $"count={items.Items.Count}, total={items.TotalRecordCount}, first={first?.Name}/{first?.Type}");
        }
        catch (Exception ex)
        {
            Step("列条目 GET /Users/{userId}/Items", false, ex.Message);
        }

        // ④ 取详情
        try
        {
            var detail = await emby.GetItemAsync(MockEmbyServer.MovieId, cancellationToken: cancellationToken);
            Step("取详情 GET /Users/{userId}/Items/{id}", detail != null && detail.MediaSources.Count == 2 && detail.UserData.PlaybackPositionTicks == 3000000000L,
                $"name={detail?.Name}, mediaSources={detail?.MediaSources.Count}, resumeTicks={detail?.UserData.ResumeTicks}(期望 3000000000，>int.MaxValue 用于暴露截断), imdb={detail?.ImdbId}");
        }
        catch (Exception ex)
        {
            Step("取详情 GET /Users/{userId}/Items/{id}", false, ex.Message);
        }

        // ⑤ 生成可播放流地址（内核 --open= 首选形态）
        var streamUrl = emby.DirectStreamUrl(MockEmbyServer.MovieId, mediaSourceId: "ms-1");
        var streamOk = streamUrl.Contains($"/Videos/{MockEmbyServer.MovieId}/stream") && streamUrl.Contains("static=true") && streamUrl.Contains($"api_key={MockEmbyServer.AccessToken}");
        Step("生成可播放流地址 /Videos/{id}/stream?static=true&api_key=***", streamOk, MaskToken(streamUrl));

        // ⑤b 真的取到流（字节非空）
        try
        {
            var streamResult = await http.GetAsync(streamUrl, cancellationToken: cancellationToken);
            Step("取流实际返回字节", streamResult.Bytes.Length > 0, $"status={streamResult.StatusCode}, bytes={streamResult.Bytes.Length}");
        }
        catch (Exception ex)
        {
            Step("取流实际返回字节", false, ex.Message);
        }

        // ⑥ 播放信息（含降级路径）
        try
        {
            var info = await emby.PlaybackInfoAsync(MockEmbyServer.MovieId, cancellationToken: cancellationToken);
            Step("播放信息 POST /Items/{id}/PlaybackInfo", info != null && info.MediaSources.Count == 2,
                $"mediaSources={info?.MediaSources.Count}, labels=[{string.Join(" | ", info?.MediaSources.Select(s => s.VersionLabel) ?? Enumerable.Empty<string>())}]");
        }
        catch (Exception ex)
        {
            Step("播放信息 POST /Items/{id}/PlaybackInfo", false, ex.Message);
        }

        // ⑦ 媒体段（ticks → 毫秒）
        try
        {
            var segments = await emby.GetMediaSegmentsAsync(MockEmbyServer.MovieId, cancellationToken);
            var intro = segments.FirstOrDefault(s => s.Type == MediaSegmentType.Intro);
            var credits = segments.FirstOrDefault(s => s.Type == MediaSegmentType.Credits);
            Step("媒体段 GET /MediaSegments/{id}（ticks→ms，64 位不截断）",
                segments.Count == 2 && intro != null && intro.StartMs == 12000 && credits != null && credits.StartMs == 330000 && credits.EndMs == null,
                string.Join("; ", segments.Select(s => $"{s.Type.Id()} {s.StartMs}→{(s.EndMs?.ToString() ?? "end")}ms src={s.Source}")));
        }
        catch (Exception ex)
        {
            Step("媒体段 GET /MediaSegments/{id}（ticks→ms）", false, ex.Message);
        }

        // ⑧ 进度上报三连（内核 progress/stopped 的必要接口）
        try
        {
            await emby.ReportPlaybackStartAsync(MockEmbyServer.MovieId, "sess-1", "ms-1", 0);
            await emby.ReportPlaybackProgressAsync(MockEmbyServer.MovieId, "sess-1", 42.5, false, "ms-1");
            await emby.ReportPlaybackStoppedAsync(MockEmbyServer.MovieId, "sess-1", 55.0, "ms-1");
            var started = mock.CountRequests("POST", "/Sessions/Playing");
            Step("进度上报 /Sessions/Playing[/Progress|/Stopped]", started == 3, $"mock 收到 {started} 次 POST /Sessions/Playing*");
        }
        catch (Exception ex)
        {
            Step("进度上报 /Sessions/Playing[/Progress|/Stopped]", false, ex.Message);
        }

        // ⑨ 收藏切换（POST 增 / DELETE 删）
        try
        {
            var on = await emby.SetFavoriteAsync(MockEmbyServer.MovieId, true, cancellationToken);
            var off = await emby.SetFavoriteAsync(MockEmbyServer.MovieId, false, cancellationToken);
            var posts = mock.Requests.Count(r => r.Method == "POST" && r.Path.Contains("/FavoriteItems/"));
            var deletes = mock.Requests.Count(r => r.Method == "DELETE" && r.Path.Contains("/FavoriteItems/"));
            Step("收藏切换 FavoriteItems", on && off && posts == 1 && deletes == 1, $"POST={posts}, DELETE={deletes}");
        }
        catch (Exception ex)
        {
            Step("收藏切换 FavoriteItems", false, ex.Message);
        }

        // ⑩ 未鉴权请求确实被拒（证明 Token 头被带上而非 mock 放水）
        Step("鉴权头 X-Emby-Token 生效", mock.UnauthorizedCount == 0, $"401 次数={mock.UnauthorizedCount}");

        // ⑪ 凭据受保护存储 + 回填（验收②）
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var vault = SecureKvStore.At(Path.Combine(dir, "credentials.bin"));
            var passwords = new PasswordStore(vault);
            passwords.SetServerPassword("srv-mock", "p@ssw0rd-秘密");
            var back = passwords.GetServerPassword("srv-mock");
            var raw = File.ReadAllBytes(Path.Combine(dir, "credentials.bin"));
            var plainVisible = Encoding.UTF8.GetString(raw).Contains("p@ssw0rd");

            Step("凭据 DPAPI 加密存储 + 回填",
                back == "p@ssw0rd-秘密" && passwords.IsEncrypted && !plainVisible,
                $"readback={(back == "p@ssw0rd-秘密" ? "ok" : "mismatch")}, isEncrypted={passwords.IsEncrypted}, fileHasPlaintext={plainVisible}, bytes={raw.Length}, degraded={passwords.DegradedReason ?? "-"}");

            // ⑫ 服务器配置持久化 + 排序 + passwordOf
            var store = ServerConfigStore.At(Path.Combine(dir, TestServersFileName), vault);
            store.Load();
            var added = store.Add(new ServerConfig
            {
                Id = store.NewId(),
                Kind = ServerKind.Emby,
                Name = "本地 Mock",
                BaseUrl = mock.BaseUrl,
                AccessToken = MockEmbyServer.AccessToken,
                Extra = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(StringComparer.Ordinal)
                {
                    ["password"] = System.Text.Json.Nodes.JsonValue.Create("p@ssw0rd-秘密"),
                },
            });
            var added2 = store.Add(new ServerConfig { Id = store.NewId(), Kind = ServerKind.Navidrome, Name = "音乐服", BaseUrl = "http://127.0.0.1:4533" });

            store.Reorder(new List<string> { added2.Id, added.Id });
            var ordered = store.Servers;
            var reorderOk = ordered.Count == 2 && ordered[0].Id == added2.Id;

            var serversJson = File.ReadAllText(Path.Combine(dir, TestServersFileName));
            var leaksSecret = serversJson.Contains("p@ssw0rd");

            var reloaded = ServerConfigStore.At(Path.Combine(dir, TestServersFileName), vault);
            reloaded.Load();
            var passwordOf = reloaded.PasswordOf(added.Id);

            Step("服务器配置持久化 + reorder + passwordOf",
                reorderOk && !leaksSecret && passwordOf == "p@ssw0rd-秘密" && reloaded.Servers.Count == 2,
                $"reorder={(reorderOk ? "ok" : "fail")}, servers.json 含明文密码={leaksSecret}, passwordOf={(passwordOf == null ? "null" : "回填成功")}, count={reloaded.Servers.Count}");
        }
        catch (Exception ex)
        {
            Step("凭据/配置持久化", false, ex.ToString().Split('\n')[0]);
        }

        // ⑫b 原版 accounts.json 兼容读（事实 70 裁决 2）+ **双向反控制**
        //   正例：合法 accounts.json 必须映射成功（且排序/字段对得上）
        //   反控 A：改名成 notaccounts.json ⇒ 必须 0 台
        //   反控 B：坏 JSON ⇒ 必须 0 台 **且留下显式错误**（不得静默当"通过"）
        //   附：原版条目**只读**，不得被 Save() 回写进 servers.json（否则"改名即 0 台"的反控会失效）
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "aiplayer-accounts", Guid.NewGuid().ToString("N"));
            var good = Path.Combine(root, "good");
            Directory.CreateDirectory(good);
            var accountsPath = Path.Combine(good, "accounts.json");
            File.WriteAllText(accountsPath, """
            [
              {
                "id": "orig-a", "provider": "emby", "name": "", "serverName": "原版A",
                "username": "u1", "lastKnownUserId": "uid-a", "encryptedToken": "tok-a",
                "rememberPassword": true, "encryptedPassword": "", "lastLoginAt": "2026-09-09T14:22:20.714218",
                "lines": [ { "name": "Primary", "url": "https://a.example", "isMain": true } ],
                "preferredLineUrl": "https://a.example/emby", "iconUrl": "", "sortOrder": 3, "useProxy": true,
                "cachedMovieCount": 12, "cachedFavoriteCount": 1, "cachedLastPlayedAt": "2026-09-10T16:53:00",
                "note": "n1", "visibleLibraryIds": [], "spotlightLibraryIds": [], "libraryOrder": []
              },
              {
                "id": "orig-b", "provider": "jellyfin", "name": "原版B", "serverName": "B",
                "username": "u2", "lastKnownUserId": "uid-b", "encryptedToken": "tok-b",
                "preferredLineUrl": "https://b.example/jellyfin", "sortOrder": 1, "useProxy": false
              }
            ]
            """);
            var store = ServerConfigStore.At(Path.Combine(root, TestServersFileName), SecureKvStore.At(Path.Combine(root, "credentials.bin")), accountsPath);
            var accountsShaBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(accountsPath)));
            var accountsMtimeBefore = File.GetLastWriteTimeUtc(accountsPath);
            store.Load();
            var a = store.ById("orig-a");
            var b = store.ById("orig-b");
            var ordered = store.Servers;
            var mappedCount = ordered.Count;
            var goodOk = mappedCount == 2 && a != null && b != null
                         && a.Name == "原版A" && a.BaseUrl == "https://a.example/emby" && a.UserId == "uid-a"
                         && a.AccessToken == "tok-a" && a.Kind == ServerKind.Emby && a.SortIndex == 3
                         && b.Kind == ServerKind.Jellyfin && b.Name == "原版B"
                         && ordered[0].Id == "orig-b" && store.IsReadOnlyServer("orig-a") && !store.IsReadOnlyServer("srv-x")
                         // t25-③：兼容读留下的原版字段能被**类型化访问器**读出（数据面早就存在，只是缺入口）
                         && a.CachedLastPlayedAt.HasValue && a.CachedLastPlayedAt.Value.Day == 10
                         && a.IsOriginalAccountEntry && b.IsOriginalAccountEntry;

            store.Add(new ServerConfig { Id = store.NewId(), Kind = ServerKind.Navidrome, Name = "我们自己的", BaseUrl = "http://127.0.0.1:4533" });
            var written = File.ReadAllText(Path.Combine(root, TestServersFileName));
            var noWriteBack = !written.Contains("orig-a") && !written.Contains("orig-b") && !written.Contains("tok-a");

            // 🔴 ui 提的更强断言：**兼容读 + 触发一次 Save 之后，原版 accounts.json 必须逐字节不变**
            //    （含 mtime；「能读出 16 台」只证明读对了，「Save 后原文件不变」才证明不会毁数据）
            var accountsShaAfter = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(accountsPath)));
            var accountsMtimeAfter = File.GetLastWriteTimeUtc(accountsPath);
            var accountsUntouched = accountsShaBefore == accountsShaAfter && accountsMtimeBefore == accountsMtimeAfter;
            // 写路径恒定 = 构造时传入的 servers.json（与兼容读来源无关）
            var saveTargetOk = string.Equals(store.SaveTargetPath, Path.Combine(root, TestServersFileName), StringComparison.OrdinalIgnoreCase);

            var negDir = Path.Combine(root, "neg");
            Directory.CreateDirectory(negDir);
            File.Move(accountsPath, Path.Combine(negDir, "notaccounts.json"));
            var negStore = ServerConfigStore.At(Path.Combine(negDir, TestServersFileName), SecureKvStore.At(Path.Combine(negDir, "credentials.bin")), Path.Combine(negDir, "accounts.json"));
            negStore.Load();

            var badDir = Path.Combine(root, "bad");
            Directory.CreateDirectory(badDir);
            var badPath = Path.Combine(badDir, "accounts.json");
            File.WriteAllText(badPath, "{ this is not json");
            var badStore = ServerConfigStore.At(Path.Combine(badDir, TestServersFileName), SecureKvStore.At(Path.Combine(badDir, "credentials.bin")), badPath);
            badStore.Load();

            Step("原版 accounts.json 兼容读（双向反控制）",
                goodOk && noWriteBack && accountsUntouched && saveTargetOk
                && negStore.Servers.Count == 0 && negStore.OriginalAccountsLoaded == 0
                && badStore.Servers.Count == 0 && !string.IsNullOrEmpty(badStore.OriginalAccountsError),
                $"合法文件映射={mappedCount} 台（A name={(a == null ? "-" : a.Name)}/base={(a == null ? "-" : a.BaseUrl)}/kind={(a == null ? "-" : a.Kind.Id())}/sort={(a == null ? "-" : a.SortIndex.ToString())}，B kind={(b == null ? "-" : b.Kind.Id())}）；排序首项={(ordered.Count > 0 ? ordered[0].Id : "-")}；" +
                $"只读标记={store.IsReadOnlyServer("orig-a")}；回写检查={(noWriteBack ? "servers.json 未含原版条目/token" : "❌ 原版条目被写进 servers.json")}；" +
                $"**Save 后原版文件逐字节不变**={(accountsUntouched ? "是" : "❌ 内容或 mtime 变了")}（sha {accountsShaBefore.Substring(0, 8)}→{accountsShaAfter.Substring(0, 8)}）；" +
                $"写目标={(saveTargetOk ? "恒为传入的 servers.json" : "❌ " + store.SaveTargetPath)}；" +
                $"反控A(改名 notaccounts.json)={negStore.Servers.Count} 台；反控B(坏 JSON)={badStore.Servers.Count} 台 error={badStore.OriginalAccountsError ?? "无"}；" +
                $"t25 类型化读 cachedLastPlayedAt={(a == null || !a.CachedLastPlayedAt.HasValue ? "❌ 读不到" : a.CachedLastPlayedAt.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture))}" +
                $"（accountsOrigin 标记 A={a?.IsOriginalAccountEntry} / B={b?.IsOriginalAccountEntry}）");
        }
        catch (Exception ex)
        {
            Step("原版 accounts.json 兼容读（双向反控制）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑫b2 旧 Roaming 根 servers.json 的**只读合并**（事实 70 裁决 1；ui 追问「旧根旧内容怎么办」）
        //   正例：Local 有 2 条（含 1 条与旧根同 id）+ 旧根 2 条 ⇒ 合并后 3 条、同 id 以 Local 为准；
        //         Save 后 Local 收敛为 3 条，而**旧根文件逐字节不变**（不写、不改名）。
        //   反控 A：旧根路径不存在 ⇒ 合并 0 条。
        //   反控 B：旧根文件是坏 JSON ⇒ 不抛、合并 0 条、且旧文件**未被改名隔离**（文件名与内容都不变）。
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "aiplayer-legacy-merge", Guid.NewGuid().ToString("N"));
            var localDir = Path.Combine(root, "local");
            var legacyDir = Path.Combine(root, "legacy");
            Directory.CreateDirectory(localDir);
            Directory.CreateDirectory(legacyDir);
            var localPath = Path.Combine(localDir, TestServersFileName);
            var legacyPath = Path.Combine(legacyDir, TestServersFileName);

            File.WriteAllText(localPath, """
            { "servers": [
              { "id": "N-own", "kind": "navidrome", "name": "本地自有", "baseUrl": "http://127.0.0.1:4533", "enabled": true, "sortIndex": 0, "accessToken": "", "iconUrl": "", "hiddenLibraryIds": [], "extra": {} },
              { "id": "L-dup", "kind": "emby", "name": "本地版", "baseUrl": "https://local.example/emby", "enabled": true, "sortIndex": 1, "accessToken": "", "iconUrl": "", "hiddenLibraryIds": [], "extra": {} }
            ] }
            """);
            File.WriteAllText(legacyPath, """
            { "servers": [
              { "id": "L-dup", "kind": "emby", "name": "旧根版", "baseUrl": "https://legacy.example/emby", "enabled": true, "sortIndex": 9, "accessToken": "旧根token", "iconUrl": "", "hiddenLibraryIds": [], "extra": {} },
              { "id": "L-keep", "kind": "emby", "name": "旧根独有", "baseUrl": "https://keep.example/emby", "enabled": true, "sortIndex": 5, "accessToken": "", "iconUrl": "", "hiddenLibraryIds": [], "extra": {} }
            ] }
            """);
            var legacyShaBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(legacyPath)));

            var store = ServerConfigStore.At(localPath, SecureKvStore.At(Path.Combine(localDir, "credentials.bin")), null, legacyPath);
            store.Load();
            var dup = store.ById("L-dup");
            var kept = store.ById("L-keep");
            var mergedOk = store.Servers.Count == 3 && store.LegacyServersLoaded == 1
                           && dup != null && dup.Name == "本地版" && kept != null && kept.Name == "旧根独有";

            store.Save();
            var localAfter = File.ReadAllText(localPath);
            var legacyShaAfter = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(legacyPath)));
            var converged = localAfter.Contains("L-keep") && localAfter.Contains("N-own") && localAfter.Contains("L-dup");
            var legacyUntouched = legacyShaBefore == legacyShaAfter;

            var noneStore = ServerConfigStore.At(localPath, SecureKvStore.At(Path.Combine(localDir, "credentials.bin")), null, Path.Combine(legacyDir, "no-such.json"));
            noneStore.Load();

            var badLegacyDir = Path.Combine(root, "badlegacy");
            Directory.CreateDirectory(badLegacyDir);
            var badLegacy = Path.Combine(badLegacyDir, TestServersFileName);
            File.WriteAllText(badLegacy, "{ not json");
            var badShaBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(badLegacy)));
            var badStore = ServerConfigStore.At(localPath, SecureKvStore.At(Path.Combine(localDir, "credentials.bin")), null, badLegacy);
            badStore.Load();
            var badUntouched = File.Exists(badLegacy)
                               && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(badLegacy))) == badShaBefore;

            Step("旧根 servers.json 只读合并（合并去重 + 只写新根）",
                mergedOk && converged && legacyUntouched && noneStore.LegacyServersLoaded == 0
                && badStore.LegacyServersLoaded == 0 && badUntouched,
                $"合并后={store.Servers.Count} 条（N-own/L-dup/L-keep），旧根贡献={store.LegacyServersLoaded} 条（期望 1），同 id 取本地={dup?.Name}（期望 本地版）；" +
                $"Save 后新根含 3 条={converged}；**旧根文件逐字节不变**={(legacyUntouched ? "是" : "❌ 被改动了")}（sha {legacyShaBefore.Substring(0, 8)}→{legacyShaAfter.Substring(0, 8)}）；" +
                $"反控A(旧根不存在)={noneStore.LegacyServersLoaded} 条；反控B(旧根坏 JSON)={badStore.LegacyServersLoaded} 条且旧文件未被改名/改动={badUntouched}");
        }
        catch (Exception ex)
        {
            Step("旧根 servers.json 只读合并（合并去重 + 只写新根）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑫c 数据根对齐：生产解析必须是 **Local**（与内核/原版一致），旧 Roaming 仅作兼容读
        try
        {
            var dir = AppDataDir.Instance;
            var local = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty;
            var roaming = Environment.GetEnvironmentVariable("APPDATA") ?? string.Empty;
            var rootIsLocal = local.Length > 0 && dir.Root.StartsWith(local, StringComparison.OrdinalIgnoreCase)
                              && dir.Root.TrimEnd('\\').EndsWith("AIPlayer", StringComparison.OrdinalIgnoreCase);
            var legacyIsRoaming = roaming.Length > 0 && !string.IsNullOrEmpty(dir.LegacyRoot)
                                  && dir.LegacyRoot.StartsWith(roaming, StringComparison.OrdinalIgnoreCase);
            var localRootMatchesKernel = local.Length > 0
                                         && string.Equals(dir.LocalRoot, Path.Combine(local, "AIPlayer"), StringComparison.OrdinalIgnoreCase);
            Step("数据根对齐 Local（+ 旧 Roaming 兼容读）",
                rootIsLocal && legacyIsRoaming && localRootMatchesKernel,
                $"Root={dir.Root}｜LocalRoot={dir.LocalRoot}｜LegacyRoot={dir.LegacyRoot}｜原版 accounts.json 存在={dir.ExistingOriginalAccountsFile() != null}");
        }
        catch (Exception ex)
        {
            Step("数据根对齐 Local（+ 旧 Roaming 兼容读）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑫d 原版同名文件的一次性备份（事实 70 副作用防护）：根对齐到 Local 后，我们的 settings.json
        //     与原版 Flutter 外壳的 settings.json 同路径 ⇒ 首次写入前必须备份用户原版数据。
        //     双向：有同名文件 ⇒ 必须产出 .original-*.bak 且原文件不变、二次调用幂等；无同名文件 ⇒ 0。
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "aiplayer-foreign", Guid.NewGuid().ToString("N"));
            var legacy = Path.Combine(root, "legacy");
            var data = Path.Combine(root, "data");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(data);
            var originalSettings = "{\"proxyMode\":\"custom\",\"webdavAutoBackupRemoteDir\":\"/AI Player\"}";
            File.WriteAllText(Path.Combine(data, "settings.json"), originalSettings);
            var dir = new AppDataDir(data, false, legacy, canAutoMigrate: true);

            var first = dir.BackupPreExistingForeignFiles();
            var backups = Directory.GetFiles(data, "settings.json.original-*.bak");
            var originalIntact = File.ReadAllText(Path.Combine(data, "settings.json")) == originalSettings;
            var backupMatches = backups.Length == 1 && File.ReadAllText(backups[0]) == originalSettings;
            var second = dir.BackupPreExistingForeignFiles(); // 幂等

            var emptyDir = Path.Combine(root, "empty");
            Directory.CreateDirectory(emptyDir);
            var none = new AppDataDir(emptyDir, false, legacy, canAutoMigrate: true).BackupPreExistingForeignFiles();

            Step("原版同名文件一次性备份（防覆盖）",
                first == 1 && second == 0 && none == 0 && backupMatches && originalIntact,
                $"首次备份数={first}（期望 1），二次={second}（期望 0 幂等），无文件目录={none}（期望 0），备份内容一致={backupMatches}，原文件未变={originalIntact}，备份文件={backups.Length} 个（{Path.GetFileName(backups.Length > 0 ? backups[0] : "-")}）");
        }
        catch (Exception ex)
        {
            Step("原版同名文件一次性备份（防覆盖）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑬ 内核契约编码往返（HOST_CONTRACT §3）
        try
        {
            var shortcuts = new HostShortcuts();
            var decoded = HostValueCodec.DecodeHostValue(shortcuts.ToBase64());
            var playPause = decoded.HasValue ? JsonRead.IntOrNull(decoded, "playPause") : null;

            var seg = new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 12000, EndMs = 102000, Source = "emby" };
            var segDecoded = HostValueCodec.DecodeHostValue(seg.ToBase64());

            var chapter = new HostChapter { MarkerId = 7, MarkerType = "chapter", Title = "开场", TimeStart = 0, TimeEnd = 90000 };
            var chapterDecoded = HostValueCodec.DecodeHostValue(chapter.ToBase64());

            var sprite = new HostSprite { SpriteId = 3, Width = 160, Height = 90, VttUrl = "https://x/s.vtt" };
            var spriteDecoded = HostValueCodec.DecodeHostValue(sprite.ToBase64());

            var ok = playPause == 32
                     && segDecoded.HasValue && JsonRead.Str(segDecoded, "type") == "intro" && JsonRead.Int(segDecoded, "startMs") == 12000
                     && chapterDecoded.HasValue && JsonRead.Int(chapterDecoded, "marker_id") == 7 && JsonRead.Int(chapterDecoded, "time_end") == 90000
                     && spriteDecoded.HasValue && JsonRead.Str(spriteDecoded, "vtt_url") == "https://x/s.vtt" && JsonRead.Int(spriteDecoded, "width") == 160;

            Step("内核契约 Base64(JSON) 往返（shortcuts/segment/chapter/sprite）", ok,
                $"playPause={playPause}, segment={segDecoded?.GetRawText()}, chapter={chapterDecoded?.GetRawText()}, sprite={spriteDecoded?.GetRawText()}");
        }
        catch (Exception ex)
        {
            Step("内核契约 Base64(JSON) 往返", false, ex.Message);
        }

        // ⑭ AppSettings 全字段往返 + With
        try
        {
            var original = new AppSettings { MaxVolume = 150, AnimeMode = "denoise", MergeServerLibraries = true, AccentColor = 0xFF112233 };
            original.DanmakuApis.Add(new DanmakuApiConfig { Url = "https://d.example/api", Name = "弹幕A", Enabled = true });
            original.DanmakuApis.Add(new DanmakuApiConfig { Url = "https://d2.example/api", Name = "弹幕B", Enabled = false });
            original.SkipSources = original.SkipSources.With(theOtherDb: true, customTemplate: "https://x/api?imdb={imdb}&s={season}&e={episode}");

            var roundTrip = AppSettings.FromJson(JsonRead.FromNode(original.ToJson().ToJsonString()).Value);
            var withResult = roundTrip.With(proxyEnabled: true, maxVolume: 180);

            var activeApis = roundTrip.ActiveDanmakuApis.Count;
            var ok = roundTrip.MaxVolume == 150 && roundTrip.AnimeMode == "denoise" && roundTrip.MergeServerLibraries
                     && roundTrip.AccentColor == 0xFF112233 && roundTrip.DanmakuApis.Count == 2 && activeApis == 1
                     && roundTrip.SkipSources.TheOtherDb && roundTrip.SkipSources.CustomTemplate.Contains("{imdb}")
                     && withResult != null && withResult.MaxVolume == 180 && withResult.ProxyEnabled && withResult.AnimeMode == "denoise";

            Step("AppSettings 全字段往返 + ActiveDanmakuApis 过滤 + With", ok,
                $"maxVolume={roundTrip.MaxVolume}, anime={roundTrip.AnimeMode}, accent=0x{roundTrip.AccentColor:X8}, apis={roundTrip.DanmakuApis.Count}/active={activeApis}, otherDb={roundTrip.SkipSources.TheOtherDb}");
        }
        catch (Exception ex)
        {
            Step("AppSettings 全字段往返", false, ex.Message);
        }

        // ⑮ SettingsService 落盘往返
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "settings.json");

            var settingsService = SettingsService.At(file);
            settingsService.Load();
            var defaults = settingsService.Settings.LibraryPageSize;
            settingsService.Patch(s => s.With(libraryPageSize: 120, themeMode: "dark"));

            var reloaded = SettingsService.At(file);
            reloaded.Load();
            var ok = defaults == 60 && File.Exists(file) && reloaded.Settings.LibraryPageSize == 120 && reloaded.Settings.ThemeMode == "dark";

            Step("SettingsService 落盘 + 重载", ok,
                $"默认 pageSize={defaults}, 落盘后重载 pageSize={reloaded.Settings.LibraryPageSize}, theme={reloaded.Settings.ThemeMode}, file={new FileInfo(file).Length}B");
        }
        catch (Exception ex)
        {
            Step("SettingsService 落盘 + 重载", false, ex.Message);
        }

        // ⑯ 跳过片段：容错解析 + 归一化 + 三源聚合与缓存
        try
        {
            // 形态 1：数组 + ms 字段
            var shape1 = JsonRead.FromNode("[{\"type\":\"intro\",\"start_ms\":15000,\"end_ms\":60000}]");
            var parsed1 = SegmentService.ParseSegments(shape1, "s1");
            // 形态 2：键名即类型 + 秒
            var shape2 = JsonRead.FromNode("{\"intro\":{\"start\":15,\"end\":60},\"outro\":{\"start\":1380}}");
            var parsed2 = SegmentService.ParseSegments(shape2, "s2");
            // 形态 3：包装层下钻 + 字符串值
            var shape3 = JsonRead.FromNode("{\"data\":{\"items\":[{\"kind\":\"recap\",\"start\":\"5\",\"end\":\"20\"}]}}");
            var parsed3 = SegmentService.ParseSegments(shape3, "s3");

            var okParse = parsed1.Count == 1 && parsed1[0].StartMs == 15000 && parsed1[0].EndMs == 60000
                          && parsed2.Count == 2 && parsed2[0].EndMs == 60000 && parsed2[1].EndMs == null
                          && parsed3.Count == 1 && parsed3[0].Type == MediaSegmentType.Recap && parsed3[0].StartMs == 5000;

            // 归一化：同 type 取并集（最早 start / 最晚 end；任一方到片尾 ⇒ 到片尾）
            var normalized = SegmentService.Normalize(new[]
            {
                new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 15000, EndMs = 60000, Source = "theintrodb" },
                new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 12000, EndMs = 90000, Source = "introdb.app" },
                new MediaSegmentDto { Type = MediaSegmentType.Credits, StartMs = 1380000, EndMs = null, Source = "emby" },
                new MediaSegmentDto { Type = MediaSegmentType.Recap, StartMs = 0, EndMs = -1, Source = "bad" },
            }, durationSeconds: 1500);

            var okNormalize = normalized.Count == 2
                              && normalized[0].Type == MediaSegmentType.Intro && normalized[0].StartMs == 12000 && normalized[0].EndMs == 90000
                              && normalized[0].Source == "theintrodb+introdb.app"
                              && normalized[1].EndMs == null;

            Step("跳过片段容错解析（三种形态）+ 归一化合并", okParse && okNormalize,
                $"p1={parsed1.Count}, p2={parsed2.Count}, p3={parsed3.Count}; normalize={string.Join(" | ", normalized.Select(s => $"{s.Type.Id()} {s.StartMs}→{(s.EndMs?.ToString() ?? "end")} ({s.Source})"))}");

            // 三源聚合（mock）+ 二次调用命中缓存
            using var segMock = new MockHttpServer(req =>
            {
                if (req.Path.EndsWith("/v3/media", StringComparison.Ordinal))
                    return MockResponse.Json("[{\"type\":\"intro\",\"start_ms\":11000,\"end_ms\":50000}]");
                if (req.Path.EndsWith("/segments", StringComparison.Ordinal))
                    return MockResponse.Json("{\"intro\":{\"start\":11,\"end\":50},\"outro\":{\"start\":1400}}");
                return MockResponse.Json("{}", 404);
            }).Start();

            var cacheStorage = new JsonStorage(Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck", "segments-" + Guid.NewGuid().ToString("N") + ".json"));
            using var segHttp = new ShellHttpClient();
            var segmentService = new SegmentService(segHttp, cacheStorage, theIntroDbBase: segMock.BaseUrl + "/v3/media", introDbAppBase: segMock.BaseUrl + "/segments");
            var settings2 = new SkipSourceSettings { Emby = false, TheIntroDb = true, IntroDbApp = true, TheOtherDb = false, CustomTemplate = string.Empty };

            var first = await segmentService.CollectAsync(settings2, imdbId: "tt1234567", season: 1, episode: 2, cancellationToken: cancellationToken);
            var requestsAfterFirst = segMock.Requests.Count;
            var second = await segmentService.CollectAsync(settings2, imdbId: "tt1234567", season: 1, episode: 2, cancellationToken: cancellationToken);
            var requestsAfterSecond = segMock.Requests.Count;

            var okCollect = first.Count == 2 && first[0].StartMs == 11000 && first[0].EndMs == 50000
                            && second.Count == first.Count && requestsAfterFirst == 2 && requestsAfterSecond == requestsAfterFirst;

            Step("跳过片段三源聚合（mock）+ 磁盘缓存命中去重", okCollect,
                $"首轮={string.Join(" | ", first.Select(s => $"{s.Type.Id()} {s.StartMs}→{(s.EndMs?.ToString() ?? "end")} ({s.Source})"))}; 请求数 {requestsAfterFirst}→{requestsAfterSecond}（第二轮未再发请求=命中缓存）");
        }
        catch (Exception ex)
        {
            Step("跳过片段聚合/归一化", false, ex.ToString().Split('\n')[0]);
        }

        // ⑰ 章节/雪碧图（TODB）归一化
        try
        {
            using var todbMock = new MockHttpServer(req => MockResponse.Json(
                "{\"chapters\":[" +
                "{\"marker_id\":1,\"marker_type\":\"chapter\",\"title\":\"开场\",\"time_start\":0,\"time_end\":90000}," +
                "{\"id\":2,\"type\":\"opening\",\"name\":\"OP\",\"start\":120,\"end\":180}]," +
                "\"sprite\":{\"sprite_id\":3,\"width\":160,\"height\":90,\"vtt_url\":\"https://x/s.vtt\"}}")).Start();

            using var todbHttp = new ShellHttpClient();
            var todb = new TodbService(todbHttp, metadataBase: todbMock.BaseUrl + "/api/metadata");
            var meta = await todb.FetchAsync("tt1234567", 1, 2, 1500, cancellationToken);

            var ok = meta.Chapters.Count == 2
                     && meta.Chapters[0].MarkerId == 1 && meta.Chapters[0].TimeStart == 0 && meta.Chapters[0].TimeEnd == 90000
                     && meta.Chapters[1].MarkerType == "opening" && meta.Chapters[1].TimeStart == 120000 && meta.Chapters[1].TimeEnd == 180000
                     && meta.Sprite != null && meta.Sprite.SpriteId == 3 && meta.Sprite.Width == 160 && meta.Sprite.VttUrl == "https://x/s.vtt"
                     && !meta.IsEmpty;

            Step("TODB 章节/雪碧图归一化（snake_case 与别名并存）", ok,
                $"chapters={string.Join(" | ", meta.Chapters.Select(c => $"#{c.MarkerId} {c.MarkerType} {c.Title} {c.TimeStart}→{(c.TimeEnd?.ToString() ?? "end")}"))}, sprite={meta.Sprite?.SpriteId}/{meta.Sprite?.Width}x{meta.Sprite?.Height}");
        }
        catch (Exception ex)
        {
            Step("TODB 章节/雪碧图归一化", false, ex.Message);
        }

        // ⑱ Navidrome：原生登录 + Subsonic token+salt 鉴权 + 列表/详情/搜索/星标/歌词/取流
        try
        {
            using var ndMock = new MockNavidromeServer().Start();
            using var ndHttp = new ShellHttpClient();

            var auth = await NavidromeService.LoginNativeAsync(ndHttp, ndMock.BaseUrl, MockNavidromeServer.ValidUser, MockNavidromeServer.ValidPassword, cancellationToken);
            var ndServer = new ServerConfig
            {
                Id = "srv-nd",
                Kind = ServerKind.Navidrome,
                Name = "Mock Navidrome",
                BaseUrl = ndMock.BaseUrl,
                UserName = MockNavidromeServer.ValidUser,
                AccessToken = auth.Token,
            };
            ndServer.SetExtra("password", MockNavidromeServer.ValidPassword);

            var nd = new NavidromeService(ndHttp, ndServer);
            var ping = await nd.PingAsync(cancellationToken);
            var albums = await nd.GetAlbumList2Async(size: 10, cancellationToken: cancellationToken);
            var album = await nd.GetAlbumAsync(MockNavidromeServer.AlbumId, cancellationToken);
            var playlist = await nd.GetPlaylistAsync("pl-1", cancellationToken);
            var search = await nd.Search3Async("示例", cancellationToken: cancellationToken);
            var starOn = await nd.SetStarredAsync(MockNavidromeServer.SongId, true, cancellationToken: cancellationToken);
            var starOff = await nd.SetStarredAsync(MockNavidromeServer.SongId, false, cancellationToken: cancellationToken);
            var lyrics = await nd.GetLyricsBySongIdAsync(MockNavidromeServer.SongId, cancellationToken);
            var ndStreamUrl = nd.StreamUrl(MockNavidromeServer.SongId);

            var ndStreamOk = ndStreamUrl.Contains("/rest/stream") && ndStreamUrl.Contains($"id={MockNavidromeServer.SongId}")
                           && ndStreamUrl.Contains("u=" + MockNavidromeServer.ValidUser) && ndStreamUrl.Contains("t=")
                           && ndStreamUrl.Contains("s=") && ndStreamUrl.Contains("f=json") && ndStreamUrl.Contains("c=");

            var ok = auth.Token == MockNavidromeServer.NativeToken
                     && ping
                     && albums.Count == 2
                     && album.Songs.Count == 1
                     && playlist.Count == 1
                     && (search.Songs.Count + search.Albums.Count + search.Artists.Count) > 0
                     && starOn && starOff
                     && !lyrics.IsEmpty
                     && ndStreamOk
                     && ndMock.AuthFailureCount == 0;

            Step("Navidrome 登录 + Subsonic token+salt + 列表/详情/搜索/星标/歌词/取流", ok,
                $"token={(auth.Token == MockNavidromeServer.NativeToken ? "ok" : auth.Token)}, ping={ping}, albums={albums.Count}, albumSongs={album.Songs.Count}, playlist={playlist.Count}, search={search.Artists.Count}a/{search.Albums.Count}al/{search.Songs.Count}s, star={starOn}/{starOff}, lyricsLines={lyrics.Lines.Count}, authFailures={ndMock.AuthFailureCount}; streamUrl={ndStreamUrl}");
        }
        catch (Exception ex)
        {
            Step("Navidrome 登录 + Subsonic token+salt + 取流", false, ex.Message);
        }

        // ⑲ WebDAV：Basic 鉴权 + PROPFIND(207 XML) + MKCOL + PUT/GET 往返 + DELETE + 备份包往返
        try
        {
            using var davMock = new MockWebDavServer().Start();
            using var davHttp = new ShellHttpClient();

            var davServer = new ServerConfig
            {
                Id = "srv-dav",
                Kind = ServerKind.WebDav,
                Name = "Mock WebDAV",
                BaseUrl = davMock.BaseUrl,
                UserName = MockWebDavServer.ValidUser,
            };
            davServer.SetExtra("password", MockWebDavServer.ValidPassword);

            var dav = new WebDavService(davHttp, davServer);
            var ping = await dav.PingAsync();
            var created = await dav.CreateDirectoryAsync("/backup");

            const string remotePath = "/backup/backup_20260911_190000.json";
            const string payload = "{\"schema\":1,\"note\":\"中文内容往返\"}";
            var written = await dav.WriteStringAsync(remotePath, payload);

            var entries = await dav.ListAsync("/backup");
            var fileEntry = entries.FirstOrDefault(e => !e.IsDirectory && e.Name.StartsWith("backup_", StringComparison.Ordinal));
            var readBack = await dav.ReadStringAsync(remotePath);
            var exists = await dav.ExistsAsync(remotePath);
            var deleted = await dav.DeleteAsync(remotePath);
            var existsAfterDelete = await dav.ExistsAsync(remotePath);

            var davOk = ping && created && written
                        && fileEntry != null && fileEntry.Size == System.Text.Encoding.UTF8.GetByteCount(payload)
                        && readBack == payload && exists && deleted && !existsAfterDelete
                        && davMock.AuthFailureCount == 0;

            Step("WebDAV PROPFIND(207)/MKCOL/PUT/GET/DELETE + Basic 鉴权", davOk,
                $"ping={ping}, mkcol={created}, put={written}, entries={entries.Count}, file={fileEntry?.Name}/{fileEntry?.Size}B, 读回一致={readBack == payload}, exists={exists}→{existsAfterDelete}, authFailures={davMock.AuthFailureCount}, 请求={string.Join(",", davMock.Requests.Select(r => r.Method).Distinct())}");

            // 备份包结构往返（离线）
            var bundle = new BackupBundle { Schema = 1, CreatedAt = new DateTime(2026, 9, 11, 19, 0, 0, DateTimeKind.Utc) };
            var bundleRoundTrip = BackupBundle.FromJson(JsonRead.FromNode(bundle.ToJson().ToJsonString()).Value);
            var stamp = WebDavAutoBackupService.Timestamp(new DateTime(2026, 9, 11, 19, 0, 0, DateTimeKind.Local));
            Step("WebDAV 备份包结构与产物命名",
                bundleRoundTrip.Schema == 1 && bundleRoundTrip.CreatedAt.Year == 2026 && stamp.StartsWith("20260911_1900", StringComparison.Ordinal),
                $"schema={bundleRoundTrip.Schema}, createdAt={bundleRoundTrip.CreatedAt:O}, timestamp={stamp}, prefix={WebDavAutoBackupService.BackupPrefix}");
        }
        catch (Exception ex)
        {
            Step("WebDAV PROPFIND/MKCOL/PUT/GET/DELETE", false, ex.Message);
        }

        // ⑳ MpvConfService 预置键的证据分档（DESIGN r24 §4.1 #26）
        try
        {
            var confContain = MpvConfService.FromSettings(new AppSettings());
            var confCover = MpvConfService.FromSettings(new AppSettings { VideoFitMode = "cover" });
            var confProxy = MpvConfService.FromSettings(new AppSettings { ProxyEnabled = true, ProxyUrl = "http://127.0.0.1:7890" });

            var containText = confContain.Build().Replace("\n", " ").Trim();
            var coverText = confCover.Build().Replace("\n", " ").Trim();
            var proxyText = confProxy.Build().Replace("\n", " ").Trim();

            // 断言形状（DESIGN §9.5 追加推论）：断言**集合边界**（白名单 / 恰好等于），
            // 而不是「某个已知坏值不出现」——后者挡不住「换一个没人想到的坏键」（如未来有人加 ytdl=yes）。
            var allowedKeys = new HashSet<string>(StringComparer.Ordinal) { "keep-open", "panscan", "http-proxy" };
            var producedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var svc in new[] { confContain, confCover, confProxy })
            {
                foreach (var kv in svc.Options) producedKeys.Add(kv.Key);
            }

            var unexpectedKeys = producedKeys.Where(k => !allowedKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var missingKeys = allowedKeys.Where(k => !producedKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

            var confOk = producedKeys.SetEquals(allowedKeys)
                         && confContain.Has("keep-open") && !confContain.Has("panscan")
                         && confCover.Has("panscan") && coverText.Contains("panscan=1")
                         && confProxy.Has("http-proxy") && proxyText.Contains("http-proxy=http://127.0.0.1:7890");

            Step("MpvConfService 预置键白名单（产出键集合 ⊆ {keep-open,panscan,http-proxy} 且三者齐备）", confOk,
                $"产出键集合=[{string.Join(",", producedKeys.OrderBy(k => k, StringComparer.Ordinal))}]"
                + $" 不在白名单=[{(unexpectedKeys.Count == 0 ? "无" : string.Join(",", unexpectedKeys))}]"
                + $" 白名单内缺失=[{(missingKeys.Count == 0 ? "无" : string.Join(",", missingKeys))}]"
                + $"｜contain=[{containText}] cover=[{coverText}] proxy=[{proxyText}]");
        }
        catch (Exception ex)
        {
            Step("MpvConfService 预置键分档", false, ex.Message);
        }

        // 最后一条：实例模式下 `accounts.json` 的**路径重解析**（文件移走→0 台；放回→又能读到）。
        // 放在最后，因为它用 AppDataDir.Override 临时改根，紧随其后全部复位，不影响其它步骤。
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "aiplayer-accounts-reload", Guid.NewGuid().ToString("N"));
            var legacy = Path.Combine(root, "legacy");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(legacy);
            var accountsPath = Path.Combine(root, "accounts.json");
            File.WriteAllText(accountsPath, "[{\"id\":\"re-1\",\"provider\":\"emby\",\"serverName\":\"重解析\",\"preferredLineUrl\":\"https://re.example/emby\",\"encryptedToken\":\"tok\",\"sortOrder\":0}]");

            AppDataDir.Override(new AppDataDir(root, false, legacy, canAutoMigrate: false));
            var store = new ServerConfigStore(new JsonStorage(Path.Combine(root, TestServersFileName)), SecureKvStore.At(Path.Combine(root, "credentials.bin")), readOriginalAccounts: true);
            store.Load();
            var afterLoad = store.Servers.Count;

            File.Move(accountsPath, Path.Combine(root, "notaccounts.json"));
            store.Reload();
            var afterRename = store.Servers.Count;

            File.Move(Path.Combine(root, "notaccounts.json"), accountsPath);
            store.Reload();
            var afterRestore = store.Servers.Count;

            Step("accounts.json 路径重解析（移走→0 台；放回→恢复）",
                afterLoad == 1 && afterRename == 0 && afterRestore == 1,
                $"Load={afterLoad}（期望 1）｜改名后 Reload={afterRename}（期望 0）｜放回后 Reload={afterRestore}（期望 1）");
        }
        catch (Exception ex)
        {
            Step("accounts.json 路径重解析（移走→0 台；放回→恢复）", false, ex.ToString().Split('\n')[0]);
        }
        finally
        {
            AppDataDir.Reset();
        }

        // ⑬b t25-① Emby 搜索的类型过滤参数（默认不带 IncludeItemTypes / 显式带 / 历史常量可复现）
        //   断言取自 **mock 实际收到的请求**（不是看源码），因为这条契约就是"线上 query 长什么样"。
        try
        {
            var beforeDefault = mock.Requests.Count;
            var defaultResult = await emby.SearchAsync("movie", cancellationToken: cancellationToken);
            var defaultReq = mock.Requests.Skip(beforeDefault)
                .LastOrDefault(r => r.Path.EndsWith("/Items", StringComparison.Ordinal));
            var defaultHasType = defaultReq != null && defaultReq.QueryValue("IncludeItemTypes") != null;

            var beforeTyped = mock.Requests.Count;
            var typedResult = await emby.SearchAsync("movie", includeItemTypes: "Movie", cancellationToken: cancellationToken);
            var typedReq = mock.Requests.Skip(beforeTyped)
                .LastOrDefault(r => r.Path.EndsWith("/Items", StringComparison.Ordinal));
            var typedValue = typedReq == null ? null : typedReq.QueryValue("IncludeItemTypes");

            var beforeLegacy = mock.Requests.Count;
            await emby.SearchAsync("movie", includeItemTypes: EmbyService.DefaultSearchItemTypes, cancellationToken: cancellationToken);
            var legacyReq = mock.Requests.Skip(beforeLegacy)
                .LastOrDefault(r => r.Path.EndsWith("/Items", StringComparison.Ordinal));
            var legacyValue = legacyReq == null ? null : legacyReq.QueryValue("IncludeItemTypes");

            Step("Emby 搜索类型参数（默认不带 / 显式带 / 历史常量可复现）",
                defaultReq != null && !defaultHasType && typedValue == "Movie"
                && legacyValue == EmbyService.DefaultSearchItemTypes
                && defaultResult != null && typedResult != null,
                $"默认 SearchAsync(term) ⇒ IncludeItemTypes={(defaultHasType ? "存在 ❌" : "无 ✓")}；" +
                $"SearchAsync(term, includeItemTypes: \"Movie\") ⇒ {typedValue ?? "（缺）❌"}；" +
                $"显式传 DefaultSearchItemTypes ⇒ {(legacyValue == EmbyService.DefaultSearchItemTypes ? "逐字符相同 ✓（聚合搜索的旧行为靠这条保持）" : (legacyValue ?? "（缺）❌"))}");
        }
        catch (Exception ex)
        {
            Step("Emby 搜索类型参数（默认不带 / 显式带 / 历史常量可复现）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑬c t25-② 聚合搜索三数：TotalHits（含空 Id）/ MergedCount（去重、不受 limit）/ Items.Count（已截断）
        try
        {
            var sample = new List<AggregatedSearchHit>
            {
                new AggregatedSearchHit { Id = "x1", Name = "同名片", Type = "Movie", ProductionYear = 2020, ServerName = "A" },
                new AggregatedSearchHit { Id = "x2", Name = "同名片", Type = "Movie", ProductionYear = 2020, ServerName = "B" }, // 与上同键（跨源同条目）
                new AggregatedSearchHit { Id = string.Empty, Name = "空Id", Type = "Movie", ProductionYear = 2021, ServerName = "A" }, // 空 Id：计入 TotalHits、不进分组
                new AggregatedSearchHit { Id = "y1", Name = "另一个", Type = "Movie", ProductionYear = 2019, ServerName = "A" },
            };
            var mergedAll = AggregatedSearchResult.FromItems(sample, term: "同名");
            var mergedLimit1 = AggregatedSearchResult.FromItems(sample, term: "同名", limit: 1);
            var countsOk = mergedAll.TotalHits == 4 && mergedAll.MergedCount == 2 && mergedAll.MergedHitCount == 3
                           && mergedAll.Items.Count == 2
                           && mergedLimit1.MergedCount == 2 && mergedLimit1.MergedHitCount == 3 && mergedLimit1.Items.Count == 1;
            Step("聚合搜索三数（TotalHits 含空 Id / MergedCount 去重且不受 limit / Items.Count 已截断）", countsOk,
                $"样本 4 条命中（含 1 条空 Id、1 组跨源同键）⇒ TotalHits={mergedAll.TotalHits}(期望 4)" +
                $"｜MergedCount={mergedAll.MergedCount}(期望 2)｜MergedHitCount={mergedAll.MergedHitCount}(期望 3)｜Items.Count={mergedAll.Items.Count}(期望 2)；" +
                $"limit=1 时 ⇒ MergedCount={mergedLimit1.MergedCount}(期望 2，不受截断)/MergedHitCount={mergedLimit1.MergedHitCount}(期望 3)/Items.Count={mergedLimit1.Items.Count}(期望 1)");
        }
        catch (Exception ex)
        {
            Step("聚合搜索三数（TotalHits 含空 Id / MergedCount 去重且不受 limit / Items.Count 已截断）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑬d t25-③ cachedLastPlayedAt 类型化访问器（原版形态可读 / 缺失与坏值都不抛 / 写回内存 / 原版来源标记）
        try
        {
            var cfg = new ServerConfig { Id = "c1", Name = "n1" };
            cfg.SetExtra("cachedLastPlayedAt", "2026-09-10T16:53:00");
            var parsed = cfg.CachedLastPlayedAt;
            var raw = cfg.CachedLastPlayedAtRaw;

            var missingCfg = new ServerConfig { Id = "c2", Name = "n2" };
            var missing = missingCfg.CachedLastPlayedAt;
            var missingRaw = missingCfg.CachedLastPlayedAtRaw;

            var badCfg = new ServerConfig { Id = "c3", Name = "n3" };
            badCfg.SetExtra("cachedLastPlayedAt", "不是时间");
            var malformed = badCfg.CachedLastPlayedAt;

            var stamp = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
            cfg.SetCachedLastPlayedAt(stamp);
            var roundTrip = cfg.CachedLastPlayedAt == stamp;

            var ownCfg = new ServerConfig { Id = "c4", Name = "n4" };
            var originMarked = new ServerConfig { Id = "c5", Name = "n5" };
            originMarked.SetExtra("accountsOrigin", "true");

            var accessorOk = parsed.HasValue && parsed.Value.Year == 2026 && parsed.Value.Month == 9 && parsed.Value.Day == 10
                             && raw == "2026-09-10T16:53:00"
                             && missing == null && missingRaw == null
                             && malformed == null
                             && roundTrip
                             && !ownCfg.IsOriginalAccountEntry && originMarked.IsOriginalAccountEntry;
            Step("cachedLastPlayedAt 类型化访问器（读 / 缺失不抛 / 坏值不抛 / 写回内存 / 原版来源标记）", accessorOk,
                $"原版形态 '2026-09-10T16:53:00' ⇒ {(parsed.HasValue ? parsed.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture) : "null ❌")}（raw={(raw ?? "null")}）；" +
                $"缺失 ⇒ {(missing == null && missingRaw == null ? "null ✓" : "❌ 非 null")}；坏值 '不是时间' ⇒ {(malformed == null ? "null ✓" : "❌ 非 null")}；" +
                $"SetCachedLastPlayedAt 往返 ⇒ {(roundTrip ? "一致 ✓" : "❌ 不一致")}；IsOriginalAccountEntry：普通配置={ownCfg.IsOriginalAccountEntry}(期望 false) / accountsOrigin=true={originMarked.IsOriginalAccountEntry}(期望 true)");
        }
        catch (Exception ex)
        {
            Step("cachedLastPlayedAt 类型化访问器（读 / 缺失不抛 / 坏值不抛 / 写回内存 / 原版来源标记）", false, ex.ToString().Split('\n')[0]);
        }

        // ═══════════ t33 / S5 体验设施（服务侧）═══════════
        // 全部在**临时数据根**上跑，不碰用户真实数据（`AppDataDir` 只作为值对象传入，不用 Override）。
        var s5Root = Path.Combine(Path.GetTempPath(), "aiplayer-s5-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var s5Data = new AppDataDir(s5Root, false);
        var s5Log = new List<string>();

        // ③a 图片磁盘缓存：MISS → STORE → HIT（命中那次**不调** fetch）；统计与清理前后条数
        try
        {
            var images = new ImageCacheManager(s5Data, log: m => s5Log.Add(m));
            var fetchCalls = 0;
            Func<Task<byte[]>> fetch = () => { fetchCalls++; return Task.FromResult(new byte[1024]); };

            var first = await images.GetOrFetchAsync("https://img.example/a.jpg", fetch);   // MISS
            var second = await images.GetOrFetchAsync("https://img.example/a.jpg", fetch);  // HIT（不该再调 fetch）
            await images.GetOrFetchAsync("https://img.example/b.jpg", fetch);               // MISS

            var statsBefore = images.Stats();
            var reportBefore = new CacheStatsService(s5Data).Report();
            var removed = new CacheStatsService(s5Data).ClearShellCaches();
            var statsAfter = images.Stats();

            var hasMiss = s5Log.Exists(l => l.StartsWith("IMAGE-CACHE-MISS", StringComparison.Ordinal));
            var hasHit = s5Log.Exists(l => l.StartsWith("IMAGE-CACHE-HIT", StringComparison.Ordinal));
            var ok = first != null && second != null && fetchCalls == 2
                     && hasMiss && hasHit
                     && statsBefore.Count == 2 && statsBefore.Bytes == 2048
                     && removed >= 2 && statsAfter.Count == 0 && statsAfter.Bytes == 0;
            Step("S5① 图片磁盘缓存（MISS→STORE→HIT 且命中不调 fetch；统计与清理前后条数）", ok,
                $"fetch 调用={fetchCalls}（期望 2：命中那次不调）｜日志 MISS={hasMiss} HIT={hasHit}｜" +
                $"清理前 {statsBefore.Count} 条 / {statsBefore.Bytes} B（{reportBefore}）⇒ ClearShellCaches() 删除 {removed} 个 ⇒ 清理后 {statsAfter.Count} 条 / {statsAfter.Bytes} B｜缓存目录={images.Store.Directory}");
        }
        catch (Exception ex)
        {
            Step("S5① 图片磁盘缓存（MISS→STORE→HIT 且命中不调 fetch；统计与清理前后条数）", false, ex.ToString().Split('\n')[0]);
        }

        // ③b 弹幕磁盘缓存：同一 match-name 二次取数走缓存（网络取数次数前后对照）
        try
        {
            var danmaku = new DanmakuDiskCacheStore(s5Data, log: m => s5Log.Add(m));
            var netCalls = 0;
            Func<Task<string>> pull = () => { netCalls++; return Task.FromResult("{\"comments\":[1,2,3]}"); };

            var r1 = await danmaku.GetOrFetchAsync("葬送的芙莉莲 S01E01", pull);
            var r2 = await danmaku.GetOrFetchAsync("葬送的芙莉莲 S01E01", pull);
            var other = await danmaku.GetOrFetchAsync("另一个番 S01E01", pull);

            var ok = netCalls == 2 && r1 == r2 && other != null
                     && danmaku.FetchCount == 2 && danmaku.HitCount == 1
                     && danmaku.Contains("葬送的芙莉莲 S01E01");
            Step("S5② 弹幕磁盘缓存（按 match-name；二次取数命中缓存）", ok,
                $"网络取数次数={netCalls}（期望 2：同 match-name 第二次命中、换 match-name 再取一次）｜缓存 FetchCount={danmaku.FetchCount} HitCount={danmaku.HitCount}｜" +
                $"第一次={(r1 == null ? "null" : "取到 " + r1.Length + " 字符")} 第二次={(r2 == null ? "null" : "取到 " + r2.Length + " 字符")}（两者相等={r1 == r2}）｜目录={danmaku.Directory}");
        }
        catch (Exception ex)
        {
            Step("S5② 弹幕磁盘缓存（按 match-name；二次取数命中缓存）", false, ex.ToString().Split('\n')[0]);
        }

        // ③c 数据迁移：旧根 → 新根，条目数前后一致；再跑一次幂等（复制 0、跳过 N）
        try
        {
            var legacy = Path.Combine(s5Root, "legacy-root");
            var target = Path.Combine(s5Root, "migrated-root");
            Directory.CreateDirectory(Path.Combine(legacy, "sub"));
            File.WriteAllText(Path.Combine(legacy, TestServersFileName), "{\"servers\":[]}");
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "{\"theme\":\"dark\"}");
            File.WriteAllText(Path.Combine(legacy, "sub", "extra.txt"), "x");

            var migration = new DataMigrationService(legacy, target, m => s5Log.Add(m));
            var run1 = migration.Run();
            var run2 = migration.Run();
            var sourceStillThere = File.Exists(Path.Combine(legacy, TestServersFileName)) && File.Exists(Path.Combine(legacy, "sub", "extra.txt"));

            var ok = run1.Copied == 3 && run1.Failed == 0 && run1.TargetFilesAfter == run1.SourceFiles && run1.TargetFilesAfter == 3
                     && run2.Copied == 0 && run2.Skipped == 3 && run2.Failed == 0 && run2.TargetFilesAfter == 3
                     && sourceStillThere;
            Step("S5③ 数据迁移（旧根→新根；首跑全复制 + 二跑幂等 + 源不被删）", ok,
                $"源 {run1.SourceFiles} 个文件 ⇒ 首跑：目标 {run1.TargetFilesBefore}⇒{run1.TargetFilesAfter}（复制 {run1.Copied}/跳过 {run1.Skipped}/失败 {run1.Failed}）；" +
                $"二跑：复制 {run2.Copied}/跳过 {run2.Skipped}/失败 {run2.Failed}（目标仍 {run2.TargetFilesAfter}）｜源文件仍在={sourceStillThere}");
        }
        catch (Exception ex)
        {
            Step("S5③ 数据迁移（旧根→新根；首跑全复制 + 二跑幂等 + 源不被删）", false, ex.ToString().Split('\n')[0]);
        }

        // ③d 设备 ID：新实例稳定 + 备份/恢复后保持（并记录"无备份删除"的语义）
        try
        {
            var device = new DeviceIdService(s5Data, m => s5Log.Add(m));
            var v1 = device.GetOrCreate();
            var v2 = new DeviceIdService(s5Data).GetOrCreate();                 // 新实例（模拟重启）
            var backup = Path.Combine(s5Root, "backup", "device-id.txt");
            device.BackupTo(backup);
            File.Delete(device.FilePath);                                        // 模拟重装丢文件
            var restored = device.RestoreFrom(backup);                           // 从备份恢复
            var v4 = new DeviceIdService(s5Data).GetOrCreate();

            var noBackupDir = Path.Combine(s5Root, "no-backup");
            var noBackupService = new DeviceIdService(new AppDataDir(noBackupDir, false));
            var fresh1 = noBackupService.GetOrCreate();

            var ok = v1 == v2 && restored == v1 && v4 == v1 && v1.Length >= DeviceIdService.MinLength
                     && fresh1.Length >= DeviceIdService.MinLength;
            Step("S5④ 设备 ID（新实例稳定 / 备份恢复后保持）", ok,
                $"首次={v1}｜新实例={v2}（同值={v1 == v2}）｜备份→删文件→恢复={restored}（同值={restored == v1}）｜恢复后重读={v4}（同值={v4 == v1}）；" +
                $"另注：**无备份**的另一个数据根生成新值={fresh1}（设计如此：删文件且无备份 = 设备重装语义，不是缺陷）｜文件={device.FilePath}");
        }
        catch (Exception ex)
        {
            Step("S5④ 设备 ID（新实例稳定 / 备份恢复后保持）", false, ex.ToString().Split('\n')[0]);
        }

        // ③e 固定端口：两次"启动"解析出同一端口（回调 URL 稳定）
        try
        {
            var portA = new ConfigPortService(s5Data, m => s5Log.Add("PORT-A " + m));
            var p1 = portA.Resolve();
            var url1 = portA.CallbackUrl;
            var portB = new ConfigPortService(s5Data, m => s5Log.Add("PORT-B " + m));
            var p2 = portB.Resolve();
            var url2 = portB.CallbackUrl;

            var ok = p1 == p2 && p1 >= 49152 && p1 <= 65535 && url1 == url2 && ConfigPortService.IsFree(p1);
            Step("S5⑤ 固定端口（两次启动回调 URL 的端口一致）", ok,
                $"第一次：port={p1} url={url1}｜第二次（新实例）：port={p2} url={url2}｜一致={p1 == p2 && url1 == url2}｜端口此刻空闲={ConfigPortService.IsFree(p1)}｜落盘={portA.FilePath}");
        }
        catch (Exception ex)
        {
            Step("S5⑤ 固定端口（两次启动回调 URL 的端口一致）", false, ex.ToString().Split('\n')[0]);
        }

        // ③f 系统代理 + 外部 mpv：配了就走、没配走默认（各一次）
        try
        {
            var configuredProxy = new ProxySetting { Enabled = true, Server = "http://127.0.0.1:7890" };
            var argOn = WindowsProxy.ToKernelArgument(configuredProxy);
            var argOff = WindowsProxy.ToKernelArgument(new ProxySetting { Enabled = false, Server = "http://127.0.0.1:7890" });
            var realProxy = WindowsProxy.Read();                     // 真机读数（只观察，不作判据）
            var realArg = WindowsProxy.ToKernelArgument(realProxy);

            var mpv = new ExternalMpvService(s5Data, m => s5Log.Add(m));
            var stubExe = Path.Combine(s5Root, "mpv.exe");
            File.WriteAllText(stubExe, "stub");
            var on = mpv.Resolve(stubExe);
            var off = mpv.Resolve(string.Empty);
            var missing = mpv.Resolve(Path.Combine(s5Root, "does-not-exist.exe"));
            mpv.SaveConfiguredPath(stubExe);
            var persisted = new ExternalMpvService(s5Data).Resolve();

            var ok = argOn == "--http-proxy=http://127.0.0.1:7890" && argOff == null
                     && on.UseExternal && on.Argument == "--mpv-path=" + stubExe
                     && !off.UseExternal && off.Argument == null
                     && !missing.UseExternal
                     && persisted.UseExternal && persisted.Argument == on.Argument;
            Step("S5⑥ 系统代理与外部 mpv（配了就走 / 没配走默认）", ok,
                $"代理：配了 ⇒ {argOn ?? "null"}；没配（ProxyEnable=0）⇒ {argOff ?? "null"}；真机系统代理读数=[{realProxy}] ⇒ 内核参数={realArg ?? "null"}；" +
                $"外部 mpv：配了且存在 ⇒ {on}；没配 ⇒ {off}；配了但不存在 ⇒ {missing}；落盘后新实例读回 ⇒ useExternal={persisted.UseExternal}");
        }
        catch (Exception ex)
        {
            Step("S5⑥ 系统代理与外部 mpv（配了就走 / 没配走默认）", false, ex.ToString().Split('\n')[0]);
        }

        // ③g 快捷键组：落盘 + 冲突校验（默认无冲突；人为撞码必被检出）
        try
        {
            var shortcuts = new PlayerShortcutsStore(s5Data, m => s5Log.Add(m));
            var defaults = shortcuts.Load();
            var cleanReport = PlayerShortcutsStore.Validate(defaults);
            var clash = new HostShortcuts { ToggleMute = defaults.PlayPause };
            var clashReport = PlayerShortcutsStore.Validate(clash);
            shortcuts.Save(clash);
            var reloaded = new PlayerShortcutsStore(s5Data).Load();

            var ok = !cleanReport.HasConflict && clashReport.HasConflict
                     && reloaded.ToggleMute == reloaded.PlayPause;
            Step("S5⑦ 快捷键组（落盘 + 冲突校验）", ok,
                $"默认键位校验={cleanReport}｜人为把 ToggleMute 设成 PlayPause 的码 ⇒ {clashReport}｜落盘后重载 ToggleMute={reloaded.ToggleMute} / PlayPause={reloaded.PlayPause}（撞码成立={reloaded.ToggleMute == reloaded.PlayPause}）｜文件={shortcuts.FilePath}");
        }
        catch (Exception ex)
        {
            Step("S5⑦ 快捷键组（落盘 + 冲突校验）", false, ex.ToString().Split('\n')[0]);
        }

        // ③h IME 状态机：组字中回车只提交候选、不触发搜索
        try
        {
            var ime = new ImeStateService();
            var enterIdle = ime.ShouldTriggerSearchOnEnter;
            ime.BeginComposition("fu");
            var enterComposing = ime.ShouldTriggerSearchOnEnter;
            var escComposing = ime.ShouldClearInputOnEscape;
            var committed = ime.EndComposition("芙");
            var enterAfter = ime.ShouldTriggerSearchOnEnter;

            var ok = enterIdle && !enterComposing && !escComposing && enterAfter && committed == "芙";
            Step("S5⑧ IME 状态（组字中 Enter 只提交候选、不触发搜索）", ok,
                $"未组字 ShouldTriggerSearchOnEnter={enterIdle}（期望 true）｜组字中={enterComposing}（期望 false）｜组字中 Esc 只清组字={escComposing}（期望 false）｜提交={committed}｜提交后={enterAfter}（期望 true）");
        }
        catch (Exception ex)
        {
            Step("S5⑧ IME 状态（组字中 Enter 只提交候选、不触发搜索）", false, ex.ToString().Split('\n')[0]);
        }

        // ③i 音频状态：调音量 → 新实例（模拟重启）仍保留
        try
        {
            var audio = new AudioPlaybackStateStore(s5Data, m => s5Log.Add(m));
            var before = audio.Get("srv-1").Volume;
            var afterAdjust = audio.AdjustVolume("srv-1", -0.25);
            var reread = new AudioPlaybackStateStore(s5Data).Get("srv-1");
            var clamped = audio.SetVolume("srv-1", 2.0);
            audio.SetAudioTrack("srv-1", 3, "jpn");
            var withTrack = new AudioPlaybackStateStore(s5Data).Get("srv-1");

            var ok = Math.Abs(before - 1.0) < 1e-9
                     && Math.Abs(afterAdjust - 0.75) < 1e-9
                     && Math.Abs(reread.Volume - 0.75) < 1e-9
                     && clamped == 1.0
                     && withTrack.AudioStreamIndex == 3 && withTrack.AudioLanguage == "jpn";
            Step("S5⑨ 音频状态（音量/音轨落盘，重启后保留）", ok,
                $"调整前 volume={before} ⇒ 调 -0.25 后 {afterAdjust} ⇒ **新实例读回 {reread.Volume}**（保留={Math.Abs(reread.Volume - 0.75) < 1e-9}）｜越界夹取 SetVolume(2.0)={clamped}（期望 1）｜音轨落盘后读回 index={withTrack.AudioStreamIndex} lang={withTrack.AudioLanguage}｜文件={audio.PathFor("srv-1")}");
        }
        catch (Exception ex)
        {
            Step("S5⑨ 音频状态（音量/音轨落盘，重启后保留）", false, ex.ToString().Split('\n')[0]);
        }

        try { Directory.Delete(s5Root, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)   // 有意忽略：临时目录清理失败不影响判据（下次运行用新目录）
        {
        }

        // t30 前置（ui3 设置屏）：AppSettings 新增 MinimizeToTrayOnClose —— 默认 false + With/JSON 往返（与 Patch 同一条机制）
        try
        {
            var defaults = new AppSettings();
            var patched = defaults.With(minimizeToTrayOnClose: true);
            using var doc = System.Text.Json.JsonDocument.Parse(patched.ToJson().ToJsonString());
            var reloaded = AppSettings.FromJson(doc.RootElement);
            var jsonNode = patched.ToJson();
            var key = jsonNode["minimizeToTrayOnClose"];
            var ok = !defaults.MinimizeToTrayOnClose
                     && patched.MinimizeToTrayOnClose
                     && reloaded.MinimizeToTrayOnClose
                     && key != null && key.GetValue<bool>();
            Step("AppSettings.MinimizeToTrayOnClose（默认 false + With/JSON 往返）", ok,
                $"默认={defaults.MinimizeToTrayOnClose}（期望 False）｜With(true)={patched.MinimizeToTrayOnClose}｜JSON 键={(key == null ? "缺 ❌" : key.ToJsonString())}｜反序列化回读={reloaded.MinimizeToTrayOnClose}");
        }
        catch (Exception ex)
        {
            Step("AppSettings.MinimizeToTrayOnClose（默认 false + With/JSON 往返）", false, ex.ToString().Split('\n')[0]);
        }

        // ④ E-P2（t39）内核控制客户端：**宿主级实测**（真发 HTTP）——回推解析 / 204 / 400 / 不可达 / 无端点 / 载荷形状
        //    被测 = shell/Services/Playback/KernelControlClient.cs（内核侧对端在 t38 已实测；此处用回环 mock 替身复现同契约）
        try
        {
            var seen = new List<string>();
            using var kernelMock = new MockHttpServer(req =>
            {
                lock (seen) { seen.Add($"{req.Method} {req.Path} :: {req.Body}"); }
                if (req.Path != "/control") { return MockResponse.Text("not-found", 404); }
                if (req.Method != "POST") { return MockResponse.Text("method-not-allowed", 405); }
                var raw = req.Body ?? string.Empty;
                if (raw.IndexOf("\"Commands\"", StringComparison.Ordinal) < 0)
                {
                    // 内核真实行为：外层键大小写不符 ⇒ 绑定为空 ⇒ 400（t38 反控实证）
                    return MockResponse.Json("{\"index\":0,\"name\":\"\",\"error\":\"empty-commands\"}", 400);
                }
                if (raw.IndexOf("\"trackId\":999", StringComparison.Ordinal) >= 0 || raw.IndexOf("\"value\":\"999\"", StringComparison.Ordinal) >= 0)
                {
                    // 内核真实行为（t38 §5(b) 已修 K11）：不存在的 track id ⇒ 400；错误体按契约用 `kind`（旧键 `name` 亦回落）
                    return MockResponse.Json("{\"index\":0,\"kind\":\"SetAudioTrack\",\"error\":\"bad-track:trackId:999\"}", 400);
                }
                return MockResponse.Empty(204);
            }).Start();

            var progressBody =
                "{\"event\":\"progress\",\"positionSeconds\":12.5,\"isPaused\":false,\"durationSeconds\":1200," +
                "\"audioStreamIndex\":1,\"subtitleStreamIndex\":2,\"subtitleOffset\":0.75,\"volumeLevel\":35," +
                "\"isMuted\":false,\"playbackRate\":1.5,\"aid\":3,\"sid\":5,\"updateUrl\":\"" + kernelMock.BaseUrl + "/segments\"}";

            using var live = new KernelControlClient();
            var state = live.ApplyProgressBody(progressBody);
            var resolved = live.TryResolveControlUri(out var controlUri, out var uriError);
            var badBody = live.ApplyProgressBody("{ 这不是 JSON");
            var stateOk = state != null
                          && live.HasEndpoint && live.UpdateUrl == kernelMock.BaseUrl + "/segments"
                          && state.Aid == 3 && state.Sid == 5 && state.Speed == 1.5 && state.SubDelay == 0.75 && state.Volume == 35
                          && state.EmbyAudioStreamIndex == 1 && state.EmbySubtitleStreamIndex == 2
                          && resolved && controlUri == kernelMock.BaseUrl + "/control"
                          && badBody == null && live.HasEndpoint;
            Step("S6① 内核回推状态解析（aid/sid/speed/subDelay/volume + updateUrl ⇒ /control；坏载荷不丢端点）", stateOk,
                $"updateUrl={live.UpdateUrl} ⇒ 控制地址={controlUri}（err={uriError}）｜mpv: aid={state?.Aid} sid={state?.Sid} speed={state?.Speed} subDelay={state?.SubDelay} volume={state?.Volume}" +
                $"｜emby 流序号 audio={state?.EmbyAudioStreamIndex} sub={state?.EmbySubtitleStreamIndex}｜坏载荷 ⇒ 解析={(badBody == null ? "null（未抛异常）" : "非 null ❌")} 端点仍在={live.HasEndpoint}｜原始回推={state}");

            var r204 = await live.SendAsync(KernelControlCommand.SetSpeed(1.25));
            var sentBody = string.Empty;
            lock (seen) { sentBody = seen.Count > 0 ? seen[seen.Count - 1] : string.Empty; }
            var ok204 = r204.Success && r204.StatusCode == 204 && r204.Outcome == KernelControlOutcome.Success && r204.IsReachable
                        && sentBody == "POST /control :: {\"Commands\":[{\"kind\":\"SetSpeed\",\"rate\":1.25}]}";
            Step("S6② POST {updateUrl}/control ⇒ 204（实测走出 HTTP，请求行与载荷形状逐字符比对；契约形态 = kind + 类型化字段）", ok204,
                $"结果={r204}｜服务端实收=「{sentBody}」（期望 `POST /control :: {{\"Commands\":[{{\"kind\":\"SetSpeed\",\"rate\":1.25}}]}}`）");

            var r400 = await live.SendAsync(KernelControlCommand.SetAudioTrack(999));
            var ok400 = !r400.Success && r400.StatusCode == 400 && r400.Outcome == KernelControlOutcome.Rejected
                        && r400.ErrorCommand == "SetAudioTrack" && r400.Error == "bad-track:trackId:999" && r400.ErrorIndex == 0
                        && r400.IsReachable && r400.UserFacingFailure.Length > 0;
            Step("S6③ 内核 400 ⇒ 结构化失败（index/kind/error 可读，不得当成成功；给 UI 的降级文案非空）", ok400,
                $"结果={r400}｜error={r400.Error} name={r400.ErrorCommand} index={r400.ErrorIndex}｜给 UI：{r400.UserFacingFailure}");

            var deadPort = 0;
            using (var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
            {
                probe.Start();
                deadPort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
            }
            using var dead = new KernelControlClient(timeout: TimeSpan.FromSeconds(3));
            dead.SetUpdateUrl($"http://127.0.0.1:{deadPort}/segments");
            var rDead = await dead.SendAsync(KernelControlCommand.SetVolume(10));
            using var none = new KernelControlClient(timeout: TimeSpan.FromSeconds(3));
            var rNone = await none.SendAsync(KernelControlCommand.SetVolume(10));
            var okDegrade = !rDead.Success && rDead.Outcome == KernelControlOutcome.EndpointUnreachable
                            && rDead.StatusCode == 0 && !rDead.IsReachable && rDead.UserFacingFailure.Length > 0
                            && !rNone.Success && rNone.Outcome == KernelControlOutcome.NoEndpoint && rNone.StatusCode == 0;
            Step("S6④ 降级链判据（端点不可达 / 未收到 updateUrl ⇒ 明确失败，绝不假装成功）", okDegrade,
                $"不可达(127.0.0.1:{deadPort}) ⇒ {rDead.Outcome} status={rDead.StatusCode} 文案=「{rDead.UserFacingFailure}」error={rDead.Error}" +
                $"｜无端点 ⇒ {rNone.Outcome} status={rNone.StatusCode} 文案=「{rNone.UserFacingFailure}」");

            // 载荷面：外层键恒 PascalCase；数值走 InvariantCulture（de-DE 下不得出现 1,5）；卡面 TypedKind 形态亦可产出
            var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
            string payloadNameValue;
            string payloadTyped;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                payloadNameValue = KernelControlPayload.Build(new[]
                {
                    KernelControlCommand.SetAudioTrack(3), KernelControlCommand.SetSubtitleTrack(5),
                    KernelControlCommand.SetSubtitleDelay(0.5), KernelControlCommand.SetSubtitleVisibility(true),
                    KernelControlCommand.AddExternalSubtitle("C:\\x\\s.srt"), KernelControlCommand.SetSpeed(1.25),
                    KernelControlCommand.SetVolume(80), KernelControlCommand.SeekAbsolute(123.4),
                }, KernelControlSchema.NameValue);   // ⚠️ 已废弃形态：只做历史对照，内核会回 400 legacy-schema
                payloadTyped = KernelControlPayload.Build(new[]
                {
                    KernelControlCommand.SetAudioTrack(3), KernelControlCommand.SetSubtitleDelay(0.5),
                });                                  // 默认 = 契约形态（TypedKind）
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previousCulture;
            }
            var expectedTyped = "{\"Commands\":[{\"kind\":\"SetAudioTrack\",\"trackId\":3},{\"kind\":\"SetSubtitleDelay\",\"seconds\":0.5}]}";
            var okPayload = payloadTyped == expectedTyped
                            && payloadTyped.IndexOf("\"commands\"", StringComparison.Ordinal) < 0     // 外层恒 PascalCase
                            && payloadTyped.IndexOf("\"Kind\"", StringComparison.Ordinal) < 0          // 内层恒 camelCase
                            && payloadNameValue.IndexOf("{\"Commands\":[", StringComparison.Ordinal) == 0
                            && payloadNameValue.Contains("\"name\":\"speed\"") && payloadNameValue.Contains("\"value\":\"1,25\"") == false;
            Step("S6⑤ 载荷形状（契约形态：外层 `Commands` + 内层 camelCase 类型化字段；de-DE 下仍 `1.25`；废弃形态可复算）", okPayload,
                $"契约形态（默认，de-DE 区域）= {payloadTyped}（期望 {expectedTyped}）｜外壳键以 `{{\"Commands\":[` 开头（实测 IndexOf={payloadTyped.IndexOf("{\"Commands\":[", StringComparison.Ordinal)}，期望 0）｜" +
                $"内层大写 `\"Kind\"` 出现={payloadTyped.IndexOf("\"Kind\"", StringComparison.Ordinal) >= 0}（期望 False）｜已废弃形态（对照）= {payloadNameValue}");

            // 回归：**旧内核**（无 aid/sid 字段）的 progress 体仍要能解析 —— 既有字段语义未变
            var legacyBody = "{\"event\":\"progress\",\"positionSeconds\":5,\"playbackRate\":1.0,\"subtitleOffset\":0.0,\"volumeLevel\":100,\"audioStreamIndex\":0,\"subtitleStreamIndex\":-1,\"updateUrl\":\"" + kernelMock.BaseUrl + "/segments\"}";
            using var legacy = new KernelControlClient();
            var legacyState = legacy.ApplyProgressBody(legacyBody);
            var okLegacy = legacyState != null && !legacyState.Aid.HasValue && !legacyState.Sid.HasValue
                           && legacyState.Speed == 1.0 && legacyState.Volume == 100 && legacyState.HasUpdateUrl;
            Step("S6⑥ 回归：旧格式 progress（无 aid/sid）仍可解析（既有字段语义不变）", okLegacy,
                $"旧体 ⇒ {legacyState}｜aid/sid 缺省为 {(legacyState?.Aid.HasValue ?? false)}/{(legacyState?.Sid.HasValue ?? false)}（期望 False/False）");
        }
        catch (Exception ex)
        {
            Step("S6①–⑥ 内核控制客户端（宿主级实测）", false, ex.ToString().Split('\n')[0]);
        }

        // ⑤ t44-A：聚合搜索**逐源增量**（快源先回 ⇒ 先显示；同键就地合并；坏源进失败列表；终值等价 SearchAsync）
        try
        {
            // 慢源 300 ms / 快源 20 ms：两者返回顺序与"谁先回调"必须一致
            var slowHit = new AggregatedSearchHit { ServerId = "srv-slow", ServerName = "慢源", Id = "id-slow-1", Name = "乙条目", Type = "Movie", ProductionYear = 2001 };
            var fastHit = new AggregatedSearchHit { ServerId = "srv-fast", ServerName = "快源", Id = "id-fast-1", Name = "甲条目", Type = "Movie", ProductionYear = 2002 };
            // 同键双源：Name/Type/Year 全同 ⇒ KeyOf 相同（去重键不能被增量打破）
            var dupA = new AggregatedSearchHit { ServerId = "srv-a", ServerName = "A 服", Id = "dup-a", Name = "同名片", Type = "Movie", ProductionYear = 2020 };
            var dupB = new AggregatedSearchHit { ServerId = "srv-b", ServerName = "B 服", Id = "dup-b", Name = "同名片", Type = "Movie", ProductionYear = 2020 };

            var sources = new List<IAggregatedSearchSource>
            {
                new FakeAggregatedSource("慢源", new[] { slowHit }, delayMs: 300),
                new FakeAggregatedSource("快源", new[] { fastHit }, delayMs: 20),
                new FakeAggregatedSource("同键A", new[] { dupA }, delayMs: 5),
                new FakeAggregatedSource("同键B", new[] { dupB }, delayMs: 15),
                new FakeAggregatedSource("坏源", null, delayMs: 10, throwIt: true),
            };
            var aggregator = new AggregatedSearchService(sources, m => s5Log.Add(m));
            var progress = new CollectingProgress<AggregatedSearchProgress>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var final = await aggregator.SearchIncrementalAsync("测试", limit: 10, progress: progress).ConfigureAwait(false);
            sw.Stop();
            var arrivals = progress.Items;
            var first = arrivals.Count > 0 ? arrivals[0] : null;
            // ⚠️ 不断言"第一个回调必须是某个 5 ms 源"（同毫秒级并发会因调度抖动而翻转，那是假失败）；
            //    真正要证的是：**慢源之前已经有多个源回调过** + 慢源在最后（⇒ 不是等全部）。
            var firstNotSlow = first != null && first.ArrivedServerName != "慢源";
            var arrivedBeforeSlow = arrivals.TakeWhile(p => p.ArrivedServerName != "慢源").Count();
            var fastBeforeSlow = firstNotSlow && arrivedBeforeSlow >= 3;
            var slowArrivalIndex = arrivals.FindIndex(p => p.ArrivedServerName == "慢源");
            var fastArrivalIndex = arrivals.FindIndex(p => p.ArrivedServerName == "快源");
            var notWaitingForAll = fastArrivalIndex >= 0 && slowArrivalIndex >= 0 && fastArrivalIndex < slowArrivalIndex;
            var incrementalKeptOneCard = arrivals.TrueForAll(p => p.MergedCount <= 3);
            var dupEntry = final.Items.Find(e => e.Primary != null && e.Primary.Name == "同名片");
            // 5 源 = 乙条目(慢) + 甲条目(快) + 同名片(两源合并成 1 条) + 坏源(无条目) ⇒ 条目 3 / 命中 4
            var dedupOk = final.MergedCount == 3 && final.MergedHitCount == 4 && dupEntry != null
                          && dupEntry.Hits.Count == 2 && dupEntry.Sources.Count == 2 && dupEntry.HasAlternatives;
            var failedOk = final.FailedServers.Count == 1 && final.FailedServers[0] == "坏源"
                           && arrivals.Exists(p => p.FailedThisTime && p.ArrivedServerName == "坏源")
                           && final.Items.Count == 3;                                                   // 坏源不影响已到达结果
            var lastIsFinal = arrivals.Count == 5 && arrivals[4].IsFinal && arrivals[4].PendingSourceCount == 0;
            var okIncremental = fastBeforeSlow && notWaitingForAll && incrementalKeptOneCard && dedupOk && failedOk && lastIsFinal;
            Step("S7① 聚合搜索逐源增量（快源先回调 ⇒ 不是等全部；同键就地合并 ⇒ 无第二张卡；坏源进失败列表）", okIncremental,
                $"回调次数={arrivals.Count}（源数 5）｜慢源之前的回调数={arrivedBeforeSlow}（期望 ≥3：5/15/20 ms 三源）｜首个回调={first?.ArrivedServerName}（不得是 300 ms 的「慢源」）｜快源#{fastArrivalIndex} < 慢源#{slowArrivalIndex}（期望 true）｜" +
                $"终值 条目={final.Items.Count} 合并数={final.MergedCount}/命中={final.MergedHitCount}（期望 3/3/4）｜同名片 Hits={dupEntry?.Hits.Count} Sources={dupEntry?.Sources.Count} HasAlternatives={dupEntry?.HasAlternatives}（期望 2/2/True）｜" +
                $"失败={string.Join(",", final.FailedServers)}｜最后一发 IsFinal={arrivals.Count > 0 && arrivals[arrivals.Count - 1].IsFinal}（期望 True）｜耗时={sw.ElapsedMilliseconds} ms｜" +
                $"末次快照={arrivals[arrivals.Count - 1]}");

            // 终值必须与既有 SearchAsync **逐字节等价**（纯加法的判据）
            var viaWhenAll = await aggregator.SearchAsync("测试", 10).ConfigureAwait(false);
            var sameAsLegacy = viaWhenAll.Items.Count == final.Items.Count
                               && viaWhenAll.MergedCount == final.MergedCount
                               && viaWhenAll.MergedHitCount == final.MergedHitCount
                               && viaWhenAll.TotalHits == final.TotalHits
                               && viaWhenAll.FailedServers.Count == final.FailedServers.Count
                               && string.Join("|", viaWhenAll.Items.ConvertAll(e => e.Primary?.Name)) == string.Join("|", final.Items.ConvertAll(e => e.Primary?.Name));
            Step("S7② 回归：增量终值 == 既有 SearchAsync（同源同词，逐字段一致 ⇒ 纯加法、零语义改动）", sameAsLegacy,
                $"SearchAsync: 条目={viaWhenAll.Items.Count} 合并={viaWhenAll.MergedCount} 命中={viaWhenAll.MergedHitCount} 总计={viaWhenAll.TotalHits} 失败={viaWhenAll.FailedServers.Count}｜" +
                $"增量终值: 条目={final.Items.Count} 合并={final.MergedCount} 命中={final.MergedHitCount} 总计={final.TotalHits} 失败={final.FailedServers.Count}｜" +
                $"顺序={string.Join(" → ", final.Items.ConvertAll(e => e.Primary?.Name))}");
        }
        catch (Exception ex)
        {
            Step("S7①–② 聚合搜索逐源增量", false, ex.ToString().Split('\n')[0]);
        }

        // ⑥ t44-B：通用 SWR 快照（冷启动 / 热启动先出缓存且零网络 / 刷新后替换 / 清缓存退化为冷启动）
        try
        {
            var swrRoot = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck-t44-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(swrRoot);
            var cacheA = new SwrSnapshotCache<List<string>>(swrRoot, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));
            var network = 0;
            Func<CancellationToken, Task<List<string>>> fetch = _ =>
            {
                Interlocked.Increment(ref network);
                return Task.FromResult(new List<string> { "网络数据" });
            };

            // 冷启动：无缓存 ⇒ Miss ⇒ 走网络
            var coldLookup = cacheA.TryRead("favorites:srv-1");
            var cold = await cacheA.RefreshAsync("favorites:srv-1", fetch).ConfigureAwait(false);
            var afterCold = cacheA.TryRead("favorites:srv-1");
            var networkAfterCold = network;      // 冷启动后计数（后续热启动会再 +1，故不能到最后才比）

            // 热启动：新实例（模拟重启）⇒ 先出缓存（**此刻网络计数仍为 1**）→ 再刷新 ⇒ 网络计数 +1
            var networkAtCallback = -1;
            var cacheB = new SwrSnapshotCache<List<string>>(swrRoot, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));
            var hot = await cacheB.ReadThenRefreshAsync(
                "favorites:srv-1",
                fetch,
                lookup =>
                {
                    networkAtCallback = network;                 // 回调里读：必须还没发网络
                    Console.WriteLine($"    SWR 先显示缓存 ⇒ {lookup}");
                }).ConfigureAwait(false);

            // 反控：清掉缓存 ⇒ 同一条热启动路径必须退化成冷启动（Miss）
            var cleared = cacheB.Clear();
            var afterClear = cacheB.TryRead("favorites:srv-1");

            var okSwr = coldLookup.Freshness == SwrFreshness.Miss && !coldLookup.HasValue
                        && cold.Success && networkAfterCold == 1
                        && afterCold.Freshness == SwrFreshness.Fresh && afterCold.HasValue && afterCold.Value.Count == 1
                        && networkAtCallback == 1                              // 回调时尚未发起新请求
                        && hot.Success && hot.PreviousFreshness == SwrFreshness.Fresh && hot.Value.Count == 1
                        && cleared >= 1 && afterClear.Freshness == SwrFreshness.Miss && !afterClear.HasValue;
            Step("S7③ 通用 SWR 快照（冷启动 Miss→网络；热启动先出缓存且**零网络**；刷新后拿到新值；清缓存 ⇒ 退化为冷启动）", okSwr,
                $"冷：TryRead={coldLookup} ⇒ Refresh {cold}｜读回={afterCold}｜热：回调时网络计数={networkAtCallback}（期望 1 = 尚未再发请求）⇒ {hot}｜" +
                $"清除条数={cleared} ⇒ TryRead={afterClear}（期望 Miss）｜网络总调用={network}｜目录={cacheA.Directory} TTL={cacheA.Ttl.TotalMinutes} 分钟");

            // 坏缓存必须当 Miss（不得当 Fresh 显示半截内容）
            File.WriteAllText(cacheA.PathFor("broken"), "{ 这不是 JSON");
            var broken = cacheA.TryRead("broken");
            Step("S7④ SWR 反控：坏快照 ⇒ 当 Miss（不得当 Fresh）；失败刷新不落盘", broken.Freshness == SwrFreshness.Miss && !broken.HasValue,
                $"坏文件={cacheA.PathFor("broken")} ⇒ {broken}（期望 Miss）");

            try { Directory.Delete(swrRoot, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)   // 有意忽略：临时目录清理失败不影响判据
            {
            }
        }
        catch (Exception ex)
        {
            Step("S7③–④ 通用 SWR 快照", false, ex.ToString().Split('\n')[0]);
        }

        // ⑦ t44 卡面三条硬要求：取消**真能中断在途源** / 无快照必须是 **null**（不得空集合冒充）/ 坏快照**隔离 .corrupt** / 按服务器失效
        try
        {
            // (a) 取消：3 s 慢源，80 ms 后就取消 ⇒ 必须抛 OperationCanceledException 且远早于 3 s 返回
            var cancelSources = new List<IAggregatedSearchSource>
            {
                new FakeAggregatedSource("超慢源", new[] { new AggregatedSearchHit { ServerId = "srv-x", ServerName = "超慢源", Id = "id-x", Name = "取消用条目", Type = "Movie" } }, delayMs: 3000),
            };
            using var cts = new CancellationTokenSource(80);
            var swCancel = System.Diagnostics.Stopwatch.StartNew();
            var cancelled = false;
            try
            {
                await new AggregatedSearchService(cancelSources).SearchIncrementalAsync("测试", 10, new CollectingProgress<AggregatedSearchProgress>(), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            swCancel.Stop();

            // (b)–(d) serverId 作用域 API + 坏快照隔离 + 按服务器失效
            var dir2 = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck-t44b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir2);
            var scoped = new SwrSnapshotCache<List<string>>(dir2, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));

            var noSnapshot = scoped.TryGetSnapshot("srv-9", "favorites");            // 无快照 ⇒ Miss + null
            var nullNot = noSnapshot.Freshness == SwrFreshness.Miss && noSnapshot.Value == null && !noSnapshot.HasValue;

            scoped.SaveSnapshot("srv-9", "favorites", new List<string> { "来自服务器" });
            var saved = scoped.TryGetSnapshot("srv-9", "favorites");                 // 写后能读（带时间戳 ⇒ Fresh）
            var savedOk = saved.Freshness == SwrFreshness.Fresh && saved.HasValue && saved.Value.Count == 1
                          && saved.CachedAtUtc.HasValue && saved.AgeSeconds.HasValue;

            File.WriteAllText(scoped.PathFor("srv-9::favorites"), "{ 这不是 JSON");   // 人为损坏
            var brokenLookup = scoped.TryGetSnapshot("srv-9", "favorites");
            var quarantined = File.Exists(scoped.PathFor("srv-9::favorites") + ".corrupt");
            var brokenOk = brokenLookup.Freshness == SwrFreshness.Miss && brokenLookup.Value == null && quarantined;

            scoped.SaveSnapshot("srv-9", "k1", new List<string> { "9" });
            scoped.SaveSnapshot("srv-8", "k1", new List<string> { "8" });
            var indexBefore = scoped.SnapshotIndex();
            var dropped = scoped.Invalidate("srv-9");                                // 按服务器失效
            var goneForNine = scoped.TryGetSnapshot("srv-9", "k1");
            var keptForEight = scoped.TryGetSnapshot("srv-8", "k1");
            var invalidationOk = dropped == 1 && goneForNine.Freshness == SwrFreshness.Miss && keptForEight.Freshness == SwrFreshness.Fresh;

            var okControls = cancelled && swCancel.ElapsedMilliseconds < 1000 && nullNot && savedOk && brokenOk && invalidationOk;
            Step("S7⑤ 卡面反控四条（取消真中断 / 无快照=null / 坏快照隔离 .corrupt / 按服务器失效）", okControls,
                $"取消：慢源 3000 ms + 80 ms 取消 ⇒ caught={cancelled} 耗时={swCancel.ElapsedMilliseconds} ms（期望 <1000）｜" +
                $"无快照：{noSnapshot} Value={(noSnapshot.Value == null ? "null ✅" : "非 null ❌")}｜写后读：{saved}（时间戳={saved.CachedAtUtc:HH:mm:ss}）｜" +
                $"坏快照：{brokenLookup} 隔离文件存在={quarantined}（{Path.GetFileName(scoped.PathFor("srv-9::favorites") + ".corrupt")}）｜" +
                $"按服务器失效：事前清单={indexBefore.Count} 条 ⇒ Invalidate(\"srv-9\") 删 {dropped} 条 ⇒ srv-9={goneForNine.Freshness} / srv-8={keptForEight.Freshness}（期望 Miss/Fresh）");

            try { Directory.Delete(dir2, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)   // 有意忽略：临时目录清理失败不影响判据
            {
            }
        }
        catch (Exception ex)
        {
            Step("S7⑤ 卡面反控四条", false, ex.ToString().Split('\n')[0]);
        }

        // ⑧ t44-B 上调：§12 通用「缓存优先 + 并发刷新」四反控（冷启动可退化 / 清缓存必退化 / 坏源保留缓存 / 两组 id 分别打印）
        try
        {
            var dir3 = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck-t44c-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir3);
            var loader = new SwrSnapshotCache<List<string>>(dir3, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));
            Func<List<string>, IEnumerable<string>> idsOf = list => list;

            // S8① §12.1 并发取证：先播种缓存（旧 id 集合），再 LoadAsync（fetch 故意慢 250 ms）
            loader.SaveSnapshot("srv-1", "favorites", new List<string> { "旧A", "旧B" });
            var fetchCalls = 0;
            var fetchInvokedBeforeRender = false;
            var renderSawCache = false;
            var fetchStartedAt = DateTime.MinValue;
            var renderAt = DateTime.MinValue;
            Func<CancellationToken, Task<List<string>>> slowFetch = async ct =>
            {
                fetchCalls++;
                fetchStartedAt = DateTime.UtcNow;                 // 请求真正发出
                await Task.Delay(250, ct).ConfigureAwait(false);
                return new List<string> { "旧A", "旧B", "新增C" };
            };
            var loaded = await loader.LoadAsync(
                "srv-1::favorites",
                slowFetch,
                onCacheValue: lookup =>
                {
                    renderAt = DateTime.UtcNow;                   // T0 渲染时刻
                    renderSawCache = lookup.HasValue;
                    fetchInvokedBeforeRender = fetchCalls > 0;    // 渲染时请求已在途 ⇒ 并发（不是"渲染完再发"）
                },
                idsOf: idsOf).ConfigureAwait(false);
            var concurrentOk = renderSawCache && fetchInvokedBeforeRender
                               && fetchStartedAt <= renderAt
                               && loaded.CacheHit && loaded.RefreshStarted && loaded.RefreshSucceeded
                               && string.Join(",", loaded.CachedIds) == "旧A,旧B"
                               && string.Join(",", loaded.RefreshedIds) == "旧A,旧B,新增C"
                               && loaded.ElapsedMs >= 200;                                // 确实等了慢源
            Step("S8① §12.1 缓存优先 + **同一时刻并发**（请求已在途时渲染；cachedIds 与 refreshedIds **分别打印**）", concurrentOk,
                $"T0 渲染时 fetch 已发出={fetchInvokedBeforeRender}（期望 True）｜fetchStartedAt={fetchStartedAt:HH:mm:ss.fff} ≤ renderAt={renderAt:HH:mm:ss.fff}｜" +
                $"cache={(loaded.CacheHit ? "hit" : "miss")} age={loaded.CacheAgeSeconds:0.0}s refresh={(loaded.RefreshSucceeded ? "done" : "failed")}｜" +
                $"cachedIds=[{string.Join(",", loaded.CachedIds)}]（期望 旧A,旧B）｜refreshedIds=[{string.Join(",", loaded.RefreshedIds)}]（期望 旧A,旧B,新增C）｜elapsed={loaded.ElapsedMs:0}ms｜{loaded}");

            // S8② §12.4②：清缓存后热路径**必须退化**（证明内容真来自缓存，不是内存副本）
            var cleared2 = loader.Clear();
            var coldAgain = await loader.LoadAsync("srv-1::favorites", slowFetch, onCacheValue: _ => { }, idsOf: idsOf).ConfigureAwait(false);
            Step("S8② §12.4② 清缓存 ⇒ 热路径退化为冷启动（cache=miss，cachedIds 为空，功能仍可用）", cleared2 >= 1 && !coldAgain.CacheHit && coldAgain.CachedIds.Count == 0 && coldAgain.RefreshSucceeded,
                $"Clear 删除 {cleared2} 条 ⇒ cache={(coldAgain.CacheHit ? "hit ❌" : "miss ✅")} cachedIds=[{string.Join(",", coldAgain.CachedIds)}]（期望空）refresh={(coldAgain.RefreshSucceeded ? "done（功能仍可用）" : "failed ❌")}");

            // S8③ §12.4①：冷启动（无缓存）⇒ 骨架屏（Cached=null）但刷新照常成功
            var dir4 = Path.Combine(Path.GetTempPath(), "aiplayer-selfcheck-t44d-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir4);
            var cold = new SwrSnapshotCache<List<string>>(dir4, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));
            var renderCalls = 0;
            var coldLoad = await cold.LoadAsync(
                "home::hero",
                _ => Task.FromResult(new List<string> { "网络条目" }),
                onCacheValue: _ => renderCalls++,
                idsOf: idsOf).ConfigureAwait(false);
            var coldOk = !coldLoad.CacheHit && coldLoad.Cached == null && renderCalls == 0
                         && coldLoad.RefreshSucceeded && coldLoad.Value.Count == 1
                         && coldLoad.RefreshedIds.Count == 1 && coldLoad.CachedIds.Count == 0;
            // §12.3 末句：**无缓存 + 请求失败 ⇒ 走空态**（不得白屏/报错；Cached=null、CachePreserved=false、Error 非空）
            var coldFail = await cold.LoadAsync(
                "home::empty",
                async ct => { await Task.Delay(30, ct).ConfigureAwait(false); throw new InvalidOperationException("空态用坏源"); },
                onCacheValue: _ => renderCalls++,
                idsOf: idsOf).ConfigureAwait(false);
            var emptyStateOk = !coldFail.CacheHit && !coldFail.RefreshSucceeded && coldFail.Cached == null
                               && !coldFail.CachePreserved && !string.IsNullOrEmpty(coldFail.Error) && renderCalls == 0;
            Step("S8③ §12.4① 冷启动（无缓存）⇒ 骨架屏语义 + 刷新成功；**无缓存+失败 ⇒ 空态**（不白屏、不报错）", coldOk && emptyStateOk,
                $"冷启动：cache={(coldLoad.CacheHit ? "hit ❌" : "miss ✅")} Cached={(coldLoad.Cached == null ? "null（骨架屏）" : "非 null ❌")}｜缓存渲染回调调用次数={renderCalls}（期望 0）｜" +
                $"refresh={(coldLoad.RefreshSucceeded ? "done ✅" : "failed ❌")} refreshedIds=[{string.Join(",", coldLoad.RefreshedIds)}]｜" +
                $"无缓存+失败：cache={(coldFail.CacheHit ? "hit ❌" : "miss ✅")} refresh={(coldFail.RefreshSucceeded ? "done ❌" : "failed ✅")} " +
                $"Cached={(coldFail.Cached == null ? "null（空态）✅" : "非 null ❌")} cache-preserved={coldFail.CachePreserved}（期望 False）error={coldFail.Error}");

            // S8④ §12.3/§12.4③：坏源 ⇒ 缓存仍在 + 可见失败（**不清空、不删缓存**）
            var seed = new SwrSnapshotCache<List<string>>(dir4, TimeSpan.FromMinutes(10), log: m => s5Log.Add(m));
            seed.SaveSnapshot("home::hero", "x", new List<string> { "缓存仍在A", "缓存仍在B" });
            var brokenPath = seed.PathFor("home::hero::x");
            var failLoad = await seed.LoadAsync(
                "home::hero::x",
                async ct =>                                     // 模拟"请求已发出、随后失败"（异步坏源，非同步抛）
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                    throw new InvalidOperationException("坏源：服务器不可达");
                },
                onCacheValue: _ => { },
                idsOf: idsOf).ConfigureAwait(false);
            var failOk = failLoad.CacheHit && !failLoad.RefreshSucceeded && !string.IsNullOrEmpty(failLoad.Error)
                         && failLoad.CachePreserved && failLoad.Cached != null && failLoad.Cached.Count == 2
                         && File.Exists(brokenPath);                                  // 缓存文件未被删
            Step("S8④ §12.3/§12.4③ 坏源 ⇒ **保留缓存** + 可见失败（不清空、不删缓存文件）", failOk,
                $"cache={(failLoad.CacheHit ? "hit ✅" : "miss ❌")} refresh={(failLoad.RefreshSucceeded ? "done ❌" : "failed ✅")}｜error={failLoad.Error}｜" +
                $"cache-preserved={failLoad.CachePreserved} 缓存内容={failLoad.Cached?.Count} 条（期望 2）｜缓存文件仍在={File.Exists(brokenPath)}｜{failLoad}");

            TryDeleteTempDir(dir3);
            TryDeleteTempDir(dir4);
        }
        catch (Exception ex)
        {
            Step("S8①–④ §12 缓存优先 + 并发刷新四反控", false, ex.ToString().Split('\n')[0]);
        }

        // ⑨ t47：**adopt-on-write 接管 + 墓碑**（红线：accounts.json 全程零写入；未接管仍必须被拦）
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "aiplayer-adopt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var accountsPath = Path.Combine(root, "accounts.json");
            File.WriteAllText(accountsPath, """
            [
              { "id": "imp-1", "provider": "emby", "serverName": "导入一号", "preferredLineUrl": "https://imp1.example/emby", "sortOrder": 1 },
              { "id": "imp-2", "provider": "jellyfin", "serverName": "导入二号", "preferredLineUrl": "https://imp2.example/jellyfin", "sortOrder": 2 },
              { "id": "imp-3", "provider": "emby", "serverName": "导入三号", "preferredLineUrl": "https://imp3.example/emby", "sortOrder": 3 }
            ]
            """);
            var serversPath = Path.Combine(root, TestServersFileName);
            var vaultPath = Path.Combine(root, "credentials.bin");
            string ShaOf(string p) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p)));

            var store = ServerConfigStore.At(serversPath, SecureKvStore.At(vaultPath), accountsPath);
            store.Load();
            var shaBefore = ShaOf(accountsPath);
            var imported = store.Servers.Count;

            // (d) 反控**先做**：未接管状态下 Update / Remove 都必须被拦，且不许落盘、不许动 accounts.json
            var blockedUpdate = false;
            var blockedRemove = false;
            try
            {
                // ⚠️ 用 `With(...)` 造**游离副本**再试图写：直接改 ById 返回的对象会连带改到内存里那份（那是测试自己造的假象）
                store.Update(store.ById("imp-1").With(name: "未接管改名尝试"));
            }
            catch (InvalidOperationException) { blockedUpdate = true; }
            try { store.Remove("imp-3"); } catch (InvalidOperationException) { blockedRemove = true; }
            var serversTextWhileBlocked = File.Exists(serversPath) ? File.ReadAllText(serversPath) : string.Empty;
            var blockedOk = blockedUpdate && blockedRemove
                            && store.IsReadOnlyServer("imp-1") && !store.CanWriteServer("imp-1")
                            && store.ById("imp-1").Name == "导入一号"
                            && store.ById("imp-3") != null
                            && ShaOf(accountsPath) == shaBefore
                            && !serversTextWhileBlocked.Contains("imp-1")
                            && !serversTextWhileBlocked.Contains("imp-3");
            Step("S9① 反控(d)：**未接管**的只读行 写入/删除 必须被拦（不落盘、accounts.json 不变）", blockedOk,
                $"导入 {imported} 台｜Update 被拦={blockedUpdate} Remove 被拦={blockedRemove}（期望 True/True）｜名字未被改={store.ById("imp-1").Name == "导入一号"}（期望 True）｜" +
                $"imp-3 仍在={store.ById("imp-3") != null}｜servers.json 未含 imp-1/imp-3={!serversTextWhileBlocked.Contains("imp-1") && !serversTextWhileBlocked.Contains("imp-3")}｜accounts.json sha 不变={ShaOf(accountsPath) == shaBefore}");

            // (a) 接管 + 编辑：servers.json 出现该条目（含血缘）+ accounts.json sha256 不变
            var adopted = store.Adopt("imp-1");
            var target = store.ById("imp-1");
            target.Name = "改过的名字";
            store.Update(target);
            var serversText = File.ReadAllText(serversPath);
            // ⚠️ 不能拿原文 `Contains("改过的名字")` 断言：JsonStorage 的写入器会**把非 ASCII 转义成 \uXXXX**。
            //    ⇒ 解析 JSON 后按 id 找条目、比 `name` 字段（这才是"落盘内容"的正确读法）。
            string persistedName = null;
            using (var doc = System.Text.Json.JsonDocument.Parse(serversText))
            {
                foreach (var element in doc.RootElement.GetProperty("servers").EnumerateArray())
                {
                    if (element.TryGetProperty("id", out var idProp) && idProp.GetString() == "imp-1"
                        && element.TryGetProperty("name", out var nameProp))
                    {
                        persistedName = nameProp.GetString();
                    }
                }
            }
            var afterEdit = store.ById("imp-1");
            var adoptOk = adopted && !store.IsReadOnlyServer("imp-1") && store.CanWriteServer("imp-1")
                          && persistedName == "改过的名字"
                          && afterEdit != null && afterEdit.IsOriginalAccountEntry && ServerConfigStore.IsAdoptedEntry(afterEdit)
                          && ShaOf(accountsPath) == shaBefore;
            Step("S9② (a) 编辑导入服务器 ⇒ 接管后写入 servers.json（血缘 accountsOrigin + shellAdopted 保留）＋ accounts.json sha256 不变", adoptOk,
                $"Adopt 生效={adopted}｜只读={store.IsReadOnlyServer("imp-1")}（期望 False）｜名字={afterEdit?.Name}｜**落盘 name={persistedName}**（期望 改过的名字）｜血缘 IsOriginalAccountEntry={afterEdit?.IsOriginalAccountEntry} 已接管标记={ServerConfigStore.IsAdoptedEntry(afterEdit)}｜" +
                $"servers.json 含 imp-1={serversText.Contains("imp-1")}｜accounts.json sha 不变={ShaOf(accountsPath) == shaBefore}");

            // (b) 重启后仍在（新实例重新加载），且不出现第二份
            var reloaded = ServerConfigStore.At(serversPath, SecureKvStore.At(vaultPath), accountsPath);
            reloaded.Load();
            var reloadedEntry = reloaded.ById("imp-1");
            var restartOk = reloadedEntry != null && reloadedEntry.Name == "改过的名字"
                            && reloaded.Servers.Count(s => s.Id == "imp-1") == 1
                            && !reloaded.IsReadOnlyServer("imp-1");
            Step("S9③ (b) 接管后的写操作**重启仍在**（新实例 Load ⇒ 改名保留、条目不重复）", restartOk,
                $"重启后 imp-1 名字={reloadedEntry?.Name}｜同 id 条数={reloaded.Servers.Count(s => s.Id == "imp-1")}（期望 1）｜仍只读={reloaded.IsReadOnlyServer("imp-1")}（期望 False）｜可见集合={reloaded.Servers.Count} 台");

            // (c) 删除 ⇒ 可见集合消失 + 墓碑落盘 + 重启不复活；accounts.json sha256 仍不变
            reloaded.AdoptAndRemove("imp-2");
            var serversText2 = File.ReadAllText(serversPath);
            var afterRemove = ServerConfigStore.At(serversPath, SecureKvStore.At(vaultPath), accountsPath);
            afterRemove.Load();
            var removeOk = afterRemove.ById("imp-2") == null
                           && reloaded.IsHidden("imp-2") && afterRemove.IsHidden("imp-2")
                           && serversText2.Contains("hiddenOriginalIds")
                           && ShaOf(accountsPath) == shaBefore;
            Step("S9④ (c) 删除 ⇒ 可见集合消失 + 墓碑落盘 + **重启不复活**；accounts.json sha256 仍不变", removeOk,
                $"删除后可见 imp-2={afterRemove.ById("imp-2") != null}（期望 False）｜墓碑（内存）={reloaded.IsHidden("imp-2")}/（重载后）={afterRemove.IsHidden("imp-2")}｜" +
                $"servers.json 含 hiddenOriginalIds={serversText2.Contains("hiddenOriginalIds")}｜重载后可见={afterRemove.Servers.Count} 台（期望 {imported - 1}）｜accounts.json sha 不变={ShaOf(accountsPath) == shaBefore}"
                + $"｜accounts.json 原件仍 {imported} 条={System.Text.Json.JsonDocument.Parse(File.ReadAllText(accountsPath)).RootElement.GetArrayLength() == 3}");

            TryDeleteTempDir(root);
        }
        catch (Exception ex)
        {
            Step("S9①–④ adopt-on-write 接管 + 墓碑", false, ex.ToString().Split('\n')[0]);
        }

        // ⑩ t48：`ItemFields` 必须含 `ImageTags`（根治"外层整屏无封面"）＋ 缺凭据前置拒绝
        try
        {
            var fieldList = EmbyService.ItemFields.Split(',');
            var constOk = fieldList.Contains("ImageTags");

            using var mockTag = new MockEmbyServer().Start();
            using var httpTag = new ShellHttpClient();
            var tagServer = new ServerConfig
            {
                Id = "srv-tag",
                Kind = ServerKind.Emby,
                Name = "Mock Emby（封面标签）",
                BaseUrl = mockTag.BaseUrl,
                UserId = MockEmbyServer.UserId,
                UserName = MockEmbyServer.ValidUser,
                AccessToken = MockEmbyServer.AccessToken,
            };
            var embyTag = new EmbyService(httpTag, tagServer);

            // (i) Resume：**实际发出的请求**必须带 ImageTags；响应解出的封面标签/URL 必须非空
            var resume = await embyTag.GetResumeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var resumeReq = mockTag.Requests.LastOrDefault(r => r.Path.EndsWith("/Items/Resume", StringComparison.Ordinal));
            var resumeFields = resumeReq?.QueryValue("Fields") ?? "(无请求)";
            var firstResume = resume.Count > 0 ? resume[0] : null;
            var resumeImageUrl = firstResume == null ? string.Empty : embyTag.ImageUrl(firstResume.Id, "Primary", maxHeight: 300);
            var resumeOk = resumeFields.Contains("ImageTags")
                           && firstResume != null && !string.IsNullOrEmpty(firstResume.PrimaryImageTag)
                           && !string.IsNullOrEmpty(resumeImageUrl);
            Step("S10① GetResumeAsync 请求带 ImageTags ＋ 响应解出封面标签/URL（根治\"整屏无封面\"）", resumeOk,
                $"ItemFields 含 ImageTags={constOk}｜实发 Fields 含 ImageTags={resumeFields.Contains("ImageTags")}（query 片段={Truncate(resumeFields, 120)}）｜" +
                $"条目 {resume.Count} 条｜首条 PrimaryImageTag={firstResume?.PrimaryImageTag} ImageUrl={Truncate(MaskToken(resumeImageUrl), 100)}");
        }
        catch (Exception ex)
        {
            Step("S10① GetResumeAsync 请求带 ImageTags ＋ 响应解出封面标签/URL", false, ex.ToString().Split('\n')[0]);
        }

        // (ii) Latest：`GetLatestAsync` **没有 fields 入参** ⇒ 只能靠 ItemFields 根治（只证请求面：mock 未覆盖该端点）
        try
        {
            using var mockTag2 = new MockEmbyServer().Start();
            using var httpTag2 = new ShellHttpClient();
            var embyTag2 = new EmbyService(httpTag2, new ServerConfig
            {
                Id = "srv-tag2",
                Kind = ServerKind.Emby,
                Name = "Mock Emby（Latest）",
                BaseUrl = mockTag2.BaseUrl,
                UserId = MockEmbyServer.UserId,
                UserName = MockEmbyServer.ValidUser,
                AccessToken = MockEmbyServer.AccessToken,
            });
            var latestCount = -1;
            string latestError = null;
            try { latestCount = (await embyTag2.GetLatestAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Count; }
            catch (Exception ex) { latestError = ex.GetType().Name + ": " + ex.Message; }
            var latestReq = mockTag2.Requests.LastOrDefault(r => r.Path.EndsWith("/Items/Latest", StringComparison.Ordinal));
            var latestFields = latestReq?.QueryValue("Fields") ?? "(无请求)";
            var latestOk = latestFields.Contains("ImageTags");
            Step("S10② GetLatestAsync 请求带 ImageTags（该路径无 fields 入参 ⇒ 只能由 ItemFields 根治）", latestOk,
                $"实发 Fields 含 ImageTags={latestFields.Contains("ImageTags")}｜query 片段={Truncate(latestFields, 120)}｜" +
                $"响应面：mock **未覆盖** `/Items/Latest` ⇒ 本轮返回 {latestCount} 条／抛出 {latestError}（字段面由请求原文证明；响应面需真服务器）");
        }
        catch (Exception ex)
        {
            Step("S10② GetLatestAsync 请求带 ImageTags", false, ex.ToString().Split('\n')[0]);
        }

        // (iii) 缺凭据前置拒绝（ui2 报的健壮性问题）
        try
        {
            using var mockNo = new MockEmbyServer().Start();
            using var httpNo = new ShellHttpClient();
            var noCredService = new EmbyService(httpNo, new ServerConfig
            {
                Id = "srv-nocred",
                Kind = ServerKind.Emby,
                Name = "无凭据",
                BaseUrl = mockNo.BaseUrl,
                UserId = string.Empty,
            });
            var rejected = false;
            string rejectedMessage = null;
            var sentBefore = mockNo.Requests.Count;
            try { await noCredService.GetResumeAsync(cancellationToken: cancellationToken).ConfigureAwait(false); }
            catch (InvalidOperationException ex) { rejected = true; rejectedMessage = ex.Message; }
            var noRequestSent = mockNo.Requests.Count == sentBefore;
            Step("S10③ 缺凭据（UserId 空）⇒ 前置拒绝并给出可指认错误（不再是静默的 /Users//Items ⇒ 404）", rejected && noRequestSent,
                $"被前置拒绝={rejected}｜**未发出任何请求**={noRequestSent}｜错误={Truncate(rejectedMessage, 120)}");
        }
        catch (Exception ex)
        {
            Step("S10③ 缺凭据前置拒绝", false, ex.ToString().Split('\n')[0]);
        }

        // ── S11：t65 User-Agent（默认值 / 可自定义 / 清空回落 / 非法拒绝 / 重启生效 / 两个消费面同源）──
        try
        {
            var uaDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t65-ua-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(uaDir);
            var uaFile = System.IO.Path.Combine(uaDir, "settings.json");

            // 请求面观测点：真造一个 loopback 监听，读**实际发出的**头（不用 mock 的推断）
            var listener = new System.Net.HttpListener();
            var port = FreeLoopbackPort();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var requestFace = new List<string>();
            var acceptTask = Task.Run(async () =>
            {
                while (requestFace.Count < 3)
                {
                    var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                    requestFace.Add(ctx.Request.Headers["User-Agent"] ?? "(无 UA)");
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                }
            });
            var probeUrl = $"http://127.0.0.1:{port}/ua";

            async Task<string> UaOfRequestAsync()
            {
                var before = requestFace.Count;
                using (var c = new ShellHttpClient())
                {
                    await c.GetAsync(probeUrl, retries: 0, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                var deadline = Environment.TickCount64 + 5000;
                while (requestFace.Count <= before && Environment.TickCount64 < deadline)
                {
                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }
                return requestFace.Count > before ? requestFace[requestFace.Count - 1] : "(未观测到请求)";
            }

            // ① 默认值 = 原版实测值（唯一构造点）
            //    t88 口径：先 `ResetToDefault()` 把当前值置为常量默认（**不粘住**，后续推送仍可改回）⇒
            //    本断言不再依赖当刻真实 settings.json（否则"惰性读设置"上线后，这条会随用户盘上值飘）
            UserAgentPolicy.ResetToDefault();
            var defaultOk = UserAgentPolicy.Default == "ai-player"
                && UserAgentPolicy.Resolve(null) == UserAgentPolicy.Default
                && UserAgentPolicy.Resolve("   ") == UserAgentPolicy.Default
                && UserAgentPolicy.Resolve("bad\nvalue") == UserAgentPolicy.Default;
            Step("S11① UA 默认值 = 原版实测值 ai-player（唯一构造点 UserAgentPolicy.Current）", defaultOk,
                $"Default={UserAgentPolicy.Default}｜ResetToDefault 后 Current={UserAgentPolicy.Current}｜Resolve(null/空白/非法)=" +
                $"{UserAgentPolicy.Resolve(null)}/{UserAgentPolicy.Resolve("   ")}/{UserAgentPolicy.Resolve("bad\nvalue")}｜ShellHttpClient 默认实例读到={new ShellHttpClient().UserAgent}");

            // ② 请求面：默认值真的发出去了
            var sentDefault = await UaOfRequestAsync().ConfigureAwait(false);
            Step("S11② 请求面（默认）：真实出站 User-Agent = 默认值", sentDefault == UserAgentPolicy.Default,
                $"出站头=[{sentDefault}]｜期望=[{UserAgentPolicy.Default}]");

            // ③ 自定义 ⇒ 内存/磁盘往返 + 请求面生效 + 两个消费面同源
            var svc = SettingsService.At(uaFile);
            svc.Load();
            var custom = "Hills/1.0 (t65 probe)";
            var setOk = svc.TrySetUserAgent(custom, out var setErr);
            var sentCustom = await UaOfRequestAsync().ConfigureAwait(false);
            var embyUa = new EmbyService(new ShellHttpClient(), new ServerConfig
            {
                Id = "srv-ua",
                Kind = ServerKind.Emby,
                Name = "ua",
                BaseUrl = "http://127.0.0.1:1/emby",
                UserId = "u",
                AccessToken = "t",
            }).HostHttpHeaders["User-Agent"];
            Step("S11③ 自定义 UA：设置→落盘→请求头生效，且 EmbyService(内核 --http-header) 同源", setOk && sentCustom == custom && embyUa == custom,
                $"TrySetUserAgent={setOk}（err={setErr ?? "(无)"}）｜出站头=[{sentCustom}]｜EmbyService.HostHttpHeaders=[{embyUa}]｜盘上={svc.Settings.UserAgent}");

            // ④ 反控 A：清空/纯空白 ⇒ 回落默认（不得发空 UA、不得抛）
            //    口径：**盘上落空串**（用户意图="用默认"），生效值/出站头 = 默认 ⇒ 设置页输入框保存后仍是空，
            //    "留空回落"这条反控才可反复验证（若把默认字面量写进盘，UI 会把它渲染成实体文本而破坏该反控）。
            var blankOk = svc.TrySetUserAgent("   ", out _);
            var sentBlank = await UaOfRequestAsync().ConfigureAwait(false);
            Step("S11④ 反控A：清空/纯空白 ⇒ 盘上空串 + 生效/出站回落默认（不发空 UA）",
                blankOk && sentBlank == UserAgentPolicy.Default && svc.Settings.UserAgent.Length == 0,
                $"TrySetUserAgent(空白)={blankOk}｜出站头=[{sentBlank}]｜盘上 len={svc.Settings.UserAgent.Length}（期望 0）｜生效={UserAgentPolicy.Current}");

            // ⑤ 反控 B：非法头字符（换行）⇒ 可见拒绝 + **不写盘**
            var beforeBytes = System.IO.File.ReadAllBytes(uaFile);
            var illegalOk = !svc.TrySetUserAgent("bad\nvalue", out var illegalErr);
            var afterBytes = System.IO.File.ReadAllBytes(uaFile);
            var unchanged = beforeBytes.Length == afterBytes.Length && beforeBytes.SequenceEqual(afterBytes);
            Step("S11⑤ 反控B：非法头字符（换行）⇒ 可见拒绝且不写盘", illegalOk && !string.IsNullOrEmpty(illegalErr) && unchanged,
                $"被拒={illegalOk}｜错误={Truncate(illegalErr, 90)}｜设置文件字节不变={unchanged}（{beforeBytes.Length} B）｜生效值仍={UserAgentPolicy.Current}");

            // ⑥ 反控 C：改值后"重启"（另起实例读同一文件）仍生效
            svc.TrySetUserAgent("Hills/2.0 (restart)", out _);
            var restarted = SettingsService.At(uaFile);
            restarted.Load();
            Step("S11⑥ 反控C：改值后重启（新实例读盘）仍生效", restarted.EffectiveUserAgent == "Hills/2.0 (restart)",
                $"新实例 EffectiveUserAgent=[{restarted.EffectiveUserAgent}]｜盘上 userAgent=[{restarted.Settings.UserAgent}]");

            // ⑦ t88 优先级（三段）：显式覆盖 > 设置推送（**重置之后仍可改回**）> FollowSettings 解锁回「跟随设置」
            //    为什么要"重置之后仍可改回"这条：ResetToDefault 若把"显式默认"钉死，UI 里改 UA 就再也不会生效
            //    —— 本步是那条契约的反控（实现第一版真的错了，被 S11③/⑥ + 本步当场抓到）。
            var pushedValue = UserAgentPolicy.Current;
            UserAgentPolicy.ResetToDefault();
            var afterResetValue = UserAgentPolicy.Current;
            var diskValueStill = restarted.Settings.UserAgent;
            var resetNotExplicit = !UserAgentPolicy.HasExplicit;
            UserAgentPolicy.SetCurrent("aip-explicit/t88");
            var explicitWins = UserAgentPolicy.Current == "aip-explicit/t88" && UserAgentPolicy.HasExplicit;
            UserAgentPolicy.ResetToDefault();
            UserAgentPolicy.Apply(new AppSettings { UserAgent = "Hills/after-reset" });
            var afterPushValue = UserAgentPolicy.Current;
            UserAgentPolicy.FollowSettings();
            var unlocked = !UserAgentPolicy.IsPinned && !UserAgentPolicy.HasExplicit;
            Step("S11⑦ t88 优先级：显式覆盖 > 设置推送（重置后仍可改回）> FollowSettings 解锁回「跟随设置」",
                pushedValue == "Hills/2.0 (restart)" && afterResetValue == UserAgentPolicy.Default
                && diskValueStill == "Hills/2.0 (restart)" && resetNotExplicit && explicitWins
                && afterPushValue == "Hills/after-reset" && unlocked,
                $"② 推送值={pushedValue}｜③ ResetToDefault 后={afterResetValue}（隔离根盘上仍={diskValueStill}；HasExplicit={!resetNotExplicit} 期望 False）" +
                $"｜① SetCurrent 后={explicitWins}（期望 True）｜② 重置后再 Apply(Hills/after-reset) ⇒ {afterPushValue}（期望 Hills/after-reset）" +
                $"｜④ FollowSettings 后 IsPinned={UserAgentPolicy.IsPinned}/HasExplicit={UserAgentPolicy.HasExplicit}（期望 False/False）");

            listener.Stop();
            listener.Close();
            try { await acceptTask.ConfigureAwait(false); }
            catch (Exception ex) { Say($"  （S11 监听收尾：{ex.GetType().Name}，属正常停止路径）"); }
            UserAgentPolicy.ResetToDefault();
            TryDeleteTempDir(uaDir);
        }
        catch (Exception ex)
        {
            Step("S11 UA 自检", false, ex.ToString().Split('\n')[0]);
            UserAgentPolicy.ResetToDefault();
        }

        Say(report.ToText());
        return report;
    }

    /// <summary>取一个空闲 loopback 端口（S11 用：起真监听读真实出站头）。</summary>
    private static int FreeLoopbackPort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>证据行截断（长 query/URL 只留开头，避免污染报告）。</summary>
    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        return value.Length <= max ? value : value.Substring(0, max) + "…";
    }

    /// <summary>
    /// 证据行里的凭据必须**打码**（红线：不得把 token 形状的串写进日志/文档 —— 即使是 mock 常量，
    /// 也会被凭据扫描命中、制造假阳性噪声）。按**键名**打码：`api_key=` / `apikey=` / `accessToken=` / `token=` / `password=` / `pwd=`。
    /// </summary>
    /// <summary>
    /// 清理自检用的临时目录（每次运行都是 %TEMP% 下新目录 ⇒ 删不掉不影响判据）。
    /// 有意只吞这两类**窄异常**：被占用（IOException）/ 权限（UnauthorizedAccessException）；
    /// 其余异常（如路径非法）**不吞**，让它冒到调用处的 catch 里被记成一次失败。
    /// </summary>
        /// <summary>自检用的**测试数据**文件名（真实生产契约常量在 <c>AppDataDir.ServersFileName</c>；这里只是临时目录里的同名文件）。</summary>
    private const string TestServersFileName = "servers.json";

private static void TryDeleteTempDir(string directory)
    {
        try { System.IO.Directory.Delete(directory, recursive: true); }
        catch (System.IO.IOException) { /* 有意忽略：临时目录被占用，删不掉不影响判据 */ }
        catch (System.UnauthorizedAccessException) { /* 有意忽略：临时目录权限不足，删不掉不影响判据 */ }
    }

    /// <summary>打码 = **转发到全工程唯一真源** <c>Services.Logging.SecretMasking.Mask</c>（t225 收敛；形态照
    /// <c>shell/App/Features/Search/SearchLog.cs</c> 的既有做法）。</summary>
    /// <remarks>
    /// 本处曾是**同目标、不同写法的第二份实现**（一条正则
    /// <c>(?i)(api_key|apikey|access_?token|token|password|pwd)=([^&amp;,\s]*)</c> ⇒ <c>$1=***</c>；t208 的宽口径自查抓出）。
    /// 收敛后覆盖真源的 **5 条规则**（`api_key` 含百分号编码 / `--http-header=` / 认证头 / URL 查询串凭据键 / 密码字段），
    /// 并继承其**幂等**（`Describe` 的 <c>&lt;masked</c> 早退守卫）与**不抛**（失败返原文 + 交回错误名）两条纪律。
    /// ⇒ **覆盖面单向增加**：旧写法能掩的形态真源都能掩（`api_key=***` 的替换串与旧写法逐字相同），
    /// 且真源掩得更严（认证头 / `--http-header=` 保留长度与指纹，而不是只写 `***`）。
    /// ⚠️ 唯一的**例外形态**在调用点显式处理（见 <see cref="MaskAccessTokenPair"/>）：旧正则认 `access_?token=` 这种**裸键值对**，
    /// 而真源只认"URL 查询串里的凭据键"（要求 `?`/`&` 前缀）与认证**头**形态 ⇒ 裸 `AccessToken=…` 不在真源覆盖面内，
    /// 故该值改走真源的 <c>Describe</c>（同一直源的公开 API，**不新建第二种打码写法**）。
    /// </remarks>
    private static string MaskToken(string value)
        => AIPlayer.Shell.Services.Logging.SecretMasking.Mask(value);

    /// <summary>裸 <c>AccessToken=…</c> 键值对（**不在**真源覆盖面内，理由见 <see cref="MaskToken"/> 的 remarks）：
    /// 用真源的 <c>Describe</c> 掩掉值（保留长度 + 指纹，不可逆，<c>&lt;masked len=N sha4=XXXX&gt;</c>），
    /// 再交 <see cref="MaskToken"/> 过一遍其余规则（"不新建第二种打码写法"）。</summary>
    private static string MaskAccessTokenPair(string accessToken)
        => MaskToken("AccessToken=" + AIPlayer.Shell.Services.Logging.SecretMasking.Describe(accessToken));

    /// <summary>t44 自检用：把回调原样收进列表（`IProgress&lt;T&gt;` 的最小实现）。</summary>
    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public List<T> Items { get; } = new List<T>();

        public void Report(T value)
        {
            lock (Items)
            {
                Items.Add(value);
            }
        }
    }

    /// <summary>t44 自检用：可控延时/可控失败的假聚合源（不碰任何真实服务）。</summary>
    private sealed class FakeAggregatedSource : IAggregatedSearchSource
    {
        private readonly IReadOnlyList<AggregatedSearchHit> _hits;
        private readonly int _delayMs;
        private readonly bool _throwIt;

        public FakeAggregatedSource(string name, IReadOnlyList<AggregatedSearchHit> hits, int delayMs = 0, bool throwIt = false)
        {
            ServerName = name;
            ServerId = name;
            _hits = hits;
            _delayMs = delayMs;
            _throwIt = throwIt;
        }

        public string ServerId { get; }

        public string ServerName { get; }

        public async Task<IReadOnlyList<AggregatedSearchHit>> SearchAsync(string term, int limit, CancellationToken cancellationToken)
        {
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
            }
            if (_throwIt)
            {
                throw new InvalidOperationException("fake source failure: " + ServerName);
            }
            return _hits ?? new List<AggregatedSearchHit>();
        }
    }
}

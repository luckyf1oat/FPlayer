using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;

namespace t11real;

// t11：服务层对**真实** Emby 端到端 —— 认证 → 取库 → 取条目。
//
// 纪律（verifier 2026-09-11 复跑时提出的三条整改）：
//   1. 凭据只从**环境变量**读：EMBY_USER（默认 "fx"）/ EMBY_PW（默认空）。
//      ⚠️ **绝不在源码里硬编码口令** —— 本文件被 git 跟踪，写进去就进 git 历史。
//   2. 输出**只打 token 长度**与**截断后的 userId**（前 8 位 + …），不打完整凭据或长十六进制串。
//   3. 每一步都要有**可失败的断言**（STEP 4 从"只打类型名"改为打 `counts.Count` + 前若干键值 + 空则 FAIL）。
internal static class Program
{
    private const string BaseUrl = "https://emby.example.com";   // 用户提供的验证服务器（公开地址，非凭据）

    private static string Short(string s)
        => string.IsNullOrEmpty(s) ? "(null)" : (s.Length <= 8 ? s : s.Substring(0, 8) + "…");

    private static async Task<int> Main()
    {
        var user = Environment.GetEnvironmentVariable("EMBY_USER");
        if (string.IsNullOrWhiteSpace(user)) user = "fx";
        var password = Environment.GetEnvironmentVariable("EMBY_PW") ?? string.Empty;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = cts.Token;
        using var http = new ShellHttpClient();
        var log = new Action<string>(s => Console.WriteLine("  [svc] " + s));

        Console.WriteLine($"SAMPLE-TIME={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"BASE-URL={BaseUrl}  USER={user}  PW-FROM-ENV={(Environment.GetEnvironmentVariable("EMBY_PW") == null ? "no(default empty)" : "yes")}");

        try
        {
            Console.WriteLine("STEP 1  LoginAsync（认证）");
            var auth = await EmbyService.LoginAsync(http, BaseUrl, user, password, cancellationToken: ct);
            if (auth == null) { Console.WriteLine("  AUTH: null  => FAIL"); return 2; }
            Console.WriteLine($"  AUTH OK  user={auth.UserName}  userId={Short(auth.UserId)}  serverName={auth.ServerName}  tokenLen={(auth.AccessToken == null ? 0 : auth.AccessToken.Length)}");

            var server = new ServerConfig
            {
                Id = "t11-real",
                Name = auth.ServerName ?? "Emby",
                BaseUrl = BaseUrl,
                UserName = auth.UserName,
                UserId = auth.UserId,
                AccessToken = auth.AccessToken,
                Kind = ServerKind.Emby,
            };
            var emby = new EmbyService(http, server, onLog: log);

            Console.WriteLine("STEP 2  GetViewsAsync（媒体库）");
            var views = await emby.GetViewsAsync(ct);
            var list = views?.ToList();
            Console.WriteLine($"  libraries = {(list == null ? -1 : list.Count)}");
            if (list == null || list.Count == 0) { Console.WriteLine("  VIEWS EMPTY => FAIL"); return 3; }
            foreach (var v in list.Take(8)) Console.WriteLine($"    - {v.Name}  [{v.CollectionType}]  id={v.Id}");

            Console.WriteLine("STEP 3  GetItemsAsync（库内条目）");
            var first = list.FirstOrDefault(v => v.CollectionType == "tvshows" || v.CollectionType == "movies") ?? list[0];
            var items = await emby.GetItemsAsync(parentId: first.Id, limit: 5, cancellationToken: ct);
            var ilist = items?.Items?.ToList();
            Console.WriteLine($"  items in '{first.Name}' = {(ilist == null ? -1 : ilist.Count)}");
            if (ilist == null || ilist.Count == 0) { Console.WriteLine("  ITEMS EMPTY => FAIL"); return 4; }
            foreach (var it in ilist.Take(5)) Console.WriteLine($"    - {it.Name}  [{it.Type}]  id={it.Id}");

            Console.WriteLine("STEP 4  GetCountsAsync（计数）—— 断言：必须拿到非空字典");
            var counts = await emby.GetCountsAsync(ct);
            var n = counts?.Count ?? -1;
            Console.WriteLine($"  counts.Count = {n}");
            if (counts != null)
            {
                foreach (var kv in counts.Take(6)) Console.WriteLine($"    {kv.Key} = {kv.Value}");
            }
            if (n <= 0) { Console.WriteLine("  COUNTS EMPTY => FAIL"); return 5; }

            Console.WriteLine("STEP 5  DirectStreamUrl（取流地址形态，不打印凭据）");
            var item = ilist.FirstOrDefault(x => !string.IsNullOrEmpty(x.Id));
            if (item != null)
            {
                var url = emby.DirectStreamUrl(item.Id);
                var q = url == null ? -1 : url.IndexOf("api_key=", StringComparison.Ordinal);
                Console.WriteLine($"  streamUrl absolute-https={(url != null && url.StartsWith("https://", StringComparison.Ordinal))}  hasApiKeyParam={q >= 0}  len={(url == null ? 0 : url.Length)}");
            }

            Console.WriteLine("RESULT=PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RESULT=FAIL  {ex.GetType().FullName}: {ex.Message}");
            return 1;
        }
    }
}

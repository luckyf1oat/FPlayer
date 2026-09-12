// t31（U-E 收藏 + 聚合视界）的**取数面**：跨服务器并发取数 + 按服务器分组 + 失败源登记。
//
// 规格依据：
// · shell/docs/HILLSLITE_UI_ANALYSIS.md 行 3 / 行 5 —— 聚合视界 = 顶部三个过滤器
//     （`▶ 继续播放` / `♡ 收藏` / `▤ 媒体库`）+ **按服务器分组**（每组一行海报）+ 每张海报下**一条进度条**；
//     `server`/`ServerB` 两组显示灰底 + 胶片图标占位 ⇒ 该源无封面。
// · shell/docs/UI_SPEC_SHELL.md P4 —— 收藏 / 聚合视界：**跨服分组 + 进度条；空源显示占位图不崩**。
//
// 服务层契约（只读使用，不修改）：
//   · `EmbyService.GetResumeAsync(limit)`（`EmbyService.cs:291`）—— 继续播放
//   · `EmbyService.GetItemsAsync(isFavorite: true, …)`（`:223` / `:261` 的 `IsFavorite` 查询参数）—— 收藏
//   · `EmbyService.GetItemsAsync(includeItemTypes: EmbyViewTypes.ItemTypesForView(null),
//       sortBy: "DateCreated", sortOrder: "Descending", limit: LibraryLimitPerServer)` —— 媒体库（按添加时间）
//     ⚠ 本行原注写 `GetLatestAsync(limit)`（`:308`）**与实现不符**，t199 按实现逐字改正；
//       且本行**按服务器**聚合（行内无 view ⇒ 用单点对"无库类型"的答案），**必须**带 `includeItemTypes`
//       —— 不带会把 Studio/Genre 当条目取回来（见下方调用点注释里的真机读数）。
//   · `EmbyService.ImageUrlIfAvailable(item, "Primary", maxHeight)`—— 封面（`:572`）
//      **唯一入口**：无图 ⇒ 空串 ⇒ 不发请求（无图的条目请求 `/Items/{id}/Images/Primary` 会被服务端以 HTTP 500 回答，t70/t78）；
//      用哪个 id / 带哪个 tag（含剧集回退）也由它一处决定 ⇒ 本文件不再自算回退、也不再直接调 `ImageUrl`。
//
// ⚠ **三条必须写死的边界**（都不是猜测，是上面契约的直接读数）：
//   ① 本屏**不做去重**：与搜索不同，聚合视界要的是"按服务器分组"的**归属清晰**，
//      同一部片在 A、B 两台都有就该在两组里各出现一次（参照物即如此）。
//   ② 单源取数失败**不得让整屏失败**：失败源收进 `FailedServers`，界面用 `ServerFail` 列出来，
//      其余源照常显示 ⇒ 这是"空源显示占位图不崩"的另一半。
//   ③ 无封面的条目**必须有占位形态**（`HasImage=false`），界面画 `#333333` 灰底 + 胶片图标，
//      **不得**因为 `ImageUrl` 抛异常把整组搞没。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>聚合视界的三个过滤器（顺序即参照物里的顺序）。</summary>
public enum AggregateKind
{
    /// <summary>`▶ 继续播放`（`GetResumeAsync`；进度条有意义的唯一来源）。</summary>
    ContinueWatching,

    /// <summary>`♡ 收藏`（`GetItemsAsync(isFavorite: true)`）。</summary>
    Favorites,

    /// <summary>`▤ 媒体库`（`GetLatestAsync`：按添加时间，跨服合并展示）。</summary>
    Library,
}

/// <summary>一组的取数结果（= 一台服务器）。</summary>
public sealed class MediaGroupResult
{
    public MediaGroupResult(
        string serverId,
        string serverName,
        List<EmbyItem> items,
        string error,
        int totalCount = 0,
        bool truncated = false)
    {
        ServerId = serverId;
        ServerName = serverName;
        Items = items ?? new List<EmbyItem>();
        Error = error;
        TotalCount = totalCount;
        Truncated = truncated;
    }

    public string ServerId { get; }

    public string ServerName { get; }

    public List<EmbyItem> Items { get; }

    /// <summary>非 <c>null</c> ⇒ 该源失败（异常类型名 + 消息形态；**不含 URL**）。</summary>
    public string Error { get; }

    public bool Failed => Error != null;

    /// <summary>
    /// 服务端声明的总数（`EmbyQueryResult.TotalRecordCount`）。**0 = 服务端没给**（不是"没有内容"）。
    /// t87 起收藏族会填它：屏上条数 &lt; 它 ⇒ 必须把"还有更多"报到界面，禁止静默截断。
    /// </summary>
    public int TotalCount { get; }

    /// <summary>true ⇒ 撞上每源安全硬上限而提前停止（屏上**不是**全部 ⇒ 界面必须可见地说明）。</summary>
    public bool Truncated { get; }

    /// <summary>摘要行用的显示名：<c>ServerName（ServerId 短号）</c> —— 两台同名服务器必须可区分。</summary>
    public string DisplayName => ServerId == null || ServerId.Length == 0
        ? ServerName
        : ServerName + "（" + (ServerId.Length <= 6 ? ServerId : ServerId.Substring(0, 6)) + "）";
}

/// <summary>一次聚合取数的完整结果。</summary>
public sealed class MediaAggregate
{
    public MediaAggregate(AggregateKind kind, List<MediaGroupResult> groups, TimeSpan elapsed)
    {
        Kind = kind;
        Groups = groups ?? new List<MediaGroupResult>();
        Elapsed = elapsed;
    }

    public AggregateKind Kind { get; }

    /// <summary>**按服务器分组**（含失败组；顺序 = 调用方给的服务器顺序）。</summary>
    public List<MediaGroupResult> Groups { get; }

    public TimeSpan Elapsed { get; }

    /// <summary>有内容的组（非失败且有条目）。</summary>
    public List<MediaGroupResult> NonEmptyGroups => Groups.Where(g => !g.Failed && g.Items.Count > 0).ToList();

    public List<MediaGroupResult> FailedGroups => Groups.Where(g => g.Failed).ToList();

    /// <summary>条目总数（跨所有组求和）。</summary>
    public int TotalItems => Groups.Sum(g => g.Items.Count);

    /// <summary>服务端声明的总数（跨所有组求和；服务端没给的组按屏上条数计，避免把"未知"读成"没有"）。</summary>
    public int ServerTotalItems => Groups.Sum(g => Math.Max(g.TotalCount, g.Items.Count));

    /// <summary>撞上安全硬上限而**提前停止**的组（非空 ⇒ 屏上不是全部）。</summary>
    public List<MediaGroupResult> TruncatedGroups => Groups.Where(g => g.Truncated).ToList();

    public bool AnyTruncated => Groups.Any(g => g.Truncated);
}

/// <summary>
/// 跨服务器取数器。**每台服务器一个 <see cref="EmbyService"/>**（与 `LibraryPage` 同一构造方式），
/// 同源的请求在一个组内**串行**、跨服务器**并发**（`Task.WhenAll`）。
/// </summary>
public static class MediaAggregator
{
    /// <summary>媒体库过滤器的每源上限。</summary>
    public const int LibraryLimitPerServer = 60;

    /// <summary>继续播放的每源上限（横向行：一屏本来就看不完）。</summary>
    public const int ResumeLimitPerServer = 30;

    /// <summary>
    /// 收藏族的**每源分页大小**（一次请求取多少条）。
    /// 🔴 与 <see cref="ResumeLimitPerServer"/> **不是同一个口径**：那个是给"继续播放"横向行定的展示上限，
    /// 收藏是**完整列表**。t87 实测：ServerA 收藏实际 34 条（电影 1 + 剧 21 + 集 12），
    /// 共用 30 上限时屏上只有 30 条 ⇒ **用户可见的静默丢失**（且"收藏的剧"显示 18 而非 21）。
    /// </summary>
    public const int FavoritesPageSizePerServer = 100;

    /// <summary>
    /// 收藏族的**每源安全硬上限**（翻页翻到这里就停）。
    /// 它存在只是为了让"翻页取尽"有终止条件；**一旦命中必须可见** ——
    /// `MediaGroupResult.Truncated` 会传到界面（"还有更多"提示）与读数（日志行），不允许静默截断。
    /// </summary>
    public const int FavoritesMaxPerServer = 1000;

    /// <summary>只取前 N 台（自检提速用；默认 0 = 不限制）。</summary>
    public const string LimitServersEnvVar = "SHELL_AGG_MAX_SERVERS";

    // 取数用的字段面**不在这里定义**：唯一构造点是服务层的 `EmbyService.ItemFields`。
    // 本屏取数一律不传 `fields:`，由服务层用它的默认值 —— 同一件事只有一处定义，改字段不会漏掉某一屏。
    // （历史：早期该常量不含封面标签，本屏曾自行拼过一份字段串；服务层补齐后那份拼接成为第二处定义，已删除。）

    /// <summary>
    /// 收藏查询的条目类型：**电影 + 剧 + 集**（对应用户要的三个分区「收藏的电影 / 收藏的剧 / 收藏的集」）。
    /// ⚠ 不能直接用 `EmbyService.DefaultSearchItemTypes` —— 那是"什么都能搜到"的宽集合
    /// （还含 `BoxSet`/`Person`/`Video`/音乐/音频），而收藏屏要的就是这三个类型。
    /// 🔴 **收藏屏与聚合视界的收藏过滤器共用本常量**：两屏口径必须同源，否则同一件事会有两处定义。
    /// </summary>
    public const string FavoritesItemTypes = "Movie,Series,Episode";

    /// <summary>启用服务器（Emby 家族 + Enabled），按 SortIndex 排序；受 <see cref="LimitServersEnvVar"/> 限制。</summary>
    public static List<ServerConfig> EnabledEmbyServers(ServerConfigStore store = null)
    {
        var list = (store ?? ServerConfigStore.Instance).Servers?
            .Where(s => s.Enabled && s.Kind.IsEmbyFamily())
            .OrderBy(s => s.SortIndex)
            .ToList() ?? new List<ServerConfig>();

        var raw = Environment.GetEnvironmentVariable(LimitServersEnvVar);
        if (int.TryParse(raw, out var max) && max > 0 && list.Count > max)
        {
            return list.Take(max).ToList();
        }

        return list;
    }

    /// <summary>按过滤器跨服取数（**不抛异常**：单源失败只登记）。</summary>
    public static async Task<MediaAggregate> LoadAsync(
        AggregateKind kind,
        List<ServerConfig> servers,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        servers ??= new List<ServerConfig>();

        var tasks = servers.Select(s => LoadOneAsync(kind, s, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        stopwatch.Stop();
        return new MediaAggregate(kind, results.ToList(), stopwatch.Elapsed);
    }

    private static async Task<MediaGroupResult> LoadOneAsync(
        AggregateKind kind,
        ServerConfig server,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(server.Name) ? server.Id : server.Name;
        try
        {
            var emby = new EmbyService(new ShellHttpClient(), server,
                onLog: m => Debug.WriteLine("[agg] " + m));

            switch (kind)
            {
                case AggregateKind.ContinueWatching:
                {
                    var items = await emby.GetResumeAsync(limit: ResumeLimitPerServer, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return new MediaGroupResult(server.Id, name, items, null);
                }

                case AggregateKind.Favorites:
                {
                    // 收藏族：**电影 + 剧 + 集**（三个分区），类型集合见 `FavoritesItemTypes`
                    // —— 收藏屏与聚合视界共用同一个常量，避免两屏口径分叉。
                    // 🔴 t87：收藏是**完整列表**，不再与"继续播放"共用 `ResumeLimitPerServer=30`
                    //    （那会让 ServerA 的 34 条只显示 30 条 = 用户可见的静默丢失）；
                    //    改为**翻页取尽**，直到服务端说没有更多、或不足一页、或撞上安全硬上限
                    //    （撞上 ⇒ `Truncated=true`，界面必须可见地说明"还有更多"）。
                    var items = new List<EmbyItem>();
                    var total = 0;
                    var truncated = false;

                    while (true)
                    {
                        var page = await emby.GetItemsAsync(
                            includeItemTypes: FavoritesItemTypes,
                            isFavorite: true,
                            startIndex: items.Count,
                            limit: FavoritesPageSizePerServer,
                            sortBy: "SortName",
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        var batch = page?.Items ?? new List<EmbyItem>();
                        if (page?.TotalRecordCount > 0)
                        {
                            total = page.TotalRecordCount;      // 服务端声明的总数（0 = 没给，不当作"没有"）
                        }

                        if (batch.Count == 0)
                        {
                            break;                              // 取尽（服务端不再给）
                        }

                        items.AddRange(batch);

                        if (total > 0 && items.Count >= total)
                        {
                            break;                              // 服务端说"就这么多"
                        }

                        if (items.Count >= FavoritesMaxPerServer)
                        {
                            // 还没取完就撞上硬上限 ⇒ 明确标记（界面与读数都要看见），不静默停
                            truncated = true;
                            total = Math.Max(total, items.Count);
                            break;
                        }

                        if (batch.Count < FavoritesPageSizePerServer)
                        {
                            break;                              // 不足一页 ⇒ 没有下一页（服务端未给 TotalRecordCount 时的兜底）
                        }
                    }

                    AggregateDiagnostics.Write("favorites server=" + name
                        + " shown=" + items.Count
                        + " serverTotal=" + (total > 0 ? total.ToString() : "<none>")
                        + " pageSize=" + FavoritesPageSizePerServer
                        + " cap=" + FavoritesMaxPerServer
                        + " truncated=" + truncated);
                    return new MediaGroupResult(server.Id, name, items, null, total, truncated);
                }

                case AggregateKind.Library:
                default:
                {
                    // t199：**必须给 `includeItemTypes`**。不给的实测后果（真机 ServerA，2026-09-12 19:27）：
                    // 请求里没有 `IncludeItemTypes` ⇒ Emby 把**非播放容器**也当条目返回 —— 该行 60 条是
                    // `Studio=59, Genre=1`（制作公司名 / 流派名），**根本不是媒体**，卡片自然只剩占位底。
                    //
                    // 为什么是 `ItemTypesForView((string)null)` 而不是某个具体类型：本行**按服务器**聚合
                    // （`LoadOneAsync(kind, server, …)`，行内没有 view 可依），跨库取数**没有单一库类型**可传
                    // ⇒ 取共享单点对"未知 / 无库类型"的**既定答案** `DefaultMixedTypes`
                    // （`Movie,Series,Video,MusicVideo,BoxSet`）。既不新造第二份类型映射（t76 立的规矩），
                    // 又保证该行只剩**可播放媒体**：电影库条目仍是 `Movie`、电视库条目成为 `Series`，两种都不掉。
                    // 单点随之落的那行 `VIEW-TYPE-FALLBACK … reason=empty-collectiontype` 是它**设计如此**的
                    // 可见兜底读数（t76：兜底本身没错、静默兜底才是缺陷），不是异常。
                    var result = await emby.GetItemsAsync(
                        includeItemTypes: EmbyViewTypes.ItemTypesForView((string)null),
                        sortBy: "DateCreated",
                        sortOrder: "Descending",
                        limit: LibraryLimitPerServer,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return new MediaGroupResult(server.Id, name, result?.Items ?? new List<EmbyItem>(), null);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // t93-E（与 t52 的 M-2/M-3 同族）：**取消不得被吞成"这一源失败、0 条"** ——
            // 吞掉之后整屏会拿到一份"看起来成功、其实是空"的结果，并可能被写进 SWR 缓存
            // （t93 实测的毒缓存就是这么来的：刷新中被取消 ⇒ 快照落成 items=[]）。
            // ⇒ 取消一律**往上抛**，由 SWR 层记成显式的 `refresh=cancelled`（缓存不动）。
            throw;
        }
        catch (Exception ex)
        {
            // **只登记类型与消息形态**（不落 URL）；失败源照常进界面，其余组不受影响
            var error = ex.GetType().Name + ": " + (ex.Message ?? string.Empty);
            AggregateDiagnostics.Write("source-fail server=" + name + " kind=" + kind + " " + error);
            return new MediaGroupResult(server.Id, name, new List<EmbyItem>(), error);
        }
    }

    /// <summary>
    /// 进度（0..1）：**优先用服务端 `UserData.PlayedPercentage`**（有就直接用），
    /// 否则用 `ResumeTicks / RunTimeTicks` 自算；两者都拿不到 ⇒ <c>0</c>（界面不画进度条）。
    /// 依据：内核/原版同族语义 —— `EmbyUserData.cs:19/:25`。
    /// </summary>
    public static double ProgressFraction(EmbyItem item)
    {
        if (item == null) return 0;

        var pct = item.UserData?.PlayedPercentage ?? 0;
        if (pct > 0)
        {
            return Math.Clamp(pct / 100.0, 0, 1);
        }

        var duration = item.RunTimeTicks;
        if (duration <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)item.UserData.ResumeTicks / duration, 0, 1);
    }

    /// <summary>
    /// 封面地址（**无封面回 <c>null</c>**，界面据此画占位）。
    ///
    /// <para>"有没有图 / 用哪个 id / 带哪个 tag"**一律交给服务层唯一入口**
    /// <see cref="EmbyService.ImageUrlIfAvailable"/>：本方法原来自己复算"自家 Primary，否则剧集回退"，
    /// 且构造 URL 时**不带 `tag=`** —— 无图条目请求 <c>/Items/{id}/Images/Primary</c> 会被服务端
    /// 以 **HTTP 500**（不是 404）回答，而剧集回退还要求**换 id**（自家 id + 剧 tag 实测仍 500）。
    /// 这两条判据只有服务层那一份是对的（t70/t78，`shell/Tests/evidence/t78-image-500-mechanism.txt`），
    /// App 侧各写一套必然漂。</para>
    ///
    /// <para>契约不变：**无图 / 取不到 URL ⇒ <c>null</c>**（界面画 <c>#333333</c> 占位）；有图 ⇒ 带 `tag=` 的绝对 URL。</para>
    /// <para>[!] t305 追加：**选哪个条目去问**（单集 ⇒ 主剧集 stub）由本文件决定，**tag 回退仍归服务层出口** ——
    /// 本文件**不自己拼 tag**、也不直接调 `ImageUrl`；"不再自算回退"这条纪律指的是后者。</para>
    /// </summary>
    public static string PrimaryImageUrl(EmbyService emby, EmbyItem item, int maxHeight = 498, int maxWidth = 332)
    {
        if (emby == null || item == null || item.Id.Length == 0)
        {
            return null;
        }

        try
        {
            // [!] t305（用户第④⑧条）：**单集先取主剧集竖海报**，且 **宽高同时给**。
            // 框 = `AggregatePage.xaml:99/107` 的 166×249（`UniformToFill`）⇒ 请求 2 倍 = 332×498（t305 前只有 maxHeight，
            // 单集走出口①「自家 tag 优先」拿回 16:9 ⇒ 实测出 747x420 族，塞进竖框要横裁 60%+）。
            // 判据照抄参照实现 `HomePage.PrimaryCardImageUrl` + `SeriesImageStub`（t172 / R-1，勿重造）；
            // stub 为 null（无 SeriesId / 无 SeriesPrimaryImageTag / 与自家同 id）⇒ 回落原路径（出口②换剧 id，仍不是 500）。
            var stub = SeriesPosterStub(item);
            var branch = "own";
            string url = null;

            // 同 Search 侧的口径调和：「宽高同时给」只对会走 16:9 素材的那类（单集 / 用到剧海报 stub）；
            // 其余类型保持**改前形态**（`maxHeight` 单给 ⇒ 尺寸参数逐字不变），避免无谓地改掉它们的 URL。
            var both = stub != null || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase);

            if (stub != null)
            {
                url = emby.ImageUrlIfAvailable(stub, "Primary", maxWidth: maxWidth, maxHeight: maxHeight);
                branch = "series-stub";
            }

            if (string.IsNullOrEmpty(url))
            {
                url = both
                    ? emby.ImageUrlIfAvailable(item, "Primary", maxWidth: maxWidth, maxHeight: maxHeight)
                    : emby.ImageUrlIfAvailable(item, "Primary", maxHeight: maxHeight);
                branch = "own";
            }

            // 逐卡读数：只记 id/type/分支/请求口径与 URL 长度，**永不打印 URL**（图 URL 带 api_key）
            AggregateDiagnostics.Write("POSTER-SRC screen=aggregate id=" + item.Id
                + " seriesId=" + (string.IsNullOrEmpty(item.SeriesId) ? "-" : item.SeriesId)
                + " type=" + (string.IsNullOrEmpty(item.Type) ? "-" : item.Type)
                + " branch=" + (string.IsNullOrEmpty(url) ? "none" : branch)
                + " both=" + both
                + " px=" + (both ? maxWidth + "x" + maxHeight : "-x" + maxHeight)
                + " urlLen=" + (url == null ? 0 : url.Length));

            // 空串 = "确无图 ⇒ 不发请求"；对本函数的调用方，它和"取不到 URL"是同一件事（都画占位）
            return string.IsNullOrEmpty(url) ? null : url;
        }
        catch (Exception)
        {
            // 缺 baseUrl/凭据时构造 URL 会抛；同理归一到 null ⇒ 由界面画 #333333 占位（一条坏条目不得拖垮整屏）
            return null;
        }
    }

    /// <summary>
    /// 「主剧集竖海报」的 stub（t305）：与 `HomePage.SeriesImageStub`（t172/R-1）以及 `SearchRunner.SeriesPosterStub`
    /// **逐条同判据**（`Id = SeriesId` + `ImageTags["Primary"] = SeriesPrimaryImageTag`，且排除 SeriesId == 自家 Id）。
    /// 本卡 nonGoal 要求"两个面各写一处等价局部实现、不新增共享单点"，故这是**有意**的第二份；判据变更须三处同步。
    /// 返回 null = 拿不到剧集海报（不构造会 500 的请求）。
    /// </summary>
    private static EmbyItem SeriesPosterStub(EmbyItem item)
    {
        if (item == null
            || string.IsNullOrEmpty(item.SeriesId)
            || string.IsNullOrEmpty(item.SeriesPrimaryImageTag)
            || string.Equals(item.SeriesId, item.Id, StringComparison.Ordinal))
        {
            return null;
        }

        return new EmbyItem
        {
            Id = item.SeriesId,
            Type = "Series",
            ImageTags = new Dictionary<string, string>(StringComparer.Ordinal) { ["Primary"] = item.SeriesPrimaryImageTag },
        };
    }

    /// <summary>
    /// 集数文案的**唯一决定点**（t168 / F4）：只认 `RecursiveItemCount`（"递归子项" = 集）。
    /// <para>
    /// 为什么**不得**回退印 `ChildCount`：那是**直属子项**，剧下多半是"季" ⇒ 印成"集"是错口径
    /// （t82 F3 已在首页实测过同一个坑，判据与 `HomePage.Badge` 同族：`RecursiveItemCount` → 否则不印）。
    /// 缺失 / 非正数 ⇒ 返回空串（**不印集数、也不回退**），同时落一行可搜索标记
    /// （`BADGE-FALLBACK … reason=recursive-item-count-missing`），避免"没有数字"被误读成"没有集数"。
    /// </para>
    /// <para>本屏的**副标题与角标都从这里取**（<see cref="SubtitleOf"/> 与 `AggregatePage.BadgeOf`），
    /// 两处不再各写一份映射 —— 这就是 F4 要收敛的"先例本体"。</para>
    /// </summary>
    /// <param name="where">调用点标签（读数里区分是副标题还是角标触发的缺失）。</param>
    public static string EpisodeCountOf(EmbyItem item, string where)
    {
        if (item == null || !item.IsSeries) { return string.Empty; }
        if (item.RecursiveItemCount is int episodes && episodes > 0) { return episodes.ToString(); }

        Program.Log("BADGE-FALLBACK itemId=" + (item.Id ?? string.Empty)
            + " reason=recursive-item-count-missing type=Series where=" + (where ?? "-")
            + " childCount=" + (item.ChildCount?.ToString() ?? "-")
            + "（不回退印 ChildCount：语义是「直属子项」、多半为季）");
        return string.Empty;
    }

    /// <summary>副标题：剧集给 <c>S01E02 · 集名</c>，其余给类型/年份（与 元信息行同族）。</summary>
    public static string SubtitleOf(EmbyItem item)
    {
        if (item == null) return string.Empty;
        if (item.IsEpisode)
        {
            var code = item.EpisodeCodeText;
            return code.Length > 0 ? code + " · " + item.Name : item.Name;
        }

        // t168（F4 本体）：集数走**同一个决定点** `EpisodeCountOf`（只认 `RecursiveItemCount`）。
        if (item.IsSeries)
        {
            var episodes = EpisodeCountOf(item, "subtitle");
            if (episodes.Length > 0) { return "剧集 · " + episodes + " 集"; }
        }

        var year = item.ProductionYear.HasValue ? item.ProductionYear.Value.ToString() : string.Empty;
        return year.Length > 0 ? (item.Type ?? string.Empty) + " · " + year : (item.Type ?? string.Empty);
    }
}

// t76：**库类型 → 条目类型过滤**（Emby `IncludeItemTypes`）的**唯一落点**。
//
// 为什么必须单点（captain 2026-09-12 立卡）：原先这套映射只活在 `shell/App/Features/Library/LibraryPage.xaml.cs`
// 的私有方法里（`ItemTypesForView`），而首页/其它屏各自写自己的查询 ⇒ **同一个库在不同屏上取到不同种类的东西**。
// 实测证据（t78，真机 ServerA）：**三个电视库各 20 条返回的全是 `Episode`**（首页行没有类型过滤 + `Recursive=true`
// 递归泄出单集）⇒ 行里铺的是单集、且单集常常没有自家主图（那条又与图片 500 相关）。
// ⇒ 约定：凡"按库取条目"的地方，`includeItemTypes` **一律**取本类的返回值，不要再各写一套 switch。

using System;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Emby;

/// <summary>Emby 库（<c>CollectionType</c>）→ <c>IncludeItemTypes</c> 的共享映射（唯一落点）。</summary>
public static class EmbyViewTypes
{
    // ── 单值常量（供调用方做等值比较/拼查询，避免在各处硬编码字符串）──
    public const string Movie = "Movie";
    public const string Series = "Series";
    public const string Episode = "Episode";
    public const string BoxSet = "BoxSet";
    public const string MusicAlbum = "MusicAlbum";
    public const string Audio = "Audio";
    public const string Video = "Video";
    public const string Book = "Book";
    public const string AudioBook = "AudioBook";

    /// <summary>「电影」库。</summary>
    public const string MovieTypes = Movie;

    /// <summary>「电视节目」库 ⇒ **剧**（不是单集）。</summary>
    public const string TvShowTypes = Series;

    /// <summary>「合集」库。</summary>
    public const string BoxSetTypes = BoxSet;

    /// <summary>「音乐」库。</summary>
    public const string MusicTypes = MusicAlbum + "," + Audio;

    /// <summary>「家庭视频」库。</summary>
    public const string HomeVideoTypes = Video;

    /// <summary>「书」库。</summary>
    public const string BookTypes = Book;

    /// <summary>「有声书」库。</summary>
    public const string AudioBookTypes = AudioBook;

    /// <summary>
    /// **未知 / 空 / null 的库类型**用这一串（t76 修掉的隐患）：**绝不返回 null**。
    /// 为什么：`GetItemsAsync` 默认 `recursive: true`，`includeItemTypes` 给 null ⇒ 请求里干脆没有该参数
    /// ⇒ TV 库会把 **Episode 递归泄出来**（用户当面报的「一小集一小集」），卡片也就变成一条一条单集。
    /// 给一个"混合类型白名单"至少把"容器类/未建模类型"挡在外面。
    /// </summary>
    public const string DefaultMixedTypes = "Movie,Series,Video,MusicVideo,BoxSet";

    /// <summary>
    /// 库类型 → 条目类型过滤（Emby <c>IncludeItemTypes</c>）。
    /// **未知/空/null ⇒ <see cref="DefaultMixedTypes"/>（绝不是 null）**；已知七类按上表返回。
    /// 语义与 `LibraryPage.ItemTypesForView` 原实现**逐值一致**（差异只有一处：原实现的 `default: return null`
    /// 换成混合白名单 —— 这正是本卡要修的隐患）。**调用方 = `ui` 在 t79 把 `LibraryPage` 那份私有 switch 改为委托本方法。**
    /// ⚠️ 这个重载**拿不到视图名**（兜底日志里会写 `(string 重载：无视图名)`）—— 想要日志带视图名请用
    /// <see cref="ItemTypesForView(EmbyUserView)"/>。
    /// </summary>
    public static string ItemTypesForView(string collectionType) => Resolve(collectionType, null);

    /// <summary>
    /// 同上的 <see cref="EmbyUserView"/> 重载（**推荐入口**：兜底日志能带出视图名）。
    /// 依赖 <c>EmbyUserView.CollectionType</c>（`EmbyModels.cs` 已建模并已从 JSON 解析 ⇒ 不需要新增字段）。
    /// </summary>
    public static string ItemTypesForView(EmbyUserView view) => Resolve(view?.CollectionType, view?.Name);

    /// <summary>
    /// 单一落点。**走 default 分支（= 空 / 未映射的库类型）时落一行可见日志**（t76 追加，captain 2026-09-12）：
    /// 「**未知输入的兜底分支必须有可见读数** —— 兜底本身没错，**静默**兜底才是缺陷」
    /// （今晚同类已有两例：图片出口静默 500、本条视图类型静默降级）。
    /// 限速：同一 (视图名, 原始 collectionType, 原因) 只落一条，避免整屏刷屏。
    /// </summary>
    private static string Resolve(string collectionType, string viewName)
    {
        var raw = collectionType ?? string.Empty;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "movies":
                return MovieTypes;
            case "tvshows":
                return TvShowTypes;
            case "boxsets":
                return BoxSetTypes;
            case "music":
                return MusicTypes;
            case "homevideos":
                return HomeVideoTypes;
            case "books":
                return BookTypes;
            case "audiobooks":
                return AudioBookTypes;
            default:
                var reason = raw.Trim().Length == 0 ? "empty-collectiontype" : "unmapped-collectiontype";
                LogFallbackOnce(viewName, raw, reason);
                return DefaultMixedTypes;
        }
    }

    /// <summary>已落过兜底日志的桶（进程级；键 = 视图名|原始值|原因）。</summary>
    private static readonly System.Collections.Generic.HashSet<string> FallbackLogged =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

    private static void LogFallbackOnce(string viewName, string raw, string reason)
    {
        var name = string.IsNullOrEmpty(viewName) ? "(string 重载：无视图名)" : viewName;
        bool first;
        lock (FallbackLogged) first = FallbackLogged.Add(name + "|" + raw + "|" + reason);
        if (!first) return;
        // 判据要求：视图名 + 原始 collectionType + 落到哪个 types（+ 原因，区分"空"与"未映射"）
        DebugLog.Warn($"VIEW-TYPE-FALLBACK view=\"{name}\" collectionType=\"{raw}\" reason={reason} => types=\"{DefaultMixedTypes}\"");
    }

    /// <summary>是否"按剧铺行"的库（首页行想铺剧而不是单集时用这个判据，别各写一套）。</summary>
    public static bool IsTvShows(string collectionType)
        => string.Equals((collectionType ?? string.Empty).Trim(), "tvshows", StringComparison.OrdinalIgnoreCase);
}

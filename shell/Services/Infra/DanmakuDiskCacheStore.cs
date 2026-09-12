// S5 体验设施（t33）：弹幕磁盘缓存 —— 按 **match-name** 键缓存某个番剧的弹幕取数结果。
// 与内核取数面的配合：外壳拿着 match-name 去弹幕 API 取数（内核侧另有 --danmaku-api= 的配置面），
// 取回后按 match-name 落盘；同一 match-name 二次取数直接命中缓存 ⇒ 少发一次网络请求。
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>弹幕磁盘缓存（目录 = 数据根下的 <c>cache\danmaku</c>，事实 35 裁决保留该落点）。</summary>
public sealed class DanmakuDiskCacheStore
{
    private readonly DiskCacheStore _store;
    private readonly Action<string> _log;

    public DanmakuDiskCacheStore(AppDataDir data, long maxBytes = Constants.AppConstants.DiskCacheDefaultMaxBytes, int maxEntries = Constants.AppConstants.DanmakuCacheMaxEntries, Action<string> log = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        _log = log;
        Directory = data.DanmakuCacheDir;
        _store = new DiskCacheStore(Directory, maxBytes, maxEntries, log);
    }

    public string Directory { get; }

    /// <summary><c>fetch</c> 被真正调用的次数（**命中不计**）—— 判据「二次取数走缓存」的观测点。</summary>
    public int FetchCount { get; private set; }

    /// <summary>命中次数。</summary>
    public int HitCount { get; private set; }

    public string PathForMatch(string matchName) => _store.PathFor("danmaku:" + (matchName ?? string.Empty));

    public bool Contains(string matchName) => _store.Contains("danmaku:" + (matchName ?? string.Empty));

    /// <summary>命中 ⇒ 返回 JSON 文本；未命中 ⇒ <c>null</c>。</summary>
    public string TryGet(string matchName)
    {
        var bytes = _store.TryRead("danmaku:" + (matchName ?? string.Empty));
        if (bytes == null)
        {
            _log?.Invoke($"DANMAKU-CACHE-MISS match={matchName ?? "-"}");
            return null;
        }
        HitCount++;
        var json = Encoding.UTF8.GetString(bytes);
        _log?.Invoke($"DANMAKU-CACHE-HIT match={matchName ?? "-"} bytes={bytes.Length}");
        return json;
    }

    public void Put(string matchName, string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        _store.Write("danmaku:" + (matchName ?? string.Empty), Encoding.UTF8.GetBytes(json));
        _log?.Invoke($"DANMAKU-CACHE-STORE match={matchName ?? "-"} bytes={Encoding.UTF8.GetByteCount(json)}");
    }

    /// <summary>
    /// 取数（命中即返回，**不调用 <paramref name="fetch"/>**）；未命中 ⇒ 调一次 + 落盘。
    /// t148：<paramref name="cancellationToken"/> 现在有实际效果 —— 入口即查、取数返回后再查。
    /// ⚠️ 该重载的 <paramref name="fetch"/> **签名里没有 CT**（改签名会破坏既有调用方，不在本卡范围），
    ///     所以委托内部本身不可取消；需要"取数也能被取消"的调用方请用下面的 CT 版重载。
    /// </summary>
    public async Task<string> GetOrFetchAsync(string matchName, Func<Task<string>> fetch, CancellationToken cancellationToken = default)
        => await GetOrFetchAsync(matchName, _ => fetch(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// t148：**CT 可转发**的重载 —— 取消令牌既走边界检查，也**传进取数委托**（`fetch(ct)`），
    /// 还走落盘（`DiskCacheStore.Write` → `AtomicFile`）。已取消 ⇒ 抛 <c>OperationCanceledException</c>，
    /// 且**不会**调 `fetch`、不会落盘（"取消不是失败，必须可见地中止"）。
    /// </summary>
    public async Task<string> GetOrFetchAsync(string matchName, Func<CancellationToken, Task<string>> fetch, CancellationToken cancellationToken = default)
    {
        var hit = TryGet(matchName);
        if (hit != null) return hit;

        if (fetch == null) return null;

        cancellationToken.ThrowIfCancellationRequested();   // 边界①：已取消 ⇒ 不发起取数
        FetchCount++;
        var fresh = await fetch(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();   // 边界②：取回后、落盘前再查（避免"取消后仍然写缓存"）
        if (!string.IsNullOrEmpty(fresh)) Put(matchName, fresh);
        return fresh;
    }

    public CacheStats Stats() => _store.Stats();

    public int Prune() => _store.Prune();

    public int Clear() => _store.Clear();
}

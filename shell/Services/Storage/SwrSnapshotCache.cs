// t44-B：**通用 SWR（stale-while-revalidate）快照助手** —— 收藏 / 首页 / 聚合视界共用一件，不是三处各写一遍。
//
// 用户诉求（原话）：「收藏就可以加一个缓存，先显示缓存，然后再查询。」
// ⇒ 三步模型：① `TryRead`（**零网络**同步读盘，拿得到就先显示）②调用方在后台 `RefreshAsync`
//    （取数 + 落盘）③刷新完成后用返回值**替换面板内容**（UI 不需要用户手动重进）。
//
// 复用 t33 的 `DiskCacheStore`（SHA1 文件名 + 临时文件覆盖写 + LRU + 条数/字节双上限），**不新造轮子**。
// 快照格式：JSON（`System.Text.Json`，`camelCase` 且 `null` 不写出）；TTL 走**文件 mtime**（`DiskCacheStore.PathFor`）。
// 语义：`Fresh`（未过期，直接当最新用）｜`Stale`（过期但可先显示，随后必须刷新）｜`Miss`（无可用缓存 ⇒ 冷启动）。
// 失效条件：① 超 TTL ② 落盘键被 `Invalidate`/`Clear` ③ 反序列化失败（**坏缓存当 Miss，不得当 Fresh**）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Infra;

namespace AIPlayer.Shell.Services.Storage;

/// <summary>缓存新鲜度。</summary>
public enum SwrFreshness
{
    /// <summary>没有可用快照 ⇒ 调用方必须走网络（冷启动）。</summary>
    Miss = 0,

    /// <summary>有快照但已超 TTL ⇒ **先显示**，随后必须刷新。</summary>
    Stale = 1,

    /// <summary>快照未过期 ⇒ 可直接当最新内容用（仍可选择后台刷新）。</summary>
    Fresh = 2,
}

/// <summary>`TryRead` 的结果（**不含任何网络动作**）。</summary>
public sealed class SwrLookup<T>
{
    public SwrFreshness Freshness { get; set; } = SwrFreshness.Miss;

    public T Value { get; set; }

    /// <summary>快照落盘时刻（UTC）；无缓存时为 <c>null</c>。</summary>
    public DateTime? CachedAtUtc { get; set; }

    /// <summary>快照年龄（秒）；无缓存时为 <c>null</c>。</summary>
    public double? AgeSeconds { get; set; }

    /// <summary>是否拿到了可显示内容（Fresh 或 Stale）。</summary>
    public bool HasValue => Freshness != SwrFreshness.Miss && Value != null;

    public override string ToString()
        => Freshness == SwrFreshness.Miss
            ? "Miss（无可用缓存）"
            : $"{Freshness}｜age={(AgeSeconds.HasValue ? AgeSeconds.Value.ToString("0.0") + "s" : "-")}｜cachedAt={CachedAtUtc:HH:mm:ss}";
}

/// <summary>一次刷新的结果。</summary>
public sealed class SwrRefreshResult<T>
{
    /// <summary>取到的新值；失败时为 <c>null</c>。</summary>
    public T Value { get; set; }

    /// <summary>是否真的走了网络并成功（失败 ⇒ false）。</summary>
    public bool Success { get; set; }

    /// <summary>失败原因（本地判定，绝不写"成功"）。</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>本次耗时（毫秒，用于读数）。</summary>
    public double ElapsedMs { get; set; }

    /// <summary>本次刷新前的缓存新鲜度（便于 UI 判断"是先显示再刷新"还是"冷启动"）。</summary>
    public SwrFreshness PreviousFreshness { get; set; }

    public override string ToString()
        => (Success ? $"刷新成功 {ElapsedMs:0}ms（此前 {PreviousFreshness}）" : $"刷新失败 {ElapsedMs:0}ms：{Error}（此前 {PreviousFreshness}）");
}

/// <summary>
/// §12 一次「**缓存优先 + 并发刷新**」的完整读数（三态可分辨）。
/// <list type="bullet">
/// <item><see cref="CacheHit"/>/<see cref="CacheFreshness"/>/<see cref="CacheAgeSeconds"/> = **T0 缓存态**（可过期）；</item>
/// <item><see cref="RefreshStarted"/> = **刷新中**（T0 同一时刻已发起，不等渲染完）；</item>
/// <item><see cref="RefreshSucceeded"/> = **已刷新**（T1 就地替换）；</item>
/// <item>🔴 §12.3：失败 ⇒ <see cref="Cached"/> **保留**、<see cref="CachePreserved"/>=true、**不删缓存**，且 <see cref="Error"/> 非空（UI 必须显示可见失败提示）。</item>
/// <item>🔴 §12.4④：<see cref="CachedIds"/> 与 <see cref="RefreshedIds"/> **分别打印**（证明内容真来自缓存）。</item>
/// </list>
/// </summary>
public sealed class SwrLoadResult<T>
{
    public string Key { get; set; } = string.Empty;

    /// <summary>T0 是否拿到缓存（false ⇒ 冷启动 ⇒ UI 出骨架屏但功能仍可用）。</summary>
    public bool CacheHit { get; set; }

    public SwrFreshness CacheFreshness { get; set; } = SwrFreshness.Miss;

    public DateTime? CacheCachedAtUtc { get; set; }

    public double? CacheAgeSeconds { get; set; }

    /// <summary>T0 立即可渲染的缓存内容（无缓存 ⇒ null）。</summary>
    public T Cached { get; set; }

    /// <summary>缓存态读出的 id 集合（§12.4④ 左半）。</summary>
    public List<string> CachedIds { get; } = new List<string>();

    /// <summary>T1 刷新后的内容（失败 ⇒ null，UI 用 <see cref="Cached"/>）。</summary>
    public T Value { get; set; }

    /// <summary>刷新后的 id 集合（§12.4④ 右半）。</summary>
    public List<string> RefreshedIds { get; } = new List<string>();

    /// <summary>T0 同一时刻是否已发起真实请求。</summary>
    public bool RefreshStarted { get; set; }

    public bool RefreshSucceeded { get; set; }

    /// <summary>失败原因（本地判定；绝不写"成功"）。</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>§12.3：刷新失败但缓存仍在（**内容未被清空、缓存未被删**）。</summary>
    public bool CachePreserved => CacheHit && !RefreshSucceeded && Cached != null;

    public double ElapsedMs { get; set; }

    /// <summary>可直接进日志的一行读数（`cache=… age=… refresh=… ids=`）。</summary>
    public string Evidence
        => $"key={Key} cache={(CacheHit ? "hit" : "miss")}" + (CacheAgeSeconds.HasValue ? $" age={CacheAgeSeconds.Value:0.0}s" : string.Empty)
           + $" refresh={(RefreshSucceeded ? "done" : (RefreshStarted ? "started/failed" : "not-started"))}"
           + $" cachedIds=[{string.Join(",", CachedIds)}] refreshedIds=[{string.Join(",", RefreshedIds)}]"
           + (RefreshSucceeded ? string.Empty : $" error={Error} cache-preserved={CachePreserved}");

    public override string ToString() => Evidence;
}

/// <summary>
/// 通用 SWR 快照缓存。**一个实例可服务多种键**（键 = 逻辑名，如 `favorites:srv-1`）。
/// </summary>
public sealed class SwrSnapshotCache<T>
{
    private const string ManifestKey = "__swr_manifest__";

    private readonly DiskCacheStore _store;
    private readonly TimeSpan _ttl;
    private readonly Action<string> _log;
    private readonly JsonSerializerOptions _json = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <param name="directory">快照目录（一般取 <c>AppDataDir</c> 下的子目录）。</param>
    /// <param name="ttl">有效期；超过 ⇒ <see cref="SwrFreshness.Stale"/>（仍可先显示）。</param>
    public SwrSnapshotCache(string directory, TimeSpan ttl, long maxBytes = Constants.AppConstants.SwrCacheDefaultMaxBytes, int maxEntries = Constants.AppConstants.SwrCacheDefaultMaxEntries, Action<string> log = null)
    {
        _store = new DiskCacheStore(directory, maxBytes, maxEntries, log);
        _ttl = ttl;
        _log = log;
    }

    /// <summary>落盘目录（证据用）。</summary>
    public string Directory => _store.Directory;

    /// <summary>TTL（证据用）。</summary>
    public TimeSpan Ttl => _ttl;

    /// <summary>当前快照条数。</summary>
    public int Count => _store.Stats().Count;

    public string PathFor(string key) => _store.PathFor(key);

    /// <summary>
    /// **零网络的**读盘：能读到就返回内容（Fresh 或 Stale），否则 Miss。
    /// 坏缓存（反序列化失败）一律当 Miss（不得当 Fresh 显示半截内容）。
    /// </summary>
    public SwrLookup<T> TryRead(string key)
    {
        var lookup = new SwrLookup<T>();
        if (string.IsNullOrWhiteSpace(key)) return lookup;

        var path = _store.PathFor(key);
        byte[] bytes;
        try
        {
            bytes = _store.TryRead(key);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"SWR-READ-FAIL key={key} {ex.GetType().Name}: {ex.Message}");
            return lookup;
        }
        if (bytes == null || bytes.Length == 0) return lookup;

        T value;
        DateTime cachedAt;
        try
        {
            // ⚠️ 时间戳**写在快照里**，不能用文件 mtime：`DiskCacheStore.TryRead` 会为了 LRU **触碰 mtime**
            //    ⇒ 用 mtime 算 TTL 会永远得到 age≈0（本轮实测踩到，故改为信封内自带 `cachedAtUtc`）。
            var envelope = JsonSerializer.Deserialize<SnapshotEnvelope>(Encoding.UTF8.GetString(bytes), _json);
            if (envelope == null || envelope.Value == null)
            {
                _log?.Invoke($"SWR-BAD-SNAPSHOT key={key}（信封缺 value，当 Miss）");
                return lookup;
            }
            value = envelope.Value;
            cachedAt = envelope.CachedAtUtc;
        }
        catch (Exception ex)
        {
            // 反控要求：**损坏快照 ⇒ 隔离为 `.corrupt` 并返回 null**（不得当 Fresh 显示半截内容）
            var quarantined = Quarantine(path);
            _log?.Invoke($"SWR-BAD-SNAPSHOT key={key} {ex.GetType().Name} ⇒ 隔离为 {quarantined}（返回 Miss）");
            return lookup;
        }

        var age = (DateTime.UtcNow - cachedAt).TotalSeconds;
        lookup.Value = value;
        lookup.CachedAtUtc = cachedAt;
        lookup.AgeSeconds = age;
        lookup.Freshness = (age >= 0 && age <= _ttl.TotalSeconds) ? SwrFreshness.Fresh : SwrFreshness.Stale;
        _log?.Invoke($"SWR-READ key={key} ⇒ {lookup.Freshness}（age={age:0.0}s，ttl={_ttl.TotalSeconds:0}s）");
        return lookup;
    }

    /// <summary>删除**一条**快照（按落盘键）。</summary>
    public bool InvalidateKey(string key) => !string.IsNullOrWhiteSpace(key) && _store.Remove(key);

    // ── 卡面要求的「按服务器」三个入口（`serverId` 作用域；底层仍是同一件 DiskCacheStore）────────────
    //    键的落盘形态 = `{serverId}::{key}`；另有一份 manifest（key → serverId）支撑「按服务器失效」。

    /// <summary>读某服务器的某条快照（**零网络**）；无快照 ⇒ `Freshness = Miss` 且 `Value = null`（**不用空集合冒充**）。</summary>
    public SwrLookup<T> TryGetSnapshot(string serverId, string key) => TryRead(ScopedKey(serverId, key));

    /// <summary>写某服务器的一条快照（**带写入时间戳**，信封内 `cachedAtUtc`）；返回是否落盘成功。</summary>
    public bool SaveSnapshot(string serverId, string key, T value)
    {
        if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(key) || value == null) return false;
        var scoped = ScopedKey(serverId, key);
        Write(scoped, value);
        var manifest = ReadManifest();
        manifest[scoped] = serverId;
        WriteManifest(manifest);
        return _store.Contains(scoped);
    }

    /// <summary>按服务器失效：删掉该服务器名下**全部**快照（返回删除条数）。</summary>
    public int Invalidate(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return 0;
        var manifest = ReadManifest();
        var doomed = manifest.Where(kv => string.Equals(kv.Value, serverId, StringComparison.Ordinal)).Select(kv => kv.Key).ToList();
        var removed = 0;
        foreach (var key in doomed)
        {
            if (_store.Remove(key)) removed++;
            manifest.Remove(key);
        }
        WriteManifest(manifest);
        _log?.Invoke($"SWR-INVALIDATE server={serverId} ⇒ 删除 {removed} 条快照");
        return removed;
    }

    /// <summary>当前已登记的（服务器, 键）快照清单（证据/测试用）。</summary>
    public IReadOnlyList<KeyValuePair<string, string>> SnapshotIndex()
        => ReadManifest().ToList();

    private static string ScopedKey(string serverId, string key) => (serverId ?? string.Empty) + "::" + (key ?? string.Empty);

    /// <summary>清空全部快照（返回删除条数；反控用：清掉后热启动路径必须退化成冷启动）。</summary>
    public int Clear()
    {
        var removed = _store.Clear();
        InvalidateManifest();
        return removed;
    }

    /// <summary>损坏快照隔离：`x.cache` → `x.cache.corrupt`（**不删除**，保留现场）。</summary>
    private string Quarantine(string cachePath)
    {
        var quarantined = cachePath + ".corrupt";
        try
        {
            if (File.Exists(cachePath)) File.Move(cachePath, quarantined, overwrite: true);
        }
        catch (IOException) { /* 有意忽略：隔离文件被占用 => 调用方仍按 Miss 处理（绝不冒泡） */ }
        return quarantined;
    }

    private Dictionary<string, string> ReadManifest()
    {
        try
        {
            var bytes = _store.TryRead(ManifestKey);
            if (bytes == null || bytes.Length == 0) return new Dictionary<string, string>(StringComparer.Ordinal);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(bytes), _json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"SWR-MANIFEST-READ-FAIL {ex.GetType().Name}（当空清单）");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void WriteManifest(Dictionary<string, string> manifest)
    {
        try
        {
            _store.Write(ManifestKey, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, _json)));
        }
        catch (Exception ex)
        {
            _log?.Invoke($"SWR-MANIFEST-WRITE-FAIL {ex.GetType().Name}");
        }
    }

    private void InvalidateManifest() => _store.Remove(ManifestKey);

    /// <summary>
    /// 后台刷新：<paramref name="fetch"/> 取数 → 落盘 → 返回新值（供 UI 替换面板）。
    /// **失败不抛异常**（返回 <see cref="SwrRefreshResult{T}.Success"/>=false + 原因）；**失败不落盘**（不污染旧快照）。
    /// </summary>
    public async Task<SwrRefreshResult<T>> RefreshAsync(string key, Func<CancellationToken, Task<T>> fetch, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = new SwrRefreshResult<T> { PreviousFreshness = TryRead(key).Freshness };
        if (fetch == null)
        {
            result.Error = "no-fetch-delegate";
            return result;
        }

        try
        {
            var value = await fetch(cancellationToken).ConfigureAwait(false);
            if (value == null)
            {
                result.Error = "empty-result";
                result.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
                _log?.Invoke($"SWR-REFRESH key={key} ⇒ 空结果（不落盘）");
                return result;
            }
            Write(key, value);
            result.Value = value;
            result.Success = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Error = "cancelled";
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + ex.Message;
            _log?.Invoke($"SWR-REFRESH-FAIL key={key} {result.Error}");
        }

        result.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
        if (result.Success) _log?.Invoke($"SWR-REFRESH key={key} ⇒ ok（{result.ElapsedMs:0}ms，此前 {result.PreviousFreshness}）");
        return result;
    }

    /// <summary>
    /// 热启动组合拳：**先读缓存**（有则立刻可用）→ 再后台刷新 → 给"刷新后"的新值。
    /// <paramref name="onStaleValue"/> 在拿到缓存内容时同步回调（UI 先渲染）；返回刷新结果。
    /// </summary>
    public async Task<SwrRefreshResult<T>> ReadThenRefreshAsync(
        string key,
        Func<CancellationToken, Task<T>> fetch,
        Action<SwrLookup<T>> onStaleValue = null,
        CancellationToken cancellationToken = default)
    {
        var lookup = TryRead(key);
        if (lookup.HasValue && onStaleValue != null)
        {
            onStaleValue(lookup);   // 缓存在手 ⇒ 先显示（零网络）
        }
        return await RefreshAsync(key, fetch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>快照信封：自带写入时刻（不依赖文件 mtime —— 后者被 LRU 触碰过）。</summary>
    private sealed class SnapshotEnvelope
    {
        public DateTime CachedAtUtc { get; set; }

        public T Value { get; set; }
    }

    /// <summary>
    /// 🔴 **§12 唯一入口**（一个以「缓存键 + 取数委托」为参数的通用件；各屏**不得**自写一套）：
    /// T0 **同步读缓存**（有则**立即**回调 <paramref name="onCacheValue"/> 渲染）＋ **同一时刻并发**发起
    /// <paramref name="fetch"/>；T1 成功 ⇒ 落盘 + 返回新内容（就地替换）；失败 ⇒ **保留缓存** + 明确失败（不清空、不删缓存）。
    /// <para>日志可取证：`SWR-LOAD cache=hit age=…s` / `refresh=started` / `refresh=done ids=[…]` / 末行**分别打印** cachedIds 与 refreshedIds。</para>
    /// </summary>
    /// <param name="key">缓存键（**调用方**用 serverId 等前缀自行区分作用域）。</param>
    /// <param name="fetch">取数委托（真实网络/服务调用）。</param>
    /// <param name="onCacheValue">T0 缓存命中时的**同步**渲染回调（UI 先显示旧内容）。</param>
    /// <param name="idsOf">内容 → id 集合（§12.4④ 两组 id 分别打印；不传则打印条数占位）。</param>
    public async Task<SwrLoadResult<T>> LoadAsync(
        string key,
        Func<CancellationToken, Task<T>> fetch,
        Action<SwrLookup<T>> onCacheValue = null,
        Func<T, IEnumerable<string>> idsOf = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = new SwrLoadResult<T> { Key = key };

        // ── T0：同步读缓存（**零网络**）────────────────────────────────────────
        var lookup = TryRead(key);
        result.CacheHit = lookup.HasValue;
        result.CacheFreshness = lookup.Freshness;
        result.CacheCachedAtUtc = lookup.CachedAtUtc;
        result.CacheAgeSeconds = lookup.AgeSeconds;
        result.Cached = lookup.Value;
        CollectIds(result.CachedIds, lookup.Value, idsOf);
        _log?.Invoke($"SWR-LOAD key={key} cache={(lookup.HasValue ? "hit" : "miss")}"
                     + (lookup.AgeSeconds.HasValue ? $" age={lookup.AgeSeconds.Value:0.0}s" : string.Empty)
                     + $" ids=[{string.Join(",", result.CachedIds)}]");

        // ── §12.1：**先起请求**，再同步回调渲染 ⇒ 两者同一时刻并发（不是"渲染完再发"）──────
        Task<T> pending = null;
        if (fetch != null)
        {
            try
            {
                pending = fetch(cancellationToken);
                result.RefreshStarted = pending != null;
                if (result.RefreshStarted) _log?.Invoke($"SWR-LOAD key={key} refresh=started（与 T0 渲染并发）");
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name + ": " + ex.Message;
                _log?.Invoke($"SWR-LOAD key={key} refresh=started-failed {result.Error}");
            }
        }
        if (lookup.HasValue && onCacheValue != null)
        {
            try
            {
                onCacheValue(lookup);      // T0 渲染（此回调执行时请求已在途）
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SWR-CACHE-RENDER-FAIL key={key} {ex.GetType().Name}（UI 异常不得影响刷新）");
            }
        }

        // ── T1：等待刷新结果（成功落盘 + 替换；失败保留缓存）──────────────────
        if (pending != null)
        {
            try
            {
                var value = await pending.ConfigureAwait(false);
                if (value == null)
                {
                    result.Error = "empty-result";
                    _log?.Invoke($"SWR-LOAD key={key} refresh=done-but-empty（**不落盘、不清缓存**，cache-preserved={result.CachePreserved}）");
                }
                else
                {
                    Write(key, value);
                    result.Value = value;
                    result.RefreshSucceeded = true;
                    CollectIds(result.RefreshedIds, value, idsOf);
                    _log?.Invoke($"SWR-LOAD key={key} refresh=done ids=[{string.Join(",", result.RefreshedIds)}]");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.Error = "cancelled";
                _log?.Invoke($"SWR-LOAD key={key} refresh=cancelled（cache-preserved={result.CachePreserved}）");
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name + ": " + ex.Message;
                // §12.3：失败**不清空、不删缓存**，只给可见失败
                _log?.Invoke($"SWR-LOAD key={key} refresh=failed {result.Error}（**保留缓存** cache-preserved={result.CachePreserved}）");
            }
        }

        result.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
        // §12.4④：两组 id **分别打印**（一行总账，便于反控取证）
        _log?.Invoke($"SWR-LOAD key={key} cachedIds=[{string.Join(",", result.CachedIds)}] refreshedIds=[{string.Join(",", result.RefreshedIds)}] elapsed={result.ElapsedMs:0}ms");
        return result;
    }

    private static void CollectIds(List<string> sink, T value, Func<T, IEnumerable<string>> idsOf)
    {
        if (sink == null || value == null || idsOf == null) return;
        try
        {
            foreach (var id in idsOf(value) ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrEmpty(id)) sink.Add(id);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or ArgumentException or FormatException) { /* 有意忽略：idsOf 委托抛错 => 只少一行取证，不影响主流程 */ }
    }

    private void Write(string key, T value)
    {
        try
        {
            var envelope = new SnapshotEnvelope { CachedAtUtc = DateTime.UtcNow, Value = value };
            var json = JsonSerializer.Serialize(envelope, _json);
            _store.Write(key, Encoding.UTF8.GetBytes(json));
        }
        catch (Exception ex)
        {
            _log?.Invoke($"SWR-WRITE-FAIL key={key} {ex.GetType().Name}: {ex.Message}");
        }
    }
}

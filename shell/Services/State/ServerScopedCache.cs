// 原版文件（rebuild/ 中不存在 `server_scoped_cache.dart`），按 DESIGN §5 归属表 #3
// （`src/core/application/server_scoped_cache.dart` → `Services/State/ServerScopedCache.cs`，处置「新写」）新写。
//
// 语义（推断口径，原版无实例文本可对照）：把「按服务器维度分组、可整体失效」的缓存从业务状态里抽出来，
// 避免每个 Controller 自己维护 `Map<serverId, X>` 并在换服/删服时漏清。
//   - 按 serverId 分组；**线程安全**（所有读写走同一把锁）；
//   - 可失效：`Invalidate(serverId)`（换服/删服）、`Clear()`（退出登录/全量刷新）、`Remove(serverId,key)`（单条）；
//   - 可选 TTL：`Set(..., ttl)` 到期后视为未命中（`ScopedCacheDefaultTtlSeconds = 0` ⇒ 不过期）；
//   - `GetOrAdd` 单飞：同一键并发请求只执行一次工厂（工厂在锁外执行，避免持锁做 IO）。

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.State;

namespace AIPlayer.Shell.Services.State;

/// <summary>服务器维度的作用域缓存（对应原版 `server_scoped_cache.dart`）。</summary>
/// <typeparam name="T">缓存值类型。</typeparam>
public sealed class ServerScopedCache<T>
{
    private sealed class Entry
    {
        public T Value;
        public DateTime ExpiresAtUtc; // DateTime.MaxValue ⇒ 不过期
        public bool IsExpired => ExpiresAtUtc != DateTime.MaxValue && DateTime.UtcNow >= ExpiresAtUtc;
    }

    private readonly object _gate = new object();
    private readonly Dictionary<string, Entry> _values = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _locks = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

    /// <summary>分组键（一般填 <c>ServerConfig.Id</c>；空键归入 <see cref="LocalScope"/>）。</summary>
    public const string LocalScope = "__local__";

    /// <summary>条目上限（超出时按「最早过期/最早写入」淘汰；0 表示不限）。</summary>
    public int MaxEntries { get; set; } = Constants.AppConstants.ScopedCacheMaxEntries;

    /// <summary>可选变更通知（换服/失效时让 UI 重新取数）。</summary>
    public event EventHandler Changed;

    public static string ScopeOf(ServerConfig server) => server == null || string.IsNullOrEmpty(server.Id) ? LocalScope : server.Id;

    public static string ScopeOf(string serverId) => string.IsNullOrEmpty(serverId) ? LocalScope : serverId;

    public bool Has(string serverId, string key)
    {
        var scope = ScopeOf(serverId);
        lock (_gate)
        {
            return _values.TryGetValue(Composite(scope, key), out var entry) && !entry.IsExpired;
        }
    }

    public bool TryGet(string serverId, string key, out T value)
    {
        value = default;
        var scope = ScopeOf(serverId);
        lock (_gate)
        {
            if (!_values.TryGetValue(Composite(scope, key), out var entry)) return false;
            if (entry.IsExpired)
            {
                _values.Remove(Composite(scope, key));
                return false;
            }
            value = entry.Value;
            return true;
        }
    }

    public T Get(string serverId, string key, T fallback = default)
        => TryGet(serverId, key, out var value) ? value : fallback;

    /// <summary>写入；<paramref name="ttl"/> 为 null 时用 <see cref="Constants.AppConstants.ScopedCacheDefaultTtlSeconds"/>。</summary>
    public void Set(string serverId, string key, T value, TimeSpan? ttl = null)
    {
        if (string.IsNullOrEmpty(key)) return;
        var scope = ScopeOf(serverId);
        var effective = ttl;
        if (effective == null && Constants.AppConstants.ScopedCacheDefaultTtlSeconds > 0)
        {
            effective = TimeSpan.FromSeconds(Constants.AppConstants.ScopedCacheDefaultTtlSeconds);
        }

        lock (_gate)
        {
            _values[Composite(scope, key)] = new Entry
            {
                Value = value,
                ExpiresAtUtc = effective.HasValue ? DateTime.UtcNow.Add(effective.Value) : DateTime.MaxValue,
            };
            TrimLocked();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 读缓存，未命中时执行 <paramref name="factory"/> 并写入。
    /// 同一 (serverId,key) 的并发调用只执行一次工厂（避免同一屏重复打服务器）。
    /// </summary>
    public T GetOrAdd(string serverId, string key, Func<T> factory, TimeSpan? ttl = null)
    {
        if (string.IsNullOrEmpty(key) || factory == null) return default;
        if (TryGet(serverId, key, out var cached)) return cached;

        var scope = ScopeOf(serverId);
        var gateKey = Composite(scope, key);
        var gate = _locks.GetOrAdd(gateKey, _ => new object());
        lock (gate)
        {
            if (TryGet(serverId, key, out cached)) return cached;

            // 工厂在锁外执行（本方法持的是「每键锁」，不阻塞其它键；但仍避免持全局锁做 IO）
            var produced = factory();
            Set(serverId, key, produced, ttl);
            return produced;
        }
    }

    /// <summary>同一 (serverId,key) 的单飞锁（供调用方自定义异步工厂时复用）。</summary>
    public object LockFor(string serverId, string key)
        => _locks.GetOrAdd(Composite(ScopeOf(serverId), key), _ => new object());

    /// <summary>删除单个键（存在才返回 true）。</summary>
    public bool Remove(string serverId, string key)
    {
        var scope = ScopeOf(serverId);
        bool removed;
        lock (_gate)
        {
            removed = _values.Remove(Composite(scope, key));
        }
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    /// <summary>失效某台服务器的全部缓存（换服/重新登录时调用）。</summary>
    public int Invalidate(string serverId)
    {
        var scope = ScopeOf(serverId);
        var prefix = scope + "\u0001";
        var removed = 0;
        lock (_gate)
        {
            var keys = new List<string>();
            foreach (var key in _values.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal)) keys.Add(key);
            }
            foreach (var key in keys)
            {
                if (_values.Remove(key)) removed++;
            }
        }
        if (removed > 0) Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    /// <summary>失效某台服务器上的单个键。</summary>
    public bool Invalidate(string serverId, string key) => Remove(serverId, key);

    /// <summary>清空全部（退出登录 / 全量刷新）。</summary>
    public void Clear()
    {
        var had = false;
        lock (_gate)
        {
            had = _values.Count > 0;
            _values.Clear();
        }
        _locks.Clear();
        if (had) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>当前未过期的分组键列表（诊断/设置页展示用）。</summary>
    public List<string> Scopes
    {
        get
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            lock (_gate)
            {
                foreach (var kv in _values)
                {
                    if (kv.Value.IsExpired) continue;
                    var index = kv.Key.IndexOf('\u0001');
                    set.Add(index < 0 ? kv.Key : kv.Key.Substring(0, index));
                }
            }
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }

    /// <summary>未过期条目数（<paramref name="serverId"/> 为 null 时统计全部）。</summary>
    public int Count(string serverId = null)
    {
        var scope = string.IsNullOrEmpty(serverId) ? null : ScopeOf(serverId);
        var prefix = scope == null ? null : scope + "\u0001";
        var count = 0;
        lock (_gate)
        {
            foreach (var kv in _values)
            {
                if (kv.Value.IsExpired) continue;
                if (prefix != null && !kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                count++;
            }
        }
        return count;
    }

    /// <summary>取某台服务器的全部条目副本（诊断用）。</summary>
    public Dictionary<string, T> Items(string serverId)
    {
        var scope = ScopeOf(serverId);
        var prefix = scope + "\u0001";
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var kv in _values)
            {
                if (kv.Value.IsExpired) continue;
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                result[kv.Key.Substring(prefix.Length)] = kv.Value.Value;
            }
        }
        return result;
    }

    private static string Composite(string scope, string key) => scope + "\u0001" + (key ?? string.Empty);

    /// <summary>容量控制：先清过期，再按过期时间升序淘汰到上限。</summary>
    private void TrimLocked()
    {
        foreach (var key in new List<string>(_values.Keys))
        {
            if (_values[key].IsExpired) _values.Remove(key);
        }

        var limit = MaxEntries;
        if (limit <= 0 || _values.Count <= limit) return;

        var ordered = new List<KeyValuePair<string, Entry>>(_values);
        ordered.Sort((a, b) => a.Value.ExpiresAtUtc.CompareTo(b.Value.ExpiresAtUtc));
        var overflow = _values.Count - limit;
        for (var i = 0; i < overflow && i < ordered.Count; i++)
        {
            _values.Remove(ordered[i].Key);
        }
    }
}

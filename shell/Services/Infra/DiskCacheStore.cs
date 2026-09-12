// S5 体验设施（t33）：通用**磁盘缓存** —— key → 单文件，LRU 淘汰，统计与清理。
// 设计约束：① 键不直接做文件名（用户 URL/弹幕名含非法字符）⇒ 用 SHA1 十六进制做名；
//          ② 写入走"临时文件 + 覆盖移动"⇒ 半截文件不会被后续读取当成命中；
//          ③ 命中时**触碰 mtime** ⇒ LRU 语义（最近用过的不会被淘汰）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>某个缓存目录的一次统计快照（条数 / 字节 / 最老与最新条目时刻）。</summary>
public sealed class CacheStats
{
    public string Directory { get; set; }

    public int Count { get; set; }

    public long Bytes { get; set; }

    public DateTime? OldestUtc { get; set; }

    public DateTime? NewestUtc { get; set; }

    public override string ToString()
        => $"{(string.IsNullOrEmpty(Directory) ? "cache" : Path.GetFileName(Directory))}: {Count} 条 / {Bytes} B";
}

/// <summary>
/// 通用磁盘缓存。上限策略 = **条数上限 + 字节上限双闸**（任一超出即按 LRU 淘汰到两条都在限内）。
/// </summary>
public sealed class DiskCacheStore
{
    private readonly Action<string> _log;

    public DiskCacheStore(string directory, long maxBytes = Constants.AppConstants.DiskCacheDefaultMaxBytes, int maxEntries = Constants.AppConstants.DiskCacheDefaultMaxEntries, Action<string> log = null)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        if (maxBytes <= 0) maxBytes = Constants.AppConstants.DiskCacheDefaultMaxBytes;
        if (maxEntries <= 0) maxEntries = Constants.AppConstants.DiskCacheDefaultMaxEntries;
        MaxBytes = maxBytes;
        MaxEntries = maxEntries;
        _log = log;
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Directory { get; }

    public long MaxBytes { get; }

    public int MaxEntries { get; }

    /// <summary>键 → 文件名（SHA1 十六进制 + <c>.cache</c>；稳定、与键一一对应、无非法字符）。</summary>
    public string PathFor(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("缓存键不能为空", nameof(key));
        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = SHA1.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2 + 6);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        sb.Append(".cache");
        return Path.Combine(Directory, sb.ToString());
    }

    public bool Contains(string key) => File.Exists(PathFor(key));

    /// <summary>读；命中 ⇒ 触碰 mtime（LRU）并记 <c>CACHE-HIT</c>，未命中 ⇒ 记 <c>CACHE-MISS</c> 并返回 <c>null</c>。</summary>
    public byte[] TryRead(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            _log?.Invoke($"CACHE-MISS {Path.GetFileName(path)}");
            return null;
        }
        try
        {
            var data = File.ReadAllBytes(path);
            try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
            catch (IOException)   // 有意忽略：触碰 mtime 失败不影响命中判定（仅影响 LRU 顺序）
            {
            }
            _log?.Invoke($"CACHE-HIT {Path.GetFileName(path)} bytes={data.Length}");
            return data;
        }
        catch (IOException ex)
        {
            _log?.Invoke($"CACHE-READ-ERROR {Path.GetFileName(path)} {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// 写（覆盖）。t204 起**本类自己实现** "进程唯一 tmp + 原子改名"：
    /// 旧实现走 <see cref="AtomicFile.WriteAllBytes"/>，其临时名是**确定性**的 <c>&lt;sha1&gt;.cache.tmp</c>
    /// ⇒ 两个进程/两个实例写同一个 URL 时**撞锁**（实测真根日志三条 <c>IOException: The process cannot access the file
    /// '…&lt;sha1&gt;.cache.tmp' because it is being used by another process</c>，2026-09-12 18:28:43.890 / 18:28:46.088 / 18:29:31.928），
    /// 该图当次**写不进缓存**（读侧下次仍 miss）⇒ 用户观感"缓存没生效"。
    /// </summary>
    public void Write(string key, byte[] data)
    {
        if (data == null) return;
        var path = PathFor(key);
        WriteAtomic(path, data);
        _log?.Invoke($"CACHE-STORE {Path.GetFileName(path)} bytes={data.Length}");

        // t297：**上限在日常写路径上生效** —— 阈值触发（近似值越界）或每 N 次写兜底做一次真扫描 + LRU 淘汰。
        _approxBytes += data.Length;
        _approxEntries++;
        _writesSinceCheck++;
        if (_approxBytes > MaxBytes || _approxEntries > MaxEntries || _writesSinceCheck >= EnforceCheckEveryWrites)
        {
            _writesSinceCheck = 0;
            EnforceCaps("write-path");
        }
    }

    /// <summary>
    /// 原子写：**进程唯一**临时名（<c>&lt;sha1&gt;.cache.&lt;pid&gt;-&lt;guid&gt;.tmp</c>，同目录 ⇒ 同卷改名是原子的）
    /// ⇒ 多进程/多实例并写同一项**不再撞锁**；写完 <see cref="File.Move(string,string,bool)"/> 覆盖改名到正式条目
    /// ⇒ 读侧永远只看到完整内容（半截永远停在 tmp 名下，而 tmp 不计入命中/统计/淘汰）。
    /// <para>失败语义：**写 tmp 失败** ⇒ 删掉可能的半截 tmp（不留垃圾）；**改名失败** ⇒ **保留 tmp**（不丢数据）
    /// 并落一行可读日志 <c>CACHE-STORE-RENAME-FAIL</c>；两种 tmp 形态都被 <see cref="IsTmpName"/> 认作本缓存的临时态
    /// ⇒ 后续 <see cref="Clear"/> 可回收，不会变成永久垃圾。</para>
    /// </summary>
    /// <summary>t206：改名重试次数与线性退避（毫秒）。Windows 覆盖改名要求目标不被他人占用 ⇒ 并写时会瞬时失败，重试即可吃掉绝大多数。</summary>
    private const int RenameRetryCount = 5;

    /// <summary>第 n 次重试前的退避 = <c>n × 本值</c>（线性），最大 5×15=75 ms。</summary>
    private const int RenameRetryDelayMs = 15;

    /// <summary>t206 机会式清扫阈值（秒）：本类 tmp 的 mtime 早于它 ⇒ 视为上次中断的遗留，可回收（垃圾有界）。</summary>
    private const int StaleTmpSeconds = 60;

    private int _renameRetryHits;
    private int _renameRetryFailures;

    /// <summary>改名重试命中累计（t206 读数：重试命中数）。</summary>
    public int RenameRetryHits => _renameRetryHits;

    /// <summary>重试全败累计（t206 读数：重试全败数 ⇒ 保留 tmp 的那些次）。</summary>
    public int RenameRetryFailures => _renameRetryFailures;

    private void WriteAtomic(string path, byte[] data)
    {
        SweepStaleTmp();
        var tmp = TempPathFor(path);
        try
        {
            File.WriteAllBytes(tmp, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(tmp);
            _log?.Invoke($"CACHE-STORE-WRITE-FAIL {Path.GetFileName(path)} {ex.GetType().Name}: {ex.Message}");
            return;
        }

        for (var attempt = 1; attempt <= RenameRetryCount; attempt++)
        {
            try
            {
                File.Move(tmp, path, overwrite: true);
                if (attempt > 1)
                {
                    _renameRetryHits++;
                    _log?.Invoke($"CACHE-STORE-RENAME-RETRY hit attempt={attempt} retryHits={_renameRetryHits} retryFailures={_renameRetryFailures} {Path.GetFileName(path)}");
                }
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < RenameRetryCount)
                {
                    // 小退避（线性）：给占用方一点时间释放；只在**重试全败**时才保留 tmp（不丢数据）。
                    System.Threading.Thread.Sleep(RenameRetryDelayMs * attempt);
                    continue;
                }

                _renameRetryFailures++;
                _log?.Invoke($"CACHE-STORE-RENAME-FAIL {Path.GetFileName(path)} keep={Path.GetFileName(tmp)} attempt={attempt} retryFailures={_renameRetryFailures} {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// t206 机会式清扫：删掉本类 tmp 里 **mtime 早于 <see cref="StaleTmpSeconds"/>** 的件 ⇒ 即使"重试全败"偶尔留下 tmp，
    /// 垃圾也**有界**（不会像上一轮那样累积到 1500+ 枚）。在途写入的 tmp 一定新鲜 ⇒ 不会被误删；失败不抛。
    /// </summary>
    private void SweepStaleTmp()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-StaleTmpSeconds);
            foreach (var file in new DirectoryInfo(Directory).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (!IsTmpName(file.Name)) continue;
                if (file.LastWriteTimeUtc >= cutoff) continue;
                TryDelete(file.FullName);
            }
        }
        catch (IOException) { /* 有意忽略：清扫 best-effort */ }
        catch (UnauthorizedAccessException) { /* 有意忽略：同上 */ }
    }

    /// <summary>进程唯一临时名：<c>&lt;sha1&gt;.cache.&lt;pid&gt;-&lt;guid&gt;.tmp</c>（同目录；pid+guid ⇒ 跨进程/跨写入都唯一）。</summary>
    private static string TempPathFor(string entryPath)
    {
        var token = Environment.ProcessId.ToString() + "-" + Guid.NewGuid().ToString("n");
        var unique = entryPath.Substring(0, entryPath.Length - EntrySuffix.Length) + TempUniqueInfix + token + ".tmp";
        return unique;
    }

    /// <summary>尽力删除（失败不抛：清理是 best-effort，不得因清理失败掩盖主流程）。</summary>
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* 有意忽略：tmp 清理 best-effort */ }
        catch (UnauthorizedAccessException) { /* 有意忽略：同上 */ }
    }

    public bool Remove(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public CacheStats Stats()
    {
        var files = EnumerateEntries();
        var stats = new CacheStats { Directory = Directory, Count = files.Count, Bytes = files.Sum(f => f.Length) };
        if (files.Count > 0)
        {
            stats.OldestUtc = files.Min(f => f.LastWriteTimeUtc);
            stats.NewestUtc = files.Max(f => f.LastWriteTimeUtc);
        }
        return stats;
    }

    /// <summary>
    /// 按上限淘汰（LRU：最老的先删）。返回删除条数。
    /// <para>t297：与**写路径**共用同一实现（<see cref="EnforceCaps"/>）⇒ 手工清理路径（<c>CacheStatsService</c>）返回语义不变，
    /// 仅多一行 <c>CACHE-EVICT-SUMMARY … reason=manual-prune</c> 汇总；逐条 <c>CACHE-EVICT</c> 行保持逐字不变。</para>
    /// </summary>
    public int Prune() => EnforceCaps("manual-prune");

    // ── t297：容量上限在**日常写路径**上真正生效 ─────────────────────────────
    //
    // 修前事实（t297 复采）：`Prune()` 全仓只有 `CacheStatsService`（"清理缓存"）在调 ⇒ 上限**日常不生效**，
    // 缓存可以无限长（真根实测 1,469 条 / 67.05 MB；cap = 20,000 条 / 512 MiB ⇒ 未超，但机制确实不存在）。
    //
    // 机制（**阈值触发 + 周期兜底**，稳态零额外开销）：
    //   · 本实例维护"自上次全扫描以来写入"的近似字节/条数（`_approxBytes` / `_approxEntries`）；
    //   · 每次成功写入后，只要**近似值越过 MaxBytes/MaxEntries** ⇒ 做一次真扫描并按 LRU 淘汰到上限内；
    //   · 另设**兜底**：每 `EnforceCheckEveryWrites` 次写做一次真扫描（覆盖其他进程增删造成的估算漂移）。
    /// <summary>写路径触发阈值：每 N 次写入做一次真扫描（兜底）。</summary>
    private const int EnforceCheckEveryWrites = 64;

    private long _approxBytes;
    private int _approxEntries;
    private int _writesSinceCheck;
    private long _evictions;
    private long _evictedBytes;

    /// <summary>t297 读数：**累计淘汰条数**（&gt;0 即"上限在日常路径上真的触发过"）。</summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>t297 读数：累计淘汰字节。</summary>
    public long EvictedBytes => Interlocked.Read(ref _evictedBytes);

    /// <summary>
    /// t297：**在日常写路径上执行容量上限** —— 超出 <see cref="MaxEntries"/>/<see cref="MaxBytes"/> 时按 LRU
    /// （最老 mtime 先删）淘汰到上限内；返回删除条数。被写路径与 <see cref="Prune"/> 共用。
    /// <para>代价与边界（写清，免得被当成恒真）：每次调用做**一次目录全扫描**，但只在**触发时**调用 ⇒ 稳态不扫描；
    /// 多进程写同一目录时本实例的估算可能偏低，由"每 N 次写兜底"与下一次触发纠正；多进程同时淘汰会重复删同一文件（已忽略）。
    /// 淘汰**失败**（被占用/无权限）只跳过该条、不阻断整体 —— 与 <see cref="Prune"/> 同语义。</para>
    /// </summary>
    private int EnforceCaps(string reason)
    {
        var files = EnumerateEntries();
        var total = files.Sum(f => f.Length);
        var beforeCount = files.Count;
        var beforeBytes = total;

        var removed = 0;
        var freed = 0L;
        if (beforeCount > MaxEntries || total > MaxBytes)
        {
            foreach (var f in files.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (beforeCount - removed <= MaxEntries && total <= MaxBytes) break;
                try
                {
                    var len = f.Length;
                    File.Delete(f.FullName);
                    removed++;
                    freed += len;
                    total -= len;
                    // 逐条行：手工清理路径（manual-prune）**逐字不变**；写路径新增 reason= 便于归因。
                    _log?.Invoke(reason == "manual-prune"
                        ? $"CACHE-EVICT {f.Name} bytes={len}"
                        : $"CACHE-EVICT {f.Name} bytes={len} reason={reason}");
                }
                catch (IOException)
                {
                    // 有意忽略：被占用/权限不足 ⇒ 跳过该条，不阻断整体淘汰（与 Prune 原语义一致）。
                }
            }
        }

        _approxBytes = total;
        _approxEntries = beforeCount - removed;
        if (removed > 0)
        {
            Interlocked.Add(ref _evictions, removed);
            Interlocked.Add(ref _evictedBytes, freed);
            _log?.Invoke($"CACHE-EVICT-SUMMARY reason={reason} evicted={removed} entries={beforeCount}->{beforeCount - removed}"
                + $" bytesTotal={beforeBytes}->{total} cap={MaxEntries}/{MaxBytes} evictionsTotal={Evictions} evictedBytesTotal={EvictedBytes}");
        }

        return removed;
    }

    /// <summary>
    /// 清空**本缓存的全部条目**（正式条目 <c>&lt;40hex&gt;.cache</c> + 中断写入遗留的 <c>&lt;40hex&gt;.cache.tmp</c>），
    /// 返回删除条数，并**必打一条可见汇总日志** <c>CACHE-CLEAR items=N bytes=M dir=…</c>。
    /// </summary>
    /// <remarks>
    /// 🔴 t44 修缺陷：原实现只枚举 <c>*.tmp</c>（<c>EnumerateEntries(includeTmp: true)</c> 的过滤器是"只 tmp"）
    /// ⇒ <b>Clear() 删不掉任何 .cache 条目</b>（实测返回 0、快照仍在）。该缺陷已在 t44 改为"两类都删"。
    /// 🔴 t68 修另两处（由"清缓存静默"清单延续出来，实测发现）：
    ///   ① **误删无关文件**：旧过滤器按**扩展名**放行 ⇒ 同目录里的 `foreign.tmp`、`deadbeef.cache`（非本缓存命名）
    ///      会被一起删掉。现改为**按命名约定**（40 位小写 hex + 后缀）判定"属于本缓存"，其余一律不动。
    ///   ② **零可见性**：旧实现删完什么都不记 ⇒ 用户点"清理"看到 0 条会以为坏了（这正是 t43 把它列进静默失败清单的原因）。
    ///      现在无论删了几条（含 0 条）都打汇总行 —— "清了 0 条"本身也是有效读数。
    /// </remarks>
    public int Clear()
    {
        var removed = 0;
        var freed = 0L;
        foreach (var f in EnumerateEntries(includeTmp: true))
        {
            try
            {
                var len = f.Length;
                File.Delete(f.FullName);
                removed++;
                freed += len;
            }
            catch (IOException ex)
            {
                // 记录 + 降级：被占用/权限不足 ⇒ 跳过该条并继续清理其余条目（不阻断整体）。
                _log?.Invoke($"CACHE-CLEAR-SKIP {f.Name} {ex.GetType().Name}");
            }
            catch (UnauthorizedAccessException ex)
            {
                // 同上（只读/ACL 拒绝）：可见跳过，不吞掉整轮清理。
                _log?.Invoke($"CACHE-CLEAR-SKIP {f.Name} {ex.GetType().Name}");
            }
        }

        _log?.Invoke($"CACHE-CLEAR items={removed} bytes={freed} dir={Directory}");
        return removed;
    }

    /// <summary>正式条目后缀（键的 SHA1 十六进制 + 此后缀）。</summary>
    private const string EntrySuffix = ".cache";

    /// <summary>写入中的临时态后缀（<c>&lt;40hex&gt;.cache.tmp</c>）；半截文件不被当成命中。</summary>
    private const string TempSuffix = ".cache.tmp";

    /// <summary>t204：进程唯一临时名的中缀 —— <c>&lt;40hex&gt;.cache.&lt;token&gt;.tmp</c>（token = <c>pid-guid</c>）。</summary>
    private const string TempUniqueInfix = ".cache.";

    /// <summary>40 位小写 hex 前缀（正式条目与两种临时态共用的"属于本缓存"判据）。</summary>
    private static bool IsHex40Prefix(string name)
    {
        if (name == null || name.Length < 40) return false;
        for (var i = 0; i < 40; i++)
        {
            var c = name[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }

    /// <summary>正式条目名：<c>&lt;40hex&gt;.cache</c>。</summary>
    private static bool IsEntryName(string name)
        => name != null && name.Length == 40 + EntrySuffix.Length && IsHex40Prefix(name)
           && name.EndsWith(EntrySuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 临时态名（**两种形态都认**）：旧 <c>&lt;40hex&gt;.cache.tmp</c>（历史遗留）与新
    /// <c>&lt;40hex&gt;.cache.&lt;pid-guid&gt;.tmp</c>（t204 起）；两者都**不计入**统计/淘汰，且可被 <see cref="Clear"/> 回收。
    /// </summary>
    private static bool IsTmpName(string name)
        => name != null && name.Length > 40 + EntrySuffix.Length
           && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
           && IsHex40Prefix(name)
           && name.Substring(40).StartsWith(EntrySuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 该文件名是否**属于本缓存**：40 位小写 hex + <c>.cache</c> 或 + <c>.cache.tmp</c>。
    /// 这是"哪些文件归我们管"的**唯一判据**（清空/统计/淘汰都走它）⇒ 同目录里的无关文件永不被删。
    /// </summary>
    private static bool IsCacheFileName(string name)
    {
        return IsEntryName(name) || IsTmpName(name);
    }

    /// <summary>
    /// 枚举**属于本缓存**的文件。参数语义 = **加性**：<paramref name="includeTmp"/>=<c>true</c> ⇒ 正式条目 + 临时态；
    /// <c>false</c> ⇒ 仅正式条目（<see cref="Stats"/>/<see cref="Prune"/> 只看正式条目，临时态不计入统计与淘汰）。
    /// </summary>
    private List<FileInfo> EnumerateEntries(bool includeTmp = false)
    {
        if (!System.IO.Directory.Exists(Directory)) return new List<FileInfo>();
        return new DirectoryInfo(Directory)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(f => IsCacheFileName(f.Name)
                && (includeTmp || !IsTmpName(f.Name)))
            .ToList();
    }
}

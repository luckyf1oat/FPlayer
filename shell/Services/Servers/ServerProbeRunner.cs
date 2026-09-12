// t293：**启动期服务器状态检测**的并发化落点（服务层）。
//
// 背景（用户第 1 条：「提高启动应用时的服务器状态检测速度 并发检测？」）：
//   App 侧当刻逐字循环（`MainWindow.ProbeAllAsync`，shell/App/MainWindow.xaml.cs:203-220）：
//     foreach (row in rows) { using cts = 8s; new EmbyService(new ShellHttpClient(), row.Server); await emby.GetViewsAsync(cts.Token); … }
//   ⇒ **逐台串行**：16 台里只要有一台 8 s 超时，整体墙钟就被那一台拖满。
//
// 本文件的职责（**只做服务层**，不改 App）：把"同一串行语义"改成**同一批并发**，并**逐台**产出结果：
//   · 逐台结果对象携带 `Name / Ok / ViewCount / Detail / ElapsedMs / StartedAtMs / FinishedAtMs`；
//   · `Detail` 与 App 当刻文案**逐字一致**（成功 = `连通 · N 库`；失败 = `连接失败：<异常类型名>`）⇒ 失败台**逐台可见**，不合并/不吞；
//   · 每台各自的超时（`timeout`）用**每任务独立 CTS** ⇒ 单台慢/超时**不拖累**其余台（其余台在各自完成时即出结果，经 `onResult` 立即可读）；
//   · 并发上限 `maxConcurrency`（默认见 <see cref="DefaultMaxConcurrency"/>），用 `SemaphoreSlim` 闸住同时在飞的台数。
//
// 未做/边界：**App 的调用点替换不在本卡 inScope**（`shell/App/` 出界）⇒ 在 App 换用本 API 之前，启动路径仍是串行。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Servers;

/// <summary>单台服务器的状态检测结果（t293；字段口径与 App 侧 `ServerRow.SetStatus(name, ok, detail)` 对齐）。</summary>
public sealed class ServerProbeResult
{
    /// <summary>被探测的服务器配置（原样回带，便于调用方按 `Id` 回填行）。</summary>
    public ServerConfig Server { get; init; }

    public string ServerId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>输入批次里的序号（0 起）——**结果按它排序回传**，与 App 的逐行顺序一致。</summary>
    public int Index { get; init; }

    /// <summary>是否连通（= `GetViewsAsync` 未抛）。</summary>
    public bool Ok { get; init; }

    /// <summary>连通的库数量（`Ok=false` 时为 0）。</summary>
    public int ViewCount { get; init; }

    /// <summary>与 App 当刻**逐字相同**的文案：成功 `连通 · N 库`；失败 `连接失败：&lt;异常类型名&gt;`。</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>本台耗时（毫秒）。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>相对**批次起点**的开始/结束时刻（毫秒）——用于判定串行/并发与重叠关系。</summary>
    public int StartedAtMs { get; init; }

    public int FinishedAtMs { get; init; }
}

/// <summary>
/// 启动期服务器状态检测的**并发执行器**（t293）。语义 = App 串行循环的逐台结果，只是同批并发 + 每台独立超时。
/// </summary>
public static class ServerProbeRunner
{
    /// <summary>
    /// 默认同时在飞上限（**经验值、可调**）：16 台实测下 4 路已把总墙钟从 Σ(台) 压到 ~ceil(N/4)×单台（且被最慢那台的超时封顶）；
    /// 再高会放大"首屏一起打"的网络峰值与假死面、收益递减 ⇒ **未做 2/4/8 对照实验**，要更激进的提速应按实测再调。
    /// </summary>
    public const int DefaultMaxConcurrency = 4;

    /// <summary>
    /// 并发探测全部服务器；**结果按输入顺序返回**（`Index` 升序），逐台经 <paramref name="onResult"/> 立即可读。
    /// </summary>
    /// <param name="servers">服务器列表（调用方自己的顺序，通常是 `SortIndex`）。</param>
    /// <param name="timeout">**每台**的超时（与 App 当刻的 8 s 一致）。</param>
    /// <param name="maxConcurrency">同时在飞上限；≤0 视作 1。</param>
    /// <param name="onResult">每台**一有结果就回调**（不等到全批结束）——UI 可逐行点亮/置红。</param>
    /// <param name="onStart">每台**真正开始**时回调（用于测"同时在飞数"）。</param>
    /// <param name="onFinish">每台结束时回调（与 <paramref name="onResult"/> 同刻，先释放闸门后回调）。</param>
    /// <param name="onLog">审计行（每台一行 `SERVER-PROBE …`）。</param>
    public static async Task<IReadOnlyList<ServerProbeResult>> ProbeAllAsync(
        IEnumerable<ServerConfig> servers,
        TimeSpan timeout,
        int maxConcurrency = DefaultMaxConcurrency,
        Action<ServerProbeResult> onResult = null,
        Action<ServerConfig> onStart = null,
        Action<ServerProbeResult> onFinish = null,
        Action<string> onLog = null,
        CancellationToken cancellationToken = default)
    {
        var list = (servers ?? Enumerable.Empty<ServerConfig>()).Where(s => s != null).ToList();
        if (list.Count == 0)
        {
            return new List<ServerProbeResult>();
        }

        var cap = maxConcurrency <= 0 ? 1 : maxConcurrency;
        if (cap > list.Count)
        {
            cap = list.Count;
        }

        onLog?.Invoke($"SERVER-PROBE batch start servers={list.Count} cap={cap} timeoutMs={(long)timeout.TotalMilliseconds}");
        var batch = Stopwatch.StartNew();
        using var gate = new SemaphoreSlim(cap, cap);
        var callbackGate = new object();
        var results = new ServerProbeResult[list.Count];

        var tasks = new List<Task>(list.Count);
        for (var index = 0; index < list.Count; index++)
        {
            var i = index;
            var server = list[i];
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                var startedAt = (int)batch.ElapsedMilliseconds;
                onStart?.Invoke(server);
                var sw = Stopwatch.StartNew();
                bool ok;
                int viewCount = 0;
                string detail;
                try
                {
                    // 每台独立超时（与 App 同一 8 s 语义）：一台慢不会把别的台一起等到超时。
                    using var perServer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    perServer.CancelAfter(timeout);
                    var emby = new EmbyService(new ShellHttpClient(), server);
                    var views = await emby.GetViewsAsync(perServer.Token).ConfigureAwait(false);
                    ok = true;
                    viewCount = views?.Count ?? 0;
                    detail = "连通 · " + viewCount + " 库";
                }
                catch (Exception ex)
                {
                    // 失败**逐台**留痕：类型名进 Detail，整条进审计行 —— 不合并、不吞。
                    ok = false;
                    detail = "连接失败：" + ex.GetType().Name;
                }
                sw.Stop();
                var result = new ServerProbeResult
                {
                    Server = server,
                    ServerId = server.Id,
                    Name = server.Name,
                    Index = i,
                    Ok = ok,
                    ViewCount = viewCount,
                    Detail = detail,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    StartedAtMs = startedAt,
                    FinishedAtMs = (int)batch.ElapsedMilliseconds,
                };
                results[i] = result;
                lock (callbackGate)
                {
                    onLog?.Invoke($"SERVER-PROBE name={result.Name} ok={result.Ok} elapsed={result.ElapsedMs}ms detail={result.Detail}");
                    onResult?.Invoke(result);
                    onFinish?.Invoke(result);
                }
                gate.Release();
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        batch.Stop();
        onLog?.Invoke($"SERVER-PROBE batch done servers={list.Count} totalMs={batch.ElapsedMilliseconds} fail={results.Count(r => r != null && !r.Ok)}");
        return results.Where(r => r != null).OrderBy(r => r.Index).ToList();
    }
}

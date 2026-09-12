// 等价移植：原版 `src/features/settings/application/webdav_auto_backup_scheduler.dart`
// （DESIGN §4.1 #145 → Services/WebDav/WebDavAutoBackupScheduler.cs）。
// 重建版把该调度语义合并进了 rebuild/ai_player/lib/core/services/webdav_backup_service.dart:183-204
// （`schedule` / `cancelSchedule` / `isScheduled`），本文件按原类名拆出，语义逐行对齐：
//   - `schedule(davProvider, interval, keepCount, directory)`：先取消旧定时器，再按固定间隔周期触发；
//     每轮 = 备份一次 + 滚动清理一次。
//   - `cancelSchedule()`：停止并释放定时器；`isScheduled` = 定时器存在（等价 Dart `_timer?.isActive ?? false`）。
//   - 间隔 <c>&lt;= 0</c> 回落 24 小时（等价 app_state.dart:172 `autoBackupIntervalHours &lt;= 0 ? 24`）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §4（PROPFIND / MKCOL / PUT / DELETE，`/backup_<日期>`）。
// 实现约束：仅用 System.Threading.Timer，不引入第三方调度库；上一轮未结束时跳过本轮（避免堆积）。

using System;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Infra;

namespace AIPlayer.Shell.Services.WebDav;

/// <summary>WebDAV 自动备份调度器（对应原版 <c>WebDavAutoBackupScheduler</c>）。</summary>
public sealed class WebDavAutoBackupScheduler : IDisposable
{
    /// <summary>Timer 周期上限（.NET Timer 受 uint 毫秒上限约束；超出则夹取）。</summary>
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromMilliseconds(4294967294d);

    private readonly WebDavAutoBackupService _service;
    private readonly Action<string> _onLog;

    private Timer _timer;
    private Func<WebDavService> _davProvider;
    private string _directory = "/backup";
    private int _keepCount = 7;
    private int _running;
    private bool _disposed;

    public WebDavAutoBackupScheduler(WebDavAutoBackupService service, Action<string> onLog = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _onLog = onLog;
    }

    /// <summary>是否已排定（等价 Dart <c>isScheduled</c>）。</summary>
    public bool IsScheduled => _timer != null;

    /// <summary>当前间隔（未排定时为 <c>null</c>，便于调用方判断是否需要重排）。</summary>
    public TimeSpan? Interval { get; private set; }

    /// <summary>
    /// 排定自动备份（对应 Dart <c>schedule</c>）：重复调用会先取消旧定时器再按新参数重排。
    /// <paramref name="davProvider"/> 每轮调用一次以取当前 WebDAV 客户端（Dart 同样是「每次新建实例」语义）。
    /// </summary>
    public void Schedule(
        Func<WebDavService> davProvider,
        TimeSpan interval,
        int keepCount = 7,
        string directory = "/backup")
    {
        if (davProvider == null) throw new ArgumentNullException(nameof(davProvider));
        if (_disposed) throw new ObjectDisposedException(nameof(WebDavAutoBackupScheduler));

        CancelSchedule();

        _davProvider = davProvider;
        _keepCount = keepCount;
        _directory = string.IsNullOrEmpty(directory) ? "/backup" : directory;

        // 等价 Dart：小时数 <= 0 时回落 24 小时
        if (interval <= TimeSpan.Zero) interval = TimeSpan.FromHours(24);
        if (interval > MaxPeriod) interval = MaxPeriod;

        Interval = interval;
        // dueTime = period：与 Dart `Timer.periodic` 一致，首次触发在一个间隔之后（不立即执行）
        _timer = new Timer(OnTick, null, interval, interval);
        Log($"已开启自动备份，间隔 {(long)interval.TotalHours} 小时");
    }

    /// <summary>停止并释放定时器（对应 Dart <c>cancelSchedule</c>）；未排定时为空操作。</summary>
    public void CancelSchedule()
    {
        var timer = _timer;
        _timer = null;
        Interval = null;
        if (timer == null) return;
        try
        {
            timer.Dispose();
        }
        catch (ObjectDisposedException) { /* 有意忽略：计时器已释放 => Dispose 幂等 */ }
    }

    /// <summary>
    /// 立即执行一轮（备份 + 滚动清理）。Dart 由定时器回调驱动；此处公开以便「启动即备份」或手动触发
    /// （未找到 WebDAV 服务器时只记日志并跳过，等价 app_state.dart:168-171 的告警路径）。
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var provider = _davProvider;
        var dav = provider == null ? null : provider();
        if (dav == null)
        {
            Log("自动备份已跳过：未找到 WebDAV 服务器");
            return;
        }

        await _service.BackupAsync(dav, _directory, cancellationToken).ConfigureAwait(false);
        await _service.PruneAsync(dav, _directory, _keepCount, cancellationToken).ConfigureAwait(false);
    }

    private void OnTick(object state)
    {
        // 定时器回调不能 await：转交 TickAsync，内部已全量 try/catch，不会产生未观察异常
        _ = TickAsync();
    }

    private async Task TickAsync()
    {
        // 上一轮尚未结束 ⇒ 跳过本轮，避免备份任务堆积
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        try
        {
            await RunOnceAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"自动备份失败：{ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void Log(string message)
    {
        _onLog?.Invoke(message);
        AppDataDir.Instance.Log("[webdav-backup] " + message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelSchedule();
    }
}

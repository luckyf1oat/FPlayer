using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;

namespace AIPlayer.Shell.Shell;

/// <summary>
/// 外壳状态中枢（t8 第 ③ 项）：全壳单一状态源。
///
/// 职责（只做三件，不夹带业务）：
///   1) 当前路由（<see cref="CurrentTag"/>）—— 导航的唯一真相，页面读它、MainWindow 写它；
///   2) 派生状态摘要（服务器台数 / 启用数、日志条数）—— 供状态栏与后续屏复用；
///   3) 请求式导航（<see cref="RequestNavigate"/>）—— 让页面**不必持有 Frame 引用**就能切页，
///      避免把 MainWindow 变成全局单例。
///
/// 与 shell/Services 的关系：服务器集经 <see cref="ServerConfigStore"/> 读同一份落盘配置
/// （凭据经 <see cref="SecureKvStore"/>，与服务器页同一条链路），不另建缓存 —— 否则两处会漂移。
/// 日志条数经 <see cref="PlayerLogService"/>（等价 Dart <c>PlayerLogService</c>）。
///
/// ⚠️ 全部成员都在 UI 线程上使用；<see cref="PropertyChanged"/> 的订阅方需自行回投 UI 线程
/// （服务层日志可能来自非 UI 线程，这条链路不保证线程亲和）。
/// </summary>
public sealed class ShellState : INotifyPropertyChanged
{
    public static ShellState Current { get; } = new ShellState();

    private string _currentTag = "library";
    private string _serverSummary = "服务器：（未读）";
    private string _logSummary = "日志：（未读）";

    private ShellState()
    {
        // 日志条数随写入实时更新（日志页自己订阅同一事件刷新列表）。
        try
        {
            PlayerLogService.Instance.PropertyChanged += (_, __) => UpdateLogSummary();
        }
        catch (Exception ex)
        {
            Program.Log("ShellState: log subscribe failed: " + ex.Message);
        }
        UpdateLogSummary();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>请求导航到某个导航标签（与 MainWindow 的 NavigationViewItem.Tag 同域）。</summary>
    public event EventHandler<string> NavigateRequested;

    /// <summary>当前路由标签：library / music / books / player / servers / logs / settings。</summary>
    public string CurrentTag
    {
        get => _currentTag;
        set => Set(ref _currentTag, value ?? string.Empty);
    }

    /// <summary>形如「服务器 2 台（启用 1）」。</summary>
    public string ServerSummary
    {
        get => _serverSummary;
        private set => Set(ref _serverSummary, value);
    }

    /// <summary>形如「日志 37 条」。</summary>
    public string LogSummary
    {
        get => _logSummary;
        private set => Set(ref _logSummary, value);
    }

    public void RequestNavigate(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            NavigateRequested?.Invoke(this, tag);
        }
    }

    /// <summary>
    /// 重算派生状态。读失败**不抛**：状态栏是常驻 UI，读不到就如实显示失败类型，
    /// 但绝不因此让外壳起不来（与 Program.Log 的容错口径一致）。
    /// </summary>
    public void Refresh()
    {
        try
        {
            // ⚠️ 必须用 ServerConfigStore.Instance（环境解析版）：显式 ServerConfigStore.At(...)
            //    **不触发**「读原版 accounts.json」的回退路径 ⇒ 会得到 0 台，
            //    而同一个进程里服务器页用 Instance 得到 16 台（2026-09-11 实测：状态栏 0 vs 页 16）。
            var store = ServerConfigStore.Instance;
            store.Load();

            var servers = store.Servers?.ToList() ?? new List<ServerConfig>();
            ServerSummary = "服务器 " + servers.Count + " 台（启用 " + servers.Count(s => s.Enabled) + "）";
        }
        catch (Exception ex)
        {
            ServerSummary = "服务器读取失败：" + ex.GetType().Name;
            Program.Log("ShellState.Refresh failed: " + ex);
        }

        UpdateLogSummary();
    }

    private void UpdateLogSummary()
    {
        try
        {
            LogSummary = "日志 " + PlayerLogService.Instance.Entries.Count + " 条";
        }
        catch (Exception ex)
        {
            LogSummary = "日志读取失败：" + ex.GetType().Name;
        }
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

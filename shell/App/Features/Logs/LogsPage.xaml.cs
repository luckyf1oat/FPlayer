using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell.Features.Logs;

/// <summary>
/// 日志页（DESIGN §5 屏清单的「日志」）。
/// 数据源 = <see cref="PlayerLogService"/>（等价 Dart <c>PlayerLogService</c>，内存环形缓冲 + 落盘 logs/aiplayer.log）。
/// 逐项依据：
///   - 内存条目 → <see cref="PlayerLogService.Entries"/>（时间倒序，最新在前），由服务层负责“最新在前”，本页不再排序；
///   - 落盘文件 → <see cref="AppDataDir.LogFile"/>（等价 Dart <c>AppPaths.log</c>）；
///   - 「清空」只清内存环形缓冲，**不动落盘文件**（服务层 Clear 的实现面即如此，UI 文案如实标注）。
/// </summary>
public sealed partial class LogsPage : Page
{
    private readonly ObservableCollection<LogRow> _rows = new ObservableCollection<LogRow>();
    private readonly PlayerLogService _log = PlayerLogService.Instance;

    /// <summary>构造期间 XAML 会先触发 SelectedIndex/TextChanged，此时字段尚未就绪，用就绪位挡住。</summary>
    private bool _ready;

    public LogsPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _rows;
        _ready = true;

        // 服务层是 ChangeNotifierBase（INotifyPropertyChanged）。日志可能来自非 UI 线程
        // （程序集解析等），故一律经 DispatcherQueue 回投 UI 线程再刷。
        _log.PropertyChanged += OnLogChanged;
        Unloaded += (_, __) => _log.PropertyChanged -= OnLogChanged;

        Refresh();
        Program.Log("LogsPage loaded: entries=" + _log.Entries.Count);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void OnLogChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DispatcherQueue is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(Refresh);
    }

    private void Refresh()
    {
        var all = _log.Entries;
        var level = CurrentLevel();
        var query = SearchBox.Text is null ? string.Empty : SearchBox.Text.Trim();

        IEnumerable<LogEntry> view = all;
        if (level != "all")
        {
            view = view.Where(x => string.Equals(x.Level, level, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Length > 0)
        {
            view = view.Where(x => x.Message != null
                && x.Message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        var shown = view.ToList();
        _rows.Clear();
        foreach (var entry in shown)
        {
            _rows.Add(LogRow.From(entry));
        }

        SummaryText.Text = shown.Count + " / " + all.Count + " 条（内存环形上限 " + PlayerLogService.MaxEntries
            + "）｜落盘：" + SafeLogFile()
            + "｜来源：外壳启动诊断 + 服务层（DebugLog）";
    }

    private string CurrentLevel()
    {
        if (LevelBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }
        return "all";
    }

    private static string SafeLogFile()
    {
        try
        {
            return AppDataDir.Instance.LogFile;
        }
        catch (Exception ex)
        {
            return "(路径不可用：" + ex.Message + ")";
        }
    }

    private void Level_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready)
        {
            Refresh();
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_ready)
        {
            Refresh();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        StatusText.Text = "已刷新（" + _rows.Count + " 条可见）。";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        // 复制当前**可见**行（已应用级别与关键字过滤），而不是全量 —— 与列表所见一致。
        var text = string.Join(Environment.NewLine, _rows.Select(r => r.TimeText + "  " + r.LevelText + "  " + r.Message));
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            StatusText.Text = "已复制 " + _rows.Count + " 条到剪贴板。";
        }
        catch (Exception ex)
        {
            StatusText.Text = "复制失败：" + ex.Message;
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var file = SafeLogFile();
        if (!File.Exists(file))
        {
            StatusText.Text = "日志文件尚不存在（服务层未写过任何一条）：" + file;
            return;
        }
        Shell(file);
    }

    private void OpenDir_Click(object sender, RoutedEventArgs e)
    {
        var file = SafeLogFile();
        var dir = Path.GetDirectoryName(file);
        if (dir is null || !Directory.Exists(dir))
        {
            StatusText.Text = "日志目录尚不存在：" + (dir ?? file);
            return;
        }
        Shell(dir);
    }

    private void Shell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            StatusText.Text = "已用系统默认程序打开：" + path;
        }
        catch (Exception ex)
        {
            StatusText.Text = "打开失败：" + ex.Message;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var before = _log.Entries.Count;
        _log.Clear();
        Program.Log("LogsPage: cleared in-memory log (" + before + " -> 0), file kept");
        StatusText.Text = "已清空内存日志 " + before + " 条；落盘文件保留（" + SafeLogFile() + "）。";
    }
}

/// <summary>列表行：DataTemplate 只绑普通属性，避免为「时间格式 / 级别配色」引入转换器与主题资源查找。</summary>
public sealed class LogRow
{
    public string TimeText { get; init; }

    public string LevelText { get; init; }

    public string Message { get; init; }

    public Brush LevelBrush { get; init; }

    public static LogRow From(LogEntry entry) => new LogRow
    {
        TimeText = entry.Time.ToString("HH:mm:ss"),
        LevelText = (entry.Level ?? "info").ToUpperInvariant(),
        Message = entry.Message ?? string.Empty,
        LevelBrush = BrushFor(entry.Level),
    };

    private static Brush BrushFor(string level)
    {
        // 显式色值，不走 ThemeResource 查找：{StaticResource}/{ThemeResource} 缺键会静默降级（Spike §9.6），
        // 而这三个键在 Application 级资源里未必存在。三色按 Fluent 语义取（信息=次要灰 / 警告=琥珀 / 错误=红）。
        switch ((level ?? "info").ToLowerInvariant())
        {
            case "error":
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 209, 52, 56));
            case "warn":
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 199, 119, 0));
            default:
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 138, 138, 142));
        }
    }
}

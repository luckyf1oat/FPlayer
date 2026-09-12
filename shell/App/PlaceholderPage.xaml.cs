using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AIPlayer.Shell;

/// <summary>
/// 占位页：把导航标签映射成中文标题，用于证明「导航可达」。
/// 后续每个标签会被替换为 DESIGN §5 屏清单里的真实页面。
/// </summary>
public sealed partial class PlaceholderPage : Page
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["library"]  = "媒体库",
        ["music"]    = "音乐",
        ["books"]    = "有声书",
        ["player"]   = "播放",
        ["servers"]  = "服务器",
        ["logs"]     = "日志",
        ["settings"] = "设置",
    };

    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var tag = e.Parameter as string ?? "library";
        TitleText.Text = Titles.TryGetValue(tag, out var t) ? t : tag;
        Program.Log("PlaceholderPage -> " + tag);
    }
}

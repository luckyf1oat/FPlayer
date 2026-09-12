using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Servers;

/// <summary>
/// 服务器管理要用的几个小对话框（t30 / U-D）。
/// 抽出来是为了让 <see cref="ServerRowContextMenu"/> 只剩"逐项绑定"的语义，不被 XAML 噪声淹没。
/// 全部走 <see cref="ContentDialog"/> + 显式 <see cref="XamlRoot"/>（WinUI 3 非打包形态的硬要求）。
/// </summary>
internal static class ServerPrompts
{
    /// <summary>单行文本输入（修改备注 / 自定义图标 URL / rootPath 等）。取消返回 null。</summary>
    public static async Task<string> AskTextAsync(XamlRoot root, string title, string label, string initial, string placeholder = null)
    {
        var box = new TextBox
        {
            Header = label,
            Text = initial ?? string.Empty,
            PlaceholderText = placeholder ?? string.Empty,
            Width = 380,
            AcceptsReturn = false,
        };

        var dialog = new ContentDialog
        {
            RequestedTheme = ElementTheme.Dark,
            XamlRoot = root,
            Title = title,
            Content = box,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? (box.Text ?? string.Empty) : null;
    }

    /// <summary>口令输入（修改密码）。取消返回 null。</summary>
    public static async Task<string> AskPasswordAsync(XamlRoot root, string title, string label)
    {
        var box = new PasswordBox { Header = label, Width = 380 };
        var dialog = new ContentDialog
        {
            RequestedTheme = ElementTheme.Dark,
            XamlRoot = root,
            Title = title,
            Content = box,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? (box.Password ?? string.Empty) : null;
    }

    /// <summary>确认框（删除这种破坏性动作）。</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            RequestedTheme = ElementTheme.Dark,
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            PrimaryButtonText = primaryText,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>多选清单（媒体库：勾选**要显示**的库）。取消返回 null。</summary>
    public static async Task<HashSet<string>> AskMultiSelectAsync(
        XamlRoot root,
        string title,
        string subtitle,
        IReadOnlyList<(string Id, string Name)> items,
        IReadOnlyCollection<string> initiallyChecked)
    {
        var panel = new StackPanel { Spacing = 6, Width = 420 };
        if (!string.IsNullOrEmpty(subtitle))
        {
            panel.Children.Add(new TextBlock
            {
                Text = subtitle,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
            });
        }

        var boxes = new List<(string Id, CheckBox Box)>();
        foreach (var item in items)
        {
            var check = new CheckBox
            {
                Content = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                IsChecked = initiallyChecked.Contains(item.Id),
                Tag = item.Id,
            };
            boxes.Add((item.Id, check));
            panel.Children.Add(check);
        }

        var dialog = new ContentDialog
        {
            RequestedTheme = ElementTheme.Dark,
            XamlRoot = root,
            Title = title,
            Content = new ScrollViewer { Content = panel, MaxHeight = 420 },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return null; }

        return new HashSet<string>(boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Id), StringComparer.Ordinal);
    }

    /// <summary>单选清单（修改图标：内置图标 + 自定义）。返回选中的 key。
    /// <paramref name="options"/> 第三项 = **图标字体字形码位**（十六进制串，如 `E8B2`）；空串则只显示文字。
    /// 🔴 禁止 emoji 当图标（规格 §1.1）：字形一律走 `Segoe Fluent Icons`，尺寸/颜色取值与设置页一致。</summary>
    public static async Task<string> AskSingleSelectAsync(
        XamlRoot root,
        string title,
        string subtitle,
        IReadOnlyList<(string Key, string Label, string Glyph)> options,
        string initiallySelected)
    {
        var panel = new StackPanel { Spacing = 6, Width = 420 };
        if (!string.IsNullOrEmpty(subtitle))
        {
            panel.Children.Add(new TextBlock
            {
                Text = subtitle,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
            });
        }

        var radios = new List<(string Key, RadioButton Button)>();
        var group = "icon-choice";
        foreach (var option in options)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

            if (!string.IsNullOrEmpty(option.Glyph))
            {
                row.Children.Add(new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = char.ConvertFromUtf32(Convert.ToInt32(option.Glyph, 16)),
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            row.Children.Add(new TextBlock { Text = option.Label, VerticalAlignment = VerticalAlignment.Center });

            var radio = new RadioButton
            {
                Content = row,
                GroupName = group,
                IsChecked = string.Equals(option.Key, initiallySelected, StringComparison.Ordinal),
                Tag = option.Key,
            };
            radios.Add((option.Key, radio));
            panel.Children.Add(radio);
        }

        var dialog = new ContentDialog
        {
            RequestedTheme = ElementTheme.Dark,
            XamlRoot = root,
            Title = title,
            Content = new ScrollViewer { Content = panel, MaxHeight = 420 },
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return null; }

        var picked = radios.FirstOrDefault(r => r.Button.IsChecked == true);
        return picked.Button == null ? null : picked.Key;
    }
}

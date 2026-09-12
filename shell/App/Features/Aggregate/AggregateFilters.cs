// t31（U-E 收藏 + 聚合视界）：三个过滤器 + 按服务器分组的行模型。
//
// 规格依据（`HILLSLITE_UI_ANALYSIS.md` 行 3）：顶部三个过滤标签 **`▶ 继续播放` / `♡ 收藏` / `▤ 媒体库`**；
// 下面**按服务器分组**（server / ServerC / ServerB / ServerA…），每组一行海报。
//
// 「空源显示占位图不崩」（`UI_SPEC_SHELL.md` P4）的**两种空**，必须分开表达：
//   · 该源**取数失败** ⇒ 行内红字（`ServerFail`），不是"空"；
//   · 该源**取数成功但没条目** ⇒ 行内灰字"该服务器没有…"，**仍然保留该组**（用户要看得出"这台查过了"）。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>顶部的三个过滤器。</summary>
public sealed class AggregateFilter
{
    public AggregateFilter(AggregateKind kind, string label, string glyph)
    {
        Kind = kind;
        Label = label;
        Glyph = glyph;
    }

    public AggregateKind Kind { get; }

    /// <summary>`▶ 继续播放` / `♡ 收藏` / `▤ 媒体库`。</summary>
    public string Label { get; }

    public string Glyph { get; }

    /// <summary>三个过滤器的固定顺序（即参照物里的顺序）。</summary>
    public static List<AggregateFilter> All { get; } = new List<AggregateFilter>
    {
        new AggregateFilter(AggregateKind.ContinueWatching, "继续播放", "\u25B6"),
        new AggregateFilter(AggregateKind.Favorites, "收藏", "\u2661"),
        new AggregateFilter(AggregateKind.Library, "媒体库", "\u25A4"),
    };
}

/// <summary>聚合视界里的一行 = 一台服务器的一组海报。</summary>
public sealed class AggregateRow
{
    public AggregateRow(MediaGroupResult group, List<AggregatePoster> posters)
    {
        Group = group;
        Posters = posters == null
            ? new ObservableCollection<AggregatePoster>()
            : new ObservableCollection<AggregatePoster>(posters);
    }

    public MediaGroupResult Group { get; }

    /// <summary>本行海报（可变：取消收藏后要从所属分区里摘掉那一张）。</summary>
    public ObservableCollection<AggregatePoster> Posters { get; }

/// <summary>行标题 = `服务器名（ServerId 短号）` —— 同名服务器必须可区分（ 同纪律）。</summary>
    public string Header => Group.DisplayName;

    public string CountText => Posters.Count > 0 ? Posters.Count + " 项" : string.Empty;

    /// <summary>该源取数失败 ⇒ 红字（`ServerFail`）。</summary>
    public Visibility FailVisibility => Group.Failed ? Visibility.Visible : Visibility.Collapsed;

    public string FailText => Group.Failed ? "该源取数失败：" + Group.Error : string.Empty;

    /// <summary>取数成功但空 ⇒ 灰字提示（**保留该组**）。</summary>
    public Visibility EmptyVisibility => (!Group.Failed && Posters.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

    public string EmptyText => Group.Failed || Posters.Count > 0 ? string.Empty : "该服务器没有匹配的条目";

    public Visibility ItemsVisibility => Posters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>收藏屏的一行（收藏屏**不按服务器分组**：它是"我的收藏"一个整体列表，
/// 但每张卡右上角带**来源服务器角标** —— 跨服收藏必须看得出东西是哪来的）。</summary>
public sealed class FavoriteRow
{
    public FavoriteRow(MediaGroupResult group, List<AggregatePoster> posters)
    {
        Group = group;
        Posters = posters ?? new List<AggregatePoster>();
    }

    public MediaGroupResult Group { get; }

    public List<AggregatePoster> Posters { get; }

    public string Header => Group.DisplayName;

    public Visibility FailVisibility => Group.Failed ? Visibility.Visible : Visibility.Collapsed;

    public string FailText => Group.Failed ? "该源取数失败：" + Group.Error : string.Empty;
}

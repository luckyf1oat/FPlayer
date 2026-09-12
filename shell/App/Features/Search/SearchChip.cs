using System;
using System.Collections.Generic;
using System.Linq;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Search;

/// <summary>芯片的查询范围（UI_SPEC §7.5 的建模：单选，但内部按 scope + 类型集合建模，留升级口）。</summary>
public enum SearchScope
{
    /// <summary>跨全部启用服务器并发查询 → 去重 → 排序（§7.1「聚合搜索」芯片）。</summary>
    Aggregated,

    /// <summary>当前主源内按类型查询（§7.4 的其余芯片）。</summary>
    Typed,
}

/// <summary>
/// 搜索芯片（UI_SPEC §7.3 外观 / §7.4 类型映射）。
///
/// 纪律（§7.4 + 用户硬约束「能点的元素必须有真实绑定动作」）：
///   · <see cref="IsAvailable"/> = false 的芯片**仍然显示**（不静默省略）但置灰，
///     并带 <see cref="UnavailableReason"/> 供 tooltip —— 用户把"少项"当 bug。
///   · 只有规格**明确授权隐藏**的芯片才不画：「频道」（§7.4：无 Live TV 能力时隐藏此芯片，
///     不画死按钮）；隐藏原因写进日志，不写进 UI。
/// </summary>
public sealed class SearchChip
{
    public SearchChip(string label, SearchScope scope, string includeItemTypes = null)
    {
        Label = label;
        Scope = scope;
        IncludeItemTypes = includeItemTypes;
    }

    /// <summary>芯片文案（照抄参照物用词：聚合搜索 / 电影 / 剧 / 集 / 视频 / 合集 / 演职人员）。</summary>
    public string Label { get; }

    public SearchScope Scope { get; }

    /// <summary>Emby <c>IncludeItemTypes</c>；聚合搜索为 null（服务层按 8 类默认）。</summary>
    public string IncludeItemTypes { get; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>置灰原因（置灰时必须非空 —— 空原因 = 静默省略）。</summary>
    public string UnavailableReason { get; set; }

    public override string ToString() => Label;
}

/// <summary>芯片目录：按**主源能力**生成（§7.4 缺口 2：用现成的 <c>ServerKindExtensions</c> 判定，不新造能力表）。</summary>
public static class SearchChipCatalog
{
    /// <summary>规格授权「无能力时隐藏」的芯片 —— 只此一个（§7.4 频道）。</summary>
    public const string HiddenWhenUnsupported = "频道";

    public static List<SearchChip> Build(ServerKind kind, out string note)
    {
        var chips = new List<SearchChip>
        {
            new SearchChip("聚合搜索", SearchScope.Aggregated),
        };

        var notes = new List<string>();

        if (kind.IsEmbyFamily())
        {
            chips.Add(new SearchChip("电影", SearchScope.Typed, "Movie"));
            chips.Add(new SearchChip("剧", SearchScope.Typed, "Series"));
            chips.Add(new SearchChip("集", SearchScope.Typed, "Episode"));
            chips.Add(new SearchChip("视频", SearchScope.Typed, "Video"));
            chips.Add(new SearchChip("合集", SearchScope.Typed, "BoxSet"));
            chips.Add(new SearchChip("演职人员", SearchScope.Typed, "Person"));

            // 隐藏而非置灰：§7.4 明写「无该能力时隐藏此芯片（不画死按钮）」。
            // 判定 Live TV 能力需要服务层接口，当刻 EmbyService 无该 API（已核 API_SURFACE）⇒ 一律隐藏 + 记原因。
            notes.Add($"已隐藏芯片「{HiddenWhenUnsupported}」：服务层无 Live TV 能力判定接口（§7.4 授权隐藏）");
        }
        else if (kind.IsMusic())
        {
            chips.Add(new SearchChip("专辑", SearchScope.Typed, "MusicAlbum"));
            chips.Add(new SearchChip("歌曲", SearchScope.Typed, "Audio"));
            chips.Add(new SearchChip("歌手", SearchScope.Typed, "MusicArtist"));
            notes.Add("音乐源芯片置灰：Navidrome 搜索结果适配器不在 t28 范围（补屏 t32 接入）");
        }
        else if (kind.IsAudioBook())
        {
            chips.Add(new SearchChip("书库", SearchScope.Typed, "Book"));
            chips.Add(new SearchChip("有声书", SearchScope.Typed, "AudioBook"));
            notes.Add("有声书芯片置灰：ABS 搜索结果适配器不在 t28 范围（补屏 t32 接入）");
        }
        else
        {
            notes.Add($"主源类型 {kind} 无类型搜索能力（WebDAV 无搜索接口）");
        }

        // 非 Emby 系的类型芯片：聚合搜索仍可用（聚合只覆盖 Emby 系源，摘要行会写明），
        // 但"当前主源内按类型查"没有适配器 ⇒ 置灰 + 写明原因。
        if (!kind.IsEmbyFamily())
        {
            foreach (var c in chips.Where(c => c.Scope == SearchScope.Typed))
            {
                c.IsAvailable = false;
                c.UnavailableReason = $"当刻没有 {ServerKindExtensions.DisplayName(kind)} 的搜索结果适配器（t28 仅接 Emby 系）";
            }
        }

        note = string.Join("；", notes);
        return chips;
    }
}

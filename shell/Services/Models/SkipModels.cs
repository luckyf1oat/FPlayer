// 对应 DESIGN §4.2 `core/models/skip_models.dart` → `Services/Models/SkipModels.cs`。
// ⚠️ 三态标注：`rebuild/ai_player/lib/core/models/skip_models.dart` **在 rebuild 树中不存在**（原版文件），
// 故本文件属**重建实现**（依据 SERVICE_API §6.2 的片段语义 + DESIGN §4.1 #35/#28 的目标路径）。
// 与既有实现的分工：`Skip/SkipStore.cs`（用户可见片段集）、`Skip/MpvHostSkipStore.cs`（喂内核 --segment= 的载荷）
// 各自持有自己的条目类型；本文件只提供**跨两者共享的偏好/设置模型**，避免重复定义。

using System;
using System.Collections.Generic;
using System.Text.Json;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>单条目（一部影片/一集）的片头片尾设置，单位毫秒（与内核 <c>--segment=</c> 一致）。</summary>
public sealed class SkipSettings
{
    public string ItemId { get; set; } = string.Empty;

    /// <summary>总体开关。</summary>
    public bool Enabled { get; set; } = true;

    public long IntroStartMs { get; set; }
    public long IntroEndMs { get; set; }

    /// <summary>片尾起点；<c>0</c> 表示未设置（表示「到片尾」的语义由 <see cref="MediaSegmentDto.EndMs"/>=null 承担）。</summary>
    public long CreditsStartMs { get; set; }

    public bool HasIntro => IntroEndMs > IntroStartMs;

    public bool HasCredits => CreditsStartMs > 0;

    public bool IsEmpty => !HasIntro && !HasCredits;

    /// <summary>转成内核片段（<see cref="MediaSegmentDto"/>）；未设置的类别不产出。</summary>
    public List<MediaSegmentDto> ToSegments()
    {
        var segments = new List<MediaSegmentDto>();
        if (!Enabled) return segments;

        if (HasIntro)
        {
            segments.Add(new MediaSegmentDto
            {
                Type = MediaSegmentType.Intro,
                StartMs = IntroStartMs,
                EndMs = IntroEndMs,
                Source = "local",
            });
        }

        if (HasCredits)
        {
            segments.Add(new MediaSegmentDto
            {
                Type = MediaSegmentType.Credits,
                StartMs = CreditsStartMs,
                EndMs = null, // 到片尾
                Source = "local",
            });
        }

        return segments;
    }

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["itemId"] = ItemId,
        ["enabled"] = Enabled,
        ["introStartMs"] = IntroStartMs,
        ["introEndMs"] = IntroEndMs,
        ["creditsStartMs"] = CreditsStartMs,
    };

    public static SkipSettings FromJson(JsonElement json) => new SkipSettings
    {
        ItemId = JsonRead.Str(json, "itemId"),
        Enabled = JsonRead.Bool(json, "enabled", true),
        IntroStartMs = JsonRead.LongOrNull(json, "introStartMs") ?? 0,
        IntroEndMs = JsonRead.LongOrNull(json, "introEndMs") ?? 0,
        CreditsStartMs = JsonRead.LongOrNull(json, "creditsStartMs") ?? 0,
    };

    public SkipSettings With(
        string itemId = null,
        bool? enabled = null,
        long? introStartMs = null,
        long? introEndMs = null,
        long? creditsStartMs = null)
    {
        var c = (SkipSettings)MemberwiseClone();
        if (itemId != null) c.ItemId = itemId;
        if (enabled.HasValue) c.Enabled = enabled.Value;
        if (introStartMs.HasValue) c.IntroStartMs = introStartMs.Value;
        if (introEndMs.HasValue) c.IntroEndMs = introEndMs.Value;
        if (creditsStartMs.HasValue) c.CreditsStartMs = creditsStartMs.Value;
        return c;
    }
}

/// <summary>各类片段是否允许跳过（对应用户在设置页里的勾选；四类与内核一致）。</summary>
public sealed class SkipSegmentPreference
{
    public bool Intro { get; set; } = true;
    public bool Credits { get; set; } = true;
    public bool Recap { get; set; } = true;
    public bool Preview { get; set; } = true;

    public bool Allows(MediaSegmentType type) => type switch
    {
        MediaSegmentType.Intro => Intro,
        MediaSegmentType.Credits => Credits,
        MediaSegmentType.Recap => Recap,
        MediaSegmentType.Preview => Preview,
        _ => false,
    };

    /// <summary>按偏好过滤片段列表。</summary>
    public List<MediaSegmentDto> Filter(IEnumerable<MediaSegmentDto> segments)
    {
        var result = new List<MediaSegmentDto>();
        if (segments == null) return result;
        foreach (var s in segments)
        {
            if (s != null && Allows(s.Type)) result.Add(s);
        }
        return result;
    }

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["intro"] = Intro,
        ["credits"] = Credits,
        ["recap"] = Recap,
        ["preview"] = Preview,
    };

    public static SkipSegmentPreference FromJson(JsonElement json) => new SkipSegmentPreference
    {
        Intro = JsonRead.Bool(json, "intro", true),
        Credits = JsonRead.Bool(json, "credits", true),
        Recap = JsonRead.Bool(json, "recap", true),
        Preview = JsonRead.Bool(json, "preview", true),
    };
}

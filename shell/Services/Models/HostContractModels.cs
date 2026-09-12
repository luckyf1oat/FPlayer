// 内核接口契约模型（服务层自建同形 DTO）。
// 字段名与编码规则严格对齐内核解析器（MpvHost/WinUISample/App.cs → ParseLaunchOptions 系列），
// 完整规格见 reversed/FlutterApp/HOST_CONTRACT.md。
//
// 规格依据（DESIGN 修订 r20 §2 D5 硬规则 S-1）：
//   服务层**不得引用内核程序集、不得使用任何 `WinUISample.*` 类型**（内核程序集带 URFW 自包含模块初始化器，
//   拖进类库会让任何宿主进程（含 testhost.exe）在 .cctor 抛
//   DllNotFoundException: Microsoft.WindowsAppRuntime.dll / 0x8007007E）。
//   ⇒ 一律「自建同形 DTO + 磁盘/配置格式兼容 + App 侧适配器」；本文件即该路线的落点之一，
//      App 层把 ToJson()/ToBase64() 的结果映射到内核类型。
//
// 同族文件（按 DESIGN §4.2 的映射点分文件）：
//   - `Models/PlayerShortcuts.cs`：HostShortcuts（--shortcuts=）
//   - `Models/TodbModels.cs`    ：HostChapter / HostSprite（--chapter= / --sprite=）

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>内核约定的复合值编码：URL-safe Base64(UTF-8 JSON)，去掉尾部 <c>=</c> 填充。</summary>
public static class HostValueCodec
{
    public static string EncodeHostValue(object value)
    {
        var json = value is string text ? text : JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static JsonElement? DecodeHostValue(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return null;
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var pad = padded.Length % 4;
        if (pad == 2) padded += "==";
        else if (pad == 3) padded += "=";
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonRead.FromNode(json);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>音轨 / 字幕轨（<c>--audio-track=</c> / <c>--subtitle-track=</c>）。<c>label</c> 为空时内核会整条丢弃。</summary>
public sealed class HostTrackOption
{
    public string Label { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool External { get; set; }
    public bool Selected { get; set; }
    public bool Special { get; set; }

    /// <summary>Emby 媒体流 Index；缺省 <c>-1</c>。</summary>
    public int EmbyIndex { get; set; } = -1;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["label"] = Label,
        ["id"] = Id,
        ["url"] = Url,
        ["title"] = Title,
        ["language"] = Language,
        ["external"] = External,
        ["selected"] = Selected,
        ["special"] = Special,
        ["embyIndex"] = EmbyIndex,
    };

    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());
}

/// <summary>版本（清晰度）选项（<c>--version-option=</c>）。<c>index &lt; 0</c> 会被内核丢弃。</summary>
public sealed class HostVersionOption
{
    public string Label { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public bool Selected { get; set; }

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["label"] = Label,
        ["id"] = Id,
        ["index"] = Index,
        ["selected"] = Selected,
    };

    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());
}

/// <summary>剧集列表项（<c>--episode-list=</c>，整体是一个 JSON 数组）。<c>id</c> 为空会被丢弃。</summary>
public sealed class HostEpisodeItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsPlayed { get; set; }

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["id"] = Id,
        ["label"] = Label,
        ["isCurrent"] = IsCurrent,
        ["isPlayed"] = IsPlayed,
    };
}

/// <summary>视频缩放模式（<c>--video-fit-mode=</c>）。<c>fill</c> 在内核侧等价于 <c>cover</c>。</summary>
public enum HostVideoFitMode
{
    Contain,
    Cover,
    Stretch,
}

/// <summary>弹幕 API 条目（<c>--danmaku-api=</c>）。<c>url</c> 必需。</summary>
public sealed class HostDanmakuApi
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["name"] = Name,
        ["url"] = Url,
    };

    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());
}

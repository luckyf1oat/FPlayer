// 对应 DESIGN §4.2 `core/models/player_shortcuts.dart` → `Services/Models/PlayerShortcuts.cs`。
// 规格依据（DESIGN 修订 r20 §4.2 + 硬规则 S-1）：服务层**自建同形 DTO**，App 侧再映射内核类型；
// 服务层不得使用任何 `WinUISample.*` 类型（内核程序集带 URFW 自包含模块初始化器，拖进类库会让宿主在 .cctor 抛
// DllNotFoundException: Microsoft.WindowsAppRuntime.dll / 0x8007007E）。
//
// 值语义与内核一致：Windows `VirtualKey` 数字码；字段名与内核 `--shortcuts=` 的 JSON 形态一致，
// 任何一项缺失/类型不符都回落到默认值（等价内核 `ParseShortcuts` 失败回落 `Default`）。

using System;
using System.Collections.Generic;
using System.Text.Json;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Models;

/// <summary>播放快捷键（<c>--shortcuts=</c>）。</summary>
public sealed class HostShortcuts
{
    public int PlayPause { get; set; } = 32;      // Space
    public int SeekForward { get; set; } = 39;    // →
    public int SeekBackward { get; set; } = 37;   // ←
    public int VolumeUp { get; set; } = 38;       // ↑
    public int VolumeDown { get; set; } = 40;     // ↓
    public int ToggleFullscreen { get; set; } = 70; // F
    public int ToggleMute { get; set; } = 77;     // M
    public int NextEpisode { get; set; } = 190;   // .
    public int PreviousEpisode { get; set; } = 188; // ,
    public int SpeedUp { get; set; } = 221;       // ]
    public int SpeedDown { get; set; } = 219;     // [
    public int ResetSpeed { get; set; } = 8;      // Backspace
    public int SeekForwardSeconds { get; set; } = 10;
    public int SeekBackwardSeconds { get; set; } = 10;

    public System.Text.Json.Nodes.JsonObject ToJson() => new System.Text.Json.Nodes.JsonObject
    {
        ["playPause"] = PlayPause,
        ["seekForward"] = SeekForward,
        ["seekBackward"] = SeekBackward,
        ["volumeUp"] = VolumeUp,
        ["volumeDown"] = VolumeDown,
        ["toggleFullscreen"] = ToggleFullscreen,
        ["toggleMute"] = ToggleMute,
        ["nextEpisode"] = NextEpisode,
        ["previousEpisode"] = PreviousEpisode,
        ["speedUp"] = SpeedUp,
        ["speedDown"] = SpeedDown,
        ["resetSpeed"] = ResetSpeed,
        ["seekForwardSeconds"] = SeekForwardSeconds,
        ["seekBackwardSeconds"] = SeekBackwardSeconds,
    };

    /// <summary>任何一项缺失/类型不符都回落到默认值（与内核 <c>ParseShortcuts</c> 失败回落 <c>Default</c> 一致）。</summary>
    public static HostShortcuts FromJson(JsonElement json)
    {
        var d = new HostShortcuts();

        int Pick(string key, int fallback)
        {
            var v = JsonRead.LongOrNull(json, key);
            return v.HasValue ? (int)v.Value : fallback;
        }

        return new HostShortcuts
        {
            PlayPause = Pick("playPause", d.PlayPause),
            SeekForward = Pick("seekForward", d.SeekForward),
            SeekBackward = Pick("seekBackward", d.SeekBackward),
            VolumeUp = Pick("volumeUp", d.VolumeUp),
            VolumeDown = Pick("volumeDown", d.VolumeDown),
            ToggleFullscreen = Pick("toggleFullscreen", d.ToggleFullscreen),
            ToggleMute = Pick("toggleMute", d.ToggleMute),
            NextEpisode = Pick("nextEpisode", d.NextEpisode),
            PreviousEpisode = Pick("previousEpisode", d.PreviousEpisode),
            SpeedUp = Pick("speedUp", d.SpeedUp),
            SpeedDown = Pick("speedDown", d.SpeedDown),
            ResetSpeed = Pick("resetSpeed", d.ResetSpeed),
            SeekForwardSeconds = Pick("seekForwardSeconds", d.SeekForwardSeconds),
            SeekBackwardSeconds = Pick("seekBackwardSeconds", d.SeekBackwardSeconds),
        };
    }

    public HostShortcuts With(
        int? playPause = null, int? seekForward = null, int? seekBackward = null,
        int? volumeUp = null, int? volumeDown = null, int? toggleFullscreen = null,
        int? toggleMute = null, int? nextEpisode = null, int? previousEpisode = null,
        int? speedUp = null, int? speedDown = null, int? resetSpeed = null,
        int? seekForwardSeconds = null, int? seekBackwardSeconds = null)
    {
        var c = (HostShortcuts)MemberwiseClone();
        if (playPause.HasValue) c.PlayPause = playPause.Value;
        if (seekForward.HasValue) c.SeekForward = seekForward.Value;
        if (seekBackward.HasValue) c.SeekBackward = seekBackward.Value;
        if (volumeUp.HasValue) c.VolumeUp = volumeUp.Value;
        if (volumeDown.HasValue) c.VolumeDown = volumeDown.Value;
        if (toggleFullscreen.HasValue) c.ToggleFullscreen = toggleFullscreen.Value;
        if (toggleMute.HasValue) c.ToggleMute = toggleMute.Value;
        if (nextEpisode.HasValue) c.NextEpisode = nextEpisode.Value;
        if (previousEpisode.HasValue) c.PreviousEpisode = previousEpisode.Value;
        if (speedUp.HasValue) c.SpeedUp = speedUp.Value;
        if (speedDown.HasValue) c.SpeedDown = speedDown.Value;
        if (resetSpeed.HasValue) c.ResetSpeed = resetSpeed.Value;
        if (seekForwardSeconds.HasValue) c.SeekForwardSeconds = seekForwardSeconds.Value;
        if (seekBackwardSeconds.HasValue) c.SeekBackwardSeconds = seekBackwardSeconds.Value;
        return c;
    }

    /// <summary>内核 <c>--shortcuts=</c> 的 Base64(URL-safe JSON) 载荷。</summary>
    public string ToBase64() => HostValueCodec.EncodeHostValue(ToJson().ToJsonString());
}

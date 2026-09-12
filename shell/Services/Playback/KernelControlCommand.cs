// E-P2（t39）：外壳 → 内核的控制命令（`POST {updateUrl}/control` 的载荷）。
//
// 🔴 **契约（Captain 2026-09-11 裁定「按卡面」，以 `kernel/src/WinUISample.Models/KernelControlCommand.cs` 为权威实现）**：
//   外层键 **PascalCase `Commands`**（内核默认反序列化大小写敏感 ⇒ 写错即静默空绑定/400）；
//   内层字段 **camelCase + 显式 `[JsonPropertyName]`**：`kind` / `trackId` / `seconds` / `visible` / `url` / `rate` / `level`；
//   `kind` 字面量为 PascalCase：SetAudioTrack / SetSubtitleTrack / SetSubtitleDelay / SetSubtitleVisibility /
//   AddExternalSubtitle / SetSpeed / SetVolume / SeekAbsolute；`trackId: -1` = 关闭字幕轨。
//   错误体（内核）：`{"index":N,"kind":"…","error":"…"}`。
// ⚠️ **本类默认产出 CardSchema**；`NameValue` 形态（t38 过渡）**已被内核废弃**（内核回 400 `legacy-schema`），仅供对照存档。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AIPlayer.Shell.Services.Playback;

/// <summary>控制命令种类（8 条，与内核 `ApplyControlCommandAsync` 的 name 一一对应）。</summary>
public enum KernelControlKind
{
    /// <summary>内核 name=`aid`（value=mpv track id 字符串）。</summary>
    SetAudioTrack,

    /// <summary>内核 name=`sid`（value=mpv track id 字符串；`no`/`auto` 亦走此名）。</summary>
    SetSubtitleTrack,

    /// <summary>内核 name=`subDelay`（value=秒，InvariantCulture）。</summary>
    SetSubtitleDelay,

    /// <summary>内核 name=`subtitleToggle`（value=`true`/`false` ⇒ 内核转 `sid=auto`/`sid=no`）。</summary>
    SetSubtitleVisibility,

    /// <summary>内核 name=`externalSubtitle`（value=字幕文件路径/URL）。</summary>
    AddExternalSubtitle,

    /// <summary>内核 name=`speed`（value=倍率）。</summary>
    SetSpeed,

    /// <summary>内核 name=`volume`（value=0–100 音量）。</summary>
    SetVolume,

    /// <summary>内核 name=`seek`（value=绝对位置秒）。</summary>
    SeekAbsolute,
}

/// <summary>载荷形态。</summary>
public enum KernelControlSchema
{
    /// <summary>**契约形态（默认）**：`{"Commands":[{"kind":"SetSpeed","rate":1.25}]}`（内层 camelCase + 类型化值）。</summary>
    TypedKind = 0,

    /// <summary>
    /// ⚠️ **已废弃**：t38 过渡期的 `{"Commands":[{"name":"speed","value":"1.25"}]}`。
    /// 内核已删除该形态（`PlayerUpdateServer.cs` 对 `name`/`value` 回 **400 `legacy-schema`**）；保留仅供对照/历史证据复算。
    /// </summary>
    NameValue = 1,
}

/// <summary>一条控制命令。命令一旦构造即不可变（避免"发出去的载荷被后来的人改掉"）。</summary>
public sealed class KernelControlCommand
{
    private KernelControlCommand(KernelControlKind kind, object value, string wireName, string typedName, string typedField)
    {
        Kind = kind;
        Value = value;
        WireName = wireName;
        TypedName = typedName;
        TypedField = typedField;
    }

    public KernelControlKind Kind { get; }

    /// <summary>类型化值（int / long / double / bool / string）。</summary>
    public object Value { get; }

    /// <summary>内核 rev1 的 name（8 名之一）。</summary>
    public string WireName { get; }

    /// <summary>卡面 schema 的 Kind 名。</summary>
    public string TypedName { get; }

    /// <summary>卡面 schema 的字段名（TrackId / Seconds / Visible / Url / Rate / Level）。</summary>
    public string TypedField { get; }

    /// <summary>内核 rev1 的 value 字符串（数值走 InvariantCulture —— 中文/欧洲区域设置下小数点不得变逗号）。</summary>
    public string WireValue
    {
        get
        {
            switch (Value)
            {
                case null:
                    return string.Empty;
                case bool b:
                    return b ? "true" : "false";
                case string s:
                    return s;
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return Value.ToString();
            }
        }
    }

    public static KernelControlCommand SetAudioTrack(long trackId)
        => new KernelControlCommand(KernelControlKind.SetAudioTrack, trackId, "aid", "SetAudioTrack", "trackId");

    public static KernelControlCommand SetSubtitleTrack(long trackId)
        => new KernelControlCommand(KernelControlKind.SetSubtitleTrack, trackId, "sid", "SetSubtitleTrack", "trackId");

    /// <summary>关字幕（契约：`trackId = -1`）。</summary>
    public static KernelControlCommand SetSubtitleTrackDisabled()
        => SetSubtitleTrack(-1);

    public static KernelControlCommand SetSubtitleDelay(double seconds)
        => new KernelControlCommand(KernelControlKind.SetSubtitleDelay, seconds, "subDelay", "SetSubtitleDelay", "seconds");

    public static KernelControlCommand SetSubtitleVisibility(bool visible)
        => new KernelControlCommand(KernelControlKind.SetSubtitleVisibility, visible, "subtitleToggle", "SetSubtitleVisibility", "visible");

    public static KernelControlCommand AddExternalSubtitle(string pathOrUrl)
        => new KernelControlCommand(KernelControlKind.AddExternalSubtitle, pathOrUrl ?? string.Empty, "externalSubtitle", "AddExternalSubtitle", "url");

    public static KernelControlCommand SetSpeed(double rate)
        => new KernelControlCommand(KernelControlKind.SetSpeed, rate, "speed", "SetSpeed", "rate");

    public static KernelControlCommand SetVolume(int level)
        => new KernelControlCommand(KernelControlKind.SetVolume, level, "volume", "SetVolume", "level");

    public static KernelControlCommand SeekAbsolute(double seconds)
        => new KernelControlCommand(KernelControlKind.SeekAbsolute, seconds, "seek", "SeekAbsolute", "seconds");

    /// <summary>把一条命令写进 JSON（内层键按 schema）。仅供 <see cref="KernelControlPayload"/> 调用。</summary>
    internal void WriteJson(StringBuilder sb, KernelControlSchema schema)
    {
        if (schema == KernelControlSchema.TypedKind)
        {
            sb.Append("{\"kind\":\"");
            sb.Append(TypedName);
            sb.Append("\",\"");
            sb.Append(TypedField);
            sb.Append("\":");
            AppendTypedValue(sb);
            sb.Append('}');
            return;
        }

        sb.Append("{\"name\":\"");
        sb.Append(JsonEscape(WireName));
        sb.Append("\",\"value\":\"");
        sb.Append(JsonEscape(WireValue));
        sb.Append("\"}");
    }

    private void AppendTypedValue(StringBuilder sb)
    {
        switch (Value)
        {
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case string s:
                sb.Append('"').Append(JsonEscape(s)).Append('"');
                break;
            case double d:
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                break;
            case float f:
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                break;
            case IFormattable formattable:
                sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append('"').Append(JsonEscape(WireValue)).Append('"');
                break;
        }
    }

    internal static string JsonEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ')
                    {
                        sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    public override string ToString() => $"{WireName}={WireValue}";
}

/// <summary>`POST /control` 的载荷构造（外层键一律大写开头的 `Commands`）。</summary>
public static class KernelControlPayload
{
    public const string EnvelopeKey = "Commands";

    public static string Build(IReadOnlyList<KernelControlCommand> commands, KernelControlSchema schema = KernelControlSchema.TypedKind)
    {
        if (commands == null)
        {
            throw new ArgumentNullException(nameof(commands));
        }
        if (commands.Count == 0)
        {
            throw new ArgumentException("命令集为空：内核会按「空命令集」回 400（这是设计，不是容错点）", nameof(commands));
        }
        var sb = new StringBuilder();
        sb.Append("{\"").Append(EnvelopeKey).Append("\":[");
        for (var i = 0; i < commands.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            commands[i].WriteJson(sb, schema);
        }
        sb.Append("]}");
        return sb.ToString();
    }

    public static string Build(KernelControlCommand command, KernelControlSchema schema = KernelControlSchema.TypedKind)
        => Build(new[] { command }, schema);
}

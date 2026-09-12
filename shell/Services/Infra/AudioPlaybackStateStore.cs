// S5 体验设施（t33）：音频播放状态（音量 / 静音 / 音轨偏好）的持久化。
// 落点与命名**照原版实测**：`%LOCALAPPDATA%\AIPlayer\audio_playback_<serverId>.json`
//   （本机实测存在 `audio_playback_<原版 serverId>.json`，内容为空对象 `{}`）。
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>一个服务器的音频播放状态。</summary>
public sealed class AudioPlaybackState
{
    /// <summary>音量（0.0 – 1.0；与原版一致，1.0 = 100%）。</summary>
    public double Volume { get; set; } = 1.0;

    public bool Muted { get; set; }

    /// <summary>上次选中的音轨（Emby <c>AudioStreamIndex</c>）；未选过 ⇒ null。</summary>
    public int? AudioStreamIndex { get; set; }

    /// <summary>上次音轨的语言标签（用于换源后重选，可空）。</summary>
    public string AudioLanguage { get; set; }

    public AudioPlaybackState Clone() => new AudioPlaybackState
    {
        Volume = Volume,
        Muted = Muted,
        AudioStreamIndex = AudioStreamIndex,
        AudioLanguage = AudioLanguage,
    };

    public override string ToString()
        => $"volume={Volume.ToString("0.###", CultureInfo.InvariantCulture)} muted={Muted} audioStreamIndex={(AudioStreamIndex.HasValue ? AudioStreamIndex.Value.ToString() : "-")} lang={(string.IsNullOrEmpty(AudioLanguage) ? "-" : AudioLanguage)}";
}

/// <summary>音频状态存储：按 serverId 一个文件，改即落盘，重启后读回同一值。</summary>
public sealed class AudioPlaybackStateStore
{
    private readonly AppDataDir _data;
    private readonly Action<string> _log;

    public AudioPlaybackStateStore(AppDataDir data, Action<string> log = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _log = log;
    }

    /// <summary>该服务器的状态文件路径（原版命名 <c>audio_playback_&lt;serverId&gt;.json</c>）。</summary>
    public string PathFor(string serverId)
    {
        var safe = string.IsNullOrEmpty(serverId) ? "default" : serverId;
        foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        return _data.File("audio_playback_" + safe + ".json");
    }

    /// <summary>读；文件缺失/损坏 ⇒ 默认状态（**不抛**）。</summary>
    public AudioPlaybackState Get(string serverId)
    {
        var path = PathFor(serverId);
        var state = new AudioPlaybackState();
        try
        {
            if (!File.Exists(path)) return state;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return state;
            if (root.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var vol)) state.Volume = Clamp01(vol);
            if (root.TryGetProperty("muted", out var m) && (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False)) state.Muted = m.GetBoolean();
            if (root.TryGetProperty("audioStreamIndex", out var idx) && idx.ValueKind == JsonValueKind.Number && idx.TryGetInt32(out var i)) state.AudioStreamIndex = i;
            if (root.TryGetProperty("audioLanguage", out var lang) && lang.ValueKind == JsonValueKind.String) state.AudioLanguage = lang.GetString();
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException)
        {
            _log?.Invoke($"AUDIO-STATE 读取失败（按默认值处理）：{ex.GetType().Name}");
        }
        return state;
    }

    /// <summary>写（整对象覆盖）。</summary>
    public void Set(string serverId, AudioPlaybackState state)
    {
        if (state == null) return;
        state.Volume = Clamp01(state.Volume);
        var obj = new JsonObject
        {
            ["volume"] = state.Volume,
            ["muted"] = state.Muted,
            ["updatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };
        // 可选字段**只在有值时写入**（避免 JsonNode 里出现 null 节点，读取侧也就不必处理 Null 形态）
        if (state.AudioStreamIndex.HasValue) obj["audioStreamIndex"] = state.AudioStreamIndex.Value;
        if (!string.IsNullOrEmpty(state.AudioLanguage)) obj["audioLanguage"] = state.AudioLanguage;
        var path = PathFor(serverId);
        AtomicFile.WriteAllText(path, obj.ToJsonString());   // t148：原子写（音量/音轨状态不得留半截）
        _log?.Invoke($"AUDIO-STATE 落盘 {Path.GetFileName(path)} {state}");
    }

    /// <summary>调音量（增量，夹取到 [0,1]）并落盘，返回新音量。</summary>
    public double AdjustVolume(string serverId, double delta)
    {
        var state = Get(serverId);
        state.Volume = Clamp01(state.Volume + delta);
        if (delta != 0) state.Muted = false;
        Set(serverId, state);
        return state.Volume;
    }

    /// <summary>直接设音量并落盘，返回新音量。</summary>
    public double SetVolume(string serverId, double volume)
    {
        var state = Get(serverId);
        state.Volume = Clamp01(volume);
        Set(serverId, state);
        return state.Volume;
    }

    /// <summary>记一次音轨选择并落盘。</summary>
    public void SetAudioTrack(string serverId, int? audioStreamIndex, string language = null)
    {
        var state = Get(serverId);
        state.AudioStreamIndex = audioStreamIndex;
        state.AudioLanguage = language;
        Set(serverId, state);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}

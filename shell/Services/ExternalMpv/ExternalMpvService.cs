// S5 体验设施（t33）：外部 mpv —— 用户配置了外部 mpv 可执行文件就走它，否则走内核自带的 libmpv。
// 判据形态：**配了且文件存在 ⇒ 产出 `--mpv-path=<路径>`；没配 ⇒ 产出 null（不加参数）；配了但不存在 ⇒ 回落内置并给出原因**。
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;

namespace AIPlayer.Shell.Services.ExternalMpv;

/// <summary>外部 mpv 的解析结果（可打印、可断言）。</summary>
public sealed class ExternalMpvDecision
{
    public bool UseExternal { get; set; }

    public string ExecutablePath { get; set; }

    public string Reason { get; set; }

    /// <summary>内核启动参数片段（走内置时为 <c>null</c>）。</summary>
    public string Argument { get; set; }

    public override string ToString()
        => $"useExternal={UseExternal} path={(string.IsNullOrEmpty(ExecutablePath) ? "-" : ExecutablePath)} arg={(string.IsNullOrEmpty(Argument) ? "-" : Argument)} reason={Reason}";
}

/// <summary>
/// 外部 mpv 服务：解析 + 落盘配置（<c>&lt;数据根&gt;\external-mpv.json</c>）。
/// </summary>
public sealed class ExternalMpvService
{
    public const string SettingFileName = "external-mpv.json";

    private readonly AppDataDir _data;
    private readonly Action<string> _log;

    public ExternalMpvService(AppDataDir data, Action<string> log = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _log = log;
    }

    public string SettingPath => _data.File(SettingFileName);

    /// <summary>读取已保存的外部 mpv 路径（无配置 ⇒ <c>null</c>）。</summary>
    public string ReadConfiguredPath()
    {
        try
        {
            if (!File.Exists(SettingPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingPath));
            return doc.RootElement.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                ? NullIfEmpty(p.GetString())
                : null;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException)
        {
            _log?.Invoke($"EXTERNAL-MPV 配置读取失败：{ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>保存/清除外部 mpv 路径（<c>null</c> 或空 ⇒ 清除配置 = 回到内置）。</summary>
    public void SaveConfiguredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (File.Exists(SettingPath)) File.Delete(SettingPath);
            _log?.Invoke("EXTERNAL-MPV 配置已清除（走内置 libmpv）");
            return;
        }
        var obj = new JsonObject { ["path"] = path.Trim(), ["updatedAtUtc"] = DateTime.UtcNow.ToString("o") };
        AtomicFile.WriteAllText(SettingPath, obj.ToJsonString());   // t148：原子写（外部播放器路径不得留半截）
        _log?.Invoke($"EXTERNAL-MPV 配置已保存 path={path.Trim()}");
    }

    /// <summary>按"显式传入 ⇒ 落盘配置"的顺序解析。<paramref name="configuredPath"/> 为 null 时读落盘配置。</summary>
    public ExternalMpvDecision Resolve(string configuredPath = null)
    {
        var path = configuredPath ?? ReadConfiguredPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            var builtIn = new ExternalMpvDecision
            {
                UseExternal = false,
                Reason = "未配置外部 mpv ⇒ 走内核自带 libmpv（不加 --mpv-path）",
            };
            _log?.Invoke($"EXTERNAL-MPV {builtIn}");
            return builtIn;
        }

        path = path.Trim();
        if (!File.Exists(path))
        {
            var fallback = new ExternalMpvDecision
            {
                UseExternal = false,
                ExecutablePath = path,
                Reason = "配置的外部 mpv 路径不存在 ⇒ 回落内核自带 libmpv",
            };
            _log?.Invoke($"EXTERNAL-MPV {fallback}");
            return fallback;
        }

        var decision = new ExternalMpvDecision
        {
            UseExternal = true,
            ExecutablePath = path,
            Argument = "--mpv-path=" + path,
            Reason = "已配置外部 mpv 且文件存在 ⇒ 走外部",
        };
        _log?.Invoke($"EXTERNAL-MPV {decision}");
        return decision;
    }

    /// <summary>直接产出内核参数（走内置 ⇒ <c>null</c>）。</summary>
    public string ResolveArgument(string configuredPath = null) => Resolve(configuredPath).Argument;

    private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

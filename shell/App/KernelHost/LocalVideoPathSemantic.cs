// t27-F-A 第 ④ 项：**凭据面语义修正 —— 不把带 token 的网络流 URL 写进「上次本地视频」槽**。
//
// 背景（`shell/docs/VERIFY_S1.md` + `WORKSPACE.md` 事实 91 一族，2026-09-11 实测）：
//   全树穷举 `%LOCALAPPDATA%\AIPlayer` 2,204 个文件，含明文 `api_key=[0-9a-fA-F]{16,}` 的 = **1 个** ⇒
//   `player\settings.json` 的 **`LastLocalVideoPath`**。
//   归属链：内核 `AppViewModel.cs:296`（`HandleLaunchAsync`）无条件
//   `WriteLocalSetting("LastLocalVideoPath", options.MediaPath)`；M3 把**带 token 的 Emby 流 URL**
//   当 `--open=` 交给内核 ⇒ 内核按原语义把它存进"最后打开的媒体路径"。
//
// 裁决口径（**照抄，不扩张**）：
//   · **不采**"打码器扩范围" —— 那会让内核读回 `***` ⇒「恢复上次播放」静默失效（把"暴露"换成"功能坏掉"）；
//   · **采**"语义修正" —— 该槽的正确语义 = **本地文件路径**（内核侧两处真正的写点：`AppViewModel.cs:329` 与
//     `LocalVideoPageViewModel.cs:78` 写的都是 `StorageFile.Path`）；
//   · 网络流 URL **不得留在该槽**：检测到就恢复成"本机可确认存在的本地路径"，没有就**删键**
//     （= 什么都没发生过），并在外壳自己的键里留一条"上一个本地视频"以免功能丢失。
//
// ⚠️ 与"打码"的关系：本模块**不碰**打码（打码仍只在 `Program.Log` / `KernelLogMasker`）。它做的是
//    **根本不让这个值以"上次本地视频"的身份留在盘上**。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AIPlayer.Shell.KernelHost;

/// <summary>`LastLocalVideoPath` 的语义闸门（外壳侧单点）。</summary>
public static class LocalVideoPathSemantic
{
    /// <summary>内核「上次本地视频」槽的键名（**不要改**：内核按同一字面量读写）。</summary>
    public const string SlotKey = "LastLocalVideoPath";

    /// <summary>外壳自己的"上一个本地视频"键（独立于内核槽；值同样按内核口径 = `JsonSerializer.Serialize(path)`）。</summary>
    public const string OwnKeepKey = "Shell.LastLocalVideoPath";

    // t245：根推导改走 `AppDataDir.Instance.Root`（与外壳其余落点同源）。
    // 改前 = `<本地已知文件夹 API>\AIPlayer\player\settings.json`（该 API 走 SHGetKnownFolderPath，
    // **不看 `AIPLAYER_APPDATA_ROOT`**）⇒ 实测：设了覆盖的实例里，本模块仍读写**真实根**的那个文件
    // （沙箱里准备的 `player\settings.json` 一个字节都没被碰过，而真实根同名件被本模块改写）。
    // 说明：`AppDataDir` 另有 `LocalApplicationDataRoot`（**恒为真实根**，便携模式也不变）——
    // 这里**故意不用它**：本模块读写的是"内核那份 settings.json"，必须跟数据根走，否则沙箱实例会改真实根。
    private static readonly string SettingsPath = Path.Combine(
        AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root,
        "player", "settings.json");

    private static readonly object Gate = new object();

    private static bool _restorePending;

    /// <summary>本次判定：槽里是"网络流 URL"（= 不该在这个槽里的东西）。</summary>
    public static bool IsStreamUrl(string value)
    {
        var v = (value ?? string.Empty).Trim().Trim('"').Trim();
        return v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 启动**之前**调用：槽里若已是流 URL（上一轮残留），先清掉并把"要恢复的值"记下来。
    /// <paramref name="log"/> 为空时用 <c>Program.Log</c>。
    /// </summary>
    public static void GuardBeforeLaunch(Action<string> log = null)
    {
        log ??= Program.Log;
        lock (Gate)
        {
            try
            {
                var slot = ReadSlotValue();
                if (!IsStreamUrl(slot))
                {
                    _restorePending = false;
                    return;
                }

                var keep = ReadOwnKeepValue();
                ApplyLocalValue(keep);
                _restorePending = true;
                log($"LOCALSLOT-GUARD before-launch stripped-stream-url restore={(string.IsNullOrWhiteSpace(keep) ? "<none>" : "<local>")}");
            }
            catch (Exception ex)
            {
                log("LOCALSLOT-GUARD before-launch FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 播放**之后**调用（幂等、可反复调）：槽里若出现流 URL ⇒ 恢复本地值/删键。
    /// 返回是否真的改动了盘上内容。
    /// </summary>
    public static bool RestoreAfterPlayback(Action<string> log = null)
    {
        log ??= Program.Log;
        lock (Gate)
        {
            try
            {
                var slot = ReadSlotValue();
                if (!IsStreamUrl(slot))
                {
                    return false;   // 槽里本来就不是流 URL ⇒ 一个字节都不动
                }

                var keep = ReadOwnKeepValue();
                ApplyLocalValue(keep);
                _restorePending = false;
                log("LOCALSLOT-GUARD after-playback restored"
                    + " slotWas=" + Describe(slot)
                    + " restoredTo=" + (string.IsNullOrWhiteSpace(keep) ? "<removed-key>" : "<local-path>"));
                return true;
            }
            catch (Exception ex)
            {
                log("LOCALSLOT-GUARD after-playback FAIL " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>是否有待恢复项（自检/审计用）。</summary>
    public static bool RestorePending { get { lock (Gate) { return _restorePending; } } }

    /// <summary>当前槽值（自检/审计用；**只回形态摘要**，不回原值）。</summary>
    public static string SlotValueShape()
    {
        var slot = ReadSlotValue();
        return IsStreamUrl(slot) ? "<stream-url>" : Describe(slot);
    }

    // ── 底层：与内核同一份 settings.json（只改这一个键，其余键逐字保留）─────────

    private static void ApplyLocalValue(string keep)
    {
        var map = ReadAll();
        if (map == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(keep) && File.Exists(keep))
        {
            // 内核口径：值是 `JsonSerializer.Serialize(string)` 的结果（**带引号**）。
            map[SlotKey] = JsonSerializer.Serialize(keep);
            map[OwnKeepKey] = JsonSerializer.Serialize(keep);
        }
        else
        {
            map.Remove(SlotKey);
            map.Remove(OwnKeepKey);
        }

        WriteAll(map);
    }

    private static string ReadSlotValue() => ReadStringKey(SlotKey);

    private static string ReadOwnKeepValue() => ReadStringKey(OwnKeepKey);

    private static string ReadStringKey(string key)
    {
        var map = ReadAll();
        if (map == null || !map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        try
        {
            var value = JsonSerializer.Deserialize<string>(raw);
            return value ?? string.Empty;
        }
        catch (JsonException)
        {
            // 形态不符（值不是 JSON 字符串）⇒ 当作"没有"（与内核 `ReadLocalSetting` 的容错口径一致）。
            // **不静默**：坏形态本身是要看出来的事 ⇒ 记一条形态级日志（不含原值）。
            Program.Log("LOCALSLOT-GUARD value-shape-invalid key=" + key);
            return string.Empty;
        }
    }

    private static Dictionary<string, string> ReadAll()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath))
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Program.Log("LOCALSLOT-GUARD read FAIL " + ex.GetType().Name + ": " + ex.Message);
            return null;    // 解析失败 ⇒ 宁可不写（绝不用空字典覆盖用户配置）
        }
    }

    private static void WriteAll(Dictionary<string, string> map)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        // UTF-8 无 BOM（本队踩过的编码坑）；缩进与内核 DesktopSettingsToolkit 的写回形态一致
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }),
            new System.Text.UTF8Encoding(false));
    }

    /// <summary>只回可公开的形态（**绝不回带凭据的原值**）。</summary>
    private static string Describe(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty>";
        return IsStreamUrl(value) ? "<stream-url>" : "<local-path>";
    }
}

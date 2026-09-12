// 对应 DESIGN §4.1 #26 `mpv_conf_service.dart` → `Services/Settings/MpvConfService.cs`
// （修订 r20 把该条从 `Services/Kernel/` 移到 `Services/Settings/`：纯文本生成，无内核依赖）。
// 三态标注（r24 更正）：`mpv_conf_service.dart` **属原版 151 文件树**（`src/core/services/`，DESIGN §5 #55），
//   只是不在 `rebuild/` 重建版里 ⇒「外壳写 mpv.conf」是**原版行为**，不是本项目的发明；
//   本文件是对该原版行为的**功能等价重建**（原 Dart 实现不可读，故机制与键名均按下述证据落地）。
//
// 机制实证（内核侧，非推断）：
//   - `reversed/MpvHost/WinUISample.ViewModels/PlayerViewModel.cs:2850-2851`
//     `UseConfig = !string.IsNullOrWhiteSpace(MpvConfigDir)` / `ConfigDirectory = MpvConfigDir`
//     ⇒ 只要 `--mpv-config-dir=` 非空，内核就以该目录为 mpv 配置目录。
//   - 同文件 `:2872-2881`：**没有 mpv.conf 时**内核自行套用「runtime decode/cache defaults」；
//     **有 mpv.conf 时**走「stream reconnect options」分支 ⇒ 本服务写出的 mpv.conf 会被内核实际采用。
//   - `reversed/MpvHost/WinUISample.Models/HostLaunchOptions.cs:69`：`MpvConfigDir` 是契约入参。
//   - `reversed/MpvHost/WinUISample/App.cs:349`：该入参来自命令行 `--mpv-config-dir=`。
//
// 预置键的证据来源（`reversed/FlutterApp/strings_all.txt` 原始字符串命中数，r24 §4.1 #26 裁决）：
//   `keep-open` **3** ✅ ／ `panscan` **2** ✅ ／ `http-proxy` **1** ✅ ／ `volume-max` **0** ❌ ／ `keepaspect` **0** ❌
//   ⇒ 前三者允许预置；后两者**默认不得预置**（仅保留可配置 API）。同源旁证：字符串表另有
//   `--osc=yes`、`--hwdec=d3d11va` 等选项名，说明原版确实把这些选项名当字面量带在身上。
//
// 边界：本服务只负责「把配置写成 mpv.conf 文件」。**`--mpv-config-dir=` 本身由 App 侧的入参组装负责**
// （S-1：服务层不碰内核类型）；`--gpu-api=` / `--hwdec=` / `--input-ipc-server=` 等属外挂 mpv 路径（DESIGN §2 D9，
// 归 S5），本服务**不**为它们预留字段。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Services.Settings;

/// <summary>mpv.conf 生成（对应原版 <c>mpv_conf_service</c>；重建实现）。</summary>
public sealed class MpvConfService
{
    public const string FileName = "mpv.conf";

    private readonly List<KeyValuePair<string, string>> _options = new List<KeyValuePair<string, string>>();

    /// <summary>按插入顺序保留的选项（后写覆盖同名项）。</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Options => _options.ToList();

    /// <summary>写或覆盖一个选项。<paramref name="value"/> 为 <c>null</c> 时表示「仅开关」选项（如 <c>keep-open</c>）。</summary>
    public MpvConfService Set(string key, string value = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return this;
        _options.RemoveAll(kv => string.Equals(kv.Key, key, StringComparison.Ordinal));
        _options.Add(new KeyValuePair<string, string>(key.Trim(), value));
        return this;
    }

    public MpvConfService Remove(string key)
    {
        _options.RemoveAll(kv => string.Equals(kv.Key, key, StringComparison.Ordinal));
        return this;
    }

    public bool Has(string key) => _options.Any(kv => string.Equals(kv.Key, key, StringComparison.Ordinal));

    public void Clear() => _options.Clear();

    /// <summary>渲染为 mpv.conf 文本（LF；含注释头）。</summary>
    public string Build()
    {
        var sb = new StringBuilder();
        sb.Append("# 由 AIPlayer.Shell 生成（MpvConfService）—— 手工修改会在下次写盘时被覆盖\n");
        foreach (var kv in _options)
        {
            if (string.IsNullOrEmpty(kv.Value)) sb.Append(kv.Key).Append('\n');
            else sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>写入 <paramref name="directory"/> 下的 mpv.conf，返回文件绝对路径。</summary>
    public string WriteTo(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("目录不能为空", nameof(directory));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        File.WriteAllText(path, Build(), new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// 由应用设置推导一组预置项。
    ///
    /// **预置键的证据裁决**（DESIGN r24 §4.1 第 26 项）——预置前先查 `reversed/FlutterApp/strings_all.txt`：
    /// | 键 | 原始字符串命中 | 处置 |
    /// |---|---|---|
    /// | `keep-open` | **3**（`--keep-open=yes`、`--keep-open-pause=no`、`keep-open`） | ✅ 允许预置 |
    /// | `panscan` | **2**（`--panscan=0`、`--panscan=1`） | ✅ 允许预置 |
    /// | `http-proxy` | **1**（`--http-proxy=`） | ✅ 允许预置（仅当设置里启用了代理） |
    /// | `volume-max` | **0** | ❌ **默认不得预置**（只留可配置 API：调用方仍可 `Set("volume-max", …)`） |
    /// | `keepaspect` | **0** | ❌ **默认不得预置**（同上） |
    /// 依据 DESIGN §1.2：注入无从验证的行为视为新增原版没有的功能面。
    /// </summary>
    public static MpvConfService FromSettings(AppSettings settings)
    {
        var svc = new MpvConfService();
        if (settings == null) return svc;

        // 命中 3 ⇒ 允许预置
        svc.Set("keep-open", "yes");

        // 命中 1 ⇒ 允许预置（仅当用户启用了代理；与内核 --http-proxy= 同一语义面）
        if (settings.ProxyEnabled && !string.IsNullOrWhiteSpace(settings.ProxyUrl))
        {
            svc.Set("http-proxy", settings.ProxyUrl.Trim());
        }

        // 命中 2 ⇒ 允许预置：仅 cover 模式写 panscan。
        // 注意：**不再写 `keepaspect`**（命中 0，已按 r24 降级为「仅在调用方显式配置时写入」），
        // 因此 stretch/contain 两种模式不下发任何键 —— 画面适应由内核 `--video-fit-mode=` 负责（DESIGN §2 D9 口径）。
        if (string.Equals((settings.VideoFitMode ?? "contain").Trim(), "cover", StringComparison.OrdinalIgnoreCase))
        {
            svc.Set("panscan", "1");
        }

        return svc;
    }

    /// <summary>便捷：按设置直接写盘（返回路径）。</summary>
    public static string WriteFromSettings(AppSettings settings, string directory)
        => FromSettings(settings).WriteTo(directory);

    /// <summary>便捷：写进应用数据目录下的 mpv 配置目录。</summary>
    public static string WriteToDefaultDir(AppSettings settings)
        => FromSettings(settings).WriteTo(AppDataDir.Instance.MpvConfigDir);
}

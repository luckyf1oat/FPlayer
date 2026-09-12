// t221（重建 t40 / E-P3）：外壳侧「播放中控制」的**会话单点**。
//
// 三态标注（本项目纪律：逆向到了 / 重建实现了 / 运行验证过了，前三态不得写成"已完成"）：
//   [逆向到了] 菜单项清单 = `shell/docs/PLAYBACK_LIVE_CONTROL.md:79`
//              （`音轨 ▸` / `字幕 ▸` / `字幕延迟 ±0.5s` / `倍速` / `音量` / `关闭弹幕`）
//              降级链 = 同文件 §4 三级（① 实时 → ② 重启续接 → ③ 提示"请在播放器窗口内切换"）
//              端点契约 = 同文件 §2.1（`POST {updateUrl}/control`；204 / 400 / 405 / 404 / 500）
//              单一真相源 = 同文件 §2.2（我们的 UI **只回显** mpv 真实状态，不缓存用户意图）
//   [重建实现了] 本文件 + `PlaybackTray.cs`（Win32 托盘）+ `PlaybackControlSelfTest.cs`（自检驱动）
//   [运行验证过了] 见 `shell/Tests/evidence/t221-inplayback-controls.txt`
//
// **本类是「外壳侧控制客户端」的唯一实例化点**（t221 验收⑤）：
//    t39 交付的客户端 `shell/Services/Playback/KernelControlClient.cs` 由本类按 SEAM①
//    （`KernelPlaybackBridge.FromProgressBody(progressBody, http)`，见同文件 `:361-370` 的常量）建一次、
//    同一播放会话内复用；**不得再出现第二个 `new KernelControlClient`**。
//
// **为什么播放/暂停、上一/下一**（t221 卡面 ① 里点名的两项）**在内核侧不存在**（不是我没接）：
//    `kernel/src/WinUISample.Models/KernelControlCommand.cs:52-73` 的全部命令 = 9 条
//    （SetAudioTrack / SetSubtitleTrack / SetSubtitleDelay / SetSubtitleVisibility / AddExternalSubtitle /
//      SetSpeed / SetVolume / SeekAbsolute / SetDanmakuEnabled）；**无 pause/play/next/prev/stop**。
//    未知 kind 的内核行为已定死：`kernel/src/WinUISample.ViewModels/PlayerViewModel.cs:4162-4163`
//    `default: return "unknown-kind:" + kind;` ⇒ PlayerUpdateServer 按语义回 **400**。
//    而旁路也被文档判死：`PLAYBACK_LIVE_CONTROL.md:17`+`:32`（路线 C）「内核不给 mpv 开 IPC
//    （`--input-ipc-server` 全树 0 命中）⇒ 无管道可连」⇒ 外壳**无法**直控 mpv 的 `pause`。
//    ⇒ 本实现按 t221 验收③ 指定的降级形态（**菜单项置灰 + 可见原因**）呈现，并留一条**真读数**
//    （发 `kind="Pause"` ⇒ 内核 400 `unknown-kind:Pause`）证明是内核缺命令。
//
// **第三处"设计上不可达"：`关闭弹幕`** —— 内核有第 9 条 `SetDanmakuEnabled`（`t46`），
//    但**外壳侧命令集只有 8 条**（`shell/Services/Playback/KernelControlCommand.cs:19-44`；构造器 `:62` private）
//    且 `shell/Services/` 不在本卡 `inScope` ⇒ 同样按"置灰 + 可见原因"呈现 + 记 finding（补 kind 需另开 `services` 卡）。
//
// **`退出播放` 走进程面而不是 `/control`**（内核无 stop kind）：只停**本进程自己** `Process.Start`
//    返回的那个内核（`KernelLauncher.LastLaunchedKernel`，与 `KernelLogMasker.cs:187-190` 的 H6 纪律同源），
//    pid 与本次会话记录不符则**拒绝动手**并留痕（绝不安名批量结束 —— 那会杀掉队友/用户正在跑的内核）。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using AIPlayer.Shell.KernelHost;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Playback;

namespace AIPlayer.Shell.Features.Player;

/// <summary>菜单项的动作类别（证据里逐项打印；`Unsupported` = 内核无该命令，置灰呈现）。</summary>
public enum PlaybackMenuAction
{
    /// <summary>只读信息行（如"正在播放……"），不可点。</summary>
    Info = 0,

    /// <summary>`SetVolume`。</summary>
    Volume = 1,

    /// <summary>`SetAudioTrack`（值为 mpv `aid` 序号）。</summary>
    AudioTrack = 2,

    /// <summary>`SetSubtitleTrack`（值为 mpv `sid` 序号；-1 = 关）。</summary>
    SubtitleTrack = 3,

    /// <summary>`SetSubtitleVisibility`（可逆开关，不丢已选 sid）。</summary>
    SubtitleVisibility = 4,

    /// <summary>`SetSubtitleDelay`。</summary>
    SubtitleDelay = 5,

    /// <summary>`SetSpeed`。</summary>
    Speed = 6,

    /// <summary>`SetDanmakuEnabled`。</summary>
    Danmaku = 7,

    /// <summary>停掉本进程自己拉起的那个内核进程（**不是** `/control` 命令）。</summary>
    StopPlayback = 8,

    /// <summary>内核没有这条命令 ⇒ 恒置灰（`播放/暂停`、`上一集`、`下一集`）。</summary>
    Unsupported = 9,
}

/// <summary>一条可展示的托盘菜单项（**不依赖任何 Win32 类型** ⇒ 可被自检直接断言/打印）。</summary>
public sealed class PlaybackMenuEntry
{
    /// <summary>菜单文字。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>证据/日志用的稳定动作 id（如 `volume:50`、`aid:2`、`stop`）。</summary>
    public string ActionId { get; set; } = string.Empty;

    public PlaybackMenuAction Action { get; set; } = PlaybackMenuAction.Info;

    /// <summary>非空 ⇒ **置灰**并把它作为可见原因（t221 验收③ 的"可辨识降级态"）。</summary>
    public string DisabledReason { get; set; } = string.Empty;

    /// <summary>`DisabledReason` 为空即可点（唯一判据，避免"两份可用性真相"）。</summary>
    public bool Enabled => string.IsNullOrEmpty(DisabledReason);

    /// <summary>子菜单（非 null ⇒ 自身是父项，动作=子项）。</summary>
    public List<PlaybackMenuEntry> Children { get; set; }

    /// <summary>叶子动作要发的命令（`StopPlayback` 时为 null）。</summary>
    public KernelControlCommand Command { get; set; }

    /// <summary>分隔线（Win32 `MF_SEPARATOR`）。</summary>
    public bool Separator { get; set; }

    public override string ToString()
    {
        var kind = Separator ? "sep" : (Children != null ? "sub" : Action.ToString());
        var state = Enabled ? "enabled" : "DISABLED(" + DisabledReason + ")";
        return ActionId + " | " + Label + " | " + kind + " | " + state;
    }
}

/// <summary>
/// 播放中控制会话：**持有唯一 <see cref="KernelControlClient"/>**、回显内核回推状态、
/// 产出托盘菜单模型、并把每条命令的判定结果留痕（成功的 204 与失败的降级都不静默）。
/// </summary>
public sealed class PlaybackControlSession
{
    /// <summary>自检钩子（默认零影响）：`SHELL_SELFTEST_PLAYER` 见 <see cref="PlaybackControlSelfTest"/>。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_PLAYER";

    /// <summary>进程级单点。</summary>
    public static PlaybackControlSession Current { get; } = new PlaybackControlSession();

    private readonly object _gate = new object();
    private bool _started;
    private KernelControlClient _live;
    private HttpClient _http;
    private KernelPlaybackState _state;
    private KernelControlResult _lastResult;
    private string _lastLabel = string.Empty;
    private int _launchedPid = -1;
    private string _launchedTitle = string.Empty;
    private long _sendSeq;

    private PlaybackControlSession()
    {
    }

    // ── 只读面（UI / 自检 / 证据都从这里读，不各写一份）────────────────────────

    /// <summary>内核回推的最近一次状态（**只读回显**；未收到 ⇒ null）。</summary>
    public KernelPlaybackState State { get { lock (_gate) { return _state; } } }

    /// <summary>最近一次命令的判定结果（未发过 ⇒ null）。</summary>
    public KernelControlResult LastResult { get { lock (_gate) { return _lastResult; } } }

    /// <summary>最近一次命令的标签（证据行用）。</summary>
    public string LastCommandLabel { get { lock (_gate) { return _lastLabel; } } }

    /// <summary>本进程自己拉起的那个内核 pid（未起播 ⇒ -1）。</summary>
    public int LaunchedPid { get { lock (_gate) { return _launchedPid; } } }

    /// <summary>本次播放的标题（内核 `--title=`，仅用于菜单信息行；**不含凭据**）。</summary>
    public string LaunchedTitle { get { lock (_gate) { return _launchedTitle; } } }

    /// <summary>宿主在起播时提供的音轨清单（Emby 侧；用于生成 `音轨 ▸` 的标签）。</summary>
    public List<HostTrackOption> AudioTracks { get; private set; } = new List<HostTrackOption>();

    /// <summary>宿主在起播时提供的字幕轨清单。</summary>
    public List<HostTrackOption> SubtitleTracks { get; private set; } = new List<HostTrackOption>();

    /// <summary>控制端点是否真的可用（= 收到过非空 `updateUrl`）。</summary>
    public bool HasEndpoint
    {
        get { lock (_gate) { return _live != null && _live.HasEndpoint; } }
    }

    /// <summary>唯一控制客户端的当刻地址（证据用；不含凭据）。</summary>
    public string Endpoint
    {
        get { lock (_gate) { return _live == null ? string.Empty : _live.UpdateUrl; } }
    }

    /// <summary>本进程自己拉起的那个内核进程当刻是否还活着（**判据是同一 pid 的进程对象**，不是按名扫描）。</summary>
    public bool LaunchedKernelAlive
    {
        get
        {
            var launched = KernelLauncher.LastLaunchedKernel;
            if (launched == null)
            {
                return false;
            }
            try
            {
                return !launched.HasExited;
            }
            catch (Exception)
            {
                // 进程句柄已失效（内核已退出并回收）⇒ 按"不在跑"处理（与 HasExited=true 同义）
                return false;
            }
        }
    }

    /// <summary>是否处于"可控制"态：**有端点 且 本次起播的内核还活着**。</summary>
    public bool IsActive => HasEndpoint && LaunchedKernelAlive;

    /// <summary>可否停播：有自己拉起的、还活着的内核。</summary>
    public bool CanStopPlayback => LaunchedPid > 0 && LaunchedKernelAlive;

    // ── 接线 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 订阅内核回调体的观察点（**由 <see cref="ShellCallback.StartOnce"/> 在上壳启动时调一次**）。
    /// 本方法不建 UI、不起进程 ⇒ 可安全地在 `App.OnLaunched` 早期调用。
    /// </summary>
    public void StartOnce()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        ShellCallback.CallbackObserved += OnCallbackObserved;
        Program.Log("PLAYER-SESSION start hook=ShellCallback.CallbackObserved version=t221");
    }

    /// <summary>起播发布点（由 <see cref="KernelLauncher.Launch"/> 在我方成功 `Process.Start` 后调用）。</summary>
    public void OnKernelLaunched(KernelLaunchRequest request, Process process)
    {
        if (request == null)
        {
            return;
        }

        lock (_gate)
        {
            // 新一轮播放 = 新内核进程 = **新的一次性端点** ⇒ 旧客户端必须丢掉（否则把命令发到上一轮的地址）。
            _live?.Dispose();
            _live = null;
            _state = null;
            _lastResult = null;
            _lastLabel = string.Empty;
            _launchedPid = process == null ? -1 : process.Id;
            _launchedTitle = request.Title ?? string.Empty;
            AudioTracks = request.AudioTracks ?? new List<HostTrackOption>();
            SubtitleTracks = request.SubtitleTracks ?? new List<HostTrackOption>();
        }

        Program.Log("PLAYER-SESSION launched pid=" + (process == null ? -1 : process.Id)
            + " audioTracks=" + AudioTracks.Count
            + " subtitleTracks=" + SubtitleTracks.Count
            + " title_len=" + (_launchedTitle ?? string.Empty).Length
            + " endpoint=(等待 progress 回推)");
    }

    private void OnCallbackObserved(string eventName, string body)
    {
        try
        {
            ApplyCallbackBody(eventName, body);
        }
        catch (Exception ex)
        {
            // 回调面在端点线程上 ⇒ 绝不把异常抛回去打死端点（与 ShellCallback.Respond 同口径）
            Program.Log("PLAYER-SESSION apply FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>接一次内核回调体；返回解析出的状态（非法体/无关事件 ⇒ null，**不改动**已有端点）。</summary>
    public KernelPlaybackState ApplyCallbackBody(string eventName, string body)
    {
        if (string.Equals(eventName, "stopped", StringComparison.Ordinal))
        {
            // 播完/被停：端点随进程失效 ⇒ 菜单必须立刻回到"未播放"态（不留一堆能点的假控制项）
            Program.Log("PLAYER-SESSION stopped（本轮回推结束 ⇒ 控制项回到未播放态）");
            return null;
        }

        if (!string.Equals(eventName, "progress", StringComparison.Ordinal))
        {
            return null;
        }

        KernelPlaybackState state;
        bool created = false;
        lock (_gate)
        {
            if (_live == null)
            {
                _live = KernelPlaybackBridge.FromProgressBody(body, GetHttp());
                created = true;
            }
            state = KernelPlaybackBridge.UpdateFromProgressBody(_live, body);
            if (state != null)
            {
                _state = state;
            }
        }

        if (state == null)
        {
            Program.Log("PLAYER-SESSION progress body unusable（解析为 null ⇒ 不改动已有点端与状态）");
            return null;
        }

        if (created)
        {
            Program.Log("PLAYER-SESSION live-created via=KernelPlaybackBridge.FromProgressBody（SEAM① 唯一一行）"
                + " hasEndpoint=" + HasEndpoint);
        }

        // 首次拿到端点 ⇒ 一条显式读数（"能不能实时控制"必须当场可判，不靠事后外推）
        Program.Log("PLAYER-STATE " + state + " ｜ active=" + IsActive);
        return state;
    }

    /// <summary>
    /// 唯一 `HttpClient`（**不出网到别处**：控制端点只在回环；显式关代理）。
    /// 超时 5 s = t39 客户端默认（超时臂的反控读数见 `EndpointUnreachable` + `error=timeout`）。
    /// </summary>
    private HttpClient GetHttp()
    {
        if (_http == null)
        {
            _http = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
        }

        return _http;
    }

    // ── 命令 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 发一条命令并把判定结果留痕。**不抛异常**（t39 客户端已把所有失败编码进结果对象）；
    /// 失败时打印 `PLAYER-DEGRADE`（含用户可读文案 = 菜单置灰/提示的同一素材）。
    /// </summary>
    public async Task<KernelControlResult> SendAsync(KernelControlCommand command, string label)
    {
        var seq = System.Threading.Interlocked.Increment(ref _sendSeq);
        KernelControlClient client;
        lock (_gate)
        {
            client = _live;
        }

        KernelControlResult result;
        if (command == null)
        {
            result = new KernelControlResult { Outcome = KernelControlOutcome.Rejected, Error = "null-command" };
        }
        else if (client == null)
        {
            // 内核未起 / 未收到过 updateUrl ⇒ 根本没有端点（降级链第 ③ 级）
            result = new KernelControlResult { Outcome = KernelControlOutcome.NoEndpoint, Error = "no-session" };
        }
        else
        {
            result = await client.SendAsync(command).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _lastResult = result;
            _lastLabel = label ?? string.Empty;
        }

        Program.Log("PLAYER-CMD seq=" + seq + " label=" + (label ?? "<null>")
            + " kind=" + (command == null ? "<null>" : command.Kind.ToString())
            + " status=" + result.StatusCode
            + " outcome=" + result.Outcome
            + " success=" + result.Success
            + " endpoint=" + (string.IsNullOrEmpty(result.Endpoint) ? (Endpoint.Length == 0 ? "<none>" : Endpoint) : result.Endpoint)
            + " body=" + result.RequestBody
            + " resp=" + Shorten(result.ResponseBody)
            + " err=" + (string.IsNullOrEmpty(result.Error) ? "-" : result.Error));

        if (!result.Success)
        {
            Program.Log("PLAYER-DEGRADE seq=" + seq + " label=" + (label ?? "<null>")
                + " outcome=" + result.Outcome
                + " status=" + result.StatusCode
                + " userText=" + result.UserFacingFailure);
        }

        return result;
    }

    private static string Shorten(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "-";
        }

        var flat = text.Replace("\r", " ").Replace("\n", " ");
        return flat.Length <= 200 ? flat : flat.Substring(0, 200) + "…";
    }

    /// <summary>
    /// `退出播放`：只停**本进程自己**拉起的那个内核（pid 与本次会话记录必须一致）。
    /// 返回一行判定（同时写日志），可用性判据**不靠猜**：`LaunchedKernelAlive`。
    /// </summary>
    public string StopPlayback()
    {
        var launched = KernelLauncher.LastLaunchedKernel;
        if (launched == null)
        {
            var line = "skipped reason=no-self-launched-kernel（不按进程名结束他人进程）";
            Program.Log("PLAYER-STOP " + line);
            return line;
        }

        int ownPid;
        lock (_gate)
        {
            ownPid = _launchedPid;
        }

        if (ownPid <= 0 || launched.Id != ownPid)
        {
            var line = "refused reason=pid-mismatch ownPid=" + ownPid + " lastLaunchedPid=" + launched.Id;
            Program.Log("PLAYER-STOP " + line);
            return line;
        }

        try
        {
            launched.Kill(entireProcessTree: true);
            Program.Log("PLAYER-STOP killed pid=" + ownPid + " kind=self-launched");
            return "killed pid=" + ownPid;
        }
        catch (Exception ex)
        {
            var line = "kill FAIL pid=" + ownPid + " " + ex.GetType().Name + ": " + ex.Message;
            Program.Log("PLAYER-STOP " + line);
            return line;
        }
    }

    // ── 菜单模型（纯数据；Win32 面在 PlaybackTray.cs）─────────────────────────

    /// <summary>
    /// 产出托盘菜单。**可用性只有一条判据**（`IsActive` + 该项的前提），
    /// 未播放/无端点时**每一项都带可见原因**（t221 验收③：置灰或一行可见提示，绝不静默无效）。
    /// </summary>
    public List<PlaybackMenuEntry> BuildMenu()
    {
        var items = new List<PlaybackMenuEntry>();
        var state = State;
        var active = IsActive;
        var notActiveReason = NotActiveReason();

        // ① 信息行（永远置灰；它的作用是把"当前状态 + 为什么不能控制"摆在第一眼）
        items.Add(new PlaybackMenuEntry
        {
            Label = DegradationPrefix() + (active ? InfoLine(state) : "AIPlayer · 未在播放"),
            ActionId = "info",
            Action = PlaybackMenuAction.Info,
            DisabledReason = "信息行",
        });

        // ② 音轨 ▸（宿主起播时给的清单；**序号 → mpv aid** 的映射由内核 track-list 校验兜底：
        //    不存在 ⇒ 400 `track-not-found`，绝不静默成功）
        items.Add(new PlaybackMenuEntry
        {
            Label = "音轨",
            ActionId = "menu:aid",
            Action = PlaybackMenuAction.AudioTrack,
            DisabledReason = active ? (AudioTracks.Count == 0 ? "宿主未提供音轨清单" : string.Empty) : notActiveReason,
            Children = AudioChildren(state),
        });

        // ③ 字幕 ▸（含"关闭字幕"= `sid=no`；另有可逆的显示/隐藏开关）
        items.Add(new PlaybackMenuEntry
        {
            Label = "字幕",
            ActionId = "menu:sid",
            Action = PlaybackMenuAction.SubtitleTrack,
            DisabledReason = active ? string.Empty : notActiveReason,
            Children = SubtitleChildren(state),
        });

        // ④ 字幕延迟 ▸（±0.5 s / 归零；基准 = 内核回推的 `subDelay`，不是本地缓存）
        items.Add(new PlaybackMenuEntry
        {
            Label = "字幕延迟",
            ActionId = "menu:subdelay",
            Action = PlaybackMenuAction.SubtitleDelay,
            DisabledReason = active ? string.Empty : notActiveReason,
            Children = SubtitleDelayChildren(state),
        });

        // ⑤ 倍速 ▸
        items.Add(new PlaybackMenuEntry
        {
            Label = "倍速",
            ActionId = "menu:speed",
            Action = PlaybackMenuAction.Speed,
            DisabledReason = active ? string.Empty : notActiveReason,
            Children = SpeedChildren(),
        });

        // ⑥ 音量 ▸（上限 = 内核 `--max-volume=`（本壳传 `MaxVolume`），越界内核回 400 ⇒ 这里不出越界项）
        items.Add(new PlaybackMenuEntry
        {
            Label = "音量",
            ActionId = "menu:volume",
            Action = PlaybackMenuAction.Volume,
            DisabledReason = active ? string.Empty : notActiveReason,
            Children = VolumeChildren(state),
        });

        // ⑦ 弹幕开关 —— [!] **本卡不可达**：内核侧第 9 条命令 `SetDanmakuEnabled`（`t46` 已交付）在
        //    **外壳侧命令集里不存在**：`shell/Services/Playback/KernelControlCommand.cs:19-44`
        //    （`KernelControlKind`）仍只有 **8 条**，且该类的构造器 `:62` 是 `private`
        //    ⇒ 外壳**构造不出**这条命令（也就无法真调端点）。`shell/Services/` 不在 t221 的 `inScope`
        //    ⇒ 按验收③ 的降级形态**置灰 + 可见原因**呈现；"补第 9 条 kind"作为 finding 交 captain。
        items.Add(new PlaybackMenuEntry
        {
            Label = "关闭弹幕",
            ActionId = "danmaku:off",
            Action = PlaybackMenuAction.Danmaku,
            DisabledReason = "外壳侧命令集缺 SetDanmakuEnabled（KernelControlKind 只有 8 条；Services/ 出界）",
        });

        items.Add(new PlaybackMenuEntry { Separator = true, ActionId = "sep1" });

        // ⑧ 内核没有的两项：**置灰 + 可见原因**（t221 卡面 ① 点名，但内核 `/control` 无该命令）
        items.Add(new PlaybackMenuEntry
        {
            Label = "播放 / 暂停",
            ActionId = "pause",
            Action = PlaybackMenuAction.Unsupported,
            DisabledReason = "内核 /control 无 pause 命令（9 条 kind 里没有它；外壳也无 mpv IPC 通道）",
        });
        items.Add(new PlaybackMenuEntry
        {
            Label = "上一集 / 下一集",
            ActionId = "prev-next",
            Action = PlaybackMenuAction.Unsupported,
            DisabledReason = "内核 /control 无 prev/next 命令；换源按项目口径 = 重启 + 续播（非实时）",
        });

        items.Add(new PlaybackMenuEntry { Separator = true, ActionId = "sep2" });

        // ⑨ 退出播放（进程面：只停本进程自己拉起的那个内核）
        items.Add(new PlaybackMenuEntry
        {
            Label = CanStopPlayback ? ("退出播放（停止内核 pid=" + LaunchedPid + "）") : "退出播放",
            ActionId = "stop",
            Action = PlaybackMenuAction.StopPlayback,
            DisabledReason = CanStopPlayback ? string.Empty : "没有本进程自己拉起的、还在跑的内核",
        });

        PropagateDisabled(items);
        return items;
    }

    /// <summary>
    /// 父项不可用 ⇒ **子项一律不可用**（原因继承父项）。
    /// 为什么必须有这一步：Win32 里父项灰化后子菜单本该打不开，但**模型层**若仍报"子项 enabled"，
    /// 就等于留了第二份可用性真相（自检/后续 UI 会照它误判）。t221 MENU 臂实测过这个形态
    /// （改前读数：`menu:sid | 字幕 | disabled:未在播放` 之下 `sid:off | enabled`）。
    /// </summary>
    private static void PropagateDisabled(List<PlaybackMenuEntry> items)
    {
        foreach (var item in items)
        {
            if (item.Children == null)
            {
                continue;
            }

            if (!item.Enabled && string.IsNullOrEmpty(item.DisabledReason))
            {
                item.DisabledReason = "父项不可用";
            }

            if (!item.Enabled)
            {
                foreach (var child in item.Children)
                {
                    if (string.IsNullOrEmpty(child.DisabledReason))
                    {
                        child.DisabledReason = "父项「" + item.Label + "」不可用：" + item.DisabledReason;
                    }
                }
            }

            PropagateDisabled(item.Children);
        }
    }

    /// <summary>
    /// 降级提示前缀（t221 验收③ 的"一行可见提示"）：最近一次命令失败时，把
    /// <see cref="KernelControlResult.UserFacingFailure"/>（**唯一文案真源**）摆在菜单第一行。
    /// 成功/未发过 ⇒ 空串（不留噪音）。
    /// </summary>
    private string DegradationPrefix()
    {
        var last = LastResult;
        if (last == null || last.Success)
        {
            return string.Empty;
        }

        return "[!] " + last.UserFacingFailure
            + "（outcome=" + last.Outcome + " status=" + last.StatusCode + " label=" + LastCommandLabel + "） ｜ ";
    }

    private string NotActiveReason()
    {        if (!HasEndpoint)
        {
            return LaunchedKernelAlive
                ? "未收到内核控制端点（本轮播放的内核未提供 updateUrl）"
                : "未在播放";
        }

        return "本次起播的内核已退出（端点随进程失效）";
    }

    private string InfoLine(KernelPlaybackState state)
    {
        var text = "正在播放" + (string.IsNullOrEmpty(LaunchedTitle) ? string.Empty : "：" + LaunchedTitle);
        if (state == null)
        {
            return text + "（等待第一次状态回推）";
        }

        return text + " ｜ mpv: aid=" + Fmt(state.Aid) + " sid=" + Fmt(state.Sid)
            + " 音量=" + Fmt(state.Volume) + " 倍速=" + Fmt(state.Speed)
            + " 字幕延迟=" + Fmt(state.SubDelay)
            + (state.IsPaused == true ? "（已暂停）" : string.Empty);
    }

    private static string Fmt(object value) => value == null ? "-" : Convert.ToString(value, CultureInfo.InvariantCulture);

    private List<PlaybackMenuEntry> AudioChildren(KernelPlaybackState state)
    {
        var list = new List<PlaybackMenuEntry>();
        var tracks = AudioTracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            // 序号 → mpv aid：容器里第 i 条音轨 = mpv `aid=i+1`（与内核 `ResolveInternalAudioTrackId`
            // 的解析口径同源）。**错了也不会静默**：内核 `VerifyTrackAsync` 用 track-list 探针
            // 判存在性 ⇒ 不存在即 400 `track-not-found`（`PlayerViewModel.cs:4300-4303`）。
            var aid = i + 1;
            var track = tracks[i];
            var current = state != null && state.Aid == aid;
            list.Add(new PlaybackMenuEntry
            {
                Label = (current ? "● " : string.Empty) + (i + 1) + ". " + Describe(track),
                ActionId = "aid:" + aid,
                Action = PlaybackMenuAction.AudioTrack,
                Command = KernelControlCommand.SetAudioTrack(aid),
            });
        }

        if (list.Count == 0)
        {
            list.Add(new PlaybackMenuEntry
            {
                Label = "（无音轨清单）",
                ActionId = "aid:none",
                Action = PlaybackMenuAction.AudioTrack,
                DisabledReason = "宿主未提供音轨清单",
            });
        }

        return list;
    }

    private List<PlaybackMenuEntry> SubtitleChildren(KernelPlaybackState state)
    {
        var list = new List<PlaybackMenuEntry>
        {
            new PlaybackMenuEntry
            {
                Label = "关闭字幕（sid=no，不丢已选轨）",
                ActionId = "sid:off",
                Action = PlaybackMenuAction.SubtitleTrack,
                Command = KernelControlCommand.SetSubtitleTrackDisabled(),
            },
            new PlaybackMenuEntry
            {
                Label = state != null && state.Sid.HasValue && state.Sid.Value < 0 ? "显示字幕（sub-visibility=yes）" : "隐藏字幕（sub-visibility=no）",
                ActionId = "sid:visibility",
                Action = PlaybackMenuAction.SubtitleVisibility,
                Command = KernelControlCommand.SetSubtitleVisibility(state == null || state.Sid == null || state.Sid.Value >= 0 ? false : true),
            },
        };

        var tracks = SubtitleTracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            var sid = i + 1;
            var track = tracks[i];
            if (track.Special)
            {
                // 图形字幕（PGS 等）：内核交给内部处理，且 `sid` 未必对应 ⇒ 明确置灰而不是发一条可能错的命令
                list.Add(new PlaybackMenuEntry
                {
                    Label = (i + 1) + ". " + Describe(track) + "（图形字幕）",
                    ActionId = "sid:" + sid,
                    Action = PlaybackMenuAction.SubtitleTrack,
                    DisabledReason = "图形字幕由内核内部处理，无 mpv sid 可定点切换",
                });
                continue;
            }

            var current = state != null && state.Sid == sid;
            list.Add(new PlaybackMenuEntry
            {
                Label = (current ? "● " : string.Empty) + (i + 1) + ". " + Describe(track),
                ActionId = "sid:" + sid,
                Action = PlaybackMenuAction.SubtitleTrack,
                Command = KernelControlCommand.SetSubtitleTrack(sid),
            });
        }

        if (tracks.Count == 0)
        {
            list.Add(new PlaybackMenuEntry
            {
                Label = "（宿主未提供字幕清单）",
                ActionId = "sid:none",
                Action = PlaybackMenuAction.SubtitleTrack,
                DisabledReason = "宿主未提供字幕清单（仍可用「关闭字幕」与「隐藏字幕」）",
            });
        }

        return list;
    }

    private static List<PlaybackMenuEntry> SubtitleDelayChildren(KernelPlaybackState state)
    {
        // 基准 = **内核回推的当前值**（单一真相源）；未收到回推时按 0 起步（菜单文字里如实写出来）
        var baseValue = state?.SubDelay ?? 0.0;
        var list = new List<PlaybackMenuEntry>();
        var steps = new[] { -1.0, -0.5, 0.5, 1.0 };
        foreach (var step in steps)
        {
            var target = Math.Round(baseValue + step, 2);
            list.Add(new PlaybackMenuEntry
            {
                Label = (step > 0 ? "+" : string.Empty) + step.ToString("0.0", CultureInfo.InvariantCulture) + " 秒"
                    + " → " + target.ToString("0.0", CultureInfo.InvariantCulture),
                ActionId = "subdelay:" + target.ToString("0.0", CultureInfo.InvariantCulture),
                Action = PlaybackMenuAction.SubtitleDelay,
                Command = KernelControlCommand.SetSubtitleDelay(target),
            });
        }

        list.Add(new PlaybackMenuEntry
        {
            Label = "归零（0.0 秒）",
            ActionId = "subdelay:0.0",
            Action = PlaybackMenuAction.SubtitleDelay,
            Command = KernelControlCommand.SetSubtitleDelay(0.0),
        });
        return list;
    }

    private static List<PlaybackMenuEntry> SpeedChildren()
    {
        var list = new List<PlaybackMenuEntry>();
        foreach (var rate in new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 })
        {
            list.Add(new PlaybackMenuEntry
            {
                Label = rate.ToString("0.##", CultureInfo.InvariantCulture) + "×",
                ActionId = "speed:" + rate.ToString("0.##", CultureInfo.InvariantCulture),
                Action = PlaybackMenuAction.Speed,
                Command = KernelControlCommand.SetSpeed(rate),
            });
        }

        return list;
    }

    private static List<PlaybackMenuEntry> VolumeChildren(KernelPlaybackState state)
    {
        var list = new List<PlaybackMenuEntry>();
        var current = state?.Volume;
        foreach (var level in new[] { 0, 25, 50, 75, 100 })
        {
            list.Add(new PlaybackMenuEntry
            {
                Label = (current.HasValue && current.Value == level ? "● " : string.Empty) + level + "%",
                ActionId = "volume:" + level,
                Action = PlaybackMenuAction.Volume,
                Command = KernelControlCommand.SetVolume(level),
            });
        }

        return list;
    }

    private static string Describe(HostTrackOption track)
    {
        if (track == null)
        {
            return "（未知轨）";
        }

        var text = !string.IsNullOrWhiteSpace(track.Label) ? track.Label
            : (!string.IsNullOrWhiteSpace(track.Title) ? track.Title : track.Language);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "轨";
        }

        if (!string.IsNullOrWhiteSpace(track.Language) && text.IndexOf(track.Language, StringComparison.OrdinalIgnoreCase) < 0)
        {
            text = text + "[" + track.Language + "]";
        }

        return text;
    }

    /// <summary>自检用：把一个菜单模型摊平成可逐行打印/断言的文本（`[深度] actionId | 文字 | 状态`）。</summary>
    public static List<string> Flatten(List<PlaybackMenuEntry> items)
    {
        var lines = new List<string>();
        FlattenInto(items, 0, lines);
        return lines;
    }

    private static void FlattenInto(List<PlaybackMenuEntry> items, int depth, List<string> lines)
    {
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.Separator)
            {
                lines.Add(new string(' ', depth * 2) + "---- separator ----");
                continue;
            }

            lines.Add(new string(' ', depth * 2) + item.ActionId + " | " + item.Label + " | "
                + (item.Enabled ? "enabled" : "disabled:" + item.DisabledReason));
            if (item.Children != null)
            {
                FlattenInto(item.Children, depth + 1, lines);
            }
        }
    }
}

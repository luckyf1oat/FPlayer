// t221（重建 t40 / E-P3）：播放中控制的**自检驱动**（默认零影响；只在设了 `SHELL_SELFTEST_PLAYER` 时干活）。
//
// 为什么需要它（本队纪律：完成必须带**运行**证据，不接受"编译通过"）：
//   · 托盘菜单的"可用性两态"（播放中 / 未播放）+ "真实 Win32 菜单结构（含灰化位）"必须能**自动取证**；
//   · 实时切轨必须走**真内核**（`kernel/` 的 `/control`），并留下内核侧 `[CONTROL-APPLY]` / `[K3-PUSH]` 回读行；
//   · 三条降级反控（内核未起 / 端点超时 / 返回非 2xx）必须各有界面读数，且**不得假装成功**。
//
// 模式（`SHELL_SELFTEST_PLAYER=<mode>`）：
//   `menu`     : 建托盘图标 + 打印菜单模型 + **枚举真实 HMENU** + 真弹一次菜单（自检自动关闭）
//   `play`     : 真起内核（本地样本 + `--callback-url`）→ 等 progress → 逐条控制命令（含切轨/反控）→ 停自己起的内核
//   `degrade`  : 三条降级臂（无端点 / 端点超时 / 非 2xx），各给 outcome + 界面文案读数
//   `all`      : menu + play + degrade（`play` 里的 404/400 反控与 `degrade` 分开跑，互不干扰）
//
// 环境变量（全部可选，缺省自洽）：
//   `SHELL_SELFTEST_PLAYER_DELAY`  = 起窗后延迟多少毫秒再跑（默认 6000；托盘/窗口都需要消息泵）
//   `SHELL_SELFTEST_PLAYER_MEDIA`  = 本地样本路径（默认 `%TEMP%\t45\sample-120s.wav`）
//   `SHELL_SELFTEST_PLAYER_EXIT`   = 1 ⇒ 跑完打印 `PLAYER-SELFTEST END`（进程退出仍由 `SHELL_SELFTEST_EXIT_API` 驱动）
//
// **本文件里的原始 HTTP `POST …/control` 只是"反控探针"**（证明内核确实**没有** `Pause` 这条命令），
//    **不参与产品路径**；产品路径的唯一控制客户端是 `Shell/Services/Playback/KernelControlClient.cs`
//    经 `PlaybackControlSession` 实例化的那一个（验收⑤ 的机械扫描口径见证据件）。
//
// **进程纪律**（验收⑦）：只停 `KernelLauncher.LastLaunchedKernel`（本进程自己 `Process.Start` 的那一个）。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.KernelHost;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Playback;

namespace AIPlayer.Shell.Features.Player;

/// <summary>播放中控制的自检驱动（见文件头模式表）。</summary>
public static class PlaybackControlSelfTest
{
    public const string ModeEnvVar = "SHELL_SELFTEST_PLAYER";
    public const string DelayEnvVar = "SHELL_SELFTEST_PLAYER_DELAY";
    public const string MediaEnvVar = "SHELL_SELFTEST_PLAYER_MEDIA";

    private static readonly HttpClient ProbeHttp = new HttpClient(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    private static int _started;

    /// <summary>由 `ShellCallback.StartOnce()`（= `App.OnLaunched` 早期、UI 线程）调用；未设变量时立即返回。</summary>
    public static void RunIfRequested()
    {
        var mode = Environment.GetEnvironmentVariable(ModeEnvVar);
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        var delayMs = 6000;
        if (int.TryParse(Environment.GetEnvironmentVariable(DelayEnvVar), out var parsed) && parsed >= 0)
        {
            delayMs = parsed;
        }

        Program.Log("PLAYER-SELFTEST BEGIN mode=" + mode + " delayMs=" + delayMs + " pid=" + Environment.ProcessId);

        // 必须回到 **UI 线程**跑（托盘窗口过程 / TrackPopupMenu 都在 UI 线程上）
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue == null)
        {
            Program.Log("PLAYER-SELFTEST FAIL reason=no-dispatcher-queue（自检必须在 UI 线程上排队）");
            return;
        }

        queue.TryEnqueue(async () =>
        {
            try
            {
                await Task.Delay(delayMs).ConfigureAwait(true);
                await RunAsync(mode).ConfigureAwait(true);
                Program.Log("PLAYER-SELFTEST END mode=" + mode);
            }
            catch (Exception ex)
            {
                Program.Log("PLAYER-SELFTEST FAIL mode=" + mode + " " + ex.GetType().Name + ": " + ex.Message);
            }
        });
    }

    private static async Task RunAsync(string mode)
    {
        if (string.Equals(mode, "menu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            RunMenuArm(show: true);
        }

        if (string.Equals(mode, "degrade", StringComparison.OrdinalIgnoreCase))
        {
            await RunDegradeArmsAsync().ConfigureAwait(true);
        }

        if (string.Equals(mode, "play", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            await RunPlaybackArmAsync().ConfigureAwait(true);
        }
    }

    // ── 臂 ①：托盘菜单（结构 / 两态 / 真弹一次）───────────────────────────────

    /// <summary>
    /// 托盘臂：建图标 → 打印"未播放"态的模型与真实 `HMENU` → （可选）真弹一次菜单。
    /// <paramref name="show"/> = false 时只读结构不弹（用于"播放中"态复读）。
    /// </summary>
    public static void RunMenuArm(bool show)
    {
        var tray = PlaybackTray.Current;
        tray.StartOnce();
        Program.Log("PLAYER-SELFTEST MENU iconAdded=" + tray.IconAdded
            + " hwnd=0x" + tray.Handle.ToInt64().ToString("X")
            + " err=" + (string.IsNullOrEmpty(tray.LastError) ? "-" : tray.LastError));

        DumpMenuModel("model");
        DumpWin32Menu();

        if (show)
        {
            // 真弹一次（默认 2500 ms 后由 WM_TIMER → EndMenu() 关闭）——证明菜单**真的渲染出来**，
            // 外部驱动可在此期间对 `#32768`（菜单窗口类）按句柄抓图（不是桌面/全屏截图）。
            // `SHELL_SELFTEST_PLAYER_MENU_MS` 可拉长停留时间（抓图需要几秒的启动开销）。
            var menuMs = 2500;
            if (int.TryParse(Environment.GetEnvironmentVariable("SHELL_SELFTEST_PLAYER_MENU_MS"), out var parsed) && parsed > 0)
            {
                menuMs = parsed;
            }

            var result = tray.ShowMenuAtCursor(autoCloseMs: menuMs);
            Program.Log("PLAYER-SELFTEST MENU showResult=" + result + " showCount=" + tray.ShowCount + " autoCloseMs=" + menuMs);
        }
    }

    /// <summary>打印菜单**模型**（可用性唯一真相源），并给出计数摘要行。</summary>
    public static void DumpMenuModel(string tag)
    {
        var items = PlaybackControlSession.Current.BuildMenu();
        var flat = PlaybackControlSession.Flatten(items);
        foreach (var line in flat)
        {
            Program.Log("PLAYER-MENU [" + tag + "] " + line);
        }

        var enabled = 0;
        var disabled = 0;
        foreach (var line in flat)
        {
            if (line.Contains("| enabled")) enabled++;
            else if (line.Contains("| disabled:")) disabled++;
        }

        Program.Log("PLAYER-MENU-SUMMARY [" + tag + "] items=" + flat.Count
            + " enabled=" + enabled + " disabled=" + disabled
            + " active=" + PlaybackControlSession.Current.IsActive
            + " hasEndpoint=" + PlaybackControlSession.Current.HasEndpoint);
    }

    /// <summary>打印**真实 Win32 菜单结构**（`GetMenuItemCount` / `GetMenuStringW` / `GetMenuState`）。</summary>
    public static void DumpWin32Menu()
    {
        var tray = PlaybackTray.Current;
        var hMenu = tray.BuildMenuForInspection(out var byId);
        if (hMenu == IntPtr.Zero)
        {
            Program.Log("PLAYER-HMENU FAIL build-returned-null");
            return;
        }

        try
        {
            var count = GetMenuItemCount(hMenu);
            Program.Log("PLAYER-HMENU top-level-count=" + count + " mappedCommandIds=" + byId.Count);
            DumpWin32MenuLevel(hMenu, 0, "top");
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    private static void DumpWin32MenuLevel(IntPtr hMenu, int depth, string path)
    {
        var count = GetMenuItemCount(hMenu);
        var buffer = new StringBuilder(512);
        for (var i = 0; i < count; i++)
        {
            buffer.Clear();
            buffer.Capacity = 512;
            var length = GetMenuStringW(hMenu, (uint)i, buffer, buffer.Capacity, MF_BYPOSITION);
            var text = length > 0 ? buffer.ToString() : string.Empty;
            var state = GetMenuState(hMenu, (uint)i, MF_BYPOSITION);
            var kind = (state & MF_POPUP) != 0 ? "sub" : ((state & MF_SEPARATOR) != 0 ? "sep" : "leaf");
            var grayed = (state & (MF_GRAYED | MF_DISABLED)) != 0;
            Program.Log("PLAYER-HMENU [" + path + ":" + i + "] kind=" + kind
                + " grayed=" + grayed
                + " stateFlags=0x" + state.ToString("X")
                + " text=" + text);

            if (kind == "sub")
            {
                var sub = GetSubMenu(hMenu, i);
                if (sub != IntPtr.Zero)
                {
                    DumpWin32MenuLevel(sub, depth + 1, path + "/" + i);
                }
            }
        }
    }

    // ── 臂 ②：真内核 + 逐条控制命令 ──────────────────────────────────────────

    /// <summary>
    /// 真起内核（本地样本，走**产品同一条** `KernelLauncher.Launch`），等 `progress` 带来端点到齐后
    /// 逐条发命令并打印 before/after（before/after 都取**内核回推**，即 mpv 真实值）。
    /// 收尾只停本进程自己拉起的那个内核。
    /// </summary>
    public static async Task RunPlaybackArmAsync()
    {
        var session = PlaybackControlSession.Current;
        var media = ResolveMedia();
        if (media == null)
        {
            Program.Log("PLAYER-SELFTEST PLAY skipped reason=no-media");
            return;
        }

        var subs = EnsureSubtitleSamples();
        var request = new KernelLaunchRequest
        {
            OpenUrl = media,
            Title = "t221 in-playback control probe",
            Subtitle = "自检样本",
            CallbackUrl = ShellCallback.Url,
            LibMpvPath = KernelLauncher.LibMpvPath,
            ParentPid = Environment.ProcessId,
            AudioTracks = new List<HostTrackOption>
            {
                new HostTrackOption { Label = "日语", Id = "1", EmbyIndex = 1, Selected = false },
                new HostTrackOption { Label = "国语", Id = "2", EmbyIndex = 2, Selected = false },
            },
            SubtitleTracks = new List<HostTrackOption>
            {
                new HostTrackOption { Label = "简体中文", Id = "1", EmbyIndex = 3, Selected = false },
                new HostTrackOption { Label = "繁体中文", Id = "2", EmbyIndex = 4, Selected = false },
            },
        };

        Program.Log("PLAYER-SELFTEST PLAY media=" + media + " subs=" + string.Join(",", subs) + " callbackUrl=" + ShellCallback.Url);
        var process = KernelLauncher.Launch(request, Program.Log);
        if (process == null)
        {
            Program.Log("PLAYER-SELFTEST PLAY FAIL reason=launch-returned-null");
            return;
        }

        Program.Log("PLAYER-SELFTEST PLAY launched pid=" + process.Id
            + " kernelExe=" + KernelLauncher.ResolveKernelExe().Path
            + " source=" + KernelLauncher.ResolveKernelExe().Source);

        // 等第一次 progress（端点随回调体到齐）——最多 30 s，判据是会话自己的 HasEndpoint
        var got = false;
        for (var i = 0; i < 60; i++)
        {
            if (session.HasEndpoint)
            {
                got = true;
                break;
            }

            await Task.Delay(500).ConfigureAwait(true);
        }

        Program.Log("PLAYER-SELFTEST PLAY endpoint-ready=" + got
            + " endpoint=" + (session.Endpoint.Length == 0 ? "<none>" : session.Endpoint)
            + " waitedMs=" + (got ? "<30s" : "30000"));
        if (!got)
        {
            Program.Log("PLAYER-SELFTEST PLAY 端点未到 ⇒ 仅打印未播放态菜单，随后停内核");
            DumpMenuModel("play-no-endpoint");
            session.StopPlayback();
            return;
        }

        // 「播放中」态的菜单（所有控制项应可用；两项内核无命令的仍应灰化）
        DumpMenuModel("playing");
        DumpWin32Menu();

        // ── 逐条命令（每条都打 before/after；before/after = 内核回推值）──────────
        await SendAndReadAsync("volume-25", KernelControlCommand.SetVolume(25)).ConfigureAwait(true);
        await SendAndReadAsync("volume-75", KernelControlCommand.SetVolume(75)).ConfigureAwait(true);
        await SendAndReadAsync("speed-1.25", KernelControlCommand.SetSpeed(1.25)).ConfigureAwait(true);
        await SendAndReadAsync("speed-1.0", KernelControlCommand.SetSpeed(1.0)).ConfigureAwait(true);
        await SendAndReadAsync("subdelay-0.5", KernelControlCommand.SetSubtitleDelay(0.5)).ConfigureAwait(true);
        await SendAndReadAsync("subdelay-0.0", KernelControlCommand.SetSubtitleDelay(0.0)).ConfigureAwait(true);

        // 字幕轨：先挂两条外挂字幕（得到 sid 列表 [1,2]），再做**真切换**
        await SendAndReadAsync("add-sub-a", KernelControlCommand.AddExternalSubtitle(subs[0])).ConfigureAwait(true);
        await SendAndReadAsync("add-sub-b", KernelControlCommand.AddExternalSubtitle(subs[1])).ConfigureAwait(true);
        var sidFirst = await SendAndReadAsync("sid-1", KernelControlCommand.SetSubtitleTrack(1)).ConfigureAwait(true);
        var sidSecond = await SendAndReadAsync("sid-2", KernelControlCommand.SetSubtitleTrack(2)).ConfigureAwait(true);
        await SendAndReadAsync("sid-1-again", KernelControlCommand.SetSubtitleTrack(1)).ConfigureAwait(true);
        Program.Log("PLAYER-SELFTEST SWITCH aid/sid 变化判据：sid 第 1 次 after=" + sidFirst
            + " 第 2 次 after=" + sidSecond + " ⇒ changed=" + (sidFirst != sidSecond));

        // 字幕可见性（可逆、不丢 sid）
        await SendAndReadAsync("sub-visibility-off", KernelControlCommand.SetSubtitleVisibility(false)).ConfigureAwait(true);
        await SendAndReadAsync("sub-visibility-on", KernelControlCommand.SetSubtitleVisibility(true)).ConfigureAwait(true);

        // 音轨：aid=1 存在（可回读）；aid=9 不存在 ⇒ **必须 400 track-not-found**（反控）
        await SendAndReadAsync("aid-1", KernelControlCommand.SetAudioTrack(1)).ConfigureAwait(true);
        await SendAndReadAsync("aid-9-bogus", KernelControlCommand.SetAudioTrack(9)).ConfigureAwait(true);

        // 静音/越界音量：内核 `--max-volume` 之外 ⇒ 400（反控）
        await SendAndReadAsync("volume-150-bogus", KernelControlCommand.SetVolume(150)).ConfigureAwait(true);

        // 非 2xx 反控（真实端点）：把端点临时指到内核**不存在的路径** ⇒ 404
        var realEndpoint = session.Endpoint;
        session.ApplyCallbackBody("progress", "{\"event\":\"progress\",\"updateUrl\":\""
            + RealEndpointWithBogusPath(realEndpoint) + "\"}");
        await SendAndReadAsync("non-2xx-404", KernelControlCommand.SetVolume(50)).ConfigureAwait(true);
        // 还原真实端点（喂回真实 updateUrl），并用一条 204 证明还原成功
        session.ApplyCallbackBody("progress", "{\"event\":\"progress\",\"updateUrl\":\"" + realEndpoint + "\"}");
        await SendAndReadAsync("restore-check", KernelControlCommand.SetVolume(50)).ConfigureAwait(true);

        // 「内核没有这条命令」的真读数（反控探针，非产品路径）
        await ProbeUnknownKindAsync(session, "Pause").ConfigureAwait(true);
        await ProbeUnknownKindAsync(session, "NextEpisode").ConfigureAwait(true);

        // 未播放态复读（停掉内核后菜单必须全部灰化）
        Program.Log("PLAYER-SELFTEST PLAY stop -> " + session.StopPlayback());
        await Task.Delay(1000).ConfigureAwait(true);
        DumpMenuModel("after-stop");
    }

    private static string RealEndpointWithBogusPath(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri) { Path = "/t221-does-not-exist/segments", Query = string.Empty };
            return builder.Uri.ToString();
        }

        return endpoint + "/../t221-does-not-exist/segments";
    }

    /// <summary>发一条命令并打印 before/after（都取内核回推状态）；返回 after 的 `sid` 供"变化判据"用。</summary>
    private static async Task<string> SendAndReadAsync(string label, KernelControlCommand command)
    {
        var session = PlaybackControlSession.Current;
        var before = Snapshot(session.State);
        var result = await session.SendAsync(command, label).ConfigureAwait(true);
        await Task.Delay(700).ConfigureAwait(true);
        var after = Snapshot(session.State);
        Program.Log("PLAYER-PROBE label=" + label
            + " status=" + result.StatusCode
            + " outcome=" + result.Outcome
            + " success=" + result.Success
            + " before[" + before + "]"
            + " after[" + after + "]");
        return after;
    }

    private static string Snapshot(KernelPlaybackState state)
    {
        if (state == null)
        {
            return "no-state";
        }

        return "aid=" + (state.Aid?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " sid=" + (state.Sid?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " volume=" + (state.Volume?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " speed=" + (state.Speed?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " subDelay=" + (state.SubDelay?.ToString(CultureInfo.InvariantCulture) ?? "-");
    }

    /// <summary>
    /// 反控探针：直接 `POST …/control` 一条**内核没有的 kind**，读内核的 400 错误体。
    /// **只用于证明"内核侧缺这条命令"**；产品路径不用它（唯一的控制客户端仍是会话持有的那个）。
    /// </summary>
    private static async Task ProbeUnknownKindAsync(PlaybackControlSession session, string kind)
    {
        var endpoint = session.Endpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            Program.Log("PLAYER-PROBE-UNKNOWN kind=" + kind + " skipped reason=no-endpoint");
            return;
        }

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath.Substring(0, uri.AbsolutePath.LastIndexOf('/') + 1) + "control",
            Query = string.Empty,
        };
        var body = "{\"Commands\":[{\"kind\":\"" + kind + "\",\"trackId\":1}]}";
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await ProbeHttp.PostAsync(builder.Uri, content).ConfigureAwait(true);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            Program.Log("PLAYER-PROBE-UNKNOWN kind=" + kind
                + " status=" + (int)response.StatusCode
                + " body=" + text.Replace("\r", " ").Replace("\n", " "));
        }
        catch (Exception ex)
        {
            Program.Log("PLAYER-PROBE-UNKNOWN kind=" + kind + " FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ── 臂 ③：三条降级反控（无端点 / 超时 / 非 2xx）──────────────────────────

    /// <summary>
    /// 三条降级臂（各自独立进程内跑，互不污染）：
    /// ① **内核未起**：会话从未收到过 progress ⇒ `NoEndpoint`；
    /// ② **端点超时**：把 updateUrl 指到一个"接受连接但永不回应"的回环监听器 ⇒ 5 s 后 `EndpointUnreachable(timeout)`；
    /// ③ **返回非 2xx**：把 updateUrl 指到一个真实回环 HTTP 服务（回 500） ⇒ `ServerError`；
    ///    再指到一个**不存在的端口** ⇒ `EndpointUnreachable(unreachable)`。
    /// 三臂都打印"界面读数"= 菜单信息行文案（= 用户在托盘里看到的那一行）。
    /// </summary>
    public static async Task RunDegradeArmsAsync()
    {
        var session = PlaybackControlSession.Current;

        // 臂 ①：内核未起（此刻没有起过任何内核）
        await DegradeArmAsync(session, "arm1-no-kernel", null, "从未收到 updateUrl（内核未起）").ConfigureAwait(true);

        // 臂 ②：接受连接但永不回应 ⇒ 客户端 5 s 超时
        var blackHole = StartBlackHoleListener(out var blackHolePort);
        await DegradeArmAsync(session, "arm2-timeout",
            "http://127.0.0.1:" + blackHolePort + "/segments",
            "回环监听器接受连接但永不回应（HttpClient 5 s 超时）").ConfigureAwait(true);
        StopBlackHole(blackHole);

        // 臂 ③a：真实回环 HTTP 服务回 500
        var server = StartFiveHundredServer(out var serverPort);
        await DegradeArmAsync(session, "arm3-http-500",
            "http://127.0.0.1:" + serverPort + "/segments",
            "回环 HTTP 服务对任何请求回 500").ConfigureAwait(true);
        StopFiveHundred(server);

        // 臂 ③b：端口不可达（连接被拒）
        await DegradeArmAsync(session, "arm3b-refused",
            "http://127.0.0.1:9/segments",
            "回环 9 端口（discard）无监听 ⇒ 连接被拒").ConfigureAwait(true);
    }

    private static async Task DegradeArmAsync(PlaybackControlSession session, string arm, string updateUrl, string setup)
    {
        Program.Log("PLAYER-DEGRADE-ARM begin arm=" + arm + " setup=" + setup
            + " updateUrl=" + (updateUrl ?? "<none>"));
        if (updateUrl != null)
        {
            session.ApplyCallbackBody("progress", "{\"event\":\"progress\",\"updateUrl\":\"" + updateUrl + "\"}");
        }

        DumpMenuModel(arm + "-before");
        var result = await session.SendAsync(KernelControlCommand.SetVolume(50), arm).ConfigureAwait(true);
        Program.Log("PLAYER-DEGRADE-ARM result arm=" + arm
            + " outcome=" + result.Outcome
            + " status=" + result.StatusCode
            + " success=" + result.Success
            + " error=" + result.Error
            + " userText=" + result.UserFacingFailure);
        DumpMenuModel(arm + "-after");
    }

    /// <summary>起一个"接受连接但永不回应"的回环监听器（超时臂的夹具）。</summary>
    private static TcpListener StartBlackHoleListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(async () =>
        {
            var held = new List<TcpClient>();
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    held.Add(client);   // 持有不放 ⇒ 请求永远等不到响应
                }
            }
            catch (Exception)
            {
                foreach (var client in held)
                {
                    client.Dispose();
                }
            }
        });
        Program.Log("PLAYER-DEGRADE-ARM fixture=black-hole port=" + port);
        return listener;
    }

    private static void StopBlackHole(TcpListener listener)
    {
        try
        {
            listener.Stop();
        }
        catch (Exception ex)
        {
            // 夹具清理失败不影响任何结论；但仍要留痕（门禁 H3：catch 体里必须有语句，只写注释会被判空 catch）
            Program.Log("PLAYER-DEGRADE-ARM fixture black-hole stop FAIL " + ex.GetType().Name);
        }
    }

    /// <summary>起一个对任何请求回 500 的回环 HTTP 服务（非 2xx 臂的夹具）。</summary>
    private static HttpListener StartFiveHundredServer(out int port)
    {
        port = PickFreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    context.Response.StatusCode = 500;
                    var payload = Encoding.UTF8.GetBytes("{\"error\":\"t221-fixture-500\"}");
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(payload, 0, payload.Length);
                    context.Response.Close();
                }
                catch (Exception ex)
                {
                    // 单次响应失败不影响后续臂；留痕（门禁 H3 要求 catch 体里有语句）
                    Program.Log("PLAYER-DEGRADE-ARM fixture 500-write FAIL " + ex.GetType().Name);
                }
            }
        });
        Program.Log("PLAYER-DEGRADE-ARM fixture=http-500 port=" + port);
        return listener;
    }

    private static void StopFiveHundred(HttpListener listener)
    {
        try
        {
            listener.Stop();
        }
        catch (Exception ex)
        {
            // 与 StopBlackHole 同口径：清理失败只留痕（门禁 H3）
            Program.Log("PLAYER-DEGRADE-ARM fixture http-500 stop FAIL " + ex.GetType().Name);
        }
    }

    private static int PickFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    // ── 夹具 ────────────────────────────────────────────────────────────────

    private static string ResolveMedia()
    {
        var fromEnv = Environment.GetEnvironmentVariable(MediaEnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var fallback = Path.Combine(Path.GetTempPath(), "t45", "sample-120s.wav");
        if (File.Exists(fallback))
        {
            return fallback;
        }

        // 都不在 ⇒ 现场生成一个 5 s 的静音 WAV（自洽，不依赖历史 %TEMP% 残留）
        var generated = Path.Combine(Path.GetTempPath(), "t221", "sample-5s.wav");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(generated));
            WriteSilentWav(generated, seconds: 5);
            Program.Log("PLAYER-SELFTEST PLAY media-generated=" + generated);
            return File.Exists(generated) ? generated : null;
        }
        catch (Exception ex)
        {
            Program.Log("PLAYER-SELFTEST PLAY media-generate FAIL " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>写两条内容不同的最小 SRT（切轨取证需要 ≥2 条字幕轨；不依赖历史 %TEMP% 残留）。</summary>
    private static List<string> EnsureSubtitleSamples()
    {
        var dir = Path.Combine(Path.GetTempPath(), "t221");
        Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "t221-a.srt");
        var b = Path.Combine(dir, "t221-b.srt");
        File.WriteAllText(a, "1\n00:00:00,500 --> 00:00:03,000\nAIPlayer t221 track A\n\n", new UTF8Encoding(false));
        File.WriteAllText(b, "1\n00:00:00,500 --> 00:00:03,000\nAIPlayer t221 track B\n\n", new UTF8Encoding(false));
        return new List<string> { a, b };
    }

    private static void WriteSilentWav(string path, int seconds)
    {
        const int sampleRate = 8000;
        var dataBytes = sampleRate * seconds * 2;
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        var zeros = new byte[dataBytes];
        writer.Write(zeros);
    }

    // ── 菜单枚举用的 Win32 ───────────────────────────────────────────────────

    private const uint MF_BYPOSITION = 0x00000400;
    private const uint MF_POPUP = 0x00000010;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuStringW(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int cchMax, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetMenuState(IntPtr hMenu, uint uId, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);
}

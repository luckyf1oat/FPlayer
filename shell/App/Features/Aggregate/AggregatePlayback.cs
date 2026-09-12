// t31：**收藏屏与聚合视界屏共用的起播入口**。
//
// 本文件**不新造播放链**：它把 t27（F-A）已经真机验证过的那条路径原样复用一次 ——
//   `EmbyPlaybackSession.BuildRequestAsync`（含 `NeedsTranscode` 判据，**不用** `PlaybackUrlResolver`）
//   → `KernelArgumentBuilder.FromPlayback`（41 参数）
//   → `KernelLauncher.Launch`（M3 进程外起内核）
//   → 注册 `ShellCallback.NavigateHandler` 处理导航回调。
//
// 🔴 为什么抽出来而不是让两个屏各写一遍：起播链上有**三处"错了就静默失效"**（转码判据、`--danmaku-match-name`
//    的编码方向、`variables` 首播不带 `--start=`），复制粘贴必然会漂移；一处实现 + 两处调用是唯一可持续的形态。
//
// ⚠️ 归属说明：`LibraryPage.PlayAsync` 里那份**保持不动**（它是 t27 的交付面与自检宿主）；
//    本文件是给"非媒体库页"的屏用的**同构第二入口**，两者共用同一套 `KernelArgumentBuilder`。

using System;
using System.Threading.Tasks;
using AIPlayer.Shell.KernelHost;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Segments;
using AIPlayer.Shell.Services.Settings;
using AIPlayer.Shell.Services.Todb;
using AIPlayer.Shell.Player;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>收藏 / 聚合视界的起播入口（复用 t27 已验证的 M3 参数面）。</summary>
public static class AggregatePlayback
{
    /// <summary>
    /// 从聚合类屏起播一个条目。失败只记日志、**不抛**（UI 不该因为起播失败而崩）。
    /// </summary>
    public static void Play(ServerConfig server, EmbyItem item)
    {
        if (server == null || item == null)
        {
            return;
        }

        // t103 取证开关：证明"起播入口仍在且真的被走到"**而不启动内核**（否则自检会把 mpv 拉起来）。
        // 默认关闭；设了必留一行日志（不允许静默跳过）。
        if (string.Equals(Environment.GetEnvironmentVariable(NoLaunchEnvVar), "1", StringComparison.Ordinal))
        {
            Program.Log("AGG PLAY selftest-suppressed item=" + item.Name + " server=" + (server.Name ?? "?")
                + "（" + NoLaunchEnvVar + "=1 ⇒ 只证明入口被走到，不启动内核）");
            return;
        }

        _ = PlayAsync(server, item);
    }

    /// <summary>见 <see cref="Play"/>：自检专用"走到入口但不启内核"的开关。</summary>
    public const string NoLaunchEnvVar = "SHELL_SELFTEST_NO_LAUNCH";

    private static async Task PlayAsync(ServerConfig server, EmbyItem item)
    {
        try
        {
            if (!SettingsService.Instance.IsLoaded)
            {
                SettingsService.Instance.Load();
            }

            var settings = SettingsService.Instance.Settings;
            var http = new ShellHttpClient();
            var emby = new EmbyService(http, server, onLog: m => Program.Log("AGG EMBY " + m));
            var session = new EmbyPlaybackSession(
                emby,
                settings,
                segmentService: new SegmentService(http),
                todbService: new TodbService(http),
                onLog: m => Program.Log("AGG SESSION " + m));

            session.SetItem(item);
            var request = await session.BuildRequestAsync(item);
            if (request == null || string.IsNullOrEmpty(request.MediaPath))
            {
                Program.Log("AGG PLAY FAIL no-url item=" + item.Name);
                return;
            }

            // 导航回调（下一集/换集/换版/刷新地址）→ 同一个会话解析 → camelCase 响应体
            ShellCallback.NavigateHandler = reply =>
            {
                try
                {
                    if (!reply.Event.AcceptsSourceReply)
                    {
                        return null;
                    }

                    var next = session.ResolveNavigationAsync(reply.Event).GetAwaiter().GetResult();
                    if (next == null || string.IsNullOrWhiteSpace(next.MediaPath))
                    {
                        return null;   // 无下一条 ⇒ 空体（内核保持现状）
                    }

                    return next.ToNavigateOptions(
                        callbackUrl: ShellCallback.Url,
                        currentEpisodeId: session.Item?.Id,
                        resumePosition: reply.Event.PositionSeconds);
                }
                catch (Exception ex)
                {
                    Program.Log("AGG NAV FAIL " + ex.GetType().Name + ": " + ex.Message);
                    return null;
                }
            };

            var launch = KernelArgumentBuilder.FromPlayback(
                request,
                settings,
                callbackUrl: ShellCallback.Url,
                libMpvPath: KernelLauncher.LibMpvPath,
                parentPid: Environment.ProcessId);

            var args = KernelArgumentBuilder.BuildArguments(launch);
            Program.Log("AGG PLAY resolved item=" + item.Name
                + " server=" + (server.Name ?? "?")
                + " needTranscode=" + session.NeedsTranscode
                + " urlLen=" + request.MediaPath.Length
                + " " + KernelArgumentBuilder.DescribeParameterSurface(launch));
            // t99：`args` 里有 `--http-header=<base64>`（内含 X-Emby-Token）⇒ **先打码再落日志**
            // （旧实现原样写进 aiplayer.log ⇒ 实测这条路径贡献 24 行裸 base64）。
            Program.Log("AGG PLAY-KERNEL-ARGS-RAW " + Infrastructure.SecretMasker.Mask(args));

            var process = KernelLauncher.Launch(launch, Program.Log);
            Program.Log(process == null
                ? "AGG PLAY LAUNCH-FAIL"
                : "AGG PLAY OK pid=" + process.Id);

            await Task.Delay(1500);
            LocalVideoPathSemantic.RestoreAfterPlayback(Program.Log);
        }
        catch (Exception ex)
        {
            Program.Log("AGG PLAY FAIL " + ex.GetType().FullName + ": " + ex.Message);
        }
    }
}

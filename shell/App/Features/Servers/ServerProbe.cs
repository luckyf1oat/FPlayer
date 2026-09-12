using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services;
using AIPlayer.Shell.Services.Models;

namespace AIPlayer.Shell.Features.Servers;

/// <summary>
/// **唯一连通探测路径**（captain 裁决③：不许出现第二条 —— 否则"按钮验的"与"真的"会静默漂移）。
///
/// 宿主：
/// <list type="bullet">
/// <item><c>ServersPage.ProbeConnectAsync</c> —— 页面上的「测试连接」按钮与 `SHELL_SELFTEST_SERVERS` 钩子（薄包装，委托到本类）。</item>
/// <item><c>AddServerDialog.ConnectAsync</c> —— 添加/编辑对话框的「提交即认证」（登录换到 token 之后走同一条）。</item>
/// </list>
///
/// 逐 Kind 各选一个**必然发请求**的实例方法：
/// Emby/Jellyfin → <c>GetViewsAsync</c>｜Navidrome → <c>PingAsync</c>｜Audiobookshelf → <c>GetLibrariesAsync</c>｜WebDAV → <c>PingAsync</c>。
/// 凭据纪律：本类**不读也不打印**密码/Token（由服务层内部经 SecureKvStore 取用）。
/// </summary>
internal static class ServerProbe
{
    /// <summary>成功返回附加说明串；失败**向上抛**（由调用方决定怎么呈现/记录）。</summary>
    public static async Task<string> ProbeAsync(ServerConfig cfg, CancellationToken ct)
    {
        using var registry = new ServiceRegistry();
        registry.Initialize();
        var logs = new List<string>();
        var log = new Action<string>(s => logs.Add(s));

        switch (cfg.Kind)
        {
            case ServerKind.Emby:
            case ServerKind.Jellyfin:
                await registry.CreateEmby(cfg, log).GetViewsAsync(ct);
                break;
            case ServerKind.Navidrome:
                await registry.CreateNavidrome(cfg, log).PingAsync(ct);
                break;
            case ServerKind.Audiobookshelf:
                await registry.CreateAudioBookshelf(cfg, log).GetLibrariesAsync(ct);
                break;
            case ServerKind.WebDav:
                await registry.CreateWebDav(cfg, log).PingAsync(ct);
                break;
            default:
                throw new NotSupportedException("未知服务器类型：" + cfg.Kind);
        }

        return logs.Count > 0 ? "；日志：" + string.Join(" / ", logs.Take(2)) : string.Empty;
    }
}

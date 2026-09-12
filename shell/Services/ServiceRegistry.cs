// 服务层组装入口（composition root）。
// 依据 DESIGN §2 D6：原版 app_providers.dart 的 provider 语义在 C# 侧由 DI 承载；
// 服务层不引用 Microsoft.Extensions.DependencyInjection（避免为纯类库引入容器依赖），
// 这里给出**显式手工组装**的等价入口，供外壳 App 直接取用（也可由 App 自己用 MS DI 包一层）。
//
// 作用：保证服务层"写而不接"不会发生 —— 每个服务都有明确的构造与获取路径。

using System;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.AudioBookshelf;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Icons;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Navidrome;
using AIPlayer.Shell.Services.Segments;
using AIPlayer.Shell.Services.Servers;
using AIPlayer.Shell.Services.Settings;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Todb;
using AIPlayer.Shell.Services.Trakt;
using AIPlayer.Shell.Services.WebDav;

namespace AIPlayer.Shell.Services;

/// <summary>服务层组装入口：统一持有 HTTP 客户端、凭据库、各服务实例。</summary>
public sealed class ServiceRegistry : IDisposable
{
    private readonly ShellHttpClient _http;
    private readonly bool _ownsHttp;

    public ServiceRegistry(
        ShellHttpClient http = null,
        SecureKvStore credentials = null,
        JsonStorage settingsStorage = null,
        JsonStorage serversStorage = null)
    {
        _ownsHttp = http == null;
        _http = http ?? new ShellHttpClient();
        Credentials = new PasswordStore(credentials ?? SecureKvStore.Instance);
        Settings = settingsStorage == null ? SettingsService.Instance : SettingsService.At(settingsStorage.FilePath);
        Servers = serversStorage == null
            ? ServerConfigStore.Instance
            : ServerConfigStore.At(serversStorage.FilePath, credentials ?? SecureKvStore.Instance);
        Icons = new IconLibraryStore(_http);
        Segments = new SegmentService(_http);
        Todb = new TodbService(_http);
    }

    public ShellHttpClient Http => _http;

    public PasswordStore Credentials { get; }

    public SettingsService Settings { get; }

    public ServerConfigStore Servers { get; }

    public IconLibraryStore Icons { get; }

    public SegmentService Segments { get; }

    public TodbService Todb { get; }

    /// <summary>
    /// 图片缓存唯一入口（默认数据根、进程级唯一）：App 侧一律经它取图，**禁止**在 <c>shell/App</c> 里再
    /// <c>new ImageCacheManager(...)</c>（第二实例 = 第二容量口径，即今晚踩过的"第二构造点"）。
    /// </summary>
    public ImageCacheManager Images => ImageCacheManager.Default;

    /// <summary>启动时一次性加载（幂等）。</summary>
    public void Initialize()
    {
        Settings.Load();
        Servers.Load();
        Icons.Load();
        ApplyProxyFromSettings();
    }

    /// <summary>
    /// 把设置里的代理应用到 HTTP 客户端（对应 S5 系统代理/内置默认 127.0.0.1:7890）。
    /// t61（ui2 取证 + 请求）：改为**推给进程级策略** <see cref="ProxyPolicy"/> —— 此前只作用于注册表自己那一个客户端，
    /// 而外壳其余 15 处 `new ShellHttpClient()`（不传 proxyUrl）全部直连（ui2 哨兵实测：注册表路径 12+ 次代理拒连，
    /// 而 `HOME IMG-OK 4/8`、`SWR-LOAD … elapsed=1004ms` 直连成功；证据 `t61-proxy-effectiveness.txt`）。
    /// 语义不变：仍由「设置 → 代理」这一个决定驱动；显式给某实例赋值（含显式 null）的路径仍优先。
    /// </summary>
    public void ApplyProxyFromSettings()
    {
        // 推送（不是 Set）：Set 是"显式覆盖"、会冻结值；Apply 让后续设置改动继续生效
        ProxyPolicy.Apply(Settings.Settings);
    }

    /// <summary>按服务器配置构造 Emby/Jellyfin 客户端（Jellyfin 与 Emby 共用同一套 API）。</summary>
    public EmbyService CreateEmby(ServerConfig server, Action<string> onLog = null)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));
        if (!server.Kind.IsEmbyFamily())
        {
            throw new ArgumentException($"服务器 {server.Name}({server.Kind.Id()}) 不是 Emby/Jellyfin 类型", nameof(server));
        }
        return new EmbyService(_http, server, onLog: onLog ?? (m => DebugLog.Info(m)));
    }

    /// <summary>取指定 id 的服务器并构造 Emby 客户端；不存在返回 <c>null</c>。</summary>
    public EmbyService CreateEmbyById(string serverId, Action<string> onLog = null)
    {
        var server = Servers.ById(serverId);
        return server == null ? null : CreateEmby(server, onLog);
    }

    /// <summary>按服务器配置构造 Navidrome / Subsonic 客户端。</summary>
    public NavidromeService CreateNavidrome(ServerConfig server, Action<string> onLog = null)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));
        if (server.Kind != ServerKind.Navidrome)
        {
            throw new ArgumentException($"服务器 {server.Name}({server.Kind.Id()}) 不是 Navidrome 类型", nameof(server));
        }
        return new NavidromeService(_http, server, onLog: onLog ?? (m => DebugLog.Info(m)));
    }

    /// <summary>取指定 id 的服务器并构造 Navidrome 客户端；不存在返回 <c>null</c>。</summary>
    public NavidromeService CreateNavidromeById(string serverId, Action<string> onLog = null)
    {
        var server = Servers.ById(serverId);
        return server == null ? null : CreateNavidrome(server, onLog);
    }

    /// <summary>按服务器配置构造 WebDAV 客户端（备份/浏览用）。</summary>
    public WebDavService CreateWebDav(ServerConfig server, Action<string> onLog = null)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));
        if (server.Kind != ServerKind.WebDav)
        {
            throw new ArgumentException($"服务器 {server.Name}({server.Kind.Id()}) 不是 WebDAV 类型", nameof(server));
        }
        return new WebDavService(_http, server, onLog ?? (m => DebugLog.Info(m)));
    }

    /// <summary>取指定 id 的服务器并构造 WebDAV 客户端；不存在返回 <c>null</c>。</summary>
    public WebDavService CreateWebDavById(string serverId, Action<string> onLog = null)
    {
        var server = Servers.ById(serverId);
        return server == null ? null : CreateWebDav(server, onLog);
    }

    /// <summary>按服务器配置构造 Audiobookshelf 客户端。</summary>
    public AudioBookshelfService CreateAudioBookshelf(ServerConfig server, Action<string> onLog = null)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));
        if (server.Kind != ServerKind.Audiobookshelf)
        {
            throw new ArgumentException($"服务器 {server.Name}({server.Kind.Id()}) 不是 Audiobookshelf 类型", nameof(server));
        }
        return new AudioBookshelfService(_http, server, onLog ?? (m => DebugLog.Info(m)));
    }

    /// <summary>取指定 id 的服务器并构造 Audiobookshelf 客户端；不存在返回 <c>null</c>。</summary>
    public AudioBookshelfService CreateAudioBookshelfById(string serverId, Action<string> onLog = null)
    {
        var server = Servers.ById(serverId);
        return server == null ? null : CreateAudioBookshelf(server, onLog);
    }

    /// <summary>
    /// 构造 Trakt 客户端。客户端凭据优先取显式参数，其次取设置里的 <c>TraktClientId</c>/<c>TraktClientSecret</c>；
    /// 均为空时由 <see cref="TraktService"/> 回落到 DPAPI 凭据库（键 <c>trakt.clientId</c>/<c>trakt.clientSecret</c>）。
    /// </summary>
    public TraktService CreateTrakt(string clientId = null, string clientSecret = null, Action<string> onLog = null)
    {
        var s = Settings.Settings;
        var id = !string.IsNullOrWhiteSpace(clientId)
            ? clientId
            : (string.IsNullOrWhiteSpace(s.TraktClientId) ? null : s.TraktClientId);
        var secret = !string.IsNullOrWhiteSpace(clientSecret)
            ? clientSecret
            : (string.IsNullOrWhiteSpace(s.TraktClientSecret) ? null : s.TraktClientSecret);

        return new TraktService(_http, Credentials, id, secret, onLog ?? (m => DebugLog.Info(m)));
    }

    /// <summary>Trakt 令牌存取（DPAPI 凭据库，含 Dart 单键 <c>trakt_token</c> 的兼容读取与迁移）。</summary>
    public TraktTokenStore CreateTraktTokenStore() => new TraktTokenStore(Credentials);

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

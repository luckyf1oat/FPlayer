using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services;
using AIPlayer.Shell.Services.AudioBookshelf;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Emby;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Servers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIPlayer.Shell.Features.Servers;

/// <summary>
/// 屏 A：添加服务器对话框（t30 / U-D），规格 = `UI_SPEC_SHELL.md` §8 + `HILLSLITE_UI_ANALYSIS.md` §4.3。
///
/// **对外契约（给外壳一行挂载；ui 已在 `MainWindow.AddServer_Click` 留好签名）**
/// <code>var ok = await Features.Servers.AddServerDialog.ShowAsync(ContentFrame.XamlRoot); if (ok) { OnServersChanged(); }</code>
/// 也可以预填一台既有服务器（右键菜单「编辑」复用本对话框）：`ShowAsync(root, existingServer)`。
///
/// **状态机**（与规格 §8「提交即认证」一致）
/// <code>
/// Idle ─失焦校验→ Invalid（行内红字，主按钮仍可点但提交会被挡下）
/// Idle ─点「登录」→ Submitting（两按钮禁用 + 状态行「正在连接 …」；输入内容不清空）
/// Submitting ─成功→ Probing（逐 Kind 选一个必然发请求的实例方法；Emby 系另有 LoginAsync 换 token）
/// Probing ─通过→ Persisting（ServerConfigStore.Add/Update + Save + PasswordStore.Save）
///            → Success：关闭对话框、返回 true（调用方刷新侧栏）
/// Submitting/Probing ─失败→ Failure（**三类可区分**：HTTP 401/403 = 可达但认证失败｜超时｜网络不可达），
///            对话框**保持打开**、输入保留，错误原文打码后既进 UI 也进日志
/// </code>
///
/// 🔴 凭据纪律：密码只经 <see cref="PasswordStore"/>（DPAPI 密文）；日志里的 `api_key=` 一律打码。
/// </summary>
public sealed partial class AddServerDialog : ContentDialog
{
    /// <summary>取证钩子（默认关闭）：不设 ⇒ 产品路径零输出。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_ADDSERVER";

    private readonly ServerConfigStore _store = ServerConfigStore.Instance;
    private readonly PasswordStore _passwords = PasswordStore.Instance;
    private readonly ServerConfig _existing;
    private bool _succeeded;

    private static readonly (ServerKind Kind, string Label)[] Kinds =
    {
        (ServerKind.Emby, "Emby"),
        (ServerKind.Jellyfin, "Jellyfin"),
        (ServerKind.Navidrome, "Navidrome（音乐）"),
        (ServerKind.Audiobookshelf, "AudioBookshelf（有声书）"),
        (ServerKind.WebDav, "WebDAV（文件/直链）"),
    };

    /// <summary>ctor 为 internal：宿主/取证工程可以直接构造实例来"立起来截图"，而产品路径只用 <see cref="ShowAsync"/>。</summary>
    internal AddServerDialog(ServerConfig existing)
    {
        InitializeComponent();
        _existing = existing;

        // ContentDialog 在**独立弹窗根**里渲染，不继承页面/窗口根节点的 RequestedTheme
        //（实测：宿主根节点设了 Dark，对话框仍是浅色）⇒ 自己声明深色，与外壳观感一致。
        RequestedTheme = ElementTheme.Dark;

        KindBox.ItemsSource = Kinds.Select(k => k.Label).ToList();
        KindBox.SelectedIndex = 0;

        try { _store.Load(); } catch (Exception ex) { Log("ADDSERVER store-load-fail " + ex.GetType().Name); }

        if (existing != null)
        {
            Title = "编辑服务器";
            PrimaryButtonText = "保存";
            KindBox.SelectedIndex = Math.Max(0, Array.FindIndex(Kinds, k => k.Kind == existing.Kind));
            NameBox.Text = existing.Name ?? string.Empty;
            UrlBox.Text = SplitUrl(existing.BaseUrl, out var port);
            PortBox.Text = port;
            UserBox.Text = existing.UserName ?? string.Empty;
            RootPathBox.Text = existing.ExtraString("rootPath") ?? string.Empty;
            PassBox.Password = existing.Kind == ServerKind.Navidrome
                ? (existing.ExtraString("password") ?? string.Empty)
                : (_passwords.GetServerPassword(existing.Id) ?? string.Empty);
        }

        ApplyKindVisibility();
    }

    /// <summary>新增：`ShowAsync(root)`；编辑：`ShowAsync(root, server)`。返回 **true = 已成功写入配置**。</summary>
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, ServerConfig existing = null)
    {
        var dialog = new AddServerDialog(existing) { XamlRoot = xamlRoot };
        await dialog.ShowAsync();
        return dialog._succeeded;
    }

    // ===================== 交互 =====================

    private void OnKindChanged(object sender, SelectionChangedEventArgs e) => ApplyKindVisibility();

    /// <summary>地址/端口任一变化 ⇒ 立刻重算「端口冲突」可见说明（不等到点登录才发现）。</summary>
    private void OnUrlOrPortChanged(object sender, TextChangedEventArgs e) => UpdatePortNote();

    private void UpdatePortNote()
    {
        var note = PortConflictNote(UrlBox.Text, PortBox.Text);
        if (note.Length == 0)
        {
            if (StatusText.Text != null && StatusText.Text.StartsWith("地址里已带端口", StringComparison.Ordinal))
            {
                StatusText.Text = string.Empty;
                StatusText.Visibility = Visibility.Collapsed;
            }
            return;
        }

        StatusText.Text = note;
        StatusText.Visibility = Visibility.Visible;
    }

    private void ApplyKindVisibility()
    {
        var kind = SelectedKind();
        var webdav = kind == ServerKind.WebDav;

        RootPathBox.Visibility = webdav ? Visibility.Visible : Visibility.Collapsed;
        // services 口径：五类**用户名都必填**（WebDAV 恒发 Basic）；早期版本对 WebDAV 放过空，已收紧。
        UserBox.PlaceholderText = "必填";

        // 口令提示按类型切换（**只切提示与必填标记，字段集不换**）：Emby/Jellyfin 支持空口令账号；WebDAV 的凭据随
        // 请求发出、提交即认证 —— 留空而服务端要认证会当场失败并回显，不会静默写入坏配置，故同样按选填处理
        var passwordOptional = kind == ServerKind.Emby || kind == ServerKind.Jellyfin || kind == ServerKind.WebDav;
        PassBox.PlaceholderText = passwordOptional
            ? (webdav ? "选填（凭据随请求发出，认证失败会当场回显）" : "选填（Emby/Jellyfin 支持空口令）")
            : "必填（" + ServerKindExtensions.DisplayName(kind) + " 不支持空口令）";
        PassBox.Header = passwordOptional ? "密码" : "密码（必填）";

        // 口径说明行：WebDAV 用户名必填 / 密码可留空，并说明留空为什么是安全的选择
        const string webDavNotePrefix = "说明：WebDAV 的用户名必填、密码可留空";
        if (webdav)
        {
            StatusText.Text = webDavNotePrefix + "：可以匿名访问的服务端请留空，需要认证的请填 —— 本屏提交即认证，"
                + "认证失败会当场提示。「匿名可读的 WebDAV」是真实使用形态，其能否匿名列举目录/取流尚待实测。";
            StatusText.Visibility = Visibility.Visible;
        }
        else if (StatusText.Text != null && StatusText.Text.StartsWith(webDavNotePrefix, StringComparison.Ordinal))
        {
            StatusText.Text = string.Empty;
            StatusText.Visibility = Visibility.Collapsed;
        }

        HintText.Visibility = Visibility.Collapsed;
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        var reveal = RevealButton.IsChecked == true;
        if (reveal)
        {
            PassPlain.Text = PassBox.Password;
            PassPlain.Visibility = Visibility.Visible;
            PassBox.Visibility = Visibility.Collapsed;
            RevealButton.Content = "隐藏";
        }
        else
        {
            PassBox.Password = PassPlain.Text;
            PassBox.Visibility = Visibility.Visible;
            PassPlain.Visibility = Visibility.Collapsed;
            RevealButton.Content = "显示";
        }
    }

    private string Password() =>
        PassBox.Visibility == Visibility.Visible ? (PassBox.Password ?? string.Empty) : (PassPlain.Text ?? string.Empty);

    private ServerKind SelectedKind() =>
        KindBox.SelectedIndex >= 0 && KindBox.SelectedIndex < Kinds.Length ? Kinds[KindBox.SelectedIndex].Kind : ServerKind.Emby;

    private async void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 异步登录必须用 deferral，否则对话框会在 await 之前就关掉。
        var deferral = args.GetDeferral();
        args.Cancel = true; // 只有成功路径才把它改回 false（= 允许关闭）
        try
        {
            var error = Validate();
            if (error != null)
            {
                ShowHint(error);
                Log("ADDSERVER invalid " + error);
                return;
            }

            SetBusy(true);
            try
            {
                var detail = await SubmitAsync();
                var isEdit = _existing != null;
                if (isEdit)
                {
                    _store.Update(LastDraft);
                }
                else
                {
                    _store.Add(LastDraft);
                }
                _store.Save();
                _passwords.Save();

                _succeeded = true;
                Log($"ADDSERVER ok edit={isEdit} id={LastDraft.Id} kind={LastDraft.Kind}"
                    + $" name=\"{LastDraft.Name}\" url={Mask(LastDraft.BaseUrl)} detail={Mask(detail)}");

                args.Cancel = false; // 允许关闭 ⇒ 调用方拿到 true 后刷新侧栏
            }
            catch (OperationCanceledException)
            {
                ShowHint("连接超时（8 秒内无响应）：" + Mask(UrlBox.Text));
                Log("ADDSERVER timeout " + Mask(UrlBox.Text));
            }
            catch (Exception ex)
            {
                // 401/403 = 可达但认证失败；其它 = 连不上。两者绝不混说。
                ShowHint(DescribeFailure(ex));
                Log($"ADDSERVER fail {ex.GetType().Name}: {Mask(ex.Message)}");
            }
            finally
            {
                SetBusy(false);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>本次提交的草稿（成功后由调用方落盘；失败路径不落盘）。</summary>
    private ServerConfig LastDraft { get; set; }

    private void SetBusy(bool busy)
    {
        IsPrimaryButtonEnabled = !busy;
        IsSecondaryButtonEnabled = !busy;
        StatusText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            StatusText.Text = "正在连接 " + Mask(UrlBox.Text) + " …";
        }
    }

    private void ShowHint(string text)
    {
        HintText.Text = text;
        HintText.Visibility = Visibility.Visible;
    }

    private string Validate() => ValidationError(
        UrlBox.Text, PortBox.Text, SelectedKind(), UserBox.Text, RootPathBox.Text, Password());

    /// <summary>
    /// 纯函数校验（**与 UI 无关**，所以能被自检直接跑出一张判定表 —— §8「地址自带校验」的可失败证据）。
    /// 返回 null = 通过。
    ///
    /// 🔴 逐类型口径来自 `services`（2026-09-12 经 captain 转达；来源 = 服务层登录方法签名 + `ServerConfig` 头注释，
    ///    **其中"Emby 空口令可用"是本机实测**）：
    ///   · Emby / Jellyfin：BaseUrl + UserName 必填；**Password 允许为空**（本机 `fx` 那台就是空口令，
    ///     `AuthenticateByName` 用 `Pw:""` 照样 200）—— **绝不能拦**，拦了会把用户真实可用的服务器挡死。
    ///   · Navidrome：BaseUrl + UserName + **Password 必填**（Subsonic token+salt 由密码派生）。
    ///   · AudioBookshelf：BaseUrl + UserName + **Password 必填**；`Extra["libraryId"]` 可空（登录后可发现）。
    ///   · WebDAV：BaseUrl（完整 URL 到目录）+ UserName + Password **选填**（凭据随请求发出，认证失败会当场回显）；
    ///     `Extra["rootPath"]` **可空**（默认用 BaseUrl 路径）。
    ///   · 端口 = **选填**且**并入 BaseUrl**（服务层没有独立端口字段；§8 契约原文也要求 BaseUrl 容纳"地址 / 地址+端口"两形态）。
    /// </summary>
    internal static string ValidationError(string rawUrl, string rawPort, ServerKind kind, string user, string rootPath, string password)
    {
        var raw = (rawUrl ?? string.Empty).Trim();
        if (raw.Length == 0) { return "服务器地址必填。"; }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "服务器地址格式不对：需要 http:// 或 https:// 开头的绝对地址。";
        }

        var portText = (rawPort ?? string.Empty).Trim();
        if (portText.Length > 0 && (!int.TryParse(portText, out var port) || port < 1 || port > 65535))
        {
            return "端口需要是 1–65535 的数字（留空 = 用地址里的端口）。";
        }

        // 用户名：五类都必填（WebDAV 恒发 Basic ⇒ 也要凭据；早期版本对 WebDAV 放过空，已按 services 口径收紧）
        if ((user ?? string.Empty).Trim().Length == 0)
        {
            return "用户名必填。";
        }

        // 口令：必填矩阵（captain 2026-09-12 **修正版**，以 WORKSPACE 事实 147 为主口径）
        //   Emby/Jellyfin **选填**（§8「兼容空口令账号」；本机那台空口令服务器已实测 —— 拦了会被自己的校验挡死）
        //   Navidrome / ABS **必填**（LoginNativeAsync / LoginAsync 都要 username+password）
        //   WebDAV **选填**（captain 最终裁定）：口令在三处登录路径里都是随请求发出的凭据，提交即认证；
        //     留空但服务端要认证 ⇒ 当场认证失败并回显错误，不会静默写入坏配置；用户名仍必填（决定认证形态）
        var needPassword = kind == ServerKind.Navidrome
            || kind == ServerKind.Audiobookshelf;
        if (needPassword && (password ?? string.Empty).Length == 0)
        {
            return ServerKindExtensions.DisplayName(kind) + " 的密码必填（当刻不支持空口令）。";
        }

        // rootPath 可空（默认用 BaseUrl 路径）⇒ 不做必填校验；仅当填了才要求以 / 开头
        var root = (rootPath ?? string.Empty).Trim();
        if (kind == ServerKind.WebDav && root.Length > 0 && !root.StartsWith("/", StringComparison.Ordinal))
        {
            return "WebDAV 根路径若填写，需要以 / 开头（例如 /dav）；留空则用地址里的路径。";
        }

        return null;
    }

    /// <summary>
    /// 取证钩子（只被宿主调用，产品路径不经过）：对着**真实 HTTP 端点**（服务层自带 `MockEmbyServer`）
    /// 走完整"提交即认证 → 写 servers.json → 写凭据库"路径，并打印**前后 diff**；收尾把自检条目删掉并
    /// 证明文件回到原样。用途 = 验收第 2 条「五字段流程可真实加进一台服务器并存盘」的可失败证据。
    /// </summary>
    internal static async Task RunAddSelfCheckAsync(string baseUrl, Action<string> log)
    {
        var store = ServerConfigStore.Instance;
        var passwords = PasswordStore.Instance;
        var file = AIPlayer.Shell.Services.Infra.AppDataDir.Instance.ServersFile;

        // 🔴 加载失败 ⇒ 自检不成立（before/after 会读到错的文件）⇒ **显式报错并中止**，绝不静默继续
        try
        {
            store.Load();
        }
        catch (Exception ex)
        {
            log($"ADDSERVER-SELFCHECK-FAIL store.Load {ex.GetType().Name}: {ex.Message}");
            return;
        }
        var before = ReadAll(file);

        var draft = new ServerConfig
        {
            Id = store.NewId(),
            Kind = ServerKind.Emby,
            BaseUrl = baseUrl,
            UserName = AIPlayer.Shell.Services.Mock.MockEmbyServer.ValidUser,
            Name = "t30 自检(Mock Emby)",
            Enabled = true,
            SortIndex = store.Servers?.Count ?? 0,
        };

        log($"ADDSERVER-ADD-BEGIN file={file} bytesBefore={before.Length} id={draft.Id} url={baseUrl}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var detail = await ConnectAsync(draft, AIPlayer.Shell.Services.Mock.MockEmbyServer.ValidPassword, cts.Token);

        passwords.SetServerPassword(draft.Id, AIPlayer.Shell.Services.Mock.MockEmbyServer.ValidPassword);
        if (!string.IsNullOrEmpty(draft.AccessToken)) { passwords.SetServerToken(draft.Id, draft.AccessToken); }
        passwords.Save();

        store.Add(draft);
        store.Save();

        var after = ReadAll(file);
        log($"ADDSERVER-ADD-OK name=\"{draft.Name}\" kind={draft.Kind} userId={draft.UserId}"
            + $" tokenLen={(draft.AccessToken ?? string.Empty).Length} detail={Mask(detail)}");
        log($"ADDSERVER-ADD-DIFF bytes {before.Length}->{after.Length} containsId={after.Contains(draft.Id)}"
            + $" containsUrl={after.Contains(baseUrl)}");

        var hasPassword = !string.IsNullOrEmpty(passwords.GetServerPassword(draft.Id));
        var hasToken = !string.IsNullOrEmpty(passwords.GetServerToken(draft.Id));
        log($"ADDSERVER-ADD-CREDENTIALS id={draft.Id} hasPassword={hasPassword} hasToken={hasToken}");

        // 反控制：删掉自检条目后文件必须回到字节原样（不给用户留脏数据）
        store.Remove(draft.Id);
        passwords.RemoveServer(draft.Id);
        store.Save();
        passwords.Save();
        var final = ReadAll(file);
        log($"ADDSERVER-ADD-CLEANUP bytes={final.Length} identicalToBefore={before == final}");
    }

    private static string ReadAll(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
        catch (Exception) { return "(unreadable)"; }   // 读不出配置原文只影响自检 diff 的可读性
    }

    /// <summary>
    /// 校验表的自检（取证用；**不驱动任何输入**）：对一组边界输入逐条打印"判定 + 原因"。
    /// 表里**必须**含「Emby 空口令 ⇒ pass」这条 —— 它是实测出来的硬事实，
    /// 也是"校验把用户可用服务器拦死"这类回归的哨兵。
    /// </summary>
    internal static void RunValidationSelfCheck(Action<string> log)
    {
        var cases = new (string Url, string Port, ServerKind Kind, string User, string Root, string Pwd, string Expect)[]
        {
            ("", "", ServerKind.Emby, "u", "", "p", "fail:地址必填"),
            ("not-a-url", "", ServerKind.Emby, "u", "", "p", "fail:格式"),
            ("ftp://x/y", "", ServerKind.Emby, "u", "", "p", "fail:格式"),
            ("http://127.0.0.1:8096", "", ServerKind.Emby, "", "", "p", "fail:用户名必填"),
            ("http://127.0.0.1:8096", "0", ServerKind.Emby, "u", "", "p", "fail:端口范围"),
            ("http://127.0.0.1:8096", "70000", ServerKind.Emby, "u", "", "p", "fail:端口范围"),
            ("http://127.0.0.1:8096", "", ServerKind.Emby, "u", "", "p", "pass"),

            // ★ 实测口径：Emby/Jellyfin **允许空口令**（本机有一台空口令服务器）⇒ 必须 pass，绝不拦
            ("https://fx.example.com/emby", "", ServerKind.Emby, "fx", "", "", "pass:空口令放行"),
            ("https://jf.example.com", "", ServerKind.Jellyfin, "u", "", "", "pass:空口令放行"),

            // Navidrome / AudioBookshelf：密码必填（services 口径）
            ("http://nas.local", "5005", ServerKind.Navidrome, "u", "", "", "fail:Navidrome 密码必填"),
            ("http://nas.local", "5005", ServerKind.Navidrome, "u", "", "p", "pass"),
            ("http://abs.local", "", ServerKind.Audiobookshelf, "u", "", "", "fail:ABS 密码必填"),
            ("http://abs.local", "", ServerKind.Audiobookshelf, "u", "", "p", "pass"),

            // WebDAV：用户名必填、**密码选填**（凭据随请求发出）；rootPath 可空、填了要以 / 开头
            ("http://nas.local/dav", "", ServerKind.WebDav, "", "", "p", "fail:WebDAV 用户名必填"),
            ("http://nas.local/dav", "", ServerKind.WebDav, "u", "", "", "pass:WebDAV 密码选填"),
            ("http://nas.local/dav", "", ServerKind.WebDav, "u", "", "p", "pass:rootPath 可空"),
            ("http://nas.local/dav", "", ServerKind.WebDav, "u", "dav", "p", "fail:rootPath 不以 / 开头"),
            ("http://nas.local/dav", "", ServerKind.WebDav, "u", "/dav", "p", "pass"),
        };

        foreach (var c in cases)
        {
            var error = ValidationError(c.Url, c.Port, c.Kind, c.User, c.Root, c.Pwd);
            var actual = error == null ? "pass" : "fail:" + error;
            var ok = error == null
                ? c.Expect.StartsWith("pass", StringComparison.Ordinal)
                : c.Expect.StartsWith("fail", StringComparison.Ordinal);
            log($"ADDSERVER-VALIDATE url=\"{c.Url}\" port=\"{c.Port}\" kind={c.Kind} user=\"{c.User}\""
                + $" root=\"{c.Root}\" pwdLen={c.Pwd.Length} expect={c.Expect} actual={actual} match={ok}");
        }

        // 归一化：地址 + 端口两形态
        log($"ADDSERVER-NORMALIZE url+port -> \"{NormalizeBaseUrl("https://emby.example.com/", "8443")}\"");
        log($"ADDSERVER-NORMALIZE url-only -> \"{NormalizeBaseUrl("https://emby.example.com/emby", "")}\"");
        log($"ADDSERVER-NORMALIZE trailing-slash -> \"{NormalizeBaseUrl("http://127.0.0.1:8096/", "")}\"");
    }


    // ===================== 提交 =====================

    private async Task<string> SubmitAsync()
    {
        var kind = SelectedKind();
        var url = NormalizeBaseUrl(UrlBox.Text, PortBox.Text);
        var password = Password();

        // 🔴 ServerConfig.With(...) **没有 kind 参数**（逐字核过 API：id/name/baseUrl/userName/userId/accessToken/
        //    iconUrl/iconData/enabled/sortIndex/hiddenLibraryIds/extra）⇒ 编辑路径直接改属性（属性是可写的），
        //    不再用 With 拼一个"看起来能改 Kind"的假象。
        var draft = _existing ?? new ServerConfig
        {
            Id = _store.NewId(),
            Enabled = true,
            SortIndex = _store.Servers?.Count ?? 0,
        };

        draft.Kind = kind;
        draft.BaseUrl = url;
        draft.UserName = (UserBox.Text ?? string.Empty).Trim();

        var typedName = (NameBox.Text ?? string.Empty).Trim();
        if (typedName.Length > 0) { draft.Name = typedName; }
        if (string.IsNullOrEmpty(draft.Name)) { draft.Name = ServerKindExtensions.DisplayName(kind) + " 服务器"; }

        if (kind == ServerKind.WebDav) { draft.SetExtra("rootPath", (RootPathBox.Text ?? string.Empty).Trim()); }

        Log($"ADDSERVER submit kind={kind} url={Mask(url)} user=\"{draft.UserName}\" passLen={password.Length}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var detail = await ConnectAsync(draft, password, cts.Token);

        // 凭据落库（DPAPI 密文；Navidrome 另存一份到 Extra 是服务层的既有语义，见 ServerConfig.cs:90-91）
        if (kind == ServerKind.Navidrome)
        {
            draft.SetExtra("password", password);
        }
        else if (password.Length > 0)
        {
            _passwords.SetServerPassword(draft.Id, password);
        }
        if (!string.IsNullOrEmpty(draft.AccessToken))
        {
            _passwords.SetServerToken(draft.Id, draft.AccessToken);
        }

        LastDraft = draft;
        return detail;
    }

    /// <summary>
    /// 「提交即认证」：逐 Kind 走**真实登录**，再用**同一条探测路径**验证可用性。
    /// 🔴 captain 裁决③：探测**必须复用** `ServersPage.ProbeConnectAsync`（`ServersPage.xaml.cs:258`），
    ///    不许另写一条 —— 否则"按钮验的"和"真的"是两条路，会静默漂移。
    ///    登录部分（Emby 换 token / ABS 换 token）是新增服务器独有的前置，探测仍是共享的那一条。
    /// </summary>
    private static async Task<string> ConnectAsync(ServerConfig cfg, string password, CancellationToken ct)
    {
        using var registry = new ServiceRegistry();
        registry.Initialize();
        var logs = new List<string>();
        var log = new Action<string>(s => logs.Add(s));

        switch (cfg.Kind)
        {
            case ServerKind.Emby:
            case ServerKind.Jellyfin:
            {
                var deviceId = SafeDeviceId();
                var auth = await EmbyService.LoginAsync(
                    registry.Http, cfg.BaseUrl, cfg.UserName, password,
                    deviceId, "AI Player", "AI Player", "1.0.0", ct);

                if (string.IsNullOrEmpty(auth.AccessToken))
                {
                    throw new InvalidOperationException("服务器没有返回 AccessToken（认证未通过）。");
                }

                cfg.AccessToken = auth.AccessToken;
                cfg.UserId = auth.UserId;
                if (!string.IsNullOrEmpty(auth.UserName)) { cfg.UserName = auth.UserName; }
                if (!string.IsNullOrEmpty(auth.ServerName) && (cfg.Name ?? string.Empty).Length == 0) { cfg.Name = auth.ServerName; }
                break;
            }

            case ServerKind.Audiobookshelf:
            {
                var auth = await AudioBookshelfService.LoginAsync(registry.Http, cfg.BaseUrl, cfg.UserName, password, ct);
                cfg.AccessToken = auth.Token;
                cfg.UserId = auth.UserId;
                if (!string.IsNullOrEmpty(auth.UserName)) { cfg.UserName = auth.UserName; }
                break;
            }

            case ServerKind.Navidrome:
                // Subsonic 认证由 username+password 派生，无 token 换取步骤
                cfg.SetExtra("password", password);
                break;

            case ServerKind.WebDav:
                // Basic 认证，无 token 概念
                break;

            default:
                throw new NotSupportedException("未知服务器类型：" + cfg.Kind);
        }

        // 共享探测（唯一路径）：Emby/Jellyfin → GetViewsAsync；Navidrome/WebDAV → PingAsync；ABS → GetLibrariesAsync
        log("PROBE shared ServerProbe.ProbeAsync kind=" + cfg.Kind);
        var detail = await ServerProbe.ProbeAsync(cfg, ct);

        if (logs.Count > 0) { detail = string.Join(" / ", logs) + (string.IsNullOrEmpty(detail) ? string.Empty : "；" + detail); }
        return detail;
    }

    private static string SafeDeviceId()
    {
        try { return new DeviceIdService(AppDataDir.Instance).GetOrCreate(); }
        catch (Exception) { return "aiplayer-rebuild"; }   // 设备 ID 取不到不应挡住自检，用固定占位
    }

    /// <summary>失败分类（§8 的「可达但认证失败」与「连不上」必须能分开说）。</summary>
    private static string DescribeFailure(Exception ex)
    {
        var message = Mask(ex.Message ?? string.Empty);
        var status = message.Contains("401") || message.Contains("403")
            || message.Contains("Unauthorized") || message.Contains("Forbidden");

        if (status)
        {
            return "可达，但认证失败（用户名/密码不对）：" + message;
        }
        if (ex is System.Net.Http.HttpRequestException || ex is System.Net.Sockets.SocketException)
        {
            return "连不上服务器（网络/地址/证书问题）：" + message;
        }
        return ex.GetType().Name + "：" + message;
    }

    // ===================== 小工具 =====================

    /// <summary>地址 + 端口归一化：容纳「地址」与「地址+端口」两形态（§8 契约原文）。</summary>
    internal static string NormalizeBaseUrl(string rawUrl, string rawPort)
    {
        var url = (rawUrl ?? string.Empty).Trim().TrimEnd('/');
        var port = (rawPort ?? string.Empty).Trim();
        if (port.Length == 0) { return url; }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) { return url; }

        // 🔴 captain 裁决：地址**已带显式端口**而端口框也填了 ⇒ **以地址为准、不静默覆盖**
        //    （静默二选一是缺陷）。冲突由 <see cref="PortConflictNote"/> 在界面上明示。
        var defaultPort = uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
        if (uri.Port != defaultPort) { return url; }

        var builder = new UriBuilder(uri) { Port = int.Parse(port) };
        return builder.Uri.ToString().TrimEnd('/');
    }

    /// <summary>地址与端口框冲突时的**可见说明**（空串 = 无冲突）。界面上必须显示，不许静默二选一。</summary>
    internal static string PortConflictNote(string rawUrl, string rawPort)
    {
        var url = (rawUrl ?? string.Empty).Trim();
        var port = (rawPort ?? string.Empty).Trim();
        if (port.Length == 0) { return string.Empty; }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) { return string.Empty; }

        var defaultPort = uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
        if (uri.Port == defaultPort) { return string.Empty; }

        return "地址里已带端口 " + uri.Port + "：以地址为准，端口框里的 " + port + " 被忽略（此处明示，不静默覆盖）。";
    }

    /// <summary>把 BaseUrl 拆成「不含端口的部分」与「显式端口」（编辑预填用）。</summary>
    private static string SplitUrl(string baseUrl, out string port)
    {
        port = string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl)) { return string.Empty; }
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)) { return baseUrl; }

        var defaultPort = uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
        if (uri.Port == defaultPort) { return uri.GetLeftPart(UriPartial.Path).TrimEnd('/'); }

        port = uri.Port.ToString();
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').Replace(":" + uri.Port, string.Empty);
    }

    internal static string Mask(string s) => Program.MaskSecrets(s);

    private static void Log(string line)
    {
        try { Program.Log(line); } catch (Exception) { }   // 写不出去只影响取证可读性，不得打断自检流程
    }
}

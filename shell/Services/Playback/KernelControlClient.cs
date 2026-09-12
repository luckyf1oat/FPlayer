// E-P2（t39）：**外壳侧的内核控制客户端** + 状态回推接入点。
//
// 契约（实证来源：kernel/src/WinUISample.Services/PlayerUpdateServer.cs:56-210、shell/docs/LIVE_CONTROL_P1.md §1）：
//   · 端点地址 = 内核在 `progress` 回调体里给出的 `updateUrl`，形如 `http://127.0.0.1:{port}/segments`
//     ⇒ 控制同为该端点的 `/control`（**同族**：同一进程、同一端口、同一次性 token 语义、只监听回环）。
//   · `POST /control`，体 = `{"Commands":[ … ]}`（外层 PascalCase）。
//   · 返回码：`204` 成功 ｜ `400` 载荷/语义错误（体含 `index`/`name`/`error`）｜ `404` 路径错 ｜ `405` 方法错 ｜ `500` 内核侧异常。
//
// 🔴 **降级设计（t40 的判据来源）**：本客户端**不抛异常、不假装成功** —— 端点不可达/超时/非预期码
//    一律返回明确的 <see cref="KernelControlOutcome"/> + `StatusCode=0` + 原因文本，调用方据此走「重启续接 / 请在播放器窗口内切换」。
// 🔴 **只改内核不改外壳 UI**：本文件不持有任何"影子状态"，收到的状态由调用方**只回显**（t39 卡面 §3）。

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.Playback;

/// <summary>一次 `/control` 调用的判定结果（不抛异常）。</summary>
public enum KernelControlOutcome
{
    /// <summary>`204` —— 内核已把命令落到 mpv 写点。</summary>
    Success = 0,

    /// <summary>`400` —— 载荷不合法 / 未知命令 / 不存在的 track id（内核**不得**静默）。</summary>
    Rejected = 1,

    /// <summary>端点不存在（`404`）—— 通常意味着 updateUrl 已过期（上一轮播放的进程已退出）。</summary>
    NotFound = 2,

    /// <summary>方法错（`405`）—— 我们把非 POST 发出去了（实现 bug 的信号，不是运行期常态）。</summary>
    MethodNotAllowed = 3,

    /// <summary>`401`/`403` —— 一次性 token 失配。</summary>
    Unauthorized = 4,

    /// <summary>`5xx` —— 内核侧异常（命令可能部分生效，调用方应重读状态）。</summary>
    ServerError = 5,

    /// <summary>非预期状态码。</summary>
    UnexpectedStatus = 6,

    /// <summary>**未收到 updateUrl** ⇒ 根本没有端点可发（"重启续接/窗口内切换"的唯一正当理由）。</summary>
    NoEndpoint = 7,

    /// <summary>端点不可达 / 连接被拒 / 超时（进程不在或不接受回环连接）。</summary>
    EndpointUnreachable = 8,
}

/// <summary>`/control` 调用结果。</summary>
public sealed class KernelControlResult
{
    public KernelControlOutcome Outcome { get; set; } = KernelControlOutcome.NoEndpoint;

    /// <summary>HTTP 状态码；`0` = 未拿到响应（不可达/无端点）。</summary>
    public int StatusCode { get; set; }

    /// <summary>是否真的成功（只有 `204` 才是 true）。</summary>
    public bool Success => Outcome == KernelControlOutcome.Success && StatusCode == 204;

    /// <summary>失败原因（内核 400 体的 `error` 字段；否则为本地判定文本）。**绝不写"命令成功"**。</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>内核 400 体里的第几条（`index`）。</summary>
    public int? ErrorIndex { get; set; }

    /// <summary>出错的命令名（内核 400 体的 `name`）。</summary>
    public string ErrorCommand { get; set; } = string.Empty;

    /// <summary>实际发往的地址（便于日志/证据；**不含任何凭据**）。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>请求体原文（证据用）。</summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>响应体原文（证据用，通常为空或内核错误体）。</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>端点是否可达（不可达 ⇒ 调用方必须走降级链）。</summary>
    public bool IsReachable => Outcome != KernelControlOutcome.NoEndpoint && Outcome != KernelControlOutcome.EndpointUnreachable;

    /// <summary>可直接展示给用户的中文说明（t40 降级文案的素材；不含"成功"字样的失败兜底）。</summary>
    public string UserFacingFailure
    {
        get
        {
            switch (Outcome)
            {
                case KernelControlOutcome.Success:
                    return string.Empty;
                case KernelControlOutcome.NoEndpoint:
                    return "内核未提供控制端点（本次播放的内核版本或启动参数不支持实时切换）";
                case KernelControlOutcome.EndpointUnreachable:
                    return "控制端点不可达（播放进程可能已退出）";
                case KernelControlOutcome.Rejected:
                    return string.IsNullOrEmpty(Error) ? "内核拒绝该命令" : "内核拒绝该命令：" + Error;
                case KernelControlOutcome.NotFound:
                    return "控制端点地址已失效（本轮播放已结束）";
                case KernelControlOutcome.Unauthorized:
                    return "控制端点鉴权失败";
                case KernelControlOutcome.ServerError:
                    return "内核执行命令时出错";
                default:
                    return $"控制端点返回非预期状态（HTTP {StatusCode}）";
            }
        }
    }

    public override string ToString()
        => Success
            ? $"204 ok ｜ endpoint={Endpoint} ｜ body={RequestBody}"
            : $"{Outcome} ｜ status={StatusCode} ｜ endpoint={Endpoint} ｜ error={Error} ｜ body={RequestBody}";
}

/// <summary>
/// 内核控制客户端。**一个内核进程一个实例**（updateUrl 与进程一一对应）。
/// 用法（SEAM①）：`var live = KernelPlaybackBridge.FromProgressBody(progressBody, http);` ⇒ 托盘/详情页按钮调 `live.SendAsync(...)`。
/// </summary>
public sealed class KernelControlClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly object _gate = new object();

    public KernelControlClient(HttpClient http = null, KernelControlSchema schema = KernelControlSchema.TypedKind, TimeSpan? timeout = null)
    {
        _ownsHttp = http == null;
        _http = http ?? new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        Schema = schema;
    }

    /// <summary>载荷形态（默认内核 rev1 的 `name`/`value`；卡面 schema 需内核先支持，见 KernelControlCommand 首注）。</summary>
    public KernelControlSchema Schema { get; }

    /// <summary>内核给出的一次性控制端点（`…/segments`）。空 = 未收到（⇒ <see cref="KernelControlOutcome.NoEndpoint"/>）。</summary>
    public string UpdateUrl { get; private set; } = string.Empty;

    /// <summary>最近一次解析到的回推状态（**只读快照，仅用于回显**）。</summary>
    public KernelPlaybackState LastState { get; private set; }

    public bool HasEndpoint => !string.IsNullOrWhiteSpace(UpdateUrl);

    /// <summary>显式注入端点（日志线索/测试用；常规路径是 <see cref="ApplyProgressBody"/>）。</summary>
    public void SetUpdateUrl(string updateUrl)
    {
        lock (_gate)
        {
            UpdateUrl = updateUrl ?? string.Empty;
        }
    }

    /// <summary>
    /// 接一次内核回调体（`progress`）⇒ 记录 `updateUrl` + 更新只读状态快照。
    /// 返回解析出的状态；非法/空体返回 null 且**不改动**已有端点（不得因一次坏载荷丢掉端点）。
    /// </summary>
    public KernelPlaybackState ApplyProgressBody(string body)
    {
        var state = KernelPlaybackState.FromBody(body);
        if (state == null)
        {
            return null;
        }
        lock (_gate)
        {
            if (state.HasUpdateUrl)
            {
                UpdateUrl = state.UpdateUrl;
            }
            LastState = state;
        }
        return state;
    }

    /// <summary>兼容事件名不定的回调（含 `stopped`）：只在 `progress`/`stopped` 上取状态，其余返回 null。</summary>
    public KernelPlaybackState ApplyCallbackBody(string eventName, string body)
    {
        if (!string.Equals(eventName, "progress", StringComparison.Ordinal)
            && !string.Equals(eventName, "stopped", StringComparison.Ordinal))
        {
            return null;
        }
        return ApplyProgressBody(body);
    }

    /// <summary>`{updateUrl}` → `{同端点}/control`（不动宿主、不动端口、不动查询串）。</summary>
    public bool TryResolveControlUri(out string uri, out string error)
    {
        uri = string.Empty;
        error = string.Empty;
        var current = UpdateUrl;
        if (string.IsNullOrWhiteSpace(current))
        {
            error = "no-update-url";
            return false;
        }
        if (!Uri.TryCreate(current, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttp)
        {
            error = "bad-update-url: " + current;
            return false;
        }
        var path = parsed.AbsolutePath;
        var slash = path.LastIndexOf('/');
        var directory = slash >= 0 ? path.Substring(0, slash + 1) : "/";
        var builder = new UriBuilder(parsed)
        {
            Path = directory + "control",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        uri = builder.Uri.ToString();
        return true;
    }

    public Task<KernelControlResult> SendAsync(KernelControlCommand command, CancellationToken cancellationToken = default)
        => SendAsync(new[] { command }, cancellationToken);

    /// <summary>
    /// 发一批命令（**串行语义由内核保证**：任一失败即整批 400）。本方法**不抛异常**：所有失败都编码进 <see cref="KernelControlResult"/>。
    /// </summary>
    public async Task<KernelControlResult> SendAsync(System.Collections.Generic.IReadOnlyList<KernelControlCommand> commands, CancellationToken cancellationToken = default)
    {
        var result = new KernelControlResult();
        if (commands == null || commands.Count == 0)
        {
            result.Outcome = KernelControlOutcome.Rejected;
            result.Error = "empty-commands";
            return result;
        }
        if (!TryResolveControlUri(out var uri, out var uriError))
        {
            result.Outcome = uriError == "no-update-url" ? KernelControlOutcome.NoEndpoint : KernelControlOutcome.EndpointUnreachable;
            result.Error = uriError;
            return result;
        }

        var payload = KernelControlPayload.Build(commands, Schema);
        result.Endpoint = uri;
        result.RequestBody = payload;

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
            result.Outcome = MapStatus(response.StatusCode, result);
            if (result.Success)
            {
                result.Error = string.Empty;
            }
            else if (string.IsNullOrEmpty(result.Error))
            {
                result.Error = "http-" + result.StatusCode;
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.Outcome = KernelControlOutcome.EndpointUnreachable;
            result.StatusCode = 0;
            result.Error = "timeout";
            return result;
        }
        catch (HttpRequestException ex)
        {
            result.Outcome = KernelControlOutcome.EndpointUnreachable;
            result.StatusCode = 0;
            result.Error = "unreachable: " + ex.Message;
            return result;
        }
        catch (Exception ex)
        {
            result.Outcome = KernelControlOutcome.EndpointUnreachable;
            result.StatusCode = 0;
            result.Error = "send-failed: " + ex.Message;
            return result;
        }
    }

    private static KernelControlOutcome MapStatus(HttpStatusCode status, KernelControlResult result)
    {
        switch ((int)status)
        {
            case 204:
                return KernelControlOutcome.Success;
            case 400:
                ParseErrorBody(result);
                return KernelControlOutcome.Rejected;
            case 401:
            case 403:
                return KernelControlOutcome.Unauthorized;
            case 404:
                return KernelControlOutcome.NotFound;
            case 405:
                return KernelControlOutcome.MethodNotAllowed;
            default:
                if ((int)status >= 500)
                {
                    return KernelControlOutcome.ServerError;
                }
                return KernelControlOutcome.UnexpectedStatus;
        }
    }

    /// <summary>解析内核错误体：`{"index":1,"kind":"SetSpeed","error":"bad-number"}`（`kind` 缺省时回落到旧键 `name`）。</summary>
    private static void ParseErrorBody(KernelControlResult result)
    {
        var body = result.ResponseBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }
        var root = Util.JsonRead.FromNode(body);
        if (root == null)
        {
            result.Error = body.Length > 200 ? body.Substring(0, 200) : body;
            return;
        }
        result.Error = Util.JsonRead.Str(root, "error");
        // 契约（Captain 裁定 2026-09-11）：错误体键为 `kind`；`name` 是 t38 过渡期的旧键，仅作回落。
        result.ErrorCommand = Util.JsonRead.Str(root, "kind");
        if (string.IsNullOrEmpty(result.ErrorCommand))
        {
            result.ErrorCommand = Util.JsonRead.Str(root, "name");
        }
        var index = Util.JsonRead.IntOrNull(root, "index");
        if (index.HasValue)
        {
            result.ErrorIndex = index.Value;
        }
        if (string.IsNullOrEmpty(result.Error))
        {
            result.Error = body.Length > 200 ? body.Substring(0, 200) : body;
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}

/// <summary>
/// **SEAM①（App 侧接线契约，一行）**：外壳收到内核 `progress` 回调体后
/// ⇒ `var live = KernelPlaybackBridge.FromProgressBody(body, http);` ⇒ 播放中控制入口（t40 托盘菜单）只调 `live.SendAsync(...)`。
/// 除此之外 App 侧**不需要**知道 `/control`、端口、外层键大小写等任何细节；`live` 不可用时按
/// <see cref="KernelControlResult.UserFacingFailure"/> 走「重启续接 / 请在播放器窗口内切换」降级。
/// </summary>
public static class KernelPlaybackBridge
{
    /// <summary>可直接抄进 App 侧注释的一行契约（保持与实现同步的唯一字符串）。</summary>
    public const string SeamLine =
        "SEAM①: AppViewModel 收到 \"progress\" 回调体 ⇒ KernelPlaybackBridge.FromProgressBody(body, http) ⇒ _live；播放中控制命令 ⇒ _live.SendAsync(...)（失败按 KernelControlResult.UserFacingFailure 显式降级）";

    /// <summary>从一次 `progress` 回调体建客户端（未携带 updateUrl 时返回的客户端 <see cref="KernelControlClient.HasEndpoint"/>=false）。</summary>
    public static KernelControlClient FromProgressBody(string progressBody, HttpClient http = null, KernelControlSchema schema = KernelControlSchema.TypedKind)
    {
        var client = new KernelControlClient(http, schema);
        client.ApplyProgressBody(progressBody);
        return client;
    }

    /// <summary>同一播放会话内复用：把新的回调体喂给既有客户端（端点变更则随之更新）。</summary>
    public static KernelPlaybackState UpdateFromProgressBody(KernelControlClient client, string progressBody)
        => client == null ? null : client.ApplyProgressBody(progressBody);
}

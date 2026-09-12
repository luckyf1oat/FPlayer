// 原版文件（rebuild/ 中不存在 `friendly_error.dart`），按 DESIGN §4.2 映射表
// （`src/core/utils/friendly_error.dart` → `Services/Util/FriendlyError.cs`；
// 说明栏「网络错误友好文案（`GAP_AUDIT §4`）」）新写。
//
// 输入面是本层既有的异常类型：`ShellHttpException`（含 `StatusCode`）/ `OperationCanceledException`
// （用户取消 vs 超时——`ShellHttpClient` 超时抛的是 <c>请求超时: …</c> 形态的 ShellHttpException）
// / `HttpRequestException` / `SocketException` 等 .NET 网络异常。
// 文案口径：中文可读提示 + **保留原始细节**（`Detail`），便于日志排查；不做网络请求、不吞异常类型信息。
// 诚实标注：原版 `friendly_error.dart` 的具体文案无字符串实证 ⇒ 此处为语义等价的中文文案实现。

using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using AIPlayer.Shell.Services.Http;

namespace AIPlayer.Shell.Services.Util;

/// <summary>网络/服务错误的友好文案（对应原版 `friendly_error.dart`）。</summary>
public static class FriendlyError
{
    /// <summary>
    /// 把任意异常映射为一句中文可读提示。永不抛异常、永不为 null 或空串。
    /// </summary>
    public static string Describe(Exception error)
    {
        if (error == null) return "未知错误";

        if (error is OperationCanceledException canceled)
        {
            // ShellHttpClient 的超时被包装成 ShellHttpException；走到这里通常是真的被取消
            return canceled.CancellationToken.IsCancellationRequested ? "请求已取消" : "请求超时，请检查网络或服务器状态";
        }

        if (error is ShellHttpException http)
        {
            return Describe(http.Message, http.StatusCode);
        }

        if (error is HttpRequestException request)
        {
            var kind = DescribeNetworkException(request);
            if (kind.Length > 0) return kind;
            return "网络请求失败：" + Trim(request.Message);
        }

        if (error is SocketException socket) return DescribeSocket(socket);

        if (error is AuthenticationException) return "安全连接失败：服务器证书不受信任（可在设置中改用 http 或更换证书）";

        if (error is TimeoutException) return "请求超时，请检查网络或服务器状态";

        if (error is UriFormatException) return "服务器地址格式不正确，请检查「http://主机:端口」写法";

        if (error is System.Text.Json.JsonException) return "服务器返回的数据无法解析（可能不是 Emby/Jellyfin 服务）";

        var network = DescribeNetworkException(error);
        return network.Length > 0 ? network : Trim(error.Message);
    }

    /// <summary>按 HTTP 状态码给出中文文案（无状态码时回落到消息解析）。</summary>
    public static string Describe(string message, int? statusCode)
    {
        switch (statusCode)
        {
            case 400:
                return "请求参数有误（400），请检查服务器地址与账号信息";
            case 401:
                return "认证失败（401）：用户名/密码错误，或令牌已失效，请重新登录";
            case 403:
                return "没有权限访问该内容（403）";
            case 404:
                return "接口不存在（404）：请确认该地址是 Emby/Jellyfin 服务器，且版本受支持";
            case 405:
                return "服务端不接受该请求方法（405）：服务端版本可能不兼容";
            case 408:
                return "服务端请求超时（408），请稍后重试";
            case 429:
                return "请求过于频繁（429），请稍后重试";
            case 502:
                return "网关错误（502）：反向代理未取到后端服务";
            case 503:
                return "服务不可用（503）：服务器可能正在启动或维护";
            case 504:
                return "网关超时（504）：反向代理等待后端超时";
            case 500:
            case 501:
            case 505:
                return $"服务端错误（{statusCode}），请查看服务端日志";
        }

        if (statusCode.HasValue && statusCode.Value >= 500) return $"服务端错误（{statusCode.Value}），请稍后重试";

        // 无状态码：按消息形态判断（ShellHttpClient 的超时文案是「请求超时: …」）
        var text = message ?? string.Empty;
        if (text.Contains("请求超时", StringComparison.Ordinal)
            || text.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "请求超时，请检查网络或服务器状态";
        }
        return text.Length == 0 ? "未知错误" : Trim(text);
    }

    /// <summary>按状态码给出文案（便捷重载）。</summary>
    public static string DescribeStatus(int statusCode) => Describe(null, statusCode);

    /// <summary>是否为「网络类」错误（可重试）。</summary>
    public static bool IsNetworkError(Exception error)
    {
        if (error == null) return false;
        if (error is ShellHttpException http)
        {
            // 4xx 是客户端问题，不该盲目重试；5xx/无状态码（连接失败/超时）可重试
            return !http.StatusCode.HasValue || http.StatusCode.Value >= 500;
        }
        if (error is OperationCanceledException canceled) return !canceled.CancellationToken.IsCancellationRequested;
        return DescribeNetworkException(error).Length > 0;
    }

    /// <summary>是否为认证失败（需要重新登录）。</summary>
    public static bool IsAuthError(Exception error)
        => error is ShellHttpException http && (http.StatusCode == 401 || http.StatusCode == 403);

    /// <summary>短文案（截断到 <paramref name="max"/> 字符），用于状态栏/Toast。</summary>
    public static string Short(Exception error, int max = 60) => TextUtils.Truncate(Describe(error), max);

    private static string DescribeNetworkException(Exception error)
    {
        var message = error?.Message ?? string.Empty;
        if (message.Length == 0) return string.Empty;

        if (message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            || message.Contains("由于目标计算机积极拒绝", StringComparison.Ordinal))
        {
            return "无法连接服务器：目标主机拒绝连接，请确认服务已启动且端口正确";
        }
        if (message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
            || message.Contains("nodename nor servname", StringComparison.OrdinalIgnoreCase))
        {
            return "无法解析服务器域名，请检查网络或改用 IP 地址";
        }
        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "连接超时，请检查网络或服务器状态";
        }
        if (message.Contains("The SSL connection could not be established", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
            || message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            return "安全连接失败：服务器证书不受信任（可在设置中改用 http 或更换证书）";
        }
        if (message.Contains("proxy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("代理", StringComparison.Ordinal))
        {
            return "代理连接失败，请在设置中关闭代理或改用直连";
        }
        if (message.Contains("network is unreachable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Network is down", StringComparison.OrdinalIgnoreCase))
        {
            return "网络不可达，请检查本机网络连接";
        }
        return string.Empty;
    }

    private static string DescribeSocket(SocketException socket)
        => socket.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => "无法连接服务器：目标主机拒绝连接，请确认服务已启动且端口正确",
            SocketError.HostNotFound => "无法解析服务器域名，请检查网络或改用 IP 地址",
            SocketError.TimedOut => "连接超时，请检查网络或服务器状态",
            SocketError.NetworkUnreachable => "网络不可达，请检查本机网络连接",
            SocketError.ConnectionReset => "连接被服务器重置，请稍后重试",
            SocketError.AccessDenied => "连接被本机网络策略拒绝，请检查防火墙或代理设置",
            _ => "网络错误：" + Trim(socket.Message),
        };

    private static string Trim(string message)
    {
        var text = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (text.Length == 0) return "未知错误";
        return text.Length > 200 ? text.Substring(0, 200) + "…" : text;
    }
}

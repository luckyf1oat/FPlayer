// S5 体验设施（t33）：外部 mpv 的 IPC 约定 —— mpv 的 `--input-ipc-server=<管道名>` 走 Windows 命名管道，
// 本类负责管道名生成与"发一条 JSON 命令"的最小客户端。
// ⚠️ 诚实边界：本文件**未在本机跑通外部 mpv**（机器上没有外部 mpv 可执行文件）⇒ 只有管道名与协议形态，
//    端到端连通性标注为「未验证」。走内置 libmpv 时本类不参与。
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.ExternalMpv;

/// <summary>外部 mpv 的命名管道地址与最小命令客户端。</summary>
public static class ExternalMpvIpc
{
    /// <summary>按实例名生成管道名（mpv 的 <c>--input-ipc-server=\\.\pipe\&lt;name&gt;</c>）。</summary>
    public static string PipePath(string instanceName)
    {
        var name = string.IsNullOrWhiteSpace(instanceName) ? "aiplayer-mpv" : instanceName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return @"\\.\pipe\" + name;
    }

    /// <summary>内核/外壳启动外部 mpv 时要带的参数（IPC 管道）。</summary>
    public static string InputIpcArgument(string instanceName) => "--input-ipc-server=" + PipePath(instanceName);

    /// <summary>
    /// 发一条 mpv JSON IPC 命令（一行一条，以 <c>\n</c> 结束）。返回 mpv 的响应原文；连不上/超时 ⇒ <c>null</c>。
    /// </summary>
    public static async Task<string> TrySendAsync(string pipePath, string commandJson, int timeoutMs = 1500, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pipePath) || string.IsNullOrWhiteSpace(commandJson)) return null;
        var name = pipePath.StartsWith(@"\\.\pipe\", StringComparison.Ordinal) ? pipePath.Substring(9) : pipePath;

        try
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            await pipe.ConnectAsync(timeoutMs, cts.Token).ConfigureAwait(false);

            var payload = Encoding.UTF8.GetBytes(commandJson.TrimEnd('\n') + "\n");
            await pipe.WriteAsync(payload, 0, payload.Length, cts.Token).ConfigureAwait(false);
            await pipe.FlushAsync(cts.Token).ConfigureAwait(false);

            var buffer = new byte[4096];
            var read = await pipe.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
            return read <= 0 ? null : Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\n');
        }
        catch (Exception)
        {
            // 没连上（没起外部 mpv）= 正常路径，不抛
            return null;
        }
    }

    /// <summary>便捷命令：设置属性。<c>{"command":["set_property","volume",50]}</c></summary>
    public static string SetPropertyCommand(string property, object value)
        => "{\"command\":[\"set_property\",\"" + property + "\"," + FormatValue(value) + "]}";

    private static string FormatValue(object value)
    {
        switch (value)
        {
            case null: return "null";
            case bool b: return b ? "true" : "false";
            case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            default: return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

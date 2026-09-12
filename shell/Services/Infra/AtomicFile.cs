// t148：**全工程唯一的"状态文件写入"实现**（tmp + 覆盖式 Move）。
//
// 为什么要有这个文件（同一事实只留一个构造点）：
//   `t140` 面的代码 review 实测 —— 同一个仓库里对"状态文件"存在**两种写盘纪律**：
//     · 原子形态（tmp + Move）：`JsonStorage`、`SecureKvStore`、`DiskCacheStore` 各自手写了一遍（3 处实现）；
//     · 非原子形态（就地覆盖）：`DeviceIdService:37/62/74`、`ConfigPortService:119`、
//       `AudioPlaybackStateStore:97`、`ExternalMpvService:74` 共 5 处 `File.WriteAllText` 直写。
//   非原子的后果：进程在写中途被杀 / 磁盘满 ⇒ 目标文件**半截**；而 `DeviceIdService.TryRead` 当时只判长度，
//   半截设备 ID 会被当成合法值接受（服务端视作新设备）——这是那张 review 里**唯一用户可感**的一条。
//   ⇒ 本类把这些写点全部收敛到**一个实现**，并统一采用 `File.Move(tmp, path, overwrite: true)`：
//     相比旧的"先 Delete 再 Move"还少了一个窗口（Delete 成功、Move 前崩溃 ⇒ 目标文件直接消失）。
//
// 语义（与既有 3 处保持逐条一致）：
//   · 目录不存在 ⇒ 自动建（调用方不必各自 `Directory.CreateDirectory`）；
//   · 文本编码 = **UTF-8 无 BOM**（本仓证据件/配置件的既有形态，门禁与工具都按这个读）；
//   · 失败时**不留半截目标**：写发生在 `*.tmp` 上，只有完整写成功才 Move 到目标路径；
//   · 不吞异常：写失败 ⇒ 向上抛（由调用方按各自语义处理，例如凭据库要显式失败）。
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>状态文件的原子写入（唯一实现）。见文件头注释。</summary>
public static class AtomicFile
{
    /// <summary>临时文件后缀（与既有实现一致：目标路径 + `.tmp`）。</summary>
    public const string TmpSuffix = ".tmp";

    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>原子写文本（UTF-8 无 BOM）。</summary>
    public static void WriteAllText(string path, string text)
        => WriteAllBytes(path, Utf8NoBom.GetBytes(text ?? string.Empty));

    /// <summary>原子写字节。</summary>
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        EnsureDirectory(path);
        var tmp = path + TmpSuffix;
        File.WriteAllBytes(tmp, bytes ?? Array.Empty<byte>());
        MoveOverwrite(tmp, path);
    }

    /// <summary>原子写文本，**可取消**（t148：取消令牌真的走到 IO 上，不只是加个参数）。</summary>
    public static async Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken = default)
        => await WriteAllBytesAsync(path, Utf8NoBom.GetBytes(text ?? string.Empty), cancellationToken).ConfigureAwait(false);

    /// <summary>原子写字节，**可取消**。</summary>
    public static async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

        cancellationToken.ThrowIfCancellationRequested();   // 边界①：进入即查（已取消 ⇒ 连 tmp 都不建）
        EnsureDirectory(path);
        var tmp = path + TmpSuffix;

        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            await fs.WriteAsync(bytes ?? Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();   // 边界②：Move 之前再查（避免"取消后仍然覆盖目标"）
        MoveOverwrite(tmp, path);
    }

    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static void MoveOverwrite(string tmp, string target)
        => File.Move(tmp, target, overwrite: true);
}

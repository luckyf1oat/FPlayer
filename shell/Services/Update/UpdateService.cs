// 等价移植：rebuild/ai_player/lib/core/services/update_service.dart（Dart `UpdateInfo` + `UpdateService`，282 行）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §7「自更新与社区」——
//   版本清单 GET https://aiplayer.arkhamimp.qzz.io/downloads/version.json；
//   上报前缀 /aiplayer/report/（遥测/错误上报，配 debug_log）；
//   社区 https://t.me/aiemby、赞助 https://afdian.com/a/aiplayer。
// 语义保持：拉清单/检查更新/语义化比较/构造下载地址/流式下载/解压/热替换脚本，以及 **P1 零调用项 report()**。
// 与 Dart 的差异（均为 C# 侧适配，逐条标注）：
//   ① Dart `File`/`Directory` 参数改成路径字符串（本工程服务层统一用路径字符串，见 JsonStorage/AppDataDir）；
//   ② 下载与 Dart 一致走**独立原始 HttpClient**（Dart 侧同样是 `HttpClient()..userAgent = 'AI Player'`），
//      因此不经 ShellHttpClient 的代理设置 —— 与原版行为一致；
//   ③ `Platform.*` / `Platform.version` 在 C# 侧不存在，诊断信息用 RuntimeInformation 取值，**键名沿用原版 schema**；
//   ④ 下载文件名过 TextUtils.SanitizeFileName（防清单里的 url 带路径穿越写出目标目录）；
//   ⑤ 解压/脚本执行保持 Dart 的 `tar.exe` → PowerShell `Expand-Archive` 二级回落，零第三方依赖。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Http;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Update;

/// <summary>版本清单条目（对应 Dart <c>UpdateInfo</c>）。</summary>
public sealed class UpdateInfo
{
    public string Version { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string PublishedAt { get; set; } = string.Empty;

    public bool Mandatory { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    /// <summary>包大小（字节）——大整数，走 <see cref="JsonRead.LongOrNull"/>。</summary>
    public long SizeBytes { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>等价 Dart <c>hasDownload</c>。</summary>
    public bool HasDownload => (DownloadUrl ?? string.Empty).Trim().Length > 0;

    /// <summary>
    /// 等价 Dart <c>UpdateInfo.fromJson</c>：字段名在不同版本清单里可能是
    /// <c>version</c>/<c>tag_name</c>/<c>name</c>、<c>url</c>/<c>download_url</c>/<c>windows</c>/<c>file</c> 等。
    /// Dart 的 <c>??</c> 只在 null 时回落（**空串不回落**），<c>FirstString</c> 保持同一语义。
    /// 未实证，容错解析：<c>size</c> 走 LongOrNull（字符串数字也认）。
    /// </summary>
    public static UpdateInfo FromJson(JsonElement json)
    {
        var url = FirstString(json, "url", "download_url", "windows", "file");
        return new UpdateInfo
        {
            Version = FirstString(json, "version", "tag_name", "name"),
            DownloadUrl = url,
            Notes = FirstString(json, "notes", "body", "changelog"),
            PublishedAt = FirstString(json, "published_at", "date", "time"),
            // 等价 Dart `json['mandatory'] as bool? ?? json['force'] as bool? ?? false`（严格 bool，不做字符串转换）。
            Mandatory = StrictBool(json, "mandatory") ?? StrictBool(json, "force") ?? false,
            Sha256 = FirstString(json, "sha256"),
            SizeBytes = JsonRead.LongOrNull(json, "size") ?? 0,
            FileName = url.Length == 0 ? string.Empty : url.Split('/').Last(),
        };
    }

    /// <summary>取第一个「存在且非 null」的成员并转成字符串（等价 Dart 的 `??` 链）。</summary>
    private static string FirstString(JsonElement json, params string[] names)
    {
        if (json.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var name in names)
        {
            if (!json.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined) continue;
            return JsonRead.AsString(value);
        }
        return string.Empty;
    }

    /// <summary>等价 Dart 的 `as bool?`：只有真布尔才认，其余（含 "true"）返回 null。</summary>
    private static bool? StrictBool(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }
}

/// <summary>自更新检查 + 社区链接 + 上报（对应 Dart <c>UpdateService</c>）。</summary>
public sealed class UpdateService
{
    /// <summary>版本清单（SERVICE_API §7 实证）。</summary>
    public const string VersionManifestUrl = "https://aiplayer.arkhamimp.qzz.io/downloads/version.json";

    public const string TelegramUrl = "https://t.me/aiemby";

    public const string SponsorUrl = "https://afdian.com/a/aiplayer";

    /// <summary>遥测/错误上报前缀（SERVICE_API §7 `[E]` 实证）。</summary>
    public const string ReportUrlPrefix = "/aiplayer/report/";

    /// <summary>写盘用 UTF-8 无 BOM（与 Dart <c>writeAsString</c> 一致）。</summary>
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    private static readonly Regex VersionSeparator = new Regex(@"[.\-+]", RegexOptions.Compiled);

    private static readonly Regex LeadingDigits = new Regex(@"^[0-9]+", RegexOptions.Compiled);

    public UpdateService(ShellHttpClient http, Action<string> onLog = null, string currentVersion = "1.0.0")
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        OnLog = onLog;
        CurrentVersion = currentVersion ?? "1.0.0";
    }

    public ShellHttpClient Http { get; }

    public Action<string> OnLog { get; }

    public string CurrentVersion { get; }

    private void Log(string message) => OnLog?.Invoke(message);

    // ── 版本检查 ───────────────────────────────────────────────────────────

    /// <summary>拉取版本清单（Dart <c>fetchLatest</c>）；失败返回 <c>null</c>、只记日志（不阻塞启动）。</summary>
    public async Task<UpdateInfo> FetchLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Http.GetAsync(
                VersionManifestUrl,
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var json = result.JsonMap;
            if (json == null) return null;
            return UpdateInfo.FromJson(json.Value);
        }
        catch (Exception ex)
        {
            Log($"版本检查失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>是否有新版本（Dart <c>checkForUpdate</c>）；无更新/失败返回 <c>null</c>。</summary>
    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var latest = await FetchLatestAsync(cancellationToken).ConfigureAwait(false);
        if (latest == null || latest.Version.Length == 0) return null;
        return IsNewer(latest.Version, CurrentVersion) ? latest : null;
    }

    /// <summary>等价 Dart <c>isNewer</c>：<c>remote &gt; local</c>（非严格 semver：按数字段逐位比较）。</summary>
    public static bool IsNewer(string remote, string local)
    {
        var r = Segments(remote);
        var l = Segments(local);
        var length = Math.Max(r.Count, l.Count);
        for (var i = 0; i < length; i++)
        {
            var rv = i < r.Count ? r[i] : 0L;
            var lv = i < l.Count ? l[i] : 0L;
            if (rv != lv) return rv > lv;
        }
        return false;
    }

    /// <summary>
    /// 等价 Dart <c>_segments</c>：去开头一个 <c>v</c>/<c>V</c> → 按 <c>. - +</c> 切段 → 每段取前导数字。
    /// Dart int 为 64 位、解析溢出时 <c>tryParse</c> 返回 null ⇒ 0，此处用 <c>long.TryParse</c> 同语义。
    /// </summary>
    public static List<long> Segments(string version)
    {
        var cleaned = version ?? string.Empty;
        if (cleaned.Length > 0 && (cleaned[0] == 'v' || cleaned[0] == 'V')) cleaned = cleaned.Substring(1);

        var output = new List<long>();
        foreach (var segment in VersionSeparator.Split(cleaned))
        {
            var match = LeadingDigits.Match(segment);
            output.Add(match.Success && long.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0L);
        }
        return output;
    }

    // ── 下载 / 解压 / 热替换 ───────────────────────────────────────────────

    /// <summary>
    /// 下载更新包到 <paramref name="targetDir"/>（Dart <c>download</c>），返回本地文件路径；失败返回 <c>null</c>。
    /// 流式落盘（避免整包进内存）；进度回调参数为 <c>(received, total)</c>，无法得知总长时 <c>total = -1</c>。
    /// </summary>
    public async Task<string> DownloadAsync(
        UpdateInfo info,
        string targetDir,
        Action<long, long> onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (info == null || !info.HasDownload) return null;

        try
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // 等价 Dart：`info.fileName.isEmpty ? 'aipayer-${info.version}.zip' : info.fileName`
            // （原版字符串里的 "aipayer" 拼写照抄，保持行为等价）。
            var fileName = info.FileName.Length == 0 ? $"aipayer-{info.Version}.zip" : info.FileName;
            var target = Path.Combine(targetDir, TextUtils.SanitizeFileName(fileName));

            using var client = new HttpClient();
            // t65：UA 走唯一构造点。⚠️ 刻意差异（**已上报待裁定**）：原版这条路写死 `'AI Player'`
            // （Dart `HttpClient()..userAgent = 'AI Player'`，见文件头 ②）；本卡按"UA 单一构造点 + 用户可自定义"
            // 把它收敛到 policy ⇒ 该路 UA 由 `AI Player` 变为 policy 值（默认 `ai-player` 或用户自定义）。
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AIPlayer.Shell.Services.Http.UserAgentPolicy.Current);
            using var response = await client
                .GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if ((int)response.StatusCode != 200)
            {
                Log($"下载失败：HTTP {(int)response.StatusCode}");
                return null;
            }

            var total = response.Content.Headers.ContentLength ?? -1L;
            var received = 0L;

            using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var sink = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    onProgress?.Invoke(received, total);
                }
                await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            Log($"更新包已下载：{target}（{received / 1024} KB）");
            return target;
        }
        catch (Exception ex)
        {
            Log($"下载更新包失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 遥测/错误上报（Dart <c>report</c>）：<c>POST {清单 origin}/aiplayer/report/{path}</c>，超时 8s、不重试。
    /// **失败只记日志**（P1 零调用项，验收要求必须实现且不得影响主流程）。
    /// </summary>
    public async Task ReportAsync(string path, IDictionary<string, object> payload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var origin = new Uri(VersionManifestUrl).GetLeftPart(UriPartial.Authority);
            var url = $"{origin}{ReportUrlPrefix}{path}";
            await Http.PostJsonAsync(
                url,
                payload ?? new Dictionary<string, object>(StringComparer.Ordinal),
                timeout: TimeSpan.FromSeconds(8),
                retries: 0,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"上报失败（忽略）：{ex.Message}");
        }
    }

    /// <summary>
    /// 打包诊断信息（Dart <c>buildDiagnostics</c>；日志页「导出诊断」用），缩进 2 空格的 JSON。
    /// 键名沿用原版 schema：<c>platform</c> = windows/linux/macos，<c>osVersion</c> = 系统版本串，
    /// <c>dart</c> = 运行时描述（C# 侧取 .NET 运行时，Dart VM 不存在）；<paramref name="extra"/> 最后覆盖同名键。
    /// </summary>
    public static string BuildDiagnostics(string appVersion, IDictionary<string, object> extra = null)
    {
        var root = new JsonObject
        {
            ["app"] = "AI Player (rebuilt shell)",
            ["version"] = appVersion ?? string.Empty,
            ["platform"] = PlatformName(),
            ["osVersion"] = RuntimeInformation.OSDescription,
            ["dart"] = RuntimeInformation.FrameworkDescription,
            ["generatedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
        };

        if (extra != null)
        {
            foreach (var kv in extra)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                root[kv.Key] = kv.Value == null ? null : JsonSerializer.SerializeToNode(kv.Value);
            }
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>等价 Dart <c>Platform.operatingSystem</c> 的取值集合。</summary>
    private static string PlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos";
        return "unknown";
    }

    /// <summary>
    /// 解压更新包（Dart <c>extract</c>，零第三方依赖）：Windows 自带 <c>tar.exe</c>（bsdtar，原生吃 zip），
    /// 失败再回落 PowerShell <c>Expand-Archive</c>；非 zip（<c>.7z</c>/<c>.exe</c> 等）返回 <c>false</c> 并提示手动安装。
    /// </summary>
    public async Task<bool> ExtractAsync(string archivePath, string targetDir, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(archivePath) || !archivePath.ToLowerInvariant().EndsWith(".zip", StringComparison.Ordinal))
        {
            Log($"更新包不是 zip（{archivePath}）⇒ 需手动解压安装");
            return false;
        }
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        if (await RunToolAsync("tar.exe", new[] { "-xf", archivePath, "-C", targetDir }, cancellationToken).ConfigureAwait(false))
        {
            Log($"更新包已解压（tar.exe）⇒ {targetDir}");
            return true;
        }

        var ok = await RunToolAsync(
            "powershell.exe",
            new[]
            {
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                $"Expand-Archive -LiteralPath '{archivePath}' -DestinationPath '{targetDir}' -Force",
            },
            cancellationToken).ConfigureAwait(false);

        if (ok) Log($"更新包已解压（PowerShell）⇒ {targetDir}");
        return ok;
    }

    /// <summary>解压目录归一化（Dart <c>normalizeExtractedRoot</c>）：发行包常带单层顶层目录 ⇒ 返回真正的程序根。</summary>
    public static string NormalizeExtractedRoot(string stagingDir)
    {
        if (string.IsNullOrEmpty(stagingDir) || !Directory.Exists(stagingDir)) return stagingDir;

        var entries = Directory.GetFileSystemEntries(stagingDir);
        var dirs = entries.Where(Directory.Exists).ToList();
        var files = entries.Where(File.Exists).ToList();
        if (files.Count == 0 && dirs.Count == 1) return dirs[0];
        return stagingDir;
    }

    /// <summary>
    /// 生成「等待退出 → 覆盖安装 → 自动重启」脚本并**分离启动**（Dart <c>applyUpdate</c>），返回脚本路径；失败返回 <c>null</c>。
    /// 调用方随后应自行退出应用：脚本先等本进程退出，再覆盖文件（运行中的 exe/dll 被占用，无法就地替换）。
    /// bat 必须 CRLF 行尾（cmd 对 LF-only 的 goto/标签解析会出错 —— Dart 侧同一注释）。
    /// </summary>
    public async Task<string> ApplyUpdateAsync(
        string stagingDir,
        string installDir,
        string exeName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exe = exeName;
            if (exe == null)
            {
                // 等价 Dart `Platform.resolvedExecutable.split(sep).last`。
                var resolved = Environment.ProcessPath;
                exe = string.IsNullOrEmpty(resolved) ? string.Empty : Path.GetFileName(resolved);
            }

            var script = Path.Combine(Path.GetTempPath(), "aiplayer_update.bat");
            const string crlf = "\r\n";
            var lines = new[]
            {
                "@echo off",
                "setlocal",
                "title AI Player 更新",
                "set \"SRC=" + stagingDir + "\"",
                "set \"DST=" + installDir + "\"",
                "set \"EXE=" + exe + "\"",
                "echo [1/3] 等待 AI Player 退出…",
                ":wait",
                "tasklist /fi \"imagename eq %EXE%\" | find /i \"%EXE%\" >nul && ( timeout /t 1 /nobreak >nul & goto wait )",
                "echo [2/3] 覆盖程序文件…",
                "xcopy /E /Y /I /Q \"%SRC%\\*\" \"%DST%\\\" >nul",
                "echo [3/3] 启动新版本…",
                "start \"\" \"%DST%\\%EXE%\"",
                "rd /s /q \"%SRC%\" >nul 2>&1",
                "del \"%~f0\"",
            };
            var bat = string.Join(crlf, lines);

            await File.WriteAllTextAsync(script, bat, Utf8NoBom, cancellationToken).ConfigureAwait(false);

            // UseShellExecute=true ⇒ 子进程不随本进程退出而死（等价 Dart ProcessStartMode.detached）。
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"/c \"{script}\"",
            };
            Process.Start(startInfo);

            Log($"更新脚本已启动：{script}");
            return script;
        }
        catch (Exception ex)
        {
            Log($"生成更新脚本失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>等价 Dart <c>extract</c> 内部的 <c>run()</c>：外部工具执行 + 退出码判定，失败只记日志。</summary>
    private async Task<bool> RunToolAsync(string exe, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log($"{exe} 启动失败（返回 null）");
                return false;
            }

            // 两个流并发抽干，避免子进程写满缓冲区而死锁（等价 Dart Process.run 的收集行为）。
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            if (process.ExitCode == 0) return true;
            Log($"{exe} 退出码 {process.ExitCode}：{stderrTask.Result}");
        }
        catch (Exception ex)
        {
            Log($"{exe} 调用失败：{ex.Message}");
        }
        return false;
    }
}

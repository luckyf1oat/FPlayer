using System;
using System.IO;
using System.Reflection;

namespace AIPlayer.Shell.Features.Search;

/// <summary>
/// 搜索屏的日志（t28 / U-B）。
///
/// 为什么自带一个日志而不是直接调 <c>Program.Log</c>：
///   本目录的源码要能同时被**外壳**与**独立取证宿主**（仓库外、链接同一份源码）编译。
///   宿主的程序集名与外壳不同 ⇒ 编译期写死 <c>Program.Log</c> 会 CS0103。
///   故：写自己的文件（默认 <c>%LOCALAPPDATA%\AIPlayer\logs\t28-search.log</c>，
///   可用 <c>SHELL_SEARCH_LOG</c> 覆盖），并**用反射**尝试转发到外壳的
///   <c>AIPlayer.Shell.Program.Log</c>（存在则转发，不存在则静默跳过）。
///
/// 凭据纪律：调用方传入前已打码（本类不做打码，也不打印任何 URL 之外的秘密）。
/// </summary>
public static class SearchLog
{
    /// <summary>日志落点覆盖（取证用：把同一份日志写进仓库内的 evidence 目录）。</summary>
    public const string PathEnvVar = "SHELL_SEARCH_LOG";

    private static readonly object Gate = new();
    private static MethodInfo _forward;
    private static bool _forwardResolved;

    private static readonly Lazy<string> _path = new(ComputePath);

    /// <summary>日志文件绝对路径（供取证脚本引用）。</summary>
    public static string FilePath => _path.Value;

    private static string ComputePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(PathEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var p = overridePath.Trim();
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir))
            {
                try { Directory.CreateDirectory(dir); } catch (Exception) { }   // 只读目录：退回默认路径
            }
            return p;
        }

        // t226：根解析**不再硬编码** —— 与 `AppDataDir.cs:467 LogFile => Path.Combine(Root,"logs","aiplayer.log")`
        // 同源（`AppDataDir.Instance.Root` 三源现算：AIPLAYER_APPDATA_ROOT 覆盖 / 便携 data\ / 默认 %LOCALAPPDATA%\AIPlayer）。
        // 为什么必须改：硬编码 `%LOCALAPPDATA%\AIPlayer\logs` 时，设了覆盖也只写真实根 ⇒ 沙箱化只对图片/缓存生效、
        // 这枚日志仍落到用户真实根（**旁路**）。文件名与内容语义一律不变。
        var logs = Path.Combine(AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root, "logs");
        // 目录建不出来（只读盘 / 权限）不打断搜索：后续 AppendAllText 各自失败并被就地忽略
        try { Directory.CreateDirectory(logs); } catch (Exception) { }
        return Path.Combine(logs, "t28-search.log");
    }

    /// <summary>追加一行（带毫秒时间戳）。任何 IO 异常都被吞掉 —— 日志失败不得影响搜索。</summary>
    public static void Write(string line)
    {
        // 🔴 凭据纪律：本屏会把图片 URL、搜索词、服务器名写进日志，而 Emby 的图片 URL 带
        //    `api_key=<token>`（**实测泄漏过一次**：`…/Images/Primary?maxHeight=300&api_key=<32hex>`）。
        //    打码放在**唯一的写盘入口**上，调用方漏打也不会泄漏。
        //    与外壳 `Program.MaskSecrets` / `shell/Spike/Probe.MaskSecrets` 同一条正则语义。
        var masked = Mask(line);
        var text = DateTime.Now.ToString("HH:mm:ss.fff") + " " + masked;
        System.Diagnostics.Debug.WriteLine("[SEARCH] " + text);
        try
        {
            lock (Gate) { File.AppendAllText(FilePath, text + Environment.NewLine); }
        }
        catch (Exception) { }   // 写盘失败不中断搜索：日志是旁路观测，上面已另行 Debug.WriteLine 一份

        ForwardToShell(text);
    }

    /// <summary>打码 = 转发到**全工程唯一实现** <c>Services.Logging.SecretMasking.Mask</c>（t157 收敛）。</summary>
    /// <remarks>
    /// 本处曾是**窄副本**（只有 `api_key` 的两种书写形态 —— 等号与百分号编码 —— 那一条正则）⇒ 收敛后覆盖 **5 条规则**（`api_key` / `--http-header=` /
    /// 认证头 / URL 查询串凭据键 / 密码字段），并继承其**幂等**与**不抛**两条纪律。
    /// 这是**覆盖面单向增加**（原来漏打的形态现在会打码），`api_key=***` 的替换串与原来逐字相同。
    /// </remarks>
    internal static string Mask(string s) => AIPlayer.Shell.Services.Logging.SecretMasking.Mask(s);

    private static void ForwardToShell(string text)
    {
        try
        {
            if (!_forwardResolved)
            {
                _forwardResolved = true;
                var t = Type.GetType("AIPlayer.Shell.Program, AIPlayer.Shell", throwOnError: false);
                _forward = t?.GetMethod("Log", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            _forward?.Invoke(null, new object[] { "[SEARCH] " + text });
        }
        catch (Exception) { }   // 镜像转发失败（外壳不在场 / 签名变动）只影响镜像；本屏日志已落盘
    }

    /// <summary>清空日志（每次取证跑之前调一次，避免把上一轮的读数和这轮混在一起）。</summary>
    public static void Clear()
    {
        // 删不掉只意味着上一轮读数还在：取证按时间戳区间读即可，不值得让「开始新一轮」失败
        try { lock (Gate) { if (File.Exists(FilePath)) File.Delete(FilePath); } } catch (Exception) { }
    }
}

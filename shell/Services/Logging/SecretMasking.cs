// t132：**全工程唯一的凭据打码实现（sink 级）**。
//
// 为什么要在服务层再来一份（不是重复劳动）：`ui2` 的 t99 把规则落在 `shell/App/Infrastructure/SecretMasker.cs`，
// 但那条路只覆盖**经 App 门面**（`Program.Log` / App 侧调用点）的写盘；而 `shell/Services/Logging/DebugLog.cs` 的
// `PlayerLogService.Add` 是**全工程唯一 sink**（环形缓冲 + 文件写盘都从它出去）⇒ 任何**直接** `DebugLog.Info/Warn/Error`
// 的调用点（实测量：`KernelLauncher.cs:85` 的 `KERNEL-LAUNCH … args=` 贡献 24 行裸 base64，另有
// `PLAY-KERNEL-ARGS-RAW` 同量）都会写裸值。逐点补是"打地鼠"，且必然再漏。
//   ⇒ 打码**下沉到 sink 入口**：`PlayerLogService.Add` 第一件事就是 mask，这样**新旧调用点全部自动受益**。
//
// 与 t99 那份的关系（卡面顺序）：本文件是**唯一实现**；`ui2` 随后把 `SecretMasker` 改成**转发**到本实现（全工程只剩一份）。
//   ⇒ 规则、`MaskToken`、`Describe` 的输出形态**与 t99 那份逐字同源**（同 5 条规则 + 同一替换串），
//     唯一增强 = **显式幂等守卫**（t99 那份的 `--http-header=` 替换串含空格，二次打码会再替换一次 ⇒ 不幂等；本卡要求幂等）。
//
// 🔴 两条纪律（与 t99 同）：
//   ① **只打码、绝不删除**行（保留 host/path/头名/长度与指纹，便于排障）；
//   ② 打码器本身**不许抛**（它在日志热路径上；抛了就等于"日志整段消失"）——失败时返回原文并交回错误名，由 sink 留一行诊断。

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AIPlayer.Shell.Services.Logging;

/// <summary>全工程唯一的凭据打码器（`api_key` / `--http-header=` / 认证头 / URL 查询串 / 密码字段 / 轨道·清单类选项（t248））。</summary>
public static class SecretMasking
{
    /// <summary>打码后的占位形态（固定串，便于门禁与证据逐字核对；与 t99 的 <c>SecretMasker.MaskToken</c> 同源）。</summary>
    public const string MaskToken = "***";

    /// <summary>早退守卫用的触发子串（一个都不含 ⇒ 直接原样返回，避免每条日志都跑 6 次正则）。
    /// 🔴 t248：**新增规则必须同时补这里** —— 否则 `--subtitle-track=&lt;base64&gt;` 这类行一个既有触发子串都不含，
    /// 守卫会直接原样返回、新规则永不执行（漏打码的成因之一）。</summary>
    private static readonly string[] Triggers =
    {
        "api_key", "API_KEY", "http-header", "X-Emby", "x-emby", "Authorization", "authorization",
        "Bearer", "bearer", "password", "Password", "token=", "Token=", "access_token",
        // t248：轨道/清单类选项名（`-track=` 覆盖 `--subtitle-track=` 与 `--audio-track=`，其余逐个列）
        "-track=", "--version-option", "--episode-list", "--segment", "--chapter", "--sprite", "--shortcuts", "--danmaku-api",
    };

    /// <summary>`--http-header=&lt;base64&gt;`：保留选项名与"被掩掉多少字节 + 指纹"，值整体不落盘。
    /// 🔴 t255：值的**右界必须排除 `|`**（`([^\s|]+)`）—— 与 t253 的轨道/清单规则同性质：`|` 是日志字段分隔符，
    /// 用 `\S+` 时 `--http-header=&lt;b64&gt;|manual-api_key=…` 会把 `|` 之后整段并进"值"一起掩掉（键名消失、判据恒真）。</summary>
    private static readonly Regex HttpHeaderOption = new Regex(
        @"(--http-header=)([^\s|]+)", RegexOptions.CultureInvariant);

    /// <summary>`api_key=&lt;值&gt;`（与旧实现逐字同形态：`api_key=***`）；兼容**百分号编码**形态 `api_key%3D&lt;值&gt;`。</summary>
    private static readonly Regex ApiKey = new Regex(
        @"(api_key(?:(?:%3D)|=))[^&\s""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>认证类请求头：`X-Emby-Token: ab12…` / `X-Emby-Authorization: Emby …` / `Authorization: Bearer …`。</summary>
    private static readonly Regex AuthHeader = new Regex(
        @"((?:X-Emby-Token|X-Emby-Authorization|Authorization)\s*:\s*)(?:(Bearer|Emby)\s+)?([^\s""',;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>URL 查询串里的凭据键（`token=` / `password=` / `access_token=` …）。</summary>
    private static readonly Regex QuerySecret = new Regex(
        @"((?:\?|&)(?:token|access_token|password|passwd|pwd|secret|api_key)=)[^&\s""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>JSON/键值形态里的密码（`"Password": "…"` / `password=…`）。</summary>
    private static readonly Regex PasswordField = new Regex(
        @"((?:""?[Pp]assword""?\s*[:=]\s*""?)|(?:password=))([^"",;\s]+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// t248：**轨道/清单类选项**（`--subtitle-track=` / `--audio-track=` / `--version-option=` / `--episode-list=` /
    /// `--segment=` / `--chapter=` / `--sprite=` / `--shortcuts=` / `--danmaku-api=`）。
    /// 为什么单开一条：这类值可能是 **base64**（例如 `{"url":"…?api_key=…"}` 的编码），既有 5 条规则一条都命中不了
    /// ⇒ `api_key` 经 base64 落盘（真根日志实测存在）。
    /// 🔴 **窄在哪**：①只认这 9 个参数名（不是所有 `--xxx=`）；②值是**可读短标量**（纯数字 / `no` / `auto`）时**不掩**
    /// （掩了反而妨碍排障）——判据见 <see cref="IsMaskableOptionValue"/>。
    /// 输出与 <c>--http-header=</c> **同形**：`&lt;masked len=N sha4=XXXX&gt;`（保长度 + 指纹，值整体不落盘）。
    /// 🔴 t253：值的**右界必须排除 `|`**（`([^\s|]+)`）。日志里 `|` 是字段分隔符：用 `\S+` 时，轨道选项后**无空白**
    /// 紧跟 `|manual-api_key=…` 这类字段，会把 `|` 之后的整段尾巴并进"值"一起掩掉 ⇒ 键名从日志里消失，
    /// 且"不该打码"类判据在这种行型上**恒真**（不是因为打码正确，而是整段被吞）。
    /// </summary>
    private static readonly Regex TrackOption = new Regex(
        @"(--(?:subtitle-track|audio-track|version-option|episode-list|segment|chapter|sprite|shortcuts|danmaku-api)=)([^\s|]+)",
        RegexOptions.CultureInvariant);

    /// <summary>t248 窄规则的**豁免判据**：纯数字 / `no` / `auto`（大小写不敏感）⇒ **不掩**（可读标量）。</summary>
    private static bool IsMaskableOptionValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"', '\'');
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsDigit(ch))
            {
                return true;
            }
        }

        return false;   // 纯数字 ⇒ 不掩
    }

    /// <summary>
    /// 打码一行（**幂等**：已打码的输入再打一次结果不变）。不抛异常：出错时返回原串并给出 <paramref name="error"/>。
    /// </summary>
    public static string Mask(string s) => Mask(s, out _);

    /// <summary>与 <see cref="Mask(string)"/> 同体，但把失败原因交回调用方（sink 用它留一行诊断，见 <see cref="MaskFailPrefix"/>）。</summary>
    public static string Mask(string s, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(s) || !HasTrigger(s))
        {
            return s;
        }

        try
        {
            var masked = HttpHeaderOption.Replace(s, m => m.Groups[1].Value + Describe(m.Groups[2].Value));
            // t248：轨道/清单类选项（值可能是 base64 内含 api_key）⇒ 值非「纯数字 / no / auto」时按 http-header 同形掩
            masked = TrackOption.Replace(masked, m => IsMaskableOptionValue(m.Groups[2].Value)
                ? m.Groups[1].Value + Describe(m.Groups[2].Value)
                : m.Value);
            masked = ApiKey.Replace(masked, "$1" + MaskToken);
            masked = AuthHeader.Replace(masked, m => m.Groups[1].Value + (m.Groups[2].Success ? m.Groups[2].Value + " " : string.Empty) + Describe(m.Groups[3].Value));
            masked = QuerySecret.Replace(masked, "$1" + MaskToken);
            masked = PasswordField.Replace(masked, m => m.Groups[1].Value + MaskToken);
            return masked;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or RegexMatchTimeoutException)
        {
            // 打码器不得成为新的故障源：返回原串 + 让 sink 留一行诊断（"宁可少打一次，不可让日志消失"）。
            error = ex.GetType().Name;
            return s;
        }
    }

    /// <summary>sink 在打码失败时追加的诊断行前缀（可见、可门禁核对）。</summary>
    public const string MaskFailPrefix = "LOG-MASK-FAIL";

    /// <summary>
    /// t138：打码失败时 sink 写出的**不可逆占位符** —— <c>&lt;masked-failed len=N sha4=XXXX&gt;</c>。
    /// 与 <see cref="Describe"/> 的区别只在意图：那个用于"掩掉某个值"，本方法用于"整行都没能打码"。
    /// <list type="bullet">
    /// <item><b>保行</b>：调用方照样写出这一行（行数/位置/时间/级别与正常路径逐一相同）⇒ "少了哪一行"仍可判；</item>
    /// <item><b>不保明文</b>：内容只有"多长 + 指纹 + 失败类型"，不含任何可逆内容（t99 的明文泄露不得在异常路径上回归）；</item>
    /// <item><b>幂等友好</b>：以 <c>&lt;masked</c> 开头 ⇒ 再次进入 <see cref="Describe"/> 的"原样返回"守卫。</item>
    /// </list>
    /// 指纹沿用 <see cref="Sha4"/> ⇒ 同一个值在不同行上仍能互相对照（排障价值所在）。
    /// </summary>
    public static string DescribeFailure(string value)
    {
        var len = value?.Length ?? 0;
        return "<masked-failed len=" + len + " sha4=" + Sha4(value) + ">";
    }

    /// <summary>
    /// 值的可读替代：`&lt;masked len=N sha4=XXXX&gt;` —— 保留**长度与指纹**（足以判断"是不是同一个值"），
    /// 但不落任何可逆内容。空值给 `&lt;masked empty&gt;`。
    /// 🔴 **幂等守卫**：输入已经是本形态（以 `&lt;masked` 开头）⇒ 原样返回，不再嵌套打码。
    /// </summary>
    public static string Describe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<masked empty>";
        }

        if (value.StartsWith("<masked", StringComparison.Ordinal))
        {
            return value;   // 幂等：已打码的值不再套一层
        }

        return "<masked len=" + value.Length + " sha4=" + Sha4(value) + ">";
    }

    /// <summary>值的前 4 位十六进制指纹（SHA-256 前 2 字节）——**不可逆**，只用于跨行比对同一个值。</summary>
    public static string Sha4(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(hash).Substring(0, 4);
    }

    private static bool HasTrigger(string s)
    {
        foreach (var trigger in Triggers)
        {
            if (s.IndexOf(trigger, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

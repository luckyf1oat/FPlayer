using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace WinUISample.Services;

/// <summary>
/// 写日志前的**凭据打码层**：先按原模板渲染，再把渲染结果里的凭据换成 <c>***&lt;len=N,sha4=XXXX&gt;</c>
/// （保留长度 + 4 位前缀哈希 ⇒ 仍能对账"是不是同一个凭据 / 有多长"，但拿不回原值）。
/// </summary>
/// <remarks>
/// <b>为什么打在这一层，而不是逐个日志调用点</b>：
/// <list type="number">
///   <item><b>调用点覆盖不全</b>：mpv 自己的日志文本也被转发进同一个文件（<c>[MPV] Log message: …</c>），
///         它由外部程序集产出，kernel/src 里**根本没有可改的调用点** ⇒ 只改调用点必然漏；</item>
///   <item><b>一次落点覆盖全部</b>：包括以后新增的日志调用（新代码不必记得打码，默认就是安全的）；</item>
///   <item><b>凭据形态是有限的几种</b>（URL 查询串的 <c>api_key=…</c>、<c>Authorization: …</c>、
///         命令行的 <c>--http-header=…</c>）⇒ 集中在一处正则可测、可加、可审。</item>
/// </list>
/// <b>有意保留</b>：scheme/host/path、参数名、以及 <c>***</c> 后的长度与前缀哈希——排查"凭据是否为空 /
/// 两次跑是不是同一个"仍然可行（"留痕但不留秘密"）。
/// <para>⚠️ 这是**兜底**而不是唯一防线：调用点仍不应主动把凭据拼进消息。</para>
/// </remarks>
internal sealed class SecretMaskingTextFormatter : ITextFormatter
{
	private const string MaskedPrefix = "<masked len=";

	/// <summary>
	/// 键值对（<c>api_key=…</c> / <c>token=…</c> / <c>password=…</c>）的占位符：与**外壳侧**
	/// <c>shell/Services/Logging/SecretMasking.cs</c> 的 <c>MaskToken</c> 逐字一致（<c>***</c>）。
	/// </summary>
	private const string MaskToken = "***";

	/// <summary>URL 查询串 / 头部风格的 <c>key=value</c>：只打码值，保留参数名。</summary>
	private static readonly Regex SecretPair = new Regex(
		@"(?<key>api[_-]?key|api[_-]?token|access[_-]?token|x-emby-token|token|password|pwd)=(?<value>[^&\s""'<>,\\)\]}]+)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary><c>Authorization: &lt;scheme&gt; &lt;value&gt;</c>（含 Bearer/Basic）与裸 <c>Bearer &lt;value&gt;</c>。</summary>
	private static readonly Regex AuthorizationValue = new Regex(
		@"(?<prefix>(?:Authorization\s*:\s*)(?:[A-Za-z]+\s+)?|Bearer\s+)(?<value>[A-Za-z0-9._~+/\-]{6,}=*)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>命令行的 <c>--http-header=&lt;base64&gt;</c>：值是（名, 值）对，整段打码。</summary>
	private static readonly Regex HttpHeaderArgument = new Regex(
		@"(?<prefix>--http-header=)(?<value>[A-Za-z0-9+/_\-]+=*)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>
	/// 命令行的 <c>--user-agent=&lt;value&gt;</c>（t200）—— **有意默认不掩**，只对**命中凭据形态**的值打码。
	/// 理由见 <see cref="IsCredentialShapedUserAgent"/> 上方注释：UA 在线上本就明文、掩它不减少真实暴露，
	/// 却会砍掉 UA 归一化（t79/t80/t90）唯一的运行取证通道；但"把 token 塞进 UA 的探针"必须被挡住。
	/// 取值到"下一个 <c> --</c> 或行尾"为止（UA 可含空格，如 <c>Bearer eyJ…</c>）⇒ 不会把后一个参数吃进来。
	/// </summary>
	private static readonly Regex UserAgentArgument = new Regex(
		@"(?<prefix>--user-agent=)(?<value>.+?)(?=\s--|$)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>
	/// 命令行里**以 base64 承载 JSON/URL 的参数**（<c>--subtitle-track=</c> / <c>--audio-track=</c> /
	/// <c>--version-option=</c> / <c>--sprite=</c> / <c>--danmaku-api=</c>）：它们能把带 <c>api_key=…</c> 的 URL
	/// 整段编成一个不透明的 blob —— **只按字面扫是扫不出来的**，于是会出现"打码看着生效、秘密仍可解出"的假安全。
	/// 这里解码后**只在该 blob 真的含凭据时才整段打码**（不含凭据的 blob 保持可读，不牺牲排查面）。
	/// <c>--http-header=</c> 不在此列：它**本身就是**一个凭据容器，由 <see cref="HttpHeaderArgument"/> 无条件打码。
	/// </summary>
	private static readonly Regex Base64Argument = new Regex(
		@"(?<prefix>--(?:subtitle-track|audio-track|version-option|sprite|danmaku-api)=)(?<value>[A-Za-z0-9+/_\-]{16,}={0,2})",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	private readonly MessageTemplateTextFormatter _inner;

	private SecretMaskingTextFormatter(string outputTemplate, IFormatProvider? formatProvider = null)
	{
		_inner = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
	}

	/// <summary>
	/// **formatter 的唯一取用口**（t222 / K-07「按 sink 收口」）：构造函数私有 ⇒ 任何 sink 想拿本类实例
	/// 只能经这里，而这里返回的**必定是已打码的那一个**；日后新增 sink（console/debug/网络/第二个文件）
	/// 照此取用即**默认受保护**，不必每个写点自己记得（构造保证，而非纪律保证）。
	/// <para>机械可核（读数见 `kernel/evidence/t222-masking-at-sink.txt`）：sink 注册调用在整个 `kernel/src` 里
	/// **只有一处** —— `AddMaskedFileSink`（判据 `git grep -n -E 'WriteTo[.]File' -- kernel/src`，命中 = 1，
	/// 注释里写的是带方括号的形态、不自匹配）；`ITextFormatter` 的实现类也**只有一个**（本类）。</para>
	/// </summary>
	internal static ITextFormatter CreateForSink(string outputTemplate, IFormatProvider? formatProvider = null)
	{
		return new SecretMaskingTextFormatter(outputTemplate, formatProvider);
	}

	public void Format(LogEvent logEvent, TextWriter output)
	{
		using StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
		_inner.Format(logEvent, stringWriter);
		output.Write(MaskSecrets(stringWriter.ToString()));
	}

	/// <summary>把一段文本里所有可识别的凭据换成掩码（唯一实现点）。</summary>
	public static string MaskSecrets(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text ?? string.Empty;
		}
		string masked = Base64Argument.Replace(text, MaskBase64Argument);
		masked = HttpHeaderArgument.Replace(masked, MaskMatch);
		// t200：`--user-agent=` 走**条件**规则（凭据形态才掩）—— 放在 MaskText 之前，
		// 使命中凭据的 UA 整值成 `<masked …>`，而不是只被 SecretPair 抠掉里面某个 `token=` 的值。
		masked = UserAgentArgument.Replace(masked, MaskUserAgentArgument);
		return MaskText(masked);
	}

	/// <summary>纯文本规则（不含 base64 拆包）—— 供解码后的内容复用，保证**只有一层解码**、不会递归。</summary>
	private static string MaskText(string text)
	{
		string masked = AuthorizationValue.Replace(text, MaskMatch);
		// 键值对走 <c>***</c>（与外壳侧同形态）；Authorization 走 <c>&lt;masked len=N sha4=XXXX&gt;</c>。
		return SecretPair.Replace(masked, MaskKeyValue);
	}

	/// <summary>
	/// base64 参数：解码后若**真的含凭据**才整段打码；解码不出文本（或里面没有凭据）⇒ 原样保留。
	/// </summary>
	private static string MaskBase64Argument(Match match)
	{
		string value = match.Groups["value"].Value;
		if (TryDecodeBase64(value, out string decoded) && MaskText(decoded) != decoded)
		{
			return match.Groups["prefix"].Value + Mask(value);
		}
		return match.Value;
	}

	/// <summary>URL-safe base64（允许缺省填充）⇒ UTF-8 文本；解码失败或不是合法 UTF-8 时返回 false。</summary>
	private static bool TryDecodeBase64(string value, out string decoded)
	{
		decoded = string.Empty;
		string text = value.Replace('-', '+').Replace('_', '/');
		switch (text.Length % 4)
		{
			case 0:
				break;
			case 2:
				text += "==";
				break;
			case 3:
				text += "=";
				break;
			default:
				return false;
		}
		try
		{
			decoded = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(text));
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (DecoderFallbackException)
		{
			return false;
		}
	}

	/// <summary>
	/// `--user-agent=` 的**条件**打码（t200，captain 裁定「甲」）：**默认保持原文**，只在值命中
	/// **凭据形态**时整值打码（形态与同行 `--http-header=` 一致：`&lt;masked len=N sha4=XXXX&gt;`）。
	/// <para><b>为什么默认不掩（有意，不是漏项）</b>：UA 在 HTTP 线上本就是明文，掩它**不减少真实暴露**，
	/// 却会砍掉 UA 归一化（t79/t80/t90）**唯一的运行取证通道**；而"把 token 塞进 UA"的探针必须被挡住
	/// ⇒ 判据落在**值的形态**上，而不是落在键名上。</para>
	/// <para><b>凭据形态（可复算）</b>：含 <c>Bearer </c> / <c>Basic </c> / <c>token=</c> / <c>api_key=</c> /
	/// <c>apikey=</c> / <c>password=</c> / <c>X-Emby-</c>，或值本身是 32 位 hex，或值本身是 ≥24 字符的
	/// base64-ish（<c>[A-Za-z0-9+/_\-]</c> + 至多 2 个尾部 <c>=</c>）。</para>
	/// </summary>
	private static string MaskUserAgentArgument(Match match)
	{
		string text = match.Groups["value"].Value.Trim().Trim('"');
		if (!IsCredentialShapedUserAgent(text))
		{
			return match.Value;
		}
		return match.Groups["prefix"].Value + Mask(text);
	}

	/// <summary>凭据形态判定（t200）—— 纯函数、无副作用；判据只此一处，别处不得再写第二份。</summary>
	private static bool IsCredentialShapedUserAgent(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		if (value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("Basic ", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("token=", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("api_key=", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("apikey=", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("password=", StringComparison.OrdinalIgnoreCase)
			|| value.Contains("X-Emby-", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (value.Length == 32 && IsHex32(value))
		{
			return true;
		}
		return value.Length >= 24 && IsBase64ish(value);
	}

	private static bool IsHex32(string value)
	{
		foreach (char c in value)
		{
			if (!Uri.IsHexDigit(c))
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsBase64ish(string value)
	{
		int padding = 0;
		foreach (char c in value)
		{
			if (c == '=')
			{
				padding++;
				if (padding > 2)
				{
					return false;
				}
				continue;
			}
			if (padding > 0)
			{
				return false;
			}
			if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '+' && c != '/' && c != '_' && c != '-')
			{
				return false;
			}
		}
		return true;
	}

	private static string MaskMatch(Match match)
	{
		return match.Groups["prefix"].Value + Mask(match.Groups["value"].Value);
	}

	/// <summary>键值对：<c>key=***</c>（只换值，保留参数名 —— 与外壳侧一致）。</summary>
	private static string MaskKeyValue(Match match)
	{
		return match.Groups["key"].Value + "=" + MaskToken;
	}

	/// <summary>
	/// 把一个凭据换成 <c>&lt;masked len=N sha4=XXXX&gt;</c>（与外壳侧 <c>Describe()</c> 逐字一致）；
	/// sha4 = SHA256 前 2 字节的十六进制，**不可反推**。空值给 <c>&lt;masked empty&gt;</c>。
	/// 🔴 **幂等守卫**：输入已是本形态（以 <c>&lt;masked</c> 开头）或已是 <c>***</c> ⇒ 原样返回，不再嵌套打码
	/// （外壳侧同样有这条守卫，两侧行为一致 ⇒ 二次打码不会造出第三种形态）。
	/// </summary>
	private static string Mask(string value)
	{
		if (value.Length == 0)
		{
			return "<masked empty>";
		}
		if (value == MaskToken || value.StartsWith("<masked", StringComparison.Ordinal))
		{
			return value;
		}
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return MaskedPrefix + value.Length.ToString(CultureInfo.InvariantCulture) + " sha4=" + Convert.ToHexString(hash.AsSpan(0, 2)) + ">";
	}
}

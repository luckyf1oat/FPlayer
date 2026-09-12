using System;
using System.Collections.Generic;
using Serilog;

namespace WinUISample.Models.Constants;

internal static class ClientIdentity
{
	public const string ClientName = "ai-player";

	private static string _httpUserAgent = "ai-player";

	/// <summary>
	/// 宿主是否**显式**给过 <c>--user-agent=</c>（= 当前 <see cref="HttpUserAgent"/> 是宿主设的，不是默认值）。
	/// 归一时必须分清"用户主动指定的"与"内核兜底默认的"，否则默认值会把头通道盖掉。
	/// </summary>
	public static bool IsExplicitlySet { get; private set; }

	public static string HttpUserAgent
	{
		get
		{
			return _httpUserAgent;
		}
		set
		{
			IsExplicitlySet = !string.IsNullOrWhiteSpace(value);
			_httpUserAgent = (string.IsNullOrWhiteSpace(value) ? "ai-player" : value.Trim());
		}
	}
}

/// <summary>
/// mpv **出站身份**（<c>User-Agent</c> 与头列表）的**唯一归一化落点**。
/// </summary>
/// <remarks>
/// 宿主可以用两种形态把 UA 交给内核：<c>--user-agent=&lt;v&gt;</c>（→ <see cref="ClientIdentity.HttpUserAgent"/>）、
/// 或 <c>--http-header={"name":"User-Agent",…}</c>（→ 头列表）。外部客户端
/// （<c>Richasy.MpvKernel.Core</c> 的 <c>PlayAsync</c>）把这二者分别写进 mpv 的**两个不同属性**：
/// 先 <c>user-agent</c>、再 <c>http-header-fields</c>；而线级实测**生效的只有 <c>user-agent</c>**
/// —— 只把 UA 放进 <c>--http-header</c> 时，媒体请求的 <c>User-Agent</c> 仍是内核默认值 ⇒ 头通道**静默失效**。
/// 这里把它**提升**为 <c>user-agent</c> 的值、并从头列表里删掉，让同一个事实**只有一个写点**（与 K7 同族）。
/// 另：外部客户端写这两个属性**本身没有日志**，所以这里必须留一行，否则事后无法判定到底应用了没有。
/// </remarks>
internal static class OutboundIdentity
{
	/// <summary>承载 UA 的头部名（大小写不敏感匹配）。</summary>
	public const string UserAgentHeaderName = "User-Agent";

	private static ILogger Log => Serilog.Log.ForContext(typeof(OutboundIdentity));

	/// <summary>
	/// 归一：返回 <c>(mpv 的 user-agent 值, 去掉 User-Agent 条目后的头列表)</c>。
	/// 优先级 = 显式 <c>--user-agent=</c> &gt; 头里的 <c>User-Agent</c> &gt; 内核默认值；头列表为空时返回 <c>null</c>（与原实现一致）。
	/// </summary>
	public static (string UserAgent, Dictionary<string, string>? Headers) Normalize(IReadOnlyDictionary<string, string>? headers)
	{
		string? userAgentFromHeader = null;
		Dictionary<string, string>? keptHeaders = null;
		if (headers != null && headers.Count > 0)
		{
			foreach (KeyValuePair<string, string> header in headers)
			{
				if (string.Equals(header.Key, UserAgentHeaderName, StringComparison.OrdinalIgnoreCase))
				{
					if (!string.IsNullOrWhiteSpace(header.Value))
					{
						userAgentFromHeader = header.Value.Trim();
					}
					continue;
				}
				(keptHeaders ??= new Dictionary<string, string>())[header.Key] = header.Value;
			}
		}
		string userAgent = ((ClientIdentity.IsExplicitlySet || userAgentFromHeader == null) ? ClientIdentity.HttpUserAgent : userAgentFromHeader);
		Log.Information("[StartupDiag] mpv outbound identity userAgent={UserAgent} headers={HeaderCount} fromHeader={FromHeader}", userAgent, keptHeaders?.Count ?? 0, userAgentFromHeader != null);
		return (userAgent, keptHeaders);
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinUISample.Models;

namespace WinUISample.Services;

internal sealed class PlayerUpdateServer : IDisposable
{
	private sealed class SegmentUpdatePayload
	{
		public List<HostNavigateSegmentOption>? Segments { get; set; }
	}

	// 请求体/命令的类型化 DTO 在 `WinUISample.Models.KernelControlCommand.cs`
	//   （`KernelControlPayload` 外层键 PascalCase `Commands`；`KernelControlCommand` 内层 camelCase
	//   `kind`/`trackId`/`seconds`/`visible`/`url`/`rate`/`level`，全部显式 `[JsonPropertyName]`）。
	//   此处不再内嵌 `ControlPayload`/`ControlCommand`（t38 的 `{name,value}` 过渡形态已删除）。

	private readonly HttpListener _listener;

	private readonly ILogger<PlayerUpdateServer> _logger;

	private readonly Action<IReadOnlyList<MediaSegment>> _onSegmentsReceived;

	/// <summary>控制命令落点（返回 <c>null</c> = 成功；非 null = 失败原因）。由 AppViewModel 注入到 PlayerViewModel。</summary>
	private readonly Func<KernelControlCommand, Task<string?>>? _onControlCommand;

	private CancellationTokenSource? _cts;

	private Task? _listenTask;

	private bool _disposed;

	public string? UpdateUrl { get; private set; }

	public PlayerUpdateServer(
		Action<IReadOnlyList<MediaSegment>> onSegmentsReceived,
		ILogger<PlayerUpdateServer> logger,
		Func<KernelControlCommand, Task<string?>>? onControlCommand = null)
	{
		_onSegmentsReceived = onSegmentsReceived;
		_logger = logger;
		_onControlCommand = onControlCommand;
		_listener = new HttpListener();
	}

	public bool Start()
	{
		if (_cts != null)
		{
			_logger.LogWarning("PlayerUpdateServer already started");
			return false;
		}
		try
		{
			// ── 端口选择──────────────────────────────────────────────────────────────
			// 原实现写死 "http://127.0.0.1:0/"，而 HttpListener **不接受端口 0**
			// （AddPrefixCore 抛 HttpListenerException(87) = ERROR_INVALID_PARAMETER）。
			// 后果：Start() 必失败 ⇒ UpdateUrl 恒为 null ⇒ 外壳永远拿不到 /segments 地址
			// （P0 实测三次同一异常，见 shell/docs/P0_LIVE_CONTROL_FEASIBILITY.md §3.2）。
			// 修法：先用 TcpListener 在回环上探一个**具体**空闲端口，再用它注册前缀；
			//       端口万一在"探到→注册"之间被抢，就换一个端口重试（最多 3 次）。
			int attempt = 0;
			while (true)
			{
				attempt++;
				int port = ReserveFreeLoopbackPort();
				_listener.Prefixes.Clear();
				_listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
				try
				{
					_listener.Start();
					_logger.LogInformation("PlayerUpdateServer 端口选定 {Port}（第 {Attempt} 次尝试）", port, attempt);
					break;
				}
				catch (HttpListenerException exception2) when (attempt < 3)
				{
					_logger.LogWarning(exception2, "PlayerUpdateServer 启动失败，换端口重试（第 {Attempt} 次）", attempt);
				}
			}
			string text = _listener.Prefixes.First();
			UpdateUrl = text.TrimEnd('/');
			_logger.LogInformation("PlayerUpdateServer started on {Url}", UpdateUrl);
			_cts = new CancellationTokenSource();
			_listenTask = ListenAsync(_cts.Token);
			return true;
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to start PlayerUpdateServer");
			UpdateUrl = null;
			return false;
		}
	}

	/// <summary>
	/// 让 OS 分配一个当前空闲的**回环**端口并立刻释放（随后由 <see cref="HttpListener"/> 注册使用）。
	/// 端口 0 只用于"让 OS 挑"，**不能**直接写进 HttpListener 的前缀。
	/// </summary>
	private static int ReserveFreeLoopbackPort()
	{
		System.Net.Sockets.TcpListener tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		try
		{
			tcpListener.Start();
			return ((System.Net.IPEndPoint)tcpListener.LocalEndpoint).Port;
		}
		finally
		{
			tcpListener.Stop();
		}
	}

	private async Task ListenAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				HandleRequestAsync(await _listener.GetContextAsync().WaitAsync(token), token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (HttpListenerException)
			{
				break;
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Error accepting connection");
			}
		}
	}

	/// <summary>
	/// <c>POST /control</c> —— **类型化 schema**（外层 PascalCase <c>Commands</c>、
	/// 内层 camelCase + 显式 <c>[JsonPropertyName]</c>）。语义 —— <b>204</b> = 全部命令已应用；
	/// <b>400</b> = 载荷不合法（JSON 失败 / 外层键大小写错 / 旧 <c>name-value</c> 形态 / 内层 PascalCase /
	/// 未知 kind / 缺类型化字段 / 数值非法 / <c>trackId</c> 回读不一致）；<b>500</b> = 未注入落点或 mpv 层失败。
	/// 每条错误都带"第几条 + 哪个命令 + 什么错"。
	/// </summary>
	private async Task HandleControlAsync(string text, HttpListenerResponse response)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(text);
		}
		catch (JsonException exception)
		{
			_logger.LogWarning(exception, "CONTROL-BAD-JSON");
			WriteControlError(response, 0, null, "bad-envelope-json");
			return;
		}
		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				_logger.LogWarning("CONTROL-BAD-SHAPE root={Kind}（根必须是 JSON 对象）", document.RootElement.ValueKind);
				WriteControlError(response, 0, null, "bad-envelope-shape");
				return;
			}
			if (!document.RootElement.TryGetProperty(KernelControlPayload.CommandsEnvelopeKey, out JsonElement commandsElement))
			{
				// 反控：外层写成小写 `commands` ⇒ 绑不上。这里**必须**用小写字面量（要构造的正是"大小写写错"这一种错），
				// 因此不能引用 CommandsEnvelopeKey —— 这一处是"故意与唯一构造点不同"，不是重复构造点。
				bool lowerCase = document.RootElement.TryGetProperty("commands", out _);
				_logger.LogWarning("CONTROL-ENVELOPE-MISSING lowerCaseCommands={Lower}（外层键必须是 PascalCase `{Expected}`）", lowerCase, KernelControlPayload.CommandsEnvelopeKey);
				WriteControlError(response, 0, null, lowerCase ? "bad-envelope-key" : "bad-envelope-missing");
				return;
			}
			if (commandsElement.ValueKind != JsonValueKind.Array || commandsElement.GetArrayLength() == 0)
			{
				_logger.LogWarning("CONTROL-EMPTY 命令集为空（`Commands` 必须是非空数组；kind={Kind}）", commandsElement.ValueKind);
				WriteControlError(response, 0, null, "bad-envelope-empty");
				return;
			}
			if (_onControlCommand == null)
			{
				_logger.LogWarning("CONTROL-NO-HANDLER 未注入控制落点");
				WriteControlError(response, 0, null, "no-control-handler", 500, null, RestOf(commandsElement, 0));
				return;
			}
			// ── 原子性口径（t187 K-04）──────────────────────────────────────────────
			// 本端点是**逐条 applied**（**不是**"全有全无"）：按数组顺序执行，遇到第一条失败**即停止**，
			// 且**已执行成功的命令不回滚**（mpv 侧也没有事务语义）。因此失败响应体必须给出
			// `applied`（已生效）+ `notExecuted`（未执行）+ `atomic:false`，让客户端**照读数回退**而不是靠猜。
			List<AppliedCommand> applied = new List<AppliedCommand>();
			int index = 0;
			foreach (JsonElement element in commandsElement.EnumerateArray())
			{
				index++;
				if (element.ValueKind != JsonValueKind.Object)
				{
					_logger.LogWarning("CONTROL-BAD-ITEM index={Index} jsonKind={Kind}", index, element.ValueKind);
					WriteControlError(response, index, null, "bad-payload:item-not-object", 400, applied, RestOf(commandsElement, index));
					return;
				}
				// 旧形态（t38 过渡形态 `{name,value}`）—— **明确拒绝**，不静默当成空命令。
				if (!element.TryGetProperty("kind", out _) && (element.TryGetProperty("name", out _) || element.TryGetProperty("value", out _)))
				{
					_logger.LogWarning("CONTROL-LEGACY-SCHEMA index={Index}（t38 的 name/value 形态已废弃；请用 kind + 类型化字段）", index);
					WriteControlError(response, index, null, "legacy-schema", 400, applied, RestOf(commandsElement, index));
					return;
				}
				// 内层 PascalCase（`Kind`/`TrackId`…）：默认大小写敏感 ⇒ 绑不上 ⇒ **明确指认**，不静默报"缺 kind"。
				if (!element.TryGetProperty("kind", out _) && element.TryGetProperty("Kind", out _))
				{
					_logger.LogWarning("CONTROL-INNER-CASE index={Index}（内层键必须 camelCase：kind/trackId/seconds/visible/url/rate/level）", index);
					WriteControlError(response, index, null, "envelope-case", 400, applied, RestOf(commandsElement, index));
					return;
				}
				KernelControlCommand command;
				try
				{
					command = element.Deserialize<KernelControlCommand>();
				}
				catch (JsonException exception2)
				{
					_logger.LogWarning(exception2, "CONTROL-BAD-ITEM-JSON index={Index}", index);
					WriteControlError(response, index, null, "bad-payload:item-json", 400, applied, RestOf(commandsElement, index));
					return;
				}
				if (command == null)
				{
					_logger.LogWarning("CONTROL-NULL-ITEM index={Index}", index);
					WriteControlError(response, index, null, "bad-payload:item-null", 400, applied, RestOf(commandsElement, index));
					return;
				}
				string? error = await _onControlCommand(command);
				if (!string.IsNullOrEmpty(error))
				{
					// 语义：**载荷面错误一律 400**（未知 kind / 缺字段 / 数值非法 /
					// 不存在的 trackId），并且**带"第几条 + 什么错"**；只有"落点缺失 / mpv 层失败或命令未生效"才是 500。
					bool payloadFault = IsPayloadFault(error);
					_logger.LogWarning("CONTROL-FAIL index={Index} kind={Kind} error={Error} => {Status}", index, command.Kind, error, (payloadFault ? 400 : 500));
					WriteControlError(response, index, command.Kind, error, payloadFault ? 400 : 500, applied, RestOf(commandsElement, index));
					return;
				}
				_logger.LogInformation("CONTROL-OK index={Index} kind={Kind}", index, command.Kind);
				applied.Add(new AppliedCommand(index, command.Kind));
			}
			response.StatusCode = 204;
			response.Close();
		}
	}

	/// <summary>
	/// 错误分类 —— <b>载荷面错误 ⇒ 400</b>（<c>bad-*</c> / <c>unknown-*</c> / <c>track-not-found</c> /
	/// <c>mpv-not-applied</c> / <c>danmaku-unavailable</c> / 旧形态 / 内层大小写）；其余（落点缺失、mpv 层写失败）⇒ <b>500</b>。
	/// <b>为什么 <c>track-not-found</c> 与 <c>mpv-not-applied</c> 归 400</b>：两者的语义都是
	/// "**这条命令没被接受**"（客户端据此提示/回退），**不是**服务端内部错误；用 500 会让外壳当**传输/内核故障**处理
	/// （可能触发重连/降级）= 语义错层。⇒ 这是对"mpv 层面失败 ⇒ 500"这条默认分类的**有意窄化，不是疏漏**。
	/// 两者各带自己的 <c>error</c> 码，客户端**按键区分**而不是看状态码。
	/// 同上：<c>danmaku-unavailable</c>（未配置弹幕源 ⇒ 命令没被接受，不得静默 204）。
	/// <b>命名映射（两个命名空间，勿混）</b>：wire 错误码 <c>envelope-case</c> ↔ 日志标记 <c>CONTROL-INNER-CASE</c>。
	/// </summary>
	private static bool IsPayloadFault(string error)
	{
		return error.StartsWith("bad-", StringComparison.Ordinal)
			|| error.StartsWith("unknown-", StringComparison.Ordinal)
			|| error.StartsWith("track-not-found", StringComparison.Ordinal)
			|| error.StartsWith("mpv-not-applied", StringComparison.Ordinal)
			|| error == "danmaku-unavailable"
			|| error == "legacy-schema"
			|| error == "envelope-case";
	}

	/// <summary>
	/// 400/500 的**明确错误体**（不是静默）：
	/// <c>{"index":2,"kind":"SetAudioTrack","name":"SetAudioTrack","error":"bad-track:aid:999:mpv-not-applied"}</c>。
	/// <c>kind</c> = 正式键；<c>name</c> = **兼容别名（值等于 kind）** —— 外壳 t39 既有解析器读的是 <c>name</c>
	/// （<c>shell/Services/Playback/KernelControlClient.cs:325-326 ParseErrorBody</c>），保留它可让两代解析器都拿到命令标识。
	/// </summary>
	/// <summary>错误体条目：**哪一条**（1-based 序号）与**哪个命令**（formal kind；识别不到时为空串）。</summary>
	private sealed record AppliedCommand(int Index, string Kind);

	/// <summary>
	/// **统一错误出口（t187 K-03）**：每个 4xx/5xx 出口都必须走这里，响应体是**可机器解析**的 JSON：
	/// <c>error</c>（稳定错误码，**既有码一个不改**）/ <c>reason</c>（人读原因）/ <c>rejectedField</c>（被拒字段）/
	/// <c>applied</c>（已生效的命令）/ <c>notExecuted</c>（未执行的命令）/ <c>atomic</c>（**恒 false** —— 本端点逐条 applied，见 HandleControlAsync）。
	/// </summary>
	private void WriteError(HttpListenerResponse response, int statusCode, string error, string reason, string? rejectedField, int index, string? kind, IReadOnlyList<AppliedCommand>? applied, IReadOnlyList<AppliedCommand>? notExecuted)
	{
		response.StatusCode = statusCode;
		Dictionary<string, object> body = new Dictionary<string, object>
		{
			["error"] = error,
			["reason"] = reason,
			["index"] = index,
			["kind"] = kind ?? string.Empty,
			["name"] = kind ?? string.Empty,
			["rejectedField"] = rejectedField ?? string.Empty,
			["atomic"] = false,
			["applied"] = ToWireList(applied),
			["notExecuted"] = ToWireList(notExecuted),
		};
		byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
		try
		{
			response.ContentType = "application/json";
			response.ContentLength64 = bytes.Length;
			response.OutputStream.Write(bytes, 0, bytes.Length);
			response.Close();
		}
		catch (Exception exception)
		{
			// 响应可能已被客户端中止：**错误路径不得再抛**（客户端至少已拿到状态码），如实记一条即可。
			_logger.LogWarning(exception, "WriteError 写出失败（响应可能已被中止）");
		}
	}

	private static List<Dictionary<string, object>> ToWireList(IReadOnlyList<AppliedCommand>? commands)
	{
		List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
		if (commands != null)
		{
			foreach (AppliedCommand command in commands)
			{
				list.Add(new Dictionary<string, object>
				{
					["index"] = command.Index,
					["kind"] = command.Kind,
				});
			}
		}
		return list;
	}

	/// <summary>稳定错误码 ⇒ 人读原因（**只加不改**：既有错误码一个字不动，新增的只是 <c>reason</c> 文案）。</summary>
	private static string ReasonFor(string error)
	{
		return error switch
		{
			"bad-envelope-json" => "请求体不是合法 JSON",
			"bad-envelope-shape" => "根必须是 JSON 对象",
			"bad-envelope-key" => "外层键大小写错（应为 PascalCase `Commands`）",
			"bad-envelope-missing" => "缺外层键 `Commands`",
			"bad-envelope-empty" => "`Commands` 必须是非空数组",
			"no-control-handler" => "内核未注入控制落点",
			"legacy-schema" => "旧 name/value 形态已废弃（请用 kind + 类型化字段）",
			"envelope-case" => "内层键大小写错（应为 camelCase：kind/trackId/seconds/visible/url/rate/level）",
			"bad-payload:item-not-object" => "命令项不是 JSON 对象",
			"bad-payload:item-json" => "命令项反序列化失败",
			"bad-payload:item-null" => "命令项反序列化为 null",
			"method-not-allowed" => "只接受 POST",
			"unknown-path" => "未知路径（仅 /segments 与 /control）",
			"empty-body" => "请求体为空",
			"bad-segments-empty" => "`Segments` 必须是非空数组",
			"bad-segments-json" => "`Segments` 载荷不是合法 JSON",
			"internal-error" => "服务端内部错误",
			_ => error,
		};
	}

	/// <summary>稳定错误码 ⇒ 被拒字段（**每个码都有确定值**；语义上无字段可指时为空串）。</summary>
	private static string RejectedFieldFor(string error, int index)
	{
		return error switch
		{
			"bad-envelope-json" or "bad-envelope-shape" or "bad-segments-json" => "body",
			"bad-envelope-key" or "bad-envelope-missing" or "bad-envelope-empty" => KernelControlPayload.CommandsEnvelopeKey,
			"bad-payload:item-not-object" or "bad-payload:item-json" or "bad-payload:item-null" => $"Commands[{index}]",
			"legacy-schema" => "kind",
			"envelope-case" => "Kind",
			"method-not-allowed" => "method",
			"unknown-path" => "path",
			"empty-body" => "body",
			"bad-segments-empty" => "Segments",
			_ => string.Empty,
		};
	}

	/// <summary>
	/// <c>POST /control</c> 的错误出口：**保留既有 <c>kind</c>/<c>name</c> 兼容别名**
	/// （外壳 t39 的 <c>ParseErrorBody</c> 读 <c>name</c>），并补齐 <c>reason</c>/<c>rejectedField</c>/<c>applied</c>/<c>notExecuted</c>。
	/// </summary>
	private void WriteControlError(HttpListenerResponse response, int index, string? kind, string error, int statusCode = 400, IReadOnlyList<AppliedCommand>? applied = null, IReadOnlyList<AppliedCommand>? notExecuted = null)
	{
		WriteError(response, statusCode, error, ReasonFor(error), RejectedFieldFor(error, index), index, kind, applied, notExecuted);
	}

	/// <summary>
	/// 失败点**之后**尚未执行的命令（<c>notExecuted</c>）：只看数组里每条**能识别到的** <c>kind</c>，
	/// 识别不到就记空串 —— **不**为了填字段去反序列化，避免"报错路径自己抛异常"。
	/// </summary>
	private static List<AppliedCommand> RestOf(JsonElement commandsElement, int appliedUpToIndex)
	{
		List<AppliedCommand> list = new List<AppliedCommand>();
		int position = 0;
		foreach (JsonElement element in commandsElement.EnumerateArray())
		{
			position++;
			if (position <= appliedUpToIndex)
			{
				continue;
			}
			string kind = string.Empty;
			if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kind", out JsonElement kindElement) && kindElement.ValueKind == JsonValueKind.String)
			{
				kind = kindElement.GetString() ?? string.Empty;
			}
			list.Add(new AppliedCommand(position, kind));
		}
		return list;
	}

	private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
	{
		HttpListenerRequest request = context.Request;
		HttpListenerResponse response = context.Response;
		try
		{
			if (request.HttpMethod != "POST")
			{
				_logger.LogWarning("HTTP-METHOD-NOT-ALLOWED method={Method}（只接受 POST）", request.HttpMethod);
				WriteError(response, 405, "method-not-allowed", ReasonFor("method-not-allowed"), RejectedFieldFor("method-not-allowed", 0), 0, null, null, null);
				return;
			}
			string path = request.Url?.AbsolutePath ?? "/";
			if (path != "/segments" && path != "/control")
			{
				_logger.LogWarning("HTTP-UNKNOWN-PATH path={Path}（仅 /segments 与 /control）", path);
				WriteError(response, 404, "unknown-path", ReasonFor("unknown-path") + "：" + path, RejectedFieldFor("unknown-path", 0), 0, null, null, null);
				return;
			}
			using StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
			string text = await reader.ReadToEndAsync(token);
			if (string.IsNullOrWhiteSpace(text))
			{
				// **K-03 的正面案例**：空体 400 必须带错误体 —— 改前这里是裸 400（响应体 0 字节）。
				_logger.LogWarning("HTTP-EMPTY-BODY path={Path}（请求体为空）", path);
				WriteError(response, 400, "empty-body", ReasonFor("empty-body"), RejectedFieldFor("empty-body", 0), 0, null, null, null);
				return;
			}
			if (path == "/control")
			{
				await HandleControlAsync(text, response);
				return;
			}
			SegmentUpdatePayload segmentUpdatePayload = JsonSerializer.Deserialize<SegmentUpdatePayload>(text);
			if (segmentUpdatePayload?.Segments == null || segmentUpdatePayload.Segments.Count == 0)
			{
				_logger.LogWarning("SEGMENTS-EMPTY 载荷里没有 Segments（必须是非空数组）");
				WriteError(response, 400, "bad-segments-empty", ReasonFor("bad-segments-empty"), RejectedFieldFor("bad-segments-empty", 0), 0, null, null, null);
				return;
			}
			List<MediaSegment> list = (from opt in segmentUpdatePayload.Segments
									   select opt.ToMediaSegment() into s
									   where s != null
									   select s).Cast<MediaSegment>().ToList();
			if (list.Count > 0)
			{
				_logger.LogInformation("Received {Count} segment(s) from Flutter", list.Count);
				_onSegmentsReceived(list);
			}
			response.StatusCode = 204;
			response.Close();
		}
		catch (JsonException exception)
		{
			_logger.LogWarning(exception, "Failed to parse segment payload");
			WriteError(response, 400, "bad-segments-json", ReasonFor("bad-segments-json"), RejectedFieldFor("bad-segments-json", 0), 0, null, null, null);
		}
		catch (Exception exception2)
		{
			_logger.LogWarning(exception2, "Error handling request");
			WriteError(response, 500, "internal-error", ReasonFor("internal-error"), RejectedFieldFor("internal-error", 0), 0, null, null, null);
		}
	}

	public void Stop()
	{
		if (_cts != null)
		{
			_cts.Cancel();
			_listener.Stop();
			try
			{
				_listenTask?.Wait(TimeSpan.FromSeconds(2L));
			}
			catch (AggregateException)
			{
			}
			_cts.Dispose();
			_cts = null;
			_listenTask = null;
			UpdateUrl = null;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Stop();
			_listener.Close();
			_disposed = true;
		}
	}
}

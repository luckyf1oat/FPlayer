// POST /control 的**类型化**载荷：外层键 PascalCase、内层字段 camelCase 且逐字段显式 [JsonPropertyName]。
//   全字符串的 {name,value} 形态已被本文件取代 —— 它会把"非法值"退化成"解析失败但看起来成功"。
//
// 契约（外层 PascalCase、内层 camelCase + 显式 [JsonPropertyName]；"一节内两种大小写"是设计如此）：
//   POST {updateUrl}/control
//   { "Commands": [
//       { "kind":"SetAudioTrack",        "trackId":3      },
//       { "kind":"SetSubtitleTrack",     "trackId":5      },   // -1（或 0 / "no"）= 关闭；"auto" = 由 mpv 选
//       { "kind":"SetSubtitleDelay",     "seconds":0.5    },
//       { "kind":"SetSubtitleVisibility","visible":true   },
//       { "kind":"AddExternalSubtitle",  "url":"…"        },
//       { "kind":"SetSpeed",             "rate":1.25      },
//       { "kind":"SetVolume",            "level":80       },
//       { "kind":"SeekAbsolute",         "seconds":123.4  }
//   ] }
//
// 为什么类型化：`seconds`/`trackId`/`rate`/`level` 是**数字**、`visible` 是**布尔**，
//   JSON 层就能把"非法值"钉成 400（`"seconds":"abc"` ⇒ 绑定失败/类型不符 ⇒ 400），
//   而全字符串的 `value` 会把"非法"退化成"解析失败但看起来成功"。
// ⚠️ 大小写敏感是**故意的**：外层写小写 `commands` ⇒ 绑定为空 ⇒ 400（反控）；
//   内层写 PascalCase `Kind`/`TrackId` ⇒ 同样绑不上 ⇒ 由 PlayerUpdateServer 显式回
//   `envelope-case` 400（**不静默**，提示"内层键必须 camelCase"）。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUISample.Models;

/// <summary>
/// <c>POST /control</c> 的请求体。外层键**必须**是 PascalCase <c>Commands</c>。
/// <b>public</b>：与「入参侧 DTO 一律 public」的既有规约一致（<c>HostLaunchOptions</c> 同样 public；
/// 只有**响应侧** DTO 才是 internal —— 见 T12_CALLBACK_SURFACE.md §F4）。
/// </summary>
public sealed class KernelControlPayload
{
	/// <summary>
	/// 外层键（PascalCase）的**唯一构造点**：序列化特性与服务端的绑定查找共用它，避免契约字符串散落两处、改一处漏一处。
	/// 大小写敏感是设计（小写 <c>commands</c> 绑不上，服务端有一条专门指认它的反控分支）。
	/// </summary>
	public const string CommandsEnvelopeKey = "Commands";

	[JsonPropertyName(CommandsEnvelopeKey)]
	public List<KernelControlCommand> Commands { get; set; }
}

/// <summary>
/// 单条类型化控制命令。<c>kind</c> 取 8 个语义名之一；**只解释该 kind 对应的字段**。
/// <c>trackId</c> 用 <see cref="JsonElement"/>，以同时接受数字 id 与 <c>"no"</c>/<c>"auto"</c>（外壳既有的枚举形态）。
/// </summary>
public sealed class KernelControlCommand
{
	public const string KindSetAudioTrack = "SetAudioTrack";

	public const string KindSetSubtitleTrack = "SetSubtitleTrack";

	public const string KindSetSubtitleDelay = "SetSubtitleDelay";

	public const string KindSetSubtitleVisibility = "SetSubtitleVisibility";

	public const string KindAddExternalSubtitle = "AddExternalSubtitle";

	public const string KindSetSpeed = "SetSpeed";

	public const string KindSetVolume = "SetVolume";

	public const string KindSeekAbsolute = "SeekAbsolute";

	/// <summary>
	/// 弹幕开关，也是第 9 条控制命令。写的是 <c>PlayerViewModel.IsDanmakuEnabled</c>
	/// （播放器浮层菜单写的是同一个属性 ⇒ 两条入口共用一条生效链，不另开写点）。
	/// 线上形态：<c>{"kind":"SetDanmakuEnabled","enabled":true|false}</c>。
	/// </summary>
	public const string KindSetDanmakuEnabled = "SetDanmakuEnabled";

	[JsonPropertyName("kind")]
	public string Kind { get; set; }

	[JsonPropertyName("trackId")]
	public JsonElement? TrackId { get; set; }

	[JsonPropertyName("seconds")]
	public double? Seconds { get; set; }

	[JsonPropertyName("visible")]
	public bool? Visible { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; }

	[JsonPropertyName("rate")]
	public double? Rate { get; set; }

	[JsonPropertyName("level")]
	public int? Level { get; set; }

	/// <summary><c>SetDanmakuEnabled</c> 的 bool 字段（与 <c>SetSubtitleVisibility</c>+<c>visible</c> 同构）。</summary>
	[JsonPropertyName("enabled")]
	public bool? Enabled { get; set; }
}

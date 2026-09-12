using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.AIPlayer_MpvHost_XamlTypeInfo;
using WinUISample.Models;
using WinUISample.Models.Constants;
using WinUISample.ViewModels;

namespace WinUISample;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IApplicationOverrides")]
[WinRTExposedType(typeof(WinUISample_AppWinRTTypeDetails))]
public class App : Application, IXamlMetadataProvider
{
	private static bool _isHandlingUnhandledException;

	private ILogger<App>? _logger;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private XamlMetaDataProvider __appProvider;

	internal const string AppDataRootOverrideEnvVar = "AIPLAYER_APPDATA_ROOT";

	private static string _appDataRoot;

	private static bool _appDataRootResolved;

	/// <summary>
	/// 本机数据根。**优先级 = 显式覆盖（<see cref="AppDataRootOverrideEnvVar"/>） &gt; 默认（`LocalApplicationData\AIPlayer`）**。
	/// 为什么需要它：`SpecialFolder.LocalApplicationData` 走 `SHGetKnownFolderPath`，**不读 `LOCALAPPDATA` 环境变量** ⇒
	/// 起内核的测试此前无法沙箱化，必然写用户真实根（`t98` 实测：播放结束后 14 ms 改写了用户 `settings.json`）。
	/// 覆盖值非法（非绝对路径 / 建不出来 / 不可写）⇒ 落一行显式 `[ROOT-OVERRIDE-REJECTED]`（先 stderr，日志就绪前唯一可见通道）
	/// 并**回落到默认根**；选"回落 + 显式行"而不抛异常的理由：**一个拼错的环境变量不该让播放器起不来**。
	/// **不设该变量时逐字等于原实现**（`Path.Combine(GetFolderPath(LocalApplicationData), "AIPlayer")`），行为不变。
	/// </summary>
	internal static string AppDataRoot
	{
		get
		{
			if (!_appDataRootResolved)
			{
				_appDataRoot = ResolveAppDataRoot();
				_appDataRootResolved = true;
			}
			return _appDataRoot;
		}
	}

	/// <summary>覆盖解析结果：`default` / `override` / `rejected:&lt;reason&gt;`（供启动日志与反控读数使用）。</summary>
	internal static string AppDataRootSource { get; private set; } = "unresolved";

	private static string DefaultAppDataRoot()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIPlayer");
	}

	/// <summary>legacy 迁移源根的可覆盖入口（t192 / K-06）。不设该变量时**逐字等于原实现**。</summary>
	private const string LegacyRootOverrideEnvVar = "AIPLAYER_LEGACY_ROOT";

	private static string LegacyRootDefault()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIPlayer.MpvHost");
	}

	private static string _legacyRoot;

	private static bool _legacyRootResolved;

	/// <summary>
	/// legacy 迁移的**源根**（读侧）。默认 = 真实 `LocalApplicationData\AIPlayer.MpvHost`；
	/// 设 <see cref="LegacyRootOverrideEnvVar"/> ⇒ 读该根 ⇒ **沙箱化运行不再触碰用户真实 profile**。
	/// 拒绝规则与 <see cref="ResolveAppDataRoot"/> 共用同一套纯路径判定（见 <see cref="ValidateRootCandidate"/>）。
	/// </summary>
	internal static string LegacyRoot
	{
		get
		{
			if (!_legacyRootResolved)
			{
				_legacyRoot = ResolveLegacyRoot();
				_legacyRootResolved = true;
			}
			return _legacyRoot;
		}
	}

	/// <summary>legacy 源根解析结果：`default` / `override` / `rejected:&lt;reason&gt;`（供启动诊断与反控读数使用）。</summary>
	internal static string LegacyRootSource { get; private set; } = "unresolved";

	private static string ResolveLegacyRoot()
	{
		string text = Environment.GetEnvironmentVariable(LegacyRootOverrideEnvVar);
		if (string.IsNullOrWhiteSpace(text))
		{
			LegacyRootSource = "default";
			return LegacyRootDefault();
		}
		string text2 = text.Trim();
		string? text3 = ValidateRootCandidate(text2);
		if (text3 != null)
		{
			LegacyRootSource = "rejected:" + text3;
			WriteRootRejectedLine(LegacyRootOverrideEnvVar, text2, text3);
			return LegacyRootDefault();
		}
		LegacyRootSource = "override";
		return text2;
	}

	/// <summary>
	/// **纯路径合法性判定（t192 / K-12：先判后写）** —— 本方法**不做任何文件系统写操作**，
	/// 因此非法/恶意候选根**不会被创建、也不会被写入探针**。
	/// 返回 <c>null</c> = 通过；否则返回**机器可判**的拒绝原因。
	/// 判据顺序：空 ⇒ 非绝对路径 ⇒ 非法字符（含通配 `*` `?`）⇒ 设备命名空间 ⇒ 盘根/驱动器相对 ⇒ 过长。
	/// **注意**：这里只判"路径形态"，"可写性"仍由随后的探针写决定（探针只在形态通过后才发生）。
	/// </summary>
	private static string? ValidateRootCandidate(string candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate))
		{
			return "empty";
		}
		if (!Path.IsPathRooted(candidate))
		{
			return "not-absolute";
		}
		char[] invalid = Path.GetInvalidPathChars();
		if (candidate.IndexOfAny(invalid) >= 0 || candidate.IndexOf('*') >= 0 || candidate.IndexOf('?') >= 0)
		{
			return "illegal-char";
		}
		if (candidate.StartsWith("\\\\.\\", StringComparison.Ordinal) || candidate.StartsWith("\\\\?\\GLOBALROOT", StringComparison.OrdinalIgnoreCase))
		{
			return "device-namespace";
		}
		string pathRoot = Path.GetPathRoot(candidate) ?? string.Empty;
		if (string.Equals(pathRoot.TrimEnd('\\', '/'), candidate.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
		{
			return "path-is-drive-root";
		}
		if (candidate.Length > 240)
		{
			return "too-long";
		}
		return null;
	}

	private static void WriteRootRejectedLine(string envVar, string value, string reason)
	{
		try
		{
			// t237：`value=` 是**用户可控输入**（环境变量原文，可能是带 token/api_key 的 URL）⇒ 必须先经
			// **唯一打码真源** `SecretMaskingTextFormatter.MaskSecrets(...)` 再落 stderr（与 Serilog sink 同一实现 ⇒ 同形态），
			// 不在此处新增第二套正则；非凭据形态的普通路径**原样保留**（可读性不受影响）。
			Console.Error.WriteLine(WinUISample.Services.SecretMaskingTextFormatter.MaskSecrets(
				"[ROOT-OVERRIDE-REJECTED] env=" + envVar + " value=" + value + " reason=" + reason + " action=fallback-to-default"));
		}
		catch (Exception ex)
		{
			// t237 显式理由（**有意不掩**）：本行插值项**逐字枚举** = `ex.GetType().Name`（唯一一项）。
			// 用户可控输入数 = **0**：`envVar` / `value` / `reason` 都不在本语句内，而 `ex` 的类型名取自固定异常集合（非用户数据）。
			// ⚠️ 护栏（可失败判据）：**若将来有人往本行加入任何用户可控值**（value / path / url / header / 命令行参数），
			//    打码即变为必需 —— 处置 = 用 `SecretMaskingTextFormatter.MaskSecrets(...)` 包住整条语句；
			//    否则本行即刻构成本族的第五处旁路（同 K-01/K-02 的形状）。
			Debug.WriteLine("ROOT-OVERRIDE-REJECTED stderr write failed: " + ex.GetType().Name);
		}
	}

	private static string ResolveAppDataRoot()
	{
		string text = Environment.GetEnvironmentVariable(AppDataRootOverrideEnvVar);
		if (string.IsNullOrWhiteSpace(text))
		{
			AppDataRootSource = "default";
			return DefaultAppDataRoot();
		}
		string text2 = text.Trim();
		// K-12：**先做纯路径判定（无任何写操作）**，通过后才允许创建目录并写探针。
		string? text3 = ValidateRootCandidate(text2);
		if (text3 == null)
		{
			try
			{
				Directory.CreateDirectory(text2);
				string text4 = Path.Combine(text2, ".write-probe-" + Environment.ProcessId);
				try
				{
					File.WriteAllText(text4, "ok");
				}
				finally
				{
					// 即便写入抛出，也尽力清掉探针（旧实现在 Delete 抛出时会留下探针文件）。
					if (File.Exists(text4))
					{
						File.Delete(text4);
					}
				}
			}
			catch (Exception ex)
			{
				text3 = ex.GetType().Name;
			}
		}
		if (text3 != null)
		{
			AppDataRootSource = "rejected:" + text3;
			WriteRootRejectedLine(AppDataRootOverrideEnvVar, text2, text3);
			return DefaultAppDataRoot();
		}
		AppDataRootSource = "override";
		return text2;
	}

	public static string LoggerFolder { get; private set; }

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	private XamlMetaDataProvider _AppProvider
	{
		get
		{
			if (__appProvider == null)
			{
				__appProvider = new XamlMetaDataProvider();
			}
			return __appProvider;
		}
	}

	private static HostLaunchOptions ParseLaunchOptions(string[] args)
	{
		args = ExpandLaunchFileArgs(args);
		string libMpvPath = null;
		string mediaPath = null;
		string title = null;
		string subtitle = null;
		string subtitleId = null;
		string subtitleUrl = null;
		string callbackUrl = null;
		string previousEpisodeId = null;
		string nextEpisodeId = null;
		string seasonId = null;
		string monogram = null;
		string logo = null;
		string backdropUrl = null;
		string httpProxy = null;
		List<DanmakuApiEntry> list = new List<DanmakuApiEntry>();
		string danmakuMatchName = null;
		List<EpisodeListItem> list2 = new List<EpisodeListItem>();
		List<MediaSegment> list3 = new List<MediaSegment>();
		List<TodbChapter> list4 = new List<TodbChapter>();
		TodbSprite sprite = null;
		string mpvConfigDir = null;
		bool fitVideoSize = true;
		VideoFitMode videoFitMode = VideoFitMode.Contain;
		double? startPosition = null;
		bool defaultRtxVsr = false;
		bool defaultRtxVideoHdr = false;
		string defaultAnimeMode = "none";
		string defaultSharpenMode = "none";
		bool autoPlayNextEpisode = true;
		int maxVolume = 100;
		List<string> list5 = new List<string>();
		List<OverlayVersionOption> list6 = new List<OverlayVersionOption>();
		List<OverlayTrackOption> list7 = new List<OverlayTrackOption>();
		List<OverlayTrackOption> list8 = new List<OverlayTrackOption>();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string userAgent = null;
		bool disableSkipMarkers = false;
		PlayerShortcuts shortcuts = PlayerShortcuts.Default;
		int? parentPid = null;
		foreach (string item in args.Skip(1))
		{
			double result;
			int result2;
			int result4;
			if (item.StartsWith("--libmpv=", StringComparison.OrdinalIgnoreCase))
			{
				libMpvPath = item.Substring("--libmpv=".Length).Trim('"');
			}
			else if (item.StartsWith("--open=", StringComparison.OrdinalIgnoreCase))
			{
				mediaPath = item.Substring("--open=".Length).Trim('"');
			}
			else if (item.StartsWith("--title=", StringComparison.OrdinalIgnoreCase))
			{
				title = item.Substring("--title=".Length).Trim('"');
			}
			else if (item.StartsWith("--subtitle=", StringComparison.OrdinalIgnoreCase))
			{
				subtitle = item.Substring("--subtitle=".Length).Trim('"');
			}
			else if (item.StartsWith("--subtitle-id=", StringComparison.OrdinalIgnoreCase))
			{
				subtitleId = item.Substring("--subtitle-id=".Length).Trim('"');
			}
			else if (item.StartsWith("--subtitle-url=", StringComparison.OrdinalIgnoreCase))
			{
				subtitleUrl = item.Substring("--subtitle-url=".Length).Trim('"');
			}
			else if (item.StartsWith("--callback-url=", StringComparison.OrdinalIgnoreCase))
			{
				callbackUrl = item.Substring("--callback-url=".Length).Trim('"');
			}
			else if (item.StartsWith("--previous-episode-id=", StringComparison.OrdinalIgnoreCase))
			{
				previousEpisodeId = item.Substring("--previous-episode-id=".Length).Trim('"');
			}
			else if (item.StartsWith("--next-episode-id=", StringComparison.OrdinalIgnoreCase))
			{
				nextEpisodeId = item.Substring("--next-episode-id=".Length).Trim('"');
			}
			else if (item.StartsWith("--season-id=", StringComparison.OrdinalIgnoreCase))
			{
				seasonId = item.Substring("--season-id=".Length).Trim('"');
			}
			else if (item.StartsWith("--monogram=", StringComparison.OrdinalIgnoreCase))
			{
				monogram = item.Substring("--monogram=".Length).Trim('"');
			}
			else if (item.StartsWith("--logo=", StringComparison.OrdinalIgnoreCase))
			{
				logo = item.Substring("--logo=".Length).Trim('"');
			}
			else if (item.StartsWith("--backdrop=", StringComparison.OrdinalIgnoreCase))
			{
				backdropUrl = item.Substring("--backdrop=".Length).Trim('"');
			}
			else if (item.StartsWith("--badge=", StringComparison.OrdinalIgnoreCase))
			{
				string text = item.Substring("--badge=".Length).Trim('"');
				if (!string.IsNullOrWhiteSpace(text))
				{
					list5.Add(text);
				}
			}
			else if (item.StartsWith("--audio-track=", StringComparison.OrdinalIgnoreCase))
			{
				OverlayTrackOption overlayTrackOption = ParseTrackOption(item.Substring("--audio-track=".Length).Trim('"'));
				if (overlayTrackOption != null)
				{
					list7.Add(overlayTrackOption);
				}
			}
			else if (item.StartsWith("--version-option=", StringComparison.OrdinalIgnoreCase))
			{
				OverlayVersionOption overlayVersionOption = ParseVersionOption(item.Substring("--version-option=".Length).Trim('"'));
				if (overlayVersionOption != null)
				{
					list6.Add(overlayVersionOption);
				}
			}
			else if (item.StartsWith("--subtitle-track=", StringComparison.OrdinalIgnoreCase))
			{
				OverlayTrackOption overlayTrackOption2 = ParseTrackOption(item.Substring("--subtitle-track=".Length).Trim('"'));
				if (overlayTrackOption2 != null)
				{
					list8.Add(overlayTrackOption2);
				}
			}
			else if (item.StartsWith("--http-header=", StringComparison.OrdinalIgnoreCase))
			{
				(string, string)? tuple = ParseHeaderOption(item.Substring("--http-header=".Length).Trim('"'));
				if (tuple.HasValue)
				{
					dictionary[tuple.Value.Item1] = tuple.Value.Item2;
				}
			}
			else if (item.StartsWith("--user-agent=", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = item.Substring("--user-agent=".Length).Trim('"');
				if (!string.IsNullOrWhiteSpace(text2))
				{
					userAgent = text2;
				}
			}
			else if (item.Equals("--disable-skip-markers", StringComparison.OrdinalIgnoreCase))
			{
				disableSkipMarkers = true;
			}
			else if (item.StartsWith("--start=", StringComparison.OrdinalIgnoreCase) && double.TryParse(item.Substring("--start=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
			{
				startPosition = result;
			}
			else if (item.StartsWith("--shortcuts=", StringComparison.OrdinalIgnoreCase))
			{
				shortcuts = PlayerShortcuts.FromBase64(item.Substring("--shortcuts=".Length));
			}
			else if (item.StartsWith("--http-proxy=", StringComparison.OrdinalIgnoreCase))
			{
				string text3 = item.Substring("--http-proxy=".Length).Trim('"');
				if (!string.IsNullOrWhiteSpace(text3))
				{
					httpProxy = text3;
				}
			}
			else if (item.StartsWith("--danmaku-api=", StringComparison.OrdinalIgnoreCase))
			{
				DanmakuApiEntry danmakuApiEntry = ParseDanmakuApiEntry(item.Substring("--danmaku-api=".Length).Trim('"'));
				if (danmakuApiEntry != null && list.Count < 5)
				{
					list.Add(danmakuApiEntry);
				}
			}
			else if (item.StartsWith("--danmaku-match-name=", StringComparison.OrdinalIgnoreCase))
			{
				string text4 = item.Substring("--danmaku-match-name=".Length).Trim('"');
				if (!string.IsNullOrWhiteSpace(text4))
				{
					danmakuMatchName = Uri.UnescapeDataString(text4);
				}
			}
			else if (item.StartsWith("--mpv-config-dir=", StringComparison.OrdinalIgnoreCase))
			{
				string text5 = item.Substring("--mpv-config-dir=".Length).Trim('"');
				if (!string.IsNullOrWhiteSpace(text5))
				{
					mpvConfigDir = text5;
				}
			}
			else if (item.StartsWith("--fit-video-size=", StringComparison.OrdinalIgnoreCase))
			{
				fitVideoSize = !string.Equals(item.Substring("--fit-video-size=".Length).Trim('"'), "false", StringComparison.OrdinalIgnoreCase);
			}
			else if (item.StartsWith("--video-fit-mode=", StringComparison.OrdinalIgnoreCase))
			{
				VideoFitMode videoFitMode2;
				switch (item.Substring("--video-fit-mode=".Length).Trim('"').ToLowerInvariant())
				{
					case "cover":
					case "fill":
						videoFitMode2 = VideoFitMode.Cover;
						break;
					case "stretch":
						videoFitMode2 = VideoFitMode.Stretch;
						break;
					default:
						videoFitMode2 = VideoFitMode.Contain;
						break;
				}
				videoFitMode = videoFitMode2;
			}
			else if (item.StartsWith("--parent-pid=", StringComparison.OrdinalIgnoreCase) && int.TryParse(item.Substring("--parent-pid=".Length), out result2) && result2 > 0)
			{
				parentPid = result2;
			}
			else if (item.StartsWith("--episode-list=", StringComparison.OrdinalIgnoreCase))
			{
				string encoded = item.Substring("--episode-list=".Length).Trim('"');
				list2.AddRange(ParseEpisodeList(encoded));
			}
			else if (item.StartsWith("--segment=", StringComparison.OrdinalIgnoreCase))
			{
				MediaSegment mediaSegment = ParseSegment(item.Substring("--segment=".Length).Trim('"'));
				if (mediaSegment != null)
				{
					list3.Add(mediaSegment);
				}
			}
			else if (item.StartsWith("--chapter=", StringComparison.OrdinalIgnoreCase))
			{
				TodbChapter todbChapter = ParseChapter(item.Substring("--chapter=".Length).Trim('"'));
				if (todbChapter != null)
				{
					list4.Add(todbChapter);
				}
			}
			else if (item.StartsWith("--sprite=", StringComparison.OrdinalIgnoreCase))
			{
				sprite = ParseSprite(item.Substring("--sprite=".Length).Trim('"'));
			}
			else if (item.Equals("--rtx-vsr=true", StringComparison.OrdinalIgnoreCase))
			{
				defaultRtxVsr = true;
			}
			else if (item.Equals("--rtx-video-hdr=true", StringComparison.OrdinalIgnoreCase))
			{
				defaultRtxVideoHdr = true;
			}
			else if (item.StartsWith("--anime-mode=", StringComparison.OrdinalIgnoreCase))
			{
				defaultAnimeMode = item.Substring("--anime-mode=".Length).Trim('"').ToLowerInvariant();
			}
			else if (item.StartsWith("--sharpen-mode=", StringComparison.OrdinalIgnoreCase))
			{
				defaultSharpenMode = item.Substring("--sharpen-mode=".Length).Trim('"').ToLowerInvariant();
			}
			else if (item.StartsWith("--auto-play-next-episode=", StringComparison.OrdinalIgnoreCase))
			{
				autoPlayNextEpisode = !bool.TryParse(item.Substring("--auto-play-next-episode=".Length).Trim('"'), out var result3) | result3;
			}
			else if (item.StartsWith("--max-volume=", StringComparison.OrdinalIgnoreCase) && int.TryParse(item.Substring("--max-volume=".Length).Trim('"'), out result4))
			{
				maxVolume = Math.Clamp(result4, 100, 200);
			}
		}
		return new HostLaunchOptions
		{
			LibMpvPath = libMpvPath,
			MediaPath = mediaPath,
			StartPosition = startPosition,
			Title = title,
			Subtitle = subtitle,
			SubtitleId = subtitleId,
			SubtitleUrl = subtitleUrl,
			CallbackUrl = callbackUrl,
			PreviousEpisodeId = previousEpisodeId,
			NextEpisodeId = nextEpisodeId,
			SeasonId = seasonId,
			Monogram = monogram,
			Logo = logo,
			BackdropUrl = backdropUrl,
			Badges = list5,
			VersionOptions = list6,
			AudioTracks = list7,
			SubtitleTracks = list8,
			HttpHeaders = dictionary,
			UserAgent = userAgent,
			DisableSkipMarkers = disableSkipMarkers,
			Shortcuts = shortcuts,
			HttpProxy = httpProxy,
			DanmakuApis = list,
			DanmakuMatchName = danmakuMatchName,
			// 这里能置 true 的理由：danmakuMatchName 只可能在 `--danmaku-match-name=` 分支被赋值，
			// 而那条分支的约定就是"命令行传编码值、解析器解码" ⇒ 走到这里它已经是原始名。
			DanmakuMatchNameDecodedFromCli = !string.IsNullOrWhiteSpace(danmakuMatchName),
			MpvConfigDir = mpvConfigDir,
			FitVideoSize = fitVideoSize,
			VideoFitMode = videoFitMode,
			ParentPid = parentPid,
			EpisodeList = list2,
			Segments = list3,
			Chapters = list4,
			Sprite = sprite,
			DefaultRtxVsr = defaultRtxVsr,
			DefaultRtxVideoHdr = defaultRtxVideoHdr,
			DefaultAnimeMode = defaultAnimeMode,
			DefaultSharpenMode = defaultSharpenMode,
			AutoPlayNextEpisode = autoPlayNextEpisode,
			MaxVolume = maxVolume
		};
	}

	private static string[] ExpandLaunchFileArgs(string[] args)
	{
		List<string> list = new List<string> { (args.Length != 0) ? args[0] : string.Empty };
		foreach (string item in args.Skip(1))
		{
			if (!item.StartsWith("--launch-file=", StringComparison.OrdinalIgnoreCase))
			{
				list.Add(item);
				continue;
			}
			string text = item.Substring("--launch-file=".Length).Trim('"');
			if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
			{
				continue;
			}
			try
			{
				string[] array = JsonSerializer.Deserialize<string[]>(File.ReadAllText(text));
				if (array != null)
				{
					list.AddRange(array.Where((string item) => !string.IsNullOrWhiteSpace(item)));
				}
			}
			catch
			{
			}
			finally
			{
				try
				{
					File.Delete(text);
				}
				catch
				{
				}
			}
		}
		return list.ToArray();
	}

	private static OverlayTrackOption? ParseTrackOption(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			string text2 = (rootElement.TryGetProperty("label", out var value) ? value.GetString() : null);
			if (string.IsNullOrWhiteSpace(text2))
			{
				return null;
			}
			string text3 = (rootElement.TryGetProperty("id", out var value2) ? value2.GetString() : null);
			string text4 = (rootElement.TryGetProperty("url", out var value3) ? value3.GetString() : null);
			string text5 = (rootElement.TryGetProperty("title", out var value4) ? value4.GetString() : null);
			string text6 = (rootElement.TryGetProperty("language", out var value5) ? value5.GetString() : null);
			bool flag = rootElement.TryGetProperty("external", out var value6);
			if (flag)
			{
				JsonValueKind valueKind = value6.ValueKind;
				bool flag2 = valueKind - 5 <= JsonValueKind.Object;
				flag = flag2;
			}
			bool isExternal = flag && value6.GetBoolean();
			flag = rootElement.TryGetProperty("selected", out var value7);
			if (flag)
			{
				JsonValueKind valueKind = value7.ValueKind;
				bool flag2 = valueKind - 5 <= JsonValueKind.Object;
				flag = flag2;
			}
			bool isSelected = flag && value7.GetBoolean();
			flag = rootElement.TryGetProperty("special", out var value8);
			if (flag)
			{
				JsonValueKind valueKind = value8.ValueKind;
				bool flag2 = valueKind - 5 <= JsonValueKind.Object;
				flag = flag2;
			}
			bool isSpecial = flag && value8.GetBoolean();
			int embyStreamIndex = ((rootElement.TryGetProperty("embyIndex", out var value9) && value9.ValueKind == JsonValueKind.Number && value9.TryGetInt32(out var value10)) ? value10 : (-1));
			return new OverlayTrackOption
			{
				Id = (text3 ?? string.Empty),
				Label = text2,
				Url = (text4 ?? string.Empty),
				Title = (text5 ?? string.Empty),
				Language = (text6 ?? string.Empty),
				IsExternal = isExternal,
				IsSelected = isSelected,
				IsSpecial = isSpecial,
				EmbyStreamIndex = embyStreamIndex
			};
		}
		catch
		{
			return null;
		}
	}

	private static OverlayVersionOption? ParseVersionOption(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			string text2 = (rootElement.TryGetProperty("label", out var value) ? value.GetString() : null);
			if (string.IsNullOrWhiteSpace(text2))
			{
				return null;
			}
			string text3 = (rootElement.TryGetProperty("id", out var value2) ? value2.GetString() : null);
			int num = ((rootElement.TryGetProperty("index", out var value3) && value3.TryGetInt32(out var value4)) ? value4 : (-1));
			bool flag = rootElement.TryGetProperty("selected", out var value5);
			if (flag)
			{
				JsonValueKind valueKind = value5.ValueKind;
				bool flag2 = valueKind - 5 <= JsonValueKind.Object;
				flag = flag2;
			}
			bool isSelected = flag && value5.GetBoolean();
			return (num < 0) ? null : new OverlayVersionOption
			{
				Index = num,
				Id = (text3 ?? string.Empty),
				Label = text2,
				IsSelected = isSelected
			};
		}
		catch
		{
			return null;
		}
	}

	private static (string Name, string Value)? ParseHeaderOption(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			string text2 = (rootElement.TryGetProperty("name", out var value) ? value.GetString() : null);
			string text3 = (rootElement.TryGetProperty("value", out var value2) ? value2.GetString() : null);
			if (string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
			{
				return null;
			}
			return (text2, text3);
		}
		catch
		{
			return null;
		}
	}

	private static DanmakuApiEntry? ParseDanmakuApiEntry(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			string text2 = (rootElement.TryGetProperty("url", out var value) ? value.GetString() : null);
			if (string.IsNullOrWhiteSpace(text2))
			{
				return null;
			}
			string name = (rootElement.TryGetProperty("name", out var value2) ? (value2.GetString() ?? string.Empty) : string.Empty);
			return new DanmakuApiEntry
			{
				Name = name,
				Url = text2
			};
		}
		catch
		{
			return null;
		}
	}

	private static List<EpisodeListItem> ParseEpisodeList(string encoded)
	{
		List<EpisodeListItem> list = new List<EpisodeListItem>();
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return list;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
			{
				string text2 = (item.TryGetProperty("id", out var value) ? (value.GetString() ?? string.Empty) : string.Empty);
				string label = (item.TryGetProperty("label", out var value2) ? (value2.GetString() ?? string.Empty) : string.Empty);
				bool flag = item.TryGetProperty("isCurrent", out var value3);
				if (flag)
				{
					JsonValueKind valueKind = value3.ValueKind;
					bool flag2 = valueKind - 5 <= JsonValueKind.Object;
					flag = flag2;
				}
				bool isCurrent = flag && value3.GetBoolean();
				flag = item.TryGetProperty("isPlayed", out var value4);
				if (flag)
				{
					JsonValueKind valueKind = value4.ValueKind;
					bool flag2 = valueKind - 5 <= JsonValueKind.Object;
					flag = flag2;
				}
				bool isPlayed = flag && value4.GetBoolean();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					list.Add(new EpisodeListItem
					{
						Id = text2,
						Label = label,
						IsCurrent = isCurrent,
						IsPlayed = isPlayed
					});
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static MediaSegment? ParseSegment(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			MediaSegmentType mediaSegmentType;
			switch ((rootElement.TryGetProperty("type", out value) ? (value.GetString() ?? "intro") : "intro").ToLowerInvariant())
			{
				case "recap":
					mediaSegmentType = MediaSegmentType.Recap;
					break;
				case "credits":
				case "outro":
					mediaSegmentType = MediaSegmentType.Credits;
					break;
				case "preview":
					mediaSegmentType = MediaSegmentType.Preview;
					break;
				default:
					mediaSegmentType = MediaSegmentType.Intro;
					break;
			}
			MediaSegmentType type = mediaSegmentType;
			long num = ((rootElement.TryGetProperty("startMs", out var value2) && value2.ValueKind == JsonValueKind.Number) ? value2.GetInt64() : 0);
			long? endMs = ((rootElement.TryGetProperty("endMs", out var value3) && value3.ValueKind == JsonValueKind.Number) ? new long?(value3.GetInt64()) : null);
			string source = (rootElement.TryGetProperty("source", out var value4) ? (value4.GetString() ?? string.Empty) : string.Empty);
			if (num <= 0 && !endMs.HasValue)
			{
				return null;
			}
			return new MediaSegment
			{
				Type = type,
				StartMs = num,
				EndMs = endMs,
				Source = source
			};
		}
		catch
		{
			return null;
		}
	}

	private static TodbChapter? ParseChapter(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			int markerId = ((rootElement.TryGetProperty("marker_id", out var value) && value.ValueKind == JsonValueKind.Number) ? value.GetInt32() : 0);
			string markerType = (rootElement.TryGetProperty("marker_type", out var value2) ? (value2.GetString() ?? "chapter") : "chapter");
			string title = (rootElement.TryGetProperty("title", out var value3) ? value3.GetString() : null);
			int timeStart = ((rootElement.TryGetProperty("time_start", out var value4) && value4.ValueKind == JsonValueKind.Number) ? value4.GetInt32() : 0);
			int? timeEnd = ((rootElement.TryGetProperty("time_end", out var value5) && value5.ValueKind == JsonValueKind.Number) ? new int?(value5.GetInt32()) : null);
			return new TodbChapter
			{
				MarkerId = markerId,
				MarkerType = markerType,
				Title = title,
				TimeStart = timeStart,
				TimeEnd = timeEnd
			};
		}
		catch
		{
			return null;
		}
	}

	private static TodbSprite? ParseSprite(string encoded)
	{
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return null;
		}
		try
		{
			string text = encoded.Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
				case 2:
					text += "==";
					break;
				case 3:
					text += "=";
					break;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Convert.FromBase64String(text));
			JsonElement rootElement = jsonDocument.RootElement;
			int spriteId = ((rootElement.TryGetProperty("sprite_id", out var value) && value.ValueKind == JsonValueKind.Number) ? value.GetInt32() : 0);
			int num = ((rootElement.TryGetProperty("width", out var value2) && value2.ValueKind == JsonValueKind.Number) ? value2.GetInt32() : 0);
			int num2 = ((rootElement.TryGetProperty("height", out var value3) && value3.ValueKind == JsonValueKind.Number) ? value3.GetInt32() : 0);
			string text2 = (rootElement.TryGetProperty("vtt_url", out var value4) ? (value4.GetString() ?? string.Empty) : string.Empty);
			if (num <= 0 || num2 <= 0 || string.IsNullOrWhiteSpace(text2))
			{
				return null;
			}
			return new TodbSprite
			{
				SpriteId = spriteId,
				Width = num,
				Height = num2,
				VttUrl = text2
			};
		}
		catch
		{
			return null;
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

	public App()
	{
		EnsureUnpackagedResourceRoot();
		InitializeComponent();
		SetCurrentProcessExplicitAppUserModelID("AIPlayer");
		base.UnhandledException += OnUnhandledException;
	}

	private static void EnsureUnpackagedResourceRoot()
	{
		string processPath = Environment.ProcessPath;
		if (!string.IsNullOrWhiteSpace(processPath))
		{
			string directoryName = Path.GetDirectoryName(processPath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.SetCurrentDirectory(directoryName);
			}
		}
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		MigrateDataIfNeeded();
		LoggerFolder = Path.Combine(AppDataRoot, "logs");
		if (!Directory.Exists(LoggerFolder))
		{
			Directory.CreateDirectory(LoggerFolder);
		}
		GlobalDependencies.Initialize();
		_logger = GlobalDependencies.Kernel.GetRequiredService<ILogger<App>>();
		_logger.LogInformation("OnLaunched begin");
		_logger.LogInformation("[StartupDiag] appdata root={AppDataRoot} source={AppDataRootSource}", AppDataRoot, AppDataRootSource);
		// K-06（t192）：把**本次实际使用的 legacy 迁移源根**打成一行（可 grep）——
		// 使命题"这次运行有没有触碰真实 profile"**可判**，而不是靠读代码猜。
		_logger.LogInformation("[StartupDiag] legacy root={LegacyRoot} source={LegacyRootSource}", LegacyRoot, LegacyRootSource);
		string text = string.Join(' ', Environment.GetCommandLineArgs());
		_logger.LogInformation("Command line: {CommandLine}", text);
		HostLaunchOptions hostLaunchOptions = ParseLaunchOptions(Environment.GetCommandLineArgs());
		if (!string.IsNullOrWhiteSpace(hostLaunchOptions.UserAgent))
		{
			ClientIdentity.HttpUserAgent = hostLaunchOptions.UserAgent;
		}
		_logger.LogInformation("OnLaunched options: open={HasOpen} title={Title} subtitleId={SubtitleId} subtitleUrl={SubtitleUrl} subtitleTracks={SubtitleTrackCount} danmakuApis={DanmakuApiCount} danmakuMatchName={DanmakuMatchName}", hostLaunchOptions.HasOpenRequest, hostLaunchOptions.Title ?? string.Empty, hostLaunchOptions.SubtitleId ?? string.Empty, hostLaunchOptions.SubtitleUrl ?? string.Empty, hostLaunchOptions.SubtitleTracks.Count, hostLaunchOptions.DanmakuApis.Count, hostLaunchOptions.DanmakuMatchName ?? string.Empty);
		_logger.LogDebug("[StartupDiag] libMpvPath from args={LibMpvPath}", hostLaunchOptions.LibMpvPath ?? "(null)");
		HandleInitialLaunchAsync(hostLaunchOptions);
	}

	private static Task MonitorParentProcessAsync(int parentPid)
	{
		return Task.Run(async delegate
		{
			try
			{
				using Process parent = Process.GetProcessById(parentPid);
				await parent.WaitForExitAsync();
			}
			catch (Exception ex) when (!(ex is OutOfMemoryException))
			{
			}
			await ReportAllPlayersStoppedAsync();
			try
			{
				Environment.Exit(0);
			}
			catch
			{
				Application.Current?.Exit();
			}
		});
	}

	private static async Task ReportAllPlayersStoppedAsync()
	{
		try
		{
			if (GlobalDependencies.Kernel == null)
			{
				return;
			}
			AppViewModel requiredService = GlobalDependencies.Kernel.GetRequiredService<AppViewModel>();
			if (requiredService.PlayerWindows.Count != 0)
			{
				await Task.WhenAll(from player in requiredService.PlayerWindows.ToArray()
								   select player.TryReportStoppedAsync());
			}
		}
		catch (Exception ex) when (!(ex is OutOfMemoryException))
		{
		}
	}

	private async Task HandleInitialLaunchAsync(HostLaunchOptions launchOptions)
	{
		try
		{
			_logger?.LogInformation("[StartupDiag] HandleInitialLaunchAsync begin parentPid={ParentPid} media={MediaPath}", launchOptions.ParentPid?.ToString() ?? "(none)", SummarizeLaunchMediaPath(launchOptions.MediaPath));
			int? parentPid = launchOptions.ParentPid;
			if (parentPid.HasValue && parentPid.GetValueOrDefault() > 0)
			{
				_logger?.LogDebug("HandleInitialLaunchAsync monitoring parent pid={ParentPid}", launchOptions.ParentPid.Value);
				MonitorParentProcessAsync(launchOptions.ParentPid.Value);
			}
			AppViewModel appViewModel = this.Get<AppViewModel>();
			_logger?.LogDebug("[StartupDiag] HandleInitialLaunchAsync got AppViewModel, calling HandleLaunchAsync");
			bool flag = await appViewModel.HandleLaunchAsync(launchOptions);
			_logger?.LogInformation("[StartupDiag] HandleInitialLaunchAsync done launchedPlayer={LaunchedPlayer}", flag);
			if (!flag)
			{
				if (launchOptions.HasOpenRequest)
				{
					_logger?.LogDebug("HandleInitialLaunchAsync exit because open request failed");
					Application.Current.Exit();
				}
				else
				{
					new MainWindow().Activate();
					_logger?.LogDebug("HandleInitialLaunchAsync activated MainWindow");
				}
			}
		}
		catch (Exception exception)
		{
			_logger?.LogError(exception, "[StartupDiag] HandleInitialLaunchAsync threw exception");
			if (launchOptions.HasOpenRequest)
			{
				Application.Current.Exit();
			}
		}
	}

	private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		if (_isHandlingUnhandledException)
		{
			e.Handled = true;
			return;
		}
		_isHandlingUnhandledException = true;
		_logger?.LogError(e.Exception, "Unhandled exception occurred.");
		e.Handled = true;
	}

	private static void MigrateDataIfNeeded()
	{
		string path = Path.Combine(AppDataRoot, ".migrated_player");
		if (File.Exists(path))
		{
			return;
		}
		// K-06（t192）：源根走 `LegacyRoot` —— 可用 `AIPLAYER_LEGACY_ROOT` 覆盖，
		// 不设该变量时逐字等于原实现（真实 `LocalApplicationData\AIPlayer.MpvHost`）。
		// ⇒ 沙箱化运行（设了覆盖）**不再读取用户真实 profile**；本次实际用了哪个根见启动诊断
		// `[StartupDiag] legacy root=… source=…`。
		string text = LegacyRoot;
		if (Directory.Exists(text))
		{
			MigrateFile(Path.Combine(text, "settings.json"), Path.Combine(AppDataRoot, "player", "settings.json"));
			string text2 = Path.Combine(text, "mpv");
			string text3 = Path.Combine(AppDataRoot, "mpv");
			if (Directory.Exists(text2))
			{
				string[] files = Directory.GetFiles(text2, "*", SearchOption.AllDirectories);
				foreach (string obj in files)
				{
					string text4 = obj.Substring(text2.Length);
					MigrateFile(obj, text3 + text4);
				}
			}
		}
		Directory.CreateDirectory(AppDataRoot);
		File.WriteAllText(path, DateTimeOffset.Now.ToString("O"));
	}

	private static void MigrateFile(string src, string dst)
	{
		if (File.Exists(src) && !File.Exists(dst))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(dst));
			File.Copy(src, dst);
		}
	}

	private static string SummarizeLaunchMediaPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "(empty)";
		}
		if (Uri.TryCreate(path, UriKind.Absolute, out Uri result))
		{
			return result.Scheme + "://" + result.Host + result.AbsolutePath;
		}
		if (path.Length > 120)
		{
			return string.Concat(path.AsSpan(0, 120), "...".AsSpan());
		}
		return path;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///App.xaml");
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IXamlType GetXamlType(Type type)
	{
		return _AppProvider.GetXamlType(type);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IXamlType GetXamlType(string fullName)
	{
		return _AppProvider.GetXamlType(fullName);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public XmlnsDefinition[] GetXmlnsDefinitions()
	{
		return _AppProvider.GetXmlnsDefinitions();
	}
}

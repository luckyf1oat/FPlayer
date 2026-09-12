using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Richasy.WinUIKernel.Share.Toolkits;
using Windows.System;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.Services;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Controls_SubtitleSyncDialogWinRTTypeDetails))]
public sealed class SubtitleSearchDialog : ContentDialog, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private interface ISubtitleSearchDialog_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface ISubtitleSearchDialog_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Microsoft_UI_Xaml_FrameworkElement_Tag(FrameworkElement obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.Tag = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(TextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.IDataTemplateExtension")]
	[WinRTExposedType(typeof(WinUISample_Controls_DanmakuSearchDialog_DanmakuSearchDialog_obj13_BindingsWinRTTypeDetails))]
	private class SubtitleSearchDialog_obj9_Bindings : IDataTemplateExtension, IDataTemplateComponent, IComponentConnector, ISubtitleSearchDialog_Bindings
	{
		private SubtitleResult dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler;

		private WeakReference obj9;

		private Button obj10;

		private TextBlock obj11;

		private TextBlock obj12;

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 9:
					obj9 = new WeakReference(target.As<Border>());
					break;
				case 10:
					obj10 = target.As<Button>();
					break;
				case 11:
					obj11 = target.As<TextBlock>();
					break;
				case 12:
					obj12 = target.As<TextBlock>();
					break;
			}
		}

		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		public IComponentConnector GetBindingConnector(int connectionId, object target)
		{
			return null;
		}

		public void DataContextChangedHandler(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			if (SetDataRoot(args.NewValue))
			{
				Update();
			}
		}

		public bool ProcessBinding(uint phase)
		{
			throw new NotImplementedException();
		}

		public int ProcessBindings(ContainerContentChangingEventArgs args)
		{
			int nextPhase = -1;
			ProcessBindings(args.Item, args.ItemIndex, (int)args.Phase, out nextPhase);
			return nextPhase;
		}

		public void ResetTemplate()
		{
			Recycle();
		}

		public void ProcessBindings(object item, int itemIndex, int phase, out int nextPhase)
		{
			nextPhase = -1;
			if (phase == 0)
			{
				nextPhase = -1;
				SetDataRoot(item);
				if (!removedDataContextHandler)
				{
					removedDataContextHandler = true;
					Border border = obj9.Target as Border;
					if (border != null)
					{
						border.DataContextChanged -= DataContextChangedHandler;
					}
				}
				initialized = true;
			}
			Update_(item.As<SubtitleResult>(), 1 << phase);
		}

		public void Recycle()
		{
		}

		public void Initialize()
		{
			if (!initialized)
			{
				Update();
			}
		}

		public void Update()
		{
			Update_(dataRoot, int.MinValue);
			initialized = true;
		}

		public void StopTracking()
		{
		}

		public void DisconnectUnloadedObject(int connectionId)
		{
			throw new ArgumentException("No unloadable elements to disconnect.");
		}

		public bool SetDataRoot(object newDataRoot)
		{
			if (newDataRoot != null)
			{
				dataRoot = newDataRoot.As<SubtitleResult>();
				return true;
			}
			return false;
		}

		private void Update_(SubtitleResult obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_Filename(obj.Filename, phase);
				Update_Detail(obj.Detail, phase);
			}
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_FrameworkElement_Tag(obj10, obj, null);
			}
		}

		private void Update_Filename(string obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj11, obj, null);
			}
		}

		private void Update_Detail(string obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj12, obj, null);
			}
		}
	}

	private static readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(20L),
		DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" } }
	};

	private static string SubtitleLogFile
	{
		get
		{
			try
			{
				string dir = Path.Combine(App.AppDataRoot, "logs");
				Directory.CreateDirectory(dir);
				return Path.Combine(dir, "aiplayer_subtitle.log");
			}
			// t235：命名类型 = `Exception`，**不能再窄** —— `App.AppDataRoot`（`ArgumentException`/`SecurityException`）、
			// `Directory.CreateDirectory`（`IOException`/`UnauthorizedAccessException`/`NotSupportedException`/`PathTooLongException`）、
			// `Path.Combine`（`ArgumentException`）共同祖先只有 `Exception`；契约 = 任何失败都不回落 %TEMP%（t154 / K-02）。
			catch (Exception)
			{
				// 兜底不回落 %TEMP%（🩹 t154 / K-02）：数据根不可用 ⇒ 本进程不写这条日志。
				return string.Empty;
			}
		}
	}

	private readonly PlayerViewModel _vm;

	private readonly ISettingsToolkit _settings;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private StackPanel TokenWarningPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private InfoBar ErrorInfoBar;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ProgressRing LoadingRing;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock HintText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ScrollViewer ResultsScroll;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ItemsControl ResultsList;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBox SearchBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private PasswordBox TokenBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ISubtitleSearchDialog_Bindings Bindings;

	public SubtitleSearchDialog(PlayerViewModel viewModel)
	{
		_vm = viewModel;
		_settings = this.Get<ISettingsToolkit>();
		InitializeComponent();
		SearchBox.Text = BuildSearchQuery(viewModel.MediaTitle, viewModel.MediaSubtitle);
		if (string.IsNullOrWhiteSpace(_settings.ReadLocalSetting("AssrtUserToken", string.Empty)))
		{
			TokenWarningPanel.Visibility = Visibility.Visible;
		}
	}

	private static void Log(string message)
	{
		try
		{
			// 🩹 t154（K-02）：原先直写 `%TEMP%\aiplayer_subtitle.log` —— 绕过打码器与数据根。
			//   现在落数据根 `logs\` 且过 `MaskSecrets`（与 Serilog sink 同一实现 ⇒ 同形态）。
			string logFile = SubtitleLogFile;
			if (logFile.Length == 0)
			{
				return;
			}
			File.AppendAllText(logFile, SecretMaskingTextFormatter.MaskSecrets($"[{DateTime.Now:HH:mm:ss.fff}] {message}") + Environment.NewLine);
		}
		// t235：命名类型 = `Exception` + **块内理由**（此前为无类型空体 ⇒ H3b 与 H3 双命中）：
		// 本处是诊断日志写出（内容已在 `:318` 过 `MaskSecrets`）；写失败**有意不抛出、不上报**，
		// 理由 = 字幕搜索主流程不得因日志不可写而失败（诊断面与业务面解耦）。
		// 可抛集合不可收敛：`SubtitleLogFile` 内部三调用（数据根/建目录/拼接）+ `File.AppendAllText`（`IOException`/
		// `UnauthorizedAccessException`/`DirectoryNotFoundException`/`ArgumentException`）⇒ 共同祖先只有 `Exception`。
		catch (Exception)
		{
		}
	}

	private void OnSaveTokenClick(object sender, RoutedEventArgs e)
	{
		string value = TokenBox.Password?.Trim();
		if (!string.IsNullOrWhiteSpace(value))
		{
			_settings.WriteLocalSetting("AssrtUserToken", value);
			TokenWarningPanel.Visibility = Visibility.Collapsed;
		}
	}

	private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter)
		{
			SearchAsync();
		}
	}

	private void OnSearchClick(object sender, RoutedEventArgs e)
	{
		SearchAsync();
	}

	private async Task SearchAsync()
	{
		string text = SearchBox.Text?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		string token = _settings.ReadLocalSetting("AssrtUserToken", string.Empty);
		if (string.IsNullOrWhiteSpace(token))
		{
			TokenWarningPanel.Visibility = Visibility.Visible;
			return;
		}
		SetLoadingState(loading: true);
		ErrorInfoBar.IsOpen = false;
		ResultsList.ItemsSource = null;
		try
		{
			string requestUri = $"{"https://api.assrt.net/v1"}/sub/search?q={Uri.EscapeDataString(text)}&cnt=15&token={Uri.EscapeDataString(token)}";
			Log("[Search] query=" + text);
			using JsonDocument searchDoc = JsonDocument.Parse(await _http.GetStringAsync(requestUri));
			JsonElement rootElement = searchDoc.RootElement;
			if (!TryGetInt32(rootElement, "status", out var value))
			{
				Log("[Search] API response missing status");
				ShowError("字幕接口返回格式异常，请稍后重试。");
				return;
			}
			if (value != 0)
			{
				string text2 = (rootElement.TryGetProperty("errmsg", out var value2) ? value2.GetString() : null);
				Log("[Search] API error: " + text2);
				ShowError(text2 ?? "搜索失败，请检查用户令牌是否正确。");
				return;
			}
			if (!TryGetNestedArray(rootElement, "sub", "subs", out var array))
			{
				Log("[Search] API response missing sub.subs");
				HintText.Text = "未找到字幕结果";
				HintText.Visibility = Visibility.Visible;
				ResultsScroll.Visibility = Visibility.Collapsed;
				return;
			}
			List<Task<IEnumerable<SubtitleResult>>> list = new List<Task<IEnumerable<SubtitleResult>>>();
			foreach (JsonElement item in array.EnumerateArray())
			{
				string jsonString = GetJsonString(item, "id");
				if (string.IsNullOrWhiteSpace(jsonString))
				{
					Log("[Search] package skipped: missing id");
					continue;
				}
				string text3 = ((item.TryGetProperty("lang", out var value3) && value3.TryGetProperty("desc", out var value4)) ? (value4.GetString() ?? "未知") : "未知");
				double? rating = null;
				if (TryGetDouble(item, "vote_score", out var value5) && value5 > 0.0)
				{
					rating = value5;
				}
				Log("[Search] package id=" + jsonString + " lang=" + text3);
				list.Add(FetchFileEntriesAsync(jsonString, text3, rating, token));
			}
			Log($"[Search] fetching details for {list.Count} packages in parallel...");
			List<SubtitleResult> list2 = (await Task.WhenAll(list)).SelectMany((IEnumerable<SubtitleResult> g) => g).ToList();
			Log($"[Search] total subtitle files found={list2.Count}");
			if (list2.Count == 0)
			{
				HintText.Text = "未找到可直接下载的字幕文件";
				HintText.Visibility = Visibility.Visible;
				ResultsScroll.Visibility = Visibility.Collapsed;
			}
			else
			{
				ResultsList.ItemsSource = list2;
				HintText.Visibility = Visibility.Collapsed;
				ResultsScroll.Visibility = Visibility.Visible;
			}
		}
		catch (Exception ex)
		{
			Log($"[Search] exception: {ex}");
			ShowError("网络错误：" + ex.Message);
		}
		finally
		{
			SetLoadingState(loading: false);
		}
	}

	private static async Task<IEnumerable<SubtitleResult>> FetchFileEntriesAsync(string id, string lang, double? rating, string token)
	{
		string requestUri = $"{"https://api.assrt.net/v1"}/sub/detail?id={id}&token={Uri.EscapeDataString(token)}";
		Log("[Detail] GET id=" + id);
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(await _http.GetStringAsync(requestUri));
			JsonElement rootElement = jsonDocument.RootElement;
			if (!TryGetInt32(rootElement, "status", out var value))
			{
				Log("[Detail] id=" + id + " missing status");
				return Array.Empty<SubtitleResult>();
			}
			if (value != 0)
			{
				Log($"[Detail] id={id} API status={value}");
				return Array.Empty<SubtitleResult>();
			}
			if (!TryGetNestedArray(rootElement, "sub", "subs", out var array))
			{
				Log("[Detail] id=" + id + " missing sub.subs");
				return Array.Empty<SubtitleResult>();
			}
			if (!array.EnumerateArray().Any())
			{
				Log("[Detail] id=" + id + " subs empty");
				return Array.Empty<SubtitleResult>();
			}
			JsonElement jsonElement = array.EnumerateArray().First();
			List<SubtitleResult> list = new List<SubtitleResult>();
			if (!jsonElement.TryGetProperty("filelist", out var value2) || value2.ValueKind != JsonValueKind.Array)
			{
				Log("[Detail] id=" + id + " no filelist");
				return Array.Empty<SubtitleResult>();
			}
			foreach (JsonElement item in value2.EnumerateArray())
			{
				string jsonString = GetJsonString(item, "url");
				string jsonString2 = GetJsonString(item, "f");
				string jsonString3 = GetJsonString(item, "s");
				Log($"[Detail] id={id} entry: f={jsonString2} s={jsonString3}");
				if (!string.IsNullOrWhiteSpace(jsonString) && !string.IsNullOrWhiteSpace(jsonString2))
				{
					bool flag;
					switch (Path.GetExtension(jsonString2).ToLowerInvariant())
					{
						case ".srt":
						case ".ass":
						case ".ssa":
						case ".vtt":
						case ".sub":
							flag = true;
							break;
						default:
							flag = false;
							break;
					}
					if (!flag)
					{
						Log("[Detail] id=" + id + " skipping non-subtitle: " + jsonString2);
						continue;
					}
					list.Add(new SubtitleResult
					{
						PackageId = id,
						Filename = jsonString2,
						DownloadUrl = jsonString,
						Language = DetectLanguage(jsonString2, lang),
						Rating = rating
					});
				}
			}
			Log($"[Detail] id={id} subtitle entries={list.Count}");
			return list;
		}
		catch (Exception ex)
		{
			Log("[Detail] id=" + id + " exception: " + ex.Message);
			return Array.Empty<SubtitleResult>();
		}
	}

	private static bool TryGetNestedArray(JsonElement root, string objectKey, string arrayKey, out JsonElement array)
	{
		array = default(JsonElement);
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(objectKey, out var value) || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(arrayKey, out array) || array.ValueKind != JsonValueKind.Array)
		{
			return false;
		}
		return true;
	}

	private static string? GetJsonString(JsonElement root, string key)
	{
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var value))
		{
			return null;
		}
		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString(),
			JsonValueKind.Number => value.ToString(),
			JsonValueKind.True => bool.TrueString,
			JsonValueKind.False => bool.FalseString,
			_ => null,
		};
	}

	private static bool TryGetInt32(JsonElement root, string key, out int value)
	{
		value = 0;
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var value2))
		{
			return false;
		}
		if (value2.ValueKind == JsonValueKind.Number)
		{
			return value2.TryGetInt32(out value);
		}
		if (value2.ValueKind == JsonValueKind.String)
		{
			return int.TryParse(value2.GetString(), out value);
		}
		return false;
	}

	private static bool TryGetDouble(JsonElement root, string key, out double value)
	{
		value = 0.0;
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var value2))
		{
			return false;
		}
		if (value2.ValueKind == JsonValueKind.Number)
		{
			return value2.TryGetDouble(out value);
		}
		if (value2.ValueKind == JsonValueKind.String)
		{
			return double.TryParse(value2.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}
		return false;
	}

	private async void OnLoadSubtitleClick(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: var tag } btn) || !(tag is SubtitleResult result))
		{
			return;
		}
		btn.IsEnabled = false;
		btn.Content = "下载中...";
		Log("[Download] start id=" + result.PackageId + " file=" + result.Filename);
		try
		{
			string text = await DownloadSubtitleFileAsync(result);
			if (text != null)
			{
				Log("[Download] success, loading: " + text);
				await _vm.LoadSubtitleFileAsync(text);
				Hide();
			}
			else
			{
				Log("[Download] failed, opening browser fallback");
				await Launcher.LaunchUriAsync(new Uri("https://assrt.net/sub/" + result.PackageId + ".html"));
				ShowError("无法直接下载，已打开字幕页面。请手动下载后，通过「添加本地字幕」加载。");
				btn.Content = "加载";
				btn.IsEnabled = true;
			}
		}
		catch (Exception value)
		{
			Log($"[Download] exception: {value}");
			btn.Content = "加载";
			btn.IsEnabled = true;
			ShowError("操作失败，请稍后重试。");
		}
	}

	private static async Task<string?> DownloadSubtitleFileAsync(SubtitleResult result)
	{
		Log("[Download] fetching: " + result.DownloadUrl);
		byte[] array;
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, result.DownloadUrl);
			request.Headers.Referrer = new Uri("https://assrt.net");
			using HttpResponseMessage response = await _http.SendAsync(request);
			string text = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
			Log($"[Download] HTTP {(int)response.StatusCode} content-type={text}");
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			if (text.Contains("html", StringComparison.OrdinalIgnoreCase))
			{
				Log("[Download] got HTML (login wall?)");
				return null;
			}
			array = await response.Content.ReadAsByteArrayAsync();
			Log($"[Download] received {array.Length} bytes");
		}
		catch (Exception ex)
		{
			Log("[Download] request exception: " + ex.Message);
			return null;
		}
		// 🩹 t154（K-02）：字幕下载的临时目录原先落 `%TEMP%\aiplayer_subs` ⇒ 改到数据根下（覆盖变量可沙箱化）。
		string text2 = Path.Combine(App.AppDataRoot, "subs-tmp");
		Directory.CreateDirectory(text2);
		char[] invalidChars = Path.GetInvalidFileNameChars();
		string text3 = string.Concat(result.Filename.Select((char c) => (!invalidChars.Contains(c)) ? c : '_'));
		string tempFile = Path.Combine(text2, result.PackageId + "_" + text3);
		await File.WriteAllBytesAsync(tempFile, array);
		Log("[Download] saved to: " + tempFile);
		bool flag;
		switch (Path.GetExtension(result.Filename).ToLowerInvariant())
		{
			case ".srt":
			case ".ass":
			case ".ssa":
			case ".vtt":
			case ".sub":
				flag = true;
				break;
			default:
				flag = false;
				break;
		}
		return flag ? tempFile : null;
	}

	private static string DetectLanguage(string filename, string fallback)
	{
		string stem = Path.GetFileNameWithoutExtension(filename);
		if (stem.Contains("chs&eng", StringComparison.OrdinalIgnoreCase) || stem.Contains("简体&英文") || stem.Contains("简&英") || stem.Contains("简英") || stem.Contains("中英"))
		{
			return "中英双语";
		}
		if (stem.Contains("cht&eng", StringComparison.OrdinalIgnoreCase) || stem.Contains("繁体&英文") || stem.Contains("繁&英") || stem.Contains("繁英"))
		{
			return "繁英双语";
		}
		if (Word("chs") || stem.Contains("简体") || stem.Contains("简中"))
		{
			return "简体中文";
		}
		if (Word("cht") || stem.Contains("繁体") || stem.Contains("繁中"))
		{
			return "繁体中文";
		}
		if (Word("eng") || stem.Contains("英文"))
		{
			return "英文";
		}
		if (Word("jpn") || Word("jap") || stem.Contains("日文"))
		{
			return "日文";
		}
		if (Word("kor") || stem.Contains("韩文"))
		{
			return "韩文";
		}
		if (Word("fra") || Word("fre") || stem.Contains("法文"))
		{
			return "法文";
		}
		if (Word("spa") || stem.Contains("西班牙"))
		{
			return "西班牙文";
		}
		return fallback;
		bool Word(string token)
		{
			return Regex.IsMatch(stem, "(?<![A-Za-z])" + Regex.Escape(token) + "(?![A-Za-z])", RegexOptions.IgnoreCase);
		}
	}

	private static string BuildSearchQuery(string? title, string? subtitle)
	{
		string text = (title ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(subtitle))
		{
			return text;
		}
		string text2 = subtitle.Split('·')[0].Trim();
		if (!Regex.IsMatch(text2, "^S\\d+E\\d+", RegexOptions.IgnoreCase))
		{
			return text;
		}
		return text + " " + text2;
	}

	private void SetLoadingState(bool loading)
	{
		LoadingRing.IsActive = loading;
		LoadingRing.Visibility = ((!loading) ? Visibility.Collapsed : Visibility.Visible);
		if (loading)
		{
			HintText.Visibility = Visibility.Collapsed;
			ResultsScroll.Visibility = Visibility.Collapsed;
		}
	}

	private void ShowError(string message)
	{
		ErrorInfoBar.Message = message;
		ErrorInfoBar.IsOpen = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/SubtitleSearchDialog.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
			case 2:
				TokenWarningPanel = target.As<StackPanel>();
				break;
			case 3:
				ErrorInfoBar = target.As<InfoBar>();
				break;
			case 4:
				LoadingRing = target.As<ProgressRing>();
				break;
			case 5:
				HintText = target.As<TextBlock>();
				break;
			case 6:
				ResultsScroll = target.As<ScrollViewer>();
				break;
			case 7:
				ResultsList = target.As<ItemsControl>();
				break;
			case 10:
				target.As<Button>().Click += OnLoadSubtitleClick;
				break;
			case 13:
				SearchBox = target.As<TextBox>();
				SearchBox.KeyDown += OnSearchKeyDown;
				break;
			case 14:
				target.As<Button>().Click += OnSearchClick;
				break;
			case 15:
				TokenBox = target.As<PasswordBox>();
				break;
			case 16:
				target.As<Button>().Click += OnSaveTokenClick;
				break;
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		IComponentConnector result = null;
		if (connectionId == 9)
		{
			Border border = (Border)target;
			SubtitleSearchDialog_obj9_Bindings subtitleSearchDialog_obj9_Bindings = new SubtitleSearchDialog_obj9_Bindings();
			result = subtitleSearchDialog_obj9_Bindings;
			subtitleSearchDialog_obj9_Bindings.SetDataRoot(border.DataContext);
			border.DataContextChanged += subtitleSearchDialog_obj9_Bindings.DataContextChangedHandler;
			DataTemplate.SetExtensionInstance(border, subtitleSearchDialog_obj9_Bindings);
			XamlBindingHelper.SetDataTemplateComponent(border, subtitleSearchDialog_obj9_Bindings);
		}
		return result;
	}
}

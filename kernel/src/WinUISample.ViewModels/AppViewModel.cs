using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core.Models;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.Storage;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.Models;
using WinUISample.Models.Constants;
using WinUISample.Services;

namespace WinUISample.ViewModels;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(WinUISample_Models_DanmakuSearchApiGroupWinRTTypeDetails))]
public sealed class AppViewModel : ViewModelBase
{
	private readonly ILogger<AppViewModel> _logger;

	private readonly ISettingsToolkit _settingsToolkit;

	private readonly IFileToolkit _fileToolkit;

	/// <summary>
	/// 最近一次起播时，弹幕匹配名是从哪条通道来的（`cli` = 命令行启动参数，已在其边界解码；`inproc` = 进程内宿主直接构造）。
	/// <see cref="OpenVideoAsync"/> 的形参表有 40+ 项，为不把"来源标签"也塞进去（那会让调用点更难读），
	/// 在起播入口 <see cref="HandleLaunchAsync"/> 里记这一次的标签，供 <see cref="OpenVideoAsync"/> 取用。
	/// </summary>
	private string _danmakuMatchNameSource = "inproc";

	[CompilerGenerated]
	private SectionType _003CCurrentSectionType_003Ek__BackingField;

	[CompilerGenerated]
	private string _003CLibMpvPath_003Ek__BackingField;

	[CompilerGenerated]
	private string _003CLastOpenedMediaPath_003Ek__BackingField;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? pickLibMpvCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? openMediaCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? reopenLastMediaCommand;

	public XamlRoot? ActivateXamlRoot { get; set; }

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SectionType CurrentSectionType
	{
		get
		{
			return _003CCurrentSectionType_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<SectionType>.Default.Equals(_003CCurrentSectionType_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.CurrentSectionType);
				_003CCurrentSectionType_003Ek__BackingField = value;
				OnCurrentSectionTypeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.CurrentSectionType);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LibMpvPath
	{
		get
		{
			return _003CLibMpvPath_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CLibMpvPath_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.LibMpvPath);
				_003CLibMpvPath_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.LibMpvPath);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LastOpenedMediaPath
	{
		get
		{
			return _003CLastOpenedMediaPath_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CLastOpenedMediaPath_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.LastOpenedMediaPath);
				_003CLastOpenedMediaPath_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.LastOpenedMediaPath);
			}
		}
	}

	public List<PlayerViewModel> PlayerWindows { get; set; } = new List<PlayerViewModel>();

	public MainWindow? MainWindow { get; set; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand PickLibMpvCommand => pickLibMpvCommand ?? (pickLibMpvCommand = new AsyncRelayCommand(PickLibMpvAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand OpenMediaCommand => openMediaCommand ?? (openMediaCommand = new AsyncRelayCommand(OpenMediaAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ReopenLastMediaCommand => reopenLastMediaCommand ?? (reopenLastMediaCommand = new AsyncRelayCommand(ReopenLastMediaAsync));

	public AppViewModel(ILogger<AppViewModel> logger, ISettingsToolkit settingsToolkit, IFileToolkit fileToolkit)
	{
		_logger = logger;
		_settingsToolkit = settingsToolkit;
		_fileToolkit = fileToolkit;
		CurrentSectionType = settingsToolkit.ReadLocalSetting("SectionType", SectionType.Local);
		LibMpvPath = settingsToolkit.ReadLocalSetting("LibMpvPath", string.Empty);
		LastOpenedMediaPath = settingsToolkit.ReadLocalSetting("LastLocalVideoPath", string.Empty);
		if (!string.IsNullOrEmpty(LibMpvPath))
		{
			MpvNative.Initialize(LibMpvPath);
		}
	}

	public async Task<PlayerViewModel> OpenVideoAsync(string videoPath, MpvPlayOptions? options = null, string? title = null, string? subtitle = null, string? monogram = null, string? logo = null, string? backdropUrl = null, string? callbackUrl = null, string? previousEpisodeId = null, string? nextEpisodeId = null, string? seasonId = null, IReadOnlyList<string>? badges = null, IReadOnlyList<OverlayVersionOption>? versionOptions = null, IReadOnlyList<OverlayTrackOption>? audioTracks = null, IReadOnlyList<OverlayTrackOption>? subtitleTracks = null, bool isSkipFeatureEnabled = true, PlayerShortcuts? shortcuts = null, string? httpProxy = null, IReadOnlyList<DanmakuApiEntry>? danmakuApis = null, string? danmakuMatchName = null, string? mpvConfigDir = null, bool fitVideoSize = true, VideoFitMode videoFitMode = VideoFitMode.Contain, IReadOnlyList<EpisodeListItem>? episodeList = null, IReadOnlyList<MediaSegment>? segments = null, IReadOnlyList<TodbChapter>? chapters = null, TodbSprite? sprite = null, bool defaultRtxVsr = false, bool defaultRtxVideoHdr = false, string defaultAnimeMode = "none", string defaultSharpenMode = "none", bool autoPlayNextEpisode = true, int maxVolume = 100)
	{
		PlayerViewModel playerViewModel = PlayerWindows.Find((PlayerViewModel p) => p.Id == videoPath);
		if (playerViewModel != null)
		{
			playerViewModel.Window.Show();
			return playerViewModel;
		}
		PlayerViewModel playerVM = this.Get<PlayerViewModel>();
		PlayerWindows.Add(playerVM);
		playerVM.MediaTitle = (string.IsNullOrWhiteSpace(title) ? Path.GetFileName(videoPath) : title);
		playerVM.MediaSubtitle = subtitle ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(monogram))
		{
			playerVM.MediaMonogram = monogram;
		}
		playerVM.MediaLogo = logo ?? string.Empty;
		playerVM.MediaBackdropUrl = backdropUrl ?? string.Empty;
		playerVM.ReportClient = new PlaybackReportClient(callbackUrl, this.Get<ILogger<PlaybackReportClient>>());
		PlayerUpdateServer playerUpdateServer = new PlayerUpdateServer(delegate (IReadOnlyList<MediaSegment> activeSegments)
		{
			playerVM.SetActiveSegments(activeSegments);
		}, this.Get<ILogger<PlayerUpdateServer>>(), async delegate (KernelControlCommand command)
		{
			// 把 /control 的**类型化**命令直接转给播放器 VM（同一条 mpv 写点，
			//   见 PlayerViewModel.ApplyControlCommandAsync —— 8 个 kind 一一对应）。
			return await playerVM.ApplyControlCommandAsync(command);
		});
		if (playerUpdateServer.Start())
		{
			playerVM.UpdateServer = playerUpdateServer;
			playerVM.UpdateUrl = playerUpdateServer.UpdateUrl;
			_logger.LogInformation("PlayerUpdateServer started at {Url}", playerUpdateServer.UpdateUrl);
			// **可观测的 fork 身份指纹**：重建版与原版的版本串完全相同（1.0.0+7e541a41c35b…），
			// 没有这一行就无法证明"这次跑的是我们的内核"（P4 回退验证、任何"我们的内核 vs 原版 DLL"对照都会退化成猜哈希）。
			System.Reflection.Assembly forkAssembly = typeof(AppViewModel).Assembly;
			object[] informationalAttributes = forkAssembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
			string informationalVersion = ((informationalAttributes.Length > 0) ? ((System.Reflection.AssemblyInformationalVersionAttribute)informationalAttributes[0]).InformationalVersion : "(none)");
			_logger.LogInformation("KERNEL-FORK rev={Rev} assembly={Assembly} version={Version} informational={Informational}", 1, forkAssembly.Location, forkAssembly.GetName().Version?.ToString(), informationalVersion);
			// **线程模型基线**：本方法运行在 UI 线程，所以这一行就是"UI 线程 id"的基准值。
			//   这一行是基准值（本方法运行在 UI 线程：Program.cs:21 已装 DispatcherQueueSynchronizationContext），
			//   与每条命令的 `[CONTROL-APPLY] … managedThreadId=` 比对即可判断控制命令落在哪个线程 —— 不能靠读代码猜：实测是不相等（线程池）。
			_logger.LogInformation("[THREAD] ui-managed-thread-id={ThreadId}（/control 落点应与之相等；t46 实测口径）", Environment.CurrentManagedThreadId);
		}
		else
		{
			playerUpdateServer.Dispose();
			_logger.LogWarning("Failed to start PlayerUpdateServer, late segments will not be received");
		}
		playerVM.PreviousEpisodeId = previousEpisodeId ?? string.Empty;
		playerVM.NextEpisodeId = nextEpisodeId ?? string.Empty;
		playerVM.SeasonId = seasonId ?? string.Empty;
		playerVM.MediaBadges.Clear();
		if (badges != null)
		{
			foreach (string item in badges.Where((string p) => !string.IsNullOrWhiteSpace(p)))
			{
				playerVM.MediaBadges.Add(item);
			}
		}
		playerVM.VersionOptions.Clear();
		if (versionOptions != null)
		{
			foreach (OverlayVersionOption item2 in versionOptions.Where((OverlayVersionOption p) => !string.IsNullOrWhiteSpace(p.Label)))
			{
				playerVM.VersionOptions.Add(new OverlayVersionOption
				{
					Index = item2.Index,
					Id = item2.Id,
					Label = item2.Label,
					IsSelected = item2.IsSelected
				});
			}
		}
		playerVM.AudioTracks.Clear();
		if (audioTracks != null)
		{
			foreach (OverlayTrackOption item3 in audioTracks.Where((OverlayTrackOption p) => !string.IsNullOrWhiteSpace(p.Label)))
			{
				playerVM.AudioTracks.Add(new OverlayTrackOption
				{
					Id = item3.Id,
					Label = item3.Label,
					Url = item3.Url,
					Title = item3.Title,
					Language = item3.Language,
					IsExternal = item3.IsExternal,
					IsSelected = item3.IsSelected,
					IsSpecial = item3.IsSpecial,
					EmbyStreamIndex = item3.EmbyStreamIndex
				});
			}
		}
		playerVM.SubtitleTracks.Clear();
		if (subtitleTracks != null)
		{
			foreach (OverlayTrackOption item4 in subtitleTracks.Where((OverlayTrackOption p) => !string.IsNullOrWhiteSpace(p.Label)))
			{
				playerVM.SubtitleTracks.Add(new OverlayTrackOption
				{
					Id = item4.Id,
					Label = item4.Label,
					Url = item4.Url,
					Title = item4.Title,
					Language = item4.Language,
					IsExternal = item4.IsExternal,
					IsSelected = item4.IsSelected,
					IsSpecial = item4.IsSpecial,
					EmbyStreamIndex = item4.EmbyStreamIndex
				});
			}
		}
		_logger.LogDebug($"AppViewModel.OpenVideoAsync subtitleTracks={playerVM.SubtitleTracks.Count} selected={playerVM.SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected)?.Label ?? string.Empty}");
		playerVM.Shortcuts = shortcuts ?? PlayerShortcuts.Default;
		playerVM.IsSkipFeatureEnabled = isSkipFeatureEnabled;
		playerVM.HttpProxy = httpProxy;
		playerVM.DanmakuApis = danmakuApis ?? Array.Empty<DanmakuApiEntry>();
		playerVM.SetDanmakuMatchNameFromHost(danmakuMatchName, _danmakuMatchNameSource);
		playerVM.MpvConfigDir = mpvConfigDir;
		playerVM.FitVideoSize = fitVideoSize;
		playerVM.PersistFitVideoSizePreference();
		playerVM.VideoFitMode = videoFitMode;
		playerVM.DefaultRtxVsr = defaultRtxVsr;
		playerVM.DefaultRtxVideoHdr = defaultRtxVideoHdr;
		playerVM.DefaultAnimeMode = defaultAnimeMode;
		playerVM.DefaultSharpenMode = defaultSharpenMode;
		playerVM.AutoPlayNextEpisode = autoPlayNextEpisode;
		playerVM.MaxVolume = Math.Clamp(maxVolume, 100, 200);
		playerVM.EpisodeList.Clear();
		if (episodeList != null)
		{
			foreach (EpisodeListItem item5 in episodeList.Where((EpisodeListItem e) => !string.IsNullOrWhiteSpace(e.Id)))
			{
				playerVM.EpisodeList.Add(item5);
			}
		}
		playerVM.NotifyEpisodeListNavigationChanged();
		playerVM.SetActiveSegments(segments);
		playerVM.SetChapters(chapters);
		playerVM.SetSprite(sprite);
		await playerVM.LoadAsync(videoPath, options);
		return playerVM;
	}

	public async Task<bool> HandleLaunchAsync(HostLaunchOptions options)
	{
		// **被启动进程自报身份（t130 引入，t134 上移到启动入口）**：只有进程**自己读自己的镜像**，才能把
		//   "启动器指向了哪条路径"（意图）与"实际被加载执行的是哪份 dll"（事实）分开 —— 原版/fork 的 exe
		//   同为 384,000 B，拿 exe 当判据不牢；真正决定行为的是同目录的 `AIPlayer.MpvHost.dll`。
		//   ⚠️ t134 修正：t130 原先把本块放在 `OpenVideoAsync` 内 ⇒ 只在**走到开播路径**时才打
		//   （t162 修：原先这里写的是行号 `:331`/`:335` 两道闸 + `:364` 才调 —— 那组数字在 t134 插入本块后
		//   已整体漂移约 3 行；**引用一律改成不带行号的锚文本**，因为行号引用注定在下一次插入后过期）：
		//   两道闸（`options.HasOpenRequest` 与 `LibMpvPath` 的组合判断，各自 `return false`）之后的
		//   `await OpenVideoAsync(...)` 才是唯一常规开播入口 ⇒ "启动首行"字面不成立（实测：无 `--open`
		//   的最小启动 `KERNEL-SELF=0`）。现移到 `HandleLaunchAsync` **第一条语句** ⇒ **任何**启动恰打一次。
		//   本行**不依赖任何外部输入**（环境变量/命令行都不参与），sha12 由进程自己读自身装配件算出；
		//   读不到时打印 `reason=`，**不**回落到任何默认身份。
		try
		{
			string selfImage = typeof(AppViewModel).Assembly.Location;
			string selfSha12;
			using (System.IO.FileStream selfStream = System.IO.File.OpenRead(selfImage))
			using (System.Security.Cryptography.SHA256 selfHasher = System.Security.Cryptography.SHA256.Create())
			{
				selfSha12 = System.Convert.ToHexString(selfHasher.ComputeHash(selfStream)).Substring(0, 12);
			}
			_logger.LogInformation("KERNEL-SELF path={Path} sha12={Sha12} pid={Pid} exe={Exe}", selfImage, selfSha12, Environment.ProcessId, Environment.ProcessPath);
		}
		catch (Exception selfEx) when (selfEx is System.IO.IOException or System.UnauthorizedAccessException or System.NotSupportedException)
		{
			_logger.LogInformation("KERNEL-SELF path={Path} sha12=(unreadable) reason={Reason} pid={Pid}", typeof(AppViewModel).Assembly.Location, selfEx.GetType().Name, Environment.ProcessId);
		}
		_logger.LogInformation("[StartupDiag] HandleLaunchAsync begin media={MediaPath} mpvConfigDir={MpvConfigDir}", SummarizeMediaPath(options.MediaPath), options.MpvConfigDir ?? "(none)");
		// 弹幕匹配名的**来源标签**在这里定型：命令行通道已在解析器里解码（→ `cli`），进程内宿主构造的对象没解码（→ `inproc`）。
		_danmakuMatchNameSource = (options.DanmakuMatchNameDecodedFromCli ? "cli" : "inproc");
		if (!string.IsNullOrWhiteSpace(options.LibMpvPath) && File.Exists(options.LibMpvPath))
		{
			LibMpvPath = options.LibMpvPath;
			_settingsToolkit.WriteLocalSetting("LibMpvPath", options.LibMpvPath);
			MpvNative.Initialize(options.LibMpvPath);
		}
		if (options.HasOpenRequest && string.IsNullOrWhiteSpace(LibMpvPath))
		{
			return false;
		}
		if (!options.HasOpenRequest || string.IsNullOrWhiteSpace(LibMpvPath))
		{
			return false;
		}
		// "上次本地视频"槽（`LastLocalVideoPath`）**只接受本机文件路径**：
		//   读侧本来就有 File.Exists 闸门（AppViewModel.cs:361 / LocalVideoPageViewModel.cs:86）⇒ 写进网络流 URL 是**零功能**的，
		//   而宿主用 `--open=<带 api_key 的流 URL>` 启动时，无条件写会把凭据**持久化到明文 JSON**（还会被 RootLayout 显示、
		//   被外壳 ShellToolkits 原样合并写回，且两个打码器都扫不到）⇒ 这里收窄暴露面。
		//   ⚠️ 与原版"无条件写"是**一处有意的偏离**（契约声明见 kernel/CHANGES.md §t71）。
		//   注意：判据**不**要求 File.Exists —— 暂不可用的网络盘/移动盘仍是"本地文件路径"，仍要记住。
		if (IsLocalFilePath(options.MediaPath))
		{
			LastOpenedMediaPath = options.MediaPath;
			_settingsToolkit.WriteLocalSetting("LastLocalVideoPath", options.MediaPath);
		}
		else
		{
			_logger.LogInformation("[StartupDiag] LastLocalVideoPath skipped reason=non-local path={Path}", SummarizeMediaPath(options.MediaPath));
		}
		(string outboundUserAgent, Dictionary<string, string>? outboundHeaders) = OutboundIdentity.Normalize(options.HttpHeaders);
		MpvPlayOptions mpvPlayOptions = new MpvPlayOptions
		{
			StartPosition = options.StartPosition,
			InitialSubtitleId = options.SubtitleId,
			ExtraSubtitleUrl = options.SubtitleUrl,
			HttpHeaders = outboundHeaders,
			UserAgent = outboundUserAgent
		};
		_logger.LogDebug($"AppViewModel.HandleLaunchAsync playOptions subtitleId={mpvPlayOptions.InitialSubtitleId ?? string.Empty} subtitleUrl={mpvPlayOptions.ExtraSubtitleUrl ?? string.Empty}");
		await OpenVideoAsync(options.MediaPath, mpvPlayOptions, options.Title, options.Subtitle, options.Monogram, options.Logo, options.BackdropUrl, options.CallbackUrl, options.PreviousEpisodeId, options.NextEpisodeId, options.SeasonId, options.Badges, options.VersionOptions, options.AudioTracks, options.SubtitleTracks, !options.DisableSkipMarkers, options.Shortcuts, options.HttpProxy, options.DanmakuApis, options.DanmakuMatchName, options.MpvConfigDir, options.FitVideoSize, options.VideoFitMode, (options.EpisodeList.Count > 0) ? options.EpisodeList : null, (options.Segments.Count > 0) ? options.Segments : null, (options.Chapters.Count > 0) ? options.Chapters : null, options.Sprite, options.DefaultRtxVsr, options.DefaultRtxVideoHdr, options.DefaultAnimeMode, options.DefaultSharpenMode, options.AutoPlayNextEpisode, options.MaxVolume);
		return true;
	}

	[RelayCommand]
	private async Task PickLibMpvAsync()
	{
		StorageFile storageFile = await _fileToolkit.PickFileAsync(".dll", MainWindow);
		if (storageFile != null)
		{
			LibMpvPath = storageFile.Path;
			_settingsToolkit.WriteLocalSetting("LibMpvPath", storageFile.Path);
			MpvNative.Initialize(storageFile.Path);
		}
	}

	[RelayCommand]
	private async Task OpenMediaAsync()
	{
		StorageFile storageFile = await _fileToolkit.PickFileAsync(".mp4,.mkv,.avi,.mov,.rmvb,.wmv,.flv,.ts,.m2ts", MainWindow);
		if (!(storageFile == null))
		{
			LastOpenedMediaPath = storageFile.Path;
			_settingsToolkit.WriteLocalSetting("LastLocalVideoPath", storageFile.Path);
			await OpenVideoAsync(storageFile.Path);
		}
	}

	[RelayCommand]
	private async Task ReopenLastMediaAsync()
	{
		if (!string.IsNullOrEmpty(LastOpenedMediaPath) && File.Exists(LastOpenedMediaPath))
		{
			await OpenVideoAsync(LastOpenedMediaPath);
		}
		else if (await new ContentDialog
		{
			Title = "没有可重开的媒体",
			Content = "还没有记录到可用的上次播放文件，请先手动选择一个视频文件。",
			CloseButtonText = "知道了",
			PrimaryButtonText = "选择文件",
			XamlRoot = ActivateXamlRoot
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			await OpenMediaAsync();
		}
	}

	/// <summary>
	/// 判断 <c>--open=</c> 的值是不是**本机文件路径**（= 可以记进"上次本地视频"槽）。
	/// <para>判据 = **没有 URI scheme，或 scheme 是 <c>file</c>**：`C:\x.mkv`、`\\server\share\x.mkv`、`file:///C:/x.mkv` 一律算本机文件，
	/// 而 `http(s)://`、`rtsp://`、`rtmp://` 等一律不算。</para>
	/// <para>⚠️ **刻意不要求 <c>File.Exists</c>**：暂不可用的网络盘/移动盘仍是"本地文件路径"，仍要记住它
	/// （要求 file-exists 会把这这类路径静默丢掉，那正是本卡被否掉的选项 (b)）。</para>
	/// </summary>
	private static bool IsLocalFilePath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		string text = path.Trim().Trim('"');
		if (text.Length == 0)
		{
			return false;
		}
		// 不是绝对 URI 形态（相对路径、裸文件名）⇒ 按本机文件处理；是绝对 URI ⇒ 只认 file scheme。
		return !Uri.TryCreate(text, UriKind.Absolute, out Uri result) || result.IsFile;
	}

	private static string SummarizeMediaPath(string? path)
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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentSectionTypeChanged(SectionType value)
	{
		_settingsToolkit.WriteLocalSetting("SectionType", value);
	}
}

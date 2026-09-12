using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core;
using Richasy.MpvKernel.Core.Enums;
using Richasy.MpvKernel.Core.Models;
using Richasy.MpvKernel.WinUI;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinRT.Interop;
using WinUISample.Controls;
using WinUISample.Models;
using WinUISample.Models.Constants;
using WinUISample.Services;

namespace WinUISample.ViewModels;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(WinUISample_Models_DanmakuSearchApiGroupWinRTTypeDetails))]
public sealed class PlayerViewModel : ViewModelBase
{
	private sealed record MpvAudioTrack(int Id, string Title, string Language, bool Selected);

	private sealed record MpvSubtitleTrack(int Id, string Title, string Language, bool External, bool Selected);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate nint SubclassProcDelegate(nint hWnd, uint uMsg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData);

	private struct WinPoint
	{
		public int X;

		public int Y;
	}

	private struct WinRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct WinMinMaxInfo
	{
		public WinPoint Reserved;

		public WinPoint MaxSize;

		public WinPoint MaxPosition;

		public WinPoint MinTrackSize;

		public WinPoint MaxTrackSize;
	}

	private struct WinMonitorInfo
	{
		public uint cbSize;

		public WinRect Monitor;

		public WinRect WorkArea;

		public uint dwFlags;
	}

	private struct WindowPos
	{
		public nint Hwnd;

		public nint HwndInsertAfter;

		public int X;

		public int Y;

		public int Cx;

		public int Cy;

		public uint Flags;
	}

	/// <summary>
	/// 导航调试日志的落点（🩹 t154 / K-01）：**`数据根\logs\`**，不再写 `%TEMP%` ——
	/// 这样 `AIPLAYER_APPDATA_ROOT` 能把它一起沙箱化，且与 Serilog 的 `LoggerFolder` 同一个根。
	/// 目录不存在时按需创建（Serilog 自己也建，二者顺序不定）。
	/// </summary>
	private static string NavDebugLogPath
	{
		get
		{
			try
			{
				string dir = Path.Combine(App.AppDataRoot, "logs");
				Directory.CreateDirectory(dir);
				return Path.Combine(dir, "ai_player_nav_debug.log");
			}
			// t231（承 t224）：命名类型 = `Exception`，**这里不能更窄** —— 本 try 内三种调用各自可抛的集合不同：
			// `App.AppDataRoot`（ArgumentException / SecurityException）、`Directory.CreateDirectory`
			// （IOException / UnauthorizedAccessException / NotSupportedException / PathTooLongException）、
			// `Path.Combine`（ArgumentException）；共同祖先只有 `Exception`，而本处契约是
			// **任何失败都不回落 %TEMP%**（漏一种就等于把那条路放回来）⇒ 用 `Exception` 而非更窄类型。
			catch (Exception)
			{
				// 兜底：数据根不可用时**不回落 %TEMP%**（那正是本卡要消灭的形状），改为不写这一条。
				return string.Empty;
			}
		}
	}

	private static readonly nint HwndTopmost = new IntPtr(-1);

	private static readonly nint HwndNotTopmost = new IntPtr(-2);

	private static readonly Dictionary<string, HashSet<string>> LanguageAliasMap = BuildLanguageAliasMap();

	private static readonly IReadOnlyDictionary<string, string[]> _animeModeShaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["standard"] = new string[6] { "Anime4K_Clamp_Highlights.glsl", "Anime4K_Restore_CNN_VL.glsl", "Anime4K_Upscale_CNN_x2_VL.glsl", "Anime4K_AutoDownscalePre_x2.glsl", "Anime4K_AutoDownscalePre_x4.glsl", "Anime4K_Upscale_CNN_x2_M.glsl" },
		["soft"] = new string[6] { "Anime4K_Clamp_Highlights.glsl", "Anime4K_Restore_CNN_Soft_VL.glsl", "Anime4K_Upscale_CNN_x2_VL.glsl", "Anime4K_AutoDownscalePre_x2.glsl", "Anime4K_AutoDownscalePre_x4.glsl", "Anime4K_Upscale_CNN_x2_M.glsl" },
		["denoise"] = new string[5] { "Anime4K_Clamp_Highlights.glsl", "Anime4K_Upscale_Denoise_CNN_x2_VL.glsl", "Anime4K_AutoDownscalePre_x2.glsl", "Anime4K_AutoDownscalePre_x4.glsl", "Anime4K_Upscale_CNN_x2_M.glsl" }
	};

	private static readonly IReadOnlyDictionary<string, string> _sharpenModeShaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["light"] = "adaptive-sharpen-light.glsl",
		["medium"] = "adaptive-sharpen-medium.glsl",
		["strong"] = "adaptive-sharpen-strong.glsl"
	};

	private static string? _hostBundleDirectory;

	private static readonly HashSet<string> ImageVideoCodecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "png", "jpeg", "jpg", "gif", "webp", "bmp", "tga", "svg" };

	private bool _isPreloading;
	private readonly ILogger<PlayerViewModel> _logger;

	private readonly DispatcherQueue _queue;

	private readonly ISettingsToolkit _settingsToolkit;

	// t209（G1 收 0）：原 `private readonly IFileToolkit _fileToolkit;` 已删除 —— IDE0052（只写不读的私有成员）
	// 且**工具无自动修复** ⇒ 它是 `dotnet format --verify-no-changes` 收 0 的最后一处阻塞。删除为**行为等价**（全文件 0 处读取，唯一写点即下方构造器赋值）。

	private DispatcherQueueTimer? _tipTimer;

	private string _lastMediaPath = string.Empty;

	private MpvPlayOptions? _lastPlayOptions;

	private bool _isWindowShown;

	private bool _suppressMediaBadgeRefresh;

	private bool _hasAppliedInitialTrackSelection;

	private string? _pendingSubtitleId;

	private string? _pendingSubtitleUrl;

	private List<OverlayTrackOption> _hostSubtitleTracks = new List<OverlayTrackOption>();

	private DateTimeOffset _lastProgressReportAt = DateTimeOffset.MinValue;

	private double _lastReportedPosition = -1.0;

	private bool _loggedProgressReporting;

	private bool _stopReported;

	private bool _hasObservedPlayback;

	private bool _hasStartedPlaying;

	private HostNavigateOptions? _pendingNextEpisodeData;

	private DispatcherQueueTimer? _networkPollTimer;

	private double _lastCacheSpeed;

	private readonly HashSet<(long Start, long End)> _skippedHeadSegments = new HashSet<(long, long)>();

	private readonly HashSet<(long Start, long End)> _skippedTailSegments = new HashSet<(long, long)>();

	private bool _suppressSkipSettingWrites;

	private double _preMuteVolume = 100.0;

	private RectInt32? _preFullscreenRect;

	private bool _preFullscreenMaximized;

	private int _fullScreenGeneration;

	private RectInt32? _preCompactOverlayRect;

	private readonly Dictionary<int, DanmakuApiSelectionState> _danmakuSelections = new Dictionary<int, DanmakuApiSelectionState>();

	private bool _suppressDanmakuSettingWrites;

	private bool _suppressShaderReapply;

	private bool _isResizingToVideo;

	private RectInt32? _initWindowTargetRect;

	private MpvEndFileReason? _lastEndFileReason;

	private bool _isGaplessTransitioning;

	private bool _gaplessHandoffInProgress;

	private long _preloadedAtPlaylistPos = -1L;

	private bool _allowEpisodeNavigation;

	private bool _autoAdvanceInProgress;

	private bool _isClosingWindow;

	private bool _streamAccessErrorDetected;

	private bool _streamRefreshInProgress;

	private bool _streamAbortInFlight;

	private int _streamRefreshAttempts;

	private DateTimeOffset _lastStreamRefreshAttempt = DateTimeOffset.MinValue;

	private bool _awaitingStreamRecoveryLoad;

	private double _streamRecoveryPosition;

	private string? _pendingRecoveryMediaPath;

	private DateTimeOffset? _streamPausedAtUtc;

	private static readonly TimeSpan ProactiveStreamRefreshPauseThreshold = TimeSpan.FromMinutes(2L);
	private HostNavigateOptions? _pendingStreamRefreshData;

	private bool _streamRefreshFetchInProgress;

	private bool _streamRefreshAppended;

	private bool _streamRefreshHandoffInProgress;

	private bool _isStreamRefreshTransitioning;

	private double _streamRefreshHandoffPosition;

	// t209（G1 收 0）：原 `private long _streamRefreshAppendedAtPlaylistPos = -1L;` 已删除 —— 同为 IDE0052（只写不读）
	// 且无自动修复；全文件 0 处读取，唯一写点即下方赋值 ⇒ 删除行为等价。

	private OverlayTrackOption? _streamRefreshPreservedAudio;

	private OverlayTrackOption? _streamRefreshPreservedSubtitle;

	private DispatcherQueueTimer? _pauseRefreshTimer;

	[CompilerGenerated]
	private string _003CMediaTitle_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private string _003CMediaSubtitle_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private string _003CMediaMonogram_003Ek__BackingField = "AI";

	[CompilerGenerated]
	private string _003CMediaLogo_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private string _003CMediaBackdropUrl_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private VideoFitMode _003CVideoFitMode_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsDanmakuEnabled_003Ek__BackingField = true;

	[CompilerGenerated]
	private double _003CSpeed_003Ek__BackingField = 1.0;

	[CompilerGenerated]
	private bool _003CIsWindowButtonsVisible_003Ek__BackingField = true;

	[CompilerGenerated]
	private string _003CPreviousEpisodeId_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private string _003CNextEpisodeId_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private string _003CNetworkSpeedText_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private bool _003CIsSkipFeatureEnabled_003Ek__BackingField = true;

	private List<TodbChapter> _todbChapters = new List<TodbChapter>();

	[CompilerGenerated]
	private double _003CDanmakuOpacity_003Ek__BackingField = 1.0;

	[CompilerGenerated]
	private int _003CDanmakuAreaRatio_003Ek__BackingField = 9;

	[CompilerGenerated]
	private int _003CDanmakuDensity_003Ek__BackingField = -1;

	[CompilerGenerated]
	private double _003CDanmakuSpeed_003Ek__BackingField = 5.0;

	[CompilerGenerated]
	private double _003CDanmakuFontSizeOffset_003Ek__BackingField = 1.0;

	[CompilerGenerated]
	private bool _003CDanmakuRollingEnabled_003Ek__BackingField = true;

	[CompilerGenerated]
	private bool _003CDanmakuTopEnabled_003Ek__BackingField = true;

	[CompilerGenerated]
	private bool _003CDanmakuBottomEnabled_003Ek__BackingField = true;

	[CompilerGenerated]
	private double _003CDanmakuTimeOffsetSeconds_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CDanmakuFollowSpeed_003Ek__BackingField = true;

	[CompilerGenerated]
	private string _003CDanmakuFontFamily_003Ek__BackingField = string.Empty;

	[CompilerGenerated]
	private double _003CDanmakuOutlineSize_003Ek__BackingField = 1.0;

	[CompilerGenerated]
	private string _003CCurrentAnimeMode_003Ek__BackingField = "none";

	[CompilerGenerated]
	private string _003CCurrentSharpenMode_003Ek__BackingField = "none";
	private SubclassProcDelegate? _subclassProc;

	[CompilerGenerated]
	private bool _003CIsSourceLoading_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsFileLoading_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsPlaying_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsIdle_003Ek__BackingField;

	[CompilerGenerated]
	private MpvPlayerState _003CLastState_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CDuration_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CCurrentPosition_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CVolume_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CSubtitleDelay_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsFullScreen_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsCompactOverlay_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsTopmost_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsProgressChanging_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CPreviewPosition_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsVolumeChanging_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsControlVisible_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsBackdropVisible_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsRestartVisible_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsWindowMaximized_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CBufferedPosition_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsNetworkSpeedVisible_003Ek__BackingField;

	[CompilerGenerated]
	private string _003CSeasonId_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CIntroEndTime_003Ek__BackingField;

	[CompilerGenerated]
	private double _003COutroStartTime_003Ek__BackingField;

	[CompilerGenerated]
	private double _003CPreviewStartTime_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CAutoSkipIntro_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CAutoSkipRecap_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CAutoSkipOutro_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CAutoSkipPreview_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CDanmakuBold_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CDanmakuNoOverlapSubtitle_003Ek__BackingField;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? togglePlayPauseCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? increaseVolumeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? decreaseVolumeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? toggleMuteCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? increaseSpeedCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? decreaseSpeedCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? resetSpeedCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? backToDefaultModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? forwardSkipCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? backwardSkipCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? navigatePreviousEpisodeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? navigateNextEpisodeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<string?>? navigateEpisodeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<double>? seekToCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<double>? setVolumeValueCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<double>? setSpeedValueCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<VideoFitMode>? selectVideoFitModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<OverlayTrackOption?>? selectAudioTrackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<OverlayTrackOption?>? selectSubtitleTrackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<OverlayVersionOption?>? selectVersionCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? addExternalSubtitleCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<double>? setSubtitleDelayCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? restartCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? toggleCompactOverlayCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? toggleFullScreenCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? exitFullScreenCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleTopmostCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? minimizeWindowCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMaximizeRestoreWindowCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? markIntroEndCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? markOutroStartCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? clearIntroCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? clearOutroCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleAutoSkipIntroCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleAutoSkipRecapCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleAutoSkipOutroCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleAutoSkipPreviewCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? closeWindowCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<string>? selectAnimeModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<string>? selectSharpenModeCommand;

	private static string HostBundleDirectory => _hostBundleDirectory ?? (_hostBundleDirectory = Path.GetFullPath(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory));

	internal PlayerUpdateServer? UpdateServer { get; set; }

	internal bool IsGaplessTransitioning => _isGaplessTransitioning;

	internal bool IsStreamRefreshTransitioning => _isStreamRefreshTransitioning;

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MediaTitle
	{
		get
		{
			return _003CMediaTitle_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CMediaTitle_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.MediaTitle);
				_003CMediaTitle_003Ek__BackingField = value;
				OnMediaTitleChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.MediaTitle);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MediaSubtitle
	{
		get
		{
			return _003CMediaSubtitle_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CMediaSubtitle_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.MediaSubtitle);
				_003CMediaSubtitle_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.MediaSubtitle);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MediaMonogram
	{
		get
		{
			return _003CMediaMonogram_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CMediaMonogram_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.MediaMonogram);
				_003CMediaMonogram_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.MediaMonogram);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MediaLogo
	{
		get
		{
			return _003CMediaLogo_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CMediaLogo_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.MediaLogo);
				_003CMediaLogo_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.MediaLogo);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MediaBackdropUrl
	{
		get
		{
			return _003CMediaBackdropUrl_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CMediaBackdropUrl_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.MediaBackdropUrl);
				_003CMediaBackdropUrl_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.MediaBackdropUrl);
			}
		}
	}

	internal MpvClient? Client { get; private set; }

	internal MpvPlayerWindow? Window { get; private set; }

	internal PlaybackReportClient? ReportClient { get; set; }

	internal string Id { get; private set; }

	internal string? HttpProxy { get; set; }

	internal string? MpvConfigDir { get; set; }

	internal bool FitVideoSize { get; set; } = true;

	internal bool AutoPlayNextEpisode { get; set; } = true;

	internal int MaxVolume { get; set; } = 100;

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public VideoFitMode VideoFitMode
	{
		get
		{
			return _003CVideoFitMode_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<VideoFitMode>.Default.Equals(_003CVideoFitMode_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.VideoFitMode);
				_003CVideoFitMode_003Ek__BackingField = value;
				OnVideoFitModeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.VideoFitMode);
			}
		}
	}

	internal IReadOnlyList<DanmakuApiEntry> DanmakuApis { get; set; } = Array.Empty<DanmakuApiEntry>();

	internal string? DanmakuMatchName { get; set; }

	internal int SelectedDanmakuApiIndex { get; set; }

	internal string? CurrentDanmakuApiBase
	{
		get
		{
			if (DanmakuApis.Count <= 0)
			{
				return null;
			}
			return DanmakuApis[Math.Clamp(SelectedDanmakuApiIndex, 0, DanmakuApis.Count - 1)].Url;
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDanmakuEnabled
	{
		get
		{
			return _003CIsDanmakuEnabled_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsDanmakuEnabled_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsDanmakuEnabled);
				_003CIsDanmakuEnabled_003Ek__BackingField = value;
				OnIsDanmakuEnabledChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsDanmakuEnabled);
			}
		}
	}

	internal bool IsPickerOpen { get; private set; }

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSourceLoading
	{
		get
		{
			return _003CIsSourceLoading_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsSourceLoading_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsSourceLoading);
				_003CIsSourceLoading_003Ek__BackingField = value;
				OnIsSourceLoadingChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsSourceLoading);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsFileLoading
	{
		get
		{
			return _003CIsFileLoading_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsFileLoading_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsFileLoading);
				_003CIsFileLoading_003Ek__BackingField = value;
				OnIsFileLoadingChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsFileLoading);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsPlaying
	{
		get
		{
			return _003CIsPlaying_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsPlaying_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsPlaying);
				_003CIsPlaying_003Ek__BackingField = value;
				OnIsPlayingChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsPlaying);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsIdle
	{
		get
		{
			return _003CIsIdle_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsIdle_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsIdle);
				_003CIsIdle_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsIdle);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public MpvPlayerState LastState
	{
		get
		{
			return _003CLastState_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<MpvPlayerState>.Default.Equals(_003CLastState_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.LastState);
				_003CLastState_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.LastState);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double Duration
	{
		get
		{
			return _003CDuration_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDuration_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.Duration);
				_003CDuration_003Ek__BackingField = value;
				OnDurationChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.Duration);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double CurrentPosition
	{
		get
		{
			return _003CCurrentPosition_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CCurrentPosition_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.CurrentPosition);
				_003CCurrentPosition_003Ek__BackingField = value;
				OnCurrentPositionChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.CurrentPosition);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double Volume
	{
		get
		{
			return _003CVolume_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CVolume_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.Volume);
				_003CVolume_003Ek__BackingField = value;
				OnVolumeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.Volume);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double Speed
	{
		get
		{
			return _003CSpeed_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CSpeed_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.Speed);
				_003CSpeed_003Ek__BackingField = value;
				OnSpeedChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.Speed);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double SubtitleDelay
	{
		get
		{
			return _003CSubtitleDelay_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CSubtitleDelay_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.SubtitleDelay);
				_003CSubtitleDelay_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.SubtitleDelay);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsFullScreen
	{
		get
		{
			return _003CIsFullScreen_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsFullScreen_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsFullScreen);
				_003CIsFullScreen_003Ek__BackingField = value;
				OnIsFullScreenChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsFullScreen);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsCompactOverlay
	{
		get
		{
			return _003CIsCompactOverlay_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsCompactOverlay_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsCompactOverlay);
				_003CIsCompactOverlay_003Ek__BackingField = value;
				OnIsCompactOverlayChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsCompactOverlay);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsTopmost
	{
		get
		{
			return _003CIsTopmost_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsTopmost_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsTopmost);
				_003CIsTopmost_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsTopmost);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsProgressChanging
	{
		get
		{
			return _003CIsProgressChanging_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsProgressChanging_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsProgressChanging);
				_003CIsProgressChanging_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsProgressChanging);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double PreviewPosition
	{
		get
		{
			return _003CPreviewPosition_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CPreviewPosition_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.PreviewPosition);
				_003CPreviewPosition_003Ek__BackingField = value;
				OnPreviewPositionChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.PreviewPosition);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsVolumeChanging
	{
		get
		{
			return _003CIsVolumeChanging_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsVolumeChanging_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsVolumeChanging);
				_003CIsVolumeChanging_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsVolumeChanging);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsControlVisible
	{
		get
		{
			return _003CIsControlVisible_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsControlVisible_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsControlVisible);
				_003CIsControlVisible_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsControlVisible);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsBackdropVisible
	{
		get
		{
			return _003CIsBackdropVisible_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsBackdropVisible_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsBackdropVisible);
				_003CIsBackdropVisible_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsBackdropVisible);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRestartVisible
	{
		get
		{
			return _003CIsRestartVisible_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsRestartVisible_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsRestartVisible);
				_003CIsRestartVisible_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsRestartVisible);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsWindowMaximized
	{
		get
		{
			return _003CIsWindowMaximized_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsWindowMaximized_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsWindowMaximized);
				_003CIsWindowMaximized_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsWindowMaximized);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsWindowButtonsVisible
	{
		get
		{
			return _003CIsWindowButtonsVisible_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsWindowButtonsVisible_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsWindowButtonsVisible);
				_003CIsWindowButtonsVisible_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsWindowButtonsVisible);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string PreviousEpisodeId
	{
		get
		{
			return _003CPreviousEpisodeId_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CPreviousEpisodeId_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.PreviousEpisodeId);
				_003CPreviousEpisodeId_003Ek__BackingField = value;
				OnPreviousEpisodeIdChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.PreviousEpisodeId);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string NextEpisodeId
	{
		get
		{
			return _003CNextEpisodeId_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CNextEpisodeId_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.NextEpisodeId);
				_003CNextEpisodeId_003Ek__BackingField = value;
				OnNextEpisodeIdChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.NextEpisodeId);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double BufferedPosition
	{
		get
		{
			return _003CBufferedPosition_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CBufferedPosition_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.BufferedPosition);
				_003CBufferedPosition_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.BufferedPosition);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string NetworkSpeedText
	{
		get
		{
			return _003CNetworkSpeedText_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CNetworkSpeedText_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.NetworkSpeedText);
				_003CNetworkSpeedText_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.NetworkSpeedText);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsNetworkSpeedVisible
	{
		get
		{
			return _003CIsNetworkSpeedVisible_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsNetworkSpeedVisible_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsNetworkSpeedVisible);
				_003CIsNetworkSpeedVisible_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsNetworkSpeedVisible);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SeasonId
	{
		get
		{
			return _003CSeasonId_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CSeasonId_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.SeasonId);
				_003CSeasonId_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.SeasonId);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double IntroEndTime
	{
		get
		{
			return _003CIntroEndTime_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CIntroEndTime_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IntroEndTime);
				_003CIntroEndTime_003Ek__BackingField = value;
				OnIntroEndTimeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IntroEndTime);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double OutroStartTime
	{
		get
		{
			return _003COutroStartTime_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003COutroStartTime_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.OutroStartTime);
				_003COutroStartTime_003Ek__BackingField = value;
				OnOutroStartTimeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.OutroStartTime);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double PreviewStartTime
	{
		get
		{
			return _003CPreviewStartTime_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CPreviewStartTime_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.PreviewStartTime);
				_003CPreviewStartTime_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.PreviewStartTime);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool AutoSkipIntro
	{
		get
		{
			return _003CAutoSkipIntro_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CAutoSkipIntro_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.AutoSkipIntro);
				_003CAutoSkipIntro_003Ek__BackingField = value;
				OnAutoSkipIntroChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.AutoSkipIntro);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool AutoSkipRecap
	{
		get
		{
			return _003CAutoSkipRecap_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CAutoSkipRecap_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.AutoSkipRecap);
				_003CAutoSkipRecap_003Ek__BackingField = value;
				OnAutoSkipRecapChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.AutoSkipRecap);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool AutoSkipOutro
	{
		get
		{
			return _003CAutoSkipOutro_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CAutoSkipOutro_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.AutoSkipOutro);
				_003CAutoSkipOutro_003Ek__BackingField = value;
				OnAutoSkipOutroChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.AutoSkipOutro);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool AutoSkipPreview
	{
		get
		{
			return _003CAutoSkipPreview_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CAutoSkipPreview_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.AutoSkipPreview);
				_003CAutoSkipPreview_003Ek__BackingField = value;
				OnAutoSkipPreviewChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.AutoSkipPreview);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSkipFeatureEnabled
	{
		get
		{
			return _003CIsSkipFeatureEnabled_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsSkipFeatureEnabled_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsSkipFeatureEnabled);
				_003CIsSkipFeatureEnabled_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsSkipFeatureEnabled);
			}
		}
	}

	public ObservableCollection<string> MediaBadges { get; } = new ObservableCollection<string>();

	public ObservableCollection<OverlayVersionOption> VersionOptions { get; } = new ObservableCollection<OverlayVersionOption>();

	public ObservableCollection<OverlayTrackOption> AudioTracks { get; } = new ObservableCollection<OverlayTrackOption>();

	public ObservableCollection<OverlayTrackOption> SubtitleTracks { get; } = new ObservableCollection<OverlayTrackOption>();

	public ObservableCollection<EpisodeListItem> EpisodeList { get; } = new ObservableCollection<EpisodeListItem>();

	public ObservableCollection<MediaSegment> ActiveSegments { get; } = new ObservableCollection<MediaSegment>();

	public bool HasActiveSegments => ActiveSegments.Count > 0;

	public string? UpdateUrl { get; set; }

	public ObservableCollection<TodbChapter> Chapters { get; } = new ObservableCollection<TodbChapter>();

	public bool HasChapters => Chapters.Count > 0;

	public TodbSprite? Sprite { get; private set; }

	public bool HasSprite => Sprite != null;

	public List<VttSpriteEntry> VttEntries { get; } = new List<VttSpriteEntry>();

	internal Dictionary<string, SoftwareBitmap?> SpriteImageCache { get; } = new Dictionary<string, SoftwareBitmap>();

	public string CurrentPositionText => FormatTime(IsProgressChanging ? PreviewPosition : CurrentPosition);

	public string DurationText => FormatTime(Duration);

	public bool HasMedia => !string.IsNullOrWhiteSpace(MediaTitle);

	public string PlayPauseGlyph
	{
		get
		{
			if (!IsPlaying)
			{
				return "\ue768";
			}
			return "\ue769";
		}
	}

	public string SelectedAudioTrackLabel => AudioTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected)?.Label ?? "音轨";

	public string SelectedSubtitleTrackLabel => SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected)?.Label ?? "字幕";

	public string SelectedVersionLabel => VersionOptions.FirstOrDefault((OverlayVersionOption p) => p.IsSelected)?.Label ?? "版本";

	public string VideoFitModeLabel => VideoFitMode switch
	{
		VideoFitMode.Cover => "填充",
		VideoFitMode.Stretch => "拉伸",
		_ => "适应",
	};

	public bool HasEpisodeList => EpisodeList.Count > 0;

	public bool HasPreviousEpisode => !string.IsNullOrWhiteSpace(PreviousEpisodeId);

	public bool HasPreviousInEpisodeList
	{
		get
		{
			if (!HasEpisodeList)
			{
				return false;
			}
			EpisodeListItem episodeListItem = EpisodeList.FirstOrDefault((EpisodeListItem e) => e.IsCurrent);
			if (episodeListItem == null)
			{
				return false;
			}
			return EpisodeList.IndexOf(episodeListItem) > 0;
		}
	}

	public bool CanNavigatePreviousEpisode
	{
		get
		{
			if (!HasEpisodeList)
			{
				return HasPreviousEpisode;
			}
			return HasPreviousInEpisodeList;
		}
	}

	public bool HasNextEpisode => !string.IsNullOrWhiteSpace(NextEpisodeId);

	public bool HasNextInEpisodeList
	{
		get
		{
			if (!HasEpisodeList)
			{
				return false;
			}
			EpisodeListItem episodeListItem = EpisodeList.FirstOrDefault((EpisodeListItem e) => e.IsCurrent);
			if (episodeListItem == null)
			{
				return false;
			}
			int num = EpisodeList.IndexOf(episodeListItem);
			if (num >= 0)
			{
				return num < EpisodeList.Count - 1;
			}
			return false;
		}
	}

	public bool CanNavigateNextEpisode
	{
		get
		{
			if (!HasEpisodeList)
			{
				return HasNextEpisode;
			}
			return HasNextInEpisodeList;
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DanmakuOpacity
	{
		get
		{
			return _003CDanmakuOpacity_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDanmakuOpacity_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuOpacity);
				_003CDanmakuOpacity_003Ek__BackingField = value;
				OnDanmakuOpacityChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuOpacity);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int DanmakuAreaRatio
	{
		get
		{
			return _003CDanmakuAreaRatio_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_003CDanmakuAreaRatio_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuAreaRatio);
				_003CDanmakuAreaRatio_003Ek__BackingField = value;
				OnDanmakuAreaRatioChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuAreaRatio);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int DanmakuDensity
	{
		get
		{
			return _003CDanmakuDensity_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_003CDanmakuDensity_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuDensity);
				_003CDanmakuDensity_003Ek__BackingField = value;
				OnDanmakuDensityChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuDensity);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DanmakuSpeed
	{
		get
		{
			return _003CDanmakuSpeed_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDanmakuSpeed_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuSpeed);
				_003CDanmakuSpeed_003Ek__BackingField = value;
				OnDanmakuSpeedChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuSpeed);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DanmakuFontSizeOffset
	{
		get
		{
			return _003CDanmakuFontSizeOffset_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDanmakuFontSizeOffset_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuFontSizeOffset);
				_003CDanmakuFontSizeOffset_003Ek__BackingField = value;
				OnDanmakuFontSizeOffsetChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuFontSizeOffset);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuBold
	{
		get
		{
			return _003CDanmakuBold_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuBold_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuBold);
				_003CDanmakuBold_003Ek__BackingField = value;
				OnDanmakuBoldChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuBold);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuRollingEnabled
	{
		get
		{
			return _003CDanmakuRollingEnabled_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuRollingEnabled_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuRollingEnabled);
				_003CDanmakuRollingEnabled_003Ek__BackingField = value;
				OnDanmakuRollingEnabledChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuRollingEnabled);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuTopEnabled
	{
		get
		{
			return _003CDanmakuTopEnabled_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuTopEnabled_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuTopEnabled);
				_003CDanmakuTopEnabled_003Ek__BackingField = value;
				OnDanmakuTopEnabledChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuTopEnabled);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuBottomEnabled
	{
		get
		{
			return _003CDanmakuBottomEnabled_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuBottomEnabled_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuBottomEnabled);
				_003CDanmakuBottomEnabled_003Ek__BackingField = value;
				OnDanmakuBottomEnabledChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuBottomEnabled);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DanmakuTimeOffsetSeconds
	{
		get
		{
			return _003CDanmakuTimeOffsetSeconds_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDanmakuTimeOffsetSeconds_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuTimeOffsetSeconds);
				_003CDanmakuTimeOffsetSeconds_003Ek__BackingField = value;
				OnDanmakuTimeOffsetSecondsChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuTimeOffsetSeconds);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuFollowSpeed
	{
		get
		{
			return _003CDanmakuFollowSpeed_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuFollowSpeed_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuFollowSpeed);
				_003CDanmakuFollowSpeed_003Ek__BackingField = value;
				OnDanmakuFollowSpeedChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuFollowSpeed);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DanmakuFontFamily
	{
		get
		{
			return _003CDanmakuFontFamily_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CDanmakuFontFamily_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuFontFamily);
				_003CDanmakuFontFamily_003Ek__BackingField = value;
				OnDanmakuFontFamilyChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuFontFamily);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DanmakuOutlineSize
	{
		get
		{
			return _003CDanmakuOutlineSize_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_003CDanmakuOutlineSize_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuOutlineSize);
				_003CDanmakuOutlineSize_003Ek__BackingField = value;
				OnDanmakuOutlineSizeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuOutlineSize);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DanmakuNoOverlapSubtitle
	{
		get
		{
			return _003CDanmakuNoOverlapSubtitle_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CDanmakuNoOverlapSubtitle_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.DanmakuNoOverlapSubtitle);
				_003CDanmakuNoOverlapSubtitle_003Ek__BackingField = value;
				OnDanmakuNoOverlapSubtitleChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.DanmakuNoOverlapSubtitle);
			}
		}
	}

	internal PlayerShortcuts Shortcuts { get; set; } = PlayerShortcuts.Default;

	internal bool DefaultRtxVsr { get; set; }

	internal bool DefaultRtxVideoHdr { get; set; }

	internal string DefaultAnimeMode { get; set; } = "none";

	internal string DefaultSharpenMode { get; set; } = "none";

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentAnimeMode
	{
		get
		{
			return _003CCurrentAnimeMode_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CCurrentAnimeMode_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.CurrentAnimeMode);
				_003CCurrentAnimeMode_003Ek__BackingField = value;
				OnCurrentAnimeModeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.CurrentAnimeMode);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentSharpenMode
	{
		get
		{
			return _003CCurrentSharpenMode_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CCurrentSharpenMode_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.CurrentSharpenMode);
				_003CCurrentSharpenMode_003Ek__BackingField = value;
				OnCurrentSharpenModeChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.CurrentSharpenMode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand TogglePlayPauseCommand => togglePlayPauseCommand ?? (togglePlayPauseCommand = new AsyncRelayCommand(TogglePlayPauseAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand IncreaseVolumeCommand => increaseVolumeCommand ?? (increaseVolumeCommand = new AsyncRelayCommand(IncreaseVolumeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand DecreaseVolumeCommand => decreaseVolumeCommand ?? (decreaseVolumeCommand = new AsyncRelayCommand(DecreaseVolumeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ToggleMuteCommand => toggleMuteCommand ?? (toggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand IncreaseSpeedCommand => increaseSpeedCommand ?? (increaseSpeedCommand = new AsyncRelayCommand(IncreaseSpeedAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand DecreaseSpeedCommand => decreaseSpeedCommand ?? (decreaseSpeedCommand = new AsyncRelayCommand(DecreaseSpeedAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ResetSpeedCommand => resetSpeedCommand ?? (resetSpeedCommand = new AsyncRelayCommand(ResetSpeedAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand BackToDefaultModeCommand => backToDefaultModeCommand ?? (backToDefaultModeCommand = new AsyncRelayCommand(BackToDefaultModeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ForwardSkipCommand => forwardSkipCommand ?? (forwardSkipCommand = new AsyncRelayCommand(ForwardSkipAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand BackwardSkipCommand => backwardSkipCommand ?? (backwardSkipCommand = new AsyncRelayCommand(BackwardSkipAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand NavigatePreviousEpisodeCommand => navigatePreviousEpisodeCommand ?? (navigatePreviousEpisodeCommand = new AsyncRelayCommand(NavigatePreviousEpisodeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand NavigateNextEpisodeCommand => navigateNextEpisodeCommand ?? (navigateNextEpisodeCommand = new AsyncRelayCommand(NavigateNextEpisodeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<string?> NavigateEpisodeCommand => navigateEpisodeCommand ?? (navigateEpisodeCommand = new AsyncRelayCommand<string>(NavigateEpisodeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<double> SeekToCommand => seekToCommand ?? (seekToCommand = new AsyncRelayCommand<double>(SeekToAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<double> SetVolumeValueCommand => setVolumeValueCommand ?? (setVolumeValueCommand = new AsyncRelayCommand<double>(SetVolumeValueAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<double> SetSpeedValueCommand => setSpeedValueCommand ?? (setSpeedValueCommand = new AsyncRelayCommand<double>(SetSpeedValueAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<VideoFitMode> SelectVideoFitModeCommand => selectVideoFitModeCommand ?? (selectVideoFitModeCommand = new RelayCommand<VideoFitMode>(SelectVideoFitMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<OverlayTrackOption?> SelectAudioTrackCommand => selectAudioTrackCommand ?? (selectAudioTrackCommand = new AsyncRelayCommand<OverlayTrackOption>(SelectAudioTrackAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<OverlayTrackOption?> SelectSubtitleTrackCommand => selectSubtitleTrackCommand ?? (selectSubtitleTrackCommand = new AsyncRelayCommand<OverlayTrackOption>(SelectSubtitleTrackAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<OverlayVersionOption?> SelectVersionCommand => selectVersionCommand ?? (selectVersionCommand = new AsyncRelayCommand<OverlayVersionOption>(SelectVersionAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand AddExternalSubtitleCommand => addExternalSubtitleCommand ?? (addExternalSubtitleCommand = new AsyncRelayCommand(AddExternalSubtitleAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<double> SetSubtitleDelayCommand => setSubtitleDelayCommand ?? (setSubtitleDelayCommand = new AsyncRelayCommand<double>(SetSubtitleDelayAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RestartCommand => restartCommand ?? (restartCommand = new AsyncRelayCommand(RestartAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ToggleCompactOverlayCommand => toggleCompactOverlayCommand ?? (toggleCompactOverlayCommand = new AsyncRelayCommand(ToggleCompactOverlayAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ToggleFullScreenCommand => toggleFullScreenCommand ?? (toggleFullScreenCommand = new AsyncRelayCommand(ToggleFullScreenAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ExitFullScreenCommand => exitFullScreenCommand ?? (exitFullScreenCommand = new AsyncRelayCommand(ExitFullScreenAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleTopmostCommand => toggleTopmostCommand ?? (toggleTopmostCommand = new RelayCommand(ToggleTopmost));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand MinimizeWindowCommand => minimizeWindowCommand ?? (minimizeWindowCommand = new RelayCommand(MinimizeWindow));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMaximizeRestoreWindowCommand => toggleMaximizeRestoreWindowCommand ?? (toggleMaximizeRestoreWindowCommand = new RelayCommand(ToggleMaximizeRestoreWindow));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand MarkIntroEndCommand => markIntroEndCommand ?? (markIntroEndCommand = new RelayCommand(MarkIntroEnd));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand MarkOutroStartCommand => markOutroStartCommand ?? (markOutroStartCommand = new RelayCommand(MarkOutroStart));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ClearIntroCommand => clearIntroCommand ?? (clearIntroCommand = new RelayCommand(ClearIntro));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ClearOutroCommand => clearOutroCommand ?? (clearOutroCommand = new RelayCommand(ClearOutro));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleAutoSkipIntroCommand => toggleAutoSkipIntroCommand ?? (toggleAutoSkipIntroCommand = new RelayCommand(ToggleAutoSkipIntro));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleAutoSkipRecapCommand => toggleAutoSkipRecapCommand ?? (toggleAutoSkipRecapCommand = new RelayCommand(ToggleAutoSkipRecap));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleAutoSkipOutroCommand => toggleAutoSkipOutroCommand ?? (toggleAutoSkipOutroCommand = new RelayCommand(ToggleAutoSkipOutro));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleAutoSkipPreviewCommand => toggleAutoSkipPreviewCommand ?? (toggleAutoSkipPreviewCommand = new RelayCommand(ToggleAutoSkipPreview));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CloseWindowCommand => closeWindowCommand ?? (closeWindowCommand = new AsyncRelayCommand(CloseWindowAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<string> SelectAnimeModeCommand => selectAnimeModeCommand ?? (selectAnimeModeCommand = new RelayCommand<string>(SelectAnimeMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<string> SelectSharpenModeCommand => selectSharpenModeCommand ?? (selectSharpenModeCommand = new RelayCommand<string>(SelectSharpenMode));

	private static void NavDebug(string msg)
	{
		try
		{
			// 🩹 t154（K-01）：本行原先直写 `%TEMP%\ai_player_nav_debug.log` —— **既绕过唯一打码点**
			//   （`SecretMaskingTextFormatter`），**又绕过数据根**（`AIPLAYER_APPDATA_ROOT` 管不到它）。
			//   现在：① 文件落 `App.AppDataRoot\logs\`（与 Serilog 同一个数据根，可用覆盖变量沙箱化）；
			//   ② 写出前过 `SecretMaskingTextFormatter.MaskSecrets`（与 Serilog sink 同一实现 ⇒ 同形态）。
			string path = NavDebugLogPath;
			if (path.Length == 0)
			{
				return;
			}
			File.AppendAllText(path, SecretMaskingTextFormatter.MaskSecrets($"{DateTime.Now:HH:mm:ss.fff} {msg}") + Environment.NewLine);
		}
		// t235：命名类型 = `Exception` + **块内理由**（此前为无类型空体 ⇒ H3b 与 H3 双命中）：
		// 本处是导航调试日志写出（内容已在上一行过 `MaskSecrets`）；写失败**有意不抛出、不上报**，
		// 理由 = 导航调试日志是纯诊断面，不得因日志不可写而影响导航主流程。
		// 可抛集合不可收敛：数据根解析 + `File.AppendAllText`（`IOException`/`UnauthorizedAccessException`/
		// `DirectoryNotFoundException`/`ArgumentException`）⇒ 共同祖先只有 `Exception`。
		catch (Exception)
		{
		}
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[RelayCommand]
	private async Task TogglePlayPauseAsync()
	{
		if (Client != null)
		{
			if (LastState == MpvPlayerState.End)
			{
				_allowEpisodeNavigation = true;
				await Client.ReplayAsync();
			}
			else if (IsPlaying)
			{
				await Client.PauseAsync();
				_streamPausedAtUtc = DateTimeOffset.UtcNow;
				SchedulePauseStreamRefreshTimer();
			}
			else
			{
				CancelPauseStreamRefreshTimer();
				ScheduleSeamlessStreamRefreshOnResume();
				_streamPausedAtUtc = null;
				await Client.ResumeAsync();
			}
		}
	}

	[RelayCommand]
	private async Task IncreaseVolumeAsync()
	{
		if (Client != null)
		{
			double num = Math.Min(MaxVolume, Volume + 5.0);
			if (Math.Abs(num - Volume) > 1.0)
			{
				Volume = num;
				_settingsToolkit.WriteLocalSetting("PlayerVolume", num);
				await Client.SetVolumeAsync(num);
				ShowVolumeTip();
			}
		}
	}

	[RelayCommand]
	private async Task DecreaseVolumeAsync()
	{
		if (Client != null)
		{
			double num = Math.Max(0.0, Volume - 5.0);
			if (Math.Abs(num - Volume) > 1.0)
			{
				Volume = num;
				_settingsToolkit.WriteLocalSetting("PlayerVolume", num);
				await Client.SetVolumeAsync(num);
				ShowVolumeTip();
			}
		}
	}

	[RelayCommand]
	private async Task ToggleMuteAsync()
	{
		if (Client != null)
		{
			if (Volume > 0.0)
			{
				_preMuteVolume = Volume;
				await SetVolumeValueAsync(0.0);
			}
			else
			{
				await SetVolumeValueAsync((_preMuteVolume > 0.0) ? _preMuteVolume : 100.0);
			}
		}
	}

	[RelayCommand]
	private async Task IncreaseSpeedAsync()
	{
		double num = Math.Min(3.0, Speed + 0.25);
		if (Math.Abs(num - Speed) > 0.01)
		{
			Speed = num;
			await Client.SetSpeedAsync(num);
		}
	}

	[RelayCommand]
	private async Task DecreaseSpeedAsync()
	{
		double num = Math.Max(0.1, Speed - 0.25);
		if (Math.Abs(num - Speed) > 0.01)
		{
			Speed = num;
			await Client.SetSpeedAsync(num);
		}
	}

	[RelayCommand]
	private async Task ResetSpeedAsync()
	{
		if (Client != null)
		{
			await SetSpeedValueAsync(1.0);
		}
	}

	[RelayCommand]
	private async Task BackToDefaultModeAsync()
	{
		if (IsFullScreen || IsWindowInFullScreenPresenter())
		{
			await SetFullScreenAsync(isFullScreen: false);
		}
		else if (IsCompactOverlay)
		{
			await Client.SetCompactOverlayStateAsync(isCompactOverlay: false);
		}
	}

	[RelayCommand]
	private async Task ForwardSkipAsync()
	{
		double num = Shortcuts.SeekForwardSeconds;
		if (!(num <= 0.0) && !(CurrentPosition <= 0.0))
		{
			double num2 = Math.Min(Duration, CurrentPosition + num);
			if (Math.Abs(num2 - CurrentPosition) > 1.0)
			{
				await Client.SetCurrentPositionAsync(num2);
			}
		}
	}

	[RelayCommand]
	private async Task BackwardSkipAsync()
	{
		double num = Shortcuts.SeekBackwardSeconds;
		if (!(num <= 0.0))
		{
			double currentPositionAsync = Math.Max(0.0, CurrentPosition - num);
			await Client.SetCurrentPositionAsync(currentPositionAsync);
		}
	}

	[RelayCommand]
	private async Task NavigatePreviousEpisodeAsync()
	{
		NavDebug($"NavigatePreviousEpisodeAsync ENTER ReportClient={((ReportClient == null) ? "null" : "ok")} HasEpisodeList={HasEpisodeList} HasPrev={HasPreviousEpisode}");
		if (ReportClient == null || !ReportClient.IsEnabled)
		{
			NavDebug("  -> early return (ReportClient null/disabled)");
		}
		else if (HasEpisodeList)
		{
			EpisodeListItem episodeListItem = EpisodeList.FirstOrDefault((EpisodeListItem e) => e.IsCurrent);
			if (episodeListItem != null)
			{
				int num = EpisodeList.IndexOf(episodeListItem);
				if (num > 0)
				{
					await NavigateEpisodeAsync(EpisodeList[num - 1].Id);
				}
			}
		}
		else
		{
			if (!HasPreviousEpisode)
			{
				return;
			}
			IsSourceLoading = true;
			bool handover = false;
			try
			{
				HostNavigateOptions hostNavigateOptions = await ReportClient.ReportNavigatePreviousAsync(Math.Max(0.0, CurrentPosition));
				if (hostNavigateOptions != null && !string.IsNullOrWhiteSpace(hostNavigateOptions.MediaPath))
				{
					await ReloadForNavigateAsync(hostNavigateOptions);
					handover = true;
				}
			}
			finally
			{
				if (!handover)
				{
					IsSourceLoading = false;
				}
			}
		}
	}

	[RelayCommand]
	private async Task NavigateNextEpisodeAsync()
	{
		NavDebug($"NavigateNextEpisodeAsync ENTER ReportClient={((ReportClient == null) ? "null" : "ok")} HasEpisodeList={HasEpisodeList} HasNext={HasNextEpisode}");
		if (IsSourceLoading)
		{
			NavDebug("  -> early return (IsSourceLoading=true, already navigating)");
		}
		else if (ReportClient == null || !ReportClient.IsEnabled)
		{
			NavDebug("  -> early return (ReportClient null/disabled)");
		}
		else if (HasEpisodeList)
		{
			EpisodeListItem episodeListItem = EpisodeList.FirstOrDefault((EpisodeListItem e) => e.IsCurrent);
			if (episodeListItem == null)
			{
				return;
			}
			int num = EpisodeList.IndexOf(episodeListItem);
			if (num >= 0 && num < EpisodeList.Count - 1)
			{
				string id = EpisodeList[num + 1].Id;
				_logger.LogInformation("Nav NEXT check: nextId={nextId} pendingId={pendingId}", id, _pendingNextEpisodeData?.NewItemId);
				if (_pendingNextEpisodeData == null || !string.Equals(_pendingNextEpisodeData.NewItemId, id, StringComparison.OrdinalIgnoreCase))
				{
					await NavigateEpisodeAsync(id);
					return;
				}
				_logger.LogInformation("Seamlessly jumping to preloaded next episode via playlist-next");
				_allowEpisodeNavigation = true;
				await HandoffToNextPlaylistEntryAsync();
			}
		}
		else
		{
			if (!HasNextEpisode)
			{
				return;
			}
			_logger.LogInformation("Nav NEXT check (fallback): nextId={nextId} pendingId={pendingId}", NextEpisodeId, _pendingNextEpisodeData?.NewItemId);
			if (_pendingNextEpisodeData == null || !string.Equals(_pendingNextEpisodeData.NewItemId, NextEpisodeId, StringComparison.OrdinalIgnoreCase))
			{
				IsSourceLoading = true;
				_logger.LogInformation("Nav NEXT: IsSourceLoading=true set, before HTTP call");
				bool handover = false;
				try
				{
					HostNavigateOptions hostNavigateOptions = await ReportClient.ReportNavigateNextAsync(Math.Max(0.0, CurrentPosition));
					if (hostNavigateOptions != null && !string.IsNullOrWhiteSpace(hostNavigateOptions.MediaPath))
					{
						await ReloadForNavigateAsync(hostNavigateOptions);
						handover = true;
					}
					return;
				}
				finally
				{
					if (!handover)
					{
						IsSourceLoading = false;
					}
				}
			}
			_logger.LogInformation("Seamlessly jumping to preloaded next episode via playlist-next (fallback)");
			_allowEpisodeNavigation = true;
			await HandoffToNextPlaylistEntryAsync();
		}
	}

	[RelayCommand]
	private async Task NavigateEpisodeAsync(string? episodeId)
	{
		NavDebug("NavigateEpisodeAsync ENTER episodeId=" + (episodeId ?? "<null>") + " ReportClient=" + ((ReportClient == null) ? "null" : "ok"));
		if (string.IsNullOrWhiteSpace(episodeId) || ReportClient == null || !ReportClient.IsEnabled)
		{
			NavDebug("  -> early return (bad args or ReportClient null/disabled)");
			return;
		}
		_logger.LogInformation("Nav EPISODE check: targetId={episodeId} pendingId={pendingId}", episodeId, _pendingNextEpisodeData?.NewItemId);
		if (_pendingNextEpisodeData != null && string.Equals(_pendingNextEpisodeData.NewItemId, episodeId, StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogInformation("Seamlessly jumping to preloaded target episode via playlist-next");
			_allowEpisodeNavigation = true;
			await HandoffToNextPlaylistEntryAsync();
			return;
		}
		IsSourceLoading = true;
		NavDebug("  -> IsSourceLoading=true set, awaiting HTTP");
		_logger.LogInformation("Nav EPISODE: IsSourceLoading=true set, before HTTP call");
		bool handover = false;
		try
		{
			HostNavigateOptions hostNavigateOptions = await ReportClient.ReportNavigateEpisodeAsync(episodeId, Math.Max(0.0, CurrentPosition));
			if (hostNavigateOptions != null && !string.IsNullOrWhiteSpace(hostNavigateOptions.MediaPath))
			{
				await ReloadForNavigateAsync(hostNavigateOptions);
				handover = true;
			}
		}
		finally
		{
			if (!handover)
			{
				IsSourceLoading = false;
			}
		}
	}

	[RelayCommand]
	private async Task SeekToAsync(double position)
	{
		if (Client != null)
		{
			double currentPositionAsync = Math.Max(0.0, Math.Min(Duration, position));
			await Client.SetCurrentPositionAsync(currentPositionAsync);
		}
	}

	[RelayCommand]
	private async Task SetVolumeValueAsync(double volume)
	{
		if (Client != null)
		{
			double num = (Volume = Math.Max(0.0, Math.Min(MaxVolume, volume)));
			if (num > 0.0)
			{
				_settingsToolkit.WriteLocalSetting("PlayerVolume", num);
			}
			await Client.SetVolumeAsync(num);
			ShowVolumeTip();
		}
	}

	[RelayCommand]
	private async Task SetSpeedValueAsync(double speed)
	{
		if (Client != null)
		{
			double speedAsync = (Speed = Math.Max(0.1, Math.Min(3.0, speed)));
			await Client.SetSpeedAsync(speedAsync);
		}
	}

	[RelayCommand]
	private void SelectVideoFitMode(VideoFitMode mode)
	{
		VideoFitMode = mode;
	}

	[RelayCommand]
	private async Task SelectAudioTrackAsync(OverlayTrackOption? track)
	{
		if (track != null && Client != null && await ApplyInternalAudioSelectionAsync(track))
		{
			MarkSelectedTrack(AudioTracks, track);
			OnPropertyChanged("SelectedAudioTrackLabel");
		}
	}

	[RelayCommand]
	private async Task SelectSubtitleTrackAsync(OverlayTrackOption? track)
	{
		if (track != null && Client != null && await ApplyTrackSelectionAsync("sid", track))
		{
			MarkSelectedTrack(SubtitleTracks, track);
			RefreshSubtitleTracksFromMpv();
			OnPropertyChanged("SelectedSubtitleTrackLabel");
		}
	}

	[RelayCommand]
	private async Task SelectVersionAsync(OverlayVersionOption? version)
	{
		if (version == null || ReportClient == null || !ReportClient.IsEnabled || version.IsSelected)
		{
			return;
		}
		IsSourceLoading = true;
		bool handover = false;
		try
		{
			HostNavigateOptions hostNavigateOptions = await ReportClient.ReportSwitchVersionAsync(version.Index, Math.Max(0.0, CurrentPosition));
			if (hostNavigateOptions != null && !string.IsNullOrWhiteSpace(hostNavigateOptions.MediaPath))
			{
				await ReloadForNavigateAsync(hostNavigateOptions);
				handover = true;
			}
		}
		finally
		{
			if (!handover)
			{
				IsSourceLoading = false;
			}
		}
	}

	[RelayCommand]
	private async Task AddExternalSubtitleAsync()
	{
		if (Client != null && Window != null)
		{
			FileOpenPicker fileOpenPicker = new FileOpenPicker();
			fileOpenPicker.FileTypeFilter.Add(".srt");
			fileOpenPicker.FileTypeFilter.Add(".ass");
			fileOpenPicker.FileTypeFilter.Add(".ssa");
			fileOpenPicker.FileTypeFilter.Add(".vtt");
			fileOpenPicker.FileTypeFilter.Add(".sub");
			InitializeWithWindow.Initialize(fileOpenPicker, Window.Handle);
			IsPickerOpen = true;
			Window.ShowCursor();
			StorageFile storageFile;
			try
			{
				storageFile = await fileOpenPicker.PickSingleFileAsync();
			}
			finally
			{
				IsPickerOpen = false;
				Window.ShowCursor();
			}
			if (storageFile is not null && (await Client.AddSubtitleAsync(storageFile.Path)).IsSuccess)
			{
				await RefreshTracksAsync();
			}
		}
	}

	[RelayCommand]
	private async Task SetSubtitleDelayAsync(double delay)
	{
		if (Client != null)
		{
			await Client.SetSubtitleDelayAsync(delay);
			SubtitleDelay = delay;
		}
	}

	[RelayCommand]
	private async Task RestartAsync()
	{
		if (Client != null)
		{
			_allowEpisodeNavigation = true;
			await Client.ReplayAsync();
		}
	}

	[RelayCommand]
	private async Task ToggleCompactOverlayAsync()
	{
		await Client.SetCompactOverlayStateAsync(!IsCompactOverlay);
	}

	[RelayCommand]
	private async Task ToggleFullScreenAsync()
	{
		await SetFullScreenAsync(!IsFullScreen);
	}

	[RelayCommand]
	private async Task ExitFullScreenAsync()
	{
		if (IsFullScreen || IsWindowInFullScreenPresenter())
		{
			await SetFullScreenAsync(isFullScreen: false);
		}
	}

	[RelayCommand]
	private void ToggleTopmost()
	{
		IsTopmost = !IsTopmost;
		if (Window?.GetWindow().Presenter is OverlappedPresenter overlappedPresenter)
		{
			overlappedPresenter.IsAlwaysOnTop = IsTopmost || IsCompactOverlay;
		}
	}

	[RelayCommand]
	private void MinimizeWindow()
	{
		if (Window?.GetWindow().Presenter is OverlappedPresenter overlappedPresenter)
		{
			overlappedPresenter.Minimize();
		}
	}

	[RelayCommand]
	private void ToggleMaximizeRestoreWindow()
	{
		if (Window?.GetWindow().Presenter is OverlappedPresenter overlappedPresenter)
		{
			if (IsWindowMaximized)
			{
				overlappedPresenter.Restore();
			}
			else
			{
				overlappedPresenter.Maximize();
			}
			UpdateWindowState();
		}
	}

	internal void BeginWindowDrag()
	{
		if (Window != null && !IsFullScreen)
		{
			ReleaseCapture();
			SendMessage(Window.Handle, 161u, new IntPtr(2), IntPtr.Zero);
		}
	}

	internal bool CanDragWindow()
	{
		if (Window != null && !IsFullScreen)
		{
			return !IsWindowMaximized;
		}
		return false;
	}

	internal PointInt32 GetWindowPosition()
	{
		if (Window == null)
		{
			return new PointInt32(0, 0);
		}
		PointInt32 position = Window.GetWindow().Position;
		return new PointInt32(position.X, position.Y);
	}

	internal void MoveWindowTo(int left, int top)
	{
		if (Window != null)
		{
			AppWindow window = Window.GetWindow();
			SizeInt32 size = window.Size;
			window.MoveAndResize(new RectInt32(left, top, size.Width, size.Height));
		}
	}

	[RelayCommand]
	private void MarkIntroEnd()
	{
		if (IsSkipFeatureEnabled)
		{
			IntroEndTime = CurrentPosition;
		}
	}

	[RelayCommand]
	private void MarkOutroStart()
	{
		if (IsSkipFeatureEnabled)
		{
			OutroStartTime = CurrentPosition;
		}
	}

	[RelayCommand]
	private void ClearIntro()
	{
		if (IsSkipFeatureEnabled)
		{
			IntroEndTime = 0.0;
		}
	}

	[RelayCommand]
	private void ClearOutro()
	{
		if (IsSkipFeatureEnabled)
		{
			OutroStartTime = 0.0;
		}
	}

	[RelayCommand]
	private void ToggleAutoSkipIntro()
	{
		if (IsSkipFeatureEnabled)
		{
			AutoSkipIntro = !AutoSkipIntro;
		}
	}

	[RelayCommand]
	private void ToggleAutoSkipRecap()
	{
		if (IsSkipFeatureEnabled)
		{
			AutoSkipRecap = !AutoSkipRecap;
		}
	}

	[RelayCommand]
	private void ToggleAutoSkipOutro()
	{
		if (IsSkipFeatureEnabled)
		{
			AutoSkipOutro = !AutoSkipOutro;
		}
	}

	[RelayCommand]
	private void ToggleAutoSkipPreview()
	{
		if (IsSkipFeatureEnabled)
		{
			AutoSkipPreview = !AutoSkipPreview;
		}
	}

	[RelayCommand]
	private async Task CloseWindowAsync()
	{
		if (_isClosingWindow)
		{
			_logger.LogDebug("CloseWindowAsync skipped: close already in progress.");
			return;
		}
		_isClosingWindow = true;
		Window?.Hide();
		StopNetworkPollTimer();
		_tipTimer?.Stop();
		if (_tipTimer is not null)
		{
			_tipTimer.Tick -= OnTipTimerTick;
		}
		_tipTimer = null;
		UpdateServer?.Dispose();
		UpdateServer = null;
		UpdateUrl = null;
		this.Get<AppViewModel>().PlayerWindows.Remove(this);
		bool shouldExit = this.Get<AppViewModel>().MainWindow is null && this.Get<AppViewModel>().PlayerWindows.Count == 0;
		MpvPlayerWindow window = Window;
		if (window != null)
		{
			window.GetWindow().Closing -= OnWindowClosing;
			window.GetWindow().Changed -= OnAppWindowChanged;
		}
		SaveCurrentWindowStats();
		TryReportStoppedAsync();
		if (window != null)
		{
			RemoveMaximizeSubclass();
			Window = null;
			if (shouldExit)
			{
				await window.DisposeAsync();
			}
			else
			{
				window.DisposeAsync();
			}
		}
		if (shouldExit)
		{
			Application.Current.Exit();
		}
	}

	public PlayerViewModel(ILogger<PlayerViewModel> logger, DispatcherQueue dispatcherQueue, ISettingsToolkit settingsToolkit, IFileToolkit fileToolkit)
	{
		_logger = logger;
		_queue = dispatcherQueue;
		_settingsToolkit = settingsToolkit;
		// t209：（原 `_fileToolkit = fileToolkit;` 一行随字段删除 —— 该字段只写不读，见类头注释）
		_tipTimer = _queue.CreateTimer();
		_tipTimer.Interval = TimeSpan.FromSeconds(1L);
		_tipTimer.Tick += OnTipTimerTick;
		LoadDanmakuDisplaySettings();
	}

	private void LoadDanmakuDisplaySettings()
	{
		_suppressDanmakuSettingWrites = true;
		DanmakuOpacity = _settingsToolkit.ReadLocalSetting("DanmakuOpacity", 1.0);
		DanmakuAreaRatio = _settingsToolkit.ReadLocalSetting("DanmakuAreaRatio", 9);
		DanmakuDensity = _settingsToolkit.ReadLocalSetting("DanmakuDensity", -1);
		DanmakuSpeed = _settingsToolkit.ReadLocalSetting("DanmakuSpeed", 5.0);
		DanmakuFontSizeOffset = _settingsToolkit.ReadLocalSetting("DanmakuFontSizeOffset", 1.0);
		DanmakuBold = _settingsToolkit.ReadLocalSetting("DanmakuBold", defaultValue: false);
		DanmakuRollingEnabled = _settingsToolkit.ReadLocalSetting("DanmakuRollingEnabled", defaultValue: true);
		DanmakuTopEnabled = _settingsToolkit.ReadLocalSetting("DanmakuTopEnabled", defaultValue: true);
		DanmakuBottomEnabled = _settingsToolkit.ReadLocalSetting("DanmakuBottomEnabled", defaultValue: true);
		DanmakuTimeOffsetSeconds = _settingsToolkit.ReadLocalSetting("DanmakuTimeOffset", 0.0);
		DanmakuFollowSpeed = _settingsToolkit.ReadLocalSetting("DanmakuFollowSpeed", defaultValue: true);
		DanmakuFontFamily = _settingsToolkit.ReadLocalSetting("DanmakuFontFamily", string.Empty);
		DanmakuOutlineSize = _settingsToolkit.ReadLocalSetting("DanmakuOutlineSize", 1.0);
		DanmakuNoOverlapSubtitle = _settingsToolkit.ReadLocalSetting("DanmakuNoOverlapSubtitle", defaultValue: false);
		_suppressDanmakuSettingWrites = false;
	}

	public async Task LoadAsync(string fileUrl, MpvPlayOptions? options)
	{
		_logger.LogInformation("[StartupDiag] LoadAsync begin media={MediaPath} mpvConfigDir={MpvConfigDir} videoFitMode={VideoFitMode}", DescribeMediaPathForLog(fileUrl), MpvConfigDir ?? "(none)", VideoFitMode);
		if (Client == null)
		{
			MpvInitializeOptions mpvInitializeOptions = new MpvInitializeOptions
			{
				UseConfig = !string.IsNullOrWhiteSpace(MpvConfigDir),
				ConfigDirectory = (string.IsNullOrWhiteSpace(MpvConfigDir) ? null : MpvConfigDir),
				HttpProxy = (string.IsNullOrWhiteSpace(HttpProxy) ? null : HttpProxy)
			};
			_logger.LogInformation("[StartupDiag] MpvClient.CreateAsync begin useConfig={UseConfig} httpProxy={HasHttpProxy}", mpvInitializeOptions.UseConfig == true, !string.IsNullOrWhiteSpace(mpvInitializeOptions.HttpProxy));
			Client = await MpvClient.CreateAsync(mpvInitializeOptions, _logger);
			_logger.LogInformation("[StartupDiag] MpvClient.CreateAsync done");
			Client.DataNotify += OnClientDataNotify;
			Client.ReachFileLoading += OnFileLoading;
			Client.ReachFileLoaded += OnFileLoaded;
			Client.ReachFileEnd += OnFileEnd;
			Client.StreamHttpError += OnStreamHttpError;
			Volume = Math.Clamp(_settingsToolkit.ReadLocalSetting("PlayerVolume", 50.0), 0.0, MaxVolume);
			_logger.LogInformation("[StartupDiag] InitializeWindow begin");
			InitializeWindow();
			_logger.LogInformation("[StartupDiag] InitializeWindow done handle={WindowHandle}", ((IntPtr)Window?.Handle).ToString() ?? "(null)");
			EnsureWindowShown();
			_logger.LogInformation("[StartupDiag] EnsureWindowShown done");
			await Client.SetLogLevelAsync(MpvLogLevel.Info);
			await Client.UseIdleAsync(true);
			await Client.UseKeepOpenAsync(isKeepOpen: true);
			_logger.LogInformation("[StartupDiag] mpv idle/keep-open configured");
			if (string.IsNullOrWhiteSpace(MpvConfigDir))
			{
				_logger.LogInformation("[StartupDiag] applying runtime decode/cache defaults (no mpv.conf)");
				await InitializeDecodeAsync();
				await InitializeCacheAsync();
				await ApplyEnhancementDefaultsAsync();
			}
			else
			{
				_logger.LogInformation("[StartupDiag] applying stream reconnect options (mpv.conf)");
				await ApplyStreamReconnectOptionsAsync();
			}
			_logger.LogInformation("[StartupDiag] ApplyVideoFitModeAsync begin mode={VideoFitMode}", VideoFitMode);
			await ApplyVideoFitModeAsync();
			_logger.LogInformation("[StartupDiag] ApplyVideoFitModeAsync done");
		}
		if (IsSkipFeatureEnabled)
		{
			IntroEndTime = _settingsToolkit.ReadLocalSetting(_GetSkipSettingKey("IntroEndTime"), 0.0);
			OutroStartTime = _settingsToolkit.ReadLocalSetting(_GetSkipSettingKey("OutroStartTime"), 0.0);
			AutoSkipIntro = _settingsToolkit.ReadLocalSetting("AutoSkipIntro", defaultValue: false);
			AutoSkipRecap = _settingsToolkit.ReadLocalSetting("AutoSkipRecap", defaultValue: true);
			AutoSkipOutro = _settingsToolkit.ReadLocalSetting("AutoSkipOutro", defaultValue: false);
			AutoSkipPreview = _settingsToolkit.ReadLocalSetting("AutoSkipPreview", defaultValue: true);
			PreviewStartTime = 0.0;
			_ApplySegmentMarkers();
		}
		else
		{
			_suppressSkipSettingWrites = true;
			IntroEndTime = 0.0;
			OutroStartTime = 0.0;
			PreviewStartTime = 0.0;
			AutoSkipIntro = false;
			AutoSkipRecap = false;
			AutoSkipOutro = false;
			AutoSkipPreview = false;
			_suppressSkipSettingWrites = false;
		}
		IsDanmakuEnabled = _settingsToolkit.ReadLocalSetting("IsDanmakuEnabled", defaultValue: true);
		Id = _lastMediaPath;
		_lastMediaPath = fileUrl;
		if (string.IsNullOrWhiteSpace(MediaTitle))
		{
			MediaTitle = Path.GetFileName(fileUrl);
		}
		if (string.IsNullOrWhiteSpace(MediaSubtitle))
		{
			MediaSubtitle = BuildMediaSubtitle(fileUrl, MediaTitle);
		}
		_lastPlayOptions = options ?? new MpvPlayOptions();
		if (IsSkipFeatureEnabled && !_lastPlayOptions.StartPosition.HasValue)
		{
			double num = ComputeInitialSkipPosition();
			if (num > 0.0)
			{
				_lastPlayOptions.StartPosition = num;
			}
		}
		_hasAppliedInitialTrackSelection = false;
		_logger.LogInformation("[StartupDiag] LoadMediaAsync begin");
		await LoadMediaAsync();
		_logger.LogInformation("[StartupDiag] LoadMediaAsync done");
	}

	private static string DescribeMediaPathForLog(string? path)
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

	internal async Task HandoffToNextPlaylistEntryAsync()
	{
		if (Client == null)
		{
			return;
		}
		if (_gaplessHandoffInProgress)
		{
			_logger.LogDebug("Gapless playlist handoff already in progress, skipping duplicate request.");
			return;
		}
		_gaplessHandoffInProgress = true;
		HostNavigateOptions pending = _pendingNextEpisodeData;
		if (ReportClient != null && ReportClient.IsEnabled)
		{
			try
			{
				double num = Math.Max(0.0, CurrentPosition);
				_logger.LogInformation("Gapless transition: reporting stopped for current episode before handoff (position={Position}s)", num);
				ReportClient.ReportStoppedAsync(num, BuildPlaybackStateSnapshot());
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Failed to report stopped before gapless handoff");
			}
		}
		if (!HasTrailingPlaylistEntries())
		{
			if (HasMpvAutoAdvancedToPreloadedEntry())
			{
				_logger.LogInformation("Gapless playlist handoff: mpv already auto-advanced to preloaded entry (pendingId={PendingId}).", pending?.NewItemId);
				return;
			}
			_logger.LogWarning("Gapless playlist handoff skipped: no trailing playlist entries (pendingId={PendingId}).", pending?.NewItemId);
			_gaplessHandoffInProgress = false;
			if (pending != null)
			{
				await FallbackToManualNavigateAsync(pending, "missing playlist entry");
			}
			return;
		}
		Result result = await Client.PlaylistNextAsync();
		if (result.IsFailed)
		{
			_gaplessHandoffInProgress = false;
			string text = result.Errors.FirstOrDefault()?.Message ?? "unknown error";
			_logger.LogWarning("Gapless playlist handoff failed ({Error}); pendingId={PendingId}.", text, pending?.NewItemId);
			if (pending != null)
			{
				await FallbackToManualNavigateAsync(pending, "playlist-next failed");
			}
		}
	}

	private async Task FallbackToManualNavigateAsync(HostNavigateOptions pending, string reason)
	{
		_logger.LogInformation("Falling back to manual reload for next episode ({Reason}): {MediaPath}", reason, pending.MediaPath);
		_allowEpisodeNavigation = true;
		await ReloadForNavigateAsync(pending);
	}

	internal void ResetStreamRecoveryState()
	{
		_streamAccessErrorDetected = false;
		_streamRefreshAttempts = 0;
		_streamAbortInFlight = false;
		_awaitingStreamRecoveryLoad = false;
		_streamRecoveryPosition = 0.0;
		_pendingRecoveryMediaPath = null;
		_streamPausedAtUtc = null;
		_lastStreamRefreshAttempt = DateTimeOffset.MinValue;
	}

	private void CaptureStreamRecoveryPosition()
	{
		double num = Math.Max(0.0, CurrentPosition);
		if (num > 0.0)
		{
			_streamRecoveryPosition = num;
		}
	}

	private void ClearSeamlessStreamRefreshState()
	{
		_pendingStreamRefreshData = null;
		_streamRefreshFetchInProgress = false;
		_streamRefreshAppended = false;
		_streamRefreshHandoffInProgress = false;
		_isStreamRefreshTransitioning = false;
		_streamRefreshHandoffPosition = 0.0;
		// t209：（原 `_streamRefreshAppendedAtPlaylistPos = -1L;` 一行随字段删除 —— 该字段只写不读，见类头注释）
		_streamRefreshPreservedAudio = null;
		_streamRefreshPreservedSubtitle = null;
		CancelPauseStreamRefreshTimer();
	}

	internal async Task AbortStreamPlaybackAsync(string reason)
	{
		if (Client == null || _isClosingWindow)
		{
			return;
		}
		try
		{
			_logger.LogWarning("Aborting playback ({Reason}) to prevent corrupt-stream resync crash.", reason);
			Result result = await Client.AbortPlaybackAsync();
			if (result.IsFailed)
			{
				_logger.LogWarning("Abort failed during stream abort: {Error}", result.Errors.FirstOrDefault()?.Message ?? "unknown error");
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Exception while aborting stream playback.");
		}
	}

	internal async Task AbortStreamAndRecoverAsync(int statusCode)
	{
		if (_streamAbortInFlight || _isClosingWindow)
		{
			return;
		}
		_streamAbortInFlight = true;
		_awaitingStreamRecoveryLoad = true;
		CaptureStreamRecoveryPosition();
		try
		{
			string reason = ((statusCode > 0) ? $"HTTP {statusCode}" : "transport I/O");
			await AbortStreamPlaybackAsync(reason);
			await TryRecoverStreamUrlAsync();
		}
		finally
		{
			_streamAbortInFlight = false;
		}
	}

	internal async Task HandleStreamTransportErrorAsync(int statusCode)
	{
		_streamAccessErrorDetected = true;
		string text = ((statusCode > 0) ? $"HTTP {statusCode}" : "transport I/O");
		if (statusCode > 0)
		{
			_logger.LogWarning("Stream HTTP {StatusCode} detected; evaluating cache before recovery.", statusCode);
		}
		else
		{
			_logger.LogWarning("Stream transport error detected; evaluating cache before recovery.");
		}
		double streamCacheLeadSeconds = GetStreamCacheLeadSeconds();
		if (ShouldDeferStreamRecoveryForCache(streamCacheLeadSeconds))
		{
			_logger.LogInformation("Deferring stream recovery ({Reason}): {CacheLead:0.1}s cache ahead at {Position}s, continuing from buffer.", text, streamCacheLeadSeconds, CurrentPosition);
			EnsureSeamlessStreamRefreshScheduled();
			if (Client != null && !IsPlaying && LastState != MpvPlayerState.End)
			{
				await Client.ResumeAsync();
			}
		}
		else if (!(await TrySeamlessStreamRefreshHandoffAsync(text)))
		{
			CaptureStreamRecoveryPosition();
			await AbortStreamAndRecoverAsync(statusCode);
		}
	}

	private void EnsureSeamlessStreamRefreshScheduled()
	{
		if (!_streamRefreshAppended && !_streamRefreshFetchInProgress && ReportClient != null && ReportClient.IsEnabled && IsRemoteStreamMedia())
		{
			BeginSeamlessStreamRefreshAsync(Math.Max(0.0, CurrentPosition));
		}
	}

	private static bool ShouldDeferStreamRecoveryForCache(double cacheLead)
	{
		return cacheLead > 15.0;
	}

	internal double GetStreamCacheLeadSeconds()
	{
		if (Client == null)
		{
			return Math.Max(0.0, BufferedPosition - CurrentPosition);
		}
		double num = 0.0;
		if (MpvNative.GetPropertyDouble(Client.Handle, "demuxer-cache-time", MpvFormat.Double, out var data) == MpvError.Success)
		{
			num = data;
		}
		if (num <= 0.0 && MpvNative.GetPropertyDouble(Client.Handle, "demuxer-cache-duration", MpvFormat.Double, out var data2) == MpvError.Success)
		{
			num = Math.Max(0.0, CurrentPosition) + Math.Max(0.0, data2);
		}
		if (num <= 0.0)
		{
			num = BufferedPosition;
		}
		if (Duration > 0.0)
		{
			num = Math.Clamp(num, 0.0, Duration);
		}
		return Math.Max(0.0, num - Math.Max(0.0, CurrentPosition));
	}

	internal bool ShouldAttemptStreamRecovery()
	{
		if (ReportClient != null && ReportClient.IsEnabled && !_streamRefreshInProgress && !_isClosingWindow && _streamRefreshAttempts < 3)
		{
			if (!_streamAccessErrorDetected)
			{
				return IsPrematurePlaybackEnd();
			}
			return true;
		}
		return false;
	}

	internal bool IsPrematurePlaybackEnd()
	{
		if (Duration > 60.0 && CurrentPosition > 0.0)
		{
			return CurrentPosition < Duration * 0.95;
		}
		return false;
	}

	internal async Task<bool> TryRecoverStreamUrlAsync(bool triggeredByEnd = false, bool proactiveRefresh = false)
	{
		if (_streamRefreshInProgress || ReportClient == null || !ReportClient.IsEnabled || _isClosingWindow)
		{
			return false;
		}
		if (!proactiveRefresh && _streamRefreshAttempts >= 3)
		{
			_logger.LogWarning("Stream URL refresh attempt limit reached.");
			return false;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (utcNow - _lastStreamRefreshAttempt < TimeSpan.FromSeconds(10L))
		{
			return false;
		}
		_streamRefreshInProgress = true;
		_awaitingStreamRecoveryLoad = true;
		_lastStreamRefreshAttempt = utcNow;
		if (!proactiveRefresh)
		{
			_streamRefreshAttempts++;
		}
		OverlayTrackOption preservedAudio = AudioTracks.FirstOrDefault((OverlayTrackOption t) => t.IsSelected);
		OverlayTrackOption preservedSubtitle = SubtitleTracks.FirstOrDefault((OverlayTrackOption t) => t.IsSelected);
		try
		{
			CaptureStreamRecoveryPosition();
			double position = Math.Max(0.0, CurrentPosition);
			if (position <= 0.0 && _streamRecoveryPosition > 0.0)
			{
				position = _streamRecoveryPosition;
			}
			_logger.LogInformation("Refreshing playback URL at position {Position}s (attempt {Attempt}, triggeredByEnd={TriggeredByEnd}, proactive={Proactive}).", position, (!proactiveRefresh) ? _streamRefreshAttempts : 0, triggeredByEnd, proactiveRefresh);
			HostNavigateOptions options = await ReportClient.ReportRefreshPlaybackUrlAsync(position);
			if (options == null || string.IsNullOrWhiteSpace(options.MediaPath))
			{
				_logger.LogWarning("Stream URL refresh returned no playback URL.");
				_awaitingStreamRecoveryLoad = false;
				return false;
			}
			double? startPosition = options.StartPosition;
			double? startPosition2 = ((startPosition.HasValue && startPosition.GetValueOrDefault() > 0.0) ? options.StartPosition : new double?(position));
			_pendingRecoveryMediaPath = options.MediaPath;
			_allowEpisodeNavigation = true;
			await AbortStreamPlaybackAsync(proactiveRefresh ? "proactive pause-resume reload" : "pre-recovery reload");
			await ReloadForNavigateAsync(options, startPosition2, preservedAudio, preservedSubtitle);
			return true;
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Stream URL refresh failed.");
			_awaitingStreamRecoveryLoad = false;
			return false;
		}
		finally
		{
			_streamRefreshInProgress = false;
		}
	}

	private bool IsRemoteStreamMedia()
	{
		if (!string.IsNullOrWhiteSpace(_lastMediaPath))
		{
			if (!_lastMediaPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
			{
				return _lastMediaPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private bool ShouldScheduleSeamlessStreamRefresh()
	{
		if (ReportClient == null || !ReportClient.IsEnabled || _isClosingWindow || IsFileLoading || _streamRefreshFetchInProgress || _streamRefreshAppended || _awaitingStreamRecoveryLoad || !IsRemoteStreamMedia() || Duration <= 0.0 || CurrentPosition <= 0.0)
		{
			return false;
		}
		DateTimeOffset? streamPausedAtUtc = _streamPausedAtUtc;
		if (!streamPausedAtUtc.HasValue)
		{
			return false;
		}
		return DateTimeOffset.UtcNow - _streamPausedAtUtc.Value >= ProactiveStreamRefreshPauseThreshold;
	}

	private void SchedulePauseStreamRefreshTimer()
	{
		CancelPauseStreamRefreshTimer();
		if (!IsRemoteStreamMedia() || ReportClient == null || !ReportClient.IsEnabled)
		{
			return;
		}
		_pauseRefreshTimer = _queue.CreateTimer();
		_pauseRefreshTimer.Interval = ProactiveStreamRefreshPauseThreshold;
		_pauseRefreshTimer.IsRepeating = false;
		_pauseRefreshTimer.Tick += delegate
		{
			if (!IsPlaying)
			{
				DateTimeOffset? streamPausedAtUtc = _streamPausedAtUtc;
				if (streamPausedAtUtc.HasValue && !_streamRefreshAppended && !_streamRefreshFetchInProgress)
				{
					_logger.LogInformation("Pause threshold reached while paused; prefetching refreshed playback URL at {Position}s.", CurrentPosition);
					BeginSeamlessStreamRefreshAsync(Math.Max(0.0, CurrentPosition));
				}
			}
		};
		_pauseRefreshTimer.Start();
	}

	private void CancelPauseStreamRefreshTimer()
	{
		if (_pauseRefreshTimer is not null)
		{
			_pauseRefreshTimer.Stop();
			_pauseRefreshTimer = null;
		}
	}

	private void ScheduleSeamlessStreamRefreshOnResume()
	{
		if (!_streamRefreshAppended && !_streamRefreshFetchInProgress && ShouldScheduleSeamlessStreamRefresh())
		{
			double totalSeconds = (DateTimeOffset.UtcNow - _streamPausedAtUtc.Value).TotalSeconds;
			_logger.LogInformation("Scheduling seamless playback URL refresh after {PauseSeconds:0}s pause at {Position}s.", totalSeconds, CurrentPosition);
			BeginSeamlessStreamRefreshAsync(Math.Max(0.0, CurrentPosition));
		}
	}

	private static async Task BeginSeamlessStreamRefreshAsync(double position)
	{
		await Task.CompletedTask;
	}

	internal async Task<bool> TrySeamlessStreamRefreshHandoffAsync(string reason)
	{
		if (!_streamRefreshAppended || _pendingStreamRefreshData == null || Client == null || _streamRefreshHandoffInProgress)
		{
			return false;
		}
		double streamCacheLeadSeconds = GetStreamCacheLeadSeconds();
		if (ShouldDeferStreamRecoveryForCache(streamCacheLeadSeconds))
		{
			_logger.LogDebug("Seamless handoff deferred ({Reason}): {CacheLead:0.1}s cache remaining at {Position}s.", reason, streamCacheLeadSeconds, CurrentPosition);
			return false;
		}
		_streamRefreshHandoffInProgress = true;
		_isStreamRefreshTransitioning = true;
		_streamRefreshHandoffPosition = Math.Max(0.0, CurrentPosition);
		_logger.LogInformation("Seamless stream refresh handoff ({Reason}) at {Position}s.", reason, _streamRefreshHandoffPosition);
		Result result = await Client.PlaylistNextAsync();
		if (result.IsFailed)
		{
			_streamRefreshHandoffInProgress = false;
			_isStreamRefreshTransitioning = false;
			_logger.LogWarning("Seamless stream refresh handoff failed ({Reason}).", result.Errors.FirstOrDefault()?.Message ?? "unknown error");
			return false;
		}
		return true;
	}

	internal void TryPerformStreamRefreshHandoff(string reason)
	{
		if (_streamRefreshAppended && _pendingStreamRefreshData != null && !_streamRefreshHandoffInProgress && Client != null && !IsFileLoading && !_awaitingStreamRecoveryLoad && (IsPlaying || _streamAccessErrorDetected))
		{
			double streamCacheLeadSeconds = GetStreamCacheLeadSeconds();
			bool flag = _lastCacheSpeed <= 0.0 && _streamRefreshAppended;
			if (streamCacheLeadSeconds <= 15.0 || (flag && streamCacheLeadSeconds <= 60.0))
			{
				TrySeamlessStreamRefreshHandoffAsync(reason);
			}
		}
	}

	private async Task FinishStreamRefreshHandoffAsync(HostNavigateOptions options, double targetPosition)
	{
		_ = 3;
		try
		{
			if (!string.IsNullOrWhiteSpace(options.CallbackUrl))
			{
				ReportClient = new PlaybackReportClient(options.CallbackUrl, this.Get<ILogger<PlaybackReportClient>>());
			}
			_lastMediaPath = options.MediaPath;
			Id = _lastMediaPath;
			ApplyPreservedTrackSelection(_streamRefreshPreservedAudio, _streamRefreshPreservedSubtitle);
			if (options.AudioTracks.Count > 0 || options.SubtitleTracks.Count > 0)
			{
				AudioTracks.Clear();
				foreach (HostNavigateTrackOption item in options.AudioTracks.Where((HostNavigateTrackOption t) => !string.IsNullOrWhiteSpace(t.Label)))
				{
					AudioTracks.Add(item.ToTrackOption());
				}
				SubtitleTracks.Clear();
				foreach (HostNavigateTrackOption item2 in options.SubtitleTracks.Where((HostNavigateTrackOption t) => !string.IsNullOrWhiteSpace(t.Label)))
				{
					SubtitleTracks.Add(item2.ToTrackOption());
				}
				ApplyPreservedTrackSelection(_streamRefreshPreservedAudio, _streamRefreshPreservedSubtitle);
				CaptureHostSubtitleTracks();
			}
			_hasAppliedInitialTrackSelection = false;
			await ApplyInitialTrackSelectionAsync();
			_logger.LogInformation("Stream refresh handoff complete; seeking to {Position}s.", targetPosition);
			await Client.SetCurrentPositionAsync(targetPosition);
			await Client.ResumeAsync();
			_streamAccessErrorDetected = false;
			await Client.ClearTrailingPlaylistEntriesAsync();
			ClearSeamlessStreamRefreshState();
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Stream refresh handoff apply failed.");
			ClearSeamlessStreamRefreshState();
		}
		finally
		{
			_isStreamRefreshTransitioning = false;
			_queue.TryEnqueue(CompleteFileLoadedUi);
		}
	}

	private async Task InitializeDecodeAsync()
	{
		PreferDecodeType preferDecodeType = PreferDecodeType.Auto;
		try
		{
			preferDecodeType = _settingsToolkit.ReadLocalSetting("PreferDecode", PreferDecodeType.Auto);
		}
		catch (Exception)
		{
			_settingsToolkit.WriteLocalSetting("PreferDecode", PreferDecodeType.Auto);
		}
		switch (preferDecodeType)
		{
			case PreferDecodeType.Auto:
				try
				{
					await Client.SetVideoOutputAsync(VideoOutputType.GpuNext);
					await Client.SetGpuApiAsync(GpuApiType.D3D11);
					await Client.SetGpuContextAsync(GpuContextType.D3D11);
					await Client.SetHardwareDecodeAsync(HardwareDecodeType.Auto);
				}
				catch (Exception exception)
				{
					_logger.LogWarning(exception, "Failed to initialize gpu-next with D3D11, falling back to gpu with auto backend.");
					await Client.SetVideoOutputAsync(VideoOutputType.Gpu);
					await Client.SetGpuApiAsync(GpuApiType.Auto);
					await Client.SetGpuContextAsync(GpuContextType.Auto);
					await Client.SetHardwareDecodeAsync(HardwareDecodeType.Auto);
				}
				break;
			case PreferDecodeType.D3D11:
				await Client.SetVideoOutputAsync(VideoOutputType.Gpu);
				await Client.SetGpuContextAsync(GpuContextType.D3D11);
				await Client.SetHardwareDecodeAsync(HardwareDecodeType.D3D11va);
				break;
			case PreferDecodeType.NVDEC:
				await Client.SetVideoOutputAsync(VideoOutputType.Gpu);
				await Client.SetGpuContextAsync(GpuContextType.Auto);
				await Client.SetHardwareDecodeAsync(HardwareDecodeType.Nvdec);
				break;
			case PreferDecodeType.Vulkan:
				await Client.SetVideoOutputAsync(VideoOutputType.GpuNext);
				await Client.SetGpuContextAsync(GpuContextType.WindowsVulkan);
				await Client.SetHardwareDecodeAsync(HardwareDecodeType.Auto);
				break;
			case PreferDecodeType.DXVA2:
				await Client.SetVideoOutputAsync(VideoOutputType.Gpu);
				await Client.SetGpuContextAsync(GpuContextType.D3D11);
				await Client.SetHardwareDecodeAsync(HardwareDecodeType.Dxva2);
				break;
		}
	}

	private async Task InitializeCacheAsync()
	{
		if (Client != null)
		{
			await SetOptionStringAsync("cache", "yes");
			await SetOptionStringAsync("demuxer-max-bytes", "500MiB");
			await SetOptionStringAsync("demuxer-max-back-bytes", "150MiB");
			await SetOptionStringAsync("cache-secs", "300");
			await SetOptionStringAsync("stream-buffer-size", "4MiB");
			await SetOptionStringAsync("demuxer-readahead-secs", "300");
			await SetOptionStringAsync("force-seekable", "yes");
			await SetOptionStringAsync("prefetch-playlist", "yes");
			await ApplyStreamReconnectOptionsAsync();
		}
	}

	private async Task ApplyStreamReconnectOptionsAsync()
	{
		if (Client != null)
		{
			await SetOptionStringAsync("network-timeout", "60");
			await SetOptionStringAsync("stream-lavf-o", "reconnect=1,reconnect_streamed=1,reconnect_delay_max=5");
		}
	}

	private async Task SetOptionStringAsync(string name, string value)
	{
		if (Client != null)
		{
			MpvError errorCode = MpvError.Success;
			await Task.Run(() => errorCode = MpvNative.SetOptionString(Client.Handle, name, value));
			if (errorCode != MpvError.Success)
			{
				_logger.LogWarning("Failed to set mpv option {Option}={Value}, error={ErrorCode}", name, value, errorCode);
			}
		}
	}

	internal async Task ApplyVideoFitModeAsync()
	{
		if (Client == null)
		{
			return;
		}
		try
		{
			switch (VideoFitMode)
			{
				case VideoFitMode.Cover:
					await SetOptionStringAsync("panscan", "1");
					await SetOptionStringAsync("video-aspect-override", "no");
					break;
				case VideoFitMode.Stretch:
					await SetOptionStringAsync("panscan", "0");
					await SetOptionStringAsync("video-aspect-override", GetWindowAspectString());
					break;
				default:
					await SetOptionStringAsync("panscan", "0");
					await SetOptionStringAsync("video-aspect-override", "no");
					break;
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to apply video fit mode {VideoFitMode}.", VideoFitMode);
		}
	}

	[RelayCommand]
	private void SelectAnimeMode(string mode)
	{
		CurrentAnimeMode = mode;
	}

	[RelayCommand]
	private void SelectSharpenMode(string mode)
	{
		CurrentSharpenMode = mode;
	}

	private async Task ApplyEnhancementDefaultsAsync()
	{
		if (Client != null)
		{
			if (DefaultRtxVsr)
			{
				await SetOptionStringAsync("d3d11-rtx-video-sr", "yes");
			}
			if (DefaultRtxVideoHdr)
			{
				await SetOptionStringAsync("d3d11-rtx-truehdr", "yes");
			}
			_suppressShaderReapply = true;
			CurrentAnimeMode = DefaultAnimeMode;
			CurrentSharpenMode = DefaultSharpenMode;
			_suppressShaderReapply = false;
			await ApplyShaderOverrideAsync(DefaultAnimeMode, DefaultSharpenMode);
		}
	}

	private async Task ApplyShaderOverrideAsync(string animeMode, string sharpenMode)
	{
		if (Client == null)
		{
			return;
		}
		string path = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
		List<string> list = new List<string>();
		if (_animeModeShaders.TryGetValue(animeMode, out string[] value))
		{
			string[] array = value;
			foreach (string text in array)
			{
				if (File.Exists(Path.Combine(path, "shaders", text)))
				{
					list.Add(Path.Combine("shaders", text));
					continue;
				}
				_logger.LogWarning("Anime4K shader not found: {File}", text);
			}
		}
		if (_sharpenModeShaders.TryGetValue(sharpenMode, out string value2))
		{
			if (File.Exists(Path.Combine(path, "shaders", value2)))
			{
				list.Add(Path.Combine("shaders", value2));
			}
			else
			{
				_logger.LogWarning("Sharpen shader not found: {File}", value2);
			}
		}
		await SetOptionStringAsync("glsl-shaders", (list.Count > 0) ? string.Join(';', list) : string.Empty);
	}

	private string GetWindowAspectString()
	{
		if (Window == null)
		{
			return "no";
		}
		SizeInt32 size = Window.GetWindow().Size;
		if (size.Width <= 0 || size.Height <= 0)
		{
			return "no";
		}
		return (size.Width / (double)size.Height).ToString("0.######", CultureInfo.InvariantCulture);
	}

	private RectInt32 MoveAndResize()
	{
		DisplayArea displayArea = DisplayArea.GetFromPoint(GetSavedWindowPosition(), DisplayAreaFallback.Primary) ?? DisplayArea.Primary;
		RectInt32 renderRect = GetRenderRect(displayArea);
		SetWindowPos(Window.Handle, IntPtr.Zero, renderRect.X, renderRect.Y, renderRect.Width, renderRect.Height, 20u);
		return renderRect;
	}

	private void InitializeWindow()
	{
		_isResizingToVideo = true;
		_logger.LogDebug("PlayerViewModel.InitializeWindow begin");
		_logger.LogDebug("PlayerViewModel.InitializeWindow before new MpvPlayerWindow");
		Window = new MpvPlayerWindow(Client, _queue, MaxVolume);
		_logger.LogDebug("PlayerViewModel.InitializeWindow after new MpvPlayerWindow");
		InstallMaximizeSubclass();
		Window.UINotify += OnUINotify;
		AppWindow window = Window.GetWindow();
		_logger.LogDebug("PlayerViewModel.InitializeWindow after GetWindow");
		window.TitleBar.ExtendsContentIntoTitleBar = true;
		_logger.LogDebug("PlayerViewModel.InitializeWindow after ExtendsContentIntoTitleBar");
		Window.SuppressActivationBorder();
		_logger.LogDebug("PlayerViewModel.InitializeWindow after SuppressActivationBorder");
		if (window.Presenter is OverlappedPresenter overlappedPresenter)
		{
			_logger.LogDebug("PlayerViewModel.InitializeWindow before presenter config");
			overlappedPresenter.PreferredMinimumWidth = 1;
			overlappedPresenter.PreferredMinimumHeight = 1;
		}
		_logger.LogDebug("PlayerViewModel.InitializeWindow before MoveAndResize");
		_initWindowTargetRect = MoveAndResize();
		_logger.LogDebug("PlayerViewModel.InitializeWindow after MoveAndResize");
		bool flag = _settingsToolkit.ReadLocalSetting("IsPlayerWindowMaximized", defaultValue: false);
		_logger.LogDebug($"PlayerViewModel.InitializeWindow read IsPlayerWindowMaximized={flag}");
		if (flag && window.Presenter is OverlappedPresenter overlappedPresenter2)
		{
			_logger.LogDebug("PlayerViewModel.InitializeWindow before maximize");
			overlappedPresenter2.Maximize();
			_initWindowTargetRect = null;
			_logger.LogDebug("PlayerViewModel.InitializeWindow after maximize");
		}
		window.Closing += OnWindowClosing;
		window.Changed += OnAppWindowChanged;
		_logger.LogDebug("PlayerViewModel.InitializeWindow after window events");
		IsControlVisible = true;
		_logger.LogDebug("PlayerViewModel.InitializeWindow before SetUIElement");
		_logger.LogInformation("[StartupDiag] SetUIElement(PlayerOverlay) begin");
		Window.SetUIElement(new PlayerOverlay(this));
		_logger.LogInformation("[StartupDiag] SetUIElement(PlayerOverlay) done");
		_logger.LogDebug("PlayerViewModel.InitializeWindow after SetUIElement");
		UpdateWindowState();
		DispatcherQueueTimer dispatcherQueueTimer = _queue.CreateTimer();
		dispatcherQueueTimer.Interval = TimeSpan.FromMilliseconds(300L, 0L);
		dispatcherQueueTimer.IsRepeating = false;
		dispatcherQueueTimer.Tick += delegate (DispatcherQueueTimer t, object _)
		{
			t.Stop();
			_initWindowTargetRect = null;
			_isResizingToVideo = false;
		};
		dispatcherQueueTimer.Start();
	}

	private async Task LoadMediaAsync()
	{
		if (Client != null && Window != null)
		{
			_logger.LogInformation("[StartupDiag] LoadMediaAsync preparing wid={WindowHandle} media={MediaPath} subtitleTracks={SubtitleTrackCount}", Window.Handle, DescribeMediaPathForLog(_lastMediaPath), SubtitleTracks.Count);
			CaptureHostSubtitleTracks();
			_lastPlayOptions.WindowHandle = Window.Handle;
			_lastPlayOptions.InitialVolume = _settingsToolkit.ReadLocalSetting("PlayerVolume", 50.0);
			_lastPlayOptions.InitialSpeed = _settingsToolkit.ReadLocalSetting("PlayerSpeed", 1.0);
			_stopReported = false;
			_hasObservedPlayback = false;
			_hasStartedPlaying = false;
			_skippedHeadSegments.Clear();
			_skippedTailSegments.Clear();
			_lastProgressReportAt = DateTimeOffset.MinValue;
			_lastReportedPosition = -1.0;
			_loggedProgressReporting = false;
			OverlayTrackOption overlayTrackOption = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
			string text = ResolveSelectedSubtitleUrl();
			string text2 = ResolveSelectedSubtitleId();
			string extraSubtitleUrl = _lastPlayOptions.ExtraSubtitleUrl;
			string initialSubtitleId = _lastPlayOptions.InitialSubtitleId;
			bool flag = !string.IsNullOrWhiteSpace(extraSubtitleUrl) && overlayTrackOption != null && !overlayTrackOption.IsSpecial && string.IsNullOrWhiteSpace(text) && string.Equals(overlayTrackOption.Id, initialSubtitleId, StringComparison.Ordinal);
			_pendingSubtitleUrl = ((overlayTrackOption == null || !overlayTrackOption.IsExternal) ? (flag ? extraSubtitleUrl : null) : ((!string.IsNullOrWhiteSpace(text)) ? text : extraSubtitleUrl));
			_pendingSubtitleId = ((overlayTrackOption != null && overlayTrackOption.IsSpecial) ? "no" : ((!string.IsNullOrWhiteSpace(text2)) ? text2 : initialSubtitleId));
			_lastPlayOptions.ExtraSubtitleUrl = null;
			_lastPlayOptions.InitialSubtitleId = null;
			_logger.LogDebug($"PlayerViewModel.LoadMediaAsync subtitle option id={_pendingSubtitleId ?? string.Empty} url={_pendingSubtitleUrl ?? string.Empty} tracks={SubtitleTracks.Count}");
			Window.GetWindow().Title = MediaTitle;
			_logger.LogInformation("[StartupDiag] PlayAsync invoking pendingSubtitleId={PendingSubtitleId}", _pendingSubtitleId ?? string.Empty);
			await Client.PlayAsync(_lastMediaPath, _lastPlayOptions);
			_logger.LogInformation("[StartupDiag] PlayAsync returned");
			EnsureWindowShown();
		}
	}

	private void ResizeWindowToVideo()
	{
		if (Client == null || Window == null)
		{
			return;
		}
		try
		{
			int? num = TryGetMpvDimension("dwidth") ?? TryGetMpvDimension("width");
			int? num2 = TryGetMpvDimension("dheight") ?? TryGetMpvDimension("height");
			if (num.HasValue && num2.HasValue && !(num <= 0) && !(num2 <= 0))
			{
				AppWindow window = Window.GetWindow();
				double num3 = Windows.Win32.PInvoke.GetDpiForWindow(new Windows.Win32.Foundation.HWND(Window.Handle)) / 96.0;
				int num4 = Math.Max(1, Convert.ToInt32(num.Value * num3));
				int num5 = Math.Max(1, Convert.ToInt32(num2.Value * num3));
				RectInt32 workArea = (DisplayArea.GetFromWindowId(window.Id, DisplayAreaFallback.Primary) ?? DisplayArea.Primary).WorkArea;
				if (num4 > workArea.Width)
				{
					num5 = Convert.ToInt32(num5 * (workArea.Width / (double)num4));
					num4 = workArea.Width;
				}
				if (num5 > workArea.Height)
				{
					num4 = Convert.ToInt32(num4 * (workArea.Height / (double)num5));
					num5 = workArea.Height;
				}
				num4 = Math.Max(1, num4);
				num5 = Math.Max(1, num5);
				int x = workArea.X + Math.Max(0, (workArea.Width - num4) / 2);
				int y = workArea.Y + Math.Max(0, (workArea.Height - num5) / 2);
				_initWindowTargetRect = null;
				_isResizingToVideo = true;
				window.MoveAndResize(new RectInt32(x, y, num4, num5));
				_queue.TryEnqueue(DispatcherQueuePriority.Low, delegate
				{
					_isResizingToVideo = false;
				});
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to resize player window to video size.");
		}
	}

	private int? TryGetMpvDimension(string propertyName)
	{
		if (Client == null)
		{
			return null;
		}
		if (MpvNative.GetProperty(Client.Handle, propertyName, MpvFormat.Int64, out var data) != MpvError.Success)
		{
			return null;
		}
		long integerValue = data.IntegerValue;
		if (integerValue <= 0 || integerValue > int.MaxValue)
		{
			return null;
		}
		return (int)integerValue;
	}

	private void ApplyMediaBadges(IReadOnlyList<string> badges)
	{
		if (_suppressMediaBadgeRefresh)
		{
			return;
		}
		_suppressMediaBadgeRefresh = true;
		try
		{
			MediaBadges.Clear();
			foreach (string badge in badges)
			{
				MediaBadges.Add(badge);
			}
		}
		finally
		{
			_suppressMediaBadgeRefresh = false;
		}
	}

	private void EnsureWindowShown()
	{
		if (!_isWindowShown && Window != null)
		{
			_logger.LogDebug("PlayerViewModel.EnsureWindowShown");
			Window.Show();
			_isWindowShown = true;
		}
	}

	private async Task ApplyInitialTrackSelectionAsync()
	{
		if (!_hasAppliedInitialTrackSelection && Client != null)
		{
			_hasAppliedInitialTrackSelection = true;
			OverlayTrackOption overlayTrackOption = AudioTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected && !string.IsNullOrWhiteSpace(p.Id));
			if (overlayTrackOption != null)
			{
				await ApplyInternalAudioSelectionAsync(overlayTrackOption);
			}
			OverlayTrackOption overlayTrackOption2 = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
			if (!string.IsNullOrWhiteSpace(_pendingSubtitleUrl))
			{
				await ApplyExternalSubtitleUrlAsync(_pendingSubtitleUrl);
			}
			else if (overlayTrackOption2 != null && !overlayTrackOption2.IsSpecial)
			{
				await ApplyInternalSubtitleSelectionAsync(overlayTrackOption2);
			}
			else if (overlayTrackOption2 != null)
			{
				await ApplyTrackSelectionAsync("sid", overlayTrackOption2);
			}
			else if (!string.IsNullOrWhiteSpace(_pendingSubtitleId))
			{
				await ApplySubtitleIdAsync(_pendingSubtitleId);
			}
		}
	}

	private async Task<bool> ApplyTrackSelectionAsync(string propertyName, OverlayTrackOption track)
	{
		if (Client == null)
		{
			return false;
		}
		if (propertyName == "sid" && !string.IsNullOrWhiteSpace(track.Url))
		{
			return await ApplyExternalSubtitleSelectionAsync(track);
		}
		string value = (track.IsSpecial ? "no" : ((!string.IsNullOrWhiteSpace(track.ResolvedId)) ? track.ResolvedId : track.Id));
		if (string.IsNullOrWhiteSpace(value))
		{
			_logger.LogDebug("PlayerViewModel.ApplyTrackSelectionAsync skipped empty value property=" + propertyName + " label=" + track.Label);
			return false;
		}
		_logger.LogDebug($"PlayerViewModel.ApplyTrackSelectionAsync set {propertyName}={value} label={track.Label}");
		MpvError mpvError = await Task.Run(() => MpvNative.SetPropertyString(Client.Handle, propertyName, value));
		_logger.LogDebug($"PlayerViewModel.ApplyTrackSelectionAsync set result={mpvError}");
		if (mpvError != MpvError.Success)
		{
			_logger.LogWarning("Failed to apply mpv property {PropertyName}={Value}. Error={ErrorCode}", propertyName, value, mpvError);
			return false;
		}
		if (propertyName == "sid" && !track.IsSpecial)
		{
			await TryEnableSubtitleVisibilityAsync("ApplyTrackSelectionAsync").ConfigureAwait(false);
		}
		return true;
	}

	/// <summary>
	/// 让字幕可见（<c>sub-visibility=yes</c>）。这是**附加努力**：调用方的"成功"指的是主操作
	/// （切 <c>sid</c> / <c>sub-add</c>）已成功，把这里的失败改判成主操作失败会把"sid 确实切成功"
	/// 报成 500（越界结论）。**但失败仍必须留痕**：写失败时用户看到的是"选了字幕却没有字幕"，
	/// 没有这行日志就完全无从定位（默认日志级别下 <c>Debug</c> 等于不存在）。
	/// </summary>
	private async Task TryEnableSubtitleVisibilityAsync(string context)
	{
		MpvError error = await Task.Run(() => MpvNative.SetPropertyString(Client.Handle, "sub-visibility", "yes"));
		if (error == MpvError.Success)
		{
			_logger.LogDebug("[SUB-VISIBILITY] enabled context={Context}", context);
			return;
		}
		_logger.LogWarning("[SUB-VISIBILITY] write-failed context={Context} Error={ErrorCode}（主操作已成功，仅此附加步骤失败）", context, error);
	}

	private async Task<bool> ApplyInternalAudioSelectionAsync(OverlayTrackOption track)
	{
		if (Client == null)
		{
			return false;
		}
		string text = await Task.Run(() => ResolveInternalAudioTrackId(track));
		if (!string.IsNullOrWhiteSpace(text))
		{
			track.ResolvedId = text;
			_logger.LogDebug($"PlayerViewModel.ApplyInternalAudioSelectionAsync resolved label={track.Label} requestedId={track.Id} resolvedId={text}");
			return await ApplyTrackSelectionAsync("aid", track);
		}
		if (!string.IsNullOrWhiteSpace(track.Id))
		{
			track.ResolvedId = track.Id;
			_logger.LogDebug("PlayerViewModel.ApplyInternalAudioSelectionAsync fallback label=" + track.Label + " id=" + track.Id);
			return await ApplyTrackSelectionAsync("aid", track);
		}
		_logger.LogWarning("[SUB-TRACK] skipped reason=no-usable-track-id property=aid label={Label} requestedId={RequestedId}（既解析不出 mpv 音轨 id、宿主也没给 id ⇒ 什么都不会发生，必须可见）", track.Label, track.Id);
		return false;
	}

	/// <summary>
	/// <c>POST /control</c> 的落点 —— **类型化的 8 条播放中控制命令**（schema：
	/// <c>kind</c> + <c>trackId</c>/<c>seconds</c>/<c>visible</c>/<c>url</c>/<c>rate</c>/<c>level</c>）。
	/// <b>复用既有写点</b>：与浮层/外挂字幕走同一条 mpv 写点（<c>MpvNative.SetPropertyString</c> / <c>Client.*Async</c>），
	/// **不另起一套换轨路径**。<br/>
	/// 返回值：<c>null</c> = 成功；否则为失败原因（由 PlayerUpdateServer 记日志并按语义回 400/500）——
	/// 前缀约定：<c>bad-*</c> / <c>unknown-*</c> / <c>track-not-found</c> / <c>mpv-not-applied</c> /
	/// <c>danmaku-unavailable</c> = **载荷面（400）**；其余（<c>add-subtitle-failed</c> 等）= 内核/mpv 层（500）。
	/// </summary>
	public async Task<string?> ApplyControlCommandAsync(KernelControlCommand command)
	{
		if (command == null)
		{
			return "bad-payload:null-command";
		}
		string kind = command.Kind;
		if (string.IsNullOrWhiteSpace(kind))
		{
			return "bad-payload:missing-kind";
		}
		if (Client == null)
		{
			return "no-client";
		}
		// **每条命令的 before 值**：只有把改前/改后都按 mpv 真实属性值打出来，"生效"才是可对照的。
		//   before 走既有的 ReadMpvState()（与进度回推同一条读 mpv 路径，不新增读点），after 由方法末尾的 [K3-PUSH] 给出。
		(double beforeSpeed, double beforeSubDelay, int? beforeVolume, long beforeAid, long beforeSid, bool? beforeSubVisible, bool? _) = ReadMpvState();
		_logger.LogInformation("[CONTROL-BEFORE] kind={Kind} ｜ mpv: speed={Speed} subDelay={SubDelay} volume={Volume} aid={Aid} sid={Sid}", kind, beforeSpeed, beforeSubDelay, beforeVolume, beforeAid, beforeSid);
		string normalized;
		switch (kind)
		{
			case KernelControlCommand.KindSetSpeed:
				{
					if (!command.Rate.HasValue)
					{
						return "bad-payload:SetSpeed:rate";
					}
					double rate = command.Rate.Value;
					if (!(rate > 0.0) || double.IsNaN(rate) || double.IsInfinity(rate))
					{
						return "bad-value:SetSpeed:rate=" + FormatControlNumber(rate);
					}
					await Client.SetSpeedAsync(rate).ConfigureAwait(false);
					normalized = FormatControlNumber(rate);
					break;
				}
			case KernelControlCommand.KindSetVolume:
				{
					if (!command.Level.HasValue)
					{
						return "bad-payload:SetVolume:level";
					}
					int level = command.Level.Value;
					if (level < 0 || level > MaxVolume)
					{
						return "bad-value:SetVolume:level=" + level.ToString(CultureInfo.InvariantCulture) + ":range=0.." + MaxVolume.ToString(CultureInfo.InvariantCulture);
					}
					await Client.SetVolumeAsync(level).ConfigureAwait(false);
					normalized = level.ToString(CultureInfo.InvariantCulture);
					break;
				}
			case KernelControlCommand.KindSeekAbsolute:
				{
					if (!command.Seconds.HasValue)
					{
						return "bad-payload:SeekAbsolute:seconds";
					}
					double seconds = command.Seconds.Value;
					if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
					{
						return "bad-value:SeekAbsolute:seconds=" + FormatControlNumber(seconds);
					}
					await Client.SetCurrentPositionAsync(seconds).ConfigureAwait(false);
					normalized = FormatControlNumber(seconds);
					break;
				}
			case KernelControlCommand.KindSetSubtitleDelay:
				{
					if (!command.Seconds.HasValue)
					{
						return "bad-payload:SetSubtitleDelay:seconds";
					}
					double seconds = command.Seconds.Value;
					if (double.IsNaN(seconds) || double.IsInfinity(seconds))
					{
						return "bad-value:SetSubtitleDelay:seconds=" + FormatControlNumber(seconds);
					}
					await Client.SetSubtitleDelayAsync(seconds).ConfigureAwait(false);
					normalized = FormatControlNumber(seconds);
					break;
				}
			case KernelControlCommand.KindAddExternalSubtitle:
				{
					if (string.IsNullOrWhiteSpace(command.Url))
					{
						return "bad-payload:AddExternalSubtitle:url";
					}
					// 既有写点：外挂字幕（同 PlayerViewModel 里那条 sub-add 路径的公开封装）。
					// 这条路径**不需要**回读 track-list：`Client.AddSubtitleAsync` 会带出 mpv 的真实结果
					// （不存在的文件 / 非字幕文件 ⇒ IsSuccess=false ⇒ 500 `add-subtitle-failed`）。
					// 对照 ApplyExternalSubtitleSelectionAsync：它直接下发 `sub-add`（成功只代表"已排队"），所以那里必须回读。
					if (!(await Client.AddSubtitleAsync(command.Url).ConfigureAwait(false)).IsSuccess)
					{
						return "add-subtitle-failed:" + command.Url;
					}
					normalized = command.Url;
					break;
				}
			case KernelControlCommand.KindSetAudioTrack:
				{
					if (!TryResolveTrackId(command.TrackId, kind, out string trackId, out long verifyId, out string trackError))
					{
						return trackError;
					}
					if (verifyId > 0)
					{
						// F4（t241）：**存在性判定前移到写之前**。旧顺序 = 先 Apply 再校验 ⇒ 非法 id 会让 400 与
						//   "mpv 状态已变"同时成立（实测 aid 1 → −1）。顺序改了，判定口径与读次数一字未改。
						string existsErrorAid = EnsureTrackExistsForControl("aid", verifyId);
						if (existsErrorAid != null)
						{
							return existsErrorAid;
						}
					}
					if (!await ApplyTrackSelectionAsync("aid", new OverlayTrackOption { Id = trackId, Label = trackId }).ConfigureAwait(false))
					{
						return "bad-track:aid:" + trackId + ":apply-failed";
					}
					// 数字 id 的判定**顺序 = ① 存在性（写之前，见上）② 回读值比对（这里）**。
					//   为什么顺序关键：mpv 在**播放已结束/空闲**状态下会把 `aid`/`sid` **回显**成请求里的任意数字
					//   （实测：播放结束后请求 `aid=999`，回读就是 999）⇒ 只比回读值会给出**假 204**；合法 id 必在 track-list 里 ⇒ 存在性优先不会误伤。
					if (verifyId > 0)
					{
						string verifyError = await VerifyTrackAsync("aid", verifyId, beforeAid).ConfigureAwait(false);
						if (verifyError != null)
						{
							return verifyError;
						}
					}
					else
					{
						// **没做校验**也要留痕（否则"204 通过"会被读成"已验证生效"）。
						_logger.LogInformation("[CONTROL-VERIFY] skipped reason=non-numeric-id value={Value} property=aid", trackId);
					}
					normalized = trackId;
					break;
				}
			case KernelControlCommand.KindSetSubtitleTrack:
				{
					if (!TryResolveTrackId(command.TrackId, kind, out string trackId, out long verifyId, out string trackError))
					{
						return trackError;
					}
					if (verifyId > 0)
					{
						// F4（t241）：与 SetAudioTrack 同口径 —— 存在性判定在写之前（旧顺序的实测后果：sid 2 → −1）。
						string existsErrorSid = EnsureTrackExistsForControl("sid", verifyId);
						if (existsErrorSid != null)
						{
							return existsErrorSid;
						}
					}
					if (!await ApplyTrackSelectionAsync("sid", new OverlayTrackOption { Id = trackId, Label = trackId }).ConfigureAwait(false))
					{
						return "bad-track:sid:" + trackId + ":apply-failed";
					}
					if (verifyId > 0)
					{
						string verifyError = await VerifyTrackAsync("sid", verifyId, beforeSid).ConfigureAwait(false);
						if (verifyError != null)
						{
							return verifyError;
						}
					}
					else
					{
						_logger.LogInformation("[CONTROL-VERIFY] skipped reason=non-numeric-id value={Value} property=sid", trackId);
					}
					normalized = trackId;
					break;
				}
			case KernelControlCommand.KindSetSubtitleVisibility:
				{
					// 字幕开关写 **`sub-visibility` no/yes**（可逆、**不丢已选 sid**），
					//   不再用"把 sid 切到 auto/no"的做法 —— 后者重开只能落到 auto，会把用户已选的字幕轨丢掉。
					//   为什么这里**可以**回读：`sub-visibility` 是 mpv 的独立 flag，**与片源有没有字幕轨无关**
					//   ⇒ 这里可以回读：`sub-visibility` 与片源有没有字幕轨无关（这正是 sid 那条不能回读的原因）。
					if (!command.Visible.HasValue)
					{
						return "bad-payload:SetSubtitleVisibility:visible";
					}
					bool wantVisible = command.Visible.Value;
					long sidBefore = beforeSid;
					if (!await ApplySubtitleVisibilityAsync(wantVisible).ConfigureAwait(false))
					{
						return "mpv-not-applied:sub-visibility:" + (wantVisible ? "yes" : "no") + "/write-failed";
					}
					bool? actual = beforeSubVisible;
					bool applied = false;
					for (int attempt = 0; attempt < 3; attempt++)
					{
						await Task.Delay(100).ConfigureAwait(false);
						(double _, double _, int? _, long _, long _, bool? sv, bool? _) = ReadMpvState();
						actual = sv;
						if (sv.HasValue && sv.Value == wantVisible)
						{
							applied = true;
							break;
						}
					}
					long sidAfter = ReadMpvState().Sid;
					// 核心读数：**改前 sid / 改后 sid 必须不变** —— 显式打印，不靠推理（端到端验证也用它当判据）。
					_logger.LogInformation("[CONTROL-VERIFY] kind=SetSubtitleVisibility beforeSubVisibility={Before} wanted={Wanted} afterSubVisibility={After} applied={Applied} ｜ sid: before={SidBefore} after={SidAfter} sidUnchanged={SidSame}", ((object)beforeSubVisible ?? "(unknown)"), wantVisible, ((object)actual ?? "(unknown)"), applied, sidBefore, sidAfter, (sidBefore == sidAfter));
					if (!applied)
					{
						// 修法 B（t252）：本命令**没有"存在性"可判**（`sub-visibility` 与片源有没有字幕轨无关）⇒
						//   "先判后写"对它不适用；唯一能同时保证「报错」与「状态未变」的手段 = **写后失败即回滚到 before**。
						//   ⚠️ 回滚**本身**也可能失败 ⇒ 那个状态必须**被命名**（既不落到无名的 500，也不静默当作成功）。
						string rollbackError = await RollbackSubtitleVisibilityAsync(beforeSubVisible, actual).ConfigureAwait(false);
						if (rollbackError != null)
						{
							_logger.LogWarning("[CONTROL-VERIFY] kind=SetSubtitleVisibility rollback-failed wanted={Wanted} actual={Actual} error={Error}（未定义态：写已发生且回滚未确认）", wantVisible, ((object)actual ?? "(unknown)"), rollbackError);
							return rollbackError;
						}
						_logger.LogInformation("[CONTROL-VERIFY] kind=SetSubtitleVisibility rollback-ok before={Before} wanted={Wanted} actual={Actual}（状态已回滚到 before ⇒ 400 与「状态逐位未变」同时成立）", ((object)beforeSubVisible ?? "(unknown)"), wantVisible, ((object)actual ?? "(unknown)"));
						return "mpv-not-applied:sub-visibility:" + (wantVisible ? "yes" : "no") + "/" + (actual.HasValue ? (actual.Value ? "yes" : "no") : "unknown");
					}
					if (sidBefore != sidAfter)
					{
						_logger.LogWarning("[CONTROL-VERIFY] sid 被可见性开关改动了（违反裁定 (b) 的『不丢已选 sid』）：before={Before} after={After}", sidBefore, sidAfter);
					}
					normalized = (wantVisible ? "true" : "false");
					break;
				}
			case KernelControlCommand.KindSetDanmakuEnabled:
				{
					// **第 9 条命令 = 弹幕开关**。写的是既有属性 `IsDanmakuEnabled`
					//   （浮层菜单 `PlayerOverlay.cs:2594-2602` 写的同一个属性 ⇒ 同一条链，**不新开写点**）。
					if (!command.Enabled.HasValue)
					{
						return "bad-payload:SetDanmakuEnabled:enabled";
					}
					// 🔴 消灭一处静默：`--danmaku-apis` 未配置时，浮层的开启分支被门挡住
					//   （`PlayerOverlay.cs:1355` 判 `CurrentDanmakuApiBase` 非空）⇒ 旧行为是 **204 但什么都不发生**。
					//   判据 = `DanmakuApis.Count == 0` **或** `CurrentDanmakuApiBase` 空 —— **不用**"发了请求没回"来判。
					int apiCount = ((DanmakuApis != null) ? DanmakuApis.Count : 0);
					string? apiBase = CurrentDanmakuApiBase;
					if (apiCount == 0 || string.IsNullOrWhiteSpace(apiBase))
					{
						_logger.LogWarning("[CONTROL-VERIFY] danmaku-unavailable kind=SetDanmakuEnabled danmakuApis={Count} currentDanmakuApiBase={Base}（未配置弹幕源 ⇒ 命令未被接受，不得静默 204）", apiCount, apiBase ?? "(null)");
						return "danmaku-unavailable";
					}
					bool wantEnabled = command.Enabled.Value;
					bool danmakuBefore = IsDanmakuEnabled;
					// 🔴 **为什么必须 marshal 回 UI 线程**：控制命令落在**线程池线程**（实测：本行线程 id 从未等于 UI 线程 id）
					//   （日志实证：`[THREAD] ui-managed-thread-id=2` vs `[CONTROL-APPLY] … managedThreadId=9/11`）
					//   ⇒ 直接改 `IsDanmakuEnabled` 会让 `PlayerOverlay.HandleDanmakuPropertyChanged`
					//   （`:1350-1374` 动 `DanmakuLayer.Visibility` / `_danmaku.Pause()`）在**非 UI 线程**上跑 = 跨线程访问 UI。
					//   ⇒ 所以这里**必须 marshal 回 UI 线程**（用既有的 `_queue`，不新开 dispatcher）。
					if (_queue != null && !_queue.HasThreadAccess)
					{
						_queue.TryEnqueue(delegate
						{
							IsDanmakuEnabled = wantEnabled;
						});
					}
					else
					{
						IsDanmakuEnabled = wantEnabled;
					}
					bool danmakuAfter = danmakuBefore;
					for (int attempt = 0; attempt < 3; attempt++)
					{
						await Task.Delay(100).ConfigureAwait(false);
						danmakuAfter = IsDanmakuEnabled;
						if (danmakuAfter == wantEnabled)
						{
							break;
						}
					}
					_logger.LogInformation("[CONTROL-VERIFY] kind=SetDanmakuEnabled before={Before} wanted={Wanted} after={After} applied={Applied} danmakuApis={Count} marshalUi={Marshal} managedThreadId={ThreadId}", danmakuBefore, wantEnabled, danmakuAfter, (danmakuAfter == wantEnabled), apiCount, (_queue != null && !_queue.HasThreadAccess), Environment.CurrentManagedThreadId);
					if (danmakuAfter != wantEnabled)
					{
						// K8 静默族（t252）：**回读不一致这条失败路径此前只留一行 `applied=false`，响应仍是 204**
						//   ⇒ 对调用方"看起来成功"。本卡给它一个**独立、可 grep、可与成功区分**的命名标记；
						//   响应码**有意保持 204**，理由逐条（也是本卡对"保留静默"显式理由那一格的答案）：
						//   ① nonGoal：9 条既有命令的响应码与 after 回读语义不许动（t46 已把它记成 204）；
						//   ② 语义面不同源：`IsDanmakuEnabled` 是**本进程的 UI 侧开关**（`PlayerOverlay` 消费），
						//      不是 mpv 状态 ⇒ **不存在** F4 那种"外部可观察状态已被改，却报 400"的不一致；
						//   ③ 判别改由**命名标记**承担（本行 + 上面的 `applied=` 行），且该标记**可被反例推翻**：
						//      见证据件 t252 的判据 D2（反控 = 改前 dll 上该标记命中 0）。
						_logger.LogWarning("[CONTROL-VERIFY] danmaku-not-applied kind=SetDanmakuEnabled before={Before} wanted={Wanted} actual={Actual} reason=readback-mismatch danmakuApis={Count} marshalUi={Marshal}（失败路径**命名**：响应码仍是 204，见本行上方三条理由）", danmakuBefore, wantEnabled, danmakuAfter, apiCount, (_queue != null && !_queue.HasThreadAccess));
					}
					if (danmakuBefore == wantEnabled)
					{
						// 反控要求：**重复设同值必须可区分** ⇒ 打 noop 行（这个 204 是空操作，不是"这次改了它"）。
						_logger.LogInformation("[CONTROL-VERIFY] noop kind=SetDanmakuEnabled before={Before} wanted={Wanted}（本来就是这个值：204 不代表这次改了它）", danmakuBefore, wantEnabled);
					}
					normalized = (wantEnabled ? "true" : "false");
					break;
				}
			default:
				return "unknown-kind:" + kind;
		}
		_logger.LogInformation("[CONTROL-APPLY] kind={Kind} value={Value} managedThreadId={ThreadId} result=ok via existing-write-point", kind, normalized, Environment.CurrentManagedThreadId);
		// **单一真相源 = mpv** —— 回推前从 mpv 读真实状态（不是读 VM 缓存），并**立即**推一次进度
		//           （force: true 绕过 5 s / 5 m 节流）。浮层改轨与外壳改轨走同一条写点 ⇒ 两边看到的是同一个 mpv 状态。
		(double mpvSpeed, double mpvSubDelay, int? mpvVolume, long mpvAid, long mpvSid, bool? mpvSubVisible, bool? mpvMuted) = ReadMpvState();
		_logger.LogInformation("[K3-PUSH] after kind={Kind} value={Value} ｜ mpv: speed={Speed} subDelay={SubDelay} volume={Volume} aid={Aid} sid={Sid} subVisible={SubVisible}", kind, normalized, mpvSpeed, mpvSubDelay, mpvVolume, mpvAid, mpvSid, ((object)mpvSubVisible ?? "(unknown)"));
		TryScheduleProgressReport(force: true);
		return null;
	}

	/// <summary>
	/// 把类型化 <c>trackId</c> 解析成 mpv 认的字符串。接受：**数字**（<c>-1</c>/<c>0</c> = 关闭 ⇒ <c>"no"</c>）、
	/// <c>"no"</c>/<c>"none"</c>、<c>"auto"</c>（外壳既有枚举形态，保持兼容）。
	/// <paramref name="verifyId"/> &gt; 0 表示"这是数字 id，可以回读校验"；<c>no</c>/<c>auto</c> 为 -1（不回读）。
	/// </summary>
	private static bool TryResolveTrackId(System.Text.Json.JsonElement? element, string kind, out string trackId, out long verifyId, out string error)
	{
		trackId = null;
		verifyId = -1L;
		error = null;
		if (!element.HasValue)
		{
			error = "bad-payload:" + kind + ":trackId";
			return false;
		}
		System.Text.Json.JsonElement value = element.Value;
		if (value.ValueKind == System.Text.Json.JsonValueKind.Number)
		{
			if (!value.TryGetInt64(out long numericId))
			{
				error = "bad-value:" + kind + ":trackId-not-integer";
				return false;
			}
			if (numericId <= 0)
			{
				trackId = "no";
				verifyId = -1L;
				return true;
			}
			trackId = numericId.ToString(CultureInfo.InvariantCulture);
			verifyId = numericId;
			return true;
		}
		if (value.ValueKind == System.Text.Json.JsonValueKind.String)
		{
			string text = (value.GetString() ?? string.Empty).Trim();
			if (text.Equals("no", StringComparison.OrdinalIgnoreCase) || text.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				trackId = "no";
				verifyId = -1L;
				return true;
			}
			if (text.Equals("auto", StringComparison.OrdinalIgnoreCase))
			{
				trackId = "auto";
				verifyId = -1L;
				return true;
			}
			if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
			{
				trackId = parsed.ToString(CultureInfo.InvariantCulture);
				verifyId = parsed;
				return true;
			}
			error = "bad-value:" + kind + ":trackId=" + text;
			return false;
		}
		error = "bad-value:" + kind + ":trackId-type=" + value.ValueKind;
		return false;
	}

	/// <summary>数值的 InvariantCulture 文本（日志与规范值用；避免 de-DE 等区域把小数点变成逗号）。</summary>
	private static string FormatControlNumber(double value)
	{
		return value.ToString("R", CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// <b>`sub-visibility` 的独立可逆写点</b>（<c>"yes"</c>/<c>"no"</c>）。
	/// <para><b>只动 `sub-visibility`，不动 `sid`</b> ⇒ 关掉再开**不丢用户已选字幕**（这是裁定 (b) 相对 (a) 的核心收益）。</para>
	/// <para><b>为什么算"既有写点"</b>：走的是与内核既有 3 处 `sub-visibility` 写点**完全相同**的 API
	/// （<see cref="MpvNative.SetPropertyString"/>，见 `PlayerViewModel.cs` 的三处 `"yes"`），
	/// 区别只是本方法**也能写 `"no"`** —— 既有 3 处从来只写 `"yes"`，所以"关字幕"此前没有写点。</para>
	/// </summary>
	private async Task<bool> ApplySubtitleVisibilityAsync(bool visible)
	{
		if (Client == null)
		{
			return false;
		}
		string wanted = (visible ? "yes" : "no");
		MpvError write = await Task.Run(() => MpvNative.SetPropertyString(Client.Handle, "sub-visibility", wanted)).ConfigureAwait(false);
		_logger.LogInformation("[CONTROL-VERIFY] write sub-visibility={Wanted} result={Result}", wanted, write);
		return write == MpvError.Success;
	}

	/// <summary>
	/// <b>修法 B（t252）：把 `sub-visibility` 回滚到 <paramref name="before"/> 并回读确认</b>（3×100 ms）。
	/// <para>返回 <c>null</c> = **回滚成功**（状态已恢复到 before ⇒ 调用方可安全地"报错且状态未变"）；
	/// 否则返回**命名的未定义态**错误码（前缀仍是 <c>mpv-not-applied</c> ⇒ 端点按既有 <c>IsPayloadFault</c> 前缀映射为 <b>400</b>，
	/// 不需要改 <c>Services</c>；两个新态的**区分靠名字**，不靠状态码 —— 与既有 <c>danmaku-unavailable</c> 同一纪律）：</para>
	/// <list type="bullet">
	///   <item><c>…/rollback-unavailable:before=unknown</c> —— <c>before</c> 读不到（unknown）⇒ **不猜**：写任何值都可能是错的原值。</item>
	///   <item><c>…/rollback-failed:target=&lt;yes|no&gt;/&lt;observed&gt;</c> —— 回滚写成功但回读未确认（mpv 拒写或时序未落）。</item>
	/// </list>
	/// <para>⚠️ 本方法**只**在"写已发生但回读不匹配"这条路径上被调用（见 <c>SetSubtitleVisibility</c> 的 <c>!applied</c> 分支）；
	/// 成功路径一字未动。</para>
	/// </summary>
	private async Task<string> RollbackSubtitleVisibilityAsync(bool? before, bool? actual)
	{
		string actualText = (actual.HasValue ? (actual.Value ? "yes" : "no") : "unknown");
		if (!before.HasValue)
		{
			_logger.LogWarning("[CONTROL-VERIFY] rollback-unavailable sub-visibility before=(unknown) actual={Actual}（before 读不到 ⇒ 不猜、不回写）", actualText);
			return "mpv-not-applied:sub-visibility:rollback-unavailable:before=unknown/actual=" + actualText;
		}
		bool target = before.Value;
		string targetText = (target ? "yes" : "no");
		bool write = await ApplySubtitleVisibilityAsync(target).ConfigureAwait(false);
		if (!write)
		{
			_logger.LogWarning("[CONTROL-VERIFY] rollback-failed sub-visibility target={Target} reason=write-failed actual={Actual}", targetText, actualText);
			return "mpv-not-applied:sub-visibility:rollback-failed:target=" + targetText + "/write-failed/actual=" + actualText;
		}
		bool? seen = null;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			await Task.Delay(100).ConfigureAwait(false);
			(double _, double _, int? _, long _, long _, bool? sv, bool? _) = ReadMpvState();
			seen = sv;
			if (sv.HasValue && sv.Value == target)
			{
				_logger.LogInformation("[CONTROL-VERIFY] rollback-verified sub-visibility target={Target} attempt={Attempt}", targetText, attempt + 1);
				return null;
			}
		}
		string seenText = (seen.HasValue ? (seen.Value ? "yes" : "no") : "unknown");
		_logger.LogWarning("[CONTROL-VERIFY] rollback-failed sub-visibility target={Target} reason=readback-mismatch observed={Seen}", targetText, seenText);
		return "mpv-not-applied:sub-visibility:rollback-failed:target=" + targetText + "/" + seenText;
	}

	/// <summary>

	/// 只对**数字** id 生效：`no`/`auto` 的 mpv 结果取决于片源（无字幕轨时 `sid` 恒 -1），加校验会产生假 400。
	/// 回读用的是与进度回推**同一条** <see cref="ReadMpvState"/>（不新增读 mpv 路径）；最多 3 次 × 100 ms 等 mpv 落地。
	/// </summary>
	private async Task<(bool Applied, long Actual)> VerifyNumericTrackAppliedAsync(string property, long wanted, long before)
	{
		long actual = before;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			await Task.Delay(100).ConfigureAwait(false);
			(double _, double _, int? _, long mpvAid, long mpvSid, bool? _, bool? _) = ReadMpvState();
			actual = ((property == "aid") ? mpvAid : mpvSid);
			// 每行都要能看出**改之前**是什么 —— 只有 wanted/actual 无法判断
			// "本来就是这个值"（那种情况下回读成功其实是**空操作**，见 LogTrackNoOpIfAny）。
			_logger.LogInformation("[CONTROL-VERIFY] property={Property} before={Before} wanted={Wanted} actual={Actual} attempt={Attempt} applied={Applied}", property, before, wanted, actual, attempt + 1, (actual == wanted));
			if (actual == wanted)
			{
				return (true, actual);
			}
		}
		return (false, actual);
	}

	/// <summary>
	/// 数字 track id 的**存在性前置校验**（返回 <c>null</c> = 通过；否则 <c>track-not-found:&lt;prop&gt;:&lt;id&gt;</c>）。
	/// <para>🔴 <b>必须在写 mpv 之前调用</b>（t241 / F4，实测）：旧顺序 = "先 Apply 再校验" ⇒ 非法 id（<c>aid=9</c>）
	/// 在返回 <c>400 track-not-found</c> 的**同时**已把 mpv 的 <c>aid</c> 改成 <c>-1</c>（实测 1 → −1；<c>sid</c> 同理 2 → −1）
	/// ⇒ 外部看到的是"报错但状态已经变了"。前移后：拒绝发生在**任何写之前** ⇒ 400 与"mpv 状态逐位未变"同时成立。</para>
	/// <para>⚠️ 保守性**一字未改**：探针**读失败**时**绝不**判"不存在"（<see cref="TryProbeTrackIds"/> 返回 false ⇒ 不拦），
	/// 交由写后的 <see cref="VerifyTrackAsync"/> 给出 <c>mpv-not-applied</c>（读不到 ⇒ 不冤枉合法 id）。</para>
	/// <para>读/写计数**不变**：仍是同一次 <c>track-list</c> 读、同一个 mpv 写点（K4 仍满足 —— 不新增写路径）。</para>
	/// </summary>
	private string EnsureTrackExistsForControl(string property, long wanted)
	{
		string trackType = ((property == "aid") ? "audio" : "sub");
		if (TryProbeTrackIds(trackType, out HashSet<long> ids) && !ids.Contains(wanted))
		{
			_logger.LogWarning("[CONTROL-VERIFY] track-not-found property={Property} wanted={Wanted} trackList=[{Ids}] phase=pre-write（先判后写：本条拒绝发生在任何 mpv 写之前 ⇒ mpv 状态未被改动）", property, wanted, string.Join(",", ids));
			return "track-not-found:" + property + ":" + wanted.ToString(CultureInfo.InvariantCulture);
		}
		return null;
	}

	/// <summary>
	/// 数字 track id 的**回读校验**（返回 <c>null</c> = 通过；否则是错误码）。
	/// <list type="number">
	///   <item><b>① 存在性</b>：**不在这里** —— 已前移到 <see cref="EnsureTrackExistsForControl"/>，由调用方
	///         在**写 mpv 之前**调用（t241 / F4：先判后写）。</item>
	///   <item><b>② 再比回读值</b>：3×100 ms 回读 <c>aid</c>/<c>sid</c>，不等 ⇒ <c>mpv-not-applied:&lt;prop&gt;:&lt;wanted&gt;/&lt;actual&gt;</c>。</item>
	/// </list>
	/// 🔴 <b>为什么"存在性"必须在写之前（本方法的前身把两步合在一起）</b>：合并的顺序是"写 → 判存在 → 回读"，
	/// 于是"判存在失败"这条出口**已经在写之后** ⇒ 400 与状态已变同时成立（实测 <c>aid=9</c> ⇒ 1 → −1）。
	/// 拆开后两条判据各就各位：**存在性 = 拒之于写前**，**回读 = 确认写真的落地**。
	/// <para>⚠️ 存在性**不能只靠回读值**：mpv 在**播放已结束/空闲**状态下会把 <c>aid</c>/<c>sid</c>
	/// **回显**成请求的任意数字（实测：播放结束后 `aid=999` ⇒ 回读 = **999**）⇒ 只比回读值会给出**假 204**。</para>
	/// </summary>
	private async Task<string> VerifyTrackAsync(string property, long wanted, long before)
	{
		(bool applied, long actual) = await VerifyNumericTrackAppliedAsync(property, wanted, before).ConfigureAwait(false);
		if (!applied)
		{
			_logger.LogWarning("[CONTROL-VERIFY] mpv-not-applied property={Property} before={Before} wanted={Wanted} actual={Actual}", property, before, wanted, actual);
			return "mpv-not-applied:" + property + ":" + wanted.ToString(CultureInfo.InvariantCulture) + "/" + actual.ToString(CultureInfo.InvariantCulture);
		}
		LogTrackNoOpIfAny(property, before, wanted);
		return null;
	}

	/// <summary>回读**成功**但"本来就是这个值"时留一行 —— 那种 204 是**空操作**，不是"命令改了它"。</summary>
	private void LogTrackNoOpIfAny(string property, long before, long wanted)
	{
		if (before == wanted)
		{
			_logger.LogInformation("[CONTROL-VERIFY] noop property={Property} before={Before} wanted={Wanted}（本来就是这个值：回读成功 ≠ 命令改了它）", property, before, wanted);
		}
	}

	/// <summary>
	/// 读 mpv 的 <c>track-list</c>，取该 <paramref name="trackType"/>（<c>audio</c>/<c>sub</c>）下的 id 集合，
	/// **并区分"读失败"与"列表为空"**（返回 false = 读不到 ⇒ 调用方不得断言"不存在"）。
	/// ⚠️ **为什么不用既有 <see cref="GetMpvAudioTracks"/>/<see cref="GetMpvSubtitleTracks"/> 判存在性**：它们在
	/// <c>track-list</c> 读失败时都返回**空列表**，与"真的没有轨"不可区分 ⇒ 拿它们判 <c>track-not-found</c> 会在
	/// "mpv 一时读不到"时产生**假 400**。这里只新增**读**路径（不新增任何 mpv 写点，K4 仍满足），
	/// 且复用既有的 <see cref="ReadNodeMap"/>/<see cref="GetNodeInt64"/>/<see cref="GetNodeString"/> 解析函数。
	/// </summary>
	private bool TryProbeTrackIds(string trackType, out HashSet<long> ids)
	{
		ids = new HashSet<long>();
		if (Client == null)
		{
			return false;
		}
		MpvError probe = MpvNative.GetProperty(Client.Handle, "track-list", MpvFormat.Node, out var data);
		if (probe != MpvError.Success || data.Format != MpvFormat.NodeArray)
		{
			_logger.LogWarning("[CONTROL-VERIFY] track-list-probe-failed result={Result} format={Format}（读不到 ⇒ 不判 track-not-found）", probe, data.Format);
			if (data.Format != MpvFormat.None)
			{
				try
				{
					MpvNative.FreeNodeContents(ref data);
				}
				catch (Exception freeEx)
				{
					// 释放失败不影响本方法的结论（"读不到"这个语义已经确定）⇒ 只记录，不上抛。
					_logger.LogWarning(freeEx, "释放 mpv track-list 节点失败（探测已按读不到处理）");
				}
			}
			return false;
		}
		try
		{
			MpvNodeList remoteNodeListValue = data.RemoteNodeListValue;
			int num = Math.Max(0, Math.Min(remoteNodeListValue.Num, 128));
			for (int i = 0; i < num; i++)
			{
				MpvNode node = Marshal.PtrToStructure<MpvNode>(remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>());
				if (node.Format != MpvFormat.NodeMap)
				{
					continue;
				}
				Dictionary<string, MpvNode> map = ReadNodeMap(node);
				if (!string.Equals(GetNodeString(map, "type"), trackType, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				long? nodeInt = GetNodeInt64(map, "id");
				if (nodeInt.HasValue && nodeInt.GetValueOrDefault() > 0)
				{
					ids.Add(nodeInt.Value);
				}
			}
			return true;
		}
		finally
		{
			try
			{
				MpvNative.FreeNodeContents(ref data);
			}
			catch (Exception freeEx)
			{
				// 同上：结论已定（ids 已填好），释放失败不改结论 ⇒ 只记录。
				_logger.LogWarning(freeEx, "释放 mpv track-list 节点失败（探测结果已得出）");
			}
		}
	}

	/// <summary>
	/// 从 **mpv 本体**读播放状态（speed / sub-delay / volume / aid / sid）。
	/// 读不到时回落到 VM 缓存值（首次上报前 mpv 可能尚未就绪）—— 回落值**不**冒充 mpv 值，日志里能看出读了哪些。
	/// </summary>
	private (double Speed, double SubDelay, int? Volume, long Aid, long Sid, bool? SubVisible, bool? Muted) ReadMpvState()
	{
		double speed = Speed;
		double subDelay = 0.0;
		// K-05（t162）：**读失败 = `null`，不再兜底成 UI 音量** —— 旧实现把 UI 音量当"读失败值"，
		//   调用方再用 `> 0` 判读 ⇒ mpv 真为 0 时被误判成读失败。`0` 是合法读数，必须与 null 区分。
		int? volume = null;
		bool? muted = null;
		long aid = -1L;
		long sid = -1L;
		// `sub-visibility` 的**真实值**。读不到 ⇒ null —— **不做假值**（不冒充 true/false），
		//   外壳据此看出"字幕被关了"；null 表示"未知"，与 false 严格区分。
		bool? subVisible = null;
		if (Client != null)
		{
			if (MpvNative.GetPropertyDouble(Client.Handle, "speed", MpvFormat.Double, out var s) == MpvError.Success)
			{
				speed = s;
			}
			if (MpvNative.GetPropertyDouble(Client.Handle, "sub-delay", MpvFormat.Double, out var d) == MpvError.Success)
			{
				subDelay = d;
			}
			if (MpvNative.GetPropertyDouble(Client.Handle, "volume", MpvFormat.Double, out var v) == MpvError.Success)
			{
				// 读成功 ⇒ 原样采用（**包括 0**）：0 与"读失败(null)"从此不同形。
				volume = (int)Math.Round(v);
			}
			if (MpvNative.GetPropertyInt64(Client.Handle, "aid", MpvFormat.Int64, out var a) == MpvError.Success)
			{
				aid = a;
			}
			if (MpvNative.GetPropertyInt64(Client.Handle, "sid", MpvFormat.Int64, out var si) == MpvError.Success)
			{
				sid = si;
			}
			// K-05（t162）：读 mpv 的 `mute` **真值**（Node 形式，与 `sub-visibility` 同一条已证安全的路径；
			//   字符串形式在工作线程上会让进程随后无日志退出 —— 见下方 `sub-visibility` 的实测注释）。
			//   读不到 ⇒ 保持 null（"未知"），**不冒充 false**。
			try
			{
				if (MpvNative.GetProperty(Client.Handle, "mute", MpvFormat.Node, out var muteNode) == MpvError.Success)
				{
					try
					{
						if (muteNode.Format == MpvFormat.Flag)
						{
							muted = (muteNode.Flag != 0);
						}
					}
					finally
					{
						try
						{
							MpvNative.FreeNodeContents(ref muteNode);
						}
						catch (Exception freeMuteEx)
						{
							_logger.LogWarning(freeMuteEx, "释放 mpv mute 节点失败（读取结果已确定）");
						}
					}
				}
			}
			catch (Exception readMuteEx)
			{
				_logger.LogWarning(readMuteEx, "读取 mpv mute 失败：本次按未知(null)处理");
				muted = null;
			}
			// 读 mpv 的 sub-visibility。为什么用 Node 形式而不是直接请求 Flag：请求 Flag 时返回节点的 Format 并不是 Flag，
			// 会永远读不到（表现为该字段恒为"未知"）；而字符串形式虽然能读到，却在工作线程上调用会让进程随后无任何日志地退出。
			// Node 形式与本文件读 track-list 用的是同一条已证安全的路径，标量节点也不需要额外分配。
			try
			{
				if (MpvNative.GetProperty(Client.Handle, "sub-visibility", MpvFormat.Node, out var svNode) == MpvError.Success)
				{
					try
					{
						if (svNode.Format == MpvFormat.Flag)
						{
							subVisible = (svNode.Flag != 0);
						}
					}
					finally
					{
						try
						{
							MpvNative.FreeNodeContents(ref svNode);
						}
						catch (Exception freeEx)
						{
							// 释放失败不改结论（subVisible 已按节点内容定好或保持 null）⇒ 只记录，不上抛。
							_logger.LogWarning(freeEx, "释放 mpv sub-visibility 节点失败（读取结果已确定）");
						}
					}
				}
			}
			catch (Exception readEx)
			{
				// 读不到就保持 null（该字段的"未知"语义，绝不冒充 true/false）；但必须留痕，
				// 否则调用方只看到"没有值"，分不清"读失败"与"本来就没有"。
				_logger.LogWarning(readEx, "读取 mpv sub-visibility 失败：本次按未知(null)处理");
				subVisible = null;
			}
		}
		return (speed, subDelay, volume, aid, sid, subVisible, muted);
	}

	private async Task<bool> ApplyInternalSubtitleSelectionAsync(OverlayTrackOption track)
	{
		if (Client == null)
		{
			return false;
		}
		string text = await Task.Run(() => ResolveInternalSubtitleTrackId(track));
		if (!string.IsNullOrWhiteSpace(text))
		{
			track.ResolvedId = text;
			_logger.LogDebug($"PlayerViewModel.ApplyInternalSubtitleSelectionAsync resolved label={track.Label} requestedId={track.Id} resolvedId={text}");
			return await ApplySubtitleIdAsync(text);
		}
		if (!string.IsNullOrWhiteSpace(track.Id))
		{
			track.ResolvedId = track.Id;
			_logger.LogDebug("PlayerViewModel.ApplyInternalSubtitleSelectionAsync fallback label=" + track.Label + " id=" + track.Id);
			return await ApplySubtitleIdAsync(track.Id);
		}
		_logger.LogWarning("[SUB-TRACK] skipped reason=no-usable-track-id property=sid label={Label} requestedId={RequestedId}（既解析不出 mpv 字幕轨 id、宿主也没给 id ⇒ 什么都不会发生，必须可见）", track.Label, track.Id);
		return false;
	}

	private Task RefreshTracksAsync()
	{
		_queue.TryEnqueue(RefreshSubtitleTracksFromMpv);
		return Task.CompletedTask;
	}

	/// <summary>
	/// 加载（拖入 / 字幕搜索选中的）本地字幕文件。
	/// <b>三条分支都必须可见</b>：过去只在成功分支做事 —— add 失败、mpv 未就绪、空路径全是**零痕迹**，
	/// 用户侧的表现是"拖了字幕毫无反应"，日志里也查不到任何线索（这是整个内核最静默的一条路径）。
	/// </summary>
	internal async Task LoadSubtitleFileAsync(string path)
	{
		if (Client == null)
		{
			_logger.LogWarning("[SUB-FILE] skipped reason=mpv-not-ready path={Path}", path);
			return;
		}
		if (string.IsNullOrWhiteSpace(path))
		{
			_logger.LogWarning("[SUB-FILE] skipped reason=empty-path");
			return;
		}
		if (!(await Client.AddSubtitleAsync(path)).IsSuccess)
		{
			_logger.LogWarning("[SUB-FILE] add-failed path={Path}（mpv 拒绝 sub-add；成功时不会出现本行）", path);
			return;
		}
		await RefreshTracksAsync();
		_logger.LogInformation("[SUB-FILE] queued path={Path}", path);
	}

	private async Task LoadDroppedSubtitleFilesAsync(IReadOnlyList<string> paths)
	{
		foreach (string path in paths)
		{
			await LoadSubtitleFileAsync(path);
		}
	}

	internal void RefreshSubtitleTracksFromMpv()
	{
		if (Client == null)
		{
			return;
		}
		EnsureHostSubtitleTracksCaptured();
		List<MpvSubtitleTrack> mpvSubtitleTracks = GetMpvSubtitleTracks();
		List<OverlayTrackOption> hostSubtitleTracks = _hostSubtitleTracks;
		List<OverlayTrackOption> list = hostSubtitleTracks.Where((OverlayTrackOption t) => !t.IsSpecial && !t.IsExternal).ToList();
		List<OverlayTrackOption> list2 = (from g in hostSubtitleTracks.Where((OverlayTrackOption t) => !t.IsSpecial && t.IsExternal && !string.IsNullOrWhiteSpace(t.Url)).GroupBy<OverlayTrackOption, string>((OverlayTrackOption t) => NormalizeExternalSubtitleKey(t.Url), StringComparer.OrdinalIgnoreCase)
										  select g.First()).ToList();
		List<MpvSubtitleTrack> list3 = mpvSubtitleTracks.Where((MpvSubtitleTrack t) => !t.External).ToList();
		List<MpvSubtitleTrack> list4 = mpvSubtitleTracks.Where((MpvSubtitleTrack t) => t.External).ToList();
		MpvSubtitleTrack mpvSubtitleTrack = mpvSubtitleTracks.FirstOrDefault((MpvSubtitleTrack t) => t.Selected);
		bool isSelected = mpvSubtitleTrack is null && (mpvSubtitleTracks.Count == 0 || mpvSubtitleTracks.All((MpvSubtitleTrack p) => !p.Selected));
		List<OverlayTrackOption> list5 = new List<OverlayTrackOption>
		{
			new OverlayTrackOption
			{
				Id = "no",
				Label = "关闭字幕",
				IsSpecial = true,
				IsSelected = isSelected
			}
		};
		if (list3.Count > 0)
		{
			foreach (MpvSubtitleTrack item in list3)
			{
				OverlayTrackOption overlayTrackOption = FindHostInternalMatch(list, item);
				list5.Add(new OverlayTrackOption
				{
					Id = item.Id.ToString(),
					ResolvedId = item.Id.ToString(),
					Label = (overlayTrackOption?.Label ?? BuildSubtitleTrackLabel(item)),
					Title = (overlayTrackOption?.Title ?? item.Title),
					Language = (overlayTrackOption?.Language ?? item.Language),
					Url = string.Empty,
					IsExternal = false,
					IsSelected = false,
					IsSpecial = false,
					EmbyStreamIndex = (overlayTrackOption?.EmbyStreamIndex ?? (-1))
				});
			}
		}
		else
		{
			foreach (OverlayTrackOption item2 in list)
			{
				list5.Add(CloneTrackOption(item2));
			}
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (OverlayTrackOption item3 in list2)
		{
			MpvSubtitleTrack mpvSubtitleTrack2 = FindMpvExternalMatch(list4, item3);
			if (mpvSubtitleTrack2 is not null)
			{
				hashSet.Add(mpvSubtitleTrack2.Id);
			}
			list5.Add(new OverlayTrackOption
			{
				Id = ((mpvSubtitleTrack2 is not null) ? mpvSubtitleTrack2.Id.ToString() : item3.Id),
				ResolvedId = (mpvSubtitleTrack2?.Id.ToString() ?? string.Empty),
				Label = item3.Label,
				Title = item3.Title,
				Language = item3.Language,
				Url = item3.Url,
				IsExternal = true,
				IsSelected = false,
				IsSpecial = false,
				EmbyStreamIndex = item3.EmbyStreamIndex
			});
		}
		foreach (MpvSubtitleTrack track in list4)
		{
			if (!hashSet.Contains(track.Id) && !list2.Any((OverlayTrackOption hostTrack) => ExternalSubtitleUrlsMatch(hostTrack.Url, track)) && LooksLikeLocalSubtitlePath(track.Title))
			{
				list5.Add(new OverlayTrackOption
				{
					Id = track.Id.ToString(),
					ResolvedId = track.Id.ToString(),
					Label = BuildSubtitleTrackLabel(track),
					Title = track.Title,
					Language = track.Language,
					Url = string.Empty,
					IsExternal = true,
					IsSelected = false,
					IsSpecial = false
				});
			}
		}
		ApplySingleSubtitleSelection(list5, mpvSubtitleTrack);
		SubtitleTracks.Clear();
		foreach (OverlayTrackOption item4 in list5)
		{
			SubtitleTracks.Add(item4);
		}
		OnPropertyChanged("SelectedSubtitleTrackLabel");
	}

	private void CaptureHostSubtitleTracks()
	{
		_hostSubtitleTracks = (from t in SubtitleTracks
							   where !string.IsNullOrWhiteSpace(t.Label)
							   select CloneTrackOption(t)).ToList();
	}

	private void EnsureHostSubtitleTracksCaptured()
	{
		if (_hostSubtitleTracks.Count <= 0)
		{
			CaptureHostSubtitleTracks();
		}
	}

	private static OverlayTrackOption CloneTrackOption(OverlayTrackOption source, bool? isSelected = null)
	{
		return new OverlayTrackOption
		{
			Id = source.Id,
			Label = source.Label,
			Url = source.Url,
			Title = source.Title,
			Language = source.Language,
			IsExternal = source.IsExternal,
			IsSelected = (isSelected ?? source.IsSelected),
			ResolvedId = source.ResolvedId,
			IsSpecial = source.IsSpecial,
			EmbyStreamIndex = source.EmbyStreamIndex
		};
	}

	private static OverlayTrackOption? FindHostInternalMatch(IReadOnlyList<OverlayTrackOption> hostInternals, MpvSubtitleTrack mpvTrack)
	{
		if (hostInternals.Count == 0)
		{
			return null;
		}
		string b = NormalizeSubtitleText(mpvTrack.Title);
		string b2 = NormalizeSubtitleText(mpvTrack.Language);
		foreach (OverlayTrackOption hostInternal in hostInternals)
		{
			string text = NormalizeSubtitleText(hostInternal.Title);
			string text2 = NormalizeSubtitleText(hostInternal.Language);
			if (!string.IsNullOrWhiteSpace(text) && string.Equals(text, b, StringComparison.Ordinal))
			{
				return hostInternal;
			}
			if (!string.IsNullOrWhiteSpace(text2) && string.Equals(text2, b2, StringComparison.Ordinal))
			{
				return hostInternal;
			}
		}
		return null;
	}

	private static MpvSubtitleTrack? FindMpvExternalMatch(IReadOnlyList<MpvSubtitleTrack> mpvExternals, OverlayTrackOption hostTrack)
	{
		if (mpvExternals.Count == 0 || string.IsNullOrWhiteSpace(hostTrack.Url))
		{
			return null;
		}
		List<MpvSubtitleTrack> list = mpvExternals.Where((MpvSubtitleTrack mpvTrack) => ExternalSubtitleUrlsMatch(hostTrack.Url, mpvTrack)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		MpvSubtitleTrack mpvSubtitleTrack = list.FirstOrDefault((MpvSubtitleTrack t) => t.Selected);
		if (mpvSubtitleTrack is null)
		{
			mpvSubtitleTrack = list[list.Count - 1];
		}
		return mpvSubtitleTrack;
	}

	private static bool ExternalSubtitleUrlsMatch(string hostUrl, MpvSubtitleTrack mpvTrack)
	{
		string text = NormalizeExternalSubtitleKey(hostUrl);
		string text2 = NormalizeExternalSubtitleKey(mpvTrack.Title);
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			return false;
		}
		if (!string.Equals(text, text2, StringComparison.OrdinalIgnoreCase) && !text.Contains(text2, StringComparison.OrdinalIgnoreCase))
		{
			return text2.Contains(text, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string NormalizeExternalSubtitleKey(string value)
	{
		string text = value.Trim().Trim(new char[2] { '\'', '"' });
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			return result.PathAndQuery.TrimStart('/');
		}
		int num = text.IndexOf('?');
		if (num >= 0)
		{
			int num2 = text.LastIndexOf('/', num);
			if (num2 < 0)
			{
				return text;
			}
			return text.Substring(num2 + 1);
		}
		return text;
	}

	private static bool LooksLikeLocalSubtitlePath(string value)
	{
		string text = value.Trim().Trim(new char[2] { '\'', '"' });
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (text.Contains('\\', StringComparison.Ordinal) || text.Contains(":/", StringComparison.Ordinal))
		{
			return true;
		}
		if (!text.Contains('?', StringComparison.Ordinal) && text.EndsWith(".ass", StringComparison.OrdinalIgnoreCase))
		{
			return !text.Contains("api_key=", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static void ApplySingleSubtitleSelection(IList<OverlayTrackOption> items, MpvSubtitleTrack? mpvSelected)
	{
		foreach (OverlayTrackOption item in items)
		{
			item.IsSelected = false;
		}
		if (mpvSelected is null)
		{
			OverlayTrackOption overlayTrackOption = items.FirstOrDefault((OverlayTrackOption t) => t.IsSpecial);
			if (overlayTrackOption != null)
			{
				overlayTrackOption.IsSelected = true;
			}
			return;
		}
		OverlayTrackOption overlayTrackOption2 = null;
		overlayTrackOption2 = ((!mpvSelected.External) ? items.FirstOrDefault((OverlayTrackOption item) => !item.IsSpecial && !item.IsExternal && string.Equals(item.ResolvedId, mpvSelected.Id.ToString(), StringComparison.Ordinal)) : items.FirstOrDefault((OverlayTrackOption item) => !item.IsSpecial && item.IsExternal && (string.Equals(item.ResolvedId, mpvSelected.Id.ToString(), StringComparison.Ordinal) || ExternalSubtitleUrlsMatch(item.Url, mpvSelected))));
		if (overlayTrackOption2 != null)
		{
			overlayTrackOption2.IsSelected = true;
			return;
		}
		OverlayTrackOption overlayTrackOption3 = items.FirstOrDefault((OverlayTrackOption t) => t.IsSpecial);
		if (overlayTrackOption3 != null)
		{
			overlayTrackOption3.IsSelected = true;
		}
	}

	private string? FindMpvExternalSubtitleIdByUrl(string url)
	{
		List<MpvSubtitleTrack> list = (from track in GetMpvSubtitleTracks()
									   where track.External && ExternalSubtitleUrlsMatch(url, track)
									   select track).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		MpvSubtitleTrack mpvSubtitleTrack = list.FirstOrDefault((MpvSubtitleTrack t) => t.Selected);
		if (mpvSubtitleTrack is null)
		{
			mpvSubtitleTrack = list[list.Count - 1];
		}
		return mpvSubtitleTrack.Id.ToString();
	}

	private async Task CleanupDuplicateExternalSubtitlesAsync(string url, string? keepId = null)
	{
		if (Client == null || string.IsNullOrWhiteSpace(url))
		{
			return;
		}
		List<MpvSubtitleTrack> list = (from mpvSubtitleTrack in GetMpvSubtitleTracks()
									   where mpvSubtitleTrack.External && ExternalSubtitleUrlsMatch(url, mpvSubtitleTrack)
									   select mpvSubtitleTrack).ToList();
		if (list.Count <= 1)
		{
			return;
		}
		object obj = keepId;
		if (obj == null)
		{
			obj = list.FirstOrDefault((MpvSubtitleTrack t) => t.Selected)?.Id.ToString();
			if (obj == null)
			{
				obj = list[list.Count - 1].Id.ToString();
			}
		}
		string keep = (string)obj;
		ulong requestId = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		foreach (MpvSubtitleTrack track in list.Where((MpvSubtitleTrack mpvSubtitleTrack) => mpvSubtitleTrack.Id.ToString() != keep))
		{
			ulong num = requestId;
			requestId = num + 1;
			MpvError mpvError = await Task.Run(() => MpvNative.SetCommandAsync(Client.Handle, requestId, new string[2]
			{
				"sub-remove",
				track.Id.ToString()
			}));
			if (mpvError != MpvError.Success)
			{
				_logger.LogWarning("Failed to remove duplicate external subtitle sid={SubtitleId}. Error={ErrorCode}", track.Id, mpvError);
			}
		}
	}

	private async Task<bool> ApplyExternalSubtitleSelectionAsync(OverlayTrackOption track)
	{
		if (Client == null)
		{
			_logger.LogWarning("[SUB-EXTERNAL] skipped reason=mpv-not-ready url={Url}", track?.Url);
			return false;
		}
		if (string.IsNullOrWhiteSpace(track.Url))
		{
			_logger.LogWarning("[SUB-EXTERNAL] skipped reason=empty-url label={Label}", track?.Label);
			return false;
		}
		string existingId = ((!string.IsNullOrWhiteSpace(track.ResolvedId)) ? track.ResolvedId : FindMpvExternalSubtitleIdByUrl(track.Url));
		if (!string.IsNullOrWhiteSpace(existingId))
		{
			_logger.LogDebug("PlayerViewModel.ApplyExternalSubtitleSelectionAsync reuse sid=" + existingId + " label=" + track.Label);
			await CleanupDuplicateExternalSubtitlesAsync(track.Url, existingId);
			return await ApplySubtitleIdAsync(existingId);
		}
		_logger.LogDebug("PlayerViewModel.ApplyExternalSubtitleSelectionAsync sub-add begin label=" + track.Label + " url=" + track.Url);
		ulong requestId = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		MpvError mpvError = await Task.Run(() => MpvNative.SetCommandAsync(Client.Handle, requestId, new string[3] { "sub-add", track.Url, "select" }));
		_logger.LogDebug($"PlayerViewModel.ApplyExternalSubtitleSelectionAsync sub-add queued result={mpvError} requestId={requestId}");
		if (mpvError != MpvError.Success)
		{
			_logger.LogWarning("Failed to add subtitle from url {Url}. Error={ErrorCode}", track.Url, mpvError);
			return false;
		}
		// 🔴 **`sub-add` 排队成功 ≠ 字幕已加进来**：文件不存在 / 格式不支持时 mpv 只是把命令丢掉，
		//   于是这里会 `return true` —— 上层（含 `POST /control`）把它读成"字幕已挂上"，
		//   而用户看到的是"选了字幕却没有字幕"。⇒ 必须**回读** `track-list` 再下结论。
		(string? landedId, bool trackListReadable) = await WaitExternalSubtitleIdAsync(track.Url).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(landedId))
		{
			if (!trackListReadable)
			{
				// 读不到 ≠ 没有该轨：主写点（sub-add）已经成功，**不能**用一个"读不到"的回读把成功判成失败。
				_logger.LogWarning("[SUB-EXTERNAL] landing-unknown reason=track-list-unreadable url={Url}（读不到 ≠ 没有该轨 ⇒ 不判失败，写点已下发）", track.Url);
				return true;
			}
			_logger.LogWarning("[SUB-EXTERNAL] not-landed url={Url}（sub-add 已排队，但 track-list 里始终没有这条 external 字幕）", track.Url);
			return false;
		}
		await CleanupDuplicateExternalSubtitlesAsync(track.Url, landedId);
		await TryEnableSubtitleVisibilityAsync("ApplyExternalSubtitleSelectionAsync").ConfigureAwait(false);
		_logger.LogInformation("[SUB-EXTERNAL] landed url={Url} sid={Sid} label={Label}", track.Url, landedId, track.Label);
		return true;
	}

	/// <summary>
	/// 有界回读：等 <paramref name="url"/> 对应的外挂字幕出现在 mpv 的 <c>track-list</c> 里。
	/// 返回 <c>(null, true)</c> = 列表读得到、但**始终没有**这条字幕（= 真的没落地）；
	/// <c>(null, false)</c> = <c>track-list</c> **读不到** ⇒ **不可判**（调用方**不得**据此判失败）。
	/// 只在失败 / 读不到路径上花满窗口（成功时第一次探测就命中）。
	/// 读的是 <c>track-list</c> 节点（<see cref="MpvFormat.Node"/>）⇒ 从控制端点的线程池线程调用也是安全的。
	/// </summary>
	private async Task<(string? Id, bool TrackListReadable)> WaitExternalSubtitleIdAsync(string url)
	{
		for (int attempt = 0; attempt < 5; attempt++)
		{
			string? id = FindMpvExternalSubtitleIdByUrl(url);
			if (!string.IsNullOrWhiteSpace(id))
			{
				return (id, true);
			}
			// 读不到 ⇒ "空列表"与"真的没有该轨"不可区分 ⇒ 立刻改成"不可判"，不再空等 1 s。
			if (!TryProbeTrackIds("sub", out _))
			{
				return (null, false);
			}
			await Task.Delay(200).ConfigureAwait(false);
		}
		return (null, true);
	}

	private async Task ApplyExternalSubtitleUrlAsync(string subtitleUrl)
	{
		if (Client != null && !string.IsNullOrWhiteSpace(subtitleUrl))
		{
			await ApplyExternalSubtitleSelectionAsync(new OverlayTrackOption
			{
				Url = subtitleUrl,
				Label = subtitleUrl,
				IsExternal = true
			});
		}
	}

	private async Task<bool> ApplySubtitleIdAsync(string subtitleId)
	{
		if (Client == null)
		{
			_logger.LogWarning("[SUB-TRACK] skipped reason=mpv-not-ready property=sid sid={SubtitleId}", subtitleId);
			return false;
		}
		if (string.IsNullOrWhiteSpace(subtitleId))
		{
			_logger.LogWarning("[SUB-TRACK] skipped reason=empty-sid");
			return false;
		}
		_logger.LogDebug("PlayerViewModel.ApplySubtitleIdAsync sid=" + subtitleId);
		MpvError mpvError = await Task.Run(() => MpvNative.SetPropertyString(Client.Handle, "sid", subtitleId));
		_logger.LogDebug($"PlayerViewModel.ApplySubtitleIdAsync sid result={mpvError}");
		if (mpvError != MpvError.Success)
		{
			_logger.LogWarning("Failed to apply subtitle id {SubtitleId}. Error={ErrorCode}", subtitleId, mpvError);
			return false;
		}
		await TryEnableSubtitleVisibilityAsync("ApplySubtitleIdAsync").ConfigureAwait(false);
		return true;
	}

	private static void MarkSelectedTrack(IList<OverlayTrackOption> tracks, OverlayTrackOption selected)
	{
		foreach (OverlayTrackOption track in tracks)
		{
			track.IsSelected = track == selected;
		}
	}

	private string? ResolveSelectedSubtitleUrl()
	{
		OverlayTrackOption overlayTrackOption = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
		if (overlayTrackOption == null || overlayTrackOption.IsSpecial)
		{
			_logger.LogDebug("PlayerViewModel.ResolveSelectedSubtitleUrl empty");
			return null;
		}
		string text = ((!overlayTrackOption.IsExternal || string.IsNullOrWhiteSpace(overlayTrackOption.Url)) ? null : overlayTrackOption.Url);
		_logger.LogDebug("PlayerViewModel.ResolveSelectedSubtitleUrl label=" + overlayTrackOption.Label + " url=" + (text ?? string.Empty));
		return text;
	}

	private string? ResolveSelectedSubtitleId()
	{
		OverlayTrackOption overlayTrackOption = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
		if (overlayTrackOption == null)
		{
			_logger.LogDebug("PlayerViewModel.ResolveSelectedSubtitleId empty");
			return null;
		}
		if (overlayTrackOption.IsSpecial)
		{
			_logger.LogDebug("PlayerViewModel.ResolveSelectedSubtitleId special=" + overlayTrackOption.Label);
			return "no";
		}
		string text = ((!overlayTrackOption.IsExternal) ? overlayTrackOption.Id : null);
		_logger.LogDebug("PlayerViewModel.ResolveSelectedSubtitleId label=" + overlayTrackOption.Label + " id=" + (text ?? string.Empty));
		return text;
	}

	private string? ResolveInternalAudioTrackId(OverlayTrackOption target)
	{
		if (Client == null)
		{
			return null;
		}
		List<MpvAudioTrack> mpvAudioTracks = GetMpvAudioTracks();
		if (mpvAudioTracks.Count == 0)
		{
			_logger.LogDebug("PlayerViewModel.ResolveInternalAudioTrackId no mpv audio tracks");
			return null;
		}
		List<OverlayTrackOption> list = AudioTracks.Where((OverlayTrackOption p) => !p.IsSpecial).ToList();
		int num = list.FindIndex((OverlayTrackOption option) => option == target);
		if (num < 0)
		{
			num = list.FindIndex((OverlayTrackOption option) => string.Equals(option.Id, target.Id, StringComparison.Ordinal));
		}
		if (num >= 0 && num < mpvAudioTracks.Count)
		{
			return mpvAudioTracks[num].Id.ToString();
		}
		int? requestedId = (int.TryParse(target.Id, out var result) ? new int?(result) : null);
		if (requestedId.HasValue && mpvAudioTracks.Any((MpvAudioTrack track) => track.Id == requestedId.Value))
		{
			return requestedId.Value.ToString();
		}
		return null;
	}

	private string? ResolveInternalSubtitleTrackId(OverlayTrackOption target)
	{
		if (Client == null)
		{
			return null;
		}
		List<MpvSubtitleTrack> mpvSubtitleTracks = GetMpvSubtitleTracks();
		if (mpvSubtitleTracks.Count == 0)
		{
			_logger.LogDebug("PlayerViewModel.ResolveInternalSubtitleTrackId no mpv subtitle tracks");
			return null;
		}
		List<MpvSubtitleTrack> list = mpvSubtitleTracks.Where((MpvSubtitleTrack p) => !p.External).ToList();
		List<OverlayTrackOption> list2 = SubtitleTracks.Where((OverlayTrackOption p) => !p.IsSpecial && !p.IsExternal).ToList();
		int num = list2.FindIndex((OverlayTrackOption option) => option == target);
		if (num < 0)
		{
			num = list2.FindIndex((OverlayTrackOption option) => string.Equals(option.Id, target.Id, StringComparison.Ordinal));
		}
		if (num >= 0 && num < list.Count)
		{
			return list[num].Id.ToString();
		}
		int? requestedId = (int.TryParse(target.Id, out var result) ? new int?(result) : null);
		if (requestedId.HasValue && list.Any((MpvSubtitleTrack track) => track.Id == requestedId.Value))
		{
			return requestedId.Value.ToString();
		}
		return null;
	}

	private void ApplyPreservedTrackSelection(OverlayTrackOption? preserveAudioTrack, OverlayTrackOption? preserveSubtitleTrack)
	{
		if (preserveAudioTrack != null)
		{
			MarkPreservedTrackSelected(AudioTracks, preserveAudioTrack);
		}
		if (preserveSubtitleTrack != null)
		{
			MarkPreservedTrackSelected(SubtitleTracks, preserveSubtitleTrack);
		}
	}

	private static void MarkPreservedTrackSelected(ObservableCollection<OverlayTrackOption> tracks, OverlayTrackOption preserved)
	{
		OverlayTrackOption overlayTrackOption = null;
		if (!string.IsNullOrWhiteSpace(preserved.Id))
		{
			overlayTrackOption = tracks.FirstOrDefault((OverlayTrackOption t) => string.Equals(t.Id, preserved.Id, StringComparison.Ordinal) || string.Equals(t.ResolvedId, preserved.Id, StringComparison.Ordinal));
		}
		if (overlayTrackOption == null)
		{
			overlayTrackOption = tracks.FirstOrDefault((OverlayTrackOption t) => preserved.EmbyStreamIndex >= 0 && t.EmbyStreamIndex == preserved.EmbyStreamIndex);
		}
		if (overlayTrackOption == null && !string.IsNullOrWhiteSpace(preserved.Language))
		{
			overlayTrackOption = tracks.FirstOrDefault((OverlayTrackOption t) => string.Equals(t.Language, preserved.Language, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Title, preserved.Title, StringComparison.OrdinalIgnoreCase));
		}
		if (overlayTrackOption == null)
		{
			overlayTrackOption = tracks.FirstOrDefault((OverlayTrackOption t) => string.Equals(t.Label, preserved.Label, StringComparison.OrdinalIgnoreCase));
		}
		if (overlayTrackOption == null)
		{
			return;
		}
		foreach (OverlayTrackOption track in tracks)
		{
			track.IsSelected = track == overlayTrackOption;
		}
	}

	internal bool ShouldIgnoreMpvLoad()
	{
		if (IsHostBundleSpuriousLoad())
		{
			return true;
		}
		if (!_awaitingStreamRecoveryLoad)
		{
			return false;
		}
		if (IsSpuriousRecoveryLoad())
		{
			return true;
		}
		return !HasMpvVideoTrack();
	}

	internal bool IsTransientPlaybackLoad()
	{
		return ShouldIgnoreMpvLoad();
	}

	private bool IsHostBundleSpuriousLoad()
	{
		if (!IsRemoteStreamMedia() && !_awaitingStreamRecoveryLoad)
		{
			return false;
		}
		string mpvCurrentPath = GetMpvCurrentPath();
		if (string.IsNullOrWhiteSpace(mpvCurrentPath) || IsRemoteMediaPath(mpvCurrentPath))
		{
			return false;
		}
		if (!IsUnderHostBundleDirectory(mpvCurrentPath))
		{
			return false;
		}
		if (HasMpvImageVideoTrack())
		{
			return true;
		}
		if (!string.IsNullOrEmpty(Path.GetExtension(mpvCurrentPath)))
		{
			return string.Equals(Path.GetFileName(mpvCurrentPath), "Assets", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsUnderHostBundleDirectory(string path)
	{
		try
		{
			string fullPath = Path.GetFullPath(path);
			string text = HostBundleDirectory.TrimEnd(new char[2]
			{
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			});
			return fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath, text, StringComparison.OrdinalIgnoreCase);
		}
		// t235：命名类型 = `Exception`，**不能再窄** —— `Path.GetFullPath`/`StartsWith` 链可抛
		// `ArgumentException`（非法字符）/`PathTooLongException`/`NotSupportedException`/`SecurityException` ⇒
		// 共同祖先只有 `Exception`；契约 = 任何失败一律判"不在该目录下"（保守 false，不放行越界路径）。
		catch (Exception)
		{
			return false;
		}
	}

	internal void SuppressSpuriousMpvLoad(string? path)
	{
		_logger.LogWarning("Suppressing spurious mpv load of host bundle asset: {Path}", path ?? "(unknown)");
		if (Client != null)
		{
			Client.StopAndClearPlaylistAsync();
		}
	}

	private bool IsSpuriousRecoveryLoad()
	{
		if (HasMpvImageVideoTrack())
		{
			return true;
		}
		string mpvCurrentPath = GetMpvCurrentPath();
		if (string.IsNullOrWhiteSpace(mpvCurrentPath))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(_pendingRecoveryMediaPath) && !PathsEquivalent(mpvCurrentPath, _pendingRecoveryMediaPath))
		{
			return true;
		}
		if (IsRemoteStreamMedia())
		{
			return !IsRemoteMediaPath(mpvCurrentPath);
		}
		return false;
	}

	private static bool IsRemoteMediaPath(string path)
	{
		if (!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			return path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool PathsEquivalent(string? a, string? b)
	{
		if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
		{
			return false;
		}
		return string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
	}

	private string? GetMpvCurrentPath()
	{
		if (Client == null)
		{
			return null;
		}
		try
		{
			return MpvNative.GetPropertyString(Client.Handle, "path");
		}
		// t235：命名类型 = `Exception`，**不能再窄** —— 本处是 mpv P/Invoke 读属性，可抛面包含
		// `EntryPointNotFoundException`/`DllNotFoundException`/`InvalidOperationException`/`ArgumentException`
		// （且句柄失效时有 `AccessViolationException` 风险路径）⇒ 共同祖先只有 `Exception`；
		// 契约 = 读不到 ⇒ 返回 `null`（与"读到空串"严格区分，见 K9 `[TRACK-LIST] unreadable` 口径）。
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// 判断一次 <c>track-list</c> 读取是否**读不到**（返回 <c>true</c> = 读不到），并在读不到时留一行可见记录。
	/// <para>🔴 <b>为什么必须区分"读不到"与"列表为空"</b>：<c>track-list</c> 读失败（mpv 未就绪 / 句柄已失效 / 属性类型不符）
	/// 与"这条轨道真的不存在"在返回值上**完全一样**（都得到空列表）⇒ 上层会把"读不到"当成"没有音轨 / 没有字幕轨"，
	/// 于是音轨字幕菜单变空、图片模式误判、<c>aid/sid</c> 回推成 -1，而**日志里一行痕迹都没有**。
	/// 所以读失败必须留痕（默认级别可见），且调用方**不得**据此断言"没有轨"。</para>
	/// </summary>
	private bool IsTrackListReadFailure(MpvError result, MpvFormat format, string reader)
	{
		if (result == MpvError.Success && format == MpvFormat.NodeArray)
		{
			return false;
		}
		_logger.LogWarning("[TRACK-LIST] unreadable reader={Reader} result={Result} format={Format}（读不到 ≠ 列表为空 ⇒ 不得据此断言\"没有轨\"）", reader, result, format);
		return true;
	}

	private bool HasMpvImageVideoTrack()
	{
		if (Client == null)
		{
			return false;
		}
		MpvError probe = MpvNative.GetProperty(Client.Handle, "track-list", MpvFormat.Node, out var data);
		if (IsTrackListReadFailure(probe, data.Format, "HasMpvImageVideoTrack"))
		{
			if (data.Format != MpvFormat.None)
			{
				try
				{
					MpvNative.FreeNodeContents(ref data);
				}
				// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
				// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
				// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
				catch (Exception)
				{
				}
			}
			return false;
		}
		try
		{
			MpvNodeList remoteNodeListValue = data.RemoteNodeListValue;
			int num = Math.Max(0, Math.Min(remoteNodeListValue.Num, 128));
			for (int i = 0; i < num; i++)
			{
				MpvNode node = Marshal.PtrToStructure<MpvNode>(remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>());
				if (node.Format != MpvFormat.NodeMap)
				{
					continue;
				}
				Dictionary<string, MpvNode> map = ReadNodeMap(node);
				if (string.Equals(GetNodeString(map, "type"), "video", StringComparison.OrdinalIgnoreCase))
				{
					string nodeString = GetNodeString(map, "codec");
					if (!string.IsNullOrWhiteSpace(nodeString) && ImageVideoCodecs.Contains(nodeString))
					{
						return true;
					}
				}
			}
			return false;
		}
		finally
		{
			try
			{
				MpvNative.FreeNodeContents(ref data);
			}
			// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
			// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
			// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
			catch (Exception)
			{
			}
		}
	}

	private bool HasMpvVideoTrack()
	{
		if (Client == null)
		{
			return false;
		}
		MpvError probe = MpvNative.GetProperty(Client.Handle, "track-list", MpvFormat.Node, out var data);
		if (IsTrackListReadFailure(probe, data.Format, "HasMpvVideoTrack"))
		{
			if (data.Format != MpvFormat.None)
			{
				try
				{
					MpvNative.FreeNodeContents(ref data);
				}
				// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
				// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
				// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
				catch (Exception)
				{
				}
			}
			return false;
		}
		try
		{
			MpvNodeList remoteNodeListValue = data.RemoteNodeListValue;
			int num = Math.Max(0, Math.Min(remoteNodeListValue.Num, 128));
			for (int i = 0; i < num; i++)
			{
				MpvNode node = Marshal.PtrToStructure<MpvNode>(remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>());
				if (node.Format == MpvFormat.NodeMap && string.Equals(GetNodeString(ReadNodeMap(node), "type"), "video", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
		finally
		{
			try
			{
				MpvNative.FreeNodeContents(ref data);
			}
			// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
			// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
			// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
			catch (Exception)
			{
			}
		}
	}

	private List<MpvAudioTrack> GetMpvAudioTracks()
	{
		List<MpvAudioTrack> list = new List<MpvAudioTrack>();
		if (Client == null)
		{
			return list;
		}
		MpvError property = MpvNative.GetProperty(Client.Handle, "track-list", MpvFormat.Node, out var data);
		_logger.LogDebug($"PlayerViewModel.GetMpvAudioTracks get track-list result={property} format={data.Format}");
		if (IsTrackListReadFailure(property, data.Format, "GetMpvAudioTracks"))
		{
			if (data.Format != MpvFormat.None)
			{
				try
				{
					MpvNative.FreeNodeContents(ref data);
				}
				// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
				// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
				// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
				catch (Exception)
				{
				}
			}
			return list;
		}
		try
		{
			MpvNodeList remoteNodeListValue = data.RemoteNodeListValue;
			int num = Math.Max(0, Math.Min(remoteNodeListValue.Num, 128));
			for (int i = 0; i < num; i++)
			{
				MpvNode node = Marshal.PtrToStructure<MpvNode>(remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>());
				if (node.Format != MpvFormat.NodeMap)
				{
					continue;
				}
				Dictionary<string, MpvNode> map = ReadNodeMap(node);
				if (string.Equals(GetNodeString(map, "type"), "audio", StringComparison.OrdinalIgnoreCase))
				{
					long? nodeInt = GetNodeInt64(map, "id");
					if ((nodeInt.HasValue && nodeInt.GetValueOrDefault() > 0) || 1 == 0)
					{
						string title = GetNodeString(map, "title") ?? string.Empty;
						string language = GetNodeString(map, "lang") ?? string.Empty;
						bool nodeBool = GetNodeBool(map, "selected");
						list.Add(new MpvAudioTrack((int)nodeInt.Value, title, language, nodeBool));
					}
				}
			}
			return list;
		}
		finally
		{
			try
			{
				MpvNative.FreeNodeContents(ref data);
			}
			// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
			// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
			// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
			catch (Exception)
			{
			}
		}
	}

	private List<MpvSubtitleTrack> GetMpvSubtitleTracks()
	{
		List<MpvSubtitleTrack> list = new List<MpvSubtitleTrack>();
		if (Client == null)
		{
			return list;
		}
		MpvError property = MpvNative.GetProperty(Client.Handle, "track-list", MpvFormat.Node, out var data);
		_logger.LogDebug($"PlayerViewModel.GetMpvSubtitleTracks get track-list result={property} format={data.Format}");
		if (IsTrackListReadFailure(property, data.Format, "GetMpvSubtitleTracks"))
		{
			if (data.Format != MpvFormat.None)
			{
				try
				{
					MpvNative.FreeNodeContents(ref data);
				}
				// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
				// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
				// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
				catch (Exception)
				{
				}
			}
			return list;
		}
		try
		{
			MpvNodeList remoteNodeListValue = data.RemoteNodeListValue;
			int num = Math.Max(0, Math.Min(remoteNodeListValue.Num, 128));
			for (int i = 0; i < num; i++)
			{
				MpvNode node = Marshal.PtrToStructure<MpvNode>(remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>());
				if (node.Format != MpvFormat.NodeMap)
				{
					continue;
				}
				Dictionary<string, MpvNode> map = ReadNodeMap(node);
				if (string.Equals(GetNodeString(map, "type"), "sub", StringComparison.OrdinalIgnoreCase))
				{
					long? nodeInt = GetNodeInt64(map, "id");
					if ((nodeInt.HasValue && nodeInt.GetValueOrDefault() > 0) || 1 == 0)
					{
						string title = GetNodeString(map, "title") ?? string.Empty;
						string language = GetNodeString(map, "lang") ?? string.Empty;
						bool nodeBool = GetNodeBool(map, "external");
						bool nodeBool2 = GetNodeBool(map, "selected");
						list.Add(new MpvSubtitleTrack((int)nodeInt.Value, title, language, nodeBool, nodeBool2));
					}
				}
			}
			return list;
		}
		finally
		{
			try
			{
				MpvNative.FreeNodeContents(ref data);
			}
			// t235：命名类型 = `Exception` + **块内理由**：本处是原生内存释放（`MpvNative.FreeNodeContents`）；
			// 释放失败**有意吞掉** —— 该调用在这一族读函数里是错误路径/`finally` 侧的清理动作，
			// 重复释放或句柄已失效时抛错会把"清理"变成"新故障"；本处无用户可控输入、不触日志/数据根/凭据面。
			catch (Exception)
			{
			}
		}
	}

	private static Dictionary<string, MpvNode> ReadNodeMap(MpvNode node)
	{
		Dictionary<string, MpvNode> dictionary = new Dictionary<string, MpvNode>(StringComparer.OrdinalIgnoreCase);
		if (node.Format != MpvFormat.NodeMap)
		{
			return dictionary;
		}
		MpvNodeList remoteNodeListValue = node.RemoteNodeListValue;
		for (int i = 0; i < remoteNodeListValue.Num; i++)
		{
			string text = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(remoteNodeListValue.KeysPointer, i * IntPtr.Size));
			if (!string.IsNullOrWhiteSpace(text))
			{
				nint ptr = remoteNodeListValue.NodesPointer + i * Marshal.SizeOf<MpvNode>();
				dictionary[text] = Marshal.PtrToStructure<MpvNode>(ptr);
			}
		}
		return dictionary;
	}

	private static string? GetNodeString(IReadOnlyDictionary<string, MpvNode> map, string key)
	{
		if (!map.TryGetValue(key, out var value))
		{
			return null;
		}
		return value.Format switch
		{
			MpvFormat.String => value.StringValue,
			MpvFormat.OsdString => value.StringValue,
			_ => null,
		};
	}

	private static long? GetNodeInt64(IReadOnlyDictionary<string, MpvNode> map, string key)
	{
		if (!map.TryGetValue(key, out var value))
		{
			return null;
		}
		return value.Format switch
		{
			MpvFormat.Int64 => value.IntegerValue,
			MpvFormat.Double => (long)value.DoubleValue,
			_ => null,
		};
	}

	private static bool GetNodeBool(IReadOnlyDictionary<string, MpvNode> map, string key)
	{
		if (!map.TryGetValue(key, out var value))
		{
			return false;
		}
		return value.Format switch
		{
			MpvFormat.Flag => value.Flag != 0,
			MpvFormat.Int64 => value.IntegerValue != 0,
			MpvFormat.String => string.Equals(value.StringValue, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value.StringValue, "true", StringComparison.OrdinalIgnoreCase),
			MpvFormat.OsdString => string.Equals(value.StringValue, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value.StringValue, "true", StringComparison.OrdinalIgnoreCase),
			_ => false,
		};
	}

	private static string NormalizeSubtitleText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(value.Trim().ToLowerInvariant(), "\\([^)]*\\)", string.Empty).Replace('-', ' '), "\\s+", " ").Trim();
	}

	private static string BuildSubtitleTrackLabel(MpvSubtitleTrack track)
	{
		string text = track.Title.Trim();
		string readableLanguageName = GetReadableLanguageName(track.Language);
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(readableLanguageName))
		{
			return readableLanguageName + " (" + text + ")";
		}
		if (!string.IsNullOrWhiteSpace(readableLanguageName))
		{
			return readableLanguageName;
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return $"Track {track.Id}";
	}

	private static string GetReadableLanguageName(string? value)
	{
		string text = (value ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		try
		{
			return CultureInfo.GetCultureInfo(text).EnglishName;
		}
		catch (CultureNotFoundException)
		{
			string key = NormalizeSubtitleText(text);
			if (LanguageAliasMap.TryGetValue(key, out HashSet<string> value2))
			{
				string text2 = value2.FirstOrDefault((string alias) => alias.Contains(' ') && !alias.Contains('-', StringComparison.Ordinal));
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text2);
				}
			}
			return text;
		}
	}

	private static Dictionary<string, HashSet<string>> BuildLanguageAliasMap()
	{
		Dictionary<string, HashSet<string>> dictionary = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
		foreach (CultureInfo cultureInfo in cultures)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			AddAlias(hashSet, cultureInfo.TwoLetterISOLanguageName);
			AddAlias(hashSet, cultureInfo.ThreeLetterISOLanguageName);
			AddAlias(hashSet, cultureInfo.ThreeLetterWindowsLanguageName);
			AddAlias(hashSet, cultureInfo.EnglishName);
			AddAlias(hashSet, cultureInfo.NativeName);
			AddAlias(hashSet, cultureInfo.Name);
			foreach (string item in hashSet)
			{
				if (!dictionary.TryGetValue(item, out var value))
				{
					value = (dictionary[item] = new HashSet<string>(StringComparer.Ordinal));
				}
				foreach (string item2 in hashSet)
				{
					value.Add(item2);
				}
			}
		}
		return dictionary;
	}

	private static void AddAlias(HashSet<string> aliases, string? raw)
	{
		string text = NormalizeSubtitleText(raw);
		if (!string.IsNullOrWhiteSpace(text))
		{
			aliases.Add(text);
		}
	}

	internal async Task ReloadForNavigateAsync(HostNavigateOptions options, double? startPositionOverride = null, OverlayTrackOption? preserveAudioTrack = null, OverlayTrackOption? preserveSubtitleTrack = null)
	{
		_allowEpisodeNavigation = true;
		await ApplyHostNavigateOptionsAsync(options);
		if (preserveAudioTrack != null || preserveSubtitleTrack != null)
		{
			ApplyPreservedTrackSelection(preserveAudioTrack, preserveSubtitleTrack);
		}
		OverlayTrackOption overlayTrackOption = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
		(string outboundUserAgent, Dictionary<string, string>? outboundHeaders) = OutboundIdentity.Normalize(options.HttpHeaders);
		MpvPlayOptions options2 = new MpvPlayOptions
		{
			StartPosition = (startPositionOverride ?? options.StartPosition),
			InitialSubtitleId = ((overlayTrackOption != null && overlayTrackOption.IsSpecial) ? "no" : ((!string.IsNullOrWhiteSpace(overlayTrackOption?.Id)) ? overlayTrackOption.Id : options.SubtitleId)),
			ExtraSubtitleUrl = ((overlayTrackOption != null && overlayTrackOption.IsExternal && !string.IsNullOrWhiteSpace(overlayTrackOption.Url)) ? overlayTrackOption.Url : options.SubtitleUrl),
			HttpHeaders = outboundHeaders,
			UserAgent = outboundUserAgent
		};
		await LoadAsync(options.MediaPath, options2);
	}

	internal async Task ApplyHostNavigateOptionsAsync(HostNavigateOptions options)
	{
		if (!string.IsNullOrWhiteSpace(options.CallbackUrl))
		{
			ReportClient = new PlaybackReportClient(options.CallbackUrl, this.Get<ILogger<PlaybackReportClient>>());
		}
		_stopReported = false;
		_hasObservedPlayback = false;
		_hasStartedPlaying = false;
		_loggedProgressReporting = false;
		_hasAppliedInitialTrackSelection = false;
		_pendingNextEpisodeData = null;
		_preloadedAtPlaylistPos = -1L;
		ResetStreamRecoveryState();
		ClearSeamlessStreamRefreshState();
		MediaTitle = options.Title ?? string.Empty;
		MediaSubtitle = options.Subtitle ?? string.Empty;
		MediaMonogram = (string.IsNullOrWhiteSpace(options.Monogram) ? "AI" : options.Monogram);
		MediaLogo = options.Logo ?? string.Empty;
		MediaBackdropUrl = options.BackdropUrl ?? string.Empty;
		PreviousEpisodeId = options.PreviousEpisodeId ?? string.Empty;
		NextEpisodeId = options.NextEpisodeId ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(options.CurrentEpisodeId))
		{
			UpdateEpisodeListCurrent(options.CurrentEpisodeId);
		}
		ApplyMediaBadges(options.Badges);
		VersionOptions.Clear();
		foreach (HostNavigateVersionOption item in options.VersionOptions.Where((HostNavigateVersionOption v) => !string.IsNullOrWhiteSpace(v.Label)))
		{
			VersionOptions.Add(item.ToVersionOption());
		}
		OnPropertyChanged("SelectedVersionLabel");
		AudioTracks.Clear();
		foreach (HostNavigateTrackOption item2 in options.AudioTracks.Where((HostNavigateTrackOption t) => !string.IsNullOrWhiteSpace(t.Label)))
		{
			AudioTracks.Add(item2.ToTrackOption());
		}
		SubtitleTracks.Clear();
		foreach (HostNavigateTrackOption item3 in options.SubtitleTracks.Where((HostNavigateTrackOption t) => !string.IsNullOrWhiteSpace(t.Label)))
		{
			SubtitleTracks.Add(item3.ToTrackOption());
		}
		CaptureHostSubtitleTracks();
		SeasonId = options.SeasonId ?? string.Empty;
		ApplyNavigateSegments(options.Segments);
		if (options.DanmakuMatchName != null)
		{
			ApplyDanmakuMatchNameFromNavigate(options);
		}
	}

	internal void ApplyNavigateSegments(IReadOnlyList<HostNavigateSegmentOption>? segments)
	{
		List<MediaSegment> list = segments?.Select((HostNavigateSegmentOption segment) => segment.ToMediaSegment()).Where((MediaSegment segment) => segment != null).Cast<MediaSegment>()
			.ToList();
		_logger.LogInformation("ApplyNavigateSegments: received={ReceivedCount} parsed={ParsedCount}", segments?.Count ?? 0, list?.Count ?? 0);
		SetActiveSegments((list != null && list.Count > 0) ? list : null);
		_skippedHeadSegments.Clear();
		_skippedTailSegments.Clear();
		if (IsSkipFeatureEnabled)
		{
			PreviewStartTime = 0.0;
			IntroEndTime = _settingsToolkit.ReadLocalSetting(_GetSkipSettingKey("IntroEndTime"), 0.0);
			OutroStartTime = _settingsToolkit.ReadLocalSetting(_GetSkipSettingKey("OutroStartTime"), 0.0);
			_ApplySegmentMarkers();
		}
	}

	/// <summary>
	/// 宿主在**导航回调**里送来的新匹配名。语义与其它通道完全一致：值必须是**原始名**（只 Trim，不解码）。
	/// 值没变时什么都不做 —— 否则每次进度回调都会把浮层的弹幕选择清一遍。
	/// </summary>
	internal void ApplyDanmakuMatchNameFromNavigate(HostNavigateOptions options)
	{
		if (options.DanmakuMatchName != null)
		{
			SetDanmakuMatchNameFromHost(options.DanmakuMatchName, "navigate");
		}
	}

	/// <summary>
	/// **"宿主给内核的弹幕匹配名"的唯一落点**：只 <c>Trim</c>，**绝不解码**。
	/// <para><b>为什么不解码</b>：内核把名字原样交给弹幕服务 —— 匹配请求把它放进 JSON 体（<c>{"fileName": …}</c>，不经 URL 编码），
	/// 搜索请求还会对它再 <c>Uri.EscapeDataString</c> 一次。若这里拿到的是 <c>%E5%A4%8D…</c>，就会**二次编码**
	/// ⇒ 服务端按字面量匹配 ⇒ 一条弹幕也匹配不到，而界面只表现为"没有弹幕"（看不见任何错误）。</para>
	/// <para><b>为什么保留原文而不是顺手反转义</b>：合法的名字里可能含 <c>%</c>（如 <c>100%</c>），
	/// 无条件反转义会把它改成别的字符。所以只**原样保留 + 把"看起来像百分号编码"记进日志**，让传错这件事可见；
	/// "编码/解码"的配对只属于命令行通道，已在解析器的 <c>--danmaku-match-name=</c> 分支完成，不在这里。</para>
	/// </summary>
	internal void SetDanmakuMatchNameFromHost(string? raw, string source)
	{
		string value = (raw ?? string.Empty).Trim();
		bool looksEncoded = LooksPercentEncoded(value);
		_logger.LogInformation("[K7-NAME] source={Source} value={Value} len={Length} looksPercentEncoded={LooksEncoded}", source, value, value.Length, looksEncoded);
		if (looksEncoded && !string.Equals(source, "cli", StringComparison.Ordinal))
		{
			_logger.LogWarning("[K7-WARN] source={Source} 的值看起来是百分号编码；非命令行通道必须给原始名，否则匹配会二次编码而失配", source);
		}
		if (string.Equals(DanmakuMatchName, value, StringComparison.Ordinal))
		{
			return;
		}
		DanmakuMatchName = value;
		ClearDanmakuSelections();
		OnPropertyChanged("DanmakuMatchName");
	}

	/// <summary>
	/// 值里是否含**至少两处** <c>%XX</c> 十六进制转义（CJK 的 UTF-8 转义必然 ≥2 处，故不会把 <c>50%</c> 这类名字误判）。
	/// 只用于告警，**不改写值**。
	/// </summary>
	private static bool LooksPercentEncoded(string value)
	{
		return Regex.IsMatch(value, "%[0-9A-Fa-f]{2}.*%[0-9A-Fa-f]{2}");
	}

	internal void UpdateEpisodeListCurrent(string currentId)
	{
		foreach (EpisodeListItem episode in EpisodeList)
		{
			episode.IsCurrent = string.Equals(episode.Id, currentId, StringComparison.Ordinal);
		}
		NotifyEpisodeListNavigationChanged();
	}

	internal void NotifyEpisodeListNavigationChanged()
	{
		OnPropertyChanged("HasEpisodeList");
		OnPropertyChanged("HasPreviousInEpisodeList");
		OnPropertyChanged("HasNextInEpisodeList");
		OnPropertyChanged("CanNavigatePreviousEpisode");
		OnPropertyChanged("CanNavigateNextEpisode");
	}

	private string _GetSkipSettingKey(string name)
	{
		if (!string.IsNullOrWhiteSpace(SeasonId))
		{
			return name + "_" + SeasonId;
		}
		return name;
	}

	internal void SetActiveSegments(IReadOnlyList<MediaSegment>? segments)
	{
		ActiveSegments.Clear();
		if (segments != null)
		{
			foreach (MediaSegment segment in segments)
			{
				ActiveSegments.Add(segment);
			}
		}
		OnPropertyChanged("HasActiveSegments");
	}

	internal void RefreshSegmentOverlay()
	{
		OnPropertyChanged("HasActiveSegments");
	}

	internal void SetChapters(IReadOnlyList<TodbChapter>? chapters)
	{
		_todbChapters = chapters?.ToList() ?? new List<TodbChapter>();
	}

	internal async void MergeEmbeddedAndTodbChapters()
	{
		if (Client == null)
		{
			_ApplyMergedChapters(_todbChapters);
			return;
		}
		try
		{
			Result<MpvNode> result = await Client.GetPropertyNodeAsync("chapter-list");
			if (result.IsFailed || result.Value.Format != MpvFormat.NodeArray)
			{
				_logger.LogDebug("No embedded chapters found or property unavailable.");
				_ApplyMergedChapters(_todbChapters);
				return;
			}
			MpvNode[] valuesArray = result.Value.RemoteNodeListValue.ValuesArray;
			if (valuesArray == null || valuesArray.Length == 0)
			{
				_logger.LogDebug("Embedded chapter list is empty.");
				_ApplyMergedChapters(_todbChapters);
				return;
			}
			_logger.LogInformation("Found {Count} embedded chapters.", valuesArray.Length);
			List<TodbChapter> list = new List<TodbChapter>();
			for (int i = 0; i < valuesArray.Length; i++)
			{
				MpvNode mpvNode = valuesArray[i];
				if (mpvNode.Format != MpvFormat.NodeMap)
				{
					continue;
				}
				Dictionary<string, MpvNode> valuesMap = mpvNode.RemoteNodeListValue.ValuesMap;
				if (valuesMap == null)
				{
					continue;
				}
				string text = null;
				int timeStart = 0;
				if (valuesMap.TryGetValue("title", out var value) && value.Format == MpvFormat.String)
				{
					text = value.StringValue;
				}
				if (valuesMap.TryGetValue("time", out var value2) && value2.Format == MpvFormat.Double)
				{
					timeStart = (int)value2.DoubleValue;
				}
				int? timeEnd = null;
				if (i < valuesArray.Length - 1)
				{
					MpvNode mpvNode2 = valuesArray[i + 1];
					if (mpvNode2.Format == MpvFormat.NodeMap)
					{
						Dictionary<string, MpvNode> valuesMap2 = mpvNode2.RemoteNodeListValue.ValuesMap;
						if (valuesMap2 != null && valuesMap2.TryGetValue("time", out var value3) && value3.Format == MpvFormat.Double)
						{
							timeEnd = (int)value3.DoubleValue;
						}
					}
				}
				string markerType = _InferChapterType(text);
				list.Add(new TodbChapter
				{
					MarkerId = i,
					MarkerType = markerType,
					Title = (text ?? $"Chapter {i + 1}"),
					TimeStart = timeStart,
					TimeEnd = timeEnd
				});
			}
			List<TodbChapter> chapters = _MergeChapters(list, _todbChapters);
			_ApplyMergedChapters(chapters);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to read embedded chapters.");
			_ApplyMergedChapters(_todbChapters);
		}
	}

	private static List<TodbChapter> _MergeChapters(List<TodbChapter> embedded, List<TodbChapter> todb)
	{
		List<TodbChapter> list = new List<TodbChapter>(embedded);
		foreach (TodbChapter ch in todb)
		{
			if (!list.Any((TodbChapter r) => r.MarkerType == ch.MarkerType && Math.Abs(r.TimeStart - ch.TimeStart) < 5))
			{
				list.Add(ch);
			}
			else if (string.IsNullOrWhiteSpace(list.FirstOrDefault((TodbChapter r) => r.MarkerType == ch.MarkerType && Math.Abs(r.TimeStart - ch.TimeStart) < 5)?.Title) && !string.IsNullOrWhiteSpace(ch.Title))
			{
				TodbChapter todbChapter = list.FirstOrDefault((TodbChapter r) => r.MarkerType == ch.MarkerType && Math.Abs(r.TimeStart - ch.TimeStart) < 5);
				if (todbChapter != null)
				{
					list.Remove(todbChapter);
					list.Add(ch);
				}
			}
		}
		return list.OrderBy((TodbChapter c) => c.TimeStart).ToList();
	}

	private static string _InferChapterType(string? title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return "chapter";
		}
		string text = title.ToLowerInvariant();
		if (text.Contains("intro") || text.Contains("opening") || text.Contains("op ") || text.Contains(" op") || text.Contains("prologue"))
		{
			return "intro";
		}
		if (text.Contains("outro") || text.Contains("ending") || text.Contains("credits") || text.Contains("credit") || text.Contains("ed ") || text.Contains(" ed") || text.Contains("epilogue"))
		{
			return "credits";
		}
		if (text.Contains("recap") || text.Contains("previously") || text.Contains("last time") || text.Contains("回顾"))
		{
			return "recap";
		}
		if (text.Contains("preview") || text.Contains("teaser") || text.Contains("trailer") || text.Contains("next") || text.Contains("预告"))
		{
			return "preview";
		}
		return "chapter";
	}

	private void _ApplyMergedChapters(List<TodbChapter> chapters)
	{
		_queue.TryEnqueue(delegate
		{
			Chapters.Clear();
			foreach (TodbChapter chapter in chapters)
			{
				Chapters.Add(chapter);
			}
			OnPropertyChanged("HasChapters");
		});
	}

	internal async void SetSprite(TodbSprite? sprite)
	{
		Sprite = sprite;
		VttEntries.Clear();
		SpriteImageCache.Clear();
		OnPropertyChanged("HasSprite");
		if (sprite == null || string.IsNullOrWhiteSpace(sprite.VttUrl))
		{
			return;
		}
		try
		{
			using HttpClient client = new HttpClient();
			List<VttSpriteEntry> list = VttSpriteParser.Parse(await client.GetStringAsync(sprite.VttUrl));
			VttEntries.AddRange(list);
			_logger.LogInformation("Parsed {Count} VTT sprite entries", list.Count);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to fetch/parse VTT: {Url}", sprite.VttUrl);
		}
	}

	private void _ApplySegmentMarkers()
	{
		if (ActiveSegments.Count == 0)
		{
			return;
		}
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		foreach (MediaSegment activeSegment in ActiveSegments)
		{
			switch (activeSegment.Type)
			{
				case MediaSegmentType.Intro:
				case MediaSegmentType.Recap:
					{
						double valueOrDefault = activeSegment.EndSeconds.GetValueOrDefault();
						if (valueOrDefault > num)
						{
							num = valueOrDefault;
						}
						break;
					}
				case MediaSegmentType.Preview:
					{
						double startSeconds2 = activeSegment.StartSeconds;
						if (startSeconds2 > 0.0 && (num3 == 0.0 || startSeconds2 < num3))
						{
							num3 = startSeconds2;
						}
						break;
					}
				default:
					{
						double startSeconds = activeSegment.StartSeconds;
						if (startSeconds > 0.0 && (num2 == 0.0 || startSeconds < num2))
						{
							num2 = startSeconds;
						}
						break;
					}
			}
		}
		bool suppressSkipSettingWrites = _suppressSkipSettingWrites;
		_suppressSkipSettingWrites = true;
		if (num > 0.0)
		{
			IntroEndTime = num;
		}
		if (num2 > 0.0)
		{
			OutroStartTime = num2;
		}
		PreviewStartTime = num3;
		_suppressSkipSettingWrites = suppressSkipSettingWrites;
	}

	private void CheckAutoSkip(double position)
	{
		if (!IsSkipFeatureEnabled || Client == null || IsFileLoading || !IsPlaying || position <= 0.0)
		{
			return;
		}
		CheckPreload(position);
		if (AutoSkipIntro || AutoSkipRecap)
		{
			foreach (MediaSegment item3 in CollectHeadSegments())
			{
				if (!IsHeadSegmentEnabled(item3))
				{
					continue;
				}
				double valueOrDefault = item3.EndSeconds.GetValueOrDefault();
				if (!(valueOrDefault <= 0.0))
				{
					(long, long) item = (item3.StartMs, item3.EndMs.GetValueOrDefault());
					if (!_skippedHeadSegments.Contains(item) && position >= item3.StartSeconds && position < valueOrDefault)
					{
						_skippedHeadSegments.Add(item);
						Client.SetCurrentPositionAsync(valueOrDefault);
						return;
					}
				}
			}
		}
		if (!AutoSkipOutro || !(Duration > 0.0))
		{
			return;
		}
		foreach (MediaSegment item4 in CollectTailSegments())
		{
			if (!IsTailSegmentEnabled(item4))
			{
				continue;
			}
			double num = ResolveTailSkipTarget(item4, Duration);
			if (!(num <= item4.StartSeconds))
			{
				(long, long) item2 = (item4.StartMs, item4.EndMs.GetValueOrDefault());
				if (!_skippedTailSegments.Contains(item2) && position >= item4.StartSeconds && position < num)
				{
					_skippedTailSegments.Add(item2);
					Client.SetCurrentPositionAsync(num);
					break;
				}
			}
		}
	}

	private void CheckPreload(double position)
	{
		if (!(Duration <= 0.0) && !_isPreloading && _pendingNextEpisodeData == null)
		{
			_ = CanNavigateNextEpisode;
		}
	}

	private List<MediaSegment> CollectHeadSegments()
	{
		List<MediaSegment> list = new List<MediaSegment>();
		foreach (MediaSegment activeSegment in ActiveSegments)
		{
			MediaSegmentType type = activeSegment.Type;
			bool flag = (uint)type <= 1u;
			if (flag && !(activeSegment.EndSeconds.GetValueOrDefault() <= activeSegment.StartSeconds))
			{
				list.Add(activeSegment);
			}
		}
		if (list.Count == 0 && IntroEndTime > 0.0)
		{
			list.Add(new MediaSegment
			{
				Type = MediaSegmentType.Intro,
				StartMs = 0L,
				EndMs = (long)(IntroEndTime * 1000.0),
				Source = "manual"
			});
		}
		list.Sort((MediaSegment a, MediaSegment b) => a.StartMs.CompareTo(b.StartMs));
		return list;
	}

	private bool IsHeadSegmentEnabled(MediaSegment seg)
	{
		return seg.Type switch
		{
			MediaSegmentType.Intro => AutoSkipIntro,
			MediaSegmentType.Recap => AutoSkipRecap,
			_ => false,
		};
	}

	private List<MediaSegment> CollectTailSegments()
	{
		List<MediaSegment> list = new List<MediaSegment>();
		foreach (MediaSegment activeSegment in ActiveSegments)
		{
			MediaSegmentType type = activeSegment.Type;
			bool flag = (uint)(type - 2) <= 1u;
			if (flag && !(activeSegment.StartSeconds <= 0.0))
			{
				list.Add(activeSegment);
			}
		}
		if (list.Count == 0 && OutroStartTime > 0.0)
		{
			list.Add(new MediaSegment
			{
				Type = MediaSegmentType.Credits,
				StartMs = (long)(OutroStartTime * 1000.0),
				EndMs = null,
				Source = "manual"
			});
		}
		list.Sort((MediaSegment a, MediaSegment b) => a.StartMs.CompareTo(b.StartMs));
		return list;
	}

	private bool IsTailSegmentEnabled(MediaSegment seg)
	{
		return seg.Type switch
		{
			MediaSegmentType.Credits => AutoSkipOutro,
			MediaSegmentType.Preview => AutoSkipOutro && AutoSkipPreview,
			_ => false,
		};
	}

	private static double ResolveTailSkipTarget(MediaSegment seg, double duration)
	{
		double? endSeconds = seg.EndSeconds;
		if (endSeconds.HasValue && endSeconds.GetValueOrDefault() > 0.0 && endSeconds.Value > seg.StartSeconds)
		{
			return endSeconds.Value;
		}
		if (!(duration > 0.0))
		{
			return seg.StartSeconds;
		}
		return Math.Max(seg.StartSeconds, duration - 1.0);
	}

	private double ComputeInitialSkipPosition()
	{
		List<MediaSegment> list = CollectHeadSegments();
		if (list.Count == 0)
		{
			return 0.0;
		}
		double num = 0.0;
		bool flag;
		do
		{
			flag = false;
			foreach (MediaSegment item in list)
			{
				if (IsHeadSegmentEnabled(item))
				{
					double valueOrDefault = item.EndSeconds.GetValueOrDefault();
					if (!(valueOrDefault <= num) && item.StartSeconds <= num + 0.5)
					{
						num = valueOrDefault;
						flag = true;
					}
				}
			}
		}
		while (flag);
		return num;
	}

	private RectInt32 GetRenderRect(DisplayArea displayArea)
	{
		RectInt32 workArea = displayArea.WorkArea;
		Windows.Win32.PInvoke.GetDpiForMonitor(new Windows.Win32.Graphics.Gdi.HMONITOR(Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId)), Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var _);
		double num = ((dpiX != 0) ? (dpiX / 96.0) : (Windows.Win32.PInvoke.GetDpiForWindow(new Windows.Win32.Foundation.HWND(Window.Handle)) / 96.0));
		double num2 = _settingsToolkit.ReadLocalSetting("PlayerWindowWidth", 1120.0);
		double num3 = _settingsToolkit.ReadLocalSetting("PlayerWindowHeight", 740.0);
		_logger.LogDebug("GetRenderRect read settings: width={Width}, height={Height}, scale={Scale}", num2, num3, num);
		int num4 = Convert.ToInt32(num2 * num);
		int num5 = Convert.ToInt32(num3 * num);
		if (num5 > workArea.Height - 20)
		{
			num5 = workArea.Height - 20;
		}
		PointInt32 savedWindowPosition = GetSavedWindowPosition();
		_logger.LogDebug("GetRenderRect lastPoint: {Point}", savedWindowPosition);
		bool num6 = savedWindowPosition.X == 0 && savedWindowPosition.Y == 0;
		int num7 = Math.Max(workArea.X, workArea.X + workArea.Width - num4);
		int num8 = Math.Max(workArea.Y, workArea.Y + workArea.Height - num5);
		bool flag = savedWindowPosition.X >= workArea.X && savedWindowPosition.Y >= workArea.Y && savedWindowPosition.X <= num7 && savedWindowPosition.Y <= num8;
		double value = ((num6 || !flag) ? (workArea.X + (workArea.Width - num4) / 2.0) : savedWindowPosition.X);
		double value2 = ((num6 || !flag) ? (workArea.Y + (workArea.Height - num5) / 2.0) : savedWindowPosition.Y);
		RectInt32 rectInt = new RectInt32(Convert.ToInt32(value), Convert.ToInt32(value2), num4, num5);
		_logger.LogDebug("GetRenderRect returning rect: {Rect}", rectInt);
		return rectInt;
	}

	private void SaveCurrentWindowStats()
	{
		if (IsFullScreen || IsCompactOverlay || Window == null || _isResizingToVideo)
		{
			return;
		}
		AppWindow window = Window.GetWindow();
		Windows.Win32.Foundation.HWND hWND = new Windows.Win32.Foundation.HWND(Window.Handle);
		double num = Windows.Win32.PInvoke.GetDpiForWindow(hWND) / 96.0;
		int x = window.Position.X;
		int y = window.Position.Y;
		Windows.Win32.Foundation.BOOL bOOL = Windows.Win32.PInvoke.IsZoomed(hWND);
		_settingsToolkit.WriteLocalSetting("IsPlayerWindowMaximized", (bool)bOOL);
		if (!bOOL)
		{
			_settingsToolkit.WriteLocalSetting("PlayerWindowPositionLeft", x);
			_settingsToolkit.WriteLocalSetting("PlayerWindowPositionTop", y);
			if (!FitVideoSize && window.Size.Height >= 480 && window.Size.Width >= 640)
			{
				double num2 = window.Size.Width / num;
				double num3 = window.Size.Height / num;
				_logger.LogDebug("SaveCurrentWindowStats saving: w={Width}, h={Height}, scale={Scale}", num2, num3, num);
				_settingsToolkit.WriteLocalSetting("PlayerWindowHeight", num3);
				_settingsToolkit.WriteLocalSetting("PlayerWindowWidth", num2);
			}
		}
	}

	private PointInt32 GetSavedWindowPosition()
	{
		int x = _settingsToolkit.ReadLocalSetting("PlayerWindowPositionLeft", 0);
		int y = _settingsToolkit.ReadLocalSetting("PlayerWindowPositionTop", 0);
		return new PointInt32(x, y);
	}

	internal void PersistFitVideoSizePreference()
	{
		_settingsToolkit.WriteLocalSetting("FitVideoSize", FitVideoSize);
		_settingsToolkit.WriteLocalSetting("UserPreferredSizeSet", !FitVideoSize);
	}

	private void CheckBackdropVisible()
	{
		bool flag = IsFileLoading;
		if (!flag)
		{
			MpvPlayerState lastState = LastState;
			bool flag2 = (uint)(lastState - 3) <= 1u;
			flag = flag2 && CurrentPosition <= 0.5;
		}
		IsBackdropVisible = flag || IsIdle || IsSourceLoading;
	}

	internal void UpdateWindowState()
	{
		if (Window != null)
		{
			IsWindowMaximized = Windows.Win32.PInvoke.IsZoomed(new Windows.Win32.Foundation.HWND(Window.Handle));
		}
	}

	private static string BuildMediaSubtitle(string path, string title)
	{
		string fileName = Path.GetFileName(Path.GetDirectoryName(path));
		if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, title, StringComparison.OrdinalIgnoreCase))
		{
			return fileName;
		}
		return Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
	}

	private void TryScheduleProgressReport(bool force = false)
	{
		if (_stopReported || ReportClient == null || !ReportClient.IsEnabled || IsFileLoading || !_hasStartedPlaying)
		{
			return;
		}
		double position = Math.Max(0.0, CurrentPosition);
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (!force && !(_lastProgressReportAt == DateTimeOffset.MinValue) && !(utcNow - _lastProgressReportAt >= TimeSpan.FromSeconds(5L)) && !(Math.Abs(position - _lastReportedPosition) >= 5.0))
		{
			return;
		}
		_lastProgressReportAt = utcNow;
		_lastReportedPosition = position;
		if (!_loggedProgressReporting)
		{
			_loggedProgressReporting = true;
			_logger.LogInformation("Progress reporting started at {Position}s", position);
		}
		bool isPlaying = IsPlaying;
		PlaybackStateSnapshot snapshot = BuildPlaybackStateSnapshot();
		bool alreadyPreloaded = _pendingNextEpisodeData != null;
		Task.Run(async delegate
		{
			_ = 2;
			try
			{
				HostNavigateOptions options = await ReportClient.ReportProgressAsync(position, !isPlaying, snapshot, UpdateUrl);
				if (options != null && !alreadyPreloaded && AutoPlayNextEpisode)
				{
					_logger.LogInformation("Received preload options for {MediaPath}. StartPosition={StartPosition}", options.MediaPath, options.StartPosition);
					TaskCompletionSource pendingSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
					_queue.TryEnqueue(delegate
					{
						if (_pendingNextEpisodeData == null)
						{
							_pendingNextEpisodeData = options;
						}
						pendingSet.TrySetResult();
					});
					await pendingSet.Task.ConfigureAwait(continueOnCapturedContext: false);
					if (Client != null && !string.IsNullOrEmpty(options.MediaPath))
					{
						Result result = await Client.AppendToPlaylistAsync(options.MediaPath);
						if (result.IsSuccess)
						{
							_queue.TryEnqueue(delegate
							{
								if (TryGetPlaylistPos(out var pos))
								{
									_preloadedAtPlaylistPos = pos;
								}
							});
							_logger.LogInformation("Appended next episode to mpv playlist: {MediaPath}", options.MediaPath);
						}
						else
						{
							string text = result.Errors.FirstOrDefault()?.Message ?? "unknown error";
							_logger.LogWarning("Failed to append next episode to mpv playlist ({Error}): {MediaPath}", text, options.MediaPath);
						}
					}
				}
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Failed to handle preload response from progress report");
			}
		});
	}

	private PlaybackStateSnapshot BuildPlaybackStateSnapshot()
	{
		int audioStreamIndex = AudioTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected && !p.IsSpecial)?.EmbyStreamIndex ?? (-1);
		OverlayTrackOption overlayTrackOption = SubtitleTracks.FirstOrDefault((OverlayTrackOption p) => p.IsSelected);
		int subtitleStreamIndex = ((overlayTrackOption == null || overlayTrackOption.IsSpecial) ? (-1) : overlayTrackOption.EmbyStreamIndex);
		double subtitleOffset = 0.0;
		if (Client != null && MpvNative.GetPropertyDouble(Client.Handle, "sub-delay", MpvFormat.Double, out var data) == MpvError.Success)
		{
			subtitleOffset = data;
		}
		// K-05（t162）：**「音量真为 0」与「读 mpv 失败」必须不同形** —— 旧实现兜底成 UI 音量，
		//   再用 `if (mpvVolume > 0)` 判读 ⇒ mpv 真为 0 时被当成读失败、静默回推 UI 音量，并把 IsMuted 强制成 false。
		//   现在：读失败 = `null`（可判），读到 0 = `0`（合法值）；静音状态另读 mpv 的 `mute` 标志（`null` = 读不到）。
		int uiVolume = (int)Math.Round(Math.Clamp(Volume, 0.0, MaxVolume));
		// 回推的 speed / volume **以 mpv 为准**（浮层改、外壳改，最终都是 mpv 的值）。
		(double mpvSpeed, double mpvSubDelay, int? mpvVolume, long mpvAid, long mpvSid, bool? mpvSubVisible, bool? mpvMuted) = ReadMpvState();
		bool volumeFromMpv = mpvVolume.HasValue;
		int volumeLevel = mpvVolume ?? uiVolume;
		// IsMuted：优先 mpv 的 `mute` 真值；读不到才退化，且退化来源**逐字打在日志里**（不静默、不伪造）。
		bool isMuted = mpvMuted ?? (volumeFromMpv && mpvVolume.Value <= 0);
		string volumeSource = volumeFromMpv ? "mpv" : "ui-fallback(read-failed)";
		string mutedSource = mpvMuted.HasValue ? "mpv" : (volumeFromMpv ? "derived(mpv volume==0)" : "derived(ui-fallback)");
		_logger.LogInformation("[K3-STATE] speed={Speed} subDelay={SubDelay} volume={Volume} volumeSource={VolumeSource} isMuted={IsMuted} mutedSource={MutedSource} aid={Aid} sid={Sid} subVisible={SubVisible} embyAudio={Audio} embySub={Sub}", mpvSpeed, mpvSubDelay, volumeLevel, volumeSource, isMuted, mutedSource, mpvAid, mpvSid, ((object)mpvSubVisible ?? "(unknown)"), audioStreamIndex, subtitleStreamIndex);
		return new PlaybackStateSnapshot(audioStreamIndex, subtitleStreamIndex, mpvSubDelay, volumeLevel, isMuted, mpvSpeed)
		{
			DurationSeconds = Duration,
			// 把 mpv 的 aid/sid **真实值**一并回推 —— 否则外壳拿不到它们，
			// ⇒「单一真相源 = mpv」在外壳侧无从落地。既有字段语义不变（Emby 流序号仍在 audioStreamIndex/subtitleStreamIndex）。
			MpvAudioTrackId = mpvAid,
			MpvSubtitleTrackId = mpvSid,
			// sub-visibility 真实值（null = 读不到，外壳当"未知"）⇒「关了字幕外壳看得出」。
			MpvSubtitleVisible = mpvSubVisible
		};
	}

	internal Task TryReportStoppedAsync()
	{
		if (_stopReported || ReportClient == null || !ReportClient.IsEnabled)
		{
			return Task.CompletedTask;
		}
		if (!_hasObservedPlayback)
		{
			_stopReported = true;
			_logger.LogInformation("Skipping Stopped report: no playback observed yet.");
			return Task.CompletedTask;
		}
		_stopReported = true;
		_logger.LogInformation("Stopped report sent: position={Position}s", Math.Max(0.0, CurrentPosition));
		return ReportClient.ReportStoppedAsync(Math.Max(0.0, CurrentPosition), BuildPlaybackStateSnapshot());
	}

	private void TryScheduleStoppedReport()
	{
		TryReportStoppedAsync();
	}

	internal void StartNetworkPollTimer()
	{
		if (_networkPollTimer is null)
		{
			_networkPollTimer = _queue.CreateTimer();
			_networkPollTimer.Interval = TimeSpan.FromSeconds(1L);
			_networkPollTimer.Tick += OnNetworkPollTimerTick;
			_networkPollTimer.Start();
		}
	}

	internal void StopNetworkPollTimer()
	{
		if (_networkPollTimer is not null)
		{
			_networkPollTimer.Stop();
			_networkPollTimer.Tick -= OnNetworkPollTimerTick;
			_networkPollTimer = null;
			if (!_isGaplessTransitioning && !_isStreamRefreshTransitioning)
			{
				BufferedPosition = 0.0;
			}
			IsNetworkSpeedVisible = false;
			NetworkSpeedText = string.Empty;
			_lastCacheSpeed = 0.0;
		}
	}

	private void OnNetworkPollTimerTick(DispatcherQueueTimer sender, object args)
	{
		if (Client != null)
		{
			double num = 0.0;
			MpvError propertyDouble = MpvNative.GetPropertyDouble(Client.Handle, "demuxer-cache-time", MpvFormat.Double, out var data);
			if (propertyDouble == MpvError.Success)
			{
				num = data;
			}
			if (num <= 0.0 && MpvNative.GetPropertyDouble(Client.Handle, "demuxer-cache-duration", MpvFormat.Double, out var data2) == MpvError.Success)
			{
				num = Math.Max(0.0, CurrentPosition) + Math.Max(0.0, data2);
			}
			if (num <= 0.0)
			{
				num = Math.Max(0.0, CurrentPosition);
			}
			BufferedPosition = ((Duration > 0.0) ? Math.Clamp(num, 0.0, Duration) : 0.0);
			MpvError propertyDouble2 = MpvNative.GetPropertyDouble(Client.Handle, "cache-speed", MpvFormat.Double, out var data3);
			long num2 = 0L;
			long data4;
			if (propertyDouble2 == MpvError.Success)
			{
				num2 = (long)Math.Max(0.0, data3);
			}
			else if (MpvNative.GetPropertyInt64(Client.Handle, "cache-speed", MpvFormat.Int64, out data4) == MpvError.Success)
			{
				num2 = Math.Max(0L, data4);
			}
			IsNetworkSpeedVisible = !IsIdle;
			NetworkSpeedText = FormatNetworkSpeed(num2);
			_lastCacheSpeed = num2;
			_logger.LogDebug($"PlayerViewModel.NetworkPoll cacheErr={propertyDouble} cache={BufferedPosition:0.###} duration={Duration:0.###} speedErr={propertyDouble2} speed={num2} rawDouble={data3:0.###} visible={IsNetworkSpeedVisible}");
			TryPerformStreamRefreshHandoff("cache-low");
		}
	}

	private static string FormatNetworkSpeed(long bytesPerSec)
	{
		if (bytesPerSec <= 0)
		{
			return "0 B/s";
		}
		if (bytesPerSec >= 1024)
		{
			double num = bytesPerSec / 1024.0;
			if (!(num < 1024.0))
			{
				double num2 = num / 1024.0;
				if (!(num2 < 1024.0))
				{
					double value = num2 / 1024.0;
					return $"{value:0.#} GB/s";
				}
				return $"{num2:0.#} MB/s";
			}
			return $"{num:0.#} KB/s";
		}
		return $"{bytesPerSec:0} B/s";
	}

	private void OnFileLoaded(object? sender, EventArgs e)
	{
		_queue.TryEnqueue(delegate
		{
			try
			{
				_logger.LogInformation("[StartupDiag] OnFileLoaded path={Path}", GetMpvCurrentPath() ?? string.Empty);
				_logger.LogDebug("PlayerViewModel.OnFileLoaded");
				if (_streamRefreshHandoffInProgress && _pendingStreamRefreshData != null)
				{
					if (IsTransientPlaybackLoad())
					{
						_logger.LogDebug("PlayerViewModel.OnFileLoaded ignored transient load during stream refresh handoff.");
					}
					else
					{
						_streamRefreshHandoffInProgress = false;
						HostNavigateOptions pendingStreamRefreshData = _pendingStreamRefreshData;
						_pendingStreamRefreshData = null;
						_streamRefreshAppended = false;
						double streamRefreshHandoffPosition = _streamRefreshHandoffPosition;
						_logger.LogInformation("Stream refresh handoff detected. Applying refreshed URL for {MediaPath}.", pendingStreamRefreshData.MediaPath);
						FinishStreamRefreshHandoffAsync(pendingStreamRefreshData, streamRefreshHandoffPosition);
					}
				}
				else if (_pendingNextEpisodeData != null && _gaplessHandoffInProgress)
				{
					if (!AutoPlayNextEpisode)
					{
						_logger.LogInformation("Gapless transition blocked: auto-play next episode disabled.");
						_gaplessHandoffInProgress = false;
						_pendingNextEpisodeData = null;
						_isGaplessTransitioning = false;
						if (Client != null)
						{
							Client.PlaylistPrevAsync();
						}
						CompleteFileLoadedUi();
					}
					else
					{
						_isGaplessTransitioning = true;
						_gaplessHandoffInProgress = false;
						HostNavigateOptions pendingNextEpisodeData = _pendingNextEpisodeData;
						_pendingNextEpisodeData = null;
						_logger.LogInformation("Gapless transition detected. Applying metadata for {MediaPath}. StartPosition={StartPosition}", pendingNextEpisodeData.MediaPath, pendingNextEpisodeData.StartPosition);
						FinishGaplessFileLoadedAsync(pendingNextEpisodeData);
					}
				}
				else
				{
					if (_gaplessHandoffInProgress)
					{
						_gaplessHandoffInProgress = false;
						_logger.LogWarning("Gapless handoff finished without preload metadata; falling back to normal load UI.");
					}
					_isGaplessTransitioning = false;
					if (IsTransientPlaybackLoad())
					{
						_logger.LogDebug("PlayerViewModel.OnFileLoaded ignored transient load during stream recovery.");
					}
					else
					{
						CompleteFileLoadedUi();
						if (_awaitingStreamRecoveryLoad)
						{
							ResetStreamRecoveryState();
						}
						ApplyInitialStartPositionAsync();
					}
				}
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "Unhandled exception in OnFileLoaded");
			}
		});
	}

	private async Task HandlePlaybackEndAutoAdvanceAsync()
	{
		_ = 2;
		try
		{
			if (_pendingNextEpisodeData != null)
			{
				if (_gaplessHandoffInProgress || _isGaplessTransitioning)
				{
					_logger.LogDebug("Gapless transition already in progress at End, skipping playlist-next.");
				}
				else if (HasMpvAutoAdvancedToPreloadedEntry())
				{
					_logger.LogInformation("Playback ended after mpv EOF auto-advance to preloaded entry.");
					await TryApplyPendingGaplessMetadataIfNeededAsync("EOF auto-advance");
				}
				else
				{
					_logger.LogInformation("Playback ended, seamlessly jumping to preloaded next episode via playlist-next.");
					await HandoffToNextPlaylistEntryAsync();
				}
			}
			else
			{
				_logger.LogInformation("Playback ended, auto-playing next episode via manual reload.");
				await NavigateNextEpisodeAsync();
			}
		}
		finally
		{
			_autoAdvanceInProgress = false;
		}
	}

	private async Task FinishGaplessFileLoadedAsync(HostNavigateOptions pending)
	{
		try
		{
			await ApplyGaplessTransitionAsync(pending).ConfigureAwait(continueOnCapturedContext: true);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Gapless transition metadata apply failed");
		}
		finally
		{
			_isGaplessTransitioning = false;
			_queue.TryEnqueue(CompleteFileLoadedUi);
		}
	}

	private void CompleteFileLoadedUi()
	{
		IsFileLoading = false;
		if (FitVideoSize && !IsFullScreen && !IsCompactOverlay)
		{
			_logger.LogDebug("PlayerViewModel.OnFileLoaded before ResizeWindowToVideo");
			ResizeWindowToVideo();
		}
		else
		{
			_logger.LogDebug("PlayerViewModel.OnFileLoaded FitVideoSize=false, skip resize");
		}
		_logger.LogDebug("PlayerViewModel.OnFileLoaded before EnsureWindowShown");
		EnsureWindowShown();
		Window?.ActivateForKeyboardInput();
		RefreshSegmentOverlay();
		ApplyInitialTrackSelectionAndRefreshAsync();
		StartNetworkPollTimer();
		MergeEmbeddedAndTodbChapters();
	}

	private void OnFileLoading(object? sender, EventArgs e)
	{
		_queue.TryEnqueue(delegate
		{
			_logger.LogInformation("[StartupDiag] OnFileLoading path={Path}", GetMpvCurrentPath() ?? string.Empty);
			_logger.LogDebug("PlayerViewModel.OnFileLoading");
			if (ShouldIgnoreMpvLoad())
			{
				SuppressSpuriousMpvLoad(GetMpvCurrentPath());
				_logger.LogDebug("PlayerViewModel.OnFileLoading ignored spurious load (path={Path}).", GetMpvCurrentPath() ?? string.Empty);
			}
			else if (_streamRefreshHandoffInProgress)
			{
				_isStreamRefreshTransitioning = true;
				_lastEndFileReason = null;
			}
			else if (!AutoPlayNextEpisode && !_allowEpisodeNavigation && _hasStartedPlaying && (_pendingNextEpisodeData != null || HasTrailingPlaylistEntries()))
			{
				_logger.LogInformation("Blocking automatic episode transition: auto-play next episode disabled.");
				_pendingNextEpisodeData = null;
				_preloadedAtPlaylistPos = -1L;
				_isGaplessTransitioning = false;
				if (Client != null)
				{
					Task.Run(async delegate
					{
						await Client.PlaylistPrevAsync();
						await Client.ClearTrailingPlaylistEntriesAsync();
					});
				}
			}
			else
			{
				if (_pendingNextEpisodeData != null && !_gaplessHandoffInProgress)
				{
					bool flag = TryDetectGaplessAutoAdvance();
					if (!flag && HasTrailingPlaylistEntries())
					{
						_logger.LogInformation("Gapless transition assumed (pending data + trailing entries, playlist-pos may lag).");
						flag = true;
					}
					if (flag)
					{
						_gaplessHandoffInProgress = true;
					}
				}
				_allowEpisodeNavigation = false;
				_isGaplessTransitioning = _pendingNextEpisodeData != null && _gaplessHandoffInProgress;
				IsFileLoading = true;
				IsSourceLoading = false;
				_lastEndFileReason = null;
				if (!_isGaplessTransitioning && Client != null)
				{
					Client.ResumeAsync();
				}
			}
		});
	}

	private void OnFileEnd(object? sender, MpvEventEndFile e)
	{
		_queue.TryEnqueue(delegate
		{
			_logger.LogWarning($"PlayerViewModel.OnFileEnd reason={e.Reason} error={e.Error}");
			_lastEndFileReason = e.Reason;
		});
	}

	private void OnStreamHttpError(object? sender, int statusCode)
	{
		_queue.TryEnqueue(async delegate
		{
			await HandleStreamTransportErrorAsync(statusCode);
		});
	}

	private void OnClientDataNotify(object? sender, MpvClientNotifyEventArgs e)
	{
		_queue.TryEnqueue(delegate
		{
			switch (e.Id)
			{
				case MpvClientEventId.StateChanged:
					_logger.LogDebug($"PlayerViewModel.StateChanged={(MpvPlayerState)e.Data}");
					HandleStateChanged((MpvPlayerState)e.Data);
					if ((MpvPlayerState)e.Data == MpvPlayerState.End)
					{
						if (ShouldIgnoreMpvLoad() || _awaitingStreamRecoveryLoad)
						{
							_logger.LogDebug("Ignoring End state during spurious/recovery load (path={Path}).", GetMpvCurrentPath() ?? string.Empty);
						}
						else if (_lastEndFileReason == MpvEndFileReason.Error)
						{
							_logger.LogWarning("Playback ended with load error, keeping window open for user retry.");
						}
						else if (AutoPlayNextEpisode && (HasNextEpisode || HasNextInEpisodeList))
						{
							if (_autoAdvanceInProgress)
							{
								_logger.LogDebug("Playback ended auto-advance already in progress, skipping duplicate End event.");
							}
							else
							{
								_autoAdvanceInProgress = true;
								_allowEpisodeNavigation = true;
								HandlePlaybackEndAutoAdvanceAsync();
							}
						}
						else if (!AutoPlayNextEpisode && (HasNextEpisode || HasNextInEpisodeList))
						{
							_logger.LogInformation("Playback ended, auto-play next episode disabled, staying at end.");
							_pendingNextEpisodeData = null;
							if (Client != null)
							{
								Client.ClearTrailingPlaylistEntriesAsync();
							}
						}
						else if (ShouldAttemptStreamRecovery())
						{
							_logger.LogWarning("Playback ended early (position={Position}s, duration={Duration}s) after stream error; attempting URL refresh.", CurrentPosition, Duration);
							HandleStreamTransportErrorAsync(0);
						}
						else if (IsPrematurePlaybackEnd() || _streamAccessErrorDetected)
						{
							_logger.LogWarning("Playback ended early (position={Position}s, duration={Duration}s); keeping player open for retry.", CurrentPosition, Duration);
						}
						else if (!_isClosingWindow)
						{
							_logger.LogInformation("Playback ended, no next episode, closing window.");
							CloseWindowAsync();
						}
					}
					else
					{
						TryScheduleProgressReport(force: true);
					}
					break;
				case MpvClientEventId.VolumeChanged:
					{
						double num2 = (double)e.Data;
						if (Math.Abs(num2 - Volume) >= 1.0)
						{
							Volume = num2;
						}
						break;
					}
				case MpvClientEventId.SpeedChanged:
					{
						double num = (double)e.Data;
						if (Math.Abs(num - Speed) >= 0.1)
						{
							Speed = num;
						}
						break;
					}
				case MpvClientEventId.DurationChanged:
					Duration = (double)e.Data;
					break;
				case MpvClientEventId.PositionChanged:
					CurrentPosition = (double)e.Data;
					if (CurrentPosition > 0.0)
					{
						_hasObservedPlayback = true;
					}
					if (!IsProgressChanging)
					{
						IsProgressChanging = false;
					}
					TryScheduleProgressReport();
					break;
				case MpvClientEventId.FullScreenChanged:
					ApplyFullScreenWindowState((bool)e.Data);
					break;
				case MpvClientEventId.CompactOverlayChanged:
					IsCompactOverlay = (bool)e.Data;
					ApplyCompactOverlayWindowState(IsCompactOverlay);
					UpdateWindowState();
					break;
			}
		});
	}

	private void OnUINotify(object? sender, MpvUINotifyEventArgs e)
	{
		_queue.TryEnqueue(delegate
		{
			switch (e.Id)
			{
				case MpvUIEventId.PreviewPositionChanged:
					if (e.Data is double previewPosition)
					{
						PreviewPosition = previewPosition;
						IsProgressChanging = true;
					}
					break;
				case MpvUIEventId.PreviewScrubCommit:
					if (e.Data is double num)
					{
						PreviewPosition = num;
						SeekToCommand.ExecuteAsync(num);
						IsProgressChanging = false;
					}
					break;
				case MpvUIEventId.PreviewScrubCancel:
					IsProgressChanging = false;
					break;
				case MpvUIEventId.VolumeChanged:
					if (e.Data is double value)
					{
						Volume = Math.Clamp(value, 0.0, MaxVolume);
						if (Volume > 0.0)
						{
							_settingsToolkit.WriteLocalSetting("PlayerVolume", Volume);
						}
					}
					ShowVolumeTip();
					break;
				case MpvUIEventId.Tapped:
					IsControlVisible = !IsControlVisible;
					break;
				case MpvUIEventId.SubtitleFilesDropped:
					if (e.Data is IReadOnlyList<string> paths)
					{
						LoadDroppedSubtitleFilesAsync(paths);
					}
					break;
				case MpvUIEventId.StateChecked:
				case MpvUIEventId.PointerMoved:
				case MpvUIEventId.KeyPressed:
				case MpvUIEventId.DoubleTapped:
				case MpvUIEventId.KeyReleased:
				case MpvUIEventId.HoldSpeedStart:
				case MpvUIEventId.HoldSpeedEnd:
					break;
			}
		});
	}

	private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
	{
		args.Cancel = true;
		CloseWindowAsync();
	}

	private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
	{
		_logger.LogDebug("OnAppWindowChanged: sizeChange={SizeChange}, visChange={VisChange}, presChange={PresChange}, resizingToVideo={Resizing}", args.DidSizeChange, args.DidVisibilityChange, args.DidPresenterChange, _isResizingToVideo);
		if (args.DidPresenterChange || args.DidSizeChange)
		{
			UpdateWindowState();
		}
		if (args.DidSizeChange && IsFullScreen && Window != null)
		{
			ApplyFullScreenMonitorBounds();
			Window.SetFullBleedChrome(enabled: true);
		}
		if (!args.DidSizeChange)
		{
			return;
		}
		if (_initWindowTargetRect.HasValue && Window != null && !IsFullScreen)
		{
			SizeInt32 size = sender.Size;
			RectInt32 value = _initWindowTargetRect.Value;
			if (size.Width != value.Width || size.Height != value.Height)
			{
				_logger.LogDebug("OnAppWindowChanged: init settling resize {W}x{H} → re-applying target {TW}x{TH}", size.Width, size.Height, value.Width, value.Height);
				SetWindowPos(Window.Handle, IntPtr.Zero, value.X, value.Y, value.Width, value.Height, 20u);
			}
		}
		else
		{
			if (VideoFitMode == VideoFitMode.Stretch)
			{
				ApplyVideoFitModeAsync();
			}
			if (!_isResizingToVideo && FitVideoSize)
			{
				FitVideoSize = false;
				PersistFitVideoSizePreference();
				ReportClient?.ReportManualResizeAsync();
			}
			SaveCurrentWindowStats();
		}
	}

	private void OnTipTimerTick(DispatcherQueueTimer sender, object args)
	{
		IsVolumeChanging = false;
	}

	private void ShowVolumeTip()
	{
		IsVolumeChanging = true;
		_tipTimer?.Stop();
		_tipTimer?.Start();
	}

	private async void OnRequestReload(object? sender, EventArgs e)
	{
		await LoadMediaAsync();
	}

	private void OnRequestClear(object? sender, EventArgs e)
	{
		TryScheduleStoppedReport();
		MpvNative.SetCommandString(Client.Handle, "stop");
	}

	private void HandleStateChanged(MpvPlayerState state)
	{
		LastState = state;
		IsPlaying = LastState == MpvPlayerState.Playing;
		if (IsPlaying)
		{
			_hasStartedPlaying = true;
			_streamPausedAtUtc = null;
		}
		IsIdle = LastState == MpvPlayerState.Idle;
		IsRestartVisible = LastState == MpvPlayerState.End;
		CheckBackdropVisible();
		if (state == MpvPlayerState.End || state == MpvPlayerState.Idle)
		{
			if (_lastEndFileReason == MpvEndFileReason.Error)
			{
				_isGaplessTransitioning = false;
				_gaplessHandoffInProgress = false;
			}
			StopNetworkPollTimer();
		}
	}

	private async Task ApplyInitialTrackSelectionAndRefreshAsync()
	{
		await ApplyInitialTrackSelectionAsync();
		_queue.TryEnqueue(RefreshSubtitleTracksFromMpv);
	}

	private void ApplyCompactOverlayWindowState(bool compact)
	{
		if (Window == null)
		{
			return;
		}
		AppWindow window = Window.GetWindow();
		window.SetPresenter(AppWindowPresenterKind.Overlapped);
		if (!(window.Presenter is OverlappedPresenter overlappedPresenter))
		{
			return;
		}
		overlappedPresenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
		overlappedPresenter.IsAlwaysOnTop = compact || IsTopmost;
		if (compact)
		{
			RectInt32 valueOrDefault = _preCompactOverlayRect.GetValueOrDefault();
			if (!_preCompactOverlayRect.HasValue)
			{
				valueOrDefault = new RectInt32(window.Position.X, window.Position.Y, window.Size.Width, window.Size.Height);
				_preCompactOverlayRect = valueOrDefault;
			}
			MoveWindowToCompactOverlayBounds(window);
		}
		else if (_preCompactOverlayRect.HasValue)
		{
			window.MoveAndResize(_preCompactOverlayRect.Value);
			_preCompactOverlayRect = null;
		}
		Window.SuppressActivationBorder();
	}

	internal bool IsWindowInFullScreenPresenter()
	{
		return IsFullScreen;
	}

	internal void ApplyFullScreenWindowState(bool isFullScreen)
	{
		if (Window == null)
		{
			IsFullScreen = isFullScreen;
			return;
		}
		if (isFullScreen)
		{
			if (IsFullScreen)
			{
				ApplyFullScreenMonitorBounds();
				Window.SetFullBleedChrome(enabled: true);
				Window.SuppressActivationBorder();
				return;
			}
			IsFullScreen = true;
			AppWindow window = Window.GetWindow();
			_preFullscreenMaximized = IsWindowMaximized;
			if (window.Presenter is OverlappedPresenter overlappedPresenter && _preFullscreenMaximized)
			{
				overlappedPresenter.Restore();
			}
			_preFullscreenRect = CaptureWindowRect();
			if (window.Presenter.Kind == AppWindowPresenterKind.FullScreen)
			{
				window.SetPresenter(AppWindowPresenterKind.Overlapped);
			}
			if (window.Presenter is OverlappedPresenter overlappedPresenter2)
			{
				overlappedPresenter2.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
			}
			ApplyFullScreenMonitorBounds();
			Window.SetFullBleedChrome(enabled: true);
		}
		else
		{
			if (!IsFullScreen)
			{
				return;
			}
			AppWindow window2 = Window.GetWindow();
			RectInt32? preFullscreenRect = _preFullscreenRect;
			bool preFullscreenMaximized = _preFullscreenMaximized;
			_preFullscreenRect = null;
			_preFullscreenMaximized = false;
			IsFullScreen = false;
			if (window2.Presenter.Kind == AppWindowPresenterKind.FullScreen)
			{
				window2.SetPresenter(AppWindowPresenterKind.Overlapped);
			}
			if (window2.Presenter is OverlappedPresenter overlappedPresenter3)
			{
				overlappedPresenter3.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
			}
			if (preFullscreenRect.HasValue)
			{
				RestoreWindowBounds(preFullscreenRect.Value);
			}
			Window.SetFullBleedChrome(enabled: false);
			if (preFullscreenMaximized && window2.Presenter is OverlappedPresenter overlappedPresenter4)
			{
				overlappedPresenter4.Maximize();
			}
		}
		UpdateWindowState();
		Window.SuppressActivationBorder();
	}

	private async Task SetFullScreenAsync(bool isFullScreen)
	{
		if (Client == null || Window == null)
		{
			return;
		}
		if (isFullScreen && IsCompactOverlay)
		{
			await Client.SetCompactOverlayStateAsync(isCompactOverlay: false);
		}
		int generation = ++_fullScreenGeneration;
		ApplyFullScreenWindowState(isFullScreen);
		if ((await Client.SetFullScreenStateAsync(isFullScreen)).IsFailed)
		{
			_logger.LogWarning("Failed to sync mpv fullscreen property to {State}", isFullScreen);
		}
		_queue.TryEnqueue(DispatcherQueuePriority.High, delegate
		{
			if (generation == _fullScreenGeneration)
			{
				ApplyFullScreenWindowState(isFullScreen);
			}
		});
	}

	private static void MoveWindowToCompactOverlayBounds(AppWindow wnd)
	{
		RectInt32 workArea = (DisplayArea.GetFromPoint(wnd.Position, DisplayAreaFallback.Nearest) ?? DisplayArea.Primary).WorkArea;
		double num = ((wnd.Size.Height > 0) ? Math.Clamp(wnd.Size.Width / (double)wnd.Size.Height, 1.2, 2.4) : 1.7777777777777777);
		int num2 = Math.Clamp((int)(workArea.Width * 0.36), 460, 680);
		int num3 = Math.Clamp((int)(num2 / num), 260, 420);
		int x = workArea.X + workArea.Width - num2 - 24;
		int y = workArea.Y + workArea.Height - num3 - 48;
		wnd.MoveAndResize(new RectInt32(x, y, num2, num3));
	}

	private async Task ApplyGaplessTransitionAsync(HostNavigateOptions pending)
	{
		await ApplyHostNavigateOptionsAsync(pending);
		if (Client == null)
		{
			return;
		}
		double num = 0.0;
		double? startPosition = pending.StartPosition;
		if (startPosition.HasValue && startPosition.GetValueOrDefault() > 0.0)
		{
			num = pending.StartPosition.Value;
			_logger.LogInformation("Gapless transition: seeking to resume position {Position}s", num);
		}
		else if (IsSkipFeatureEnabled)
		{
			num = ComputeInitialSkipPosition();
			if (num > 0.0)
			{
				_logger.LogInformation("Gapless transition: seeking past intro to {Position}s", num);
			}
		}
		await Client.SetCurrentPositionAsync(num);
		await Client.ResumeAsync();
	}

	private async Task ApplyInitialStartPositionAsync()
	{
		if (Client != null && _pendingNextEpisodeData == null)
		{
			double? num = _lastPlayOptions?.StartPosition;
			if (num.HasValue && num.GetValueOrDefault() > 0.0)
			{
				_logger.LogInformation("Initial load: seeking to start position {Position}s", num.Value);
				await Client.SetCurrentPositionAsync(num.Value);
			}
		}
	}

	private bool HasTrailingPlaylistEntries()
	{
		if (Client == null)
		{
			return false;
		}
		if (MpvNative.GetPropertyInt64(Client.Handle, "playlist-count", MpvFormat.Int64, out var data) != MpvError.Success)
		{
			return false;
		}
		if (!TryGetPlaylistPos(out var pos))
		{
			return false;
		}
		return data > pos + 1;
	}

	private bool TryGetPlaylistPos(out long pos)
	{
		pos = -1L;
		if (Client == null)
		{
			return false;
		}
		return MpvNative.GetPropertyInt64(Client.Handle, "playlist-pos", MpvFormat.Int64, out pos) == MpvError.Success;
	}

	private bool HasMpvAutoAdvancedToPreloadedEntry()
	{
		if (_preloadedAtPlaylistPos >= 0 && TryGetPlaylistPos(out var pos))
		{
			return pos > _preloadedAtPlaylistPos;
		}
		return false;
	}

	private bool TryDetectGaplessAutoAdvance()
	{
		if (_pendingNextEpisodeData == null || Client == null || _preloadedAtPlaylistPos < 0)
		{
			return false;
		}
		if (HasMpvAutoAdvancedToPreloadedEntry())
		{
			_logger.LogInformation("Gapless auto-advance detected (playlist-pos advanced past {Pos}).", _preloadedAtPlaylistPos);
			return true;
		}
		if (LastState == MpvPlayerState.End && HasTrailingPlaylistEntries())
		{
			_logger.LogInformation("Gapless auto-advance imminent at EOF (playlist-pos={Pos}).", _preloadedAtPlaylistPos);
			return true;
		}
		return false;
	}

	private async Task TryApplyPendingGaplessMetadataIfNeededAsync(string reason)
	{
		HostNavigateOptions pendingNextEpisodeData = _pendingNextEpisodeData;
		if (pendingNextEpisodeData == null || _gaplessHandoffInProgress || _isGaplessTransitioning)
		{
			return;
		}
		_logger.LogInformation("Applying preloaded episode metadata after {Reason}: {MediaPath}", reason, pendingNextEpisodeData.MediaPath);
		_gaplessHandoffInProgress = true;
		_isGaplessTransitioning = true;
		try
		{
			await ApplyGaplessTransitionAsync(pendingNextEpisodeData);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Failed to apply gapless metadata after {Reason}", reason);
		}
		finally
		{
			_pendingNextEpisodeData = null;
			_preloadedAtPlaylistPos = -1L;
			_gaplessHandoffInProgress = false;
			_isGaplessTransitioning = false;
			_queue.TryEnqueue(CompleteFileLoadedUi);
		}
	}

	internal void ClearDanmakuSelections()
	{
		_danmakuSelections.Clear();
	}

	internal IReadOnlyList<DanmakuSelectionCandidate> GetDanmakuCandidates(int apiIndex)
	{
		if (!_danmakuSelections.TryGetValue(apiIndex, out DanmakuApiSelectionState value))
		{
			return new List<DanmakuSelectionCandidate>();
		}
		return value.Candidates;
	}

	internal string? GetDanmakuActiveCommentUrl(int apiIndex)
	{
		if (!_danmakuSelections.TryGetValue(apiIndex, out DanmakuApiSelectionState value))
		{
			return null;
		}
		return value.ActiveCommentUrl;
	}

	internal string? GetDanmakuMatchTitle(int apiIndex)
	{
		if (!_danmakuSelections.TryGetValue(apiIndex, out DanmakuApiSelectionState value))
		{
			return null;
		}
		return value.ActiveCandidate?.DisplayTitle;
	}

	internal void MergeDanmakuMatchCandidates(int apiIndex, IReadOnlyList<DanmakuMatchResult> matches)
	{
		if (matches.Count == 0)
		{
			return;
		}
		DanmakuApiSelectionState orCreateDanmakuSelection = GetOrCreateDanmakuSelection(apiIndex);
		string previousActive = orCreateDanmakuSelection.ActiveCommentUrl;
		foreach (DanmakuMatchResult match in matches)
		{
			if (orCreateDanmakuSelection.Candidates.FirstOrDefault((DanmakuSelectionCandidate c) => string.Equals(c.CommentUrl, match.CommentUrl, StringComparison.Ordinal)) is null)
			{
				orCreateDanmakuSelection.Candidates.Add(new DanmakuSelectionCandidate(match.DisplayTitle, match.CommentUrl, IsManual: false));
			}
		}
		if (!string.IsNullOrWhiteSpace(previousActive) && orCreateDanmakuSelection.Candidates.Any((DanmakuSelectionCandidate c) => string.Equals(c.CommentUrl, previousActive, StringComparison.Ordinal)))
		{
			orCreateDanmakuSelection.ActiveCommentUrl = previousActive;
		}
		else
		{
			orCreateDanmakuSelection.ActiveCommentUrl = matches[0].CommentUrl;
		}
	}

	internal void SetDanmakuManualSelection(int apiIndex, string displayTitle, string commentUrl)
	{
		if (!string.IsNullOrWhiteSpace(commentUrl))
		{
			DanmakuApiSelectionState orCreateDanmakuSelection = GetOrCreateDanmakuSelection(apiIndex);
			DanmakuSelectionCandidate danmakuSelectionCandidate = orCreateDanmakuSelection.Candidates.FirstOrDefault((DanmakuSelectionCandidate c) => string.Equals(c.CommentUrl, commentUrl, StringComparison.Ordinal));
			if (danmakuSelectionCandidate is not null)
			{
				orCreateDanmakuSelection.Candidates.Remove(danmakuSelectionCandidate);
			}
			orCreateDanmakuSelection.Candidates.Insert(0, new DanmakuSelectionCandidate(displayTitle, commentUrl, IsManual: true));
			orCreateDanmakuSelection.ActiveCommentUrl = commentUrl;
		}
	}

	internal void SetDanmakuActiveCommentUrl(int apiIndex, string commentUrl)
	{
		if (!string.IsNullOrWhiteSpace(commentUrl))
		{
			DanmakuApiSelectionState orCreateDanmakuSelection = GetOrCreateDanmakuSelection(apiIndex);
			if (!orCreateDanmakuSelection.Candidates.Any((DanmakuSelectionCandidate c) => string.Equals(c.CommentUrl, commentUrl, StringComparison.Ordinal)))
			{
				orCreateDanmakuSelection.Candidates.Add(new DanmakuSelectionCandidate(commentUrl, commentUrl, IsManual: false));
			}
			orCreateDanmakuSelection.ActiveCommentUrl = commentUrl;
		}
	}

	private DanmakuApiSelectionState GetOrCreateDanmakuSelection(int apiIndex)
	{
		if (!_danmakuSelections.TryGetValue(apiIndex, out DanmakuApiSelectionState value))
		{
			value = new DanmakuApiSelectionState();
			_danmakuSelections[apiIndex] = value;
		}
		return value;
	}

	private static string FormatTime(double value)
	{
		if (value < 0.0 || double.IsNaN(value) || double.IsInfinity(value))
		{
			return "00:00";
		}
		TimeSpan timeSpan = TimeSpan.FromSeconds(value);
		if (!(timeSpan.TotalHours >= 1.0))
		{
			return timeSpan.ToString("mm\\:ss");
		}
		return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
	}

	private static string BuildMediaMonogram(string? title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return "AI";
		}
		char[] array = (from p in title.Split(new char[9] { ' ', '-', '_', '.', ':', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
						select char.ToUpperInvariant(p[0])).ToArray();
		if (array.Length == 0)
		{
			return title.Substring(0, Math.Min(2, title.Length)).ToUpperInvariant();
		}
		return new string(array);
	}

	[DllImport("comctl32.dll", SetLastError = true)]
	private static extern bool SetWindowSubclass(nint hWnd, SubclassProcDelegate pfnSubclass, nint uIdSubclass, nint dwRefData);

	[DllImport("comctl32.dll", SetLastError = true)]
	private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProcDelegate pfnSubclass, nint uIdSubclass);

	[DllImport("comctl32.dll")]
	private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(nint hMonitor, ref WinMonitorInfo lpmi);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out WinRect lpRect);

	private void InstallMaximizeSubclass()
	{
		if (Window != null)
		{
			_subclassProc = MaximizeSubclassProc;
			SetWindowSubclass(Window.Handle, _subclassProc, new IntPtr(1), IntPtr.Zero);
		}
	}

	private void RemoveMaximizeSubclass()
	{
		if (_subclassProc != null && Window != null)
		{
			RemoveWindowSubclass(Window.Handle, _subclassProc, new IntPtr(1));
			_subclassProc = null;
		}
	}

	private static bool TryGetMonitorRect(nint hwnd, out WinRect monitor)
	{
		monitor = default(WinRect);
		nint hMonitor = MonitorFromWindow(hwnd, 2u);
		WinMonitorInfo lpmi = new WinMonitorInfo
		{
			cbSize = (uint)Marshal.SizeOf<WinMonitorInfo>()
		};
		if (!GetMonitorInfo(hMonitor, ref lpmi))
		{
			return false;
		}
		monitor = lpmi.Monitor;
		return true;
	}

	private RectInt32 CaptureWindowRect()
	{
		GetWindowRect(Window.Handle, out var lpRect);
		return new RectInt32(lpRect.Left, lpRect.Top, lpRect.Right - lpRect.Left, lpRect.Bottom - lpRect.Top);
	}

	private void ApplyFullScreenMonitorBounds()
	{
		if (Window != null && TryGetMonitorRect(Window.Handle, out var monitor))
		{
			SetWindowPos(Window.Handle, HwndTopmost, monitor.Left, monitor.Top, monitor.Right - monitor.Left, monitor.Bottom - monitor.Top, 64u);
		}
	}

	private void RestoreWindowBounds(RectInt32 rect)
	{
		if (Window != null)
		{
			nint hWndInsertAfter = (IsTopmost ? HwndTopmost : HwndNotTopmost);
			SetWindowPos(Window.Handle, hWndInsertAfter, rect.X, rect.Y, rect.Width, rect.Height, 64u);
		}
	}

	private nint MaximizeSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData)
	{
		if (uMsg == 70 && IsFullScreen)
		{
			if (!TryGetMonitorRect(hWnd, out var monitor))
			{
				return DefSubclassProc(hWnd, uMsg, wParam, lParam);
			}
			WindowPos structure = Marshal.PtrToStructure<WindowPos>(lParam);
			structure.X = monitor.Left;
			structure.Y = monitor.Top;
			structure.Cx = monitor.Right - monitor.Left;
			structure.Cy = monitor.Bottom - monitor.Top;
			structure.Flags &= 4294967293u;
			structure.Flags &= 4294967294u;
			Marshal.StructureToPtr(structure, lParam, fDeleteOld: true);
			return IntPtr.Zero;
		}
		if (uMsg == 36)
		{
			WinMinMaxInfo structure2 = Marshal.PtrToStructure<WinMinMaxInfo>(lParam);
			nint hMonitor = MonitorFromWindow(hWnd, 2u);
			WinMonitorInfo lpmi = new WinMonitorInfo
			{
				cbSize = (uint)Marshal.SizeOf<WinMonitorInfo>()
			};
			if (GetMonitorInfo(hMonitor, ref lpmi))
			{
				WinRect workArea = lpmi.WorkArea;
				WinRect monitor2 = lpmi.Monitor;
				if (IsFullScreen)
				{
					structure2.MaxPosition.X = monitor2.Left;
					structure2.MaxPosition.Y = monitor2.Top;
					structure2.MaxSize.X = monitor2.Right - monitor2.Left;
					structure2.MaxSize.Y = monitor2.Bottom - monitor2.Top;
				}
				else
				{
					structure2.MaxPosition.X = workArea.Left - monitor2.Left;
					structure2.MaxPosition.Y = workArea.Top - monitor2.Top;
					structure2.MaxSize.X = workArea.Right - workArea.Left;
					structure2.MaxSize.Y = workArea.Bottom - workArea.Top;
				}
				structure2.MaxTrackSize.X = structure2.MaxSize.X;
				structure2.MaxTrackSize.Y = structure2.MaxSize.Y;
				Marshal.StructureToPtr(structure2, lParam, fDeleteOld: true);
			}
			return IntPtr.Zero;
		}
		return DefSubclassProc(hWnd, uMsg, wParam, lParam);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnMediaTitleChanged(string value)
	{
		OnPropertyChanged("HasMedia");
		if (string.IsNullOrWhiteSpace(MediaMonogram) || string.Equals(MediaMonogram, "AI", StringComparison.Ordinal))
		{
			MediaMonogram = BuildMediaMonogram(value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVideoFitModeChanged(VideoFitMode value)
	{
		OnPropertyChanged("VideoFitModeLabel");
		_settingsToolkit.WriteLocalSetting("VideoFitMode", value);
		ApplyVideoFitModeAsync();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsDanmakuEnabledChanged(bool value)
	{
		_settingsToolkit.WriteLocalSetting("IsDanmakuEnabled", value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsSourceLoadingChanged(bool value)
	{
		CheckBackdropVisible();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsFileLoadingChanged(bool value)
	{
		CheckBackdropVisible();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsPlayingChanged(bool value)
	{
		OnPropertyChanged("PlayPauseGlyph");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDurationChanged(double value)
	{
		OnPropertyChanged("DurationText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentPositionChanged(double value)
	{
		OnPropertyChanged("CurrentPositionText");
		CheckAutoSkip(value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVolumeChanged(double value)
	{
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSpeedChanged(double value)
	{
		_settingsToolkit.WriteLocalSetting("PlayerSpeed", value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsFullScreenChanged(bool value)
	{
		IsWindowButtonsVisible = !value && !IsCompactOverlay;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsCompactOverlayChanged(bool value)
	{
		IsWindowButtonsVisible = !value && !IsFullScreen;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnPreviewPositionChanged(double value)
	{
		OnPropertyChanged("CurrentPositionText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnPreviousEpisodeIdChanged(string value)
	{
		OnPropertyChanged("HasPreviousEpisode");
		OnPropertyChanged("CanNavigatePreviousEpisode");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnNextEpisodeIdChanged(string value)
	{
		OnPropertyChanged("HasNextEpisode");
		OnPropertyChanged("CanNavigateNextEpisode");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIntroEndTimeChanged(double value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting(_GetSkipSettingKey("IntroEndTime"), value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnOutroStartTimeChanged(double value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting(_GetSkipSettingKey("OutroStartTime"), value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutoSkipIntroChanged(bool value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting("AutoSkipIntro", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutoSkipRecapChanged(bool value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting("AutoSkipRecap", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutoSkipOutroChanged(bool value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting("AutoSkipOutro", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutoSkipPreviewChanged(bool value)
	{
		if (!_suppressSkipSettingWrites && IsSkipFeatureEnabled)
		{
			_settingsToolkit.WriteLocalSetting("AutoSkipPreview", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuOpacityChanged(double value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuOpacity", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuAreaRatioChanged(int value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuAreaRatio", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuDensityChanged(int value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuDensity", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuSpeedChanged(double value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuSpeed", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuFontSizeOffsetChanged(double value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuFontSizeOffset", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuBoldChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuBold", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuRollingEnabledChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuRollingEnabled", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuTopEnabledChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuTopEnabled", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuBottomEnabledChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuBottomEnabled", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuTimeOffsetSecondsChanged(double value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuTimeOffset", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuFollowSpeedChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuFollowSpeed", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuFontFamilyChanged(string value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuFontFamily", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuOutlineSizeChanged(double value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuOutlineSize", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDanmakuNoOverlapSubtitleChanged(bool value)
	{
		if (!_suppressDanmakuSettingWrites)
		{
			_settingsToolkit.WriteLocalSetting("DanmakuNoOverlapSubtitle", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentAnimeModeChanged(string value)
	{
		if (!_suppressShaderReapply)
		{
			ApplyShaderOverrideAsync(value, CurrentSharpenMode);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentSharpenModeChanged(string value)
	{
		if (!_suppressShaderReapply)
		{
			ApplyShaderOverrideAsync(CurrentAnimeMode, value);
		}
	}
}

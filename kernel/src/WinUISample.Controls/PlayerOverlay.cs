using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using FluentIcons.WinUI.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using Richasy.MpvKernel.WinUI;
using Richasy.MpvKernel.WinUI.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.Models;
using WinUISample.Models.Constants;
using WinUISample.Services;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Controls_RootLayoutWinRTTypeDetails))]
public sealed class PlayerOverlay : PlayerOverlayBase, IMpvUIElement, IComponentConnector
{
	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private interface IPlayerOverlay_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface IPlayerOverlay_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(ItemsControl obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.ItemsSource = value;
		}

		public static void Set_Microsoft_UI_Xaml_UIElement_Visibility(UIElement obj, Visibility value)
		{
			obj.Visibility = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(Control obj, bool value)
		{
			obj.IsEnabled = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(TextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Primitives_RangeBase_Maximum(RangeBase obj, double value)
		{
			obj.Maximum = value;
		}

		public static void Set_FluentIcons_WinUI_Internals_GenericIcon_IconVariant(GenericIcon obj, IconVariant value)
		{
			obj.IconVariant = value;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.Markup.IComponentConnector")]
	[WinRTExposedType(typeof(WinUISample_MainWindowWinRTTypeDetails))]
	private class PlayerOverlay_obj1_Bindings : IComponentConnector, IPlayerOverlay_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		private class PlayerOverlay_obj1_BindingsTracking
		{
			private WeakReference<PlayerOverlay_obj1_Bindings> weakRefToBindingObj;

			private PlayerViewModel cache_ViewModel;

			private ObservableCollection<string> cache_ViewModel_MediaBadges;

			public PlayerOverlay_obj1_BindingsTracking(PlayerOverlay_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<PlayerOverlay_obj1_Bindings>(obj);
			}

			public PlayerOverlay_obj1_Bindings TryGetBindingObject()
			{
				PlayerOverlay_obj1_Bindings target = null;
				if (weakRefToBindingObj != null)
				{
					weakRefToBindingObj.TryGetTarget(out target);
					if (target == null)
					{
						weakRefToBindingObj = null;
						ReleaseAllListeners();
					}
				}
				return target;
			}

			public void ReleaseAllListeners()
			{
				UpdateChildListeners_ViewModel(null);
				UpdateChildListeners_ViewModel_MediaBadges(null);
			}

			public void PropertyChanged_ViewModel(object sender, PropertyChangedEventArgs e)
			{
				PlayerOverlay_obj1_Bindings playerOverlay_obj1_Bindings = TryGetBindingObject();
				if (playerOverlay_obj1_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				PlayerViewModel playerViewModel = sender as PlayerViewModel;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (playerViewModel != null)
					{
						playerOverlay_obj1_Bindings.Update_ViewModel_MediaBadges(playerViewModel.MediaBadges, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_IsSkipFeatureEnabled(playerViewModel.IsSkipFeatureEnabled, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_CanNavigatePreviousEpisode(playerViewModel.CanNavigatePreviousEpisode, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_CanNavigateNextEpisode(playerViewModel.CanNavigateNextEpisode, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_CurrentPositionText(playerViewModel.CurrentPositionText, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_DurationText(playerViewModel.DurationText, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_Duration(playerViewModel.Duration, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_IsTopmost(playerViewModel.IsTopmost, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_NetworkSpeedText(playerViewModel.NetworkSpeedText, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_MediaTitle(playerViewModel.MediaTitle, 1073741824);
						playerOverlay_obj1_Bindings.Update_ViewModel_MediaSubtitle(playerViewModel.MediaSubtitle, 1073741824);
					}
				}
				else
				{
					if (propertyName == null)
					{
						return;
					}
					switch (propertyName.Length)
					{
						case 11:
							if (propertyName == "MediaBadges" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_MediaBadges(playerViewModel.MediaBadges, 1073741824);
							}
							break;
						case 20:
							if (propertyName == "IsSkipFeatureEnabled" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_IsSkipFeatureEnabled(playerViewModel.IsSkipFeatureEnabled, 1073741824);
							}
							break;
						case 26:
							if (propertyName == "CanNavigatePreviousEpisode" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_CanNavigatePreviousEpisode(playerViewModel.CanNavigatePreviousEpisode, 1073741824);
							}
							break;
						case 22:
							if (propertyName == "CanNavigateNextEpisode" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_CanNavigateNextEpisode(playerViewModel.CanNavigateNextEpisode, 1073741824);
							}
							break;
						case 19:
							if (propertyName == "CurrentPositionText" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_CurrentPositionText(playerViewModel.CurrentPositionText, 1073741824);
							}
							break;
						case 12:
							if (propertyName == "DurationText" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_DurationText(playerViewModel.DurationText, 1073741824);
							}
							break;
						case 8:
							if (propertyName == "Duration" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_Duration(playerViewModel.Duration, 1073741824);
							}
							break;
						case 9:
							if (propertyName == "IsTopmost" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_IsTopmost(playerViewModel.IsTopmost, 1073741824);
							}
							break;
						case 16:
							if (propertyName == "NetworkSpeedText" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_NetworkSpeedText(playerViewModel.NetworkSpeedText, 1073741824);
							}
							break;
						case 10:
							if (propertyName == "MediaTitle" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_MediaTitle(playerViewModel.MediaTitle, 1073741824);
							}
							break;
						case 13:
							if (propertyName == "MediaSubtitle" && playerViewModel != null)
							{
								playerOverlay_obj1_Bindings.Update_ViewModel_MediaSubtitle(playerViewModel.MediaSubtitle, 1073741824);
							}
							break;
						case 14:
						case 15:
						case 17:
						case 18:
						case 21:
						case 23:
						case 24:
						case 25:
							break;
					}
				}
			}

			public void UpdateChildListeners_ViewModel(PlayerViewModel obj)
			{
				if (obj != cache_ViewModel)
				{
					if (cache_ViewModel != null)
					{
						((INotifyPropertyChanged)cache_ViewModel).PropertyChanged -= PropertyChanged_ViewModel;
						cache_ViewModel = null;
					}
					if (obj != null)
					{
						cache_ViewModel = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_ViewModel;
					}
				}
			}

			public void PropertyChanged_ViewModel_MediaBadges(object sender, PropertyChangedEventArgs e)
			{
				if (TryGetBindingObject() != null)
				{
					string.IsNullOrEmpty(e.PropertyName);
				}
			}

			public void CollectionChanged_ViewModel_MediaBadges(object sender, NotifyCollectionChangedEventArgs e)
			{
				TryGetBindingObject();
			}

			public void UpdateChildListeners_ViewModel_MediaBadges(ObservableCollection<string> obj)
			{
				if (obj != cache_ViewModel_MediaBadges)
				{
					if (cache_ViewModel_MediaBadges != null)
					{
						((INotifyPropertyChanged)cache_ViewModel_MediaBadges).PropertyChanged -= PropertyChanged_ViewModel_MediaBadges;
						((INotifyCollectionChanged)cache_ViewModel_MediaBadges).CollectionChanged -= CollectionChanged_ViewModel_MediaBadges;
						cache_ViewModel_MediaBadges = null;
					}
					if (obj != null)
					{
						cache_ViewModel_MediaBadges = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_ViewModel_MediaBadges;
						((INotifyCollectionChanged)obj).CollectionChanged += CollectionChanged_ViewModel_MediaBadges;
					}
				}
			}
		}

		private PlayerOverlay dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private ResourceDictionary localResources;

		private WeakReference<FrameworkElement> converterLookupRoot;

		private ItemsControl obj41;

		private Button obj49;

		private Button obj80;

		private Button obj84;

		private TextBlock obj86;

		private TextBlock obj87;

		private ProgressBar obj88;

		private Slider obj91;

		private FluentIcons.WinUI.SymbolIcon obj104;

		private TextBlock obj105;

		private TextBlock obj106;

		private TextBlock obj107;

		private PlayerOverlay_obj1_BindingsTracking bindingsTracking;

		public PlayerOverlay_obj1_Bindings()
		{
			bindingsTracking = new PlayerOverlay_obj1_BindingsTracking(this);
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 41:
					obj41 = target.As<ItemsControl>();
					break;
				case 49:
					obj49 = target.As<Button>();
					break;
				case 80:
					obj80 = target.As<Button>();
					break;
				case 84:
					obj84 = target.As<Button>();
					break;
				case 86:
					obj86 = target.As<TextBlock>();
					break;
				case 87:
					obj87 = target.As<TextBlock>();
					break;
				case 88:
					obj88 = target.As<ProgressBar>();
					break;
				case 91:
					obj91 = target.As<Slider>();
					break;
				case 104:
					obj104 = target.As<FluentIcons.WinUI.SymbolIcon>();
					break;
				case 105:
					obj105 = target.As<TextBlock>();
					break;
				case 106:
					obj106 = target.As<TextBlock>();
					break;
				case 107:
					obj107 = target.As<TextBlock>();
					break;
			}
		}

		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		public IComponentConnector GetBindingConnector(int connectionId, object target)
		{
			return null;
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
			bindingsTracking.ReleaseAllListeners();
			initialized = false;
		}

		public void DisconnectUnloadedObject(int connectionId)
		{
			throw new ArgumentException("No unloadable elements to disconnect.");
		}

		public bool SetDataRoot(object newDataRoot)
		{
			bindingsTracking.ReleaseAllListeners();
			if (newDataRoot != null)
			{
				dataRoot = newDataRoot.As<PlayerOverlay>();
				return true;
			}
			return false;
		}

		public void Activated(object obj, WindowActivatedEventArgs data)
		{
			Initialize();
		}

		public void Loading(FrameworkElement src, object data)
		{
			Initialize();
		}

		public void SetConverterLookupRoot(FrameworkElement rootElement)
		{
			converterLookupRoot = new WeakReference<FrameworkElement>(rootElement);
		}

		public IValueConverter LookupConverter(string key)
		{
			if (localResources == null)
			{
				converterLookupRoot.TryGetTarget(out var target);
				localResources = target.Resources;
				converterLookupRoot = null;
			}
			return (IValueConverter)(localResources.ContainsKey(key) ? localResources[key] : Application.Current.Resources[key]);
		}

		private void Update_(PlayerOverlay obj, int phase)
		{
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_ViewModel(obj.ViewModel, phase);
			}
		}

		private void Update_ViewModel(PlayerViewModel obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_ViewModel(obj);
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_ViewModel_MediaBadges(obj.MediaBadges, phase);
				Update_ViewModel_IsSkipFeatureEnabled(obj.IsSkipFeatureEnabled, phase);
				Update_ViewModel_CanNavigatePreviousEpisode(obj.CanNavigatePreviousEpisode, phase);
				Update_ViewModel_CanNavigateNextEpisode(obj.CanNavigateNextEpisode, phase);
				Update_ViewModel_CurrentPositionText(obj.CurrentPositionText, phase);
				Update_ViewModel_DurationText(obj.DurationText, phase);
				Update_ViewModel_Duration(obj.Duration, phase);
				Update_ViewModel_IsTopmost(obj.IsTopmost, phase);
				Update_ViewModel_NetworkSpeedText(obj.NetworkSpeedText, phase);
				Update_ViewModel_MediaTitle(obj.MediaTitle, phase);
				Update_ViewModel_MediaSubtitle(obj.MediaSubtitle, phase);
			}
		}

		private void Update_ViewModel_MediaBadges(ObservableCollection<string> obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_ViewModel_MediaBadges(obj);
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(obj41, obj, null);
			}
		}

		private void Update_ViewModel_IsSkipFeatureEnabled(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_UIElement_Visibility(obj49, (Visibility)LookupConverter("BoolToVisibilityConverter").Convert(obj, typeof(Visibility), null, null));
			}
		}

		private void Update_ViewModel_CanNavigatePreviousEpisode(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(obj80, obj);
			}
		}

		private void Update_ViewModel_CanNavigateNextEpisode(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(obj84, obj);
			}
		}

		private void Update_ViewModel_CurrentPositionText(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj86, obj, null);
			}
		}

		private void Update_ViewModel_DurationText(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj87, obj, null);
			}
		}

		private void Update_ViewModel_Duration(double obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_RangeBase_Maximum(obj88, obj);
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_RangeBase_Maximum(obj91, obj);
			}
		}

		private void Update_ViewModel_IsTopmost(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_FluentIcons_WinUI_Internals_GenericIcon_IconVariant(obj104, (IconVariant)LookupConverter("BoolToIconVariantConverter").Convert(obj, typeof(IconVariant), null, null));
			}
		}

		private void Update_ViewModel_NetworkSpeedText(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj105, obj, null);
			}
		}

		private void Update_ViewModel_MediaTitle(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj106, obj, null);
			}
		}

		private void Update_ViewModel_MediaSubtitle(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj107, obj, null);
			}
		}
	}

	private readonly ILogger<PlayerOverlay> _logger;

	private readonly DispatcherTimer _autoHideTimer;

	private readonly DispatcherTimer _keyLongPressTimer;

	private CancellationTokenSource? _danmakuResumeProbeCts;

	private bool _retryingDanmakuInitialize;

	private double _lastNonZeroVolume = 100.0;

	private bool _isUpdatingFromViewModel;

	private bool _isPointerInsideChrome;

	private bool _isTopBarDragging;

	private uint _topBarPointerId;

	private bool _isScrubPointerDown;

	private uint _scrubPointerId;

	private bool _spaceKeyDown;

	private bool _isHoldSpeedActive;

	private double _preHoldSpeed = 1.0;

	private DanmakuFrostMaster? _danmaku;

	private string? _lastDanmakuUrl;

	private string? _lastPrefetchKey;

	private readonly DispatcherTimer _danmakuMessageTimer;

	private static readonly HttpClient _thumbnailClient = new HttpClient();

	private volatile bool _thumbnailLoading;

	private PointInt32 _dragWindowStart;

	private NativePoint _dragCursorStart;

	private FrameworkElement? _episodeListScrollTarget;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Grid DanmakuLayer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Grid VideoBackdrop;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Grid ChromeRoot;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border LoadingRingHost;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border DanmakuMessageBorder;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock DanmakuMessageText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ProgressRing LoadingRing;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Canvas ThumbnailCanvas;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Canvas HoverHintLayer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border HoverHintBorder;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock HoverHintText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ItemsControl BadgeItemsControl;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private StackPanel PlaybackControlsPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private StackPanel SecondaryControlsPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button SpeedButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button AudioTrackButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button VersionButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button SubtitleTrackButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button DanmakuButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button SkipButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button EpisodeListButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button SettingsButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button FullScreenButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private FluentIcons.WinUI.SymbolIcon FullScreenIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout SettingsFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyoutSubItem VideoFitModeSubItem;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyoutSubItem AnimeModeSubItem;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyoutSubItem SharpenModeSubItem;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyoutItem CompactOverlayMenuItem;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Flyout EpisodeListFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ScrollViewer EpisodeListScrollViewer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private StackPanel EpisodeListPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout SkipFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private FluentIcons.WinUI.SymbolIcon DanmakuIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout DanmakuFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout SubtitleTrackFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout VersionFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout AudioTrackFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock SpeedTextBlock;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private MenuFlyout SpeedFlyout;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button MuteButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider VolumeSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private FluentIcons.WinUI.SymbolIcon VolumeIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button PreviousEpisodeButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button BackwardButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button PlayPauseButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button ForwardButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button NextEpisodeButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private FluentIcons.WinUI.SymbolIcon PlayPauseIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock CurrentPositionBlock;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock DurationBlock;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ProgressBar BufferBar;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Canvas SegmentOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Canvas ChapterOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider ProgressSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Grid LogoContainer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border TopMetadataPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button ExitPipButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border NetworkSpeedBadge;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private StackPanel WindowButtonsPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button TopmostButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button MinimizeButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button MaximizeRestoreButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Button CloseButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock MaximizeRestoreIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock NetworkSpeedText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Image LogoImage;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Border ThumbnailPreview;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Image ThumbnailImage;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock ThumbnailTimeText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Image VideoBackdropImage;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private IPlayerOverlay_Bindings Bindings;

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out NativePoint lpPoint);

	public PlayerOverlay(PlayerViewModel viewModel)
	{
		base.ViewModel = viewModel;
		_logger = this.Get<ILogger<PlayerOverlay>>();
		InitializeComponent();
		VolumeSlider.Maximum = base.ViewModel.MaxVolume;
		base.DataContext = viewModel;
		base.ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		base.ViewModel.ActiveSegments.CollectionChanged += OnActiveSegmentsChanged;
		base.ViewModel.Chapters.CollectionChanged += OnChaptersChanged;
		_autoHideTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3L)
		};
		_autoHideTimer.Tick += OnAutoHideTimerTick;
		_keyLongPressTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500L, 0L)
		};
		_keyLongPressTimer.Tick += OnKeyLongPressTimerTick;
		_danmakuMessageTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(8L)
		};
		_danmakuMessageTimer.Tick += OnDanmakuMessageTimerTick;
		base.IsHitTestVisible = true;
		AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnRootPointerWheelChanged), handledEventsToo: true);
		AttachButtonFeedbackHandlers(this);
		ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnProgressPointerPressed), handledEventsToo: true);
		ProgressSlider.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnProgressPointerMoved), handledEventsToo: true);
		ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnProgressPointerReleased), handledEventsToo: true);
		ProgressSlider.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnProgressPointerCanceled), handledEventsToo: true);
		ProgressSlider.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(OnProgressSliderPointerEntered), handledEventsToo: true);
		ProgressSlider.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnProgressSliderPointerExited), handledEventsToo: true);
		RefreshFromViewModel();
		ShowChrome();
	}

	public void Disconnect()
	{
		_danmakuResumeProbeCts?.Cancel();
		_danmakuResumeProbeCts?.Dispose();
		_danmakuResumeProbeCts = null;
		_danmaku?.Close();
		_danmaku = null;
		_autoHideTimer.Stop();
		_autoHideTimer.Tick -= OnAutoHideTimerTick;
		_keyLongPressTimer.Stop();
		_keyLongPressTimer.Tick -= OnKeyLongPressTimerTick;
		_danmakuMessageTimer.Stop();
		_danmakuMessageTimer.Tick -= OnDanmakuMessageTimerTick;
		_spaceKeyDown = false;
		_isHoldSpeedActive = false;
		base.ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		base.ViewModel.ActiveSegments.CollectionChanged -= OnActiveSegmentsChanged;
		base.ViewModel.Chapters.CollectionChanged -= OnChaptersChanged;
	}

	public void HandleUINotify(MpvUIEventId id, object data)
	{
		switch (id)
		{
			case MpvUIEventId.PointerMoved:
				ShowChrome();
				break;
			case MpvUIEventId.Tapped:
				ShowChrome();
				base.ViewModel.TogglePlayPauseCommand.ExecuteAsync(null);
				break;
			case MpvUIEventId.DoubleTapped:
				base.ViewModel.ToggleFullScreenCommand.ExecuteAsync(null);
				break;
			case MpvUIEventId.PreviewPositionChanged:
				ShowChrome();
				break;
			case MpvUIEventId.KeyPressed:
				HandleKeyDown((VirtualKey)data);
				break;
			case MpvUIEventId.KeyReleased:
				HandleKeyUp((VirtualKey)data);
				break;
			case MpvUIEventId.HoldSpeedStart:
				EnterHoldSpeed();
				break;
			case MpvUIEventId.HoldSpeedEnd:
				ExitHoldSpeed();
				break;
			case MpvUIEventId.StateChecked:
				RefreshFromViewModel();
				break;
			case MpvUIEventId.SubtitleFilesDropped:
				ShowChrome();
				break;
			case MpvUIEventId.VolumeChanged:
			case MpvUIEventId.PreviewScrubCommit:
			case MpvUIEventId.PreviewScrubCancel:
				break;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		HandleDanmakuPropertyChanged(e.PropertyName);
		string propertyName = e.PropertyName;
		if ((propertyName == "Duration" || propertyName == "HasActiveSegments") ? true : false)
		{
			RenderSegmentOverlay();
		}
		propertyName = e.PropertyName;
		if ((propertyName == "Duration" || propertyName == "HasChapters") ? true : false)
		{
			RenderChapterOverlay();
		}
		RefreshFromViewModel();
	}

	private void OnActiveSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RenderSegmentOverlay();
	}

	private void OnChaptersChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RenderChapterOverlay();
	}

	private void OnSegmentOverlaySizeChanged(object sender, SizeChangedEventArgs e)
	{
		RenderSegmentOverlay();
	}

	private void RenderSegmentOverlay()
	{
		if (SegmentOverlay is null)
		{
			return;
		}
		SegmentOverlay.Children.Clear();
		double actualWidth = SegmentOverlay.ActualWidth;
		double duration = base.ViewModel.Duration;
		if (actualWidth <= 0.0 || duration <= 0.0 || base.ViewModel.ActiveSegments.Count == 0)
		{
			return;
		}
		foreach (MediaSegment activeSegment in base.ViewModel.ActiveSegments)
		{
			double num = Math.Clamp(activeSegment.StartSeconds, 0.0, duration);
			double num2 = Math.Clamp(activeSegment.EndSeconds ?? duration, 0.0, duration);
			if (!(num2 <= num))
			{
				double length = num / duration * actualWidth;
				double num3 = (num2 - num) / duration * actualWidth;
				if (num3 < 1.0)
				{
					num3 = 1.0;
				}
				Rectangle rectangle = new Rectangle
				{
					Width = num3,
					Height = SegmentOverlay.Height,
					RadiusX = 1.0,
					RadiusY = 1.0,
					Fill = new SolidColorBrush(SegmentColor(activeSegment.Type))
				};
				Canvas.SetLeft(rectangle, length);
				Canvas.SetTop(rectangle, 0.0);
				SegmentOverlay.Children.Add(rectangle);
			}
		}
	}

	private static Color SegmentColor(MediaSegmentType type)
	{
		return type switch
		{
			MediaSegmentType.Recap => Color.FromArgb(204, 79, 195, 247),
			MediaSegmentType.Credits => Color.FromArgb(204, 186, 104, 200),
			MediaSegmentType.Preview => Color.FromArgb(204, 129, 199, 132),
			_ => Color.FromArgb(204, byte.MaxValue, 183, 77),
		};
	}

	private void OnChapterOverlaySizeChanged(object sender, SizeChangedEventArgs e)
	{
		RenderChapterOverlay();
	}

	private void RenderChapterOverlay()
	{
		if (ChapterOverlay is null)
		{
			return;
		}
		ChapterOverlay.Children.Clear();
		double actualWidth = ChapterOverlay.ActualWidth;
		double duration = base.ViewModel.Duration;
		if (actualWidth <= 0.0 || duration <= 0.0 || !base.ViewModel.HasChapters)
		{
			return;
		}
		foreach (TodbChapter chapter in base.ViewModel.Chapters)
		{
			double num = Math.Clamp(chapter.TimeStart, 0.0, duration);
			double num2 = num / duration * actualWidth;
			if (chapter.TimeEnd.HasValue && chapter.TimeEnd.Value > num)
			{
				double num3 = (Math.Clamp(chapter.TimeEnd.Value, 0.0, duration) - num) / duration * actualWidth;
				if (num3 < 1.0)
				{
					num3 = 1.0;
				}
				Rectangle rectangle = new Rectangle
				{
					Width = num3,
					Height = 4.0,
					RadiusX = 2.0,
					RadiusY = 2.0,
					Fill = new SolidColorBrush(ChapterSegmentColor(chapter.MarkerType)),
					Opacity = 0.7,
					Tag = chapter
				};
				Canvas.SetLeft(rectangle, num2);
				Canvas.SetTop(rectangle, 4.0);
				ChapterOverlay.Children.Add(rectangle);
			}
			Border border = new Border
			{
				Width = 3.0,
				Height = 8.0,
				Background = new SolidColorBrush(ChapterMarkerColor(chapter.MarkerType)),
				CornerRadius = new CornerRadius(1.5),
				Tag = chapter
			};
			ToolTipService.SetToolTip(border, chapter.Title ?? chapter.MarkerType);
			Canvas.SetLeft(border, num2 - 1.5);
			Canvas.SetTop(border, 2.0);
			ChapterOverlay.Children.Add(border);
		}
	}

	private static Color ChapterSegmentColor(string markerType)
	{
		return markerType.ToLowerInvariant() switch
		{
			"intro" => Color.FromArgb(170, byte.MaxValue, 183, 77),
			"credits" => Color.FromArgb(170, 186, 104, 200),
			"recap" => Color.FromArgb(170, 79, 195, 247),
			"preview" => Color.FromArgb(170, 129, 199, 132),
			_ => Color.FromArgb(0, 0, 0, 0),
		};
	}

	private void OnChapterOverlayPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		double x = e.GetCurrentPoint(ChapterOverlay).Position.X;
		foreach (UIElement child in ChapterOverlay.Children)
		{
			if (child is Border border)
			{
				double left = Canvas.GetLeft(border);
				if (x >= left - 5.0 && x <= left + border.Width + 5.0 && border.Tag is TodbChapter todbChapter)
				{
					base.ViewModel.Client?.SetCurrentPositionAsync(todbChapter.TimeStart);
					e.Handled = true;
					break;
				}
			}
		}
	}

	private static Color ChapterMarkerColor(string markerType)
	{
		return markerType.ToLowerInvariant() switch
		{
			"intro" => Color.FromArgb(byte.MaxValue, byte.MaxValue, 183, 77),
			"credits" => Color.FromArgb(byte.MaxValue, 186, 104, 200),
			"recap" => Color.FromArgb(byte.MaxValue, 79, 195, 247),
			"preview" => Color.FromArgb(byte.MaxValue, 129, 199, 132),
			_ => Color.FromArgb(byte.MaxValue, 144, 202, 249),
		};
	}

	private void OnProgressSliderPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (base.ViewModel.HasSprite && base.ViewModel.VttEntries.Count > 0)
		{
			ThumbnailPreview.Visibility = Visibility.Visible;
		}
	}

	private void OnProgressSliderPointerExited(object sender, PointerRoutedEventArgs e)
	{
		ThumbnailPreview.Visibility = Visibility.Collapsed;
	}

	private async Task LoadThumbnailAsync(VttSpriteEntry entry)
	{
		if (_thumbnailLoading)
		{
			return;
		}
		try
		{
			_thumbnailLoading = true;
			string cacheKey = $"{entry.ImageUrl}_{entry.X}_{entry.Y}_{entry.Width}_{entry.Height}";
			if (base.ViewModel.SpriteImageCache.TryGetValue(cacheKey, out SoftwareBitmap value))
			{
				if (value is not null)
				{
					SoftwareBitmapSource source = new SoftwareBitmapSource();
					await source.SetBitmapAsync(value);
					ThumbnailImage.Source = source;
				}
				return;
			}
			SoftwareBitmap softwareBitmap = await DownloadAndCropSpriteAsync(entry);
			base.ViewModel.SpriteImageCache[cacheKey] = softwareBitmap;
			if (softwareBitmap is not null)
			{
				SoftwareBitmapSource source = new SoftwareBitmapSource();
				await source.SetBitmapAsync(softwareBitmap);
				ThumbnailImage.Source = source;
			}
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to load thumbnail");
		}
		finally
		{
			_thumbnailLoading = false;
		}
	}

	private async Task<SoftwareBitmap?> DownloadAndCropSpriteAsync(VttSpriteEntry entry)
	{
		_ = 3;
		try
		{
			byte[] value = await _thumbnailClient.GetByteArrayAsync(entry.ImageUrl);
			using InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
			await stream.WriteAsync(CryptographicBuffer.CreateFromByteArray(value));
			stream.Seek(0uL);
			SoftwareBitmap softwareBitmap = await (await BitmapDecoder.CreateAsync(stream)).GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
			SoftwareBitmap softwareBitmap2 = new SoftwareBitmap(BitmapPixelFormat.Bgra8, entry.Width, entry.Height, BitmapAlphaMode.Premultiplied);
			Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer((uint)(softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4));
			softwareBitmap.CopyToBuffer(buffer);
			CryptographicBuffer.CopyToByteArray(buffer, out var value2);
			byte[] array = new byte[entry.Width * entry.Height * 4];
			int num = softwareBitmap.PixelWidth * 4;
			int num2 = entry.Width * 4;
			for (int i = 0; i < entry.Height; i++)
			{
				int sourceIndex = (entry.Y + i) * num + entry.X * 4;
				int destinationIndex = i * num2;
				Array.Copy(value2, sourceIndex, array, destinationIndex, num2);
			}
			IBuffer buffer2 = CryptographicBuffer.CreateFromByteArray(array);
			softwareBitmap2.CopyFromBuffer(buffer2);
			return softwareBitmap2;
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to download/crop sprite");
			return null;
		}
	}

	private static string FormatTime(double seconds)
	{
		if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
		{
			return "00:00";
		}
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
		if (timeSpan.TotalHours >= 1.0)
		{
			return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
		}
		return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
	}

	private void OnRootPointerWheelChanged(object sender, PointerRoutedEventArgs e)
	{
		int mouseWheelDelta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
		if (mouseWheelDelta != 0)
		{
			ShowChrome();
			if (mouseWheelDelta <= 0)
			{
				base.ViewModel.DecreaseVolumeCommand.ExecuteAsync(null);
			}
			else
			{
				base.ViewModel.IncreaseVolumeCommand.ExecuteAsync(null);
			}
			e.Handled = true;
		}
	}

	private void HandleDanmakuPropertyChanged(string? propertyName)
	{
		if (propertyName == null)
		{
			return;
		}
		switch (propertyName.Length)
		{
			case 18:
				switch (propertyName[7])
				{
					default:
						return;
					case 'e':
						if (propertyName == "IsProgressChanging" && !base.ViewModel.IsProgressChanging)
						{
							_danmaku?.UpdateTime(DanmakuTimeMs(base.ViewModel.CurrentPosition));
						}
						return;
					case 'F':
						break;
					case 'O':
						if (propertyName == "DanmakuOutlineSize")
						{
							_danmaku?.SetOutlineSize(base.ViewModel.DanmakuOutlineSize);
						}
						return;
				}
				if (!(propertyName == "DanmakuFollowSpeed"))
				{
					break;
				}
				goto IL_04f8;
			case 16:
				switch (propertyName[7])
				{
					case 'k':
						if (!(propertyName == "IsDanmakuEnabled"))
						{
							break;
						}
						_logger.LogDebug($"PlayerOverlay.HandleDanmakuPropertyChanged IsDanmakuEnabled={base.ViewModel.IsDanmakuEnabled}");
						if (base.ViewModel.IsDanmakuEnabled && !string.IsNullOrWhiteSpace(base.ViewModel.CurrentDanmakuApiBase))
						{
							DanmakuLayer.Visibility = Visibility.Visible;
							if (_danmaku == null)
							{
								InitializeDanmakuAsync();
								break;
							}
							_danmaku.Seek(DanmakuTimeMs(base.ViewModel.CurrentPosition));
							if (base.ViewModel.IsPlaying)
							{
								_danmaku.Resume();
							}
						}
						else
						{
							DanmakuLayer.Visibility = Visibility.Collapsed;
							_danmaku?.Pause();
						}
						break;
					case 'M':
						if (propertyName == "DanmakuMatchName" && base.ViewModel.IsFileLoading && !string.IsNullOrWhiteSpace(base.ViewModel.DanmakuMatchName))
						{
							PrefetchDanmakuAsync();
						}
						break;
					case 'A':
						if (propertyName == "DanmakuAreaRatio")
						{
							_danmaku?.SetRollingAreaRatio(base.ViewModel.DanmakuAreaRatio);
						}
						break;
				}
				break;
			case 14:
				switch (propertyName[7])
				{
					case 'O':
						if (propertyName == "DanmakuOpacity")
						{
							_danmaku?.SetOpacity(base.ViewModel.DanmakuOpacity);
						}
						break;
					case 'D':
						if (propertyName == "DanmakuDensity")
						{
							_danmaku?.SetRollingDensity(base.ViewModel.DanmakuDensity);
						}
						break;
				}
				break;
			case 21:
				switch (propertyName[7])
				{
					default:
						return;
					case 'R':
						break;
					case 'F':
						if (propertyName == "DanmakuFontSizeOffset")
						{
							_danmaku?.SetDanmakuFontSizeOffset(base.ViewModel.DanmakuFontSizeOffset);
						}
						return;
				}
				if (!(propertyName == "DanmakuRollingEnabled"))
				{
					break;
				}
				goto IL_0538;
			case 17:
				switch (propertyName[7])
				{
					default:
						return;
					case 'T':
						break;
					case 'F':
						if (propertyName == "DanmakuFontFamily")
						{
							_danmaku?.SetFontFamilyName(base.ViewModel.DanmakuFontFamily);
						}
						return;
				}
				if (!(propertyName == "DanmakuTopEnabled"))
				{
					break;
				}
				goto IL_0538;
			case 24:
				switch (propertyName[7])
				{
					case 'T':
						if (propertyName == "DanmakuTimeOffsetSeconds")
						{
							_danmaku?.Seek(DanmakuTimeMs(base.ViewModel.CurrentPosition));
						}
						break;
					case 'N':
						if (propertyName == "DanmakuNoOverlapSubtitle")
						{
							_danmaku?.SetNoOverlapSubtitle(base.ViewModel.DanmakuNoOverlapSubtitle);
						}
						break;
				}
				break;
			case 15:
				if (propertyName == "CurrentPosition" && !base.ViewModel.IsProgressChanging)
				{
					_danmaku?.UpdateTime(DanmakuTimeMs(base.ViewModel.CurrentPosition));
				}
				break;
			case 9:
				if (!(propertyName == "IsPlaying"))
				{
					break;
				}
				_logger.LogDebug($"PlayerOverlay.HandleDanmakuPropertyChanged IsPlaying={base.ViewModel.IsPlaying}");
				if (base.ViewModel.IsPlaying)
				{
					if (base.ViewModel.IsDanmakuEnabled)
					{
						_danmaku?.Resume();
						_logger.LogDebug("PlayerOverlay.HandleDanmakuPropertyChanged resume");
					}
				}
				else
				{
					_danmaku?.Pause();
					_logger.LogDebug("PlayerOverlay.HandleDanmakuPropertyChanged pause");
				}
				break;
			case 13:
				if (!(propertyName == "IsFileLoading"))
				{
					break;
				}
				if (base.ViewModel.IsFileLoading)
				{
					if (!base.ViewModel.IsGaplessTransitioning)
					{
						PrefetchDanmakuAsync();
					}
				}
				else
				{
					InitializeDanmakuAsync();
				}
				break;
			case 12:
				if (!(propertyName == "DanmakuSpeed"))
				{
					break;
				}
				goto IL_04f8;
			case 5:
				if (!(propertyName == "Speed"))
				{
					break;
				}
				goto IL_04f8;
			case 20:
				if (!(propertyName == "DanmakuBottomEnabled"))
				{
					break;
				}
				goto IL_0538;
			case 11:
				if (propertyName == "DanmakuBold")
				{
					_danmaku?.SetIsTextBold(base.ViewModel.DanmakuBold);
				}
				break;
			case 6:
			case 7:
			case 8:
			case 10:
			case 19:
			case 22:
			case 23:
				break;
			IL_0538:
				_danmaku?.SetDanmakuEnabledType(base.ViewModel.DanmakuRollingEnabled, base.ViewModel.DanmakuTopEnabled, base.ViewModel.DanmakuBottomEnabled);
				break;
			IL_04f8:
				_danmaku?.SetRollingSpeed(base.ViewModel.DanmakuSpeed * (base.ViewModel.DanmakuFollowSpeed ? base.ViewModel.Speed : 1.0));
				break;
		}
	}

	private uint DanmakuTimeMs(double position)
	{
		return (uint)Math.Max(0.0, (position + base.ViewModel.DanmakuTimeOffsetSeconds) * 1000.0);
	}

	private void ApplyDanmakuDisplaySettings()
	{
		if (_danmaku != null)
		{
			_danmaku.SetOpacity(base.ViewModel.DanmakuOpacity);
			_danmaku.SetRollingAreaRatio(base.ViewModel.DanmakuAreaRatio);
			_danmaku.SetRollingDensity(base.ViewModel.DanmakuDensity);
			_danmaku.SetRollingSpeed(base.ViewModel.DanmakuSpeed * (base.ViewModel.DanmakuFollowSpeed ? base.ViewModel.Speed : 1.0));
			_danmaku.SetDanmakuFontSizeOffset(base.ViewModel.DanmakuFontSizeOffset);
			_danmaku.SetIsTextBold(base.ViewModel.DanmakuBold);
			_danmaku.SetDanmakuEnabledType(base.ViewModel.DanmakuRollingEnabled, base.ViewModel.DanmakuTopEnabled, base.ViewModel.DanmakuBottomEnabled);
			_danmaku.SetFontFamilyName(base.ViewModel.DanmakuFontFamily);
			_danmaku.SetOutlineSize(base.ViewModel.DanmakuOutlineSize);
			_danmaku.SetNoOverlapSubtitle(base.ViewModel.DanmakuNoOverlapSubtitle);
		}
	}

	private void ClearActiveDanmaku()
	{
		_lastDanmakuUrl = null;
		_danmaku?.Pause();
		_danmaku?.SetDanmakuList(new List<DanmakuItem>());
		DanmakuLayer.Visibility = Visibility.Collapsed;
		HideDanmakuMessage();
		_logger.LogDebug("PlayerOverlay.ClearActiveDanmaku cleared");
	}

	private void ShowDanmakuMessage(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
		{
			DanmakuMessageText.Text = message;
			DanmakuMessageBorder.Visibility = Visibility.Visible;
			_danmakuMessageTimer.Stop();
			_danmakuMessageTimer.Start();
			_logger.LogWarning("PlayerOverlay danmaku status: {Message}", message);
		}
	}

	private void HideDanmakuMessage()
	{
		_danmakuMessageTimer.Stop();
		DanmakuMessageBorder.Visibility = Visibility.Collapsed;
	}

	private void OnDanmakuMessageTimerTick(object? sender, object e)
	{
		HideDanmakuMessage();
	}

	private async void PrefetchDanmakuAsync()
	{
		string matchName = base.ViewModel.DanmakuMatchName;
		IReadOnlyList<DanmakuApiEntry> apis = base.ViewModel.DanmakuApis;
		if (apis.Count != 0 && !string.IsNullOrWhiteSpace(matchName))
		{
			string text = matchName;
			if (!string.Equals(text, _lastPrefetchKey, StringComparison.Ordinal))
			{
				_lastPrefetchKey = text;
				EnsureDanmakuRenderer();
				_logger.LogDebug($"PlayerOverlay.PrefetchDanmakuAsync matchName={matchName} apis={apis.Count}");
				await TryRestorePersistedDanmakuSelectionsAsync();
				await DanmakuParser.PrefetchAllAsync(apis, matchName);
				_logger.LogDebug("PlayerOverlay.PrefetchDanmakuAsync prefetch_started matchName={MatchName}", matchName);
			}
		}
		else
		{
			// "想预取但没条件预取"必须留痕：否则"没有弹幕"与"根本没去取"在日志里长得一样。
			_logger.LogInformation("[DANMAKU-SKIP] prefetch skipped apis={ApiCount} matchNameEmpty={MatchNameEmpty}", apis.Count, string.IsNullOrWhiteSpace(matchName));
		}
	}

	private void EnsureDanmakuRenderer()
	{
		if (_danmaku == null && !(DanmakuLayer.ActualWidth <= 1.0) && !(DanmakuLayer.ActualHeight <= 1.0))
		{
			_logger.LogDebug("PlayerOverlay.EnsureDanmakuRenderer creating DanmakuFrostMaster");
			_danmaku = new DanmakuFrostMaster(DanmakuLayer);
			ApplyDanmakuDisplaySettings();
		}
	}

	private async void InitializeDanmakuAsync()
	{
		string matchName = base.ViewModel.DanmakuMatchName;
		IReadOnlyList<DanmakuApiEntry> apis = base.ViewModel.DanmakuApis;
		_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync apis={apis.Count} matchName={matchName ?? string.Empty} position={base.ViewModel.CurrentPosition:0.###} isPlaying={base.ViewModel.IsPlaying} layer={FormatLayerSize()}");
		if (apis.Count == 0 || string.IsNullOrWhiteSpace(matchName))
		{
			if (string.IsNullOrWhiteSpace(matchName))
			{
				ClearActiveDanmaku();
			}
			// 同 PrefetchDanmakuAsync：这里的 return 过去完全静默 ⇒ "没配弹幕源"与"弹幕源没命中"不可区分。
			_logger.LogInformation("[DANMAKU-SKIP] initialize skipped apis={ApiCount} matchNameEmpty={MatchNameEmpty}", apis.Count, string.IsNullOrWhiteSpace(matchName));
			return;
		}
		if ((DanmakuLayer.ActualWidth <= 1.0 || DanmakuLayer.ActualHeight <= 1.0) && !_retryingDanmakuInitialize)
		{
			_retryingDanmakuInitialize = true;
			_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync layer not ready, schedule retry");
			base.DispatcherQueue.TryEnqueue(async delegate
			{
				await Task.Delay(16);
				_retryingDanmakuInitialize = false;
				InitializeDanmakuAsync();
			});
			return;
		}
		EnsureDanmakuRenderer();
		await TryRestorePersistedDanmakuSelectionsAsync();
		IReadOnlyList<DanmakuApiMatchResult> readOnlyList = await DanmakuParser.MatchAllWithStatusAsync(apis, matchName);
		foreach (DanmakuApiMatchResult item in readOnlyList)
		{
			if (item.Attempt.IsSuccess)
			{
				base.ViewModel.MergeDanmakuMatchCandidates(item.ApiIndex, item.Attempt.Results);
			}
		}
		int selectedDanmakuApiIndex = base.ViewModel.SelectedDanmakuApiIndex;
		if (HasManualSelection(selectedDanmakuApiIndex))
		{
			string danmakuActiveCommentUrl = base.ViewModel.GetDanmakuActiveCommentUrl(selectedDanmakuApiIndex);
			_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync preferred manual apiIndex={ApiIndex} url={Url}", selectedDanmakuApiIndex, danmakuActiveCommentUrl);
			await LoadDanmakuByUrlAsync(danmakuActiveCommentUrl, selectedDanmakuApiIndex, updateSelection: false);
			return;
		}
		var (num, danmakuMatchAttempt) = ResolveDanmakuApiMatch(readOnlyList, selectedDanmakuApiIndex);
		_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync apiIndex={num} resolved_url={danmakuMatchAttempt.Result?.CommentUrl ?? "null"} count={danmakuMatchAttempt.Results.Count}");
		if (!danmakuMatchAttempt.IsSuccess)
		{
			for (int num2 = 0; num2 < apis.Count; num2++)
			{
				string danmakuActiveCommentUrl2 = base.ViewModel.GetDanmakuActiveCommentUrl(num2);
				if (!string.IsNullOrWhiteSpace(danmakuActiveCommentUrl2))
				{
					base.ViewModel.SelectedDanmakuApiIndex = num2;
					await LoadDanmakuByUrlAsync(danmakuActiveCommentUrl2, num2, updateSelection: false);
					return;
				}
			}
			ShowDanmakuMessage(danmakuMatchAttempt.ErrorMessage ?? "弹幕匹配失败");
			return;
		}
		if (num != base.ViewModel.SelectedDanmakuApiIndex)
		{
			_logger.LogInformation("PlayerOverlay.InitializeDanmakuAsync switched api {From} -> {To}", base.ViewModel.SelectedDanmakuApiIndex, num);
			base.ViewModel.SelectedDanmakuApiIndex = num;
		}
		string text = base.ViewModel.GetDanmakuActiveCommentUrl(num) ?? danmakuMatchAttempt.Results[0].CommentUrl;
		if (string.Equals(text, _lastDanmakuUrl, StringComparison.Ordinal) && _danmaku != null)
		{
			_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync cache_hit seek");
			_danmaku.Seek(DanmakuTimeMs(base.ViewModel.CurrentPosition));
			if (!base.ViewModel.IsPlaying || !base.ViewModel.IsDanmakuEnabled)
			{
				_danmaku.Pause();
			}
			return;
		}
		_lastDanmakuUrl = text;
		try
		{
			if (_danmaku == null)
			{
				_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync creating DanmakuFrostMaster xamlRoot={DanmakuLayer.XamlRoot is not null}");
				_danmaku = new DanmakuFrostMaster(DanmakuLayer);
				ApplyDanmakuDisplaySettings();
				_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync layer children after ctor=" + DescribeLayerChildren());
			}
			DanmakuFetchAttempt danmakuFetchAttempt = await DanmakuParser.FetchCommentAsync(text);
			List<DanmakuItem> items = ((danmakuFetchAttempt.Items is List<DanmakuItem> list) ? list : danmakuFetchAttempt.Items.ToList());
			_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync items={items.Count}");
			if (!danmakuFetchAttempt.HasItems)
			{
				ShowDanmakuMessage(danmakuFetchAttempt.ErrorMessage ?? "弹幕加载失败或内容为空");
				return;
			}
			HideDanmakuMessage();
			if (DanmakuLayer.Children.Count > 0)
			{
				UIElement uIElement = DanmakuLayer.Children[0];
				if (uIElement is FrameworkElement { IsLoaded: false } canvasEl)
				{
					TaskCompletionSource tcs = new TaskCompletionSource();
					canvasEl.Loaded += delegate
					{
						tcs.TrySetResult();
					};
					_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync waiting for canvas Loaded");
					try
					{
						await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2L));
					}
					catch (TimeoutException)
					{
						_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync canvas Loaded timeout");
					}
					_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync canvas IsLoaded={canvasEl.IsLoaded}");
					goto IL_094c;
				}
			}
			_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync canvas already loaded or no children (count={DanmakuLayer.Children.Count})");
			goto IL_094c;
		IL_094c:
			_danmaku.SetDanmakuList(items);
			uint num3 = DanmakuTimeMs(base.ViewModel.CurrentPosition);
			_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync posMs={num3} isPlaying={base.ViewModel.IsPlaying} layer={FormatLayerSize()}");
			_danmaku.Seek(num3);
			_logger.LogDebug($"PlayerOverlay.InitializeDanmakuAsync seek posMs={num3}");
			if (!base.ViewModel.IsPlaying || !base.ViewModel.IsDanmakuEnabled)
			{
				_danmaku.Pause();
				_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync paused after seek");
				if (base.ViewModel.IsDanmakuEnabled)
				{
					StartDanmakuResumeProbe();
				}
				else
				{
					_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync danmaku disabled, probe skipped");
				}
			}
			else
			{
				_logger.LogDebug("PlayerOverlay.InitializeDanmakuAsync playing");
			}
		}
		catch (Exception ex2)
		{
			_logger.LogWarning(ex2, "PlayerOverlay.InitializeDanmakuAsync error");
			ShowDanmakuMessage("弹幕加载出错：" + ex2.Message);
		}
	}

	private string FormatLayerSize()
	{
		return $"{DanmakuLayer.ActualWidth:0.##}x{DanmakuLayer.ActualHeight:0.##}";
	}

	private string DescribeLayerChildren()
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(DanmakuLayer);
		if (childrenCount == 0)
		{
			return "0";
		}
		List<string> list = new List<string>();
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(DanmakuLayer, i);
			list.Add($"{i}:{child.GetType().FullName}");
		}
		return string.Join(" | ", list);
	}

	private void StartDanmakuResumeProbe()
	{
		_danmakuResumeProbeCts?.Cancel();
		_danmakuResumeProbeCts?.Dispose();
		_danmakuResumeProbeCts = new CancellationTokenSource();
		CancellationToken token = _danmakuResumeProbeCts.Token;
		double currentPosition = base.ViewModel.CurrentPosition;
		_logger.LogDebug($"PlayerOverlay.StartDanmakuResumeProbe startPosition={currentPosition:0.###} isPlaying={base.ViewModel.IsPlaying}");
		ProbeDanmakuResumeAsync(token, currentPosition);
	}

	private async Task ProbeDanmakuResumeAsync(CancellationToken token, double startPosition)
	{
		try
		{
			for (int attempt = 1; attempt <= 20; attempt++)
			{
				await Task.Delay(200, token);
				bool flag = base.ViewModel.IsPlaying || base.ViewModel.CurrentPosition > startPosition + 0.2;
				_logger.LogDebug($"PlayerOverlay.StartDanmakuResumeProbe attempt={attempt} isPlaying={base.ViewModel.IsPlaying} position={base.ViewModel.CurrentPosition:0.###} shouldResume={flag}");
				if (flag && _danmaku != null)
				{
					_danmaku.Resume();
					_logger.LogDebug("PlayerOverlay.StartDanmakuResumeProbe resume");
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("PlayerOverlay.StartDanmakuResumeProbe canceled");
		}
	}

	private void RefreshFromViewModel()
	{
		_isUpdatingFromViewModel = true;
		try
		{
			bool flag = (base.ViewModel.IsFileLoading || base.ViewModel.IsSourceLoading) && !base.ViewModel.IsGaplessTransitioning && !base.ViewModel.IsStreamRefreshTransitioning;
			if (flag && LoadingRingHost.Visibility != Visibility.Visible)
			{
				_logger.LogInformation($"LoadingRing → Visible (IsFileLoading={base.ViewModel.IsFileLoading} IsSourceLoading={base.ViewModel.IsSourceLoading})");
			}
			else if (!flag && LoadingRingHost.Visibility == Visibility.Visible)
			{
				_logger.LogInformation("LoadingRing → Collapsed");
			}
			LoadingRingHost.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			bool isBackdropVisible = base.ViewModel.IsBackdropVisible;
			VideoBackdrop.Visibility = ((!isBackdropVisible) ? Visibility.Collapsed : Visibility.Visible);
			if (isBackdropVisible)
			{
				string text = base.ViewModel.MediaBackdropUrl?.Trim() ?? string.Empty;
				if (((VideoBackdropImage.Source as BitmapImage)?.UriSource?.ToString() ?? string.Empty) != text)
				{
					VideoBackdropImage.Source = (string.IsNullOrWhiteSpace(text) ? null : new BitmapImage(new Uri(text)));
				}
			}
			PlayPauseIcon.Symbol = (base.ViewModel.IsPlaying ? FluentIcons.Common.Symbol.Pause : FluentIcons.Common.Symbol.Play);
			FullScreenIcon.Symbol = (base.ViewModel.IsFullScreen ? FluentIcons.Common.Symbol.FullScreenMinimize : FluentIcons.Common.Symbol.FullScreenMaximize);
			RefreshTopmostButton();
			MaximizeRestoreIcon.Text = (base.ViewModel.IsWindowMaximized ? "\ue923" : "\ue922");
			NetworkSpeedBadge.Visibility = ((!base.ViewModel.IsNetworkSpeedVisible) ? Visibility.Collapsed : Visibility.Visible);
			BadgeItemsControl.Visibility = ((base.ViewModel.MediaBadges.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
			RefreshCompactOverlayChrome();
			RefreshLogo();
			ProgressSlider.Maximum = Math.Max(0.0, base.ViewModel.Duration);
			if (!_isScrubPointerDown)
			{
				double val = (base.ViewModel.IsProgressChanging ? base.ViewModel.PreviewPosition : base.ViewModel.CurrentPosition);
				ProgressSlider.Value = Math.Max(0.0, Math.Min(ProgressSlider.Maximum, val));
			}
			UpdateBufferTrack();
			VolumeSlider.Value = Math.Clamp(base.ViewModel.Volume, 0.0, base.ViewModel.MaxVolume);
			if (base.ViewModel.Volume > 0.0)
			{
				_lastNonZeroVolume = base.ViewModel.Volume;
			}
			SpeedTextBlock.Text = $"{((base.ViewModel.Speed <= 0.0) ? 1.0 : base.ViewModel.Speed):0.0}x";
			VolumeIcon.Symbol = ((base.ViewModel.Volume <= 0.0) ? FluentIcons.Common.Symbol.SpeakerMute : ((base.ViewModel.Volume < 45.0) ? FluentIcons.Common.Symbol.Speaker1 : FluentIcons.Common.Symbol.Speaker2));
			AudioTrackButton.IsEnabled = base.ViewModel.AudioTracks.Count > 0;
			VersionButton.Visibility = ((base.ViewModel.VersionOptions.Count <= 1) ? Visibility.Collapsed : Visibility.Visible);
			SubtitleTrackButton.IsEnabled = true;
			EpisodeListButton.Visibility = ((!base.ViewModel.HasEpisodeList) ? Visibility.Collapsed : Visibility.Visible);
			ToolTipService.SetToolTip(EpisodeListButton, "选集");
			bool flag2 = base.ViewModel.DanmakuApis.Count > 0;
			DanmakuButton.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			DanmakuIcon.Symbol = (base.ViewModel.IsDanmakuEnabled ? FluentIcons.Common.Symbol.Comment : FluentIcons.Common.Symbol.CommentOff);
			ToolTipService.SetToolTip(DanmakuButton, "弹幕");
			ToolTipService.SetToolTip(PreviousEpisodeButton, "上一集");
			ToolTipService.SetToolTip(BackwardButton, "后退 10 秒");
			ToolTipService.SetToolTip(PlayPauseButton, base.ViewModel.IsPlaying ? "暂停" : "播放");
			ToolTipService.SetToolTip(ForwardButton, "前进 10 秒");
			ToolTipService.SetToolTip(NextEpisodeButton, "下一集");
			ToolTipService.SetToolTip(MuteButton, (base.ViewModel.Volume <= 0.0) ? "恢复音量" : "静音");
			ToolTipService.SetToolTip(SpeedButton, "播放速度 " + SpeedTextBlock.Text);
			ToolTipService.SetToolTip(VersionButton, base.ViewModel.SelectedVersionLabel);
			ToolTipService.SetToolTip(AudioTrackButton, base.ViewModel.SelectedAudioTrackLabel);
			ToolTipService.SetToolTip(SubtitleTrackButton, base.ViewModel.SelectedSubtitleTrackLabel);
			ToolTipService.SetToolTip(SkipButton, "跳过片头/片尾");
			ToolTipService.SetToolTip(SettingsButton, "更多设置");
			ToolTipService.SetToolTip(FullScreenButton, base.ViewModel.IsFullScreen ? "退出全屏" : "进入全屏");
			ToolTipService.SetToolTip(MinimizeButton, "最小化");
			ToolTipService.SetToolTip(MaximizeRestoreButton, base.ViewModel.IsWindowMaximized ? "还原" : "最大化");
			ToolTipService.SetToolTip(CloseButton, "关闭");
			ToolTipService.SetToolTip(ExitPipButton, "退出画中画");
		}
		finally
		{
			_isUpdatingFromViewModel = false;
		}
	}

	private void RefreshCompactOverlayChrome()
	{
		bool isCompactOverlay = base.ViewModel.IsCompactOverlay;
		Visibility visibility = ((!isCompactOverlay) ? Visibility.Collapsed : Visibility.Visible);
		Visibility visibility2 = (isCompactOverlay ? Visibility.Collapsed : Visibility.Visible);
		TopMetadataPanel.Visibility = visibility2;
		LogoContainer.Visibility = visibility2;
		NetworkSpeedBadge.Visibility = (isCompactOverlay ? Visibility.Collapsed : NetworkSpeedBadge.Visibility);
		BadgeItemsControl.Visibility = (isCompactOverlay ? Visibility.Collapsed : BadgeItemsControl.Visibility);
		CurrentPositionBlock.Visibility = visibility2;
		DurationBlock.Visibility = visibility2;
		SecondaryControlsPanel.Visibility = visibility2;
		ExitPipButton.Visibility = visibility;
		WindowButtonsPanel.Visibility = ((!isCompactOverlay && !base.ViewModel.IsWindowButtonsVisible) ? Visibility.Collapsed : Visibility.Visible);
		TopmostButton.Visibility = visibility2;
		MinimizeButton.Visibility = visibility2;
		MaximizeRestoreButton.Visibility = visibility2;
	}

	private void RefreshTopmostButton()
	{
		if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var value) && value is Color color)
		{
			TopmostButton.Foreground = (base.ViewModel.IsTopmost ? new SolidColorBrush(color) : new SolidColorBrush(new Color
			{
				A = byte.MaxValue,
				R = byte.MaxValue,
				G = byte.MaxValue,
				B = byte.MaxValue
			}));
		}
	}

	private void RefreshLogo()
	{
		string text = base.ViewModel.MediaLogo?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			LogoImage.Source = null;
			LogoImage.Visibility = Visibility.Collapsed;
			return;
		}
		if (LogoImage.Source is BitmapImage { UriSource: not null } bitmapImage && string.Equals(bitmapImage.UriSource.AbsoluteUri, text, StringComparison.OrdinalIgnoreCase))
		{
			LogoImage.Visibility = Visibility.Visible;
			return;
		}
		try
		{
			LogoImage.Source = new BitmapImage(new Uri(text));
			LogoImage.Visibility = Visibility.Visible;
		}
		// t235：命名类型 = `Exception`，**不能再窄** —— 本 try 内两处可抛集合不同（`new Uri(text)`：`UriFormatException`；
		// `BitmapImage` 在远端图不可解码时可抛 WinRT/COM 异常）⇒ 共同祖先只有 `Exception`；契约 = 任何失败都回落"隐藏 Logo"。
		catch (Exception)
		{
			LogoImage.Source = null;
			LogoImage.Visibility = Visibility.Collapsed;
		}
	}

	private void UpdateBufferTrack()
	{
		if (base.ViewModel.Duration <= 0.0 || double.IsNaN(base.ViewModel.BufferedPosition) || base.ViewModel.BufferedPosition <= 0.0)
		{
			BufferBar.Maximum = 1.0;
			BufferBar.Value = 0.0;
		}
		else
		{
			BufferBar.Maximum = base.ViewModel.Duration;
			BufferBar.Value = Math.Clamp(base.ViewModel.BufferedPosition, 0.0, base.ViewModel.Duration);
		}
	}

	private void ShowChrome()
	{
		base.IsHitTestVisible = true;
		ChromeRoot.Visibility = Visibility.Visible;
		base.ViewModel.IsControlVisible = true;
		base.ViewModel.Window?.ShowCursor();
		RestartAutoHideTimer();
	}

	private void RefreshChromeIfVisible()
	{
		if (base.ViewModel.IsControlVisible)
		{
			ShowChrome();
		}
	}

	private void HideChrome()
	{
		if (!base.ViewModel.IsRestartVisible && !base.ViewModel.IsPickerOpen && !_isPointerInsideChrome)
		{
			ChromeRoot.Visibility = Visibility.Collapsed;
			base.IsHitTestVisible = false;
			base.ViewModel.IsControlVisible = false;
			base.ViewModel.Window?.HideCursor();
			_autoHideTimer.Stop();
		}
	}

	private void RestartAutoHideTimer()
	{
		_autoHideTimer.Stop();
		if (!base.ViewModel.IsRestartVisible)
		{
			_autoHideTimer.Start();
		}
	}

	private void OnAutoHideTimerTick(object? sender, object e)
	{
		HideChrome();
	}

	private async void OnPlayPauseClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.TogglePlayPauseCommand.ExecuteAsync(null);
	}

	private async void OnBackwardClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.BackwardSkipCommand.ExecuteAsync(null);
	}

	private async void OnForwardClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.ForwardSkipCommand.ExecuteAsync(null);
	}

	private async void OnPreviousEpisodeClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.NavigatePreviousEpisodeCommand.ExecuteAsync(null);
	}

	private async void OnNextEpisodeClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.NavigateNextEpisodeCommand.ExecuteAsync(null);
	}

	private async void OnFullScreenClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.ToggleFullScreenCommand.ExecuteAsync(null);
	}

	private void OnTopmostClick(object sender, RoutedEventArgs e)
	{
		base.ViewModel.ToggleTopmostCommand.Execute(null);
		RefreshTopmostButton();
	}

	private void OnCloseClick(object sender, RoutedEventArgs e)
	{
		base.ViewModel.CloseWindowCommand.Execute(null);
	}

	private void OnMinimizeClick(object sender, RoutedEventArgs e)
	{
		base.ViewModel.MinimizeWindowCommand.Execute(null);
	}

	private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
	{
		base.ViewModel.ToggleMaximizeRestoreWindowCommand.Execute(null);
	}

	private async void OnMuteClick(object sender, RoutedEventArgs e)
	{
		double parameter = ((base.ViewModel.Volume <= 0.0) ? Math.Clamp((_lastNonZeroVolume <= 0.0) ? 100.0 : _lastNonZeroVolume, 0.0, base.ViewModel.MaxVolume) : 0.0);
		await base.ViewModel.SetVolumeValueCommand.ExecuteAsync(parameter);
	}

	private void OnProgressValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		if (!_isUpdatingFromViewModel && base.ViewModel.HasMedia)
		{
			base.ViewModel.PreviewPosition = e.NewValue;
			base.ViewModel.IsProgressChanging = true;
		}
	}

	private void OnProgressPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (base.ViewModel.HasMedia && (e.GetCurrentPoint(ProgressSlider).Properties.IsLeftButtonPressed || e.Pointer.PointerDeviceType == PointerDeviceType.Touch || e.Pointer.PointerDeviceType == PointerDeviceType.Pen))
		{
			_isScrubPointerDown = true;
			_scrubPointerId = e.Pointer.PointerId;
			ShowChrome();
			double previewPosition = PositionFromPointer(ProgressSlider, e);
			base.ViewModel.PreviewPosition = previewPosition;
			base.ViewModel.IsProgressChanging = true;
		}
	}

	private void OnProgressPointerMoved(object sender, PointerRoutedEventArgs e)
	{
		UpdateThumbnailPreview(e);
		if (_isScrubPointerDown && e.Pointer.PointerId == _scrubPointerId)
		{
			ShowChrome();
			base.ViewModel.PreviewPosition = PositionFromPointer(ProgressSlider, e);
		}
	}

	private void UpdateThumbnailPreview(PointerRoutedEventArgs e)
	{
		if (!base.ViewModel.HasSprite || base.ViewModel.VttEntries.Count == 0 || ProgressSlider is null)
		{
			return;
		}
		double duration = base.ViewModel.Duration;
		if (!(duration <= 0.0))
		{
			double x = e.GetCurrentPoint(ProgressSlider).Position.X;
			double actualWidth = ProgressSlider.ActualWidth;
			double seconds = Math.Clamp(x / actualWidth * duration, 0.0, duration);
			ThumbnailTimeText.Text = FormatTime(seconds);
			double num = 176.0;
			Point point = ProgressSlider.TransformToVisual(this).TransformPoint(new Point(0f, 0f));
			double value = point.X + x - num / 2.0;
			int num2 = 20;
			double max = base.ActualWidth - num - 20.0;
			value = Math.Clamp(value, num2, max);
			double num3 = 106.0;
			double val = point.Y - num3 - 8.0;
			Canvas.SetLeft(ThumbnailPreview, value);
			Canvas.SetTop(ThumbnailPreview, Math.Max(0.0, val));
			ThumbnailPreview.Visibility = Visibility.Visible;
			VttSpriteEntry vttSpriteEntry = VttSpriteParser.FindEntry(base.ViewModel.VttEntries, seconds);
			if (vttSpriteEntry != null)
			{
				LoadThumbnailAsync(vttSpriteEntry);
			}
		}
	}

	private async void OnProgressPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (_isScrubPointerDown && e.Pointer.PointerId == _scrubPointerId)
		{
			_isScrubPointerDown = false;
			_scrubPointerId = 0u;
			await base.ViewModel.SeekToCommand.ExecuteAsync(base.ViewModel.PreviewPosition);
			base.ViewModel.IsProgressChanging = false;
		}
	}

	private void OnProgressPointerCanceled(object sender, PointerRoutedEventArgs e)
	{
		if (_isScrubPointerDown && e.Pointer.PointerId == _scrubPointerId)
		{
			_isScrubPointerDown = false;
			_scrubPointerId = 0u;
			base.ViewModel.IsProgressChanging = false;
		}
	}

	private double PositionFromPointer(Slider slider, PointerRoutedEventArgs e)
	{
		PointerPoint currentPoint = e.GetCurrentPoint(slider);
		double num = Math.Max(1.0, slider.ActualWidth);
		double num2 = Math.Clamp(currentPoint.Position.X, 0.0, num) / num;
		double num3 = Math.Max(0.0, base.ViewModel.Duration);
		return num2 * num3;
	}

	private async void OnVolumeValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		if (!_isUpdatingFromViewModel && !(Math.Abs(e.NewValue - e.OldValue) < 1.0))
		{
			await base.ViewModel.SetVolumeValueCommand.ExecuteAsync(e.NewValue);
		}
	}

	private void OnTopZonePointerEntered(object sender, PointerRoutedEventArgs e)
	{
		ShowChrome();
	}

	private void OnTopZonePointerExited(object sender, PointerRoutedEventArgs e)
	{
		RestartAutoHideTimer();
	}

	private void OnBottomZonePointerEntered(object sender, PointerRoutedEventArgs e)
	{
		ShowChrome();
	}

	private void OnBottomZonePointerExited(object sender, PointerRoutedEventArgs e)
	{
		RestartAutoHideTimer();
	}

	private void OnBottomZonePointerMoved(object sender, PointerRoutedEventArgs e)
	{
		RestartAutoHideTimer();
	}

	private void OnTopBarPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed && !IsInteractiveElement(e.OriginalSource as DependencyObject) && base.ViewModel.CanDragWindow() && GetCursorPos(out _dragCursorStart))
		{
			_dragWindowStart = base.ViewModel.GetWindowPosition();
			_isTopBarDragging = true;
			_topBarPointerId = e.Pointer.PointerId;
			(sender as UIElement)?.CapturePointer(e.Pointer);
			e.Handled = true;
		}
	}

	private void OnTopBarPointerMoved(object sender, PointerRoutedEventArgs e)
	{
		RestartAutoHideTimer();
		if (_isTopBarDragging && e.Pointer.PointerId == _topBarPointerId)
		{
			NativePoint lpPoint;
			if (!e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
			{
				OnTopBarPointerReleased(sender, e);
			}
			else if (GetCursorPos(out lpPoint))
			{
				int left = _dragWindowStart.X + (lpPoint.X - _dragCursorStart.X);
				int top = _dragWindowStart.Y + (lpPoint.Y - _dragCursorStart.Y);
				base.ViewModel.MoveWindowTo(left, top);
				e.Handled = true;
			}
		}
	}

	private void OnTopBarPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (e.Pointer.PointerId == _topBarPointerId || _topBarPointerId == 0)
		{
			(sender as UIElement)?.ReleasePointerCaptures();
			_isTopBarDragging = false;
			_topBarPointerId = 0u;
		}
	}

	private void OnTopBarDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (!IsInteractiveElement(e.OriginalSource as DependencyObject) && !base.ViewModel.IsFullScreen)
		{
			base.ViewModel.ToggleMaximizeRestoreWindowCommand.Execute(null);
			e.Handled = true;
		}
	}

	private void OnLogoImageFailed(object sender, ExceptionRoutedEventArgs e)
	{
		LogoImage.Source = null;
		LogoImage.Visibility = Visibility.Collapsed;
	}

	private void OnAudioTrackFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildTrackFlyout(AudioTrackFlyout, base.ViewModel.AudioTracks, base.ViewModel.SelectAudioTrackCommand);
	}

	private void OnVersionFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildVersionFlyout(VersionFlyout, base.ViewModel.VersionOptions, base.ViewModel.SelectVersionCommand);
	}

	private void OnSubtitleDragEnter(object sender, DragEventArgs e)
	{
		UpdateSubtitleDragOperation(e);
	}

	private void OnSubtitleDragOver(object sender, DragEventArgs e)
	{
		UpdateSubtitleDragOperation(e);
	}

	private static void UpdateSubtitleDragOperation(DragEventArgs e)
	{
		if (SubtitleDropUtility.CanAccept(e.DataView))
		{
			SubtitleDropUtility.AcceptDrag(e);
		}
		else
		{
			e.AcceptedOperation = DataPackageOperation.None;
		}
	}

	private async void OnSubtitleDrop(object sender, DragEventArgs e)
	{
		IReadOnlyList<string> readOnlyList = await SubtitleDropUtility.GetSubtitlePathsAsync(e.DataView);
		if (readOnlyList.Count == 0)
		{
			return;
		}
		e.Handled = true;
		ShowChrome();
		foreach (string item in readOnlyList)
		{
			await base.ViewModel.LoadSubtitleFileAsync(item);
		}
	}

	private void OnSubtitleTrackFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		base.ViewModel.RefreshSubtitleTracksFromMpv();
		RebuildTrackFlyout(SubtitleTrackFlyout, base.ViewModel.SubtitleTracks, base.ViewModel.SelectSubtitleTrackCommand);
		SubtitleTrackFlyout.Items.Add(new MenuFlyoutSeparator());
		SubtitleTrackFlyout.Items.Add(new MenuFlyoutItem
		{
			Text = "添加本地字幕...",
			Icon = new FluentIcons.WinUI.SymbolIcon
			{
				Symbol = FluentIcons.Common.Symbol.Add
			},
			Command = base.ViewModel.AddExternalSubtitleCommand
		});
		MenuFlyoutItem menuFlyoutItem = new MenuFlyoutItem
		{
			Text = "搜索在线字幕...",
			Icon = new FluentIcons.WinUI.SymbolIcon
			{
				Symbol = FluentIcons.Common.Symbol.Search
			}
		};
		menuFlyoutItem.Click += OnSearchOnlineSubtitleClick;
		SubtitleTrackFlyout.Items.Add(menuFlyoutItem);
		MenuFlyoutSubItem menuFlyoutSubItem = new MenuFlyoutSubItem
		{
			Text = "字幕同步"
		};
		menuFlyoutSubItem.Items.Add(new MenuFlyoutItem
		{
			Text = "提前 0.5s",
			Command = base.ViewModel.SetSubtitleDelayCommand,
			CommandParameter = base.ViewModel.SubtitleDelay - 0.5
		});
		menuFlyoutSubItem.Items.Add(new MenuFlyoutItem
		{
			Text = "滞后 0.5s",
			Command = base.ViewModel.SetSubtitleDelayCommand,
			CommandParameter = base.ViewModel.SubtitleDelay + 0.5
		});
		menuFlyoutSubItem.Items.Add(new MenuFlyoutItem
		{
			Text = "重置",
			Command = base.ViewModel.SetSubtitleDelayCommand,
			CommandParameter = 0.0
		});
		menuFlyoutSubItem.Items.Add(new MenuFlyoutSeparator());
		MenuFlyoutItem menuFlyoutItem2 = new MenuFlyoutItem
		{
			Text = "输入偏移值...",
			Icon = new FluentIcons.WinUI.SymbolIcon
			{
				Symbol = FluentIcons.Common.Symbol.Edit
			}
		};
		menuFlyoutItem2.Click += OnSubtitleSyncClick;
		menuFlyoutSubItem.Items.Add(menuFlyoutItem2);
		menuFlyoutSubItem.Items.Add(new MenuFlyoutItem
		{
			Text = $"当前偏移: {base.ViewModel.SubtitleDelay:0.###}s",
			IsEnabled = false
		});
		SubtitleTrackFlyout.Items.Add(menuFlyoutSubItem);
	}

	private void OnSpeedFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		foreach (ToggleMenuFlyoutItem item in SpeedFlyout.Items.OfType<ToggleMenuFlyoutItem>())
		{
			item.IsChecked = item.Tag is string s && double.TryParse(s, out var result) && Math.Abs(result - ((base.ViewModel.Speed <= 0.0) ? 1.0 : base.ViewModel.Speed)) < 0.01;
		}
	}

	private void OnSettingsFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildVideoFitModeFlyout();
		RebuildAnimeModeSubMenu();
		RebuildSharpenModeSubMenu();
		CompactOverlayMenuItem.Text = (base.ViewModel.IsCompactOverlay ? "退出画中画" : "画中画");
	}

	private void RebuildVideoFitModeFlyout()
	{
		VideoFitModeSubItem.Items.Clear();
		VideoFitMode[] array = new VideoFitMode[3]
		{
			VideoFitMode.Contain,
			VideoFitMode.Cover,
			VideoFitMode.Stretch
		};
		foreach (VideoFitMode videoFitMode in array)
		{
			VideoFitModeSubItem.Items.Add(new ToggleMenuFlyoutItem
			{
				Text = VideoFitModeText(videoFitMode),
				IsChecked = (base.ViewModel.VideoFitMode == videoFitMode),
				Command = base.ViewModel.SelectVideoFitModeCommand,
				CommandParameter = videoFitMode
			});
		}
	}

	private static string VideoFitModeText(VideoFitMode mode)
	{
		return mode switch
		{
			VideoFitMode.Cover => "填充",
			VideoFitMode.Stretch => "拉伸",
			_ => "适应",
		};
	}

	private void RebuildAnimeModeSubMenu()
	{
		AnimeModeSubItem.Items.Clear();
		(string, string)[] array = new (string, string)[4]
		{
			("none", "未设置"),
			("standard", "标准"),
			("soft", "柔和"),
			("denoise", "降噪")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (text, text2) = array[i];
			AnimeModeSubItem.Items.Add(new ToggleMenuFlyoutItem
			{
				Text = text2,
				IsChecked = string.Equals(base.ViewModel.CurrentAnimeMode, text, StringComparison.OrdinalIgnoreCase),
				Command = base.ViewModel.SelectAnimeModeCommand,
				CommandParameter = text
			});
		}
	}

	private void RebuildSharpenModeSubMenu()
	{
		SharpenModeSubItem.Items.Clear();
		(string, string)[] array = new (string, string)[4]
		{
			("none", "未设置"),
			("light", "轻度"),
			("medium", "中度"),
			("strong", "强度")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (text, text2) = array[i];
			SharpenModeSubItem.Items.Add(new ToggleMenuFlyoutItem
			{
				Text = text2,
				IsChecked = string.Equals(base.ViewModel.CurrentSharpenMode, text, StringComparison.OrdinalIgnoreCase),
				Command = base.ViewModel.SelectSharpenModeCommand,
				CommandParameter = text
			});
		}
	}

	private void OnDanmakuFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildDanmakuFlyout();
	}

	private void RebuildDanmakuFlyout()
	{
		DanmakuFlyout.Items.Clear();
		IReadOnlyList<DanmakuApiEntry> danmakuApis = base.ViewModel.DanmakuApis;
		for (int i = 0; i < danmakuApis.Count; i++)
		{
			int idx = i;
			bool flag = idx == base.ViewModel.SelectedDanmakuApiIndex && base.ViewModel.IsDanmakuEnabled;
			IReadOnlyList<DanmakuSelectionCandidate> danmakuCandidates = base.ViewModel.GetDanmakuCandidates(idx);
			string danmakuActiveCommentUrl = base.ViewModel.GetDanmakuActiveCommentUrl(idx);
			if (danmakuCandidates.Count > 0)
			{
				MenuFlyoutSubItem menuFlyoutSubItem = new MenuFlyoutSubItem
				{
					Text = danmakuApis[i].DisplayName
				};
				if (flag)
				{
					menuFlyoutSubItem.Icon = new FontIcon
					{
						Glyph = "\ue73e",
						FontSize = 12.0
					};
				}
				foreach (DanmakuSelectionCandidate item in danmakuCandidates)
				{
					string candidateUrl = item.CommentUrl;
					MenuFlyoutItem menuFlyoutItem = new MenuFlyoutItem
					{
						Text = item.DisplayTitle
					};
					if (flag && string.Equals(candidateUrl, danmakuActiveCommentUrl, StringComparison.Ordinal))
					{
						menuFlyoutItem.Icon = new FontIcon
						{
							Glyph = "\ue73e",
							FontSize = 12.0
						};
					}
					menuFlyoutItem.Click += delegate
					{
						OnDanmakuCandidateSelected(idx, candidateUrl);
					};
					menuFlyoutSubItem.Items.Add(menuFlyoutItem);
				}
				DanmakuFlyout.Items.Add(menuFlyoutSubItem);
			}
			else
			{
				MenuFlyoutItem menuFlyoutItem2 = new MenuFlyoutItem
				{
					Text = danmakuApis[i].DisplayName
				};
				if (flag)
				{
					menuFlyoutItem2.Icon = new FontIcon
					{
						Glyph = "\ue73e",
						FontSize = 12.0
					};
				}
				menuFlyoutItem2.Click += delegate
				{
					OnDanmakuApiSelected(idx);
				};
				DanmakuFlyout.Items.Add(menuFlyoutItem2);
			}
		}
		if (danmakuApis.Count > 0)
		{
			DanmakuFlyout.Items.Add(new MenuFlyoutSeparator());
		}
		MenuFlyoutItem menuFlyoutItem3 = new MenuFlyoutItem
		{
			Text = "搜索弹幕...",
			Icon = new FluentIcons.WinUI.SymbolIcon
			{
				Symbol = FluentIcons.Common.Symbol.Search
			}
		};
		menuFlyoutItem3.Click += OnSearchDanmakuClick;
		DanmakuFlyout.Items.Add(menuFlyoutItem3);
		MenuFlyoutItem menuFlyoutItem4 = new MenuFlyoutItem
		{
			Text = "弹幕设置",
			Icon = new FluentIcons.WinUI.SymbolIcon
			{
				Symbol = FluentIcons.Common.Symbol.Settings
			}
		};
		menuFlyoutItem4.Click += OnDanmakuSettingsClick;
		DanmakuFlyout.Items.Add(menuFlyoutItem4);
		DanmakuFlyout.Items.Add(new MenuFlyoutSeparator());
		MenuFlyoutItem menuFlyoutItem5 = new MenuFlyoutItem
		{
			Text = (base.ViewModel.IsDanmakuEnabled ? "关闭弹幕" : "开启弹幕")
		};
		menuFlyoutItem5.Click += delegate
		{
			base.ViewModel.IsDanmakuEnabled = !base.ViewModel.IsDanmakuEnabled;
		};
		DanmakuFlyout.Items.Add(menuFlyoutItem5);
	}

	private async void OnDanmakuSettingsClick(object sender, RoutedEventArgs e)
	{
		DanmakuSettingsDialog obj = new DanmakuSettingsDialog(base.ViewModel)
		{
			XamlRoot = base.XamlRoot
		};
		KeepChromeVisible();
		await obj.ShowAsync();
		_isPointerInsideChrome = false;
		RestartAutoHideTimer();
	}

	private async void OnSearchDanmakuClick(object sender, RoutedEventArgs e)
	{
		IReadOnlyList<DanmakuApiEntry> danmakuApis = base.ViewModel.DanmakuApis;
		if (danmakuApis != null && danmakuApis.Count != 0)
		{
			string defaultQuery = ExtractAnimeTitle(base.ViewModel.DanmakuMatchName);
			int? season = ExtractSeason(base.ViewModel.DanmakuMatchName);
			int? episode = ExtractEpisode(base.ViewModel.DanmakuMatchName);
			DanmakuSearchDialog obj = new DanmakuSearchDialog(danmakuApis, base.ViewModel.CurrentDanmakuApiBase, defaultQuery, season, episode, LoadDanmakuFromSearchAsync)
			{
				XamlRoot = base.XamlRoot
			};
			KeepChromeVisible();
			await obj.ShowAsync();
			_isPointerInsideChrome = false;
			RestartAutoHideTimer();
		}
	}

	internal async Task LoadDanmakuFromSearchAsync(DanmakuManualLoadRequest request)
	{
		if (request.ApiIndex >= 0 && request.ApiIndex < base.ViewModel.DanmakuApis.Count)
		{
			base.ViewModel.SelectedDanmakuApiIndex = request.ApiIndex;
			base.ViewModel.SetDanmakuManualSelection(request.ApiIndex, request.DisplayTitle, request.CommentUrl);
			string danmakuMatchName = base.ViewModel.DanmakuMatchName;
			string text = base.ViewModel.DanmakuApis[request.ApiIndex].Url?.TrimEnd('/');
			if (!string.IsNullOrWhiteSpace(danmakuMatchName) && !string.IsNullOrWhiteSpace(text))
			{
				await DanmakuParser.PersistManualSelectionAsync(text, danmakuMatchName, request.DisplayTitle, request.CommentUrl);
			}
			await LoadDanmakuByUrlAsync(request.CommentUrl, request.ApiIndex, updateSelection: false, forceRefresh: true);
		}
	}

	private bool HasManualSelection(int apiIndex)
	{
		string url = base.ViewModel.GetDanmakuActiveCommentUrl(apiIndex);
		if (string.IsNullOrWhiteSpace(url))
		{
			return false;
		}
		return base.ViewModel.GetDanmakuCandidates(apiIndex).Any((DanmakuSelectionCandidate c) => c.IsManual && string.Equals(c.CommentUrl, url, StringComparison.Ordinal));
	}

	private static (int ApiIndex, DanmakuMatchAttempt Attempt) ResolveDanmakuApiMatch(IReadOnlyList<DanmakuApiMatchResult> matchResults, int preferredApiIndex)
	{
		if (matchResults.Count == 0)
		{
			return (ApiIndex: preferredApiIndex, Attempt: DanmakuMatchAttempt.Failure("未配置弹幕 API"));
		}
		DanmakuApiMatchResult danmakuApiMatchResult = matchResults.FirstOrDefault((DanmakuApiMatchResult r) => r.ApiIndex == preferredApiIndex);
		if (danmakuApiMatchResult is not null && danmakuApiMatchResult.Attempt.IsSuccess)
		{
			return (ApiIndex: danmakuApiMatchResult.ApiIndex, Attempt: danmakuApiMatchResult.Attempt);
		}
		DanmakuApiMatchResult danmakuApiMatchResult2 = matchResults.FirstOrDefault((DanmakuApiMatchResult r) => r.Attempt.IsSuccess);
		if (danmakuApiMatchResult2 is not null)
		{
			return (ApiIndex: danmakuApiMatchResult2.ApiIndex, Attempt: danmakuApiMatchResult2.Attempt);
		}
		DanmakuMatchAttempt attempt = matchResults[matchResults.Count - 1].Attempt;
		return (ApiIndex: matchResults[0].ApiIndex, Attempt: attempt);
	}

	private async Task TryRestorePersistedDanmakuSelectionsAsync()
	{
		string matchName = base.ViewModel.DanmakuMatchName;
		if (string.IsNullOrWhiteSpace(matchName))
		{
			return;
		}
		IReadOnlyList<DanmakuApiEntry> apis = base.ViewModel.DanmakuApis;
		for (int i = 0; i < apis.Count; i++)
		{
			string text = apis[i].Url?.Trim();
			if (!string.IsNullOrWhiteSpace(text))
			{
				DanmakuMatchResult danmakuMatchResult = await DanmakuParser.TryRestoreManualSelectionAsync(text, matchName);
				if (danmakuMatchResult is not null)
				{
					base.ViewModel.SetDanmakuManualSelection(i, danmakuMatchResult.DisplayTitle, danmakuMatchResult.CommentUrl);
					_logger.LogDebug("PlayerOverlay.TryRestorePersistedDanmakuSelectionsAsync apiIndex={ApiIndex} url={Url}", i, danmakuMatchResult.CommentUrl);
				}
			}
		}
	}

	internal async Task LoadDanmakuByUrlAsync(string commentUrl, int? apiIndex = null, bool updateSelection = true, bool forceRefresh = false)
	{
		if (string.IsNullOrWhiteSpace(commentUrl))
		{
			return;
		}
		_logger.LogDebug($"PlayerOverlay.LoadDanmakuByUrlAsync url={commentUrl} forceRefresh={forceRefresh}");
		_lastDanmakuUrl = null;
		try
		{
			if (_danmaku == null)
			{
				_danmaku = new DanmakuFrostMaster(DanmakuLayer);
				ApplyDanmakuDisplaySettings();
				if (DanmakuLayer.Children.Count > 0 && DanmakuLayer.Children[0] is FrameworkElement { IsLoaded: false } frameworkElement)
				{
					TaskCompletionSource tcs = new TaskCompletionSource();
					frameworkElement.Loaded += delegate
					{
						tcs.TrySetResult();
					};
					try
					{
						await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2L));
					}
					catch (TimeoutException)
					{
						_logger.LogDebug("PlayerOverlay.LoadDanmakuByUrlAsync canvas Loaded timeout");
					}
				}
			}
			DanmakuFetchAttempt danmakuFetchAttempt = await DanmakuParser.FetchCommentAsync(commentUrl, forceRefresh);
			List<DanmakuItem> list = ((danmakuFetchAttempt.Items is List<DanmakuItem> list2) ? list2 : danmakuFetchAttempt.Items.ToList());
			_logger.LogDebug($"PlayerOverlay.LoadDanmakuByUrlAsync items={list.Count} forceRefresh={forceRefresh}");
			if (!danmakuFetchAttempt.HasItems)
			{
				ShowDanmakuMessage(danmakuFetchAttempt.ErrorMessage ?? "弹幕加载失败或内容为空");
				return;
			}
			if (apiIndex.HasValue)
			{
				base.ViewModel.SelectedDanmakuApiIndex = apiIndex.Value;
				if (updateSelection)
				{
					base.ViewModel.SetDanmakuActiveCommentUrl(apiIndex.Value, commentUrl);
				}
			}
			_lastDanmakuUrl = commentUrl;
			DanmakuLayer.Visibility = Visibility.Visible;
			HideDanmakuMessage();
			_danmaku.SetDanmakuList(list);
			uint num = DanmakuTimeMs(base.ViewModel.CurrentPosition);
			base.ViewModel.IsDanmakuEnabled = true;
			_danmaku.Seek(num);
			if (!base.ViewModel.IsPlaying || !base.ViewModel.IsDanmakuEnabled)
			{
				_danmaku.Pause();
			}
			else
			{
				_danmaku.Resume();
			}
			_logger.LogDebug($"PlayerOverlay.LoadDanmakuByUrlAsync seek posMs={num}");
		}
		catch (Exception ex2)
		{
			_logger.LogWarning(ex2, "PlayerOverlay.LoadDanmakuByUrlAsync error");
			ShowDanmakuMessage("弹幕加载出错：" + ex2.Message);
		}
	}

	private static string ExtractAnimeTitle(string? matchName)
	{
		if (string.IsNullOrWhiteSpace(matchName))
		{
			return string.Empty;
		}
		Match match = Regex.Match(matchName, "^(.+?)\\s+S\\d+E\\d+", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return matchName.Split(' ')[0];
		}
		return match.Groups[1].Value.Trim();
	}

	private static int? ExtractSeason(string? matchName)
	{
		if (string.IsNullOrWhiteSpace(matchName))
		{
			return null;
		}
		Match match = Regex.Match(matchName, "S(\\d+)E\\d+", RegexOptions.IgnoreCase);
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return null;
		}
		return result;
	}

	private static int? ExtractEpisode(string? matchName)
	{
		if (string.IsNullOrWhiteSpace(matchName))
		{
			return null;
		}
		Match match = Regex.Match(matchName, "S\\d+E(\\d+)", RegexOptions.IgnoreCase);
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return null;
		}
		return result;
	}

	private void OnDanmakuApiSelected(int index)
	{
		bool flag = index != base.ViewModel.SelectedDanmakuApiIndex;
		base.ViewModel.SelectedDanmakuApiIndex = index;
		base.ViewModel.IsDanmakuEnabled = true;
		string danmakuActiveCommentUrl = base.ViewModel.GetDanmakuActiveCommentUrl(index);
		if (!string.IsNullOrWhiteSpace(danmakuActiveCommentUrl))
		{
			_lastDanmakuUrl = null;
			LoadDanmakuByUrlAsync(danmakuActiveCommentUrl, index, updateSelection: false);
			return;
		}
		if (flag)
		{
			_lastDanmakuUrl = null;
			InitializeDanmakuAsync();
			return;
		}
		if (_danmaku == null)
		{
			InitializeDanmakuAsync();
			return;
		}
		_danmaku.Seek(DanmakuTimeMs(base.ViewModel.CurrentPosition));
		if (base.ViewModel.IsPlaying)
		{
			_danmaku.Resume();
		}
	}

	private void OnDanmakuCandidateSelected(int apiIndex, string commentUrl)
	{
		base.ViewModel.SelectedDanmakuApiIndex = apiIndex;
		base.ViewModel.SetDanmakuActiveCommentUrl(apiIndex, commentUrl);
		base.ViewModel.IsDanmakuEnabled = true;
		_lastDanmakuUrl = null;
		LoadDanmakuByUrlAsync(commentUrl, apiIndex, updateSelection: false, forceRefresh: true);
	}

	private void OnEpisodeListFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildEpisodeListFlyout();
	}

	private void OnEpisodeListFlyoutOpened(object sender, object e)
	{
		if (_episodeListScrollTarget is not null)
		{
			CenterEpisodeInListScrollViewer(_episodeListScrollTarget);
		}
	}

	private void CenterEpisodeInListScrollViewer(FrameworkElement row)
	{
		if (EpisodeListScrollViewer.ViewportHeight <= 0.0 || row.ActualHeight <= 0.0)
		{
			row.StartBringIntoView(new BringIntoViewOptions
			{
				VerticalAlignmentRatio = 0.5,
				AnimationDesired = false
			});
		}
		else if (EpisodeListScrollViewer.Content is UIElement visual)
		{
			double value = row.TransformToVisual(visual).TransformPoint(new Point(0f, 0f)).Y + row.ActualHeight / 2.0 - EpisodeListScrollViewer.ViewportHeight / 2.0;
			value = Math.Clamp(value, 0.0, EpisodeListScrollViewer.ScrollableHeight);
			EpisodeListScrollViewer.ChangeView(null, value, null, disableAnimation: true);
		}
	}

	private void RebuildEpisodeListFlyout()
	{
		_episodeListScrollTarget = null;
		EpisodeListPanel.Children.Clear();
		if (base.ViewModel.EpisodeList.Count == 0)
		{
			EpisodeListPanel.Children.Add(new TextBlock
			{
				Text = "暂无剧集",
				Foreground = new SolidColorBrush(new Color
				{
					A = 128,
					R = byte.MaxValue,
					G = byte.MaxValue,
					B = byte.MaxValue
				}),
				Margin = new Thickness(8.0, 6.0, 8.0, 6.0),
				FontSize = 13.0
			});
			return;
		}
		SolidColorBrush accentBrush = ((!Application.Current.Resources.TryGetValue("SystemAccentColor", out var value) || !(value is Color color)) ? new SolidColorBrush(new Color
		{
			A = byte.MaxValue,
			R = 0,
			G = 120,
			B = 212
		}) : new SolidColorBrush(color));
		FrameworkElement episodeListScrollTarget = null;
		foreach (EpisodeListItem episode in base.ViewModel.EpisodeList)
		{
			Button button = CreateEpisodeRow(episode, accentBrush);
			EpisodeListPanel.Children.Add(button);
			if (episode.IsCurrent)
			{
				episodeListScrollTarget = button;
			}
		}
		_episodeListScrollTarget = episodeListScrollTarget;
	}

	private Button CreateEpisodeRow(EpisodeListItem episode, SolidColorBrush accentBrush)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(new Color
		{
			A = byte.MaxValue,
			R = byte.MaxValue,
			G = byte.MaxValue,
			B = byte.MaxValue
		});
		Brush background = (episode.IsCurrent ? accentBrush : solidColorBrush);
		double opacity = (episode.IsCurrent ? 1.0 : (episode.IsPlayed ? 0.5 : 0.2));
		Border item = new Border
		{
			Width = 6.0,
			Height = 6.0,
			CornerRadius = new CornerRadius(3.0),
			Background = background,
			Opacity = opacity,
			VerticalAlignment = VerticalAlignment.Center
		};
		double opacity2 = ((!episode.IsCurrent && episode.IsPlayed) ? 0.55 : 1.0);
		TextBlock item2 = new TextBlock
		{
			Text = episode.Label,
			Foreground = (episode.IsCurrent ? accentBrush : solidColorBrush),
			Opacity = opacity2,
			FontSize = 13.0,
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center,
			MaxWidth = 280.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 10.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(item2);
		Button button = new Button();
		button.Content = stackPanel;
		button.Style = (Style)base.Resources["EpisodeRowButtonStyle"];
		button.Command = base.ViewModel.NavigateEpisodeCommand;
		button.CommandParameter = episode.Id;
		button.Click += delegate
		{
			EpisodeListFlyout.Hide();
		};
		return button;
	}

	private void OnSkipFlyoutOpening(object sender, object e)
	{
		KeepChromeVisible();
		RebuildSkipFlyout();
	}

	private void RebuildSkipFlyout()
	{
		SkipFlyout.Items.Clear();
		if (base.ViewModel.Duration <= 0.0 || base.ViewModel.CurrentPosition < base.ViewModel.Duration * 0.5)
		{
			SkipFlyout.Items.Add(new MenuFlyoutItem
			{
				Text = "标记为片头结束",
				Command = base.ViewModel.MarkIntroEndCommand
			});
			SkipFlyout.Items.Add(new MenuFlyoutItem
			{
				Text = "已标记：" + FormatSkipTime(base.ViewModel.IntroEndTime),
				IsEnabled = false
			});
			if (base.ViewModel.IntroEndTime > 0.0)
			{
				SkipFlyout.Items.Add(new MenuFlyoutItem
				{
					Text = "清除片头",
					Command = base.ViewModel.ClearIntroCommand
				});
			}
		}
		else
		{
			SkipFlyout.Items.Add(new MenuFlyoutItem
			{
				Text = "标记为片尾开始",
				Command = base.ViewModel.MarkOutroStartCommand
			});
			SkipFlyout.Items.Add(new MenuFlyoutItem
			{
				Text = "已标记：" + FormatSkipTime(base.ViewModel.OutroStartTime),
				IsEnabled = false
			});
			if (base.ViewModel.OutroStartTime > 0.0)
			{
				SkipFlyout.Items.Add(new MenuFlyoutItem
				{
					Text = "清除片尾",
					Command = base.ViewModel.ClearOutroCommand
				});
			}
		}
		SkipFlyout.Items.Add(new MenuFlyoutSeparator());
		SkipFlyout.Items.Add(new ToggleMenuFlyoutItem
		{
			Text = "自动跳过片头",
			IsChecked = base.ViewModel.AutoSkipIntro,
			Command = base.ViewModel.ToggleAutoSkipIntroCommand
		});
		SkipFlyout.Items.Add(new ToggleMenuFlyoutItem
		{
			Text = "    └ 包含前情提要",
			IsChecked = base.ViewModel.AutoSkipRecap,
			IsEnabled = base.ViewModel.AutoSkipIntro,
			Command = base.ViewModel.ToggleAutoSkipRecapCommand
		});
		SkipFlyout.Items.Add(new ToggleMenuFlyoutItem
		{
			Text = "自动跳过片尾",
			IsChecked = base.ViewModel.AutoSkipOutro,
			Command = base.ViewModel.ToggleAutoSkipOutroCommand
		});
		SkipFlyout.Items.Add(new ToggleMenuFlyoutItem
		{
			Text = "    └ 包含下集预告",
			IsChecked = base.ViewModel.AutoSkipPreview,
			IsEnabled = base.ViewModel.AutoSkipOutro,
			Command = base.ViewModel.ToggleAutoSkipPreviewCommand
		});
	}

	private static string FormatSkipTime(double seconds)
	{
		if (seconds <= 0.0)
		{
			return "0s";
		}
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
		if (timeSpan.TotalHours >= 1.0)
		{
			return timeSpan.ToString("hh\\:mm\\:ss");
		}
		if (!(timeSpan.TotalMinutes >= 1.0))
		{
			return $"{(int)timeSpan.TotalSeconds}s";
		}
		return timeSpan.ToString("mm\\:ss");
	}

	private async void OnSearchOnlineSubtitleClick(object sender, RoutedEventArgs e)
	{
		SubtitleSearchDialog obj = new SubtitleSearchDialog(base.ViewModel)
		{
			XamlRoot = base.XamlRoot
		};
		KeepChromeVisible();
		await obj.ShowAsync();
		_isPointerInsideChrome = false;
		RestartAutoHideTimer();
	}

	private async void OnSubtitleSyncClick(object sender, RoutedEventArgs e)
	{
		SubtitleSyncDialog obj = new SubtitleSyncDialog(base.ViewModel)
		{
			XamlRoot = base.XamlRoot
		};
		KeepChromeVisible();
		await obj.ShowAsync();
		_isPointerInsideChrome = false;
		RestartAutoHideTimer();
	}

	private void OnFlyoutClosed(object sender, object e)
	{
		_isPointerInsideChrome = false;
		RestartAutoHideTimer();
	}

	private static void RebuildTrackFlyout(MenuFlyout flyout, IEnumerable<OverlayTrackOption> tracks, IRelayCommand<OverlayTrackOption?> command)
	{
		flyout.Items.Clear();
		foreach (OverlayTrackOption track in tracks)
		{
			flyout.Items.Add(new ToggleMenuFlyoutItem
			{
				Text = track.Label,
				IsChecked = track.IsSelected,
				Command = command,
				CommandParameter = track
			});
		}
		if (flyout.Items.Count == 0)
		{
			flyout.Items.Add(new MenuFlyoutItem
			{
				Text = "当前没有可选轨道",
				IsEnabled = false
			});
		}
	}

	private static void RebuildVersionFlyout(MenuFlyout flyout, IEnumerable<OverlayVersionOption> versions, IRelayCommand<OverlayVersionOption?> command)
	{
		flyout.Items.Clear();
		foreach (OverlayVersionOption version in versions)
		{
			flyout.Items.Add(new ToggleMenuFlyoutItem
			{
				Text = version.Label,
				IsChecked = version.IsSelected,
				Command = command,
				CommandParameter = version
			});
		}
		if (flyout.Items.Count == 0)
		{
			flyout.Items.Add(new MenuFlyoutItem
			{
				Text = "当前没有可选版本",
				IsEnabled = false
			});
		}
	}

	private async void OnSpeedPresetClick(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem { Tag: string tag } && double.TryParse(tag, out var result))
		{
			SpeedTextBlock.Text = $"{result:0.0}x";
			await base.ViewModel.SetSpeedValueCommand.ExecuteAsync(result);
		}
	}

	private async void OnCompactOverlayClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.ToggleCompactOverlayCommand.ExecuteAsync(null);
	}

	private async void OnExitPipClick(object sender, RoutedEventArgs e)
	{
		await base.ViewModel.ToggleCompactOverlayCommand.ExecuteAsync(null);
	}

	private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (!IsTextInputFocused())
		{
			VirtualKey key = ((e.OriginalKey != VirtualKey.None) ? e.OriginalKey : e.Key);
			if (IsKnownShortcut(key))
			{
				e.Handled = true;
				HandleKeyDown(key);
			}
		}
	}

	private void OnRootPreviewKeyUp(object sender, KeyRoutedEventArgs e)
	{
		if (!IsTextInputFocused())
		{
			VirtualKey virtualKey = ((e.OriginalKey != VirtualKey.None) ? e.OriginalKey : e.Key);
			if (virtualKey == base.ViewModel.Shortcuts.PlayPause)
			{
				e.Handled = true;
				HandleKeyUp(virtualKey);
			}
		}
	}

	private void AttachButtonFeedbackHandlers(DependencyObject root)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is Button button)
			{
				button.RenderTransformOrigin = new Point(0.5, 0.5);
				button.RenderTransform = new ScaleTransform
				{
					ScaleX = 1.0,
					ScaleY = 1.0
				};
				button.PointerEntered += OnOverlayButtonPointerEntered;
				button.PointerExited += OnOverlayButtonPointerExited;
				button.PointerPressed += OnOverlayButtonPointerPressed;
				button.PointerReleased += OnOverlayButtonPointerReleased;
				button.PointerCanceled += OnOverlayButtonPointerExited;
				button.Click += OnOverlayButtonClick;
				if (ToolTipService.GetToolTip(button) is string text && !string.IsNullOrEmpty(text))
				{
					button.Tag = text;
					if (string.IsNullOrEmpty(AutomationProperties.GetName(button)))
					{
						AutomationProperties.SetName(button, text);
					}
					ToolTipService.SetToolTip(button, null);
				}
			}
			AttachButtonFeedbackHandlers(child);
		}
	}

	private void ShowHoverHint(Button button)
	{
		if (!(button.Tag is string text) || string.IsNullOrEmpty(text))
		{
			HoverHintBorder.Visibility = Visibility.Collapsed;
			return;
		}
		HoverHintText.Text = text;
		HoverHintBorder.Visibility = Visibility.Visible;
		HoverHintBorder.UpdateLayout();
		double actualWidth = HoverHintBorder.ActualWidth;
		double actualHeight = HoverHintBorder.ActualHeight;
		if (!(actualWidth <= 0.0) && !(actualHeight <= 0.0))
		{
			Point point = button.TransformToVisual(HoverHintLayer).TransformPoint(new Point(button.ActualWidth / 2.0, 0.0));
			double num = point.X - actualWidth / 2.0;
			double num2 = point.Y - actualHeight - 8.0;
			double actualWidth2 = HoverHintLayer.ActualWidth;
			if (actualWidth2 > 0.0)
			{
				num = Math.Clamp(num, 4.0, Math.Max(4.0, actualWidth2 - actualWidth - 4.0));
			}
			if (num2 < 0.0)
			{
				num2 = point.Y + button.ActualHeight + 8.0;
			}
			Canvas.SetLeft(HoverHintBorder, num);
			Canvas.SetTop(HoverHintBorder, num2);
		}
	}

	private void HideHoverHint()
	{
		HoverHintBorder.Visibility = Visibility.Collapsed;
	}

	private void OnOverlayButtonPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Button { IsEnabled: not false } button)
		{
			ApplyHoveredButtonState(button);
			ShowHoverHint(button);
		}
	}

	private void OnOverlayButtonPointerExited(object sender, PointerRoutedEventArgs e)
	{
		HideHoverHint();
		if (sender is Button { RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.ScaleX = 1.0;
			renderTransform.ScaleY = 1.0;
		}
	}

	private void OnOverlayButtonPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Button { IsEnabled: not false, RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.ScaleX = 0.94;
			renderTransform.ScaleY = 0.94;
		}
	}

	private void OnOverlayButtonPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Button { IsEnabled: not false } button)
		{
			ApplyHoveredButtonState(button);
		}
	}

	private void OnOverlayButtonClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { IsEnabled: not false } button)
		{
			ApplyHoveredButtonState(button);
		}
	}

	private static bool IsTextInputFocused()
	{
		DependencyObject dependencyObject = FocusManager.GetFocusedElement() as DependencyObject;
		while (dependencyObject is not null)
		{
			if ((dependencyObject is TextBox || dependencyObject is PasswordBox) ? true : false)
			{
				return true;
			}
			if (dependencyObject is ComboBox { IsEditable: not false, IsDropDownOpen: not false })
			{
				return true;
			}
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
		return false;
	}

	private static void ApplyHoveredButtonState(Button button)
	{
		if (button.RenderTransform is ScaleTransform scaleTransform)
		{
			scaleTransform.ScaleX = 1.03;
			scaleTransform.ScaleY = 1.03;
		}
	}

	private static bool IsInteractiveElement(DependencyObject? source)
	{
		while (source is not null)
		{
			if (source is ButtonBase || source is RangeBase || source is TextBox || source is PasswordBox || source is MenuFlyoutPresenter || source is MenuFlyoutItemBase)
			{
				return true;
			}
			source = VisualTreeHelper.GetParent(source);
		}
		return false;
	}

	private void KeepChromeVisible()
	{
		_isPointerInsideChrome = true;
		ShowChrome();
		_autoHideTimer.Stop();
	}

	private bool IsKnownShortcut(VirtualKey key)
	{
		if (key == VirtualKey.Escape)
		{
			return true;
		}
		PlayerShortcuts shortcuts = base.ViewModel.Shortcuts;
		if (key != shortcuts.PlayPause && key != shortcuts.SeekForward && key != shortcuts.SeekBackward && key != shortcuts.VolumeUp && key != shortcuts.VolumeDown && key != shortcuts.ToggleFullscreen && key != shortcuts.ToggleMute && key != shortcuts.NextEpisode && key != shortcuts.PreviousEpisode && key != shortcuts.SpeedUp && key != shortcuts.SpeedDown)
		{
			return key == shortcuts.ResetSpeed;
		}
		return true;
	}

	private async void HandleKeyDown(VirtualKey key)
	{
		PlayerShortcuts shortcuts = base.ViewModel.Shortcuts;
		if (key == VirtualKey.Escape)
		{
			RefreshChromeIfVisible();
			if (base.ViewModel.IsFullScreen)
			{
				await base.ViewModel.ExitFullScreenCommand.ExecuteAsync(null);
			}
			return;
		}
		RefreshChromeIfVisible();
		if (key == shortcuts.PlayPause)
		{
			if (!_spaceKeyDown)
			{
				_spaceKeyDown = true;
				_keyLongPressTimer.Stop();
				_keyLongPressTimer.Start();
			}
		}
		else if (key == shortcuts.SeekForward)
		{
			await base.ViewModel.ForwardSkipCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.SeekBackward)
		{
			await base.ViewModel.BackwardSkipCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.VolumeUp)
		{
			await base.ViewModel.IncreaseVolumeCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.VolumeDown)
		{
			await base.ViewModel.DecreaseVolumeCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.ToggleFullscreen)
		{
			await base.ViewModel.ToggleFullScreenCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.ToggleMute)
		{
			await base.ViewModel.ToggleMuteCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.NextEpisode)
		{
			await base.ViewModel.NavigateNextEpisodeCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.PreviousEpisode)
		{
			await base.ViewModel.NavigatePreviousEpisodeCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.SpeedUp)
		{
			await base.ViewModel.IncreaseSpeedCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.SpeedDown)
		{
			await base.ViewModel.DecreaseSpeedCommand.ExecuteAsync(null);
		}
		else if (key == shortcuts.ResetSpeed)
		{
			await base.ViewModel.ResetSpeedCommand.ExecuteAsync(null);
		}
	}

	private async void HandleKeyUp(VirtualKey key)
	{
		PlayerShortcuts shortcuts = base.ViewModel.Shortcuts;
		if (key == shortcuts.PlayPause && _spaceKeyDown)
		{
			_spaceKeyDown = false;
			_keyLongPressTimer.Stop();
			if (_isHoldSpeedActive)
			{
				ExitHoldSpeed();
			}
			else
			{
				await base.ViewModel.TogglePlayPauseCommand.ExecuteAsync(null);
			}
		}
	}

	private void OnKeyLongPressTimerTick(object? sender, object e)
	{
		_keyLongPressTimer.Stop();
		EnterHoldSpeed();
	}

	private async void EnterHoldSpeed()
	{
		if (!_isHoldSpeedActive)
		{
			_preHoldSpeed = ((base.ViewModel.Speed <= 0.0) ? 1.0 : base.ViewModel.Speed);
			_isHoldSpeedActive = true;
			await base.ViewModel.SetSpeedValueCommand.ExecuteAsync(2.0);
		}
	}

	private async void ExitHoldSpeed()
	{
		if (_isHoldSpeedActive)
		{
			_isHoldSpeedActive = false;
			await base.ViewModel.SetSpeedValueCommand.ExecuteAsync(_preHoldSpeed);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/PlayerOverlay.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
			case 1:
				{
					PlayerOverlayBase playerOverlayBase = target.As<PlayerOverlayBase>();
					playerOverlayBase.DragEnter += OnSubtitleDragEnter;
					playerOverlayBase.DragOver += OnSubtitleDragOver;
					playerOverlayBase.Drop += OnSubtitleDrop;
					break;
				}
			case 27:
				{
					Grid grid = target.As<Grid>();
					grid.PreviewKeyDown += OnRootPreviewKeyDown;
					grid.PreviewKeyUp += OnRootPreviewKeyUp;
					break;
				}
			case 28:
				DanmakuLayer = target.As<Grid>();
				break;
			case 29:
				VideoBackdrop = target.As<Grid>();
				break;
			case 30:
				ChromeRoot = target.As<Grid>();
				break;
			case 31:
				LoadingRingHost = target.As<Border>();
				break;
			case 32:
				DanmakuMessageBorder = target.As<Border>();
				break;
			case 33:
				DanmakuMessageText = target.As<TextBlock>();
				break;
			case 34:
				LoadingRing = target.As<ProgressRing>();
				break;
			case 35:
				ThumbnailCanvas = target.As<Canvas>();
				break;
			case 36:
				{
					Border border2 = target.As<Border>();
					border2.PointerEntered += OnTopZonePointerEntered;
					border2.PointerExited += OnTopZonePointerExited;
					border2.DoubleTapped += OnTopBarDoubleTapped;
					border2.PointerPressed += OnTopBarPointerPressed;
					border2.PointerMoved += OnTopBarPointerMoved;
					border2.PointerReleased += OnTopBarPointerReleased;
					border2.PointerCanceled += OnTopBarPointerReleased;
					break;
				}
			case 37:
				{
					Border border = target.As<Border>();
					border.PointerEntered += OnBottomZonePointerEntered;
					border.PointerExited += OnBottomZonePointerExited;
					border.PointerMoved += OnBottomZonePointerMoved;
					break;
				}
			case 38:
				HoverHintLayer = target.As<Canvas>();
				break;
			case 39:
				HoverHintBorder = target.As<Border>();
				break;
			case 40:
				HoverHintText = target.As<TextBlock>();
				break;
			case 41:
				BadgeItemsControl = target.As<ItemsControl>();
				break;
			case 42:
				PlaybackControlsPanel = target.As<StackPanel>();
				break;
			case 43:
				SecondaryControlsPanel = target.As<StackPanel>();
				break;
			case 44:
				SpeedButton = target.As<Button>();
				break;
			case 45:
				AudioTrackButton = target.As<Button>();
				break;
			case 46:
				VersionButton = target.As<Button>();
				break;
			case 47:
				SubtitleTrackButton = target.As<Button>();
				break;
			case 48:
				DanmakuButton = target.As<Button>();
				break;
			case 49:
				SkipButton = target.As<Button>();
				break;
			case 50:
				EpisodeListButton = target.As<Button>();
				break;
			case 51:
				SettingsButton = target.As<Button>();
				break;
			case 52:
				FullScreenButton = target.As<Button>();
				FullScreenButton.Click += OnFullScreenClick;
				break;
			case 53:
				FullScreenIcon = target.As<FluentIcons.WinUI.SymbolIcon>();
				break;
			case 54:
				SettingsFlyout = target.As<MenuFlyout>();
				SettingsFlyout.Opening += OnSettingsFlyoutOpening;
				SettingsFlyout.Closed += OnFlyoutClosed;
				break;
			case 55:
				VideoFitModeSubItem = target.As<MenuFlyoutSubItem>();
				break;
			case 56:
				AnimeModeSubItem = target.As<MenuFlyoutSubItem>();
				break;
			case 57:
				SharpenModeSubItem = target.As<MenuFlyoutSubItem>();
				break;
			case 58:
				CompactOverlayMenuItem = target.As<MenuFlyoutItem>();
				CompactOverlayMenuItem.Click += OnCompactOverlayClick;
				break;
			case 59:
				EpisodeListFlyout = target.As<Flyout>();
				EpisodeListFlyout.Opening += OnEpisodeListFlyoutOpening;
				EpisodeListFlyout.Opened += OnEpisodeListFlyoutOpened;
				EpisodeListFlyout.Closed += OnFlyoutClosed;
				break;
			case 60:
				EpisodeListScrollViewer = target.As<ScrollViewer>();
				break;
			case 61:
				EpisodeListPanel = target.As<StackPanel>();
				break;
			case 62:
				SkipFlyout = target.As<MenuFlyout>();
				SkipFlyout.Opening += OnSkipFlyoutOpening;
				SkipFlyout.Closed += OnFlyoutClosed;
				break;
			case 63:
				DanmakuIcon = target.As<FluentIcons.WinUI.SymbolIcon>();
				break;
			case 64:
				DanmakuFlyout = target.As<MenuFlyout>();
				DanmakuFlyout.Opening += OnDanmakuFlyoutOpening;
				DanmakuFlyout.Closed += OnFlyoutClosed;
				break;
			case 65:
				SubtitleTrackFlyout = target.As<MenuFlyout>();
				SubtitleTrackFlyout.Opening += OnSubtitleTrackFlyoutOpening;
				SubtitleTrackFlyout.Closed += OnFlyoutClosed;
				break;
			case 66:
				VersionFlyout = target.As<MenuFlyout>();
				VersionFlyout.Opening += OnVersionFlyoutOpening;
				VersionFlyout.Closed += OnFlyoutClosed;
				break;
			case 67:
				AudioTrackFlyout = target.As<MenuFlyout>();
				AudioTrackFlyout.Opening += OnAudioTrackFlyoutOpening;
				AudioTrackFlyout.Closed += OnFlyoutClosed;
				break;
			case 68:
				SpeedTextBlock = target.As<TextBlock>();
				break;
			case 69:
				SpeedFlyout = target.As<MenuFlyout>();
				SpeedFlyout.Opening += OnSpeedFlyoutOpening;
				SpeedFlyout.Closed += OnFlyoutClosed;
				break;
			case 70:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 71:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 72:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 73:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 74:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 75:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 76:
				target.As<MenuFlyoutItem>().Click += OnSpeedPresetClick;
				break;
			case 77:
				MuteButton = target.As<Button>();
				MuteButton.Click += OnMuteClick;
				break;
			case 78:
				VolumeSlider = target.As<Slider>();
				VolumeSlider.ValueChanged += OnVolumeValueChanged;
				break;
			case 79:
				VolumeIcon = target.As<FluentIcons.WinUI.SymbolIcon>();
				break;
			case 80:
				PreviousEpisodeButton = target.As<Button>();
				PreviousEpisodeButton.Click += OnPreviousEpisodeClick;
				break;
			case 81:
				BackwardButton = target.As<Button>();
				BackwardButton.Click += OnBackwardClick;
				break;
			case 82:
				PlayPauseButton = target.As<Button>();
				PlayPauseButton.Click += OnPlayPauseClick;
				break;
			case 83:
				ForwardButton = target.As<Button>();
				ForwardButton.Click += OnForwardClick;
				break;
			case 84:
				NextEpisodeButton = target.As<Button>();
				NextEpisodeButton.Click += OnNextEpisodeClick;
				break;
			case 85:
				PlayPauseIcon = target.As<FluentIcons.WinUI.SymbolIcon>();
				break;
			case 86:
				CurrentPositionBlock = target.As<TextBlock>();
				break;
			case 87:
				DurationBlock = target.As<TextBlock>();
				break;
			case 88:
				BufferBar = target.As<ProgressBar>();
				break;
			case 89:
				SegmentOverlay = target.As<Canvas>();
				SegmentOverlay.SizeChanged += OnSegmentOverlaySizeChanged;
				break;
			case 90:
				ChapterOverlay = target.As<Canvas>();
				ChapterOverlay.PointerPressed += OnChapterOverlayPointerPressed;
				ChapterOverlay.SizeChanged += OnChapterOverlaySizeChanged;
				break;
			case 91:
				ProgressSlider = target.As<Slider>();
				ProgressSlider.ValueChanged += OnProgressValueChanged;
				break;
			case 94:
				LogoContainer = target.As<Grid>();
				break;
			case 95:
				TopMetadataPanel = target.As<Border>();
				break;
			case 96:
				ExitPipButton = target.As<Button>();
				ExitPipButton.Click += OnExitPipClick;
				break;
			case 97:
				NetworkSpeedBadge = target.As<Border>();
				break;
			case 98:
				WindowButtonsPanel = target.As<StackPanel>();
				break;
			case 99:
				TopmostButton = target.As<Button>();
				TopmostButton.Click += OnTopmostClick;
				break;
			case 100:
				MinimizeButton = target.As<Button>();
				MinimizeButton.Click += OnMinimizeClick;
				break;
			case 101:
				MaximizeRestoreButton = target.As<Button>();
				MaximizeRestoreButton.Click += OnMaximizeRestoreClick;
				break;
			case 102:
				CloseButton = target.As<Button>();
				CloseButton.Click += OnCloseClick;
				break;
			case 103:
				MaximizeRestoreIcon = target.As<TextBlock>();
				break;
			case 105:
				NetworkSpeedText = target.As<TextBlock>();
				break;
			case 108:
				LogoImage = target.As<Image>();
				LogoImage.ImageFailed += OnLogoImageFailed;
				break;
			case 109:
				ThumbnailPreview = target.As<Border>();
				break;
			case 110:
				ThumbnailImage = target.As<Image>();
				break;
			case 111:
				ThumbnailTimeText = target.As<TextBlock>();
				break;
			case 112:
				VideoBackdropImage = target.As<Image>();
				break;
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		IComponentConnector result = null;
		if (connectionId == 1)
		{
			PlayerOverlayBase obj = (PlayerOverlayBase)target;
			PlayerOverlay_obj1_Bindings playerOverlay_obj1_Bindings = new PlayerOverlay_obj1_Bindings();
			result = playerOverlay_obj1_Bindings;
			playerOverlay_obj1_Bindings.SetDataRoot(this);
			playerOverlay_obj1_Bindings.SetConverterLookupRoot(this);
			Bindings = playerOverlay_obj1_Bindings;
			obj.Loading += playerOverlay_obj1_Bindings.Loading;
		}
		return result;
	}
}

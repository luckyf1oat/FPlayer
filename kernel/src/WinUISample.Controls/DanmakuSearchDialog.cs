using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.Models;
using WinUISample.Services;

namespace WinUISample.Controls;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Controls_SubtitleSyncDialogWinRTTypeDetails))]
public sealed class DanmakuSearchDialog : ContentDialog, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private interface IDanmakuSearchDialog_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface IDanmakuSearchDialog_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Microsoft_UI_Xaml_Controls_Expander_IsExpanded(Expander obj, bool value)
		{
			obj.IsExpanded = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(TextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(ItemsControl obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.ItemsSource = value;
		}

		public static void Set_Microsoft_UI_Xaml_FrameworkElement_Tag(FrameworkElement obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.Tag = value;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.IDataTemplateExtension")]
	[WinRTExposedType(typeof(WinUISample_Controls_DanmakuSearchDialog_DanmakuSearchDialog_obj13_BindingsWinRTTypeDetails))]
	private class DanmakuSearchDialog_obj17_Bindings : IDataTemplateExtension, IDataTemplateComponent, IComponentConnector, IDanmakuSearchDialog_Bindings
	{
		private DanmakuSearchEpisode dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler;

		private WeakReference obj17;

		private TextBlock obj18;

		private Button obj19;

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 17:
					obj17 = new WeakReference(target.As<Grid>());
					break;
				case 18:
					obj18 = target.As<TextBlock>();
					break;
				case 19:
					obj19 = target.As<Button>();
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
					Grid grid = obj17.Target as Grid;
					if (grid != null)
					{
						grid.DataContextChanged -= DataContextChangedHandler;
					}
				}
				initialized = true;
			}
			Update_(item.As<DanmakuSearchEpisode>(), 1 << phase);
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
				dataRoot = newDataRoot.As<DanmakuSearchEpisode>();
				return true;
			}
			return false;
		}

		private void Update_(DanmakuSearchEpisode obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_EpisodeTitle(obj.EpisodeTitle, phase);
			}
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_FrameworkElement_Tag(obj19, obj, null);
			}
		}

		private void Update_EpisodeTitle(string obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj18, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.IDataTemplateExtension")]
	[WinRTExposedType(typeof(WinUISample_Controls_DanmakuSearchDialog_DanmakuSearchDialog_obj13_BindingsWinRTTypeDetails))]
	private class DanmakuSearchDialog_obj13_Bindings : IDataTemplateExtension, IDataTemplateComponent, IComponentConnector, IDanmakuSearchDialog_Bindings
	{
		private DanmakuSearchAnime dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler;

		private WeakReference obj13;

		private TextBlock obj14;

		private ItemsControl obj15;

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 13:
					obj13 = new WeakReference(target.As<Expander>());
					break;
				case 14:
					obj14 = target.As<TextBlock>();
					break;
				case 15:
					obj15 = target.As<ItemsControl>();
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
					Expander expander = obj13.Target as Expander;
					if (expander != null)
					{
						expander.DataContextChanged -= DataContextChangedHandler;
					}
				}
				initialized = true;
			}
			Update_(item.As<DanmakuSearchAnime>(), 1 << phase);
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
				dataRoot = newDataRoot.As<DanmakuSearchAnime>();
				return true;
			}
			return false;
		}

		private void Update_(DanmakuSearchAnime obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_AnimeTitle(obj.AnimeTitle, phase);
				Update_Episodes(obj.Episodes, phase);
			}
		}

		private void Update_AnimeTitle(string obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj14, obj, null);
			}
		}

		private void Update_Episodes(IReadOnlyList<DanmakuSearchEpisode> obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(obj15, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.IDataTemplateExtension")]
	[WinRTExposedType(typeof(WinUISample_Controls_DanmakuSearchDialog_DanmakuSearchDialog_obj13_BindingsWinRTTypeDetails))]
	private class DanmakuSearchDialog_obj9_Bindings : IDataTemplateExtension, IDataTemplateComponent, IComponentConnector, IDanmakuSearchDialog_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		private class DanmakuSearchDialog_obj9_BindingsTracking
		{
			private WeakReference<DanmakuSearchDialog_obj9_Bindings> weakRefToBindingObj;

			public DanmakuSearchDialog_obj9_BindingsTracking(DanmakuSearchDialog_obj9_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<DanmakuSearchDialog_obj9_Bindings>(obj);
			}

			public DanmakuSearchDialog_obj9_Bindings TryGetBindingObject()
			{
				DanmakuSearchDialog_obj9_Bindings target = null;
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
				UpdateChildListeners_(null);
			}

			public void PropertyChanged_(object sender, PropertyChangedEventArgs e)
			{
				DanmakuSearchDialog_obj9_Bindings danmakuSearchDialog_obj9_Bindings = TryGetBindingObject();
				if (danmakuSearchDialog_obj9_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				DanmakuSearchApiGroup danmakuSearchApiGroup = sender as DanmakuSearchApiGroup;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (danmakuSearchApiGroup != null)
					{
						danmakuSearchDialog_obj9_Bindings.Update_IsExpanded(danmakuSearchApiGroup.IsExpanded, 1073741824);
						danmakuSearchDialog_obj9_Bindings.Update_Header(danmakuSearchApiGroup.Header, 1073741824);
						danmakuSearchDialog_obj9_Bindings.Update_Animes(danmakuSearchApiGroup.Animes, 1073741824);
					}
					return;
				}
				switch (propertyName)
				{
					case "IsExpanded":
						if (danmakuSearchApiGroup != null)
						{
							danmakuSearchDialog_obj9_Bindings.Update_IsExpanded(danmakuSearchApiGroup.IsExpanded, 1073741824);
						}
						break;
					case "Header":
						if (danmakuSearchApiGroup != null)
						{
							danmakuSearchDialog_obj9_Bindings.Update_Header(danmakuSearchApiGroup.Header, 1073741824);
						}
						break;
					case "Animes":
						if (danmakuSearchApiGroup != null)
						{
							danmakuSearchDialog_obj9_Bindings.Update_Animes(danmakuSearchApiGroup.Animes, 1073741824);
						}
						break;
				}
			}

			public void UpdateChildListeners_(DanmakuSearchApiGroup obj)
			{
				DanmakuSearchDialog_obj9_Bindings danmakuSearchDialog_obj9_Bindings = TryGetBindingObject();
				if (danmakuSearchDialog_obj9_Bindings != null)
				{
					if (danmakuSearchDialog_obj9_Bindings.dataRoot != null)
					{
						((INotifyPropertyChanged)danmakuSearchDialog_obj9_Bindings.dataRoot).PropertyChanged -= PropertyChanged_;
					}
					if (obj != null)
					{
						danmakuSearchDialog_obj9_Bindings.dataRoot = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_;
					}
				}
			}

			public void RegisterTwoWayListener_9(Expander sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_9_IsExpanded();
				});
			}
		}

		private DanmakuSearchApiGroup dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler;

		private WeakReference obj9;

		private TextBlock obj10;

		private ItemsControl obj11;

		private DanmakuSearchDialog_obj9_BindingsTracking bindingsTracking;

		public DanmakuSearchDialog_obj9_Bindings()
		{
			bindingsTracking = new DanmakuSearchDialog_obj9_BindingsTracking(this);
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 9:
					obj9 = new WeakReference(target.As<Expander>());
					bindingsTracking.RegisterTwoWayListener_9(obj9.Target as Expander);
					break;
				case 10:
					obj10 = target.As<TextBlock>();
					break;
				case 11:
					obj11 = target.As<ItemsControl>();
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
					Expander expander = obj9.Target as Expander;
					if (expander != null)
					{
						expander.DataContextChanged -= DataContextChangedHandler;
					}
				}
				initialized = true;
			}
			Update_(item.As<DanmakuSearchApiGroup>(), 1 << phase);
		}

		public void Recycle()
		{
			bindingsTracking.ReleaseAllListeners();
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
				dataRoot = newDataRoot.As<DanmakuSearchApiGroup>();
				return true;
			}
			return false;
		}

		private void Update_(DanmakuSearchApiGroup obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_(obj);
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_IsExpanded(obj.IsExpanded, phase);
				Update_Header(obj.Header, phase);
				Update_Animes(obj.Animes, phase);
			}
		}

		private void Update_IsExpanded(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && obj9.Target as Expander != null)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Expander_IsExpanded(obj9.Target as Expander, obj);
			}
		}

		private void Update_Header(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj10, obj, null);
			}
		}

		private void Update_Animes(IReadOnlyList<DanmakuSearchAnime> obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(obj11, obj, null);
			}
		}

		private void UpdateTwoWay_9_IsExpanded()
		{
			if (initialized && dataRoot != null)
			{
				dataRoot.IsExpanded = (obj9.Target as Expander).IsExpanded;
			}
		}
	}

	private readonly IReadOnlyList<DanmakuApiEntry> _apis;

	private readonly string? _activeApiBase;

	private readonly Func<DanmakuManualLoadRequest, Task> _loadCallback;

	private readonly ObservableCollection<DanmakuSearchApiGroup> _groups = new ObservableCollection<DanmakuSearchApiGroup>();

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock SubtitleText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private InfoBar ErrorBar;

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
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private IDanmakuSearchDialog_Bindings Bindings;

	public DanmakuSearchDialog(IReadOnlyList<DanmakuApiEntry> apis, string? activeApiBase, string defaultQuery, int? season, int? episode, Func<DanmakuManualLoadRequest, Task> loadCallback)
	{
		_apis = apis;
		_activeApiBase = activeApiBase;
		_loadCallback = loadCallback;
		InitializeComponent();
		ResultsList.ItemsSource = _groups;
		SearchBox.Text = defaultQuery;
		if (!string.IsNullOrEmpty(defaultQuery))
		{
			string text = "当前剧集：" + defaultQuery;
			if (season.HasValue)
			{
				text += $" 第 {season.Value} 季";
			}
			if (episode.HasValue)
			{
				text += $" 第 {episode.Value} 集";
			}
			SubtitleText.Text = text;
			SubtitleText.Visibility = Visibility.Visible;
		}
		if (!string.IsNullOrWhiteSpace(defaultQuery))
		{
			SearchAsync();
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
		string query = SearchBox.Text?.Trim();
		if (string.IsNullOrWhiteSpace(query))
		{
			return;
		}
		if (_apis.Count == 0)
		{
			ErrorBar.Message = "未配置弹幕 API。";
			ErrorBar.IsOpen = true;
			return;
		}
		SetLoading(loading: true);
		ErrorBar.IsOpen = false;
		_groups.Clear();
		string text = NormalizeBase(_activeApiBase);
		foreach (DanmakuApiEntry api in _apis)
		{
			string text2 = NormalizeBase(api.Url);
			_groups.Add(new DanmakuSearchApiGroup
			{
				Api = api,
				ApiBase = text2,
				IsLoading = true,
				IsExpanded = (!string.IsNullOrEmpty(text) && string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
			});
		}
		HintText.Visibility = Visibility.Collapsed;
		ResultsScroll.Visibility = Visibility.Visible;
		LoadingRing.IsActive = false;
		LoadingRing.Visibility = Visibility.Collapsed;
		await Task.WhenAll(_groups.Select((DanmakuSearchApiGroup g) => SearchOneAsync(g, query)).ToArray());
	}

	private static async Task SearchOneAsync(DanmakuSearchApiGroup group, string query)
	{
		try
		{
			group.Animes = await DanmakuParser.SearchEpisodesAsync(group.ApiBase, query);
		}
		catch (Exception ex)
		{
			group.ErrorMessage = ex.Message;
		}
		finally
		{
			group.IsLoading = false;
		}
	}

	private async void OnLoadEpisodeClick(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: DanmakuSearchEpisode tag } button))
		{
			return;
		}
		DanmakuSearchApiGroup danmakuSearchApiGroup = FindGroupForButton(button);
		if (danmakuSearchApiGroup != null)
		{
			DanmakuSearchAnime danmakuSearchAnime = FindAnimeForButton(button);
			int num = FindApiIndex(danmakuSearchApiGroup.ApiBase);
			if (num >= 0)
			{
				button.IsEnabled = false;
				button.Content = "加载中...";
				string commentUrl = DanmakuParser.BuildCommentUrl(danmakuSearchApiGroup.ApiBase, tag.EpisodeId);
				string displayTitle = BuildDisplayTitle(danmakuSearchAnime?.AnimeTitle, tag.EpisodeTitle);
				Hide();
				await _loadCallback(new DanmakuManualLoadRequest(num, commentUrl, displayTitle));
			}
		}
	}

	private int FindApiIndex(string apiBase)
	{
		for (int i = 0; i < _apis.Count; i++)
		{
			if (string.Equals(NormalizeBase(_apis[i].Url), apiBase, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private static string BuildDisplayTitle(string? animeTitle, string episodeTitle)
	{
		string text = animeTitle?.Trim() ?? string.Empty;
		string text2 = episodeTitle?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(text))
		{
			return text2;
		}
		if (string.IsNullOrEmpty(text2))
		{
			return text;
		}
		return text + " · " + text2;
	}

	private static DanmakuSearchApiGroup? FindGroupForButton(FrameworkElement element)
	{
		DependencyObject dependencyObject = element;
		while (dependencyObject is not null)
		{
			if (dependencyObject is FrameworkElement { DataContext: DanmakuSearchApiGroup dataContext })
			{
				return dataContext;
			}
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
		return null;
	}

	private static DanmakuSearchAnime? FindAnimeForButton(FrameworkElement element)
	{
		DependencyObject dependencyObject = element;
		while (dependencyObject is not null)
		{
			if (dependencyObject is FrameworkElement { DataContext: DanmakuSearchAnime dataContext })
			{
				return dataContext;
			}
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
		return null;
	}

	private void SetLoading(bool loading)
	{
		if (loading && _groups.Count == 0)
		{
			LoadingRing.IsActive = true;
			LoadingRing.Visibility = Visibility.Visible;
			HintText.Visibility = Visibility.Collapsed;
			ResultsScroll.Visibility = Visibility.Collapsed;
		}
	}

	private static string NormalizeBase(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.TrimEnd('/');
		}
		return string.Empty;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/DanmakuSearchDialog.xaml");
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
				SubtitleText = target.As<TextBlock>();
				break;
			case 3:
				ErrorBar = target.As<InfoBar>();
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
			case 19:
				target.As<Button>().Click += OnLoadEpisodeClick;
				break;
			case 20:
				SearchBox = target.As<TextBox>();
				SearchBox.KeyDown += OnSearchKeyDown;
				break;
			case 21:
				target.As<Button>().Click += OnSearchClick;
				break;
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		IComponentConnector result = null;
		switch (connectionId)
		{
			case 9:
				{
					Expander expander2 = (Expander)target;
					DanmakuSearchDialog_obj9_Bindings danmakuSearchDialog_obj9_Bindings = new DanmakuSearchDialog_obj9_Bindings();
					result = danmakuSearchDialog_obj9_Bindings;
					danmakuSearchDialog_obj9_Bindings.SetDataRoot(expander2.DataContext);
					expander2.DataContextChanged += danmakuSearchDialog_obj9_Bindings.DataContextChangedHandler;
					DataTemplate.SetExtensionInstance(expander2, danmakuSearchDialog_obj9_Bindings);
					XamlBindingHelper.SetDataTemplateComponent(expander2, danmakuSearchDialog_obj9_Bindings);
					break;
				}
			case 13:
				{
					Expander expander = (Expander)target;
					DanmakuSearchDialog_obj13_Bindings danmakuSearchDialog_obj13_Bindings = new DanmakuSearchDialog_obj13_Bindings();
					result = danmakuSearchDialog_obj13_Bindings;
					danmakuSearchDialog_obj13_Bindings.SetDataRoot(expander.DataContext);
					expander.DataContextChanged += danmakuSearchDialog_obj13_Bindings.DataContextChangedHandler;
					DataTemplate.SetExtensionInstance(expander, danmakuSearchDialog_obj13_Bindings);
					XamlBindingHelper.SetDataTemplateComponent(expander, danmakuSearchDialog_obj13_Bindings);
					break;
				}
			case 17:
				{
					Grid grid = (Grid)target;
					DanmakuSearchDialog_obj17_Bindings danmakuSearchDialog_obj17_Bindings = new DanmakuSearchDialog_obj17_Bindings();
					result = danmakuSearchDialog_obj17_Bindings;
					danmakuSearchDialog_obj17_Bindings.SetDataRoot(grid.DataContext);
					grid.DataContextChanged += danmakuSearchDialog_obj17_Bindings.DataContextChangedHandler;
					DataTemplate.SetExtensionInstance(grid, danmakuSearchDialog_obj17_Bindings);
					XamlBindingHelper.SetDataTemplateComponent(grid, danmakuSearchDialog_obj17_Bindings);
					break;
				}
		}
		return result;
	}
}

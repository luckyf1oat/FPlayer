using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Richasy.WinUIKernel.Share.Base;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Controls_RootLayoutWinRTTypeDetails))]
public sealed class RootLayout : RootLayoutBase, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private interface IRootLayout_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface IRootLayout_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Richasy_WinUIKernel_Share_Base_TrimTextBlock_Text(TrimTextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(ButtonBase obj, ICommand value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (ICommand)XamlBindingHelper.ConvertValue(typeof(ICommand), targetNullValue);
			}
			obj.Command = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(Control obj, bool value)
		{
			obj.IsEnabled = value;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.Markup.IComponentConnector")]
	[WinRTExposedType(typeof(WinUISample_MainWindowWinRTTypeDetails))]
	private class RootLayout_obj1_Bindings : IComponentConnector, IRootLayout_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		private class RootLayout_obj1_BindingsTracking
		{
			private WeakReference<RootLayout_obj1_Bindings> weakRefToBindingObj;

			private long tokenDPC_ViewModel;

			private AppViewModel cache_ViewModel;

			public RootLayout_obj1_BindingsTracking(RootLayout_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<RootLayout_obj1_Bindings>(obj);
			}

			public RootLayout_obj1_Bindings TryGetBindingObject()
			{
				RootLayout_obj1_Bindings target = null;
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
				UpdateChildListeners_ViewModel(null);
			}

			public void DependencyPropertyChanged_ViewModel(DependencyObject sender, DependencyProperty prop)
			{
				RootLayout_obj1_Bindings rootLayout_obj1_Bindings = TryGetBindingObject();
				if (rootLayout_obj1_Bindings != null)
				{
					RootLayout rootLayout = sender as RootLayout;
					if (rootLayout != null)
					{
						rootLayout_obj1_Bindings.Update_ViewModel(rootLayout.ViewModel, 1073741824);
					}
				}
			}

			public void UpdateChildListeners_(RootLayout obj)
			{
				RootLayout_obj1_Bindings rootLayout_obj1_Bindings = TryGetBindingObject();
				if (rootLayout_obj1_Bindings != null)
				{
					if (rootLayout_obj1_Bindings.dataRoot != null)
					{
						rootLayout_obj1_Bindings.dataRoot.UnregisterPropertyChangedCallback(LayoutUserControlBase<AppViewModel>.ViewModelProperty, tokenDPC_ViewModel);
					}
					if (obj != null)
					{
						rootLayout_obj1_Bindings.dataRoot = obj;
						tokenDPC_ViewModel = obj.RegisterPropertyChangedCallback(LayoutUserControlBase<AppViewModel>.ViewModelProperty, DependencyPropertyChanged_ViewModel);
					}
				}
			}

			public void PropertyChanged_ViewModel(object sender, PropertyChangedEventArgs e)
			{
				RootLayout_obj1_Bindings rootLayout_obj1_Bindings = TryGetBindingObject();
				if (rootLayout_obj1_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				AppViewModel appViewModel = sender as AppViewModel;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (appViewModel != null)
					{
						rootLayout_obj1_Bindings.Update_ViewModel_LastOpenedMediaPath(appViewModel.LastOpenedMediaPath, 1073741824);
						rootLayout_obj1_Bindings.Update_ViewModel_LibMpvPath(appViewModel.LibMpvPath, 1073741824);
					}
				}
				else if (!(propertyName == "LastOpenedMediaPath"))
				{
					if (propertyName == "LibMpvPath" && appViewModel != null)
					{
						rootLayout_obj1_Bindings.Update_ViewModel_LibMpvPath(appViewModel.LibMpvPath, 1073741824);
					}
				}
				else if (appViewModel != null)
				{
					rootLayout_obj1_Bindings.Update_ViewModel_LastOpenedMediaPath(appViewModel.LastOpenedMediaPath, 1073741824);
				}
			}

			public void UpdateChildListeners_ViewModel(AppViewModel obj)
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
		}

		private RootLayout dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private ResourceDictionary localResources;

		private WeakReference<FrameworkElement> converterLookupRoot;

		private TrimTextBlock obj3;

		private Button obj4;

		private Button obj5;

		private Button obj6;

		private TrimTextBlock obj7;

		private RootLayout_obj1_BindingsTracking bindingsTracking;

		public RootLayout_obj1_Bindings()
		{
			bindingsTracking = new RootLayout_obj1_BindingsTracking(this);
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 3:
					obj3 = target.As<TrimTextBlock>();
					break;
				case 4:
					obj4 = target.As<Button>();
					break;
				case 5:
					obj5 = target.As<Button>();
					break;
				case 6:
					obj6 = target.As<Button>();
					break;
				case 7:
					obj7 = target.As<TrimTextBlock>();
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
				dataRoot = newDataRoot.As<RootLayout>();
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

		private void Update_(RootLayout obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_(obj);
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_ViewModel(obj.ViewModel, phase);
			}
		}

		private void Update_ViewModel(AppViewModel obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_ViewModel(obj);
			if (obj != null)
			{
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel_LastOpenedMediaPath(obj.LastOpenedMediaPath, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_ViewModel_OpenMediaCommand(obj.OpenMediaCommand, phase);
				}
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel_LibMpvPath(obj.LibMpvPath, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_ViewModel_ReopenLastMediaCommand(obj.ReopenLastMediaCommand, phase);
					Update_ViewModel_PickLibMpvCommand(obj.PickLibMpvCommand, phase);
				}
			}
		}

		private void Update_ViewModel_LastOpenedMediaPath(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Richasy_WinUIKernel_Share_Base_TrimTextBlock_Text(obj3, obj, null);
			}
		}

		private void Update_ViewModel_OpenMediaCommand(IAsyncRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj4, obj, null);
			}
		}

		private void Update_ViewModel_LibMpvPath(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(obj4, (bool)LookupConverter("ObjectToBoolConverter").Convert(obj, typeof(bool), null, null));
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(obj5, (bool)LookupConverter("ObjectToBoolConverter").Convert(obj, typeof(bool), null, null));
				XamlBindingSetters.Set_Richasy_WinUIKernel_Share_Base_TrimTextBlock_Text(obj7, obj, null);
			}
		}

		private void Update_ViewModel_ReopenLastMediaCommand(IAsyncRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj5, obj, null);
			}
		}

		private void Update_ViewModel_PickLibMpvCommand(IAsyncRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj6, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private AppTitleBar TitleBar;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private IRootLayout_Bindings Bindings;

	public RootLayout()
	{
		InitializeComponent();
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/RootLayout.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		if (connectionId == 2)
		{
			TitleBar = target.As<AppTitleBar>();
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
			RootLayoutBase obj = (RootLayoutBase)target;
			RootLayout_obj1_Bindings rootLayout_obj1_Bindings = new RootLayout_obj1_Bindings();
			result = rootLayout_obj1_Bindings;
			rootLayout_obj1_Bindings.SetDataRoot(this);
			rootLayout_obj1_Bindings.SetConverterLookupRoot(this);
			Bindings = rootLayout_obj1_Bindings;
			obj.Loading += rootLayout_obj1_Bindings.Loading;
		}
		return result;
	}
}

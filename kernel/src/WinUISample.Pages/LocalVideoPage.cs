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

namespace WinUISample.Pages;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Pages_LocalVideoPageWinRTTypeDetails))]
public sealed class LocalVideoPage : LocalVideoPageBase, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private interface ILocalVideoPage_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface ILocalVideoPage_BindingsScopeConnector
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

		public static void Set_Microsoft_UI_Xaml_UIElement_Visibility(UIElement obj, Visibility value)
		{
			obj.Visibility = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(ButtonBase obj, ICommand value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (ICommand)XamlBindingHelper.ConvertValue(typeof(ICommand), targetNullValue);
			}
			obj.Command = value;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	[WinRTRuntimeClassName("Microsoft.UI.Xaml.Markup.IComponentConnector")]
	[WinRTExposedType(typeof(WinUISample_MainWindowWinRTTypeDetails))]
	private class LocalVideoPage_obj1_Bindings : IComponentConnector, ILocalVideoPage_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
		[DebuggerNonUserCode]
		private class LocalVideoPage_obj1_BindingsTracking
		{
			private WeakReference<LocalVideoPage_obj1_Bindings> weakRefToBindingObj;

			private long tokenDPC_ViewModel;

			private LocalVideoPageViewModel cache_ViewModel;

			public LocalVideoPage_obj1_BindingsTracking(LocalVideoPage_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<LocalVideoPage_obj1_Bindings>(obj);
			}

			public LocalVideoPage_obj1_Bindings TryGetBindingObject()
			{
				LocalVideoPage_obj1_Bindings target = null;
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
				LocalVideoPage_obj1_Bindings localVideoPage_obj1_Bindings = TryGetBindingObject();
				if (localVideoPage_obj1_Bindings != null)
				{
					LocalVideoPage localVideoPage = sender as LocalVideoPage;
					if (localVideoPage != null)
					{
						localVideoPage_obj1_Bindings.Update_ViewModel(localVideoPage.ViewModel, 1073741824);
					}
				}
			}

			public void UpdateChildListeners_(LocalVideoPage obj)
			{
				LocalVideoPage_obj1_Bindings localVideoPage_obj1_Bindings = TryGetBindingObject();
				if (localVideoPage_obj1_Bindings != null)
				{
					if (localVideoPage_obj1_Bindings.dataRoot != null)
					{
						localVideoPage_obj1_Bindings.dataRoot.UnregisterPropertyChangedCallback(LayoutPageBase<LocalVideoPageViewModel>.ViewModelProperty, tokenDPC_ViewModel);
					}
					if (obj != null)
					{
						localVideoPage_obj1_Bindings.dataRoot = obj;
						tokenDPC_ViewModel = obj.RegisterPropertyChangedCallback(LayoutPageBase<LocalVideoPageViewModel>.ViewModelProperty, DependencyPropertyChanged_ViewModel);
					}
				}
			}

			public void PropertyChanged_ViewModel(object sender, PropertyChangedEventArgs e)
			{
				LocalVideoPage_obj1_Bindings localVideoPage_obj1_Bindings = TryGetBindingObject();
				if (localVideoPage_obj1_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				LocalVideoPageViewModel localVideoPageViewModel = sender as LocalVideoPageViewModel;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (localVideoPageViewModel != null)
					{
						localVideoPage_obj1_Bindings.Update_ViewModel_LastVideoPath(localVideoPageViewModel.LastVideoPath, 1073741824);
					}
				}
				else if (propertyName == "LastVideoPath" && localVideoPageViewModel != null)
				{
					localVideoPage_obj1_Bindings.Update_ViewModel_LastVideoPath(localVideoPageViewModel.LastVideoPath, 1073741824);
				}
			}

			public void UpdateChildListeners_ViewModel(LocalVideoPageViewModel obj)
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

		private LocalVideoPage dataRoot;

		private bool initialized;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private ResourceDictionary localResources;

		private WeakReference<FrameworkElement> converterLookupRoot;

		private TrimTextBlock obj2;

		private Button obj3;

		private Button obj4;

		private LocalVideoPage_obj1_BindingsTracking bindingsTracking;

		public LocalVideoPage_obj1_Bindings()
		{
			bindingsTracking = new LocalVideoPage_obj1_BindingsTracking(this);
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
				case 2:
					obj2 = target.As<TrimTextBlock>();
					break;
				case 3:
					obj3 = target.As<Button>();
					break;
				case 4:
					obj4 = target.As<Button>();
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
				dataRoot = newDataRoot.As<LocalVideoPage>();
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

		private void Update_(LocalVideoPage obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_(obj);
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_ViewModel(obj.ViewModel, phase);
			}
		}

		private void Update_ViewModel(LocalVideoPageViewModel obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_ViewModel(obj);
			if (obj != null)
			{
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel_LastVideoPath(obj.LastVideoPath, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_ViewModel_OpenLocalFileCommand(obj.OpenLocalFileCommand, phase);
					Update_ViewModel_ReopenLastFileCommand(obj.ReopenLastFileCommand, phase);
				}
			}
		}

		private void Update_ViewModel_LastVideoPath(string obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				XamlBindingSetters.Set_Richasy_WinUIKernel_Share_Base_TrimTextBlock_Text(obj2, obj, null);
				XamlBindingSetters.Set_Microsoft_UI_Xaml_UIElement_Visibility(obj2, (Visibility)LookupConverter("ObjectToVisibilityConverter").Convert(obj, typeof(Visibility), null, null));
				XamlBindingSetters.Set_Microsoft_UI_Xaml_UIElement_Visibility(obj4, (Visibility)LookupConverter("ObjectToVisibilityConverter").Convert(obj, typeof(Visibility), null, null));
			}
		}

		private void Update_ViewModel_OpenLocalFileCommand(IAsyncRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj3, obj, null);
			}
		}

		private void Update_ViewModel_ReopenLastFileCommand(IAsyncRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj4, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ILocalVideoPage_Bindings Bindings;

	public LocalVideoPage()
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
			Uri resourceLocator = new Uri("ms-appx:///Pages/LocalVideoPage.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		IComponentConnector result = null;
		if (connectionId == 1)
		{
			LocalVideoPageBase obj = (LocalVideoPageBase)target;
			LocalVideoPage_obj1_Bindings localVideoPage_obj1_Bindings = new LocalVideoPage_obj1_Bindings();
			result = localVideoPage_obj1_Bindings;
			localVideoPage_obj1_Bindings.SetDataRoot(this);
			localVideoPage_obj1_Bindings.SetConverterLookupRoot(this);
			Bindings = localVideoPage_obj1_Bindings;
			obj.Loading += localVideoPage_obj1_Bindings.Loading;
		}
		return result;
	}
}

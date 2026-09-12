using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Richasy.WinUIKernel.Share.Base;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUIEx;
using WinUISample.ViewModels;

namespace WinUISample;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Markup.IComponentConnector")]
[WinRTExposedType(typeof(WinUISample_MainWindowWinRTTypeDetails))]
public sealed class MainWindow : WindowBase, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	public AppViewModel AppVM { get; }

	public MainWindow()
	{
		InitializeComponent();
		base.Width = 980.0;
		base.Height = 720.0;
		this.CenterOnScreen();
		AppVM = this.Get<AppViewModel>();
		AppVM.MainWindow = this;
		base.Activated += OnActivated;
	}

	private void OnActivated(object sender, WindowActivatedEventArgs args)
	{
		if (args.WindowActivationState != WindowActivationState.Deactivated)
		{
			AppVM.ActivateXamlRoot = base.Content.XamlRoot;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///MainWindow.xaml");
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
		return null;
	}
}

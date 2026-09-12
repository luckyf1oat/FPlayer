using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.IUIElementOverrides")]
[WinRTExposedType(typeof(WinUISample_Controls_SubtitleSyncDialogWinRTTypeDetails))]
public sealed class SubtitleSyncDialog : ContentDialog, IComponentConnector
{
	private readonly PlayerViewModel _viewModel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private NumberBox DelayBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	public SubtitleSyncDialog(PlayerViewModel viewModel)
	{
		_viewModel = viewModel;
		InitializeComponent();
		base.Opened += OnOpened;
		base.PrimaryButtonClick += OnPrimaryButtonClick;
	}

	private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
	{
		DelayBox.Value = _viewModel.SubtitleDelay;
		DelayBox.Focus(FocusState.Programmatic);
	}

	private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
	{
		if (double.IsNaN(DelayBox.Value))
		{
			args.Cancel = true;
			return;
		}
		args.Cancel = true;
		Hide();
		await _viewModel.SetSubtitleDelayCommand.ExecuteAsync(DelayBox.Value);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/SubtitleSyncDialog.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		if (connectionId == 2)
		{
			DelayBox = target.As<NumberBox>();
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		return null;
	}
}

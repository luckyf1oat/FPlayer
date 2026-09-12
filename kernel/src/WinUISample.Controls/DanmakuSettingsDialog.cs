using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
public sealed class DanmakuSettingsDialog : ContentDialog, IComponentConnector
{
	private readonly PlayerViewModel _viewModel;

	private bool _loading;

	private static List<string>? _cachedSystemFonts;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private CheckBox RollingCheckBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private CheckBox TopCheckBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private CheckBox BottomCheckBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider TimeOffsetSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock TimeOffsetLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ToggleSwitch FollowSpeedToggle;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider SpeedSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock SpeedLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider OutlineSizeSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock OutlineSizeLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ToggleSwitch BoldToggle;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider FontSizeSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock FontSizeLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ComboBox FontFamilyCombo;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ToggleSwitch NoOverlapSubtitleToggle;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private ComboBox DensityCombo;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider AreaRatioSlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock AreaRatioLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private Slider OpacitySlider;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private TextBlock OpacityLabel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	private bool _contentLoaded;

	public DanmakuSettingsDialog(PlayerViewModel viewModel)
	{
		_viewModel = viewModel;
		InitializeComponent();
		base.PrimaryButtonClick += OnResetClick;
		base.Opened += OnOpened;
	}

	private void PopulateFontFamilyCombo()
	{
		if (FontFamilyCombo.Items.Count > 0)
		{
			return;
		}
		if (_cachedSystemFonts == null)
		{
			_cachedSystemFonts = (from f in System.Drawing.FontFamily.Families
								  select f.Name into n
								  where !string.IsNullOrWhiteSpace(n)
								  select n).OrderBy<string, string>((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
		}
		FontFamilyCombo.Items.Add(new ComboBoxItem
		{
			Content = "系统默认",
			Tag = string.Empty
		});
		foreach (string cachedSystemFont in _cachedSystemFonts)
		{
			FontFamilyCombo.Items.Add(new ComboBoxItem
			{
				Content = cachedSystemFont,
				Tag = cachedSystemFont
			});
		}
	}

	private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
	{
		_loading = true;
		PopulateFontFamilyCombo();
		TimeOffsetSlider.Minimum = -60.0;
		TimeOffsetSlider.Maximum = 60.0;
		TimeOffsetSlider.StepFrequency = 1.0;
		OpacitySlider.Minimum = 0.0;
		OpacitySlider.Maximum = 90.0;
		OpacitySlider.StepFrequency = 5.0;
		AreaRatioSlider.Minimum = 1.0;
		AreaRatioSlider.Maximum = 10.0;
		AreaRatioSlider.StepFrequency = 1.0;
		FontSizeSlider.Minimum = 1.0;
		FontSizeSlider.Maximum = 20.0;
		FontSizeSlider.StepFrequency = 1.0;
		SpeedSlider.Minimum = 1.0;
		SpeedSlider.Maximum = 10.0;
		SpeedSlider.StepFrequency = 1.0;
		OutlineSizeSlider.Minimum = 0.0;
		OutlineSizeSlider.Maximum = 5.0;
		OutlineSizeSlider.StepFrequency = 0.5;
		OpacitySlider.Value = (1.0 - _viewModel.DanmakuOpacity) * 100.0;
		AreaRatioSlider.Value = _viewModel.DanmakuAreaRatio;
		FontSizeSlider.Value = _viewModel.DanmakuFontSizeOffset * 10.0;
		SpeedSlider.Value = _viewModel.DanmakuSpeed;
		OutlineSizeSlider.Value = _viewModel.DanmakuOutlineSize;
		FollowSpeedToggle.IsOn = _viewModel.DanmakuFollowSpeed;
		BoldToggle.IsOn = _viewModel.DanmakuBold;
		NoOverlapSubtitleToggle.IsOn = _viewModel.DanmakuNoOverlapSubtitle;
		RollingCheckBox.IsChecked = _viewModel.DanmakuRollingEnabled;
		TopCheckBox.IsChecked = _viewModel.DanmakuTopEnabled;
		BottomCheckBox.IsChecked = _viewModel.DanmakuBottomEnabled;
		TimeOffsetSlider.Value = _viewModel.DanmakuTimeOffsetSeconds;
		SelectDensityCombo(_viewModel.DanmakuDensity);
		SelectFontFamilyCombo(_viewModel.DanmakuFontFamily);
		UpdateLabels();
		_loading = false;
	}

	private void OnResetClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
	{
		args.Cancel = true;
		_loading = true;
		TimeOffsetSlider.Value = 0.0;
		OpacitySlider.Value = 0.0;
		AreaRatioSlider.Value = 9.0;
		FontSizeSlider.Value = 10.0;
		SpeedSlider.Value = 5.0;
		OutlineSizeSlider.Value = 1.0;
		FollowSpeedToggle.IsOn = true;
		BoldToggle.IsOn = false;
		NoOverlapSubtitleToggle.IsOn = false;
		RollingCheckBox.IsChecked = true;
		TopCheckBox.IsChecked = true;
		BottomCheckBox.IsChecked = true;
		SelectDensityCombo(-1);
		SelectFontFamilyCombo(string.Empty);
		UpdateLabels();
		_loading = false;
		CommitAll();
	}

	private void OnTimeOffsetChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateTimeOffsetLabel();
		if (!_loading)
		{
			_viewModel.DanmakuTimeOffsetSeconds = TimeOffsetSlider.Value;
		}
	}

	private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateOpacityLabel();
		if (!_loading)
		{
			_viewModel.DanmakuOpacity = 1.0 - OpacitySlider.Value / 100.0;
		}
	}

	private void OnAreaRatioChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateAreaRatioLabel();
		if (!_loading)
		{
			_viewModel.DanmakuAreaRatio = (int)AreaRatioSlider.Value;
		}
	}

	private void OnFontSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateFontSizeLabel();
		if (!_loading)
		{
			_viewModel.DanmakuFontSizeOffset = FontSizeSlider.Value / 10.0;
		}
	}

	private void OnSpeedChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateSpeedLabel();
		if (!_loading)
		{
			_viewModel.DanmakuSpeed = SpeedSlider.Value;
		}
	}

	private void OnFollowSpeedToggled(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			_viewModel.DanmakuFollowSpeed = FollowSpeedToggle.IsOn;
		}
	}

	private void OnBoldToggled(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			_viewModel.DanmakuBold = BoldToggle.IsOn;
		}
	}

	private void OnDensityChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading && DensityCombo.SelectedItem is ComboBoxItem { Tag: var tag } && int.TryParse(tag?.ToString(), out var result))
		{
			_viewModel.DanmakuDensity = result;
		}
	}

	private void OnTypeChanged(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			_viewModel.DanmakuRollingEnabled = RollingCheckBox.IsChecked == true;
			_viewModel.DanmakuTopEnabled = TopCheckBox.IsChecked == true;
			_viewModel.DanmakuBottomEnabled = BottomCheckBox.IsChecked == true;
		}
	}

	private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading && FontFamilyCombo.SelectedItem is ComboBoxItem comboBoxItem)
		{
			_viewModel.DanmakuFontFamily = comboBoxItem.Tag?.ToString() ?? string.Empty;
		}
	}

	private void OnOutlineSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateOutlineSizeLabel();
		if (!_loading)
		{
			_viewModel.DanmakuOutlineSize = OutlineSizeSlider.Value;
		}
	}

	private void OnNoOverlapSubtitleToggled(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			_viewModel.DanmakuNoOverlapSubtitle = NoOverlapSubtitleToggle.IsOn;
		}
	}

	private void SelectDensityCombo(int density)
	{
		string text = density.ToString();
		foreach (ComboBoxItem item in DensityCombo.Items)
		{
			if (item.Tag?.ToString() == text)
			{
				DensityCombo.SelectedItem = item;
				return;
			}
		}
		DensityCombo.SelectedIndex = 0;
	}

	private void SelectFontFamilyCombo(string fontFamily)
	{
		foreach (ComboBoxItem item in FontFamilyCombo.Items)
		{
			if (string.Equals(item.Tag?.ToString(), fontFamily, StringComparison.OrdinalIgnoreCase))
			{
				FontFamilyCombo.SelectedItem = item;
				return;
			}
		}
		FontFamilyCombo.SelectedIndex = 0;
	}

	private void CommitAll()
	{
		_viewModel.DanmakuTimeOffsetSeconds = TimeOffsetSlider.Value;
		_viewModel.DanmakuOpacity = 1.0 - OpacitySlider.Value / 100.0;
		_viewModel.DanmakuAreaRatio = (int)Math.Round(AreaRatioSlider.Value);
		_viewModel.DanmakuFontSizeOffset = FontSizeSlider.Value / 10.0;
		_viewModel.DanmakuSpeed = SpeedSlider.Value;
		_viewModel.DanmakuOutlineSize = OutlineSizeSlider.Value;
		_viewModel.DanmakuFollowSpeed = FollowSpeedToggle.IsOn;
		_viewModel.DanmakuBold = BoldToggle.IsOn;
		_viewModel.DanmakuNoOverlapSubtitle = NoOverlapSubtitleToggle.IsOn;
		_viewModel.DanmakuRollingEnabled = RollingCheckBox.IsChecked == true;
		_viewModel.DanmakuTopEnabled = TopCheckBox.IsChecked == true;
		_viewModel.DanmakuBottomEnabled = BottomCheckBox.IsChecked == true;
		if (DensityCombo.SelectedItem is ComboBoxItem { Tag: var tag } && int.TryParse(tag?.ToString(), out var result))
		{
			_viewModel.DanmakuDensity = result;
		}
		if (FontFamilyCombo.SelectedItem is ComboBoxItem comboBoxItem2)
		{
			_viewModel.DanmakuFontFamily = comboBoxItem2.Tag?.ToString() ?? string.Empty;
		}
	}

	private void UpdateLabels()
	{
		UpdateTimeOffsetLabel();
		UpdateOpacityLabel();
		UpdateAreaRatioLabel();
		UpdateFontSizeLabel();
		UpdateSpeedLabel();
		UpdateOutlineSizeLabel();
	}

	private void UpdateTimeOffsetLabel()
	{
		int num = (int)TimeOffsetSlider.Value;
		TimeOffsetLabel.Text = ((num >= 0) ? $"+{num}s" : $"{num}s");
	}

	private void UpdateOpacityLabel()
	{
		OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";
	}

	private void UpdateAreaRatioLabel()
	{
		AreaRatioLabel.Text = $"{(int)(AreaRatioSlider.Value * 10.0)}%";
	}

	private void UpdateFontSizeLabel()
	{
		FontSizeLabel.Text = $"×{FontSizeSlider.Value / 10.0:0.0}";
	}

	private void UpdateSpeedLabel()
	{
		SpeedLabel.Text = $"{(int)SpeedSlider.Value}";
	}

	private void UpdateOutlineSizeLabel()
	{
		OutlineSizeLabel.Text = $"{OutlineSizeSlider.Value:0.0}";
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Controls/DanmakuSettingsDialog.xaml");
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
				RollingCheckBox = target.As<CheckBox>();
				RollingCheckBox.Checked += OnTypeChanged;
				RollingCheckBox.Unchecked += OnTypeChanged;
				break;
			case 3:
				TopCheckBox = target.As<CheckBox>();
				TopCheckBox.Checked += OnTypeChanged;
				TopCheckBox.Unchecked += OnTypeChanged;
				break;
			case 4:
				BottomCheckBox = target.As<CheckBox>();
				BottomCheckBox.Checked += OnTypeChanged;
				BottomCheckBox.Unchecked += OnTypeChanged;
				break;
			case 5:
				TimeOffsetSlider = target.As<Slider>();
				TimeOffsetSlider.ValueChanged += OnTimeOffsetChanged;
				break;
			case 6:
				TimeOffsetLabel = target.As<TextBlock>();
				break;
			case 7:
				FollowSpeedToggle = target.As<ToggleSwitch>();
				FollowSpeedToggle.Toggled += OnFollowSpeedToggled;
				break;
			case 8:
				SpeedSlider = target.As<Slider>();
				SpeedSlider.ValueChanged += OnSpeedChanged;
				break;
			case 9:
				SpeedLabel = target.As<TextBlock>();
				break;
			case 10:
				OutlineSizeSlider = target.As<Slider>();
				OutlineSizeSlider.ValueChanged += OnOutlineSizeChanged;
				break;
			case 11:
				OutlineSizeLabel = target.As<TextBlock>();
				break;
			case 12:
				BoldToggle = target.As<ToggleSwitch>();
				BoldToggle.Toggled += OnBoldToggled;
				break;
			case 13:
				FontSizeSlider = target.As<Slider>();
				FontSizeSlider.ValueChanged += OnFontSizeChanged;
				break;
			case 14:
				FontSizeLabel = target.As<TextBlock>();
				break;
			case 15:
				FontFamilyCombo = target.As<ComboBox>();
				FontFamilyCombo.SelectionChanged += OnFontFamilyChanged;
				break;
			case 16:
				NoOverlapSubtitleToggle = target.As<ToggleSwitch>();
				NoOverlapSubtitleToggle.Toggled += OnNoOverlapSubtitleToggled;
				break;
			case 17:
				DensityCombo = target.As<ComboBox>();
				DensityCombo.SelectionChanged += OnDensityChanged;
				break;
			case 18:
				AreaRatioSlider = target.As<Slider>();
				AreaRatioSlider.ValueChanged += OnAreaRatioChanged;
				break;
			case 19:
				AreaRatioLabel = target.As<TextBlock>();
				break;
			case 20:
				OpacitySlider = target.As<Slider>();
				OpacitySlider.ValueChanged += OnOpacityChanged;
				break;
			case 21:
				OpacityLabel = target.As<TextBlock>();
				break;
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

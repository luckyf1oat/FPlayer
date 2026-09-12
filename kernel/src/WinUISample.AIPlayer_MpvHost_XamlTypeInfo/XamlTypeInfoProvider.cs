using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using FluentIcons.WinUI.Internals;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.XamlTypeInfo;
using Richasy.MpvKernel.WinUI.Converters;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Converters;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.Globalization.NumberFormatting;
using WinUIEx;
using WinUISample.Controls;
using WinUISample.Pages;
using WinUISample.ViewModels;

namespace WinUISample.AIPlayer_MpvHost_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2509")]
[DebuggerNonUserCode]
internal class XamlTypeInfoProvider
{
	private Dictionary<string, IXamlType> _xamlTypeCacheByName = new Dictionary<string, IXamlType>();

	private Dictionary<Type, IXamlType> _xamlTypeCacheByType = new Dictionary<Type, IXamlType>();

	private Dictionary<string, IXamlMember> _xamlMembers = new Dictionary<string, IXamlMember>();

	private string[] _typeNameTable;

	private Type[] _typeTable;

	private List<IXamlMetadataProvider> _otherProviders;

	private List<IXamlMetadataProvider> OtherProviders
	{
		get
		{
			if (_otherProviders == null)
			{
				List<IXamlMetadataProvider> list = new List<IXamlMetadataProvider>();
				IXamlMetadataProvider item = new XamlControlsXamlMetaDataProvider();
				list.Add(item);
				item = new CommunityToolkit.WinUI.Controls.SizersRns.CommunityToolkit_WinUI_Controls_Sizers_XamlTypeInfo.XamlMetaDataProvider();
				list.Add(item);
				item = new Richasy.MpvKernel.WinUI.MpvKernel_WinUI_XamlTypeInfo.XamlMetaDataProvider();
				list.Add(item);
				item = new Richasy.WinUIKernel.Share.WinUIKernel_Share_XamlTypeInfo.XamlMetaDataProvider();
				list.Add(item);
				item = new WinUIEx.WinUIEx_XamlTypeInfo.XamlMetaDataProvider();
				list.Add(item);
				_otherProviders = list;
			}
			return _otherProviders;
		}
	}

	public IXamlType GetXamlTypeByType(Type type)
	{
		IXamlType value;
		lock (_xamlTypeCacheByType)
		{
			if (_xamlTypeCacheByType.TryGetValue(type, out value))
			{
				return value;
			}
			int num = LookupTypeIndexByType(type);
			if (num != -1)
			{
				value = CreateXamlType(num);
			}
			XamlUserType xamlUserType = value as XamlUserType;
			if (value == null || (xamlUserType != null && xamlUserType.IsReturnTypeStub && !xamlUserType.IsLocalType))
			{
				IXamlType xamlType = CheckOtherMetadataProvidersForType(type);
				if (xamlType != null && (xamlType.IsConstructible || value == null))
				{
					value = xamlType;
				}
			}
			if (value != null)
			{
				_xamlTypeCacheByName.Add(value.FullName, value);
				_xamlTypeCacheByType.Add(value.UnderlyingType, value);
			}
		}
		return value;
	}

	public IXamlType GetXamlTypeByName(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}
		IXamlType value;
		lock (_xamlTypeCacheByType)
		{
			if (_xamlTypeCacheByName.TryGetValue(typeName, out value))
			{
				return value;
			}
			int num = LookupTypeIndexByName(typeName);
			if (num != -1)
			{
				value = CreateXamlType(num);
			}
			XamlUserType xamlUserType = value as XamlUserType;
			if (value == null || (xamlUserType != null && xamlUserType.IsReturnTypeStub && !xamlUserType.IsLocalType))
			{
				IXamlType xamlType = CheckOtherMetadataProvidersForName(typeName);
				if (xamlType != null && (xamlType.IsConstructible || value == null))
				{
					value = xamlType;
				}
			}
			if (value != null)
			{
				_xamlTypeCacheByName.Add(value.FullName, value);
				_xamlTypeCacheByType.Add(value.UnderlyingType, value);
			}
		}
		return value;
	}

	public IXamlMember GetMemberByLongName(string longMemberName)
	{
		if (string.IsNullOrEmpty(longMemberName))
		{
			return null;
		}
		IXamlMember value;
		lock (_xamlMembers)
		{
			if (_xamlMembers.TryGetValue(longMemberName, out value))
			{
				return value;
			}
			value = CreateXamlMember(longMemberName);
			if (value != null)
			{
				_xamlMembers.Add(longMemberName, value);
			}
		}
		return value;
	}

	private void InitTypeTables()
	{
		_typeNameTable = new string[86];
		_typeNameTable[0] = "Microsoft.UI.Xaml.Controls.XamlControlsResources";
		_typeNameTable[1] = "Microsoft.UI.Xaml.ResourceDictionary";
		_typeNameTable[2] = "Object";
		_typeNameTable[3] = "Boolean";
		_typeNameTable[4] = "Richasy.WinUIKernel.Share.Converters.BoolToVisibilityConverter";
		_typeNameTable[5] = "Richasy.WinUIKernel.Share.Converters.ObjectToBoolConverter";
		_typeNameTable[6] = "Richasy.WinUIKernel.Share.Converters.ObjectToVisibilityConverter";
		_typeNameTable[7] = "Richasy.WinUIKernel.Share.Converters.BoolToIconVariantConverter";
		_typeNameTable[8] = "Microsoft.UI.Xaml.Controls.ProgressRing";
		_typeNameTable[9] = "Microsoft.UI.Xaml.Controls.Control";
		_typeNameTable[10] = "Double";
		_typeNameTable[11] = "Microsoft.UI.Xaml.Controls.ProgressRingTemplateSettings";
		_typeNameTable[12] = "Microsoft.UI.Xaml.DependencyObject";
		_typeNameTable[13] = "Microsoft.UI.Xaml.Controls.InfoBar";
		_typeNameTable[14] = "Microsoft.UI.Xaml.Controls.InfoBarSeverity";
		_typeNameTable[15] = "System.Enum";
		_typeNameTable[16] = "System.ValueType";
		_typeNameTable[17] = "Microsoft.UI.Xaml.Controls.Primitives.ButtonBase";
		_typeNameTable[18] = "System.Windows.Input.ICommand";
		_typeNameTable[19] = "Microsoft.UI.Xaml.Style";
		_typeNameTable[20] = "Microsoft.UI.Xaml.DataTemplate";
		_typeNameTable[21] = "Microsoft.UI.Xaml.Controls.IconSource";
		_typeNameTable[22] = "String";
		_typeNameTable[23] = "Microsoft.UI.Xaml.Controls.InfoBarTemplateSettings";
		_typeNameTable[24] = "Microsoft.UI.Xaml.Controls.Expander";
		_typeNameTable[25] = "Microsoft.UI.Xaml.Controls.ContentControl";
		_typeNameTable[26] = "Microsoft.UI.Xaml.Controls.ExpandDirection";
		_typeNameTable[27] = "Microsoft.UI.Xaml.Controls.DataTemplateSelector";
		_typeNameTable[28] = "Microsoft.UI.Xaml.Controls.ExpanderTemplateSettings";
		_typeNameTable[29] = "WinUISample.Controls.DanmakuSearchDialog";
		_typeNameTable[30] = "Microsoft.UI.Xaml.Controls.ContentDialog";
		_typeNameTable[31] = "WinUISample.Controls.DanmakuSettingsDialog";
		_typeNameTable[32] = "WinUISample.Controls.PlayerOverlayBase";
		_typeNameTable[33] = "Microsoft.UI.Xaml.Controls.UserControl";
		_typeNameTable[34] = "WinUISample.ViewModels.PlayerViewModel";
		_typeNameTable[35] = "Richasy.WinUIKernel.Share.ViewModels.ViewModelBase";
		_typeNameTable[36] = "CommunityToolkit.Mvvm.ComponentModel.ObservableObject";
		_typeNameTable[37] = "Richasy.MpvKernel.WinUI.Converters.SecondsToTimeTextConverter";
		_typeNameTable[38] = "Microsoft.UI.Xaml.Controls.Button";
		_typeNameTable[39] = "FluentIcons.WinUI.SymbolIcon";
		_typeNameTable[40] = "FluentIcons.WinUI.Internals.GenericIcon";
		_typeNameTable[41] = "Microsoft.UI.Xaml.Controls.FontIcon";
		_typeNameTable[42] = "FluentIcons.Common.Symbol";
		_typeNameTable[43] = "FluentIcons.Common.IconVariant";
		_typeNameTable[44] = "Microsoft.UI.Xaml.Controls.ProgressBar";
		_typeNameTable[45] = "Microsoft.UI.Xaml.Controls.Primitives.RangeBase";
		_typeNameTable[46] = "Microsoft.UI.Xaml.Controls.ProgressBarTemplateSettings";
		_typeNameTable[47] = "WinUISample.Controls.PlayerOverlay";
		_typeNameTable[48] = "WinUISample.Controls.RootLayoutBase";
		_typeNameTable[49] = "Richasy.WinUIKernel.Share.Base.LayoutUserControlBase`1<WinUISample.ViewModels.AppViewModel>";
		_typeNameTable[50] = "Richasy.WinUIKernel.Share.Base.LayoutUserControlBase";
		_typeNameTable[51] = "WinUISample.ViewModels.AppViewModel";
		_typeNameTable[52] = "Richasy.WinUIKernel.Share.Base.AppTitleBar";
		_typeNameTable[53] = "Richasy.WinUIKernel.Share.Base.LayoutControlBase";
		_typeNameTable[54] = "Microsoft.UI.Xaml.Controls.IconElement";
		_typeNameTable[55] = "Richasy.WinUIKernel.Share.Base.AppTitleBarTemplateSettings";
		_typeNameTable[56] = "Richasy.WinUIKernel.Share.Base.TrimTextBlock";
		_typeNameTable[57] = "Int32";
		_typeNameTable[58] = "WinUISample.Controls.RootLayout";
		_typeNameTable[59] = "WinUISample.Controls.SubtitleSearchDialog";
		_typeNameTable[60] = "Microsoft.UI.Xaml.Controls.NumberBox";
		_typeNameTable[61] = "Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode";
		_typeNameTable[62] = "Windows.Globalization.NumberFormatting.INumberFormatter2";
		_typeNameTable[63] = "Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase";
		_typeNameTable[64] = "Microsoft.UI.Xaml.Media.SolidColorBrush";
		_typeNameTable[65] = "Microsoft.UI.Xaml.TextReadingOrder";
		_typeNameTable[66] = "Microsoft.UI.Xaml.Controls.NumberBoxValidationMode";
		_typeNameTable[67] = "WinUISample.Controls.SubtitleSyncDialog";
		_typeNameTable[68] = "Richasy.WinUIKernel.Share.Base.WindowBase";
		_typeNameTable[69] = "WinUIEx.WindowEx";
		_typeNameTable[70] = "Microsoft.UI.Xaml.Window";
		_typeNameTable[71] = "Microsoft.UI.Windowing.AppWindow";
		_typeNameTable[72] = "WinUIEx.Icon";
		_typeNameTable[73] = "WinUIEx.WindowState";
		_typeNameTable[74] = "Microsoft.UI.Windowing.AppWindowPresenter";
		_typeNameTable[75] = "Microsoft.UI.Windowing.AppWindowPresenterKind";
		_typeNameTable[76] = "WinUIEx.SystemBackdrop";
		_typeNameTable[77] = "WinUISample.MainWindow";
		_typeNameTable[78] = "WinUISample.Pages.LocalVideoPageBase";
		_typeNameTable[79] = "Richasy.WinUIKernel.Share.Base.LayoutPageBase`1<WinUISample.ViewModels.LocalVideoPageViewModel>";
		_typeNameTable[80] = "Richasy.WinUIKernel.Share.Base.LayoutPageBase";
		_typeNameTable[81] = "Microsoft.UI.Xaml.Controls.Page";
		_typeNameTable[82] = "WinUISample.ViewModels.LocalVideoPageViewModel";
		_typeNameTable[83] = "WinUISample.Pages.LocalVideoPage";
		_typeNameTable[84] = "Microsoft.UI.Xaml.Controls.TreeViewNode";
		_typeNameTable[85] = "System.Collections.Generic.IList`1<Microsoft.UI.Xaml.Controls.TreeViewNode>";
		_typeTable = new Type[86];
		_typeTable[0] = typeof(XamlControlsResources);
		_typeTable[1] = typeof(ResourceDictionary);
		_typeTable[2] = typeof(object);
		_typeTable[3] = typeof(bool);
		_typeTable[4] = typeof(BoolToVisibilityConverter);
		_typeTable[5] = typeof(ObjectToBoolConverter);
		_typeTable[6] = typeof(ObjectToVisibilityConverter);
		_typeTable[7] = typeof(BoolToIconVariantConverter);
		_typeTable[8] = typeof(ProgressRing);
		_typeTable[9] = typeof(Control);
		_typeTable[10] = typeof(double);
		_typeTable[11] = typeof(ProgressRingTemplateSettings);
		_typeTable[12] = typeof(DependencyObject);
		_typeTable[13] = typeof(InfoBar);
		_typeTable[14] = typeof(InfoBarSeverity);
		_typeTable[15] = typeof(Enum);
		_typeTable[16] = typeof(ValueType);
		_typeTable[17] = typeof(ButtonBase);
		_typeTable[18] = typeof(ICommand);
		_typeTable[19] = typeof(Style);
		_typeTable[20] = typeof(DataTemplate);
		_typeTable[21] = typeof(IconSource);
		_typeTable[22] = typeof(string);
		_typeTable[23] = typeof(InfoBarTemplateSettings);
		_typeTable[24] = typeof(Expander);
		_typeTable[25] = typeof(ContentControl);
		_typeTable[26] = typeof(ExpandDirection);
		_typeTable[27] = typeof(DataTemplateSelector);
		_typeTable[28] = typeof(ExpanderTemplateSettings);
		_typeTable[29] = typeof(DanmakuSearchDialog);
		_typeTable[30] = typeof(ContentDialog);
		_typeTable[31] = typeof(DanmakuSettingsDialog);
		_typeTable[32] = typeof(PlayerOverlayBase);
		_typeTable[33] = typeof(UserControl);
		_typeTable[34] = typeof(PlayerViewModel);
		_typeTable[35] = typeof(ViewModelBase);
		_typeTable[36] = typeof(ObservableObject);
		_typeTable[37] = typeof(SecondsToTimeTextConverter);
		_typeTable[38] = typeof(Button);
		_typeTable[39] = typeof(FluentIcons.WinUI.SymbolIcon);
		_typeTable[40] = typeof(GenericIcon);
		_typeTable[41] = typeof(FontIcon);
		_typeTable[42] = typeof(FluentIcons.Common.Symbol);
		_typeTable[43] = typeof(IconVariant);
		_typeTable[44] = typeof(ProgressBar);
		_typeTable[45] = typeof(RangeBase);
		_typeTable[46] = typeof(ProgressBarTemplateSettings);
		_typeTable[47] = typeof(PlayerOverlay);
		_typeTable[48] = typeof(RootLayoutBase);
		_typeTable[49] = typeof(LayoutUserControlBase<AppViewModel>);
		_typeTable[50] = typeof(LayoutUserControlBase);
		_typeTable[51] = typeof(AppViewModel);
		_typeTable[52] = typeof(AppTitleBar);
		_typeTable[53] = typeof(LayoutControlBase);
		_typeTable[54] = typeof(IconElement);
		_typeTable[55] = typeof(AppTitleBarTemplateSettings);
		_typeTable[56] = typeof(TrimTextBlock);
		_typeTable[57] = typeof(int);
		_typeTable[58] = typeof(RootLayout);
		_typeTable[59] = typeof(SubtitleSearchDialog);
		_typeTable[60] = typeof(NumberBox);
		_typeTable[61] = typeof(NumberBoxSpinButtonPlacementMode);
		_typeTable[62] = typeof(INumberFormatter2);
		_typeTable[63] = typeof(FlyoutBase);
		_typeTable[64] = typeof(SolidColorBrush);
		_typeTable[65] = typeof(TextReadingOrder);
		_typeTable[66] = typeof(NumberBoxValidationMode);
		_typeTable[67] = typeof(SubtitleSyncDialog);
		_typeTable[68] = typeof(WindowBase);
		_typeTable[69] = typeof(WindowEx);
		_typeTable[70] = typeof(Window);
		_typeTable[71] = typeof(AppWindow);
		_typeTable[72] = typeof(WinUIEx.Icon);
		_typeTable[73] = typeof(WindowState);
		_typeTable[74] = typeof(AppWindowPresenter);
		_typeTable[75] = typeof(AppWindowPresenterKind);
		_typeTable[76] = typeof(WinUIEx.SystemBackdrop);
		_typeTable[77] = typeof(MainWindow);
		_typeTable[78] = typeof(LocalVideoPageBase);
		_typeTable[79] = typeof(LayoutPageBase<LocalVideoPageViewModel>);
		_typeTable[80] = typeof(LayoutPageBase);
		_typeTable[81] = typeof(Page);
		_typeTable[82] = typeof(LocalVideoPageViewModel);
		_typeTable[83] = typeof(LocalVideoPage);
		_typeTable[84] = typeof(TreeViewNode);
		_typeTable[85] = typeof(IList<TreeViewNode>);
	}

	private int LookupTypeIndexByName(string typeName)
	{
		if (_typeNameTable == null)
		{
			InitTypeTables();
		}
		for (int i = 0; i < _typeNameTable.Length; i++)
		{
			if (string.CompareOrdinal(_typeNameTable[i], typeName) == 0)
			{
				return i;
			}
		}
		return -1;
	}

	private int LookupTypeIndexByType(Type type)
	{
		if (_typeTable == null)
		{
			InitTypeTables();
		}
		for (int i = 0; i < _typeTable.Length; i++)
		{
			if (type == _typeTable[i])
			{
				return i;
			}
		}
		return -1;
	}

	private object Activate_0_XamlControlsResources()
	{
		return new XamlControlsResources();
	}

	private object Activate_4_BoolToVisibilityConverter()
	{
		return new BoolToVisibilityConverter();
	}

	private object Activate_5_ObjectToBoolConverter()
	{
		return new ObjectToBoolConverter();
	}

	private object Activate_6_ObjectToVisibilityConverter()
	{
		return new ObjectToVisibilityConverter();
	}

	private object Activate_7_BoolToIconVariantConverter()
	{
		return new BoolToIconVariantConverter();
	}

	private object Activate_8_ProgressRing()
	{
		return new ProgressRing();
	}

	private object Activate_13_InfoBar()
	{
		return new InfoBar();
	}

	private object Activate_23_InfoBarTemplateSettings()
	{
		return new InfoBarTemplateSettings();
	}

	private object Activate_24_Expander()
	{
		return new Expander();
	}

	private object Activate_37_SecondsToTimeTextConverter()
	{
		return new SecondsToTimeTextConverter();
	}

	private object Activate_39_SymbolIcon()
	{
		return new FluentIcons.WinUI.SymbolIcon();
	}

	private object Activate_44_ProgressBar()
	{
		return new ProgressBar();
	}

	private object Activate_52_AppTitleBar()
	{
		return new AppTitleBar();
	}

	private object Activate_55_AppTitleBarTemplateSettings()
	{
		return new AppTitleBarTemplateSettings();
	}

	private object Activate_56_TrimTextBlock()
	{
		return new TrimTextBlock();
	}

	private object Activate_58_RootLayout()
	{
		return new RootLayout();
	}

	private object Activate_60_NumberBox()
	{
		return new NumberBox();
	}

	private object Activate_69_WindowEx()
	{
		return new WindowEx();
	}

	private object Activate_77_MainWindow()
	{
		return new MainWindow();
	}

	private object Activate_83_LocalVideoPage()
	{
		return new LocalVideoPage();
	}

	private object Activate_84_TreeViewNode()
	{
		return new TreeViewNode();
	}

	private void StaticInitializer_0_XamlControlsResources()
	{
		RuntimeHelpers.RunClassConstructor(typeof(XamlControlsResources).TypeHandle);
	}

	private void StaticInitializer_4_BoolToVisibilityConverter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(BoolToVisibilityConverter).TypeHandle);
	}

	private void StaticInitializer_5_ObjectToBoolConverter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ObjectToBoolConverter).TypeHandle);
	}

	private void StaticInitializer_6_ObjectToVisibilityConverter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ObjectToVisibilityConverter).TypeHandle);
	}

	private void StaticInitializer_7_BoolToIconVariantConverter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(BoolToIconVariantConverter).TypeHandle);
	}

	private void StaticInitializer_8_ProgressRing()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ProgressRing).TypeHandle);
	}

	private void StaticInitializer_11_ProgressRingTemplateSettings()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ProgressRingTemplateSettings).TypeHandle);
	}

	private void StaticInitializer_13_InfoBar()
	{
		RuntimeHelpers.RunClassConstructor(typeof(InfoBar).TypeHandle);
	}

	private void StaticInitializer_14_InfoBarSeverity()
	{
		RuntimeHelpers.RunClassConstructor(typeof(InfoBarSeverity).TypeHandle);
	}

	private void StaticInitializer_15_Enum()
	{
		RuntimeHelpers.RunClassConstructor(typeof(Enum).TypeHandle);
	}

	private void StaticInitializer_16_ValueType()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ValueType).TypeHandle);
	}

	private void StaticInitializer_18_ICommand()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ICommand).TypeHandle);
	}

	private void StaticInitializer_23_InfoBarTemplateSettings()
	{
		RuntimeHelpers.RunClassConstructor(typeof(InfoBarTemplateSettings).TypeHandle);
	}

	private void StaticInitializer_24_Expander()
	{
		RuntimeHelpers.RunClassConstructor(typeof(Expander).TypeHandle);
	}

	private void StaticInitializer_26_ExpandDirection()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ExpandDirection).TypeHandle);
	}

	private void StaticInitializer_28_ExpanderTemplateSettings()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ExpanderTemplateSettings).TypeHandle);
	}

	private void StaticInitializer_29_DanmakuSearchDialog()
	{
		RuntimeHelpers.RunClassConstructor(typeof(DanmakuSearchDialog).TypeHandle);
	}

	private void StaticInitializer_31_DanmakuSettingsDialog()
	{
		RuntimeHelpers.RunClassConstructor(typeof(DanmakuSettingsDialog).TypeHandle);
	}

	private void StaticInitializer_32_PlayerOverlayBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(PlayerOverlayBase).TypeHandle);
	}

	private void StaticInitializer_34_PlayerViewModel()
	{
		RuntimeHelpers.RunClassConstructor(typeof(PlayerViewModel).TypeHandle);
	}

	private void StaticInitializer_35_ViewModelBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ViewModelBase).TypeHandle);
	}

	private void StaticInitializer_36_ObservableObject()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ObservableObject).TypeHandle);
	}

	private void StaticInitializer_37_SecondsToTimeTextConverter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(SecondsToTimeTextConverter).TypeHandle);
	}

	private void StaticInitializer_39_SymbolIcon()
	{
		RuntimeHelpers.RunClassConstructor(typeof(FluentIcons.WinUI.SymbolIcon).TypeHandle);
	}

	private void StaticInitializer_40_GenericIcon()
	{
		RuntimeHelpers.RunClassConstructor(typeof(GenericIcon).TypeHandle);
	}

	private void StaticInitializer_42_Symbol()
	{
		RuntimeHelpers.RunClassConstructor(typeof(FluentIcons.Common.Symbol).TypeHandle);
	}

	private void StaticInitializer_43_IconVariant()
	{
		RuntimeHelpers.RunClassConstructor(typeof(IconVariant).TypeHandle);
	}

	private void StaticInitializer_44_ProgressBar()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ProgressBar).TypeHandle);
	}

	private void StaticInitializer_46_ProgressBarTemplateSettings()
	{
		RuntimeHelpers.RunClassConstructor(typeof(ProgressBarTemplateSettings).TypeHandle);
	}

	private void StaticInitializer_47_PlayerOverlay()
	{
		RuntimeHelpers.RunClassConstructor(typeof(PlayerOverlay).TypeHandle);
	}

	private void StaticInitializer_48_RootLayoutBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(RootLayoutBase).TypeHandle);
	}

	private void StaticInitializer_49_LayoutUserControlBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LayoutUserControlBase<AppViewModel>).TypeHandle);
	}

	private void StaticInitializer_50_LayoutUserControlBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LayoutUserControlBase).TypeHandle);
	}

	private void StaticInitializer_51_AppViewModel()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppViewModel).TypeHandle);
	}

	private void StaticInitializer_52_AppTitleBar()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppTitleBar).TypeHandle);
	}

	private void StaticInitializer_53_LayoutControlBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LayoutControlBase).TypeHandle);
	}

	private void StaticInitializer_55_AppTitleBarTemplateSettings()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppTitleBarTemplateSettings).TypeHandle);
	}

	private void StaticInitializer_56_TrimTextBlock()
	{
		RuntimeHelpers.RunClassConstructor(typeof(TrimTextBlock).TypeHandle);
	}

	private void StaticInitializer_58_RootLayout()
	{
		RuntimeHelpers.RunClassConstructor(typeof(RootLayout).TypeHandle);
	}

	private void StaticInitializer_59_SubtitleSearchDialog()
	{
		RuntimeHelpers.RunClassConstructor(typeof(SubtitleSearchDialog).TypeHandle);
	}

	private void StaticInitializer_60_NumberBox()
	{
		RuntimeHelpers.RunClassConstructor(typeof(NumberBox).TypeHandle);
	}

	private void StaticInitializer_61_NumberBoxSpinButtonPlacementMode()
	{
		RuntimeHelpers.RunClassConstructor(typeof(NumberBoxSpinButtonPlacementMode).TypeHandle);
	}

	private void StaticInitializer_62_INumberFormatter2()
	{
		RuntimeHelpers.RunClassConstructor(typeof(INumberFormatter2).TypeHandle);
	}

	private void StaticInitializer_66_NumberBoxValidationMode()
	{
		RuntimeHelpers.RunClassConstructor(typeof(NumberBoxValidationMode).TypeHandle);
	}

	private void StaticInitializer_67_SubtitleSyncDialog()
	{
		RuntimeHelpers.RunClassConstructor(typeof(SubtitleSyncDialog).TypeHandle);
	}

	private void StaticInitializer_68_WindowBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(WindowBase).TypeHandle);
	}

	private void StaticInitializer_69_WindowEx()
	{
		RuntimeHelpers.RunClassConstructor(typeof(WindowEx).TypeHandle);
	}

	private void StaticInitializer_71_AppWindow()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppWindow).TypeHandle);
	}

	private void StaticInitializer_72_Icon()
	{
		RuntimeHelpers.RunClassConstructor(typeof(WinUIEx.Icon).TypeHandle);
	}

	private void StaticInitializer_73_WindowState()
	{
		RuntimeHelpers.RunClassConstructor(typeof(WindowState).TypeHandle);
	}

	private void StaticInitializer_74_AppWindowPresenter()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppWindowPresenter).TypeHandle);
	}

	private void StaticInitializer_75_AppWindowPresenterKind()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AppWindowPresenterKind).TypeHandle);
	}

	private void StaticInitializer_76_SystemBackdrop()
	{
		RuntimeHelpers.RunClassConstructor(typeof(WinUIEx.SystemBackdrop).TypeHandle);
	}

	private void StaticInitializer_77_MainWindow()
	{
		RuntimeHelpers.RunClassConstructor(typeof(MainWindow).TypeHandle);
	}

	private void StaticInitializer_78_LocalVideoPageBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LocalVideoPageBase).TypeHandle);
	}

	private void StaticInitializer_79_LayoutPageBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LayoutPageBase<LocalVideoPageViewModel>).TypeHandle);
	}

	private void StaticInitializer_80_LayoutPageBase()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LayoutPageBase).TypeHandle);
	}

	private void StaticInitializer_82_LocalVideoPageViewModel()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LocalVideoPageViewModel).TypeHandle);
	}

	private void StaticInitializer_83_LocalVideoPage()
	{
		RuntimeHelpers.RunClassConstructor(typeof(LocalVideoPage).TypeHandle);
	}

	private void StaticInitializer_84_TreeViewNode()
	{
		RuntimeHelpers.RunClassConstructor(typeof(TreeViewNode).TypeHandle);
	}

	private void StaticInitializer_85_IList()
	{
		RuntimeHelpers.RunClassConstructor(typeof(IList<TreeViewNode>).TypeHandle);
	}

	private void MapAdd_0_XamlControlsResources(object instance, object key, object item)
	{
		((IDictionary<object, object>)instance).Add(key, item);
	}

	private void VectorAdd_85_IList(object instance, object item)
	{
		ICollection<TreeViewNode> obj = (ICollection<TreeViewNode>)instance;
		TreeViewNode item2 = (TreeViewNode)item;
		obj.Add(item2);
	}

	private IXamlType CreateXamlType(int typeIndex)
	{
		XamlSystemBaseType result = null;
		string fullName = _typeNameTable[typeIndex];
		Type type = _typeTable[typeIndex];
		switch (typeIndex)
		{
			case 0:
				{
					XamlUserType xamlUserType13 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.ResourceDictionary"));
					xamlUserType13.Activator = Activate_0_XamlControlsResources;
					xamlUserType13.StaticInitializer = StaticInitializer_0_XamlControlsResources;
					xamlUserType13.DictionaryAdd = MapAdd_0_XamlControlsResources;
					xamlUserType13.AddMemberName("UseCompactResources");
					result = xamlUserType13;
					break;
				}
			case 1:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 2:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 3:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 4:
				{
					XamlUserType xamlUserType52 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType52.Activator = Activate_4_BoolToVisibilityConverter;
					xamlUserType52.StaticInitializer = StaticInitializer_4_BoolToVisibilityConverter;
					xamlUserType52.AddMemberName("IsReverse");
					result = xamlUserType52;
					break;
				}
			case 5:
				{
					XamlUserType xamlUserType51 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType51.Activator = Activate_5_ObjectToBoolConverter;
					xamlUserType51.StaticInitializer = StaticInitializer_5_ObjectToBoolConverter;
					xamlUserType51.AddMemberName("IsReverse");
					result = xamlUserType51;
					break;
				}
			case 6:
				{
					XamlUserType xamlUserType50 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType50.Activator = Activate_6_ObjectToVisibilityConverter;
					xamlUserType50.StaticInitializer = StaticInitializer_6_ObjectToVisibilityConverter;
					xamlUserType50.AddMemberName("IsReverse");
					result = xamlUserType50;
					break;
				}
			case 7:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"))
				{
					Activator = Activate_7_BoolToIconVariantConverter,
					StaticInitializer = StaticInitializer_7_BoolToIconVariantConverter
				};
				break;
			case 8:
				{
					XamlUserType xamlUserType49 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
					xamlUserType49.Activator = Activate_8_ProgressRing;
					xamlUserType49.StaticInitializer = StaticInitializer_8_ProgressRing;
					xamlUserType49.AddMemberName("IsActive");
					xamlUserType49.AddMemberName("IsIndeterminate");
					xamlUserType49.AddMemberName("Maximum");
					xamlUserType49.AddMemberName("Minimum");
					xamlUserType49.AddMemberName("TemplateSettings");
					xamlUserType49.AddMemberName("Value");
					result = xamlUserType49;
					break;
				}
			case 9:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 10:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 11:
				{
					XamlUserType xamlUserType48 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType48.StaticInitializer = StaticInitializer_11_ProgressRingTemplateSettings;
					xamlUserType48.SetIsReturnTypeStub();
					result = xamlUserType48;
					break;
				}
			case 12:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 13:
				{
					XamlUserType xamlUserType47 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
					xamlUserType47.Activator = Activate_13_InfoBar;
					xamlUserType47.StaticInitializer = StaticInitializer_13_InfoBar;
					xamlUserType47.SetContentPropertyName("Microsoft.UI.Xaml.Controls.InfoBar.Content");
					xamlUserType47.AddMemberName("Content");
					xamlUserType47.AddMemberName("IsOpen");
					xamlUserType47.AddMemberName("IsClosable");
					xamlUserType47.AddMemberName("Severity");
					xamlUserType47.AddMemberName("ActionButton");
					xamlUserType47.AddMemberName("CloseButtonCommand");
					xamlUserType47.AddMemberName("CloseButtonCommandParameter");
					xamlUserType47.AddMemberName("CloseButtonStyle");
					xamlUserType47.AddMemberName("ContentTemplate");
					xamlUserType47.AddMemberName("IconSource");
					xamlUserType47.AddMemberName("IsIconVisible");
					xamlUserType47.AddMemberName("Message");
					xamlUserType47.AddMemberName("TemplateSettings");
					xamlUserType47.AddMemberName("Title");
					result = xamlUserType47;
					break;
				}
			case 14:
				{
					XamlUserType xamlUserType46 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType46.StaticInitializer = StaticInitializer_14_InfoBarSeverity;
					xamlUserType46.AddEnumValue("Informational", InfoBarSeverity.Informational);
					xamlUserType46.AddEnumValue("Success", InfoBarSeverity.Success);
					xamlUserType46.AddEnumValue("Warning", InfoBarSeverity.Warning);
					xamlUserType46.AddEnumValue("Error", InfoBarSeverity.Error);
					result = xamlUserType46;
					break;
				}
			case 15:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"))
				{
					StaticInitializer = StaticInitializer_15_Enum
				};
				break;
			case 16:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"))
				{
					StaticInitializer = StaticInitializer_16_ValueType
				};
				break;
			case 17:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 18:
				{
					XamlUserType xamlUserType45 = new XamlUserType(this, fullName, type, null);
					xamlUserType45.StaticInitializer = StaticInitializer_18_ICommand;
					xamlUserType45.SetIsReturnTypeStub();
					result = xamlUserType45;
					break;
				}
			case 19:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 20:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 21:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 22:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 23:
				{
					XamlUserType xamlUserType44 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType44.StaticInitializer = StaticInitializer_23_InfoBarTemplateSettings;
					xamlUserType44.SetIsReturnTypeStub();
					result = xamlUserType44;
					break;
				}
			case 24:
				{
					XamlUserType xamlUserType43 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentControl"));
					xamlUserType43.Activator = Activate_24_Expander;
					xamlUserType43.StaticInitializer = StaticInitializer_24_Expander;
					xamlUserType43.AddMemberName("IsExpanded");
					xamlUserType43.AddMemberName("Header");
					xamlUserType43.AddMemberName("ExpandDirection");
					xamlUserType43.AddMemberName("HeaderTemplate");
					xamlUserType43.AddMemberName("HeaderTemplateSelector");
					xamlUserType43.AddMemberName("TemplateSettings");
					result = xamlUserType43;
					break;
				}
			case 25:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 26:
				{
					XamlUserType xamlUserType42 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType42.StaticInitializer = StaticInitializer_26_ExpandDirection;
					xamlUserType42.AddEnumValue("Down", ExpandDirection.Down);
					xamlUserType42.AddEnumValue("Up", ExpandDirection.Up);
					result = xamlUserType42;
					break;
				}
			case 27:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 28:
				{
					XamlUserType xamlUserType41 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType41.StaticInitializer = StaticInitializer_28_ExpanderTemplateSettings;
					xamlUserType41.SetIsReturnTypeStub();
					result = xamlUserType41;
					break;
				}
			case 29:
				{
					XamlUserType xamlUserType40 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentDialog"));
					xamlUserType40.StaticInitializer = StaticInitializer_29_DanmakuSearchDialog;
					xamlUserType40.SetIsLocalType();
					result = xamlUserType40;
					break;
				}
			case 30:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 31:
				{
					XamlUserType xamlUserType39 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentDialog"));
					xamlUserType39.StaticInitializer = StaticInitializer_31_DanmakuSettingsDialog;
					xamlUserType39.SetIsLocalType();
					result = xamlUserType39;
					break;
				}
			case 32:
				{
					XamlUserType xamlUserType38 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.UserControl"));
					xamlUserType38.StaticInitializer = StaticInitializer_32_PlayerOverlayBase;
					xamlUserType38.AddMemberName("ViewModel");
					xamlUserType38.SetIsLocalType();
					result = xamlUserType38;
					break;
				}
			case 33:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 34:
				{
					XamlUserType xamlUserType37 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.ViewModels.ViewModelBase"));
					xamlUserType37.StaticInitializer = StaticInitializer_34_PlayerViewModel;
					xamlUserType37.SetIsReturnTypeStub();
					xamlUserType37.SetIsLocalType();
					result = xamlUserType37;
					break;
				}
			case 35:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.Mvvm.ComponentModel.ObservableObject"))
				{
					StaticInitializer = StaticInitializer_35_ViewModelBase
				};
				break;
			case 36:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"))
				{
					StaticInitializer = StaticInitializer_36_ObservableObject
				};
				break;
			case 37:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"))
				{
					Activator = Activate_37_SecondsToTimeTextConverter,
					StaticInitializer = StaticInitializer_37_SecondsToTimeTextConverter
				};
				break;
			case 38:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 39:
				{
					XamlUserType xamlUserType36 = new XamlUserType(this, fullName, type, GetXamlTypeByName("FluentIcons.WinUI.Internals.GenericIcon"));
					xamlUserType36.Activator = Activate_39_SymbolIcon;
					xamlUserType36.StaticInitializer = StaticInitializer_39_SymbolIcon;
					xamlUserType36.AddMemberName("Symbol");
					xamlUserType36.AddMemberName("UseSegoeMetrics");
					result = xamlUserType36;
					break;
				}
			case 40:
				{
					XamlUserType xamlUserType35 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.FontIcon"));
					xamlUserType35.StaticInitializer = StaticInitializer_40_GenericIcon;
					xamlUserType35.AddMemberName("IconVariant");
					result = xamlUserType35;
					break;
				}
			case 41:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 42:
				{
					XamlUserType xamlUserType34 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType34.StaticInitializer = StaticInitializer_42_Symbol;
					xamlUserType34.AddEnumValue("AccessTime", FluentIcons.Common.Symbol.AccessTime);
					xamlUserType34.AddEnumValue("Accessibility", FluentIcons.Common.Symbol.Accessibility);
					xamlUserType34.AddEnumValue("AccessibilityCheckmark", FluentIcons.Common.Symbol.AccessibilityCheckmark);
					xamlUserType34.AddEnumValue("AccessibilityError", FluentIcons.Common.Symbol.AccessibilityError);
					xamlUserType34.AddEnumValue("AccessibilityMore", FluentIcons.Common.Symbol.AccessibilityMore);
					xamlUserType34.AddEnumValue("AccessibilityQuestionMark", FluentIcons.Common.Symbol.AccessibilityQuestionMark);
					xamlUserType34.AddEnumValue("Add", FluentIcons.Common.Symbol.Add);
					xamlUserType34.AddEnumValue("AddCircle", FluentIcons.Common.Symbol.AddCircle);
					xamlUserType34.AddEnumValue("AddSquare", FluentIcons.Common.Symbol.AddSquare);
					xamlUserType34.AddEnumValue("AddSquareMultiple", FluentIcons.Common.Symbol.AddSquareMultiple);
					xamlUserType34.AddEnumValue("AddSubtractCircle", FluentIcons.Common.Symbol.AddSubtractCircle);
					xamlUserType34.AddEnumValue("Agents", FluentIcons.Common.Symbol.Agents);
					xamlUserType34.AddEnumValue("Airplane", FluentIcons.Common.Symbol.Airplane);
					xamlUserType34.AddEnumValue("AirplaneLanding", FluentIcons.Common.Symbol.AirplaneLanding);
					xamlUserType34.AddEnumValue("AirplaneTakeOff", FluentIcons.Common.Symbol.AirplaneTakeOff);
					xamlUserType34.AddEnumValue("Album", FluentIcons.Common.Symbol.Album);
					xamlUserType34.AddEnumValue("AlbumAdd", FluentIcons.Common.Symbol.AlbumAdd);
					xamlUserType34.AddEnumValue("Alert", FluentIcons.Common.Symbol.Alert);
					xamlUserType34.AddEnumValue("AlertBadge", FluentIcons.Common.Symbol.AlertBadge);
					xamlUserType34.AddEnumValue("AlertOff", FluentIcons.Common.Symbol.AlertOff);
					xamlUserType34.AddEnumValue("AlertOn", FluentIcons.Common.Symbol.AlertOn);
					xamlUserType34.AddEnumValue("AlertSnooze", FluentIcons.Common.Symbol.AlertSnooze);
					xamlUserType34.AddEnumValue("AlertUrgent", FluentIcons.Common.Symbol.AlertUrgent);
					xamlUserType34.AddEnumValue("AlignBottom", FluentIcons.Common.Symbol.AlignBottom);
					xamlUserType34.AddEnumValue("AlignCenterHorizontal", FluentIcons.Common.Symbol.AlignCenterHorizontal);
					xamlUserType34.AddEnumValue("AlignCenterVertical", FluentIcons.Common.Symbol.AlignCenterVertical);
					xamlUserType34.AddEnumValue("AlignEndHorizontal", FluentIcons.Common.Symbol.AlignEndHorizontal);
					xamlUserType34.AddEnumValue("AlignEndVertical", FluentIcons.Common.Symbol.AlignEndVertical);
					xamlUserType34.AddEnumValue("AlignLeft", FluentIcons.Common.Symbol.AlignLeft);
					xamlUserType34.AddEnumValue("AlignRight", FluentIcons.Common.Symbol.AlignRight);
					xamlUserType34.AddEnumValue("AlignSpaceAroundHorizontal", FluentIcons.Common.Symbol.AlignSpaceAroundHorizontal);
					xamlUserType34.AddEnumValue("AlignSpaceAroundVertical", FluentIcons.Common.Symbol.AlignSpaceAroundVertical);
					xamlUserType34.AddEnumValue("AlignSpaceBetweenHorizontal", FluentIcons.Common.Symbol.AlignSpaceBetweenHorizontal);
					xamlUserType34.AddEnumValue("AlignSpaceBetweenVertical", FluentIcons.Common.Symbol.AlignSpaceBetweenVertical);
					xamlUserType34.AddEnumValue("AlignSpaceEvenlyHorizontal", FluentIcons.Common.Symbol.AlignSpaceEvenlyHorizontal);
					xamlUserType34.AddEnumValue("AlignSpaceEvenlyVertical", FluentIcons.Common.Symbol.AlignSpaceEvenlyVertical);
					xamlUserType34.AddEnumValue("AlignSpaceFitVertical", FluentIcons.Common.Symbol.AlignSpaceFitVertical);
					xamlUserType34.AddEnumValue("AlignStartHorizontal", FluentIcons.Common.Symbol.AlignStartHorizontal);
					xamlUserType34.AddEnumValue("AlignStartVertical", FluentIcons.Common.Symbol.AlignStartVertical);
					xamlUserType34.AddEnumValue("AlignStraighten", FluentIcons.Common.Symbol.AlignStraighten);
					xamlUserType34.AddEnumValue("AlignStretchHorizontal", FluentIcons.Common.Symbol.AlignStretchHorizontal);
					xamlUserType34.AddEnumValue("AlignStretchVertical", FluentIcons.Common.Symbol.AlignStretchVertical);
					xamlUserType34.AddEnumValue("AlignTop", FluentIcons.Common.Symbol.AlignTop);
					xamlUserType34.AddEnumValue("AnimalCat", FluentIcons.Common.Symbol.AnimalCat);
					xamlUserType34.AddEnumValue("AnimalDog", FluentIcons.Common.Symbol.AnimalDog);
					xamlUserType34.AddEnumValue("AnimalPawPrint", FluentIcons.Common.Symbol.AnimalPawPrint);
					xamlUserType34.AddEnumValue("AnimalRabbit", FluentIcons.Common.Symbol.AnimalRabbit);
					xamlUserType34.AddEnumValue("AnimalRabbitOff", FluentIcons.Common.Symbol.AnimalRabbitOff);
					xamlUserType34.AddEnumValue("AnimalTurtle", FluentIcons.Common.Symbol.AnimalTurtle);
					xamlUserType34.AddEnumValue("AppFolder", FluentIcons.Common.Symbol.AppFolder);
					xamlUserType34.AddEnumValue("AppGeneric", FluentIcons.Common.Symbol.AppGeneric);
					xamlUserType34.AddEnumValue("AppRecent", FluentIcons.Common.Symbol.AppRecent);
					xamlUserType34.AddEnumValue("AppTitle", FluentIcons.Common.Symbol.AppTitle);
					xamlUserType34.AddEnumValue("ApprovalsApp", FluentIcons.Common.Symbol.ApprovalsApp);
					xamlUserType34.AddEnumValue("Apps", FluentIcons.Common.Symbol.Apps);
					xamlUserType34.AddEnumValue("AppsAddIn", FluentIcons.Common.Symbol.AppsAddIn);
					xamlUserType34.AddEnumValue("AppsAddInOff", FluentIcons.Common.Symbol.AppsAddInOff);
					xamlUserType34.AddEnumValue("AppsList", FluentIcons.Common.Symbol.AppsList);
					xamlUserType34.AddEnumValue("AppsListDetail", FluentIcons.Common.Symbol.AppsListDetail);
					xamlUserType34.AddEnumValue("AppsSettings", FluentIcons.Common.Symbol.AppsSettings);
					xamlUserType34.AddEnumValue("AppsShield", FluentIcons.Common.Symbol.AppsShield);
					xamlUserType34.AddEnumValue("Archive", FluentIcons.Common.Symbol.Archive);
					xamlUserType34.AddEnumValue("ArchiveArrowBack", FluentIcons.Common.Symbol.ArchiveArrowBack);
					xamlUserType34.AddEnumValue("ArchiveMultiple", FluentIcons.Common.Symbol.ArchiveMultiple);
					xamlUserType34.AddEnumValue("ArchiveSettings", FluentIcons.Common.Symbol.ArchiveSettings);
					xamlUserType34.AddEnumValue("ArrowAutofitContent", FluentIcons.Common.Symbol.ArrowAutofitContent);
					xamlUserType34.AddEnumValue("ArrowAutofitDown", FluentIcons.Common.Symbol.ArrowAutofitDown);
					xamlUserType34.AddEnumValue("ArrowAutofitHeight", FluentIcons.Common.Symbol.ArrowAutofitHeight);
					xamlUserType34.AddEnumValue("ArrowAutofitHeightDotted", FluentIcons.Common.Symbol.ArrowAutofitHeightDotted);
					xamlUserType34.AddEnumValue("ArrowAutofitHeightIn", FluentIcons.Common.Symbol.ArrowAutofitHeightIn);
					xamlUserType34.AddEnumValue("ArrowAutofitUp", FluentIcons.Common.Symbol.ArrowAutofitUp);
					xamlUserType34.AddEnumValue("ArrowAutofitWidth", FluentIcons.Common.Symbol.ArrowAutofitWidth);
					xamlUserType34.AddEnumValue("ArrowAutofitWidthDotted", FluentIcons.Common.Symbol.ArrowAutofitWidthDotted);
					xamlUserType34.AddEnumValue("ArrowBetweenDown", FluentIcons.Common.Symbol.ArrowBetweenDown);
					xamlUserType34.AddEnumValue("ArrowBetweenUp", FluentIcons.Common.Symbol.ArrowBetweenUp);
					xamlUserType34.AddEnumValue("ArrowBidirectionalLeftRight", FluentIcons.Common.Symbol.ArrowBidirectionalLeftRight);
					xamlUserType34.AddEnumValue("ArrowBidirectionalUpDown", FluentIcons.Common.Symbol.ArrowBidirectionalUpDown);
					xamlUserType34.AddEnumValue("ArrowBounce", FluentIcons.Common.Symbol.ArrowBounce);
					xamlUserType34.AddEnumValue("ArrowCircleDown", FluentIcons.Common.Symbol.ArrowCircleDown);
					xamlUserType34.AddEnumValue("ArrowCircleDownDouble", FluentIcons.Common.Symbol.ArrowCircleDownDouble);
					xamlUserType34.AddEnumValue("ArrowCircleDownRight", FluentIcons.Common.Symbol.ArrowCircleDownRight);
					xamlUserType34.AddEnumValue("ArrowCircleDownSplit", FluentIcons.Common.Symbol.ArrowCircleDownSplit);
					xamlUserType34.AddEnumValue("ArrowCircleDownUp", FluentIcons.Common.Symbol.ArrowCircleDownUp);
					xamlUserType34.AddEnumValue("ArrowCircleLeft", FluentIcons.Common.Symbol.ArrowCircleLeft);
					xamlUserType34.AddEnumValue("ArrowCircleRight", FluentIcons.Common.Symbol.ArrowCircleRight);
					xamlUserType34.AddEnumValue("ArrowCircleUp", FluentIcons.Common.Symbol.ArrowCircleUp);
					xamlUserType34.AddEnumValue("ArrowCircleUpLeft", FluentIcons.Common.Symbol.ArrowCircleUpLeft);
					xamlUserType34.AddEnumValue("ArrowCircleUpRight", FluentIcons.Common.Symbol.ArrowCircleUpRight);
					xamlUserType34.AddEnumValue("ArrowCircleUpSparkle", FluentIcons.Common.Symbol.ArrowCircleUpSparkle);
					xamlUserType34.AddEnumValue("ArrowClockwise", FluentIcons.Common.Symbol.ArrowClockwise);
					xamlUserType34.AddEnumValue("ArrowClockwiseDashes", FluentIcons.Common.Symbol.ArrowClockwiseDashes);
					xamlUserType34.AddEnumValue("ArrowClockwiseDashesSettings", FluentIcons.Common.Symbol.ArrowClockwiseDashesSettings);
					xamlUserType34.AddEnumValue("ArrowCollapseAll", FluentIcons.Common.Symbol.ArrowCollapseAll);
					xamlUserType34.AddEnumValue("ArrowCounterclockwise", FluentIcons.Common.Symbol.ArrowCounterclockwise);
					xamlUserType34.AddEnumValue("ArrowCounterclockwiseDashes", FluentIcons.Common.Symbol.ArrowCounterclockwiseDashes);
					xamlUserType34.AddEnumValue("ArrowCounterclockwiseInfo", FluentIcons.Common.Symbol.ArrowCounterclockwiseInfo);
					xamlUserType34.AddEnumValue("ArrowCurveDownLeft", FluentIcons.Common.Symbol.ArrowCurveDownLeft);
					xamlUserType34.AddEnumValue("ArrowCurveDownRight", FluentIcons.Common.Symbol.ArrowCurveDownRight);
					xamlUserType34.AddEnumValue("ArrowCurveUpLeft", FluentIcons.Common.Symbol.ArrowCurveUpLeft);
					xamlUserType34.AddEnumValue("ArrowCurveUpRight", FluentIcons.Common.Symbol.ArrowCurveUpRight);
					xamlUserType34.AddEnumValue("ArrowDown", FluentIcons.Common.Symbol.ArrowDown);
					xamlUserType34.AddEnumValue("ArrowDownExclamation", FluentIcons.Common.Symbol.ArrowDownExclamation);
					xamlUserType34.AddEnumValue("ArrowDownLeft", FluentIcons.Common.Symbol.ArrowDownLeft);
					xamlUserType34.AddEnumValue("ArrowDownRight", FluentIcons.Common.Symbol.ArrowDownRight);
					xamlUserType34.AddEnumValue("ArrowDownload", FluentIcons.Common.Symbol.ArrowDownload);
					xamlUserType34.AddEnumValue("ArrowDownloadOff", FluentIcons.Common.Symbol.ArrowDownloadOff);
					xamlUserType34.AddEnumValue("ArrowEject", FluentIcons.Common.Symbol.ArrowEject);
					xamlUserType34.AddEnumValue("ArrowEnter", FluentIcons.Common.Symbol.ArrowEnter);
					xamlUserType34.AddEnumValue("ArrowEnterLeft", FluentIcons.Common.Symbol.ArrowEnterLeft);
					xamlUserType34.AddEnumValue("ArrowEnterUp", FluentIcons.Common.Symbol.ArrowEnterUp);
					xamlUserType34.AddEnumValue("ArrowExit", FluentIcons.Common.Symbol.ArrowExit);
					xamlUserType34.AddEnumValue("ArrowExpand", FluentIcons.Common.Symbol.ArrowExpand);
					xamlUserType34.AddEnumValue("ArrowExpandAll", FluentIcons.Common.Symbol.ArrowExpandAll);
					xamlUserType34.AddEnumValue("ArrowExport", FluentIcons.Common.Symbol.ArrowExport);
					xamlUserType34.AddEnumValue("ArrowExportUp", FluentIcons.Common.Symbol.ArrowExportUp);
					xamlUserType34.AddEnumValue("ArrowFit", FluentIcons.Common.Symbol.ArrowFit);
					xamlUserType34.AddEnumValue("ArrowFitIn", FluentIcons.Common.Symbol.ArrowFitIn);
					xamlUserType34.AddEnumValue("ArrowFlowDiagonalUpRight", FluentIcons.Common.Symbol.ArrowFlowDiagonalUpRight);
					xamlUserType34.AddEnumValue("ArrowFlowUpRight", FluentIcons.Common.Symbol.ArrowFlowUpRight);
					xamlUserType34.AddEnumValue("ArrowFlowUpRightRectangleMultiple", FluentIcons.Common.Symbol.ArrowFlowUpRightRectangleMultiple);
					xamlUserType34.AddEnumValue("ArrowForward", FluentIcons.Common.Symbol.ArrowForward);
					xamlUserType34.AddEnumValue("ArrowForwardDownLightning", FluentIcons.Common.Symbol.ArrowForwardDownLightning);
					xamlUserType34.AddEnumValue("ArrowForwardDownPerson", FluentIcons.Common.Symbol.ArrowForwardDownPerson);
					xamlUserType34.AddEnumValue("ArrowHookDownLeft", FluentIcons.Common.Symbol.ArrowHookDownLeft);
					xamlUserType34.AddEnumValue("ArrowHookDownRight", FluentIcons.Common.Symbol.ArrowHookDownRight);
					xamlUserType34.AddEnumValue("ArrowHookUpLeft", FluentIcons.Common.Symbol.ArrowHookUpLeft);
					xamlUserType34.AddEnumValue("ArrowHookUpRight", FluentIcons.Common.Symbol.ArrowHookUpRight);
					xamlUserType34.AddEnumValue("ArrowImport", FluentIcons.Common.Symbol.ArrowImport);
					xamlUserType34.AddEnumValue("ArrowJoin", FluentIcons.Common.Symbol.ArrowJoin);
					xamlUserType34.AddEnumValue("ArrowLeft", FluentIcons.Common.Symbol.ArrowLeft);
					xamlUserType34.AddEnumValue("ArrowMaximize", FluentIcons.Common.Symbol.ArrowMaximize);
					xamlUserType34.AddEnumValue("ArrowMaximizeVertical", FluentIcons.Common.Symbol.ArrowMaximizeVertical);
					xamlUserType34.AddEnumValue("ArrowMinimize", FluentIcons.Common.Symbol.ArrowMinimize);
					xamlUserType34.AddEnumValue("ArrowMinimizeVertical", FluentIcons.Common.Symbol.ArrowMinimizeVertical);
					xamlUserType34.AddEnumValue("ArrowMove", FluentIcons.Common.Symbol.ArrowMove);
					xamlUserType34.AddEnumValue("ArrowMoveInward", FluentIcons.Common.Symbol.ArrowMoveInward);
					xamlUserType34.AddEnumValue("ArrowNext", FluentIcons.Common.Symbol.ArrowNext);
					xamlUserType34.AddEnumValue("ArrowOutlineDownLeft", FluentIcons.Common.Symbol.ArrowOutlineDownLeft);
					xamlUserType34.AddEnumValue("ArrowOutlineUpRight", FluentIcons.Common.Symbol.ArrowOutlineUpRight);
					xamlUserType34.AddEnumValue("ArrowParagraph", FluentIcons.Common.Symbol.ArrowParagraph);
					xamlUserType34.AddEnumValue("ArrowPrevious", FluentIcons.Common.Symbol.ArrowPrevious);
					xamlUserType34.AddEnumValue("ArrowRedo", FluentIcons.Common.Symbol.ArrowRedo);
					xamlUserType34.AddEnumValue("ArrowRepeat1", FluentIcons.Common.Symbol.ArrowRepeat1);
					xamlUserType34.AddEnumValue("ArrowRepeatAll", FluentIcons.Common.Symbol.ArrowRepeatAll);
					xamlUserType34.AddEnumValue("ArrowRepeatAllOff", FluentIcons.Common.Symbol.ArrowRepeatAllOff);
					xamlUserType34.AddEnumValue("ArrowReply", FluentIcons.Common.Symbol.ArrowReply);
					xamlUserType34.AddEnumValue("ArrowReplyAll", FluentIcons.Common.Symbol.ArrowReplyAll);
					xamlUserType34.AddEnumValue("ArrowReplyDown", FluentIcons.Common.Symbol.ArrowReplyDown);
					xamlUserType34.AddEnumValue("ArrowReset", FluentIcons.Common.Symbol.ArrowReset);
					xamlUserType34.AddEnumValue("ArrowRight", FluentIcons.Common.Symbol.ArrowRight);
					xamlUserType34.AddEnumValue("ArrowRotateClockwise", FluentIcons.Common.Symbol.ArrowRotateClockwise);
					xamlUserType34.AddEnumValue("ArrowRotateCounterclockwise", FluentIcons.Common.Symbol.ArrowRotateCounterclockwise);
					xamlUserType34.AddEnumValue("ArrowRouting", FluentIcons.Common.Symbol.ArrowRouting);
					xamlUserType34.AddEnumValue("ArrowRoutingRectangleMultiple", FluentIcons.Common.Symbol.ArrowRoutingRectangleMultiple);
					xamlUserType34.AddEnumValue("ArrowShuffle", FluentIcons.Common.Symbol.ArrowShuffle);
					xamlUserType34.AddEnumValue("ArrowShuffleOff", FluentIcons.Common.Symbol.ArrowShuffleOff);
					xamlUserType34.AddEnumValue("ArrowSort", FluentIcons.Common.Symbol.ArrowSort);
					xamlUserType34.AddEnumValue("ArrowSortDown", FluentIcons.Common.Symbol.ArrowSortDown);
					xamlUserType34.AddEnumValue("ArrowSortDownLines", FluentIcons.Common.Symbol.ArrowSortDownLines);
					xamlUserType34.AddEnumValue("ArrowSortUp", FluentIcons.Common.Symbol.ArrowSortUp);
					xamlUserType34.AddEnumValue("ArrowSortUpLines", FluentIcons.Common.Symbol.ArrowSortUpLines);
					xamlUserType34.AddEnumValue("ArrowSplit", FluentIcons.Common.Symbol.ArrowSplit);
					xamlUserType34.AddEnumValue("ArrowSprint", FluentIcons.Common.Symbol.ArrowSprint);
					xamlUserType34.AddEnumValue("ArrowSquareDown", FluentIcons.Common.Symbol.ArrowSquareDown);
					xamlUserType34.AddEnumValue("ArrowSquareUpRight", FluentIcons.Common.Symbol.ArrowSquareUpRight);
					xamlUserType34.AddEnumValue("ArrowStepBack", FluentIcons.Common.Symbol.ArrowStepBack);
					xamlUserType34.AddEnumValue("ArrowStepIn", FluentIcons.Common.Symbol.ArrowStepIn);
					xamlUserType34.AddEnumValue("ArrowStepInDiagonalDownLeft", FluentIcons.Common.Symbol.ArrowStepInDiagonalDownLeft);
					xamlUserType34.AddEnumValue("ArrowStepInLeft", FluentIcons.Common.Symbol.ArrowStepInLeft);
					xamlUserType34.AddEnumValue("ArrowStepInRight", FluentIcons.Common.Symbol.ArrowStepInRight);
					xamlUserType34.AddEnumValue("ArrowStepOut", FluentIcons.Common.Symbol.ArrowStepOut);
					xamlUserType34.AddEnumValue("ArrowStepOver", FluentIcons.Common.Symbol.ArrowStepOver);
					xamlUserType34.AddEnumValue("ArrowSwap", FluentIcons.Common.Symbol.ArrowSwap);
					xamlUserType34.AddEnumValue("ArrowSync", FluentIcons.Common.Symbol.ArrowSync);
					xamlUserType34.AddEnumValue("ArrowSyncCheckmark", FluentIcons.Common.Symbol.ArrowSyncCheckmark);
					xamlUserType34.AddEnumValue("ArrowSyncCircle", FluentIcons.Common.Symbol.ArrowSyncCircle);
					xamlUserType34.AddEnumValue("ArrowSyncDismiss", FluentIcons.Common.Symbol.ArrowSyncDismiss);
					xamlUserType34.AddEnumValue("ArrowSyncOff", FluentIcons.Common.Symbol.ArrowSyncOff);
					xamlUserType34.AddEnumValue("ArrowTrending", FluentIcons.Common.Symbol.ArrowTrending);
					xamlUserType34.AddEnumValue("ArrowTrendingCheckmark", FluentIcons.Common.Symbol.ArrowTrendingCheckmark);
					xamlUserType34.AddEnumValue("ArrowTrendingDown", FluentIcons.Common.Symbol.ArrowTrendingDown);
					xamlUserType34.AddEnumValue("ArrowTrendingLines", FluentIcons.Common.Symbol.ArrowTrendingLines);
					xamlUserType34.AddEnumValue("ArrowTrendingSettings", FluentIcons.Common.Symbol.ArrowTrendingSettings);
					xamlUserType34.AddEnumValue("ArrowTrendingSparkle", FluentIcons.Common.Symbol.ArrowTrendingSparkle);
					xamlUserType34.AddEnumValue("ArrowTrendingText", FluentIcons.Common.Symbol.ArrowTrendingText);
					xamlUserType34.AddEnumValue("ArrowTrendingWrench", FluentIcons.Common.Symbol.ArrowTrendingWrench);
					xamlUserType34.AddEnumValue("ArrowTurnBidirectionalDownRight", FluentIcons.Common.Symbol.ArrowTurnBidirectionalDownRight);
					xamlUserType34.AddEnumValue("ArrowTurnDownLeft", FluentIcons.Common.Symbol.ArrowTurnDownLeft);
					xamlUserType34.AddEnumValue("ArrowTurnDownRight", FluentIcons.Common.Symbol.ArrowTurnDownRight);
					xamlUserType34.AddEnumValue("ArrowTurnDownUp", FluentIcons.Common.Symbol.ArrowTurnDownUp);
					xamlUserType34.AddEnumValue("ArrowTurnLeftDown", FluentIcons.Common.Symbol.ArrowTurnLeftDown);
					xamlUserType34.AddEnumValue("ArrowTurnLeftRight", FluentIcons.Common.Symbol.ArrowTurnLeftRight);
					xamlUserType34.AddEnumValue("ArrowTurnLeftUp", FluentIcons.Common.Symbol.ArrowTurnLeftUp);
					xamlUserType34.AddEnumValue("ArrowTurnRight", FluentIcons.Common.Symbol.ArrowTurnRight);
					xamlUserType34.AddEnumValue("ArrowTurnRightDown", FluentIcons.Common.Symbol.ArrowTurnRightDown);
					xamlUserType34.AddEnumValue("ArrowTurnRightLeft", FluentIcons.Common.Symbol.ArrowTurnRightLeft);
					xamlUserType34.AddEnumValue("ArrowTurnRightUp", FluentIcons.Common.Symbol.ArrowTurnRightUp);
					xamlUserType34.AddEnumValue("ArrowTurnUpDown", FluentIcons.Common.Symbol.ArrowTurnUpDown);
					xamlUserType34.AddEnumValue("ArrowTurnUpLeft", FluentIcons.Common.Symbol.ArrowTurnUpLeft);
					xamlUserType34.AddEnumValue("ArrowUndo", FluentIcons.Common.Symbol.ArrowUndo);
					xamlUserType34.AddEnumValue("ArrowUp", FluentIcons.Common.Symbol.ArrowUp);
					xamlUserType34.AddEnumValue("ArrowUpExclamation", FluentIcons.Common.Symbol.ArrowUpExclamation);
					xamlUserType34.AddEnumValue("ArrowUpLeft", FluentIcons.Common.Symbol.ArrowUpLeft);
					xamlUserType34.AddEnumValue("ArrowUpRight", FluentIcons.Common.Symbol.ArrowUpRight);
					xamlUserType34.AddEnumValue("ArrowUpRightDashes", FluentIcons.Common.Symbol.ArrowUpRightDashes);
					xamlUserType34.AddEnumValue("ArrowUpload", FluentIcons.Common.Symbol.ArrowUpload);
					xamlUserType34.AddEnumValue("ArrowWrap", FluentIcons.Common.Symbol.ArrowWrap);
					xamlUserType34.AddEnumValue("ArrowWrapOff", FluentIcons.Common.Symbol.ArrowWrapOff);
					xamlUserType34.AddEnumValue("ArrowWrapUpToDown", FluentIcons.Common.Symbol.ArrowWrapUpToDown);
					xamlUserType34.AddEnumValue("ArrowsBidirectional", FluentIcons.Common.Symbol.ArrowsBidirectional);
					xamlUserType34.AddEnumValue("Attach", FluentIcons.Common.Symbol.Attach);
					xamlUserType34.AddEnumValue("AttachArrowRight", FluentIcons.Common.Symbol.AttachArrowRight);
					xamlUserType34.AddEnumValue("AttachText", FluentIcons.Common.Symbol.AttachText);
					xamlUserType34.AddEnumValue("AutoFit", FluentIcons.Common.Symbol.AutoFit);
					xamlUserType34.AddEnumValue("AutoFitHeight", FluentIcons.Common.Symbol.AutoFitHeight);
					xamlUserType34.AddEnumValue("AutoFitWidth", FluentIcons.Common.Symbol.AutoFitWidth);
					xamlUserType34.AddEnumValue("Autocorrect", FluentIcons.Common.Symbol.Autocorrect);
					xamlUserType34.AddEnumValue("Autosum", FluentIcons.Common.Symbol.Autosum);
					xamlUserType34.AddEnumValue("Backpack", FluentIcons.Common.Symbol.Backpack);
					xamlUserType34.AddEnumValue("BackpackAdd", FluentIcons.Common.Symbol.BackpackAdd);
					xamlUserType34.AddEnumValue("Backspace", FluentIcons.Common.Symbol.Backspace);
					xamlUserType34.AddEnumValue("Badge", FluentIcons.Common.Symbol.Badge);
					xamlUserType34.AddEnumValue("Balloon", FluentIcons.Common.Symbol.Balloon);
					xamlUserType34.AddEnumValue("BarcodeScanner", FluentIcons.Common.Symbol.BarcodeScanner);
					xamlUserType34.AddEnumValue("Battery0", FluentIcons.Common.Symbol.Battery0);
					xamlUserType34.AddEnumValue("Battery1", FluentIcons.Common.Symbol.Battery1);
					xamlUserType34.AddEnumValue("Battery10", FluentIcons.Common.Symbol.Battery10);
					xamlUserType34.AddEnumValue("Battery2", FluentIcons.Common.Symbol.Battery2);
					xamlUserType34.AddEnumValue("Battery3", FluentIcons.Common.Symbol.Battery3);
					xamlUserType34.AddEnumValue("Battery4", FluentIcons.Common.Symbol.Battery4);
					xamlUserType34.AddEnumValue("Battery5", FluentIcons.Common.Symbol.Battery5);
					xamlUserType34.AddEnumValue("Battery6", FluentIcons.Common.Symbol.Battery6);
					xamlUserType34.AddEnumValue("Battery7", FluentIcons.Common.Symbol.Battery7);
					xamlUserType34.AddEnumValue("Battery8", FluentIcons.Common.Symbol.Battery8);
					xamlUserType34.AddEnumValue("Battery9", FluentIcons.Common.Symbol.Battery9);
					xamlUserType34.AddEnumValue("BatteryCharge", FluentIcons.Common.Symbol.BatteryCharge);
					xamlUserType34.AddEnumValue("BatteryCheckmark", FluentIcons.Common.Symbol.BatteryCheckmark);
					xamlUserType34.AddEnumValue("BatterySaver", FluentIcons.Common.Symbol.BatterySaver);
					xamlUserType34.AddEnumValue("BatteryWarning", FluentIcons.Common.Symbol.BatteryWarning);
					xamlUserType34.AddEnumValue("Beach", FluentIcons.Common.Symbol.Beach);
					xamlUserType34.AddEnumValue("Beaker", FluentIcons.Common.Symbol.Beaker);
					xamlUserType34.AddEnumValue("BeakerAdd", FluentIcons.Common.Symbol.BeakerAdd);
					xamlUserType34.AddEnumValue("BeakerDismiss", FluentIcons.Common.Symbol.BeakerDismiss);
					xamlUserType34.AddEnumValue("BeakerEdit", FluentIcons.Common.Symbol.BeakerEdit);
					xamlUserType34.AddEnumValue("BeakerOff", FluentIcons.Common.Symbol.BeakerOff);
					xamlUserType34.AddEnumValue("BeakerSettings", FluentIcons.Common.Symbol.BeakerSettings);
					xamlUserType34.AddEnumValue("Bed", FluentIcons.Common.Symbol.Bed);
					xamlUserType34.AddEnumValue("Bench", FluentIcons.Common.Symbol.Bench);
					xamlUserType34.AddEnumValue("BezierCurveSquare", FluentIcons.Common.Symbol.BezierCurveSquare);
					xamlUserType34.AddEnumValue("BinFull", FluentIcons.Common.Symbol.BinFull);
					xamlUserType34.AddEnumValue("BinRecycle", FluentIcons.Common.Symbol.BinRecycle);
					xamlUserType34.AddEnumValue("BinRecycleFull", FluentIcons.Common.Symbol.BinRecycleFull);
					xamlUserType34.AddEnumValue("BinderTriangle", FluentIcons.Common.Symbol.BinderTriangle);
					xamlUserType34.AddEnumValue("Bluetooth", FluentIcons.Common.Symbol.Bluetooth);
					xamlUserType34.AddEnumValue("BluetoothConnected", FluentIcons.Common.Symbol.BluetoothConnected);
					xamlUserType34.AddEnumValue("BluetoothDisabled", FluentIcons.Common.Symbol.BluetoothDisabled);
					xamlUserType34.AddEnumValue("BluetoothSearching", FluentIcons.Common.Symbol.BluetoothSearching);
					xamlUserType34.AddEnumValue("Blur", FluentIcons.Common.Symbol.Blur);
					xamlUserType34.AddEnumValue("Board", FluentIcons.Common.Symbol.Board);
					xamlUserType34.AddEnumValue("BoardGames", FluentIcons.Common.Symbol.BoardGames);
					xamlUserType34.AddEnumValue("BoardHeart", FluentIcons.Common.Symbol.BoardHeart);
					xamlUserType34.AddEnumValue("BoardSplit", FluentIcons.Common.Symbol.BoardSplit);
					xamlUserType34.AddEnumValue("Book", FluentIcons.Common.Symbol.Book);
					xamlUserType34.AddEnumValue("BookAdd", FluentIcons.Common.Symbol.BookAdd);
					xamlUserType34.AddEnumValue("BookArrowClockwise", FluentIcons.Common.Symbol.BookArrowClockwise);
					xamlUserType34.AddEnumValue("BookClock", FluentIcons.Common.Symbol.BookClock);
					xamlUserType34.AddEnumValue("BookCoins", FluentIcons.Common.Symbol.BookCoins);
					xamlUserType34.AddEnumValue("BookCompass", FluentIcons.Common.Symbol.BookCompass);
					xamlUserType34.AddEnumValue("BookContacts", FluentIcons.Common.Symbol.BookContacts);
					xamlUserType34.AddEnumValue("BookDatabase", FluentIcons.Common.Symbol.BookDatabase);
					xamlUserType34.AddEnumValue("BookDefault", FluentIcons.Common.Symbol.BookDefault);
					xamlUserType34.AddEnumValue("BookDismiss", FluentIcons.Common.Symbol.BookDismiss);
					xamlUserType34.AddEnumValue("BookExclamationMark", FluentIcons.Common.Symbol.BookExclamationMark);
					xamlUserType34.AddEnumValue("BookGlobe", FluentIcons.Common.Symbol.BookGlobe);
					xamlUserType34.AddEnumValue("BookInformation", FluentIcons.Common.Symbol.BookInformation);
					xamlUserType34.AddEnumValue("BookLetter", FluentIcons.Common.Symbol.BookLetter);
					xamlUserType34.AddEnumValue("BookNumber", FluentIcons.Common.Symbol.BookNumber);
					xamlUserType34.AddEnumValue("BookOpen", FluentIcons.Common.Symbol.BookOpen);
					xamlUserType34.AddEnumValue("BookOpenGlobe", FluentIcons.Common.Symbol.BookOpenGlobe);
					xamlUserType34.AddEnumValue("BookOpenMicrophone", FluentIcons.Common.Symbol.BookOpenMicrophone);
					xamlUserType34.AddEnumValue("BookPulse", FluentIcons.Common.Symbol.BookPulse);
					xamlUserType34.AddEnumValue("BookQuestionMark", FluentIcons.Common.Symbol.BookQuestionMark);
					xamlUserType34.AddEnumValue("BookSearch", FluentIcons.Common.Symbol.BookSearch);
					xamlUserType34.AddEnumValue("BookStar", FluentIcons.Common.Symbol.BookStar);
					xamlUserType34.AddEnumValue("BookTemplate", FluentIcons.Common.Symbol.BookTemplate);
					xamlUserType34.AddEnumValue("BookTheta", FluentIcons.Common.Symbol.BookTheta);
					xamlUserType34.AddEnumValue("BookToolbox", FluentIcons.Common.Symbol.BookToolbox);
					xamlUserType34.AddEnumValue("Bookmark", FluentIcons.Common.Symbol.Bookmark);
					xamlUserType34.AddEnumValue("BookmarkAdd", FluentIcons.Common.Symbol.BookmarkAdd);
					xamlUserType34.AddEnumValue("BookmarkMultiple", FluentIcons.Common.Symbol.BookmarkMultiple);
					xamlUserType34.AddEnumValue("BookmarkOff", FluentIcons.Common.Symbol.BookmarkOff);
					xamlUserType34.AddEnumValue("BookmarkSearch", FluentIcons.Common.Symbol.BookmarkSearch);
					xamlUserType34.AddEnumValue("BorderAll", FluentIcons.Common.Symbol.BorderAll);
					xamlUserType34.AddEnumValue("BorderBottom", FluentIcons.Common.Symbol.BorderBottom);
					xamlUserType34.AddEnumValue("BorderBottomDouble", FluentIcons.Common.Symbol.BorderBottomDouble);
					xamlUserType34.AddEnumValue("BorderBottomThick", FluentIcons.Common.Symbol.BorderBottomThick);
					xamlUserType34.AddEnumValue("BorderInside", FluentIcons.Common.Symbol.BorderInside);
					xamlUserType34.AddEnumValue("BorderLeft", FluentIcons.Common.Symbol.BorderLeft);
					xamlUserType34.AddEnumValue("BorderLeftRight", FluentIcons.Common.Symbol.BorderLeftRight);
					xamlUserType34.AddEnumValue("BorderNone", FluentIcons.Common.Symbol.BorderNone);
					xamlUserType34.AddEnumValue("BorderOutside", FluentIcons.Common.Symbol.BorderOutside);
					xamlUserType34.AddEnumValue("BorderOutsideThick", FluentIcons.Common.Symbol.BorderOutsideThick);
					xamlUserType34.AddEnumValue("BorderRight", FluentIcons.Common.Symbol.BorderRight);
					xamlUserType34.AddEnumValue("BorderTop", FluentIcons.Common.Symbol.BorderTop);
					xamlUserType34.AddEnumValue("BorderTopBottom", FluentIcons.Common.Symbol.BorderTopBottom);
					xamlUserType34.AddEnumValue("BorderTopBottomDouble", FluentIcons.Common.Symbol.BorderTopBottomDouble);
					xamlUserType34.AddEnumValue("BorderTopBottomThick", FluentIcons.Common.Symbol.BorderTopBottomThick);
					xamlUserType34.AddEnumValue("Bot", FluentIcons.Common.Symbol.Bot);
					xamlUserType34.AddEnumValue("BotAdd", FluentIcons.Common.Symbol.BotAdd);
					xamlUserType34.AddEnumValue("BotSparkle", FluentIcons.Common.Symbol.BotSparkle);
					xamlUserType34.AddEnumValue("BowTie", FluentIcons.Common.Symbol.BowTie);
					xamlUserType34.AddEnumValue("BowlChopsticks", FluentIcons.Common.Symbol.BowlChopsticks);
					xamlUserType34.AddEnumValue("BowlSalad", FluentIcons.Common.Symbol.BowlSalad);
					xamlUserType34.AddEnumValue("Box", FluentIcons.Common.Symbol.Box);
					xamlUserType34.AddEnumValue("BoxArrowLeft", FluentIcons.Common.Symbol.BoxArrowLeft);
					xamlUserType34.AddEnumValue("BoxArrowUp", FluentIcons.Common.Symbol.BoxArrowUp);
					xamlUserType34.AddEnumValue("BoxCheckmark", FluentIcons.Common.Symbol.BoxCheckmark);
					xamlUserType34.AddEnumValue("BoxDismiss", FluentIcons.Common.Symbol.BoxDismiss);
					xamlUserType34.AddEnumValue("BoxEdit", FluentIcons.Common.Symbol.BoxEdit);
					xamlUserType34.AddEnumValue("BoxMultiple", FluentIcons.Common.Symbol.BoxMultiple);
					xamlUserType34.AddEnumValue("BoxMultipleArrowLeft", FluentIcons.Common.Symbol.BoxMultipleArrowLeft);
					xamlUserType34.AddEnumValue("BoxMultipleArrowRight", FluentIcons.Common.Symbol.BoxMultipleArrowRight);
					xamlUserType34.AddEnumValue("BoxMultipleCheckmark", FluentIcons.Common.Symbol.BoxMultipleCheckmark);
					xamlUserType34.AddEnumValue("BoxMultipleSearch", FluentIcons.Common.Symbol.BoxMultipleSearch);
					xamlUserType34.AddEnumValue("BoxSearch", FluentIcons.Common.Symbol.BoxSearch);
					xamlUserType34.AddEnumValue("BoxToolbox", FluentIcons.Common.Symbol.BoxToolbox);
					xamlUserType34.AddEnumValue("Braces", FluentIcons.Common.Symbol.Braces);
					xamlUserType34.AddEnumValue("BracesVariable", FluentIcons.Common.Symbol.BracesVariable);
					xamlUserType34.AddEnumValue("Brain", FluentIcons.Common.Symbol.Brain);
					xamlUserType34.AddEnumValue("BrainCircuit", FluentIcons.Common.Symbol.BrainCircuit);
					xamlUserType34.AddEnumValue("BrainSparkle", FluentIcons.Common.Symbol.BrainSparkle);
					xamlUserType34.AddEnumValue("Branch", FluentIcons.Common.Symbol.Branch);
					xamlUserType34.AddEnumValue("BranchCompare", FluentIcons.Common.Symbol.BranchCompare);
					xamlUserType34.AddEnumValue("BranchFork", FluentIcons.Common.Symbol.BranchFork);
					xamlUserType34.AddEnumValue("BranchForkHint", FluentIcons.Common.Symbol.BranchForkHint);
					xamlUserType34.AddEnumValue("BranchForkLink", FluentIcons.Common.Symbol.BranchForkLink);
					xamlUserType34.AddEnumValue("BranchRequest", FluentIcons.Common.Symbol.BranchRequest);
					xamlUserType34.AddEnumValue("BranchRequestClosed", FluentIcons.Common.Symbol.BranchRequestClosed);
					xamlUserType34.AddEnumValue("BranchRequestDraft", FluentIcons.Common.Symbol.BranchRequestDraft);
					xamlUserType34.AddEnumValue("BreakoutRoom", FluentIcons.Common.Symbol.BreakoutRoom);
					xamlUserType34.AddEnumValue("Briefcase", FluentIcons.Common.Symbol.Briefcase);
					xamlUserType34.AddEnumValue("BriefcaseMedical", FluentIcons.Common.Symbol.BriefcaseMedical);
					xamlUserType34.AddEnumValue("BriefcaseOff", FluentIcons.Common.Symbol.BriefcaseOff);
					xamlUserType34.AddEnumValue("BriefcaseSearch", FluentIcons.Common.Symbol.BriefcaseSearch);
					xamlUserType34.AddEnumValue("BrightnessHigh", FluentIcons.Common.Symbol.BrightnessHigh);
					xamlUserType34.AddEnumValue("BrightnessLow", FluentIcons.Common.Symbol.BrightnessLow);
					xamlUserType34.AddEnumValue("BroadActivityFeed", FluentIcons.Common.Symbol.BroadActivityFeed);
					xamlUserType34.AddEnumValue("Broom", FluentIcons.Common.Symbol.Broom);
					xamlUserType34.AddEnumValue("BubbleMultiple", FluentIcons.Common.Symbol.BubbleMultiple);
					xamlUserType34.AddEnumValue("Bug", FluentIcons.Common.Symbol.Bug);
					xamlUserType34.AddEnumValue("BugArrowCounterclockwise", FluentIcons.Common.Symbol.BugArrowCounterclockwise);
					xamlUserType34.AddEnumValue("BugProhibited", FluentIcons.Common.Symbol.BugProhibited);
					xamlUserType34.AddEnumValue("Building", FluentIcons.Common.Symbol.Building);
					xamlUserType34.AddEnumValue("BuildingBank", FluentIcons.Common.Symbol.BuildingBank);
					xamlUserType34.AddEnumValue("BuildingBankLink", FluentIcons.Common.Symbol.BuildingBankLink);
					xamlUserType34.AddEnumValue("BuildingBankToolbox", FluentIcons.Common.Symbol.BuildingBankToolbox);
					xamlUserType34.AddEnumValue("BuildingCheckmark", FluentIcons.Common.Symbol.BuildingCheckmark);
					xamlUserType34.AddEnumValue("BuildingDesktop", FluentIcons.Common.Symbol.BuildingDesktop);
					xamlUserType34.AddEnumValue("BuildingFactory", FluentIcons.Common.Symbol.BuildingFactory);
					xamlUserType34.AddEnumValue("BuildingGovernment", FluentIcons.Common.Symbol.BuildingGovernment);
					xamlUserType34.AddEnumValue("BuildingGovernmentSearch", FluentIcons.Common.Symbol.BuildingGovernmentSearch);
					xamlUserType34.AddEnumValue("BuildingHome", FluentIcons.Common.Symbol.BuildingHome);
					xamlUserType34.AddEnumValue("BuildingLighthouse", FluentIcons.Common.Symbol.BuildingLighthouse);
					xamlUserType34.AddEnumValue("BuildingMosque", FluentIcons.Common.Symbol.BuildingMosque);
					xamlUserType34.AddEnumValue("BuildingMultiple", FluentIcons.Common.Symbol.BuildingMultiple);
					xamlUserType34.AddEnumValue("BuildingPeople", FluentIcons.Common.Symbol.BuildingPeople);
					xamlUserType34.AddEnumValue("BuildingRetail", FluentIcons.Common.Symbol.BuildingRetail);
					xamlUserType34.AddEnumValue("BuildingRetailMoney", FluentIcons.Common.Symbol.BuildingRetailMoney);
					xamlUserType34.AddEnumValue("BuildingRetailMore", FluentIcons.Common.Symbol.BuildingRetailMore);
					xamlUserType34.AddEnumValue("BuildingRetailShield", FluentIcons.Common.Symbol.BuildingRetailShield);
					xamlUserType34.AddEnumValue("BuildingRetailToolbox", FluentIcons.Common.Symbol.BuildingRetailToolbox);
					xamlUserType34.AddEnumValue("BuildingShop", FluentIcons.Common.Symbol.BuildingShop);
					xamlUserType34.AddEnumValue("BuildingSkyscraper", FluentIcons.Common.Symbol.BuildingSkyscraper);
					xamlUserType34.AddEnumValue("BuildingSwap", FluentIcons.Common.Symbol.BuildingSwap);
					xamlUserType34.AddEnumValue("BuildingTownhouse", FluentIcons.Common.Symbol.BuildingTownhouse);
					xamlUserType34.AddEnumValue("Button", FluentIcons.Common.Symbol.Button);
					xamlUserType34.AddEnumValue("Calculator", FluentIcons.Common.Symbol.Calculator);
					xamlUserType34.AddEnumValue("CalculatorArrowClockwise", FluentIcons.Common.Symbol.CalculatorArrowClockwise);
					xamlUserType34.AddEnumValue("CalculatorMultiple", FluentIcons.Common.Symbol.CalculatorMultiple);
					xamlUserType34.AddEnumValue("Calendar", FluentIcons.Common.Symbol.Calendar);
					xamlUserType34.AddEnumValue("Calendar3Day", FluentIcons.Common.Symbol.Calendar3Day);
					xamlUserType34.AddEnumValue("CalendarAdd", FluentIcons.Common.Symbol.CalendarAdd);
					xamlUserType34.AddEnumValue("CalendarAgenda", FluentIcons.Common.Symbol.CalendarAgenda);
					xamlUserType34.AddEnumValue("CalendarArrowCounterclockwise", FluentIcons.Common.Symbol.CalendarArrowCounterclockwise);
					xamlUserType34.AddEnumValue("CalendarArrowDown", FluentIcons.Common.Symbol.CalendarArrowDown);
					xamlUserType34.AddEnumValue("CalendarArrowRepeatAll", FluentIcons.Common.Symbol.CalendarArrowRepeatAll);
					xamlUserType34.AddEnumValue("CalendarArrowRight", FluentIcons.Common.Symbol.CalendarArrowRight);
					xamlUserType34.AddEnumValue("CalendarAssistant", FluentIcons.Common.Symbol.CalendarAssistant);
					xamlUserType34.AddEnumValue("CalendarCancel", FluentIcons.Common.Symbol.CalendarCancel);
					xamlUserType34.AddEnumValue("CalendarChat", FluentIcons.Common.Symbol.CalendarChat);
					xamlUserType34.AddEnumValue("CalendarCheckmark", FluentIcons.Common.Symbol.CalendarCheckmark);
					xamlUserType34.AddEnumValue("CalendarClock", FluentIcons.Common.Symbol.CalendarClock);
					xamlUserType34.AddEnumValue("CalendarDataBar", FluentIcons.Common.Symbol.CalendarDataBar);
					xamlUserType34.AddEnumValue("CalendarDate", FluentIcons.Common.Symbol.CalendarDate);
					xamlUserType34.AddEnumValue("CalendarDay", FluentIcons.Common.Symbol.CalendarDay);
					xamlUserType34.AddEnumValue("CalendarEdit", FluentIcons.Common.Symbol.CalendarEdit);
					xamlUserType34.AddEnumValue("CalendarEmpty", FluentIcons.Common.Symbol.CalendarEmpty);
					xamlUserType34.AddEnumValue("CalendarError", FluentIcons.Common.Symbol.CalendarError);
					xamlUserType34.AddEnumValue("CalendarEye", FluentIcons.Common.Symbol.CalendarEye);
					xamlUserType34.AddEnumValue("CalendarInfo", FluentIcons.Common.Symbol.CalendarInfo);
					xamlUserType34.AddEnumValue("CalendarLock", FluentIcons.Common.Symbol.CalendarLock);
					xamlUserType34.AddEnumValue("CalendarMail", FluentIcons.Common.Symbol.CalendarMail);
					xamlUserType34.AddEnumValue("CalendarMention", FluentIcons.Common.Symbol.CalendarMention);
					xamlUserType34.AddEnumValue("CalendarMonth", FluentIcons.Common.Symbol.CalendarMonth);
					xamlUserType34.AddEnumValue("CalendarMultiple", FluentIcons.Common.Symbol.CalendarMultiple);
					xamlUserType34.AddEnumValue("CalendarNote", FluentIcons.Common.Symbol.CalendarNote);
					xamlUserType34.AddEnumValue("CalendarPattern", FluentIcons.Common.Symbol.CalendarPattern);
					xamlUserType34.AddEnumValue("CalendarPerson", FluentIcons.Common.Symbol.CalendarPerson);
					xamlUserType34.AddEnumValue("CalendarPhone", FluentIcons.Common.Symbol.CalendarPhone);
					xamlUserType34.AddEnumValue("CalendarPlay", FluentIcons.Common.Symbol.CalendarPlay);
					xamlUserType34.AddEnumValue("CalendarQuestionMark", FluentIcons.Common.Symbol.CalendarQuestionMark);
					xamlUserType34.AddEnumValue("CalendarRecord", FluentIcons.Common.Symbol.CalendarRecord);
					xamlUserType34.AddEnumValue("CalendarReply", FluentIcons.Common.Symbol.CalendarReply);
					xamlUserType34.AddEnumValue("CalendarSearch", FluentIcons.Common.Symbol.CalendarSearch);
					xamlUserType34.AddEnumValue("CalendarSettings", FluentIcons.Common.Symbol.CalendarSettings);
					xamlUserType34.AddEnumValue("CalendarShield", FluentIcons.Common.Symbol.CalendarShield);
					xamlUserType34.AddEnumValue("CalendarSparkle", FluentIcons.Common.Symbol.CalendarSparkle);
					xamlUserType34.AddEnumValue("CalendarStar", FluentIcons.Common.Symbol.CalendarStar);
					xamlUserType34.AddEnumValue("CalendarSync", FluentIcons.Common.Symbol.CalendarSync);
					xamlUserType34.AddEnumValue("CalendarTemplate", FluentIcons.Common.Symbol.CalendarTemplate);
					xamlUserType34.AddEnumValue("CalendarToday", FluentIcons.Common.Symbol.CalendarToday);
					xamlUserType34.AddEnumValue("CalendarTodo", FluentIcons.Common.Symbol.CalendarTodo);
					xamlUserType34.AddEnumValue("CalendarToolbox", FluentIcons.Common.Symbol.CalendarToolbox);
					xamlUserType34.AddEnumValue("CalendarVideo", FluentIcons.Common.Symbol.CalendarVideo);
					xamlUserType34.AddEnumValue("CalendarWeekNumbers", FluentIcons.Common.Symbol.CalendarWeekNumbers);
					xamlUserType34.AddEnumValue("CalendarWeekStart", FluentIcons.Common.Symbol.CalendarWeekStart);
					xamlUserType34.AddEnumValue("CalendarWorkWeek", FluentIcons.Common.Symbol.CalendarWorkWeek);
					xamlUserType34.AddEnumValue("Call", FluentIcons.Common.Symbol.Call);
					xamlUserType34.AddEnumValue("CallAdd", FluentIcons.Common.Symbol.CallAdd);
					xamlUserType34.AddEnumValue("CallCheckmark", FluentIcons.Common.Symbol.CallCheckmark);
					xamlUserType34.AddEnumValue("CallConnecting", FluentIcons.Common.Symbol.CallConnecting);
					xamlUserType34.AddEnumValue("CallDismiss", FluentIcons.Common.Symbol.CallDismiss);
					xamlUserType34.AddEnumValue("CallEnd", FluentIcons.Common.Symbol.CallEnd);
					xamlUserType34.AddEnumValue("CallExclamation", FluentIcons.Common.Symbol.CallExclamation);
					xamlUserType34.AddEnumValue("CallForward", FluentIcons.Common.Symbol.CallForward);
					xamlUserType34.AddEnumValue("CallInbound", FluentIcons.Common.Symbol.CallInbound);
					xamlUserType34.AddEnumValue("CallMissed", FluentIcons.Common.Symbol.CallMissed);
					xamlUserType34.AddEnumValue("CallOutbound", FluentIcons.Common.Symbol.CallOutbound);
					xamlUserType34.AddEnumValue("CallPark", FluentIcons.Common.Symbol.CallPark);
					xamlUserType34.AddEnumValue("CallPause", FluentIcons.Common.Symbol.CallPause);
					xamlUserType34.AddEnumValue("CallProhibited", FluentIcons.Common.Symbol.CallProhibited);
					xamlUserType34.AddEnumValue("CallRectangleLandscape", FluentIcons.Common.Symbol.CallRectangleLandscape);
					xamlUserType34.AddEnumValue("CallSquare", FluentIcons.Common.Symbol.CallSquare);
					xamlUserType34.AddEnumValue("CallTransfer", FluentIcons.Common.Symbol.CallTransfer);
					xamlUserType34.AddEnumValue("CallWarning", FluentIcons.Common.Symbol.CallWarning);
					xamlUserType34.AddEnumValue("CalligraphyPen", FluentIcons.Common.Symbol.CalligraphyPen);
					xamlUserType34.AddEnumValue("CalligraphyPenCheckmark", FluentIcons.Common.Symbol.CalligraphyPenCheckmark);
					xamlUserType34.AddEnumValue("CalligraphyPenError", FluentIcons.Common.Symbol.CalligraphyPenError);
					xamlUserType34.AddEnumValue("CalligraphyPenQuestionMark", FluentIcons.Common.Symbol.CalligraphyPenQuestionMark);
					xamlUserType34.AddEnumValue("Camera", FluentIcons.Common.Symbol.Camera);
					xamlUserType34.AddEnumValue("CameraAdd", FluentIcons.Common.Symbol.CameraAdd);
					xamlUserType34.AddEnumValue("CameraArrowUp", FluentIcons.Common.Symbol.CameraArrowUp);
					xamlUserType34.AddEnumValue("CameraDome", FluentIcons.Common.Symbol.CameraDome);
					xamlUserType34.AddEnumValue("CameraEdit", FluentIcons.Common.Symbol.CameraEdit);
					xamlUserType34.AddEnumValue("CameraOff", FluentIcons.Common.Symbol.CameraOff);
					xamlUserType34.AddEnumValue("CameraSparkles", FluentIcons.Common.Symbol.CameraSparkles);
					xamlUserType34.AddEnumValue("CameraSwitch", FluentIcons.Common.Symbol.CameraSwitch);
					xamlUserType34.AddEnumValue("CardUi", FluentIcons.Common.Symbol.CardUi);
					xamlUserType34.AddEnumValue("CardUiPortraitFlip", FluentIcons.Common.Symbol.CardUiPortraitFlip);
					xamlUserType34.AddEnumValue("CaretDown", FluentIcons.Common.Symbol.CaretDown);
					xamlUserType34.AddEnumValue("CaretDownRight", FluentIcons.Common.Symbol.CaretDownRight);
					xamlUserType34.AddEnumValue("CaretLeft", FluentIcons.Common.Symbol.CaretLeft);
					xamlUserType34.AddEnumValue("CaretRight", FluentIcons.Common.Symbol.CaretRight);
					xamlUserType34.AddEnumValue("CaretUp", FluentIcons.Common.Symbol.CaretUp);
					xamlUserType34.AddEnumValue("Cart", FluentIcons.Common.Symbol.Cart);
					xamlUserType34.AddEnumValue("Cast", FluentIcons.Common.Symbol.Cast);
					xamlUserType34.AddEnumValue("CastMultiple", FluentIcons.Common.Symbol.CastMultiple);
					xamlUserType34.AddEnumValue("CatchUp", FluentIcons.Common.Symbol.CatchUp);
					xamlUserType34.AddEnumValue("Cellular3g", FluentIcons.Common.Symbol.Cellular3g);
					xamlUserType34.AddEnumValue("Cellular4g", FluentIcons.Common.Symbol.Cellular4g);
					xamlUserType34.AddEnumValue("Cellular5g", FluentIcons.Common.Symbol.Cellular5g);
					xamlUserType34.AddEnumValue("CellularData1", FluentIcons.Common.Symbol.CellularData1);
					xamlUserType34.AddEnumValue("CellularData2", FluentIcons.Common.Symbol.CellularData2);
					xamlUserType34.AddEnumValue("CellularData3", FluentIcons.Common.Symbol.CellularData3);
					xamlUserType34.AddEnumValue("CellularData4", FluentIcons.Common.Symbol.CellularData4);
					xamlUserType34.AddEnumValue("CellularData5", FluentIcons.Common.Symbol.CellularData5);
					xamlUserType34.AddEnumValue("CellularOff", FluentIcons.Common.Symbol.CellularOff);
					xamlUserType34.AddEnumValue("CellularWarning", FluentIcons.Common.Symbol.CellularWarning);
					xamlUserType34.AddEnumValue("CenterHorizontal", FluentIcons.Common.Symbol.CenterHorizontal);
					xamlUserType34.AddEnumValue("CenterVertical", FluentIcons.Common.Symbol.CenterVertical);
					xamlUserType34.AddEnumValue("Certificate", FluentIcons.Common.Symbol.Certificate);
					xamlUserType34.AddEnumValue("Channel", FluentIcons.Common.Symbol.Channel);
					xamlUserType34.AddEnumValue("ChannelAdd", FluentIcons.Common.Symbol.ChannelAdd);
					xamlUserType34.AddEnumValue("ChannelAlert", FluentIcons.Common.Symbol.ChannelAlert);
					xamlUserType34.AddEnumValue("ChannelArrowLeft", FluentIcons.Common.Symbol.ChannelArrowLeft);
					xamlUserType34.AddEnumValue("ChannelDismiss", FluentIcons.Common.Symbol.ChannelDismiss);
					xamlUserType34.AddEnumValue("ChannelShare", FluentIcons.Common.Symbol.ChannelShare);
					xamlUserType34.AddEnumValue("ChannelSubtract", FluentIcons.Common.Symbol.ChannelSubtract);
					xamlUserType34.AddEnumValue("ChartMultiple", FluentIcons.Common.Symbol.ChartMultiple);
					xamlUserType34.AddEnumValue("ChartPerson", FluentIcons.Common.Symbol.ChartPerson);
					xamlUserType34.AddEnumValue("Chat", FluentIcons.Common.Symbol.Chat);
					xamlUserType34.AddEnumValue("ChatAdd", FluentIcons.Common.Symbol.ChatAdd);
					xamlUserType34.AddEnumValue("ChatArrowBack", FluentIcons.Common.Symbol.ChatArrowBack);
					xamlUserType34.AddEnumValue("ChatArrowBackDown", FluentIcons.Common.Symbol.ChatArrowBackDown);
					xamlUserType34.AddEnumValue("ChatArrowDoubleBack", FluentIcons.Common.Symbol.ChatArrowDoubleBack);
					xamlUserType34.AddEnumValue("ChatBubblesQuestion", FluentIcons.Common.Symbol.ChatBubblesQuestion);
					xamlUserType34.AddEnumValue("ChatCursor", FluentIcons.Common.Symbol.ChatCursor);
					xamlUserType34.AddEnumValue("ChatDismiss", FluentIcons.Common.Symbol.ChatDismiss);
					xamlUserType34.AddEnumValue("ChatEmpty", FluentIcons.Common.Symbol.ChatEmpty);
					xamlUserType34.AddEnumValue("ChatHelp", FluentIcons.Common.Symbol.ChatHelp);
					xamlUserType34.AddEnumValue("ChatHistory", FluentIcons.Common.Symbol.ChatHistory);
					xamlUserType34.AddEnumValue("ChatLock", FluentIcons.Common.Symbol.ChatLock);
					xamlUserType34.AddEnumValue("ChatMail", FluentIcons.Common.Symbol.ChatMail);
					xamlUserType34.AddEnumValue("ChatMultiple", FluentIcons.Common.Symbol.ChatMultiple);
					xamlUserType34.AddEnumValue("ChatMultipleHeart", FluentIcons.Common.Symbol.ChatMultipleHeart);
					xamlUserType34.AddEnumValue("ChatOff", FluentIcons.Common.Symbol.ChatOff);
					xamlUserType34.AddEnumValue("ChatSettings", FluentIcons.Common.Symbol.ChatSettings);
					xamlUserType34.AddEnumValue("ChatSparkle", FluentIcons.Common.Symbol.ChatSparkle);
					xamlUserType34.AddEnumValue("ChatVideo", FluentIcons.Common.Symbol.ChatVideo);
					xamlUserType34.AddEnumValue("ChatWarning", FluentIcons.Common.Symbol.ChatWarning);
					xamlUserType34.AddEnumValue("Check", FluentIcons.Common.Symbol.Check);
					xamlUserType34.AddEnumValue("Checkbox1", FluentIcons.Common.Symbol.Checkbox1);
					xamlUserType34.AddEnumValue("Checkbox2", FluentIcons.Common.Symbol.Checkbox2);
					xamlUserType34.AddEnumValue("CheckboxArrowRight", FluentIcons.Common.Symbol.CheckboxArrowRight);
					xamlUserType34.AddEnumValue("CheckboxChecked", FluentIcons.Common.Symbol.CheckboxChecked);
					xamlUserType34.AddEnumValue("CheckboxCheckedSync", FluentIcons.Common.Symbol.CheckboxCheckedSync);
					xamlUserType34.AddEnumValue("CheckboxIndeterminate", FluentIcons.Common.Symbol.CheckboxIndeterminate);
					xamlUserType34.AddEnumValue("CheckboxPerson", FluentIcons.Common.Symbol.CheckboxPerson);
					xamlUserType34.AddEnumValue("CheckboxUnchecked", FluentIcons.Common.Symbol.CheckboxUnchecked);
					xamlUserType34.AddEnumValue("CheckboxWarning", FluentIcons.Common.Symbol.CheckboxWarning);
					xamlUserType34.AddEnumValue("Checkmark", FluentIcons.Common.Symbol.Checkmark);
					xamlUserType34.AddEnumValue("CheckmarkCircle", FluentIcons.Common.Symbol.CheckmarkCircle);
					xamlUserType34.AddEnumValue("CheckmarkCircleSquare", FluentIcons.Common.Symbol.CheckmarkCircleSquare);
					xamlUserType34.AddEnumValue("CheckmarkCircleWarning", FluentIcons.Common.Symbol.CheckmarkCircleWarning);
					xamlUserType34.AddEnumValue("CheckmarkLock", FluentIcons.Common.Symbol.CheckmarkLock);
					xamlUserType34.AddEnumValue("CheckmarkNote", FluentIcons.Common.Symbol.CheckmarkNote);
					xamlUserType34.AddEnumValue("CheckmarkSquare", FluentIcons.Common.Symbol.CheckmarkSquare);
					xamlUserType34.AddEnumValue("CheckmarkStarburst", FluentIcons.Common.Symbol.CheckmarkStarburst);
					xamlUserType34.AddEnumValue("CheckmarkUnderlineCircle", FluentIcons.Common.Symbol.CheckmarkUnderlineCircle);
					xamlUserType34.AddEnumValue("Chess", FluentIcons.Common.Symbol.Chess);
					xamlUserType34.AddEnumValue("ChevronCircleDown", FluentIcons.Common.Symbol.ChevronCircleDown);
					xamlUserType34.AddEnumValue("ChevronCircleLeft", FluentIcons.Common.Symbol.ChevronCircleLeft);
					xamlUserType34.AddEnumValue("ChevronCircleRight", FluentIcons.Common.Symbol.ChevronCircleRight);
					xamlUserType34.AddEnumValue("ChevronCircleUp", FluentIcons.Common.Symbol.ChevronCircleUp);
					xamlUserType34.AddEnumValue("ChevronDoubleDown", FluentIcons.Common.Symbol.ChevronDoubleDown);
					xamlUserType34.AddEnumValue("ChevronDoubleLeft", FluentIcons.Common.Symbol.ChevronDoubleLeft);
					xamlUserType34.AddEnumValue("ChevronDoubleRight", FluentIcons.Common.Symbol.ChevronDoubleRight);
					xamlUserType34.AddEnumValue("ChevronDoubleUp", FluentIcons.Common.Symbol.ChevronDoubleUp);
					xamlUserType34.AddEnumValue("ChevronDown", FluentIcons.Common.Symbol.ChevronDown);
					xamlUserType34.AddEnumValue("ChevronDownUp", FluentIcons.Common.Symbol.ChevronDownUp);
					xamlUserType34.AddEnumValue("ChevronLeft", FluentIcons.Common.Symbol.ChevronLeft);
					xamlUserType34.AddEnumValue("ChevronRight", FluentIcons.Common.Symbol.ChevronRight);
					xamlUserType34.AddEnumValue("ChevronUp", FluentIcons.Common.Symbol.ChevronUp);
					xamlUserType34.AddEnumValue("ChevronUpDown", FluentIcons.Common.Symbol.ChevronUpDown);
					xamlUserType34.AddEnumValue("Circle", FluentIcons.Common.Symbol.Circle);
					xamlUserType34.AddEnumValue("CircleEdit", FluentIcons.Common.Symbol.CircleEdit);
					xamlUserType34.AddEnumValue("CircleEraser", FluentIcons.Common.Symbol.CircleEraser);
					xamlUserType34.AddEnumValue("CircleHalfFill", FluentIcons.Common.Symbol.CircleHalfFill);
					xamlUserType34.AddEnumValue("CircleHighlight", FluentIcons.Common.Symbol.CircleHighlight);
					xamlUserType34.AddEnumValue("CircleHint", FluentIcons.Common.Symbol.CircleHint);
					xamlUserType34.AddEnumValue("CircleHintCursor", FluentIcons.Common.Symbol.CircleHintCursor);
					xamlUserType34.AddEnumValue("CircleHintDismiss", FluentIcons.Common.Symbol.CircleHintDismiss);
					xamlUserType34.AddEnumValue("CircleHintHalfVertical", FluentIcons.Common.Symbol.CircleHintHalfVertical);
					xamlUserType34.AddEnumValue("CircleImage", FluentIcons.Common.Symbol.CircleImage);
					xamlUserType34.AddEnumValue("CircleLine", FluentIcons.Common.Symbol.CircleLine);
					xamlUserType34.AddEnumValue("CircleMultipleConcentric", FluentIcons.Common.Symbol.CircleMultipleConcentric);
					xamlUserType34.AddEnumValue("CircleMultipleHintCheckmark", FluentIcons.Common.Symbol.CircleMultipleHintCheckmark);
					xamlUserType34.AddEnumValue("CircleMultipleSubtractCheckmark", FluentIcons.Common.Symbol.CircleMultipleSubtractCheckmark);
					xamlUserType34.AddEnumValue("CircleOff", FluentIcons.Common.Symbol.CircleOff);
					xamlUserType34.AddEnumValue("CircleShadow", FluentIcons.Common.Symbol.CircleShadow);
					xamlUserType34.AddEnumValue("CircleSmall", FluentIcons.Common.Symbol.CircleSmall);
					xamlUserType34.AddEnumValue("CircleSparkle", FluentIcons.Common.Symbol.CircleSparkle);
					xamlUserType34.AddEnumValue("City", FluentIcons.Common.Symbol.City);
					xamlUserType34.AddEnumValue("Class", FluentIcons.Common.Symbol.Class);
					xamlUserType34.AddEnumValue("Classification", FluentIcons.Common.Symbol.Classification);
					xamlUserType34.AddEnumValue("ClearFormatting", FluentIcons.Common.Symbol.ClearFormatting);
					xamlUserType34.AddEnumValue("Clipboard", FluentIcons.Common.Symbol.Clipboard);
					xamlUserType34.AddEnumValue("Clipboard3Day", FluentIcons.Common.Symbol.Clipboard3Day);
					xamlUserType34.AddEnumValue("ClipboardArrowRight", FluentIcons.Common.Symbol.ClipboardArrowRight);
					xamlUserType34.AddEnumValue("ClipboardBrush", FluentIcons.Common.Symbol.ClipboardBrush);
					xamlUserType34.AddEnumValue("ClipboardBulletList", FluentIcons.Common.Symbol.ClipboardBulletList);
					xamlUserType34.AddEnumValue("ClipboardCheckmark", FluentIcons.Common.Symbol.ClipboardCheckmark);
					xamlUserType34.AddEnumValue("ClipboardClock", FluentIcons.Common.Symbol.ClipboardClock);
					xamlUserType34.AddEnumValue("ClipboardCode", FluentIcons.Common.Symbol.ClipboardCode);
					xamlUserType34.AddEnumValue("ClipboardDataBar", FluentIcons.Common.Symbol.ClipboardDataBar);
					xamlUserType34.AddEnumValue("ClipboardDay", FluentIcons.Common.Symbol.ClipboardDay);
					xamlUserType34.AddEnumValue("ClipboardEdit", FluentIcons.Common.Symbol.ClipboardEdit);
					xamlUserType34.AddEnumValue("ClipboardError", FluentIcons.Common.Symbol.ClipboardError);
					xamlUserType34.AddEnumValue("ClipboardHeart", FluentIcons.Common.Symbol.ClipboardHeart);
					xamlUserType34.AddEnumValue("ClipboardImage", FluentIcons.Common.Symbol.ClipboardImage);
					xamlUserType34.AddEnumValue("ClipboardLetter", FluentIcons.Common.Symbol.ClipboardLetter);
					xamlUserType34.AddEnumValue("ClipboardLink", FluentIcons.Common.Symbol.ClipboardLink);
					xamlUserType34.AddEnumValue("ClipboardMathFormula", FluentIcons.Common.Symbol.ClipboardMathFormula);
					xamlUserType34.AddEnumValue("ClipboardMonth", FluentIcons.Common.Symbol.ClipboardMonth);
					xamlUserType34.AddEnumValue("ClipboardMore", FluentIcons.Common.Symbol.ClipboardMore);
					xamlUserType34.AddEnumValue("ClipboardNote", FluentIcons.Common.Symbol.ClipboardNote);
					xamlUserType34.AddEnumValue("ClipboardNumber123", FluentIcons.Common.Symbol.ClipboardNumber123);
					xamlUserType34.AddEnumValue("ClipboardPaste", FluentIcons.Common.Symbol.ClipboardPaste);
					xamlUserType34.AddEnumValue("ClipboardPulse", FluentIcons.Common.Symbol.ClipboardPulse);
					xamlUserType34.AddEnumValue("ClipboardSearch", FluentIcons.Common.Symbol.ClipboardSearch);
					xamlUserType34.AddEnumValue("ClipboardSettings", FluentIcons.Common.Symbol.ClipboardSettings);
					xamlUserType34.AddEnumValue("ClipboardTask", FluentIcons.Common.Symbol.ClipboardTask);
					xamlUserType34.AddEnumValue("ClipboardTaskAdd", FluentIcons.Common.Symbol.ClipboardTaskAdd);
					xamlUserType34.AddEnumValue("ClipboardTaskList", FluentIcons.Common.Symbol.ClipboardTaskList);
					xamlUserType34.AddEnumValue("ClipboardText", FluentIcons.Common.Symbol.ClipboardText);
					xamlUserType34.AddEnumValue("ClipboardTextEdit", FluentIcons.Common.Symbol.ClipboardTextEdit);
					xamlUserType34.AddEnumValue("Clock", FluentIcons.Common.Symbol.Clock);
					xamlUserType34.AddEnumValue("ClockAlarm", FluentIcons.Common.Symbol.ClockAlarm);
					xamlUserType34.AddEnumValue("ClockArrowDownload", FluentIcons.Common.Symbol.ClockArrowDownload);
					xamlUserType34.AddEnumValue("ClockBill", FluentIcons.Common.Symbol.ClockBill);
					xamlUserType34.AddEnumValue("ClockDismiss", FluentIcons.Common.Symbol.ClockDismiss);
					xamlUserType34.AddEnumValue("ClockLock", FluentIcons.Common.Symbol.ClockLock);
					xamlUserType34.AddEnumValue("ClockPause", FluentIcons.Common.Symbol.ClockPause);
					xamlUserType34.AddEnumValue("ClockSparkle", FluentIcons.Common.Symbol.ClockSparkle);
					xamlUserType34.AddEnumValue("ClockToolbox", FluentIcons.Common.Symbol.ClockToolbox);
					xamlUserType34.AddEnumValue("ClosedCaption", FluentIcons.Common.Symbol.ClosedCaption);
					xamlUserType34.AddEnumValue("ClosedCaptionOff", FluentIcons.Common.Symbol.ClosedCaptionOff);
					xamlUserType34.AddEnumValue("ClothesHanger", FluentIcons.Common.Symbol.ClothesHanger);
					xamlUserType34.AddEnumValue("Cloud", FluentIcons.Common.Symbol.Cloud);
					xamlUserType34.AddEnumValue("CloudAdd", FluentIcons.Common.Symbol.CloudAdd);
					xamlUserType34.AddEnumValue("CloudArchive", FluentIcons.Common.Symbol.CloudArchive);
					xamlUserType34.AddEnumValue("CloudArrowDown", FluentIcons.Common.Symbol.CloudArrowDown);
					xamlUserType34.AddEnumValue("CloudArrowRight", FluentIcons.Common.Symbol.CloudArrowRight);
					xamlUserType34.AddEnumValue("CloudArrowUp", FluentIcons.Common.Symbol.CloudArrowUp);
					xamlUserType34.AddEnumValue("CloudBeaker", FluentIcons.Common.Symbol.CloudBeaker);
					xamlUserType34.AddEnumValue("CloudBidirectional", FluentIcons.Common.Symbol.CloudBidirectional);
					xamlUserType34.AddEnumValue("CloudCheckmark", FluentIcons.Common.Symbol.CloudCheckmark);
					xamlUserType34.AddEnumValue("CloudCube", FluentIcons.Common.Symbol.CloudCube);
					xamlUserType34.AddEnumValue("CloudDatabase", FluentIcons.Common.Symbol.CloudDatabase);
					xamlUserType34.AddEnumValue("CloudDesktop", FluentIcons.Common.Symbol.CloudDesktop);
					xamlUserType34.AddEnumValue("CloudDismiss", FluentIcons.Common.Symbol.CloudDismiss);
					xamlUserType34.AddEnumValue("CloudEdit", FluentIcons.Common.Symbol.CloudEdit);
					xamlUserType34.AddEnumValue("CloudError", FluentIcons.Common.Symbol.CloudError);
					xamlUserType34.AddEnumValue("CloudFlow", FluentIcons.Common.Symbol.CloudFlow);
					xamlUserType34.AddEnumValue("CloudLink", FluentIcons.Common.Symbol.CloudLink);
					xamlUserType34.AddEnumValue("CloudOff", FluentIcons.Common.Symbol.CloudOff);
					xamlUserType34.AddEnumValue("CloudSwap", FluentIcons.Common.Symbol.CloudSwap);
					xamlUserType34.AddEnumValue("CloudSync", FluentIcons.Common.Symbol.CloudSync);
					xamlUserType34.AddEnumValue("CloudWords", FluentIcons.Common.Symbol.CloudWords);
					xamlUserType34.AddEnumValue("Clover", FluentIcons.Common.Symbol.Clover);
					xamlUserType34.AddEnumValue("Code", FluentIcons.Common.Symbol.Code);
					xamlUserType34.AddEnumValue("CodeBlock", FluentIcons.Common.Symbol.CodeBlock);
					xamlUserType34.AddEnumValue("CodeCircle", FluentIcons.Common.Symbol.CodeCircle);
					xamlUserType34.AddEnumValue("CodeText", FluentIcons.Common.Symbol.CodeText);
					xamlUserType34.AddEnumValue("CodeTextEdit", FluentIcons.Common.Symbol.CodeTextEdit);
					xamlUserType34.AddEnumValue("CoinMultiple", FluentIcons.Common.Symbol.CoinMultiple);
					xamlUserType34.AddEnumValue("CoinStack", FluentIcons.Common.Symbol.CoinStack);
					xamlUserType34.AddEnumValue("Collections", FluentIcons.Common.Symbol.Collections);
					xamlUserType34.AddEnumValue("CollectionsAdd", FluentIcons.Common.Symbol.CollectionsAdd);
					xamlUserType34.AddEnumValue("CollectionsEmpty", FluentIcons.Common.Symbol.CollectionsEmpty);
					xamlUserType34.AddEnumValue("Color", FluentIcons.Common.Symbol.Color);
					xamlUserType34.AddEnumValue("ColorBackground", FluentIcons.Common.Symbol.ColorBackground);
					xamlUserType34.AddEnumValue("ColorBackgroundAccent", FluentIcons.Common.Symbol.ColorBackgroundAccent);
					xamlUserType34.AddEnumValue("ColorFill", FluentIcons.Common.Symbol.ColorFill);
					xamlUserType34.AddEnumValue("ColorFillAccent", FluentIcons.Common.Symbol.ColorFillAccent);
					xamlUserType34.AddEnumValue("ColorLine", FluentIcons.Common.Symbol.ColorLine);
					xamlUserType34.AddEnumValue("ColorLineAccent", FluentIcons.Common.Symbol.ColorLineAccent);
					xamlUserType34.AddEnumValue("Column", FluentIcons.Common.Symbol.Column);
					xamlUserType34.AddEnumValue("ColumnArrowRight", FluentIcons.Common.Symbol.ColumnArrowRight);
					xamlUserType34.AddEnumValue("ColumnDoubleCompare", FluentIcons.Common.Symbol.ColumnDoubleCompare);
					xamlUserType34.AddEnumValue("ColumnEdit", FluentIcons.Common.Symbol.ColumnEdit);
					xamlUserType34.AddEnumValue("ColumnSingleCompare", FluentIcons.Common.Symbol.ColumnSingleCompare);
					xamlUserType34.AddEnumValue("ColumnTriple", FluentIcons.Common.Symbol.ColumnTriple);
					xamlUserType34.AddEnumValue("ColumnTripleEdit", FluentIcons.Common.Symbol.ColumnTripleEdit);
					xamlUserType34.AddEnumValue("Comma", FluentIcons.Common.Symbol.Comma);
					xamlUserType34.AddEnumValue("Comment", FluentIcons.Common.Symbol.Comment);
					xamlUserType34.AddEnumValue("CommentAdd", FluentIcons.Common.Symbol.CommentAdd);
					xamlUserType34.AddEnumValue("CommentArrowLeft", FluentIcons.Common.Symbol.CommentArrowLeft);
					xamlUserType34.AddEnumValue("CommentArrowRight", FluentIcons.Common.Symbol.CommentArrowRight);
					xamlUserType34.AddEnumValue("CommentBadge", FluentIcons.Common.Symbol.CommentBadge);
					xamlUserType34.AddEnumValue("CommentCheckmark", FluentIcons.Common.Symbol.CommentCheckmark);
					xamlUserType34.AddEnumValue("CommentDismiss", FluentIcons.Common.Symbol.CommentDismiss);
					xamlUserType34.AddEnumValue("CommentEdit", FluentIcons.Common.Symbol.CommentEdit);
					xamlUserType34.AddEnumValue("CommentError", FluentIcons.Common.Symbol.CommentError);
					xamlUserType34.AddEnumValue("CommentLightning", FluentIcons.Common.Symbol.CommentLightning);
					xamlUserType34.AddEnumValue("CommentLink", FluentIcons.Common.Symbol.CommentLink);
					xamlUserType34.AddEnumValue("CommentMention", FluentIcons.Common.Symbol.CommentMention);
					xamlUserType34.AddEnumValue("CommentMultiple", FluentIcons.Common.Symbol.CommentMultiple);
					xamlUserType34.AddEnumValue("CommentMultipleCheckmark", FluentIcons.Common.Symbol.CommentMultipleCheckmark);
					xamlUserType34.AddEnumValue("CommentMultipleLink", FluentIcons.Common.Symbol.CommentMultipleLink);
					xamlUserType34.AddEnumValue("CommentMultipleMention", FluentIcons.Common.Symbol.CommentMultipleMention);
					xamlUserType34.AddEnumValue("CommentNote", FluentIcons.Common.Symbol.CommentNote);
					xamlUserType34.AddEnumValue("CommentOff", FluentIcons.Common.Symbol.CommentOff);
					xamlUserType34.AddEnumValue("CommentQuote", FluentIcons.Common.Symbol.CommentQuote);
					xamlUserType34.AddEnumValue("CommentText", FluentIcons.Common.Symbol.CommentText);
					xamlUserType34.AddEnumValue("Communication", FluentIcons.Common.Symbol.Communication);
					xamlUserType34.AddEnumValue("CommunicationPerson", FluentIcons.Common.Symbol.CommunicationPerson);
					xamlUserType34.AddEnumValue("CommunicationShield", FluentIcons.Common.Symbol.CommunicationShield);
					xamlUserType34.AddEnumValue("CompassNorthwest", FluentIcons.Common.Symbol.CompassNorthwest);
					xamlUserType34.AddEnumValue("Compose", FluentIcons.Common.Symbol.Compose);
					xamlUserType34.AddEnumValue("ConferenceRoom", FluentIcons.Common.Symbol.ConferenceRoom);
					xamlUserType34.AddEnumValue("Connected", FluentIcons.Common.Symbol.Connected);
					xamlUserType34.AddEnumValue("Connector", FluentIcons.Common.Symbol.Connector);
					xamlUserType34.AddEnumValue("ContactCard", FluentIcons.Common.Symbol.ContactCard);
					xamlUserType34.AddEnumValue("ContactCardGroup", FluentIcons.Common.Symbol.ContactCardGroup);
					xamlUserType34.AddEnumValue("ContactCardLink", FluentIcons.Common.Symbol.ContactCardLink);
					xamlUserType34.AddEnumValue("ContactCardRibbon", FluentIcons.Common.Symbol.ContactCardRibbon);
					xamlUserType34.AddEnumValue("ContentSettings", FluentIcons.Common.Symbol.ContentSettings);
					xamlUserType34.AddEnumValue("ContentView", FluentIcons.Common.Symbol.ContentView);
					xamlUserType34.AddEnumValue("ContentViewGallery", FluentIcons.Common.Symbol.ContentViewGallery);
					xamlUserType34.AddEnumValue("ContentViewGalleryLightning", FluentIcons.Common.Symbol.ContentViewGalleryLightning);
					xamlUserType34.AddEnumValue("ContractDownLeft", FluentIcons.Common.Symbol.ContractDownLeft);
					xamlUserType34.AddEnumValue("ContractUpRight", FluentIcons.Common.Symbol.ContractUpRight);
					xamlUserType34.AddEnumValue("ControlButton", FluentIcons.Common.Symbol.ControlButton);
					xamlUserType34.AddEnumValue("ConvertRange", FluentIcons.Common.Symbol.ConvertRange);
					xamlUserType34.AddEnumValue("Cookies", FluentIcons.Common.Symbol.Cookies);
					xamlUserType34.AddEnumValue("Copy", FluentIcons.Common.Symbol.Copy);
					xamlUserType34.AddEnumValue("CopyAdd", FluentIcons.Common.Symbol.CopyAdd);
					xamlUserType34.AddEnumValue("CopyArrowRight", FluentIcons.Common.Symbol.CopyArrowRight);
					xamlUserType34.AddEnumValue("CopySelect", FluentIcons.Common.Symbol.CopySelect);
					xamlUserType34.AddEnumValue("Couch", FluentIcons.Common.Symbol.Couch);
					xamlUserType34.AddEnumValue("CreditCardClock", FluentIcons.Common.Symbol.CreditCardClock);
					xamlUserType34.AddEnumValue("CreditCardPerson", FluentIcons.Common.Symbol.CreditCardPerson);
					xamlUserType34.AddEnumValue("CreditCardToolbox", FluentIcons.Common.Symbol.CreditCardToolbox);
					xamlUserType34.AddEnumValue("Crop", FluentIcons.Common.Symbol.Crop);
					xamlUserType34.AddEnumValue("CropArrowRotate", FluentIcons.Common.Symbol.CropArrowRotate);
					xamlUserType34.AddEnumValue("CropInterim", FluentIcons.Common.Symbol.CropInterim);
					xamlUserType34.AddEnumValue("CropInterimOff", FluentIcons.Common.Symbol.CropInterimOff);
					xamlUserType34.AddEnumValue("Crown", FluentIcons.Common.Symbol.Crown);
					xamlUserType34.AddEnumValue("CrownSubtract", FluentIcons.Common.Symbol.CrownSubtract);
					xamlUserType34.AddEnumValue("Cube", FluentIcons.Common.Symbol.Cube);
					xamlUserType34.AddEnumValue("CubeAdd", FluentIcons.Common.Symbol.CubeAdd);
					xamlUserType34.AddEnumValue("CubeArrowCurveDown", FluentIcons.Common.Symbol.CubeArrowCurveDown);
					xamlUserType34.AddEnumValue("CubeLink", FluentIcons.Common.Symbol.CubeLink);
					xamlUserType34.AddEnumValue("CubeMultiple", FluentIcons.Common.Symbol.CubeMultiple);
					xamlUserType34.AddEnumValue("CubeQuick", FluentIcons.Common.Symbol.CubeQuick);
					xamlUserType34.AddEnumValue("CubeRotate", FluentIcons.Common.Symbol.CubeRotate);
					xamlUserType34.AddEnumValue("CubeSync", FluentIcons.Common.Symbol.CubeSync);
					xamlUserType34.AddEnumValue("CubeTree", FluentIcons.Common.Symbol.CubeTree);
					xamlUserType34.AddEnumValue("CurrencyDollarEuro", FluentIcons.Common.Symbol.CurrencyDollarEuro);
					xamlUserType34.AddEnumValue("CurrencyDollarRupee", FluentIcons.Common.Symbol.CurrencyDollarRupee);
					xamlUserType34.AddEnumValue("Cursor", FluentIcons.Common.Symbol.Cursor);
					xamlUserType34.AddEnumValue("CursorClick", FluentIcons.Common.Symbol.CursorClick);
					xamlUserType34.AddEnumValue("CursorHover", FluentIcons.Common.Symbol.CursorHover);
					xamlUserType34.AddEnumValue("CursorHoverOff", FluentIcons.Common.Symbol.CursorHoverOff);
					xamlUserType34.AddEnumValue("CursorProhibited", FluentIcons.Common.Symbol.CursorProhibited);
					xamlUserType34.AddEnumValue("Cut", FluentIcons.Common.Symbol.Cut);
					xamlUserType34.AddEnumValue("DarkTheme", FluentIcons.Common.Symbol.DarkTheme);
					xamlUserType34.AddEnumValue("DataArea", FluentIcons.Common.Symbol.DataArea);
					xamlUserType34.AddEnumValue("DataBarHorizontal", FluentIcons.Common.Symbol.DataBarHorizontal);
					xamlUserType34.AddEnumValue("DataBarVertical", FluentIcons.Common.Symbol.DataBarVertical);
					xamlUserType34.AddEnumValue("DataBarVerticalAdd", FluentIcons.Common.Symbol.DataBarVerticalAdd);
					xamlUserType34.AddEnumValue("DataBarVerticalArrowDown", FluentIcons.Common.Symbol.DataBarVerticalArrowDown);
					xamlUserType34.AddEnumValue("DataBarVerticalAscending", FluentIcons.Common.Symbol.DataBarVerticalAscending);
					xamlUserType34.AddEnumValue("DataBarVerticalStar", FluentIcons.Common.Symbol.DataBarVerticalStar);
					xamlUserType34.AddEnumValue("DataFunnel", FluentIcons.Common.Symbol.DataFunnel);
					xamlUserType34.AddEnumValue("DataHistogram", FluentIcons.Common.Symbol.DataHistogram);
					xamlUserType34.AddEnumValue("DataLine", FluentIcons.Common.Symbol.DataLine);
					xamlUserType34.AddEnumValue("DataPie", FluentIcons.Common.Symbol.DataPie);
					xamlUserType34.AddEnumValue("DataScatter", FluentIcons.Common.Symbol.DataScatter);
					xamlUserType34.AddEnumValue("DataSunburst", FluentIcons.Common.Symbol.DataSunburst);
					xamlUserType34.AddEnumValue("DataTreemap", FluentIcons.Common.Symbol.DataTreemap);
					xamlUserType34.AddEnumValue("DataTrending", FluentIcons.Common.Symbol.DataTrending);
					xamlUserType34.AddEnumValue("DataUsage", FluentIcons.Common.Symbol.DataUsage);
					xamlUserType34.AddEnumValue("DataUsageCheckmark", FluentIcons.Common.Symbol.DataUsageCheckmark);
					xamlUserType34.AddEnumValue("DataUsageEdit", FluentIcons.Common.Symbol.DataUsageEdit);
					xamlUserType34.AddEnumValue("DataUsageSettings", FluentIcons.Common.Symbol.DataUsageSettings);
					xamlUserType34.AddEnumValue("DataUsageSparkle", FluentIcons.Common.Symbol.DataUsageSparkle);
					xamlUserType34.AddEnumValue("DataUsageToolbox", FluentIcons.Common.Symbol.DataUsageToolbox);
					xamlUserType34.AddEnumValue("DataWaterfall", FluentIcons.Common.Symbol.DataWaterfall);
					xamlUserType34.AddEnumValue("DataWhisker", FluentIcons.Common.Symbol.DataWhisker);
					xamlUserType34.AddEnumValue("Database", FluentIcons.Common.Symbol.Database);
					xamlUserType34.AddEnumValue("DatabaseArrowDown", FluentIcons.Common.Symbol.DatabaseArrowDown);
					xamlUserType34.AddEnumValue("DatabaseArrowRight", FluentIcons.Common.Symbol.DatabaseArrowRight);
					xamlUserType34.AddEnumValue("DatabaseArrowUp", FluentIcons.Common.Symbol.DatabaseArrowUp);
					xamlUserType34.AddEnumValue("DatabaseCheckmark", FluentIcons.Common.Symbol.DatabaseCheckmark);
					xamlUserType34.AddEnumValue("DatabaseLightning", FluentIcons.Common.Symbol.DatabaseLightning);
					xamlUserType34.AddEnumValue("DatabaseLink", FluentIcons.Common.Symbol.DatabaseLink);
					xamlUserType34.AddEnumValue("DatabaseMultiple", FluentIcons.Common.Symbol.DatabaseMultiple);
					xamlUserType34.AddEnumValue("DatabasePerson", FluentIcons.Common.Symbol.DatabasePerson);
					xamlUserType34.AddEnumValue("DatabasePlugConnected", FluentIcons.Common.Symbol.DatabasePlugConnected);
					xamlUserType34.AddEnumValue("DatabaseSearch", FluentIcons.Common.Symbol.DatabaseSearch);
					xamlUserType34.AddEnumValue("DatabaseSwitch", FluentIcons.Common.Symbol.DatabaseSwitch);
					xamlUserType34.AddEnumValue("DatabaseWarning", FluentIcons.Common.Symbol.DatabaseWarning);
					xamlUserType34.AddEnumValue("DatabaseWindow", FluentIcons.Common.Symbol.DatabaseWindow);
					xamlUserType34.AddEnumValue("DecimalArrowLeft", FluentIcons.Common.Symbol.DecimalArrowLeft);
					xamlUserType34.AddEnumValue("DecimalArrowRight", FluentIcons.Common.Symbol.DecimalArrowRight);
					xamlUserType34.AddEnumValue("Delete", FluentIcons.Common.Symbol.Delete);
					xamlUserType34.AddEnumValue("DeleteArrowBack", FluentIcons.Common.Symbol.DeleteArrowBack);
					xamlUserType34.AddEnumValue("DeleteDismiss", FluentIcons.Common.Symbol.DeleteDismiss);
					xamlUserType34.AddEnumValue("DeleteLines", FluentIcons.Common.Symbol.DeleteLines);
					xamlUserType34.AddEnumValue("DeleteOff", FluentIcons.Common.Symbol.DeleteOff);
					xamlUserType34.AddEnumValue("Dentist", FluentIcons.Common.Symbol.Dentist);
					xamlUserType34.AddEnumValue("DesignIdeas", FluentIcons.Common.Symbol.DesignIdeas);
					xamlUserType34.AddEnumValue("Desk", FluentIcons.Common.Symbol.Desk);
					xamlUserType34.AddEnumValue("DeskMultiple", FluentIcons.Common.Symbol.DeskMultiple);
					xamlUserType34.AddEnumValue("Desktop", FluentIcons.Common.Symbol.Desktop);
					xamlUserType34.AddEnumValue("DesktopArrowDown", FluentIcons.Common.Symbol.DesktopArrowDown);
					xamlUserType34.AddEnumValue("DesktopArrowDownOff", FluentIcons.Common.Symbol.DesktopArrowDownOff);
					xamlUserType34.AddEnumValue("DesktopArrowRight", FluentIcons.Common.Symbol.DesktopArrowRight);
					xamlUserType34.AddEnumValue("DesktopCheckmark", FluentIcons.Common.Symbol.DesktopCheckmark);
					xamlUserType34.AddEnumValue("DesktopCursor", FluentIcons.Common.Symbol.DesktopCursor);
					xamlUserType34.AddEnumValue("DesktopEdit", FluentIcons.Common.Symbol.DesktopEdit);
					xamlUserType34.AddEnumValue("DesktopFlow", FluentIcons.Common.Symbol.DesktopFlow);
					xamlUserType34.AddEnumValue("DesktopKeyboard", FluentIcons.Common.Symbol.DesktopKeyboard);
					xamlUserType34.AddEnumValue("DesktopMac", FluentIcons.Common.Symbol.DesktopMac);
					xamlUserType34.AddEnumValue("DesktopOff", FluentIcons.Common.Symbol.DesktopOff);
					xamlUserType34.AddEnumValue("DesktopPulse", FluentIcons.Common.Symbol.DesktopPulse);
					xamlUserType34.AddEnumValue("DesktopSignal", FluentIcons.Common.Symbol.DesktopSignal);
					xamlUserType34.AddEnumValue("DesktopSpeaker", FluentIcons.Common.Symbol.DesktopSpeaker);
					xamlUserType34.AddEnumValue("DesktopSpeakerOff", FluentIcons.Common.Symbol.DesktopSpeakerOff);
					xamlUserType34.AddEnumValue("DesktopSync", FluentIcons.Common.Symbol.DesktopSync);
					xamlUserType34.AddEnumValue("DesktopToolbox", FluentIcons.Common.Symbol.DesktopToolbox);
					xamlUserType34.AddEnumValue("DesktopTower", FluentIcons.Common.Symbol.DesktopTower);
					xamlUserType34.AddEnumValue("DeveloperBoard", FluentIcons.Common.Symbol.DeveloperBoard);
					xamlUserType34.AddEnumValue("DeveloperBoardLightning", FluentIcons.Common.Symbol.DeveloperBoardLightning);
					xamlUserType34.AddEnumValue("DeveloperBoardLightningToolbox", FluentIcons.Common.Symbol.DeveloperBoardLightningToolbox);
					xamlUserType34.AddEnumValue("DeveloperBoardSearch", FluentIcons.Common.Symbol.DeveloperBoardSearch);
					xamlUserType34.AddEnumValue("DeviceEq", FluentIcons.Common.Symbol.DeviceEq);
					xamlUserType34.AddEnumValue("DeviceMeetingRoom", FluentIcons.Common.Symbol.DeviceMeetingRoom);
					xamlUserType34.AddEnumValue("DeviceMeetingRoomRemote", FluentIcons.Common.Symbol.DeviceMeetingRoomRemote);
					xamlUserType34.AddEnumValue("Diagram", FluentIcons.Common.Symbol.Diagram);
					xamlUserType34.AddEnumValue("Dialpad", FluentIcons.Common.Symbol.Dialpad);
					xamlUserType34.AddEnumValue("DialpadOff", FluentIcons.Common.Symbol.DialpadOff);
					xamlUserType34.AddEnumValue("DialpadQuestionMark", FluentIcons.Common.Symbol.DialpadQuestionMark);
					xamlUserType34.AddEnumValue("Diamond", FluentIcons.Common.Symbol.Diamond);
					xamlUserType34.AddEnumValue("DiamondDismiss", FluentIcons.Common.Symbol.DiamondDismiss);
					xamlUserType34.AddEnumValue("Directions", FluentIcons.Common.Symbol.Directions);
					xamlUserType34.AddEnumValue("Dishwasher", FluentIcons.Common.Symbol.Dishwasher);
					xamlUserType34.AddEnumValue("Dismiss", FluentIcons.Common.Symbol.Dismiss);
					xamlUserType34.AddEnumValue("DismissCircle", FluentIcons.Common.Symbol.DismissCircle);
					xamlUserType34.AddEnumValue("DismissSquare", FluentIcons.Common.Symbol.DismissSquare);
					xamlUserType34.AddEnumValue("DismissSquareMultiple", FluentIcons.Common.Symbol.DismissSquareMultiple);
					xamlUserType34.AddEnumValue("Diversity", FluentIcons.Common.Symbol.Diversity);
					xamlUserType34.AddEnumValue("DividerShort", FluentIcons.Common.Symbol.DividerShort);
					xamlUserType34.AddEnumValue("DividerTall", FluentIcons.Common.Symbol.DividerTall);
					xamlUserType34.AddEnumValue("Dock", FluentIcons.Common.Symbol.Dock);
					xamlUserType34.AddEnumValue("DockRow", FluentIcons.Common.Symbol.DockRow);
					xamlUserType34.AddEnumValue("Doctor", FluentIcons.Common.Symbol.Doctor);
					xamlUserType34.AddEnumValue("Document", FluentIcons.Common.Symbol.Document);
					xamlUserType34.AddEnumValue("Document100", FluentIcons.Common.Symbol.Document100);
					xamlUserType34.AddEnumValue("DocumentAdd", FluentIcons.Common.Symbol.DocumentAdd);
					xamlUserType34.AddEnumValue("DocumentArrowDown", FluentIcons.Common.Symbol.DocumentArrowDown);
					xamlUserType34.AddEnumValue("DocumentArrowLeft", FluentIcons.Common.Symbol.DocumentArrowLeft);
					xamlUserType34.AddEnumValue("DocumentArrowRight", FluentIcons.Common.Symbol.DocumentArrowRight);
					xamlUserType34.AddEnumValue("DocumentArrowUp", FluentIcons.Common.Symbol.DocumentArrowUp);
					xamlUserType34.AddEnumValue("DocumentBorder", FluentIcons.Common.Symbol.DocumentBorder);
					xamlUserType34.AddEnumValue("DocumentBorderPrint", FluentIcons.Common.Symbol.DocumentBorderPrint);
					xamlUserType34.AddEnumValue("DocumentBriefcase", FluentIcons.Common.Symbol.DocumentBriefcase);
					xamlUserType34.AddEnumValue("DocumentBulletList", FluentIcons.Common.Symbol.DocumentBulletList);
					xamlUserType34.AddEnumValue("DocumentBulletListArrowLeft", FluentIcons.Common.Symbol.DocumentBulletListArrowLeft);
					xamlUserType34.AddEnumValue("DocumentBulletListClock", FluentIcons.Common.Symbol.DocumentBulletListClock);
					xamlUserType34.AddEnumValue("DocumentBulletListCube", FluentIcons.Common.Symbol.DocumentBulletListCube);
					xamlUserType34.AddEnumValue("DocumentBulletListMultiple", FluentIcons.Common.Symbol.DocumentBulletListMultiple);
					xamlUserType34.AddEnumValue("DocumentBulletListOff", FluentIcons.Common.Symbol.DocumentBulletListOff);
					xamlUserType34.AddEnumValue("DocumentCatchUp", FluentIcons.Common.Symbol.DocumentCatchUp);
					xamlUserType34.AddEnumValue("DocumentCheckmark", FluentIcons.Common.Symbol.DocumentCheckmark);
					xamlUserType34.AddEnumValue("DocumentChevronDouble", FluentIcons.Common.Symbol.DocumentChevronDouble);
					xamlUserType34.AddEnumValue("DocumentCopy", FluentIcons.Common.Symbol.DocumentCopy);
					xamlUserType34.AddEnumValue("DocumentCss", FluentIcons.Common.Symbol.DocumentCss);
					xamlUserType34.AddEnumValue("DocumentCube", FluentIcons.Common.Symbol.DocumentCube);
					xamlUserType34.AddEnumValue("DocumentData", FluentIcons.Common.Symbol.DocumentData);
					xamlUserType34.AddEnumValue("DocumentDataLink", FluentIcons.Common.Symbol.DocumentDataLink);
					xamlUserType34.AddEnumValue("DocumentDataLock", FluentIcons.Common.Symbol.DocumentDataLock);
					xamlUserType34.AddEnumValue("DocumentDatabase", FluentIcons.Common.Symbol.DocumentDatabase);
					xamlUserType34.AddEnumValue("DocumentDismiss", FluentIcons.Common.Symbol.DocumentDismiss);
					xamlUserType34.AddEnumValue("DocumentEdit", FluentIcons.Common.Symbol.DocumentEdit);
					xamlUserType34.AddEnumValue("DocumentEndnote", FluentIcons.Common.Symbol.DocumentEndnote);
					xamlUserType34.AddEnumValue("DocumentError", FluentIcons.Common.Symbol.DocumentError);
					xamlUserType34.AddEnumValue("DocumentFit", FluentIcons.Common.Symbol.DocumentFit);
					xamlUserType34.AddEnumValue("DocumentFlowchart", FluentIcons.Common.Symbol.DocumentFlowchart);
					xamlUserType34.AddEnumValue("DocumentFolder", FluentIcons.Common.Symbol.DocumentFolder);
					xamlUserType34.AddEnumValue("DocumentFooter", FluentIcons.Common.Symbol.DocumentFooter);
					xamlUserType34.AddEnumValue("DocumentFooterDismiss", FluentIcons.Common.Symbol.DocumentFooterDismiss);
					xamlUserType34.AddEnumValue("DocumentGlobe", FluentIcons.Common.Symbol.DocumentGlobe);
					xamlUserType34.AddEnumValue("DocumentHeader", FluentIcons.Common.Symbol.DocumentHeader);
					xamlUserType34.AddEnumValue("DocumentHeaderArrowDown", FluentIcons.Common.Symbol.DocumentHeaderArrowDown);
					xamlUserType34.AddEnumValue("DocumentHeaderDismiss", FluentIcons.Common.Symbol.DocumentHeaderDismiss);
					xamlUserType34.AddEnumValue("DocumentHeaderFooter", FluentIcons.Common.Symbol.DocumentHeaderFooter);
					xamlUserType34.AddEnumValue("DocumentHeart", FluentIcons.Common.Symbol.DocumentHeart);
					xamlUserType34.AddEnumValue("DocumentHeartPulse", FluentIcons.Common.Symbol.DocumentHeartPulse);
					xamlUserType34.AddEnumValue("DocumentImage", FluentIcons.Common.Symbol.DocumentImage);
					xamlUserType34.AddEnumValue("DocumentJava", FluentIcons.Common.Symbol.DocumentJava);
					xamlUserType34.AddEnumValue("DocumentJavascript", FluentIcons.Common.Symbol.DocumentJavascript);
					xamlUserType34.AddEnumValue("DocumentKey", FluentIcons.Common.Symbol.DocumentKey);
					xamlUserType34.AddEnumValue("DocumentLandscape", FluentIcons.Common.Symbol.DocumentLandscape);
					xamlUserType34.AddEnumValue("DocumentLandscapeData", FluentIcons.Common.Symbol.DocumentLandscapeData);
					xamlUserType34.AddEnumValue("DocumentLandscapeSplit", FluentIcons.Common.Symbol.DocumentLandscapeSplit);
					xamlUserType34.AddEnumValue("DocumentLandscapeSplitHint", FluentIcons.Common.Symbol.DocumentLandscapeSplitHint);
					xamlUserType34.AddEnumValue("DocumentLightning", FluentIcons.Common.Symbol.DocumentLightning);
					xamlUserType34.AddEnumValue("DocumentLink", FluentIcons.Common.Symbol.DocumentLink);
					xamlUserType34.AddEnumValue("DocumentLock", FluentIcons.Common.Symbol.DocumentLock);
					xamlUserType34.AddEnumValue("DocumentMargins", FluentIcons.Common.Symbol.DocumentMargins);
					xamlUserType34.AddEnumValue("DocumentMention", FluentIcons.Common.Symbol.DocumentMention);
					xamlUserType34.AddEnumValue("DocumentMultiple", FluentIcons.Common.Symbol.DocumentMultiple);
					xamlUserType34.AddEnumValue("DocumentMultiplePercent", FluentIcons.Common.Symbol.DocumentMultiplePercent);
					xamlUserType34.AddEnumValue("DocumentMultipleProhibited", FluentIcons.Common.Symbol.DocumentMultipleProhibited);
					xamlUserType34.AddEnumValue("DocumentMultipleSync", FluentIcons.Common.Symbol.DocumentMultipleSync);
					xamlUserType34.AddEnumValue("DocumentOnePage", FluentIcons.Common.Symbol.DocumentOnePage);
					xamlUserType34.AddEnumValue("DocumentOnePageAdd", FluentIcons.Common.Symbol.DocumentOnePageAdd);
					xamlUserType34.AddEnumValue("DocumentOnePageColumns", FluentIcons.Common.Symbol.DocumentOnePageColumns);
					xamlUserType34.AddEnumValue("DocumentOnePageLink", FluentIcons.Common.Symbol.DocumentOnePageLink);
					xamlUserType34.AddEnumValue("DocumentOnePageMultiple", FluentIcons.Common.Symbol.DocumentOnePageMultiple);
					xamlUserType34.AddEnumValue("DocumentOnePageMultipleSparkle", FluentIcons.Common.Symbol.DocumentOnePageMultipleSparkle);
					xamlUserType34.AddEnumValue("DocumentOnePageSparkle", FluentIcons.Common.Symbol.DocumentOnePageSparkle);
					xamlUserType34.AddEnumValue("DocumentPageBottomCenter", FluentIcons.Common.Symbol.DocumentPageBottomCenter);
					xamlUserType34.AddEnumValue("DocumentPageBottomLeft", FluentIcons.Common.Symbol.DocumentPageBottomLeft);
					xamlUserType34.AddEnumValue("DocumentPageBottomRight", FluentIcons.Common.Symbol.DocumentPageBottomRight);
					xamlUserType34.AddEnumValue("DocumentPageBreak", FluentIcons.Common.Symbol.DocumentPageBreak);
					xamlUserType34.AddEnumValue("DocumentPageNumber", FluentIcons.Common.Symbol.DocumentPageNumber);
					xamlUserType34.AddEnumValue("DocumentPageTopCenter", FluentIcons.Common.Symbol.DocumentPageTopCenter);
					xamlUserType34.AddEnumValue("DocumentPageTopLeft", FluentIcons.Common.Symbol.DocumentPageTopLeft);
					xamlUserType34.AddEnumValue("DocumentPageTopRight", FluentIcons.Common.Symbol.DocumentPageTopRight);
					xamlUserType34.AddEnumValue("DocumentPdf", FluentIcons.Common.Symbol.DocumentPdf);
					xamlUserType34.AddEnumValue("DocumentPercent", FluentIcons.Common.Symbol.DocumentPercent);
					xamlUserType34.AddEnumValue("DocumentPerson", FluentIcons.Common.Symbol.DocumentPerson);
					xamlUserType34.AddEnumValue("DocumentPill", FluentIcons.Common.Symbol.DocumentPill);
					xamlUserType34.AddEnumValue("DocumentPrint", FluentIcons.Common.Symbol.DocumentPrint);
					xamlUserType34.AddEnumValue("DocumentProhibited", FluentIcons.Common.Symbol.DocumentProhibited);
					xamlUserType34.AddEnumValue("DocumentQuestionMark", FluentIcons.Common.Symbol.DocumentQuestionMark);
					xamlUserType34.AddEnumValue("DocumentQueue", FluentIcons.Common.Symbol.DocumentQueue);
					xamlUserType34.AddEnumValue("DocumentQueueAdd", FluentIcons.Common.Symbol.DocumentQueueAdd);
					xamlUserType34.AddEnumValue("DocumentQueueMultiple", FluentIcons.Common.Symbol.DocumentQueueMultiple);
					xamlUserType34.AddEnumValue("DocumentRibbon", FluentIcons.Common.Symbol.DocumentRibbon);
					xamlUserType34.AddEnumValue("DocumentSass", FluentIcons.Common.Symbol.DocumentSass);
					xamlUserType34.AddEnumValue("DocumentSave", FluentIcons.Common.Symbol.DocumentSave);
					xamlUserType34.AddEnumValue("DocumentSearch", FluentIcons.Common.Symbol.DocumentSearch);
					xamlUserType34.AddEnumValue("DocumentSettings", FluentIcons.Common.Symbol.DocumentSettings);
					xamlUserType34.AddEnumValue("DocumentSignature", FluentIcons.Common.Symbol.DocumentSignature);
					xamlUserType34.AddEnumValue("DocumentSparkle", FluentIcons.Common.Symbol.DocumentSparkle);
					xamlUserType34.AddEnumValue("DocumentSplitHint", FluentIcons.Common.Symbol.DocumentSplitHint);
					xamlUserType34.AddEnumValue("DocumentSplitHintOff", FluentIcons.Common.Symbol.DocumentSplitHintOff);
					xamlUserType34.AddEnumValue("DocumentSync", FluentIcons.Common.Symbol.DocumentSync);
					xamlUserType34.AddEnumValue("DocumentTable", FluentIcons.Common.Symbol.DocumentTable);
					xamlUserType34.AddEnumValue("DocumentTableArrowRight", FluentIcons.Common.Symbol.DocumentTableArrowRight);
					xamlUserType34.AddEnumValue("DocumentTableCheckmark", FluentIcons.Common.Symbol.DocumentTableCheckmark);
					xamlUserType34.AddEnumValue("DocumentTableCube", FluentIcons.Common.Symbol.DocumentTableCube);
					xamlUserType34.AddEnumValue("DocumentTableSearch", FluentIcons.Common.Symbol.DocumentTableSearch);
					xamlUserType34.AddEnumValue("DocumentTableTruck", FluentIcons.Common.Symbol.DocumentTableTruck);
					xamlUserType34.AddEnumValue("DocumentTarget", FluentIcons.Common.Symbol.DocumentTarget);
					xamlUserType34.AddEnumValue("DocumentText", FluentIcons.Common.Symbol.DocumentText);
					xamlUserType34.AddEnumValue("DocumentTextClock", FluentIcons.Common.Symbol.DocumentTextClock);
					xamlUserType34.AddEnumValue("DocumentTextExtract", FluentIcons.Common.Symbol.DocumentTextExtract);
					xamlUserType34.AddEnumValue("DocumentTextLink", FluentIcons.Common.Symbol.DocumentTextLink);
					xamlUserType34.AddEnumValue("DocumentTextToolbox", FluentIcons.Common.Symbol.DocumentTextToolbox);
					xamlUserType34.AddEnumValue("DocumentToolbox", FluentIcons.Common.Symbol.DocumentToolbox);
					xamlUserType34.AddEnumValue("DocumentWidth", FluentIcons.Common.Symbol.DocumentWidth);
					xamlUserType34.AddEnumValue("DocumentYml", FluentIcons.Common.Symbol.DocumentYml);
					xamlUserType34.AddEnumValue("Door", FluentIcons.Common.Symbol.Door);
					xamlUserType34.AddEnumValue("DoorArrowLeft", FluentIcons.Common.Symbol.DoorArrowLeft);
					xamlUserType34.AddEnumValue("DoorArrowRight", FluentIcons.Common.Symbol.DoorArrowRight);
					xamlUserType34.AddEnumValue("DoorTag", FluentIcons.Common.Symbol.DoorTag);
					xamlUserType34.AddEnumValue("DoubleSwipeDown", FluentIcons.Common.Symbol.DoubleSwipeDown);
					xamlUserType34.AddEnumValue("DoubleSwipeUp", FluentIcons.Common.Symbol.DoubleSwipeUp);
					xamlUserType34.AddEnumValue("DoubleTapSwipeDown", FluentIcons.Common.Symbol.DoubleTapSwipeDown);
					xamlUserType34.AddEnumValue("DoubleTapSwipeUp", FluentIcons.Common.Symbol.DoubleTapSwipeUp);
					xamlUserType34.AddEnumValue("Drafts", FluentIcons.Common.Symbol.Drafts);
					xamlUserType34.AddEnumValue("Drag", FluentIcons.Common.Symbol.Drag);
					xamlUserType34.AddEnumValue("DrawImage", FluentIcons.Common.Symbol.DrawImage);
					xamlUserType34.AddEnumValue("DrawShape", FluentIcons.Common.Symbol.DrawShape);
					xamlUserType34.AddEnumValue("DrawText", FluentIcons.Common.Symbol.DrawText);
					xamlUserType34.AddEnumValue("Drawer", FluentIcons.Common.Symbol.Drawer);
					xamlUserType34.AddEnumValue("DrawerAdd", FluentIcons.Common.Symbol.DrawerAdd);
					xamlUserType34.AddEnumValue("DrawerArrowDownload", FluentIcons.Common.Symbol.DrawerArrowDownload);
					xamlUserType34.AddEnumValue("DrawerDismiss", FluentIcons.Common.Symbol.DrawerDismiss);
					xamlUserType34.AddEnumValue("DrawerPlay", FluentIcons.Common.Symbol.DrawerPlay);
					xamlUserType34.AddEnumValue("DrawerSubtract", FluentIcons.Common.Symbol.DrawerSubtract);
					xamlUserType34.AddEnumValue("DrinkBeer", FluentIcons.Common.Symbol.DrinkBeer);
					xamlUserType34.AddEnumValue("DrinkBottle", FluentIcons.Common.Symbol.DrinkBottle);
					xamlUserType34.AddEnumValue("DrinkBottleOff", FluentIcons.Common.Symbol.DrinkBottleOff);
					xamlUserType34.AddEnumValue("DrinkCoffee", FluentIcons.Common.Symbol.DrinkCoffee);
					xamlUserType34.AddEnumValue("DrinkMargarita", FluentIcons.Common.Symbol.DrinkMargarita);
					xamlUserType34.AddEnumValue("DrinkToGo", FluentIcons.Common.Symbol.DrinkToGo);
					xamlUserType34.AddEnumValue("DrinkWine", FluentIcons.Common.Symbol.DrinkWine);
					xamlUserType34.AddEnumValue("DriveTrain", FluentIcons.Common.Symbol.DriveTrain);
					xamlUserType34.AddEnumValue("Drop", FluentIcons.Common.Symbol.Drop);
					xamlUserType34.AddEnumValue("DualScreen", FluentIcons.Common.Symbol.DualScreen);
					xamlUserType34.AddEnumValue("DualScreenAdd", FluentIcons.Common.Symbol.DualScreenAdd);
					xamlUserType34.AddEnumValue("DualScreenArrowRight", FluentIcons.Common.Symbol.DualScreenArrowRight);
					xamlUserType34.AddEnumValue("DualScreenArrowUp", FluentIcons.Common.Symbol.DualScreenArrowUp);
					xamlUserType34.AddEnumValue("DualScreenClock", FluentIcons.Common.Symbol.DualScreenClock);
					xamlUserType34.AddEnumValue("DualScreenClosedAlert", FluentIcons.Common.Symbol.DualScreenClosedAlert);
					xamlUserType34.AddEnumValue("DualScreenDesktop", FluentIcons.Common.Symbol.DualScreenDesktop);
					xamlUserType34.AddEnumValue("DualScreenDismiss", FluentIcons.Common.Symbol.DualScreenDismiss);
					xamlUserType34.AddEnumValue("DualScreenGroup", FluentIcons.Common.Symbol.DualScreenGroup);
					xamlUserType34.AddEnumValue("DualScreenHeader", FluentIcons.Common.Symbol.DualScreenHeader);
					xamlUserType34.AddEnumValue("DualScreenLock", FluentIcons.Common.Symbol.DualScreenLock);
					xamlUserType34.AddEnumValue("DualScreenMirror", FluentIcons.Common.Symbol.DualScreenMirror);
					xamlUserType34.AddEnumValue("DualScreenPagination", FluentIcons.Common.Symbol.DualScreenPagination);
					xamlUserType34.AddEnumValue("DualScreenSettings", FluentIcons.Common.Symbol.DualScreenSettings);
					xamlUserType34.AddEnumValue("DualScreenSpan", FluentIcons.Common.Symbol.DualScreenSpan);
					xamlUserType34.AddEnumValue("DualScreenSpeaker", FluentIcons.Common.Symbol.DualScreenSpeaker);
					xamlUserType34.AddEnumValue("DualScreenStatusBar", FluentIcons.Common.Symbol.DualScreenStatusBar);
					xamlUserType34.AddEnumValue("DualScreenTablet", FluentIcons.Common.Symbol.DualScreenTablet);
					xamlUserType34.AddEnumValue("DualScreenUpdate", FluentIcons.Common.Symbol.DualScreenUpdate);
					xamlUserType34.AddEnumValue("DualScreenVerticalScroll", FluentIcons.Common.Symbol.DualScreenVerticalScroll);
					xamlUserType34.AddEnumValue("DualScreenVibrate", FluentIcons.Common.Symbol.DualScreenVibrate);
					xamlUserType34.AddEnumValue("Dumbbell", FluentIcons.Common.Symbol.Dumbbell);
					xamlUserType34.AddEnumValue("Dust", FluentIcons.Common.Symbol.Dust);
					xamlUserType34.AddEnumValue("Earth", FluentIcons.Common.Symbol.Earth);
					xamlUserType34.AddEnumValue("EarthLeaf", FluentIcons.Common.Symbol.EarthLeaf);
					xamlUserType34.AddEnumValue("Edit", FluentIcons.Common.Symbol.Edit);
					xamlUserType34.AddEnumValue("EditArrowBack", FluentIcons.Common.Symbol.EditArrowBack);
					xamlUserType34.AddEnumValue("EditLineHorizontal3", FluentIcons.Common.Symbol.EditLineHorizontal3);
					xamlUserType34.AddEnumValue("EditLock", FluentIcons.Common.Symbol.EditLock);
					xamlUserType34.AddEnumValue("EditOff", FluentIcons.Common.Symbol.EditOff);
					xamlUserType34.AddEnumValue("EditPerson", FluentIcons.Common.Symbol.EditPerson);
					xamlUserType34.AddEnumValue("EditProhibited", FluentIcons.Common.Symbol.EditProhibited);
					xamlUserType34.AddEnumValue("EditSettings", FluentIcons.Common.Symbol.EditSettings);
					xamlUserType34.AddEnumValue("Elevator", FluentIcons.Common.Symbol.Elevator);
					xamlUserType34.AddEnumValue("Emoji", FluentIcons.Common.Symbol.Emoji);
					xamlUserType34.AddEnumValue("EmojiAdd", FluentIcons.Common.Symbol.EmojiAdd);
					xamlUserType34.AddEnumValue("EmojiAngry", FluentIcons.Common.Symbol.EmojiAngry);
					xamlUserType34.AddEnumValue("EmojiEdit", FluentIcons.Common.Symbol.EmojiEdit);
					xamlUserType34.AddEnumValue("EmojiHand", FluentIcons.Common.Symbol.EmojiHand);
					xamlUserType34.AddEnumValue("EmojiHint", FluentIcons.Common.Symbol.EmojiHint);
					xamlUserType34.AddEnumValue("EmojiLaugh", FluentIcons.Common.Symbol.EmojiLaugh);
					xamlUserType34.AddEnumValue("EmojiMeh", FluentIcons.Common.Symbol.EmojiMeh);
					xamlUserType34.AddEnumValue("EmojiMeme", FluentIcons.Common.Symbol.EmojiMeme);
					xamlUserType34.AddEnumValue("EmojiMultiple", FluentIcons.Common.Symbol.EmojiMultiple);
					xamlUserType34.AddEnumValue("EmojiSad", FluentIcons.Common.Symbol.EmojiSad);
					xamlUserType34.AddEnumValue("EmojiSadSlight", FluentIcons.Common.Symbol.EmojiSadSlight);
					xamlUserType34.AddEnumValue("EmojiSmileSlight", FluentIcons.Common.Symbol.EmojiSmileSlight);
					xamlUserType34.AddEnumValue("EmojiSparkle", FluentIcons.Common.Symbol.EmojiSparkle);
					xamlUserType34.AddEnumValue("EmojiSurprise", FluentIcons.Common.Symbol.EmojiSurprise);
					xamlUserType34.AddEnumValue("Engine", FluentIcons.Common.Symbol.Engine);
					xamlUserType34.AddEnumValue("EqualCircle", FluentIcons.Common.Symbol.EqualCircle);
					xamlUserType34.AddEnumValue("EqualOff", FluentIcons.Common.Symbol.EqualOff);
					xamlUserType34.AddEnumValue("Eraser", FluentIcons.Common.Symbol.Eraser);
					xamlUserType34.AddEnumValue("EraserMedium", FluentIcons.Common.Symbol.EraserMedium);
					xamlUserType34.AddEnumValue("EraserSegment", FluentIcons.Common.Symbol.EraserSegment);
					xamlUserType34.AddEnumValue("EraserSmall", FluentIcons.Common.Symbol.EraserSmall);
					xamlUserType34.AddEnumValue("EraserTool", FluentIcons.Common.Symbol.EraserTool);
					xamlUserType34.AddEnumValue("ErrorCircle", FluentIcons.Common.Symbol.ErrorCircle);
					xamlUserType34.AddEnumValue("ErrorCircleSettings", FluentIcons.Common.Symbol.ErrorCircleSettings);
					xamlUserType34.AddEnumValue("ExpandUpLeft", FluentIcons.Common.Symbol.ExpandUpLeft);
					xamlUserType34.AddEnumValue("ExpandUpRight", FluentIcons.Common.Symbol.ExpandUpRight);
					xamlUserType34.AddEnumValue("ExtendedDock", FluentIcons.Common.Symbol.ExtendedDock);
					xamlUserType34.AddEnumValue("Eye", FluentIcons.Common.Symbol.Eye);
					xamlUserType34.AddEnumValue("EyeLines", FluentIcons.Common.Symbol.EyeLines);
					xamlUserType34.AddEnumValue("EyeOff", FluentIcons.Common.Symbol.EyeOff);
					xamlUserType34.AddEnumValue("EyeTracking", FluentIcons.Common.Symbol.EyeTracking);
					xamlUserType34.AddEnumValue("EyeTrackingOff", FluentIcons.Common.Symbol.EyeTrackingOff);
					xamlUserType34.AddEnumValue("Eyedropper", FluentIcons.Common.Symbol.Eyedropper);
					xamlUserType34.AddEnumValue("EyedropperOff", FluentIcons.Common.Symbol.EyedropperOff);
					xamlUserType34.AddEnumValue("FStop", FluentIcons.Common.Symbol.FStop);
					xamlUserType34.AddEnumValue("FastAcceleration", FluentIcons.Common.Symbol.FastAcceleration);
					xamlUserType34.AddEnumValue("FastForward", FluentIcons.Common.Symbol.FastForward);
					xamlUserType34.AddEnumValue("Fax", FluentIcons.Common.Symbol.Fax);
					xamlUserType34.AddEnumValue("Feed", FluentIcons.Common.Symbol.Feed);
					xamlUserType34.AddEnumValue("Filmstrip", FluentIcons.Common.Symbol.Filmstrip);
					xamlUserType34.AddEnumValue("FilmstripImage", FluentIcons.Common.Symbol.FilmstripImage);
					xamlUserType34.AddEnumValue("FilmstripPlay", FluentIcons.Common.Symbol.FilmstripPlay);
					xamlUserType34.AddEnumValue("FilmstripSplit", FluentIcons.Common.Symbol.FilmstripSplit);
					xamlUserType34.AddEnumValue("Filter", FluentIcons.Common.Symbol.Filter);
					xamlUserType34.AddEnumValue("FilterAdd", FluentIcons.Common.Symbol.FilterAdd);
					xamlUserType34.AddEnumValue("FilterDismiss", FluentIcons.Common.Symbol.FilterDismiss);
					xamlUserType34.AddEnumValue("FilterSync", FluentIcons.Common.Symbol.FilterSync);
					xamlUserType34.AddEnumValue("Fingerprint", FluentIcons.Common.Symbol.Fingerprint);
					xamlUserType34.AddEnumValue("Fire", FluentIcons.Common.Symbol.Fire);
					xamlUserType34.AddEnumValue("Fireplace", FluentIcons.Common.Symbol.Fireplace);
					xamlUserType34.AddEnumValue("FixedWidth", FluentIcons.Common.Symbol.FixedWidth);
					xamlUserType34.AddEnumValue("Flag", FluentIcons.Common.Symbol.Flag);
					xamlUserType34.AddEnumValue("FlagCheckered", FluentIcons.Common.Symbol.FlagCheckered);
					xamlUserType34.AddEnumValue("FlagClock", FluentIcons.Common.Symbol.FlagClock);
					xamlUserType34.AddEnumValue("FlagOff", FluentIcons.Common.Symbol.FlagOff);
					xamlUserType34.AddEnumValue("FlagPride", FluentIcons.Common.Symbol.FlagPride);
					xamlUserType34.AddEnumValue("FlagPrideIntersexInclusiveProgress", FluentIcons.Common.Symbol.FlagPrideIntersexInclusiveProgress);
					xamlUserType34.AddEnumValue("FlagPridePhiladelphia", FluentIcons.Common.Symbol.FlagPridePhiladelphia);
					xamlUserType34.AddEnumValue("FlagPrideProgress", FluentIcons.Common.Symbol.FlagPrideProgress);
					xamlUserType34.AddEnumValue("Flash", FluentIcons.Common.Symbol.Flash);
					xamlUserType34.AddEnumValue("FlashAdd", FluentIcons.Common.Symbol.FlashAdd);
					xamlUserType34.AddEnumValue("FlashAuto", FluentIcons.Common.Symbol.FlashAuto);
					xamlUserType34.AddEnumValue("FlashCheckmark", FluentIcons.Common.Symbol.FlashCheckmark);
					xamlUserType34.AddEnumValue("FlashFlow", FluentIcons.Common.Symbol.FlashFlow);
					xamlUserType34.AddEnumValue("FlashOff", FluentIcons.Common.Symbol.FlashOff);
					xamlUserType34.AddEnumValue("FlashPlay", FluentIcons.Common.Symbol.FlashPlay);
					xamlUserType34.AddEnumValue("FlashSettings", FluentIcons.Common.Symbol.FlashSettings);
					xamlUserType34.AddEnumValue("FlashSparkle", FluentIcons.Common.Symbol.FlashSparkle);
					xamlUserType34.AddEnumValue("Flashlight", FluentIcons.Common.Symbol.Flashlight);
					xamlUserType34.AddEnumValue("FlashlightOff", FluentIcons.Common.Symbol.FlashlightOff);
					xamlUserType34.AddEnumValue("FlipHorizontal", FluentIcons.Common.Symbol.FlipHorizontal);
					xamlUserType34.AddEnumValue("FlipVertical", FluentIcons.Common.Symbol.FlipVertical);
					xamlUserType34.AddEnumValue("Flow", FluentIcons.Common.Symbol.Flow);
					xamlUserType34.AddEnumValue("Flowchart", FluentIcons.Common.Symbol.Flowchart);
					xamlUserType34.AddEnumValue("FlowchartCircle", FluentIcons.Common.Symbol.FlowchartCircle);
					xamlUserType34.AddEnumValue("Fluent", FluentIcons.Common.Symbol.Fluent);
					xamlUserType34.AddEnumValue("Fluid", FluentIcons.Common.Symbol.Fluid);
					xamlUserType34.AddEnumValue("Folder", FluentIcons.Common.Symbol.Folder);
					xamlUserType34.AddEnumValue("FolderAdd", FluentIcons.Common.Symbol.FolderAdd);
					xamlUserType34.AddEnumValue("FolderArrowLeft", FluentIcons.Common.Symbol.FolderArrowLeft);
					xamlUserType34.AddEnumValue("FolderArrowRight", FluentIcons.Common.Symbol.FolderArrowRight);
					xamlUserType34.AddEnumValue("FolderArrowUp", FluentIcons.Common.Symbol.FolderArrowUp);
					xamlUserType34.AddEnumValue("FolderBriefcase", FluentIcons.Common.Symbol.FolderBriefcase);
					xamlUserType34.AddEnumValue("FolderDocument", FluentIcons.Common.Symbol.FolderDocument);
					xamlUserType34.AddEnumValue("FolderGlobe", FluentIcons.Common.Symbol.FolderGlobe);
					xamlUserType34.AddEnumValue("FolderLightning", FluentIcons.Common.Symbol.FolderLightning);
					xamlUserType34.AddEnumValue("FolderLink", FluentIcons.Common.Symbol.FolderLink);
					xamlUserType34.AddEnumValue("FolderList", FluentIcons.Common.Symbol.FolderList);
					xamlUserType34.AddEnumValue("FolderMail", FluentIcons.Common.Symbol.FolderMail);
					xamlUserType34.AddEnumValue("FolderOpen", FluentIcons.Common.Symbol.FolderOpen);
					xamlUserType34.AddEnumValue("FolderOpenDown", FluentIcons.Common.Symbol.FolderOpenDown);
					xamlUserType34.AddEnumValue("FolderOpenVertical", FluentIcons.Common.Symbol.FolderOpenVertical);
					xamlUserType34.AddEnumValue("FolderPeople", FluentIcons.Common.Symbol.FolderPeople);
					xamlUserType34.AddEnumValue("FolderPerson", FluentIcons.Common.Symbol.FolderPerson);
					xamlUserType34.AddEnumValue("FolderProhibited", FluentIcons.Common.Symbol.FolderProhibited);
					xamlUserType34.AddEnumValue("FolderSearch", FluentIcons.Common.Symbol.FolderSearch);
					xamlUserType34.AddEnumValue("FolderSwap", FluentIcons.Common.Symbol.FolderSwap);
					xamlUserType34.AddEnumValue("FolderSync", FluentIcons.Common.Symbol.FolderSync);
					xamlUserType34.AddEnumValue("FolderZip", FluentIcons.Common.Symbol.FolderZip);
					xamlUserType34.AddEnumValue("FontDecrease", FluentIcons.Common.Symbol.FontDecrease);
					xamlUserType34.AddEnumValue("FontIncrease", FluentIcons.Common.Symbol.FontIncrease);
					xamlUserType34.AddEnumValue("FontSpaceTrackingIn", FluentIcons.Common.Symbol.FontSpaceTrackingIn);
					xamlUserType34.AddEnumValue("FontSpaceTrackingOut", FluentIcons.Common.Symbol.FontSpaceTrackingOut);
					xamlUserType34.AddEnumValue("Food", FluentIcons.Common.Symbol.Food);
					xamlUserType34.AddEnumValue("FoodApple", FluentIcons.Common.Symbol.FoodApple);
					xamlUserType34.AddEnumValue("FoodCake", FluentIcons.Common.Symbol.FoodCake);
					xamlUserType34.AddEnumValue("FoodCarrot", FluentIcons.Common.Symbol.FoodCarrot);
					xamlUserType34.AddEnumValue("FoodChickenLeg", FluentIcons.Common.Symbol.FoodChickenLeg);
					xamlUserType34.AddEnumValue("FoodEgg", FluentIcons.Common.Symbol.FoodEgg);
					xamlUserType34.AddEnumValue("FoodFish", FluentIcons.Common.Symbol.FoodFish);
					xamlUserType34.AddEnumValue("FoodGrains", FluentIcons.Common.Symbol.FoodGrains);
					xamlUserType34.AddEnumValue("FoodPizza", FluentIcons.Common.Symbol.FoodPizza);
					xamlUserType34.AddEnumValue("FoodToast", FluentIcons.Common.Symbol.FoodToast);
					xamlUserType34.AddEnumValue("Form", FluentIcons.Common.Symbol.Form);
					xamlUserType34.AddEnumValue("FormMultiple", FluentIcons.Common.Symbol.FormMultiple);
					xamlUserType34.AddEnumValue("FormNew", FluentIcons.Common.Symbol.FormNew);
					xamlUserType34.AddEnumValue("FormSparkle", FluentIcons.Common.Symbol.FormSparkle);
					xamlUserType34.AddEnumValue("Fps120", FluentIcons.Common.Symbol.Fps120);
					xamlUserType34.AddEnumValue("Fps240", FluentIcons.Common.Symbol.Fps240);
					xamlUserType34.AddEnumValue("Fps30", FluentIcons.Common.Symbol.Fps30);
					xamlUserType34.AddEnumValue("Fps60", FluentIcons.Common.Symbol.Fps60);
					xamlUserType34.AddEnumValue("Fps960", FluentIcons.Common.Symbol.Fps960);
					xamlUserType34.AddEnumValue("Frame", FluentIcons.Common.Symbol.Frame);
					xamlUserType34.AddEnumValue("FullScreenMaximize", FluentIcons.Common.Symbol.FullScreenMaximize);
					xamlUserType34.AddEnumValue("FullScreenMinimize", FluentIcons.Common.Symbol.FullScreenMinimize);
					xamlUserType34.AddEnumValue("GameChat", FluentIcons.Common.Symbol.GameChat);
					xamlUserType34.AddEnumValue("Games", FluentIcons.Common.Symbol.Games);
					xamlUserType34.AddEnumValue("GanttChart", FluentIcons.Common.Symbol.GanttChart);
					xamlUserType34.AddEnumValue("Gas", FluentIcons.Common.Symbol.Gas);
					xamlUserType34.AddEnumValue("GasPump", FluentIcons.Common.Symbol.GasPump);
					xamlUserType34.AddEnumValue("Gather", FluentIcons.Common.Symbol.Gather);
					xamlUserType34.AddEnumValue("Gauge", FluentIcons.Common.Symbol.Gauge);
					xamlUserType34.AddEnumValue("GaugeAdd", FluentIcons.Common.Symbol.GaugeAdd);
					xamlUserType34.AddEnumValue("Gavel", FluentIcons.Common.Symbol.Gavel);
					xamlUserType34.AddEnumValue("GavelProhibited", FluentIcons.Common.Symbol.GavelProhibited);
					xamlUserType34.AddEnumValue("Gesture", FluentIcons.Common.Symbol.Gesture);
					xamlUserType34.AddEnumValue("Gif", FluentIcons.Common.Symbol.Gif);
					xamlUserType34.AddEnumValue("Gift", FluentIcons.Common.Symbol.Gift);
					xamlUserType34.AddEnumValue("GiftCard", FluentIcons.Common.Symbol.GiftCard);
					xamlUserType34.AddEnumValue("GiftCardAdd", FluentIcons.Common.Symbol.GiftCardAdd);
					xamlUserType34.AddEnumValue("GiftCardArrowRight", FluentIcons.Common.Symbol.GiftCardArrowRight);
					xamlUserType34.AddEnumValue("GiftCardMoney", FluentIcons.Common.Symbol.GiftCardMoney);
					xamlUserType34.AddEnumValue("GiftCardMultiple", FluentIcons.Common.Symbol.GiftCardMultiple);
					xamlUserType34.AddEnumValue("GiftOpen", FluentIcons.Common.Symbol.GiftOpen);
					xamlUserType34.AddEnumValue("Glance", FluentIcons.Common.Symbol.Glance);
					xamlUserType34.AddEnumValue("GlanceHorizontal", FluentIcons.Common.Symbol.GlanceHorizontal);
					xamlUserType34.AddEnumValue("GlanceHorizontalSparkles", FluentIcons.Common.Symbol.GlanceHorizontalSparkles);
					xamlUserType34.AddEnumValue("Glasses", FluentIcons.Common.Symbol.Glasses);
					xamlUserType34.AddEnumValue("GlassesOff", FluentIcons.Common.Symbol.GlassesOff);
					xamlUserType34.AddEnumValue("Globe", FluentIcons.Common.Symbol.Globe);
					xamlUserType34.AddEnumValue("GlobeAdd", FluentIcons.Common.Symbol.GlobeAdd);
					xamlUserType34.AddEnumValue("GlobeArrowForward", FluentIcons.Common.Symbol.GlobeArrowForward);
					xamlUserType34.AddEnumValue("GlobeArrowUp", FluentIcons.Common.Symbol.GlobeArrowUp);
					xamlUserType34.AddEnumValue("GlobeClock", FluentIcons.Common.Symbol.GlobeClock);
					xamlUserType34.AddEnumValue("GlobeDesktop", FluentIcons.Common.Symbol.GlobeDesktop);
					xamlUserType34.AddEnumValue("GlobeError", FluentIcons.Common.Symbol.GlobeError);
					xamlUserType34.AddEnumValue("GlobeLocation", FluentIcons.Common.Symbol.GlobeLocation);
					xamlUserType34.AddEnumValue("GlobeOff", FluentIcons.Common.Symbol.GlobeOff);
					xamlUserType34.AddEnumValue("GlobePerson", FluentIcons.Common.Symbol.GlobePerson);
					xamlUserType34.AddEnumValue("GlobeProhibited", FluentIcons.Common.Symbol.GlobeProhibited);
					xamlUserType34.AddEnumValue("GlobeSearch", FluentIcons.Common.Symbol.GlobeSearch);
					xamlUserType34.AddEnumValue("GlobeShield", FluentIcons.Common.Symbol.GlobeShield);
					xamlUserType34.AddEnumValue("GlobeStar", FluentIcons.Common.Symbol.GlobeStar);
					xamlUserType34.AddEnumValue("GlobeSurface", FluentIcons.Common.Symbol.GlobeSurface);
					xamlUserType34.AddEnumValue("GlobeSync", FluentIcons.Common.Symbol.GlobeSync);
					xamlUserType34.AddEnumValue("GlobeVideo", FluentIcons.Common.Symbol.GlobeVideo);
					xamlUserType34.AddEnumValue("GlobeWarning", FluentIcons.Common.Symbol.GlobeWarning);
					xamlUserType34.AddEnumValue("Grid", FluentIcons.Common.Symbol.Grid);
					xamlUserType34.AddEnumValue("GridDots", FluentIcons.Common.Symbol.GridDots);
					xamlUserType34.AddEnumValue("GridKanban", FluentIcons.Common.Symbol.GridKanban);
					xamlUserType34.AddEnumValue("Group", FluentIcons.Common.Symbol.Group);
					xamlUserType34.AddEnumValue("GroupDismiss", FluentIcons.Common.Symbol.GroupDismiss);
					xamlUserType34.AddEnumValue("GroupList", FluentIcons.Common.Symbol.GroupList);
					xamlUserType34.AddEnumValue("GroupReturn", FluentIcons.Common.Symbol.GroupReturn);
					xamlUserType34.AddEnumValue("Guardian", FluentIcons.Common.Symbol.Guardian);
					xamlUserType34.AddEnumValue("Guest", FluentIcons.Common.Symbol.Guest);
					xamlUserType34.AddEnumValue("GuestAdd", FluentIcons.Common.Symbol.GuestAdd);
					xamlUserType34.AddEnumValue("Guitar", FluentIcons.Common.Symbol.Guitar);
					xamlUserType34.AddEnumValue("HandDraw", FluentIcons.Common.Symbol.HandDraw);
					xamlUserType34.AddEnumValue("HandLeft", FluentIcons.Common.Symbol.HandLeft);
					xamlUserType34.AddEnumValue("HandLeftChat", FluentIcons.Common.Symbol.HandLeftChat);
					xamlUserType34.AddEnumValue("HandMultiple", FluentIcons.Common.Symbol.HandMultiple);
					xamlUserType34.AddEnumValue("HandOpenHeart", FluentIcons.Common.Symbol.HandOpenHeart);
					xamlUserType34.AddEnumValue("HandPoint", FluentIcons.Common.Symbol.HandPoint);
					xamlUserType34.AddEnumValue("HandRight", FluentIcons.Common.Symbol.HandRight);
					xamlUserType34.AddEnumValue("HandRightOff", FluentIcons.Common.Symbol.HandRightOff);
					xamlUserType34.AddEnumValue("HandWave", FluentIcons.Common.Symbol.HandWave);
					xamlUserType34.AddEnumValue("Handshake", FluentIcons.Common.Symbol.Handshake);
					xamlUserType34.AddEnumValue("HapticStrong", FluentIcons.Common.Symbol.HapticStrong);
					xamlUserType34.AddEnumValue("HapticWeak", FluentIcons.Common.Symbol.HapticWeak);
					xamlUserType34.AddEnumValue("HardDrive", FluentIcons.Common.Symbol.HardDrive);
					xamlUserType34.AddEnumValue("HatGraduation", FluentIcons.Common.Symbol.HatGraduation);
					xamlUserType34.AddEnumValue("HatGraduationAdd", FluentIcons.Common.Symbol.HatGraduationAdd);
					xamlUserType34.AddEnumValue("HatGraduationSparkle", FluentIcons.Common.Symbol.HatGraduationSparkle);
					xamlUserType34.AddEnumValue("Hd", FluentIcons.Common.Symbol.Hd);
					xamlUserType34.AddEnumValue("HdOff", FluentIcons.Common.Symbol.HdOff);
					xamlUserType34.AddEnumValue("Hdr", FluentIcons.Common.Symbol.Hdr);
					xamlUserType34.AddEnumValue("HdrOff", FluentIcons.Common.Symbol.HdrOff);
					xamlUserType34.AddEnumValue("Headphones", FluentIcons.Common.Symbol.Headphones);
					xamlUserType34.AddEnumValue("HeadphonesSoundWave", FluentIcons.Common.Symbol.HeadphonesSoundWave);
					xamlUserType34.AddEnumValue("Headset", FluentIcons.Common.Symbol.Headset);
					xamlUserType34.AddEnumValue("HeadsetAdd", FluentIcons.Common.Symbol.HeadsetAdd);
					xamlUserType34.AddEnumValue("HeadsetVr", FluentIcons.Common.Symbol.HeadsetVr);
					xamlUserType34.AddEnumValue("Heart", FluentIcons.Common.Symbol.Heart);
					xamlUserType34.AddEnumValue("HeartBroken", FluentIcons.Common.Symbol.HeartBroken);
					xamlUserType34.AddEnumValue("HeartCircle", FluentIcons.Common.Symbol.HeartCircle);
					xamlUserType34.AddEnumValue("HeartCircleHint", FluentIcons.Common.Symbol.HeartCircleHint);
					xamlUserType34.AddEnumValue("HeartOff", FluentIcons.Common.Symbol.HeartOff);
					xamlUserType34.AddEnumValue("HeartPulse", FluentIcons.Common.Symbol.HeartPulse);
					xamlUserType34.AddEnumValue("HeartPulseCheckmark", FluentIcons.Common.Symbol.HeartPulseCheckmark);
					xamlUserType34.AddEnumValue("HeartPulseError", FluentIcons.Common.Symbol.HeartPulseError);
					xamlUserType34.AddEnumValue("HeartPulseWarning", FluentIcons.Common.Symbol.HeartPulseWarning);
					xamlUserType34.AddEnumValue("Hexagon", FluentIcons.Common.Symbol.Hexagon);
					xamlUserType34.AddEnumValue("HexagonSparkle", FluentIcons.Common.Symbol.HexagonSparkle);
					xamlUserType34.AddEnumValue("HexagonThree", FluentIcons.Common.Symbol.HexagonThree);
					xamlUserType34.AddEnumValue("Highlight", FluentIcons.Common.Symbol.Highlight);
					xamlUserType34.AddEnumValue("HighlightAccent", FluentIcons.Common.Symbol.HighlightAccent);
					xamlUserType34.AddEnumValue("HighlightLink", FluentIcons.Common.Symbol.HighlightLink);
					xamlUserType34.AddEnumValue("Highway", FluentIcons.Common.Symbol.Highway);
					xamlUserType34.AddEnumValue("History", FluentIcons.Common.Symbol.History);
					xamlUserType34.AddEnumValue("HistoryDismiss", FluentIcons.Common.Symbol.HistoryDismiss);
					xamlUserType34.AddEnumValue("Home", FluentIcons.Common.Symbol.Home);
					xamlUserType34.AddEnumValue("HomeAdd", FluentIcons.Common.Symbol.HomeAdd);
					xamlUserType34.AddEnumValue("HomeCheckmark", FluentIcons.Common.Symbol.HomeCheckmark);
					xamlUserType34.AddEnumValue("HomeDatabase", FluentIcons.Common.Symbol.HomeDatabase);
					xamlUserType34.AddEnumValue("HomeEmpty", FluentIcons.Common.Symbol.HomeEmpty);
					xamlUserType34.AddEnumValue("HomeGarage", FluentIcons.Common.Symbol.HomeGarage);
					xamlUserType34.AddEnumValue("HomeHeart", FluentIcons.Common.Symbol.HomeHeart);
					xamlUserType34.AddEnumValue("HomeMore", FluentIcons.Common.Symbol.HomeMore);
					xamlUserType34.AddEnumValue("HomePerson", FluentIcons.Common.Symbol.HomePerson);
					xamlUserType34.AddEnumValue("HomeSplit", FluentIcons.Common.Symbol.HomeSplit);
					xamlUserType34.AddEnumValue("Hourglass", FluentIcons.Common.Symbol.Hourglass);
					xamlUserType34.AddEnumValue("HourglassHalf", FluentIcons.Common.Symbol.HourglassHalf);
					xamlUserType34.AddEnumValue("HourglassOneQuarter", FluentIcons.Common.Symbol.HourglassOneQuarter);
					xamlUserType34.AddEnumValue("HourglassThreeQuarter", FluentIcons.Common.Symbol.HourglassThreeQuarter);
					xamlUserType34.AddEnumValue("Icons", FluentIcons.Common.Symbol.Icons);
					xamlUserType34.AddEnumValue("Image", FluentIcons.Common.Symbol.Image);
					xamlUserType34.AddEnumValue("ImageAdd", FluentIcons.Common.Symbol.ImageAdd);
					xamlUserType34.AddEnumValue("ImageAltText", FluentIcons.Common.Symbol.ImageAltText);
					xamlUserType34.AddEnumValue("ImageArrowBack", FluentIcons.Common.Symbol.ImageArrowBack);
					xamlUserType34.AddEnumValue("ImageArrowCounterclockwise", FluentIcons.Common.Symbol.ImageArrowCounterclockwise);
					xamlUserType34.AddEnumValue("ImageArrowForward", FluentIcons.Common.Symbol.ImageArrowForward);
					xamlUserType34.AddEnumValue("ImageBorder", FluentIcons.Common.Symbol.ImageBorder);
					xamlUserType34.AddEnumValue("ImageCircle", FluentIcons.Common.Symbol.ImageCircle);
					xamlUserType34.AddEnumValue("ImageCopy", FluentIcons.Common.Symbol.ImageCopy);
					xamlUserType34.AddEnumValue("ImageEdit", FluentIcons.Common.Symbol.ImageEdit);
					xamlUserType34.AddEnumValue("ImageGlobe", FluentIcons.Common.Symbol.ImageGlobe);
					xamlUserType34.AddEnumValue("ImageMultiple", FluentIcons.Common.Symbol.ImageMultiple);
					xamlUserType34.AddEnumValue("ImageMultipleOff", FluentIcons.Common.Symbol.ImageMultipleOff);
					xamlUserType34.AddEnumValue("ImageOff", FluentIcons.Common.Symbol.ImageOff);
					xamlUserType34.AddEnumValue("ImageProhibited", FluentIcons.Common.Symbol.ImageProhibited);
					xamlUserType34.AddEnumValue("ImageReflection", FluentIcons.Common.Symbol.ImageReflection);
					xamlUserType34.AddEnumValue("ImageSearch", FluentIcons.Common.Symbol.ImageSearch);
					xamlUserType34.AddEnumValue("ImageShadow", FluentIcons.Common.Symbol.ImageShadow);
					xamlUserType34.AddEnumValue("ImageSparkle", FluentIcons.Common.Symbol.ImageSparkle);
					xamlUserType34.AddEnumValue("ImageSplit", FluentIcons.Common.Symbol.ImageSplit);
					xamlUserType34.AddEnumValue("ImageStack", FluentIcons.Common.Symbol.ImageStack);
					xamlUserType34.AddEnumValue("ImageTable", FluentIcons.Common.Symbol.ImageTable);
					xamlUserType34.AddEnumValue("ImmersiveReader", FluentIcons.Common.Symbol.ImmersiveReader);
					xamlUserType34.AddEnumValue("Important", FluentIcons.Common.Symbol.Important);
					xamlUserType34.AddEnumValue("Incognito", FluentIcons.Common.Symbol.Incognito);
					xamlUserType34.AddEnumValue("Info", FluentIcons.Common.Symbol.Info);
					xamlUserType34.AddEnumValue("InfoShield", FluentIcons.Common.Symbol.InfoShield);
					xamlUserType34.AddEnumValue("InfoSparkle", FluentIcons.Common.Symbol.InfoSparkle);
					xamlUserType34.AddEnumValue("InkStroke", FluentIcons.Common.Symbol.InkStroke);
					xamlUserType34.AddEnumValue("InkStrokeArrowDown", FluentIcons.Common.Symbol.InkStrokeArrowDown);
					xamlUserType34.AddEnumValue("InkStrokeArrowUpDown", FluentIcons.Common.Symbol.InkStrokeArrowUpDown);
					xamlUserType34.AddEnumValue("InkingTool", FluentIcons.Common.Symbol.InkingTool);
					xamlUserType34.AddEnumValue("InkingToolAccent", FluentIcons.Common.Symbol.InkingToolAccent);
					xamlUserType34.AddEnumValue("InprivateAccount", FluentIcons.Common.Symbol.InprivateAccount);
					xamlUserType34.AddEnumValue("Insert", FluentIcons.Common.Symbol.Insert);
					xamlUserType34.AddEnumValue("IosChevronRight", FluentIcons.Common.Symbol.IosChevronRight);
					xamlUserType34.AddEnumValue("Iot", FluentIcons.Common.Symbol.Iot);
					xamlUserType34.AddEnumValue("IotAlert", FluentIcons.Common.Symbol.IotAlert);
					xamlUserType34.AddEnumValue("Javascript", FluentIcons.Common.Symbol.Javascript);
					xamlUserType34.AddEnumValue("Joystick", FluentIcons.Common.Symbol.Joystick);
					xamlUserType34.AddEnumValue("Key", FluentIcons.Common.Symbol.Key);
					xamlUserType34.AddEnumValue("KeyCommand", FluentIcons.Common.Symbol.KeyCommand);
					xamlUserType34.AddEnumValue("KeyMultiple", FluentIcons.Common.Symbol.KeyMultiple);
					xamlUserType34.AddEnumValue("KeyReset", FluentIcons.Common.Symbol.KeyReset);
					xamlUserType34.AddEnumValue("Keyboard", FluentIcons.Common.Symbol.Keyboard);
					xamlUserType34.AddEnumValue("Keyboard123", FluentIcons.Common.Symbol.Keyboard123);
					xamlUserType34.AddEnumValue("KeyboardDock", FluentIcons.Common.Symbol.KeyboardDock);
					xamlUserType34.AddEnumValue("KeyboardLayoutFloat", FluentIcons.Common.Symbol.KeyboardLayoutFloat);
					xamlUserType34.AddEnumValue("KeyboardLayoutOneHandedLeft", FluentIcons.Common.Symbol.KeyboardLayoutOneHandedLeft);
					xamlUserType34.AddEnumValue("KeyboardLayoutResize", FluentIcons.Common.Symbol.KeyboardLayoutResize);
					xamlUserType34.AddEnumValue("KeyboardLayoutSplit", FluentIcons.Common.Symbol.KeyboardLayoutSplit);
					xamlUserType34.AddEnumValue("KeyboardShift", FluentIcons.Common.Symbol.KeyboardShift);
					xamlUserType34.AddEnumValue("KeyboardShiftUppercase", FluentIcons.Common.Symbol.KeyboardShiftUppercase);
					xamlUserType34.AddEnumValue("KeyboardTab", FluentIcons.Common.Symbol.KeyboardTab);
					xamlUserType34.AddEnumValue("Laptop", FluentIcons.Common.Symbol.Laptop);
					xamlUserType34.AddEnumValue("LaptopBriefcase", FluentIcons.Common.Symbol.LaptopBriefcase);
					xamlUserType34.AddEnumValue("LaptopDismiss", FluentIcons.Common.Symbol.LaptopDismiss);
					xamlUserType34.AddEnumValue("LaptopPerson", FluentIcons.Common.Symbol.LaptopPerson);
					xamlUserType34.AddEnumValue("LaptopSettings", FluentIcons.Common.Symbol.LaptopSettings);
					xamlUserType34.AddEnumValue("LaptopShield", FluentIcons.Common.Symbol.LaptopShield);
					xamlUserType34.AddEnumValue("LaserTool", FluentIcons.Common.Symbol.LaserTool);
					xamlUserType34.AddEnumValue("Lasso", FluentIcons.Common.Symbol.Lasso);
					xamlUserType34.AddEnumValue("LauncherSettings", FluentIcons.Common.Symbol.LauncherSettings);
					xamlUserType34.AddEnumValue("Layer", FluentIcons.Common.Symbol.Layer);
					xamlUserType34.AddEnumValue("LayerDiagonal", FluentIcons.Common.Symbol.LayerDiagonal);
					xamlUserType34.AddEnumValue("LayerDiagonalAdd", FluentIcons.Common.Symbol.LayerDiagonalAdd);
					xamlUserType34.AddEnumValue("LayerDiagonalPerson", FluentIcons.Common.Symbol.LayerDiagonalPerson);
					xamlUserType34.AddEnumValue("LayerDiagonalSparkle", FluentIcons.Common.Symbol.LayerDiagonalSparkle);
					xamlUserType34.AddEnumValue("LayoutCellFour", FluentIcons.Common.Symbol.LayoutCellFour);
					xamlUserType34.AddEnumValue("LayoutCellFourFocusBottomLeft", FluentIcons.Common.Symbol.LayoutCellFourFocusBottomLeft);
					xamlUserType34.AddEnumValue("LayoutCellFourFocusBottomRight", FluentIcons.Common.Symbol.LayoutCellFourFocusBottomRight);
					xamlUserType34.AddEnumValue("LayoutCellFourFocusTopLeft", FluentIcons.Common.Symbol.LayoutCellFourFocusTopLeft);
					xamlUserType34.AddEnumValue("LayoutCellFourFocusTopRight", FluentIcons.Common.Symbol.LayoutCellFourFocusTopRight);
					xamlUserType34.AddEnumValue("LayoutColumnFour", FluentIcons.Common.Symbol.LayoutColumnFour);
					xamlUserType34.AddEnumValue("LayoutColumnFourFocusCenterLeft", FluentIcons.Common.Symbol.LayoutColumnFourFocusCenterLeft);
					xamlUserType34.AddEnumValue("LayoutColumnFourFocusCenterRight", FluentIcons.Common.Symbol.LayoutColumnFourFocusCenterRight);
					xamlUserType34.AddEnumValue("LayoutColumnFourFocusLeft", FluentIcons.Common.Symbol.LayoutColumnFourFocusLeft);
					xamlUserType34.AddEnumValue("LayoutColumnFourFocusRight", FluentIcons.Common.Symbol.LayoutColumnFourFocusRight);
					xamlUserType34.AddEnumValue("LayoutColumnOneThirdLeft", FluentIcons.Common.Symbol.LayoutColumnOneThirdLeft);
					xamlUserType34.AddEnumValue("LayoutColumnOneThirdRight", FluentIcons.Common.Symbol.LayoutColumnOneThirdRight);
					xamlUserType34.AddEnumValue("LayoutColumnOneThirdRightHint", FluentIcons.Common.Symbol.LayoutColumnOneThirdRightHint);
					xamlUserType34.AddEnumValue("LayoutColumnThree", FluentIcons.Common.Symbol.LayoutColumnThree);
					xamlUserType34.AddEnumValue("LayoutColumnThreeFocusCenter", FluentIcons.Common.Symbol.LayoutColumnThreeFocusCenter);
					xamlUserType34.AddEnumValue("LayoutColumnThreeFocusLeft", FluentIcons.Common.Symbol.LayoutColumnThreeFocusLeft);
					xamlUserType34.AddEnumValue("LayoutColumnThreeFocusRight", FluentIcons.Common.Symbol.LayoutColumnThreeFocusRight);
					xamlUserType34.AddEnumValue("LayoutColumnTwo", FluentIcons.Common.Symbol.LayoutColumnTwo);
					xamlUserType34.AddEnumValue("LayoutColumnTwoFocusLeft", FluentIcons.Common.Symbol.LayoutColumnTwoFocusLeft);
					xamlUserType34.AddEnumValue("LayoutColumnTwoFocusRight", FluentIcons.Common.Symbol.LayoutColumnTwoFocusRight);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitLeft", FluentIcons.Common.Symbol.LayoutColumnTwoSplitLeft);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitLeftFocusBottomLeft", FluentIcons.Common.Symbol.LayoutColumnTwoSplitLeftFocusBottomLeft);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitLeftFocusRight", FluentIcons.Common.Symbol.LayoutColumnTwoSplitLeftFocusRight);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitLeftFocusTopLeft", FluentIcons.Common.Symbol.LayoutColumnTwoSplitLeftFocusTopLeft);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitRight", FluentIcons.Common.Symbol.LayoutColumnTwoSplitRight);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitRightFocusBottomRight", FluentIcons.Common.Symbol.LayoutColumnTwoSplitRightFocusBottomRight);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitRightFocusLeft", FluentIcons.Common.Symbol.LayoutColumnTwoSplitRightFocusLeft);
					xamlUserType34.AddEnumValue("LayoutColumnTwoSplitRightFocusTopRight", FluentIcons.Common.Symbol.LayoutColumnTwoSplitRightFocusTopRight);
					xamlUserType34.AddEnumValue("LayoutRowFour", FluentIcons.Common.Symbol.LayoutRowFour);
					xamlUserType34.AddEnumValue("LayoutRowFourFocusBottom", FluentIcons.Common.Symbol.LayoutRowFourFocusBottom);
					xamlUserType34.AddEnumValue("LayoutRowFourFocusCenterBottom", FluentIcons.Common.Symbol.LayoutRowFourFocusCenterBottom);
					xamlUserType34.AddEnumValue("LayoutRowFourFocusCenterTop", FluentIcons.Common.Symbol.LayoutRowFourFocusCenterTop);
					xamlUserType34.AddEnumValue("LayoutRowFourFocusTop", FluentIcons.Common.Symbol.LayoutRowFourFocusTop);
					xamlUserType34.AddEnumValue("LayoutRowThree", FluentIcons.Common.Symbol.LayoutRowThree);
					xamlUserType34.AddEnumValue("LayoutRowThreeFocusBottom", FluentIcons.Common.Symbol.LayoutRowThreeFocusBottom);
					xamlUserType34.AddEnumValue("LayoutRowThreeFocusCenter", FluentIcons.Common.Symbol.LayoutRowThreeFocusCenter);
					xamlUserType34.AddEnumValue("LayoutRowThreeFocusTop", FluentIcons.Common.Symbol.LayoutRowThreeFocusTop);
					xamlUserType34.AddEnumValue("LayoutRowTwo", FluentIcons.Common.Symbol.LayoutRowTwo);
					xamlUserType34.AddEnumValue("LayoutRowTwoFocusBottom", FluentIcons.Common.Symbol.LayoutRowTwoFocusBottom);
					xamlUserType34.AddEnumValue("LayoutRowTwoFocusTop", FluentIcons.Common.Symbol.LayoutRowTwoFocusTop);
					xamlUserType34.AddEnumValue("LayoutRowTwoFocusTopSettings", FluentIcons.Common.Symbol.LayoutRowTwoFocusTopSettings);
					xamlUserType34.AddEnumValue("LayoutRowTwoSettings", FluentIcons.Common.Symbol.LayoutRowTwoSettings);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitBottom", FluentIcons.Common.Symbol.LayoutRowTwoSplitBottom);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitBottomFocusBottomLeft", FluentIcons.Common.Symbol.LayoutRowTwoSplitBottomFocusBottomLeft);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitBottomFocusBottomRight", FluentIcons.Common.Symbol.LayoutRowTwoSplitBottomFocusBottomRight);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitBottomFocusTop", FluentIcons.Common.Symbol.LayoutRowTwoSplitBottomFocusTop);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitTop", FluentIcons.Common.Symbol.LayoutRowTwoSplitTop);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitTopFocusBottom", FluentIcons.Common.Symbol.LayoutRowTwoSplitTopFocusBottom);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitTopFocusTopLeft", FluentIcons.Common.Symbol.LayoutRowTwoSplitTopFocusTopLeft);
					xamlUserType34.AddEnumValue("LayoutRowTwoSplitTopFocusTopRight", FluentIcons.Common.Symbol.LayoutRowTwoSplitTopFocusTopRight);
					xamlUserType34.AddEnumValue("LeafOne", FluentIcons.Common.Symbol.LeafOne);
					xamlUserType34.AddEnumValue("LeafThree", FluentIcons.Common.Symbol.LeafThree);
					xamlUserType34.AddEnumValue("LeafTwo", FluentIcons.Common.Symbol.LeafTwo);
					xamlUserType34.AddEnumValue("LearningApp", FluentIcons.Common.Symbol.LearningApp);
					xamlUserType34.AddEnumValue("Library", FluentIcons.Common.Symbol.Library);
					xamlUserType34.AddEnumValue("Lightbulb", FluentIcons.Common.Symbol.Lightbulb);
					xamlUserType34.AddEnumValue("LightbulbCheckmark", FluentIcons.Common.Symbol.LightbulbCheckmark);
					xamlUserType34.AddEnumValue("LightbulbCircle", FluentIcons.Common.Symbol.LightbulbCircle);
					xamlUserType34.AddEnumValue("LightbulbFilament", FluentIcons.Common.Symbol.LightbulbFilament);
					xamlUserType34.AddEnumValue("LightbulbPerson", FluentIcons.Common.Symbol.LightbulbPerson);
					xamlUserType34.AddEnumValue("Likert", FluentIcons.Common.Symbol.Likert);
					xamlUserType34.AddEnumValue("Line", FluentIcons.Common.Symbol.Line);
					xamlUserType34.AddEnumValue("LineDashes", FluentIcons.Common.Symbol.LineDashes);
					xamlUserType34.AddEnumValue("LineFlowDiagonalUpRight", FluentIcons.Common.Symbol.LineFlowDiagonalUpRight);
					xamlUserType34.AddEnumValue("LineHorizontal1", FluentIcons.Common.Symbol.LineHorizontal1);
					xamlUserType34.AddEnumValue("LineHorizontal1DashDotDash", FluentIcons.Common.Symbol.LineHorizontal1DashDotDash);
					xamlUserType34.AddEnumValue("LineHorizontal1Dashes", FluentIcons.Common.Symbol.LineHorizontal1Dashes);
					xamlUserType34.AddEnumValue("LineHorizontal1Dot", FluentIcons.Common.Symbol.LineHorizontal1Dot);
					xamlUserType34.AddEnumValue("LineHorizontal2DashesSolid", FluentIcons.Common.Symbol.LineHorizontal2DashesSolid);
					xamlUserType34.AddEnumValue("LineHorizontal3", FluentIcons.Common.Symbol.LineHorizontal3);
					xamlUserType34.AddEnumValue("LineHorizontal4", FluentIcons.Common.Symbol.LineHorizontal4);
					xamlUserType34.AddEnumValue("LineHorizontal4Search", FluentIcons.Common.Symbol.LineHorizontal4Search);
					xamlUserType34.AddEnumValue("LineHorizontal5", FluentIcons.Common.Symbol.LineHorizontal5);
					xamlUserType34.AddEnumValue("LineHorizontal5Error", FluentIcons.Common.Symbol.LineHorizontal5Error);
					xamlUserType34.AddEnumValue("LineStyle", FluentIcons.Common.Symbol.LineStyle);
					xamlUserType34.AddEnumValue("LineStyleSketch", FluentIcons.Common.Symbol.LineStyleSketch);
					xamlUserType34.AddEnumValue("LineThickness", FluentIcons.Common.Symbol.LineThickness);
					xamlUserType34.AddEnumValue("Link", FluentIcons.Common.Symbol.Link);
					xamlUserType34.AddEnumValue("LinkAdd", FluentIcons.Common.Symbol.LinkAdd);
					xamlUserType34.AddEnumValue("LinkDismiss", FluentIcons.Common.Symbol.LinkDismiss);
					xamlUserType34.AddEnumValue("LinkEdit", FluentIcons.Common.Symbol.LinkEdit);
					xamlUserType34.AddEnumValue("LinkMultiple", FluentIcons.Common.Symbol.LinkMultiple);
					xamlUserType34.AddEnumValue("LinkPerson", FluentIcons.Common.Symbol.LinkPerson);
					xamlUserType34.AddEnumValue("LinkSquare", FluentIcons.Common.Symbol.LinkSquare);
					xamlUserType34.AddEnumValue("LinkToolbox", FluentIcons.Common.Symbol.LinkToolbox);
					xamlUserType34.AddEnumValue("List", FluentIcons.Common.Symbol.List);
					xamlUserType34.AddEnumValue("ListBar", FluentIcons.Common.Symbol.ListBar);
					xamlUserType34.AddEnumValue("ListBarTree", FluentIcons.Common.Symbol.ListBarTree);
					xamlUserType34.AddEnumValue("ListBarTreeOffset", FluentIcons.Common.Symbol.ListBarTreeOffset);
					xamlUserType34.AddEnumValue("Live", FluentIcons.Common.Symbol.Live);
					xamlUserType34.AddEnumValue("LiveOff", FluentIcons.Common.Symbol.LiveOff);
					xamlUserType34.AddEnumValue("LocalLanguage", FluentIcons.Common.Symbol.LocalLanguage);
					xamlUserType34.AddEnumValue("Location", FluentIcons.Common.Symbol.Location);
					xamlUserType34.AddEnumValue("LocationAdd", FluentIcons.Common.Symbol.LocationAdd);
					xamlUserType34.AddEnumValue("LocationAddLeft", FluentIcons.Common.Symbol.LocationAddLeft);
					xamlUserType34.AddEnumValue("LocationAddRight", FluentIcons.Common.Symbol.LocationAddRight);
					xamlUserType34.AddEnumValue("LocationAddUp", FluentIcons.Common.Symbol.LocationAddUp);
					xamlUserType34.AddEnumValue("LocationArrow", FluentIcons.Common.Symbol.LocationArrow);
					xamlUserType34.AddEnumValue("LocationArrowLeft", FluentIcons.Common.Symbol.LocationArrowLeft);
					xamlUserType34.AddEnumValue("LocationArrowRight", FluentIcons.Common.Symbol.LocationArrowRight);
					xamlUserType34.AddEnumValue("LocationArrowUp", FluentIcons.Common.Symbol.LocationArrowUp);
					xamlUserType34.AddEnumValue("LocationCheckmark", FluentIcons.Common.Symbol.LocationCheckmark);
					xamlUserType34.AddEnumValue("LocationDismiss", FluentIcons.Common.Symbol.LocationDismiss);
					xamlUserType34.AddEnumValue("LocationLive", FluentIcons.Common.Symbol.LocationLive);
					xamlUserType34.AddEnumValue("LocationOff", FluentIcons.Common.Symbol.LocationOff);
					xamlUserType34.AddEnumValue("LocationRipple", FluentIcons.Common.Symbol.LocationRipple);
					xamlUserType34.AddEnumValue("LocationSettings", FluentIcons.Common.Symbol.LocationSettings);
					xamlUserType34.AddEnumValue("LocationTargetSquare", FluentIcons.Common.Symbol.LocationTargetSquare);
					xamlUserType34.AddEnumValue("LockClosed", FluentIcons.Common.Symbol.LockClosed);
					xamlUserType34.AddEnumValue("LockClosedKey", FluentIcons.Common.Symbol.LockClosedKey);
					xamlUserType34.AddEnumValue("LockClosedRibbon", FluentIcons.Common.Symbol.LockClosedRibbon);
					xamlUserType34.AddEnumValue("LockMultiple", FluentIcons.Common.Symbol.LockMultiple);
					xamlUserType34.AddEnumValue("LockOpen", FluentIcons.Common.Symbol.LockOpen);
					xamlUserType34.AddEnumValue("LockShield", FluentIcons.Common.Symbol.LockShield);
					xamlUserType34.AddEnumValue("Lottery", FluentIcons.Common.Symbol.Lottery);
					xamlUserType34.AddEnumValue("Luggage", FluentIcons.Common.Symbol.Luggage);
					xamlUserType34.AddEnumValue("Mail", FluentIcons.Common.Symbol.Mail);
					xamlUserType34.AddEnumValue("MailAdd", FluentIcons.Common.Symbol.MailAdd);
					xamlUserType34.AddEnumValue("MailAlert", FluentIcons.Common.Symbol.MailAlert);
					xamlUserType34.AddEnumValue("MailAllRead", FluentIcons.Common.Symbol.MailAllRead);
					xamlUserType34.AddEnumValue("MailAllUnread", FluentIcons.Common.Symbol.MailAllUnread);
					xamlUserType34.AddEnumValue("MailArrowClockwise", FluentIcons.Common.Symbol.MailArrowClockwise);
					xamlUserType34.AddEnumValue("MailArrowDoubleBack", FluentIcons.Common.Symbol.MailArrowDoubleBack);
					xamlUserType34.AddEnumValue("MailArrowDown", FluentIcons.Common.Symbol.MailArrowDown);
					xamlUserType34.AddEnumValue("MailArrowForward", FluentIcons.Common.Symbol.MailArrowForward);
					xamlUserType34.AddEnumValue("MailArrowUp", FluentIcons.Common.Symbol.MailArrowUp);
					xamlUserType34.AddEnumValue("MailAttach", FluentIcons.Common.Symbol.MailAttach);
					xamlUserType34.AddEnumValue("MailCheckmark", FluentIcons.Common.Symbol.MailCheckmark);
					xamlUserType34.AddEnumValue("MailClock", FluentIcons.Common.Symbol.MailClock);
					xamlUserType34.AddEnumValue("MailCopy", FluentIcons.Common.Symbol.MailCopy);
					xamlUserType34.AddEnumValue("MailDataBar", FluentIcons.Common.Symbol.MailDataBar);
					xamlUserType34.AddEnumValue("MailDismiss", FluentIcons.Common.Symbol.MailDismiss);
					xamlUserType34.AddEnumValue("MailEdit", FluentIcons.Common.Symbol.MailEdit);
					xamlUserType34.AddEnumValue("MailError", FluentIcons.Common.Symbol.MailError);
					xamlUserType34.AddEnumValue("MailInbox", FluentIcons.Common.Symbol.MailInbox);
					xamlUserType34.AddEnumValue("MailInboxAdd", FluentIcons.Common.Symbol.MailInboxAdd);
					xamlUserType34.AddEnumValue("MailInboxAll", FluentIcons.Common.Symbol.MailInboxAll);
					xamlUserType34.AddEnumValue("MailInboxArrowDown", FluentIcons.Common.Symbol.MailInboxArrowDown);
					xamlUserType34.AddEnumValue("MailInboxArrowRight", FluentIcons.Common.Symbol.MailInboxArrowRight);
					xamlUserType34.AddEnumValue("MailInboxArrowUp", FluentIcons.Common.Symbol.MailInboxArrowUp);
					xamlUserType34.AddEnumValue("MailInboxCheckmark", FluentIcons.Common.Symbol.MailInboxCheckmark);
					xamlUserType34.AddEnumValue("MailInboxDismiss", FluentIcons.Common.Symbol.MailInboxDismiss);
					xamlUserType34.AddEnumValue("MailInboxPerson", FluentIcons.Common.Symbol.MailInboxPerson);
					xamlUserType34.AddEnumValue("MailLink", FluentIcons.Common.Symbol.MailLink);
					xamlUserType34.AddEnumValue("MailList", FluentIcons.Common.Symbol.MailList);
					xamlUserType34.AddEnumValue("MailMultiple", FluentIcons.Common.Symbol.MailMultiple);
					xamlUserType34.AddEnumValue("MailOff", FluentIcons.Common.Symbol.MailOff);
					xamlUserType34.AddEnumValue("MailOpenPerson", FluentIcons.Common.Symbol.MailOpenPerson);
					xamlUserType34.AddEnumValue("MailPause", FluentIcons.Common.Symbol.MailPause);
					xamlUserType34.AddEnumValue("MailProhibited", FluentIcons.Common.Symbol.MailProhibited);
					xamlUserType34.AddEnumValue("MailRead", FluentIcons.Common.Symbol.MailRead);
					xamlUserType34.AddEnumValue("MailReadBriefcase", FluentIcons.Common.Symbol.MailReadBriefcase);
					xamlUserType34.AddEnumValue("MailReadMultiple", FluentIcons.Common.Symbol.MailReadMultiple);
					xamlUserType34.AddEnumValue("MailRewind", FluentIcons.Common.Symbol.MailRewind);
					xamlUserType34.AddEnumValue("MailSettings", FluentIcons.Common.Symbol.MailSettings);
					xamlUserType34.AddEnumValue("MailShield", FluentIcons.Common.Symbol.MailShield);
					xamlUserType34.AddEnumValue("MailTemplate", FluentIcons.Common.Symbol.MailTemplate);
					xamlUserType34.AddEnumValue("MailUnread", FluentIcons.Common.Symbol.MailUnread);
					xamlUserType34.AddEnumValue("MailWarning", FluentIcons.Common.Symbol.MailWarning);
					xamlUserType34.AddEnumValue("Mailbox", FluentIcons.Common.Symbol.Mailbox);
					xamlUserType34.AddEnumValue("Map", FluentIcons.Common.Symbol.Map);
					xamlUserType34.AddEnumValue("MapDrive", FluentIcons.Common.Symbol.MapDrive);
					xamlUserType34.AddEnumValue("Markdown", FluentIcons.Common.Symbol.Markdown);
					xamlUserType34.AddEnumValue("MatchAppLayout", FluentIcons.Common.Symbol.MatchAppLayout);
					xamlUserType34.AddEnumValue("MathFormatLinear", FluentIcons.Common.Symbol.MathFormatLinear);
					xamlUserType34.AddEnumValue("MathFormatProfessional", FluentIcons.Common.Symbol.MathFormatProfessional);
					xamlUserType34.AddEnumValue("MathFormula", FluentIcons.Common.Symbol.MathFormula);
					xamlUserType34.AddEnumValue("MathSymbols", FluentIcons.Common.Symbol.MathSymbols);
					xamlUserType34.AddEnumValue("Maximize", FluentIcons.Common.Symbol.Maximize);
					xamlUserType34.AddEnumValue("MeetNow", FluentIcons.Common.Symbol.MeetNow);
					xamlUserType34.AddEnumValue("Megaphone", FluentIcons.Common.Symbol.Megaphone);
					xamlUserType34.AddEnumValue("MegaphoneCircle", FluentIcons.Common.Symbol.MegaphoneCircle);
					xamlUserType34.AddEnumValue("MegaphoneLoud", FluentIcons.Common.Symbol.MegaphoneLoud);
					xamlUserType34.AddEnumValue("MegaphoneOff", FluentIcons.Common.Symbol.MegaphoneOff);
					xamlUserType34.AddEnumValue("Mention", FluentIcons.Common.Symbol.Mention);
					xamlUserType34.AddEnumValue("MentionArrowDown", FluentIcons.Common.Symbol.MentionArrowDown);
					xamlUserType34.AddEnumValue("MentionBrackets", FluentIcons.Common.Symbol.MentionBrackets);
					xamlUserType34.AddEnumValue("Merge", FluentIcons.Common.Symbol.Merge);
					xamlUserType34.AddEnumValue("Mic", FluentIcons.Common.Symbol.Mic);
					xamlUserType34.AddEnumValue("MicLink", FluentIcons.Common.Symbol.MicLink);
					xamlUserType34.AddEnumValue("MicOff", FluentIcons.Common.Symbol.MicOff);
					xamlUserType34.AddEnumValue("MicProhibited", FluentIcons.Common.Symbol.MicProhibited);
					xamlUserType34.AddEnumValue("MicPulse", FluentIcons.Common.Symbol.MicPulse);
					xamlUserType34.AddEnumValue("MicPulseOff", FluentIcons.Common.Symbol.MicPulseOff);
					xamlUserType34.AddEnumValue("MicRecord", FluentIcons.Common.Symbol.MicRecord);
					xamlUserType34.AddEnumValue("MicSettings", FluentIcons.Common.Symbol.MicSettings);
					xamlUserType34.AddEnumValue("MicSparkle", FluentIcons.Common.Symbol.MicSparkle);
					xamlUserType34.AddEnumValue("MicSync", FluentIcons.Common.Symbol.MicSync);
					xamlUserType34.AddEnumValue("Microscope", FluentIcons.Common.Symbol.Microscope);
					xamlUserType34.AddEnumValue("Midi", FluentIcons.Common.Symbol.Midi);
					xamlUserType34.AddEnumValue("MobileOptimized", FluentIcons.Common.Symbol.MobileOptimized);
					xamlUserType34.AddEnumValue("Mold", FluentIcons.Common.Symbol.Mold);
					xamlUserType34.AddEnumValue("Molecule", FluentIcons.Common.Symbol.Molecule);
					xamlUserType34.AddEnumValue("Money", FluentIcons.Common.Symbol.Money);
					xamlUserType34.AddEnumValue("MoneyCalculator", FluentIcons.Common.Symbol.MoneyCalculator);
					xamlUserType34.AddEnumValue("MoneyDismiss", FluentIcons.Common.Symbol.MoneyDismiss);
					xamlUserType34.AddEnumValue("MoneyHand", FluentIcons.Common.Symbol.MoneyHand);
					xamlUserType34.AddEnumValue("MoneyOff", FluentIcons.Common.Symbol.MoneyOff);
					xamlUserType34.AddEnumValue("MoneySettings", FluentIcons.Common.Symbol.MoneySettings);
					xamlUserType34.AddEnumValue("MoreCircle", FluentIcons.Common.Symbol.MoreCircle);
					xamlUserType34.AddEnumValue("MoreHorizontal", FluentIcons.Common.Symbol.MoreHorizontal);
					xamlUserType34.AddEnumValue("MoreVertical", FluentIcons.Common.Symbol.MoreVertical);
					xamlUserType34.AddEnumValue("MountainLocationBottom", FluentIcons.Common.Symbol.MountainLocationBottom);
					xamlUserType34.AddEnumValue("MountainLocationTop", FluentIcons.Common.Symbol.MountainLocationTop);
					xamlUserType34.AddEnumValue("MountainTrail", FluentIcons.Common.Symbol.MountainTrail);
					xamlUserType34.AddEnumValue("MoviesAndTv", FluentIcons.Common.Symbol.MoviesAndTv);
					xamlUserType34.AddEnumValue("Multiplier12x", FluentIcons.Common.Symbol.Multiplier12x);
					xamlUserType34.AddEnumValue("Multiplier15x", FluentIcons.Common.Symbol.Multiplier15x);
					xamlUserType34.AddEnumValue("Multiplier18x", FluentIcons.Common.Symbol.Multiplier18x);
					xamlUserType34.AddEnumValue("Multiplier1x", FluentIcons.Common.Symbol.Multiplier1x);
					xamlUserType34.AddEnumValue("Multiplier2x", FluentIcons.Common.Symbol.Multiplier2x);
					xamlUserType34.AddEnumValue("Multiplier5x", FluentIcons.Common.Symbol.Multiplier5x);
					xamlUserType34.AddEnumValue("Multiselect", FluentIcons.Common.Symbol.Multiselect);
					xamlUserType34.AddEnumValue("MusicNote1", FluentIcons.Common.Symbol.MusicNote1);
					xamlUserType34.AddEnumValue("MusicNote2", FluentIcons.Common.Symbol.MusicNote2);
					xamlUserType34.AddEnumValue("MusicNote2Play", FluentIcons.Common.Symbol.MusicNote2Play);
					xamlUserType34.AddEnumValue("MusicNoteOff1", FluentIcons.Common.Symbol.MusicNoteOff1);
					xamlUserType34.AddEnumValue("MusicNoteOff2", FluentIcons.Common.Symbol.MusicNoteOff2);
					xamlUserType34.AddEnumValue("MyLocation", FluentIcons.Common.Symbol.MyLocation);
					xamlUserType34.AddEnumValue("Navigation", FluentIcons.Common.Symbol.Navigation);
					xamlUserType34.AddEnumValue("NavigationBriefcase", FluentIcons.Common.Symbol.NavigationBriefcase);
					xamlUserType34.AddEnumValue("NavigationLocationTarget", FluentIcons.Common.Symbol.NavigationLocationTarget);
					xamlUserType34.AddEnumValue("NavigationPerson", FluentIcons.Common.Symbol.NavigationPerson);
					xamlUserType34.AddEnumValue("NavigationPlay", FluentIcons.Common.Symbol.NavigationPlay);
					xamlUserType34.AddEnumValue("NavigationUnread", FluentIcons.Common.Symbol.NavigationUnread);
					xamlUserType34.AddEnumValue("NetworkCheck", FluentIcons.Common.Symbol.NetworkCheck);
					xamlUserType34.AddEnumValue("New", FluentIcons.Common.Symbol.New);
					xamlUserType34.AddEnumValue("News", FluentIcons.Common.Symbol.News);
					xamlUserType34.AddEnumValue("Next", FluentIcons.Common.Symbol.Next);
					xamlUserType34.AddEnumValue("NextFrame", FluentIcons.Common.Symbol.NextFrame);
					xamlUserType34.AddEnumValue("Note", FluentIcons.Common.Symbol.Note);
					xamlUserType34.AddEnumValue("NoteAdd", FluentIcons.Common.Symbol.NoteAdd);
					xamlUserType34.AddEnumValue("NoteEdit", FluentIcons.Common.Symbol.NoteEdit);
					xamlUserType34.AddEnumValue("NotePin", FluentIcons.Common.Symbol.NotePin);
					xamlUserType34.AddEnumValue("Notebook", FluentIcons.Common.Symbol.Notebook);
					xamlUserType34.AddEnumValue("NotebookAdd", FluentIcons.Common.Symbol.NotebookAdd);
					xamlUserType34.AddEnumValue("NotebookArrowCurveDown", FluentIcons.Common.Symbol.NotebookArrowCurveDown);
					xamlUserType34.AddEnumValue("NotebookError", FluentIcons.Common.Symbol.NotebookError);
					xamlUserType34.AddEnumValue("NotebookEye", FluentIcons.Common.Symbol.NotebookEye);
					xamlUserType34.AddEnumValue("NotebookLightning", FluentIcons.Common.Symbol.NotebookLightning);
					xamlUserType34.AddEnumValue("NotebookQuestionMark", FluentIcons.Common.Symbol.NotebookQuestionMark);
					xamlUserType34.AddEnumValue("NotebookSection", FluentIcons.Common.Symbol.NotebookSection);
					xamlUserType34.AddEnumValue("NotebookSectionArrowRight", FluentIcons.Common.Symbol.NotebookSectionArrowRight);
					xamlUserType34.AddEnumValue("NotebookSubsection", FluentIcons.Common.Symbol.NotebookSubsection);
					xamlUserType34.AddEnumValue("NotebookSync", FluentIcons.Common.Symbol.NotebookSync);
					xamlUserType34.AddEnumValue("Notepad", FluentIcons.Common.Symbol.Notepad);
					xamlUserType34.AddEnumValue("NotepadEdit", FluentIcons.Common.Symbol.NotepadEdit);
					xamlUserType34.AddEnumValue("NotepadPerson", FluentIcons.Common.Symbol.NotepadPerson);
					xamlUserType34.AddEnumValue("NotepadSparkle", FluentIcons.Common.Symbol.NotepadSparkle);
					xamlUserType34.AddEnumValue("NumberCircle0", FluentIcons.Common.Symbol.NumberCircle0);
					xamlUserType34.AddEnumValue("NumberCircle1", FluentIcons.Common.Symbol.NumberCircle1);
					xamlUserType34.AddEnumValue("NumberCircle2", FluentIcons.Common.Symbol.NumberCircle2);
					xamlUserType34.AddEnumValue("NumberCircle3", FluentIcons.Common.Symbol.NumberCircle3);
					xamlUserType34.AddEnumValue("NumberCircle4", FluentIcons.Common.Symbol.NumberCircle4);
					xamlUserType34.AddEnumValue("NumberCircle5", FluentIcons.Common.Symbol.NumberCircle5);
					xamlUserType34.AddEnumValue("NumberCircle6", FluentIcons.Common.Symbol.NumberCircle6);
					xamlUserType34.AddEnumValue("NumberCircle7", FluentIcons.Common.Symbol.NumberCircle7);
					xamlUserType34.AddEnumValue("NumberCircle8", FluentIcons.Common.Symbol.NumberCircle8);
					xamlUserType34.AddEnumValue("NumberCircle9", FluentIcons.Common.Symbol.NumberCircle9);
					xamlUserType34.AddEnumValue("NumberRow", FluentIcons.Common.Symbol.NumberRow);
					xamlUserType34.AddEnumValue("NumberSymbol", FluentIcons.Common.Symbol.NumberSymbol);
					xamlUserType34.AddEnumValue("NumberSymbolDismiss", FluentIcons.Common.Symbol.NumberSymbolDismiss);
					xamlUserType34.AddEnumValue("NumberSymbolSquare", FluentIcons.Common.Symbol.NumberSymbolSquare);
					xamlUserType34.AddEnumValue("Open", FluentIcons.Common.Symbol.Open);
					xamlUserType34.AddEnumValue("OpenFolder", FluentIcons.Common.Symbol.OpenFolder);
					xamlUserType34.AddEnumValue("OpenOff", FluentIcons.Common.Symbol.OpenOff);
					xamlUserType34.AddEnumValue("Options", FluentIcons.Common.Symbol.Options);
					xamlUserType34.AddEnumValue("Organization", FluentIcons.Common.Symbol.Organization);
					xamlUserType34.AddEnumValue("OrganizationHorizontal", FluentIcons.Common.Symbol.OrganizationHorizontal);
					xamlUserType34.AddEnumValue("Orientation", FluentIcons.Common.Symbol.Orientation);
					xamlUserType34.AddEnumValue("Oval", FluentIcons.Common.Symbol.Oval);
					xamlUserType34.AddEnumValue("Oven", FluentIcons.Common.Symbol.Oven);
					xamlUserType34.AddEnumValue("PaddingDown", FluentIcons.Common.Symbol.PaddingDown);
					xamlUserType34.AddEnumValue("PaddingLeft", FluentIcons.Common.Symbol.PaddingLeft);
					xamlUserType34.AddEnumValue("PaddingRight", FluentIcons.Common.Symbol.PaddingRight);
					xamlUserType34.AddEnumValue("PaddingTop", FluentIcons.Common.Symbol.PaddingTop);
					xamlUserType34.AddEnumValue("PageFit", FluentIcons.Common.Symbol.PageFit);
					xamlUserType34.AddEnumValue("PaintBrush", FluentIcons.Common.Symbol.PaintBrush);
					xamlUserType34.AddEnumValue("PaintBrushArrowDown", FluentIcons.Common.Symbol.PaintBrushArrowDown);
					xamlUserType34.AddEnumValue("PaintBrushArrowUp", FluentIcons.Common.Symbol.PaintBrushArrowUp);
					xamlUserType34.AddEnumValue("PaintBrushSparkle", FluentIcons.Common.Symbol.PaintBrushSparkle);
					xamlUserType34.AddEnumValue("PaintBrushSubtract", FluentIcons.Common.Symbol.PaintBrushSubtract);
					xamlUserType34.AddEnumValue("PaintBucket", FluentIcons.Common.Symbol.PaintBucket);
					xamlUserType34.AddEnumValue("PaintBucketBrush", FluentIcons.Common.Symbol.PaintBucketBrush);
					xamlUserType34.AddEnumValue("Pair", FluentIcons.Common.Symbol.Pair);
					xamlUserType34.AddEnumValue("PanelBottom", FluentIcons.Common.Symbol.PanelBottom);
					xamlUserType34.AddEnumValue("PanelBottomContract", FluentIcons.Common.Symbol.PanelBottomContract);
					xamlUserType34.AddEnumValue("PanelBottomExpand", FluentIcons.Common.Symbol.PanelBottomExpand);
					xamlUserType34.AddEnumValue("PanelLeft", FluentIcons.Common.Symbol.PanelLeft);
					xamlUserType34.AddEnumValue("PanelLeftAdd", FluentIcons.Common.Symbol.PanelLeftAdd);
					xamlUserType34.AddEnumValue("PanelLeftContract", FluentIcons.Common.Symbol.PanelLeftContract);
					xamlUserType34.AddEnumValue("PanelLeftDefault", FluentIcons.Common.Symbol.PanelLeftDefault);
					xamlUserType34.AddEnumValue("PanelLeftExpand", FluentIcons.Common.Symbol.PanelLeftExpand);
					xamlUserType34.AddEnumValue("PanelLeftFocusRight", FluentIcons.Common.Symbol.PanelLeftFocusRight);
					xamlUserType34.AddEnumValue("PanelLeftHeader", FluentIcons.Common.Symbol.PanelLeftHeader);
					xamlUserType34.AddEnumValue("PanelLeftHeaderAdd", FluentIcons.Common.Symbol.PanelLeftHeaderAdd);
					xamlUserType34.AddEnumValue("PanelLeftHeaderKey", FluentIcons.Common.Symbol.PanelLeftHeaderKey);
					xamlUserType34.AddEnumValue("PanelLeftKey", FluentIcons.Common.Symbol.PanelLeftKey);
					xamlUserType34.AddEnumValue("PanelLeftText", FluentIcons.Common.Symbol.PanelLeftText);
					xamlUserType34.AddEnumValue("PanelLeftTextAdd", FluentIcons.Common.Symbol.PanelLeftTextAdd);
					xamlUserType34.AddEnumValue("PanelLeftTextDismiss", FluentIcons.Common.Symbol.PanelLeftTextDismiss);
					xamlUserType34.AddEnumValue("PanelRight", FluentIcons.Common.Symbol.PanelRight);
					xamlUserType34.AddEnumValue("PanelRightAdd", FluentIcons.Common.Symbol.PanelRightAdd);
					xamlUserType34.AddEnumValue("PanelRightContract", FluentIcons.Common.Symbol.PanelRightContract);
					xamlUserType34.AddEnumValue("PanelRightCursor", FluentIcons.Common.Symbol.PanelRightCursor);
					xamlUserType34.AddEnumValue("PanelRightExpand", FluentIcons.Common.Symbol.PanelRightExpand);
					xamlUserType34.AddEnumValue("PanelRightGallery", FluentIcons.Common.Symbol.PanelRightGallery);
					xamlUserType34.AddEnumValue("PanelSeparateWindow", FluentIcons.Common.Symbol.PanelSeparateWindow);
					xamlUserType34.AddEnumValue("PanelTopContract", FluentIcons.Common.Symbol.PanelTopContract);
					xamlUserType34.AddEnumValue("PanelTopExpand", FluentIcons.Common.Symbol.PanelTopExpand);
					xamlUserType34.AddEnumValue("PanelTopGallery", FluentIcons.Common.Symbol.PanelTopGallery);
					xamlUserType34.AddEnumValue("Password", FluentIcons.Common.Symbol.Password);
					xamlUserType34.AddEnumValue("Patch", FluentIcons.Common.Symbol.Patch);
					xamlUserType34.AddEnumValue("Patient", FluentIcons.Common.Symbol.Patient);
					xamlUserType34.AddEnumValue("Pause", FluentIcons.Common.Symbol.Pause);
					xamlUserType34.AddEnumValue("PauseCircle", FluentIcons.Common.Symbol.PauseCircle);
					xamlUserType34.AddEnumValue("PauseOff", FluentIcons.Common.Symbol.PauseOff);
					xamlUserType34.AddEnumValue("PauseSettings", FluentIcons.Common.Symbol.PauseSettings);
					xamlUserType34.AddEnumValue("Payment", FluentIcons.Common.Symbol.Payment);
					xamlUserType34.AddEnumValue("PaymentWireless", FluentIcons.Common.Symbol.PaymentWireless);
					xamlUserType34.AddEnumValue("Pen", FluentIcons.Common.Symbol.Pen);
					xamlUserType34.AddEnumValue("PenDismiss", FluentIcons.Common.Symbol.PenDismiss);
					xamlUserType34.AddEnumValue("PenOff", FluentIcons.Common.Symbol.PenOff);
					xamlUserType34.AddEnumValue("PenProhibited", FluentIcons.Common.Symbol.PenProhibited);
					xamlUserType34.AddEnumValue("PenSparkle", FluentIcons.Common.Symbol.PenSparkle);
					xamlUserType34.AddEnumValue("PenSync", FluentIcons.Common.Symbol.PenSync);
					xamlUserType34.AddEnumValue("Pentagon", FluentIcons.Common.Symbol.Pentagon);
					xamlUserType34.AddEnumValue("People", FluentIcons.Common.Symbol.People);
					xamlUserType34.AddEnumValue("PeopleAdd", FluentIcons.Common.Symbol.PeopleAdd);
					xamlUserType34.AddEnumValue("PeopleAudience", FluentIcons.Common.Symbol.PeopleAudience);
					xamlUserType34.AddEnumValue("PeopleCall", FluentIcons.Common.Symbol.PeopleCall);
					xamlUserType34.AddEnumValue("PeopleChat", FluentIcons.Common.Symbol.PeopleChat);
					xamlUserType34.AddEnumValue("PeopleCheckmark", FluentIcons.Common.Symbol.PeopleCheckmark);
					xamlUserType34.AddEnumValue("PeopleCommunity", FluentIcons.Common.Symbol.PeopleCommunity);
					xamlUserType34.AddEnumValue("PeopleCommunityAdd", FluentIcons.Common.Symbol.PeopleCommunityAdd);
					xamlUserType34.AddEnumValue("PeopleEdit", FluentIcons.Common.Symbol.PeopleEdit);
					xamlUserType34.AddEnumValue("PeopleError", FluentIcons.Common.Symbol.PeopleError);
					xamlUserType34.AddEnumValue("PeopleEye", FluentIcons.Common.Symbol.PeopleEye);
					xamlUserType34.AddEnumValue("PeopleLink", FluentIcons.Common.Symbol.PeopleLink);
					xamlUserType34.AddEnumValue("PeopleList", FluentIcons.Common.Symbol.PeopleList);
					xamlUserType34.AddEnumValue("PeopleLock", FluentIcons.Common.Symbol.PeopleLock);
					xamlUserType34.AddEnumValue("PeopleMoney", FluentIcons.Common.Symbol.PeopleMoney);
					xamlUserType34.AddEnumValue("PeopleProhibited", FluentIcons.Common.Symbol.PeopleProhibited);
					xamlUserType34.AddEnumValue("PeopleQueue", FluentIcons.Common.Symbol.PeopleQueue);
					xamlUserType34.AddEnumValue("PeopleSearch", FluentIcons.Common.Symbol.PeopleSearch);
					xamlUserType34.AddEnumValue("PeopleSettings", FluentIcons.Common.Symbol.PeopleSettings);
					xamlUserType34.AddEnumValue("PeopleStar", FluentIcons.Common.Symbol.PeopleStar);
					xamlUserType34.AddEnumValue("PeopleSubtract", FluentIcons.Common.Symbol.PeopleSubtract);
					xamlUserType34.AddEnumValue("PeopleSwap", FluentIcons.Common.Symbol.PeopleSwap);
					xamlUserType34.AddEnumValue("PeopleSync", FluentIcons.Common.Symbol.PeopleSync);
					xamlUserType34.AddEnumValue("PeopleTeam", FluentIcons.Common.Symbol.PeopleTeam);
					xamlUserType34.AddEnumValue("PeopleTeamAdd", FluentIcons.Common.Symbol.PeopleTeamAdd);
					xamlUserType34.AddEnumValue("PeopleTeamDelete", FluentIcons.Common.Symbol.PeopleTeamDelete);
					xamlUserType34.AddEnumValue("PeopleTeamToolbox", FluentIcons.Common.Symbol.PeopleTeamToolbox);
					xamlUserType34.AddEnumValue("PeopleToolbox", FluentIcons.Common.Symbol.PeopleToolbox);
					xamlUserType34.AddEnumValue("Person", FluentIcons.Common.Symbol.Person);
					xamlUserType34.AddEnumValue("Person5", FluentIcons.Common.Symbol.Person5);
					xamlUserType34.AddEnumValue("Person6", FluentIcons.Common.Symbol.Person6);
					xamlUserType34.AddEnumValue("PersonAccounts", FluentIcons.Common.Symbol.PersonAccounts);
					xamlUserType34.AddEnumValue("PersonAdd", FluentIcons.Common.Symbol.PersonAdd);
					xamlUserType34.AddEnumValue("PersonAlert", FluentIcons.Common.Symbol.PersonAlert);
					xamlUserType34.AddEnumValue("PersonAlertOff", FluentIcons.Common.Symbol.PersonAlertOff);
					xamlUserType34.AddEnumValue("PersonArrowBack", FluentIcons.Common.Symbol.PersonArrowBack);
					xamlUserType34.AddEnumValue("PersonArrowLeft", FluentIcons.Common.Symbol.PersonArrowLeft);
					xamlUserType34.AddEnumValue("PersonArrowRight", FluentIcons.Common.Symbol.PersonArrowRight);
					xamlUserType34.AddEnumValue("PersonAvailable", FluentIcons.Common.Symbol.PersonAvailable);
					xamlUserType34.AddEnumValue("PersonBoard", FluentIcons.Common.Symbol.PersonBoard);
					xamlUserType34.AddEnumValue("PersonBoardAdd", FluentIcons.Common.Symbol.PersonBoardAdd);
					xamlUserType34.AddEnumValue("PersonCall", FluentIcons.Common.Symbol.PersonCall);
					xamlUserType34.AddEnumValue("PersonChat", FluentIcons.Common.Symbol.PersonChat);
					xamlUserType34.AddEnumValue("PersonCircle", FluentIcons.Common.Symbol.PersonCircle);
					xamlUserType34.AddEnumValue("PersonClock", FluentIcons.Common.Symbol.PersonClock);
					xamlUserType34.AddEnumValue("PersonDelete", FluentIcons.Common.Symbol.PersonDelete);
					xamlUserType34.AddEnumValue("PersonDesktop", FluentIcons.Common.Symbol.PersonDesktop);
					xamlUserType34.AddEnumValue("PersonEdit", FluentIcons.Common.Symbol.PersonEdit);
					xamlUserType34.AddEnumValue("PersonFeedback", FluentIcons.Common.Symbol.PersonFeedback);
					xamlUserType34.AddEnumValue("PersonHeadHint", FluentIcons.Common.Symbol.PersonHeadHint);
					xamlUserType34.AddEnumValue("PersonHeart", FluentIcons.Common.Symbol.PersonHeart);
					xamlUserType34.AddEnumValue("PersonHome", FluentIcons.Common.Symbol.PersonHome);
					xamlUserType34.AddEnumValue("PersonInfo", FluentIcons.Common.Symbol.PersonInfo);
					xamlUserType34.AddEnumValue("PersonKey", FluentIcons.Common.Symbol.PersonKey);
					xamlUserType34.AddEnumValue("PersonLightbulb", FluentIcons.Common.Symbol.PersonLightbulb);
					xamlUserType34.AddEnumValue("PersonLightning", FluentIcons.Common.Symbol.PersonLightning);
					xamlUserType34.AddEnumValue("PersonLink", FluentIcons.Common.Symbol.PersonLink);
					xamlUserType34.AddEnumValue("PersonLock", FluentIcons.Common.Symbol.PersonLock);
					xamlUserType34.AddEnumValue("PersonMail", FluentIcons.Common.Symbol.PersonMail);
					xamlUserType34.AddEnumValue("PersonMoney", FluentIcons.Common.Symbol.PersonMoney);
					xamlUserType34.AddEnumValue("PersonNote", FluentIcons.Common.Symbol.PersonNote);
					xamlUserType34.AddEnumValue("PersonPasskey", FluentIcons.Common.Symbol.PersonPasskey);
					xamlUserType34.AddEnumValue("PersonPill", FluentIcons.Common.Symbol.PersonPill);
					xamlUserType34.AddEnumValue("PersonProhibited", FluentIcons.Common.Symbol.PersonProhibited);
					xamlUserType34.AddEnumValue("PersonQuestionMark", FluentIcons.Common.Symbol.PersonQuestionMark);
					xamlUserType34.AddEnumValue("PersonRibbon", FluentIcons.Common.Symbol.PersonRibbon);
					xamlUserType34.AddEnumValue("PersonRunning", FluentIcons.Common.Symbol.PersonRunning);
					xamlUserType34.AddEnumValue("PersonSearch", FluentIcons.Common.Symbol.PersonSearch);
					xamlUserType34.AddEnumValue("PersonSettings", FluentIcons.Common.Symbol.PersonSettings);
					xamlUserType34.AddEnumValue("PersonSoundSpatial", FluentIcons.Common.Symbol.PersonSoundSpatial);
					xamlUserType34.AddEnumValue("PersonSquare", FluentIcons.Common.Symbol.PersonSquare);
					xamlUserType34.AddEnumValue("PersonSquareAdd", FluentIcons.Common.Symbol.PersonSquareAdd);
					xamlUserType34.AddEnumValue("PersonSquareCheckmark", FluentIcons.Common.Symbol.PersonSquareCheckmark);
					xamlUserType34.AddEnumValue("PersonStar", FluentIcons.Common.Symbol.PersonStar);
					xamlUserType34.AddEnumValue("PersonStarburst", FluentIcons.Common.Symbol.PersonStarburst);
					xamlUserType34.AddEnumValue("PersonSubtract", FluentIcons.Common.Symbol.PersonSubtract);
					xamlUserType34.AddEnumValue("PersonSuport", FluentIcons.Common.Symbol.PersonSuport);
					xamlUserType34.AddEnumValue("PersonSupport", FluentIcons.Common.Symbol.PersonSupport);
					xamlUserType34.AddEnumValue("PersonSwap", FluentIcons.Common.Symbol.PersonSwap);
					xamlUserType34.AddEnumValue("PersonSync", FluentIcons.Common.Symbol.PersonSync);
					xamlUserType34.AddEnumValue("PersonTag", FluentIcons.Common.Symbol.PersonTag);
					xamlUserType34.AddEnumValue("PersonTentative", FluentIcons.Common.Symbol.PersonTentative);
					xamlUserType34.AddEnumValue("PersonVoice", FluentIcons.Common.Symbol.PersonVoice);
					xamlUserType34.AddEnumValue("PersonWalking", FluentIcons.Common.Symbol.PersonWalking);
					xamlUserType34.AddEnumValue("PersonWarning", FluentIcons.Common.Symbol.PersonWarning);
					xamlUserType34.AddEnumValue("PersonWrench", FluentIcons.Common.Symbol.PersonWrench);
					xamlUserType34.AddEnumValue("Phone", FluentIcons.Common.Symbol.Phone);
					xamlUserType34.AddEnumValue("PhoneAdd", FluentIcons.Common.Symbol.PhoneAdd);
					xamlUserType34.AddEnumValue("PhoneArrowRight", FluentIcons.Common.Symbol.PhoneArrowRight);
					xamlUserType34.AddEnumValue("PhoneChat", FluentIcons.Common.Symbol.PhoneChat);
					xamlUserType34.AddEnumValue("PhoneCheckmark", FluentIcons.Common.Symbol.PhoneCheckmark);
					xamlUserType34.AddEnumValue("PhoneDesktop", FluentIcons.Common.Symbol.PhoneDesktop);
					xamlUserType34.AddEnumValue("PhoneDesktopAdd", FluentIcons.Common.Symbol.PhoneDesktopAdd);
					xamlUserType34.AddEnumValue("PhoneDismiss", FluentIcons.Common.Symbol.PhoneDismiss);
					xamlUserType34.AddEnumValue("PhoneEdit", FluentIcons.Common.Symbol.PhoneEdit);
					xamlUserType34.AddEnumValue("PhoneEraser", FluentIcons.Common.Symbol.PhoneEraser);
					xamlUserType34.AddEnumValue("PhoneFooterArrowDown", FluentIcons.Common.Symbol.PhoneFooterArrowDown);
					xamlUserType34.AddEnumValue("PhoneHeaderArrowUp", FluentIcons.Common.Symbol.PhoneHeaderArrowUp);
					xamlUserType34.AddEnumValue("PhoneKey", FluentIcons.Common.Symbol.PhoneKey);
					xamlUserType34.AddEnumValue("PhoneLaptop", FluentIcons.Common.Symbol.PhoneLaptop);
					xamlUserType34.AddEnumValue("PhoneLinkSetup", FluentIcons.Common.Symbol.PhoneLinkSetup);
					xamlUserType34.AddEnumValue("PhoneLock", FluentIcons.Common.Symbol.PhoneLock);
					xamlUserType34.AddEnumValue("PhonePageHeader", FluentIcons.Common.Symbol.PhonePageHeader);
					xamlUserType34.AddEnumValue("PhonePagination", FluentIcons.Common.Symbol.PhonePagination);
					xamlUserType34.AddEnumValue("PhoneScreenTime", FluentIcons.Common.Symbol.PhoneScreenTime);
					xamlUserType34.AddEnumValue("PhoneShake", FluentIcons.Common.Symbol.PhoneShake);
					xamlUserType34.AddEnumValue("PhoneSpanIn", FluentIcons.Common.Symbol.PhoneSpanIn);
					xamlUserType34.AddEnumValue("PhoneSpanOut", FluentIcons.Common.Symbol.PhoneSpanOut);
					xamlUserType34.AddEnumValue("PhoneSpeaker", FluentIcons.Common.Symbol.PhoneSpeaker);
					xamlUserType34.AddEnumValue("PhoneStatusBar", FluentIcons.Common.Symbol.PhoneStatusBar);
					xamlUserType34.AddEnumValue("PhoneTablet", FluentIcons.Common.Symbol.PhoneTablet);
					xamlUserType34.AddEnumValue("PhoneUpdate", FluentIcons.Common.Symbol.PhoneUpdate);
					xamlUserType34.AddEnumValue("PhoneUpdateCheckmark", FluentIcons.Common.Symbol.PhoneUpdateCheckmark);
					xamlUserType34.AddEnumValue("PhoneVerticalScroll", FluentIcons.Common.Symbol.PhoneVerticalScroll);
					xamlUserType34.AddEnumValue("PhoneVibrate", FluentIcons.Common.Symbol.PhoneVibrate);
					xamlUserType34.AddEnumValue("PhotoFilter", FluentIcons.Common.Symbol.PhotoFilter);
					xamlUserType34.AddEnumValue("Pi", FluentIcons.Common.Symbol.Pi);
					xamlUserType34.AddEnumValue("PictureInPicture", FluentIcons.Common.Symbol.PictureInPicture);
					xamlUserType34.AddEnumValue("PictureInPictureEnter", FluentIcons.Common.Symbol.PictureInPictureEnter);
					xamlUserType34.AddEnumValue("PictureInPictureExit", FluentIcons.Common.Symbol.PictureInPictureExit);
					xamlUserType34.AddEnumValue("Pill", FluentIcons.Common.Symbol.Pill);
					xamlUserType34.AddEnumValue("Pin", FluentIcons.Common.Symbol.Pin);
					xamlUserType34.AddEnumValue("PinGlobe", FluentIcons.Common.Symbol.PinGlobe);
					xamlUserType34.AddEnumValue("PinOff", FluentIcons.Common.Symbol.PinOff);
					xamlUserType34.AddEnumValue("Pipeline", FluentIcons.Common.Symbol.Pipeline);
					xamlUserType34.AddEnumValue("PipelineAdd", FluentIcons.Common.Symbol.PipelineAdd);
					xamlUserType34.AddEnumValue("PipelineArrowCurveDown", FluentIcons.Common.Symbol.PipelineArrowCurveDown);
					xamlUserType34.AddEnumValue("PipelinePlay", FluentIcons.Common.Symbol.PipelinePlay);
					xamlUserType34.AddEnumValue("Pivot", FluentIcons.Common.Symbol.Pivot);
					xamlUserType34.AddEnumValue("PlantCattail", FluentIcons.Common.Symbol.PlantCattail);
					xamlUserType34.AddEnumValue("PlantGrass", FluentIcons.Common.Symbol.PlantGrass);
					xamlUserType34.AddEnumValue("PlantRagweed", FluentIcons.Common.Symbol.PlantRagweed);
					xamlUserType34.AddEnumValue("Play", FluentIcons.Common.Symbol.Play);
					xamlUserType34.AddEnumValue("PlayCircle", FluentIcons.Common.Symbol.PlayCircle);
					xamlUserType34.AddEnumValue("PlayCircleHint", FluentIcons.Common.Symbol.PlayCircleHint);
					xamlUserType34.AddEnumValue("PlayCircleHintHalf", FluentIcons.Common.Symbol.PlayCircleHintHalf);
					xamlUserType34.AddEnumValue("PlayCircleSparkle", FluentIcons.Common.Symbol.PlayCircleSparkle);
					xamlUserType34.AddEnumValue("PlaySettings", FluentIcons.Common.Symbol.PlaySettings);
					xamlUserType34.AddEnumValue("PlayingCards", FluentIcons.Common.Symbol.PlayingCards);
					xamlUserType34.AddEnumValue("PlugConnected", FluentIcons.Common.Symbol.PlugConnected);
					xamlUserType34.AddEnumValue("PlugConnectedAdd", FluentIcons.Common.Symbol.PlugConnectedAdd);
					xamlUserType34.AddEnumValue("PlugConnectedCheckmark", FluentIcons.Common.Symbol.PlugConnectedCheckmark);
					xamlUserType34.AddEnumValue("PlugConnectedSettings", FluentIcons.Common.Symbol.PlugConnectedSettings);
					xamlUserType34.AddEnumValue("PlugDisconnected", FluentIcons.Common.Symbol.PlugDisconnected);
					xamlUserType34.AddEnumValue("PointScan", FluentIcons.Common.Symbol.PointScan);
					xamlUserType34.AddEnumValue("Poll", FluentIcons.Common.Symbol.Poll);
					xamlUserType34.AddEnumValue("PollHorizontal", FluentIcons.Common.Symbol.PollHorizontal);
					xamlUserType34.AddEnumValue("PollOff", FluentIcons.Common.Symbol.PollOff);
					xamlUserType34.AddEnumValue("PortHdmi", FluentIcons.Common.Symbol.PortHdmi);
					xamlUserType34.AddEnumValue("PortMicroUsb", FluentIcons.Common.Symbol.PortMicroUsb);
					xamlUserType34.AddEnumValue("PortUsbA", FluentIcons.Common.Symbol.PortUsbA);
					xamlUserType34.AddEnumValue("PortUsbC", FluentIcons.Common.Symbol.PortUsbC);
					xamlUserType34.AddEnumValue("PositionBackward", FluentIcons.Common.Symbol.PositionBackward);
					xamlUserType34.AddEnumValue("PositionForward", FluentIcons.Common.Symbol.PositionForward);
					xamlUserType34.AddEnumValue("PositionToBack", FluentIcons.Common.Symbol.PositionToBack);
					xamlUserType34.AddEnumValue("PositionToFront", FluentIcons.Common.Symbol.PositionToFront);
					xamlUserType34.AddEnumValue("Power", FluentIcons.Common.Symbol.Power);
					xamlUserType34.AddEnumValue("Predictions", FluentIcons.Common.Symbol.Predictions);
					xamlUserType34.AddEnumValue("Premium", FluentIcons.Common.Symbol.Premium);
					xamlUserType34.AddEnumValue("PremiumPerson", FluentIcons.Common.Symbol.PremiumPerson);
					xamlUserType34.AddEnumValue("PresenceAvailable", FluentIcons.Common.Symbol.PresenceAvailable);
					xamlUserType34.AddEnumValue("PresenceAway", FluentIcons.Common.Symbol.PresenceAway);
					xamlUserType34.AddEnumValue("PresenceBlocked", FluentIcons.Common.Symbol.PresenceBlocked);
					xamlUserType34.AddEnumValue("PresenceBusy", FluentIcons.Common.Symbol.PresenceBusy);
					xamlUserType34.AddEnumValue("PresenceDnd", FluentIcons.Common.Symbol.PresenceDnd);
					xamlUserType34.AddEnumValue("PresenceOffline", FluentIcons.Common.Symbol.PresenceOffline);
					xamlUserType34.AddEnumValue("PresenceOof", FluentIcons.Common.Symbol.PresenceOof);
					xamlUserType34.AddEnumValue("PresenceTentative", FluentIcons.Common.Symbol.PresenceTentative);
					xamlUserType34.AddEnumValue("PresenceUnknown", FluentIcons.Common.Symbol.PresenceUnknown);
					xamlUserType34.AddEnumValue("Presenter", FluentIcons.Common.Symbol.Presenter);
					xamlUserType34.AddEnumValue("PresenterOff", FluentIcons.Common.Symbol.PresenterOff);
					xamlUserType34.AddEnumValue("PreviewLink", FluentIcons.Common.Symbol.PreviewLink);
					xamlUserType34.AddEnumValue("Previous", FluentIcons.Common.Symbol.Previous);
					xamlUserType34.AddEnumValue("PreviousFrame", FluentIcons.Common.Symbol.PreviousFrame);
					xamlUserType34.AddEnumValue("Print", FluentIcons.Common.Symbol.Print);
					xamlUserType34.AddEnumValue("PrintAdd", FluentIcons.Common.Symbol.PrintAdd);
					xamlUserType34.AddEnumValue("Production", FluentIcons.Common.Symbol.Production);
					xamlUserType34.AddEnumValue("ProductionCheckmark", FluentIcons.Common.Symbol.ProductionCheckmark);
					xamlUserType34.AddEnumValue("Prohibited", FluentIcons.Common.Symbol.Prohibited);
					xamlUserType34.AddEnumValue("ProhibitedMultiple", FluentIcons.Common.Symbol.ProhibitedMultiple);
					xamlUserType34.AddEnumValue("ProhibitedNote", FluentIcons.Common.Symbol.ProhibitedNote);
					xamlUserType34.AddEnumValue("ProjectionScreen", FluentIcons.Common.Symbol.ProjectionScreen);
					xamlUserType34.AddEnumValue("ProjectionScreenDismiss", FluentIcons.Common.Symbol.ProjectionScreenDismiss);
					xamlUserType34.AddEnumValue("ProjectionScreenText", FluentIcons.Common.Symbol.ProjectionScreenText);
					xamlUserType34.AddEnumValue("Prompt", FluentIcons.Common.Symbol.Prompt);
					xamlUserType34.AddEnumValue("ProtocolHandler", FluentIcons.Common.Symbol.ProtocolHandler);
					xamlUserType34.AddEnumValue("Pulse", FluentIcons.Common.Symbol.Pulse);
					xamlUserType34.AddEnumValue("PulseSquare", FluentIcons.Common.Symbol.PulseSquare);
					xamlUserType34.AddEnumValue("PuzzleCube", FluentIcons.Common.Symbol.PuzzleCube);
					xamlUserType34.AddEnumValue("PuzzleCubePiece", FluentIcons.Common.Symbol.PuzzleCubePiece);
					xamlUserType34.AddEnumValue("PuzzlePiece", FluentIcons.Common.Symbol.PuzzlePiece);
					xamlUserType34.AddEnumValue("PuzzlePieceShield", FluentIcons.Common.Symbol.PuzzlePieceShield);
					xamlUserType34.AddEnumValue("QrCode", FluentIcons.Common.Symbol.QrCode);
					xamlUserType34.AddEnumValue("Question", FluentIcons.Common.Symbol.Question);
					xamlUserType34.AddEnumValue("QuestionCircle", FluentIcons.Common.Symbol.QuestionCircle);
					xamlUserType34.AddEnumValue("QuizNew", FluentIcons.Common.Symbol.QuizNew);
					xamlUserType34.AddEnumValue("Radar", FluentIcons.Common.Symbol.Radar);
					xamlUserType34.AddEnumValue("RadarCheckmark", FluentIcons.Common.Symbol.RadarCheckmark);
					xamlUserType34.AddEnumValue("RadarRectangleMultiple", FluentIcons.Common.Symbol.RadarRectangleMultiple);
					xamlUserType34.AddEnumValue("RadioButton", FluentIcons.Common.Symbol.RadioButton);
					xamlUserType34.AddEnumValue("Ram", FluentIcons.Common.Symbol.Ram);
					xamlUserType34.AddEnumValue("RatingMature", FluentIcons.Common.Symbol.RatingMature);
					xamlUserType34.AddEnumValue("RatioOneToOne", FluentIcons.Common.Symbol.RatioOneToOne);
					xamlUserType34.AddEnumValue("ReOrder", FluentIcons.Common.Symbol.ReOrder);
					xamlUserType34.AddEnumValue("ReOrderDotsHorizontal", FluentIcons.Common.Symbol.ReOrderDotsHorizontal);
					xamlUserType34.AddEnumValue("ReOrderDotsVertical", FluentIcons.Common.Symbol.ReOrderDotsVertical);
					xamlUserType34.AddEnumValue("ReOrderVertical", FluentIcons.Common.Symbol.ReOrderVertical);
					xamlUserType34.AddEnumValue("ReadAloud", FluentIcons.Common.Symbol.ReadAloud);
					xamlUserType34.AddEnumValue("ReadingList", FluentIcons.Common.Symbol.ReadingList);
					xamlUserType34.AddEnumValue("ReadingListAdd", FluentIcons.Common.Symbol.ReadingListAdd);
					xamlUserType34.AddEnumValue("ReadingModeMobile", FluentIcons.Common.Symbol.ReadingModeMobile);
					xamlUserType34.AddEnumValue("RealEstate", FluentIcons.Common.Symbol.RealEstate);
					xamlUserType34.AddEnumValue("Receipt", FluentIcons.Common.Symbol.Receipt);
					xamlUserType34.AddEnumValue("ReceiptAdd", FluentIcons.Common.Symbol.ReceiptAdd);
					xamlUserType34.AddEnumValue("ReceiptBag", FluentIcons.Common.Symbol.ReceiptBag);
					xamlUserType34.AddEnumValue("ReceiptCube", FluentIcons.Common.Symbol.ReceiptCube);
					xamlUserType34.AddEnumValue("ReceiptMoney", FluentIcons.Common.Symbol.ReceiptMoney);
					xamlUserType34.AddEnumValue("ReceiptPlay", FluentIcons.Common.Symbol.ReceiptPlay);
					xamlUserType34.AddEnumValue("ReceiptSearch", FluentIcons.Common.Symbol.ReceiptSearch);
					xamlUserType34.AddEnumValue("ReceiptSparkles", FluentIcons.Common.Symbol.ReceiptSparkles);
					xamlUserType34.AddEnumValue("Record", FluentIcons.Common.Symbol.Record);
					xamlUserType34.AddEnumValue("RecordStop", FluentIcons.Common.Symbol.RecordStop);
					xamlUserType34.AddEnumValue("RectangleLandscape", FluentIcons.Common.Symbol.RectangleLandscape);
					xamlUserType34.AddEnumValue("RectangleLandscapeHintCopy", FluentIcons.Common.Symbol.RectangleLandscapeHintCopy);
					xamlUserType34.AddEnumValue("RectangleLandscapeSparkle", FluentIcons.Common.Symbol.RectangleLandscapeSparkle);
					xamlUserType34.AddEnumValue("RectangleLandscapeSync", FluentIcons.Common.Symbol.RectangleLandscapeSync);
					xamlUserType34.AddEnumValue("RectangleLandscapeSyncOff", FluentIcons.Common.Symbol.RectangleLandscapeSyncOff);
					xamlUserType34.AddEnumValue("RectanglePortraitLocationTarget", FluentIcons.Common.Symbol.RectanglePortraitLocationTarget);
					xamlUserType34.AddEnumValue("Recycle", FluentIcons.Common.Symbol.Recycle);
					xamlUserType34.AddEnumValue("RemixAdd", FluentIcons.Common.Symbol.RemixAdd);
					xamlUserType34.AddEnumValue("Remote", FluentIcons.Common.Symbol.Remote);
					xamlUserType34.AddEnumValue("Rename", FluentIcons.Common.Symbol.Rename);
					xamlUserType34.AddEnumValue("Reorder", FluentIcons.Common.Symbol.Reorder);
					xamlUserType34.AddEnumValue("Replay", FluentIcons.Common.Symbol.Replay);
					xamlUserType34.AddEnumValue("Resize", FluentIcons.Common.Symbol.Resize);
					xamlUserType34.AddEnumValue("ResizeImage", FluentIcons.Common.Symbol.ResizeImage);
					xamlUserType34.AddEnumValue("ResizeLarge", FluentIcons.Common.Symbol.ResizeLarge);
					xamlUserType34.AddEnumValue("ResizeSmall", FluentIcons.Common.Symbol.ResizeSmall);
					xamlUserType34.AddEnumValue("ResizeTable", FluentIcons.Common.Symbol.ResizeTable);
					xamlUserType34.AddEnumValue("ResizeVideo", FluentIcons.Common.Symbol.ResizeVideo);
					xamlUserType34.AddEnumValue("Reward", FluentIcons.Common.Symbol.Reward);
					xamlUserType34.AddEnumValue("Rewind", FluentIcons.Common.Symbol.Rewind);
					xamlUserType34.AddEnumValue("Rhombus", FluentIcons.Common.Symbol.Rhombus);
					xamlUserType34.AddEnumValue("Ribbon", FluentIcons.Common.Symbol.Ribbon);
					xamlUserType34.AddEnumValue("RibbonAdd", FluentIcons.Common.Symbol.RibbonAdd);
					xamlUserType34.AddEnumValue("RibbonOff", FluentIcons.Common.Symbol.RibbonOff);
					xamlUserType34.AddEnumValue("RibbonStar", FluentIcons.Common.Symbol.RibbonStar);
					xamlUserType34.AddEnumValue("Road", FluentIcons.Common.Symbol.Road);
					xamlUserType34.AddEnumValue("RoadCone", FluentIcons.Common.Symbol.RoadCone);
					xamlUserType34.AddEnumValue("Rocket", FluentIcons.Common.Symbol.Rocket);
					xamlUserType34.AddEnumValue("RotateLeft", FluentIcons.Common.Symbol.RotateLeft);
					xamlUserType34.AddEnumValue("RotateRight", FluentIcons.Common.Symbol.RotateRight);
					xamlUserType34.AddEnumValue("Router", FluentIcons.Common.Symbol.Router);
					xamlUserType34.AddEnumValue("RowChild", FluentIcons.Common.Symbol.RowChild);
					xamlUserType34.AddEnumValue("RowTriple", FluentIcons.Common.Symbol.RowTriple);
					xamlUserType34.AddEnumValue("Rss", FluentIcons.Common.Symbol.Rss);
					xamlUserType34.AddEnumValue("Ruler", FluentIcons.Common.Symbol.Ruler);
					xamlUserType34.AddEnumValue("Run", FluentIcons.Common.Symbol.Run);
					xamlUserType34.AddEnumValue("Sanitize", FluentIcons.Common.Symbol.Sanitize);
					xamlUserType34.AddEnumValue("Save", FluentIcons.Common.Symbol.Save);
					xamlUserType34.AddEnumValue("SaveArrowRight", FluentIcons.Common.Symbol.SaveArrowRight);
					xamlUserType34.AddEnumValue("SaveCopy", FluentIcons.Common.Symbol.SaveCopy);
					xamlUserType34.AddEnumValue("SaveEdit", FluentIcons.Common.Symbol.SaveEdit);
					xamlUserType34.AddEnumValue("SaveImage", FluentIcons.Common.Symbol.SaveImage);
					xamlUserType34.AddEnumValue("SaveMultiple", FluentIcons.Common.Symbol.SaveMultiple);
					xamlUserType34.AddEnumValue("SaveSearch", FluentIcons.Common.Symbol.SaveSearch);
					xamlUserType34.AddEnumValue("SaveSync", FluentIcons.Common.Symbol.SaveSync);
					xamlUserType34.AddEnumValue("Savings", FluentIcons.Common.Symbol.Savings);
					xamlUserType34.AddEnumValue("ScaleFill", FluentIcons.Common.Symbol.ScaleFill);
					xamlUserType34.AddEnumValue("ScaleFit", FluentIcons.Common.Symbol.ScaleFit);
					xamlUserType34.AddEnumValue("Scales", FluentIcons.Common.Symbol.Scales);
					xamlUserType34.AddEnumValue("Scan", FluentIcons.Common.Symbol.Scan);
					xamlUserType34.AddEnumValue("ScanCamera", FluentIcons.Common.Symbol.ScanCamera);
					xamlUserType34.AddEnumValue("ScanDash", FluentIcons.Common.Symbol.ScanDash);
					xamlUserType34.AddEnumValue("ScanObject", FluentIcons.Common.Symbol.ScanObject);
					xamlUserType34.AddEnumValue("ScanPerson", FluentIcons.Common.Symbol.ScanPerson);
					xamlUserType34.AddEnumValue("ScanTable", FluentIcons.Common.Symbol.ScanTable);
					xamlUserType34.AddEnumValue("ScanText", FluentIcons.Common.Symbol.ScanText);
					xamlUserType34.AddEnumValue("ScanThumbUp", FluentIcons.Common.Symbol.ScanThumbUp);
					xamlUserType34.AddEnumValue("ScanThumbUpOff", FluentIcons.Common.Symbol.ScanThumbUpOff);
					xamlUserType34.AddEnumValue("ScanType", FluentIcons.Common.Symbol.ScanType);
					xamlUserType34.AddEnumValue("ScanTypeCheckmark", FluentIcons.Common.Symbol.ScanTypeCheckmark);
					xamlUserType34.AddEnumValue("ScanTypeOff", FluentIcons.Common.Symbol.ScanTypeOff);
					xamlUserType34.AddEnumValue("Scratchpad", FluentIcons.Common.Symbol.Scratchpad);
					xamlUserType34.AddEnumValue("ScreenCut", FluentIcons.Common.Symbol.ScreenCut);
					xamlUserType34.AddEnumValue("ScreenPerson", FluentIcons.Common.Symbol.ScreenPerson);
					xamlUserType34.AddEnumValue("ScreenSearch", FluentIcons.Common.Symbol.ScreenSearch);
					xamlUserType34.AddEnumValue("Screenshot", FluentIcons.Common.Symbol.Screenshot);
					xamlUserType34.AddEnumValue("ScreenshotRecord", FluentIcons.Common.Symbol.ScreenshotRecord);
					xamlUserType34.AddEnumValue("Script", FluentIcons.Common.Symbol.Script);
					xamlUserType34.AddEnumValue("Search", FluentIcons.Common.Symbol.Search);
					xamlUserType34.AddEnumValue("SearchInfo", FluentIcons.Common.Symbol.SearchInfo);
					xamlUserType34.AddEnumValue("SearchSettings", FluentIcons.Common.Symbol.SearchSettings);
					xamlUserType34.AddEnumValue("SearchShield", FluentIcons.Common.Symbol.SearchShield);
					xamlUserType34.AddEnumValue("SearchSparkle", FluentIcons.Common.Symbol.SearchSparkle);
					xamlUserType34.AddEnumValue("SearchSquare", FluentIcons.Common.Symbol.SearchSquare);
					xamlUserType34.AddEnumValue("SearchVisual", FluentIcons.Common.Symbol.SearchVisual);
					xamlUserType34.AddEnumValue("Seat", FluentIcons.Common.Symbol.Seat);
					xamlUserType34.AddEnumValue("SeatAdd", FluentIcons.Common.Symbol.SeatAdd);
					xamlUserType34.AddEnumValue("SelectAllOff", FluentIcons.Common.Symbol.SelectAllOff);
					xamlUserType34.AddEnumValue("SelectAllOn", FluentIcons.Common.Symbol.SelectAllOn);
					xamlUserType34.AddEnumValue("SelectObject", FluentIcons.Common.Symbol.SelectObject);
					xamlUserType34.AddEnumValue("SelectObjectSkew", FluentIcons.Common.Symbol.SelectObjectSkew);
					xamlUserType34.AddEnumValue("SelectObjectSkewDismiss", FluentIcons.Common.Symbol.SelectObjectSkewDismiss);
					xamlUserType34.AddEnumValue("SelectObjectSkewEdit", FluentIcons.Common.Symbol.SelectObjectSkewEdit);
					xamlUserType34.AddEnumValue("Send", FluentIcons.Common.Symbol.Send);
					xamlUserType34.AddEnumValue("SendBeaker", FluentIcons.Common.Symbol.SendBeaker);
					xamlUserType34.AddEnumValue("SendClock", FluentIcons.Common.Symbol.SendClock);
					xamlUserType34.AddEnumValue("SendCopy", FluentIcons.Common.Symbol.SendCopy);
					xamlUserType34.AddEnumValue("SendPerson", FluentIcons.Common.Symbol.SendPerson);
					xamlUserType34.AddEnumValue("SerialPort", FluentIcons.Common.Symbol.SerialPort);
					xamlUserType34.AddEnumValue("Server", FluentIcons.Common.Symbol.Server);
					xamlUserType34.AddEnumValue("ServerLink", FluentIcons.Common.Symbol.ServerLink);
					xamlUserType34.AddEnumValue("ServerMultiple", FluentIcons.Common.Symbol.ServerMultiple);
					xamlUserType34.AddEnumValue("ServerPlay", FluentIcons.Common.Symbol.ServerPlay);
					xamlUserType34.AddEnumValue("ServiceBell", FluentIcons.Common.Symbol.ServiceBell);
					xamlUserType34.AddEnumValue("Settings", FluentIcons.Common.Symbol.Settings);
					xamlUserType34.AddEnumValue("SettingsChat", FluentIcons.Common.Symbol.SettingsChat);
					xamlUserType34.AddEnumValue("SettingsCogMultiple", FluentIcons.Common.Symbol.SettingsCogMultiple);
					xamlUserType34.AddEnumValue("ShapeExclude", FluentIcons.Common.Symbol.ShapeExclude);
					xamlUserType34.AddEnumValue("ShapeIntersect", FluentIcons.Common.Symbol.ShapeIntersect);
					xamlUserType34.AddEnumValue("ShapeOrganic", FluentIcons.Common.Symbol.ShapeOrganic);
					xamlUserType34.AddEnumValue("ShapeSubtract", FluentIcons.Common.Symbol.ShapeSubtract);
					xamlUserType34.AddEnumValue("ShapeUnion", FluentIcons.Common.Symbol.ShapeUnion);
					xamlUserType34.AddEnumValue("Shapes", FluentIcons.Common.Symbol.Shapes);
					xamlUserType34.AddEnumValue("Share", FluentIcons.Common.Symbol.Share);
					xamlUserType34.AddEnumValue("ShareAndroid", FluentIcons.Common.Symbol.ShareAndroid);
					xamlUserType34.AddEnumValue("ShareCloseTray", FluentIcons.Common.Symbol.ShareCloseTray);
					xamlUserType34.AddEnumValue("ShareIos", FluentIcons.Common.Symbol.ShareIos);
					xamlUserType34.AddEnumValue("ShareMultiple", FluentIcons.Common.Symbol.ShareMultiple);
					xamlUserType34.AddEnumValue("ShareScreenPerson", FluentIcons.Common.Symbol.ShareScreenPerson);
					xamlUserType34.AddEnumValue("ShareScreenPersonOverlay", FluentIcons.Common.Symbol.ShareScreenPersonOverlay);
					xamlUserType34.AddEnumValue("ShareScreenPersonOverlayInside", FluentIcons.Common.Symbol.ShareScreenPersonOverlayInside);
					xamlUserType34.AddEnumValue("ShareScreenPersonP", FluentIcons.Common.Symbol.ShareScreenPersonP);
					xamlUserType34.AddEnumValue("ShareScreenStart", FluentIcons.Common.Symbol.ShareScreenStart);
					xamlUserType34.AddEnumValue("ShareScreenStop", FluentIcons.Common.Symbol.ShareScreenStop);
					xamlUserType34.AddEnumValue("Shield", FluentIcons.Common.Symbol.Shield);
					xamlUserType34.AddEnumValue("ShieldAdd", FluentIcons.Common.Symbol.ShieldAdd);
					xamlUserType34.AddEnumValue("ShieldArrowRight", FluentIcons.Common.Symbol.ShieldArrowRight);
					xamlUserType34.AddEnumValue("ShieldBadge", FluentIcons.Common.Symbol.ShieldBadge);
					xamlUserType34.AddEnumValue("ShieldCheckmark", FluentIcons.Common.Symbol.ShieldCheckmark);
					xamlUserType34.AddEnumValue("ShieldDismiss", FluentIcons.Common.Symbol.ShieldDismiss);
					xamlUserType34.AddEnumValue("ShieldDismissShield", FluentIcons.Common.Symbol.ShieldDismissShield);
					xamlUserType34.AddEnumValue("ShieldError", FluentIcons.Common.Symbol.ShieldError);
					xamlUserType34.AddEnumValue("ShieldGlobe", FluentIcons.Common.Symbol.ShieldGlobe);
					xamlUserType34.AddEnumValue("ShieldKeyhole", FluentIcons.Common.Symbol.ShieldKeyhole);
					xamlUserType34.AddEnumValue("ShieldLock", FluentIcons.Common.Symbol.ShieldLock);
					xamlUserType34.AddEnumValue("ShieldPerson", FluentIcons.Common.Symbol.ShieldPerson);
					xamlUserType34.AddEnumValue("ShieldPersonAdd", FluentIcons.Common.Symbol.ShieldPersonAdd);
					xamlUserType34.AddEnumValue("ShieldProhibited", FluentIcons.Common.Symbol.ShieldProhibited);
					xamlUserType34.AddEnumValue("ShieldQuestion", FluentIcons.Common.Symbol.ShieldQuestion);
					xamlUserType34.AddEnumValue("ShieldTask", FluentIcons.Common.Symbol.ShieldTask);
					xamlUserType34.AddEnumValue("Shifts", FluentIcons.Common.Symbol.Shifts);
					xamlUserType34.AddEnumValue("Shifts30Minutes", FluentIcons.Common.Symbol.Shifts30Minutes);
					xamlUserType34.AddEnumValue("ShiftsActivity", FluentIcons.Common.Symbol.ShiftsActivity);
					xamlUserType34.AddEnumValue("ShiftsAdd", FluentIcons.Common.Symbol.ShiftsAdd);
					xamlUserType34.AddEnumValue("ShiftsAvailability", FluentIcons.Common.Symbol.ShiftsAvailability);
					xamlUserType34.AddEnumValue("ShiftsCheckmark", FluentIcons.Common.Symbol.ShiftsCheckmark);
					xamlUserType34.AddEnumValue("ShiftsDay", FluentIcons.Common.Symbol.ShiftsDay);
					xamlUserType34.AddEnumValue("ShiftsOpen", FluentIcons.Common.Symbol.ShiftsOpen);
					xamlUserType34.AddEnumValue("ShiftsProhibited", FluentIcons.Common.Symbol.ShiftsProhibited);
					xamlUserType34.AddEnumValue("ShiftsQuestionMark", FluentIcons.Common.Symbol.ShiftsQuestionMark);
					xamlUserType34.AddEnumValue("ShiftsTeam", FluentIcons.Common.Symbol.ShiftsTeam);
					xamlUserType34.AddEnumValue("ShoppingBag", FluentIcons.Common.Symbol.ShoppingBag);
					xamlUserType34.AddEnumValue("ShoppingBagAdd", FluentIcons.Common.Symbol.ShoppingBagAdd);
					xamlUserType34.AddEnumValue("ShoppingBagArrowLeft", FluentIcons.Common.Symbol.ShoppingBagArrowLeft);
					xamlUserType34.AddEnumValue("ShoppingBagDismiss", FluentIcons.Common.Symbol.ShoppingBagDismiss);
					xamlUserType34.AddEnumValue("ShoppingBagPause", FluentIcons.Common.Symbol.ShoppingBagPause);
					xamlUserType34.AddEnumValue("ShoppingBagPercent", FluentIcons.Common.Symbol.ShoppingBagPercent);
					xamlUserType34.AddEnumValue("ShoppingBagPlay", FluentIcons.Common.Symbol.ShoppingBagPlay);
					xamlUserType34.AddEnumValue("ShoppingBagTag", FluentIcons.Common.Symbol.ShoppingBagTag);
					xamlUserType34.AddEnumValue("Shortpick", FluentIcons.Common.Symbol.Shortpick);
					xamlUserType34.AddEnumValue("Showerhead", FluentIcons.Common.Symbol.Showerhead);
					xamlUserType34.AddEnumValue("SidebarSearch", FluentIcons.Common.Symbol.SidebarSearch);
					xamlUserType34.AddEnumValue("SignOut", FluentIcons.Common.Symbol.SignOut);
					xamlUserType34.AddEnumValue("Signature", FluentIcons.Common.Symbol.Signature);
					xamlUserType34.AddEnumValue("Sim", FluentIcons.Common.Symbol.Sim);
					xamlUserType34.AddEnumValue("SkipBack10", FluentIcons.Common.Symbol.SkipBack10);
					xamlUserType34.AddEnumValue("SkipBack15", FluentIcons.Common.Symbol.SkipBack15);
					xamlUserType34.AddEnumValue("SkipForward10", FluentIcons.Common.Symbol.SkipForward10);
					xamlUserType34.AddEnumValue("SkipForward15", FluentIcons.Common.Symbol.SkipForward15);
					xamlUserType34.AddEnumValue("SkipForward30", FluentIcons.Common.Symbol.SkipForward30);
					xamlUserType34.AddEnumValue("SkipForwardTab", FluentIcons.Common.Symbol.SkipForwardTab);
					xamlUserType34.AddEnumValue("SlashForward", FluentIcons.Common.Symbol.SlashForward);
					xamlUserType34.AddEnumValue("Sleep", FluentIcons.Common.Symbol.Sleep);
					xamlUserType34.AddEnumValue("SlideAdd", FluentIcons.Common.Symbol.SlideAdd);
					xamlUserType34.AddEnumValue("SlideArrowRight", FluentIcons.Common.Symbol.SlideArrowRight);
					xamlUserType34.AddEnumValue("SlideEraser", FluentIcons.Common.Symbol.SlideEraser);
					xamlUserType34.AddEnumValue("SlideGrid", FluentIcons.Common.Symbol.SlideGrid);
					xamlUserType34.AddEnumValue("SlideHide", FluentIcons.Common.Symbol.SlideHide);
					xamlUserType34.AddEnumValue("SlideLayout", FluentIcons.Common.Symbol.SlideLayout);
					xamlUserType34.AddEnumValue("SlideLink", FluentIcons.Common.Symbol.SlideLink);
					xamlUserType34.AddEnumValue("SlideMicrophone", FluentIcons.Common.Symbol.SlideMicrophone);
					xamlUserType34.AddEnumValue("SlideMultiple", FluentIcons.Common.Symbol.SlideMultiple);
					xamlUserType34.AddEnumValue("SlideMultipleArrowRight", FluentIcons.Common.Symbol.SlideMultipleArrowRight);
					xamlUserType34.AddEnumValue("SlideMultipleSearch", FluentIcons.Common.Symbol.SlideMultipleSearch);
					xamlUserType34.AddEnumValue("SlidePlay", FluentIcons.Common.Symbol.SlidePlay);
					xamlUserType34.AddEnumValue("SlideRecord", FluentIcons.Common.Symbol.SlideRecord);
					xamlUserType34.AddEnumValue("SlideSearch", FluentIcons.Common.Symbol.SlideSearch);
					xamlUserType34.AddEnumValue("SlideSettings", FluentIcons.Common.Symbol.SlideSettings);
					xamlUserType34.AddEnumValue("SlideSize", FluentIcons.Common.Symbol.SlideSize);
					xamlUserType34.AddEnumValue("SlideText", FluentIcons.Common.Symbol.SlideText);
					xamlUserType34.AddEnumValue("SlideTextCall", FluentIcons.Common.Symbol.SlideTextCall);
					xamlUserType34.AddEnumValue("SlideTextCursor", FluentIcons.Common.Symbol.SlideTextCursor);
					xamlUserType34.AddEnumValue("SlideTextEdit", FluentIcons.Common.Symbol.SlideTextEdit);
					xamlUserType34.AddEnumValue("SlideTextMultiple", FluentIcons.Common.Symbol.SlideTextMultiple);
					xamlUserType34.AddEnumValue("SlideTextPerson", FluentIcons.Common.Symbol.SlideTextPerson);
					xamlUserType34.AddEnumValue("SlideTextSparkle", FluentIcons.Common.Symbol.SlideTextSparkle);
					xamlUserType34.AddEnumValue("SlideTransition", FluentIcons.Common.Symbol.SlideTransition);
					xamlUserType34.AddEnumValue("Smartwatch", FluentIcons.Common.Symbol.Smartwatch);
					xamlUserType34.AddEnumValue("SmartwatchDot", FluentIcons.Common.Symbol.SmartwatchDot);
					xamlUserType34.AddEnumValue("Snooze", FluentIcons.Common.Symbol.Snooze);
					xamlUserType34.AddEnumValue("SoundSource", FluentIcons.Common.Symbol.SoundSource);
					xamlUserType34.AddEnumValue("SoundWaveCircle", FluentIcons.Common.Symbol.SoundWaveCircle);
					xamlUserType34.AddEnumValue("SoundWaveCircleSparkle", FluentIcons.Common.Symbol.SoundWaveCircleSparkle);
					xamlUserType34.AddEnumValue("Space3d", FluentIcons.Common.Symbol.Space3d);
					xamlUserType34.AddEnumValue("Spacebar", FluentIcons.Common.Symbol.Spacebar);
					xamlUserType34.AddEnumValue("Sparkle", FluentIcons.Common.Symbol.Sparkle);
					xamlUserType34.AddEnumValue("SparkleCircle", FluentIcons.Common.Symbol.SparkleCircle);
					xamlUserType34.AddEnumValue("SpatulaSpoon", FluentIcons.Common.Symbol.SpatulaSpoon);
					xamlUserType34.AddEnumValue("Speaker0", FluentIcons.Common.Symbol.Speaker0);
					xamlUserType34.AddEnumValue("Speaker1", FluentIcons.Common.Symbol.Speaker1);
					xamlUserType34.AddEnumValue("Speaker2", FluentIcons.Common.Symbol.Speaker2);
					xamlUserType34.AddEnumValue("SpeakerBluetooth", FluentIcons.Common.Symbol.SpeakerBluetooth);
					xamlUserType34.AddEnumValue("SpeakerBox", FluentIcons.Common.Symbol.SpeakerBox);
					xamlUserType34.AddEnumValue("SpeakerEdit", FluentIcons.Common.Symbol.SpeakerEdit);
					xamlUserType34.AddEnumValue("SpeakerMute", FluentIcons.Common.Symbol.SpeakerMute);
					xamlUserType34.AddEnumValue("SpeakerOff", FluentIcons.Common.Symbol.SpeakerOff);
					xamlUserType34.AddEnumValue("SpeakerSettings", FluentIcons.Common.Symbol.SpeakerSettings);
					xamlUserType34.AddEnumValue("SpeakerUsb", FluentIcons.Common.Symbol.SpeakerUsb);
					xamlUserType34.AddEnumValue("SpinnerIos", FluentIcons.Common.Symbol.SpinnerIos);
					xamlUserType34.AddEnumValue("SplitHint", FluentIcons.Common.Symbol.SplitHint);
					xamlUserType34.AddEnumValue("SplitHorizontal", FluentIcons.Common.Symbol.SplitHorizontal);
					xamlUserType34.AddEnumValue("SplitVertical", FluentIcons.Common.Symbol.SplitVertical);
					xamlUserType34.AddEnumValue("Sport", FluentIcons.Common.Symbol.Sport);
					xamlUserType34.AddEnumValue("SportAmericanFootball", FluentIcons.Common.Symbol.SportAmericanFootball);
					xamlUserType34.AddEnumValue("SportBaseball", FluentIcons.Common.Symbol.SportBaseball);
					xamlUserType34.AddEnumValue("SportBasketball", FluentIcons.Common.Symbol.SportBasketball);
					xamlUserType34.AddEnumValue("SportHockey", FluentIcons.Common.Symbol.SportHockey);
					xamlUserType34.AddEnumValue("SportSoccer", FluentIcons.Common.Symbol.SportSoccer);
					xamlUserType34.AddEnumValue("Square", FluentIcons.Common.Symbol.Square);
					xamlUserType34.AddEnumValue("SquareAdd", FluentIcons.Common.Symbol.SquareAdd);
					xamlUserType34.AddEnumValue("SquareArrowForward", FluentIcons.Common.Symbol.SquareArrowForward);
					xamlUserType34.AddEnumValue("SquareDismiss", FluentIcons.Common.Symbol.SquareDismiss);
					xamlUserType34.AddEnumValue("SquareDovetailJoint", FluentIcons.Common.Symbol.SquareDovetailJoint);
					xamlUserType34.AddEnumValue("SquareEraser", FluentIcons.Common.Symbol.SquareEraser);
					xamlUserType34.AddEnumValue("SquareHint", FluentIcons.Common.Symbol.SquareHint);
					xamlUserType34.AddEnumValue("SquareHintApps", FluentIcons.Common.Symbol.SquareHintApps);
					xamlUserType34.AddEnumValue("SquareHintArrowBack", FluentIcons.Common.Symbol.SquareHintArrowBack);
					xamlUserType34.AddEnumValue("SquareHintHexagon", FluentIcons.Common.Symbol.SquareHintHexagon);
					xamlUserType34.AddEnumValue("SquareHintSparkles", FluentIcons.Common.Symbol.SquareHintSparkles);
					xamlUserType34.AddEnumValue("SquareMultiple", FluentIcons.Common.Symbol.SquareMultiple);
					xamlUserType34.AddEnumValue("SquareShadow", FluentIcons.Common.Symbol.SquareShadow);
					xamlUserType34.AddEnumValue("SquareTextArrowRepeatAll", FluentIcons.Common.Symbol.SquareTextArrowRepeatAll);
					xamlUserType34.AddEnumValue("SquaresNested", FluentIcons.Common.Symbol.SquaresNested);
					xamlUserType34.AddEnumValue("Stack", FluentIcons.Common.Symbol.Stack);
					xamlUserType34.AddEnumValue("StackAdd", FluentIcons.Common.Symbol.StackAdd);
					xamlUserType34.AddEnumValue("StackArrowForward", FluentIcons.Common.Symbol.StackArrowForward);
					xamlUserType34.AddEnumValue("StackOff", FluentIcons.Common.Symbol.StackOff);
					xamlUserType34.AddEnumValue("StackStar", FluentIcons.Common.Symbol.StackStar);
					xamlUserType34.AddEnumValue("StackVertical", FluentIcons.Common.Symbol.StackVertical);
					xamlUserType34.AddEnumValue("Stamp", FluentIcons.Common.Symbol.Stamp);
					xamlUserType34.AddEnumValue("Star", FluentIcons.Common.Symbol.Star);
					xamlUserType34.AddEnumValue("StarAdd", FluentIcons.Common.Symbol.StarAdd);
					xamlUserType34.AddEnumValue("StarArrowBack", FluentIcons.Common.Symbol.StarArrowBack);
					xamlUserType34.AddEnumValue("StarArrowRight", FluentIcons.Common.Symbol.StarArrowRight);
					xamlUserType34.AddEnumValue("StarArrowRightEnd", FluentIcons.Common.Symbol.StarArrowRightEnd);
					xamlUserType34.AddEnumValue("StarArrowRightStart", FluentIcons.Common.Symbol.StarArrowRightStart);
					xamlUserType34.AddEnumValue("StarCheckmark", FluentIcons.Common.Symbol.StarCheckmark);
					xamlUserType34.AddEnumValue("StarDismiss", FluentIcons.Common.Symbol.StarDismiss);
					xamlUserType34.AddEnumValue("StarEdit", FluentIcons.Common.Symbol.StarEdit);
					xamlUserType34.AddEnumValue("StarEmphasis", FluentIcons.Common.Symbol.StarEmphasis);
					xamlUserType34.AddEnumValue("StarHalf", FluentIcons.Common.Symbol.StarHalf);
					xamlUserType34.AddEnumValue("StarLineHorizontal3", FluentIcons.Common.Symbol.StarLineHorizontal3);
					xamlUserType34.AddEnumValue("StarOff", FluentIcons.Common.Symbol.StarOff);
					xamlUserType34.AddEnumValue("StarOneQuarter", FluentIcons.Common.Symbol.StarOneQuarter);
					xamlUserType34.AddEnumValue("StarProhibited", FluentIcons.Common.Symbol.StarProhibited);
					xamlUserType34.AddEnumValue("StarSettings", FluentIcons.Common.Symbol.StarSettings);
					xamlUserType34.AddEnumValue("StarThreeQuarter", FluentIcons.Common.Symbol.StarThreeQuarter);
					xamlUserType34.AddEnumValue("Status", FluentIcons.Common.Symbol.Status);
					xamlUserType34.AddEnumValue("Step", FluentIcons.Common.Symbol.Step);
					xamlUserType34.AddEnumValue("Steps", FluentIcons.Common.Symbol.Steps);
					xamlUserType34.AddEnumValue("Stethoscope", FluentIcons.Common.Symbol.Stethoscope);
					xamlUserType34.AddEnumValue("Sticker", FluentIcons.Common.Symbol.Sticker);
					xamlUserType34.AddEnumValue("StickerAdd", FluentIcons.Common.Symbol.StickerAdd);
					xamlUserType34.AddEnumValue("Stop", FluentIcons.Common.Symbol.Stop);
					xamlUserType34.AddEnumValue("Storage", FluentIcons.Common.Symbol.Storage);
					xamlUserType34.AddEnumValue("StoreMicrosoft", FluentIcons.Common.Symbol.StoreMicrosoft);
					xamlUserType34.AddEnumValue("Stream", FluentIcons.Common.Symbol.Stream);
					xamlUserType34.AddEnumValue("StreamInput", FluentIcons.Common.Symbol.StreamInput);
					xamlUserType34.AddEnumValue("StreamInputOutput", FluentIcons.Common.Symbol.StreamInputOutput);
					xamlUserType34.AddEnumValue("StreamOutput", FluentIcons.Common.Symbol.StreamOutput);
					xamlUserType34.AddEnumValue("StreetSign", FluentIcons.Common.Symbol.StreetSign);
					xamlUserType34.AddEnumValue("StyleGuide", FluentIcons.Common.Symbol.StyleGuide);
					xamlUserType34.AddEnumValue("SubGrid", FluentIcons.Common.Symbol.SubGrid);
					xamlUserType34.AddEnumValue("Subtitles", FluentIcons.Common.Symbol.Subtitles);
					xamlUserType34.AddEnumValue("Subtract", FluentIcons.Common.Symbol.Subtract);
					xamlUserType34.AddEnumValue("SubtractCircle", FluentIcons.Common.Symbol.SubtractCircle);
					xamlUserType34.AddEnumValue("SubtractCircleArrowBack", FluentIcons.Common.Symbol.SubtractCircleArrowBack);
					xamlUserType34.AddEnumValue("SubtractCircleArrowForward", FluentIcons.Common.Symbol.SubtractCircleArrowForward);
					xamlUserType34.AddEnumValue("SubtractParentheses", FluentIcons.Common.Symbol.SubtractParentheses);
					xamlUserType34.AddEnumValue("SubtractSquare", FluentIcons.Common.Symbol.SubtractSquare);
					xamlUserType34.AddEnumValue("SubtractSquareMultiple", FluentIcons.Common.Symbol.SubtractSquareMultiple);
					xamlUserType34.AddEnumValue("SurfaceEarbuds", FluentIcons.Common.Symbol.SurfaceEarbuds);
					xamlUserType34.AddEnumValue("SurfaceHub", FluentIcons.Common.Symbol.SurfaceHub);
					xamlUserType34.AddEnumValue("SwimmingPool", FluentIcons.Common.Symbol.SwimmingPool);
					xamlUserType34.AddEnumValue("SwipeDown", FluentIcons.Common.Symbol.SwipeDown);
					xamlUserType34.AddEnumValue("SwipeRight", FluentIcons.Common.Symbol.SwipeRight);
					xamlUserType34.AddEnumValue("SwipeUp", FluentIcons.Common.Symbol.SwipeUp);
					xamlUserType34.AddEnumValue("Symbols", FluentIcons.Common.Symbol.Symbols);
					xamlUserType34.AddEnumValue("SyncOff", FluentIcons.Common.Symbol.SyncOff);
					xamlUserType34.AddEnumValue("Syringe", FluentIcons.Common.Symbol.Syringe);
					xamlUserType34.AddEnumValue("System", FluentIcons.Common.Symbol.System);
					xamlUserType34.AddEnumValue("Tab", FluentIcons.Common.Symbol.Tab);
					xamlUserType34.AddEnumValue("TabAdd", FluentIcons.Common.Symbol.TabAdd);
					xamlUserType34.AddEnumValue("TabArrowLeft", FluentIcons.Common.Symbol.TabArrowLeft);
					xamlUserType34.AddEnumValue("TabDesktop", FluentIcons.Common.Symbol.TabDesktop);
					xamlUserType34.AddEnumValue("TabDesktopArrowClockwise", FluentIcons.Common.Symbol.TabDesktopArrowClockwise);
					xamlUserType34.AddEnumValue("TabDesktopArrowLeft", FluentIcons.Common.Symbol.TabDesktopArrowLeft);
					xamlUserType34.AddEnumValue("TabDesktopBottom", FluentIcons.Common.Symbol.TabDesktopBottom);
					xamlUserType34.AddEnumValue("TabDesktopClock", FluentIcons.Common.Symbol.TabDesktopClock);
					xamlUserType34.AddEnumValue("TabDesktopCopy", FluentIcons.Common.Symbol.TabDesktopCopy);
					xamlUserType34.AddEnumValue("TabDesktopImage", FluentIcons.Common.Symbol.TabDesktopImage);
					xamlUserType34.AddEnumValue("TabDesktopLink", FluentIcons.Common.Symbol.TabDesktopLink);
					xamlUserType34.AddEnumValue("TabDesktopMultiple", FluentIcons.Common.Symbol.TabDesktopMultiple);
					xamlUserType34.AddEnumValue("TabDesktopMultipleAdd", FluentIcons.Common.Symbol.TabDesktopMultipleAdd);
					xamlUserType34.AddEnumValue("TabDesktopMultipleBottom", FluentIcons.Common.Symbol.TabDesktopMultipleBottom);
					xamlUserType34.AddEnumValue("TabDesktopMultipleSparkle", FluentIcons.Common.Symbol.TabDesktopMultipleSparkle);
					xamlUserType34.AddEnumValue("TabDesktopNewPage", FluentIcons.Common.Symbol.TabDesktopNewPage);
					xamlUserType34.AddEnumValue("TabDesktopSearch", FluentIcons.Common.Symbol.TabDesktopSearch);
					xamlUserType34.AddEnumValue("TabGroup", FluentIcons.Common.Symbol.TabGroup);
					xamlUserType34.AddEnumValue("TabInPrivate", FluentIcons.Common.Symbol.TabInPrivate);
					xamlUserType34.AddEnumValue("TabInprivateAccount", FluentIcons.Common.Symbol.TabInprivateAccount);
					xamlUserType34.AddEnumValue("TabProhibited", FluentIcons.Common.Symbol.TabProhibited);
					xamlUserType34.AddEnumValue("TabShieldDismiss", FluentIcons.Common.Symbol.TabShieldDismiss);
					xamlUserType34.AddEnumValue("Table", FluentIcons.Common.Symbol.Table);
					xamlUserType34.AddEnumValue("TableAdd", FluentIcons.Common.Symbol.TableAdd);
					xamlUserType34.AddEnumValue("TableAltText", FluentIcons.Common.Symbol.TableAltText);
					xamlUserType34.AddEnumValue("TableArrowUp", FluentIcons.Common.Symbol.TableArrowUp);
					xamlUserType34.AddEnumValue("TableBottomRow", FluentIcons.Common.Symbol.TableBottomRow);
					xamlUserType34.AddEnumValue("TableCalculator", FluentIcons.Common.Symbol.TableCalculator);
					xamlUserType34.AddEnumValue("TableCellAdd", FluentIcons.Common.Symbol.TableCellAdd);
					xamlUserType34.AddEnumValue("TableCellEdit", FluentIcons.Common.Symbol.TableCellEdit);
					xamlUserType34.AddEnumValue("TableCellsMerge", FluentIcons.Common.Symbol.TableCellsMerge);
					xamlUserType34.AddEnumValue("TableCellsSplit", FluentIcons.Common.Symbol.TableCellsSplit);
					xamlUserType34.AddEnumValue("TableChecker", FluentIcons.Common.Symbol.TableChecker);
					xamlUserType34.AddEnumValue("TableColumnTopBottom", FluentIcons.Common.Symbol.TableColumnTopBottom);
					xamlUserType34.AddEnumValue("TableCopy", FluentIcons.Common.Symbol.TableCopy);
					xamlUserType34.AddEnumValue("TableCursor", FluentIcons.Common.Symbol.TableCursor);
					xamlUserType34.AddEnumValue("TableDeleteColumn", FluentIcons.Common.Symbol.TableDeleteColumn);
					xamlUserType34.AddEnumValue("TableDeleteRow", FluentIcons.Common.Symbol.TableDeleteRow);
					xamlUserType34.AddEnumValue("TableDismiss", FluentIcons.Common.Symbol.TableDismiss);
					xamlUserType34.AddEnumValue("TableEdit", FluentIcons.Common.Symbol.TableEdit);
					xamlUserType34.AddEnumValue("TableFreezeColumn", FluentIcons.Common.Symbol.TableFreezeColumn);
					xamlUserType34.AddEnumValue("TableFreezeColumnAndRow", FluentIcons.Common.Symbol.TableFreezeColumnAndRow);
					xamlUserType34.AddEnumValue("TableFreezeRow", FluentIcons.Common.Symbol.TableFreezeRow);
					xamlUserType34.AddEnumValue("TableImage", FluentIcons.Common.Symbol.TableImage);
					xamlUserType34.AddEnumValue("TableInsertColumn", FluentIcons.Common.Symbol.TableInsertColumn);
					xamlUserType34.AddEnumValue("TableInsertRow", FluentIcons.Common.Symbol.TableInsertRow);
					xamlUserType34.AddEnumValue("TableLightning", FluentIcons.Common.Symbol.TableLightning);
					xamlUserType34.AddEnumValue("TableLink", FluentIcons.Common.Symbol.TableLink);
					xamlUserType34.AddEnumValue("TableLock", FluentIcons.Common.Symbol.TableLock);
					xamlUserType34.AddEnumValue("TableMoveAbove", FluentIcons.Common.Symbol.TableMoveAbove);
					xamlUserType34.AddEnumValue("TableMoveBelow", FluentIcons.Common.Symbol.TableMoveBelow);
					xamlUserType34.AddEnumValue("TableMoveLeft", FluentIcons.Common.Symbol.TableMoveLeft);
					xamlUserType34.AddEnumValue("TableMoveRight", FluentIcons.Common.Symbol.TableMoveRight);
					xamlUserType34.AddEnumValue("TableMultiple", FluentIcons.Common.Symbol.TableMultiple);
					xamlUserType34.AddEnumValue("TableOffset", FluentIcons.Common.Symbol.TableOffset);
					xamlUserType34.AddEnumValue("TableOffsetAdd", FluentIcons.Common.Symbol.TableOffsetAdd);
					xamlUserType34.AddEnumValue("TableOffsetLessThanOrEqualTo", FluentIcons.Common.Symbol.TableOffsetLessThanOrEqualTo);
					xamlUserType34.AddEnumValue("TableOffsetSettings", FluentIcons.Common.Symbol.TableOffsetSettings);
					xamlUserType34.AddEnumValue("TableResizeColumn", FluentIcons.Common.Symbol.TableResizeColumn);
					xamlUserType34.AddEnumValue("TableResizeRow", FluentIcons.Common.Symbol.TableResizeRow);
					xamlUserType34.AddEnumValue("TableSearch", FluentIcons.Common.Symbol.TableSearch);
					xamlUserType34.AddEnumValue("TableSettings", FluentIcons.Common.Symbol.TableSettings);
					xamlUserType34.AddEnumValue("TableSimple", FluentIcons.Common.Symbol.TableSimple);
					xamlUserType34.AddEnumValue("TableSimpleCheckmark", FluentIcons.Common.Symbol.TableSimpleCheckmark);
					xamlUserType34.AddEnumValue("TableSimpleExclude", FluentIcons.Common.Symbol.TableSimpleExclude);
					xamlUserType34.AddEnumValue("TableSimpleInclude", FluentIcons.Common.Symbol.TableSimpleInclude);
					xamlUserType34.AddEnumValue("TableSimpleMultiple", FluentIcons.Common.Symbol.TableSimpleMultiple);
					xamlUserType34.AddEnumValue("TableSparkle", FluentIcons.Common.Symbol.TableSparkle);
					xamlUserType34.AddEnumValue("TableSplit", FluentIcons.Common.Symbol.TableSplit);
					xamlUserType34.AddEnumValue("TableStackAbove", FluentIcons.Common.Symbol.TableStackAbove);
					xamlUserType34.AddEnumValue("TableStackBelow", FluentIcons.Common.Symbol.TableStackBelow);
					xamlUserType34.AddEnumValue("TableStackLeft", FluentIcons.Common.Symbol.TableStackLeft);
					xamlUserType34.AddEnumValue("TableStackRight", FluentIcons.Common.Symbol.TableStackRight);
					xamlUserType34.AddEnumValue("TableSwitch", FluentIcons.Common.Symbol.TableSwitch);
					xamlUserType34.AddEnumValue("Tablet", FluentIcons.Common.Symbol.Tablet);
					xamlUserType34.AddEnumValue("TabletLaptop", FluentIcons.Common.Symbol.TabletLaptop);
					xamlUserType34.AddEnumValue("TabletSpeaker", FluentIcons.Common.Symbol.TabletSpeaker);
					xamlUserType34.AddEnumValue("Tabs", FluentIcons.Common.Symbol.Tabs);
					xamlUserType34.AddEnumValue("Tag", FluentIcons.Common.Symbol.Tag);
					xamlUserType34.AddEnumValue("TagCircle", FluentIcons.Common.Symbol.TagCircle);
					xamlUserType34.AddEnumValue("TagDismiss", FluentIcons.Common.Symbol.TagDismiss);
					xamlUserType34.AddEnumValue("TagError", FluentIcons.Common.Symbol.TagError);
					xamlUserType34.AddEnumValue("TagLock", FluentIcons.Common.Symbol.TagLock);
					xamlUserType34.AddEnumValue("TagLockAccent", FluentIcons.Common.Symbol.TagLockAccent);
					xamlUserType34.AddEnumValue("TagMultiple", FluentIcons.Common.Symbol.TagMultiple);
					xamlUserType34.AddEnumValue("TagOff", FluentIcons.Common.Symbol.TagOff);
					xamlUserType34.AddEnumValue("TagQuestionMark", FluentIcons.Common.Symbol.TagQuestionMark);
					xamlUserType34.AddEnumValue("TagReset", FluentIcons.Common.Symbol.TagReset);
					xamlUserType34.AddEnumValue("TagSearch", FluentIcons.Common.Symbol.TagSearch);
					xamlUserType34.AddEnumValue("TapDouble", FluentIcons.Common.Symbol.TapDouble);
					xamlUserType34.AddEnumValue("TapSingle", FluentIcons.Common.Symbol.TapSingle);
					xamlUserType34.AddEnumValue("Target", FluentIcons.Common.Symbol.Target);
					xamlUserType34.AddEnumValue("TargetAdd", FluentIcons.Common.Symbol.TargetAdd);
					xamlUserType34.AddEnumValue("TargetArrow", FluentIcons.Common.Symbol.TargetArrow);
					xamlUserType34.AddEnumValue("TargetDismiss", FluentIcons.Common.Symbol.TargetDismiss);
					xamlUserType34.AddEnumValue("TargetEdit", FluentIcons.Common.Symbol.TargetEdit);
					xamlUserType34.AddEnumValue("TaskList", FluentIcons.Common.Symbol.TaskList);
					xamlUserType34.AddEnumValue("TaskListAdd", FluentIcons.Common.Symbol.TaskListAdd);
					xamlUserType34.AddEnumValue("TaskListSquare", FluentIcons.Common.Symbol.TaskListSquare);
					xamlUserType34.AddEnumValue("TaskListSquareAdd", FluentIcons.Common.Symbol.TaskListSquareAdd);
					xamlUserType34.AddEnumValue("TaskListSquareDatabase", FluentIcons.Common.Symbol.TaskListSquareDatabase);
					xamlUserType34.AddEnumValue("TaskListSquarePerson", FluentIcons.Common.Symbol.TaskListSquarePerson);
					xamlUserType34.AddEnumValue("TaskListSquareSettings", FluentIcons.Common.Symbol.TaskListSquareSettings);
					xamlUserType34.AddEnumValue("TasksApp", FluentIcons.Common.Symbol.TasksApp);
					xamlUserType34.AddEnumValue("Teaching", FluentIcons.Common.Symbol.Teaching);
					xamlUserType34.AddEnumValue("TeardropBottomRight", FluentIcons.Common.Symbol.TeardropBottomRight);
					xamlUserType34.AddEnumValue("Teddy", FluentIcons.Common.Symbol.Teddy);
					xamlUserType34.AddEnumValue("Temperature", FluentIcons.Common.Symbol.Temperature);
					xamlUserType34.AddEnumValue("TemperatureDegreeCelsius", FluentIcons.Common.Symbol.TemperatureDegreeCelsius);
					xamlUserType34.AddEnumValue("TemperatureDegreeFahrenheit", FluentIcons.Common.Symbol.TemperatureDegreeFahrenheit);
					xamlUserType34.AddEnumValue("Tent", FluentIcons.Common.Symbol.Tent);
					xamlUserType34.AddEnumValue("TetrisApp", FluentIcons.Common.Symbol.TetrisApp);
					xamlUserType34.AddEnumValue("Text", FluentIcons.Common.Symbol.Text);
					xamlUserType34.AddEnumValue("TextAdd", FluentIcons.Common.Symbol.TextAdd);
					xamlUserType34.AddEnumValue("TextAddSpaceAfter", FluentIcons.Common.Symbol.TextAddSpaceAfter);
					xamlUserType34.AddEnumValue("TextAddSpaceBefore", FluentIcons.Common.Symbol.TextAddSpaceBefore);
					xamlUserType34.AddEnumValue("TextAddT", FluentIcons.Common.Symbol.TextAddT);
					xamlUserType34.AddEnumValue("TextAlignCenter", FluentIcons.Common.Symbol.TextAlignCenter);
					xamlUserType34.AddEnumValue("TextAlignCenterRotate270", FluentIcons.Common.Symbol.TextAlignCenterRotate270);
					xamlUserType34.AddEnumValue("TextAlignCenterRotate90", FluentIcons.Common.Symbol.TextAlignCenterRotate90);
					xamlUserType34.AddEnumValue("TextAlignDistributed", FluentIcons.Common.Symbol.TextAlignDistributed);
					xamlUserType34.AddEnumValue("TextAlignDistributedEvenly", FluentIcons.Common.Symbol.TextAlignDistributedEvenly);
					xamlUserType34.AddEnumValue("TextAlignDistributedVertical", FluentIcons.Common.Symbol.TextAlignDistributedVertical);
					xamlUserType34.AddEnumValue("TextAlignJustify", FluentIcons.Common.Symbol.TextAlignJustify);
					xamlUserType34.AddEnumValue("TextAlignJustifyLow", FluentIcons.Common.Symbol.TextAlignJustifyLow);
					xamlUserType34.AddEnumValue("TextAlignJustifyLowRotate270", FluentIcons.Common.Symbol.TextAlignJustifyLowRotate270);
					xamlUserType34.AddEnumValue("TextAlignJustifyLowRotate90", FluentIcons.Common.Symbol.TextAlignJustifyLowRotate90);
					xamlUserType34.AddEnumValue("TextAlignJustifyRotate270", FluentIcons.Common.Symbol.TextAlignJustifyRotate270);
					xamlUserType34.AddEnumValue("TextAlignJustifyRotate90", FluentIcons.Common.Symbol.TextAlignJustifyRotate90);
					xamlUserType34.AddEnumValue("TextAlignLeft", FluentIcons.Common.Symbol.TextAlignLeft);
					xamlUserType34.AddEnumValue("TextAlignLeftRotate270", FluentIcons.Common.Symbol.TextAlignLeftRotate270);
					xamlUserType34.AddEnumValue("TextAlignLeftRotate90", FluentIcons.Common.Symbol.TextAlignLeftRotate90);
					xamlUserType34.AddEnumValue("TextAlignRight", FluentIcons.Common.Symbol.TextAlignRight);
					xamlUserType34.AddEnumValue("TextAlignRightRotate270", FluentIcons.Common.Symbol.TextAlignRightRotate270);
					xamlUserType34.AddEnumValue("TextAlignRightRotate90", FluentIcons.Common.Symbol.TextAlignRightRotate90);
					xamlUserType34.AddEnumValue("TextArrowDownRightColumn", FluentIcons.Common.Symbol.TextArrowDownRightColumn);
					xamlUserType34.AddEnumValue("TextAsterisk", FluentIcons.Common.Symbol.TextAsterisk);
					xamlUserType34.AddEnumValue("TextBaseline", FluentIcons.Common.Symbol.TextBaseline);
					xamlUserType34.AddEnumValue("TextBold", FluentIcons.Common.Symbol.TextBold);
					xamlUserType34.AddEnumValue("TextBoxSettings", FluentIcons.Common.Symbol.TextBoxSettings);
					xamlUserType34.AddEnumValue("TextBulletList", FluentIcons.Common.Symbol.TextBulletList);
					xamlUserType34.AddEnumValue("TextBulletListAdd", FluentIcons.Common.Symbol.TextBulletListAdd);
					xamlUserType34.AddEnumValue("TextBulletListCheckmark", FluentIcons.Common.Symbol.TextBulletListCheckmark);
					xamlUserType34.AddEnumValue("TextBulletListDismiss", FluentIcons.Common.Symbol.TextBulletListDismiss);
					xamlUserType34.AddEnumValue("TextBulletListRotate90", FluentIcons.Common.Symbol.TextBulletListRotate90);
					xamlUserType34.AddEnumValue("TextBulletListSquare", FluentIcons.Common.Symbol.TextBulletListSquare);
					xamlUserType34.AddEnumValue("TextBulletListSquareClock", FluentIcons.Common.Symbol.TextBulletListSquareClock);
					xamlUserType34.AddEnumValue("TextBulletListSquareEdit", FluentIcons.Common.Symbol.TextBulletListSquareEdit);
					xamlUserType34.AddEnumValue("TextBulletListSquarePerson", FluentIcons.Common.Symbol.TextBulletListSquarePerson);
					xamlUserType34.AddEnumValue("TextBulletListSquareSearch", FluentIcons.Common.Symbol.TextBulletListSquareSearch);
					xamlUserType34.AddEnumValue("TextBulletListSquareSettings", FluentIcons.Common.Symbol.TextBulletListSquareSettings);
					xamlUserType34.AddEnumValue("TextBulletListSquareShield", FluentIcons.Common.Symbol.TextBulletListSquareShield);
					xamlUserType34.AddEnumValue("TextBulletListSquareSparkle", FluentIcons.Common.Symbol.TextBulletListSquareSparkle);
					xamlUserType34.AddEnumValue("TextBulletListSquareToolbox", FluentIcons.Common.Symbol.TextBulletListSquareToolbox);
					xamlUserType34.AddEnumValue("TextBulletListSquareWarning", FluentIcons.Common.Symbol.TextBulletListSquareWarning);
					xamlUserType34.AddEnumValue("TextBulletListTree", FluentIcons.Common.Symbol.TextBulletListTree);
					xamlUserType34.AddEnumValue("TextCaseLowercase", FluentIcons.Common.Symbol.TextCaseLowercase);
					xamlUserType34.AddEnumValue("TextCaseTitle", FluentIcons.Common.Symbol.TextCaseTitle);
					xamlUserType34.AddEnumValue("TextCaseUppercase", FluentIcons.Common.Symbol.TextCaseUppercase);
					xamlUserType34.AddEnumValue("TextChangeCase", FluentIcons.Common.Symbol.TextChangeCase);
					xamlUserType34.AddEnumValue("TextClearFormatting", FluentIcons.Common.Symbol.TextClearFormatting);
					xamlUserType34.AddEnumValue("TextCollapse", FluentIcons.Common.Symbol.TextCollapse);
					xamlUserType34.AddEnumValue("TextColor", FluentIcons.Common.Symbol.TextColor);
					xamlUserType34.AddEnumValue("TextColorAccent", FluentIcons.Common.Symbol.TextColorAccent);
					xamlUserType34.AddEnumValue("TextColumnOne", FluentIcons.Common.Symbol.TextColumnOne);
					xamlUserType34.AddEnumValue("TextColumnOneNarrow", FluentIcons.Common.Symbol.TextColumnOneNarrow);
					xamlUserType34.AddEnumValue("TextColumnOneSemiNarrow", FluentIcons.Common.Symbol.TextColumnOneSemiNarrow);
					xamlUserType34.AddEnumValue("TextColumnOneWide", FluentIcons.Common.Symbol.TextColumnOneWide);
					xamlUserType34.AddEnumValue("TextColumnOneWideLightning", FluentIcons.Common.Symbol.TextColumnOneWideLightning);
					xamlUserType34.AddEnumValue("TextColumnThree", FluentIcons.Common.Symbol.TextColumnThree);
					xamlUserType34.AddEnumValue("TextColumnTwo", FluentIcons.Common.Symbol.TextColumnTwo);
					xamlUserType34.AddEnumValue("TextColumnTwoLeft", FluentIcons.Common.Symbol.TextColumnTwoLeft);
					xamlUserType34.AddEnumValue("TextColumnTwoRight", FluentIcons.Common.Symbol.TextColumnTwoRight);
					xamlUserType34.AddEnumValue("TextColumnWide", FluentIcons.Common.Symbol.TextColumnWide);
					xamlUserType34.AddEnumValue("TextContinuous", FluentIcons.Common.Symbol.TextContinuous);
					xamlUserType34.AddEnumValue("TextDensity", FluentIcons.Common.Symbol.TextDensity);
					xamlUserType34.AddEnumValue("TextDescription", FluentIcons.Common.Symbol.TextDescription);
					xamlUserType34.AddEnumValue("TextDirectionHorizontal", FluentIcons.Common.Symbol.TextDirectionHorizontal);
					xamlUserType34.AddEnumValue("TextDirectionHorizontalLeft", FluentIcons.Common.Symbol.TextDirectionHorizontalLeft);
					xamlUserType34.AddEnumValue("TextDirectionHorizontalRight", FluentIcons.Common.Symbol.TextDirectionHorizontalRight);
					xamlUserType34.AddEnumValue("TextDirectionRotate270Right", FluentIcons.Common.Symbol.TextDirectionRotate270Right);
					xamlUserType34.AddEnumValue("TextDirectionRotate315Right", FluentIcons.Common.Symbol.TextDirectionRotate315Right);
					xamlUserType34.AddEnumValue("TextDirectionRotate45Right", FluentIcons.Common.Symbol.TextDirectionRotate45Right);
					xamlUserType34.AddEnumValue("TextDirectionRotate90", FluentIcons.Common.Symbol.TextDirectionRotate90);
					xamlUserType34.AddEnumValue("TextDirectionRotate90Left", FluentIcons.Common.Symbol.TextDirectionRotate90Left);
					xamlUserType34.AddEnumValue("TextDirectionRotate90Right", FluentIcons.Common.Symbol.TextDirectionRotate90Right);
					xamlUserType34.AddEnumValue("TextDirectionVertical", FluentIcons.Common.Symbol.TextDirectionVertical);
					xamlUserType34.AddEnumValue("TextEditStyle", FluentIcons.Common.Symbol.TextEditStyle);
					xamlUserType34.AddEnumValue("TextEffects", FluentIcons.Common.Symbol.TextEffects);
					xamlUserType34.AddEnumValue("TextEffectsSparkle", FluentIcons.Common.Symbol.TextEffectsSparkle);
					xamlUserType34.AddEnumValue("TextExpand", FluentIcons.Common.Symbol.TextExpand);
					xamlUserType34.AddEnumValue("TextField", FluentIcons.Common.Symbol.TextField);
					xamlUserType34.AddEnumValue("TextFirstLine", FluentIcons.Common.Symbol.TextFirstLine);
					xamlUserType34.AddEnumValue("TextFont", FluentIcons.Common.Symbol.TextFont);
					xamlUserType34.AddEnumValue("TextFontInfo", FluentIcons.Common.Symbol.TextFontInfo);
					xamlUserType34.AddEnumValue("TextFontSize", FluentIcons.Common.Symbol.TextFontSize);
					xamlUserType34.AddEnumValue("TextFootnote", FluentIcons.Common.Symbol.TextFootnote);
					xamlUserType34.AddEnumValue("TextGrammarArrowLeft", FluentIcons.Common.Symbol.TextGrammarArrowLeft);
					xamlUserType34.AddEnumValue("TextGrammarArrowRight", FluentIcons.Common.Symbol.TextGrammarArrowRight);
					xamlUserType34.AddEnumValue("TextGrammarCheckmark", FluentIcons.Common.Symbol.TextGrammarCheckmark);
					xamlUserType34.AddEnumValue("TextGrammarDismiss", FluentIcons.Common.Symbol.TextGrammarDismiss);
					xamlUserType34.AddEnumValue("TextGrammarError", FluentIcons.Common.Symbol.TextGrammarError);
					xamlUserType34.AddEnumValue("TextGrammarLightning", FluentIcons.Common.Symbol.TextGrammarLightning);
					xamlUserType34.AddEnumValue("TextGrammarSettings", FluentIcons.Common.Symbol.TextGrammarSettings);
					xamlUserType34.AddEnumValue("TextGrammarWand", FluentIcons.Common.Symbol.TextGrammarWand);
					xamlUserType34.AddEnumValue("TextHanging", FluentIcons.Common.Symbol.TextHanging);
					xamlUserType34.AddEnumValue("TextHeader1", FluentIcons.Common.Symbol.TextHeader1);
					xamlUserType34.AddEnumValue("TextHeader1Lines", FluentIcons.Common.Symbol.TextHeader1Lines);
					xamlUserType34.AddEnumValue("TextHeader1LinesCaret", FluentIcons.Common.Symbol.TextHeader1LinesCaret);
					xamlUserType34.AddEnumValue("TextHeader2", FluentIcons.Common.Symbol.TextHeader2);
					xamlUserType34.AddEnumValue("TextHeader2Lines", FluentIcons.Common.Symbol.TextHeader2Lines);
					xamlUserType34.AddEnumValue("TextHeader2LinesCaret", FluentIcons.Common.Symbol.TextHeader2LinesCaret);
					xamlUserType34.AddEnumValue("TextHeader3", FluentIcons.Common.Symbol.TextHeader3);
					xamlUserType34.AddEnumValue("TextHeader3Lines", FluentIcons.Common.Symbol.TextHeader3Lines);
					xamlUserType34.AddEnumValue("TextHeader3LinesCaret", FluentIcons.Common.Symbol.TextHeader3LinesCaret);
					xamlUserType34.AddEnumValue("TextIndentDecrease", FluentIcons.Common.Symbol.TextIndentDecrease);
					xamlUserType34.AddEnumValue("TextIndentDecreaseRotate270", FluentIcons.Common.Symbol.TextIndentDecreaseRotate270);
					xamlUserType34.AddEnumValue("TextIndentDecreaseRotate90", FluentIcons.Common.Symbol.TextIndentDecreaseRotate90);
					xamlUserType34.AddEnumValue("TextIndentIncrease", FluentIcons.Common.Symbol.TextIndentIncrease);
					xamlUserType34.AddEnumValue("TextIndentIncreaseRotate270", FluentIcons.Common.Symbol.TextIndentIncreaseRotate270);
					xamlUserType34.AddEnumValue("TextIndentIncreaseRotate90", FluentIcons.Common.Symbol.TextIndentIncreaseRotate90);
					xamlUserType34.AddEnumValue("TextItalic", FluentIcons.Common.Symbol.TextItalic);
					xamlUserType34.AddEnumValue("TextLineSpacing", FluentIcons.Common.Symbol.TextLineSpacing);
					xamlUserType34.AddEnumValue("TextListAbcLowercase", FluentIcons.Common.Symbol.TextListAbcLowercase);
					xamlUserType34.AddEnumValue("TextListAbcUppercase", FluentIcons.Common.Symbol.TextListAbcUppercase);
					xamlUserType34.AddEnumValue("TextListRomanNumeralLowercase", FluentIcons.Common.Symbol.TextListRomanNumeralLowercase);
					xamlUserType34.AddEnumValue("TextListRomanNumeralUppercase", FluentIcons.Common.Symbol.TextListRomanNumeralUppercase);
					xamlUserType34.AddEnumValue("TextMore", FluentIcons.Common.Symbol.TextMore);
					xamlUserType34.AddEnumValue("TextNumberFormat", FluentIcons.Common.Symbol.TextNumberFormat);
					xamlUserType34.AddEnumValue("TextNumberList", FluentIcons.Common.Symbol.TextNumberList);
					xamlUserType34.AddEnumValue("TextNumberListRotate270", FluentIcons.Common.Symbol.TextNumberListRotate270);
					xamlUserType34.AddEnumValue("TextNumberListRotate90", FluentIcons.Common.Symbol.TextNumberListRotate90);
					xamlUserType34.AddEnumValue("TextParagraph", FluentIcons.Common.Symbol.TextParagraph);
					xamlUserType34.AddEnumValue("TextParagraphDirection", FluentIcons.Common.Symbol.TextParagraphDirection);
					xamlUserType34.AddEnumValue("TextParagraphDirectionLeft", FluentIcons.Common.Symbol.TextParagraphDirectionLeft);
					xamlUserType34.AddEnumValue("TextParagraphDirectionRight", FluentIcons.Common.Symbol.TextParagraphDirectionRight);
					xamlUserType34.AddEnumValue("TextPeriodAsterisk", FluentIcons.Common.Symbol.TextPeriodAsterisk);
					xamlUserType34.AddEnumValue("TextPositionBehind", FluentIcons.Common.Symbol.TextPositionBehind);
					xamlUserType34.AddEnumValue("TextPositionFront", FluentIcons.Common.Symbol.TextPositionFront);
					xamlUserType34.AddEnumValue("TextPositionLine", FluentIcons.Common.Symbol.TextPositionLine);
					xamlUserType34.AddEnumValue("TextPositionSquare", FluentIcons.Common.Symbol.TextPositionSquare);
					xamlUserType34.AddEnumValue("TextPositionSquareLeft", FluentIcons.Common.Symbol.TextPositionSquareLeft);
					xamlUserType34.AddEnumValue("TextPositionSquareRight", FluentIcons.Common.Symbol.TextPositionSquareRight);
					xamlUserType34.AddEnumValue("TextPositionThrough", FluentIcons.Common.Symbol.TextPositionThrough);
					xamlUserType34.AddEnumValue("TextPositionTight", FluentIcons.Common.Symbol.TextPositionTight);
					xamlUserType34.AddEnumValue("TextPositionTopBottom", FluentIcons.Common.Symbol.TextPositionTopBottom);
					xamlUserType34.AddEnumValue("TextProofingTools", FluentIcons.Common.Symbol.TextProofingTools);
					xamlUserType34.AddEnumValue("TextQuote", FluentIcons.Common.Symbol.TextQuote);
					xamlUserType34.AddEnumValue("TextSortAscending", FluentIcons.Common.Symbol.TextSortAscending);
					xamlUserType34.AddEnumValue("TextSortDescending", FluentIcons.Common.Symbol.TextSortDescending);
					xamlUserType34.AddEnumValue("TextStrikethrough", FluentIcons.Common.Symbol.TextStrikethrough);
					xamlUserType34.AddEnumValue("TextSubscript", FluentIcons.Common.Symbol.TextSubscript);
					xamlUserType34.AddEnumValue("TextSuperscript", FluentIcons.Common.Symbol.TextSuperscript);
					xamlUserType34.AddEnumValue("TextT", FluentIcons.Common.Symbol.TextT);
					xamlUserType34.AddEnumValue("TextUnderline", FluentIcons.Common.Symbol.TextUnderline);
					xamlUserType34.AddEnumValue("TextUnderlineCharacterU", FluentIcons.Common.Symbol.TextUnderlineCharacterU);
					xamlUserType34.AddEnumValue("TextUnderlineDouble", FluentIcons.Common.Symbol.TextUnderlineDouble);
					xamlUserType34.AddEnumValue("TextWholeWord", FluentIcons.Common.Symbol.TextWholeWord);
					xamlUserType34.AddEnumValue("TextWordCount", FluentIcons.Common.Symbol.TextWordCount);
					xamlUserType34.AddEnumValue("TextWrap", FluentIcons.Common.Symbol.TextWrap);
					xamlUserType34.AddEnumValue("TextWrapOff", FluentIcons.Common.Symbol.TextWrapOff);
					xamlUserType34.AddEnumValue("Textbox", FluentIcons.Common.Symbol.Textbox);
					xamlUserType34.AddEnumValue("TextboxAlignBottom", FluentIcons.Common.Symbol.TextboxAlignBottom);
					xamlUserType34.AddEnumValue("TextboxAlignBottomCenter", FluentIcons.Common.Symbol.TextboxAlignBottomCenter);
					xamlUserType34.AddEnumValue("TextboxAlignBottomLeft", FluentIcons.Common.Symbol.TextboxAlignBottomLeft);
					xamlUserType34.AddEnumValue("TextboxAlignBottomRight", FluentIcons.Common.Symbol.TextboxAlignBottomRight);
					xamlUserType34.AddEnumValue("TextboxAlignBottomRotate90", FluentIcons.Common.Symbol.TextboxAlignBottomRotate90);
					xamlUserType34.AddEnumValue("TextboxAlignCenter", FluentIcons.Common.Symbol.TextboxAlignCenter);
					xamlUserType34.AddEnumValue("TextboxAlignMiddle", FluentIcons.Common.Symbol.TextboxAlignMiddle);
					xamlUserType34.AddEnumValue("TextboxAlignMiddleLeft", FluentIcons.Common.Symbol.TextboxAlignMiddleLeft);
					xamlUserType34.AddEnumValue("TextboxAlignMiddleRight", FluentIcons.Common.Symbol.TextboxAlignMiddleRight);
					xamlUserType34.AddEnumValue("TextboxAlignMiddleRotate90", FluentIcons.Common.Symbol.TextboxAlignMiddleRotate90);
					xamlUserType34.AddEnumValue("TextboxAlignTop", FluentIcons.Common.Symbol.TextboxAlignTop);
					xamlUserType34.AddEnumValue("TextboxAlignTopCenter", FluentIcons.Common.Symbol.TextboxAlignTopCenter);
					xamlUserType34.AddEnumValue("TextboxAlignTopLeft", FluentIcons.Common.Symbol.TextboxAlignTopLeft);
					xamlUserType34.AddEnumValue("TextboxAlignTopRight", FluentIcons.Common.Symbol.TextboxAlignTopRight);
					xamlUserType34.AddEnumValue("TextboxAlignTopRotate90", FluentIcons.Common.Symbol.TextboxAlignTopRotate90);
					xamlUserType34.AddEnumValue("TextboxCheckmark", FluentIcons.Common.Symbol.TextboxCheckmark);
					xamlUserType34.AddEnumValue("TextboxMore", FluentIcons.Common.Symbol.TextboxMore);
					xamlUserType34.AddEnumValue("TextboxRotate90", FluentIcons.Common.Symbol.TextboxRotate90);
					xamlUserType34.AddEnumValue("TextboxSettings", FluentIcons.Common.Symbol.TextboxSettings);
					xamlUserType34.AddEnumValue("Thinking", FluentIcons.Common.Symbol.Thinking);
					xamlUserType34.AddEnumValue("ThumbDislike", FluentIcons.Common.Symbol.ThumbDislike);
					xamlUserType34.AddEnumValue("ThumbLike", FluentIcons.Common.Symbol.ThumbLike);
					xamlUserType34.AddEnumValue("ThumbLikeDislike", FluentIcons.Common.Symbol.ThumbLikeDislike);
					xamlUserType34.AddEnumValue("TicketDiagonal", FluentIcons.Common.Symbol.TicketDiagonal);
					xamlUserType34.AddEnumValue("TicketHorizontal", FluentIcons.Common.Symbol.TicketHorizontal);
					xamlUserType34.AddEnumValue("TimeAndWeather", FluentIcons.Common.Symbol.TimeAndWeather);
					xamlUserType34.AddEnumValue("TimePicker", FluentIcons.Common.Symbol.TimePicker);
					xamlUserType34.AddEnumValue("Timeline", FluentIcons.Common.Symbol.Timeline);
					xamlUserType34.AddEnumValue("Timer", FluentIcons.Common.Symbol.Timer);
					xamlUserType34.AddEnumValue("Timer10", FluentIcons.Common.Symbol.Timer10);
					xamlUserType34.AddEnumValue("Timer2", FluentIcons.Common.Symbol.Timer2);
					xamlUserType34.AddEnumValue("Timer3", FluentIcons.Common.Symbol.Timer3);
					xamlUserType34.AddEnumValue("TimerOff", FluentIcons.Common.Symbol.TimerOff);
					xamlUserType34.AddEnumValue("ToggleLeft", FluentIcons.Common.Symbol.ToggleLeft);
					xamlUserType34.AddEnumValue("ToggleMultiple", FluentIcons.Common.Symbol.ToggleMultiple);
					xamlUserType34.AddEnumValue("ToggleRight", FluentIcons.Common.Symbol.ToggleRight);
					xamlUserType34.AddEnumValue("Toolbox", FluentIcons.Common.Symbol.Toolbox);
					xamlUserType34.AddEnumValue("TooltipQuote", FluentIcons.Common.Symbol.TooltipQuote);
					xamlUserType34.AddEnumValue("TopSpeed", FluentIcons.Common.Symbol.TopSpeed);
					xamlUserType34.AddEnumValue("Translate", FluentIcons.Common.Symbol.Translate);
					xamlUserType34.AddEnumValue("TranslateAuto", FluentIcons.Common.Symbol.TranslateAuto);
					xamlUserType34.AddEnumValue("TranslateOff", FluentIcons.Common.Symbol.TranslateOff);
					xamlUserType34.AddEnumValue("Transmission", FluentIcons.Common.Symbol.Transmission);
					xamlUserType34.AddEnumValue("TransparencySquare", FluentIcons.Common.Symbol.TransparencySquare);
					xamlUserType34.AddEnumValue("TrayItemAdd", FluentIcons.Common.Symbol.TrayItemAdd);
					xamlUserType34.AddEnumValue("TrayItemRemove", FluentIcons.Common.Symbol.TrayItemRemove);
					xamlUserType34.AddEnumValue("TreeDeciduous", FluentIcons.Common.Symbol.TreeDeciduous);
					xamlUserType34.AddEnumValue("TreeEvergreen", FluentIcons.Common.Symbol.TreeEvergreen);
					xamlUserType34.AddEnumValue("Triangle", FluentIcons.Common.Symbol.Triangle);
					xamlUserType34.AddEnumValue("TriangleDown", FluentIcons.Common.Symbol.TriangleDown);
					xamlUserType34.AddEnumValue("TriangleLeft", FluentIcons.Common.Symbol.TriangleLeft);
					xamlUserType34.AddEnumValue("TriangleRight", FluentIcons.Common.Symbol.TriangleRight);
					xamlUserType34.AddEnumValue("TriangleUp", FluentIcons.Common.Symbol.TriangleUp);
					xamlUserType34.AddEnumValue("Trophy", FluentIcons.Common.Symbol.Trophy);
					xamlUserType34.AddEnumValue("TrophyLock", FluentIcons.Common.Symbol.TrophyLock);
					xamlUserType34.AddEnumValue("TrophyOff", FluentIcons.Common.Symbol.TrophyOff);
					xamlUserType34.AddEnumValue("Tv", FluentIcons.Common.Symbol.Tv);
					xamlUserType34.AddEnumValue("TvArrowRight", FluentIcons.Common.Symbol.TvArrowRight);
					xamlUserType34.AddEnumValue("TvUsb", FluentIcons.Common.Symbol.TvUsb);
					xamlUserType34.AddEnumValue("Umbrella", FluentIcons.Common.Symbol.Umbrella);
					xamlUserType34.AddEnumValue("UninstallApp", FluentIcons.Common.Symbol.UninstallApp);
					xamlUserType34.AddEnumValue("UsbPlug", FluentIcons.Common.Symbol.UsbPlug);
					xamlUserType34.AddEnumValue("UsbStick", FluentIcons.Common.Symbol.UsbStick);
					xamlUserType34.AddEnumValue("Vault", FluentIcons.Common.Symbol.Vault);
					xamlUserType34.AddEnumValue("VehicleBicycle", FluentIcons.Common.Symbol.VehicleBicycle);
					xamlUserType34.AddEnumValue("VehicleBus", FluentIcons.Common.Symbol.VehicleBus);
					xamlUserType34.AddEnumValue("VehicleCab", FluentIcons.Common.Symbol.VehicleCab);
					xamlUserType34.AddEnumValue("VehicleCableCar", FluentIcons.Common.Symbol.VehicleCableCar);
					xamlUserType34.AddEnumValue("VehicleCar", FluentIcons.Common.Symbol.VehicleCar);
					xamlUserType34.AddEnumValue("VehicleCarCollision", FluentIcons.Common.Symbol.VehicleCarCollision);
					xamlUserType34.AddEnumValue("VehicleCarParking", FluentIcons.Common.Symbol.VehicleCarParking);
					xamlUserType34.AddEnumValue("VehicleCarProfile", FluentIcons.Common.Symbol.VehicleCarProfile);
					xamlUserType34.AddEnumValue("VehicleCarProfileClock", FluentIcons.Common.Symbol.VehicleCarProfileClock);
					xamlUserType34.AddEnumValue("VehicleMotorcycle", FluentIcons.Common.Symbol.VehicleMotorcycle);
					xamlUserType34.AddEnumValue("VehicleShip", FluentIcons.Common.Symbol.VehicleShip);
					xamlUserType34.AddEnumValue("VehicleSubway", FluentIcons.Common.Symbol.VehicleSubway);
					xamlUserType34.AddEnumValue("VehicleSubwayClock", FluentIcons.Common.Symbol.VehicleSubwayClock);
					xamlUserType34.AddEnumValue("VehicleTractor", FluentIcons.Common.Symbol.VehicleTractor);
					xamlUserType34.AddEnumValue("VehicleTruck", FluentIcons.Common.Symbol.VehicleTruck);
					xamlUserType34.AddEnumValue("VehicleTruckBag", FluentIcons.Common.Symbol.VehicleTruckBag);
					xamlUserType34.AddEnumValue("VehicleTruckCube", FluentIcons.Common.Symbol.VehicleTruckCube);
					xamlUserType34.AddEnumValue("VehicleTruckProfile", FluentIcons.Common.Symbol.VehicleTruckProfile);
					xamlUserType34.AddEnumValue("Video", FluentIcons.Common.Symbol.Video);
					xamlUserType34.AddEnumValue("Video360", FluentIcons.Common.Symbol.Video360);
					xamlUserType34.AddEnumValue("Video360Off", FluentIcons.Common.Symbol.Video360Off);
					xamlUserType34.AddEnumValue("VideoAdd", FluentIcons.Common.Symbol.VideoAdd);
					xamlUserType34.AddEnumValue("VideoBackgroundEffect", FluentIcons.Common.Symbol.VideoBackgroundEffect);
					xamlUserType34.AddEnumValue("VideoBackgroundEffectHorizontal", FluentIcons.Common.Symbol.VideoBackgroundEffectHorizontal);
					xamlUserType34.AddEnumValue("VideoBluetooth", FluentIcons.Common.Symbol.VideoBluetooth);
					xamlUserType34.AddEnumValue("VideoChat", FluentIcons.Common.Symbol.VideoChat);
					xamlUserType34.AddEnumValue("VideoClip", FluentIcons.Common.Symbol.VideoClip);
					xamlUserType34.AddEnumValue("VideoClipMultiple", FluentIcons.Common.Symbol.VideoClipMultiple);
					xamlUserType34.AddEnumValue("VideoClipOff", FluentIcons.Common.Symbol.VideoClipOff);
					xamlUserType34.AddEnumValue("VideoClipOptimize", FluentIcons.Common.Symbol.VideoClipOptimize);
					xamlUserType34.AddEnumValue("VideoClipWand", FluentIcons.Common.Symbol.VideoClipWand);
					xamlUserType34.AddEnumValue("VideoMultiple", FluentIcons.Common.Symbol.VideoMultiple);
					xamlUserType34.AddEnumValue("VideoOff", FluentIcons.Common.Symbol.VideoOff);
					xamlUserType34.AddEnumValue("VideoPerson", FluentIcons.Common.Symbol.VideoPerson);
					xamlUserType34.AddEnumValue("VideoPersonCall", FluentIcons.Common.Symbol.VideoPersonCall);
					xamlUserType34.AddEnumValue("VideoPersonClock", FluentIcons.Common.Symbol.VideoPersonClock);
					xamlUserType34.AddEnumValue("VideoPersonOff", FluentIcons.Common.Symbol.VideoPersonOff);
					xamlUserType34.AddEnumValue("VideoPersonPulse", FluentIcons.Common.Symbol.VideoPersonPulse);
					xamlUserType34.AddEnumValue("VideoPersonSparkle", FluentIcons.Common.Symbol.VideoPersonSparkle);
					xamlUserType34.AddEnumValue("VideoPersonSparkleOff", FluentIcons.Common.Symbol.VideoPersonSparkleOff);
					xamlUserType34.AddEnumValue("VideoPersonStar", FluentIcons.Common.Symbol.VideoPersonStar);
					xamlUserType34.AddEnumValue("VideoPersonStarOff", FluentIcons.Common.Symbol.VideoPersonStarOff);
					xamlUserType34.AddEnumValue("VideoPlayPause", FluentIcons.Common.Symbol.VideoPlayPause);
					xamlUserType34.AddEnumValue("VideoProhibited", FluentIcons.Common.Symbol.VideoProhibited);
					xamlUserType34.AddEnumValue("VideoRecording", FluentIcons.Common.Symbol.VideoRecording);
					xamlUserType34.AddEnumValue("VideoSecurity", FluentIcons.Common.Symbol.VideoSecurity);
					xamlUserType34.AddEnumValue("VideoSettings", FluentIcons.Common.Symbol.VideoSettings);
					xamlUserType34.AddEnumValue("VideoSwitch", FluentIcons.Common.Symbol.VideoSwitch);
					xamlUserType34.AddEnumValue("VideoSync", FluentIcons.Common.Symbol.VideoSync);
					xamlUserType34.AddEnumValue("VideoUsb", FluentIcons.Common.Symbol.VideoUsb);
					xamlUserType34.AddEnumValue("ViewDesktop", FluentIcons.Common.Symbol.ViewDesktop);
					xamlUserType34.AddEnumValue("ViewDesktopMobile", FluentIcons.Common.Symbol.ViewDesktopMobile);
					xamlUserType34.AddEnumValue("VirtualNetwork", FluentIcons.Common.Symbol.VirtualNetwork);
					xamlUserType34.AddEnumValue("VirtualNetworkToolbox", FluentIcons.Common.Symbol.VirtualNetworkToolbox);
					xamlUserType34.AddEnumValue("Voicemail", FluentIcons.Common.Symbol.Voicemail);
					xamlUserType34.AddEnumValue("VoicemailArrowBack", FluentIcons.Common.Symbol.VoicemailArrowBack);
					xamlUserType34.AddEnumValue("VoicemailArrowForward", FluentIcons.Common.Symbol.VoicemailArrowForward);
					xamlUserType34.AddEnumValue("VoicemailArrowSubtract", FluentIcons.Common.Symbol.VoicemailArrowSubtract);
					xamlUserType34.AddEnumValue("VoicemailShield", FluentIcons.Common.Symbol.VoicemailShield);
					xamlUserType34.AddEnumValue("VoicemailSubtract", FluentIcons.Common.Symbol.VoicemailSubtract);
					xamlUserType34.AddEnumValue("Vote", FluentIcons.Common.Symbol.Vote);
					xamlUserType34.AddEnumValue("WalkieTalkie", FluentIcons.Common.Symbol.WalkieTalkie);
					xamlUserType34.AddEnumValue("Wallet", FluentIcons.Common.Symbol.Wallet);
					xamlUserType34.AddEnumValue("WalletCreditCard", FluentIcons.Common.Symbol.WalletCreditCard);
					xamlUserType34.AddEnumValue("Wallpaper", FluentIcons.Common.Symbol.Wallpaper);
					xamlUserType34.AddEnumValue("Wand", FluentIcons.Common.Symbol.Wand);
					xamlUserType34.AddEnumValue("Warning", FluentIcons.Common.Symbol.Warning);
					xamlUserType34.AddEnumValue("WarningLockOpen", FluentIcons.Common.Symbol.WarningLockOpen);
					xamlUserType34.AddEnumValue("WarningShield", FluentIcons.Common.Symbol.WarningShield);
					xamlUserType34.AddEnumValue("Washer", FluentIcons.Common.Symbol.Washer);
					xamlUserType34.AddEnumValue("Water", FluentIcons.Common.Symbol.Water);
					xamlUserType34.AddEnumValue("WeatherBlowingSnow", FluentIcons.Common.Symbol.WeatherBlowingSnow);
					xamlUserType34.AddEnumValue("WeatherCloudy", FluentIcons.Common.Symbol.WeatherCloudy);
					xamlUserType34.AddEnumValue("WeatherDrizzle", FluentIcons.Common.Symbol.WeatherDrizzle);
					xamlUserType34.AddEnumValue("WeatherDuststorm", FluentIcons.Common.Symbol.WeatherDuststorm);
					xamlUserType34.AddEnumValue("WeatherFog", FluentIcons.Common.Symbol.WeatherFog);
					xamlUserType34.AddEnumValue("WeatherHailDay", FluentIcons.Common.Symbol.WeatherHailDay);
					xamlUserType34.AddEnumValue("WeatherHailNight", FluentIcons.Common.Symbol.WeatherHailNight);
					xamlUserType34.AddEnumValue("WeatherHaze", FluentIcons.Common.Symbol.WeatherHaze);
					xamlUserType34.AddEnumValue("WeatherMoon", FluentIcons.Common.Symbol.WeatherMoon);
					xamlUserType34.AddEnumValue("WeatherMoonOff", FluentIcons.Common.Symbol.WeatherMoonOff);
					xamlUserType34.AddEnumValue("WeatherPartlyCloudyDay", FluentIcons.Common.Symbol.WeatherPartlyCloudyDay);
					xamlUserType34.AddEnumValue("WeatherPartlyCloudyNight", FluentIcons.Common.Symbol.WeatherPartlyCloudyNight);
					xamlUserType34.AddEnumValue("WeatherRain", FluentIcons.Common.Symbol.WeatherRain);
					xamlUserType34.AddEnumValue("WeatherRainShowersDay", FluentIcons.Common.Symbol.WeatherRainShowersDay);
					xamlUserType34.AddEnumValue("WeatherRainShowersNight", FluentIcons.Common.Symbol.WeatherRainShowersNight);
					xamlUserType34.AddEnumValue("WeatherRainSnow", FluentIcons.Common.Symbol.WeatherRainSnow);
					xamlUserType34.AddEnumValue("WeatherSnow", FluentIcons.Common.Symbol.WeatherSnow);
					xamlUserType34.AddEnumValue("WeatherSnowShowerDay", FluentIcons.Common.Symbol.WeatherSnowShowerDay);
					xamlUserType34.AddEnumValue("WeatherSnowShowerNight", FluentIcons.Common.Symbol.WeatherSnowShowerNight);
					xamlUserType34.AddEnumValue("WeatherSnowflake", FluentIcons.Common.Symbol.WeatherSnowflake);
					xamlUserType34.AddEnumValue("WeatherSqualls", FluentIcons.Common.Symbol.WeatherSqualls);
					xamlUserType34.AddEnumValue("WeatherSunny", FluentIcons.Common.Symbol.WeatherSunny);
					xamlUserType34.AddEnumValue("WeatherSunnyHigh", FluentIcons.Common.Symbol.WeatherSunnyHigh);
					xamlUserType34.AddEnumValue("WeatherSunnyLow", FluentIcons.Common.Symbol.WeatherSunnyLow);
					xamlUserType34.AddEnumValue("WeatherThunderstorm", FluentIcons.Common.Symbol.WeatherThunderstorm);
					xamlUserType34.AddEnumValue("WebAsset", FluentIcons.Common.Symbol.WebAsset);
					xamlUserType34.AddEnumValue("Whiteboard", FluentIcons.Common.Symbol.Whiteboard);
					xamlUserType34.AddEnumValue("WhiteboardOff", FluentIcons.Common.Symbol.WhiteboardOff);
					xamlUserType34.AddEnumValue("Wifi1", FluentIcons.Common.Symbol.Wifi1);
					xamlUserType34.AddEnumValue("Wifi2", FluentIcons.Common.Symbol.Wifi2);
					xamlUserType34.AddEnumValue("Wifi3", FluentIcons.Common.Symbol.Wifi3);
					xamlUserType34.AddEnumValue("Wifi4", FluentIcons.Common.Symbol.Wifi4);
					xamlUserType34.AddEnumValue("WifiLock", FluentIcons.Common.Symbol.WifiLock);
					xamlUserType34.AddEnumValue("WifiOff", FluentIcons.Common.Symbol.WifiOff);
					xamlUserType34.AddEnumValue("WifiSettings", FluentIcons.Common.Symbol.WifiSettings);
					xamlUserType34.AddEnumValue("WifiWarning", FluentIcons.Common.Symbol.WifiWarning);
					xamlUserType34.AddEnumValue("Window", FluentIcons.Common.Symbol.Window);
					xamlUserType34.AddEnumValue("WindowAd", FluentIcons.Common.Symbol.WindowAd);
					xamlUserType34.AddEnumValue("WindowAdOff", FluentIcons.Common.Symbol.WindowAdOff);
					xamlUserType34.AddEnumValue("WindowAdPerson", FluentIcons.Common.Symbol.WindowAdPerson);
					xamlUserType34.AddEnumValue("WindowApps", FluentIcons.Common.Symbol.WindowApps);
					xamlUserType34.AddEnumValue("WindowArrowUp", FluentIcons.Common.Symbol.WindowArrowUp);
					xamlUserType34.AddEnumValue("WindowBrush", FluentIcons.Common.Symbol.WindowBrush);
					xamlUserType34.AddEnumValue("WindowBulletList", FluentIcons.Common.Symbol.WindowBulletList);
					xamlUserType34.AddEnumValue("WindowBulletListAdd", FluentIcons.Common.Symbol.WindowBulletListAdd);
					xamlUserType34.AddEnumValue("WindowColumnOneFourthLeft", FluentIcons.Common.Symbol.WindowColumnOneFourthLeft);
					xamlUserType34.AddEnumValue("WindowColumnOneFourthLeftFocusLeft", FluentIcons.Common.Symbol.WindowColumnOneFourthLeftFocusLeft);
					xamlUserType34.AddEnumValue("WindowColumnOneFourthLeftFocusTop", FluentIcons.Common.Symbol.WindowColumnOneFourthLeftFocusTop);
					xamlUserType34.AddEnumValue("WindowConsole", FluentIcons.Common.Symbol.WindowConsole);
					xamlUserType34.AddEnumValue("WindowDatabase", FluentIcons.Common.Symbol.WindowDatabase);
					xamlUserType34.AddEnumValue("WindowDevEdit", FluentIcons.Common.Symbol.WindowDevEdit);
					xamlUserType34.AddEnumValue("WindowDevTools", FluentIcons.Common.Symbol.WindowDevTools);
					xamlUserType34.AddEnumValue("WindowEdit", FluentIcons.Common.Symbol.WindowEdit);
					xamlUserType34.AddEnumValue("WindowFingerprint", FluentIcons.Common.Symbol.WindowFingerprint);
					xamlUserType34.AddEnumValue("WindowHeaderHorizontal", FluentIcons.Common.Symbol.WindowHeaderHorizontal);
					xamlUserType34.AddEnumValue("WindowHeaderHorizontalOff", FluentIcons.Common.Symbol.WindowHeaderHorizontalOff);
					xamlUserType34.AddEnumValue("WindowHeaderVertical", FluentIcons.Common.Symbol.WindowHeaderVertical);
					xamlUserType34.AddEnumValue("WindowInprivate", FluentIcons.Common.Symbol.WindowInprivate);
					xamlUserType34.AddEnumValue("WindowInprivateAccount", FluentIcons.Common.Symbol.WindowInprivateAccount);
					xamlUserType34.AddEnumValue("WindowLocationTarget", FluentIcons.Common.Symbol.WindowLocationTarget);
					xamlUserType34.AddEnumValue("WindowMultiple", FluentIcons.Common.Symbol.WindowMultiple);
					xamlUserType34.AddEnumValue("WindowMultipleSwap", FluentIcons.Common.Symbol.WindowMultipleSwap);
					xamlUserType34.AddEnumValue("WindowNew", FluentIcons.Common.Symbol.WindowNew);
					xamlUserType34.AddEnumValue("WindowPlay", FluentIcons.Common.Symbol.WindowPlay);
					xamlUserType34.AddEnumValue("WindowSettings", FluentIcons.Common.Symbol.WindowSettings);
					xamlUserType34.AddEnumValue("WindowShield", FluentIcons.Common.Symbol.WindowShield);
					xamlUserType34.AddEnumValue("WindowText", FluentIcons.Common.Symbol.WindowText);
					xamlUserType34.AddEnumValue("WindowWrench", FluentIcons.Common.Symbol.WindowWrench);
					xamlUserType34.AddEnumValue("Wrench", FluentIcons.Common.Symbol.Wrench);
					xamlUserType34.AddEnumValue("WrenchScrewdriver", FluentIcons.Common.Symbol.WrenchScrewdriver);
					xamlUserType34.AddEnumValue("WrenchSettings", FluentIcons.Common.Symbol.WrenchSettings);
					xamlUserType34.AddEnumValue("XboxConsole", FluentIcons.Common.Symbol.XboxConsole);
					xamlUserType34.AddEnumValue("XboxController", FluentIcons.Common.Symbol.XboxController);
					xamlUserType34.AddEnumValue("XboxControllerError", FluentIcons.Common.Symbol.XboxControllerError);
					xamlUserType34.AddEnumValue("Xray", FluentIcons.Common.Symbol.Xray);
					xamlUserType34.AddEnumValue("ZoomFit", FluentIcons.Common.Symbol.ZoomFit);
					xamlUserType34.AddEnumValue("ZoomIn", FluentIcons.Common.Symbol.ZoomIn);
					xamlUserType34.AddEnumValue("ZoomOut", FluentIcons.Common.Symbol.ZoomOut);
					result = xamlUserType34;
					break;
				}
			case 43:
				{
					XamlUserType xamlUserType33 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType33.StaticInitializer = StaticInitializer_43_IconVariant;
					xamlUserType33.AddEnumValue("Regular", IconVariant.Regular);
					xamlUserType33.AddEnumValue("Filled", IconVariant.Filled);
					xamlUserType33.AddEnumValue("Color", IconVariant.Color);
					xamlUserType33.AddEnumValue("Light", IconVariant.Light);
					result = xamlUserType33;
					break;
				}
			case 44:
				{
					XamlUserType xamlUserType32 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Primitives.RangeBase"));
					xamlUserType32.Activator = Activate_44_ProgressBar;
					xamlUserType32.StaticInitializer = StaticInitializer_44_ProgressBar;
					xamlUserType32.AddMemberName("IsIndeterminate");
					xamlUserType32.AddMemberName("ShowError");
					xamlUserType32.AddMemberName("ShowPaused");
					xamlUserType32.AddMemberName("TemplateSettings");
					result = xamlUserType32;
					break;
				}
			case 45:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 46:
				{
					XamlUserType xamlUserType31 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType31.StaticInitializer = StaticInitializer_46_ProgressBarTemplateSettings;
					xamlUserType31.SetIsReturnTypeStub();
					result = xamlUserType31;
					break;
				}
			case 47:
				{
					XamlUserType xamlUserType30 = new XamlUserType(this, fullName, type, GetXamlTypeByName("WinUISample.Controls.PlayerOverlayBase"));
					xamlUserType30.StaticInitializer = StaticInitializer_47_PlayerOverlay;
					xamlUserType30.SetIsLocalType();
					result = xamlUserType30;
					break;
				}
			case 48:
				{
					XamlUserType xamlUserType29 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutUserControlBase`1<WinUISample.ViewModels.AppViewModel>"));
					xamlUserType29.StaticInitializer = StaticInitializer_48_RootLayoutBase;
					xamlUserType29.SetIsLocalType();
					result = xamlUserType29;
					break;
				}
			case 49:
				{
					XamlUserType xamlUserType28 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutUserControlBase"));
					xamlUserType28.StaticInitializer = StaticInitializer_49_LayoutUserControlBase;
					xamlUserType28.AddMemberName("ViewModel");
					result = xamlUserType28;
					break;
				}
			case 50:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.UserControl"))
				{
					StaticInitializer = StaticInitializer_50_LayoutUserControlBase
				};
				break;
			case 51:
				{
					XamlUserType xamlUserType27 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.ViewModels.ViewModelBase"));
					xamlUserType27.StaticInitializer = StaticInitializer_51_AppViewModel;
					xamlUserType27.SetIsReturnTypeStub();
					xamlUserType27.SetIsLocalType();
					result = xamlUserType27;
					break;
				}
			case 52:
				{
					XamlUserType xamlUserType26 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutControlBase"));
					xamlUserType26.Activator = Activate_52_AppTitleBar;
					xamlUserType26.StaticInitializer = StaticInitializer_52_AppTitleBar;
					xamlUserType26.SetContentPropertyName("Richasy.WinUIKernel.Share.Base.AppTitleBar.Content");
					xamlUserType26.AddMemberName("Content");
					xamlUserType26.AddMemberName("Title");
					xamlUserType26.AddMemberName("Header");
					xamlUserType26.AddMemberName("IconElement");
					xamlUserType26.AddMemberName("Subtitle");
					xamlUserType26.AddMemberName("CenterContent");
					xamlUserType26.AddMemberName("Footer");
					xamlUserType26.AddMemberName("IsBackButtonVisible");
					xamlUserType26.AddMemberName("IsBackEnabled");
					xamlUserType26.AddMemberName("IsPaneToggleButtonVisible");
					xamlUserType26.AddMemberName("TemplateSettings");
					xamlUserType26.AddMemberName("BackIcon");
					xamlUserType26.AddMemberName("TitleMaxWidth");
					result = xamlUserType26;
					break;
				}
			case 53:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"))
				{
					StaticInitializer = StaticInitializer_53_LayoutControlBase
				};
				break;
			case 54:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 55:
				{
					XamlUserType xamlUserType25 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType25.StaticInitializer = StaticInitializer_55_AppTitleBarTemplateSettings;
					xamlUserType25.SetIsReturnTypeStub();
					result = xamlUserType25;
					break;
				}
			case 56:
				{
					XamlUserType xamlUserType24 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.UserControl"));
					xamlUserType24.Activator = Activate_56_TrimTextBlock;
					xamlUserType24.StaticInitializer = StaticInitializer_56_TrimTextBlock;
					xamlUserType24.AddMemberName("MaxLines");
					xamlUserType24.AddMemberName("Text");
					xamlUserType24.AddMemberName("IsTextSelectionEnabled");
					result = xamlUserType24;
					break;
				}
			case 57:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 58:
				{
					XamlUserType xamlUserType23 = new XamlUserType(this, fullName, type, GetXamlTypeByName("WinUISample.Controls.RootLayoutBase"));
					xamlUserType23.Activator = Activate_58_RootLayout;
					xamlUserType23.StaticInitializer = StaticInitializer_58_RootLayout;
					xamlUserType23.SetIsLocalType();
					result = xamlUserType23;
					break;
				}
			case 59:
				{
					XamlUserType xamlUserType22 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentDialog"));
					xamlUserType22.StaticInitializer = StaticInitializer_59_SubtitleSearchDialog;
					xamlUserType22.SetIsLocalType();
					result = xamlUserType22;
					break;
				}
			case 60:
				{
					XamlUserType xamlUserType21 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
					xamlUserType21.Activator = Activate_60_NumberBox;
					xamlUserType21.StaticInitializer = StaticInitializer_60_NumberBox;
					xamlUserType21.AddMemberName("Header");
					xamlUserType21.AddMemberName("SpinButtonPlacementMode");
					xamlUserType21.AddMemberName("SmallChange");
					xamlUserType21.AddMemberName("LargeChange");
					xamlUserType21.AddMemberName("Minimum");
					xamlUserType21.AddMemberName("Maximum");
					xamlUserType21.AddMemberName("AcceptsExpression");
					xamlUserType21.AddMemberName("PlaceholderText");
					xamlUserType21.AddMemberName("Description");
					xamlUserType21.AddMemberName("HeaderTemplate");
					xamlUserType21.AddMemberName("IsWrapEnabled");
					xamlUserType21.AddMemberName("NumberFormatter");
					xamlUserType21.AddMemberName("PreventKeyboardDisplayOnProgrammaticFocus");
					xamlUserType21.AddMemberName("SelectionFlyout");
					xamlUserType21.AddMemberName("SelectionHighlightColor");
					xamlUserType21.AddMemberName("Text");
					xamlUserType21.AddMemberName("TextReadingOrder");
					xamlUserType21.AddMemberName("ValidationMode");
					xamlUserType21.AddMemberName("Value");
					result = xamlUserType21;
					break;
				}
			case 61:
				{
					XamlUserType xamlUserType20 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType20.StaticInitializer = StaticInitializer_61_NumberBoxSpinButtonPlacementMode;
					xamlUserType20.AddEnumValue("Hidden", NumberBoxSpinButtonPlacementMode.Hidden);
					xamlUserType20.AddEnumValue("Compact", NumberBoxSpinButtonPlacementMode.Compact);
					xamlUserType20.AddEnumValue("Inline", NumberBoxSpinButtonPlacementMode.Inline);
					result = xamlUserType20;
					break;
				}
			case 62:
				{
					XamlUserType xamlUserType19 = new XamlUserType(this, fullName, type, null);
					xamlUserType19.StaticInitializer = StaticInitializer_62_INumberFormatter2;
					xamlUserType19.SetIsReturnTypeStub();
					result = xamlUserType19;
					break;
				}
			case 63:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 64:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 65:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 66:
				{
					XamlUserType xamlUserType18 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType18.StaticInitializer = StaticInitializer_66_NumberBoxValidationMode;
					xamlUserType18.AddEnumValue("InvalidInputOverwritten", NumberBoxValidationMode.InvalidInputOverwritten);
					xamlUserType18.AddEnumValue("Disabled", NumberBoxValidationMode.Disabled);
					result = xamlUserType18;
					break;
				}
			case 67:
				{
					XamlUserType xamlUserType17 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentDialog"));
					xamlUserType17.StaticInitializer = StaticInitializer_67_SubtitleSyncDialog;
					xamlUserType17.SetIsLocalType();
					result = xamlUserType17;
					break;
				}
			case 68:
				{
					XamlUserType xamlUserType16 = new XamlUserType(this, fullName, type, GetXamlTypeByName("WinUIEx.WindowEx"));
					xamlUserType16.StaticInitializer = StaticInitializer_68_WindowBase;
					xamlUserType16.SetContentPropertyName("WinUIEx.WindowEx.WindowContent");
					result = xamlUserType16;
					break;
				}
			case 69:
				{
					XamlUserType xamlUserType15 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Window"));
					xamlUserType15.Activator = Activate_69_WindowEx;
					xamlUserType15.StaticInitializer = StaticInitializer_69_WindowEx;
					xamlUserType15.SetContentPropertyName("WinUIEx.WindowEx.WindowContent");
					xamlUserType15.AddMemberName("WindowContent");
					xamlUserType15.AddMemberName("Title");
					xamlUserType15.AddMemberName("AppWindow");
					xamlUserType15.AddMemberName("TaskBarIcon");
					xamlUserType15.AddMemberName("PersistenceId");
					xamlUserType15.AddMemberName("IsTitleBarVisible");
					xamlUserType15.AddMemberName("IsMinimizable");
					xamlUserType15.AddMemberName("IsMaximizable");
					xamlUserType15.AddMemberName("IsResizable");
					xamlUserType15.AddMemberName("WindowState");
					xamlUserType15.AddMemberName("IsShownInSwitchers");
					xamlUserType15.AddMemberName("IsAlwaysOnTop");
					xamlUserType15.AddMemberName("Presenter");
					xamlUserType15.AddMemberName("PresenterKind");
					xamlUserType15.AddMemberName("Width");
					xamlUserType15.AddMemberName("Height");
					xamlUserType15.AddMemberName("MinWidth");
					xamlUserType15.AddMemberName("MinHeight");
					xamlUserType15.AddMemberName("MaxWidth");
					xamlUserType15.AddMemberName("MaxHeight");
					xamlUserType15.AddMemberName("Backdrop");
					result = xamlUserType15;
					break;
				}
			case 70:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 71:
				{
					XamlUserType xamlUserType14 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType14.StaticInitializer = StaticInitializer_71_AppWindow;
					xamlUserType14.SetIsReturnTypeStub();
					result = xamlUserType14;
					break;
				}
			case 72:
				{
					XamlUserType xamlUserType12 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType12.StaticInitializer = StaticInitializer_72_Icon;
					xamlUserType12.CreateFromStringMethod = WinUIEx.Icon.FromFile;
					xamlUserType12.SetIsReturnTypeStub();
					result = xamlUserType12;
					break;
				}
			case 73:
				{
					XamlUserType xamlUserType11 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType11.StaticInitializer = StaticInitializer_73_WindowState;
					xamlUserType11.AddEnumValue("Normal", WindowState.Normal);
					xamlUserType11.AddEnumValue("Minimized", WindowState.Minimized);
					xamlUserType11.AddEnumValue("Maximized", WindowState.Maximized);
					result = xamlUserType11;
					break;
				}
			case 74:
				{
					XamlUserType xamlUserType10 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType10.StaticInitializer = StaticInitializer_74_AppWindowPresenter;
					xamlUserType10.SetIsReturnTypeStub();
					result = xamlUserType10;
					break;
				}
			case 75:
				{
					XamlUserType xamlUserType9 = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
					xamlUserType9.StaticInitializer = StaticInitializer_75_AppWindowPresenterKind;
					xamlUserType9.AddEnumValue("Default", AppWindowPresenterKind.Default);
					xamlUserType9.AddEnumValue("CompactOverlay", AppWindowPresenterKind.CompactOverlay);
					xamlUserType9.AddEnumValue("FullScreen", AppWindowPresenterKind.FullScreen);
					xamlUserType9.AddEnumValue("Overlapped", AppWindowPresenterKind.Overlapped);
					result = xamlUserType9;
					break;
				}
			case 76:
				{
					XamlUserType xamlUserType8 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
					xamlUserType8.StaticInitializer = StaticInitializer_76_SystemBackdrop;
					xamlUserType8.SetIsReturnTypeStub();
					result = xamlUserType8;
					break;
				}
			case 77:
				{
					XamlUserType xamlUserType7 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.WindowBase"));
					xamlUserType7.Activator = Activate_77_MainWindow;
					xamlUserType7.StaticInitializer = StaticInitializer_77_MainWindow;
					xamlUserType7.SetContentPropertyName("WinUIEx.WindowEx.WindowContent");
					xamlUserType7.AddMemberName("AppVM");
					xamlUserType7.SetIsLocalType();
					result = xamlUserType7;
					break;
				}
			case 78:
				{
					XamlUserType xamlUserType6 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutPageBase`1<WinUISample.ViewModels.LocalVideoPageViewModel>"));
					xamlUserType6.StaticInitializer = StaticInitializer_78_LocalVideoPageBase;
					xamlUserType6.SetIsLocalType();
					result = xamlUserType6;
					break;
				}
			case 79:
				{
					XamlUserType xamlUserType5 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutPageBase"));
					xamlUserType5.StaticInitializer = StaticInitializer_79_LayoutPageBase;
					xamlUserType5.AddMemberName("ViewModel");
					result = xamlUserType5;
					break;
				}
			case 80:
				result = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"))
				{
					StaticInitializer = StaticInitializer_80_LayoutPageBase
				};
				break;
			case 81:
				result = new XamlSystemBaseType(fullName, type);
				break;
			case 82:
				{
					XamlUserType xamlUserType4 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Richasy.WinUIKernel.Share.ViewModels.ViewModelBase"));
					xamlUserType4.StaticInitializer = StaticInitializer_82_LocalVideoPageViewModel;
					xamlUserType4.SetIsReturnTypeStub();
					xamlUserType4.SetIsLocalType();
					result = xamlUserType4;
					break;
				}
			case 83:
				{
					XamlUserType xamlUserType3 = new XamlUserType(this, fullName, type, GetXamlTypeByName("WinUISample.Pages.LocalVideoPageBase"));
					xamlUserType3.Activator = Activate_83_LocalVideoPage;
					xamlUserType3.StaticInitializer = StaticInitializer_83_LocalVideoPage;
					xamlUserType3.SetIsLocalType();
					result = xamlUserType3;
					break;
				}
			case 84:
				{
					XamlUserType xamlUserType2 = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
					xamlUserType2.Activator = Activate_84_TreeViewNode;
					xamlUserType2.StaticInitializer = StaticInitializer_84_TreeViewNode;
					xamlUserType2.AddMemberName("Children");
					xamlUserType2.AddMemberName("Content");
					xamlUserType2.AddMemberName("Depth");
					xamlUserType2.AddMemberName("HasChildren");
					xamlUserType2.AddMemberName("HasUnrealizedChildren");
					xamlUserType2.AddMemberName("IsExpanded");
					xamlUserType2.AddMemberName("Parent");
					xamlUserType2.SetIsBindable();
					result = xamlUserType2;
					break;
				}
			case 85:
				{
					XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
					xamlUserType.StaticInitializer = StaticInitializer_85_IList;
					xamlUserType.CollectionAdd = VectorAdd_85_IList;
					xamlUserType.SetIsReturnTypeStub();
					result = xamlUserType;
					break;
				}
		}
		return result;
	}

	private IXamlType CheckOtherMetadataProvidersForName(string typeName)
	{
		IXamlType xamlType = null;
		IXamlType result = null;
		foreach (IXamlMetadataProvider otherProvider in OtherProviders)
		{
			xamlType = otherProvider.GetXamlType(typeName);
			if (xamlType != null)
			{
				if (xamlType.IsConstructible)
				{
					return xamlType;
				}
				result = xamlType;
			}
		}
		return result;
	}

	private IXamlType CheckOtherMetadataProvidersForType(Type type)
	{
		IXamlType xamlType = null;
		IXamlType result = null;
		foreach (IXamlMetadataProvider otherProvider in OtherProviders)
		{
			xamlType = otherProvider.GetXamlType(type);
			if (xamlType != null)
			{
				if (xamlType.IsConstructible)
				{
					return xamlType;
				}
				result = xamlType;
			}
		}
		return result;
	}

	private object get_0_XamlControlsResources_UseCompactResources(object instance)
	{
		return ((XamlControlsResources)instance).UseCompactResources;
	}

	private void set_0_XamlControlsResources_UseCompactResources(object instance, object Value)
	{
		((XamlControlsResources)instance).UseCompactResources = (bool)Value;
	}

	private object get_1_BoolToVisibilityConverter_IsReverse(object instance)
	{
		return ((BoolToVisibilityConverter)instance).IsReverse;
	}

	private void set_1_BoolToVisibilityConverter_IsReverse(object instance, object Value)
	{
		((BoolToVisibilityConverter)instance).IsReverse = (bool)Value;
	}

	private object get_2_ObjectToBoolConverter_IsReverse(object instance)
	{
		return ((ObjectToBoolConverter)instance).IsReverse;
	}

	private void set_2_ObjectToBoolConverter_IsReverse(object instance, object Value)
	{
		((ObjectToBoolConverter)instance).IsReverse = (bool)Value;
	}

	private object get_3_ObjectToVisibilityConverter_IsReverse(object instance)
	{
		return ((ObjectToVisibilityConverter)instance).IsReverse;
	}

	private void set_3_ObjectToVisibilityConverter_IsReverse(object instance, object Value)
	{
		((ObjectToVisibilityConverter)instance).IsReverse = (bool)Value;
	}

	private object get_4_ProgressRing_IsActive(object instance)
	{
		return ((ProgressRing)instance).IsActive;
	}

	private void set_4_ProgressRing_IsActive(object instance, object Value)
	{
		((ProgressRing)instance).IsActive = (bool)Value;
	}

	private object get_5_ProgressRing_IsIndeterminate(object instance)
	{
		return ((ProgressRing)instance).IsIndeterminate;
	}

	private void set_5_ProgressRing_IsIndeterminate(object instance, object Value)
	{
		((ProgressRing)instance).IsIndeterminate = (bool)Value;
	}

	private object get_6_ProgressRing_Maximum(object instance)
	{
		return ((ProgressRing)instance).Maximum;
	}

	private void set_6_ProgressRing_Maximum(object instance, object Value)
	{
		((ProgressRing)instance).Maximum = (double)Value;
	}

	private object get_7_ProgressRing_Minimum(object instance)
	{
		return ((ProgressRing)instance).Minimum;
	}

	private void set_7_ProgressRing_Minimum(object instance, object Value)
	{
		((ProgressRing)instance).Minimum = (double)Value;
	}

	private object get_8_ProgressRing_TemplateSettings(object instance)
	{
		return ((ProgressRing)instance).TemplateSettings;
	}

	private object get_9_ProgressRing_Value(object instance)
	{
		return ((ProgressRing)instance).Value;
	}

	private void set_9_ProgressRing_Value(object instance, object Value)
	{
		((ProgressRing)instance).Value = (double)Value;
	}

	private object get_10_InfoBar_Content(object instance)
	{
		return ((InfoBar)instance).Content;
	}

	private void set_10_InfoBar_Content(object instance, object Value)
	{
		((InfoBar)instance).Content = Value;
	}

	private object get_11_InfoBar_IsOpen(object instance)
	{
		return ((InfoBar)instance).IsOpen;
	}

	private void set_11_InfoBar_IsOpen(object instance, object Value)
	{
		((InfoBar)instance).IsOpen = (bool)Value;
	}

	private object get_12_InfoBar_IsClosable(object instance)
	{
		return ((InfoBar)instance).IsClosable;
	}

	private void set_12_InfoBar_IsClosable(object instance, object Value)
	{
		((InfoBar)instance).IsClosable = (bool)Value;
	}

	private object get_13_InfoBar_Severity(object instance)
	{
		return ((InfoBar)instance).Severity;
	}

	private void set_13_InfoBar_Severity(object instance, object Value)
	{
		((InfoBar)instance).Severity = (InfoBarSeverity)Value;
	}

	private object get_14_InfoBar_ActionButton(object instance)
	{
		return ((InfoBar)instance).ActionButton;
	}

	private void set_14_InfoBar_ActionButton(object instance, object Value)
	{
		((InfoBar)instance).ActionButton = (ButtonBase)Value;
	}

	private object get_15_InfoBar_CloseButtonCommand(object instance)
	{
		return ((InfoBar)instance).CloseButtonCommand;
	}

	private void set_15_InfoBar_CloseButtonCommand(object instance, object Value)
	{
		((InfoBar)instance).CloseButtonCommand = (ICommand)Value;
	}

	private object get_16_InfoBar_CloseButtonCommandParameter(object instance)
	{
		return ((InfoBar)instance).CloseButtonCommandParameter;
	}

	private void set_16_InfoBar_CloseButtonCommandParameter(object instance, object Value)
	{
		((InfoBar)instance).CloseButtonCommandParameter = Value;
	}

	private object get_17_InfoBar_CloseButtonStyle(object instance)
	{
		return ((InfoBar)instance).CloseButtonStyle;
	}

	private void set_17_InfoBar_CloseButtonStyle(object instance, object Value)
	{
		((InfoBar)instance).CloseButtonStyle = (Style)Value;
	}

	private object get_18_InfoBar_ContentTemplate(object instance)
	{
		return ((InfoBar)instance).ContentTemplate;
	}

	private void set_18_InfoBar_ContentTemplate(object instance, object Value)
	{
		((InfoBar)instance).ContentTemplate = (DataTemplate)Value;
	}

	private object get_19_InfoBar_IconSource(object instance)
	{
		return ((InfoBar)instance).IconSource;
	}

	private void set_19_InfoBar_IconSource(object instance, object Value)
	{
		((InfoBar)instance).IconSource = (IconSource)Value;
	}

	private object get_20_InfoBar_IsIconVisible(object instance)
	{
		return ((InfoBar)instance).IsIconVisible;
	}

	private void set_20_InfoBar_IsIconVisible(object instance, object Value)
	{
		((InfoBar)instance).IsIconVisible = (bool)Value;
	}

	private object get_21_InfoBar_Message(object instance)
	{
		return ((InfoBar)instance).Message;
	}

	private void set_21_InfoBar_Message(object instance, object Value)
	{
		((InfoBar)instance).Message = (string)Value;
	}

	private object get_22_InfoBar_TemplateSettings(object instance)
	{
		return ((InfoBar)instance).TemplateSettings;
	}

	private object get_23_InfoBar_Title(object instance)
	{
		return ((InfoBar)instance).Title;
	}

	private void set_23_InfoBar_Title(object instance, object Value)
	{
		((InfoBar)instance).Title = (string)Value;
	}

	private object get_24_Expander_IsExpanded(object instance)
	{
		return ((Expander)instance).IsExpanded;
	}

	private void set_24_Expander_IsExpanded(object instance, object Value)
	{
		((Expander)instance).IsExpanded = (bool)Value;
	}

	private object get_25_Expander_Header(object instance)
	{
		return ((Expander)instance).Header;
	}

	private void set_25_Expander_Header(object instance, object Value)
	{
		((Expander)instance).Header = Value;
	}

	private object get_26_Expander_ExpandDirection(object instance)
	{
		return ((Expander)instance).ExpandDirection;
	}

	private void set_26_Expander_ExpandDirection(object instance, object Value)
	{
		((Expander)instance).ExpandDirection = (ExpandDirection)Value;
	}

	private object get_27_Expander_HeaderTemplate(object instance)
	{
		return ((Expander)instance).HeaderTemplate;
	}

	private void set_27_Expander_HeaderTemplate(object instance, object Value)
	{
		((Expander)instance).HeaderTemplate = (DataTemplate)Value;
	}

	private object get_28_Expander_HeaderTemplateSelector(object instance)
	{
		return ((Expander)instance).HeaderTemplateSelector;
	}

	private void set_28_Expander_HeaderTemplateSelector(object instance, object Value)
	{
		((Expander)instance).HeaderTemplateSelector = (DataTemplateSelector)Value;
	}

	private object get_29_Expander_TemplateSettings(object instance)
	{
		return ((Expander)instance).TemplateSettings;
	}

	private object get_30_PlayerOverlayBase_ViewModel(object instance)
	{
		return ((PlayerOverlayBase)instance).ViewModel;
	}

	private object get_31_SymbolIcon_Symbol(object instance)
	{
		return ((FluentIcons.WinUI.SymbolIcon)instance).Symbol;
	}

	private void set_31_SymbolIcon_Symbol(object instance, object Value)
	{
		((FluentIcons.WinUI.SymbolIcon)instance).Symbol = (FluentIcons.Common.Symbol)Value;
	}

	private object get_32_GenericIcon_IconVariant(object instance)
	{
		return ((GenericIcon)instance).IconVariant;
	}

	private void set_32_GenericIcon_IconVariant(object instance, object Value)
	{
		((GenericIcon)instance).IconVariant = (IconVariant)Value;
	}

	private object get_33_SymbolIcon_UseSegoeMetrics(object instance)
	{
		return ((FluentIcons.WinUI.SymbolIcon)instance).UseSegoeMetrics;
	}

	private void set_33_SymbolIcon_UseSegoeMetrics(object instance, object Value)
	{
		((FluentIcons.WinUI.SymbolIcon)instance).UseSegoeMetrics = (bool)Value;
	}

	private object get_34_ProgressBar_IsIndeterminate(object instance)
	{
		return ((ProgressBar)instance).IsIndeterminate;
	}

	private void set_34_ProgressBar_IsIndeterminate(object instance, object Value)
	{
		((ProgressBar)instance).IsIndeterminate = (bool)Value;
	}

	private object get_35_ProgressBar_ShowError(object instance)
	{
		return ((ProgressBar)instance).ShowError;
	}

	private void set_35_ProgressBar_ShowError(object instance, object Value)
	{
		((ProgressBar)instance).ShowError = (bool)Value;
	}

	private object get_36_ProgressBar_ShowPaused(object instance)
	{
		return ((ProgressBar)instance).ShowPaused;
	}

	private void set_36_ProgressBar_ShowPaused(object instance, object Value)
	{
		((ProgressBar)instance).ShowPaused = (bool)Value;
	}

	private object get_37_ProgressBar_TemplateSettings(object instance)
	{
		return ((ProgressBar)instance).TemplateSettings;
	}

	private object get_38_LayoutUserControlBase_ViewModel(object instance)
	{
		return ((LayoutUserControlBase<AppViewModel>)instance).ViewModel;
	}

	private void set_38_LayoutUserControlBase_ViewModel(object instance, object Value)
	{
		((LayoutUserControlBase<AppViewModel>)instance).ViewModel = (AppViewModel)Value;
	}

	private object get_39_AppTitleBar_Content(object instance)
	{
		return ((AppTitleBar)instance).Content;
	}

	private void set_39_AppTitleBar_Content(object instance, object Value)
	{
		((AppTitleBar)instance).Content = Value;
	}

	private object get_40_AppTitleBar_Title(object instance)
	{
		return ((AppTitleBar)instance).Title;
	}

	private void set_40_AppTitleBar_Title(object instance, object Value)
	{
		((AppTitleBar)instance).Title = (string)Value;
	}

	private object get_41_AppTitleBar_Header(object instance)
	{
		return ((AppTitleBar)instance).Header;
	}

	private void set_41_AppTitleBar_Header(object instance, object Value)
	{
		((AppTitleBar)instance).Header = Value;
	}

	private object get_42_AppTitleBar_IconElement(object instance)
	{
		return ((AppTitleBar)instance).IconElement;
	}

	private void set_42_AppTitleBar_IconElement(object instance, object Value)
	{
		((AppTitleBar)instance).IconElement = (IconElement)Value;
	}

	private object get_43_AppTitleBar_Subtitle(object instance)
	{
		return ((AppTitleBar)instance).Subtitle;
	}

	private void set_43_AppTitleBar_Subtitle(object instance, object Value)
	{
		((AppTitleBar)instance).Subtitle = (string)Value;
	}

	private object get_44_AppTitleBar_CenterContent(object instance)
	{
		return ((AppTitleBar)instance).CenterContent;
	}

	private void set_44_AppTitleBar_CenterContent(object instance, object Value)
	{
		((AppTitleBar)instance).CenterContent = Value;
	}

	private object get_45_AppTitleBar_Footer(object instance)
	{
		return ((AppTitleBar)instance).Footer;
	}

	private void set_45_AppTitleBar_Footer(object instance, object Value)
	{
		((AppTitleBar)instance).Footer = Value;
	}

	private object get_46_AppTitleBar_IsBackButtonVisible(object instance)
	{
		return ((AppTitleBar)instance).IsBackButtonVisible;
	}

	private void set_46_AppTitleBar_IsBackButtonVisible(object instance, object Value)
	{
		((AppTitleBar)instance).IsBackButtonVisible = (bool)Value;
	}

	private object get_47_AppTitleBar_IsBackEnabled(object instance)
	{
		return ((AppTitleBar)instance).IsBackEnabled;
	}

	private void set_47_AppTitleBar_IsBackEnabled(object instance, object Value)
	{
		((AppTitleBar)instance).IsBackEnabled = (bool)Value;
	}

	private object get_48_AppTitleBar_IsPaneToggleButtonVisible(object instance)
	{
		return ((AppTitleBar)instance).IsPaneToggleButtonVisible;
	}

	private void set_48_AppTitleBar_IsPaneToggleButtonVisible(object instance, object Value)
	{
		((AppTitleBar)instance).IsPaneToggleButtonVisible = (bool)Value;
	}

	private object get_49_AppTitleBar_TemplateSettings(object instance)
	{
		return ((AppTitleBar)instance).TemplateSettings;
	}

	private void set_49_AppTitleBar_TemplateSettings(object instance, object Value)
	{
		((AppTitleBar)instance).TemplateSettings = (AppTitleBarTemplateSettings)Value;
	}

	private object get_50_AppTitleBar_BackIcon(object instance)
	{
		return ((AppTitleBar)instance).BackIcon;
	}

	private void set_50_AppTitleBar_BackIcon(object instance, object Value)
	{
		((AppTitleBar)instance).BackIcon = (FluentIcons.Common.Symbol)Value;
	}

	private object get_51_AppTitleBar_TitleMaxWidth(object instance)
	{
		return ((AppTitleBar)instance).TitleMaxWidth;
	}

	private void set_51_AppTitleBar_TitleMaxWidth(object instance, object Value)
	{
		((AppTitleBar)instance).TitleMaxWidth = (double)Value;
	}

	private object get_52_TrimTextBlock_MaxLines(object instance)
	{
		return ((TrimTextBlock)instance).MaxLines;
	}

	private void set_52_TrimTextBlock_MaxLines(object instance, object Value)
	{
		((TrimTextBlock)instance).MaxLines = (int)Value;
	}

	private object get_53_TrimTextBlock_Text(object instance)
	{
		return ((TrimTextBlock)instance).Text;
	}

	private void set_53_TrimTextBlock_Text(object instance, object Value)
	{
		((TrimTextBlock)instance).Text = (string)Value;
	}

	private object get_54_TrimTextBlock_IsTextSelectionEnabled(object instance)
	{
		return ((TrimTextBlock)instance).IsTextSelectionEnabled;
	}

	private void set_54_TrimTextBlock_IsTextSelectionEnabled(object instance, object Value)
	{
		((TrimTextBlock)instance).IsTextSelectionEnabled = (bool)Value;
	}

	private object get_55_NumberBox_Header(object instance)
	{
		return ((NumberBox)instance).Header;
	}

	private void set_55_NumberBox_Header(object instance, object Value)
	{
		((NumberBox)instance).Header = Value;
	}

	private object get_56_NumberBox_SpinButtonPlacementMode(object instance)
	{
		return ((NumberBox)instance).SpinButtonPlacementMode;
	}

	private void set_56_NumberBox_SpinButtonPlacementMode(object instance, object Value)
	{
		((NumberBox)instance).SpinButtonPlacementMode = (NumberBoxSpinButtonPlacementMode)Value;
	}

	private object get_57_NumberBox_SmallChange(object instance)
	{
		return ((NumberBox)instance).SmallChange;
	}

	private void set_57_NumberBox_SmallChange(object instance, object Value)
	{
		((NumberBox)instance).SmallChange = (double)Value;
	}

	private object get_58_NumberBox_LargeChange(object instance)
	{
		return ((NumberBox)instance).LargeChange;
	}

	private void set_58_NumberBox_LargeChange(object instance, object Value)
	{
		((NumberBox)instance).LargeChange = (double)Value;
	}

	private object get_59_NumberBox_Minimum(object instance)
	{
		return ((NumberBox)instance).Minimum;
	}

	private void set_59_NumberBox_Minimum(object instance, object Value)
	{
		((NumberBox)instance).Minimum = (double)Value;
	}

	private object get_60_NumberBox_Maximum(object instance)
	{
		return ((NumberBox)instance).Maximum;
	}

	private void set_60_NumberBox_Maximum(object instance, object Value)
	{
		((NumberBox)instance).Maximum = (double)Value;
	}

	private object get_61_NumberBox_AcceptsExpression(object instance)
	{
		return ((NumberBox)instance).AcceptsExpression;
	}

	private void set_61_NumberBox_AcceptsExpression(object instance, object Value)
	{
		((NumberBox)instance).AcceptsExpression = (bool)Value;
	}

	private object get_62_NumberBox_PlaceholderText(object instance)
	{
		return ((NumberBox)instance).PlaceholderText;
	}

	private void set_62_NumberBox_PlaceholderText(object instance, object Value)
	{
		((NumberBox)instance).PlaceholderText = (string)Value;
	}

	private object get_63_NumberBox_Description(object instance)
	{
		return ((NumberBox)instance).Description;
	}

	private void set_63_NumberBox_Description(object instance, object Value)
	{
		((NumberBox)instance).Description = Value;
	}

	private object get_64_NumberBox_HeaderTemplate(object instance)
	{
		return ((NumberBox)instance).HeaderTemplate;
	}

	private void set_64_NumberBox_HeaderTemplate(object instance, object Value)
	{
		((NumberBox)instance).HeaderTemplate = (DataTemplate)Value;
	}

	private object get_65_NumberBox_IsWrapEnabled(object instance)
	{
		return ((NumberBox)instance).IsWrapEnabled;
	}

	private void set_65_NumberBox_IsWrapEnabled(object instance, object Value)
	{
		((NumberBox)instance).IsWrapEnabled = (bool)Value;
	}

	private object get_66_NumberBox_NumberFormatter(object instance)
	{
		return ((NumberBox)instance).NumberFormatter;
	}

	private void set_66_NumberBox_NumberFormatter(object instance, object Value)
	{
		((NumberBox)instance).NumberFormatter = (INumberFormatter2)Value;
	}

	private object get_67_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus(object instance)
	{
		return ((NumberBox)instance).PreventKeyboardDisplayOnProgrammaticFocus;
	}

	private void set_67_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus(object instance, object Value)
	{
		((NumberBox)instance).PreventKeyboardDisplayOnProgrammaticFocus = (bool)Value;
	}

	private object get_68_NumberBox_SelectionFlyout(object instance)
	{
		return ((NumberBox)instance).SelectionFlyout;
	}

	private void set_68_NumberBox_SelectionFlyout(object instance, object Value)
	{
		((NumberBox)instance).SelectionFlyout = (FlyoutBase)Value;
	}

	private object get_69_NumberBox_SelectionHighlightColor(object instance)
	{
		return ((NumberBox)instance).SelectionHighlightColor;
	}

	private void set_69_NumberBox_SelectionHighlightColor(object instance, object Value)
	{
		((NumberBox)instance).SelectionHighlightColor = (SolidColorBrush)Value;
	}

	private object get_70_NumberBox_Text(object instance)
	{
		return ((NumberBox)instance).Text;
	}

	private void set_70_NumberBox_Text(object instance, object Value)
	{
		((NumberBox)instance).Text = (string)Value;
	}

	private object get_71_NumberBox_TextReadingOrder(object instance)
	{
		return ((NumberBox)instance).TextReadingOrder;
	}

	private void set_71_NumberBox_TextReadingOrder(object instance, object Value)
	{
		((NumberBox)instance).TextReadingOrder = (TextReadingOrder)Value;
	}

	private object get_72_NumberBox_ValidationMode(object instance)
	{
		return ((NumberBox)instance).ValidationMode;
	}

	private void set_72_NumberBox_ValidationMode(object instance, object Value)
	{
		((NumberBox)instance).ValidationMode = (NumberBoxValidationMode)Value;
	}

	private object get_73_NumberBox_Value(object instance)
	{
		return ((NumberBox)instance).Value;
	}

	private void set_73_NumberBox_Value(object instance, object Value)
	{
		((NumberBox)instance).Value = (double)Value;
	}

	private object get_74_WindowEx_WindowContent(object instance)
	{
		return ((WindowEx)instance).WindowContent;
	}

	private void set_74_WindowEx_WindowContent(object instance, object Value)
	{
		((WindowEx)instance).WindowContent = Value;
	}

	private object get_75_WindowEx_Title(object instance)
	{
		return ((WindowEx)instance).Title;
	}

	private void set_75_WindowEx_Title(object instance, object Value)
	{
		((WindowEx)instance).Title = (string)Value;
	}

	private object get_76_WindowEx_AppWindow(object instance)
	{
		return ((WindowEx)instance).AppWindow;
	}

	private object get_77_WindowEx_TaskBarIcon(object instance)
	{
		return ((WindowEx)instance).TaskBarIcon;
	}

	private void set_77_WindowEx_TaskBarIcon(object instance, object Value)
	{
		((WindowEx)instance).TaskBarIcon = (WinUIEx.Icon)Value;
	}

	private object get_78_WindowEx_PersistenceId(object instance)
	{
		return ((WindowEx)instance).PersistenceId;
	}

	private void set_78_WindowEx_PersistenceId(object instance, object Value)
	{
		((WindowEx)instance).PersistenceId = (string)Value;
	}

	private object get_79_WindowEx_IsTitleBarVisible(object instance)
	{
		return ((WindowEx)instance).IsTitleBarVisible;
	}

	private void set_79_WindowEx_IsTitleBarVisible(object instance, object Value)
	{
		((WindowEx)instance).IsTitleBarVisible = (bool)Value;
	}

	private object get_80_WindowEx_IsMinimizable(object instance)
	{
		return ((WindowEx)instance).IsMinimizable;
	}

	private void set_80_WindowEx_IsMinimizable(object instance, object Value)
	{
		((WindowEx)instance).IsMinimizable = (bool)Value;
	}

	private object get_81_WindowEx_IsMaximizable(object instance)
	{
		return ((WindowEx)instance).IsMaximizable;
	}

	private void set_81_WindowEx_IsMaximizable(object instance, object Value)
	{
		((WindowEx)instance).IsMaximizable = (bool)Value;
	}

	private object get_82_WindowEx_IsResizable(object instance)
	{
		return ((WindowEx)instance).IsResizable;
	}

	private void set_82_WindowEx_IsResizable(object instance, object Value)
	{
		((WindowEx)instance).IsResizable = (bool)Value;
	}

	private object get_83_WindowEx_WindowState(object instance)
	{
		return ((WindowEx)instance).WindowState;
	}

	private void set_83_WindowEx_WindowState(object instance, object Value)
	{
		((WindowEx)instance).WindowState = (WindowState)Value;
	}

	private object get_84_WindowEx_IsShownInSwitchers(object instance)
	{
		return ((WindowEx)instance).IsShownInSwitchers;
	}

	private void set_84_WindowEx_IsShownInSwitchers(object instance, object Value)
	{
		((WindowEx)instance).IsShownInSwitchers = (bool)Value;
	}

	private object get_85_WindowEx_IsAlwaysOnTop(object instance)
	{
		return ((WindowEx)instance).IsAlwaysOnTop;
	}

	private void set_85_WindowEx_IsAlwaysOnTop(object instance, object Value)
	{
		((WindowEx)instance).IsAlwaysOnTop = (bool)Value;
	}

	private object get_86_WindowEx_Presenter(object instance)
	{
		return ((WindowEx)instance).Presenter;
	}

	private object get_87_WindowEx_PresenterKind(object instance)
	{
		return ((WindowEx)instance).PresenterKind;
	}

	private void set_87_WindowEx_PresenterKind(object instance, object Value)
	{
		((WindowEx)instance).PresenterKind = (AppWindowPresenterKind)Value;
	}

	private object get_88_WindowEx_Width(object instance)
	{
		return ((WindowEx)instance).Width;
	}

	private void set_88_WindowEx_Width(object instance, object Value)
	{
		((WindowEx)instance).Width = (double)Value;
	}

	private object get_89_WindowEx_Height(object instance)
	{
		return ((WindowEx)instance).Height;
	}

	private void set_89_WindowEx_Height(object instance, object Value)
	{
		((WindowEx)instance).Height = (double)Value;
	}

	private object get_90_WindowEx_MinWidth(object instance)
	{
		return ((WindowEx)instance).MinWidth;
	}

	private void set_90_WindowEx_MinWidth(object instance, object Value)
	{
		((WindowEx)instance).MinWidth = (double)Value;
	}

	private object get_91_WindowEx_MinHeight(object instance)
	{
		return ((WindowEx)instance).MinHeight;
	}

	private void set_91_WindowEx_MinHeight(object instance, object Value)
	{
		((WindowEx)instance).MinHeight = (double)Value;
	}

	private object get_92_WindowEx_MaxWidth(object instance)
	{
		return ((WindowEx)instance).MaxWidth;
	}

	private void set_92_WindowEx_MaxWidth(object instance, object Value)
	{
		((WindowEx)instance).MaxWidth = (double)Value;
	}

	private object get_93_WindowEx_MaxHeight(object instance)
	{
		return ((WindowEx)instance).MaxHeight;
	}

	private void set_93_WindowEx_MaxHeight(object instance, object Value)
	{
		((WindowEx)instance).MaxHeight = (double)Value;
	}

	private object get_94_WindowEx_Backdrop(object instance)
	{
		return ((WindowEx)instance).Backdrop;
	}

	private void set_94_WindowEx_Backdrop(object instance, object Value)
	{
		((WindowEx)instance).Backdrop = (WinUIEx.SystemBackdrop)Value;
	}

	private object get_95_MainWindow_AppVM(object instance)
	{
		return ((MainWindow)instance).AppVM;
	}

	private object get_96_LayoutPageBase_ViewModel(object instance)
	{
		return ((LayoutPageBase<LocalVideoPageViewModel>)instance).ViewModel;
	}

	private void set_96_LayoutPageBase_ViewModel(object instance, object Value)
	{
		((LayoutPageBase<LocalVideoPageViewModel>)instance).ViewModel = (LocalVideoPageViewModel)Value;
	}

	private object get_97_TreeViewNode_Children(object instance)
	{
		return ((TreeViewNode)instance).Children;
	}

	private object get_98_TreeViewNode_Content(object instance)
	{
		return ((TreeViewNode)instance).Content;
	}

	private void set_98_TreeViewNode_Content(object instance, object Value)
	{
		((TreeViewNode)instance).Content = Value;
	}

	private object get_99_TreeViewNode_Depth(object instance)
	{
		return ((TreeViewNode)instance).Depth;
	}

	private object get_100_TreeViewNode_HasChildren(object instance)
	{
		return ((TreeViewNode)instance).HasChildren;
	}

	private object get_101_TreeViewNode_HasUnrealizedChildren(object instance)
	{
		return ((TreeViewNode)instance).HasUnrealizedChildren;
	}

	private void set_101_TreeViewNode_HasUnrealizedChildren(object instance, object Value)
	{
		((TreeViewNode)instance).HasUnrealizedChildren = (bool)Value;
	}

	private object get_102_TreeViewNode_IsExpanded(object instance)
	{
		return ((TreeViewNode)instance).IsExpanded;
	}

	private void set_102_TreeViewNode_IsExpanded(object instance, object Value)
	{
		((TreeViewNode)instance).IsExpanded = (bool)Value;
	}

	private object get_103_TreeViewNode_Parent(object instance)
	{
		return ((TreeViewNode)instance).Parent;
	}

	private IXamlMember CreateXamlMember(string longMemberName)
	{
		XamlMember xamlMember = null;
		switch (longMemberName)
		{
			case "Microsoft.UI.Xaml.Controls.XamlControlsResources.UseCompactResources":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.XamlControlsResources");
				xamlMember = new XamlMember(this, "UseCompactResources", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_0_XamlControlsResources_UseCompactResources;
				xamlMember.Setter = set_0_XamlControlsResources_UseCompactResources;
				break;
			case "Richasy.WinUIKernel.Share.Converters.BoolToVisibilityConverter.IsReverse":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Converters.BoolToVisibilityConverter");
				xamlMember = new XamlMember(this, "IsReverse", "Boolean");
				xamlMember.Getter = get_1_BoolToVisibilityConverter_IsReverse;
				xamlMember.Setter = set_1_BoolToVisibilityConverter_IsReverse;
				break;
			case "Richasy.WinUIKernel.Share.Converters.ObjectToBoolConverter.IsReverse":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Converters.ObjectToBoolConverter");
				xamlMember = new XamlMember(this, "IsReverse", "Boolean");
				xamlMember.Getter = get_2_ObjectToBoolConverter_IsReverse;
				xamlMember.Setter = set_2_ObjectToBoolConverter_IsReverse;
				break;
			case "Richasy.WinUIKernel.Share.Converters.ObjectToVisibilityConverter.IsReverse":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Converters.ObjectToVisibilityConverter");
				xamlMember = new XamlMember(this, "IsReverse", "Boolean");
				xamlMember.Getter = get_3_ObjectToVisibilityConverter_IsReverse;
				xamlMember.Setter = set_3_ObjectToVisibilityConverter_IsReverse;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.IsActive":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "IsActive", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_4_ProgressRing_IsActive;
				xamlMember.Setter = set_4_ProgressRing_IsActive;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.IsIndeterminate":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "IsIndeterminate", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_5_ProgressRing_IsIndeterminate;
				xamlMember.Setter = set_5_ProgressRing_IsIndeterminate;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.Maximum":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "Maximum", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_6_ProgressRing_Maximum;
				xamlMember.Setter = set_6_ProgressRing_Maximum;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.Minimum":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "Minimum", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_7_ProgressRing_Minimum;
				xamlMember.Setter = set_7_ProgressRing_Minimum;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.TemplateSettings":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.ProgressRingTemplateSettings");
				xamlMember.Getter = get_8_ProgressRing_TemplateSettings;
				xamlMember.SetIsReadOnly();
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressRing.Value":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
				xamlMember = new XamlMember(this, "Value", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_9_ProgressRing_Value;
				xamlMember.Setter = set_9_ProgressRing_Value;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.Content":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "Content", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_10_InfoBar_Content;
				xamlMember.Setter = set_10_InfoBar_Content;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.IsOpen":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "IsOpen", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_11_InfoBar_IsOpen;
				xamlMember.Setter = set_11_InfoBar_IsOpen;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.IsClosable":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "IsClosable", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_12_InfoBar_IsClosable;
				xamlMember.Setter = set_12_InfoBar_IsClosable;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.Severity":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "Severity", "Microsoft.UI.Xaml.Controls.InfoBarSeverity");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_13_InfoBar_Severity;
				xamlMember.Setter = set_13_InfoBar_Severity;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.ActionButton":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "ActionButton", "Microsoft.UI.Xaml.Controls.Primitives.ButtonBase");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_14_InfoBar_ActionButton;
				xamlMember.Setter = set_14_InfoBar_ActionButton;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.CloseButtonCommand":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "CloseButtonCommand", "System.Windows.Input.ICommand");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_15_InfoBar_CloseButtonCommand;
				xamlMember.Setter = set_15_InfoBar_CloseButtonCommand;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.CloseButtonCommandParameter":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "CloseButtonCommandParameter", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_16_InfoBar_CloseButtonCommandParameter;
				xamlMember.Setter = set_16_InfoBar_CloseButtonCommandParameter;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.CloseButtonStyle":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "CloseButtonStyle", "Microsoft.UI.Xaml.Style");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_17_InfoBar_CloseButtonStyle;
				xamlMember.Setter = set_17_InfoBar_CloseButtonStyle;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.ContentTemplate":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "ContentTemplate", "Microsoft.UI.Xaml.DataTemplate");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_18_InfoBar_ContentTemplate;
				xamlMember.Setter = set_18_InfoBar_ContentTemplate;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.IconSource":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "IconSource", "Microsoft.UI.Xaml.Controls.IconSource");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_19_InfoBar_IconSource;
				xamlMember.Setter = set_19_InfoBar_IconSource;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.IsIconVisible":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "IsIconVisible", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_20_InfoBar_IsIconVisible;
				xamlMember.Setter = set_20_InfoBar_IsIconVisible;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.Message":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "Message", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_21_InfoBar_Message;
				xamlMember.Setter = set_21_InfoBar_Message;
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.TemplateSettings":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.InfoBarTemplateSettings");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_22_InfoBar_TemplateSettings;
				xamlMember.SetIsReadOnly();
				break;
			case "Microsoft.UI.Xaml.Controls.InfoBar.Title":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.InfoBar");
				xamlMember = new XamlMember(this, "Title", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_23_InfoBar_Title;
				xamlMember.Setter = set_23_InfoBar_Title;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.IsExpanded":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "IsExpanded", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_24_Expander_IsExpanded;
				xamlMember.Setter = set_24_Expander_IsExpanded;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.Header":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "Header", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_25_Expander_Header;
				xamlMember.Setter = set_25_Expander_Header;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.ExpandDirection":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "ExpandDirection", "Microsoft.UI.Xaml.Controls.ExpandDirection");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_26_Expander_ExpandDirection;
				xamlMember.Setter = set_26_Expander_ExpandDirection;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.HeaderTemplate":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "HeaderTemplate", "Microsoft.UI.Xaml.DataTemplate");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_27_Expander_HeaderTemplate;
				xamlMember.Setter = set_27_Expander_HeaderTemplate;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.HeaderTemplateSelector":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "HeaderTemplateSelector", "Microsoft.UI.Xaml.Controls.DataTemplateSelector");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_28_Expander_HeaderTemplateSelector;
				xamlMember.Setter = set_28_Expander_HeaderTemplateSelector;
				break;
			case "Microsoft.UI.Xaml.Controls.Expander.TemplateSettings":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Expander");
				xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.ExpanderTemplateSettings");
				xamlMember.Getter = get_29_Expander_TemplateSettings;
				xamlMember.SetIsReadOnly();
				break;
			case "WinUISample.Controls.PlayerOverlayBase.ViewModel":
				_ = (XamlUserType)GetXamlTypeByName("WinUISample.Controls.PlayerOverlayBase");
				xamlMember = new XamlMember(this, "ViewModel", "WinUISample.ViewModels.PlayerViewModel");
				xamlMember.Getter = get_30_PlayerOverlayBase_ViewModel;
				xamlMember.SetIsReadOnly();
				break;
			case "FluentIcons.WinUI.SymbolIcon.Symbol":
				_ = (XamlUserType)GetXamlTypeByName("FluentIcons.WinUI.SymbolIcon");
				xamlMember = new XamlMember(this, "Symbol", "FluentIcons.Common.Symbol");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_31_SymbolIcon_Symbol;
				xamlMember.Setter = set_31_SymbolIcon_Symbol;
				break;
			case "FluentIcons.WinUI.Internals.GenericIcon.IconVariant":
				_ = (XamlUserType)GetXamlTypeByName("FluentIcons.WinUI.Internals.GenericIcon");
				xamlMember = new XamlMember(this, "IconVariant", "FluentIcons.Common.IconVariant");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_32_GenericIcon_IconVariant;
				xamlMember.Setter = set_32_GenericIcon_IconVariant;
				break;
			case "FluentIcons.WinUI.SymbolIcon.UseSegoeMetrics":
				_ = (XamlUserType)GetXamlTypeByName("FluentIcons.WinUI.SymbolIcon");
				xamlMember = new XamlMember(this, "UseSegoeMetrics", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_33_SymbolIcon_UseSegoeMetrics;
				xamlMember.Setter = set_33_SymbolIcon_UseSegoeMetrics;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressBar.IsIndeterminate":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
				xamlMember = new XamlMember(this, "IsIndeterminate", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_34_ProgressBar_IsIndeterminate;
				xamlMember.Setter = set_34_ProgressBar_IsIndeterminate;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressBar.ShowError":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
				xamlMember = new XamlMember(this, "ShowError", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_35_ProgressBar_ShowError;
				xamlMember.Setter = set_35_ProgressBar_ShowError;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressBar.ShowPaused":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
				xamlMember = new XamlMember(this, "ShowPaused", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_36_ProgressBar_ShowPaused;
				xamlMember.Setter = set_36_ProgressBar_ShowPaused;
				break;
			case "Microsoft.UI.Xaml.Controls.ProgressBar.TemplateSettings":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
				xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.ProgressBarTemplateSettings");
				xamlMember.Getter = get_37_ProgressBar_TemplateSettings;
				xamlMember.SetIsReadOnly();
				break;
			case "Richasy.WinUIKernel.Share.Base.LayoutUserControlBase`1<WinUISample.ViewModels.AppViewModel>.ViewModel":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutUserControlBase`1<WinUISample.ViewModels.AppViewModel>");
				xamlMember = new XamlMember(this, "ViewModel", "WinUISample.ViewModels.AppViewModel");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_38_LayoutUserControlBase_ViewModel;
				xamlMember.Setter = set_38_LayoutUserControlBase_ViewModel;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.Content":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "Content", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_39_AppTitleBar_Content;
				xamlMember.Setter = set_39_AppTitleBar_Content;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.Title":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "Title", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_40_AppTitleBar_Title;
				xamlMember.Setter = set_40_AppTitleBar_Title;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.Header":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "Header", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_41_AppTitleBar_Header;
				xamlMember.Setter = set_41_AppTitleBar_Header;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.IconElement":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "IconElement", "Microsoft.UI.Xaml.Controls.IconElement");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_42_AppTitleBar_IconElement;
				xamlMember.Setter = set_42_AppTitleBar_IconElement;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.Subtitle":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "Subtitle", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_43_AppTitleBar_Subtitle;
				xamlMember.Setter = set_43_AppTitleBar_Subtitle;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.CenterContent":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "CenterContent", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_44_AppTitleBar_CenterContent;
				xamlMember.Setter = set_44_AppTitleBar_CenterContent;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.Footer":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "Footer", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_45_AppTitleBar_Footer;
				xamlMember.Setter = set_45_AppTitleBar_Footer;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.IsBackButtonVisible":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "IsBackButtonVisible", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_46_AppTitleBar_IsBackButtonVisible;
				xamlMember.Setter = set_46_AppTitleBar_IsBackButtonVisible;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.IsBackEnabled":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "IsBackEnabled", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_47_AppTitleBar_IsBackEnabled;
				xamlMember.Setter = set_47_AppTitleBar_IsBackEnabled;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.IsPaneToggleButtonVisible":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "IsPaneToggleButtonVisible", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_48_AppTitleBar_IsPaneToggleButtonVisible;
				xamlMember.Setter = set_48_AppTitleBar_IsPaneToggleButtonVisible;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.TemplateSettings":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "TemplateSettings", "Richasy.WinUIKernel.Share.Base.AppTitleBarTemplateSettings");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_49_AppTitleBar_TemplateSettings;
				xamlMember.Setter = set_49_AppTitleBar_TemplateSettings;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.BackIcon":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "BackIcon", "FluentIcons.Common.Symbol");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_50_AppTitleBar_BackIcon;
				xamlMember.Setter = set_50_AppTitleBar_BackIcon;
				break;
			case "Richasy.WinUIKernel.Share.Base.AppTitleBar.TitleMaxWidth":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.AppTitleBar");
				xamlMember = new XamlMember(this, "TitleMaxWidth", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_51_AppTitleBar_TitleMaxWidth;
				xamlMember.Setter = set_51_AppTitleBar_TitleMaxWidth;
				break;
			case "Richasy.WinUIKernel.Share.Base.TrimTextBlock.MaxLines":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.TrimTextBlock");
				xamlMember = new XamlMember(this, "MaxLines", "Int32");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_52_TrimTextBlock_MaxLines;
				xamlMember.Setter = set_52_TrimTextBlock_MaxLines;
				break;
			case "Richasy.WinUIKernel.Share.Base.TrimTextBlock.Text":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.TrimTextBlock");
				xamlMember = new XamlMember(this, "Text", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_53_TrimTextBlock_Text;
				xamlMember.Setter = set_53_TrimTextBlock_Text;
				break;
			case "Richasy.WinUIKernel.Share.Base.TrimTextBlock.IsTextSelectionEnabled":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.TrimTextBlock");
				xamlMember = new XamlMember(this, "IsTextSelectionEnabled", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_54_TrimTextBlock_IsTextSelectionEnabled;
				xamlMember.Setter = set_54_TrimTextBlock_IsTextSelectionEnabled;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Header":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Header", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_55_NumberBox_Header;
				xamlMember.Setter = set_55_NumberBox_Header;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.SpinButtonPlacementMode":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "SpinButtonPlacementMode", "Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_56_NumberBox_SpinButtonPlacementMode;
				xamlMember.Setter = set_56_NumberBox_SpinButtonPlacementMode;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.SmallChange":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "SmallChange", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_57_NumberBox_SmallChange;
				xamlMember.Setter = set_57_NumberBox_SmallChange;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.LargeChange":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "LargeChange", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_58_NumberBox_LargeChange;
				xamlMember.Setter = set_58_NumberBox_LargeChange;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Minimum":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Minimum", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_59_NumberBox_Minimum;
				xamlMember.Setter = set_59_NumberBox_Minimum;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Maximum":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Maximum", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_60_NumberBox_Maximum;
				xamlMember.Setter = set_60_NumberBox_Maximum;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.AcceptsExpression":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "AcceptsExpression", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_61_NumberBox_AcceptsExpression;
				xamlMember.Setter = set_61_NumberBox_AcceptsExpression;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.PlaceholderText":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "PlaceholderText", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_62_NumberBox_PlaceholderText;
				xamlMember.Setter = set_62_NumberBox_PlaceholderText;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Description":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Description", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_63_NumberBox_Description;
				xamlMember.Setter = set_63_NumberBox_Description;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.HeaderTemplate":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "HeaderTemplate", "Microsoft.UI.Xaml.DataTemplate");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_64_NumberBox_HeaderTemplate;
				xamlMember.Setter = set_64_NumberBox_HeaderTemplate;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.IsWrapEnabled":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "IsWrapEnabled", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_65_NumberBox_IsWrapEnabled;
				xamlMember.Setter = set_65_NumberBox_IsWrapEnabled;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.NumberFormatter":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "NumberFormatter", "Windows.Globalization.NumberFormatting.INumberFormatter2");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_66_NumberBox_NumberFormatter;
				xamlMember.Setter = set_66_NumberBox_NumberFormatter;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.PreventKeyboardDisplayOnProgrammaticFocus":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "PreventKeyboardDisplayOnProgrammaticFocus", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_67_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus;
				xamlMember.Setter = set_67_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.SelectionFlyout":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "SelectionFlyout", "Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_68_NumberBox_SelectionFlyout;
				xamlMember.Setter = set_68_NumberBox_SelectionFlyout;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.SelectionHighlightColor":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "SelectionHighlightColor", "Microsoft.UI.Xaml.Media.SolidColorBrush");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_69_NumberBox_SelectionHighlightColor;
				xamlMember.Setter = set_69_NumberBox_SelectionHighlightColor;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Text":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Text", "String");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_70_NumberBox_Text;
				xamlMember.Setter = set_70_NumberBox_Text;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.TextReadingOrder":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "TextReadingOrder", "Microsoft.UI.Xaml.TextReadingOrder");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_71_NumberBox_TextReadingOrder;
				xamlMember.Setter = set_71_NumberBox_TextReadingOrder;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.ValidationMode":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "ValidationMode", "Microsoft.UI.Xaml.Controls.NumberBoxValidationMode");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_72_NumberBox_ValidationMode;
				xamlMember.Setter = set_72_NumberBox_ValidationMode;
				break;
			case "Microsoft.UI.Xaml.Controls.NumberBox.Value":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
				xamlMember = new XamlMember(this, "Value", "Double");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_73_NumberBox_Value;
				xamlMember.Setter = set_73_NumberBox_Value;
				break;
			case "WinUIEx.WindowEx.WindowContent":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "WindowContent", "Object");
				xamlMember.Getter = get_74_WindowEx_WindowContent;
				xamlMember.Setter = set_74_WindowEx_WindowContent;
				break;
			case "WinUIEx.WindowEx.Title":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "Title", "String");
				xamlMember.Getter = get_75_WindowEx_Title;
				xamlMember.Setter = set_75_WindowEx_Title;
				break;
			case "WinUIEx.WindowEx.AppWindow":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "AppWindow", "Microsoft.UI.Windowing.AppWindow");
				xamlMember.Getter = get_76_WindowEx_AppWindow;
				xamlMember.SetIsReadOnly();
				break;
			case "WinUIEx.WindowEx.TaskBarIcon":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "TaskBarIcon", "WinUIEx.Icon");
				xamlMember.Getter = get_77_WindowEx_TaskBarIcon;
				xamlMember.Setter = set_77_WindowEx_TaskBarIcon;
				break;
			case "WinUIEx.WindowEx.PersistenceId":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "PersistenceId", "String");
				xamlMember.Getter = get_78_WindowEx_PersistenceId;
				xamlMember.Setter = set_78_WindowEx_PersistenceId;
				break;
			case "WinUIEx.WindowEx.IsTitleBarVisible":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsTitleBarVisible", "Boolean");
				xamlMember.Getter = get_79_WindowEx_IsTitleBarVisible;
				xamlMember.Setter = set_79_WindowEx_IsTitleBarVisible;
				break;
			case "WinUIEx.WindowEx.IsMinimizable":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsMinimizable", "Boolean");
				xamlMember.Getter = get_80_WindowEx_IsMinimizable;
				xamlMember.Setter = set_80_WindowEx_IsMinimizable;
				break;
			case "WinUIEx.WindowEx.IsMaximizable":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsMaximizable", "Boolean");
				xamlMember.Getter = get_81_WindowEx_IsMaximizable;
				xamlMember.Setter = set_81_WindowEx_IsMaximizable;
				break;
			case "WinUIEx.WindowEx.IsResizable":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsResizable", "Boolean");
				xamlMember.Getter = get_82_WindowEx_IsResizable;
				xamlMember.Setter = set_82_WindowEx_IsResizable;
				break;
			case "WinUIEx.WindowEx.WindowState":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "WindowState", "WinUIEx.WindowState");
				xamlMember.Getter = get_83_WindowEx_WindowState;
				xamlMember.Setter = set_83_WindowEx_WindowState;
				break;
			case "WinUIEx.WindowEx.IsShownInSwitchers":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsShownInSwitchers", "Boolean");
				xamlMember.Getter = get_84_WindowEx_IsShownInSwitchers;
				xamlMember.Setter = set_84_WindowEx_IsShownInSwitchers;
				break;
			case "WinUIEx.WindowEx.IsAlwaysOnTop":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "IsAlwaysOnTop", "Boolean");
				xamlMember.Getter = get_85_WindowEx_IsAlwaysOnTop;
				xamlMember.Setter = set_85_WindowEx_IsAlwaysOnTop;
				break;
			case "WinUIEx.WindowEx.Presenter":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "Presenter", "Microsoft.UI.Windowing.AppWindowPresenter");
				xamlMember.Getter = get_86_WindowEx_Presenter;
				xamlMember.SetIsReadOnly();
				break;
			case "WinUIEx.WindowEx.PresenterKind":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "PresenterKind", "Microsoft.UI.Windowing.AppWindowPresenterKind");
				xamlMember.Getter = get_87_WindowEx_PresenterKind;
				xamlMember.Setter = set_87_WindowEx_PresenterKind;
				break;
			case "WinUIEx.WindowEx.Width":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "Width", "Double");
				xamlMember.Getter = get_88_WindowEx_Width;
				xamlMember.Setter = set_88_WindowEx_Width;
				break;
			case "WinUIEx.WindowEx.Height":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "Height", "Double");
				xamlMember.Getter = get_89_WindowEx_Height;
				xamlMember.Setter = set_89_WindowEx_Height;
				break;
			case "WinUIEx.WindowEx.MinWidth":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "MinWidth", "Double");
				xamlMember.Getter = get_90_WindowEx_MinWidth;
				xamlMember.Setter = set_90_WindowEx_MinWidth;
				break;
			case "WinUIEx.WindowEx.MinHeight":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "MinHeight", "Double");
				xamlMember.Getter = get_91_WindowEx_MinHeight;
				xamlMember.Setter = set_91_WindowEx_MinHeight;
				break;
			case "WinUIEx.WindowEx.MaxWidth":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "MaxWidth", "Double");
				xamlMember.Getter = get_92_WindowEx_MaxWidth;
				xamlMember.Setter = set_92_WindowEx_MaxWidth;
				break;
			case "WinUIEx.WindowEx.MaxHeight":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "MaxHeight", "Double");
				xamlMember.Getter = get_93_WindowEx_MaxHeight;
				xamlMember.Setter = set_93_WindowEx_MaxHeight;
				break;
			case "WinUIEx.WindowEx.Backdrop":
				_ = (XamlUserType)GetXamlTypeByName("WinUIEx.WindowEx");
				xamlMember = new XamlMember(this, "Backdrop", "WinUIEx.SystemBackdrop");
				xamlMember.Getter = get_94_WindowEx_Backdrop;
				xamlMember.Setter = set_94_WindowEx_Backdrop;
				break;
			case "WinUISample.MainWindow.AppVM":
				_ = (XamlUserType)GetXamlTypeByName("WinUISample.MainWindow");
				xamlMember = new XamlMember(this, "AppVM", "WinUISample.ViewModels.AppViewModel");
				xamlMember.Getter = get_95_MainWindow_AppVM;
				xamlMember.SetIsReadOnly();
				break;
			case "Richasy.WinUIKernel.Share.Base.LayoutPageBase`1<WinUISample.ViewModels.LocalVideoPageViewModel>.ViewModel":
				_ = (XamlUserType)GetXamlTypeByName("Richasy.WinUIKernel.Share.Base.LayoutPageBase`1<WinUISample.ViewModels.LocalVideoPageViewModel>");
				xamlMember = new XamlMember(this, "ViewModel", "WinUISample.ViewModels.LocalVideoPageViewModel");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_96_LayoutPageBase_ViewModel;
				xamlMember.Setter = set_96_LayoutPageBase_ViewModel;
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.Children":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "Children", "System.Collections.Generic.IList`1<Microsoft.UI.Xaml.Controls.TreeViewNode>");
				xamlMember.Getter = get_97_TreeViewNode_Children;
				xamlMember.SetIsReadOnly();
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.Content":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "Content", "Object");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_98_TreeViewNode_Content;
				xamlMember.Setter = set_98_TreeViewNode_Content;
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.Depth":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "Depth", "Int32");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_99_TreeViewNode_Depth;
				xamlMember.SetIsReadOnly();
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.HasChildren":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "HasChildren", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_100_TreeViewNode_HasChildren;
				xamlMember.SetIsReadOnly();
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.HasUnrealizedChildren":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "HasUnrealizedChildren", "Boolean");
				xamlMember.Getter = get_101_TreeViewNode_HasUnrealizedChildren;
				xamlMember.Setter = set_101_TreeViewNode_HasUnrealizedChildren;
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.IsExpanded":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "IsExpanded", "Boolean");
				xamlMember.SetIsDependencyProperty();
				xamlMember.Getter = get_102_TreeViewNode_IsExpanded;
				xamlMember.Setter = set_102_TreeViewNode_IsExpanded;
				break;
			case "Microsoft.UI.Xaml.Controls.TreeViewNode.Parent":
				_ = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember = new XamlMember(this, "Parent", "Microsoft.UI.Xaml.Controls.TreeViewNode");
				xamlMember.Getter = get_103_TreeViewNode_Parent;
				xamlMember.SetIsReadOnly();
				break;
		}
		return xamlMember;
	}
}

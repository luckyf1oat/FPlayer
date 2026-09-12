using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;

namespace WinUISample.Models;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(WinUISample_Models_DanmakuSearchApiGroupWinRTTypeDetails))]
public sealed class DanmakuSearchApiGroup : ObservableObject
{
	[CompilerGenerated]
	private IReadOnlyList<DanmakuSearchAnime> _003CAnimes_003Ek__BackingField = Array.Empty<DanmakuSearchAnime>();

	[CompilerGenerated]
	private bool _003CIsLoading_003Ek__BackingField;

	[CompilerGenerated]
	private string? _003CErrorMessage_003Ek__BackingField;

	[CompilerGenerated]
	private bool _003CIsExpanded_003Ek__BackingField;

	public required DanmakuApiEntry Api { get; init; }

	public required string ApiBase { get; init; }

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<DanmakuSearchAnime> Animes
	{
		get
		{
			return _003CAnimes_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<IReadOnlyList<DanmakuSearchAnime>>.Default.Equals(_003CAnimes_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.Animes);
				_003CAnimes_003Ek__BackingField = value;
				OnAnimesChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.Animes);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoading
	{
		get
		{
			return _003CIsLoading_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsLoading_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsLoading);
				_003CIsLoading_003Ek__BackingField = value;
				OnIsLoadingChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsLoading);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? ErrorMessage
	{
		get
		{
			return _003CErrorMessage_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CErrorMessage_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.ErrorMessage);
				_003CErrorMessage_003Ek__BackingField = value;
				OnErrorMessageChanged(value);
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.ErrorMessage);
			}
		}
	}

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsExpanded
	{
		get
		{
			return _003CIsExpanded_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_003CIsExpanded_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.IsExpanded);
				_003CIsExpanded_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.IsExpanded);
			}
		}
	}

	public string Header
	{
		get
		{
			if (IsLoading)
			{
				return Api.DisplayName + "（搜索中…）";
			}
			if (!string.IsNullOrEmpty(ErrorMessage))
			{
				return Api.DisplayName + "（失败：" + ErrorMessage + "）";
			}
			if (Animes.Count != 0)
			{
				return $"{Api.DisplayName}（{Animes.Count}）";
			}
			return Api.DisplayName + "（无结果）";
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAnimesChanged(IReadOnlyList<DanmakuSearchAnime> value)
	{
		OnPropertyChanged("Header");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsLoadingChanged(bool value)
	{
		OnPropertyChanged("Header");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnErrorMessageChanged(string? value)
	{
		OnPropertyChanged("Header");
	}
}

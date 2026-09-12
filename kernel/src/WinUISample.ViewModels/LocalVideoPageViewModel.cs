using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.Storage;
using WinRT;
using WinRT.AIPlayer_MpvHostVtableClasses;

namespace WinUISample.ViewModels;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(WinUISample_Models_DanmakuSearchApiGroupWinRTTypeDetails))]
public sealed class LocalVideoPageViewModel : ViewModelBase
{
	private readonly ISettingsToolkit _settingsToolkit;

	private readonly IFileToolkit _fileToolkit;

	[CompilerGenerated]
	private string? _003CLastVideoPath_003Ek__BackingField;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? openLocalFileCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? reopenLastFileCommand;

	[ObservableProperty]
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? LastVideoPath
	{
		get
		{
			return _003CLastVideoPath_003Ek__BackingField;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_003CLastVideoPath_003Ek__BackingField, value))
			{
				OnPropertyChanging(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.LastVideoPath);
				_003CLastVideoPath_003Ek__BackingField = value;
				OnPropertyChanged(CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.LastVideoPath);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand OpenLocalFileCommand => openLocalFileCommand ?? (openLocalFileCommand = new AsyncRelayCommand(OpenLocalFileAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ReopenLastFileCommand => reopenLastFileCommand ?? (reopenLastFileCommand = new AsyncRelayCommand(ReopenLastFileAsync));

	public LocalVideoPageViewModel(ISettingsToolkit settingsToolkit, IFileToolkit fileToolkit)
	{
		_settingsToolkit = settingsToolkit;
		_fileToolkit = fileToolkit;
		LastVideoPath = settingsToolkit.ReadLocalSetting("LastLocalVideoPath", string.Empty);
	}

	[RelayCommand]
	private async Task OpenLocalFileAsync()
	{
		StorageFile storageFile = await _fileToolkit.PickFileAsync(".mp4,.mkv,.avi,.mov,.rmvb,.wmv", this.Get<AppViewModel>().MainWindow);
		if (storageFile != null)
		{
			LastVideoPath = storageFile.Path;
			_settingsToolkit.WriteLocalSetting("LastLocalVideoPath", storageFile.Path);
			await this.Get<AppViewModel>().OpenVideoAsync(storageFile.Path);
		}
	}

	[RelayCommand]
	private async Task ReopenLastFileAsync()
	{
		if (string.IsNullOrEmpty(LastVideoPath) || !File.Exists(LastVideoPath))
		{
			if (await new ContentDialog
			{
				Title = "提示",
				Content = "没有找到上次播放的视频文件，是否重新选择？",
				PrimaryButtonText = "重新选择",
				CloseButtonText = "取消",
				XamlRoot = this.Get<AppViewModel>().ActivateXamlRoot
			}.ShowAsync() == ContentDialogResult.Primary)
			{
				await OpenLocalFileAsync();
			}
		}
		else
		{
			await this.Get<AppViewModel>().OpenVideoAsync(LastVideoPath);
		}
	}
}

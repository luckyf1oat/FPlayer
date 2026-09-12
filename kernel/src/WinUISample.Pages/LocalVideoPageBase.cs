using Richasy.WinUIKernel.Share.Base;
using WinUISample.ViewModels;

namespace WinUISample.Pages;

public abstract class LocalVideoPageBase : LayoutPageBase<LocalVideoPageViewModel>
{
	protected LocalVideoPageBase()
	{
		base.ViewModel = this.Get<LocalVideoPageViewModel>();
	}
}

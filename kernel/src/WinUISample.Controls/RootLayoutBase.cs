using Richasy.WinUIKernel.Share.Base;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

public abstract class RootLayoutBase : LayoutUserControlBase<AppViewModel>
{
	protected RootLayoutBase()
	{
		base.ViewModel = this.Get<AppViewModel>();
	}
}

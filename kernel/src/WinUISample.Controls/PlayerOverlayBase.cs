using Microsoft.UI.Xaml.Controls;
using WinUISample.ViewModels;

namespace WinUISample.Controls;

public abstract class PlayerOverlayBase : UserControl
{
	public PlayerViewModel ViewModel { get; protected set; }
}

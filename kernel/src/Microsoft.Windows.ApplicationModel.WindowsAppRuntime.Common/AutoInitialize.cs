using System.Runtime.CompilerServices;

namespace Microsoft.Windows.ApplicationModel.WindowsAppRuntime.Common;

internal class AutoInitialize
{
	[ModuleInitializer]
	internal static void InitializeWindowsAppSDK()
	{
		Microsoft.Windows.Foundation.UndockedRegFreeWinRTCS.AutoInitialize.AccessWindowsAppSDK();
	}
}

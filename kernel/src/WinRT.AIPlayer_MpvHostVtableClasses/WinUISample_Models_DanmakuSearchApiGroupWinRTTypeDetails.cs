using System.Runtime.InteropServices;
using ABI.System.ComponentModel;

namespace WinRT.AIPlayer_MpvHostVtableClasses;

internal sealed class WinUISample_Models_DanmakuSearchApiGroupWinRTTypeDetails : IWinRTExposedTypeDetails
{
	public ComWrappers.ComInterfaceEntry[] GetExposedInterfaces()
	{
		return new ComWrappers.ComInterfaceEntry[1]
		{
			new ComWrappers.ComInterfaceEntry
			{
				IID = INotifyPropertyChangedMethods.IID,
				Vtable = INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
			}
		};
	}
}

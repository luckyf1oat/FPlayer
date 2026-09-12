using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32;

[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal static class PInvoke
{
	[SupportedOSPlatform("windows8.1")]
	[OverloadResolutionPriority(1)]
	internal unsafe static Windows.Win32.Foundation.HRESULT GetDpiForMonitor(Windows.Win32.Graphics.Gdi.HMONITOR hmonitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY)
	{
		fixed (uint* dpiY2 = &dpiY)
		{
			fixed (uint* dpiX2 = &dpiX)
			{
				return GetDpiForMonitor(hmonitor, dpiType, dpiX2, dpiY2);
			}
		}
	}

	[DllImport("api-ms-win-shcore-scaling-l1-1-1.dll", ExactSpelling = true)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	[SupportedOSPlatform("windows8.1")]
	internal unsafe static extern Windows.Win32.Foundation.HRESULT GetDpiForMonitor(Windows.Win32.Graphics.Gdi.HMONITOR hmonitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE dpiType, uint* dpiX, uint* dpiY);

	[DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	[SupportedOSPlatform("windows5.0")]
	internal static extern Windows.Win32.Foundation.BOOL CloseHandle(Windows.Win32.Foundation.HANDLE hObject);

	[DllImport("USER32.dll", ExactSpelling = true)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	[SupportedOSPlatform("windows10.0.14393")]
	internal static extern uint GetDpiForWindow(Windows.Win32.Foundation.HWND hwnd);

	[DllImport("USER32.dll", ExactSpelling = true)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	[SupportedOSPlatform("windows5.0")]
	internal static extern Windows.Win32.Foundation.BOOL IsZoomed(Windows.Win32.Foundation.HWND hWnd);
}

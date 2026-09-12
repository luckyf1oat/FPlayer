using System;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace Windows.Win32.Foundation;

[DebuggerDisplay("{Value}")]
[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal readonly struct HWND : IEquatable<Windows.Win32.Foundation.HWND>
{
	internal unsafe readonly void* Value;

	internal static Windows.Win32.Foundation.HWND Null => default(Windows.Win32.Foundation.HWND);

	internal unsafe bool IsNull => Value == null;

	internal unsafe HWND(void* value)
	{
		Value = value;
	}

	internal unsafe HWND(nint value)
		: this((void*)value)
	{
	}

	public unsafe static implicit operator void*(Windows.Win32.Foundation.HWND value)
	{
		return value.Value;
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HWND(void* value)
	{
		return new Windows.Win32.Foundation.HWND(value);
	}

	public unsafe static bool operator ==(Windows.Win32.Foundation.HWND left, Windows.Win32.Foundation.HWND right)
	{
		return left.Value == right.Value;
	}

	public static bool operator !=(Windows.Win32.Foundation.HWND left, Windows.Win32.Foundation.HWND right)
	{
		return !(left == right);
	}

	public unsafe bool Equals(Windows.Win32.Foundation.HWND other)
	{
		return Value == other.Value;
	}

	public override bool Equals(object obj)
	{
		if (obj is Windows.Win32.Foundation.HWND other)
		{
			return Equals(other);
		}
		return false;
	}

	public unsafe override int GetHashCode()
	{
		return (int)Value;
	}

	public unsafe override string ToString()
	{
		return $"0x{(nuint)Value:x}";
	}

	public unsafe static implicit operator nint(Windows.Win32.Foundation.HWND value)
	{
		return new IntPtr(value.Value);
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HWND(nint value)
	{
		return new Windows.Win32.Foundation.HWND(((IntPtr)value).ToPointer());
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HWND(nuint value)
	{
		return new Windows.Win32.Foundation.HWND(((UIntPtr)value).ToPointer());
	}

	public unsafe static implicit operator Windows.Win32.Foundation.HANDLE(Windows.Win32.Foundation.HWND value)
	{
		return new Windows.Win32.Foundation.HANDLE(value.Value);
	}
}

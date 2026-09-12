using System;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace Windows.Win32.Graphics.Gdi;

[DebuggerDisplay("{Value}")]
[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal readonly struct HMONITOR : IEquatable<Windows.Win32.Graphics.Gdi.HMONITOR>
{
	internal unsafe readonly void* Value;

	internal static Windows.Win32.Graphics.Gdi.HMONITOR Null => default(Windows.Win32.Graphics.Gdi.HMONITOR);

	internal unsafe bool IsNull => Value == null;

	internal unsafe HMONITOR(void* value)
	{
		Value = value;
	}

	internal unsafe HMONITOR(nint value)
		: this((void*)value)
	{
	}

	public unsafe static implicit operator void*(Windows.Win32.Graphics.Gdi.HMONITOR value)
	{
		return value.Value;
	}

	public unsafe static explicit operator Windows.Win32.Graphics.Gdi.HMONITOR(void* value)
	{
		return new Windows.Win32.Graphics.Gdi.HMONITOR(value);
	}

	public unsafe static bool operator ==(Windows.Win32.Graphics.Gdi.HMONITOR left, Windows.Win32.Graphics.Gdi.HMONITOR right)
	{
		return left.Value == right.Value;
	}

	public static bool operator !=(Windows.Win32.Graphics.Gdi.HMONITOR left, Windows.Win32.Graphics.Gdi.HMONITOR right)
	{
		return !(left == right);
	}

	public unsafe bool Equals(Windows.Win32.Graphics.Gdi.HMONITOR other)
	{
		return Value == other.Value;
	}

	public override bool Equals(object obj)
	{
		if (obj is Windows.Win32.Graphics.Gdi.HMONITOR other)
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

	public unsafe static implicit operator nint(Windows.Win32.Graphics.Gdi.HMONITOR value)
	{
		return new IntPtr(value.Value);
	}

	public unsafe static explicit operator Windows.Win32.Graphics.Gdi.HMONITOR(nint value)
	{
		return new Windows.Win32.Graphics.Gdi.HMONITOR(((IntPtr)value).ToPointer());
	}

	public unsafe static explicit operator Windows.Win32.Graphics.Gdi.HMONITOR(nuint value)
	{
		return new Windows.Win32.Graphics.Gdi.HMONITOR(((UIntPtr)value).ToPointer());
	}
}

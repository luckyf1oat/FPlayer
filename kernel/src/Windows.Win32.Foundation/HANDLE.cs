using System;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace Windows.Win32.Foundation;

[DebuggerDisplay("{Value}")]
[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal readonly struct HANDLE : IEquatable<Windows.Win32.Foundation.HANDLE>
{
	internal unsafe readonly void* Value;

	internal static Windows.Win32.Foundation.HANDLE Null => default(Windows.Win32.Foundation.HANDLE);

	internal unsafe bool IsNull => Value == null;

	internal unsafe HANDLE(void* value)
	{
		Value = value;
	}

	internal unsafe HANDLE(nint value)
		: this((void*)value)
	{
	}

	public unsafe static implicit operator void*(Windows.Win32.Foundation.HANDLE value)
	{
		return value.Value;
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HANDLE(void* value)
	{
		return new Windows.Win32.Foundation.HANDLE(value);
	}

	public unsafe static bool operator ==(Windows.Win32.Foundation.HANDLE left, Windows.Win32.Foundation.HANDLE right)
	{
		return left.Value == right.Value;
	}

	public static bool operator !=(Windows.Win32.Foundation.HANDLE left, Windows.Win32.Foundation.HANDLE right)
	{
		return !(left == right);
	}

	public unsafe bool Equals(Windows.Win32.Foundation.HANDLE other)
	{
		return Value == other.Value;
	}

	public override bool Equals(object obj)
	{
		if (obj is Windows.Win32.Foundation.HANDLE other)
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

	public unsafe static implicit operator nint(Windows.Win32.Foundation.HANDLE value)
	{
		return new IntPtr(value.Value);
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HANDLE(nint value)
	{
		return new Windows.Win32.Foundation.HANDLE(((IntPtr)value).ToPointer());
	}

	public unsafe static explicit operator Windows.Win32.Foundation.HANDLE(nuint value)
	{
		return new Windows.Win32.Foundation.HANDLE(((UIntPtr)value).ToPointer());
	}
}

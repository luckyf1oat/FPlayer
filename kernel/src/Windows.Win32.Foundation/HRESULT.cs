using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Windows.Win32.Foundation;

[DebuggerDisplay("{Value}")]
[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal readonly struct HRESULT : IEquatable<Windows.Win32.Foundation.HRESULT>
{
	internal readonly int Value;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	internal bool Succeeded => Value >= 0;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	internal bool Failed => Value < 0;

	internal HRESULT(int value)
	{
		Value = value;
	}

	public static implicit operator int(Windows.Win32.Foundation.HRESULT value)
	{
		return value.Value;
	}

	public static explicit operator Windows.Win32.Foundation.HRESULT(int value)
	{
		return new Windows.Win32.Foundation.HRESULT(value);
	}

	public static bool operator ==(Windows.Win32.Foundation.HRESULT left, Windows.Win32.Foundation.HRESULT right)
	{
		return left.Value == right.Value;
	}

	public static bool operator !=(Windows.Win32.Foundation.HRESULT left, Windows.Win32.Foundation.HRESULT right)
	{
		return !(left == right);
	}

	public bool Equals(Windows.Win32.Foundation.HRESULT other)
	{
		return Value == other.Value;
	}

	public override bool Equals(object obj)
	{
		if (obj is Windows.Win32.Foundation.HRESULT other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Value.GetHashCode();
	}

	public override string ToString()
	{
		return string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", Value);
	}

	public static implicit operator uint(Windows.Win32.Foundation.HRESULT value)
	{
		return (uint)value.Value;
	}

	public static explicit operator Windows.Win32.Foundation.HRESULT(uint value)
	{
		return new Windows.Win32.Foundation.HRESULT((int)value);
	}

	internal Windows.Win32.Foundation.HRESULT ThrowOnFailure(nint errorInfo = 0)
	{
		Marshal.ThrowExceptionForHR(Value, errorInfo);
		return this;
	}

	internal string ToString(string format, IFormatProvider formatProvider)
	{
		uint value = (uint)Value;
		return value.ToString(format, formatProvider);
	}
}

using System;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace Windows.Win32.Foundation;

[DebuggerDisplay("{Value}")]
[GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
internal readonly struct BOOL : IEquatable<Windows.Win32.Foundation.BOOL>
{
	internal readonly int Value;

	internal BOOL(int value)
	{
		Value = value;
	}

	public static implicit operator int(Windows.Win32.Foundation.BOOL value)
	{
		return value.Value;
	}

	public static explicit operator Windows.Win32.Foundation.BOOL(int value)
	{
		return new Windows.Win32.Foundation.BOOL(value);
	}

	public static bool operator ==(Windows.Win32.Foundation.BOOL left, Windows.Win32.Foundation.BOOL right)
	{
		return left.Value == right.Value;
	}

	public static bool operator !=(Windows.Win32.Foundation.BOOL left, Windows.Win32.Foundation.BOOL right)
	{
		return !(left == right);
	}

	public bool Equals(Windows.Win32.Foundation.BOOL other)
	{
		return Value == other.Value;
	}

	public override bool Equals(object obj)
	{
		if (obj is Windows.Win32.Foundation.BOOL other)
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
		return $"0x{Value:x}";
	}

	internal BOOL(bool value)
	{
		Value = (value ? 1 : 0);
	}

	public static implicit operator bool(Windows.Win32.Foundation.BOOL value)
	{
		return value.Value != 0;
	}

	public static implicit operator Windows.Win32.Foundation.BOOL(bool value)
	{
		return new Windows.Win32.Foundation.BOOL(value);
	}
}

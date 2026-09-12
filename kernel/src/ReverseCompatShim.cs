// ============================================================================
// ⚠ 逆向补充文件 —— 不是原始源码！
//
// 用途：让「反编译源码 + 重建工程」能够编译通过。
//
// 背景：
//   1. 反编译产物中已经包含 CsWinRT 源生成器的全部输出（ABI 投影类型等），
//      因此必须禁用源生成器，否则出现 CS0111 / CS0102 / CS0101 重复定义；
//   2. 但禁用后 IAsyncOperation<TResult>.GetAwaiter 扩展方法随之不可见，
//      报 CS4036（全项目仅 1 处调用：
//      WinUISample.ViewModels.LocalVideoPageViewModel.cs:88 的
//      `await dialog.ShowAsync()`）；
//   3. 原始工程中该扩展方法由 CsWinRT 投影程序集 Microsoft.Windows.SDK.NET.dll
//      提供，本文件以扩展方法形式补回，语义与原实现一致。
//
// 该文件由本次逆向工程新增，原程序集中不存在。
// ============================================================================
using System;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace WinUISample;

internal static class ReverseCompatShim
{
	public static TaskAwaiter<TResult> GetAwaiter<TResult>(this IAsyncOperation<TResult> source)
	{
		return source.AsTask().GetAwaiter();
	}

	public static TaskAwaiter GetAwaiter(this IAsyncAction source)
	{
		return source.AsTask().GetAwaiter();
	}
}

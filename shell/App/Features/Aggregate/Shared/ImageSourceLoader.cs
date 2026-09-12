// t31 共用的小工具（两个屏都用；只在 `Features/Aggregate/` 内）。
//
// 为什么单独开：封面异步加载的 API 形态（WinRT 流 → BitmapImage）是**唯一容易写错又只在运行时才暴露**
// 的一处；集中一处实现 + 一处失败兜底，避免两个屏各写一版、行为不一致。

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AIPlayer.Shell.Features.Aggregate.Shared;

/// <summary>把已取到的图片字节变成可绑定的 <see cref="BitmapImage"/>。</summary>
public static class ImageSourceLoader
{
    /// <summary>失败返回 <c>null</c>（调用方保持占位），**不抛异常**。</summary>
    public static async Task<BitmapImage> LoadAsync(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is InvalidCastException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // 字节不是可用位图 / 流形态不被 WinRT 接受 ⇒ 返回 null 让调用方保持占位。
            // **不吞**：调用方（`AggregatePoster.EnsureImageAsync`）会记 `poster-image-decode-null` 留证据。
            return null;
        }
    }
}

using System.Collections.Generic;
using Richasy.Danmaku.Models;

namespace WinUISample.Services;

internal sealed record DanmakuFetchAttempt(IReadOnlyList<DanmakuItem> Items, string? ErrorMessage)
{
	public bool HasItems => Items.Count > 0;
}

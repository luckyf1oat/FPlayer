namespace WinUISample.Services;

internal sealed record DanmakuMatchResult(string EpisodeTitle, string CommentUrl, string? AnimeTitle = null)
{
	public string DisplayTitle
	{
		get
		{
			string text = AnimeTitle?.Trim() ?? string.Empty;
			string text2 = EpisodeTitle?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(text))
			{
				return text2;
			}
			if (string.IsNullOrEmpty(text2))
			{
				return text;
			}
			return text + " · " + text2;
		}
	}
}

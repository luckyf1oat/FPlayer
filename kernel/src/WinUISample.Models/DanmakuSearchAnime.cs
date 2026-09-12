using System.Collections.Generic;

namespace WinUISample.Models;

public sealed record DanmakuSearchAnime(int AnimeId, string AnimeTitle, IReadOnlyList<DanmakuSearchEpisode> Episodes);

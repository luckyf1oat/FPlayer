using System;
using System.Collections.Generic;
using System.Linq;

namespace WinUISample.Models;

internal sealed class DanmakuApiSelectionState
{
	public List<DanmakuSelectionCandidate> Candidates { get; } = new List<DanmakuSelectionCandidate>();

	public string? ActiveCommentUrl { get; set; }

	public DanmakuSelectionCandidate? ActiveCandidate => Candidates.FirstOrDefault((DanmakuSelectionCandidate c) => string.Equals(c.CommentUrl, ActiveCommentUrl, StringComparison.Ordinal));
}

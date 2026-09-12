using System;
using System.Collections.Generic;

namespace WinUISample.Services;

internal sealed record DanmakuMatchAttempt(IReadOnlyList<DanmakuMatchResult> Results, string? ErrorMessage)
{
	public bool IsSuccess => Results.Count > 0;

	public DanmakuMatchResult? Result
	{
		get
		{
			if (Results.Count <= 0)
			{
				return null;
			}
			return Results[0];
		}
	}

	public static DanmakuMatchAttempt Success(IReadOnlyList<DanmakuMatchResult> results)
	{
		return new DanmakuMatchAttempt(results, null);
	}

	public static DanmakuMatchAttempt Failure(string message)
	{
		return new DanmakuMatchAttempt(Array.Empty<DanmakuMatchResult>(), message);
	}
}

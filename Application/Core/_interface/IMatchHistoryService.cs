using Checkers.Shared.DTOs;

namespace Checkers.Application.Core.Services
{
	public interface IMatchHistoryService
	{
		/// <summary>
		/// The user's record and every match they played, newest first.
		/// </summary>
		Task<MatchHistoryDto> GetHistoryAsync(int userId);
		/// <summary>
		/// One of the user's matches with every move, or null if it doesn't exist or they didn't play in it.
		/// </summary>
		Task<MatchReplayDto?> GetMatchAsync(int userId, int matchId);
		/// <summary>
		/// Saves a finished match. <paramref name="callerUserId"/> must be one of its players.
		/// </summary>
		Task<RecordMatchResult> RecordMatchAsync(int callerUserId, RecordMatchRequestDto request);
	}
}

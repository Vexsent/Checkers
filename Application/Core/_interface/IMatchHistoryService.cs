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
		/// Saves a finished match. <paramref name="callerUserId"/> must be one of its players.
		/// </summary>
		Task<RecordMatchResult> RecordMatchAsync(int callerUserId, RecordMatchRequestDto request);
	}
}

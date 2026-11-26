using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public interface IMatchHistoryServices
	{
		/// <summary>
		/// The signed-in user's match history, or null if the Server couldn't be reached or refused the request.
		/// </summary>
		Task<MatchHistoryDto?> GetHistoryAsync();
		/// <summary>
		/// One of the signed-in user's matches with every move, or null if it doesn't exist, isn't
		/// theirs, or the Server couldn't be reached.
		/// </summary>
		Task<MatchReplayDto?> GetMatchAsync(int matchId);
		/// <summary>
		/// The signed-in user's unfinished live matches, or null if the Server couldn't be reached.
		/// </summary>
		Task<List<ActiveMatchDto>?> GetActiveMatchesAsync();
		/// <summary>
		/// Resigns a live match without rejoining it. Returns an error message, or null on success.
		/// </summary>
		Task<string?> ResignActiveMatchAsync(string matchCode);
	}
}

using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public interface IMatchHistoryServices
	{
		/// <summary>
		/// The signed-in user's match history, or null if the Server couldn't be reached or refused the request.
		/// </summary>
		Task<MatchHistoryDto?> GetHistoryAsync();
	}
}

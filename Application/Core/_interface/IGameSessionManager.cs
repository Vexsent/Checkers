using Checkers.Shared.DTOs;
using Checkers.Shared.GameModels;

namespace Checkers.Application.Core.Services
{
	public interface IGameSessionManager
	{
		/// <summary>
		/// Starts a match with the creator as black and returns its shareable code.
		/// </summary>
		SessionResult Create(int userId, string username);
		/// <summary>
		/// Takes the open seat, or returns the current state if the user is already a player.
		/// </summary>
		SessionResult Join(string matchCode, int userId, string username);
		SessionResult MakeMove(string matchCode, int userId, GameMove move);
		SessionResult Resign(string matchCode, int userId);
		/// <summary>
		/// Codes of the unfinished matches the user is playing in.
		/// </summary>
		IReadOnlyList<string> GetActiveMatchCodes(int userId);
		/// <summary>
		/// True the first time it's called for a finished match and false after that, so a match
		/// that both players finish at once is still saved only once.
		/// </summary>
		bool TryClaimRecording(string matchCode);
	}
}

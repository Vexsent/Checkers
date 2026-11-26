using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// Turns a finished live match into a match record (User Story 12). The server saves it, not
	/// the clients, so a game is saved once no matter which player's move or resignation ended it.
	/// </summary>
	public class FinishedMatchRecorder(
		IGameSessionManager sessions,
		IMatchHistoryService matchHistory) : IFinishedMatchRecorder
	{
		public async Task<RecordMatchResult?> RecordIfFinishedAsync(GameSessionDto session)
		{
			if (session.Status != SessionStatus.Finished
				|| session.WhitePlayerId is not int whitePlayerId
				|| session.StartedAtUtc is not DateTime startedAtUtc
				|| session.EndedAtUtc is not DateTime endedAtUtc
				|| ToMatchResult(session.Outcome) is not MatchResult result)
				return null;

			if (!sessions.TryClaimRecording(session.MatchCode))
				return null;

			var request = new RecordMatchRequestDto
			{
				BlackPlayerId = session.BlackPlayerId,
				WhitePlayerId = whitePlayerId,
				Result = result,
				StartedAtUtc = startedAtUtc,
				EndedAtUtc = endedAtUtc,
				Moves = session.Moves.Select(m => new MatchMoveDto
				{
					FromRow = m.FromRow,
					FromCol = m.FromCol,
					ToRow = m.ToRow,
					ToCol = m.ToCol,
				}).ToList(),
			};

			return await matchHistory.RecordMatchAsync(session.BlackPlayerId, request);
		}

		private static MatchResult? ToMatchResult(GameOutcome outcome) => outcome switch
		{
			GameOutcome.BlackWon => MatchResult.BlackWin,
			GameOutcome.WhiteWon => MatchResult.WhiteWin,
			GameOutcome.Draw => MatchResult.Draw,
			_ => null,
		};
	}
}

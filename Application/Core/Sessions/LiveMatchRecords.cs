using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// Turns a finished live match into the request that saves it to match history.
	/// </summary>
	public static class LiveMatchRecords
	{
		public static RecordMatchRequestDto ToRecordRequest(GameSessionDto session)
		{
			if (session.Status != SessionStatus.Finished)
				throw new ArgumentException("Only finished matches can be recorded.", nameof(session));

			var endedAt = session.EndedAtUtc ?? DateTime.UtcNow;

			return new RecordMatchRequestDto
			{
				BlackPlayerId = session.BlackPlayerId,
				WhitePlayerId = session.WhitePlayerId,
				Result = session.Outcome switch
				{
					GameOutcome.BlackWon => MatchResult.BlackWin,
					GameOutcome.WhiteWon => MatchResult.WhiteWin,
					GameOutcome.Draw => MatchResult.Draw,
					_ => throw new ArgumentException("A finished match must have an outcome.", nameof(session)),
				},
				StartedAtUtc = session.StartedAtUtc ?? endedAt,
				EndedAtUtc = endedAt,
				Moves = session.Moves
					.Select(move => new MatchMoveDto
					{
						FromRow = move.FromRow,
						FromCol = move.FromCol,
						ToRow = move.ToRow,
						ToCol = move.ToCol,
					})
					.ToList(),
			};
		}
	}
}

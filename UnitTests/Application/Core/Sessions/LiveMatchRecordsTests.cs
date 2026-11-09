using Checkers.Application.Core.Sessions;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Xunit;

namespace UnitTests.Application.Core.Sessions
{
	public class LiveMatchRecordsTests
	{
		private static readonly DateTime Started = new(2026, 11, 8, 18, 0, 0, DateTimeKind.Utc);
		private static readonly DateTime Ended = Started.AddMinutes(12);

		[Theory]
		[InlineData(GameOutcome.BlackWon, MatchResult.BlackWin)]
		[InlineData(GameOutcome.WhiteWon, MatchResult.WhiteWin)]
		[InlineData(GameOutcome.Draw, MatchResult.Draw)]
		public void ToRecordRequest_MapsOutcome(GameOutcome outcome, MatchResult expected)
		{
			Assert.Equal(expected, LiveMatchRecords.ToRecordRequest(Finished(outcome)).Result);
		}

		[Fact]
		public void ToRecordRequest_CopiesPlayersTimesAndMovesInOrder()
		{
			var request = LiveMatchRecords.ToRecordRequest(Finished(GameOutcome.WhiteWon));

			Assert.Equal(1, request.BlackPlayerId);
			Assert.Equal(2, request.WhitePlayerId);
			Assert.Equal(Started, request.StartedAtUtc);
			Assert.Equal(Ended, request.EndedAtUtc);
			Assert.Collection(request.Moves,
				move => Assert.Equal((2, 1, 3, 0), (move.FromRow, move.FromCol, move.ToRow, move.ToCol)),
				move => Assert.Equal((5, 0, 4, 1), (move.FromRow, move.FromCol, move.ToRow, move.ToCol)));
		}

		[Fact]
		public void ToRecordRequest_UnfinishedMatch_Throws()
		{
			var session = Finished(GameOutcome.InProgress);
			session.Status = SessionStatus.InProgress;

			Assert.Throws<ArgumentException>(() => LiveMatchRecords.ToRecordRequest(session));
		}

		private static GameSessionDto Finished(GameOutcome outcome) => new()
		{
			MatchCode = "ABC123",
			Status = SessionStatus.Finished,
			Outcome = outcome,
			BlackPlayerId = 1,
			WhitePlayerId = 2,
			StartedAtUtc = Started,
			EndedAtUtc = Ended,
			Moves = [new GameMove(2, 1, 3, 0), new GameMove(5, 0, 4, 1)],
		};
	}
}

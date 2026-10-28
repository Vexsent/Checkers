using Checkers.Application.Core.MatchHistory;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.IRepositories;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Moq;

namespace UnitTests.Application.Core.MatchHistory
{
	public class MatchHistoryServiceTests
	{
		private const int MeId = 1;
		private const int OpponentId = 2;

		private static readonly User Me = new() { Id = MeId, Username = "me" };
		private static readonly User Opponent = new() { Id = OpponentId, Username = "rival" };
		private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
		private static int _nextMatchId;

		private readonly Mock<IMatchRecordRepository> _matchRecordRepository = new();
		private readonly Mock<IUserRepository> _userRepository = new();
		private readonly MatchHistoryService _sut;

		public MatchHistoryServiceTests()
		{
			_sut = new MatchHistoryService(_matchRecordRepository.Object, _userRepository.Object);

			_userRepository.Setup(r => r.GetByIdAsync(OpponentId)).ReturnsAsync(Opponent);
		}

		private void GivenMatches(params MatchRecord[] matches) =>
			_matchRecordRepository.Setup(r => r.GetByUserIdAsync(MeId)).ReturnsAsync(matches);

		private static MatchRecord NewMatch(User? black, User? white, MatchResult result) => new()
		{
			Id = Interlocked.Increment(ref _nextMatchId),
			BlackPlayerId = black?.Id,
			BlackPlayer = black,
			WhitePlayerId = white?.Id,
			WhitePlayer = white,
			Result = result,
			StartedAtUtc = Start,
			EndedAtUtc = Start.AddMinutes(15),
		};

		private static RecordMatchRequestDto ValidRecordRequest() => new()
		{
			BlackPlayerId = MeId,
			WhitePlayerId = OpponentId,
			Result = MatchResult.BlackWin,
			StartedAtUtc = Start,
			EndedAtUtc = Start.AddMinutes(15),
			Moves =
			[
				new() { FromRow = 2, FromCol = 1, ToRow = 3, ToCol = 2 },
				new() { FromRow = 5, FromCol = 0, ToRow = 4, ToCol = 1 },
			],
		};

		// ---- GetHistoryAsync ----

		[Fact]
		public async Task GetHistoryAsync_WithNoMatches_ReturnsEmptyHistoryAndZeroRecord()
		{
			GivenMatches();

			var history = await _sut.GetHistoryAsync(MeId);

			Assert.Empty(history.Matches);
			Assert.Equal(0, history.Wins);
			Assert.Equal(0, history.Losses);
			Assert.Equal(0, history.Draws);
		}

		[Theory]
		[InlineData(true, MatchResult.BlackWin, MatchOutcome.Win)]
		[InlineData(true, MatchResult.WhiteWin, MatchOutcome.Loss)]
		[InlineData(false, MatchResult.WhiteWin, MatchOutcome.Win)]
		[InlineData(false, MatchResult.BlackWin, MatchOutcome.Loss)]
		[InlineData(true, MatchResult.Draw, MatchOutcome.Draw)]
		[InlineData(false, MatchResult.Draw, MatchOutcome.Draw)]
		public async Task GetHistoryAsync_ReportsOutcomeFromUsersSide(bool meAsBlack, MatchResult result, MatchOutcome expected)
		{
			GivenMatches(meAsBlack ? NewMatch(Me, Opponent, result) : NewMatch(Opponent, Me, result));

			var summary = Assert.Single((await _sut.GetHistoryAsync(MeId)).Matches);

			Assert.Equal(expected, summary.Outcome);
			Assert.Equal(meAsBlack ? "Black" : "White", summary.PlayedAs);
		}

		[Fact]
		public async Task GetHistoryAsync_AgainstHuman_ShowsOpponentsUsername()
		{
			GivenMatches(NewMatch(Me, Opponent, MatchResult.BlackWin));

			var summary = Assert.Single((await _sut.GetHistoryAsync(MeId)).Matches);

			Assert.Equal("rival", summary.OpponentName);
			Assert.False(summary.VsComputer);
		}

		[Fact]
		public async Task GetHistoryAsync_AgainstComputer_ShowsComputerAsOpponent()
		{
			GivenMatches(NewMatch(Me, null, MatchResult.WhiteWin));

			var summary = Assert.Single((await _sut.GetHistoryAsync(MeId)).Matches);

			Assert.Equal(MatchSummaryDto.ComputerOpponentName, summary.OpponentName);
			Assert.True(summary.VsComputer);
			Assert.Equal(MatchOutcome.Loss, summary.Outcome);
		}

		[Fact]
		public async Task GetHistoryAsync_CountsWinsLossesAndDraws()
		{
			GivenMatches(
				NewMatch(Me, Opponent, MatchResult.BlackWin),
				NewMatch(Opponent, Me, MatchResult.WhiteWin),
				NewMatch(Me, null, MatchResult.WhiteWin),
				NewMatch(null, Me, MatchResult.Draw));

			var history = await _sut.GetHistoryAsync(MeId);

			Assert.Equal(4, history.Matches.Count);
			Assert.Equal(2, history.Wins);
			Assert.Equal(1, history.Losses);
			Assert.Equal(1, history.Draws);
		}

		[Fact]
		public async Task GetHistoryAsync_KeepsRepositoryOrder()
		{
			var newer = NewMatch(Me, Opponent, MatchResult.BlackWin);
			var older = NewMatch(Me, null, MatchResult.Draw);
			GivenMatches(newer, older);

			var history = await _sut.GetHistoryAsync(MeId);

			Assert.Equal([newer.Id, older.Id], history.Matches.Select(m => m.Id));
		}

		// ---- RecordMatchAsync ----

		[Fact]
		public async Task RecordMatchAsync_WithValidMatch_SavesItWithNumberedMoves()
		{
			MatchRecord? saved = null;
			_matchRecordRepository.Setup(r => r.AddAsync(It.IsAny<MatchRecord>())).Callback<MatchRecord>(m => saved = m);

			var result = await _sut.RecordMatchAsync(MeId, ValidRecordRequest());

			Assert.True(result.Success);
			Assert.NotNull(saved);
			Assert.Equal(MeId, saved.BlackPlayerId);
			Assert.Equal(OpponentId, saved.WhitePlayerId);
			Assert.Equal(MatchResult.BlackWin, saved.Result);
			Assert.Equal([1, 2], saved.Moves.Select(m => m.MoveNumber));
			Assert.Equal(5, saved.Moves[1].FromRow);
			_matchRecordRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		}

		[Fact]
		public async Task RecordMatchAsync_AgainstComputer_SavesWithNullOpponent()
		{
			var request = ValidRecordRequest();
			request.WhitePlayerId = null;

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.True(result.Success);
			_matchRecordRepository.Verify(r => r.AddAsync(It.Is<MatchRecord>(m => m.WhitePlayerId == null)), Times.Once);
			_userRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
		}

		[Fact]
		public async Task RecordMatchAsync_WhenCallerDidNotPlay_FailsWithoutSaving()
		{
			var result = await _sut.RecordMatchAsync(callerUserId: 99, ValidRecordRequest());

			Assert.False(result.Success);
			Assert.Contains("You can only record matches you played in.", result.Errors);
			AssertNothingSaved();
		}

		[Fact]
		public async Task RecordMatchAsync_WithNoHumanPlayers_Fails()
		{
			var request = ValidRecordRequest();
			request.BlackPlayerId = null;
			request.WhitePlayerId = null;

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("A match needs at least one human player.", result.Errors);
			AssertNothingSaved();
		}

		[Fact]
		public async Task RecordMatchAsync_AgainstSelf_Fails()
		{
			var request = ValidRecordRequest();
			request.WhitePlayerId = MeId;

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("A player can't play against themselves.", result.Errors);
			AssertNothingSaved();
		}

		[Fact]
		public async Task RecordMatchAsync_WithUnknownOpponent_Fails()
		{
			var request = ValidRecordRequest();
			request.WhitePlayerId = 404;

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("Opponent not found.", result.Errors);
			AssertNothingSaved();
		}

		[Fact]
		public async Task RecordMatchAsync_EndingBeforeItStarts_Fails()
		{
			var request = ValidRecordRequest();
			request.EndedAtUtc = request.StartedAtUtc.AddSeconds(-1);

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("A match can't end before it starts.", result.Errors);
			AssertNothingSaved();
		}

		[Fact]
		public async Task RecordMatchAsync_WithUnknownResult_Fails()
		{
			var request = ValidRecordRequest();
			request.Result = (MatchResult)42;

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("Unknown match result.", result.Errors);
			AssertNothingSaved();
		}

		[Theory]
		[InlineData(-1, 0, 0, 1)]
		[InlineData(0, 8, 1, 7)]
		[InlineData(7, 0, 8, 1)]
		[InlineData(3, 3, 4, -1)]
		public async Task RecordMatchAsync_WithMoveOffTheBoard_Fails(int fromRow, int fromCol, int toRow, int toCol)
		{
			var request = ValidRecordRequest();
			request.Moves.Add(new() { FromRow = fromRow, FromCol = fromCol, ToRow = toRow, ToCol = toCol });

			var result = await _sut.RecordMatchAsync(MeId, request);

			Assert.False(result.Success);
			Assert.Contains("Move 3 is off the board.", result.Errors);
			AssertNothingSaved();
		}

		private void AssertNothingSaved()
		{
			_matchRecordRepository.Verify(r => r.AddAsync(It.IsAny<MatchRecord>()), Times.Never);
			_matchRecordRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
		}
	}
}

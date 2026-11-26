using Checkers.Application.Core.Sessions;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using UnitTests.TestSupport;

namespace UnitTests.Application.Core.Sessions
{
	/// <summary>
	/// Match creation settings: the creator's color, rule variations and the turn time limit,
	/// which resigns a player automatically when their turn runs out.
	/// </summary>
	public class MatchSettingsTests
	{
		private const int Creator = 1;
		private const int Joiner = 2;

		private static readonly GameMove BlackOpening = new(2, 1, 3, 0);
		private static readonly GameMove WhiteOpening = new(5, 0, 4, 1);

		private readonly ManualTimeProvider _clock = new();
		private readonly GameSessionManager _manager;

		public MatchSettingsTests()
		{
			_manager = new GameSessionManager(new CheckersGameRules(), _clock);
		}

		private string Start(MatchSettingsDto settings)
		{
			var created = _manager.Create(Creator, "creator", settings);
			Assert.True(created.Succeeded, created.Error);
			var code = created.Session!.MatchCode;
			Assert.True(_manager.Join(code, Joiner, "joiner").Succeeded);
			return code;
		}

		private static MatchSettingsDto TimeLimit(int seconds, ColorChoice color = ColorChoice.Black) =>
			new() { CreatorColor = color, TurnTimeLimitSeconds = seconds };

		// ---- color ----

		[Fact]
		public void Create_AsWhite_SeatsCreatorWhiteAndJoinerBlack()
		{
			var created = _manager.Create(Creator, "creator", new MatchSettingsDto { CreatorColor = ColorChoice.White }).Session!;

			Assert.Equal(Creator, created.WhitePlayerId);
			Assert.Null(created.BlackPlayerId);

			var joined = _manager.Join(created.MatchCode, Joiner, "joiner").Session!;

			Assert.Equal(Joiner, joined.BlackPlayerId);
			Assert.Equal("joiner", joined.BlackPlayerName);
			Assert.Equal(SessionStatus.InProgress, joined.Status);
			// Black still moves first, so the joiner opens.
			Assert.True(_manager.MakeMove(created.MatchCode, Joiner, BlackOpening).Succeeded);
		}

		[Fact]
		public void Create_Random_SeatsCreatorOnOneSideOrTheOther()
		{
			var seen = new HashSet<PlayerColor>();
			for (var i = 0; i < 64 && seen.Count < 2; i++)
			{
				var session = _manager.Create(Creator, "creator", new MatchSettingsDto { CreatorColor = ColorChoice.Random }).Session!;
				Assert.True(session.BlackPlayerId == Creator ^ session.WhitePlayerId == Creator);
				seen.Add(session.BlackPlayerId == Creator ? PlayerColor.Black : PlayerColor.White);
			}

			Assert.Equal(2, seen.Count);
		}

		[Fact]
		public void Join_FullMatchIsRefusedWhicheverSideTheCreatorTook()
		{
			var code = Start(new MatchSettingsDto { CreatorColor = ColorChoice.White });

			Assert.False(_manager.Join(code, 3, "third").Succeeded);
		}

		// ---- rules ----

		[Fact]
		public void Create_RulesAreUsedForTheMatchAndSentToPlayers()
		{
			var rules = new GameRuleOptions { ForcedCaptures = false, FlyingKings = true };

			var session = _manager.Create(Creator, "creator", new MatchSettingsDto { Rules = rules }).Session!;

			Assert.Equal(rules, session.Rules);
		}

		// ---- turn time limit ----

		[Theory]
		[InlineData(29)]
		[InlineData(0)]
		[InlineData(-5)]
		[InlineData(MatchSettingsDto.MaxTurnTimeLimitSeconds + 1)]
		public void Create_TimeLimitOutsideRange_IsRefused(int seconds)
		{
			var result = _manager.Create(Creator, "creator", TimeLimit(seconds));

			Assert.Equal(MatchSettingsDto.TurnTimeLimitError, result.Error);
		}

		[Theory]
		[InlineData(MatchSettingsDto.MinTurnTimeLimitSeconds)]
		[InlineData(MatchSettingsDto.MaxTurnTimeLimitSeconds)]
		public void Create_TimeLimitAtEitherEnd_IsAccepted(int seconds)
		{
			Assert.True(_manager.Create(Creator, "creator", TimeLimit(seconds)).Succeeded);
		}

		[Fact]
		public void TurnClock_StartsWhenOpponentJoins()
		{
			var created = _manager.Create(Creator, "creator", TimeLimit(60)).Session!;
			Assert.Null(created.TurnDeadlineUtc);

			_clock.Advance(TimeSpan.FromMinutes(10));
			var joined = _manager.Join(created.MatchCode, Joiner, "joiner").Session!;

			Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddSeconds(60), joined.TurnDeadlineUtc);
		}

		[Fact]
		public void TurnClock_RestartsWhenTheTurnPasses()
		{
			var code = Start(TimeLimit(60));
			_clock.Advance(TimeSpan.FromSeconds(50));

			var session = _manager.MakeMove(code, Creator, BlackOpening).Session!;

			Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddSeconds(60), session.TurnDeadlineUtc);
		}

		[Fact]
		public void TurnClock_KeepsRunningThroughAMultiJump()
		{
			// Rules that leave the turn with black after the first step, like a multi-jump.
			var manager = new GameSessionManager(new MultiStepRules(), _clock);
			var code = manager.Create(Creator, "creator", TimeLimit(60)).Session!.MatchCode;
			var deadline = manager.Join(code, Joiner, "joiner").Session!.TurnDeadlineUtc;

			_clock.Advance(TimeSpan.FromSeconds(20));
			var midJump = manager.MakeMove(code, Creator, BlackOpening).Session!;

			Assert.Equal(PlayerColor.Black, midJump.CurrentPlayer);
			Assert.Equal(deadline, midJump.TurnDeadlineUtc);
		}

		[Fact]
		public void NoTimeLimit_NoDeadlineAndNothingExpires()
		{
			var code = Start(new MatchSettingsDto());
			_clock.Advance(TimeSpan.FromDays(30));

			Assert.Null(_manager.Join(code, Creator, "creator").Session!.TurnDeadlineUtc);
			Assert.Empty(_manager.ExpireOverdueTurns());
		}

		[Fact]
		public void ExpireOverdueTurns_BeforeTheDeadline_LeavesMatchAlone()
		{
			Start(TimeLimit(60));
			_clock.Advance(TimeSpan.FromSeconds(59));

			Assert.Empty(_manager.ExpireOverdueTurns());
		}

		[Fact]
		public void ExpireOverdueTurns_ResignsThePlayerWhoseTurnRanOut()
		{
			var code = Start(TimeLimit(60));
			_manager.MakeMove(code, Creator, BlackOpening);
			_clock.Advance(TimeSpan.FromSeconds(60));

			var expired = Assert.Single(_manager.ExpireOverdueTurns());

			Assert.Equal(code, expired.MatchCode);
			Assert.Equal(SessionStatus.Finished, expired.Status);
			Assert.Equal(GameOutcome.BlackWon, expired.Outcome);
			Assert.Equal(GameEndReason.Timeout, expired.EndReason);
			Assert.Null(expired.TurnDeadlineUtc);
			Assert.Empty(_manager.GetActiveMatches(Joiner));
			// Only ends once.
			Assert.Empty(_manager.ExpireOverdueTurns());
		}

		[Fact]
		public void MoveAfterTheDeadline_IsRefusedAndReturnsTheTimedOutMatch()
		{
			var code = Start(TimeLimit(30));
			_clock.Advance(TimeSpan.FromSeconds(31));

			var result = _manager.MakeMove(code, Creator, BlackOpening);

			Assert.Equal(GameSessionManager.TimeRanOutError, result.Error);
			Assert.Equal(GameOutcome.WhiteWon, result.Session!.Outcome);
			Assert.Equal(GameEndReason.Timeout, result.Session.EndReason);
			Assert.Empty(result.Session.Moves);
		}

		[Fact]
		public void Resign_RecordsResignationAsTheEndReason()
		{
			var code = Start(TimeLimit(60));

			var session = _manager.Resign(code, Joiner).Session!;

			Assert.Equal(GameEndReason.Resignation, session.EndReason);
			Assert.Equal(GameOutcome.BlackWon, session.Outcome);
		}

		[Fact]
		public void WeekLongLimit_ExpiresOnlyAfterAWeek()
		{
			var code = Start(TimeLimit(MatchSettingsDto.MaxTurnTimeLimitSeconds));
			_manager.MakeMove(code, Creator, BlackOpening);
			_manager.MakeMove(code, Joiner, WhiteOpening);

			_clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1));
			Assert.Empty(_manager.ExpireOverdueTurns());

			_clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(GameOutcome.WhiteWon, Assert.Single(_manager.ExpireOverdueTurns()).Outcome);
		}

		/// <summary>
		/// Accepts any move, keeping black on turn after its first step (a multi-jump in progress).
		/// </summary>
		private sealed class MultiStepRules : IGameRules
		{
			public IRulesGame NewGame(GameRuleOptions rules) => new MultiStepGame();

			private sealed class MultiStepGame : IRulesGame
			{
				public Board Board { get; } = Board.CreateStandard();
				public PlayerColor CurrentTurn => PlayerColor.Black;
				public GameOutcome Outcome => GameOutcome.InProgress;
				public string? TryMove(GameMove move) => null;
			}
		}
	}
}

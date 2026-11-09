using Checkers.Application.Core.Sessions;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Xunit;

namespace UnitTests.Application.Core.Sessions
{
	public class GameSessionManagerTests
	{
		private const int Black = 1;
		private const int White = 2;
		private const int Outsider = 3;

		private static readonly GameMove BlackOpening = new(2, 1, 3, 0);
		private static readonly GameMove WhiteOpening = new(5, 0, 4, 1);

		private readonly GameSessionManager _manager = new(new PlaceholderGameRules());

		private string StartMatch()
		{
			var code = _manager.Create(Black, "black").Session!.MatchCode;
			_manager.Join(code, White, "white");
			return code;
		}

		[Fact]
		public void Create_WaitsForOpponentWithCreatorAsBlack()
		{
			var session = _manager.Create(Black, "black").Session!;

			Assert.Equal(6, session.MatchCode.Length);
			Assert.Equal(SessionStatus.WaitingForOpponent, session.Status);
			Assert.Equal(Black, session.BlackPlayerId);
			Assert.Null(session.WhitePlayerId);
		}

		[Fact]
		public void Join_FillsSeatAndStartsMatch()
		{
			var code = _manager.Create(Black, "black").Session!.MatchCode;

			var session = _manager.Join(code, White, "white").Session!;

			Assert.Equal(SessionStatus.InProgress, session.Status);
			Assert.Equal(White, session.WhitePlayerId);
			Assert.Equal("white", session.WhitePlayerName);
		}

		[Fact]
		public void Join_IgnoresCodeCase()
		{
			var code = _manager.Create(Black, "black").Session!.MatchCode;

			Assert.True(_manager.Join(code.ToLowerInvariant(), White, "white").Succeeded);
		}

		[Fact]
		public void Join_UnknownCode_Fails()
		{
			Assert.False(_manager.Join("NOPE00", White, "white").Succeeded);
		}

		[Fact]
		public void Join_FullMatch_RejectsThirdUser()
		{
			var code = StartMatch();

			Assert.False(_manager.Join(code, Outsider, "outsider").Succeeded);
		}

		[Fact]
		public void Join_ExistingPlayer_ReconnectsWithoutChangingSeats()
		{
			var code = StartMatch();
			_manager.MakeMove(code, Black, BlackOpening);

			var session = _manager.Join(code, Black, "black").Session!;

			Assert.Equal(Black, session.BlackPlayerId);
			Assert.Equal(White, session.WhitePlayerId);
			Assert.Equal([BlackOpening], session.Moves);
		}

		[Fact]
		public void MakeMove_BeforeOpponentJoins_Fails()
		{
			var code = _manager.Create(Black, "black").Session!.MatchCode;

			Assert.False(_manager.MakeMove(code, Black, BlackOpening).Succeeded);
		}

		[Fact]
		public void MakeMove_OutOfTurn_FailsAndLeavesBoardAlone()
		{
			var code = StartMatch();

			var result = _manager.MakeMove(code, White, WhiteOpening);

			Assert.False(result.Succeeded);
			Assert.Empty(_manager.Join(code, White, "white").Session!.Moves);
		}

		[Fact]
		public void MakeMove_ByOutsider_Fails()
		{
			var code = StartMatch();

			Assert.False(_manager.MakeMove(code, Outsider, BlackOpening).Succeeded);
		}

		[Fact]
		public void MakeMove_Legal_UpdatesBoardAndPassesTurn()
		{
			var code = StartMatch();

			var session = _manager.MakeMove(code, Black, BlackOpening).Session!;
			var board = Board.FromSquares(session.BoardSize, session.Squares);

			Assert.Equal(GamePieces.Empty, board[2, 1]);
			Assert.Equal(GamePieces.BlackPawn, board[3, 0]);
			Assert.Equal(PlayerColor.White, session.CurrentPlayer);
			Assert.Equal([BlackOpening], session.Moves);
		}

		[Fact]
		public void MakeMove_RejectedByRules_ReturnsRulesError()
		{
			var code = StartMatch();

			var result = _manager.MakeMove(code, Black, WhiteOpening);

			Assert.False(result.Succeeded);
			Assert.Equal("You can only move your own pieces.", result.Error);
		}

		[Fact]
		public void MakeMove_RulesKeepSamePlayer_AllowsMultiJumpContinuation()
		{
			var manager = new GameSessionManager(new ScriptedRules(PlayerColor.Black, GameOutcome.InProgress));
			var code = manager.Create(Black, "black").Session!.MatchCode;
			manager.Join(code, White, "white");

			manager.MakeMove(code, Black, BlackOpening);
			var session = manager.MakeMove(code, Black, BlackOpening).Session!;

			Assert.Equal(PlayerColor.Black, session.CurrentPlayer);
			Assert.Equal(2, session.Moves.Length);
		}

		[Fact]
		public void MakeMove_RulesEndGame_FinishesMatchAndBlocksMoreMoves()
		{
			var manager = new GameSessionManager(new ScriptedRules(PlayerColor.White, GameOutcome.BlackWon));
			var code = manager.Create(Black, "black").Session!.MatchCode;
			manager.Join(code, White, "white");

			var session = manager.MakeMove(code, Black, BlackOpening).Session!;

			Assert.Equal(SessionStatus.Finished, session.Status);
			Assert.Equal(GameOutcome.BlackWon, session.Outcome);
			Assert.False(manager.MakeMove(code, White, WhiteOpening).Succeeded);
		}

		[Fact]
		public void Resign_OpponentWins()
		{
			var code = StartMatch();

			var session = _manager.Resign(code, Black).Session!;

			Assert.Equal(SessionStatus.Finished, session.Status);
			Assert.Equal(GameOutcome.WhiteWon, session.Outcome);
		}

		[Fact]
		public void Resign_ByOutsider_Fails()
		{
			var code = StartMatch();

			Assert.False(_manager.Resign(code, Outsider).Succeeded);
		}

		[Fact]
		public void GetActiveMatchCodes_ExcludesFinishedAndOtherUsersMatches()
		{
			var finished = StartMatch();
			var active = StartMatch();
			_manager.Resign(finished, Black);

			Assert.Equal([active], _manager.GetActiveMatchCodes(Black));
			Assert.Empty(_manager.GetActiveMatchCodes(Outsider));
		}

		/// <summary>
		/// Accepts every move, then hands the turn to <paramref name="nextPlayer"/> with <paramref name="outcome"/>.
		/// </summary>
		private sealed class ScriptedRules(PlayerColor nextPlayer, GameOutcome outcome) : IGameRules
		{
			public IRulesGame NewGame() => new ScriptedGame(nextPlayer, outcome);

			private sealed class ScriptedGame(PlayerColor nextPlayer, GameOutcome outcome) : IRulesGame
			{
				public Board Board { get; } = Board.CreateStandard();
				public PlayerColor CurrentTurn { get; private set; } = PlayerColor.Black;
				public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;

				public string? TryMove(GameMove move)
				{
					CurrentTurn = nextPlayer;
					Outcome = outcome;
					return null;
				}
			}
		}
	}
}

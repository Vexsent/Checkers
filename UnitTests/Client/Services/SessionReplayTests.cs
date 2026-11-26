using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Xunit;
using BoardSquare = Checkers.Shared.GameModels.Square;

namespace UnitTests.Client.Services
{
	public class SessionReplayTests
	{
		[Fact]
		public void From_NoMoves_IsExactWithBlackToMoveAndNoTurns()
		{
			var replay = SessionReplay.From(SessionFor(new Game(), []));

			Assert.True(replay.IsExact);
			Assert.Empty(replay.Turns);
			Assert.Equal(PlayerColor.Black, replay.Game.CurrentTurn);
		}

		[Fact]
		public void From_TwoSteps_GivesOneTurnEach()
		{
			var (game, moves) = Play(new GameMove(2, 1, 3, 0), new GameMove(5, 0, 4, 1));

			var replay = SessionReplay.From(SessionFor(game, moves));

			Assert.Collection(replay.Turns,
				turn =>
				{
					Assert.Equal(GamePieces.BlackPawn, turn.Piece);
					Assert.Equal([new BoardSquare(2, 1), new BoardSquare(3, 0)], turn.Path);
				},
				turn => Assert.Equal(GamePieces.WhitePawn, turn.Piece));
		}

		// Random but seeded games cover forced jumps, multi-jumps and crowning without hand-built positions.
		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(5)]
		public void From_RandomGame_MatchesRulesEngineAfterEveryStep(int seed)
		{
			var random = new Random(seed);
			var game = new Game();
			var moves = new List<GameMove>();
			var turns = new List<(GamePieces Piece, List<BoardSquare> Path, bool Crowned)>();
			List<BoardSquare>? path = null;
			var piece = GamePieces.Empty;

			while (!game.IsOver && moves.Count < 300)
			{
				var legal = game.GetLegalMoves();
				var move = legal[random.Next(legal.Count)];
				if (path is null)
				{
					piece = game.Board[move.From];
					path = [move.From];
				}

				var mover = game.CurrentTurn;
				Assert.True(game.TryMove(move.From, move.To).Success);
				moves.Add(new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
				path.Add(move.To);
				if (game.CurrentTurn != mover || game.IsOver)
				{
					turns.Add((piece, path, !Game.IsKing(piece) && Game.IsKing(game.Board[move.To])));
					path = null;
				}

				var replay = SessionReplay.From(SessionFor(game, moves));

				Assert.True(replay.IsExact);
				Assert.Equal(game.CurrentTurn, replay.Game.CurrentTurn);
				Assert.Equal(game.JumpingPiece, replay.Game.JumpingPiece);
				Assert.Equal(game.GetLegalMoves(), replay.Game.GetLegalMoves());
				Assert.Equal(turns.Count, replay.Turns.Count);
				if (turns.Count > 0)
				{
					var last = turns[^1];
					Assert.Equal(last.Piece, replay.Turns[^1].Piece);
					Assert.Equal(last.Path, replay.Turns[^1].Path);
					Assert.Equal(last.Crowned, replay.Turns[^1].Crowned);
				}
			}

			Assert.Contains(turns, turn => turn.Path.Count > 2);
		}

		[Fact]
		public void From_BoardDoesNotMatchMoves_FallsBackToBoardAsSent()
		{
			var (game, moves) = Play(new GameMove(2, 1, 3, 0));
			var session = SessionFor(game, moves);
			session.Squares = Board.CreateStandard().ToSquares();

			var replay = SessionReplay.From(session);

			Assert.False(replay.IsExact);
			Assert.Empty(replay.Turns);
			Assert.Equal(GamePieces.BlackPawn, replay.Game.Board[2, 1]);
			Assert.Equal(PlayerColor.White, replay.Game.CurrentTurn);
		}

		[Fact]
		public void From_IllegalMove_FallsBack()
		{
			var session = SessionFor(new Game(), [new GameMove(2, 1, 4, 3)]);

			Assert.False(SessionReplay.From(session).IsExact);
		}

		// ---- FromMoves (saved matches) ----

		[Fact]
		public void FromMoves_NoMoves_IsExactWithNoTurns()
		{
			var replay = SessionReplay.FromMoves([]);

			Assert.True(replay.IsExact);
			Assert.Empty(replay.Turns);
			Assert.Equal(Board.CreateStandard().ToSquares(), replay.Game.Board.ToSquares());
		}

		[Fact]
		public void FromMoves_EachTurnKeepsTheBoardAfterIt()
		{
			var (afterFirst, _) = Play(new GameMove(2, 1, 3, 0));
			var (afterSecond, moves) = Play(new GameMove(2, 1, 3, 0), new GameMove(5, 0, 4, 1));

			var replay = SessionReplay.FromMoves(moves);

			Assert.Equal(2, replay.Turns.Count);
			Assert.Equal(afterFirst.Board.ToSquares(), replay.Turns[0].BoardAfter);
			Assert.Equal(afterSecond.Board.ToSquares(), replay.Turns[1].BoardAfter);
		}

		// Every prefix of a seeded game, so a replay that stops partway through a multi-jump
		// (a resignation mid-jump) is covered as well as whole games.
		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(5)]
		public void FromMoves_RandomGame_LastTurnShowsTheBoardAfterEveryStep(int seed)
		{
			var random = new Random(seed);
			var game = new Game();
			var moves = new List<GameMove>();
			var completedTurns = 0;

			while (!game.IsOver && moves.Count < 300)
			{
				var legal = game.GetLegalMoves();
				var move = legal[random.Next(legal.Count)];
				var mover = game.CurrentTurn;
				Assert.True(game.TryMove(move.From, move.To).Success);
				moves.Add(new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
				var turnOver = game.CurrentTurn != mover || game.IsOver;

				var replay = SessionReplay.FromMoves(moves);

				Assert.True(replay.IsExact);
				Assert.Equal(game.Board.ToSquares(), replay.Turns[^1].BoardAfter);
				// An unfinished multi-jump shows as one extra, partial turn.
				Assert.Equal(completedTurns + 1, replay.Turns.Count);
				if (turnOver)
					completedTurns++;
			}

			Assert.True(game.IsOver);
		}

		// Variant rules: the replay has to use the match's rules, or a flying king's slide or a
		// skipped optional capture would look illegal.
		[Theory]
		[InlineData(false, true, 1)]
		[InlineData(false, true, 2)]
		[InlineData(true, true, 3)]
		[InlineData(false, false, 4)]
		public void VariantRules_LiveAndSavedReplaysMatchTheEngine(bool forcedCaptures, bool flyingKings, int seed)
		{
			var rules = new GameRuleOptions { ForcedCaptures = forcedCaptures, FlyingKings = flyingKings };
			var (game, moves) = PlayRandom(rules, seed);

			var live = SessionReplay.From(SessionFor(game, moves, rules));
			var saved = SessionReplay.FromMoves(moves, rules);

			Assert.True(live.IsExact);
			Assert.True(saved.IsExact);
			Assert.Equal(game.Board.ToSquares(), saved.Game.Board.ToSquares());
			Assert.Equal(game.GetLegalMoves(), live.Game.GetLegalMoves());
			Assert.Equal(rules, live.Game.Rules);
		}

		[Fact]
		public void FlyingKingGame_ReplayedWithStandardRules_IsNotExact()
		{
			var rules = new GameRuleOptions { FlyingKings = true };
			// A seeded game where a king actually moved more than two rows at once.
			var (_, moves) = Enumerable.Range(1, 50)
				.Select(seed => PlayRandom(rules, seed))
				.First(g => g.Moves.Any(m => Math.Abs(m.ToRow - m.FromRow) > 2));

			Assert.False(SessionReplay.FromMoves(moves).IsExact);
			Assert.True(SessionReplay.FromMoves(moves, rules).IsExact);
		}

		[Fact]
		public void FlyingKingLongCapture_IsWrittenAsAJumpInTheMoveList()
		{
			var rules = new GameRuleOptions { FlyingKings = true };
			var (game, moves) = Enumerable.Range(1, 50)
				.Select(seed => PlayRandom(rules, seed))
				.First(g => g.Game.Moves.Any(IsLongCapture));
			var replay = SessionReplay.From(SessionFor(game, moves, rules));
			var relay = new RelayService();

			replay.UpdateMoveList(relay);

			var capture = game.Moves.First(IsLongCapture);
			var index = replay.Turns.ToList().FindIndex(t => t.IsJump && t.Path.Contains(capture.From) && t.Path.Contains(capture.To));
			Assert.True(index >= 0);
			Assert.Contains("x", relay.Moves[index].Text);
		}

		private static bool IsLongCapture(Move move) => move.IsJump && Math.Abs(move.To.Row - move.From.Row) > 2;

		[Fact]
		public void FromMoves_IllegalMove_StopsBeforeIt()
		{
			var (game, moves) = Play(new GameMove(2, 1, 3, 0), new GameMove(5, 0, 4, 1));
			moves.Add(new GameMove(2, 3, 4, 5)); // two rows with nothing to jump
			moves.Add(new GameMove(5, 2, 4, 3));

			var replay = SessionReplay.FromMoves(moves);

			Assert.False(replay.IsExact);
			Assert.Equal(2, replay.Turns.Count);
			Assert.Equal(game.Board.ToSquares(), replay.Turns[^1].BoardAfter);
		}

		[Fact]
		public void UpdateMoveList_AppendsOnlyNewTurnsAndKeepsExistingEntries()
		{
			var relay = new RelayService();
			var (game, moves) = Play(new GameMove(2, 1, 3, 0));
			SessionReplay.From(SessionFor(game, moves)).UpdateMoveList(relay);
			var first = relay.Moves[0];

			Assert.True(game.TryMove(new BoardSquare(5, 0), new BoardSquare(4, 1)).Success);
			moves.Add(new GameMove(5, 0, 4, 1));
			SessionReplay.From(SessionFor(game, moves)).UpdateMoveList(relay);

			Assert.Equal(2, relay.Moves.Count);
			Assert.Same(first, relay.Moves[0]);
			Assert.Equal("BP b6-a5", relay.Moves[0].Text);
			Assert.Equal("WP a3-b4", relay.Moves[1].Text);
		}

		[Fact]
		public void UpdateMoveList_RelayAheadOfMatch_StartsOver()
		{
			var relay = new RelayService();
			var (game, moves) = Play(new GameMove(2, 1, 3, 0), new GameMove(5, 0, 4, 1));
			SessionReplay.From(SessionFor(game, moves)).UpdateMoveList(relay);

			var (other, otherMoves) = Play(new GameMove(2, 3, 3, 4));
			SessionReplay.From(SessionFor(other, otherMoves)).UpdateMoveList(relay);

			Assert.Equal("BP d6-e5", Assert.Single(relay.Moves).Text);
		}

		[Fact]
		public void UpdateMoveList_NotExact_LeavesListAlone()
		{
			var relay = new RelayService();
			var (game, moves) = Play(new GameMove(2, 1, 3, 0));
			SessionReplay.From(SessionFor(game, moves)).UpdateMoveList(relay);

			var session = SessionFor(game, moves);
			session.Squares = Board.CreateStandard().ToSquares();
			SessionReplay.From(session).UpdateMoveList(relay);

			Assert.Single(relay.Moves);
		}

		private static (Game Game, List<GameMove> Moves) Play(params GameMove[] moves)
		{
			var game = new Game();
			foreach (var move in moves)
				Assert.True(game.TryMove(new BoardSquare(move.FromRow, move.FromCol), new BoardSquare(move.ToRow, move.ToCol)).Success);

			return (game, [.. moves]);
		}

		private static GameSessionDto SessionFor(Game game, IEnumerable<GameMove> moves, GameRuleOptions? rules = null) => new()
		{
			MatchCode = "ABC123",
			Status = SessionStatus.InProgress,
			CurrentPlayer = game.CurrentTurn,
			BoardSize = game.Board.Size,
			Squares = game.Board.ToSquares(),
			Moves = [.. moves],
			Rules = rules ?? GameRuleOptions.Standard,
		};

		private static (Game Game, List<GameMove> Moves) PlayRandom(GameRuleOptions rules, int seed)
		{
			var random = new Random(seed);
			var game = new Game(rules);
			var moves = new List<GameMove>();
			while (!game.IsOver && moves.Count < 300)
			{
				var legal = game.GetLegalMoves();
				var move = legal[random.Next(legal.Count)];
				Assert.True(game.TryMove(move.From, move.To).Success);
				moves.Add(new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
			}

			return (game, moves);
		}
	}
}

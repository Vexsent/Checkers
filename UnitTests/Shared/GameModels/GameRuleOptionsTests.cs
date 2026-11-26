using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace UnitTests.Shared.GameModels
{
	/// <summary>
	/// The optional rule variations: forced captures off and flying kings.
	/// </summary>
	public class GameRuleOptionsTests
	{
		private static readonly GameRuleOptions OptionalCaptures = new() { ForcedCaptures = false };
		private static readonly GameRuleOptions Flying = new() { FlyingKings = true };

		/// <summary>
		/// A board with just the given pieces, e.g. ((3, 2), BlackPawn).
		/// </summary>
		private static Board BoardWith(params ((int Row, int Col) Square, GamePieces Piece)[] pieces)
		{
			var board = new Board();
			foreach (var ((row, col), piece) in pieces)
				board[row, col] = piece;
			return board;
		}

		// ---- forced captures ----

		[Fact]
		public void ForcedCaptures_On_OnlyJumpsAreLegalWhenOneIsAvailable()
		{
			var game = new Game(BoardWith(((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((2, 5), GamePieces.BlackPawn), ((7, 0), GamePieces.WhitePawn)));

			Assert.All(game.GetLegalMoves(), move => Assert.True(move.IsJump));
			Assert.Equal(Game.MustJumpError, game.TryMove(new Square(2, 5), new Square(3, 4)).Error);
		}

		[Fact]
		public void ForcedCaptures_Off_StepsAreAllowedAlongsideJumps()
		{
			var game = new Game(BoardWith(((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((2, 5), GamePieces.BlackPawn), ((7, 0), GamePieces.WhitePawn)),
				rules: OptionalCaptures);

			var legal = game.GetLegalMoves();

			Assert.Contains(legal, move => move.IsJump);
			Assert.Contains(legal, move => !move.IsJump);
			Assert.True(game.TryMove(new Square(2, 5), new Square(3, 4)).Success);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
			Assert.Equal(GamePieces.WhitePawn, game.Board[3, 2]);
		}

		[Fact]
		public void ForcedCaptures_Off_AStartedMultiJumpMustStillBeFinished()
		{
			// Black at (0,1) can jump (1,2) to (2,3), then (3,4) to (4,5).
			var game = new Game(BoardWith(((0, 1), GamePieces.BlackPawn), ((1, 2), GamePieces.WhitePawn), ((3, 4), GamePieces.WhitePawn), ((0, 7), GamePieces.BlackPawn)),
				rules: OptionalCaptures);

			Assert.True(game.TryMove(new Square(0, 1), new Square(2, 3)).Success);

			Assert.Equal(new Square(2, 3), game.JumpingPiece);
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
			Assert.Equal(Game.ContinueJumpError, game.TryMove(new Square(0, 7), new Square(1, 6)).Error);
			Assert.True(game.TryMove(new Square(2, 3), new Square(4, 5)).Success);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		// ---- flying kings ----

		[Fact]
		public void FlyingKings_Off_KingMovesOneSquare()
		{
			var game = new Game(BoardWith(((4, 3), GamePieces.BlackKing), ((7, 0), GamePieces.WhitePawn)));

			Assert.All(game.GetLegalMoves(new Square(4, 3)), move => Assert.Equal(1, Math.Abs(move.To.Row - move.From.Row)));
		}

		[Fact]
		public void FlyingKings_KingSlidesAnyDistanceUntilBlocked()
		{
			// Up-left from (4,3): (3,2), (2,1), (1,0). Down-right: (5,4) then blocked by own pawn at (6,5).
			var game = new Game(BoardWith(((4, 3), GamePieces.BlackKing), ((6, 5), GamePieces.BlackPawn), ((0, 7), GamePieces.WhitePawn)), rules: Flying);

			var targets = game.GetLegalMoves(new Square(4, 3)).Select(m => m.To).ToHashSet();

			Assert.Contains(new Square(1, 0), targets);
			Assert.Contains(new Square(5, 4), targets);
			Assert.DoesNotContain(new Square(6, 5), targets);
			Assert.DoesNotContain(new Square(7, 6), targets);
			Assert.True(game.TryMove(new Square(4, 3), new Square(1, 0)).Success);
			Assert.Equal(GamePieces.BlackKing, game.Board[1, 0]);
		}

		[Fact]
		public void FlyingKings_PawnsStillMoveOneSquare()
		{
			var game = new Game(BoardWith(((2, 1), GamePieces.BlackPawn), ((7, 0), GamePieces.WhitePawn)), rules: Flying);

			Assert.All(game.GetLegalMoves(), move => Assert.Equal(1, move.To.Row - move.From.Row));
		}

		[Fact]
		public void FlyingKings_KingCapturesFromADistanceAndLandsOnAnyEmptySquareBeyond()
		{
			// King at (0,1) looks down-right: empty (1,2), white at (2,3), then (3,4), (4,5) empty, own pawn at (5,6).
			var game = new Game(BoardWith(((0, 1), GamePieces.BlackKing), ((2, 3), GamePieces.WhitePawn), ((5, 6), GamePieces.BlackPawn), ((7, 0), GamePieces.WhitePawn)), rules: Flying);

			var jumps = game.GetLegalMoves(new Square(0, 1));

			Assert.All(jumps, move => Assert.Equal(new Square(2, 3), move.Captured));
			Assert.Equal([new Square(3, 4), new Square(4, 5)], jumps.Select(m => m.To).Order(SquareOrder.Instance));

			Assert.True(game.TryMove(new Square(0, 1), new Square(4, 5)).Success);
			Assert.Equal(GamePieces.Empty, game.Board[2, 3]);
			Assert.Equal(GamePieces.BlackKing, game.Board[4, 5]);
		}

		[Fact]
		public void FlyingKings_CannotCaptureTwoPiecesInARowOrOverItsOwn()
		{
			// Two whites back to back at (2,3),(3,4): no landing square after the first. Own pawn shields (5,2).
			var game = new Game(BoardWith(((1, 2), GamePieces.BlackKing), ((2, 3), GamePieces.WhitePawn), ((3, 4), GamePieces.WhitePawn), ((2, 1), GamePieces.BlackPawn), ((7, 6), GamePieces.WhitePawn)),
				rules: Flying);

			Assert.DoesNotContain(game.GetLegalMoves(), move => move.IsJump);
		}

		[Fact]
		public void FlyingKings_KingKeepsCapturingFromItsLandingSquare()
		{
			// Capture (2,3) landing on (4,5), then from (4,5) capture (5,4) down-left landing on (6,3) or (7,2).
			var game = new Game(BoardWith(((0, 1), GamePieces.BlackKing), ((2, 3), GamePieces.WhitePawn), ((5, 4), GamePieces.WhitePawn), ((7, 6), GamePieces.WhitePawn)), rules: Flying);

			Assert.True(game.TryMove(new Square(0, 1), new Square(4, 5)).Success);

			Assert.Equal(new Square(4, 5), game.JumpingPiece);
			Assert.Equal([new Square(6, 3), new Square(7, 2)],
				game.GetLegalMoves().Where(m => m.Captured == new Square(5, 4)).Select(m => m.To).Order(SquareOrder.Instance));
		}

		// Seeded random games under every combination of options: the engine never offers a move it then rejects.
		[Theory]
		[InlineData(true, false)]
		[InlineData(false, false)]
		[InlineData(true, true)]
		[InlineData(false, true)]
		public void RandomGames_EveryOfferedMoveIsAccepted(bool forcedCaptures, bool flyingKings)
		{
			var rules = new GameRuleOptions { ForcedCaptures = forcedCaptures, FlyingKings = flyingKings };
			for (var seed = 1; seed <= 10; seed++)
			{
				var random = new Random(seed);
				var game = new Game(rules);
				for (var step = 0; step < 400 && !game.IsOver; step++)
				{
					var legal = game.GetLegalMoves();
					var move = legal[random.Next(legal.Count)];
					Assert.True(game.TryMove(move.From, move.To).Success);
				}
			}
		}

		private sealed class SquareOrder : IComparer<Square>
		{
			public static readonly SquareOrder Instance = new();
			public int Compare(Square x, Square y) => (x.Row, x.Col).CompareTo((y.Row, y.Col));
		}
	}
}

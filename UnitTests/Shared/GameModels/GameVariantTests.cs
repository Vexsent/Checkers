using Checkers.Client.Services;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using BoardSquare = Checkers.Shared.GameModels.Square;

namespace UnitTests.Shared.GameModels
{
	/// <summary>
	/// The variant presets and the rules behind them. Positions are built by hand: Black starts at the
	/// top (row 0) moving down, White at the bottom moving up.
	/// </summary>
	public class GameVariantTests
	{
		private static GameRuleOptions Preset(GameVariant variant) => GameVariants.Find(variant)!.Rules;

		private static Board BoardWith(int size, params ((int Row, int Col) Square, GamePieces Piece)[] pieces)
		{
			var board = new Board(size);
			foreach (var ((row, col), piece) in pieces)
				board[row, col] = piece;
			return board;
		}

		private static Board BoardWith(params ((int Row, int Col) Square, GamePieces Piece)[] pieces) => BoardWith(8, pieces);

		private static BoardSquare Sq(int row, int col) => new(row, col);

		// ---- presets ----

		public static TheoryData<GameVariant> AllPresets()
		{
			var data = new TheoryData<GameVariant>();
			foreach (var preset in GameVariants.All)
				data.Add(preset.Variant);
			return data;
		}

		[Theory]
		[MemberData(nameof(AllPresets))]
		public void Preset_IsValidAndNamed(GameVariant variant)
		{
			var preset = GameVariants.Find(variant)!;

			Assert.Null(preset.Rules.Validate());
			Assert.Equal(variant, preset.Rules.Variant);
			Assert.False(string.IsNullOrWhiteSpace(preset.Description));
			Assert.Equal(preset.Name, GameVariants.NameOf(preset.Rules));
		}

		[Theory]
		[InlineData(GameVariant.American, 8, 12, PlayerColor.Black)]
		[InlineData(GameVariant.Russian, 8, 12, PlayerColor.White)]
		[InlineData(GameVariant.Pool, 8, 12, PlayerColor.Black)]
		[InlineData(GameVariant.Brazilian, 8, 12, PlayerColor.White)]
		[InlineData(GameVariant.International, 10, 20, PlayerColor.White)]
		[InlineData(GameVariant.Canadian, 12, 30, PlayerColor.White)]
		[InlineData(GameVariant.Thai, 8, 8, PlayerColor.Black)]
		public void NewGame_UsesTheVariantsBoardPiecesAndFirstPlayer(GameVariant variant, int size, int menEach, PlayerColor first)
		{
			var game = new Game(Preset(variant));

			Assert.Equal(size, game.Board.Size);
			Assert.Equal(menEach, game.Board.ToSquares().Count(p => p == GamePieces.BlackPawn));
			Assert.Equal(menEach, game.Board.ToSquares().Count(p => p == GamePieces.WhitePawn));
			Assert.Equal(first, game.CurrentTurn);
		}

		// Seeded random games for every preset: every offered move is accepted, games finish, and a
		// replay of the moves with the same rules lands on the same board.
		[Theory]
		[MemberData(nameof(AllPresets))]
		public void Preset_RandomGamesPlayAndReplay(GameVariant variant)
		{
			var rules = Preset(variant);
			for (var seed = 1; seed <= 6; seed++)
			{
				var random = new Random(seed);
				var game = new Game(rules);
				var moves = new List<GameMove>();
				while (!game.IsOver && moves.Count < 600)
				{
					var legal = game.GetLegalMoves();
					var move = legal[random.Next(legal.Count)];
					Assert.True(game.TryMove(move.From, move.To).Success);
					moves.Add(new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
				}

				var replay = SessionReplay.FromMoves(moves, rules);
				Assert.True(replay.IsExact);
				Assert.Equal(game.Board.ToSquares(), replay.Game.Board.ToSquares());
			}
		}

		// ---- men capturing backwards ----

		[Fact]
		public void MenCaptureBackward_OnlyWhenTheRulesAllowIt()
		{
			// The white man at (3,2) is behind the black man at (4,3).
			var board = () => BoardWith(((4, 3), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((7, 0), GamePieces.WhitePawn));

			var russian = new Game(board(), PlayerColor.Black, Preset(GameVariant.Russian));
			var american = new Game(board(), PlayerColor.Black, Preset(GameVariant.American));

			Assert.Equal([new Move(Sq(4, 3), Sq(2, 1), Sq(3, 2))], russian.GetLegalMoves());
			Assert.DoesNotContain(american.GetLegalMoves(), m => m.IsJump);
		}

		// ---- choosing between captures ----

		// Black (2,1) can take two men; Black (2,5) can take one.
		private static Board TwoOrOne() => BoardWith(
			((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((5, 4), GamePieces.WhitePawn),
			((2, 5), GamePieces.BlackPawn), ((3, 6), GamePieces.WhitePawn));

		[Fact]
		public void MostPieces_OnlyTheLongestCaptureIsLegal()
		{
			var game = new Game(TwoOrOne(), PlayerColor.Black, Preset(GameVariant.Brazilian));

			Assert.Equal([new Move(Sq(2, 1), Sq(4, 3), Sq(3, 2))], game.GetLegalMoves());
			Assert.Equal(Game.BetterCaptureError, game.TryMove(Sq(2, 5), Sq(4, 7)).Error);
		}

		[Fact]
		public void AnyCapture_EitherCaptureIsLegal()
		{
			var game = new Game(TwoOrOne(), PlayerColor.Black, Preset(GameVariant.American));

			Assert.Equal(2, game.GetLegalMoves().Count);
			Assert.True(game.TryMove(Sq(2, 5), Sq(4, 7)).Success);
		}

		[Fact]
		public void MostPiecesThenKings_PrefersTakingAKing()
		{
			// Each black man can take one piece; only (2,5) takes a king.
			var game = new Game(BoardWith(
				((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn),
				((2, 5), GamePieces.BlackPawn), ((3, 6), GamePieces.WhiteKing)),
				PlayerColor.Black, Preset(GameVariant.Spanish));

			Assert.Equal([new Move(Sq(2, 5), Sq(4, 7), Sq(3, 6))], game.GetLegalMoves());
		}

		[Fact]
		public void Italian_MenCannotCaptureKings()
		{
			var board = () => BoardWith(((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhiteKing), ((7, 6), GamePieces.WhitePawn));

			var italian = new Game(board(), PlayerColor.Black, Preset(GameVariant.Italian));
			var american = new Game(board(), PlayerColor.Black, Preset(GameVariant.American));

			Assert.DoesNotContain(italian.GetLegalMoves(), m => m.IsJump);
			Assert.True(italian.TryMove(Sq(2, 1), Sq(3, 0)).Success);
			Assert.All(american.GetLegalMoves(), m => Assert.True(m.IsJump));
		}

		[Fact]
		public void Italian_EqualCaptures_MustCaptureWithTheKing()
		{
			var game = new Game(BoardWith(
				((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn),
				((2, 5), GamePieces.BlackKing), ((3, 6), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Italian));

			Assert.Equal([new Move(Sq(2, 5), Sq(4, 7), Sq(3, 6))], game.GetLegalMoves());
		}

		[Fact]
		public void Italian_EqualKingCaptures_MustTakeTheMostKings()
		{
			var game = new Game(BoardWith(
				((2, 1), GamePieces.BlackKing), ((3, 2), GamePieces.WhitePawn),
				((2, 5), GamePieces.BlackKing), ((3, 6), GamePieces.WhiteKing)),
				PlayerColor.Black, Preset(GameVariant.Italian));

			Assert.Equal([new Move(Sq(2, 5), Sq(4, 7), Sq(3, 6))], game.GetLegalMoves());
		}

		[Fact]
		public void Italian_StillPrefersMorePiecesOverAKing()
		{
			// The man's double capture beats the king's single one. The white man on (6,3) stops the king
			// from going on to take (5,4) as well, which would make its capture just as long.
			var game = new Game(BoardWith(
				((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((5, 4), GamePieces.WhitePawn),
				((2, 7), GamePieces.BlackKing), ((3, 6), GamePieces.WhitePawn), ((6, 3), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Italian));

			Assert.Equal([new Move(Sq(2, 1), Sq(4, 3), Sq(3, 2))], game.GetLegalMoves());
		}

		[Fact]
		public void Czech_AKingThatCanCaptureMust_EvenOverALongerCaptureByAMan()
		{
			// The man at (2,1) could take two; the king at (1,6) can take (2,5).
			var game = new Game(BoardWith(
				((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((5, 4), GamePieces.WhitePawn),
				((1, 6), GamePieces.BlackKing), ((2, 5), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Czech));

			var legal = game.GetLegalMoves();

			Assert.NotEmpty(legal);
			Assert.All(legal, m => Assert.Equal(Sq(1, 6), m.From));
		}

		// ---- when captured pieces come off ----

		private static Board DoubleJump() => BoardWith(
			((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((5, 4), GamePieces.WhitePawn), ((7, 0), GamePieces.WhitePawn));

		[Fact]
		public void RemoveAtEndOfMove_CapturedPiecesStayUntilTheMoveEnds()
		{
			var game = new Game(DoubleJump(), PlayerColor.Black, Preset(GameVariant.Russian));

			Assert.True(game.TryMove(Sq(2, 1), Sq(4, 3)).Success);
			Assert.Equal(GamePieces.WhitePawn, game.Board[3, 2]);
			Assert.Equal(Sq(4, 3), game.JumpingPiece);

			Assert.True(game.TryMove(Sq(4, 3), Sq(6, 5)).Success);
			Assert.Equal(GamePieces.Empty, game.Board[3, 2]);
			Assert.Equal(GamePieces.Empty, game.Board[5, 4]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		[Fact]
		public void RemoveImmediately_CapturedPieceComesOffAtOnce()
		{
			var game = new Game(DoubleJump(), PlayerColor.Black, Preset(GameVariant.American));

			Assert.True(game.TryMove(Sq(2, 1), Sq(4, 3)).Success);

			Assert.Equal(GamePieces.Empty, game.Board[3, 2]);
		}

		[Fact]
		public void RemoveAtEndOfMove_FlyingKingTakesBothPiecesOfADoubleCapture()
		{
			// The flying king takes (3,4) then (1,2); both stay on the board until the second hop ends the move.
			var rules = Preset(GameVariant.Russian);
			var game = new Game(BoardWith(((4, 5), GamePieces.BlackKing), ((3, 4), GamePieces.WhitePawn), ((1, 2), GamePieces.WhitePawn), ((7, 6), GamePieces.WhitePawn)),
				PlayerColor.Black, rules);

			// Up-left from (4,5): (3,4) white, land on (2,3); from there (1,2) white, land on (0,1).
			Assert.True(game.TryMove(Sq(4, 5), Sq(2, 3)).Success);
			Assert.Equal(Sq(2, 3), game.JumpingPiece);
			Assert.True(game.TryMove(Sq(2, 3), Sq(0, 1)).Success);

			Assert.Equal(GamePieces.Empty, game.Board[3, 4]);
			Assert.Equal(GamePieces.Empty, game.Board[1, 2]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		// ---- crowning mid-capture ----

		// Black man at (5,2) takes (6,3) onto the far row (7,4). A white man two squares up-right at
		// (5,6) can only be reached by a flying king; (6,5) is right next to it for a man jumping back.
		[Fact]
		public void Russian_ManCrownedMidCaptureCarriesOnAsAKing()
		{
			var game = new Game(BoardWith(((5, 2), GamePieces.BlackPawn), ((6, 3), GamePieces.WhitePawn), ((5, 6), GamePieces.WhitePawn), ((0, 7), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Russian));

			Assert.True(game.TryMove(Sq(5, 2), Sq(7, 4)).Success);

			Assert.Equal(GamePieces.BlackKing, game.Board[7, 4]);
			Assert.Equal(Sq(7, 4), game.JumpingPiece);
			Assert.True(game.TryMove(Sq(7, 4), Sq(4, 7)).Success);
			Assert.Equal(GamePieces.BlackKing, game.Board[4, 7]);
		}

		[Fact]
		public void American_ManCrownedMidCaptureEndsItsTurn()
		{
			var game = new Game(BoardWith(((5, 2), GamePieces.BlackPawn), ((6, 3), GamePieces.WhitePawn), ((6, 5), GamePieces.WhitePawn), ((0, 7), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.American));

			Assert.True(game.TryMove(Sq(5, 2), Sq(7, 4)).Success);

			Assert.Equal(GamePieces.BlackKing, game.Board[7, 4]);
			Assert.Null(game.JumpingPiece);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		[Fact]
		public void OnlyAtEndOfMove_ManPassingTheFarRowStaysAMan()
		{
			// Takes (6,3) onto (7,4), then jumps back over (6,5) to (5,6) as a man.
			var game = new Game(BoardWith(((5, 2), GamePieces.BlackPawn), ((6, 3), GamePieces.WhitePawn), ((6, 5), GamePieces.WhitePawn), ((0, 7), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Pool));

			Assert.True(game.TryMove(Sq(5, 2), Sq(7, 4)).Success);
			Assert.Equal(GamePieces.BlackPawn, game.Board[7, 4]);
			Assert.Equal(Sq(7, 4), game.JumpingPiece);

			Assert.True(game.TryMove(Sq(7, 4), Sq(5, 6)).Success);
			Assert.Equal(GamePieces.BlackPawn, game.Board[5, 6]);
		}

		[Fact]
		public void OnlyAtEndOfMove_ManEndingOnTheFarRowIsCrowned()
		{
			var game = new Game(BoardWith(((5, 2), GamePieces.BlackPawn), ((6, 3), GamePieces.WhitePawn), ((0, 7), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.Pool));

			Assert.True(game.TryMove(Sq(5, 2), Sq(7, 4)).Success);

			Assert.Equal(GamePieces.BlackKing, game.Board[7, 4]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		// ---- Thai kings ----

		[Fact]
		public void Thai_CapturingKingLandsRightBehindThePiece()
		{
			var board = () => BoardWith(((0, 1), GamePieces.BlackKing), ((2, 3), GamePieces.WhitePawn), ((7, 0), GamePieces.WhitePawn));

			var thai = new Game(board(), PlayerColor.Black, Preset(GameVariant.Thai));
			var russian = new Game(board(), PlayerColor.Black, Preset(GameVariant.Russian));

			Assert.Equal([Sq(3, 4)], thai.GetLegalMoves().Select(m => m.To));
			// A Russian king may land on any empty square past it: (3,4), (4,5), (5,6) or (6,7).
			Assert.Equal(4, russian.GetLegalMoves().Count);
		}

		// ---- bigger boards ----

		[Fact]
		public void International_MenCrownOnRowNine()
		{
			var game = new Game(BoardWith(10, ((8, 1), GamePieces.BlackPawn), ((0, 9), GamePieces.WhitePawn)),
				PlayerColor.Black, Preset(GameVariant.International));

			Assert.True(game.TryMove(Sq(8, 1), Sq(9, 0)).Success);

			Assert.Equal(GamePieces.BlackKing, game.Board[9, 0]);
		}

		[Fact]
		public void Notation_UsesTheBoardSize()
		{
			var relay = new RelayService { BoardSize = 10 };

			Assert.Equal("BP b10-a9", relay.FormatMove(GamePieces.BlackPawn, [new Checkers.Client.Services.Square(0, 1), new Checkers.Client.Services.Square(1, 0)]));
		}

		// ---- rule validation ----

		[Theory]
		[InlineData(9, 3)]
		[InlineData(6, 2)]
		[InlineData(8, 4)]
		[InlineData(8, 0)]
		public void Validate_RejectsBoardsThatDontWork(int size, int rows)
		{
			Assert.NotNull((GameRuleOptions.Standard with { BoardSize = size, PieceRows = rows }).Validate());
		}
	}
}

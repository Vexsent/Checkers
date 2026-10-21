using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace UnitTests.Shared.GameModels
{
	public class BoardTests
	{
		[Fact]
		public void CreateStandard_Has64Squares()
		{
			var board = Board.CreateStandard();

			Assert.Equal(8, board.Size);
			Assert.Equal(64, board.Size * board.Size);
		}

		[Theory]
		[InlineData(GamePieces.BlackPawn)]
		[InlineData(GamePieces.WhitePawn)]
		public void CreateStandard_Has12PawnsPerSide(GamePieces piece)
		{
			Assert.Equal(12, Pieces(Board.CreateStandard()).Count(p => p.Piece == piece));
		}

		[Fact]
		public void CreateStandard_HasNoKings()
		{
			Assert.DoesNotContain(Pieces(Board.CreateStandard()), p => p.Piece is GamePieces.BlackKing or GamePieces.WhiteKing);
		}

		[Fact]
		public void CreateStandard_PlacesEveryPieceOnADarkSquare()
		{
			Assert.All(Pieces(Board.CreateStandard()), p => Assert.True(Board.IsPlayable(p.Row, p.Col)));
		}

		[Fact]
		public void CreateStandard_BlackFillsTopThreeRows_WhiteFillsBottomThree()
		{
			var board = Board.CreateStandard();

			Assert.All(Pieces(board), p =>
				Assert.Equal(p.Row < 3 ? GamePieces.BlackPawn : GamePieces.WhitePawn, p.Piece));
		}

		[Theory]
		[InlineData(3)]
		[InlineData(4)]
		public void CreateStandard_LeavesMiddleRowsEmpty(int row)
		{
			var board = Board.CreateStandard();

			for (int col = 0; col < board.Size; col++)
				Assert.Equal(GamePieces.Empty, board[row, col]);
		}

		[Fact]
		public void IsPlayable_BottomLeftCornerIsDark()
		{
			// Each player's bottom-left square must be dark (playable).
			Assert.True(Board.IsPlayable(7, 0));
			Assert.True(Board.IsPlayable(0, 7));
		}

		[Fact]
		public void IsPlayable_AlternatesAcrossARow()
		{
			for (int col = 0; col < 7; col++)
				Assert.NotEqual(Board.IsPlayable(0, col), Board.IsPlayable(0, col + 1));
		}

		[Fact]
		public void NewBoard_IsEmpty()
		{
			Assert.Empty(Pieces(new Board()));
		}

		[Fact]
		public void CreateStandard_ScalesToTenByTen()
		{
			// International draughts: 10x10 with 4 rows (20 pieces) per side.
			var board = Board.CreateStandard(10);

			Assert.Equal(20, Pieces(board).Count(p => p.Piece == GamePieces.BlackPawn));
			Assert.Equal(20, Pieces(board).Count(p => p.Piece == GamePieces.WhitePawn));
		}

		private static List<(int Row, int Col, GamePieces Piece)> Pieces(Board board)
		{
			var pieces = new List<(int, int, GamePieces)>();
			for (int r = 0; r < board.Size; r++)
				for (int c = 0; c < board.Size; c++)
					if (board[r, c] != GamePieces.Empty)
						pieces.Add((r, c, board[r, c]));
			return pieces;
		}
	}
}

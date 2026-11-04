using Checkers.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Checkers.Shared.GameModels
{
	public sealed class Board
	{
		public int Size { get; }
		private readonly GamePieces[,] _squares;

		public Board(int size = 8) { Size = size; _squares = new GamePieces[size, size]; }

		public GamePieces this[int row, int col] => _squares[row, col];
		public static bool IsPlayable(int row, int col) => (row + col) % 2 == 1;

		public static Board CreateStandard(int size = 8)
		{
			var board = new Board(size);

			int pieceRows = (size - 2) / 2;

			for (int r = 0; r < size; r++)
				for (int c = 0; c < size; c++)
				{
					if (!IsPlayable(r, c)) continue;
					if (r < pieceRows) board._squares[r, c] = GamePieces.BlackPawn;
					else if (r >= size - pieceRows) board._squares[r, c] = GamePieces.WhitePawn;
				}

			return board;
		}

		/// <summary>
		/// Flattens the board row by row so it can be sent over the wire.
		/// </summary>
		public GamePieces[] ToSquares()
		{
			var squares = new GamePieces[Size * Size];
			for (int r = 0; r < Size; r++)
				for (int c = 0; c < Size; c++)
					squares[r * Size + c] = _squares[r, c];

			return squares;
		}

		/// <summary>
		/// Rebuilds a board from the row-by-row layout produced by <see cref="ToSquares"/>.
		/// </summary>
		public static Board FromSquares(int size, IReadOnlyList<GamePieces> squares)
		{
			if (squares.Count != size * size)
				throw new ArgumentException($"Expected {size * size} squares but got {squares.Count}.", nameof(squares));

			var board = new Board(size);
			for (int r = 0; r < size; r++)
				for (int c = 0; c < size; c++)
					board._squares[r, c] = squares[r * size + c];

			return board;
		}
	}
}

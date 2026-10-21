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
	}
}

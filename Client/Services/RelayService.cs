using System;
using System.Collections.Generic;
using System.Text;
using Checkers.Shared.Enums;

namespace Checkers.Client.Services
{
    internal class RelayService : IRelayService
    {
        private const int BoardSize = 8;

        private readonly List<string> _moves = new();

        public IReadOnlyList<string> Moves => _moves;

        public void AddMove(string move)
        {
            _moves.Add(move);
        }

        public string FormatMove(GamePieces piece, (int Row, int Col) from, (int Row, int Col) to)
        {
            return $"{PieceLetters(piece)} {SquareName(from)}-{SquareName(to)}";
        }

        // Color letter + piece letter, e.g. WhitePawn -> "WP", BlackKing -> "BK".
        private static string PieceLetters(GamePieces piece) => piece switch
        {
            GamePieces.WhitePawn => "WP",
            GamePieces.WhiteKing => "WK",
            GamePieces.BlackPawn => "BP",
            GamePieces.BlackKing => "BK",
            _ => throw new ArgumentException("An empty square has no piece to move.", nameof(piece)),
        };

        // Board position -> letter-number text. Columns 0-7 become a-h; rows count up
        // from white's side at the bottom, so board row 7 is "1" and board row 0 is "8".
        private static string SquareName((int Row, int Col) square)
        {
            char letter = (char)('a' + square.Col);
            int number = BoardSize - square.Row;
            return $"{letter}{number}";
        }
    }
}

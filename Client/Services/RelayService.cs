using System;
using System.Collections.Generic;
using System.Text;
using Checkers.Shared.Enums;

namespace Checkers.Client.Services
{
    internal class RelayService : IRelayService
    {
        /// <inheritdoc />
        public int BoardSize { get; set; } = 8;

        private readonly List<MoveEntry> _moves = new();

        public IReadOnlyList<MoveEntry> Moves => _moves;

        public event Action? MovesChanged;

        public MoveEntry RecordMove(GamePieces piece, IReadOnlyList<Square> path, bool crowned = false, bool? isJump = null)
        {
            // Format first so an invalid move throws before anything is added.
            string text = FormatMove(piece, path, crowned, isJump);

            var entry = new MoveEntry(
                Number: _moves.Count + 1,
                Piece: piece,
                Path: path.ToList(), // copy so the caller can't change it afterwards
                Crowned: crowned,
                PlayedAtUtc: DateTime.UtcNow,
                Text: text);

            AddMove(entry);
            return entry;
        }

        public void AddMove(MoveEntry entry)
        {
            _moves.Add(entry);
            MovesChanged?.Invoke();
        }

        public void LoadMoves(IEnumerable<MoveEntry> entries)
        {
            // Copy before clearing, in case we're handed our own list.
            var incoming = entries.ToList();

            _moves.Clear();
            _moves.AddRange(incoming);
            MovesChanged?.Invoke();
        }

        public void ClearMoves()
        {
            if (_moves.Count == 0)
                return;

            _moves.Clear();
            MovesChanged?.Invoke();
        }

        public string FormatMove(GamePieces piece, IReadOnlyList<Square> path, bool crowned = false, bool? isJump = null)
        {
            if (path.Count < 2)
                throw new ArgumentException("A move needs a starting square and at least one landing square.", nameof(path));

            // Without flying kings a normal move covers one row and a jump two. A flying king can
            // slide or capture across any distance, so callers that know pass isJump.
            bool jump = isJump ?? Math.Abs(path[1].Row - path[0].Row) == 2;
            string separator = jump ? "x" : "-";

            string text = $"{PieceLetters(piece)} {SquareName(path[0])}";
            for (int i = 1; i < path.Count; i++)
            {
                text += separator + SquareName(path[i]);
            }

            // Each landing square in a jump is one capture. Only multi-jumps get a count.
            int captures = jump ? path.Count - 1 : 0;
            if (captures >= 2)
            {
                text += $" (x{captures})";
            }

            if (crowned)
            {
                text += "=K";
            }

            return text;
        }

        private static string PieceLetters(GamePieces piece) => piece switch
        {
            GamePieces.WhitePawn => "WP",
            GamePieces.WhiteKing => "WK",
            GamePieces.BlackPawn => "BP",
            GamePieces.BlackKing => "BK",
            _ => throw new ArgumentException("An empty square has no piece to move.", nameof(piece)),
        };

        // Rows are numbered from white's side, so board row 7 is rank 1.
        private string SquareName(Square square)
        {
            char file = (char)('a' + square.Col);
            int rank = BoardSize - square.Row;
            return $"{file}{rank}";
        }
    }
}

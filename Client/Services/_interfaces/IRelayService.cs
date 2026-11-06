using Checkers.Shared.Enums;

namespace Checkers.Client.Services
{
    /// <summary>
    /// Keeps the list of moves played in the current game, in order.
    /// </summary>
    /// <remarks>
    /// Expected callers:
    /// - Rules code calls <see cref="RecordMove"/> when a turn ends, then sends the returned entry.
    /// - Live session calls <see cref="AddMove"/> for the opponent's moves and <see cref="LoadMoves"/> on reconnect.
    /// - Starting a new game calls <see cref="ClearMoves"/>.
    /// - The move list panel reads <see cref="Moves"/> and refreshes on <see cref="MovesChanged"/>.
    /// </remarks>
    internal interface IRelayService
    {
        /// <summary>All moves so far, oldest first.</summary>
        IReadOnlyList<MoveEntry> Moves { get; }

        /// <summary>Raised whenever the move list changes.</summary>
        event Action? MovesChanged;

        /// <summary>
        /// Records one of our own completed turns. The move number, timestamp and notation
        /// are filled in here. Returns the new entry so it can be sent to the opponent.
        /// </summary>
        /// <exception cref="ArgumentException">The path has fewer than 2 squares, or the piece is Empty.</exception>
        MoveEntry RecordMove(GamePieces piece, IReadOnlyList<Square> path, bool crowned = false);

        /// <summary>Adds an entry that's already complete, e.g. the opponent's move.</summary>
        void AddMove(MoveEntry entry);

        /// <summary>Replaces the whole list, e.g. with the full history after a reconnect.</summary>
        void LoadMoves(IEnumerable<MoveEntry> entries);

        /// <summary>Empties the list for a new game.</summary>
        void ClearMoves();

        /// <summary>
        /// Builds the notation for a move. The path starts with the piece's starting square,
        /// followed by every square it landed on.
        /// Examples: "WP a3-b4", "BP c5xe3", "WK c1xe3xg5 (x2)", "WP b7-a8=K".
        /// </summary>
        string FormatMove(GamePieces piece, IReadOnlyList<Square> path, bool crowned = false);
    }

    /// <summary>
    /// A board position using the same numbering as <c>Board</c>:
    /// row 0 is black's back row, row 7 is white's, column 0 is the "a" file.
    /// </summary>
    internal record Square(int Row, int Col);

    /// <summary>One completed turn in the move list.</summary>
    /// <param name="Number">Move number, starting at 1. Black moves first.</param>
    /// <param name="Piece">The piece as it was at the start of the turn (a pawn that gets crowned is still a pawn here).</param>
    /// <param name="Path">Starting square followed by each landing square.</param>
    /// <param name="Crowned">Whether the piece was crowned this turn.</param>
    /// <param name="PlayedAtUtc">When the turn was completed.</param>
    /// <param name="Text">The move in notation, e.g. "WK c1xe3xg5 (x2)".</param>
    internal record MoveEntry(
        int Number,
        GamePieces Piece,
        IReadOnlyList<Square> Path,
        bool Crowned,
        DateTime PlayedAtUtc,
        string Text);
}

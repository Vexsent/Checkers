using Checkers.Shared.Enums;

namespace Checkers.Client.Services
{
    /// <summary>
    /// The relay: the list of moves recorded during a game, in the order they were played.
    /// </summary>
    internal interface IRelayService
    {
        /// <summary>Every move recorded so far, oldest first. Read-only from the outside.</summary>
        IReadOnlyList<string> Moves { get; }

        /// <summary>Adds one move (e.g. "WP a3-b4") to the end of the list.</summary>
        void AddMove(string move);

        /// <summary>
        /// Turns a simple (non-jump) move into move-list text, e.g. a white pawn
        /// from board row 5, column 0 to row 4, column 1 becomes "WP a3-b4".
        /// </summary>
        string FormatMove(GamePieces piece, (int Row, int Col) from, (int Row, int Col) to);
    }
}
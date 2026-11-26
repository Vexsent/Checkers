using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// The full state of a live match, sent to both players after every change.
	/// </summary>
	public class GameSessionDto
	{
		public string MatchCode { get; set; } = string.Empty;
		public SessionStatus Status { get; set; }
		public GameOutcome Outcome { get; set; }
		public int BlackPlayerId { get; set; }
		public string BlackPlayerName { get; set; } = string.Empty;
		public int? WhitePlayerId { get; set; }
		public string? WhitePlayerName { get; set; }
		public PlayerColor CurrentPlayer { get; set; }
		public int BoardSize { get; set; }
		/// <summary>
		/// Row by row. Use <see cref="Board.FromSquares"/> to get a Board back.
		/// </summary>
		public GamePieces[] Squares { get; set; } = [];
		/// <summary>
		/// Every step played so far, oldest first. A multi-jump is one entry per jump.
		/// </summary>
		public GameMove[] Moves { get; set; } = [];
		/// <summary>
		/// When the second player joined. Null while waiting for an opponent.
		/// </summary>
		public DateTime? StartedAtUtc { get; set; }
		public DateTime? EndedAtUtc { get; set; }
		/// <summary>
		/// The player with an open draw offer, waiting for the opponent to answer.
		/// </summary>
		public PlayerColor? DrawOfferedBy { get; set; }
	}
}

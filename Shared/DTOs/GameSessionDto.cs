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
		public int MoveCount { get; set; }
	}
}

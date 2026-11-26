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
		/// <summary>
		/// How the match ended. Null until it's finished.
		/// </summary>
		public GameEndReason? EndReason { get; set; }
		/// <summary>
		/// Null while that seat is waiting for an opponent. The creator can sit on either side.
		/// </summary>
		public int? BlackPlayerId { get; set; }
		public string? BlackPlayerName { get; set; }
		public int? WhitePlayerId { get; set; }
		public string? WhitePlayerName { get; set; }
		/// <summary>
		/// The side the computer plays, or null when both players are people. That side has no player id.
		/// </summary>
		public PlayerColor? ComputerColor { get; set; }
		public ComputerDifficulty? ComputerDifficulty { get; set; }
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
		/// The rule variations this match is played with.
		/// </summary>
		public GameRuleOptions Rules { get; set; } = GameRuleOptions.Standard;
		/// <summary>
		/// Seconds each player gets per turn, or null for no limit.
		/// </summary>
		public int? TurnTimeLimitSeconds { get; set; }
		/// <summary>
		/// When <see cref="CurrentPlayer"/> automatically resigns if their turn isn't finished.
		/// Null when there's no limit or the match isn't in progress.
		/// </summary>
		public DateTime? TurnDeadlineUtc { get; set; }
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

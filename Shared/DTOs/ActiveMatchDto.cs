using Checkers.Shared.Enums;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// A live match the user is still playing in, seen from their side, so they can rejoin it.
	/// </summary>
	public class ActiveMatchDto
	{
		public string MatchCode { get; set; } = string.Empty;
		public SessionStatus Status { get; set; }
		/// <summary>
		/// Null while the match is still waiting for an opponent.
		/// </summary>
		public string? OpponentName { get; set; }
		/// <summary>
		/// "Black" or "White".
		/// </summary>
		public string PlayedAs { get; set; } = default!;
		public bool VsComputer { get; set; }
		/// <summary>
		/// True when the match is in progress and it's this user's move.
		/// </summary>
		public bool IsYourTurn { get; set; }
		/// <summary>
		/// One-step moves played so far (a multi-jump counts each jump).
		/// </summary>
		public int MoveCount { get; set; }
		/// <summary>
		/// When the opponent joined. Null while waiting for one.
		/// </summary>
		public DateTime? StartedAtUtc { get; set; }
		/// <summary>
		/// When whoever's turn it is automatically resigns. Null with no turn time limit.
		/// </summary>
		public DateTime? TurnDeadlineUtc { get; set; }
	}
}

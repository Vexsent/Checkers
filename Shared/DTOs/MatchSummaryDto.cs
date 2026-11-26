using Checkers.Shared.Enums;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// One row of a user's match history, seen from that user's side of the board.
	/// </summary>
	public class MatchSummaryDto
	{
		public const string ComputerOpponentName = "Computer";

		public int Id { get; set; }
		public string OpponentName { get; set; } = default!;
		public bool VsComputer { get; set; }
		/// <summary>
		/// "Black" or "White".
		/// </summary>
		public string PlayedAs { get; set; } = default!;
		public MatchOutcome Outcome { get; set; }
		public DateTime StartedAtUtc { get; set; }
		public DateTime EndedAtUtc { get; set; }
	}
}

using Checkers.Shared.Enums;

namespace Checkers.Domain.Entities
{
	/// <summary>
	/// A finished game of checkers. A null player id means that side was played by the computer opponent.
	/// </summary>
	public class MatchRecord
	{
		public int Id { get; set; }
		public int? BlackPlayerId { get; set; }
		public virtual User? BlackPlayer { get; set; }
		public int? WhitePlayerId { get; set; }
		public virtual User? WhitePlayer { get; set; }
		public MatchResult Result { get; set; }
		public DateTime StartedAtUtc { get; set; }
		public DateTime EndedAtUtc { get; set; }
		/// <summary>
		/// Seconds each player had per turn, or null for no limit.
		/// </summary>
		public int? TurnTimeLimitSeconds { get; set; }
		/// <summary>
		/// The full rule set as JSON (variant, board size, capture rules...), needed to replay the
		/// match. Null means standard American checkers, which every match before variants was.
		/// </summary>
		public string? RulesJson { get; set; }
		/// <summary>
		/// Every move in the order it was played, kept so the match can be replayed later.
		/// </summary>
		public virtual List<MatchRecordMove> Moves { get; set; } = [];
	}
}

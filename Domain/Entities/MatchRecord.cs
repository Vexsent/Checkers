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
		public User? BlackPlayer { get; set; }
		public int? WhitePlayerId { get; set; }
		public User? WhitePlayer { get; set; }
		public MatchResult Result { get; set; }
		public DateTime StartedAtUtc { get; set; }
		public DateTime EndedAtUtc { get; set; }
		/// <summary>
		/// Every move in the order it was played, kept so the match can be replayed later.
		/// </summary>
		public List<MatchRecordMove> Moves { get; set; } = [];
	}
}

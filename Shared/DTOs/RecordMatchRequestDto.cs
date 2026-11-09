using Checkers.Shared.Enums;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// A finished match to save. Leave a player id null for the side the computer played.
	/// </summary>
	public class RecordMatchRequestDto
	{
		public int? BlackPlayerId { get; set; }
		public int? WhitePlayerId { get; set; }
		public MatchResult Result { get; set; }
		public DateTime StartedAtUtc { get; set; }
		public DateTime EndedAtUtc { get; set; }
		/// <summary>
		/// Moves in the order they were played.
		/// </summary>
		public List<MatchMoveDto> Moves { get; set; } = [];
	}
}

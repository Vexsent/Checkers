using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

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
		/// <summary>
		/// The rule variations the match was played with, needed to replay its moves.
		/// </summary>
		public GameRuleOptions Rules { get; set; } = GameRuleOptions.Standard;
		public int? TurnTimeLimitSeconds { get; set; }
	}
}

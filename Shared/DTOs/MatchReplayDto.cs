using Checkers.Shared.GameModels;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// One saved match with every move, so it can be replayed. Seen from the requesting user's side,
	/// like the <see cref="MatchSummaryDto"/> it extends.
	/// </summary>
	public class MatchReplayDto : MatchSummaryDto
	{
		/// <summary>
		/// The black player's username, or <see cref="MatchSummaryDto.ComputerOpponentName"/>.
		/// </summary>
		public string BlackPlayerName { get; set; } = default!;
		/// <summary>
		/// The white player's username, or <see cref="MatchSummaryDto.ComputerOpponentName"/>.
		/// </summary>
		public string WhitePlayerName { get; set; } = default!;
		/// <summary>
		/// Every step in the order it was played. A multi-jump is one entry per jump.
		/// </summary>
		public List<MatchMoveDto> Moves { get; set; } = [];
		/// <summary>
		/// The rule variations the match was played with. The replay must use the same ones.
		/// </summary>
		public GameRuleOptions Rules { get; set; } = GameRuleOptions.Standard;
		public int? TurnTimeLimitSeconds { get; set; }
	}
}

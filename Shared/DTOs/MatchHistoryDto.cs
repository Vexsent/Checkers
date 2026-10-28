namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// A user's match history: their win/loss/draw record and every match, newest first.
	/// </summary>
	public class MatchHistoryDto
	{
		public int Wins { get; set; }
		public int Losses { get; set; }
		public int Draws { get; set; }
		public List<MatchSummaryDto> Matches { get; set; } = [];
	}
}

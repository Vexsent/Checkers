namespace Checkers.Domain.Entities
{
	/// <summary>
	/// One move of a <see cref="MatchRecord"/>: a piece going from one square to another. A multi-jump is one move per hop.
	/// </summary>
	public class MatchRecordMove
	{
		public int Id { get; set; }
		public int MatchId { get; set; }
		/// <summary>
		/// 1-based position of this move in the match.
		/// </summary>
		public int MoveNumber { get; set; }
		public int FromRow { get; set; }
		public int FromCol { get; set; }
		public int ToRow { get; set; }
		public int ToCol { get; set; }
	}
}

namespace Checkers.Application.Core
{
	public class RecordMatchResult
	{
		public bool Success { get; private init; }
		public IReadOnlyList<string> Errors { get; private init; } = [];
		public int? MatchId { get; private init; }

		public static RecordMatchResult Succeeded(int matchId) =>
			new() { Success = true, MatchId = matchId };

		public static RecordMatchResult Failed(IEnumerable<string> errors) =>
			new() { Success = false, Errors = errors.ToList() };
	}
}

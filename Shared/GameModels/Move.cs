namespace Checkers.Shared.GameModels
{
	public sealed record Move(Square From, Square To, Square? Captured = null)
	{
		public bool IsJump => Captured is not null;
	}

	public sealed record MoveResult(bool Success, string? Error = null)
	{
		public static readonly MoveResult Ok = new(true);
		public static MoveResult Fail(string error) => new(false, error);
	}
}

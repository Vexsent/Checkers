using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// What a live session needs from a ruleset. Sessions never interpret moves themselves, so
	/// turn order, forced jumps, multi-jumps, kinging and win conditions all belong behind this.
	/// </summary>
	public interface IGameRules
	{
		PlayerColor FirstPlayer { get; }
		Board CreateInitialBoard();
		/// <summary>
		/// Applies one step for <paramref name="mover"/>. A multi-jump that can continue returns
		/// <paramref name="mover"/> as the next player.
		/// </summary>
		MoveResult ApplyMove(Board board, PlayerColor mover, GameMove move);
	}

	public sealed record MoveResult(bool IsLegal, Board Board, PlayerColor NextPlayer, GameOutcome Outcome, string? Error = null)
	{
		public static MoveResult Illegal(Board board, PlayerColor mover, string error) =>
			new(false, board, mover, GameOutcome.InProgress, error);
	}
}

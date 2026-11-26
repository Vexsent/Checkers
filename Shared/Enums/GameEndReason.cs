namespace Checkers.Shared.Enums
{
	public enum GameEndReason
	{
		//The loser had no pieces left or no legal move.
		NoLegalMoves,
		Resignation,
		DrawAgreed,
		//The loser didn't finish their turn within the match's turn time limit.
		Timeout
	}
}

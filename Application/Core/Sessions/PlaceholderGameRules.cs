using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// TEMPORARY stand-in so sessions can run before the real rules engine lands. It only moves a
	/// player's own piece onto an empty playable square: no direction checks, captures, kinging or
	/// win detection. Replace its registration in AddGameSessions with the real IGameRules.
	/// </summary>
	public class PlaceholderGameRules : IGameRules
	{
		public PlayerColor FirstPlayer => PlayerColor.Black;

		public Board CreateInitialBoard() => Board.CreateStandard();

		public MoveResult ApplyMove(Board board, PlayerColor mover, GameMove move)
		{
			if (!IsOnBoard(board, move.FromRow, move.FromCol) || !IsOnBoard(board, move.ToRow, move.ToCol))
				return MoveResult.Illegal(board, mover, "That square is not on the board.");

			var piece = board[move.FromRow, move.FromCol];
			if (!Board.BelongsTo(piece, mover))
				return MoveResult.Illegal(board, mover, "You can only move your own pieces.");

			if (!Board.IsPlayable(move.ToRow, move.ToCol) || board[move.ToRow, move.ToCol] != GamePieces.Empty)
				return MoveResult.Illegal(board, mover, "That square is not available.");

			var squares = board.ToSquares();
			squares[move.FromRow * board.Size + move.FromCol] = GamePieces.Empty;
			squares[move.ToRow * board.Size + move.ToCol] = piece;

			var next = mover == PlayerColor.Black ? PlayerColor.White : PlayerColor.Black;
			return new MoveResult(true, Board.FromSquares(board.Size, squares), next, GameOutcome.InProgress);
		}

		private static bool IsOnBoard(Board board, int row, int col) =>
			row >= 0 && row < board.Size && col >= 0 && col < board.Size;
	}
}

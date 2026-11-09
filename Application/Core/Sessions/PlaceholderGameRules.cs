using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// TEMPORARY stand-in so sessions can run before the real rules engine lands. It only moves a
	/// player's own piece onto an empty playable square: no direction checks, captures, kinging or
	/// win detection. Once Game.cs is merged, replace its registration in AddGameSessions with an
	/// IGameRules whose IRulesGame wraps a Game (TryMove maps to Game.TryMove with two Squares).
	/// </summary>
	public class PlaceholderGameRules : IGameRules
	{
		public IRulesGame NewGame() => new PlaceholderGame();

		private sealed class PlaceholderGame : IRulesGame
		{
			public Board Board { get; } = Board.CreateStandard();
			public PlayerColor CurrentTurn { get; private set; } = PlayerColor.Black;
			public GameOutcome Outcome => GameOutcome.InProgress;

			public string? TryMove(GameMove move)
			{
				var from = new Square(move.FromRow, move.FromCol);
				var to = new Square(move.ToRow, move.ToCol);

				if (!Board.IsOnBoard(from) || !Board.IsOnBoard(to))
					return "That square is not on the board.";

				var piece = Board[from];
				if (!Board.BelongsTo(piece, CurrentTurn))
					return "You can only move your own pieces.";

				if (!Board.IsPlayable(to.Row, to.Col) || Board[to] != GamePieces.Empty)
					return "That square is not available.";

				Board[from] = GamePieces.Empty;
				Board[to] = piece;
				CurrentTurn = CurrentTurn == PlayerColor.Black ? PlayerColor.White : PlayerColor.Black;
				return null;
			}
		}
	}
}

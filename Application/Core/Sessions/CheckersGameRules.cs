using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// Plays live matches with the shared rules engine, so forced jumps, multi-jumps, kinging and
	/// win detection work the same online as they do in a local game.
	/// </summary>
	public class CheckersGameRules : IGameRules
	{
		public IRulesGame NewGame() => Wrap(new Game());

		/// <summary>
		/// Wraps a game that is already set up, e.g. a test position.
		/// </summary>
		public static IRulesGame Wrap(Game game) => new RulesGame(game);

		private sealed class RulesGame(Game game) : IRulesGame
		{
			public Board Board => game.Board;
			public PlayerColor CurrentTurn => game.CurrentTurn;

			public GameOutcome Outcome => !game.IsOver
				? GameOutcome.InProgress
				: game.Winner switch
				{
					PlayerColor.Black => GameOutcome.BlackWon,
					PlayerColor.White => GameOutcome.WhiteWon,
					_ => GameOutcome.Draw,
				};

			public string? TryMove(GameMove move)
			{
				var result = game.TryMove(new Square(move.FromRow, move.FromCol), new Square(move.ToRow, move.ToCol));
				return result.Success ? null : result.Error ?? Game.IllegalMoveError;
			}
		}
	}
}

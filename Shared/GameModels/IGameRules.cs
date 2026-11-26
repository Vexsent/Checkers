using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// Starts the games that live sessions play. Sessions never interpret moves themselves, so
	/// turn order, forced jumps, multi-jumps, kinging and win conditions all belong behind this.
	/// </summary>
	public interface IGameRules
	{
		IRulesGame NewGame();
	}

	/// <summary>
	/// One game in progress, shaped like the rules engine's <c>Game</c> class so it can wrap one directly.
	/// </summary>
	public interface IRulesGame
	{
		Board Board { get; }
		PlayerColor CurrentTurn { get; }
		GameOutcome Outcome { get; }
		/// <summary>
		/// Plays one step for <see cref="CurrentTurn"/>. A multi-jump that can continue leaves
		/// <see cref="CurrentTurn"/> unchanged. Returns null on success, otherwise why the move was refused.
		/// </summary>
		string? TryMove(GameMove move);
	}
}

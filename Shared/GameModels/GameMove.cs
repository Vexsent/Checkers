namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// One step of a turn. A multi-jump is sent as one GameMove per jump.
	/// </summary>
	public readonly record struct GameMove(int FromRow, int FromCol, int ToRow, int ToCol);
}

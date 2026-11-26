namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// Rule variations a match can be played with. The defaults are standard American checkers.
	/// </summary>
	public sealed record GameRuleOptions
	{
		public static GameRuleOptions Standard { get; } = new();

		/// <summary>
		/// When true, a player who can capture must. When false, capturing is optional, but a piece
		/// that has started a multi-jump must still finish it.
		/// </summary>
		public bool ForcedCaptures { get; init; } = true;

		/// <summary>
		/// When true, kings move any number of empty squares along a diagonal, and capture a piece
		/// any distance away, landing on any empty square beyond it.
		/// </summary>
		public bool FlyingKings { get; init; }
	}
}

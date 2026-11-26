namespace Checkers.Shared.Enums
{
	/// <summary>
	/// How strongly the computer opponent plays.
	/// </summary>
	public enum ComputerDifficulty
	{
		/// <summary>Picks any legal move at random.</summary>
		Beginner,
		/// <summary>Looks one turn ahead and often misjudges.</summary>
		Easy,
		/// <summary>Looks three turns ahead.</summary>
		Medium,
		/// <summary>Looks five turns ahead.</summary>
		Hard,
		/// <summary>Searches as deep as it can in a couple of seconds.</summary>
		Expert
	}
}

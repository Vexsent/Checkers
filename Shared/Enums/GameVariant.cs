namespace Checkers.Shared.Enums
{
	/// <summary>
	/// A named set of rules. Custom is any combination that doesn't match a preset.
	/// </summary>
	public enum GameVariant
	{
		American,
		Casual,
		Russian,
		Pool,
		Brazilian,
		International,
		Canadian,
		Spanish,
		Italian,
		Czech,
		Thai,
		Custom
	}

	/// <summary>
	/// Which capture a player must choose when they have several.
	/// </summary>
	public enum CaptureChoice
	{
		/// <summary>Any capture sequence, as long as it's carried through to the end.</summary>
		Any,
		/// <summary>The sequence that takes the most pieces.</summary>
		MostPieces,
		/// <summary>The most pieces, then (Spanish) the most kings among them.</summary>
		MostPiecesThenKings,
		/// <summary>
		/// Italian order: the most pieces, then capturing with a king, then the most kings, then the
		/// sequence that takes kings earliest.
		/// </summary>
		Italian,
		/// <summary>(Czech) if a king can capture it must; otherwise any sequence.</summary>
		KingsFirst
	}

	/// <summary>
	/// What happens when a man reaches the far row in the middle of a capture.
	/// </summary>
	public enum PromotionRule
	{
		/// <summary>It's crowned and the turn ends there (American).</summary>
		EndsTurn,
		/// <summary>It's crowned and carries on capturing as a king (Russian).</summary>
		ContinuesAsKing,
		/// <summary>
		/// It carries on capturing as a man, and is crowned only if the move ends on the far row
		/// (International, Brazilian, Pool).
		/// </summary>
		OnlyAtEndOfMove
	}
}

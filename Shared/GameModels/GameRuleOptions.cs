using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// The rules a match is played with. The defaults are standard American checkers;
	/// <see cref="GameVariants"/> has the presets for other variants.
	/// </summary>
	public sealed record GameRuleOptions
	{
		public static GameRuleOptions Standard { get; } = new();

		/// <summary>
		/// The preset these rules came from, or Custom once any rule has been changed.
		/// </summary>
		public GameVariant Variant { get; init; } = GameVariant.American;

		/// <summary>
		/// Squares along each side: 8, 10 or 12.
		/// </summary>
		public int BoardSize { get; init; } = 8;

		/// <summary>
		/// Rows of men each side starts with (3 on 8×8 gives 12 men each).
		/// </summary>
		public int PieceRows { get; init; } = 3;

		/// <summary>
		/// The side that moves first. Black starts at the top of the board, White at the bottom.
		/// </summary>
		public PlayerColor FirstPlayer { get; init; } = PlayerColor.Black;

		/// <summary>
		/// When true, a player who can capture must. When false, capturing is optional, but a piece
		/// that has started a multi-jump must still finish it.
		/// </summary>
		public bool ForcedCaptures { get; init; } = true;

		/// <summary>
		/// When true, kings move any number of empty squares along a diagonal, and capture a piece
		/// any distance away.
		/// </summary>
		public bool FlyingKings { get; init; }

		/// <summary>
		/// With flying kings, whether a capturing king must land on the square right behind the
		/// captured piece (Thai) instead of any empty square beyond it.
		/// </summary>
		public bool KingStopsBehindCapture { get; init; }

		/// <summary>
		/// Whether men may capture backwards as well as forwards. They always move forwards only.
		/// </summary>
		public bool MenCaptureBackward { get; init; }

		/// <summary>
		/// False (Italian) means only a king can capture a king.
		/// </summary>
		public bool MenCanCaptureKings { get; init; } = true;

		/// <summary>
		/// Which capture a player must choose when they have several.
		/// </summary>
		public CaptureChoice CaptureChoice { get; init; } = CaptureChoice.Any;

		/// <summary>
		/// What happens when a man reaches the far row partway through a capture.
		/// </summary>
		public PromotionRule PromotionDuringCapture { get; init; } = PromotionRule.EndsTurn;

		/// <summary>
		/// When true, captured pieces stay on the board until the capturing move ends (they still
		/// block, and can't be jumped twice). When false they're removed as soon as they're jumped.
		/// </summary>
		public bool RemoveCapturedAtEndOfMove { get; init; }

		public int PiecesPerSide => PieceRows * BoardSize / 2;

		/// <summary>
		/// Why these rules can't be played, or null if they're fine.
		/// </summary>
		public string? Validate()
		{
			if (BoardSize is not (8 or 10 or 12))
				return "The board must be 8×8, 10×10 or 12×12.";

			if (PieceRows < 1 || PieceRows > BoardSize / 2 - 1)
				return "Each side needs at least one row of men and an empty gap between the sides.";

			if (!Enum.IsDefined(Variant) || !Enum.IsDefined(FirstPlayer) || !Enum.IsDefined(CaptureChoice) || !Enum.IsDefined(PromotionDuringCapture))
				return "Unknown rule setting.";

			return null;
		}
	}
}

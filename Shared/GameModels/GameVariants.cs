using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// One named rule set the match creator can pick.
	/// </summary>
	public sealed record VariantPreset(GameVariant Variant, string Name, string Description, GameRuleOptions Rules);

	/// <summary>
	/// The rule presets for the national variants of checkers/draughts. Draw rules (move limits,
	/// repetition) and board mirroring aren't modelled; everything else that decides which moves
	/// are legal is.
	/// </summary>
	public static class GameVariants
	{
		public static IReadOnlyList<VariantPreset> All { get; } =
		[
			new(GameVariant.American, "American checkers",
				"English draughts. Men move and capture forwards, kings move one square, captures are compulsory.",
				GameRuleOptions.Standard),

			new(GameVariant.Casual, "Casual",
				"American checkers with optional captures. A multi-jump that's started must still be finished.",
				GameRuleOptions.Standard with { Variant = GameVariant.Casual, ForcedCaptures = false }),

			new(GameVariant.Russian, "Russian (shashki)",
				"White moves first. Men capture backwards too, kings fly, and a man crowned mid-capture carries on as a king.",
				new GameRuleOptions
				{
					Variant = GameVariant.Russian,
					FirstPlayer = PlayerColor.White,
					MenCaptureBackward = true,
					FlyingKings = true,
					PromotionDuringCapture = PromotionRule.ContinuesAsKing,
					RemoveCapturedAtEndOfMove = true,
				}),

			new(GameVariant.Pool, "Pool checkers",
				"Men capture backwards too and kings fly. A man is only crowned if its move ends on the far row.",
				new GameRuleOptions
				{
					Variant = GameVariant.Pool,
					MenCaptureBackward = true,
					FlyingKings = true,
					PromotionDuringCapture = PromotionRule.OnlyAtEndOfMove,
					RemoveCapturedAtEndOfMove = true,
				}),

			new(GameVariant.Brazilian, "Brazilian",
				"International rules on an 8×8 board: white first, flying kings, men capture backwards, and you must take the most pieces.",
				International(GameVariant.Brazilian, boardSize: 8, pieceRows: 3)),

			new(GameVariant.International, "International (10×10)",
				"The main competitive variant: 10×10 board, 20 men each, flying kings, and you must take the most pieces.",
				International(GameVariant.International, boardSize: 10, pieceRows: 4)),

			new(GameVariant.Canadian, "Canadian (12×12)",
				"International rules on a 12×12 board with 30 men each.",
				International(GameVariant.Canadian, boardSize: 12, pieceRows: 5)),

			new(GameVariant.Spanish, "Spanish",
				"White first. Men capture forwards only, kings fly, and you must take the most pieces, then the most kings.",
				new GameRuleOptions
				{
					Variant = GameVariant.Spanish,
					FirstPlayer = PlayerColor.White,
					FlyingKings = true,
					CaptureChoice = CaptureChoice.MostPiecesThenKings,
					RemoveCapturedAtEndOfMove = true,
				}),

			new(GameVariant.Italian, "Italian",
				"White first, kings move one square, men can't capture kings, and strict rules decide which capture you must take.",
				new GameRuleOptions
				{
					Variant = GameVariant.Italian,
					FirstPlayer = PlayerColor.White,
					MenCanCaptureKings = false,
					CaptureChoice = CaptureChoice.Italian,
				}),

			new(GameVariant.Czech, "Czech",
				"White first, men capture forwards only, kings fly, and if a king can capture it must.",
				new GameRuleOptions
				{
					Variant = GameVariant.Czech,
					FirstPlayer = PlayerColor.White,
					FlyingKings = true,
					CaptureChoice = CaptureChoice.KingsFirst,
					RemoveCapturedAtEndOfMove = true,
				}),

			new(GameVariant.Thai, "Thai",
				"8 men each on two rows. Kings fly but land right behind the piece they capture.",
				new GameRuleOptions
				{
					Variant = GameVariant.Thai,
					PieceRows = 2,
					FlyingKings = true,
					KingStopsBehindCapture = true,
				}),
		];

		/// <summary>
		/// The preset for <paramref name="variant"/>, or null for Custom.
		/// </summary>
		public static VariantPreset? Find(GameVariant variant) => All.FirstOrDefault(p => p.Variant == variant);

		/// <summary>
		/// The display name for a rule set: its preset's name, or "Custom".
		/// </summary>
		public static string NameOf(GameRuleOptions rules) => Find(rules.Variant)?.Name ?? "Custom";

		private static GameRuleOptions International(GameVariant variant, int boardSize, int pieceRows) => new()
		{
			Variant = variant,
			BoardSize = boardSize,
			PieceRows = pieceRows,
			FirstPlayer = PlayerColor.White,
			MenCaptureBackward = true,
			FlyingKings = true,
			CaptureChoice = CaptureChoice.MostPieces,
			PromotionDuringCapture = PromotionRule.OnlyAtEndOfMove,
			RemoveCapturedAtEndOfMove = true,
		};
	}
}

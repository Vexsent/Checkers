using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Client.Pages
{
	/// <summary>
	/// The create screen's rule picker: a preset, plus every rule as its own control. Changing a
	/// rule turns the rules into Custom, unless the result is exactly another preset's rules.
	/// </summary>
	public partial class Match
	{
		private sealed record VariantChoice(GameVariant Variant, string Name);
		private sealed record Choice<T>(T Value, string Text);

		private static readonly VariantChoice[] VariantChoices =
		[
			.. GameVariants.All.Select(p => new VariantChoice(p.Variant, p.Name)),
			new(GameVariant.Custom, "Custom"),
		];

		private static readonly Choice<int>[] BoardSizes = [new(8, "8 × 8"), new(10, "10 × 10"), new(12, "12 × 12")];

		private static readonly Choice<CaptureChoice>[] CaptureChoices =
		[
			new(CaptureChoice.Any, "Any capture, carried through to the end"),
			new(CaptureChoice.MostPieces, "Must take the most pieces"),
			new(CaptureChoice.MostPiecesThenKings, "Most pieces, then the most kings (Spanish)"),
			new(CaptureChoice.Italian, "Italian priority (most pieces, with a king, most kings, kings first)"),
			new(CaptureChoice.KingsFirst, "A king that can capture must (Czech)"),
		];

		private static readonly Choice<PromotionRule>[] PromotionChoices =
		[
			new(PromotionRule.EndsTurn, "Is crowned and the move ends"),
			new(PromotionRule.ContinuesAsKing, "Is crowned and keeps capturing as a king"),
			new(PromotionRule.OnlyAtEndOfMove, "Keeps capturing as a man; crowned only if it ends there"),
		];

		private GameVariant SelectedVariant
		{
			get => rules.Variant;
			set => rules = GameVariants.Find(value)?.Rules ?? rules with { Variant = GameVariant.Custom };
		}

		private string VariantDescription => GameVariants.Find(rules.Variant)?.Description
			?? string.Join(" · ", Services.MatchTimeText.RuleDetails(rules) is { Count: > 0 } details ? details : ["Same as American checkers"]);

		private string ColorLabel(PlayerColor color) =>
			color.ToString() + (rules.FirstPlayer == color ? " (moves first)" : "");

		private int BoardSize
		{
			get => rules.BoardSize;
			set => Change(rules with { BoardSize = value, PieceRows = (value - 2) / 2 });
		}

		private int PieceRows
		{
			get => rules.PieceRows;
			set => Change(rules with { PieceRows = Math.Clamp(value, 1, rules.BoardSize / 2 - 1) });
		}

		private PlayerColor FirstPlayer { get => rules.FirstPlayer; set => Change(rules with { FirstPlayer = value }); }
		private bool ForcedCaptures { get => rules.ForcedCaptures; set => Change(rules with { ForcedCaptures = value }); }
		private bool MenCaptureBackward { get => rules.MenCaptureBackward; set => Change(rules with { MenCaptureBackward = value }); }
		private bool MenCanCaptureKings { get => rules.MenCanCaptureKings; set => Change(rules with { MenCanCaptureKings = value }); }
		private bool KingStopsBehindCapture { get => rules.KingStopsBehindCapture; set => Change(rules with { KingStopsBehindCapture = value }); }
		private bool RemoveCapturedAtEndOfMove { get => rules.RemoveCapturedAtEndOfMove; set => Change(rules with { RemoveCapturedAtEndOfMove = value }); }
		private CaptureChoice CaptureChoiceValue { get => rules.CaptureChoice; set => Change(rules with { CaptureChoice = value }); }
		private PromotionRule Promotion { get => rules.PromotionDuringCapture; set => Change(rules with { PromotionDuringCapture = value }); }

		private bool FlyingKings
		{
			get => rules.FlyingKings;
			// "Lands behind the capture" only means something for flying kings.
			set => Change(rules with { FlyingKings = value, KingStopsBehindCapture = value && rules.KingStopsBehindCapture });
		}

		private void Change(GameRuleOptions changed)
		{
			var custom = changed with { Variant = GameVariant.Custom };
			rules = GameVariants.All.FirstOrDefault(p => p.Rules with { Variant = GameVariant.Custom } == custom)?.Rules ?? custom;
		}
	}
}

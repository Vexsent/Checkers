using Checkers.Client.Services;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;

namespace Checkers.Client.Pages
{
	/// <summary>
	/// Explains the variants and each rule. The tables and cards are built from <see cref="GameVariants"/>,
	/// so they always match the rules the game actually plays.
	/// </summary>
	public partial class Rules
	{
		[Inject]
		private NavigationManager Navigation { get; set; } = default!;

		private void Play(GameVariant variant) => Navigation.NavigateTo($"match?variant={variant}");

		private static string YesNo(bool value) => value ? "Yes" : "No";

		private static string KingsShort(GameRuleOptions rules) => !rules.FlyingKings
			? "One square"
			: rules.KingStopsBehindCapture ? "Flying, land behind capture" : "Flying";

		private static string CaptureChoiceShort(CaptureChoice choice) => choice switch
		{
			CaptureChoice.MostPieces => "Most pieces",
			CaptureChoice.MostPiecesThenKings => "Most pieces, then kings",
			CaptureChoice.Italian => "Italian priority",
			CaptureChoice.KingsFirst => "Kings first",
			_ => "Any",
		};

		private static string PromotionShort(PromotionRule rule) => rule switch
		{
			PromotionRule.ContinuesAsKing => "Keeps capturing as a king",
			PromotionRule.OnlyAtEndOfMove => "Only if the move ends there",
			_ => "Crowned, move ends",
		};

		/// <summary>
		/// What sets the variant apart from American checkers, or a note that it is the reference.
		/// </summary>
		private static List<string> Details(GameRuleOptions rules)
		{
			var details = MatchTimeText.RuleDetails(rules);
			return details.Count > 0 ? details : ["The standard rules the other variants are compared with."];
		}
	}
}

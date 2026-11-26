using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Client.Services
{
	/// <summary>
	/// Turns time limits, countdowns and rule variations into short text for the match pages.
	/// </summary>
	public static class MatchTimeText
	{
		/// <summary>
		/// A turn time limit, e.g. "30 seconds", "5 minutes", "1 day 12 hours".
		/// </summary>
		public static string Limit(int seconds)
		{
			var span = TimeSpan.FromSeconds(seconds);
			var parts = new List<string>();
			if (span.Days > 0) parts.Add(Plural(span.Days, "day"));
			if (span.Hours > 0) parts.Add(Plural(span.Hours, "hour"));
			if (span.Minutes > 0) parts.Add(Plural(span.Minutes, "minute"));
			if (span.Seconds > 0) parts.Add(Plural(span.Seconds, "second"));
			return string.Join(" ", parts);
		}

		/// <summary>
		/// Time left on a turn: "0:42" under an hour, "3h 05m" under a day, "2d 4h" beyond. Never negative.
		/// </summary>
		public static string Remaining(TimeSpan remaining)
		{
			if (remaining < TimeSpan.Zero)
				remaining = TimeSpan.Zero;

			if (remaining.TotalDays >= 1)
				return $"{(int)remaining.TotalDays}d {remaining.Hours}h";

			if (remaining.TotalHours >= 1)
				return $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m";

			return $"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}";
		}

		/// <summary>
		/// The match's rules in one line, e.g. "Russian (shashki) · 5 minutes per turn". A custom rule
		/// set lists what differs from American checkers instead of a name.
		/// </summary>
		public static string DescribeRules(GameRuleOptions? rules, int? turnTimeLimitSeconds)
		{
			rules ??= GameRuleOptions.Standard;
			List<string> parts = rules.Variant == GameVariant.Custom
				? ["Custom", .. RuleDetails(rules)]
				: [GameVariants.NameOf(rules)];
			parts.Add(turnTimeLimitSeconds is int limit ? $"{Limit(limit)} per turn" : "No turn time limit");
			return string.Join(" · ", parts);
		}

		/// <summary>
		/// Each rule that differs from American checkers, in a few words.
		/// </summary>
		public static List<string> RuleDetails(GameRuleOptions rules)
		{
			var details = new List<string>();
			if (rules.BoardSize != 8 || rules.PieceRows != 3)
				details.Add($"{rules.BoardSize}×{rules.BoardSize}, {rules.PiecesPerSide} men each");
			if (rules.FirstPlayer == PlayerColor.White) details.Add("White moves first");
			if (!rules.ForcedCaptures) details.Add("Optional captures");
			if (rules.FlyingKings) details.Add(rules.KingStopsBehindCapture ? "Flying kings (land behind capture)" : "Flying kings");
			if (rules.MenCaptureBackward) details.Add("Men capture backwards");
			if (!rules.MenCanCaptureKings) details.Add("Men can't capture kings");
			details.Add(rules.CaptureChoice switch
			{
				CaptureChoice.MostPieces => "Must take the most pieces",
				CaptureChoice.MostPiecesThenKings => "Must take the most pieces, then kings",
				CaptureChoice.Italian => "Italian capture priority",
				CaptureChoice.KingsFirst => "Kings must capture first",
				_ => "",
			});
			details.Add(rules.PromotionDuringCapture switch
			{
				PromotionRule.ContinuesAsKing => "Crowned mid-capture, keeps capturing",
				PromotionRule.OnlyAtEndOfMove => "Crowned only at the end of a move",
				_ => "",
			});
			if (rules.RemoveCapturedAtEndOfMove) details.Add("Captured pieces removed after the move");
			return details.Where(d => d.Length > 0).ToList();
		}

		private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
	}
}

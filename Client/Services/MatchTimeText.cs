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
		/// The match's rules in one line, e.g. "Forced captures · Flying kings · 5 minutes per turn".
		/// </summary>
		public static string DescribeRules(GameRuleOptions? rules, int? turnTimeLimitSeconds)
		{
			rules ??= GameRuleOptions.Standard;
			var parts = new List<string>
			{
				rules.ForcedCaptures ? "Forced captures" : "Optional captures",
				rules.FlyingKings ? "Flying kings" : "Standard kings",
				turnTimeLimitSeconds is int limit ? $"{Limit(limit)} per turn" : "No turn time limit",
			};
			return string.Join(" · ", parts);
		}

		private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
	}
}

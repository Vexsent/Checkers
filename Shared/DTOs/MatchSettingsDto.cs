using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Shared.DTOs
{
	/// <summary>
	/// What the creator picks when starting a live match.
	/// </summary>
	public class MatchSettingsDto
	{
		public const int MinTurnTimeLimitSeconds = 30;
		public const int MaxTurnTimeLimitSeconds = 7 * 24 * 60 * 60;

		public const string TurnTimeLimitError = "The turn time limit must be between 30 seconds and 1 week.";

		public ColorChoice CreatorColor { get; set; } = ColorChoice.Black;
		public GameRuleOptions Rules { get; set; } = GameRuleOptions.Standard;
		/// <summary>
		/// How long each player gets to finish a turn before they automatically resign. Null for no limit.
		/// </summary>
		public int? TurnTimeLimitSeconds { get; set; }

		/// <summary>
		/// Why these settings can't be used, or null if they're fine.
		/// </summary>
		public string? Validate()
		{
			if (!Enum.IsDefined(CreatorColor))
				return "Unknown color choice.";

			if (TurnTimeLimitSeconds is int limit && (limit < MinTurnTimeLimitSeconds || limit > MaxTurnTimeLimitSeconds))
				return TurnTimeLimitError;

			return null;
		}
	}
}

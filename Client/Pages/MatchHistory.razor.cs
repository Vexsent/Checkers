using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Checkers.Client.Pages
{
	public partial class MatchHistory
	{
		[Inject]
		private IMatchHistoryServices MatchHistoryServices { get; set; } = default!;

		private MatchHistoryDto? history;
		private bool loading = true;

		protected override async Task OnInitializedAsync()
		{
			history = await MatchHistoryServices.GetHistoryAsync();
			loading = false;
		}

		private static BadgeStyle OutcomeStyle(MatchOutcome outcome) => outcome switch
		{
			MatchOutcome.Win => BadgeStyle.Success,
			MatchOutcome.Loss => BadgeStyle.Danger,
			_ => BadgeStyle.Secondary,
		};

		private static string Duration(MatchSummaryDto match)
		{
			var length = match.EndedAtUtc - match.StartedAtUtc;
			return length.TotalHours >= 1 ? length.ToString(@"h\:mm\:ss") : length.ToString(@"m\:ss");
		}
	}
}

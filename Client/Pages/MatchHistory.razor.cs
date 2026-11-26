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

		[Inject]
		private NavigationManager Navigation { get; set; } = default!;

		[Inject]
		private DialogService DialogService { get; set; } = default!;

		private MatchHistoryDto? history;
		private List<ActiveMatchDto>? activeMatches;
		private string? activeError;
		private bool loading = true;
		private bool busy;

		protected override async Task OnInitializedAsync()
		{
			await LoadAsync();
			loading = false;
		}

		private async Task LoadAsync()
		{
			var historyTask = MatchHistoryServices.GetHistoryAsync();
			var activeTask = MatchHistoryServices.GetActiveMatchesAsync();
			history = await historyTask;
			activeMatches = await activeTask;
		}

		private async Task Surrender(ActiveMatchDto match)
		{
			var opponent = match.OpponentName ?? "your opponent";
			var confirmed = await DialogService.Confirm(
				$"Surrender match {match.MatchCode}? {opponent} will win, and the game will be saved to your match history.",
				"Surrender",
				new ConfirmOptions { OkButtonText = "Surrender", CancelButtonText = "Cancel" });

			if (confirmed != true)
				return;

			busy = true;
			activeError = null;
			try
			{
				activeError = await MatchHistoryServices.ResignActiveMatchAsync(match.MatchCode);
				// Reload either way: on success the match moves into the history below, and on
				// failure (e.g. it already ended) the list catches up with the server.
				await LoadAsync();
			}
			finally
			{
				busy = false;
			}
		}

		private void Rejoin(ActiveMatchDto match) =>
			Navigation.NavigateTo($"match?code={Uri.EscapeDataString(match.MatchCode)}");

		private static string ActiveStatus(ActiveMatchDto match) => match switch
		{
			{ Status: SessionStatus.WaitingForOpponent } => "Share the code so your opponent can join.",
			{ IsYourTurn: true } => $"Your turn · {match.MoveCount} moves played",
			_ => $"Opponent's turn · {match.MoveCount} moves played",
		};

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

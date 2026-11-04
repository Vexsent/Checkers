using System.Security.Claims;
using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Checkers.Client.Pages
{
	public partial class Match
	{
		[Inject]
		private IGameSessionServices Sessions { get; set; } = default!;

		[CascadingParameter]
		private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

		private int myUserId;
		private bool busy;
		private string joinCode = string.Empty;
		private string? error;
		private (int Row, int Col)? selected;

		private GameSessionDto? Session => Sessions.Session;
		private Board CurrentBoard => Board.FromSquares(Session!.BoardSize, Session.Squares);
		private PlayerColor MyColor => Session!.BlackPlayerId == myUserId ? PlayerColor.Black : PlayerColor.White;
		private bool IsMyTurn => Session is { Status: SessionStatus.InProgress } session && session.CurrentPlayer == MyColor;

		private string StatusText => Session switch
		{
			{ Status: SessionStatus.WaitingForOpponent } => "Waiting for your opponent to join. Share the code above.",
			{ Status: SessionStatus.InProgress } => IsMyTurn ? "Your turn" : "Opponent's turn",
			{ Outcome: GameOutcome.Draw } => "The game is a draw.",
			{ Outcome: GameOutcome.BlackWon } => MyColor == PlayerColor.Black ? "You won!" : "You lost.",
			{ Outcome: GameOutcome.WhiteWon } => MyColor == PlayerColor.White ? "You won!" : "You lost.",
			_ => string.Empty
		};

		protected override async Task OnInitializedAsync()
		{
			var state = await AuthenticationState;
			int.TryParse(state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out myUserId);
			Sessions.Changed += OnSessionChanged;
		}

		private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);

		private async Task RunAsync(Func<Task<string?>> action)
		{
			busy = true;
			error = null;
			try
			{
				error = await action();
			}
			finally
			{
				busy = false;
			}
		}

		private Task CreateMatch() => RunAsync(Sessions.CreateMatchAsync);

		private Task JoinMatch() => RunAsync(() => Sessions.JoinMatchAsync(joinCode));

		private Task Rejoin() => RunAsync(() => Sessions.JoinMatchAsync(Session!.MatchCode));

		private Task Resign() => RunAsync(Sessions.ResignAsync);

		private Task Leave()
		{
			selected = null;
			joinCode = string.Empty;
			return RunAsync(async () =>
			{
				await Sessions.LeaveAsync();
				return null;
			});
		}

		private async Task SquareClicked((int Row, int Col) square)
		{
			if (busy || !IsMyTurn)
				return;

			if (Board.BelongsTo(CurrentBoard[square.Row, square.Col], MyColor))
			{
				selected = selected == square ? null : square;
				return;
			}

			if (selected is not { } from)
				return;

			selected = null;
			await RunAsync(() => Sessions.MakeMoveAsync(new GameMove(from.Row, from.Col, square.Row, square.Col)));
		}

		public void Dispose() => Sessions.Changed -= OnSessionChanged;
	}
}

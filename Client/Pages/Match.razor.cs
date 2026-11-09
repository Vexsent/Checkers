using System.Security.Claims;
using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
// Client.Services also gets a Square type from the relay branch, so pin this one.
using Square = Checkers.Shared.GameModels.Square;

namespace Checkers.Client.Pages
{
	public partial class Match
	{
		[Inject]
		private IGameSessionServices Sessions { get; set; } = default!;

		[Inject]
		private IRelayService MoveList { get; set; } = default!;

		[CascadingParameter]
		private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

		private int myUserId;
		private bool busy;
		private string joinCode = string.Empty;
		private string? error;
		private Square? selected;

		private GameSessionDto? Session => Sessions.Session;
		private Board CurrentBoard => Board.FromSquares(Session!.BoardSize, Session.Squares);
		private PlayerColor MyColor => Session!.BlackPlayerId == myUserId ? PlayerColor.Black : PlayerColor.White;
		private bool IsMyTurn => Session is { Status: SessionStatus.InProgress } session && session.CurrentPlayer == MyColor;

		/// <summary>
		/// Partway through a multi-jump only the jumping piece may move, so it stays selected.
		/// </summary>
		private Square? Selected => (IsMyTurn ? Sessions.Replay?.Game.JumpingPiece : null) ?? selected;

		private IReadOnlyCollection<Square> Highlighted => IsMyTurn && Selected is { } from && Sessions.Replay is { } replay
			? replay.Game.GetLegalMoves(from).Select(move => move.To).ToList()
			: [];

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
			MoveList.MovesChanged += OnSessionChanged;
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

			var clicked = new Square(square.Row, square.Col);
			var midJump = Sessions.Replay?.Game.JumpingPiece is not null;
			if (Board.BelongsTo(CurrentBoard[clicked], MyColor))
			{
				if (!midJump)
					selected = selected == clicked ? null : clicked;

				return;
			}

			if (Selected is not { } from)
				return;

			selected = null;
			await RunAsync(() => Sessions.MakeMoveAsync(new GameMove(from.Row, from.Col, clicked.Row, clicked.Col)));
		}

		public void Dispose()
		{
			Sessions.Changed -= OnSessionChanged;
			MoveList.MovesChanged -= OnSessionChanged;
		}
	}
}

using System.Security.Claims;
using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
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

		[Inject]
		private DialogService DialogService { get; set; } = default!;

		[Inject]
		private NavigationManager Navigation { get; set; } = default!;

		[CascadingParameter]
		private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

		/// <summary>
		/// A match to join or rejoin on arrival, e.g. from the Rejoin button in Match History.
		/// </summary>
		[SupplyParameterFromQuery(Name = "code")]
		public string? Code { get; set; }

		/// <summary>
		/// A game type to preselect on the create screen, e.g. from "Play this variant" on the rules page.
		/// </summary>
		[SupplyParameterFromQuery(Name = "variant")]
		public string? Variant { get; set; }

		private int myUserId;
		private bool busy;
		private string joinCode = string.Empty;
		private string? error;
		private Square? selected;
		private string? drawNotice;
		private GameSessionDto? lastSession;
		private Timer? clockTimer;

		// The last square the player tried to use illegally; the board flashes it red.
		private Square? rejected;
		private int rejectedCount;

		// Refusals that mean "not a legal move here", shown as a red square rather than a message.
		// Anything else (a lost connection, a timed-out turn) still shows as an alert.
		private static readonly HashSet<string> IllegalMoveErrors =
		[
			Game.NotYourTurnError, Game.OccupiedError, Game.MustJumpError, Game.ContinueJumpError,
			Game.BetterCaptureError, Game.IllegalMoveError, Game.NoPieceError, Game.OffBoardError,
		];

		// Match settings the creator picks.
		private ColorChoice creatorColor = ColorChoice.Black;
		private GameRuleOptions rules = GameRuleOptions.Standard;
		private bool vsComputer;
		// The create panel shows either the main settings or the full rule editor.
		private bool showRuleEditor;
		private ComputerDifficulty computerLevel = ComputerDifficulty.Medium;

		// One drop-down for who you play: another player (-1) or the computer at a level.
		private const int AnotherPlayer = -1;

		private sealed record OpponentChoice(int Key, string Text);

		private static readonly OpponentChoice[] OpponentChoices =
		[
			new(AnotherPlayer, "Another player"),
			new((int)ComputerDifficulty.Beginner, "Computer: Beginner (any legal move)"),
			new((int)ComputerDifficulty.Easy, "Computer: Easy (one turn ahead)"),
			new((int)ComputerDifficulty.Medium, "Computer: Medium (three turns ahead)"),
			new((int)ComputerDifficulty.Hard, "Computer: Hard (five turns ahead)"),
			new((int)ComputerDifficulty.Expert, "Computer: Expert (thinks deepest)"),
		];

		private int OpponentKey
		{
			get => vsComputer ? (int)computerLevel : AnotherPlayer;
			set
			{
				vsComputer = value != AnotherPlayer;
				if (vsComputer) computerLevel = (ComputerDifficulty)value;
			}
		}
		private bool useTurnLimit;
		private int turnLimitAmount = 1;
		private int turnLimitUnitSeconds = 60 * 60;

		private sealed record TurnLimitUnit(string Name, int Seconds);

		private static readonly TurnLimitUnit[] TurnLimitUnits =
		[
			new("seconds", 1),
			new("minutes", 60),
			new("hours", 60 * 60),
			new("days", 24 * 60 * 60),
		];

		private long TurnLimitSeconds => (long)turnLimitAmount * turnLimitUnitSeconds;

		private string? TurnLimitError => useTurnLimit
			&& (TurnLimitSeconds < MatchSettingsDto.MinTurnTimeLimitSeconds || TurnLimitSeconds > MatchSettingsDto.MaxTurnTimeLimitSeconds)
			? MatchSettingsDto.TurnTimeLimitError
			: null;

		private GameSessionDto? Session => Sessions.Session;
		private Board CurrentBoard => Board.FromSquares(Session!.BoardSize, Session.Squares);
		private PlayerColor MyColor => Session!.BlackPlayerId == myUserId ? PlayerColor.Black : PlayerColor.White;
		private bool IsMyTurn => Session is { Status: SessionStatus.InProgress } session && session.CurrentPlayer == MyColor;
		private bool IOfferedDraw => Session?.DrawOfferedBy is { } by && by == MyColor;
		private bool OpponentOfferedDraw => Session?.DrawOfferedBy is { } by && by != MyColor;

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
			{ Status: SessionStatus.InProgress } => IsMyTurn ? "Your turn"
				: Session.ComputerColor is not null ? "The computer is thinking…" : "Opponent's turn",
			{ Outcome: GameOutcome.Draw } => "The game is a draw.",
			{ Outcome: GameOutcome.BlackWon or GameOutcome.WhiteWon } session => EndText(session),
			_ => string.Empty
		};

		private string EndText(GameSessionDto session)
		{
			var won = (session.Outcome == GameOutcome.BlackWon) == (MyColor == PlayerColor.Black);
			return session.EndReason switch
			{
				GameEndReason.Timeout => won ? "You won! Your opponent ran out of time." : "You lost. You ran out of time.",
				GameEndReason.Resignation => won ? "You won! Your opponent resigned." : "You lost. You resigned.",
				_ => won ? "You won!" : "You lost.",
			};
		}

		protected override async Task OnInitializedAsync()
		{
			var state = await AuthenticationState;
			int.TryParse(state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out myUserId);
			Sessions.Changed += OnSessionChanged;
			MoveList.MovesChanged += OnSessionChanged;
			// Redraws the turn clock once a second while there is one to show.
			clockTimer = new Timer(_ =>
			{
				if (Session is { Status: SessionStatus.InProgress, TurnDeadlineUtc: not null })
					_ = InvokeAsync(StateHasChanged);
			}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

			if (Enum.TryParse<GameVariant>(Variant, ignoreCase: true, out var variant) && GameVariants.Find(variant) is { } preset)
			{
				rules = preset.Rules;
				// Drop it from the address so a later visit starts from the default again.
				if (string.IsNullOrWhiteSpace(Code))
					Navigation.NavigateTo("match", replace: true);
			}

			if (!string.IsNullOrWhiteSpace(Code))
			{
				var code = Code.Trim();
				// Drop the code from the address so leaving and coming back doesn't rejoin again.
				Navigation.NavigateTo("match", replace: true);

				if (!string.Equals(Session?.MatchCode, code, StringComparison.OrdinalIgnoreCase))
				{
					joinCode = code;
					await RunAsync(() => Sessions.JoinMatchAsync(code));
				}
			}
		}

		private void OnSessionChanged() => _ = InvokeAsync(() =>
		{
			// The out-of-turn warning is stale once it's this player's turn.
			if (error == Game.NotYourTurnError && IsMyTurn)
				error = null;

			UpdateDrawNotice();
			StateHasChanged();
		});

		/// <summary>
		/// Tells the player when their draw offer was declined. An offer that a move cleared needs
		/// no notice, since the move itself shows on the board.
		/// </summary>
		private void UpdateDrawNotice()
		{
			var session = Session;
			if (session is null || session.DrawOfferedBy is not null || session.MatchCode != lastSession?.MatchCode)
				drawNotice = null;
			else if (lastSession is { } last
				&& last.DrawOfferedBy == MyColor
				&& session.Status == SessionStatus.InProgress
				&& session.Moves.Length == last.Moves.Length)
				drawNotice = "Your opponent declined the draw.";

			lastSession = session;
		}

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

		private Task CreateMatch() => RunAsync(() => Sessions.CreateMatchAsync(new MatchSettingsDto
		{
			CreatorColor = creatorColor,
			Rules = rules,
			TurnTimeLimitSeconds = useTurnLimit ? (int)TurnLimitSeconds : null,
			Computer = vsComputer ? computerLevel : null,
		}));

		private Task JoinMatch() => RunAsync(() => Sessions.JoinMatchAsync(joinCode));

		private Task Rejoin() => RunAsync(() => Sessions.JoinMatchAsync(Session!.MatchCode));

		private Task OfferDraw()
		{
			drawNotice = null;
			return RunAsync(Sessions.OfferDrawAsync);
		}

		private Task AnswerDraw(bool accept) => RunAsync(() => Sessions.AnswerDrawAsync(accept));

		private async Task Resign()
		{
			var confirmed = await DialogService.Confirm("Are you sure you want to resign? Your opponent will win.", "Resign",
				new ConfirmOptions { OkButtonText = "Resign", CancelButtonText = "Cancel" });

			if (confirmed == true)
				await RunAsync(Sessions.ResignAsync);
		}

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
			if (busy || Session is not { Status: SessionStatus.InProgress })
				return;

			var clicked = new Square(square.Row, square.Col);
			if (!IsMyTurn)
			{
				Reject(clicked);
				return;
			}

			error = null;

			var midJump = Sessions.Replay?.Game.JumpingPiece is not null;
			if (Board.BelongsTo(CurrentBoard[clicked], MyColor))
			{
				// Partway through a multi-jump only the jumping piece may move.
				if (midJump && clicked != Selected)
					Reject(clicked);
				else if (!midJump)
					selected = selected == clicked ? null : clicked;

				return;
			}

			if (Selected is not { } from)
			{
				Reject(clicked);
				return;
			}

			// The rules engine already knows the legal moves, so an illegal one is caught here,
			// without a trip to the server. The piece stays selected for another try.
			if (Sessions.Replay is not null && !Highlighted.Contains(clicked))
			{
				Reject(clicked);
				return;
			}

			selected = null;
			busy = true;
			try
			{
				var refused = await Sessions.MakeMoveAsync(new GameMove(from.Row, from.Col, clicked.Row, clicked.Col));
				if (refused is not null && IllegalMoveErrors.Contains(refused))
					Reject(clicked);
				else
					error = refused;
			}
			finally
			{
				busy = false;
			}
		}

		private void Reject(Square square)
		{
			rejected = square;
			rejectedCount++;
		}

		public void Dispose()
		{
			clockTimer?.Dispose();
			Sessions.Changed -= OnSessionChanged;
			MoveList.MovesChanged -= OnSessionChanged;
		}
	}
}

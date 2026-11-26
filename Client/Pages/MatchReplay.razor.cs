using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;
using Radzen;
// Client.Services also has the relay's Square type, so name both explicitly.
using RelaySquare = Checkers.Client.Services.Square;
using Square = Checkers.Shared.GameModels.Square;

namespace Checkers.Client.Pages
{
	/// <summary>
	/// Steps through a saved match one turn at a time.
	/// </summary>
	public partial class MatchReplay
	{

		[Inject]
		private IMatchHistoryServices MatchHistoryServices { get; set; } = default!;

		[Parameter]
		public int MatchId { get; set; }

		private MatchReplayDto? match;
		private SessionReplay? replay;
		private List<string> moveTexts = [];
		private bool loading = true;

		/// <summary>0 is the starting position; n is the board after turn n.</summary>
		private int step;

		private int TurnCount => replay?.Turns.Count ?? 0;

		private ReplayedTurn? LastTurn => step > 0 ? replay!.Turns[step - 1] : null;

		private GameRuleOptions Rules => match?.Rules ?? GameRuleOptions.Standard;

		// The variant's own board: its size and starting rows.
		private Board CurrentBoard => LastTurn is { } turn
			? Board.FromSquares(Rules.BoardSize, turn.BoardAfter)
			: Board.CreateStandard(Rules.BoardSize, Rules.PieceRows);

		private Square? LastLanding => LastTurn?.Path[^1];

		/// <summary>The squares the last piece passed through, shown on the board.</summary>
		private IReadOnlyCollection<Square> LastPath => LastTurn?.Path.Take(LastTurn.Path.Count - 1).ToList() ?? [];

		private string StepText => step == 0 ? $"Start · {TurnCount} moves" : $"Move {step} of {TurnCount}";

		private BadgeStyle OutcomeStyle => match?.Outcome switch
		{
			MatchOutcome.Win => BadgeStyle.Success,
			MatchOutcome.Loss => BadgeStyle.Danger,
			_ => BadgeStyle.Secondary,
		};

		protected override async Task OnParametersSetAsync()
		{
			loading = true;
			match = await MatchHistoryServices.GetMatchAsync(MatchId);
			replay = match is null
				? null
				: SessionReplay.FromMoves(match.Moves.Select(m => new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol)), match.Rules);

			// The relay's notation, so moves read the same as during a live match.
			var notation = new RelayService { BoardSize = match?.Rules.BoardSize ?? 8 };
			moveTexts = replay?.Turns
				.Select(turn => notation.FormatMove(turn.Piece, turn.Path.Select(s => new RelaySquare(s.Row, s.Col)).ToList(), turn.Crowned, turn.IsJump))
				.ToList() ?? [];

			step = 0;
			loading = false;
		}

		private void GoTo(int target) => step = Math.Clamp(target, 0, TurnCount);
	}
}

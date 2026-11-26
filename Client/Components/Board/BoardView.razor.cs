using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;

namespace Checkers.Client.Components.Board
{
	/// <summary>
	/// Renders a <see cref="Checkers.Shared.GameModels.Board"/>. Holds no game state of its own;
	/// all board logic lives in Shared so it can be used and tested without the GUI.
	/// </summary>
	public partial class BoardView
	{
		// How long a rejected square stays red.
		private static readonly TimeSpan RejectedFlash = TimeSpan.FromMilliseconds(900);

		private Square? _flashing;
		private int _flashesShown;
		private CancellationTokenSource? _flashTimer;

		[Parameter, EditorRequired]
		public Checkers.Shared.GameModels.Board Board { get; set; } = default!;

		[Parameter]
		public EventCallback<(int Row, int Col)> OnSquareClicked { get; set; }

		/// <summary>The square of the piece the player has selected, if any.</summary>
		[Parameter]
		public Square? Selected { get; set; }

		/// <summary>Squares the selected piece can legally move to.</summary>
		[Parameter]
		public IReadOnlyCollection<Square> Highlighted { get; set; } = [];

		/// <summary>
		/// Turns the board round 180° so Black's pieces sit at the bottom, for a player playing Black.
		/// Only the picture turns: squares, clicks and highlights all use real board coordinates.
		/// </summary>
		[Parameter]
		public bool Flipped { get; set; }

		/// <summary>
		/// Every move the player could make, shown as a hint (e.g. after repeated illegal tries):
		/// the pieces that can move are outlined and the squares they can reach are marked.
		/// </summary>
		[Parameter]
		public IReadOnlyCollection<Move> Hints { get; set; } = [];

		/// <summary>
		/// A square the player just tried to use illegally. It flashes red briefly instead of a message.
		/// </summary>
		[Parameter]
		public Square? Rejected { get; set; }

		/// <summary>
		/// Raise this each time a move is rejected, so the same square can flash again.
		/// </summary>
		[Parameter]
		public int RejectedCount { get; set; }

		protected override void OnParametersSet()
		{
			if (RejectedCount == _flashesShown)
				return;

			_flashesShown = RejectedCount;
			if (Rejected is not { } square)
				return;

			_flashing = square;
			_flashTimer?.Cancel();
			_flashTimer = new CancellationTokenSource();
			_ = ClearFlashAsync(_flashTimer.Token);
		}

		private async Task ClearFlashAsync(CancellationToken token)
		{
			try
			{
				await Task.Delay(RejectedFlash, token);
				_flashing = null;
				await InvokeAsync(StateHasChanged);
			}
			catch (OperationCanceledException)
			{
				// A newer flash took over.
			}
		}

		// minmax(0, 1fr): plain 1fr tracks won't shrink below their content, so a 12×12 board on a
		// narrow screen would spill past its own width.
		private string GridStyle =>
			$"grid-template-columns: repeat({Board.Size}, minmax(0, 1fr)); grid-template-rows: repeat({Board.Size}, minmax(0, 1fr));";

		// A flashing square gets a fresh key per flash, so its element is rebuilt and the animation replays.
		private string SquareKey(int row, int col) =>
			_flashing == new Square(row, col) ? $"{row}-{col}-{_flashesShown}" : $"{row}-{col}";

		private string SquareClass(int row, int col)
		{
			var square = new Square(row, col);
			var css = Checkers.Shared.GameModels.Board.IsPlayable(row, col) ? "square dark-square" : "square light-square";
			if (square == Selected) css += " selected-square";
			if (Highlighted.Contains(square)) css += " legal-move";
			if (square == _flashing) css += " rejected-square";
			if (Hints.Count > 0)
			{
				if (Hints.Any(m => m.From == square)) css += " hint-piece";
				if (Hints.Any(m => m.To == square)) css += " hint-target";
			}
			return css;
		}

		private Task SquareClicked(int row, int col) => OnSquareClicked.InvokeAsync((row, col));

		public void Dispose()
		{
			_flashTimer?.Cancel();
			_flashTimer?.Dispose();
		}
	}
}

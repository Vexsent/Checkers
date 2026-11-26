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

		// minmax(0, 1fr): plain 1fr tracks won't shrink below their content, so a 12×12 board on a
		// narrow screen would spill past its own width.
		private string GridStyle =>
			$"grid-template-columns: repeat({Board.Size}, minmax(0, 1fr)); grid-template-rows: repeat({Board.Size}, minmax(0, 1fr));";

		private string SquareClass(int row, int col)
		{
			var square = new Square(row, col);
			var css = Checkers.Shared.GameModels.Board.IsPlayable(row, col) ? "square dark-square" : "square light-square";
			if (square == Selected) css += " selected-square";
			if (Highlighted.Contains(square)) css += " legal-move";
			return css;
		}

		private Task SquareClicked(int row, int col) => OnSquareClicked.InvokeAsync((row, col));
	}
}

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

		private string GridStyle => $"grid-template-columns: repeat({Board.Size}, 1fr);";

		private static string SquareClass(int row, int col) =>
			Checkers.Shared.GameModels.Board.IsPlayable(row, col) ? "square dark-square" : "square light-square";

		private Task SquareClicked(int row, int col) => OnSquareClicked.InvokeAsync((row, col));
	}
}

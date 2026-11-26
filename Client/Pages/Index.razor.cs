using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Checkers.Client.Pages
{
	public partial class Index
	{
		[Inject]
		private NotificationService NotificationService { get; set; } = default!;
		[Inject]
		private DialogService DialogService { get; set; } = default!;

		// Null until the player presses "Start Game".
		private Game? game;
		private Square? selected;
		private IReadOnlyCollection<Square> highlighted = [];

		// The last square a player tried to use illegally; the board flashes it red instead of a message.
		private Square? rejected;
		private int rejectedCount;

		private string StatusText => game?.EndReason switch
		{
			null => $"{game?.CurrentTurn}'s turn",
			GameEndReason.DrawAgreed => "Draw by agreement",
			GameEndReason.Resignation => $"{game.Winner} wins - {Game.Opponent(game.Winner!.Value)} resigned",
			_ => $"{game.Winner} wins!",
		};

		private void StartGame()
		{
			game = new Game();
			ClearSelection();
		}

		private void MainMenu()
		{
			game = null;
			ClearSelection();
		}

		private async Task ResignAsync()
		{
			if (game is null || game.IsOver) return;

			var player = game.CurrentTurn;
			var confirmed = await DialogService.Confirm($"{player}, are you sure you want to resign?", "Resign",
				new ConfirmOptions { OkButtonText = "Resign", CancelButtonText = "Cancel" });
			if (confirmed != true) return;

			game.Resign(player);
			ClearSelection();
			NotifyGameOver();
		}

		// Both players share this device, so the opponent answers the offer straight away.
		private async Task OfferDrawAsync()
		{
			if (game is null || game.IsOver) return;

			var offeredBy = game.CurrentTurn;
			var opponent = Game.Opponent(offeredBy);
			var offer = game.OfferDraw(offeredBy);
			if (!offer.Success)
			{
				NotifyError(offer.Error!);
				return;
			}

			var accepted = await DialogService.Confirm($"{offeredBy} offers a draw. {opponent}, do you accept?", "Draw offer",
				new ConfirmOptions { OkButtonText = "Accept", CancelButtonText = "Decline" }) == true;
			game.AnswerDraw(opponent, accepted);

			if (accepted)
			{
				ClearSelection();
				NotifyGameOver();
			}
			else
			{
				NotificationService.Notify(new NotificationMessage
				{
					Summary = "Draw declined",
					Detail = $"{opponent} declined the draw. {offeredBy} to move.",
					Duration = 4000,
					Severity = NotificationSeverity.Info
				});
			}
		}

		private void SquareClicked((int Row, int Col) cell)
		{
			if (game is null) return;
			var square = new Square(cell.Row, cell.Col);
			if (game.IsOver)
			{
				Reject(square);
				return;
			}

			var owner = Game.OwnerOf(game.Board[square]);

			// Clicking one of your own pieces selects it, unless another piece is partway through a multi-jump.
			if (owner == game.CurrentTurn)
			{
				if (game.JumpingPiece is { } jumper && jumper != square)
					Reject(square);
				else
					Select(square);
				return;
			}

			if (selected is null)
			{
				Reject(square);
				return;
			}

			// A refused move leaves the piece selected for another try.
			var result = game.TryMove(selected.Value, square);
			if (!result.Success)
			{
				Reject(square);
				return;
			}

			// Keep the jumping piece selected so the player can see where it must jump next.
			if (game.JumpingPiece is { } next)
				Select(next);
			else
				ClearSelection();

			if (game.IsOver) NotifyGameOver();
		}

		private void Select(Square square)
		{
			selected = square;
			highlighted = game!.GetLegalMoves(square).Select(m => m.To).ToList();

			// A piece that can't move while a capture is required elsewhere.
			if (highlighted.Count == 0 && game.GetLegalMoves().Any(m => m.IsJump))
				Reject(square);
		}

		private void Reject(Square square)
		{
			rejected = square;
			rejectedCount++;
		}

		private void ClearSelection()
		{
			selected = null;
			highlighted = [];
		}

		private void NotifyGameOver()
		{
			NotificationService.Notify(new NotificationMessage
			{
				Summary = "Game over",
				Detail = StatusText,
				Duration = 6000,
				Severity = NotificationSeverity.Success
			});
		}

		private void NotifyError(string error)
		{
			NotificationService.Notify(new NotificationMessage
			{
				Summary = "Invalid move",
				Detail = error,
				Duration = 4000,
				Severity = NotificationSeverity.Error
			});
		}
	}
}

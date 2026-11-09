using Checkers.Application.Core.Sessions;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Xunit;

namespace UnitTests.Application.Core.Sessions
{
	public class CheckersGameRulesTests
	{
		[Fact]
		public void NewGame_StartsStandardBoardWithBlackToMove()
		{
			var game = new CheckersGameRules().NewGame();

			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
			Assert.Equal(GameOutcome.InProgress, game.Outcome);
			Assert.Equal(Board.CreateStandard().ToSquares(), game.Board.ToSquares());
		}

		[Fact]
		public void TryMove_LegalStep_MovesPieceAndPassesTurn()
		{
			var game = new CheckersGameRules().NewGame();

			Assert.Null(game.TryMove(new GameMove(2, 1, 3, 0)));
			Assert.Equal(GamePieces.BlackPawn, game.Board[3, 0]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		[Fact]
		public void TryMove_StepWhenJumpIsAvailable_ReturnsMustJumpError()
		{
			var board = new Board();
			board[2, 1] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			var game = CheckersGameRules.Wrap(new Game(board));

			Assert.Equal(Game.MustJumpError, game.TryMove(new GameMove(2, 1, 3, 0)));
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
		}

		[Fact]
		public void TryMove_MultiJump_KeepsTurnUntilLastJumpThenBlackWins()
		{
			var board = new Board();
			board[0, 1] = GamePieces.BlackPawn;
			board[1, 2] = GamePieces.WhitePawn;
			board[3, 4] = GamePieces.WhitePawn;
			var game = CheckersGameRules.Wrap(new Game(board));

			Assert.Null(game.TryMove(new GameMove(0, 1, 2, 3)));
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
			Assert.Equal(GameOutcome.InProgress, game.Outcome);

			Assert.Null(game.TryMove(new GameMove(2, 3, 4, 5)));
			Assert.Equal(GameOutcome.BlackWon, game.Outcome);
		}

		[Fact]
		public void Outcome_AfterBlackResigns_IsWhiteWon()
		{
			var inner = new Game();
			var game = CheckersGameRules.Wrap(inner);

			inner.Resign(PlayerColor.Black);

			Assert.Equal(GameOutcome.WhiteWon, game.Outcome);
		}

		[Fact]
		public void Outcome_AfterAgreedDraw_IsDraw()
		{
			var inner = new Game();
			var game = CheckersGameRules.Wrap(inner);

			inner.OfferDraw(PlayerColor.Black);
			inner.AnswerDraw(PlayerColor.White, accept: true);

			Assert.Equal(GameOutcome.Draw, game.Outcome);
		}

		[Fact]
		public void TryMove_AfterGameIsOver_ReturnsGameOverError()
		{
			var inner = new Game();
			var game = CheckersGameRules.Wrap(inner);
			inner.Resign(PlayerColor.White);

			Assert.Equal(Game.GameOverError, game.TryMove(new GameMove(2, 1, 3, 0)));
		}
	}
}

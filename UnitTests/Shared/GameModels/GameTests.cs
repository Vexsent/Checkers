using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace UnitTests.Shared.GameModels
{
	public class GameTests
	{
		// AC 3.1 / 11.1
		[Fact]
		public void NewGame_BlackMovesFirst()
		{
			var game = new Game();

			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
			Assert.Null(game.Winner);
		}

		// AC 3.1
		[Fact]
		public void TryMove_LegalStep_MovesPieceRecordsMoveAndPassesTurn()
		{
			var game = new Game();

			var result = game.TryMove(new Square(2, 1), new Square(3, 0));

			Assert.True(result.Success);
			Assert.Equal(GamePieces.Empty, game.Board[2, 1]);
			Assert.Equal(GamePieces.BlackPawn, game.Board[3, 0]);
			Assert.Equal(new Move(new Square(2, 1), new Square(3, 0)), Assert.Single(game.Moves));
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		// AC 3.2
		[Theory]
		[InlineData(2, 1, 3, 1)] // straight down
		[InlineData(2, 1, 4, 3)] // two squares without a jump
		[InlineData(2, 1, 3, 3)] // not diagonal
		public void TryMove_IllegalDestination_PieceStaysAndErrorShown(int fromRow, int fromCol, int toRow, int toCol)
		{
			var game = new Game();

			var result = game.TryMove(new Square(fromRow, fromCol), new Square(toRow, toCol));

			Assert.False(result.Success);
			Assert.Equal(Game.IllegalMoveError, result.Error);
			Assert.Equal(GamePieces.BlackPawn, game.Board[fromRow, fromCol]);
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
		}

		// AC 3.2
		[Fact]
		public void TryMove_PawnBackward_IsRejected()
		{
			var board = new Board();
			board[3, 2] = GamePieces.BlackPawn;
			board[7, 0] = GamePieces.WhitePawn;
			var game = new Game(board);

			var result = game.TryMove(new Square(3, 2), new Square(2, 1));

			Assert.Equal(Game.IllegalMoveError, result.Error);
			Assert.Equal(GamePieces.BlackPawn, game.Board[3, 2]);
		}

		// AC 3.3
		[Fact]
		public void TryMove_ToOccupiedSquare_IsRejected()
		{
			var game = new Game();

			var result = game.TryMove(new Square(1, 0), new Square(2, 1));

			Assert.False(result.Success);
			Assert.Equal(Game.OccupiedError, result.Error);
			Assert.Equal(GamePieces.BlackPawn, game.Board[1, 0]);
			Assert.Equal(GamePieces.BlackPawn, game.Board[2, 1]);
		}

		// AC 3.4
		[Theory]
		[InlineData(-1, 0)]
		[InlineData(3, -1)]
		[InlineData(8, 1)]
		[InlineData(3, 8)]
		public void TryMove_OffBoard_IsRejected(int toRow, int toCol)
		{
			var game = new Game();

			var result = game.TryMove(new Square(2, 7), new Square(toRow, toCol));

			Assert.False(result.Success);
			Assert.Equal(Game.OffBoardError, result.Error);
			Assert.Equal(GamePieces.BlackPawn, game.Board[2, 7]);
		}

		// AC 3.5
		[Fact]
		public void GetLegalMoves_Pawn_IsTheTwoForwardDiagonals()
		{
			var game = new Game();

			var targets = game.GetLegalMoves(new Square(2, 3)).Select(m => m.To);

			Assert.Equal([new Square(3, 2), new Square(3, 4)], targets.OrderBy(s => s.Col));
		}

		// AC 3.5
		[Fact]
		public void GetLegalMoves_King_MovesDiagonallyInEveryDirection()
		{
			var board = new Board();
			board[4, 3] = GamePieces.BlackKing;
			board[7, 0] = GamePieces.WhitePawn;
			var game = new Game(board);

			var targets = game.GetLegalMoves(new Square(4, 3)).Select(m => m.To).ToHashSet();

			Assert.Equal(new HashSet<Square> { new(3, 2), new(3, 4), new(5, 2), new(5, 4) }, targets);
		}

		// AC 3.5 / 10.4
		[Fact]
		public void GetLegalMoves_WhenAJumpIsAvailable_OnlyJumpsAreReturned()
		{
			var board = new Board();
			board[2, 1] = GamePieces.BlackPawn;
			board[2, 5] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			var game = new Game(board);

			var move = Assert.Single(game.GetLegalMoves());

			Assert.Equal(new Move(new Square(2, 1), new Square(4, 3), new Square(3, 2)), move);
		}

		// AC 3.6
		[Fact]
		public void PlayerWithNoLegalMoves_Loses()
		{
			var board = new Board();
			board[5, 0] = GamePieces.WhitePawn;
			board[4, 1] = GamePieces.BlackPawn; // blocks White's only step
			board[3, 2] = GamePieces.BlackPawn; // blocks the jump landing square
			board[0, 7] = GamePieces.BlackPawn;
			var game = new Game(board);

			game.TryMove(new Square(0, 7), new Square(1, 6));

			Assert.True(game.IsOver);
			Assert.Equal(PlayerColor.Black, game.Winner);
			Assert.Empty(game.GetLegalMoves());
			Assert.Equal(Game.GameOverError, game.TryMove(new Square(5, 0), new Square(4, 1)).Error);
		}

		// AC 9.1
		[Fact]
		public void PawnReachingFarRow_IsCrownedAndCanMoveBackward()
		{
			var board = new Board();
			board[6, 1] = GamePieces.BlackPawn;
			board[7, 6] = GamePieces.WhitePawn;
			var game = new Game(board);

			game.TryMove(new Square(6, 1), new Square(7, 2));
			Assert.Equal(GamePieces.BlackKing, game.Board[7, 2]);

			game.TryMove(new Square(7, 6), new Square(6, 7));
			Assert.True(game.TryMove(new Square(7, 2), new Square(6, 1)).Success);
		}

		// AC 9.2
		[Fact]
		public void CrowningByJump_EndsTheTurnEvenIfAnotherJumpIsAvailable()
		{
			var board = new Board();
			board[5, 2] = GamePieces.BlackPawn;
			board[6, 3] = GamePieces.WhitePawn;
			board[6, 5] = GamePieces.WhitePawn; // the new king could jump this, but must not
			var game = new Game(board);

			var result = game.TryMove(new Square(5, 2), new Square(7, 4));

			Assert.True(result.Success);
			Assert.Equal(GamePieces.BlackKing, game.Board[7, 4]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
			Assert.Null(game.JumpingPiece);
		}

		// AC 10.1
		[Fact]
		public void Jump_RemovesTheJumpedPiece()
		{
			var board = new Board();
			board[2, 1] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			board[7, 0] = GamePieces.WhitePawn;
			var game = new Game(board);

			var result = game.TryMove(new Square(2, 1), new Square(4, 3));

			Assert.True(result.Success);
			Assert.Equal(GamePieces.Empty, game.Board[3, 2]);
			Assert.Equal(GamePieces.BlackPawn, game.Board[4, 3]);
			Assert.Equal(new Square(3, 2), game.Moves[0].Captured);
		}

		// AC 10.2
		[Fact]
		public void MultiJump_SamePieceKeepsJumpingUntilNoJumpsRemain()
		{
			var board = new Board();
			board[0, 1] = GamePieces.BlackPawn;
			board[2, 1] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			board[5, 4] = GamePieces.WhitePawn;
			board[7, 0] = GamePieces.WhitePawn;
			var game = new Game(board);

			Assert.True(game.TryMove(new Square(2, 1), new Square(4, 3)).Success);
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
			Assert.Equal(new Square(4, 3), game.JumpingPiece);
			Assert.Equal(Game.ContinueJumpError, game.TryMove(new Square(0, 1), new Square(1, 0)).Error);

			Assert.True(game.TryMove(new Square(4, 3), new Square(6, 5)).Success);
			Assert.Equal(GamePieces.Empty, game.Board[3, 2]);
			Assert.Equal(GamePieces.Empty, game.Board[5, 4]);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
			Assert.Null(game.JumpingPiece);
		}

		// AC 10.3
		[Fact]
		public void CapturingTheLastPiece_WinsTheGame()
		{
			var board = new Board();
			board[2, 1] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			var game = new Game(board);

			game.TryMove(new Square(2, 1), new Square(4, 3));

			Assert.Equal(PlayerColor.Black, game.Winner);
		}

		// AC 10.4
		[Fact]
		public void NonJumpMove_WhenAJumpIsAvailable_IsRejected()
		{
			var board = new Board();
			board[2, 1] = GamePieces.BlackPawn;
			board[2, 5] = GamePieces.BlackPawn;
			board[3, 2] = GamePieces.WhitePawn;
			var game = new Game(board);

			var result = game.TryMove(new Square(2, 5), new Square(3, 6));

			Assert.Equal(Game.MustJumpError, result.Error);
			Assert.Equal(GamePieces.BlackPawn, game.Board[2, 5]);
		}

		// AC 11.1
		[Fact]
		public void Turns_AlternateAfterEveryMove()
		{
			var game = new Game();

			game.TryMove(new Square(2, 1), new Square(3, 0));
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
			game.TryMove(new Square(5, 0), new Square(4, 1));
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
		}

		// AC 11.2
		[Fact]
		public void MovingOutOfTurn_IsRejected()
		{
			var game = new Game();

			var result = game.TryMove(new Square(5, 0), new Square(4, 1));

			Assert.Equal(Game.NotYourTurnError, result.Error);
			Assert.Equal(GamePieces.WhitePawn, game.Board[5, 0]);
		}

		// AC 13.1
		[Theory]
		[InlineData(PlayerColor.Black, PlayerColor.White)]
		[InlineData(PlayerColor.White, PlayerColor.Black)]
		public void Resign_EndsTheGameAndOpponentWins(PlayerColor resigning, PlayerColor winner)
		{
			var game = new Game();

			var result = game.Resign(resigning);

			Assert.True(result.Success);
			Assert.True(game.IsOver);
			Assert.Equal(winner, game.Winner);
			Assert.Equal(GameEndReason.Resignation, game.EndReason);
			Assert.Equal(Game.GameOverError, game.TryMove(new Square(2, 1), new Square(3, 0)).Error);
		}

		// AC 13.1
		[Fact]
		public void Resign_AfterTheGameIsOver_IsRejected()
		{
			var game = new Game();
			game.Resign(PlayerColor.Black);

			Assert.Equal(Game.GameOverError, game.Resign(PlayerColor.White).Error);
			Assert.Equal(PlayerColor.White, game.Winner);
		}

		// AC 13.2
		[Fact]
		public void DrawOffer_Accepted_EndsTheGameAsADraw()
		{
			var game = new Game();

			Assert.True(game.OfferDraw(PlayerColor.Black).Success);
			Assert.Equal(PlayerColor.Black, game.DrawOfferedBy);
			Assert.True(game.AnswerDraw(PlayerColor.White, accept: true).Success);

			Assert.True(game.IsOver);
			Assert.True(game.IsDraw);
			Assert.Null(game.Winner);
			Assert.Equal(GameEndReason.DrawAgreed, game.EndReason);
			Assert.Empty(game.GetLegalMoves());
		}

		// AC 13.2
		[Fact]
		public void DrawOffer_Declined_GameContinues()
		{
			var game = new Game();
			game.OfferDraw(PlayerColor.Black);

			Assert.True(game.AnswerDraw(PlayerColor.White, accept: false).Success);

			Assert.False(game.IsOver);
			Assert.Null(game.DrawOfferedBy);
			Assert.True(game.TryMove(new Square(2, 1), new Square(3, 0)).Success);
		}

		// AC 13.2
		[Fact]
		public void DrawOffer_CannotBeAcceptedByThePlayerWhoOfferedIt()
		{
			var game = new Game();
			game.OfferDraw(PlayerColor.Black);

			Assert.Equal(Game.OwnDrawOfferError, game.AnswerDraw(PlayerColor.Black, accept: true).Error);
			Assert.False(game.IsOver);
		}

		[Fact]
		public void DrawOffer_WhileAnotherIsPending_IsRejected()
		{
			var game = new Game();
			game.OfferDraw(PlayerColor.Black);

			Assert.Equal(Game.DrawAlreadyOfferedError, game.OfferDraw(PlayerColor.White).Error);
		}

		[Fact]
		public void AnswerDraw_WithNoOffer_IsRejected()
		{
			Assert.Equal(Game.NoDrawOfferError, new Game().AnswerDraw(PlayerColor.White, accept: true).Error);
		}

		[Fact]
		public void MakingAMove_WithdrawsAPendingDrawOffer()
		{
			var game = new Game();
			game.OfferDraw(PlayerColor.Black);

			game.TryMove(new Square(2, 1), new Square(3, 0));

			Assert.Null(game.DrawOfferedBy);
			Assert.Equal(Game.NoDrawOfferError, game.AnswerDraw(PlayerColor.White, accept: true).Error);
		}

		[Fact]
		public void TryMove_FromEmptySquare_IsRejected()
		{
			var game = new Game();

			Assert.Equal(Game.NoPieceError, game.TryMove(new Square(3, 0), new Square(4, 1)).Error);
		}
	}
}

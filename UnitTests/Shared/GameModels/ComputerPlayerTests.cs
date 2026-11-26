using System.Diagnostics;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace UnitTests.Shared.GameModels
{
	public class ComputerPlayerTests
	{
		private static readonly TimeSpan QuickBudget = TimeSpan.FromMilliseconds(100);

		private static Board BoardWith(params ((int Row, int Col) Square, GamePieces Piece)[] pieces)
		{
			var board = new Board();
			foreach (var ((row, col), piece) in pieces)
				board[row, col] = piece;
			return board;
		}

		// A short thinking time keeps the suite fast; these tests check legality and obvious choices,
		// which every level finds well within it.
		private static ComputerPlayer Player(ComputerDifficulty level, int seed = 1) =>
			new(level, new Random(seed), QuickBudget);

		/// <summary>Plays the computer's turn and checks every step was legal and the turn really ended.</summary>
		private static void PlayTurn(Game game, IReadOnlyList<Move> turn)
		{
			var mover = game.CurrentTurn;
			Assert.NotEmpty(turn);
			foreach (var step in turn)
			{
				Assert.Equal(mover, game.CurrentTurn);
				Assert.True(game.TryMove(step.From, step.To).Success, $"{step.From} -> {step.To} was refused");
			}

			Assert.True(game.IsOver || game.CurrentTurn != mover);
		}

		// ---- Clone ----

		[Fact]
		public void Clone_IsIndependentAndKeepsAMultiJumpInProgress()
		{
			var game = new Game(BoardWith(((0, 1), GamePieces.BlackPawn), ((1, 2), GamePieces.WhitePawn), ((3, 4), GamePieces.WhitePawn), ((7, 0), GamePieces.WhitePawn)));
			Assert.True(game.TryMove(new Square(0, 1), new Square(2, 3)).Success);

			var copy = game.Clone();
			Assert.Equal(new Square(2, 3), copy.JumpingPiece);
			Assert.True(copy.TryMove(new Square(2, 3), new Square(4, 5)).Success);

			Assert.Equal(new Square(2, 3), game.JumpingPiece);
			Assert.Equal(GamePieces.WhitePawn, game.Board[3, 4]);
			Assert.Equal(PlayerColor.White, copy.CurrentTurn);
			Assert.Equal(PlayerColor.Black, game.CurrentTurn);
		}

		[Fact]
		public void Replay_StopsAtTheFirstRefusedMove()
		{
			var (game, played) = Game.Replay(GameRuleOptions.Standard, [new GameMove(2, 1, 3, 0), new GameMove(2, 3, 3, 4)]);

			Assert.Equal(1, played);
			Assert.Equal(PlayerColor.White, game.CurrentTurn);
		}

		// ---- every level plays legal, complete turns in every variant ----

		public static TheoryData<ComputerDifficulty, GameVariant> LevelsAndVariants()
		{
			var data = new TheoryData<ComputerDifficulty, GameVariant>();
			foreach (var level in Enum.GetValues<ComputerDifficulty>())
				foreach (var variant in new[] { GameVariant.American, GameVariant.Russian, GameVariant.Italian, GameVariant.International, GameVariant.Thai, GameVariant.Casual })
					data.Add(level, variant);
			return data;
		}

		[Theory]
		[MemberData(nameof(LevelsAndVariants))]
		public void EveryLevel_PlaysLegalCompleteTurns(ComputerDifficulty level, GameVariant variant)
		{
			var rules = GameVariants.Find(variant)!.Rules;
			var random = new Random(3);
			var game = new Game(rules);
			var computer = Player(level);

			// Alternate random moves with the computer's, deep enough into the game to reach captures.
			for (var turn = 0; turn < 16 && !game.IsOver; turn++)
			{
				if (turn % 2 == 0)
				{
					PlayTurn(game, computer.ChooseTurn(game));
				}
				else
				{
					var mover = game.CurrentTurn;
					while (!game.IsOver && game.CurrentTurn == mover)
					{
						var legal = game.GetLegalMoves();
						var move = legal[random.Next(legal.Count)];
						game.TryMove(move.From, move.To);
					}
				}
			}
		}

		[Fact]
		public void ChooseTurn_FinishesAMultiJumpAlreadyStarted()
		{
			var game = new Game(BoardWith(((0, 1), GamePieces.BlackPawn), ((1, 2), GamePieces.WhitePawn), ((3, 4), GamePieces.WhitePawn), ((7, 0), GamePieces.WhitePawn)));
			Assert.True(game.TryMove(new Square(0, 1), new Square(2, 3)).Success);

			var turn = Player(ComputerDifficulty.Hard).ChooseTurn(game);

			Assert.Equal([new Move(new Square(2, 3), new Square(4, 5), new Square(3, 4))], turn);
		}

		[Fact]
		public void ChooseTurn_GameOver_IsEmpty()
		{
			var game = new Game();
			game.Resign(PlayerColor.Black);

			Assert.Empty(Player(ComputerDifficulty.Medium).ChooseTurn(game));
		}

		// ---- it plays sensibly ----

		[Theory]
		[InlineData(ComputerDifficulty.Medium)]
		[InlineData(ComputerDifficulty.Hard)]
		[InlineData(ComputerDifficulty.Expert)]
		public void DoesNotGiveAwayAPiece(ComputerDifficulty level)
		{
			// Black's only man can go to (3,0), which is safe, or (3,2), where White (4,3) jumps it.
			var game = new Game(BoardWith(((2, 1), GamePieces.BlackPawn), ((4, 3), GamePieces.WhitePawn), ((7, 6), GamePieces.WhitePawn)));

			for (var seed = 1; seed <= 5; seed++)
				Assert.Equal(new Square(3, 0), Player(level, seed).ChooseTurn(game.Clone()).Single().To);
		}

		[Theory]
		[InlineData(ComputerDifficulty.Medium)]
		[InlineData(ComputerDifficulty.Hard)]
		[InlineData(ComputerDifficulty.Expert)]
		public void TakesAFreePieceWhenCapturingIsOptional(ComputerDifficulty level)
		{
			// Casual rules: Black may ignore the capture of (3,2), but taking it is safe and wins a man.
			var rules = GameVariants.Find(GameVariant.Casual)!.Rules;
			var game = new Game(BoardWith(((2, 1), GamePieces.BlackPawn), ((3, 2), GamePieces.WhitePawn), ((0, 7), GamePieces.BlackPawn), ((7, 6), GamePieces.WhitePawn)),
				PlayerColor.Black, rules);

			for (var seed = 1; seed <= 5; seed++)
				Assert.True(Player(level, seed).ChooseTurn(game.Clone()).First().IsJump);
		}

		[Fact]
		public void Medium_BeatsBeginnerAlmostAlways()
		{
			var wins = 0;
			for (var seed = 1; seed <= 6; seed++)
			{
				var medium = new ComputerPlayer(ComputerDifficulty.Medium, new Random(seed));
				var beginner = new ComputerPlayer(ComputerDifficulty.Beginner, new Random(seed + 100));
				var game = new Game();
				for (var turn = 0; turn < 300 && !game.IsOver; turn++)
					PlayTurn(game, (game.CurrentTurn == PlayerColor.Black ? medium : beginner).ChooseTurn(game));

				if (game.Winner == PlayerColor.Black) wins++;
			}

			Assert.True(wins >= 5, $"Medium won {wins} of 6");
		}

		[Fact]
		public void Expert_StaysWithinItsThinkingTimeOnABigBoard()
		{
			var game = new Game(GameVariants.Find(GameVariant.International)!.Rules);
			var budget = TimeSpan.FromMilliseconds(500);
			var expert = new ComputerPlayer(ComputerDifficulty.Expert, new Random(1), budget);

			var clock = Stopwatch.StartNew();
			var turn = expert.ChooseTurn(game);
			clock.Stop();

			PlayTurn(game, turn);
			Assert.True(clock.Elapsed < budget + TimeSpan.FromSeconds(1), $"took {clock.Elapsed}");
		}

		// ---- draw offers ----

		[Fact]
		public void AcceptsDraw_OnlyWhenClearlyBehind()
		{
			var behind = new Game(BoardWith(((0, 1), GamePieces.BlackPawn), ((7, 0), GamePieces.WhitePawn), ((7, 2), GamePieces.WhitePawn), ((7, 4), GamePieces.WhitePawn)));
			var level = new Game();
			var computer = Player(ComputerDifficulty.Hard);

			Assert.True(computer.AcceptsDraw(behind, PlayerColor.Black));
			Assert.False(computer.AcceptsDraw(level, PlayerColor.Black));
			Assert.False(computer.AcceptsDraw(behind, PlayerColor.White));
		}
	}
}

using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	public sealed class Game
	{
		public const string GameOverError = "The game is over.";
		public const string OffBoardError = "That square is off the board.";
		public const string NoPieceError = "Select one of your pieces first.";
		public const string NotYourTurnError = "It is not your turn.";
		public const string OccupiedError = "That square is already occupied.";
		public const string MustJumpError = "You must make a jump.";
		public const string ContinueJumpError = "You must keep jumping with the same piece.";
		public const string IllegalMoveError = "That is not a legal move. Try again.";
		public const string DrawAlreadyOfferedError = "A draw has already been offered.";
		public const string NoDrawOfferError = "There is no draw offer to answer.";
		public const string OwnDrawOfferError = "You cannot answer your own draw offer.";

		// Creates and array of moves that can be managed only within Game class
		private readonly List<Move> _moves = new();

		// Creation of board that will be used for the game and tracker for the player turn
		public Board Board { get; }
		public PlayerColor CurrentTurn { get; private set; }

		// The winner, or null while the game is in progress or if it ended in a draw.
		public PlayerColor? Winner { get; private set; }
		public GameEndReason? EndReason { get; private set; }
		public bool IsOver => EndReason is not null;
		public bool IsDraw => EndReason == GameEndReason.DrawAgreed;

		// The player with a draw offer waiting for the opponent's answer.
		public PlayerColor? DrawOfferedBy { get; private set; }

		// Keeps track of which piece is currently jumping, so that no other piece can be chosen.
		public Square? JumpingPiece { get; private set; }

		// array of moves that have been made in current game, can be read by outside classes (will be used for move history)
		public IReadOnlyList<Move> Moves => _moves;

		//code to start a game
		public Game() : this(Board.CreateStandard()) { }


		//Black always starts first in American checkers.
		public Game(Board board, PlayerColor firstTurn = PlayerColor.Black)
		{
			Board = board;
			CurrentTurn = firstTurn;
			CheckForWinner();
		}

		//Checks for all legal moves a player is able to make
		public IReadOnlyList<Move> GetLegalMoves()
		{
			//is there a winner?
			if (IsOver) return [];
			//is a piece currently jumping?
			if (JumpingPiece is { } jumper) return JumpsFrom(jumper).ToList();

			var pieces = PiecesOf(CurrentTurn).ToList();
            //sees which pieces can jump, if any, and returns those moves. If none can jump, returns all possible steps.
            var jumps = pieces.SelectMany(JumpsFrom).ToList();
            //if a jump is a avilable, then a piece must jump. (Rule of American Checkers)
            return jumps.Count > 0 ? jumps : pieces.SelectMany(StepsFrom).ToList();
		}

        //checks all moves for a specific piece.
        public IReadOnlyList<Move> GetLegalMoves(Square from) =>
			GetLegalMoves().Where(m => m.From == from).ToList();


        //Method for attempting to make a move, returning a result indicating success or failure and an error message if it failed.
        public MoveResult TryMove(Square from, Square to)
		{
			if (IsOver) return MoveResult.Fail(GameOverError);
			if (!Board.IsOnBoard(from) || !Board.IsOnBoard(to)) return MoveResult.Fail(OffBoardError);

			var owner = OwnerOf(Board[from]);
			if (owner is null) return MoveResult.Fail(NoPieceError);
			if (owner != CurrentTurn) return MoveResult.Fail(NotYourTurnError);
			if (Board[to] != GamePieces.Empty) return MoveResult.Fail(OccupiedError);

			var legal = GetLegalMoves();
			var move = legal.FirstOrDefault(m => m.From == from && m.To == to);
            //if player can't make a move, return an error message to explain.
            if (move is null)
			{
				if (JumpingPiece is { } jumper && jumper != from) return MoveResult.Fail(ContinueJumpError);
				if (legal.Any(m => m.IsJump)) return MoveResult.Fail(MustJumpError);
				return MoveResult.Fail(IllegalMoveError);
			}

			Apply(move);
			return MoveResult.Ok;
		}

		public MoveResult Resign(PlayerColor player)
		{
            //Cant resign if already lost
            if (IsOver) return MoveResult.Fail(GameOverError);

			End(Opponent(player), GameEndReason.Resignation);
			return MoveResult.Ok;
		}

		public MoveResult OfferDraw(PlayerColor player)
		{
			if (IsOver) return MoveResult.Fail(GameOverError);
			if (DrawOfferedBy is not null) return MoveResult.Fail(DrawAlreadyOfferedError);

			DrawOfferedBy = player;
			return MoveResult.Ok;
		}

		public MoveResult AnswerDraw(PlayerColor player, bool accept)
		{
			if (IsOver) return MoveResult.Fail(GameOverError);
			if (DrawOfferedBy is not { } offeredBy) return MoveResult.Fail(NoDrawOfferError);
			if (offeredBy == player) return MoveResult.Fail(OwnDrawOfferError);

			DrawOfferedBy = null;
			if (accept) End(null, GameEndReason.DrawAgreed);
			return MoveResult.Ok;
		}

		public static PlayerColor? OwnerOf(GamePieces piece) => piece switch
		{
			GamePieces.BlackPawn or GamePieces.BlackKing => PlayerColor.Black,
			GamePieces.WhitePawn or GamePieces.WhiteKing => PlayerColor.White,
			_ => null,
		};

		public static bool IsKing(GamePieces piece) => piece is GamePieces.BlackKing or GamePieces.WhiteKing;

		public static PlayerColor Opponent(PlayerColor player) =>
			player == PlayerColor.Black ? PlayerColor.White : PlayerColor.Black;

        // Where game pieces will actually be moved on the board, and the game state will be updated accordingly.
        private void Apply(Move move)
		{
			var piece = Board[move.From];
			Board[move.From] = GamePieces.Empty;
            // If a piece was captured, remove it from the board.
            if (move.Captured is { } captured) Board[captured] = GamePieces.Empty;

			//King Mechanic
			bool crowned = !IsKing(piece) && move.To.Row == CrownRow(OwnerOf(piece)!.Value);
			Board[move.To] = crowned ? Crown(piece) : piece;
            // Add the move to the list of moves and reset any draw offer.
            _moves.Add(move);
			DrawOfferedBy = null;

			// Being crowned ends the turn even if the new king could jump again.
			if (move.IsJump && !crowned && JumpsFrom(move.To).Any())
			{
				JumpingPiece = move.To;
				return;
			}

            //remove jumping piece indicator and switch turns, then check for a winner.
            JumpingPiece = null;
			CurrentTurn = Opponent(CurrentTurn);
			CheckForWinner();
		}

		// Covers both "no pieces left" and "every piece is blocked".
		private void CheckForWinner()
		{
			if (GetLegalMoves().Count == 0) End(Opponent(CurrentTurn), GameEndReason.NoLegalMoves);
		}

		private void End(PlayerColor? winner, GameEndReason reason)
		{
			Winner = winner;
			EndReason = reason;
			JumpingPiece = null;
			DrawOfferedBy = null;
		}

		//
		private IEnumerable<Square> PiecesOf(PlayerColor player)
		{
			for (int r = 0; r < Board.Size; r++)
				for (int c = 0; c < Board.Size; c++)
					if (OwnerOf(Board[r, c]) == player)
						yield return new Square(r, c);
		}

		private IEnumerable<Move> StepsFrom(Square from)
		{
			foreach (var (dr, dc) in Directions(Board[from]))
			{
				var to = new Square(from.Row + dr, from.Col + dc);
				if (Board.IsOnBoard(to) && Board[to] == GamePieces.Empty)
					yield return new Move(from, to);
			}
		}

		private IEnumerable<Move> JumpsFrom(Square from)
		{
			var owner = OwnerOf(Board[from]);
			foreach (var (dr, dc) in Directions(Board[from]))
			{
				var over = new Square(from.Row + dr, from.Col + dc);
				var to = new Square(from.Row + 2 * dr, from.Col + 2 * dc);
				if (!Board.IsOnBoard(to) || Board[to] != GamePieces.Empty) continue;

				var jumped = OwnerOf(Board[over]);
				if (jumped is not null && jumped != owner)
					yield return new Move(from, to, over);
			}
		}

		// Black starts at the top (row 0) and moves down the board; White moves up.
		private static IEnumerable<(int Row, int Col)> Directions(GamePieces piece)
		{
			int[] rows = piece switch
			{
				GamePieces.BlackPawn => [1],
				GamePieces.WhitePawn => [-1],
				GamePieces.BlackKing or GamePieces.WhiteKing => [1, -1],
				_ => [],
			};
			foreach (var dr in rows)
			{
				yield return (dr, -1);
				yield return (dr, 1);
			}
		}

		private int CrownRow(PlayerColor player) => player == PlayerColor.Black ? Board.Size - 1 : 0;

		private static GamePieces Crown(GamePieces pawn) =>
			pawn == GamePieces.BlackPawn ? GamePieces.BlackKing : GamePieces.WhiteKing;
	}
}

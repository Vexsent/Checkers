using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// The rules engine. A turn is played one step at a time (a multi-jump is one TryMove per hop),
	/// but captures are worked out as whole sequences when the turn starts. That's what lets variants
	/// require the longest capture, keep captured pieces on the board until the move ends, or crown a
	/// man partway through a capture. See <see cref="GameRuleOptions"/> for what can vary.
	/// </summary>
	public sealed class Game
	{
		public const string GameOverError = "The game is over.";
		public const string OffBoardError = "That square is off the board.";
		public const string NoPieceError = "Select one of your pieces first.";
		public const string NotYourTurnError = "It is not your turn.";
		public const string OccupiedError = "That square is already occupied.";
		public const string MustJumpError = "You must make a jump.";
		public const string ContinueJumpError = "You must keep jumping with the same piece.";
		public const string BetterCaptureError = "You must take a different capture: these rules say which one.";
		public const string IllegalMoveError = "That is not a legal move. Try again.";
		public const string DrawAlreadyOfferedError = "A draw has already been offered.";
		public const string NoDrawOfferError = "There is no draw offer to answer.";
		public const string OwnDrawOfferError = "You cannot answer your own draw offer.";

		// Creates and array of moves that can be managed only within Game class
		private readonly List<Move> _moves = new();

		// The captures the current player may make this turn (already narrowed by the rules), and the
		// hops made so far. Worked out when the turn starts.
		private List<CaptureSequence> _turnCaptures = [];
		private readonly List<Hop> _turnHops = [];
		// Pieces captured this move but still on the board until it ends (RemoveCapturedAtEndOfMove).
		private readonly List<Square> _pendingCaptures = [];

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

		// The rules this game is played with.
		public GameRuleOptions Rules { get; }

		//code to start a game: the variant's board, with its first player to move
		public Game(GameRuleOptions? rules = null)
			: this(Board.CreateStandard((rules ?? GameRuleOptions.Standard).BoardSize, (rules ?? GameRuleOptions.Standard).PieceRows),
				(rules ?? GameRuleOptions.Standard).FirstPlayer, rules) { }

		//Starts from any position, e.g. a test setup. Black moves first in American checkers.
		public Game(Board board, PlayerColor firstTurn = PlayerColor.Black, GameRuleOptions? rules = null)
		{
			Board = board;
			CurrentTurn = firstTurn;
			Rules = rules ?? GameRuleOptions.Standard;
			BeginTurn();
			CheckForWinner();
		}

		// Copies everything, including a multi-jump in progress, for the computer player's search.
		private Game(Game other)
		{
			Board = Board.FromSquares(other.Board.Size, other.Board.ToSquares());
			CurrentTurn = other.CurrentTurn;
			Winner = other.Winner;
			EndReason = other.EndReason;
			DrawOfferedBy = other.DrawOfferedBy;
			JumpingPiece = other.JumpingPiece;
			Rules = other.Rules;
			_moves.AddRange(other._moves);
			// The sequences themselves are never changed, so the copy can share them.
			_turnCaptures = other._turnCaptures;
			_turnHops.AddRange(other._turnHops);
			_pendingCaptures.AddRange(other._pendingCaptures);
		}

		/// <summary>
		/// An independent copy of the game as it stands, e.g. to try moves without playing them.
		/// </summary>
		public Game Clone() => new(this);

		/// <summary>
		/// Plays saved or live moves from the variant's starting position. Stops at the first move the
		/// rules refuse and returns how many were played.
		/// </summary>
		public static (Game Game, int Played) Replay(GameRuleOptions rules, IEnumerable<GameMove> moves)
		{
			var game = new Game(rules);
			var played = 0;
			foreach (var move in moves)
			{
				if (!game.TryMove(new Square(move.FromRow, move.FromCol), new Square(move.ToRow, move.ToCol)).Success)
					break;
				played++;
			}

			return (game, played);
		}

		//Checks for all legal moves a player is able to make
		public IReadOnlyList<Move> GetLegalMoves()
		{
			//is there a winner?
			if (IsOver) return [];

			//is a piece currently jumping? Only the next hops of the captures it's partway through.
			if (JumpingPiece is { } jumper)
			{
				var done = _turnHops.Count;
				return ContinuingCaptures()
					.Select(s => new Move(jumper, s.Hops[done].To, s.Hops[done].Captured))
					.Distinct()
					.ToList();
			}

			var jumps = _turnCaptures
				.Select(s => new Move(s.From, s.Hops[0].To, s.Hops[0].Captured))
				.Distinct()
				.ToList();

			//if a jump is a avilable, then a piece must jump (unless captures are optional).
			if (jumps.Count > 0 && Rules.ForcedCaptures) return jumps;
			//with forced captures off, jumps are just more options alongside the ordinary steps.
			return [.. jumps, .. PiecesOf(CurrentTurn).SelectMany(StepsFrom)];
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
				// A real capture, just not the one these rules require.
				if (JumpingPiece is null && AllCaptureSequences(CurrentTurn).Any(s => s.From == from && s.Hops[0].To == to))
					return MoveResult.Fail(BetterCaptureError);
				if (Rules.ForcedCaptures && legal.Any(m => m.IsJump)) return MoveResult.Fail(MustJumpError);
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
			_moves.Add(move);
			DrawOfferedBy = null;

			if (move.Captured is not { } captured)
			{
				//King Mechanic: a step onto the far row always crowns.
				Board[move.To] = ReachesCrownRow(piece, move.To) ? Crown(piece) : piece;
				EndTurn();
				return;
			}

			// Captured pieces either come off now or stay (blocking) until the move ends.
			if (Rules.RemoveCapturedAtEndOfMove) _pendingCaptures.Add(captured);
			else Board[captured] = GamePieces.Empty;

			_turnHops.Add(new Hop(move.To, captured));

			var crownsNow = ReachesCrownRow(piece, move.To) && Rules.PromotionDuringCapture != PromotionRule.OnlyAtEndOfMove;
			Board[move.To] = crownsNow ? Crown(piece) : piece;

			// Keep jumping while any allowed capture sequence goes further along this path.
			if (ContinuingCaptures().Any())
			{
				JumpingPiece = move.To;
				return;
			}

			foreach (var square in _pendingCaptures) Board[square] = GamePieces.Empty;
			// A man that ends its move on the far row is crowned, whichever promotion rule applies.
			if (ReachesCrownRow(Board[move.To], move.To)) Board[move.To] = Crown(Board[move.To]);
			EndTurn();
		}

		//remove jumping piece indicator and switch turns, then check for a winner.
		private void EndTurn()
		{
			JumpingPiece = null;
			CurrentTurn = Opponent(CurrentTurn);
			BeginTurn();
			CheckForWinner();
		}

		private void BeginTurn()
		{
			_turnHops.Clear();
			_pendingCaptures.Clear();
			_turnCaptures = ChooseCaptures(AllCaptureSequences(CurrentTurn));
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

		// The allowed capture sequences that follow the hops made so far and still have more to go.
		private IEnumerable<CaptureSequence> ContinuingCaptures()
		{
			var done = _turnHops.Count;
			return _turnCaptures.Where(s => s.Hops.Count > done && s.Hops.Take(done).SequenceEqual(_turnHops));
		}

		private IEnumerable<Square> PiecesOf(PlayerColor player)
		{
			for (int r = 0; r < Board.Size; r++)
				for (int c = 0; c < Board.Size; c++)
					if (OwnerOf(Board[r, c]) == player)
						yield return new Square(r, c);
		}

		// With flying kings on, a king slides along the diagonal instead of moving one square.
		private bool Flies(GamePieces piece) => Rules.FlyingKings && IsKing(piece);

		private IEnumerable<Move> StepsFrom(Square from)
		{
			var piece = Board[from];
			var flies = Flies(piece);
			foreach (var (dr, dc) in Directions(piece, RowsForSteps(piece)))
			{
				var to = new Square(from.Row + dr, from.Col + dc);
				while (Board.IsOnBoard(to) && Board[to] == GamePieces.Empty)
				{
					yield return new Move(from, to);
					if (!flies) break;
					to = new Square(to.Row + dr, to.Col + dc);
				}
			}
		}

		// ---- capture sequences ----

		private readonly record struct Hop(Square To, Square Captured);

		/// <summary>
		/// One complete capture for one piece, from its starting square through every hop.
		/// </summary>
		private sealed record CaptureSequence(Square From, IReadOnlyList<Hop> Hops, bool ByKing, IReadOnlyList<bool> CapturedKings)
		{
			public int KingsCaptured => CapturedKings.Count(k => k);
		}

		// Every complete capture each of the player's pieces could make, before the rules pick among them.
		private List<CaptureSequence> AllCaptureSequences(PlayerColor player)
		{
			var sequences = new List<CaptureSequence>();
			foreach (var from in PiecesOf(player))
				Search(from, from, IsKing(Board[from]), player, [], [], sequences);
			return sequences;
		}

		/// <summary>
		/// Depth-first search for capture sequences. Captured pieces are tracked rather than removed,
		/// so the same position works for both removal rules.
		/// </summary>
		private void Search(Square origin, Square at, bool asKing, PlayerColor owner, List<Hop> hops, List<bool> capturedKings, List<CaptureSequence> results)
		{
			var found = false;
			foreach (var hop in HopsFrom(origin, at, asKing, owner, hops))
			{
				found = true;
				hops.Add(hop);
				capturedKings.Add(IsKing(Board[hop.Captured]));

				var crowns = !asKing && hop.To.Row == CrownRow(owner);
				if (crowns && Rules.PromotionDuringCapture == PromotionRule.EndsTurn)
					results.Add(new CaptureSequence(origin, [.. hops], IsKing(Board[origin]), [.. capturedKings]));
				else
					Search(origin, hop.To, asKing || (crowns && Rules.PromotionDuringCapture == PromotionRule.ContinuesAsKing), owner, hops, capturedKings, results);

				hops.RemoveAt(hops.Count - 1);
				capturedKings.RemoveAt(capturedKings.Count - 1);
			}

			if (!found && hops.Count > 0)
				results.Add(new CaptureSequence(origin, [.. hops], IsKing(Board[origin]), [.. capturedKings]));
		}

		// The single captures available from 'at', with the piece that started on 'origin' having
		// already made 'hops'. A piece can't be captured twice in one move.
		private IEnumerable<Hop> HopsFrom(Square origin, Square at, bool asKing, PlayerColor owner, List<Hop> hops)
		{
			var flies = asKing && Rules.FlyingKings;
			var piece = asKing ? Crown(Board[origin]) : Board[origin];
			foreach (var (dr, dc) in Directions(piece, RowsForCaptures(piece)))
			{
				// A flying king may cross any run of empty squares to reach the piece it captures.
				var over = new Square(at.Row + dr, at.Col + dc);
				while (flies && Board.IsOnBoard(over) && IsOpen(over, origin, hops))
					over = new Square(over.Row + dr, over.Col + dc);

				if (!Board.IsOnBoard(over) || IsOpen(over, origin, hops) || IsCaptured(over, hops)) continue;

				var target = Board[over];
				if (OwnerOf(target) != Opponent(owner)) continue;
				if (!asKing && !Rules.MenCanCaptureKings && IsKing(target)) continue;

				// Land on the square just past the captured piece, or (flying) any empty square beyond it.
				var to = new Square(over.Row + dr, over.Col + dc);
				while (Board.IsOnBoard(to) && IsOpen(to, origin, hops))
				{
					yield return new Hop(to, over);
					if (!flies || Rules.KingStopsBehindCapture) break;
					to = new Square(to.Row + dr, to.Col + dc);
				}
			}
		}

		// Empty for the capturing piece: really empty, its own starting square, or (when captures come
		// off straight away) a piece it has already taken.
		private bool IsOpen(Square square, Square origin, List<Hop> hops) =>
			square == origin
			|| (Board[square] == GamePieces.Empty && !IsCaptured(square, hops))
			|| (!Rules.RemoveCapturedAtEndOfMove && IsCaptured(square, hops));

		private static bool IsCaptured(Square square, List<Hop> hops) => hops.Exists(h => h.Captured == square);

		/// <summary>
		/// Narrows the possible captures to the ones the variant allows the player to choose.
		/// </summary>
		private List<CaptureSequence> ChooseCaptures(List<CaptureSequence> all)
		{
			if (all.Count == 0) return all;

			IEnumerable<CaptureSequence> allowed = all;
			switch (Rules.CaptureChoice)
			{
				case CaptureChoice.MostPieces:
					allowed = KeepMax(allowed, s => s.Hops.Count);
					break;
				case CaptureChoice.MostPiecesThenKings:
					allowed = KeepMax(KeepMax(allowed, s => s.Hops.Count), s => s.KingsCaptured);
					break;
				case CaptureChoice.Italian:
					allowed = KeepMax(allowed, s => s.Hops.Count);
					allowed = KeepMax(allowed, s => s.ByKing ? 1 : 0);
					allowed = KeepMax(allowed, s => s.KingsCaptured);
					// Then whichever takes a king earliest: compare the king/man pattern hop by hop.
					var keyed = allowed.Select(s => (Sequence: s, Key: KingOrderKey(s))).ToList();
					var best = keyed.Select(k => k.Key).Aggregate((a, b) => string.CompareOrdinal(a, b) >= 0 ? a : b);
					allowed = keyed.Where(k => k.Key == best).Select(k => k.Sequence);
					break;
				case CaptureChoice.KingsFirst:
					if (allowed.Any(s => s.ByKing)) allowed = allowed.Where(s => s.ByKing);
					break;
			}

			return allowed.ToList();
		}

		private static IEnumerable<CaptureSequence> KeepMax(IEnumerable<CaptureSequence> sequences, Func<CaptureSequence, int> score)
		{
			var list = sequences.ToList();
			var max = list.Max(score);
			return list.Where(s => score(s) == max);
		}

		// '1' for a king taken, '0' for a man, hop by hop: the ordinal maximum takes a king soonest.
		private static string KingOrderKey(CaptureSequence sequence) =>
			new(sequence.CapturedKings.Select(k => k ? '1' : '0').ToArray());

		// ---- directions and crowning ----

		// Black starts at the top (row 0) and moves down the board; White moves up.
		private static int Forward(GamePieces piece) => OwnerOf(piece) == PlayerColor.Black ? 1 : -1;

		private static int[] RowsForSteps(GamePieces piece) =>
			piece == GamePieces.Empty ? [] : IsKing(piece) ? [1, -1] : [Forward(piece)];

		private int[] RowsForCaptures(GamePieces piece) =>
			piece == GamePieces.Empty ? []
			: IsKing(piece) ? [1, -1]
			: Rules.MenCaptureBackward ? [Forward(piece), -Forward(piece)]
			: [Forward(piece)];

		private static IEnumerable<(int Row, int Col)> Directions(GamePieces piece, int[] rows)
		{
			foreach (var dr in rows)
			{
				yield return (dr, -1);
				yield return (dr, 1);
			}
		}

		private bool ReachesCrownRow(GamePieces piece, Square square) =>
			!IsKing(piece) && OwnerOf(piece) is { } owner && square.Row == CrownRow(owner);

		private int CrownRow(PlayerColor player) => player == PlayerColor.Black ? Board.Size - 1 : 0;

		private static GamePieces Crown(GamePieces piece) => piece switch
		{
			GamePieces.BlackPawn => GamePieces.BlackKing,
			GamePieces.WhitePawn => GamePieces.WhiteKing,
			_ => piece,
		};
	}
}

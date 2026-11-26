using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
// Client.Services has the relay's own Square, so name both explicitly.
using BoardSquare = Checkers.Shared.GameModels.Square;
using RelaySquare = Checkers.Client.Services.Square;

namespace Checkers.Client.Services
{
	/// <summary>
	/// Replays a live match's moves through the rules engine, so the client knows what the server
	/// knows: which piece is partway through a multi-jump, which moves are legal, and how the
	/// one-step moves group into turns for the move list.
	/// </summary>
	public sealed class SessionReplay
	{
		private SessionReplay(Game game, IReadOnlyList<ReplayedTurn> turns, bool isExact)
		{
			Game = game;
			Turns = turns;
			IsExact = isExact;
		}

		/// <summary>
		/// The game as it stands. Use it for legal moves and <see cref="Game.JumpingPiece"/>.
		/// </summary>
		public Game Game { get; }

		/// <summary>
		/// Every completed turn, oldest first. A multi-jump still in progress isn't included yet.
		/// </summary>
		public IReadOnlyList<ReplayedTurn> Turns { get; }

		/// <summary>
		/// False when the moves didn't replay to the board the server sent. <see cref="Game"/> is
		/// then rebuilt from that board, which loses multi-jump state, and <see cref="Turns"/> is empty.
		/// </summary>
		public bool IsExact { get; }

		public static SessionReplay From(GameSessionDto session)
		{
			var (game, turns, allLegal) = Play(session.Moves, session.Rules);

			if (!allLegal || !game.Board.ToSquares().SequenceEqual(session.Squares))
				return FromBoard(session);

			return new SessionReplay(game, turns, isExact: true);
		}

		/// <summary>
		/// Replays a saved match's moves from the starting board. If a move is rejected, replay stops
		/// there: <see cref="Turns"/> holds the turns before it and <see cref="IsExact"/> is false.
		/// A multi-jump the match ended in the middle of (a resignation) is included as a final turn.
		/// Pass the rules the match was played with, or its moves may not replay.
		/// </summary>
		public static SessionReplay FromMoves(IEnumerable<GameMove> moves, GameRuleOptions? rules = null)
		{
			var (game, turns, allLegal) = Play(moves, rules, includeUnfinishedTurn: true);
			return new SessionReplay(game, turns, isExact: allLegal);
		}

		/// <summary>
		/// Plays the moves through the rules engine, grouping the one-step moves into turns.
		/// Stops at the first move the engine rejects.
		/// </summary>
		private static (Game Game, List<ReplayedTurn> Turns, bool AllLegal) Play(
			IEnumerable<GameMove> moves, GameRuleOptions? rules, bool includeUnfinishedTurn = false)
		{
			// Live matches start like CheckersGameRules: the standard board with black to move.
			var game = new Game(rules);
			var turns = new List<ReplayedTurn>();
			List<BoardSquare>? path = null;
			var piece = GamePieces.Empty;
			var isJump = false;
			var allLegal = true;

			foreach (var move in moves)
			{
				var from = new BoardSquare(move.FromRow, move.FromCol);
				var to = new BoardSquare(move.ToRow, move.ToCol);

				if (path is null)
				{
					piece = game.Board.IsOnBoard(from) ? game.Board[from] : GamePieces.Empty;
					path = [from];
				}

				var mover = game.CurrentTurn;
				if (!game.TryMove(from, to).Success)
				{
					allLegal = false;
					// The rejected move's square isn't part of what was played.
					path = path.Count > 1 ? path : null;
					break;
				}

				// A turn's first step says whether it's a capture; a flying king's distance can't.
				if (path.Count == 1)
					isJump = game.Moves[^1].IsJump;

				path.Add(to);
				if (game.CurrentTurn != mover || game.IsOver)
				{
					turns.Add(Turn(game, piece, path, isJump));
					path = null;
				}
			}

			if (includeUnfinishedTurn && path is not null)
				turns.Add(Turn(game, piece, path, isJump));

			return (game, turns, allLegal);
		}

		private static ReplayedTurn Turn(Game game, GamePieces piece, List<BoardSquare> path, bool isJump)
		{
			var crowned = !Game.IsKing(piece) && Game.IsKing(game.Board[path[^1]]);
			return new ReplayedTurn(piece, path, crowned, game.Board.ToSquares(), isJump);
		}

		/// <summary>
		/// Brings the relay's move list up to date. It only ever appends, so entries keep their
		/// timestamps; a list that's ahead of this match (a different match) is cleared first.
		/// </summary>
		internal void UpdateMoveList(IRelayService relay)
		{
			if (!IsExact)
				return;

			if (relay.Moves.Count > Turns.Count)
				relay.ClearMoves();

			relay.BoardSize = Game.Board.Size;

			foreach (var turn in Turns.Skip(relay.Moves.Count))
			{
				var path = turn.Path.Select(square => new RelaySquare(square.Row, square.Col)).ToList();
				relay.RecordMove(turn.Piece, path, turn.Crowned, turn.IsJump);
			}
		}

		private static SessionReplay FromBoard(GameSessionDto session) =>
			new(new Game(Board.FromSquares(session.BoardSize, session.Squares), session.CurrentPlayer, session.Rules), [], isExact: false);
	}

	/// <summary>
	/// One completed turn: the piece as it started, its starting square and every landing square,
	/// the board right after it (row by row, like <see cref="Board.ToSquares"/>), and whether it captured.
	/// </summary>
	public sealed record ReplayedTurn(GamePieces Piece, IReadOnlyList<BoardSquare> Path, bool Crowned, GamePieces[] BoardAfter, bool IsJump);
}

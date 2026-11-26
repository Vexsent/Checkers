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
			// Live matches start like CheckersGameRules: the standard board with black to move.
			var game = new Game();
			var turns = new List<ReplayedTurn>();
			List<BoardSquare>? path = null;
			var piece = GamePieces.Empty;

			foreach (var move in session.Moves)
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
					return FromBoard(session);

				path.Add(to);
				if (game.CurrentTurn != mover || game.IsOver)
				{
					var crowned = !Game.IsKing(piece) && Game.IsKing(game.Board[to]);
					turns.Add(new ReplayedTurn(piece, path, crowned));
					path = null;
				}
			}

			if (!game.Board.ToSquares().SequenceEqual(session.Squares))
				return FromBoard(session);

			return new SessionReplay(game, turns, isExact: true);
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

			foreach (var turn in Turns.Skip(relay.Moves.Count))
			{
				var path = turn.Path.Select(square => new RelaySquare(square.Row, square.Col)).ToList();
				relay.RecordMove(turn.Piece, path, turn.Crowned);
			}
		}

		private static SessionReplay FromBoard(GameSessionDto session) =>
			new(new Game(Board.FromSquares(session.BoardSize, session.Squares), session.CurrentPlayer), [], isExact: false);
	}

	/// <summary>One completed turn: the piece as it started, its starting square and every landing square.</summary>
	public sealed record ReplayedTurn(GamePieces Piece, IReadOnlyList<BoardSquare> Path, bool Crowned);
}

using System.Diagnostics;
using Checkers.Shared.Enums;

namespace Checkers.Shared.GameModels
{
	/// <summary>
	/// The computer opponent. It plays through the rules engine, so it handles every variant: it looks
	/// at whole turns (a multi-jump is one choice) and searches ahead with alpha-beta pruning.
	/// </summary>
	public sealed class ComputerPlayer
	{
		private const int Win = 1_000_000;

		private readonly Settings _settings;
		private readonly Random _random;

		/// <param name="timeBudget">Overrides the level's thinking time, e.g. for tests.</param>
		public ComputerPlayer(ComputerDifficulty difficulty, Random? random = null, TimeSpan? timeBudget = null)
		{
			Difficulty = difficulty;
			_settings = SettingsFor(difficulty);
			if (timeBudget is { } budget) _settings = _settings with { TimeBudget = budget };
			_random = random ?? Random.Shared;
		}

		public ComputerDifficulty Difficulty { get; }

		/// <summary>
		/// How each level plays: how many turns ahead it looks, how much it fumbles its evaluation,
		/// and how long it may think.
		/// </summary>
		private sealed record Settings(int MaxDepth, int Noise, TimeSpan TimeBudget, bool RandomMoves = false);

		private static Settings SettingsFor(ComputerDifficulty difficulty) => difficulty switch
		{
			ComputerDifficulty.Beginner => new(0, 0, TimeSpan.FromSeconds(1), RandomMoves: true),
			ComputerDifficulty.Easy => new(1, 90, TimeSpan.FromSeconds(1)),
			ComputerDifficulty.Medium => new(3, 25, TimeSpan.FromSeconds(1)),
			ComputerDifficulty.Hard => new(5, 0, TimeSpan.FromSeconds(1.5)),
			_ => new(12, 0, TimeSpan.FromSeconds(2.5)),
		};

		public static string NameOf(ComputerDifficulty difficulty) => $"Computer ({difficulty})";

		/// <summary>
		/// The whole turn the computer plays for the side to move: one step, or every hop of a
		/// multi-jump. Picks up a multi-jump already in progress. Empty if the game is over.
		/// </summary>
		public IReadOnlyList<Move> ChooseTurn(Game game)
		{
			var turns = Turns(game, deadline: null);
			if (turns.Count == 0) return [];
			if (turns.Count == 1 || _settings.RandomMoves) return turns[_random.Next(turns.Count)].Steps;

			var clock = Stopwatch.StartNew();
			var best = turns[0].Steps;

			// Iterative deepening: each finished depth gives a full answer, so running out of time
			// mid-search falls back to the deepest one completed.
			for (var depth = 1; depth <= _settings.MaxDepth; depth++)
			{
				var search = new Search(this, clock, _settings.TimeBudget);
				if (search.Root(turns, depth) is not { } found) break;
				best = found;
				if (search.FoundForcedWin) break;
			}

			return best;
		}

		/// <summary>
		/// Whether the computer, playing <paramref name="computer"/>, accepts a draw: only when its
		/// position looks clearly worse.
		/// </summary>
		public bool AcceptsDraw(Game game, PlayerColor computer)
		{
			var score = Evaluate(game, computer);
			return score <= -150;
		}

		// ---- turns ----

		private sealed record Turn(IReadOnlyList<Move> Steps, Game After, int Captures);

		/// <summary>
		/// Every complete turn for the side to move, each with the position it leaves. Captures first,
		/// longest first, which makes alpha-beta prune far more.
		/// </summary>
		private static List<Turn> Turns(Game game, Stopwatch? deadline, TimeSpan budget = default)
		{
			var turns = new List<Turn>();
			if (game.IsOver) return turns;

			var mover = game.CurrentTurn;
			var steps = new List<Move>();

			void Walk(Game from)
			{
				foreach (var move in from.GetLegalMoves())
				{
					var next = from.Clone();
					next.TryMove(move.From, move.To);
					steps.Add(move);
					if (next.IsOver || next.CurrentTurn != mover)
						turns.Add(new Turn([.. steps], next, steps.Count(s => s.IsJump)));
					else
						Walk(next);
					steps.RemoveAt(steps.Count - 1);

					if (deadline is not null && deadline.Elapsed > budget) return;
				}
			}

			Walk(game);
			turns.Sort((a, b) => b.Captures.CompareTo(a.Captures));
			return turns;
		}

		// ---- search ----

		private sealed class Search(ComputerPlayer player, Stopwatch clock, TimeSpan budget)
		{
			// Captures must be resolved before a position is judged, but not forever.
			private const int MaxCaptureExtension = 8;

			private bool _outOfTime;

			public bool FoundForcedWin { get; private set; }

			public IReadOnlyList<Move>? Root(List<Turn> turns, int depth)
			{
				var bestScore = int.MinValue;
				var best = new List<Turn>();
				var alpha = -Win - 1;

				foreach (var turn in turns)
				{
					var score = -Negamax(turn.After, depth - 1, -Win - 1, -alpha, ply: 1, extension: 0) + player.NoiseFor(turn);
					if (_outOfTime) return null;

					if (score > bestScore)
					{
						bestScore = score;
						best.Clear();
					}

					if (score == bestScore) best.Add(turn);
					// A pruned move only gets an upper bound. Keeping the window just below the best
					// score means only exact scores can tie it. With noise the scores aren't comparable
					// to a window, so those (shallow) levels search every root move in full.
					if (player._settings.Noise == 0) alpha = bestScore - 1;
				}

				FoundForcedWin = bestScore >= Win - 100;
				return best[player._random.Next(best.Count)].Steps;
			}

			/// <summary>The value of the position for the side to move.</summary>
			private int Negamax(Game game, int depth, int alpha, int beta, int ply, int extension)
			{
				if (clock.Elapsed > budget)
				{
					_outOfTime = true;
					return 0;
				}

				// The side to move has lost (no moves, resigned) or it's a draw. Sooner wins score higher.
				if (game.IsOver)
					return game.Winner is null ? 0 : game.Winner == game.CurrentTurn ? Win - ply : -(Win - ply);

				var best = -Win - 1;
				var capturesOnly = false;
				if (depth <= 0)
				{
					// Past the search depth, keep following captures (only captures) so it never stops
					// in the middle of an exchange.
					if (extension >= MaxCaptureExtension || !game.GetLegalMoves().Any(m => m.IsJump))
						return Evaluate(game, game.CurrentTurn);

					// When capturing is optional the side to move may also just stop here.
					if (!game.Rules.ForcedCaptures)
					{
						best = Evaluate(game, game.CurrentTurn);
						if (best >= beta) return best;
						alpha = Math.Max(alpha, best);
					}

					capturesOnly = true;
					depth = 1;
					extension++;
				}

				var turns = Turns(game, clock, budget);
				if (capturesOnly) turns.RemoveAll(t => t.Captures == 0);
				// Listing turns can itself run out the clock, leaving the list incomplete.
				if (clock.Elapsed > budget)
				{
					_outOfTime = true;
					return 0;
				}

				if (turns.Count == 0) return best;

				foreach (var turn in turns)
				{
					var score = -Negamax(turn.After, depth - 1, -beta, -alpha, ply + 1, extension);
					if (_outOfTime) return 0;
					best = Math.Max(best, score);
					alpha = Math.Max(alpha, score);
					if (alpha >= beta) break;
				}

				return best;
			}
		}

		private int NoiseFor(Turn turn) => _settings.Noise == 0 ? 0 : _random.Next(-_settings.Noise, _settings.Noise + 1);

		// ---- evaluation ----

		/// <summary>
		/// How good the position is for <paramref name="side"/>, in hundredths of a man: material first,
		/// then advanced men, the centre, and men guarding their own back row.
		/// </summary>
		private static int Evaluate(Game game, PlayerColor side)
		{
			var board = game.Board;
			var size = board.Size;
			var kingValue = game.Rules.FlyingKings ? 320 : 170;
			var score = 0;
			int mine = 0, theirs = 0, pieces = 0;
			var myKings = new List<Square>();
			var enemies = new List<Square>();

			for (int r = 0; r < size; r++)
				for (int c = 0; c < size; c++)
				{
					var piece = board[r, c];
					if (Game.OwnerOf(piece) is not { } owner) continue;
					pieces++;

					int value;
					if (Game.IsKing(piece))
					{
						value = kingValue;
					}
					else
					{
						// Rows travelled towards the crown, scaled so every board size counts the same.
						var advanced = owner == PlayerColor.Black ? r : size - 1 - r;
						value = 100 + advanced * 24 / (size - 1);
						// Men on their own back row stop the opponent crowning.
						if (advanced == 0) value += 8;
					}

					// Pieces in the middle columns have more options.
					var fromCentre = Math.Abs(2 * c - (size - 1));
					value += (size - fromCentre) / 2;

					if (owner == side)
					{
						score += value;
						mine += Game.IsKing(piece) ? kingValue : 100;
						if (Game.IsKing(piece)) myKings.Add(new Square(r, c));
					}
					else
					{
						score -= value;
						theirs += Game.IsKing(piece) ? kingValue : 100;
						enemies.Add(new Square(r, c));
					}
				}

			// When ahead, trading down helps: 3 against 2 is far stronger than 12 against 11.
			if (mine != theirs && mine + theirs > 0)
				score += (mine - theirs) * 150 / (mine + theirs);

			// In the endgame the side that's ahead hunts: its kings close in on every enemy piece, which
			// pushes the defenders out of the corners they'd otherwise shuffle in forever.
			if (mine > theirs && pieces <= size + 2 && myKings.Count > 0)
				foreach (var enemy in enemies)
					score += 6 * (size - myKings.Min(k => Math.Max(Math.Abs(enemy.Row - k.Row), Math.Abs(enemy.Col - k.Col))));

			return score;
		}
	}
}

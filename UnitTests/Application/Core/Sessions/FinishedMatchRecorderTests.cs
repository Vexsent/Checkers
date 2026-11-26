using Checkers.Application.Core.MatchHistory;
using Checkers.Client.Services;
using Checkers.Application.Core.Repositories;
using Checkers.Application.Core.Sessions;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UnitTests.TestSupport;

namespace UnitTests.Application.Core.Sessions
{
	/// <summary>
	/// User Story 12, AC 12.1: when a live game ends it is saved with its date, start time, both
	/// players, every move in order and the result, and it shows up in both players' match history.
	/// Runs the real session manager, match history service and repositories against in-memory SQLite.
	/// </summary>
	public class FinishedMatchRecorderTests : IDisposable
	{
		private static readonly GameMove BlackOpening = new(2, 1, 3, 0);
		private static readonly GameMove WhiteOpening = new(5, 0, 4, 1);

		private readonly SqliteConnection _connection;
		private readonly CheckersContext _context;
		private readonly MatchHistoryService _matchHistory;
		private readonly User _black;
		private readonly User _white;

		public FinishedMatchRecorderTests()
		{
			_connection = new SqliteConnection("DataSource=:memory:");
			_connection.Open();

			_context = new SqliteCheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
			_context.Database.EnsureCreated();

			_black = NewUser("black");
			_white = NewUser("white");
			_context.Users.AddRange(_black, _white);
			_context.SaveChanges();

			_matchHistory = new MatchHistoryService(new MatchRecordRepository(_context), new UserRepository(_context));
		}

		public void Dispose()
		{
			_context.Dispose();
			_connection.Dispose();
		}

		[Fact]
		public async Task ResignedGame_IsSavedWithPlayersTimesResultAndEveryMoveInOrder()
		{
			var (manager, recorder, code) = StartMatch(new CheckersGameRules());
			manager.MakeMove(code, _black.Id, BlackOpening);
			manager.MakeMove(code, _white.Id, WhiteOpening);
			var session = manager.Resign(code, _black.Id).Session!;

			var result = await recorder.RecordIfFinishedAsync(session);

			Assert.True(result!.Success);
			var saved = await _context.MatchRecords.Include(m => m.Moves).SingleAsync();
			Assert.Equal(_black.Id, saved.BlackPlayerId);
			Assert.Equal(_white.Id, saved.WhitePlayerId);
			Assert.Equal(MatchResult.WhiteWin, saved.Result);
			Assert.Equal(session.StartedAtUtc, saved.StartedAtUtc);
			Assert.Equal(session.EndedAtUtc, saved.EndedAtUtc);
			Assert.Equal(
				[(1, BlackOpening), (2, WhiteOpening)],
				saved.Moves.OrderBy(m => m.MoveNumber)
					.Select(m => (m.MoveNumber, new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol))));
		}

		[Fact]
		public async Task WonGame_AppearsInBothPlayersMatchHistory()
		{
			var (manager, recorder, code) = StartMatch(new ScriptedRules(GameOutcome.BlackWon));
			var session = manager.MakeMove(code, _black.Id, BlackOpening).Session!;

			await recorder.RecordIfFinishedAsync(session);

			var blackHistory = await _matchHistory.GetHistoryAsync(_black.Id);
			var whiteHistory = await _matchHistory.GetHistoryAsync(_white.Id);
			Assert.Equal(MatchOutcome.Win, Assert.Single(blackHistory.Matches).Outcome);
			Assert.Equal("white", blackHistory.Matches[0].OpponentName);
			Assert.Equal(MatchOutcome.Loss, Assert.Single(whiteHistory.Matches).Outcome);
			Assert.Equal("black", whiteHistory.Matches[0].OpponentName);
		}

		[Fact]
		public async Task DrawnGame_IsSavedAsDrawForBothPlayers()
		{
			var (manager, recorder, code) = StartMatch(new ScriptedRules(GameOutcome.Draw));
			var session = manager.MakeMove(code, _black.Id, BlackOpening).Session!;

			await recorder.RecordIfFinishedAsync(session);

			Assert.Equal(MatchOutcome.Draw, Assert.Single((await _matchHistory.GetHistoryAsync(_black.Id)).Matches).Outcome);
			Assert.Equal(MatchOutcome.Draw, Assert.Single((await _matchHistory.GetHistoryAsync(_white.Id)).Matches).Outcome);
		}

		[Fact]
		public async Task GameInProgress_IsNotSaved()
		{
			var (manager, recorder, code) = StartMatch(new CheckersGameRules());
			var session = manager.MakeMove(code, _black.Id, BlackOpening).Session!;

			Assert.Null(await recorder.RecordIfFinishedAsync(session));
			Assert.Empty(_context.MatchRecords);
		}

		[Fact]
		public async Task FinishedGame_IsSavedOnlyOnce()
		{
			var (manager, recorder, code) = StartMatch(new CheckersGameRules());
			var session = manager.Resign(code, _white.Id).Session!;

			await recorder.RecordIfFinishedAsync(session);
			var second = await recorder.RecordIfFinishedAsync(session);

			Assert.Null(second);
			Assert.Single(_context.MatchRecords);
		}

		// Store → pull → replay: a whole live game is saved, read back by each player, and replayed
		// move by move to the exact board the game ended on.
		[Theory]
		[InlineData(1, true, false)]
		[InlineData(7, true, false)]
		[InlineData(42, true, false)]
		[InlineData(3, false, true)]
		[InlineData(11, true, true)]
		[InlineData(5, false, false)]
		public async Task FinishedGame_CanBePulledBackAndReplayedToTheFinalBoard(int seed, bool forcedCaptures, bool flyingKings)
		{
			var rules = new GameRuleOptions { ForcedCaptures = forcedCaptures, FlyingKings = flyingKings };
			var (manager, recorder, code) = StartMatch(new CheckersGameRules(), new MatchSettingsDto { Rules = rules, TurnTimeLimitSeconds = 300 });
			var random = new Random(seed);
			var shadow = new Game(rules);
			GameSessionDto session = null!;

			while (!shadow.IsOver)
			{
				var legal = shadow.GetLegalMoves();
				var move = legal[random.Next(legal.Count)];
				var mover = shadow.CurrentTurn == PlayerColor.Black ? _black.Id : _white.Id;
				Assert.True(shadow.TryMove(move.From, move.To).Success);
				var result = manager.MakeMove(code, mover, new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
				Assert.True(result.Succeeded, result.Error);
				session = result.Session!;
			}

			Assert.Equal(SessionStatus.Finished, session.Status);
			var saved = await recorder.RecordIfFinishedAsync(session);
			Assert.True(saved!.Success);
			_context.ChangeTracker.Clear();

			foreach (var player in new[] { _black, _white })
			{
				var match = await _matchHistory.GetMatchAsync(player.Id, saved.MatchId!.Value);
				Assert.NotNull(match);
				Assert.Equal("black", match.BlackPlayerName);
				Assert.Equal("white", match.WhitePlayerName);
				Assert.Equal(session.Moves,
					match.Moves.Select(m => new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol)));

				Assert.Equal(rules, match.Rules);
				Assert.Equal(300, match.TurnTimeLimitSeconds);

				var replay = SessionReplay.FromMoves(match.Moves.Select(m => new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol)), match.Rules);

				Assert.True(replay.IsExact);
				Assert.True(replay.Game.IsOver);
				Assert.Equal(session.Squares, replay.Turns[^1].BoardAfter);
				Assert.Equal(session.Squares, replay.Game.Board.ToSquares());
			}
		}

		// Surrendering from Match History: the player resigns without rejoining, the match leaves both
		// players' active list, and it's saved as a loss for them and a win for the opponent.
		[Fact]
		public async Task SurrenderWithoutRejoining_EndsMatchAndSavesItForBothPlayers()
		{
			var (manager, recorder, code) = StartMatch(new CheckersGameRules());
			manager.MakeMove(code, _black.Id, BlackOpening);
			Assert.Single(manager.GetActiveMatches(_white.Id));

			var resigned = manager.Resign(code, _white.Id);
			Assert.True(resigned.Succeeded, resigned.Error);
			await recorder.RecordIfFinishedAsync(resigned.Session!);

			Assert.Empty(manager.GetActiveMatches(_white.Id));
			Assert.Empty(manager.GetActiveMatches(_black.Id));
			Assert.Equal(MatchOutcome.Loss, Assert.Single((await _matchHistory.GetHistoryAsync(_white.Id)).Matches).Outcome);
			Assert.Equal(MatchOutcome.Win, Assert.Single((await _matchHistory.GetHistoryAsync(_black.Id)).Matches).Outcome);
		}

		// The creator can sit on white, so the saved record must keep each player on their own side.
		[Fact]
		public async Task CreatorPlayingWhite_IsSavedOnTheWhiteSideWithStandardRules()
		{
			var manager = new GameSessionManager(new CheckersGameRules());
			var recorder = new FinishedMatchRecorder(manager, _matchHistory);
			var code = manager.Create(_white.Id, _white.Username, new MatchSettingsDto { CreatorColor = ColorChoice.White }).Session!.MatchCode;
			manager.Join(code, _black.Id, _black.Username);
			manager.MakeMove(code, _black.Id, BlackOpening);

			await recorder.RecordIfFinishedAsync(manager.Resign(code, _white.Id).Session!);

			var saved = await _context.MatchRecords.SingleAsync();
			Assert.Equal(_black.Id, saved.BlackPlayerId);
			Assert.Equal(_white.Id, saved.WhitePlayerId);
			Assert.Equal(MatchResult.BlackWin, saved.Result);
			Assert.Contains("\"American\"", saved.RulesJson);
			Assert.Null(saved.TurnTimeLimitSeconds);
		}

		[Fact]
		public void SurrenderBeforeOpponentJoins_IsRefused()
		{
			var manager = new GameSessionManager(new CheckersGameRules());
			var code = manager.Create(_black.Id, _black.Username).Session!.MatchCode;

			var result = manager.Resign(code, _black.Id);

			Assert.False(result.Succeeded);
			Assert.Single(manager.GetActiveMatches(_black.Id));
		}

		// Each preset: played live, saved, pulled back and replayed with its own rules (board size,
		// capture rules and all), which have to survive the trip through the rules JSON column.
		[Theory]
		[InlineData(GameVariant.Russian)]
		[InlineData(GameVariant.Pool)]
		[InlineData(GameVariant.Brazilian)]
		[InlineData(GameVariant.International)]
		[InlineData(GameVariant.Canadian)]
		[InlineData(GameVariant.Spanish)]
		[InlineData(GameVariant.Italian)]
		[InlineData(GameVariant.Czech)]
		[InlineData(GameVariant.Thai)]
		public async Task PresetGame_IsSavedAndReplaysWithItsRules(GameVariant variant)
		{
			var rules = GameVariants.Find(variant)!.Rules;
			var (manager, recorder, code) = StartMatch(new CheckersGameRules(), new MatchSettingsDto { Rules = rules });
			var random = new Random(9);
			var shadow = new Game(rules);
			GameSessionDto session = null!;

			while (!shadow.IsOver)
			{
				var legal = shadow.GetLegalMoves();
				var move = legal[random.Next(legal.Count)];
				var mover = shadow.CurrentTurn == PlayerColor.Black ? _black.Id : _white.Id;
				Assert.True(shadow.TryMove(move.From, move.To).Success);
				var result = manager.MakeMove(code, mover, new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
				Assert.True(result.Succeeded, result.Error);
				session = result.Session!;
			}

			var saved = await recorder.RecordIfFinishedAsync(session);
			Assert.True(saved!.Success, string.Join(" ", saved.Errors));
			_context.ChangeTracker.Clear();

			var match = await _matchHistory.GetMatchAsync(_white.Id, saved.MatchId!.Value);
			Assert.Equal(rules, match!.Rules);

			var replay = SessionReplay.FromMoves(match.Moves.Select(m => new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol)), match.Rules);
			Assert.True(replay.IsExact);
			Assert.Equal(session.Squares, replay.Game.Board.ToSquares());
		}

		[Fact]
		public async Task FinishedGame_CannotBePulledByAnotherUser()
		{
			var (manager, recorder, code) = StartMatch(new CheckersGameRules());
			manager.MakeMove(code, _black.Id, BlackOpening);
			var saved = await recorder.RecordIfFinishedAsync(manager.Resign(code, _white.Id).Session!);

			var outsider = NewUser("outsider");
			_context.Users.Add(outsider);
			await _context.SaveChangesAsync();

			Assert.Null(await _matchHistory.GetMatchAsync(outsider.Id, saved!.MatchId!.Value));
		}

		private (GameSessionManager Manager, FinishedMatchRecorder Recorder, string Code) StartMatch(IGameRules rules, MatchSettingsDto? settings = null)
		{
			var manager = new GameSessionManager(rules);
			var code = manager.Create(_black.Id, _black.Username, settings).Session!.MatchCode;
			manager.Join(code, _white.Id, _white.Username);
			return (manager, new FinishedMatchRecorder(manager, _matchHistory), code);
		}

		private static User NewUser(string username) => new()
		{
			Username = username,
			PasswordHash = "hash",
			Email = $"{username}@example.com",
			FirstName = username,
			LastName = "Test",
		};

		/// <summary>
		/// Accepts any move and ends the game with <paramref name="outcome"/>.
		/// </summary>
		private sealed class ScriptedRules(GameOutcome outcome) : IGameRules
		{
			public IRulesGame NewGame(GameRuleOptions rules) => new ScriptedGame(outcome);

			private sealed class ScriptedGame(GameOutcome outcome) : IRulesGame
			{
				public Board Board { get; } = Board.CreateStandard();
				public PlayerColor CurrentTurn { get; private set; } = PlayerColor.Black;
				public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;

				public string? TryMove(GameMove move)
				{
					CurrentTurn = PlayerColor.White;
					Outcome = outcome;
					return null;
				}
			}
		}
	}
}

using Checkers.Application.Core.MatchHistory;
using Checkers.Application.Core.Repositories;
using Checkers.Application.Core.Sessions;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

			_context = new CheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
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

		private (GameSessionManager Manager, FinishedMatchRecorder Recorder, string Code) StartMatch(IGameRules rules)
		{
			var manager = new GameSessionManager(rules);
			var code = manager.Create(_black.Id, _black.Username).Session!.MatchCode;
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
			public IRulesGame NewGame() => new ScriptedGame(outcome);

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

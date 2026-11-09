using Checkers.Application.Core.Repositories;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Application.Core.Repositories
{
	/// <summary>
	/// Runs MatchRecordRepository against a real (in-memory SQLite) database so the entity
	/// configurations, relationships and generated queries are all exercised.
	/// </summary>
	public class MatchRecordRepositoryTests : IDisposable
	{
		private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

		private readonly SqliteConnection _connection;
		private readonly CheckersContext _context;
		private readonly MatchRecordRepository _sut;
		private readonly User _alice;
		private readonly User _bob;
		private readonly User _carol;

		public MatchRecordRepositoryTests()
		{
			// The in-memory database lives as long as this open connection.
			_connection = new SqliteConnection("DataSource=:memory:");
			_connection.Open();

			_context = new CheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
			_context.Database.EnsureCreated();

			_alice = NewUser("alice");
			_bob = NewUser("bob");
			_carol = NewUser("carol");
			_context.Users.AddRange(_alice, _bob, _carol);
			_context.SaveChanges();

			_sut = new MatchRecordRepository(_context);
		}

		public void Dispose()
		{
			_context.Dispose();
			_connection.Dispose();
		}

		[Fact]
		public async Task GetByUserIdAsync_WithNoMatches_ReturnsEmpty()
		{
			Assert.Empty(await _sut.GetByUserIdAsync(_alice.Id));
		}

		[Fact]
		public async Task GetByUserIdAsync_ReturnsMatchesFromEitherSide_AndNotOtherUsers()
		{
			var asBlack = await SaveMatchAsync(_alice.Id, _bob.Id, minutesAfterStart: 10);
			var asWhite = await SaveMatchAsync(_bob.Id, _alice.Id, minutesAfterStart: 20);
			var vsComputer = await SaveMatchAsync(_alice.Id, null, minutesAfterStart: 30);
			await SaveMatchAsync(_bob.Id, _carol.Id, minutesAfterStart: 40);

			var matches = await _sut.GetByUserIdAsync(_alice.Id);

			Assert.Equal(3, matches.Count);
			Assert.Equivalent(new[] { asBlack.Id, asWhite.Id, vsComputer.Id }, matches.Select(m => m.Id));
		}

		[Fact]
		public async Task GetByUserIdAsync_ReturnsNewestFirst()
		{
			var oldest = await SaveMatchAsync(_alice.Id, _bob.Id, minutesAfterStart: 5);
			var newest = await SaveMatchAsync(_alice.Id, _bob.Id, minutesAfterStart: 60);
			var middle = await SaveMatchAsync(_alice.Id, null, minutesAfterStart: 30);

			var matches = await _sut.GetByUserIdAsync(_alice.Id);

			Assert.Equal([newest.Id, middle.Id, oldest.Id], matches.Select(m => m.Id));
		}

		[Fact]
		public async Task GetByUserIdAsync_LoadsBothPlayers()
		{
			await SaveMatchAsync(_alice.Id, _bob.Id, minutesAfterStart: 10);

			var match = Assert.Single(await _sut.GetByUserIdAsync(_bob.Id));

			Assert.Equal("alice", match.BlackPlayer?.Username);
			Assert.Equal("bob", match.WhitePlayer?.Username);
		}

		[Fact]
		public async Task AddAsync_StoresResultAndMovesInOrder()
		{
			var match = NewMatch(_alice.Id, null, minutesAfterStart: 10);
			match.Result = MatchResult.Draw;
			match.Moves =
			[
				new() { MoveNumber = 1, FromRow = 2, FromCol = 1, ToRow = 3, ToCol = 2 },
				new() { MoveNumber = 2, FromRow = 5, FromCol = 0, ToRow = 4, ToCol = 1 },
			];

			await _sut.AddAsync(match);
			await _sut.SaveChangesAsync();
			_context.ChangeTracker.Clear();

			var stored = await _context.MatchRecords.Include(m => m.Moves).SingleAsync(m => m.Id == match.Id);

			Assert.Equal(MatchResult.Draw, stored.Result);
			Assert.Null(stored.WhitePlayerId);
			Assert.Equal([1, 2], stored.Moves.OrderBy(m => m.MoveNumber).Select(m => m.MoveNumber));
			Assert.Equal(5, stored.Moves.Single(m => m.MoveNumber == 2).FromRow);
		}

		private async Task<MatchRecord> SaveMatchAsync(int? blackId, int? whiteId, int minutesAfterStart)
		{
			var match = NewMatch(blackId, whiteId, minutesAfterStart);
			await _sut.AddAsync(match);
			await _sut.SaveChangesAsync();
			_context.ChangeTracker.Clear();
			return match;
		}

		private static MatchRecord NewMatch(int? blackId, int? whiteId, int minutesAfterStart) => new()
		{
			BlackPlayerId = blackId,
			WhitePlayerId = whiteId,
			Result = MatchResult.BlackWin,
			StartedAtUtc = Start,
			EndedAtUtc = Start.AddMinutes(minutesAfterStart),
		};

		private static User NewUser(string username) => new()
		{
			Username = username,
			Email = $"{username}@example.com",
			PasswordHash = "hash",
			FirstName = "Test",
			LastName = "User",
		};
	}
}

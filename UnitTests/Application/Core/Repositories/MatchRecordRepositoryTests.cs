using Checkers.Application.Core.Repositories;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UnitTests.TestSupport;

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

			_context = new SqliteCheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
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

		[Fact]
		public async Task GetByIdWithMovesAsync_LoadsPlayersAndMovesInPlayOrder()
		{
			var match = NewMatch(_alice.Id, _bob.Id, minutesAfterStart: 10);
			// Added out of order so the test proves the query sorts them, not the insert order.
			match.Moves =
			[
				new() { MoveNumber = 3, FromRow = 3, FromCol = 0, ToRow = 4, ToCol = 1 },
				new() { MoveNumber = 1, FromRow = 2, FromCol = 1, ToRow = 3, ToCol = 0 },
				new() { MoveNumber = 2, FromRow = 5, FromCol = 2, ToRow = 4, ToCol = 3 },
			];
			await SaveMatchAsync(match);

			var loaded = await _sut.GetByIdWithMovesAsync(match.Id);

			Assert.NotNull(loaded);
			Assert.Equal("alice", loaded.BlackPlayer?.Username);
			Assert.Equal("bob", loaded.WhitePlayer?.Username);
			Assert.Equal([1, 2, 3], loaded.Moves.Select(m => m.MoveNumber));
			Assert.Equal(5, loaded.Moves[1].FromRow);
		}

		[Fact]
		public async Task GetByIdWithMovesAsync_UnknownId_ReturnsNull()
		{
			await SaveMatchAsync(_alice.Id, _bob.Id, minutesAfterStart: 10);

			Assert.Null(await _sut.GetByIdWithMovesAsync(9999));
		}

		[Fact]
		public async Task GetByIdWithMovesAsync_AgainstComputer_LeavesThatPlayerNull()
		{
			var match = await SaveMatchAsync(null, _carol.Id, minutesAfterStart: 10);

			var loaded = await _sut.GetByIdWithMovesAsync(match.Id);

			Assert.Null(loaded!.BlackPlayer);
			Assert.Equal("carol", loaded.WhitePlayer?.Username);
			Assert.Empty(loaded.Moves);
		}

		private async Task<MatchRecord> SaveMatchAsync(int? blackId, int? whiteId, int minutesAfterStart) =>
			await SaveMatchAsync(NewMatch(blackId, whiteId, minutesAfterStart));

		private async Task<MatchRecord> SaveMatchAsync(MatchRecord match)
		{
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

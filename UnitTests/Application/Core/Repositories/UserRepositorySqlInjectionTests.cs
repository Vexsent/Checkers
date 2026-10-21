using Checkers.Application.Core.Repositories;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.Authorization.Policies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Application.Core.Repositories
{
	/// <summary>
	/// Runs UserRepository against a real (in-memory SQLite) database so EF Core's generated SQL
	/// actually executes. A mock or the EF InMemory provider never builds SQL, so it couldn't
	/// catch an injectable query. Every payload must be treated as a literal value.
	/// </summary>
	public class UserRepositorySqlInjectionTests : IDisposable
	{
		private const string Username = "alice";
		private const string Email = "alice@example.com";

		private readonly SqliteConnection _connection;
		private readonly CheckersContext _context;
		private readonly UserRepository _sut;

		public static TheoryData<string> Payloads =>
		[
			"' OR '1'='1",
			"' OR 1=1 --",
			"alice' --",
			"alice'; --",
			"' UNION SELECT * FROM Users --",
			"'; DROP TABLE Users; --",
			"\" OR \"\"=\"",
		];

		public UserRepositorySqlInjectionTests()
		{
			// The in-memory database lives as long as this open connection.
			_connection = new SqliteConnection("DataSource=:memory:");
			_connection.Open();

			_context = new CheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
			_context.Database.EnsureCreated();

			_context.Users.Add(NewUser(Username, Email));
			_context.SaveChanges();

			_sut = new UserRepository(_context);
		}

		public void Dispose()
		{
			_context.Dispose();
			_connection.Dispose();
		}

		[Theory]
		[MemberData(nameof(Payloads))]
		public async Task GetByUsernameAsync_WithInjectionPayload_ReturnsNoUser(string payload)
		{
			var user = await _sut.GetByUsernameAsync(payload);

			Assert.Null(user);
			await AssertUsersTableIntactAsync();
		}

		[Theory]
		[MemberData(nameof(Payloads))]
		public async Task ExistsByUsernameOrEmailAsync_WithInjectionPayload_ReturnsFalse(string payload)
		{
			var exists = await _sut.ExistsByUsernameOrEmailAsync(payload, payload);

			Assert.False(exists);
			await AssertUsersTableIntactAsync();
		}

		[Theory]
		[MemberData(nameof(Payloads))]
		public async Task AddAsync_WithInjectionPayloadInFields_StoresItLiterally(string payload)
		{
			// Email and names aren't covered by UsernamePolicy, so they reach the database as typed.
			var user = NewUser("mallory", payload);
			user.FirstName = payload;
			user.NickName = payload;

			await _sut.AddAsync(user);
			await _sut.SaveChangesAsync();
			_context.ChangeTracker.Clear();

			var stored = await _sut.GetByIdAsync(user.Id);

			Assert.NotNull(stored);
			Assert.Equal(payload, stored.Email);
			Assert.Equal(payload, stored.FirstName);
			Assert.Equal(payload, stored.NickName);
			Assert.Equal(2, await _context.Users.CountAsync());
			Assert.NotNull(await _sut.GetByUsernameAsync(Username));
		}

		[Theory]
		[MemberData(nameof(Payloads))]
		public async Task GetByUsernameAsync_WithPayloadStoredAsData_MatchesOnlyExactValue(string payload)
		{
			// Even if a payload ends up stored, looking it up must match that one row, not every row.
			_context.Users.Add(NewUser(payload, "stored@example.com"));
			await _context.SaveChangesAsync();
			_context.ChangeTracker.Clear();

			var user = await _sut.GetByUsernameAsync(payload);

			Assert.NotNull(user);
			Assert.Equal(payload, user.Username);
			Assert.Equal("stored@example.com", user.Email);
		}

		[Theory]
		[MemberData(nameof(Payloads))]
		public void UsernamePolicy_RejectsInjectionPayloads(string payload)
		{
			// Defense in depth: registration never lets these through as usernames in the first place.
			Assert.NotEmpty(UsernamePolicy.Validate(payload));
		}

		private async Task AssertUsersTableIntactAsync()
		{
			Assert.Equal(1, await _context.Users.CountAsync());
			Assert.NotNull(await _sut.GetByUsernameAsync(Username));
		}

		private static User NewUser(string username, string email) => new()
		{
			Username = username,
			Email = email,
			PasswordHash = "hash",
			FirstName = "Test",
			LastName = "User",
		};
	}
}

using Checkers.Persistence.Data.DataContext;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.TestSupport
{
	/// <summary>
	/// CheckersContext for tests that run against an in-memory SQLite database.
	/// The real schema is database-first, so the entity configurations exclude every table from
	/// migrations. EnsureCreated honours that too and would create no tables, so this context
	/// clears the flag on its own copy of the model. Production code is unaffected.
	/// </summary>
	public class SqliteCheckersContext(DbContextOptions<CheckersContext> options) : CheckersContext(options)
	{
		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			foreach (var entityType in builder.Model.GetEntityTypes())
			{
				entityType.SetIsTableExcludedFromMigrations(false);
			}
		}
	}
}

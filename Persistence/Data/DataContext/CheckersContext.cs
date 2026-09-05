using Checkers.Domain.Entities;
using Checkers.Persistence.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Checkers.Persistence.Data.DataContext
{
	public class CheckersContext(DbContextOptions<CheckersContext> options) : DbContext(options)
	{

		public DbSet<User> Users => Set<User>();

		protected override void OnConfiguring(DbContextOptionsBuilder options)
		{
			base.OnConfiguring(options);
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			builder.ApplyConfigurationsFromAssembly(typeof(UserEntityConfiguration).Assembly);


			base.OnModelCreating(builder);		
		}
	}
}

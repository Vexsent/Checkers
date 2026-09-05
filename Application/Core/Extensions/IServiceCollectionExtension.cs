using Checkers.Persistence.Data.DataContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Checkers.Application.Core.Extensions
{
	public static class IServiceCollectionExtension
	{
		public static IServiceCollection AddCheckersDbContext(this IServiceCollection services, string connectionString)
		{
			services.AddDbContext<CheckersContext>(options =>
			{
				options.UseSqlServer(connectionString);
			});

			return services;
		}
	}
}

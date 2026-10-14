using Checkers.Application.Core._interface;
using Checkers.Application.Core.Authorization;
using Checkers.Persistence.Data.DataContext;
using Microsoft.EntityFrameworkCore;
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

		public static IServiceCollection AddAuthorization(this IServiceCollection services)
		{
			services.AddScoped<IAuthService, AuthService>();
			return services;
		}
}

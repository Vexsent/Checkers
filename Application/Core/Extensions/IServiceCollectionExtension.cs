using Checkers.Application.Core.Authorization;
using Checkers.Application.Core.Options;
using Checkers.Application.Core.Repositories;
using Checkers.Application.Core.Security;
using Checkers.Application.Core.Services;
using Checkers.Persistence.Data.DataContext;
using Checkers.Persistence.Data.IRepositories;
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

		public static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
		{
			services.AddScoped<IAuthService, AuthService>();
			return services;
		}

		public static IServiceCollection AddRepositories(this IServiceCollection services)
		{
			services.AddScoped<IUserRepository, UserRepository>();
			return services;
		}

		/// <summary>
		/// Registers the password hasher and JWT token service. Both read their config via
		/// <see cref="AppOptions"/> (bound separately by AddOptions) — call that too.
		/// Call AddAuthorizationServices/AddRepositories alongside this — AuthService depends
		/// on all three.
		/// </summary>
		public static IServiceCollection AddCheckersAuth(this IServiceCollection services)
		{
			services.AddScoped<IPasswordHasher, PasswordHasher>();
			services.AddScoped<IJwtTokenService, JwtTokenService>();

			return services;
		}


		public static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
			return services;
		}
	}
}

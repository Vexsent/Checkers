using Checkers.Application.Core.Authorization;
using Checkers.Application.Core.Options;
using Checkers.Application.Core.Repositories;
using Checkers.Application.Core.Security;
using Checkers.Application.Core.Services;
using Checkers.Persistence.Data.DataContext;
using Checkers.Persistence.Data.IRepositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Checkers.Application.Core.Extensions
{
	public static class IServiceCollectionExtension
	{
		/// <summary>
		/// Registers <see cref="CheckersContext"/> against the primary connection string, falling back to
		/// <paramref name="fallbackConnectionString"/> when the primary server can't be reached at startup.
		/// </summary>
		public static IServiceCollection AddCheckersDbContext(this IServiceCollection services, string connectionString, string? fallbackConnectionString = null)
		{
			var resolved = string.IsNullOrWhiteSpace(fallbackConnectionString) || CanConnect(connectionString)
				? connectionString
				: fallbackConnectionString;

			services.AddDbContext<CheckersContext>(options =>
			{
				options.UseSqlServer(resolved);
			});

			return services;
		}

		private static bool CanConnect(string connectionString)
		{
			try
			{
				// Short timeout so an unreachable primary doesn't stall startup for the default 15s.
				var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
				using var connection = new SqlConnection(builder.ConnectionString);
				connection.Open();
				return true;
			}
			catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
			{
				return false;
			}
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
			services.AddHttpContextAccessor();
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

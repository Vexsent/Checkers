using Checkers.Application.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Checkers.Application.Core.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Checkers.Client.Extensions
{
    public static class AuthorizeExtensions
    {
        private static IServiceProvider _sp = default!;
        public static void AddAuthorizationServiceExtensions(this IServiceCollection services)
        {
            _sp = services.BuildServiceProvider();
        }

        private static IAuthService Auth => _sp.GetRequiredService<IAuthService>();

        public static ClaimsPrincipal User => Auth.User;
        public static IEnumerable<string> Roles => Auth.Roles;

        public static async Task<IQueryable<T>> IsAuthorized<T>(this IQueryable<T> query, AuthorizationPolicy policy) where T : class
        {
            try
            {
                var auth = await Auth.AuthorizeAsync(policy);

                if (!auth.Succeeded)
                {
                    throw new UnauthorizedAccessException("User does not have the required role.");
                }

                return query;

            }
            catch (UnauthorizedAccessException)
            {
                return new List<T>().AsQueryable();
            }

        }

        public static async Task<IQueryable<T>> IsAuthorized<T>(this IQueryable<T> query, string policy) where T : class
        {
            try
            {
                var auth = await Auth.AuthorizeAsync(policy);

                if (!auth.Succeeded)
                {
                    throw new UnauthorizedAccessException("User does not have the required role.");
                }

                return query;

            }
            catch (UnauthorizedAccessException)
            {
                return new List<T>().AsQueryable();
            }
        }

        public static async Task<IQueryable<T>> ToAsyncQuerable<T>(this ICollection<T> query) where T : class
        {
            return query.AsQueryable();
        }

        public static IQueryable<T> IncludeByRoles<T>(this IQueryable<T> query, Func<IQueryable<T>, IQueryable<T>> include) where T : class
        {
            query = include(query);

            return query;
        }
    }
}

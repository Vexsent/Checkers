using Checkers.Shared.Authentication.Handlers;
using Checkers.Shared.Authentication.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Checkers.Shared.Extensions
{
    public static class IServiceCollectionExtension
    {
        public static IServiceCollection AddSharedExtensions(this IServiceCollection services)
        {
            services.AddScoped<IAuthorizationHandler, AnyPolicyHandler>();

            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, CheckerOptions>();

            return services;
        }
    }
}

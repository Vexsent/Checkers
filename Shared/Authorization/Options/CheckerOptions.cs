using Checkers.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Checkers.Shared.Authorization.Policies;

namespace Checkers.Shared.Authentication.Options
{
    public class CheckerOptions : IConfigureOptions<AuthorizationOptions>
    {
        public void Configure(AuthorizationOptions options)
        {
            options.AddPolicy(CheckersPolicy.Create, policy =>
                { 
                    policy.RequireRole(Roles.GetAllRoles());
                });
            options.AddPolicy(CheckersPolicy.Read, policy =>
                {
                    policy.RequireRole(Roles.GetAllRoles());
                });

        }
    }
}

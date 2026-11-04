using Checkers.Shared.Authentication.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Checkers.Shared.Authentication.Handlers
{
    public class AnyPolicyHandler(IServiceProvider serviceProvider) : AuthorizationHandler<AnyPolicyRequirement>
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPolicyRequirement requirement)
        {
            var authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();

            foreach (var policy in requirement.Policies)
            {
                var result = await authorizationService.AuthorizeAsync(context.User, null, policy);

                if (result.Succeeded)
                {
                    context.Succeed(requirement);

                    return;
                }
            }
        }
    }
}

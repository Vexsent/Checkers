using Microsoft.AspNetCore.Authorization;

namespace Checkers.Shared.Authentication.Requirements
{
    public class AnyPolicyRequirement(params string[] policies) : IAuthorizationRequirement
    {
        public IReadOnlyList<string> Policies { get; } = policies ?? throw new ArgumentNullException(nameof(policies));
    }
}

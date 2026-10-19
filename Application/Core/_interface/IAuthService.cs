using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Checkers.Application.Core.Services
{
	public interface IAuthService
	{
		ClaimsPrincipal User { get; }
		IEnumerable<string> Roles { get; }
		Task<AuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy);
		Task<AuthorizationResult> AuthorizeAsync(string policyName);
		Task<AuthResult> RegisterAsync(RegisterRequestDto request);
		Task<AuthResult> LoginAsync(LoginRequestDto request);
	}
}

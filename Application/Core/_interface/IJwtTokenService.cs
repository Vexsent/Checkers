using System.Security.Claims;
using Checkers.Domain.Entities;

namespace Checkers.Application.Core.Services
{
	public record AuthToken(string Token, DateTime ExpiresAtUtc);

	public interface IJwtTokenService
	{
		AuthToken GenerateToken(User user);

		/// <summary>
		/// The principal for the current request, built from its validated JWT. Returns an
		/// empty (unauthenticated) principal when there is no request or no token.
		/// </summary>
		ClaimsPrincipal GetCurrentUser();

		/// <summary>
		/// The role claims carried by the current request's JWT.
		/// </summary>
		IEnumerable<string> GetCurrentUserRoles();
	}
}

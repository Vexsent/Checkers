using Checkers.Domain.Entities;

namespace Checkers.Application.Core.Services
{
	public record AuthToken(string Token, DateTime ExpiresAtUtc);

	public interface IJwtTokenService
	{
		AuthToken GenerateToken(User user);
	}
}

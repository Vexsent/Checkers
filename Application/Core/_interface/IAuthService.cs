using Checkers.Shared.DTOs;

namespace Checkers.Application.Core.Services
{
	public interface IAuthService
	{
		Task<AuthResult> RegisterAsync(RegisterRequestDto request);
		Task<AuthResult> LoginAsync(LoginRequestDto request);
	}
}

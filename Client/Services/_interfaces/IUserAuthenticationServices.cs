using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public interface IUserAuthenticationServices
	{
		/// <summary>Registers and signs the user in. Returns error messages; empty means success.</summary>
		Task<IReadOnlyList<string>> RegisterUserAsync(RegisterRequestDto user);
		/// <summary>Signs the user in. Returns error messages; empty means success.</summary>
		Task<IReadOnlyList<string>> LoginUserAsync(LoginRequestDto user);
		Task LogoutUserAsync();
	}
}

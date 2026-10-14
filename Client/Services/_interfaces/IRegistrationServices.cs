using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public interface IRegistrationServices
	{
		Task RegisterUserAsync(RegisterRequestDto user);
	}
}
using Checkers.Shared.DTOs;

namespace Checkers.Application.Core
{
	public class AuthResult
	{
		public bool Success { get; private init; }
		public IReadOnlyList<string> Errors { get; private init; } = [];
		public AuthResponseDto? Response { get; private init; }

		public static AuthResult Succeeded(AuthResponseDto response) =>
			new() { Success = true, Response = response };

		public static AuthResult Failed(IEnumerable<string> errors) =>
			new() { Success = false, Errors = errors.ToList() };
	}
}

namespace Checkers.Shared.DTOs
{
	public class AuthResponseDto
	{
		public string Token { get; set; } = default!;
		public DateTime ExpiresAtUtc { get; set; }
		public UserDto User { get; set; } = default!;
	}
}

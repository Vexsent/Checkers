namespace Checkers.Shared.DTOs
{
	public class UserDto
	{
		public int Id { get; set; }
		public string Username { get; set; } = default!;
		public string FirstName { get; set; } = default!;
		public string LastName { get; set; } = default!;
		public string? MiddleName { get; set; } = "";
		public string? NickName { get; set; } = null;

		public string Name => $"{NickName ?? FirstName},{MiddleName} {LastName}";
	}
}

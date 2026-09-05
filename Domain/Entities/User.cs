namespace Checkers.Domain.Entities
{
	public class User
	{
		public int Id { get; set; }
		public string Username { get; set; } = default!;
		public string Password { get; set; } = default!;
		public string Email { get; set; } = default!;
		public string FirstName { get; set; } = default!;
		public string LastName { get; set; } = default!;
		public string? MiddleName { get; set; }
		public string? NickName { get; set; }

	}
}

namespace Checkers.Domain.Entities
{
	public class User
	{
		public int Id { get; set; }
		/// <summary>
		/// The username of the user. This should be unique across all users in the system.
		/// </summary>
		public string Username { get; set; } = default!;
		/// <summary>
		/// Password hash for the user. This should be a securely hashed representation of the user's password, not the plain text password itself.
		/// </summary>
		public string PasswordHash { get; set; } = default!;
		public string Email { get; set; } = default!;
		public string FirstName { get; set; } = default!;
		public string LastName { get; set; } = default!;
		public string? MiddleName { get; set; }
		public string? NickName { get; set; }

	}
}

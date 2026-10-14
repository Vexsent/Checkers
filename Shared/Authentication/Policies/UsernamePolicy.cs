namespace Checkers.Shared.Authentication.Policies
{
	/// <summary>
	/// Validation rules for usernames. Shared so both the Server (enforcing on register)
	/// and the Client (validating a form before it submits) run the exact same rules.
	/// </summary>
	public static class UsernamePolicy
	{
		public const int MinLength = 3;
		public const int MaxLength = 30;

		public static IReadOnlyList<string> Validate(string? username)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(username))
			{
				errors.Add("Username is required.");
				return errors;
			}

			if (username.Length < MinLength || username.Length > MaxLength)
				errors.Add($"Username must be between {MinLength} and {MaxLength} characters.");

			if (!username.All(c => char.IsLetterOrDigit(c) || c == '_'))
				errors.Add("Username may only contain letters, digits, and underscores.");

			if (!char.IsLetter(username[0]))
				errors.Add("Username must start with a letter.");

			return errors;
		}
	}
}

namespace Checkers.Shared.Authorization.Policies
{
	/// <summary>
	/// Validation rules for passwords. Shared so both the Server (enforcing on register)
	/// and the Client (validating a form before it submits) run the exact same rules.
	/// This only validates the plaintext password on submit — hashing happens server-side.
	/// </summary>
	public static class PasswordPolicy
	{
		public const int MinLength = 8;

		public static IReadOnlyList<string> Validate(string? password)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(password))
			{
				errors.Add("Password is required.");
				return errors;
			}

			if (password.Length < MinLength)
				errors.Add($"Password must be at least {MinLength} characters.");

			if (!password.Any(char.IsDigit))
				errors.Add("Password must contain at least one digit.");

			if (!password.Any(char.IsLetter))
				errors.Add("Password must contain at least one letter.");

			return errors;
		}
	}
}

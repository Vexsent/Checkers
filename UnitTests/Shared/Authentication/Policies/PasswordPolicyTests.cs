using Checkers.Shared.Authorization.Policies;

namespace UnitTests.Shared.Authentication.Policies
{
	public class PasswordPolicyTests
	{
		[Fact]
		public void Validate_AcceptsWellFormedPassword()
		{
			Assert.Empty(PasswordPolicy.Validate("Password1"));
		}

		[Fact]
		public void Validate_RejectsNullOrWhitespace()
		{
			Assert.NotEmpty(PasswordPolicy.Validate(null));
			Assert.NotEmpty(PasswordPolicy.Validate("   "));
		}

		[Fact]
		public void Validate_RejectsTooShort()
		{
			Assert.NotEmpty(PasswordPolicy.Validate("Pw1"));
		}

		[Fact]
		public void Validate_RejectsMissingDigit()
		{
			Assert.NotEmpty(PasswordPolicy.Validate("Password"));
		}

		[Fact]
		public void Validate_RejectsMissingLetter()
		{
			Assert.NotEmpty(PasswordPolicy.Validate("12345678"));
		}
	}
}

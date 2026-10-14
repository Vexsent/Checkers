using Checkers.Shared.Authentication.Policies;

namespace UnitTests.Shared.Authentication.Policies
{
	public class UsernamePolicyTests
	{
		[Theory]
		[InlineData("abc")]
		[InlineData("Vexsent_42")]
		public void Validate_AcceptsWellFormedUsernames(string username)
		{
			Assert.Empty(UsernamePolicy.Validate(username));
		}

		[Fact]
		public void Validate_RejectsNullOrWhitespace()
		{
			Assert.NotEmpty(UsernamePolicy.Validate(null));
			Assert.NotEmpty(UsernamePolicy.Validate("   "));
		}

		[Fact]
		public void Validate_RejectsTooShort()
		{
			Assert.NotEmpty(UsernamePolicy.Validate("ab"));
		}

		[Fact]
		public void Validate_RejectsInvalidCharacters()
		{
			Assert.NotEmpty(UsernamePolicy.Validate("bad name!"));
		}

		[Fact]
		public void Validate_RejectsUsernameNotStartingWithALetter()
		{
			Assert.NotEmpty(UsernamePolicy.Validate("1abc"));
		}
	}
}

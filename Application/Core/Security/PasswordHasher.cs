using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Checkers.Application.Core.Security
{
	/// <summary>
	/// Wraps ASP.NET Core Identity's PasswordHasher&lt;T&gt; (PBKDF2-based) so we get a
	/// well-vetted hash implementation without pulling in all of Identity's user/role
	/// store machinery or a third-party package.
	/// </summary>
	public class PasswordHasher : IPasswordHasher
	{
		private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _inner = new();

		public string Hash(User user, string password) =>
			_inner.HashPassword(user, password);

		public bool Verify(User user, string hashedPassword, string providedPassword) =>
			_inner.VerifyHashedPassword(user, hashedPassword, providedPassword)
				is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
	}
}

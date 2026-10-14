using Checkers.Domain.Entities;

namespace Checkers.Application.Core.Services
{
	public interface IPasswordHasher
	{
		string Hash(User user, string password);

		/// <summary>true if <paramref name="providedPassword"/> matches the stored hash.</summary>
		bool Verify(User user, string hashedPassword, string providedPassword);
	}
}

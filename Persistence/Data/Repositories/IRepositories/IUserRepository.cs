using Checkers.Domain.Entities;

namespace Checkers.Persistence.Data.IRepositories
{
	public interface IUserRepository
	{
		Task<User?> GetByIdAsync(int id);
		Task<User?> GetByUsernameAsync(string username);
		Task<bool> ExistsByUsernameOrEmailAsync(string username, string email);
		Task AddAsync(User user);
		Task SaveChangesAsync();
	}
}

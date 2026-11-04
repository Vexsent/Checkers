using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Persistence.Data.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Checkers.Application.Core.Repositories
{
	public class UserRepository(CheckersContext context) : IUserRepository
	{
		public Task<User?> GetByIdAsync(int id) =>
			context.Users.FirstOrDefaultAsync(u => u.Id == id);

		public Task<User?> GetByUsernameAsync(string username) =>
			context.Users.FirstOrDefaultAsync(u => u.Username == username);

		public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email) =>
			context.Users.AnyAsync(u => u.Username == username || u.Email == email);

		public async Task AddAsync(User user) =>
			await context.Users.AddAsync(user);

		public Task SaveChangesAsync() =>
			context.SaveChangesAsync();
	}
}

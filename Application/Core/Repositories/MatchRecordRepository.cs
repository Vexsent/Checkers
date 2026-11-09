using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Persistence.Data.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Checkers.Application.Core.Repositories
{
	public class MatchRecordRepository(CheckersContext context) : IMatchRecordRepository
	{
		public async Task<IReadOnlyList<MatchRecord>> GetByUserIdAsync(int userId) =>
			await context.MatchRecords
				.AsNoTracking()
				.Include(m => m.BlackPlayer)
				.Include(m => m.WhitePlayer)
				.Where(m => m.BlackPlayerId == userId || m.WhitePlayerId == userId)
				.OrderByDescending(m => m.EndedAtUtc)
				.ToListAsync();

		public async Task AddAsync(MatchRecord match) =>
			await context.MatchRecords.AddAsync(match);

		public Task SaveChangesAsync() =>
			context.SaveChangesAsync();
	}
}

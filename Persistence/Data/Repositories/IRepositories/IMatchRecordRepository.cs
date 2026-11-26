using Checkers.Domain.Entities;

namespace Checkers.Persistence.Data.IRepositories
{
	public interface IMatchRecordRepository
	{
		/// <summary>
		/// Every match the user played on either side, newest first, with both players loaded (moves are not).
		/// </summary>
		Task<IReadOnlyList<MatchRecord>> GetByUserIdAsync(int userId);
		Task AddAsync(MatchRecord match);
		Task SaveChangesAsync();
	}
}

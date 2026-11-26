using Checkers.Domain.Entities;

namespace Checkers.Persistence.Data.IRepositories
{
	public interface IMatchRecordRepository
	{
		/// <summary>
		/// Every match the user played on either side, newest first, with both players loaded (moves are not).
		/// </summary>
		Task<IReadOnlyList<MatchRecord>> GetByUserIdAsync(int userId);
		/// <summary>
		/// One match with both players and its moves in play order, or null if there's no such match.
		/// </summary>
		Task<MatchRecord?> GetByIdWithMovesAsync(int matchId);
		Task AddAsync(MatchRecord match);
		Task SaveChangesAsync();
	}
}

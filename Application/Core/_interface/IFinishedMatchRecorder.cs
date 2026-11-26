using Checkers.Shared.DTOs;

namespace Checkers.Application.Core.Services
{
	public interface IFinishedMatchRecorder
	{
		/// <summary>
		/// Saves a live match to both players' match history once it has finished. Returns null
		/// when there is nothing to save: the match is still going, or it was already saved.
		/// </summary>
		Task<RecordMatchResult?> RecordIfFinishedAsync(GameSessionDto session);
	}
}

using Checkers.Shared.DTOs;

namespace Checkers.Shared.Hubs
{
	/// <summary>
	/// Messages the server pushes to players in a match.
	/// </summary>
	public interface IGameHubClient
	{
		Task SessionUpdated(GameSessionDto session);
		Task PlayerConnectionChanged(int userId, bool isConnected);
	}

	public static class GameHubRoutes
	{
		public const string Path = "/hubs/game";
	}
}

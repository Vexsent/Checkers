using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Checkers.Server.Hubs
{
	/// <summary>
	/// Publishes a changed match: saves it to match history if it just finished, then sends the
	/// new state to both players. Shared by the hub and by controllers that change a match
	/// without a live connection (e.g. resigning from Match History).
	/// </summary>
	public class SessionBroadcaster(
		IFinishedMatchRecorder recorder,
		IHubContext<GameHub, IGameHubClient> hub,
		ILogger<SessionBroadcaster> logger)
	{
		public async Task PublishAsync(GameSessionDto session)
		{
			// Save before telling the players, so the game is already in their match history
			// when they see that it ended.
			await RecordIfFinishedAsync(session);
			await hub.Clients.Group(session.MatchCode).SessionUpdated(session);
		}

		private async Task RecordIfFinishedAsync(GameSessionDto session)
		{
			// The game itself is over either way, so a failed save is logged rather than sent to the players.
			try
			{
				var saved = await recorder.RecordIfFinishedAsync(session);
				if (saved is { Success: false })
					logger.LogError("Could not save match {MatchCode}: {Errors}", session.MatchCode, string.Join(" ", saved.Errors));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Could not save match {MatchCode}.", session.MatchCode);
			}
		}
	}
}

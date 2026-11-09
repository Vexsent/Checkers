using System.Security.Claims;
using Checkers.Application.Core.Services;
using Checkers.Application.Core.Sessions;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Checkers.Shared.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Checkers.Server.Hubs
{
	/// <summary>
	/// Live match traffic. The user always comes from the JWT, never from the message, and each
	/// match code doubles as the SignalR group for its two players. Failures come back as a
	/// SessionResult because SignalR prefixes HubException messages with its own text.
	/// Finished matches are saved to both players' match history.
	/// </summary>
	[Authorize]
	public class GameHub(IGameSessionManager sessions, IMatchHistoryService matchHistory, ILogger<GameHub> logger) : Hub<IGameHubClient>
	{
		private int UserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
		private string Username => Context.User!.FindFirstValue(ClaimTypes.Name)!;

		public async Task<SessionResult> CreateMatch()
		{
			var result = sessions.Create(UserId, Username);
			if (result.Session is { } session)
				await Groups.AddToGroupAsync(Context.ConnectionId, session.MatchCode);

			return result;
		}

		public async Task<SessionResult> JoinMatch(string matchCode)
		{
			var result = sessions.Join(matchCode, UserId, Username);
			if (result.Session is { } session)
			{
				await Groups.AddToGroupAsync(Context.ConnectionId, session.MatchCode);
				await Clients.OthersInGroup(session.MatchCode).PlayerConnectionChanged(UserId, true);
				await Clients.Group(session.MatchCode).SessionUpdated(session);
			}

			return result;
		}

		public Task<SessionResult> MakeMove(string matchCode, GameMove move) =>
			BroadcastAsync(sessions.MakeMove(matchCode, UserId, move));

		public Task<SessionResult> Resign(string matchCode) =>
			BroadcastAsync(sessions.Resign(matchCode, UserId));

		public override async Task OnDisconnectedAsync(Exception? exception)
		{
			foreach (var matchCode in sessions.GetActiveMatchCodes(UserId))
				await Clients.OthersInGroup(matchCode).PlayerConnectionChanged(UserId, false);

			await base.OnDisconnectedAsync(exception);
		}

		/// <summary>
		/// Only MakeMove and Resign come through here, and a match can finish only once, so a
		/// finished match is saved exactly once.
		/// </summary>
		private async Task<SessionResult> BroadcastAsync(SessionResult result)
		{
			if (result.Session is { } session)
			{
				await Clients.Group(session.MatchCode).SessionUpdated(session);

				if (session.Status == SessionStatus.Finished)
					await SaveToMatchHistoryAsync(session);
			}

			return result;
		}

		private async Task SaveToMatchHistoryAsync(GameSessionDto session)
		{
			// The game itself is already over and broadcast, so a failed save is logged rather
			// than sent back to the player as a move error.
			try
			{
				var saved = await matchHistory.RecordMatchAsync(session.BlackPlayerId, LiveMatchRecords.ToRecordRequest(session));
				if (!saved.Success)
					logger.LogWarning("Match {MatchCode} was not saved to match history: {Errors}", session.MatchCode, string.Join(" ", saved.Errors));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Saving match {MatchCode} to match history failed.", session.MatchCode);
			}
		}
	}
}

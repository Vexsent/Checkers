using System.Security.Claims;
using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
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
	/// </summary>
	[Authorize]
	public class GameHub(
		IGameSessionManager sessions,
		IFinishedMatchRecorder recorder,
		ILogger<GameHub> logger) : Hub<IGameHubClient>
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

		public Task<SessionResult> OfferDraw(string matchCode) =>
			BroadcastAsync(sessions.OfferDraw(matchCode, UserId));

		public Task<SessionResult> AnswerDraw(string matchCode, bool accept) =>
			BroadcastAsync(sessions.AnswerDraw(matchCode, UserId, accept));

		public override async Task OnDisconnectedAsync(Exception? exception)
		{
			foreach (var matchCode in sessions.GetActiveMatchCodes(UserId))
				await Clients.OthersInGroup(matchCode).PlayerConnectionChanged(UserId, false);

			await base.OnDisconnectedAsync(exception);
		}

		private async Task<SessionResult> BroadcastAsync(SessionResult result)
		{
			if (result.Session is { } session)
			{
				// Save before telling the players, so the game is already in their match history
				// when they see that it ended.
				await RecordIfFinishedAsync(session);
				await Clients.Group(session.MatchCode).SessionUpdated(session);
			}

			return result;
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

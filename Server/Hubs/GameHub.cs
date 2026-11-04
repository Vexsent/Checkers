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
	public class GameHub(IGameSessionManager sessions) : Hub<IGameHubClient>
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

		private async Task<SessionResult> BroadcastAsync(SessionResult result)
		{
			if (result.Session is { } session)
				await Clients.Group(session.MatchCode).SessionUpdated(session);

			return result;
		}
	}
}

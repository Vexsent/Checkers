using System.Security.Claims;
using Checkers.Application.Core.Services;
using Checkers.Application.Core.Sessions;
using Checkers.Shared.DTOs;
using Checkers.Shared.GameModels;
using Checkers.Shared.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Checkers.Server.Hubs
{
	/// <summary>
	/// Live match traffic. The user always comes from the JWT, never from the message, and each
	/// match code doubles as the SignalR group for its two players.
	/// </summary>
	[Authorize]
	public class GameHub(IGameSessionManager sessions) : Hub<IGameHubClient>
	{
		private int UserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
		private string Username => Context.User!.FindFirstValue(ClaimTypes.Name)!;

		public async Task<GameSessionDto> CreateMatch()
		{
			var session = Unwrap(sessions.Create(UserId, Username));
			await Groups.AddToGroupAsync(Context.ConnectionId, session.MatchCode);
			return session;
		}

		public async Task<GameSessionDto> JoinMatch(string matchCode)
		{
			var session = Unwrap(sessions.Join(matchCode, UserId, Username));
			await Groups.AddToGroupAsync(Context.ConnectionId, session.MatchCode);
			await Clients.OthersInGroup(session.MatchCode).PlayerConnectionChanged(UserId, true);
			await Clients.Group(session.MatchCode).SessionUpdated(session);
			return session;
		}

		public async Task MakeMove(string matchCode, GameMove move)
		{
			var session = Unwrap(sessions.MakeMove(matchCode, UserId, move));
			await Clients.Group(session.MatchCode).SessionUpdated(session);
		}

		public async Task Resign(string matchCode)
		{
			var session = Unwrap(sessions.Resign(matchCode, UserId));
			await Clients.Group(session.MatchCode).SessionUpdated(session);
		}

		public override async Task OnDisconnectedAsync(Exception? exception)
		{
			foreach (var matchCode in sessions.GetActiveMatchCodes(UserId))
				await Clients.OthersInGroup(matchCode).PlayerConnectionChanged(UserId, false);

			await base.OnDisconnectedAsync(exception);
		}

		// HubException is the only exception type whose message reaches the caller.
		private static GameSessionDto Unwrap(SessionResult result) =>
			result.Session ?? throw new HubException(result.Error);
	}
}

using System.Threading.RateLimiting;
using Checkers.Application.Core.Options;
using Checkers.Server.Hubs;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Checkers.Server.RateLimiting
{
	/// <summary>
	/// Limits live-match hub calls per user. HTTP rate limiting only sees the connection being
	/// opened; after that, every call travels over the same WebSocket, so it has to be counted here.
	/// Creating and joining matches share the strict "Lobby" limit (match-code guessing, match spam);
	/// moves and the rest share the looser "Game" limit.
	/// </summary>
	public sealed class HubRateLimitFilter(RateLimitingOptions options) : IHubFilter, IDisposable
	{
		private static readonly HashSet<string> LobbyMethods = [nameof(GameHub.CreateMatch), nameof(GameHub.JoinMatch)];

		private readonly PartitionedRateLimiter<string> _lobby =
			PartitionedRateLimiter.Create<string, string>(user => RateLimitingExtensions.FixedWindow(user, options.Lobby));

		private readonly PartitionedRateLimiter<string> _game =
			PartitionedRateLimiter.Create<string, string>(user => RateLimitingExtensions.FixedWindow(user, options.Game));

		public async ValueTask<object?> InvokeMethodAsync(
			HubInvocationContext invocationContext,
			Func<HubInvocationContext, ValueTask<object?>> next)
		{
			var limiter = LobbyMethods.Contains(invocationContext.HubMethodName) ? _lobby : _game;
			// The hub requires a signed-in user, so UserIdentifier is set; the connection id is a fallback.
			var user = invocationContext.Context.UserIdentifier ?? invocationContext.Context.ConnectionId;

			using var lease = limiter.AttemptAcquire(user);
			if (lease.IsAcquired)
				return await next(invocationContext);

			// Hub methods report failures as a SessionResult, which the client already shows to the player.
			if (invocationContext.HubMethod.ReturnType == typeof(Task<SessionResult>))
				return SessionResult.Failure(RateLimitingExtensions.TooManyRequestsMessage);

			throw new HubException(RateLimitingExtensions.TooManyRequestsMessage);
		}

		public void Dispose()
		{
			_lobby.Dispose();
			_game.Dispose();
		}
	}
}

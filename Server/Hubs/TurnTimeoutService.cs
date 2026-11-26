using Checkers.Application.Core.Services;

namespace Checkers.Server.Hubs
{
	/// <summary>
	/// Enforces turn time limits. Once a second it ends every match whose current turn has run out
	/// (that player resigns), then saves it and tells both players, the same way a normal ending is published.
	/// </summary>
	public class TurnTimeoutService(
		IGameSessionManager sessions,
		IServiceScopeFactory scopeFactory,
		ILogger<TurnTimeoutService> logger) : BackgroundService
	{
		private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			using var timer = new PeriodicTimer(CheckInterval);
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				try
				{
					var expired = sessions.ExpireOverdueTurns();
					if (expired.Count == 0)
						continue;

					// The broadcaster saves through scoped services (the database context), so one scope per sweep.
					await using var scope = scopeFactory.CreateAsyncScope();
					var broadcaster = scope.ServiceProvider.GetRequiredService<SessionBroadcaster>();
					foreach (var session in expired)
					{
						logger.LogInformation("Match {MatchCode} ended: {Player}'s turn timed out.", session.MatchCode, session.CurrentPlayer);
						await broadcaster.PublishAsync(session);
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// One bad sweep mustn't stop the clock for every other match.
					logger.LogError(ex, "Checking turn time limits failed.");
				}
			}
		}
	}
}

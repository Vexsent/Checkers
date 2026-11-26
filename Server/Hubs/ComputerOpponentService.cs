using System.Collections.Concurrent;
using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Server.Hubs
{
	/// <summary>
	/// Plays the computer's side of computer matches. It checks a few times a second for matches where
	/// the computer is to move (or has been offered a draw), thinks off the request threads, then plays
	/// its turn one hop at a time with a short pause so the player can follow a multi-jump. Each step is
	/// published exactly like a player's move.
	/// </summary>
	public class ComputerOpponentService(
		IGameSessionManager sessions,
		IServiceScopeFactory scopeFactory,
		ILogger<ComputerOpponentService> logger) : BackgroundService
	{
		private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);
		// Long enough to see what happened, short enough not to drag.
		private static readonly TimeSpan StepPause = TimeSpan.FromMilliseconds(450);

		// Matches the computer is already thinking about or moving in.
		private readonly ConcurrentDictionary<string, byte> _busy = new(StringComparer.OrdinalIgnoreCase);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			using var timer = new PeriodicTimer(CheckInterval);
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				try
				{
					foreach (var session in sessions.GetMatchesAwaitingComputer())
					{
						if (_busy.TryAdd(session.MatchCode, 0))
							_ = Task.Run(() => ActAsync(session, stoppingToken), stoppingToken);
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					logger.LogError(ex, "Checking for computer turns failed.");
				}
			}
		}

		private async Task ActAsync(GameSessionDto session, CancellationToken stoppingToken)
		{
			try
			{
				if (session.ComputerColor is not { } computer || session.ComputerDifficulty is not { } level)
					return;

				var (game, played) = Game.Replay(session.Rules, session.Moves);
				if (played != session.Moves.Length)
				{
					logger.LogError("Match {MatchCode}: its moves don't replay, so the computer can't play.", session.MatchCode);
					return;
				}

				var player = new ComputerPlayer(level);
				await using var scope = scopeFactory.CreateAsyncScope();
				var broadcaster = scope.ServiceProvider.GetRequiredService<SessionBroadcaster>();

				// A draw offer is answered first; a move would withdraw it anyway.
				if (session.DrawOfferedBy == Game.Opponent(computer))
				{
					var answer = sessions.AnswerDrawForComputer(session.MatchCode, player.AcceptsDraw(game, computer));
					if (answer.Session is { } answered)
						await broadcaster.PublishAsync(answered);
					return;
				}

				if (session.CurrentPlayer != computer)
					return;

				var turn = player.ChooseTurn(game);
				foreach (var step in turn)
				{
					await Task.Delay(StepPause, stoppingToken);

					var result = sessions.MakeComputerMove(session.MatchCode, new GameMove(step.From.Row, step.From.Col, step.To.Row, step.To.Col));
					// Includes a match that timed out or was resigned while the computer was thinking.
					if (result.Session is { } updated)
						await broadcaster.PublishAsync(updated);
					if (!result.Succeeded)
						return;
				}
			}
			catch (OperationCanceledException)
			{
				// Shutting down.
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "The computer couldn't play in match {MatchCode}.", session.MatchCode);
			}
			finally
			{
				_busy.TryRemove(session.MatchCode, out _);
			}
		}
	}
}

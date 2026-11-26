using System.Collections.Concurrent;
using System.Security.Cryptography;
using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;

namespace Checkers.Application.Core.Sessions
{
	/// <summary>
	/// Holds live matches in memory, so register it as a singleton. Every change to a match
	/// happens under that match's lock because both players can send at the same time.
	/// </summary>
	public class GameSessionManager(IGameRules rules, TimeProvider? timeProvider = null) : IGameSessionManager
	{
		public const string TimeRanOutError = "Time ran out for this turn, so the match is over.";

		// No 0/O or 1/I so a code can be read aloud or retyped without mistakes.
		private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		private const int CodeLength = 6;

		private readonly ConcurrentDictionary<string, LiveMatch> _matches = new(StringComparer.OrdinalIgnoreCase);
		private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

		private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

		public SessionResult Create(int userId, string username, MatchSettingsDto? settings = null)
		{
			settings ??= new MatchSettingsDto();
			if (settings.Validate() is { } error)
				return SessionResult.Failure(error);

			var gameRules = settings.Rules ?? GameRuleOptions.Standard;
			var match = new LiveMatch(rules.NewGame(gameRules), gameRules, settings.TurnTimeLimitSeconds);

			var creatorColor = settings.CreatorColor switch
			{
				ColorChoice.White => PlayerColor.White,
				ColorChoice.Random => Random.Shared.Next(2) == 0 ? PlayerColor.Black : PlayerColor.White,
				_ => PlayerColor.Black,
			};
			match.Seat(creatorColor, userId, username);

			string code;
			do
			{
				code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
				match.Code = code;
			}
			while (!_matches.TryAdd(code, match));

			return SessionResult.Success(match.ToDto());
		}

		public SessionResult Join(string matchCode, int userId, string username)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return SessionResult.Failure("Match not found.");

			lock (match)
			{
				if (match.HasPlayer(userId))
					return SessionResult.Success(match.ToDto());

				if (match.OpenSeat is not { } seat)
					return SessionResult.Failure("This match already has two players.");

				match.Seat(seat, userId, username);
				match.Status = SessionStatus.InProgress;
				match.StartedAtUtc = UtcNow;
				StartTurnClock(match);
				return SessionResult.Success(match.ToDto());
			}
		}

		public SessionResult MakeMove(string matchCode, int userId, GameMove move) =>
			ActOnMatch(matchCode, userId, (match, color) =>
			{
				if (color != match.CurrentPlayer)
					return SessionResult.Failure(Game.NotYourTurnError);

				if (match.Game.TryMove(move) is { } error)
					return SessionResult.Failure(error);

				match.Moves.Add(move);
				// Playing on answers a draw offer, from either side.
				match.DrawOfferedBy = null;

				if (match.Game.Outcome != GameOutcome.InProgress)
					match.Finish(match.Game.Outcome, GameEndReason.NoLegalMoves, UtcNow);
				// A multi-jump keeps the same turn, so its clock keeps running.
				else if (match.CurrentPlayer != color)
					StartTurnClock(match);

				return SessionResult.Success(match.ToDto());
			});

		public SessionResult Resign(string matchCode, int userId) =>
			ActOnMatch(matchCode, userId, (match, color) =>
			{
				match.Finish(WinFor(Game.Opponent(color)), GameEndReason.Resignation, UtcNow);
				return SessionResult.Success(match.ToDto());
			});

		public SessionResult OfferDraw(string matchCode, int userId) =>
			ActOnMatch(matchCode, userId, (match, color) =>
			{
				if (match.DrawOfferedBy is not null)
					return SessionResult.Failure(Game.DrawAlreadyOfferedError);

				match.DrawOfferedBy = color;
				return SessionResult.Success(match.ToDto());
			});

		public SessionResult AnswerDraw(string matchCode, int userId, bool accept) =>
			ActOnMatch(matchCode, userId, (match, color) =>
			{
				if (match.DrawOfferedBy is not { } offeredBy)
					return SessionResult.Failure(Game.NoDrawOfferError);

				if (offeredBy == color)
					return SessionResult.Failure(Game.OwnDrawOfferError);

				match.DrawOfferedBy = null;
				if (accept)
					match.Finish(GameOutcome.Draw, GameEndReason.DrawAgreed, UtcNow);

				return SessionResult.Success(match.ToDto());
			});

		public IReadOnlyList<GameSessionDto> ExpireOverdueTurns()
		{
			var expired = new List<GameSessionDto>();
			foreach (var match in _matches.Values)
			{
				lock (match)
				{
					if (IsOverdue(match))
					{
						TimeOut(match);
						expired.Add(match.ToDto());
					}
				}
			}

			return expired;
		}

		public IReadOnlyList<string> GetActiveMatchCodes(int userId) =>
			GetActiveMatches(userId).Select(m => m.MatchCode).ToList();

		public IReadOnlyList<ActiveMatchDto> GetActiveMatches(int userId)
		{
			var active = new List<ActiveMatchDto>();
			foreach (var match in _matches.Values)
			{
				lock (match)
				{
					if (match.Status == SessionStatus.Finished || !match.HasPlayer(userId))
						continue;

					var color = match.ColorOf(userId);
					active.Add(new ActiveMatchDto
					{
						MatchCode = match.Code,
						Status = match.Status,
						OpponentName = color == PlayerColor.Black ? match.WhitePlayerName : match.BlackPlayerName,
						PlayedAs = color.ToString(),
						IsYourTurn = match.Status == SessionStatus.InProgress && match.CurrentPlayer == color,
						MoveCount = match.Moves.Count,
						StartedAtUtc = match.StartedAtUtc,
						TurnDeadlineUtc = match.TurnDeadlineUtc,
					});
				}
			}

			// Null start times (still waiting) sort last.
			return active
				.OrderByDescending(m => m.StartedAtUtc.HasValue)
				.ThenByDescending(m => m.StartedAtUtc)
				.ToList();
		}

		public bool TryClaimRecording(string matchCode)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return false;

			lock (match)
			{
				if (match.Status != SessionStatus.Finished || match.RecordingClaimed)
					return false;

				match.RecordingClaimed = true;
				return true;
			}
		}

		/// <summary>
		/// Runs <paramref name="action"/> under the match's lock once the user is known to be one of
		/// its players and the match is in progress. The action gets the user's color. A turn whose
		/// time has run out ends the match first, before anyone can act on it.
		/// </summary>
		private SessionResult ActOnMatch(string matchCode, int userId, Func<LiveMatch, PlayerColor, SessionResult> action)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return SessionResult.Failure("Match not found.");

			lock (match)
			{
				if (!match.HasPlayer(userId))
					return SessionResult.Failure("You are not a player in this match.");

				// Carries the finished match with the error, so callers still publish the result.
				if (IsOverdue(match))
				{
					TimeOut(match);
					return new SessionResult(match.ToDto(), TimeRanOutError);
				}

				if (match.Status != SessionStatus.InProgress)
					return SessionResult.Failure("This match is not in progress.");

				return action(match, match.ColorOf(userId));
			}
		}

		private bool IsOverdue(LiveMatch match) =>
			match.Status == SessionStatus.InProgress && match.TurnDeadlineUtc is { } deadline && deadline <= UtcNow;

		/// <summary>
		/// The player whose turn ran out resigns automatically.
		/// </summary>
		private void TimeOut(LiveMatch match) =>
			match.Finish(WinFor(Game.Opponent(match.CurrentPlayer)), GameEndReason.Timeout, UtcNow);

		private void StartTurnClock(LiveMatch match) =>
			match.TurnDeadlineUtc = match.TurnTimeLimitSeconds is int seconds ? UtcNow.AddSeconds(seconds) : null;

		private static GameOutcome WinFor(PlayerColor winner) =>
			winner == PlayerColor.Black ? GameOutcome.BlackWon : GameOutcome.WhiteWon;

		private sealed class LiveMatch(IRulesGame game, GameRuleOptions rules, int? turnTimeLimitSeconds)
		{
			public string Code { get; set; } = string.Empty;
			public SessionStatus Status { get; set; } = SessionStatus.WaitingForOpponent;
			public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;
			public GameEndReason? EndReason { get; private set; }
			public int? BlackPlayerId { get; private set; }
			public string? BlackPlayerName { get; private set; }
			public int? WhitePlayerId { get; private set; }
			public string? WhitePlayerName { get; private set; }
			public IRulesGame Game { get; } = game;
			public GameRuleOptions Rules { get; } = rules;
			public int? TurnTimeLimitSeconds { get; } = turnTimeLimitSeconds;
			public DateTime? TurnDeadlineUtc { get; set; }
			public PlayerColor CurrentPlayer => Game.CurrentTurn;
			public List<GameMove> Moves { get; } = [];
			public DateTime? StartedAtUtc { get; set; }
			public DateTime? EndedAtUtc { get; private set; }
			public bool RecordingClaimed { get; set; }
			public PlayerColor? DrawOfferedBy { get; set; }

			/// <summary>
			/// The empty side, or null once both players are seated.
			/// </summary>
			public PlayerColor? OpenSeat =>
				BlackPlayerId is null ? PlayerColor.Black : WhitePlayerId is null ? PlayerColor.White : null;

			public bool HasPlayer(int userId) => userId == BlackPlayerId || userId == WhitePlayerId;
			public PlayerColor ColorOf(int userId) => userId == BlackPlayerId ? PlayerColor.Black : PlayerColor.White;

			public void Seat(PlayerColor color, int userId, string username)
			{
				if (color == PlayerColor.Black)
				{
					BlackPlayerId = userId;
					BlackPlayerName = username;
				}
				else
				{
					WhitePlayerId = userId;
					WhitePlayerName = username;
				}
			}

			public void Finish(GameOutcome outcome, GameEndReason reason, DateTime nowUtc)
			{
				Outcome = outcome;
				EndReason = reason;
				Status = SessionStatus.Finished;
				EndedAtUtc = nowUtc;
				TurnDeadlineUtc = null;
				DrawOfferedBy = null;
			}

			public GameSessionDto ToDto() => new()
			{
				MatchCode = Code,
				Status = Status,
				Outcome = Outcome,
				EndReason = EndReason,
				BlackPlayerId = BlackPlayerId,
				BlackPlayerName = BlackPlayerName,
				WhitePlayerId = WhitePlayerId,
				WhitePlayerName = WhitePlayerName,
				CurrentPlayer = CurrentPlayer,
				BoardSize = Game.Board.Size,
				Squares = Game.Board.ToSquares(),
				Moves = [.. Moves],
				Rules = Rules,
				TurnTimeLimitSeconds = TurnTimeLimitSeconds,
				TurnDeadlineUtc = TurnDeadlineUtc,
				StartedAtUtc = StartedAtUtc,
				EndedAtUtc = EndedAtUtc,
				DrawOfferedBy = DrawOfferedBy
			};
		}
	}
}

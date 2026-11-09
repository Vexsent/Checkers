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
	public class GameSessionManager(IGameRules rules) : IGameSessionManager
	{
		// No 0/O or 1/I so a code can be read aloud or retyped without mistakes.
		private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		private const int CodeLength = 6;

		private readonly ConcurrentDictionary<string, LiveMatch> _matches = new(StringComparer.OrdinalIgnoreCase);

		public SessionResult Create(int userId, string username)
		{
			var match = new LiveMatch(userId, username, rules.NewGame());

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

				if (match.WhitePlayerId is not null)
					return SessionResult.Failure("This match already has two players.");

				match.WhitePlayerId = userId;
				match.WhitePlayerName = username;
				match.Status = SessionStatus.InProgress;
				match.StartedAtUtc = DateTime.UtcNow;
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
					match.Finish(match.Game.Outcome);

				return SessionResult.Success(match.ToDto());
			});

		public SessionResult Resign(string matchCode, int userId) =>
			ActOnMatch(matchCode, userId, (match, color) =>
			{
				match.Finish(color == PlayerColor.Black ? GameOutcome.WhiteWon : GameOutcome.BlackWon);
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
					match.Finish(GameOutcome.Draw);

				return SessionResult.Success(match.ToDto());
			});

		public IReadOnlyList<string> GetActiveMatchCodes(int userId)
		{
			var codes = new List<string>();
			foreach (var match in _matches.Values)
			{
				lock (match)
				{
					if (match.Status != SessionStatus.Finished && match.HasPlayer(userId))
						codes.Add(match.Code);
				}
			}

			return codes;
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
		/// its players and the match is in progress. The action gets the user's color.
		/// </summary>
		private SessionResult ActOnMatch(string matchCode, int userId, Func<LiveMatch, PlayerColor, SessionResult> action)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return SessionResult.Failure("Match not found.");

			lock (match)
			{
				if (!match.HasPlayer(userId))
					return SessionResult.Failure("You are not a player in this match.");

				if (match.Status != SessionStatus.InProgress)
					return SessionResult.Failure("This match is not in progress.");

				return action(match, match.ColorOf(userId));
			}
		}

		private sealed class LiveMatch(int blackPlayerId, string blackPlayerName, IRulesGame game)
		{
			public string Code { get; set; } = string.Empty;
			public SessionStatus Status { get; set; } = SessionStatus.WaitingForOpponent;
			public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;
			public int BlackPlayerId { get; } = blackPlayerId;
			public string BlackPlayerName { get; } = blackPlayerName;
			public int? WhitePlayerId { get; set; }
			public string? WhitePlayerName { get; set; }
			public IRulesGame Game { get; } = game;
			public PlayerColor CurrentPlayer => Game.CurrentTurn;
			public List<GameMove> Moves { get; } = [];
			public DateTime? StartedAtUtc { get; set; }
			public DateTime? EndedAtUtc { get; private set; }
			public bool RecordingClaimed { get; set; }
			public PlayerColor? DrawOfferedBy { get; set; }

			public bool HasPlayer(int userId) => userId == BlackPlayerId || userId == WhitePlayerId;
			public PlayerColor ColorOf(int userId) => userId == BlackPlayerId ? PlayerColor.Black : PlayerColor.White;

			public void Finish(GameOutcome outcome)
			{
				Outcome = outcome;
				Status = SessionStatus.Finished;
				EndedAtUtc = DateTime.UtcNow;
				DrawOfferedBy = null;
			}

			public GameSessionDto ToDto() => new()
			{
				MatchCode = Code,
				Status = Status,
				Outcome = Outcome,
				BlackPlayerId = BlackPlayerId,
				BlackPlayerName = BlackPlayerName,
				WhitePlayerId = WhitePlayerId,
				WhitePlayerName = WhitePlayerName,
				CurrentPlayer = CurrentPlayer,
				BoardSize = Game.Board.Size,
				Squares = Game.Board.ToSquares(),
				Moves = [.. Moves],
				StartedAtUtc = StartedAtUtc,
				EndedAtUtc = EndedAtUtc,
				DrawOfferedBy = DrawOfferedBy
			};
		}
	}
}

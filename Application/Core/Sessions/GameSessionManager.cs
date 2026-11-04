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
			var match = new LiveMatch(userId, username, rules.CreateInitialBoard(), rules.FirstPlayer);

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
				return SessionResult.Success(match.ToDto());
			}
		}

		public SessionResult MakeMove(string matchCode, int userId, GameMove move)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return SessionResult.Failure("Match not found.");

			lock (match)
			{
				if (!match.HasPlayer(userId))
					return SessionResult.Failure("You are not a player in this match.");

				if (match.Status != SessionStatus.InProgress)
					return SessionResult.Failure("This match is not in progress.");

				if (match.ColorOf(userId) != match.CurrentPlayer)
					return SessionResult.Failure("It is not your turn.");

				var result = rules.ApplyMove(match.Board, match.CurrentPlayer, move);
				if (!result.IsLegal)
					return SessionResult.Failure(result.Error ?? "That move is not allowed.");

				match.Board = result.Board;
				match.CurrentPlayer = result.NextPlayer;
				match.Moves.Add(move);

				if (result.Outcome != GameOutcome.InProgress)
					match.Finish(result.Outcome);

				return SessionResult.Success(match.ToDto());
			}
		}

		public SessionResult Resign(string matchCode, int userId)
		{
			if (!_matches.TryGetValue(matchCode, out var match))
				return SessionResult.Failure("Match not found.");

			lock (match)
			{
				if (!match.HasPlayer(userId))
					return SessionResult.Failure("You are not a player in this match.");

				if (match.Status != SessionStatus.InProgress)
					return SessionResult.Failure("This match is not in progress.");

				match.Finish(match.ColorOf(userId) == PlayerColor.Black ? GameOutcome.WhiteWon : GameOutcome.BlackWon);
				return SessionResult.Success(match.ToDto());
			}
		}

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

		private sealed class LiveMatch(int blackPlayerId, string blackPlayerName, Board board, PlayerColor firstPlayer)
		{
			public string Code { get; set; } = string.Empty;
			public SessionStatus Status { get; set; } = SessionStatus.WaitingForOpponent;
			public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;
			public int BlackPlayerId { get; } = blackPlayerId;
			public string BlackPlayerName { get; } = blackPlayerName;
			public int? WhitePlayerId { get; set; }
			public string? WhitePlayerName { get; set; }
			public Board Board { get; set; } = board;
			public PlayerColor CurrentPlayer { get; set; } = firstPlayer;
			public List<GameMove> Moves { get; } = [];

			public bool HasPlayer(int userId) => userId == BlackPlayerId || userId == WhitePlayerId;
			public PlayerColor ColorOf(int userId) => userId == BlackPlayerId ? PlayerColor.Black : PlayerColor.White;

			public void Finish(GameOutcome outcome)
			{
				Outcome = outcome;
				Status = SessionStatus.Finished;
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
				BoardSize = Board.Size,
				Squares = Board.ToSquares(),
				MoveCount = Moves.Count
			};
		}
	}
}

using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.IRepositories;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Checkers.Application.Core.MatchHistory
{
	internal class MatchHistoryService(
		IMatchRecordRepository matchRecordRepository,
		IUserRepository userRepository) : IMatchHistoryService
	{
		private static readonly JsonSerializerOptions RulesJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

		// Stored beside the rules in the same JSON object. Reading the rules ignores it.
		private const string ComputerDifficultyProperty = "ComputerDifficulty";

		public async Task<MatchHistoryDto> GetHistoryAsync(int userId)
		{
			var matches = await matchRecordRepository.GetByUserIdAsync(userId);
			var summaries = matches.Select(m => ToSummary(m, userId)).ToList();

			return new MatchHistoryDto
			{
				Wins = summaries.Count(s => s.Outcome == MatchOutcome.Win),
				Losses = summaries.Count(s => s.Outcome == MatchOutcome.Loss),
				Draws = summaries.Count(s => s.Outcome == MatchOutcome.Draw),
				Matches = summaries,
			};
		}

		public async Task<MatchReplayDto?> GetMatchAsync(int userId, int matchId)
		{
			var match = await matchRecordRepository.GetByIdWithMovesAsync(matchId);

			// Someone else's match looks the same as a missing one, so ids can't be probed.
			if (match is null || (match.BlackPlayerId != userId && match.WhitePlayerId != userId))
				return null;

			return ToReplay(match, userId);
		}

		public async Task<RecordMatchResult> RecordMatchAsync(int callerUserId, RecordMatchRequestDto request)
		{
			var errors = Validate(callerUserId, request);

			// Only look up the opponent once the request is otherwise sound.
			if (errors.Count == 0)
			{
				var opponentId = request.BlackPlayerId == callerUserId ? request.WhitePlayerId : request.BlackPlayerId;
				if (opponentId is int id && await userRepository.GetByIdAsync(id) is null)
					errors.Add("Opponent not found.");
			}

			if (errors.Count > 0)
				return RecordMatchResult.Failed(errors);

			var match = new MatchRecord
			{
				BlackPlayerId = request.BlackPlayerId,
				WhitePlayerId = request.WhitePlayerId,
				Result = request.Result,
				StartedAtUtc = request.StartedAtUtc,
				EndedAtUtc = request.EndedAtUtc,
				RulesJson = SettingsJson(request.Rules ?? GameRuleOptions.Standard, request.ComputerDifficulty),
				TurnTimeLimitSeconds = request.TurnTimeLimitSeconds,
				Moves = request.Moves.Select((mv, i) => new MatchRecordMove
				{
					MoveNumber = i + 1,
					FromRow = mv.FromRow,
					FromCol = mv.FromCol,
					ToRow = mv.ToRow,
					ToCol = mv.ToCol,
				}).ToList(),
			};

			await matchRecordRepository.AddAsync(match);
			await matchRecordRepository.SaveChangesAsync();

			return RecordMatchResult.Succeeded(match.Id);
		}

		private static List<string> Validate(int callerUserId, RecordMatchRequestDto request)
		{
			var errors = new List<string>();

			if (request.BlackPlayerId is null && request.WhitePlayerId is null)
				errors.Add("A match needs at least one human player.");
			else if (request.BlackPlayerId == request.WhitePlayerId)
				errors.Add("A player can't play against themselves.");
			else if (request.BlackPlayerId != callerUserId && request.WhitePlayerId != callerUserId)
				errors.Add("You can only record matches you played in.");

			if (!Enum.IsDefined(request.Result))
				errors.Add("Unknown match result.");

			if (request.EndedAtUtc < request.StartedAtUtc)
				errors.Add("A match can't end before it starts.");

			if (request.ComputerDifficulty is { } level)
			{
				if (!Enum.IsDefined(level))
					errors.Add("Unknown computer difficulty.");
				else if (request.BlackPlayerId is not null && request.WhitePlayerId is not null)
					errors.Add("A computer difficulty only applies to a match against the computer.");
			}

			var rules = request.Rules ?? GameRuleOptions.Standard;
			if (rules.Validate() is { } rulesError)
				errors.Add(rulesError);

			for (int i = 0; i < request.Moves.Count; i++)
			{
				var mv = request.Moves[i];
				if (!OnBoard(mv.FromRow, mv.FromCol, rules.BoardSize) || !OnBoard(mv.ToRow, mv.ToCol, rules.BoardSize))
					errors.Add($"Move {i + 1} is off the board.");
			}

			return errors;
		}

		private static bool OnBoard(int row, int col, int boardSize) =>
			row >= 0 && row < boardSize && col >= 0 && col < boardSize;

		/// <summary>
		/// The JSON saved in Rules_Json: the rules, plus the computer's level for a computer match.
		/// </summary>
		private static string SettingsJson(GameRuleOptions rules, ComputerDifficulty? computer)
		{
			var json = JsonSerializer.SerializeToNode(rules, RulesJsonOptions)!.AsObject();
			if (computer is { } level)
				json[ComputerDifficultyProperty] = level.ToString();
			return json.ToJsonString();
		}

		/// <summary>
		/// The computer's level saved with a match, or null (two players, or saved before levels were kept).
		/// </summary>
		private static ComputerDifficulty? ComputerDifficultyOf(MatchRecord match)
		{
			if (string.IsNullOrWhiteSpace(match.RulesJson))
				return null;

			try
			{
				return JsonNode.Parse(match.RulesJson)?[ComputerDifficultyProperty]?.GetValue<string>() is { } text
					&& Enum.TryParse<ComputerDifficulty>(text, out var level)
					? level
					: null;
			}
			catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
			{
				return null;
			}
		}

		/// <summary>
		/// The rules a saved match was played with. No JSON means standard American checkers.
		/// </summary>
		private static GameRuleOptions RulesOf(MatchRecord match)
		{
			if (!string.IsNullOrWhiteSpace(match.RulesJson))
			{
				try
				{
					if (JsonSerializer.Deserialize<GameRuleOptions>(match.RulesJson, RulesJsonOptions) is { } rules)
						return rules;
				}
				catch (JsonException)
				{
					// Unreadable rules: fall back to standard, and the replay page warns if moves don't fit.
				}
			}

			return GameRuleOptions.Standard;
		}

		private static MatchSummaryDto ToSummary(MatchRecord match, int userId) =>
			FillSummary(new MatchSummaryDto(), match, userId);

		private static MatchReplayDto ToReplay(MatchRecord match, int userId)
		{
			var replay = FillSummary(new MatchReplayDto(), match, userId);
			replay.BlackPlayerName = match.BlackPlayer?.Username ?? ComputerName(replay.ComputerDifficulty);
			replay.WhitePlayerName = match.WhitePlayer?.Username ?? ComputerName(replay.ComputerDifficulty);
			replay.Rules = RulesOf(match);
			replay.TurnTimeLimitSeconds = match.TurnTimeLimitSeconds;
			replay.Moves = match.Moves
				.OrderBy(mv => mv.MoveNumber)
				.Select(mv => new MatchMoveDto
				{
					FromRow = mv.FromRow,
					FromCol = mv.FromCol,
					ToRow = mv.ToRow,
					ToCol = mv.ToCol,
				})
				.ToList();
			return replay;
		}

		// "Computer (Hard)", or plain "Computer" when the level wasn't saved.
		private static string ComputerName(ComputerDifficulty? level) =>
			level is { } known ? ComputerPlayer.NameOf(known) : MatchSummaryDto.ComputerOpponentName;

		private static T FillSummary<T>(T summary, MatchRecord match, int userId) where T : MatchSummaryDto
		{
			var playedBlack = match.BlackPlayerId == userId;
			var opponent = playedBlack ? match.WhitePlayer : match.BlackPlayer;

			summary.Id = match.Id;
			summary.VsComputer = opponent is null;
			summary.ComputerDifficulty = summary.VsComputer ? ComputerDifficultyOf(match) : null;
			summary.OpponentName = opponent?.Username ?? ComputerName(summary.ComputerDifficulty);
			summary.PlayedAs = playedBlack ? "Black" : "White";
			summary.Outcome = match.Result switch
			{
				MatchResult.Draw => MatchOutcome.Draw,
				MatchResult.BlackWin => playedBlack ? MatchOutcome.Win : MatchOutcome.Loss,
				_ => playedBlack ? MatchOutcome.Loss : MatchOutcome.Win,
			};
			summary.StartedAtUtc = match.StartedAtUtc;
			summary.EndedAtUtc = match.EndedAtUtc;
			return summary;
		}
	}
}

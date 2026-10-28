using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.IRepositories;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;

namespace Checkers.Application.Core.MatchHistory
{
	internal class MatchHistoryService(
		IMatchRecordRepository matchRecordRepository,
		IUserRepository userRepository) : IMatchHistoryService
	{
		private const int BoardSize = 8;

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

			for (int i = 0; i < request.Moves.Count; i++)
			{
				var mv = request.Moves[i];
				if (!OnBoard(mv.FromRow, mv.FromCol) || !OnBoard(mv.ToRow, mv.ToCol))
					errors.Add($"Move {i + 1} is off the board.");
			}

			return errors;
		}

		private static bool OnBoard(int row, int col) =>
			row is >= 0 and < BoardSize && col is >= 0 and < BoardSize;

		private static MatchSummaryDto ToSummary(MatchRecord match, int userId)
		{
			var playedBlack = match.BlackPlayerId == userId;
			var opponent = playedBlack ? match.WhitePlayer : match.BlackPlayer;

			return new MatchSummaryDto
			{
				Id = match.Id,
				OpponentName = opponent?.Username ?? MatchSummaryDto.ComputerOpponentName,
				VsComputer = opponent is null,
				PlayedAs = playedBlack ? "Black" : "White",
				Outcome = match.Result switch
				{
					MatchResult.Draw => MatchOutcome.Draw,
					MatchResult.BlackWin => playedBlack ? MatchOutcome.Win : MatchOutcome.Loss,
					_ => playedBlack ? MatchOutcome.Loss : MatchOutcome.Win,
				},
				StartedAtUtc = match.StartedAtUtc,
				EndedAtUtc = match.EndedAtUtc,
			};
		}
	}
}

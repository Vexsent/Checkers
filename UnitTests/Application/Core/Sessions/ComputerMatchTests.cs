using Checkers.Application.Core.MatchHistory;
using Checkers.Application.Core.Repositories;
using Checkers.Application.Core.Sessions;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.DataContext;
using Checkers.Shared.DTOs;
using Checkers.Shared.Enums;
using Checkers.Shared.GameModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UnitTests.TestSupport;

namespace UnitTests.Application.Core.Sessions
{
	/// <summary>
	/// Live matches against the computer: the computer takes the other seat, plays through the
	/// session manager like a player, and the finished game is saved with no player id on its side.
	/// </summary>
	public class ComputerMatchTests : IDisposable
	{
		private static readonly GameMove BlackOpening = new(2, 1, 3, 0);

		private readonly SqliteConnection _connection;
		private readonly CheckersContext _context;
		private readonly MatchHistoryService _matchHistory;
		private readonly User _player;
		private readonly GameSessionManager _manager = new(new CheckersGameRules());

		public ComputerMatchTests()
		{
			_connection = new SqliteConnection("DataSource=:memory:");
			_connection.Open();
			_context = new SqliteCheckersContext(new DbContextOptionsBuilder<CheckersContext>().UseSqlite(_connection).Options);
			_context.Database.EnsureCreated();

			_player = new User { Username = "player", PasswordHash = "hash", Email = "player@example.com", FirstName = "P", LastName = "Test" };
			_context.Users.Add(_player);
			_context.SaveChanges();

			_matchHistory = new MatchHistoryService(new MatchRecordRepository(_context), new UserRepository(_context));
		}

		public void Dispose()
		{
			_context.Dispose();
			_connection.Dispose();
		}

		private GameSessionDto CreateVsComputer(ColorChoice color = ColorChoice.Black, ComputerDifficulty level = ComputerDifficulty.Medium, GameRuleOptions? rules = null)
		{
			var result = _manager.Create(_player.Id, _player.Username, new MatchSettingsDto
			{
				CreatorColor = color,
				Computer = level,
				Rules = rules ?? GameRuleOptions.Standard,
			});
			Assert.True(result.Succeeded, result.Error);
			return result.Session!;
		}

		[Fact]
		public void Create_ComputerTakesTheOtherSideAndPlayStartsAtOnce()
		{
			var session = CreateVsComputer(ColorChoice.Black, ComputerDifficulty.Hard);

			Assert.Equal(SessionStatus.InProgress, session.Status);
			Assert.Equal(_player.Id, session.BlackPlayerId);
			Assert.Null(session.WhitePlayerId);
			Assert.Equal(PlayerColor.White, session.ComputerColor);
			Assert.Equal(ComputerDifficulty.Hard, session.ComputerDifficulty);
			Assert.Equal("Computer (Hard)", session.WhitePlayerName);
			Assert.NotNull(session.StartedAtUtc);
		}

		[Fact]
		public void Join_AComputerMatchHasNoOpenSeat()
		{
			var session = CreateVsComputer();

			Assert.Equal("This match already has two players.", _manager.Join(session.MatchCode, 999, "someone").Error);
		}

		[Fact]
		public void ComputerToMoveFirst_IsWaitingForTheComputer()
		{
			// American: Black moves first, and the player took White.
			var session = CreateVsComputer(ColorChoice.White);

			Assert.Contains(_manager.GetMatchesAwaitingComputer(), s => s.MatchCode == session.MatchCode);
			Assert.Equal(Game.NotYourTurnError, _manager.MakeMove(session.MatchCode, _player.Id, BlackOpening).Error);
			Assert.True(_manager.MakeComputerMove(session.MatchCode, BlackOpening).Succeeded);
			Assert.Empty(_manager.GetMatchesAwaitingComputer());
		}

		[Fact]
		public void ComputerCannotMoveOnThePlayersTurn()
		{
			var session = CreateVsComputer(ColorChoice.Black);

			Assert.Empty(_manager.GetMatchesAwaitingComputer());
			Assert.Equal(Game.NotYourTurnError, _manager.MakeComputerMove(session.MatchCode, BlackOpening).Error);
		}

		[Fact]
		public void ComputerMoves_InATwoPlayerMatch_AreRefused()
		{
			var code = _manager.Create(_player.Id, _player.Username).Session!.MatchCode;
			_manager.Join(code, 999, "friend");

			Assert.False(_manager.MakeComputerMove(code, BlackOpening).Succeeded);
		}

		[Fact]
		public void DrawOffer_WaitsForTheComputersAnswer()
		{
			var session = CreateVsComputer(ColorChoice.Black);
			_manager.OfferDraw(session.MatchCode, _player.Id);

			Assert.Contains(_manager.GetMatchesAwaitingComputer(), s => s.MatchCode == session.MatchCode);

			var declined = _manager.AnswerDrawForComputer(session.MatchCode, accept: false).Session!;
			Assert.Null(declined.DrawOfferedBy);
			Assert.Equal(SessionStatus.InProgress, declined.Status);
			Assert.Empty(_manager.GetMatchesAwaitingComputer());
		}

		[Fact]
		public void ActiveMatches_ShowTheComputerAsTheOpponent()
		{
			CreateVsComputer(ColorChoice.Black, ComputerDifficulty.Easy);

			var active = Assert.Single(_manager.GetActiveMatches(_player.Id));
			Assert.True(active.VsComputer);
			Assert.Equal("Computer (Easy)", active.OpponentName);
		}

		// A whole game, with the computer choosing every one of its turns, saved and replayed.
		[Theory]
		[InlineData(GameVariant.American, ColorChoice.Black)]
		[InlineData(GameVariant.Russian, ColorChoice.Black)]
		[InlineData(GameVariant.International, ColorChoice.White)]
		public async Task FullGameAgainstTheComputer_IsSavedAsVsComputerAndReplays(GameVariant variant, ColorChoice color)
		{
			var rules = GameVariants.Find(variant)!.Rules;
			var session = CreateVsComputer(color, ComputerDifficulty.Easy, rules);
			var code = session.MatchCode;
			var computer = new ComputerPlayer(ComputerDifficulty.Easy, new Random(5));
			var random = new Random(6);

			for (var turn = 0; turn < 400 && session.Status == SessionStatus.InProgress; turn++)
			{
				var (game, _) = Game.Replay(session.Rules, session.Moves);
				if (session.CurrentPlayer == session.ComputerColor)
				{
					foreach (var step in computer.ChooseTurn(game))
					{
						var result = _manager.MakeComputerMove(code, new GameMove(step.From.Row, step.From.Col, step.To.Row, step.To.Col));
						Assert.True(result.Succeeded, result.Error);
						session = result.Session!;
					}
				}
				else
				{
					var legal = game.GetLegalMoves();
					var move = legal[random.Next(legal.Count)];
					var result = _manager.MakeMove(code, _player.Id, new GameMove(move.From.Row, move.From.Col, move.To.Row, move.To.Col));
					Assert.True(result.Succeeded, result.Error);
					session = result.Session!;
				}
			}

			if (session.Status == SessionStatus.InProgress)
				session = _manager.Resign(code, _player.Id).Session!;

			var recorder = new FinishedMatchRecorder(_manager, _matchHistory);
			var saved = await recorder.RecordIfFinishedAsync(session);
			Assert.True(saved!.Success, string.Join(" ", saved.Errors));

			var history = await _matchHistory.GetHistoryAsync(_player.Id);
			var summary = Assert.Single(history.Matches);
			Assert.True(summary.VsComputer);
			Assert.Equal(ComputerDifficulty.Easy, summary.ComputerDifficulty);
			Assert.Equal("Computer (Easy)", summary.OpponentName);

			// The level is stored beside the rules in the same JSON column.
			var record = await _context.MatchRecords.SingleAsync();
			Assert.Contains("\"ComputerDifficulty\":\"Easy\"", record.RulesJson);

			var match = await _matchHistory.GetMatchAsync(_player.Id, saved.MatchId!.Value);
			Assert.Equal(rules, match!.Rules);
			Assert.Equal("Computer (Easy)", color == ColorChoice.Black ? match.WhitePlayerName : match.BlackPlayerName);
			Assert.Equal("player", color == ColorChoice.Black ? match.BlackPlayerName : match.WhitePlayerName);
			var (replayed, played) = Game.Replay(match.Rules, match.Moves.Select(m => new GameMove(m.FromRow, m.FromCol, m.ToRow, m.ToCol)));
			Assert.Equal(match.Moves.Count, played);
			Assert.Equal(session.Squares, replayed.Board.ToSquares());
		}
	}
}

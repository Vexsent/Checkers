using Checkers.Client.Services;
using Checkers.Shared.Enums;

namespace UnitTests.Client.Services
{
	// Story 14 (Move List). Squares are (row, col); row 0 is black's back row.
	public class RelayServiceTests
	{
		// ---------- AC 14.1: a move is recorded with color, piece, and squares ----------

		[Fact]
		public void FormatMove_WhitePawnSimpleMove_ReturnsLettersAndSquares()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.WhitePawn, Path((5, 0), (4, 1)));

			Assert.Equal("WP a3-b4", text);
		}

		[Theory]
		[InlineData(GamePieces.WhitePawn, "WP")]
		[InlineData(GamePieces.WhiteKing, "WK")]
		[InlineData(GamePieces.BlackPawn, "BP")]
		[InlineData(GamePieces.BlackKing, "BK")]
		public void FormatMove_UsesColorAndPieceLetters(GamePieces piece, string expectedLetters)
		{
			var relay = new RelayService();

			string text = relay.FormatMove(piece, Path((3, 2), (4, 3)));

			Assert.StartsWith(expectedLetters + " ", text);
		}

		[Fact]
		public void RecordMove_FillsInEveryFieldOfTheEntry()
		{
			var relay = new RelayService();
			DateTime before = DateTime.UtcNow;

			MoveEntry entry = relay.RecordMove(GamePieces.BlackPawn, Path((2, 1), (3, 0)));

			DateTime after = DateTime.UtcNow;
			Assert.Equal(1, entry.Number);
			Assert.Equal(GamePieces.BlackPawn, entry.Piece);
			Assert.Equal(Path((2, 1), (3, 0)), entry.Path);
			Assert.False(entry.Crowned);
			Assert.InRange(entry.PlayedAtUtc, before, after);
			Assert.Equal("BP b6-a5", entry.Text);
		}

		[Fact]
		public void RecordMove_AddsTheEntryToTheList()
		{
			var relay = new RelayService();

			MoveEntry entry = relay.RecordMove(GamePieces.BlackPawn, Path((2, 1), (3, 0)));

			Assert.Equal([entry], relay.Moves);
		}

		[Fact]
		public void AddMove_PutsTheOpponentsEntryInTheList()
		{
			var relay = new RelayService();
			MoveEntry fromOpponent = Entry(1, "BP b6-a5");

			relay.AddMove(fromOpponent);

			Assert.Equal([fromOpponent], relay.Moves);
		}

		// ---------- AC 14.3: a single jump uses "x" ----------

		[Fact]
		public void FormatMove_SingleJump_UsesXWithNoCaptureCount()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.BlackPawn, Path((3, 2), (5, 4)));

			Assert.Equal("BP c5xe3", text);
		}

		// ---------- AC 14.4: a multi-jump is one entry with every square and a capture count ----------

		[Fact]
		public void FormatMove_DoubleJump_ListsEverySquareAndCountsTwo()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.WhiteKing, Path((7, 2), (5, 4), (3, 6)));

			Assert.Equal("WK c1xe3xg5 (x2)", text);
		}

		[Fact]
		public void FormatMove_TripleJump_CountsThree()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.WhiteKing, Path((7, 0), (5, 2), (3, 4), (1, 6)));

			Assert.Equal("WK a1xc3xe5xg7 (x3)", text);
		}

		[Fact]
		public void RecordMove_MultiJump_IsOneEntryWithTheWholePath()
		{
			var relay = new RelayService();

			relay.RecordMove(GamePieces.WhiteKing, Path((7, 2), (5, 4), (3, 6)));

			MoveEntry only = Assert.Single(relay.Moves);
			Assert.Equal(3, only.Path.Count);
			Assert.Equal("WK c1xe3xg5 (x2)", only.Text);
		}

		// ---------- AC 14.5: moves stay in order, numbered from 1 ----------

		[Fact]
		public void RecordMove_NumbersMovesOneTwoThreeInPlayedOrder()
		{
			var relay = new RelayService();

			relay.RecordMove(GamePieces.BlackPawn, Path((2, 1), (3, 2)));
			relay.RecordMove(GamePieces.WhitePawn, Path((5, 4), (4, 3)));
			relay.RecordMove(GamePieces.BlackPawn, Path((3, 2), (5, 4)));

			Assert.Equal([1, 2, 3], relay.Moves.Select(m => m.Number));
			Assert.Equal(["BP b6-c5", "WP e3-d4", "BP c5xe3"], relay.Moves.Select(m => m.Text));
		}

		[Fact]
		public void RecordMove_CopiesThePath_SoLaterChangesByTheCallerDontLeakIn()
		{
			var relay = new RelayService();
			var path = Path((2, 1), (3, 2));

			MoveEntry entry = relay.RecordMove(GamePieces.BlackPawn, path);
			path.Add(new Square(4, 3));

			Assert.Equal(2, entry.Path.Count);
		}

		// ---------- AC 14.6 (relay side): a bad move adds nothing ----------

		[Fact]
		public void RecordMove_WithABadPath_ThrowsAndAddsNothing()
		{
			var relay = new RelayService();

			Assert.Throws<ArgumentException>(() => relay.RecordMove(GamePieces.WhitePawn, Path((5, 0))));
			Assert.Empty(relay.Moves);
		}

		// ---------- AC 14.7: reconnecting loads the full list ----------

		[Fact]
		public void LoadMoves_ReplacesWhateverWasThere()
		{
			var relay = new RelayService();
			relay.AddMove(Entry(1, "BP b6-c5"));
			MoveEntry[] fullList = [Entry(1, "BP b6-c5"), Entry(2, "WP e3-d4"), Entry(3, "BP c5xe3")];

			relay.LoadMoves(fullList);

			Assert.Equal(fullList, relay.Moves);
		}

		[Fact]
		public void LoadMoves_CopiesTheEntries_SoLaterChangesToTheSourceDontLeakIn()
		{
			var relay = new RelayService();
			var fromServer = new List<MoveEntry> { Entry(1, "BP b6-c5"), Entry(2, "WP e3-d4") };

			relay.LoadMoves(fromServer);
			fromServer.Clear();

			Assert.Equal(2, relay.Moves.Count);
		}

		[Fact]
		public void LoadMoves_FromItsOwnList_KeepsEveryEntry()
		{
			var relay = new RelayService();
			relay.AddMove(Entry(1, "BP b6-c5"));
			relay.AddMove(Entry(2, "WP e3-d4"));

			relay.LoadMoves(relay.Moves);

			Assert.Equal(["BP b6-c5", "WP e3-d4"], relay.Moves.Select(m => m.Text));
		}

		[Fact]
		public void RecordMove_AfterLoadMoves_ContinuesTheNumbering()
		{
			var relay = new RelayService();
			relay.LoadMoves([Entry(1, "BP b6-c5"), Entry(2, "WP e3-d4")]);

			MoveEntry next = relay.RecordMove(GamePieces.BlackPawn, Path((3, 2), (5, 4)));

			Assert.Equal(3, next.Number);
		}

		// ---------- AC 14.9: crowning adds "=K" ----------

		[Fact]
		public void FormatMove_CrownedBySimpleMove_EndsWithEqualsK()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.WhitePawn, Path((1, 1), (0, 0)), crowned: true);

			Assert.Equal("WP b7-a8=K", text);
		}

		[Fact]
		public void FormatMove_CrownedAfterMultiJump_PutsCountBeforeEqualsK()
		{
			var relay = new RelayService();

			string text = relay.FormatMove(GamePieces.BlackPawn, Path((3, 4), (5, 2), (7, 0)), crowned: true);

			Assert.Equal("BP e5xc3xa1 (x2)=K", text);
		}

		[Fact]
		public void RecordMove_Crowned_KeepsThePieceAsAPawnAndSetsCrowned()
		{
			var relay = new RelayService();

			MoveEntry entry = relay.RecordMove(GamePieces.WhitePawn, Path((1, 1), (0, 0)), crowned: true);

			Assert.Equal(GamePieces.WhitePawn, entry.Piece);
			Assert.True(entry.Crowned);
			Assert.Equal("WP b7-a8=K", entry.Text);
		}

		// ---------- MovesChanged event (lets the panel refresh, AC 14.2) ----------

		[Fact]
		public void RecordMove_RingsMovesChangedOnce()
		{
			var relay = new RelayService();
			int rings = 0;
			relay.MovesChanged += () => rings++;

			relay.RecordMove(GamePieces.BlackPawn, Path((2, 1), (3, 0)));

			Assert.Equal(1, rings);
		}

		[Fact]
		public void AddMove_RingsMovesChangedOnce()
		{
			var relay = new RelayService();
			int rings = 0;
			relay.MovesChanged += () => rings++;

			relay.AddMove(Entry(1, "BP b6-c5"));

			Assert.Equal(1, rings);
		}

		[Fact]
		public void LoadMoves_RingsMovesChangedOnce_NotOncePerMove()
		{
			var relay = new RelayService();
			int rings = 0;
			relay.MovesChanged += () => rings++;

			relay.LoadMoves([Entry(1, "BP b6-c5"), Entry(2, "WP e3-d4"), Entry(3, "BP c5xe3")]);

			Assert.Equal(1, rings);
		}

		// ---------- New game: ClearMoves empties the list ----------

		[Fact]
		public void ClearMoves_EmptiesTheList()
		{
			var relay = new RelayService();
			relay.AddMove(Entry(1, "BP b6-c5"));
			relay.AddMove(Entry(2, "WP e3-d4"));

			relay.ClearMoves();

			Assert.Empty(relay.Moves);
		}

		[Fact]
		public void ClearMoves_RingsMovesChangedOnce()
		{
			var relay = new RelayService();
			relay.AddMove(Entry(1, "BP b6-c5"));
			int rings = 0;
			relay.MovesChanged += () => rings++;

			relay.ClearMoves();

			Assert.Equal(1, rings);
		}

		[Fact]
		public void ClearMoves_OnAnEmptyList_DoesNotRing()
		{
			var relay = new RelayService();
			int rings = 0;
			relay.MovesChanged += () => rings++;

			relay.ClearMoves();

			Assert.Equal(0, rings);
		}

		[Fact]
		public void ClearMoves_ThenRecordMove_StartsNumberingAtOneAgain()
		{
			var relay = new RelayService();
			relay.RecordMove(GamePieces.BlackPawn, Path((2, 1), (3, 2)));

			relay.ClearMoves();
			MoveEntry first = relay.RecordMove(GamePieces.BlackPawn, Path((2, 3), (3, 4)));

			Assert.Equal(1, first.Number);
			Assert.Equal([first], relay.Moves);
		}

		// ---------- Bad input is rejected ----------

		[Fact]
		public void FormatMove_PathWithOneSquare_Throws()
		{
			var relay = new RelayService();

			Assert.Throws<ArgumentException>(() => relay.FormatMove(GamePieces.WhitePawn, Path((5, 0))));
		}

		[Fact]
		public void FormatMove_EmptyPiece_Throws()
		{
			var relay = new RelayService();

			Assert.Throws<ArgumentException>(() => relay.FormatMove(GamePieces.Empty, Path((5, 0), (4, 1))));
		}

		// ---------- Helpers ----------

		private static List<Square> Path(params (int Row, int Col)[] squares) =>
			squares.Select(s => new Square(s.Row, s.Col)).ToList();

		// Stand-in for an entry received from the opponent; only Number and Text matter here.
		private static MoveEntry Entry(int number, string text) =>
			new(number, GamePieces.BlackPawn, Path((2, 1), (3, 0)), false, DateTime.UnixEpoch, text);
	}
}

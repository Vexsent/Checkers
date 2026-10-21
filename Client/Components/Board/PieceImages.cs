using Checkers.Shared.Enums;

namespace Checkers.Client.Components.Board
{
	/// <summary>
	/// Maps a piece to its image under wwwroot. Kept in Client so Shared stays free of UI details.
	/// </summary>
	public static class PieceImages
	{
		private const string Root = "images/pieces";

		public static string? For(GamePieces piece) => piece switch
		{
			GamePieces.WhitePawn => $"{Root}/checker_white_pawn.png",
			GamePieces.BlackPawn => $"{Root}/checker_black_pawn.png",
			GamePieces.WhiteKing => $"{Root}/checker_white_king.png",
			GamePieces.BlackKing => $"{Root}/checker_black_king.png",
			_ => null,
		};
	}
}

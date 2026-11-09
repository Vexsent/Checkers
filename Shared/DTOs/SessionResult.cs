namespace Checkers.Shared.DTOs
{
	public sealed record SessionResult(GameSessionDto? Session, string? Error)
	{
		public bool Succeeded => Error is null;

		public static SessionResult Success(GameSessionDto session) => new(session, null);
		public static SessionResult Failure(string error) => new(null, error);
	}
}

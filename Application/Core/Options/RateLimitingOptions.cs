namespace Checkers.Application.Core.Options
{
	/// <summary>
	/// Request limits for the public server, bound from "AppOptions:RateLimiting". Each limit is a
	/// number of requests per fixed window. Signed-in users are counted per account; everyone else
	/// per IP address. Override any value with an environment variable, for example
	/// AppOptions__RateLimiting__Auth__PermitLimit=5.
	/// </summary>
	public class RateLimitingOptions
	{
		/// <summary>Login and register, per IP address. Slows down password guessing.</summary>
		public RateLimit Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

		/// <summary>Every other HTTP request, including the live-match connection handshake.</summary>
		public RateLimit Api { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

		/// <summary>Creating and joining live matches. Stops match spam and match-code guessing.</summary>
		public RateLimit Lobby { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

		/// <summary>Moves, resigning and draw offers in a live match.</summary>
		public RateLimit Game { get; set; } = new() { PermitLimit = 30, WindowSeconds = 10 };
	}

	public class RateLimit
	{
		public int PermitLimit { get; set; }
		public int WindowSeconds { get; set; }
	}
}

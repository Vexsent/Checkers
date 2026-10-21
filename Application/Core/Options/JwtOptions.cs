namespace Checkers.Application.Core.Options
{
	/// <summary>
	/// Bound as the nested "Jwt" object under "AppOptions". SigningKey must come from
	/// user-secrets/environment variables, never committed in appsettings.json.
	/// </summary>
	public class JwtOptions
	{
		public const string SectionName = "Jwt";

		public string Issuer { get; set; } = default!;
		public string Audience { get; set; } = default!;
		public string SigningKey { get; set; } = default!;
		public int AccessTokenExpirationMinutes { get; set; } = 60;
	}
}

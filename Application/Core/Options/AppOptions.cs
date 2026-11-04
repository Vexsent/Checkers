namespace Checkers.Application.Core.Options
{
	/// <summary>
	/// Root options object bound from the "AppOptions" config section — server-side
	/// config (JWT signing, etc.), consumed via IOptions&lt;AppOptions&gt; through DI.
	/// </summary>
	public class AppOptions
	{
		public const string SectionName = "AppOptions";

		public string BaseUrl { get; set; } = string.Empty;
		public JwtOptions Jwt { get; set; } = new();
	}
}

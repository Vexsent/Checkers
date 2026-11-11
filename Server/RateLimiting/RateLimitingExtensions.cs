using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Checkers.Application.Core.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace Checkers.Server.RateLimiting
{
	/// <summary>
	/// HTTP rate limiting. Every request counts against the "Api" limit; login and register also
	/// count against the stricter "Auth" limit. Rejected requests get 429 Too Many Requests with a
	/// Retry-After header. Behind the reverse proxy the client IP comes from X-Forwarded-For, which
	/// ASPNETCORE_FORWARDEDHEADERS_ENABLED (set in the Dockerfile) turns on.
	/// </summary>
	public static class RateLimitingExtensions
	{
		public const string AuthPolicy = "auth";
		public const string TooManyRequestsMessage = "Too many requests. Please wait a moment and try again.";

		public static IServiceCollection AddCheckersRateLimiting(this IServiceCollection services, RateLimitingOptions options)
		{
			// One shared filter instance, so its counters last for the life of the server.
			services.AddSingleton(new HubRateLimitFilter(options));

			services.AddRateLimiter(limiter =>
			{
				limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
				limiter.OnRejected = WriteRejectionAsync;
				limiter.GlobalLimiter = CreateApiLimiter(options.Api);
				limiter.AddPolicy(AuthPolicy, http => AuthPartition(http, options.Auth));
			});
			return services;
		}

		public static PartitionedRateLimiter<HttpContext> CreateApiLimiter(RateLimit limit) =>
			PartitionedRateLimiter.Create<HttpContext, string>(http => FixedWindow(PartitionKey(http), limit));

		/// <summary>
		/// Login and register are counted by IP only: nobody is signed in yet, and a signed-in
		/// attacker must not get a fresh allowance per stolen account.
		/// </summary>
		public static RateLimitPartition<string> AuthPartition(HttpContext http, RateLimit limit) =>
			FixedWindow("ip:" + ClientIp(http), limit);

		/// <summary>
		/// Signed-in users get their own allowance wherever they connect from, so players behind the
		/// same home router don't share one. Everyone else is counted by IP address.
		/// </summary>
		public static string PartitionKey(HttpContext http) =>
			http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
				? "user:" + userId
				: "ip:" + ClientIp(http);

		public static RateLimitPartition<string> FixedWindow(string key, RateLimit limit) =>
			RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = limit.PermitLimit,
				Window = TimeSpan.FromSeconds(limit.WindowSeconds),
				QueueLimit = 0,
			});

		private static string ClientIp(HttpContext http) =>
			http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

		private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
		{
			if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
			{
				context.HttpContext.Response.Headers.RetryAfter =
					((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
			}

			// Same shape as the server's other errors (a list of messages), so the client shows it as-is.
			await context.HttpContext.Response.WriteAsJsonAsync(new[] { TooManyRequestsMessage }, cancellationToken);
		}
	}
}

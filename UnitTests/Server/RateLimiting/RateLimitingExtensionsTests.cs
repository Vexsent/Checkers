using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Checkers.Application.Core.Options;
using Checkers.Server.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Server.RateLimiting
{
	public class RateLimitingExtensionsTests
	{
		private static readonly RateLimit ThreePerMinute = new() { PermitLimit = 3, WindowSeconds = 60 };

		private static HttpContext Request(string ip, int? userId = null)
		{
			var http = new DefaultHttpContext();
			http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
			if (userId is { } id)
				http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
			return http;
		}

		private static PartitionedRateLimiter<HttpContext> AuthLimiter() =>
			PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitingExtensions.AuthPartition(http, ThreePerMinute));

		private static bool Allowed(PartitionedRateLimiter<HttpContext> limiter, HttpContext http)
		{
			using var lease = limiter.AttemptAcquire(http);
			return lease.IsAcquired;
		}

		[Fact]
		public void Auth_AfterTheLimit_RejectsThatIpButNotOthers()
		{
			using var limiter = AuthLimiter();

			Assert.All(Enumerable.Range(0, 3), _ => Assert.True(Allowed(limiter, Request("203.0.113.5"))));
			Assert.False(Allowed(limiter, Request("203.0.113.5")));
			Assert.True(Allowed(limiter, Request("198.51.100.7")));
		}

		[Fact]
		public void Auth_CountsByIpEvenWhenSignedIn()
		{
			using var limiter = AuthLimiter();

			for (var user = 1; user <= 3; user++)
				Assert.True(Allowed(limiter, Request("203.0.113.5", user)));

			// A fourth account from the same address gets no fresh allowance.
			Assert.False(Allowed(limiter, Request("203.0.113.5", 4)));
		}

		[Fact]
		public void Api_SignedInUsersBehindTheSameIp_EachGetTheirOwnAllowance()
		{
			using var limiter = RateLimitingExtensions.CreateApiLimiter(ThreePerMinute);

			Assert.All(Enumerable.Range(0, 3), _ => Assert.True(Allowed(limiter, Request("203.0.113.5", 1))));
			Assert.False(Allowed(limiter, Request("203.0.113.5", 1)));
			Assert.True(Allowed(limiter, Request("203.0.113.5", 2)));
		}

		[Fact]
		public void Api_AnonymousRequests_ShareTheirIpsAllowance()
		{
			using var limiter = RateLimitingExtensions.CreateApiLimiter(ThreePerMinute);

			Assert.All(Enumerable.Range(0, 3), _ => Assert.True(Allowed(limiter, Request("203.0.113.5"))));
			Assert.False(Allowed(limiter, Request("203.0.113.5")));
		}

		[Fact]
		public void PartitionKey_UsesTheAccountWhenSignedInAndTheIpOtherwise()
		{
			Assert.Equal("user:42", RateLimitingExtensions.PartitionKey(Request("203.0.113.5", 42)));
			Assert.Equal("ip:203.0.113.5", RateLimitingExtensions.PartitionKey(Request("203.0.113.5")));
		}
	}
}

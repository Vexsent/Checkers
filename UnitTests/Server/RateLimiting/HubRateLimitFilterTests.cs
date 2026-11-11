using Checkers.Application.Core.Options;
using Checkers.Server.Hubs;
using Checkers.Server.RateLimiting;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace UnitTests.Server.RateLimiting
{
	public class HubRateLimitFilterTests : IDisposable
	{
		private readonly HubRateLimitFilter _filter = new(new RateLimitingOptions
		{
			Lobby = new() { PermitLimit = 2, WindowSeconds = 60 },
			Game = new() { PermitLimit = 3, WindowSeconds = 60 },
		});

		private int _hubCalls;

		public void Dispose() => _filter.Dispose();

		/// <summary>Stands in for the hub; the filter only looks at the method being called.</summary>
		private sealed class TestHub : Hub
		{
			public Task Ping() => Task.CompletedTask;
		}

		private ValueTask<object?> Call(string method, string user = "1", Type? hubType = null)
		{
			var context = new Mock<HubCallerContext>();
			context.SetupGet(c => c.UserIdentifier).Returns(user);
			context.SetupGet(c => c.ConnectionId).Returns("connection-" + user);

			var invocation = new HubInvocationContext(
				context.Object,
				Mock.Of<IServiceProvider>(),
				new TestHub(),
				(hubType ?? typeof(GameHub)).GetMethod(method)!,
				[]);

			return _filter.InvokeMethodAsync(invocation, _ =>
			{
				_hubCalls++;
				return ValueTask.FromResult<object?>(SessionResult.Failure("reached the hub"));
			});
		}

		private static string? ErrorOf(object? result) => Assert.IsType<SessionResult>(result).Error;

		[Fact]
		public async Task Lobby_OverTheLimit_ReturnsTooManyRequestsWithoutReachingTheHub()
		{
			await Call(nameof(GameHub.CreateMatch));
			await Call(nameof(GameHub.CreateMatch));

			var result = await Call(nameof(GameHub.CreateMatch));

			Assert.Equal(RateLimitingExtensions.TooManyRequestsMessage, ErrorOf(result));
			Assert.Equal(2, _hubCalls);
		}

		[Fact]
		public async Task CreateAndJoin_ShareTheLobbyAllowance()
		{
			await Call(nameof(GameHub.CreateMatch));
			await Call(nameof(GameHub.JoinMatch));

			Assert.Equal(RateLimitingExtensions.TooManyRequestsMessage, ErrorOf(await Call(nameof(GameHub.JoinMatch))));
		}

		[Fact]
		public async Task GameCalls_HaveTheirOwnAllowance()
		{
			await Call(nameof(GameHub.CreateMatch));
			await Call(nameof(GameHub.CreateMatch));

			Assert.Equal("reached the hub", ErrorOf(await Call(nameof(GameHub.MakeMove))));
		}

		[Fact]
		public async Task Game_OverTheLimit_IsRejected()
		{
			await Call(nameof(GameHub.MakeMove));
			await Call(nameof(GameHub.OfferDraw));
			await Call(nameof(GameHub.AnswerDraw));

			Assert.Equal(RateLimitingExtensions.TooManyRequestsMessage, ErrorOf(await Call(nameof(GameHub.Resign))));
			Assert.Equal(3, _hubCalls);
		}

		[Fact]
		public async Task EachUser_HasTheirOwnAllowance()
		{
			await Call(nameof(GameHub.CreateMatch), user: "1");
			await Call(nameof(GameHub.CreateMatch), user: "1");

			Assert.Equal("reached the hub", ErrorOf(await Call(nameof(GameHub.CreateMatch), user: "2")));
		}

		[Fact]
		public async Task MethodWithoutASessionResult_OverTheLimit_ThrowsHubException()
		{
			for (var i = 0; i < 3; i++)
				await Call(nameof(TestHub.Ping), hubType: typeof(TestHub));

			var error = await Assert.ThrowsAsync<HubException>(async () => await Call(nameof(TestHub.Ping), hubType: typeof(TestHub)));
			Assert.Equal(RateLimitingExtensions.TooManyRequestsMessage, error.Message);
		}
	}
}

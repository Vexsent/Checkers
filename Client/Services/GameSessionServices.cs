using Checkers.Client.Options;
using Checkers.Shared.DTOs;
using Checkers.Shared.GameModels;
using Checkers.Shared.Hubs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Checkers.Client.Services
{
	/// <summary>
	/// The client's end of the Server's GameHub. It outlives the match page, so navigating away
	/// doesn't drop the match. The connection is dropped when the logged-in user changes because
	/// it stays authenticated as whoever opened it. Every update is replayed through the rules
	/// engine and copied into the relay's move list.
	/// </summary>
	internal class GameSessionServices : IGameSessionServices, IAsyncDisposable
	{
		private const string NoMatchError = "There is no match in progress.";

		private readonly Uri _hubUrl;
		private readonly JwtAuthStateProvider _authStateProvider;
		private readonly IRelayService _relay;
		private HubConnection? _connection;

		public GameSessionDto? Session { get; private set; }
		public SessionReplay? Replay { get; private set; }
		public bool IsOpponentConnected { get; private set; } = true;
		public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
		public event Action? Changed;

		public GameSessionServices(IOptions<AppOptions> options, JwtAuthStateProvider authStateProvider, IRelayService relay)
		{
			_relay = relay;
			_hubUrl = new Uri(new Uri(options.Value.BaseUrl), GameHubRoutes.Path);
			_authStateProvider = authStateProvider;
			_authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
		}

		public Task<string?> CreateMatchAsync() =>
			InvokeAsync(connection => connection.InvokeAsync<SessionResult>("CreateMatch"));

		public Task<string?> JoinMatchAsync(string matchCode) =>
			InvokeAsync(connection => connection.InvokeAsync<SessionResult>("JoinMatch", matchCode.Trim()));

		public Task<string?> MakeMoveAsync(GameMove move) => Session is { } session
			? InvokeAsync(connection => connection.InvokeAsync<SessionResult>("MakeMove", session.MatchCode, move))
			: Task.FromResult<string?>(NoMatchError);

		public Task<string?> ResignAsync() => Session is { } session
			? InvokeAsync(connection => connection.InvokeAsync<SessionResult>("Resign", session.MatchCode))
			: Task.FromResult<string?>(NoMatchError);

		public Task<string?> OfferDrawAsync() => Session is { } session
			? InvokeAsync(connection => connection.InvokeAsync<SessionResult>("OfferDraw", session.MatchCode))
			: Task.FromResult<string?>(NoMatchError);

		public Task<string?> AnswerDrawAsync(bool accept) => Session is { } session
			? InvokeAsync(connection => connection.InvokeAsync<SessionResult>("AnswerDraw", session.MatchCode, accept))
			: Task.FromResult<string?>(NoMatchError);

		public Task LeaveAsync() => ResetAsync();

		private async Task<string?> InvokeAsync(Func<HubConnection, Task<SessionResult>> call)
		{
			try
			{
				var connection = await GetConnectedHubAsync();
				var result = await call(connection);
				if (result.Session is { } session)
					ApplySession(session);

				return result.Error;
			}
			catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException or OperationCanceledException)
			{
				return $"Could not reach the game server. {ex.Message}";
			}
		}

		private void ApplySession(GameSessionDto session)
		{
			if (Session?.MatchCode != session.MatchCode)
				_relay.ClearMoves();

			var replay = SessionReplay.From(session);
			Session = session;
			Replay = replay;
			replay.UpdateMoveList(_relay);
			Changed?.Invoke();
		}

		private async Task<HubConnection> GetConnectedHubAsync()
		{
			_connection ??= BuildConnection();
			if (_connection.State == HubConnectionState.Disconnected)
				await _connection.StartAsync();

			return _connection;
		}

		private HubConnection BuildConnection()
		{
			var connection = new HubConnectionBuilder()
				.WithUrl(_hubUrl, options => options.AccessTokenProvider = _authStateProvider.GetTokenAsync)
				.WithAutomaticReconnect()
				.Build();

			connection.On<GameSessionDto>(nameof(IGameHubClient.SessionUpdated), ApplySession);
			connection.On<int, bool>(nameof(IGameHubClient.PlayerConnectionChanged), (_, isConnected) =>
			{
				IsOpponentConnected = isConnected;
				Changed?.Invoke();
			});

			connection.Reconnecting += _ => NotifyChangedAsync();
			connection.Closed += _ => NotifyChangedAsync();
			connection.Reconnected += async _ =>
			{
				// A reconnect gets a new connection id, which isn't in the match's group yet.
				if (Session is { } session)
					await JoinMatchAsync(session.MatchCode);

				Changed?.Invoke();
			};

			return connection;
		}

		private Task NotifyChangedAsync()
		{
			Changed?.Invoke();
			return Task.CompletedTask;
		}

		private async void OnAuthenticationStateChanged(Task<AuthenticationState> _)
		{
			try
			{
				await ResetAsync();
			}
			catch (Exception ex)
			{
				// async void: anything escaping here would take the whole app down.
				System.Diagnostics.Debug.WriteLine($"Resetting the game connection failed: {ex}");
			}
		}

		private async Task ResetAsync()
		{
			var connection = _connection;
			_connection = null;
			Session = null;
			Replay = null;
			IsOpponentConnected = true;
			_relay.ClearMoves();

			if (connection is not null)
				await connection.DisposeAsync();

			Changed?.Invoke();
		}

		public async ValueTask DisposeAsync()
		{
			_authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
			if (_connection is not null)
				await _connection.DisposeAsync();

			GC.SuppressFinalize(this);
		}
	}
}

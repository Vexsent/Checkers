using System.Net.Http.Json;
using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public class UserAuthenticationServices(IHttpClientFactory httpClientFactory, JwtAuthStateProvider authStateProvider) : IUserAuthenticationServices
	{
		private readonly IHttpClientFactory _client = httpClientFactory;
		private readonly JwtAuthStateProvider _authStateProvider = authStateProvider;

		public Task<IReadOnlyList<string>> RegisterUserAsync(RegisterRequestDto user) =>
			AuthenticateAsync("api/auth/register", user);

		public Task<IReadOnlyList<string>> LoginUserAsync(LoginRequestDto user) =>
			AuthenticateAsync("api/auth/login", user);

		public async Task LogoutUserAsync()
		{
			try
			{
				var client = _client.CreateClient(HttpClientNames.ServerApi);
				await client.PostAsync("api/auth/logout", null);
			}
			finally
			{
				//The JWT is stateless, so discarding it is the real logout; do it even if the call fails.
				await _authStateProvider.LogoutAsync();
			}
		}

		/// <summary>
		/// Posts to a register/login endpoint and, on success, stores the returned JWT so the user is
		/// signed in. Returns the Server's error messages, or an empty list on success.
		/// </summary>
		private async Task<IReadOnlyList<string>> AuthenticateAsync<TRequest>(string uri, TRequest request)
		{
			var client = _client.CreateClient(HttpClientNames.ServerApi);
			var response = await client.PostAsJsonAsync(uri, request);

			if (!response.IsSuccessStatusCode)
				return await ReadErrorsAsync(response);

			var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
			if (string.IsNullOrWhiteSpace(auth?.Token))
				return ["The server did not return an access token."];

			await _authStateProvider.MarkUserAsAuthenticatedAsync(auth);
			return [];
		}

		private static async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response)
		{
			try
			{
				var errors = await response.Content.ReadFromJsonAsync<List<string>>();
				if (errors is { Count: > 0 })
					return errors;
			}
			catch (System.Text.Json.JsonException)
			{
				//Not the Server's error-list shape (e.g. a validation ProblemDetails); fall through.
			}

			return [$"Request failed ({(int)response.StatusCode} {response.ReasonPhrase})."];
		}
	}
}

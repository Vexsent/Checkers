using System.Net.Http.Json;
using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public class MatchHistoryServices(IHttpClientFactory httpClientFactory) : IMatchHistoryServices
	{
		private readonly IHttpClientFactory _client = httpClientFactory;

		public Task<MatchHistoryDto?> GetHistoryAsync() => GetAsync<MatchHistoryDto>("api/match-history");

		public Task<MatchReplayDto?> GetMatchAsync(int matchId) => GetAsync<MatchReplayDto>($"api/match-history/{matchId}");

		public Task<List<ActiveMatchDto>?> GetActiveMatchesAsync() => GetAsync<List<ActiveMatchDto>>("api/match-history/active");

		public async Task<string?> ResignActiveMatchAsync(string matchCode)
		{
			try
			{
				var client = _client.CreateClient(HttpClientNames.ServerApi);
				var response = await client.PostAsync($"api/match-history/active/{Uri.EscapeDataString(matchCode)}/resign", null);
				if (response.IsSuccessStatusCode)
					return null;

				// The server explains a refused resignation (e.g. the match already ended) as a list of errors.
				var errors = await TryReadErrorsAsync(response);
				return errors is { Length: > 0 } ? string.Join(" ", errors) : "The match couldn't be resigned. Please try again.";
			}
			catch (HttpRequestException)
			{
				return "Could not reach the server. Please try again later.";
			}
		}

		private static async Task<string[]?> TryReadErrorsAsync(HttpResponseMessage response)
		{
			try
			{
				return await response.Content.ReadFromJsonAsync<string[]>();
			}
			catch (System.Text.Json.JsonException)
			{
				return null;
			}
		}

		private async Task<T?> GetAsync<T>(string path) where T : class
		{
			try
			{
				var client = _client.CreateClient(HttpClientNames.ServerApi);
				var response = await client.GetAsync(path);
				if (!response.IsSuccessStatusCode)
					return null;

				return await response.Content.ReadFromJsonAsync<T>();
			}
			catch (HttpRequestException)
			{
				//Server unreachable; the page shows its error message instead of crashing.
				return null;
			}
		}
	}
}

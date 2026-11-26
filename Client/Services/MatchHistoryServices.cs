using System.Net.Http.Json;
using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public class MatchHistoryServices(IHttpClientFactory httpClientFactory) : IMatchHistoryServices
	{
		private readonly IHttpClientFactory _client = httpClientFactory;

		public Task<MatchHistoryDto?> GetHistoryAsync() => GetAsync<MatchHistoryDto>("api/match-history");

		public Task<MatchReplayDto?> GetMatchAsync(int matchId) => GetAsync<MatchReplayDto>($"api/match-history/{matchId}");

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

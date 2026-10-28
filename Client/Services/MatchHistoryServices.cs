using System.Net.Http.Json;
using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public class MatchHistoryServices(IHttpClientFactory httpClientFactory) : IMatchHistoryServices
	{
		private readonly IHttpClientFactory _client = httpClientFactory;

		public async Task<MatchHistoryDto?> GetHistoryAsync()
		{
			try
			{
				var client = _client.CreateClient(HttpClientNames.ServerApi);
				var response = await client.GetAsync("api/match-history");
				if (!response.IsSuccessStatusCode)
					return null;

				return await response.Content.ReadFromJsonAsync<MatchHistoryDto>();
			}
			catch (HttpRequestException)
			{
				//Server unreachable; the page shows its error message instead of crashing.
				return null;
			}
		}
	}
}

using System.Net.Http.Json;
using Checkers.Shared.DTOs;

namespace Checkers.Client.Services
{
	public class RegistrationServices(IHttpClientFactory httpClientFactory) : IRegistrationServices
	{
		public async Task RegisterUserAsync(RegisterRequestDto user)
		{
			var client = httpClientFactory.CreateClient(HttpClientNames.ServerApi);
			var response = await client.PostAsJsonAsync("api/auth/register", user);
			response.EnsureSuccessStatusCode();
		}
	}
}

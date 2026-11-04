using System.Net.Http.Headers;
using Checkers.Client.Services;

namespace Checkers.Client.DelegateHandlers
{
	/// <summary>
	/// Attaches the stored JWT as a Bearer header on every ServerApi request so [Authorize]
	/// endpoints can identify the user.
	/// </summary>
	public class BearerTokenHandler(JwtAuthStateProvider authStateProvider) : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var token = await authStateProvider.GetTokenAsync();
			if (!string.IsNullOrWhiteSpace(token))
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

			return await base.SendAsync(request, cancellationToken);
		}
	}
}

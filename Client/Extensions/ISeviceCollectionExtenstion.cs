using Checkers.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Checkers.Client.Extensions
{
	public static class ISeviceCollectionExtenstion
	{
		/// <summary>
		/// Every *Services class here pulls its HttpClient from IHttpClientFactory
		/// (HttpClientNames.ServerApi, registered once in MauiProgram.cs) rather than
		/// taking an HttpClient by constructor — add new services' registrations here.
		/// </summary>
		public static IServiceCollection AddClientServices(this IServiceCollection services)
		{
			services.AddScoped<IUserAuthenticationServices, UserAuthenticationServices>();
			services.AddScoped<IMatchHistoryServices, MatchHistoryServices>();

			//Auth: one JwtAuthStateProvider instance serves both its concrete type (for login/logout)
			//and AuthenticationStateProvider (for AuthorizeView / AuthorizeRouteView).
			services.AddAuthorizationCore();
			services.AddCascadingAuthenticationState();
			services.AddScoped<JwtAuthStateProvider>();
			services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

			return services;
		}
	}
}

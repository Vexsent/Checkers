using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;

namespace Checkers.Client.Services
{
	/// <summary>
	/// Feeds AuthorizeView / AuthorizeRouteView from the JWT the Server's AuthService
	/// returns. The token lives in SecureStorage so a login survives app restarts;
	/// its claims are read straight from the payload (the client doesn't validate the
	/// signature, since only the Server can, and the Server checks it on every request).
	/// </summary>
	public class JwtAuthStateProvider : AuthenticationStateProvider
	{
		private const string TokenKey = "auth_token";
		private const string AuthenticationType = "jwt";

		private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

		//JwtSecurityTokenHandler may shorten claim types when writing a token; map them back
		//so ClaimTypes.Name (username) / NameIdentifier / Email match what the Server issued.
		private static readonly Dictionary<string, string> ShortClaimTypes = new()
		{
			["unique_name"] = ClaimTypes.Name,
			["nameid"] = ClaimTypes.NameIdentifier,
			["sub"] = ClaimTypes.NameIdentifier,
			["email"] = ClaimTypes.Email,
			["role"] = ClaimTypes.Role,
		};

		public override async Task<AuthenticationState> GetAuthenticationStateAsync()
		{
			string? token;
			try
			{
				token = await GetTokenAsync();
			}
			catch (Exception)
			{
				//SecureStorage can fail (e.g. a reset Android keystore); treat it as logged out
				//rather than breaking every AuthorizeView. Clear it so the next login starts fresh.
				SecureStorage.Default.RemoveAll();
				return Anonymous;
			}

			if (string.IsNullOrWhiteSpace(token))
				return Anonymous;

			var claims = ParseClaims(token);
			if (claims is null || IsExpired(claims))
			{
				SecureStorage.Default.Remove(TokenKey);
				return Anonymous;
			}

			return BuildState(claims);
		}

		/// <summary>Call after a successful login with the Server's response.</summary>
		public async Task MarkUserAsAuthenticatedAsync(AuthResponseDto response)
		{
			await SecureStorage.Default.SetAsync(TokenKey, response.Token);

			var claims = ParseClaims(response.Token);
			var state = claims is null ? Anonymous : BuildState(claims);
			NotifyAuthenticationStateChanged(Task.FromResult(state));
		}

		public Task LogoutAsync()
		{
			SecureStorage.Default.Remove(TokenKey);
			NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
			return Task.CompletedTask;
		}

		/// <summary>The raw token, for attaching as a Bearer header on API calls.</summary>
		public Task<string?> GetTokenAsync() => SecureStorage.Default.GetAsync(TokenKey);

		private static AuthenticationState BuildState(IEnumerable<Claim> claims)
		{
			//Identity.Name is the email; the username is exposed as Identity.Account (IIdentityExtension).
			var identity = new ClaimsIdentity(claims, AuthenticationType, ClaimTypes.Email, ClaimTypes.Role);
			return new AuthenticationState(new ClaimsPrincipal(identity));
		}

		private static bool IsExpired(IEnumerable<Claim> claims)
		{
			var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
			if (!long.TryParse(exp, out var seconds))
				return false;

			return DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow;
		}

		private static List<Claim>? ParseClaims(string token)
		{
			var parts = token.Split('.');
			if (parts.Length != 3)
				return null;

			try
			{
				using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
				var claims = new List<Claim>();

				foreach (var property in payload.RootElement.EnumerateObject())
				{
					var type = ShortClaimTypes.GetValueOrDefault(property.Name, property.Name);

					if (property.Value.ValueKind == JsonValueKind.Array)
					{
						foreach (var item in property.Value.EnumerateArray())
							claims.Add(new Claim(type, item.ToString()));
					}
					else
					{
						claims.Add(new Claim(type, property.Value.ToString()));
					}
				}

				return claims;
			}
			catch (Exception ex) when (ex is FormatException or JsonException)
			{
				return null;
			}
		}

		private static byte[] Base64UrlDecode(string input)
		{
			var base64 = input.Replace('-', '+').Replace('_', '/');
			base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
			return Convert.FromBase64String(base64);
		}
	}
}

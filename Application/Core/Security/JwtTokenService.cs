using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Checkers.Application.Core.Options;
using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Checkers.Shared.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Checkers.Application.Core.Security
{
	public class JwtTokenService(IOptions<AppOptions> options, IHttpContextAccessor httpContextAccessor) : IJwtTokenService
	{
		private readonly JwtOptions _options = options.Value.Jwt;

		public AuthToken GenerateToken(User user)
		{
			var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

			var claims = new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Name, user.Username),
				new Claim(ClaimTypes.Email, user.Email),
				// Users don't persist roles yet, so every account is a plain User for now.
				new Claim(ClaimTypes.Role, Roles.User),
			};

			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
			var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

			var token = new JwtSecurityToken(
				issuer: _options.Issuer,
				audience: _options.Audience,
				claims: claims,
				expires: expiresAtUtc,
				signingCredentials: credentials);

			return new AuthToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
		}

		public ClaimsPrincipal GetCurrentUser()
		{
			// The JwtBearer middleware has already validated the token and populated HttpContext.User.
			return httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
		}

		public IEnumerable<string> GetCurrentUserRoles()
		{
			var user = GetCurrentUser();
			if (user.Identity?.IsAuthenticated != true)
				return [];

			return user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct();
		}
	}
}

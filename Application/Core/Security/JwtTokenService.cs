using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Checkers.Application.Core.Options;
using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Checkers.Application.Core.Security
{
	public class JwtTokenService(IOptions<AppOptions> options) : IJwtTokenService
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
	}
}

using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checkers.Server.Controllers
{
	[ApiController]
	[Route("api/auth")]
	public class AuthController(IAuthService authService) : ControllerBase
	{
		[AllowAnonymous]
		[HttpPost("register")]
		public async Task<IActionResult> Register(RegisterRequestDto request)
		{
			var result = await authService.RegisterAsync(request);
			if (!result.Success)
				return BadRequest(result.Errors);

			return CreatedAtAction(nameof(Register), result.Response);
		}

		[AllowAnonymous]
		[HttpPost("login")]
		public async Task<IActionResult> Login(LoginRequestDto request)
		{
			var result = await authService.LoginAsync(request);
			if (!result.Success)
				return Unauthorized(result.Errors);

			return Ok(result.Response);
		}

		/// <summary>
		/// Stateless JWT: there's no server-side session to end. This endpoint exists so the
		/// client has something to call, and so [Authorize] proves the token validated. The
		/// actual "logout" is the client discarding its token.
		/// </summary>
		[Authorize]
		[HttpPost("logout")]
		public IActionResult Logout() => Ok();
	}
}

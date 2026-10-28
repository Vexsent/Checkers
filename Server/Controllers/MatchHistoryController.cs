using System.Security.Claims;
using Checkers.Application.Core.Services;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checkers.Server.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/match-history")]
	public class MatchHistoryController(IMatchHistoryService matchHistoryService) : ControllerBase
	{
		/// <summary>
		/// The signed-in user's match history. The user comes from the JWT, never the request,
		/// so nobody can read someone else's history.
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GetHistory()
		{
			if (!TryGetUserId(out var userId))
				return Unauthorized();

			return Ok(await matchHistoryService.GetHistoryAsync(userId));
		}

		[HttpPost]
		public async Task<IActionResult> RecordMatch(RecordMatchRequestDto request)
		{
			if (!TryGetUserId(out var userId))
				return Unauthorized();

			var result = await matchHistoryService.RecordMatchAsync(userId, request);
			if (!result.Success)
				return BadRequest(result.Errors);

			return CreatedAtAction(nameof(GetHistory), result.MatchId);
		}

		private bool TryGetUserId(out int userId) =>
			int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
	}
}

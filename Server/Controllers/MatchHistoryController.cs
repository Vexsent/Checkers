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

		/// <summary>
		/// One of the signed-in user's matches with every move, for replaying it. 404 if the match
		/// doesn't exist or the user didn't play in it.
		/// </summary>
		[HttpGet("{id:int}")]
		public async Task<IActionResult> GetMatch(int id)
		{
			if (!TryGetUserId(out var userId))
				return Unauthorized();

			var match = await matchHistoryService.GetMatchAsync(userId, id);
			return match is null ? NotFound() : Ok(match);
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

using System.Security.Claims;
using Checkers.Application.Core.Services;
using Checkers.Server.Hubs;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checkers.Server.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/match-history")]
	public class MatchHistoryController(
		IMatchHistoryService matchHistoryService,
		IGameSessionManager sessions,
		SessionBroadcaster broadcaster) : ControllerBase
	{
		/// <summary>
		/// The signed-in user's unfinished live matches, so they can rejoin one they dropped or left.
		/// </summary>
		[HttpGet("active")]
		public IActionResult GetActiveMatches()
		{
			if (!TryGetUserId(out var userId))
				return Unauthorized();

			return Ok(sessions.GetActiveMatches(userId));
		}

		/// <summary>
		/// Resigns one of the signed-in user's live matches without rejoining it. The opponent wins,
		/// the match is saved to both players' history, and the opponent sees it end straight away.
		/// </summary>
		[HttpPost("active/{matchCode}/resign")]
		public async Task<IActionResult> ResignActiveMatch(string matchCode)
		{
			if (!TryGetUserId(out var userId))
				return Unauthorized();

			var result = sessions.Resign(matchCode, userId);
			if (result.Session is not { } session)
				return BadRequest(new[] { result.Error });

			await broadcaster.PublishAsync(session);
			return Ok(session);
		}

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

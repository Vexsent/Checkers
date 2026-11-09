using Checkers.Shared.DTOs;
using Checkers.Shared.GameModels;
using Microsoft.AspNetCore.SignalR.Client;

namespace Checkers.Client.Services
{
	public interface IGameSessionServices
	{
		/// <summary>The match being played, or null when there isn't one.</summary>
		GameSessionDto? Session { get; }
		bool IsOpponentConnected { get; }
		HubConnectionState ConnectionState { get; }
		/// <summary>Raised off the UI thread whenever any of the above changes.</summary>
		event Action? Changed;

		/// <summary>Starts a match. Returns an error message, or null on success.</summary>
		Task<string?> CreateMatchAsync();
		/// <summary>Joins or rejoins a match. Returns an error message, or null on success.</summary>
		Task<string?> JoinMatchAsync(string matchCode);
		/// <summary>Plays one step of a turn. Returns an error message, or null on success.</summary>
		Task<string?> MakeMoveAsync(GameMove move);
		/// <summary>Resigns the match. Returns an error message, or null on success.</summary>
		Task<string?> ResignAsync();
		/// <summary>Stops following the match on this device. A player can rejoin it with its code.</summary>
		Task LeaveAsync();
	}
}

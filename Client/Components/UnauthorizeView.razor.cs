using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Checkers.Client.Components
{
	/// <summary>
	/// The inverse of AuthorizeView: renders its content only when the user is NOT
	/// authorized (logged out, or failing the given Roles/Policy). Renders nothing for
	/// authorized users. Content gets the same AuthenticationState "context" as AuthorizeView.
	/// </summary>
	public partial class UnauthorizeView
	{
		[Parameter]
		public RenderFragment<AuthenticationState>? ChildContent { get; set; }

		/// <summary>Shown while the auth state is still loading (e.g. reading SecureStorage).</summary>
		[Parameter]
		public RenderFragment? Authorizing { get; set; }

		/// <summary>Comma-separated roles; content shows when the user is in none of them.</summary>
		[Parameter]
		public string? Roles { get; set; }

		/// <summary>Policy name; content shows when the user fails it.</summary>
		[Parameter]
		public string? Policy { get; set; }

		[Parameter]
		public object? Resource { get; set; }
	}
}

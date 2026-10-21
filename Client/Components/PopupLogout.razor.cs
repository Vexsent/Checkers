using Checkers.Client.Services;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;

namespace Checkers.Client.Components
{
	public partial class PopupLogout
	{
		[Parameter]
		public int Width { get; set; } = 300;
		[Parameter]
		public int Height { get; set; } = 200;

		/// <summary>Raised with false whenever the popup closes (after logout or a click outside).</summary>
		[Parameter]
		public EventCallback<bool> OnLogout { get; set; }

		[Inject]
		private IUserAuthenticationServices AuthenticationServices { get; set; } = default!;

		private RadzenPopup popup = default!;

		/// <summary>Opens the popup anchored to <paramref name="target"/>, or closes it if already open.</summary>
		public Task ToggleAsync(ElementReference target) => popup.ToggleAsync(target);

		private async Task Logout()
		{
			// Close first: logging out swaps the layout to the logged-out header, which disposes this component.
			await popup.CloseAsync();
			await OnLogout.InvokeAsync(false);
			await AuthenticationServices.LogoutUserAsync();
		}

		private Task HandleClose() => OnLogout.InvokeAsync(false);
	}
}

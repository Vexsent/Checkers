using Checkers.Client.Components;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Checkers.Client.Layout
{
	public partial class MainLayout
	{
		[Inject]
		private DialogService DialogService { get; set; } = default!;

		private bool sidebarExpanded = false;
		private PopupLogout popup = default!;
		private RadzenButton menuButton = default!;

		private async Task OpenRegisterDialog()
		{
			await DialogService.OpenAsync<RegisterationForm>("", null, new DialogOptions() { Width = "400px", Height = "500px", ShowClose = false });
		} 

		private async Task OpenLoginDialog()
		{
			await DialogService.OpenAsync<UserLogin>("", null, new DialogOptions() { Width = "400px", Height = "300px", ShowClose = false });
		}

		// Click rather than hover so it also works on touch screens (Android/iOS).
		private Task ToggleLogout() => popup.ToggleAsync(menuButton.Element);




	}
}

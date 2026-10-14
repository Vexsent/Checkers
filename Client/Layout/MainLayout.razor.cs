using Checkers.Client.Components;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Checkers.Client.Layout
{
	public partial class MainLayout
	{
		[Inject]
		private DialogService DialogService { get; set; } = default!;

		private bool sidebarExpanded = true;

		private async Task OpenRegisterDialog()
		{
			await DialogService.OpenAsync<RegisterationForm>("Register", null, new DialogOptions() { Width = "400px", Height = "400px" });
		} 


	}
}

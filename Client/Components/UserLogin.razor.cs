using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Checkers.Client.Components
{
	public partial class UserLogin
	{
		private LoginRequestDto User { get; set; } = new LoginRequestDto();

		[Inject]
		private DialogService DialogService { get; set; } = default!;
		[Inject]
		private NotificationService NotificationService { get; set; } = default!;

		[Inject]
		private IUserAuthenticationServices AuthenticationServices { get; set; } = default!;

		private async Task OnSubmit(LoginRequestDto user)
		{
			var errors = await AuthenticationServices.LoginUserAsync(user);
			if (errors.Count > 0)
			{
				NotifyErrors("Login failed", errors);
				return;
			}

			NotificationService.Notify(new NotificationMessage
			{
				Summary = "Success",
				Detail = "User logged in successfully.",
				Duration = 4000,
				Severity = NotificationSeverity.Success
			});

			DialogService.Close();
		}

		private void NotifyErrors(string summary, IReadOnlyList<string> errors)
		{
			NotificationService.Notify(new NotificationMessage
			{
				Summary = summary,
				Detail = string.Join(" ", errors),
				Duration = 6000,
				Severity = NotificationSeverity.Error
			});
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();
		}

	}
}

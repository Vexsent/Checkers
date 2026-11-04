using Checkers.Client.Services;
using Checkers.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;


namespace Checkers.Client.Components
{
	public partial class RegisterationForm
	{
		private RegisterRequestDto User { get; set; } = new RegisterRequestDto();

		[Inject]
		private DialogService DialogService { get; set; } = default!;
		[Inject]
		private NotificationService NotificationService { get; set; } = default!;

		[Inject] 
		private IUserAuthenticationServices AuthenticationServices { get; set; } = default!;

		private async Task OnSubmit(RegisterRequestDto user)
		{
			//Optional fields: a cleared textbox sends "", but the server treats null as "not provided".
			user.MiddleName = string.IsNullOrWhiteSpace(user.MiddleName) ? null : user.MiddleName.Trim();
			user.NickName = string.IsNullOrWhiteSpace(user.NickName) ? null : user.NickName.Trim();

			var errors = await AuthenticationServices.RegisterUserAsync(user);
			if (errors.Count > 0)
			{
				NotifyErrors("Registration failed", errors);
				return;
			}

			NotificationService.Notify(new NotificationMessage
			{
				Summary = "Success",
				Detail = "User registered successfully.",
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

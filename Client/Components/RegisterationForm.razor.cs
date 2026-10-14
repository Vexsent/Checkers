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
		private IRegistrationServices RegistrationServices { get; set; } = default!;

		private async Task OnSubmit(RegisterRequestDto user)
		{
			await RegistrationServices.RegisterUserAsync(user);

			NotificationService.Notify(new NotificationMessage
			{
				Summary = "Success",
				Detail = "User registered successfully.",
				Duration = 4000,
				Severity = NotificationSeverity.Success
			});

			DialogService.Close();
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();
		}



	}
}

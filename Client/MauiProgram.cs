using Checkers.Client.DelegateHandlers;
using Checkers.Client.Extensions;
using Checkers.Client.Options;
using Checkers.Client.Services;
using Checkers.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Radzen;

namespace Checkers.Client
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				});

			builder.Services.AddMauiBlazorWebView();
			builder.Services.AddSharedExtensions();

#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.AddDebug();
#endif

			//Configuration, each layer overriding the last:
			//appsettings.json -> appsettings.{OS}.json -> appsettings.{env}.json -> appsettings.{OS}.{env}.json
			//Production stops after the {OS} layer.
			AddAppSettings(builder.Configuration, "appsettings.json");
			AddAppSettings(builder.Configuration, $"appsettings.{AppEnvironment.Platform}.json");
			if (!AppEnvironment.IsProduction)
			{
				AddAppSettings(builder.Configuration, $"appsettings.{AppEnvironment.Name}.json");
				AddAppSettings(builder.Configuration, $"appsettings.{AppEnvironment.Platform}.{AppEnvironment.Name}.json");
			}

			//Options
			builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("AppOptions"));

			//HttpClient
			builder.Services.AddTransient<AuthorizationRedirectHandler>();
			builder.Services.AddTransient<BearerTokenHandler>();

			builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
			builder.Services.AddHttpClient(HttpClientNames.ServerApi, (sp, client) =>
			{
				var options = sp.GetRequiredService<IOptions<AppOptions>>().Value;
				client.BaseAddress = new Uri(options.BaseUrl);
			})
			.AddHttpMessageHandler<AuthorizationRedirectHandler>()
			.AddHttpMessageHandler<BearerTokenHandler>();

			//Radzen
			builder.Services.AddRadzenComponents();

			//Services
			builder.Services.AddClientServices();


			return builder.Build();
		}

		/// <summary>
		/// Loads a JSON settings file packaged from Resources/Raw. Skipped if the file
		/// wasn't packaged (no such file for this platform/environment).
		/// </summary>
		private static void AddAppSettings(ConfigurationManager configuration, string fileName)
		{
			if (!FileSystem.AppPackageFileExistsAsync(fileName).GetAwaiter().GetResult())
				return;

			using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
			configuration.AddJsonStream(stream);
		}
	}
}

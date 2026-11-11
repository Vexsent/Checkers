using System.Text;
using Checkers.Application.Core.Extensions;
using Checkers.Application.Core.Options;
using Checkers.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Checkers.Client.Extensions;
using Checkers.Application.Core.Profiles;
using Checkers.Server.Hubs;
using Checkers.Server.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Checkers.Shared.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>()
	?? throw new InvalidOperationException($"Missing '{AppOptions.SectionName}' configuration section.");
var jwtOptions = appOptions.Jwt;

//Authentication
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwtOptions.Issuer,
			ValidateAudience = true,
			ValidAudience = jwtOptions.Audience,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
		};
	});
builder.Services.AddAuthorizationServiceExtensions();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSharedExtensions();

builder.Services.AddAuthorization(options =>
{
	// Secure by default: every endpoint requires an authenticated user unless it's
	// explicitly marked [AllowAnonymous] (register/login) or [Authorize] overrides it.
	options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
		.RequireAuthenticatedUser()
		.Build();
});

//DbContext
// Connection strings live in user secrets (dotnet user-secrets --project Server), not appsettings.
builder.Services.AddCheckersDbContext(builder.Configuration.GetConnectionString("CheckersDbfallback"));
		//?? throw new InvalidOperationException("Missing 'ConnectionStrings:CheckersDb'. Set it with: dotnet user-secrets set \"ConnectionStrings:CheckersDb\" \"<connection string>\" --project Server"),
	//builder.Configuration.GetConnectionString("CheckersDbFallback"));

// Controllers
builder.Services.AddControllers();

// Blazor
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

// AutoMapper
builder.Services.AddAutoMapper(opt =>
{
	opt.LicenseKey = builder.Configuration.GetSection("AutoMapper:LicenseKey").Value;
	opt.AddProfile<CheckerProfile>();
});

//Services
builder.Services.AddAuthorizationServices();
builder.Services.AddCheckersAuth();
builder.Services.AddMatchHistoryServices();

//Cors Service
builder.Services.AddCors();

//Options
builder.Services.AddOptions(builder.Configuration);

//Repositories
builder.Services.AddRepositories();

//Rate limiting (HTTP requests and live-match hub calls)
builder.Services.AddCheckersRateLimiting(appOptions.RateLimiting);

//Live match sessions
builder.Services.AddSignalR(options => options.AddFilter<HubRateLimitFilter>());
builder.Services.AddGameSessions();

//Health check for Docker / the reverse proxy
builder.Services.AddHealthChecks();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
// After authentication so signed-in users are counted per account instead of per IP.
app.UseRateLimiter();
app.UseAuthorization();

app.UseAntiforgery();

app.UseCors();

app.MapRazorPages();
app.MapControllers();
app.MapHub<GameHub>(GameHubRoutes.Path);
// Docker's health check polls this, and a page load fetches many static files; neither is limited.
app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
app.MapStaticAssets().DisableRateLimiting();

app.Run();

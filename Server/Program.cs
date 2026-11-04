using Checkers.Application.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//DbContext
// Connection strings live in user secrets (dotnet user-secrets --project Server), not appsettings.
builder.Services.AddCheckersDbContext(builder.Configuration.GetConnectionString("CheckersDbFallback"));
		//?? throw new InvalidOperationException("Missing 'ConnectionStrings:CheckersDb'. Set it with: dotnet user-secrets set \"ConnectionStrings:CheckersDb\" \"<connection string>\" --project Server"),
	//builder.Configuration.GetConnectionString("CheckersDbFallback"));

// Controllers
builder.Services.AddControllers();

// AutoMapper
builder.Services.AddAutoMapper(opt =>
{
	opt.LicenseKey = builder.Configuration.GetSection("AutoMapper:LicenseKey").Value;
});

//Services

//Repositories

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using Checkers.TestDashboard;

var builder = WebApplication.CreateBuilder(args);

// Loopback only: this app starts processes on the machine, so it must not be reachable from the network.
builder.WebHost.UseUrls("http://127.0.0.1:5199");
builder.Services.AddSingleton<TestRunner>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/tests", async (TestRunner runner) =>
{
	var listing = await runner.ListAsync();
	return listing.Error is null
		? Results.Ok(listing.Tests)
		: Results.Problem(listing.Error);
});

app.MapPost("/api/run", async (RunRequest request, TestRunner runner) =>
{
	if (!TestRunner.AreValidTargets(request.Targets))
		return Results.BadRequest("Targets must be fully qualified test method names.");

	var run = await runner.RunAsync(request.Targets);
	if (run is null)
		return Results.Conflict("A test run is already in progress.");

	return run.Error is null
		? Results.Ok(run.Results)
		: Results.Problem(run.Error);
});

app.Run();

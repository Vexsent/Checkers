using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Checkers.TestDashboard
{
	public sealed record RunRequest(string[] Targets);
	public sealed record TestResult(string Name, string Outcome, double DurationMs, string? Message, string? StackTrace);
	public sealed record TestListing(IReadOnlyList<string> Tests, string? Error);
	public sealed record TestRun(IReadOnlyList<TestResult> Results, string? Error);

	/// <summary>
	/// Runs the UnitTests project through the dotnet CLI, one command at a time because two
	/// builds of the same project would fight over its output files.
	/// </summary>
	public partial class TestRunner
	{
		private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
		private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(2);
		private const int ErrorOutputLength = 4000;

		private readonly SemaphoreSlim _gate = new(1, 1);
		private readonly string _repoRoot = FindRepoRoot();
		private string TestProject => Path.Combine(_repoRoot, "UnitTests", "UnitTests.csproj");

		[GeneratedRegex(@"^[A-Za-z_][\w.+]*$")]
		private static partial Regex TargetPattern();

		/// <summary>
		/// Only plain method names get through, so nothing can alter the test filter's meaning.
		/// </summary>
		public static bool AreValidTargets(string[]? targets) =>
			targets is not null && targets.All(t => TargetPattern().IsMatch(t));

		public async Task<TestListing> ListAsync()
		{
			await _gate.WaitAsync();
			try
			{
				var (exitCode, output) = await RunDotnetAsync("test", TestProject, "--list-tests", "--nologo");
				if (exitCode != 0)
					return new TestListing([], Tail(output));

				var tests = output
					.Split('\n')
					.Select(line => line.Trim())
					.Where(line => line.StartsWith("UnitTests.", StringComparison.Ordinal) && !line.Contains(" -> "))
					.ToList();

				return new TestListing(tests, null);
			}
			finally
			{
				_gate.Release();
			}
		}

		/// <summary>
		/// Runs the given methods, or every test when there are none. Returns null if a run is already going.
		/// </summary>
		public async Task<TestRun?> RunAsync(string[] targets)
		{
			if (!_gate.Wait(0))
				return null;

			var resultsDirectory = Path.Combine(Path.GetTempPath(), "CheckersTestDashboard", Path.GetRandomFileName());
			try
			{
				List<string> arguments = ["test", TestProject, "--nologo", "--logger", "trx;LogFileName=results.trx", "--results-directory", resultsDirectory];
				if (targets.Length > 0)
				{
					arguments.Add("--filter");
					arguments.Add(string.Join('|', targets.Select(t => $"FullyQualifiedName={t}")));
				}

				var (_, output) = await RunDotnetAsync([.. arguments]);

				// dotnet test exits non-zero for failing tests too, so the results file is what tells
				// a failed test apart from a failed build.
				var resultsFile = Path.Combine(resultsDirectory, "results.trx");
				return File.Exists(resultsFile)
					? new TestRun(ReadResults(resultsFile), null)
					: new TestRun([], Tail(output));
			}
			finally
			{
				_gate.Release();
				if (Directory.Exists(resultsDirectory))
					Directory.Delete(resultsDirectory, recursive: true);
			}
		}

		private static List<TestResult> ReadResults(string resultsFile) =>
			XDocument.Load(resultsFile)
				.Descendants(Trx + "UnitTestResult")
				.Select(result =>
				{
					var error = result.Element(Trx + "Output")?.Element(Trx + "ErrorInfo");
					var duration = (string?)result.Attribute("duration");
					return new TestResult(
						(string)result.Attribute("testName")!,
						(string)result.Attribute("outcome")!,
						duration is null ? 0 : TimeSpan.Parse(duration).TotalMilliseconds,
						error?.Element(Trx + "Message")?.Value,
						error?.Element(Trx + "StackTrace")?.Value);
				})
				.ToList();

		private async Task<(int ExitCode, string Output)> RunDotnetAsync(params string[] arguments)
		{
			var startInfo = new ProcessStartInfo("dotnet")
			{
				WorkingDirectory = _repoRoot,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			foreach (var argument in arguments)
				startInfo.ArgumentList.Add(argument);

			// Reused build nodes would inherit the output pipes and keep them open after dotnet exits.
			startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

			using var process = Process.Start(startInfo)!;
			var standardOutput = process.StandardOutput.ReadToEndAsync();
			var standardError = process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();

			// A background compiler server can hold the pipes open as well, so don't wait forever.
			await Task.WhenAny(Task.WhenAll(standardOutput, standardError), Task.Delay(OutputDrainTimeout));

			var output = standardOutput.IsCompletedSuccessfully ? standardOutput.Result : string.Empty;
			var error = standardError.IsCompletedSuccessfully ? standardError.Result : string.Empty;
			return (process.ExitCode, output + error);
		}

		private static string Tail(string output) =>
			output.Length <= ErrorOutputLength ? output : output[^ErrorOutputLength..];

		private static string FindRepoRoot()
		{
			for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
			{
				if (File.Exists(Path.Combine(directory.FullName, "Checkers.slnx")))
					return directory.FullName;
			}

			throw new InvalidOperationException("Could not find Checkers.slnx above the application directory.");
		}
	}
}

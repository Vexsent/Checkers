using System.Reflection;

namespace Checkers.Client.Options
{
	/// <summary>
	/// The environment and platform this build was compiled for, stamped in by the
	/// AppEnvironment / AppPlatform MSBuild properties in Client.csproj (Debug -> Development,
	/// Release -> Production, or -p:AppEnvironment=Staging; platform from the target framework).
	/// Falls back to Production if the environment stamp is missing.
	/// </summary>
	public static class AppEnvironment
	{
		public const string Development = "Development";
		public const string Staging = "Staging";
		public const string Production = "Production";

		public static string Name { get; } = ReadMetadata("AppEnvironment") ?? Production;

		/// <summary>Android, iOS, MacCatalyst or Windows.</summary>
		public static string Platform { get; } = ReadMetadata("AppPlatform") ?? string.Empty;

		public static bool IsDevelopment => Name == Development;
		public static bool IsStaging => Name == Staging;
		public static bool IsProduction => Name == Production;

		private static string? ReadMetadata(string key) =>
			typeof(AppEnvironment).Assembly
				.GetCustomAttributes<AssemblyMetadataAttribute>()
				.FirstOrDefault(a => a.Key == key)?.Value;
	}
}

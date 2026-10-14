namespace Checkers.Client.Services
{
	/// <summary>
	/// Named HttpClient registered once in MauiProgram.cs via AddHttpClient(...). Every
	/// *Services class pulls it from IHttpClientFactory by this name instead of taking
	/// an HttpClient by constructor — keeps one factory serving every future service.
	/// </summary>
	public static class HttpClientNames
	{
		public const string ServerApi = "Checkers.ServerAPI";
	}
}

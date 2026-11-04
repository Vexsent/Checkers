using Microsoft.JSInterop;
using System.Net;

namespace Checkers.Client.DelegateHandlers
{

    public class AuthorizationRedirectHandler(IJSRuntime jsRuntime) : DelegatingHandler
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;

        // Endpoints whose 401 means "bad credentials", not "please sign in" — pass those straight back.
        private static readonly string[] AnonymousPaths = ["api/auth/login", "api/auth/register", "api/auth/logout"];

        // Handlers are pooled and reused across requests, so each popup needs its own completion source.
        private TaskCompletionSource<bool> _authorizationCompletedTaskCompletionSource = new();

        [JSInvokable("NotifyAuthorizationComplete")]
        public void NotifyAuthorizationComplete()
        {
            _authorizationCompletedTaskCompletionSource.TrySetResult(true);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Buffer the body now: a sent HttpRequestMessage can't be re-sent, so a retry needs a copy.
            if (request.Content is not null)
                await request.Content.LoadIntoBufferAsync(cancellationToken);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized || IsAnonymousPath(request))
                return response;

            _authorizationCompletedTaskCompletionSource = new();
            using var dotNetRef = DotNetObjectReference.Create(this);

            try
            {
                await _jsRuntime.InvokeVoidAsync("openAuthorizationPopup", cancellationToken, dotNetRef);
            }
            catch (JSException)
            {
                // No popup available; let the caller handle the 401.
                return response;
            }

            var timeSpan = TimeSpan.FromSeconds(10);

            var task = await Task.WhenAny(_authorizationCompletedTaskCompletionSource.Task, Task.Delay(timeSpan, cancellationToken));

            if (task != _authorizationCompletedTaskCompletionSource.Task)
            {
                Console.WriteLine("Authorization popup timed out.");
                return response;
            }

            Console.WriteLine("Authorization popup completed.");
            response.Dispose();

            // Retry with a fresh copy; BearerTokenHandler (inner) attaches the new token.
            return await base.SendAsync(await CloneAsync(request), cancellationToken);
        }

        private static bool IsAnonymousPath(HttpRequestMessage request)
        {
            var path = request.RequestUri?.IsAbsoluteUri == true
                ? request.RequestUri.AbsolutePath.TrimStart('/')
                : request.RequestUri?.OriginalString.TrimStart('/') ?? "";

            return AnonymousPaths.Any(p => path.EndsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

            foreach (var header in request.Headers)
            {
                if (header.Key != "Authorization")
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync());
                foreach (var header in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}

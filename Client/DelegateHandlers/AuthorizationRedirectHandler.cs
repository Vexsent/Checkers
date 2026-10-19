using Microsoft.JSInterop;
using System.Net;

namespace Checkers.Client.DelegateHandlers
{

    public class AuthorizationRedirectHandler(IJSRuntime jsRuntime) : DelegatingHandler
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;

        private readonly TaskCompletionSource<bool> _authorizationCompletedTaskCompletionSource = new();

        [JSInvokable("NotifyAuthorizationComplete")]
        public void NotifyAuthorizationComplete()
        {
            _authorizationCompletedTaskCompletionSource.TrySetResult(true);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var dotNetRef = DotNetObjectReference.Create(this);

                await _jsRuntime.InvokeVoidAsync("openAuthorizationPopup", dotNetRef);

                var timeSpan = TimeSpan.FromSeconds(10);

                var task = await Task.WhenAny(_authorizationCompletedTaskCompletionSource.Task, Task.Delay(timeSpan, cancellationToken));

                if (task == _authorizationCompletedTaskCompletionSource.Task)
                {
                    Console.WriteLine("Authorization popup completed.");
                }
                else
                {
                    Console.WriteLine("Authorization popup timed out.");
                }

                response = await base.SendAsync(request, cancellationToken);
            }

            return response;
        }
    }
}


using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace NMRS.Blazor.Resources.Components
{
    public class BlazorError : ComponentBase
    {
        public BlazorError()
        {
        }

        [Parameter]
        public BlazorErrorType Type { get; set; }
        [Parameter]
        public string Message { get; set; }
        [Parameter]
        public string ReloadMessage { get; set; }
        [Parameter]
        public string DismissMessage { get; set; }

#nullable disable
        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
            // Minimal implementation to satisfy compiler.
            // You can replace this with actual markup rendering logic.
            base.BuildRenderTree(__builder);
        }
#nullable enable
    }
}
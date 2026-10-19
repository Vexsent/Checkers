using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Collections.Generic;

namespace NMRS.Blazor.Resources.Components
{
    public class Splash : ComponentBase
    {
        public Splash();

        [Parameter]
        public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? UnmatchedAttributes { get; set; }
        [Parameter]
        public string Id { get; set; }
        [Parameter]
        public bool IsLoading { get; set; }
        [Parameter]
        public LoaderType LoaderType { get; set; }
        [Parameter]
        public string LoaderColor { get; set; }
        [Parameter]
        public string? Message { get; set; }

#nullable disable
        protected override void BuildRenderTree(RenderTreeBuilder __builder);

#nullable enable
    }
}
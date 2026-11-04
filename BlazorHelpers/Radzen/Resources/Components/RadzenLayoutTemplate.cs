using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System;

namespace NMRS.Blazor.Radzen.Resources.Components
{
    public class RadzenLayoutTemplate : ComponentBase
    {
        public RadzenLayoutTemplate();

        [Parameter]
        public string? User { get; set; }
        [Parameter]
        public EventCallback<bool> SidebarExpansionChange { get; set; }
        [Parameter]
        public bool DisplayNonProductionEnvironment { get; set; }
        [Parameter]
        public bool AlwaysShowSidebarToggle { get; set; }
        [Parameter]
        public EventCallback<bool> SidebarResponsiveChanged { get; set; }
        [Parameter]
        public bool SidebarResponsive { get; set; }
        [Parameter]
        public EventCallback<bool> SidebarExpandedChanged { get; set; }
        [Parameter]
        public bool SidebarExpanded { get; set; }
        [Parameter]
        public string Style { get; set; }
        [Obsolete("This parameter is deprecated. Use the 'Header' parameter instead.", true)]
        [Parameter]
        public string? AppName { get; set; }
        [Parameter]
        public RenderFragment? Header { get; set; }
        [Parameter]
        public RenderFragment? Menu { get; set; }
        [Parameter]
        public RenderFragment? Sidebar { get; set; }
        [Parameter]
        public RenderFragment? Body { get; set; }
        [Parameter]
        public EventCallback OnEnvironmentDoubleClick { get; set; }

#nullable disable
        protected override void BuildRenderTree(RenderTreeBuilder __builder);
        protected override void OnInitialized();
        protected override void OnParametersSet();

#nullable enable
    }
}
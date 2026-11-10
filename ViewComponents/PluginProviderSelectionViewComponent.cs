using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.ViewComponents;

/// <summary>CORE-14. The provider selection block on the plugin administration page.</summary>
public sealed class PluginProviderSelectionViewComponent : ViewComponent
{
    private readonly IPluginProviderSelectionService _selection;

    public PluginProviderSelectionViewComponent(IPluginProviderSelectionService selection)
    {
        _selection = selection;
    }

    public IViewComponentResult Invoke() => View(_selection.GetStatuses());
}

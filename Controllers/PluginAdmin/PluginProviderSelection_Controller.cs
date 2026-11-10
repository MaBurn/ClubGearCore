using ClubGear.Models.Feedback;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers;

/// <summary>CORE-14. Administrator choice of the provider for a single-active capability.</summary>
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class PluginProviderSelectionController : Controller
{
    private readonly IPluginProviderSelectionService _selection;

    public PluginProviderSelectionController(IPluginProviderSelectionService selection)
    {
        _selection = selection;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(string capabilityKey, string? moduleId, CancellationToken cancellationToken = default)
    {
        var result = await _selection.SelectAsync(capabilityKey ?? string.Empty, moduleId, User.Identity?.Name, cancellationToken);

        var feedback = result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message);
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;

        return RedirectToAction(nameof(PluginAdminController.Index), "PluginAdmin");
    }
}

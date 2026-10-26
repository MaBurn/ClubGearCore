using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/plugin/page-commands")]
[Authorize]
public sealed class PluginPageApiController : ControllerBase
{
    private readonly IPluginPageService _pluginPageService;
    private readonly IPluginPageColumnPreferenceService _columnPreferenceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PluginPageApiController(
        IPluginPageService pluginPageService,
        IPluginPageColumnPreferenceService columnPreferenceService,
        UserManager<ApplicationUser> userManager)
    {
        _pluginPageService = pluginPageService;
        _columnPreferenceService = columnPreferenceService;
        _userManager = userManager;
    }

    // One user's column-visibility choice for one generic list page, keyed by (user, moduleId,
    // pageKey). No permission check beyond [Authorize]: visibility is a personal display
    // preference, not an authorization boundary — the page's own ListPermission already gated
    // whether this user may see the page (and therefore its data) at all.
    [HttpPost("column-preferences")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetColumnPreferences(
        [FromBody] PluginPageColumnPreferenceRequest body,
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        await _columnPreferenceService.SetHiddenColumnsAsync(
            userId, body.ModuleId, body.PageKey, body.HiddenColumns ?? [], cancellationToken);

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteCommand(
        [FromBody] PluginPageCommandRequest body,
        CancellationToken cancellationToken = default)
    {
        var arguments = body.Arguments
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = await _pluginPageService.ExecuteCommandAsync(
            body.ModuleId,
            body.PageKey,
            body.CommandKey,
            body.EntityKey,
            arguments,
            User,
            cancellationToken);

        if (result.IsNotFound)
            return NotFound(new PluginCommandResult(false, "not-found", "Seite oder Befehl nicht gefunden."));

        if (result.IsForbidden)
            return StatusCode(StatusCodes.Status403Forbidden,
                new PluginCommandResult(false, "forbidden", "Keine Berechtigung."));

        if (!result.IsSuccess || result.Value is null)
            return BadRequest(new PluginCommandResult(false, "error", result.ErrorMessage ?? "Befehl fehlgeschlagen."));

        if (!result.Value.Success)
            return BadRequest(result.Value);

        return Ok(result.Value);
    }
}

public sealed record PluginPageCommandRequest(
    string ModuleId,
    string PageKey,
    string CommandKey,
    string? EntityKey = null,
    Dictionary<string, string>? Arguments = null);

public sealed record PluginPageColumnPreferenceRequest(
    string ModuleId,
    string PageKey,
    List<string>? HiddenColumns = null);

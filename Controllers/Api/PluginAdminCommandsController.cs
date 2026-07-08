using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/admin/plugin-commands")]
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class PluginAdminCommandsController : ControllerBase
{
    private readonly IPluginAdminCommandService _pluginAdminCommandService;

    public PluginAdminCommandsController(IPluginAdminCommandService pluginAdminCommandService)
    {
        _pluginAdminCommandService = pluginAdminCommandService;
    }

    [HttpGet("panels")]
    public async Task<IActionResult> GetPanels(CancellationToken cancellationToken = default)
    {
        var panels = await _pluginAdminCommandService.GetPanelsAsync(User, cancellationToken);
        return Ok(panels);
    }

    [HttpPost]
    public async Task<IActionResult> Execute(
        [FromBody] PluginAdminCommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _pluginAdminCommandService.ExecuteCommandAsync(
            request.ModuleId,
            new PluginAdminCommandRequest(request.PanelKey, request.CommandKey, request.Arguments),
            User,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Status switch
        {
            "invalid" => BadRequest(result),
            "plugin-not-active" => NotFound(result),
            "panel-not-found" => NotFound(result),
            "command-not-found" => NotFound(result),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }
}

public sealed record PluginAdminCommandExecutionRequest(
    string ModuleId,
    string PanelKey,
    string CommandKey,
    IReadOnlyDictionary<string, string>? Arguments = null);
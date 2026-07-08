using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/plugin/page-commands")]
[Authorize]
public sealed class PluginPageApiController : ControllerBase
{
    private readonly IPluginPageService _pluginPageService;

    public PluginPageApiController(IPluginPageService pluginPageService)
    {
        _pluginPageService = pluginPageService;
    }

    [HttpPost]
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

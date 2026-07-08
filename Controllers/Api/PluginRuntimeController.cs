using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/plugin-runtime")]
[Authorize]
public sealed class PluginRuntimeController : ControllerBase
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly PluginEndpointRegistrar _pluginEndpointRegistrar;

    public PluginRuntimeController(
        IPluginRegistryReader pluginRegistryReader,
        PluginEndpointRegistrar pluginEndpointRegistrar)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _pluginEndpointRegistrar = pluginEndpointRegistrar;
    }

    [HttpGet("{moduleId}")]
    public Task<IActionResult> InvokeRootRoute(string moduleId, CancellationToken cancellationToken = default)
        => InvokeRoute(moduleId, routePath: null, cancellationToken);

    [HttpGet("{moduleId}/{**routePath}")]
    public async Task<IActionResult> InvokeRoute(string moduleId, string? routePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return BadRequest(new { status = "invalid", message = "moduleId ist erforderlich." });
        }

        var module = _pluginRegistryReader.GetModule(moduleId);
        if (module is null)
        {
            return NotFound(new { status = "plugin-not-active", message = $"Plugin '{moduleId}' ist nicht aktiv." });
        }

        var normalizedRoute = string.IsNullOrWhiteSpace(routePath)
            ? "/"
            : $"/{routePath.TrimStart('/')}";

        PluginEndpointResult endpointResult;
        try
        {
            endpointResult = await _pluginEndpointRegistrar.InvokeGetAsync(module, normalizedRoute, User, cancellationToken);
        }
        catch (NotFoundException)
        {
            return NotFound(new { status = "route-not-found", message = $"Plugin-Route '{normalizedRoute}' wurde nicht gefunden." });
        }

        if (endpointResult.Body is null)
        {
            return StatusCode(endpointResult.StatusCode);
        }

        return new ContentResult
        {
            StatusCode = endpointResult.StatusCode,
            ContentType = endpointResult.ContentType,
            Content = endpointResult.Body
        };
    }
}

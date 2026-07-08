using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClubGear.Controllers;

[Authorize]
[Route("plugin-pages/{moduleId}/{pageKey}")]
public sealed class PluginPageController : Controller
{
    private readonly IPluginPageService _pluginPageService;

    public PluginPageController(IPluginPageService pluginPageService)
    {
        _pluginPageService = pluginPageService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string moduleId,
        string pageKey,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        var definitionResult = await _pluginPageService.GetPageDefinitionAsync(
            moduleId, pageKey, User, cancellationToken);

        if (definitionResult.IsForbidden)
            return Forbid();
        if (definitionResult.IsNotFound || definitionResult.Value is null)
            return NotFound();

        var rowsResult = await _pluginPageService.GetRowsAsync(
            moduleId, pageKey, User, q, entityKey: null, cancellationToken);

        if (rowsResult.IsForbidden)
            return Forbid();
        if (rowsResult.IsNotFound)
            return NotFound();

        var rows = rowsResult.Value ?? Array.Empty<IReadOnlyDictionary<string, string?>>();

        return View((definitionResult.Value, rows, moduleId, q));
    }

    [HttpGet("detail/{entityKey}")]
    public async Task<IActionResult> Detail(
        string moduleId,
        string pageKey,
        string entityKey,
        CancellationToken cancellationToken = default)
    {
        var definitionResult = await _pluginPageService.GetPageDefinitionAsync(
            moduleId, pageKey, User, cancellationToken);

        if (definitionResult.IsForbidden)
            return Forbid();
        if (definitionResult.IsNotFound || definitionResult.Value is null)
            return NotFound();

        var rowsResult = await _pluginPageService.GetRowsAsync(
            moduleId, pageKey, User, filterValue: null, entityKey: entityKey, cancellationToken);

        if (rowsResult.IsForbidden)
            return Forbid();
        if (rowsResult.IsNotFound || rowsResult.Value is null || rowsResult.Value.Count == 0)
            return NotFound();

        var row = rowsResult.Value[0];
        return View((definitionResult.Value, row, moduleId, entityKey));
    }
}

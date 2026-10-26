using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClubGear.Controllers;

[Authorize]
[Route("plugin-pages/{moduleId}/{pageKey}")]
public sealed class PluginPageController : Controller
{
    private readonly IPluginPageService _pluginPageService;
    private readonly IPluginPageColumnPreferenceService _columnPreferenceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PluginPageController(
        IPluginPageService pluginPageService,
        IPluginPageColumnPreferenceService columnPreferenceService,
        UserManager<ApplicationUser> userManager)
    {
        _pluginPageService = pluginPageService;
        _columnPreferenceService = columnPreferenceService;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string moduleId,
        string pageKey,
        string? q = null,
        string? sort = null,
        string? dir = null,
        int page = 1,
        int? size = null,
        CancellationToken cancellationToken = default)
    {
        var definitionResult = await _pluginPageService.GetPageDefinitionAsync(
            moduleId, pageKey, User, cancellationToken);

        if (definitionResult.IsForbidden)
            return Forbid();
        if (definitionResult.IsNotFound || definitionResult.Value is null)
            return NotFound();

        var query = new PluginPageQuery
        {
            FilterValue = q,
            SortKey = sort,
            SortDirection = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase)
                ? PluginSortDirection.Descending
                : PluginSortDirection.Ascending,
            Page = page,
            PageSize = size
        };

        var rowsResult = await _pluginPageService.GetRowsPageAsync(
            moduleId, pageKey, User, query, cancellationToken);

        if (rowsResult.IsForbidden)
            return Forbid();
        if (rowsResult.IsNotFound)
            return NotFound();

        var result = rowsResult.Value
            ?? new PluginPageRows(Array.Empty<IReadOnlyDictionary<string, string?>>(), 0);

        var userId = _userManager.GetUserId(User);
        var hiddenColumns = userId is null
            ? null
            : await _columnPreferenceService.GetHiddenColumnsAsync(userId, moduleId, pageKey, cancellationToken);

        var view = new PluginPageIndexViewModel(
            definitionResult.Value,
            result.Rows,
            moduleId,
            q,
            query.SortKey,
            query.SortDirection,
            query.Page,
            query.PageSize ?? definitionResult.Value.DefaultPageSize,
            result.TotalCount,
            hiddenColumns);

        return View(view);
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

/// <summary>
/// What the plugin page list view renders. Replaces the previous four-element tuple, which had
/// no room for the paging and sorting state introduced with contract 1.11.0.
/// </summary>
public sealed record PluginPageIndexViewModel(
    PluginPageDefinition Definition,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    string ModuleId,
    string? Filter,
    string? SortKey,
    PluginSortDirection SortDirection,
    int Page,
    int? PageSize,
    int TotalCount,
    IReadOnlySet<string>? HiddenColumns = null)
{
    public bool IsPaged => PageSize is > 0;

    public int PageCount => IsPaged
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize!.Value))
        : 1;

    public bool HasPreviousPage => IsPaged && Page > 1;

    public bool HasNextPage => IsPaged && Page < PageCount;

    /// <summary>1-based index of the first row on this page, for the "x–y of z" label.</summary>
    public int FirstRowNumber => TotalCount == 0
        ? 0
        : (IsPaged ? ((Page - 1) * PageSize!.Value) + 1 : 1);

    public int LastRowNumber => TotalCount == 0
        ? 0
        : (IsPaged ? Math.Min(Page * PageSize!.Value, TotalCount) : TotalCount);
}

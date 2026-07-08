using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Admin;

[Route("Admin/ExternalLogin")]
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class ExternalLoginAdminController : Controller
{
    private readonly IExternalLoginConfigService _configService;

    public ExternalLoginAdminController(IExternalLoginConfigService configService)
    {
        _configService = configService;
    }

    // ------------------------------------------------------------------
    // GET /Admin/ExternalLogin  or  GET /Admin/ExternalLogin/Index
    // ------------------------------------------------------------------

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var providers = await _configService.GetAllDeclaredProvidersAsync(cancellationToken);
        return View("~/Views/Admin/ExternalLogin/Index.cshtml", providers);
    }

    // ------------------------------------------------------------------
    // GET /Admin/ExternalLogin/Configure?provider=<key>
    // ------------------------------------------------------------------

    [HttpGet("Configure")]
    public async Task<IActionResult> Configure(
        string? provider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return RedirectToAction(nameof(Index));

        var providers = await _configService.GetAllDeclaredProvidersAsync(cancellationToken);
        var info = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderKey, provider, StringComparison.OrdinalIgnoreCase));

        if (info is null)
            return NotFound();

        // Retrieve the current config values (may be empty).
        var configValues = await _configService.GetConfigAsync(provider, cancellationToken);

        var model = new ExternalLoginConfigureViewModel(info, configValues);
        return View("~/Views/Admin/ExternalLogin/Configure.cshtml", model);
    }

    // ------------------------------------------------------------------
    // POST /Admin/ExternalLogin/Configure?provider=<key>
    // ------------------------------------------------------------------

    [HttpPost("Configure")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(
        string? provider,
        [FromForm] Dictionary<string, string> configValues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return RedirectToAction(nameof(Index));

        // Remove the MVC anti-forgery token and action keys that leak in via [FromForm].
        configValues.Remove("__RequestVerificationToken");
        configValues.Remove("provider");

        await _configService.SaveConfigAsync(provider, configValues, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // POST /Admin/ExternalLogin/Test?provider=<key>
    // ------------------------------------------------------------------

    [HttpPost("Test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(
        string? provider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { success = false, message = "provider parameter is required." });

        var result = await _configService.TestConnectionAsync(provider, cancellationToken);

        return Json(new { success = result.Success, message = result.Message });
    }

    // ------------------------------------------------------------------
    // POST /Admin/ExternalLogin/Activate?provider=<key>&enabled=<bool>
    // ------------------------------------------------------------------

    [HttpPost("Activate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        string? provider,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return RedirectToAction(nameof(Index));

        var flag = new Dictionary<string, string>
        {
            ["enabled"] = enabled ? "true" : "false"
        };

        await _configService.SaveConfigAsync(provider, flag, cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}

// ---------------------------------------------------------------------------
// View model used by the Configure GET/POST pair
// ---------------------------------------------------------------------------

public sealed record ExternalLoginConfigureViewModel(
    ExternalProviderInfo ProviderInfo,
    IReadOnlyDictionary<string, string> ConfigValues);

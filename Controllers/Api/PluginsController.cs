using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/plugins")]
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class PluginsController : ControllerBase
{
    private readonly IPluginInstallerService _pluginInstallerService;
    private readonly IPluginLifecycleService _pluginLifecycleService;
    private readonly IPluginAdminQueryService _pluginAdminQueryService;

    public PluginsController(
        IPluginInstallerService pluginInstallerService,
        IPluginLifecycleService pluginLifecycleService,
        IPluginAdminQueryService pluginAdminQueryService)
    {
        _pluginInstallerService = pluginInstallerService;
        _pluginLifecycleService = pluginLifecycleService;
        _pluginAdminQueryService = pluginAdminQueryService;
    }

    [HttpGet("installed")]
    public IActionResult GetInstalled()
    {
        var installed = _pluginAdminQueryService.GetPluginStatuses();
        return Ok(installed);
    }

    [HttpPost("install/marketplace")]
    public async Task<IActionResult> InstallFromMarketplace(
        [FromBody] MarketplaceInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _pluginInstallerService.InstallOrUpgradeFromMarketplaceAsync(request.ModuleId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("install/zip")]
    public async Task<IActionResult> InstallFromZip(
        [FromBody] ZipInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        byte[] packageBytes;
        try
        {
            packageBytes = Convert.FromBase64String(request.ZipPackageBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new PluginInstallOperationResult(false, "invalid", "zipPackageBase64 ist kein gueltiger Base64-String."));
        }

        var result = await _pluginInstallerService.InstallOrUpgradeFromZipAsync(
            request.FileName,
            packageBytes,
            request.ExpectedSha256Hex,
            request.SignatureBase64,
            request.SignerPublicKeyPem,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{moduleId}/activate")]
    public async Task<IActionResult> Activate(string moduleId, CancellationToken cancellationToken = default)
    {
        var result = await _pluginLifecycleService.ActivateAsync(moduleId, cancellationToken);
        return MapLifecycleResult(result);
    }

    [HttpPost("{moduleId}/deactivate")]
    public async Task<IActionResult> Deactivate(string moduleId, CancellationToken cancellationToken = default)
    {
        var result = await _pluginLifecycleService.DeactivateAsync(moduleId, cancellationToken);
        return MapLifecycleResult(result);
    }

    private static IActionResult MapLifecycleResult(PluginLifecycleOperationResult result)
    {
        if (result.Success)
        {
            return new OkObjectResult(result);
        }

        if (string.Equals(result.Status, "not-found", StringComparison.OrdinalIgnoreCase))
        {
            return new NotFoundObjectResult(result);
        }

        return new BadRequestObjectResult(result);
    }
}

public sealed record MarketplaceInstallRequest(string ModuleId);

public sealed record ZipInstallRequest(
    string FileName,
    string ZipPackageBase64,
    string ExpectedSha256Hex,
    string SignatureBase64,
    string SignerPublicKeyPem);

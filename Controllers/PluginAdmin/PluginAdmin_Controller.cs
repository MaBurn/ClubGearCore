using ClubGear.Models.Feedback;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace ClubGear.Controllers;

[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class PluginAdminController : Controller
{
    private readonly IPluginInstallerService _pluginInstallerService;
    private readonly IPluginLifecycleService _pluginLifecycleService;
    private readonly IPluginAdminQueryService _pluginAdminQueryService;
    private readonly IPluginUninstallService _pluginUninstallService;

    public PluginAdminController(
        IPluginInstallerService pluginInstallerService,
        IPluginLifecycleService pluginLifecycleService,
        IPluginAdminQueryService pluginAdminQueryService,
        IPluginUninstallService pluginUninstallService)
    {
        _pluginInstallerService = pluginInstallerService;
        _pluginLifecycleService = pluginLifecycleService;
        _pluginAdminQueryService = pluginAdminQueryService;
        _pluginUninstallService = pluginUninstallService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(_pluginAdminQueryService.GetPluginStatuses());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InstallFromMarketplace(string moduleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Error("Plugin-Modul-ID ist erforderlich."));
            return RedirectToAction(nameof(Index));
        }

        var result = await _pluginInstallerService.InstallOrUpgradeFromMarketplaceAsync(moduleId.Trim(), cancellationToken);
        SetTempDataFeedback(result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InstallFromZip(
        IFormFile? zipFile,
        string expectedSha256Hex,
        string signatureBase64,
        string signerPublicKeyPem,
        CancellationToken cancellationToken = default)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Error("Bitte eine gueltige ZIP-Datei auswaehlen."));
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(expectedSha256Hex)
            || string.IsNullOrWhiteSpace(signatureBase64)
            || string.IsNullOrWhiteSpace(signerPublicKeyPem))
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Error("SHA-256, Signatur und Public Key sind fuer den ZIP-Upload erforderlich."));
            return RedirectToAction(nameof(Index));
        }

        await using var zipStream = zipFile.OpenReadStream();
        using var buffer = new MemoryStream();
        await zipStream.CopyToAsync(buffer, cancellationToken);

        var result = await _pluginInstallerService.InstallOrUpgradeFromZipAsync(
            zipFile.FileName,
            buffer.ToArray(),
            expectedSha256Hex.Trim(),
            signatureBase64.Trim(),
            signerPublicKeyPem.Trim(),
            cancellationToken);

        SetTempDataFeedback(result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string moduleId, CancellationToken cancellationToken = default)
    {
        var result = await _pluginLifecycleService.ActivateAsync(moduleId, cancellationToken);
        SetTempDataFeedback(result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string moduleId, CancellationToken cancellationToken = default)
    {
        var result = await _pluginLifecycleService.DeactivateAsync(moduleId, cancellationToken);
        SetTempDataFeedback(result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message));

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Detail(string moduleId)
    {
        var viewModel = _pluginAdminQueryService.GetPluginStatus(moduleId);
        if (viewModel is null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View("Detail", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string moduleId, bool removeData, CancellationToken cancellationToken = default)
    {
        var result = await _pluginUninstallService.UninstallAsync(moduleId, removeData, cancellationToken);
        SetTempDataFeedback(result.Success
            ? ActionFeedbackViewModel.Success(result.Message)
            : ActionFeedbackViewModel.Error(result.Message));

        return RedirectToAction(nameof(Index));
    }

    private void SetTempDataFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }
}
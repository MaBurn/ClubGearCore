using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers;

[Authorize]
[PermissionAuthorize(PermissionKeys.SelfServiceAccess)]
public class SelfServiceController : Controller
{
    private readonly ISelfServiceFeatureService _selfServiceFeatureService;
    private readonly IMemberPluginSlotService _memberPluginSlotService;
    private readonly ISelfServiceSectionService _selfServiceSectionService;

    public SelfServiceController(
        ISelfServiceFeatureService selfServiceFeatureService,
        IMemberPluginSlotService memberPluginSlotService,
        ISelfServiceSectionService selfServiceSectionService)
    {
        _selfServiceFeatureService = selfServiceFeatureService;
        _memberPluginSlotService = memberPluginSlotService;
        _selfServiceSectionService = selfServiceSectionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var outcome = await _selfServiceFeatureService.GetDashboardAsync(User, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Challenge();
        }

        ViewData["MemberLinked"] = outcome.MemberLinked;

        if (outcome.Member is not null)
        {
            var snapshot = await _memberPluginSlotService.GetSlotsAsync(outcome.Member, User, cancellationToken);
            ViewData["MemberPluginSlots"] = snapshot;
            ViewData["PluginActionEndpoint"] = "/api/self-service/plugin-actions";
            ViewData["PluginSlotMode"] = "details";
        }

        return View(outcome.Member);
    }

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken = default)
    {
        var outcome = await _selfServiceFeatureService.GetProfileAsync(User, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Challenge();
        }

        await PopulatePluginViewDataAsync(cancellationToken);

        return View(outcome.Profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.SelfServiceProfileEdit)]
    public async Task<IActionResult> Profile(SelfServiceProfileViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await PopulatePluginViewDataAsync(cancellationToken);
            return View(model);
        }

        var outcome = await _selfServiceFeatureService.UpdateProfileAsync(User, model, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Challenge();
        }

        if (!outcome.Succeeded)
        {
            foreach (var error in outcome.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await PopulatePluginViewDataAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Profil erfolgreich aktualisiert.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.SelfServiceProfileEdit)]
    public async Task<IActionResult> UploadProfileImage(IFormFile? profileImage, CancellationToken cancellationToken = default)
    {
        if (profileImage is null)
        {
            return BadRequest(new { success = false, message = "Keine Datei empfangen." });
        }

        await using var stream = profileImage.OpenReadStream();
        var outcome = await _selfServiceFeatureService.UploadProfileImageAsync(
            User,
            profileImage.FileName,
            profileImage.ContentType,
            stream,
            cancellationToken);

        if (outcome.RequiresChallenge)
        {
            return Unauthorized(new { success = false, message = "Nicht angemeldet." });
        }

        if (!outcome.Succeeded)
        {
            return BadRequest(new { success = false, message = outcome.ErrorMessage ?? "Upload fehlgeschlagen." });
        }

        return Json(new
        {
            success = true,
            message = "Profilbild erfolgreich hochgeladen.",
            imagePath = outcome.ImagePath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.SelfServiceProfileEdit)]
    public async Task<IActionResult> DeleteProfileImage(CancellationToken cancellationToken = default)
    {
        var outcome = await _selfServiceFeatureService.DeleteProfileImageAsync(User, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Unauthorized(new { success = false, message = "Nicht angemeldet." });
        }

        if (!outcome.Succeeded)
        {
            return BadRequest(new { success = false, message = outcome.ErrorMessage ?? "Loeschen fehlgeschlagen." });
        }

        return Json(new { success = true, message = "Profilbild erfolgreich geloescht." });
    }

    private async Task PopulatePluginViewDataAsync(CancellationToken cancellationToken)
    {
        var dashboard = await _selfServiceFeatureService.GetDashboardAsync(User, cancellationToken);
        ViewData["PluginActionEndpoint"] = "/api/self-service/plugin-actions";
        ViewData["PluginSlotMode"] = "edit-cards";
        ViewData["MemberPluginSlots"] = dashboard.Member is null
            ? MemberPluginSlotSnapshot.Empty
            : await _memberPluginSlotService.GetSlotsAsync(dashboard.Member, User, cancellationToken);
        ViewData["SelfServicePluginSections"] = dashboard.Member is null
            ? (IReadOnlyList<SelfServicePluginSectionView>)Array.Empty<SelfServicePluginSectionView>()
            : await _selfServiceSectionService.GetSelfServiceSectionsAsync(dashboard.Member, User, cancellationToken);
        ViewData["SelfServiceActionEndpoint"] = "/api/self-service/plugin-self-service-actions";
    }
}

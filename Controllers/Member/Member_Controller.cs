using ClubGear.Models;
using ClubGear.Models.Feedback;
using ClubGear.Models.MemberFilters;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ClubGear.Controllers;

[Authorize]
[PermissionAuthorize(PermissionKeys.MembersRead)]
public class MembersController : Controller
{
    private readonly IMemberFeatureService _memberFeatureService;
    private readonly IMemberPluginSlotService _memberPluginSlotService;
    private readonly IMembershipTypeService _membershipTypeService;

    public MembersController(IMemberFeatureService memberFeatureService)
        : this(memberFeatureService, NullMemberPluginSlotService.Instance, NullMembershipTypeService.Instance)
    {
    }

    public MembersController(IMemberFeatureService memberFeatureService, IMemberPluginSlotService memberPluginSlotService)
        : this(memberFeatureService, memberPluginSlotService, NullMembershipTypeService.Instance)
    {
    }

    [ActivatorUtilitiesConstructor]
    public MembersController(
        IMemberFeatureService memberFeatureService,
        IMemberPluginSlotService memberPluginSlotService,
        IMembershipTypeService membershipTypeService)
    {
        _memberFeatureService = memberFeatureService;
        _memberPluginSlotService = memberPluginSlotService;
        _membershipTypeService = membershipTypeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var searchFilter = new MemberSearchFilterViewModel
        {
            Search = NormalizeSearch(search),
            Status = MemberSearchFilterViewModel.NormalizeStatus(status)
        };

        // Load the full member set (no server-side search narrowing): narrowing could drop a
        // container parent whose only matching member is a sub-member (or vice versa), breaking
        // group cohesion. The normalized search/status are passed to the view for prefill only;
        // the group-aware client filter operates on the rendered rows.
        var members = await _memberFeatureService.GetListAsync(search: null, cancellationToken);
        var filteredMembers = ApplyStatusFilter(members, searchFilter.NormalizedStatus);
        var segments = _memberFeatureService.BuildListSegments(filteredMembers);
        var hierarchy = _memberFeatureService.BuildHierarchy(filteredMembers);

        ViewData["SearchFilter"] = searchFilter;
        ViewData["MemberListSegments"] = segments;
        ViewData["MemberHierarchy"] = hierarchy;
        await SetMembershipTypesViewDataAsync(cancellationToken);

        return View(filteredMembers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(MemberSearchFilterViewModel searchFilter)
    {
        var normalizedSearch = NormalizeSearch(searchFilter.Search);
        var normalizedStatus = searchFilter.NormalizedStatus;

        return RedirectToAction(
            nameof(Index),
            new
            {
                search = normalizedSearch,
                status = normalizedStatus
            });
    }

    [HttpGet]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        await SetMembershipTypesViewDataAsync(cancellationToken);
        return View(new Member());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Create(Member member, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await SetMembershipTypesViewDataAsync(cancellationToken);
            SetViewFeedback(ActionFeedbackViewModel.Error("Mitglied konnte nicht erstellt werden. Bitte Eingaben pruefen."));
            return View(member);
        }

        await _memberFeatureService.CreateAsync(member, User.Identity?.Name, cancellationToken);
        SetTempDataFeedback(ActionFeedbackViewModel.Success("Mitglied wurde erfolgreich erstellt."));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var member = await _memberFeatureService.GetByIdAsync(id, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        ViewData["MemberPluginSlots"] = await _memberPluginSlotService.GetSlotsAsync(member, User, cancellationToken);
        await SetMembershipTypesViewDataAsync(cancellationToken);

        return View(member);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Edit(int id, Member member, CancellationToken cancellationToken = default)
    {
        var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (id != member.Id)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(member.MemberNumber))
        {
            ModelState.AddModelError(nameof(Member.MemberNumber), "Mitgliedsnummer darf nicht leer sein.");
        }

        if (!ModelState.IsValid)
        {
            await SetMembershipTypesViewDataAsync(cancellationToken);

            if (isAjaxRequest)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                SetViewFeedback(ActionFeedbackViewModel.Error("Mitglied konnte nicht gespeichert werden. Bitte Eingaben pruefen."));
                return View(member);
            }

            SetViewFeedback(ActionFeedbackViewModel.Error("Mitglied konnte nicht gespeichert werden. Bitte Eingaben pruefen."));
            return View(member);
        }

        var status = await _memberFeatureService.UpdateAsync(member, User.Identity?.Name, cancellationToken);
        if (status == MemberMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (isAjaxRequest)
        {
            return Json(new { success = true, message = "Mitglied wurde erfolgreich aktualisiert." });
        }

        SetTempDataFeedback(ActionFeedbackViewModel.Success("Mitglied wurde erfolgreich aktualisiert."));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Verify(int id, CancellationToken cancellationToken = default)
    {
        var status = await _memberFeatureService.VerifyAsync(id, User.Identity?.Name, cancellationToken);
        if (status == MemberMutationStatus.NotFound)
        {
            return NotFound();
        }

        SetTempDataFeedback(ActionFeedbackViewModel.Success("Mitglied wurde verifiziert."));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var member = await _memberFeatureService.GetByIdAsync(id, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        ViewData["MemberPluginSlots"] = await _memberPluginSlotService.GetSlotsAsync(member, User, cancellationToken);

        return View(member);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var status = await _memberFeatureService.DeleteAsync(id, User.Identity?.Name, cancellationToken);
        if (status == MemberMutationStatus.NotFound)
        {
            return NotFound();
        }

        SetTempDataFeedback(ActionFeedbackViewModel.Success("Mitglied wurde geloescht."));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> Import(IFormFile? csvFile, CancellationToken cancellationToken = default)
    {
        if (csvFile is null || csvFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Bitte eine CSV-Datei auswaehlen.");
            SetViewFeedback(ActionFeedbackViewModel.Error("Import fehlgeschlagen: bitte eine gueltige CSV-Datei auswaehlen."));
            return View();
        }

        await using var stream = csvFile.OpenReadStream();
        var result = await _memberFeatureService.ImportCsvAsync(stream, User.Identity?.Name, cancellationToken);

        ViewData["ImportResult"] = result;
        if (result.Errors.Count == 0)
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Success($"Import abgeschlossen: {result.Created} erstellt, {result.Updated} aktualisiert, {result.Skipped} uebersprungen."));
            return RedirectToAction(nameof(Index));
        }

        SetViewFeedback(ActionFeedbackViewModel.Warning($"Import mit Hinweisen abgeschlossen: {result.Errors.Count} Problem(e) gefunden."));

        return View();
    }

    [HttpGet]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> BulkDeleteTerminatedMembers(CancellationToken cancellationToken = default)
    {
        var inactiveMembers = await _memberFeatureService.GetInactiveAsync(cancellationToken);
        return View(inactiveMembers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(PermissionKeys.MembersManage)]
    public async Task<IActionResult> BulkDeleteTerminatedMembers(BulkMemberActionRequest request, CancellationToken cancellationToken = default)
    {
        var hasManagePermission = HasMembersManagePermission(User);
        if (!hasManagePermission)
        {
            return Forbid();
        }

        var selectedMemberIds = request.GetValidSelectedMemberIds();
        if (selectedMemberIds.Count == 0)
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Error("Keine gueltigen Mitglieder zum Loeschen ausgewaehlt."));
            return RedirectToIndex(request.Search, request.Status);
        }

        var deleted = await _memberFeatureService.BulkDeleteAsync(
            selectedMemberIds,
            User.Identity?.Name,
            hasManagePermission,
            cancellationToken);
        if (deleted == 0)
        {
            SetTempDataFeedback(ActionFeedbackViewModel.Warning("Es wurden keine inaktiven Mitglieder geloescht."));
            return RedirectToIndex(request.Search, request.Status);
        }

        SetTempDataFeedback(ActionFeedbackViewModel.Success($"{deleted} inaktive Mitglieder wurden geloescht."));

        return RedirectToIndex(request.Search, request.Status);
    }

    private IActionResult RedirectToIndex(string? search, string? status)
    {
        return RedirectToAction(
            nameof(Index),
            new
            {
                search = NormalizeSearch(search),
                status = MemberSearchFilterViewModel.NormalizeStatus(status)
            });
    }

    private async Task SetMembershipTypesViewDataAsync(CancellationToken cancellationToken)
    {
        var membershipTypes = await _membershipTypeService.GetAllAsync(cancellationToken);
        ViewData["MembershipTypes"] = membershipTypes.Where(t => t.IsActive).ToList();
    }

    private void SetTempDataFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }

    private void SetViewFeedback(ActionFeedbackViewModel feedback)
    {
        ViewData[ActionFeedbackViewModel.ViewDataKey] = feedback;
    }

    private static bool HasMembersManagePermission(ClaimsPrincipal user)
    {
        return user.Claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(claim.Value, PermissionKeys.MembersManage, StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Value, PermissionKeys.Wildcard, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<Member> ApplyStatusFilter(IReadOnlyList<Member> members, string normalizedStatus)
    {
        return normalizedStatus switch
        {
            "active" => members.Where(m => m.IsActive).ToList(),
            "inactive" => members.Where(m => !m.IsActive).ToList(),
            _ => members.ToList()
        };
    }

    private static string? NormalizeSearch(string? search)
    {
        return string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }

    private sealed class NullMemberPluginSlotService : IMemberPluginSlotService
    {
        public static IMemberPluginSlotService Instance { get; } = new NullMemberPluginSlotService();

        public Task<MemberPluginSlotSnapshot> GetSlotsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberPluginSlotSnapshot.Empty);

        public Task<ClubGear.Plugin.Contracts.PluginMemberActionResult> ExecuteActionAsync(
            ClubGear.Models.MemberActions.PluginMemberActionRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ClubGear.Plugin.Contracts.PluginMemberActionResult(false, "not-supported", "Plugin-Mitgliedsaktionen sind nicht verfuegbar."));
    }

    private sealed class NullMembershipTypeService : IMembershipTypeService
    {
        public static IMembershipTypeService Instance { get; } = new NullMembershipTypeService();

        public Task<IReadOnlyList<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MembershipType>>(Array.Empty<MembershipType>());

        public Task<MembershipType?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<MembershipType?>(null);

        public Task<MembershipTypeOperationResult> CreateTypeAsync(MembershipType type, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeOperationResult.NotFoundResult());

        public Task<MembershipTypeOperationResult> UpdateTypeAsync(int id, MembershipType updated, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeOperationResult.NotFoundResult());

        public Task<MembershipTypeOperationResult> DeleteTypeAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeOperationResult.NotFoundResult());

        public Task<MembershipTypeFieldOperationResult> AddFieldAsync(int membershipTypeId, MembershipTypeField field, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeFieldOperationResult.NotFoundResult());

        public Task<MembershipTypeFieldOperationResult> UpdateFieldAsync(int fieldId, MembershipTypeField updated, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeFieldOperationResult.NotFoundResult());

        public Task<MembershipTypeFieldOperationResult> RemoveFieldAsync(int fieldId, CancellationToken cancellationToken = default)
            => Task.FromResult(MembershipTypeFieldOperationResult.NotFoundResult());
    }
}

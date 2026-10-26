using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Models.Feedback;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Admin;

[Route("Admin/MembershipTypes")]
[Authorize]
[PermissionAuthorize(PermissionKeys.MembersTypesManage)]
public sealed class MembershipTypesController : Controller
{
    private readonly IMembershipTypeService _membershipTypeService;

    public MembershipTypesController(IMembershipTypeService membershipTypeService)
    {
        _membershipTypeService = membershipTypeService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var types = await _membershipTypeService.GetAllAsync(cancellationToken);

        var model = new MembershipTypesViewModel
        {
            Types = types.ToList(),
            Feedback = ActionFeedbackViewModel.FromTempData(TempData)
        };

        return View("~/Views/Admin/MembershipTypes/Index.cshtml", model);
    }

    [HttpPost("CreateType")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateType(CreateMembershipTypeInputModel input, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.CreateTypeAsync(new MembershipType
        {
            Key = input.Key,
            Name = input.Name,
            Description = input.Description,
            DefaultDiscountPercent = input.DefaultDiscountPercent,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            AllowsSubMembers = input.AllowsSubMembers,
            SubMemberLabel = input.SubMemberLabel,
            FeeAmount = input.FeeAmount,
            BillingInterval = input.BillingInterval
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Mitgliedsart '{result.Type!.Name}' wurde angelegt.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht angelegt werden."));

        return RedirectBack(returnTo);
    }

    [HttpPost("UpdateType")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateType(UpdateMembershipTypeInputModel input, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.UpdateTypeAsync(input.Id, new MembershipType
        {
            Name = input.Name,
            Description = input.Description,
            DefaultDiscountPercent = input.DefaultDiscountPercent,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            AllowsSubMembers = input.AllowsSubMembers,
            SubMemberLabel = input.SubMemberLabel,
            FeeAmount = input.FeeAmount,
            BillingInterval = input.BillingInterval
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Mitgliedsart '{result.Type!.Name}' wurde aktualisiert.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht aktualisiert werden."));

        return RedirectBack(returnTo);
    }

    [HttpPost("DeleteType")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteType(int id, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.DeleteTypeAsync(id, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Warning($"Mitgliedsart '{result.Type!.Name}' wurde geloescht.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht geloescht werden."));

        return RedirectBack(returnTo);
    }

    [HttpPost("AddField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(CreateMembershipTypeFieldInputModel input, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.AddFieldAsync(input.MembershipTypeId, new MembershipTypeField
        {
            Key = input.Key,
            Label = input.Label,
            FieldType = input.FieldType,
            IsRequired = input.IsRequired,
            HelpText = input.HelpText,
            SortOrder = input.SortOrder
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Feld '{result.Field!.Label}' wurde angelegt.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Feld konnte nicht angelegt werden."));

        return RedirectBack(returnTo);
    }

    [HttpPost("UpdateField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateField(UpdateMembershipTypeFieldInputModel input, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.UpdateFieldAsync(input.Id, new MembershipTypeField
        {
            Key = input.Key,
            Label = input.Label,
            FieldType = input.FieldType,
            IsRequired = input.IsRequired,
            HelpText = input.HelpText,
            SortOrder = input.SortOrder
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Feld '{result.Field!.Label}' wurde aktualisiert.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Feld konnte nicht aktualisiert werden."));

        return RedirectBack(returnTo);
    }

    [HttpPost("RemoveField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveField(int id, string? returnTo = null, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.RemoveFieldAsync(id, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Warning($"Feld '{result.Field!.Label}' wurde entfernt.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Feld konnte nicht entfernt werden."));

        return RedirectBack(returnTo);
    }

    // Returns to the host page the action was invoked from (e.g. the Admin Functions tab) when a
    // safe local returnTo is supplied, otherwise falls back to the standalone management page.
    private IActionResult RedirectBack(string? returnTo)
        => Url?.IsLocalUrl(returnTo) == true ? Redirect(returnTo!) : RedirectToAction(nameof(Index));

    private void SetFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }
}

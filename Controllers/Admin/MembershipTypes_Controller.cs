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
    public async Task<IActionResult> CreateType(CreateMembershipTypeInputModel input, CancellationToken cancellationToken = default)
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
            SubMemberLabel = input.SubMemberLabel
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Mitgliedsart '{result.Type!.Name}' wurde angelegt.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht angelegt werden."));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdateType")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateType(UpdateMembershipTypeInputModel input, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.UpdateTypeAsync(input.Id, new MembershipType
        {
            Name = input.Name,
            Description = input.Description,
            DefaultDiscountPercent = input.DefaultDiscountPercent,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            AllowsSubMembers = input.AllowsSubMembers,
            SubMemberLabel = input.SubMemberLabel
        }, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Success($"Mitgliedsart '{result.Type!.Name}' wurde aktualisiert.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht aktualisiert werden."));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("DeleteType")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteType(int id, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.DeleteTypeAsync(id, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Warning($"Mitgliedsart '{result.Type!.Name}' wurde geloescht.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Mitgliedsart konnte nicht geloescht werden."));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(CreateMembershipTypeFieldInputModel input, CancellationToken cancellationToken = default)
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdateField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateField(UpdateMembershipTypeFieldInputModel input, CancellationToken cancellationToken = default)
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RemoveField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveField(int id, CancellationToken cancellationToken = default)
    {
        var result = await _membershipTypeService.RemoveFieldAsync(id, cancellationToken);

        SetFeedback(result.Success
            ? ActionFeedbackViewModel.Warning($"Feld '{result.Field!.Label}' wurde entfernt.")
            : ActionFeedbackViewModel.Error(result.ErrorMessage ?? "Feld konnte nicht entfernt werden."));

        return RedirectToAction(nameof(Index));
    }

    private void SetFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }
}

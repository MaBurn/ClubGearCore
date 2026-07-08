using ClubGear.Models;
using ClubGear.Models.MemberActions;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/self-service")]
[Authorize]
[PermissionAuthorize(PermissionKeys.SelfServiceAccess)]
public class SelfServiceApiController : ControllerBase
{
    private readonly ISelfServiceFeatureService _selfServiceFeatureService;
    private readonly IMemberPluginSlotService _memberPluginSlotService;
    private readonly ISelfServiceSectionService _selfServiceSectionService;

    public SelfServiceApiController(
        ISelfServiceFeatureService selfServiceFeatureService,
        IMemberPluginSlotService memberPluginSlotService,
        ISelfServiceSectionService selfServiceSectionService)
    {
        _selfServiceFeatureService = selfServiceFeatureService;
        _memberPluginSlotService = memberPluginSlotService;
        _selfServiceSectionService = selfServiceSectionService;
    }

    [HttpGet("plugin-slots")]
    public async Task<IActionResult> GetPluginSlots(CancellationToken cancellationToken = default)
    {
        var dashboard = await _selfServiceFeatureService.GetDashboardAsync(User, cancellationToken);
        if (dashboard.RequiresChallenge)
        {
            return Unauthorized();
        }

        var member = dashboard.Member;
        if (member is null)
        {
            return NotFound(new { status = "member-not-linked", message = "Es ist kein Mitglied mit dem Self-Service-Konto verknuepft." });
        }

        var slots = await _memberPluginSlotService.GetSlotsAsync(member, User, cancellationToken);
        return Ok(slots);
    }

    [HttpPost("plugin-actions")]
    public async Task<IActionResult> ExecutePluginAction(
        [FromBody] SelfServicePluginActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await _selfServiceFeatureService.GetDashboardAsync(User, cancellationToken);
        if (dashboard.RequiresChallenge)
        {
            return Unauthorized();
        }

        var member = dashboard.Member;
        if (member is null)
        {
            return NotFound(new PluginMemberActionResult(false, "member-not-linked", "Es ist kein Mitglied mit dem Self-Service-Konto verknuepft."));
        }

        var result = await _memberPluginSlotService.ExecuteActionAsync(
            new MemberPluginActionRequestModel(request.ModuleId, request.ActionKey, member.Id, request.Arguments),
            User,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Status switch
        {
            "invalid" => BadRequest(result),
            "plugin-not-active" => NotFound(result),
            "member-not-found" => NotFound(result),
            "member-not-linked" => NotFound(result),
            "action-not-found" => NotFound(result),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken = default)
    {
        var outcome = await _selfServiceFeatureService.GetProfileAsync(User, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Unauthorized();
        }

        return Ok(outcome.Profile);
    }

    [HttpPut("profile")]
    [PermissionAuthorize(PermissionKeys.SelfServiceProfileEdit)]
    public async Task<IActionResult> UpdateProfile([FromBody] SelfServiceProfileViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var outcome = await _selfServiceFeatureService.UpdateProfileAsync(User, model, cancellationToken);
        if (outcome.RequiresChallenge)
        {
            return Unauthorized();
        }

        if (!outcome.Succeeded)
        {
            return BadRequest(new { success = false, errors = outcome.Errors });
        }

        return Ok(new { success = true });
    }

    [HttpPost("plugin-self-service-actions")]
    public async Task<IActionResult> ExecutePluginSelfServiceAction(
        [FromBody] SelfServiceSectionActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await _selfServiceFeatureService.GetDashboardAsync(User, cancellationToken);
        if (dashboard.RequiresChallenge)
            return Unauthorized();
        if (dashboard.Member is null)
            return NotFound();

        var result = await _selfServiceSectionService.ExecuteSelfServiceActionAsync(
            request, dashboard.Member, User, cancellationToken);

        return result.Status switch
        {
            "action-not-found" or "plugin-not-active" => NotFound(new { result.Message }),
            _ when result.Success => Ok(new { result.Message }),
            _ => BadRequest(new { result.Message, result.Status, FieldErrors = result.FieldErrors ?? Array.Empty<PluginFieldError>() })
        };
    }
}

public sealed record SelfServicePluginActionRequest(
    string ModuleId,
    string ActionKey,
    IReadOnlyDictionary<string, string>? Arguments = null);

using System.Security.Claims;
using ClubGear.Models.MemberActions;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Models.MemberFilters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/member")]
[Authorize]
[PermissionAuthorize(PermissionKeys.MembersRead)]
public class MemberApiController : ControllerBase
{
    private readonly IMemberFeatureService _memberFeatureService;
    private readonly IMemberPluginSlotService _memberPluginSlotService;

    public MemberApiController(IMemberFeatureService memberFeatureService)
        : this(memberFeatureService, NullMemberPluginSlotService.Instance)
    {
    }

    [ActivatorUtilitiesConstructor]
    public MemberApiController(IMemberFeatureService memberFeatureService, IMemberPluginSlotService memberPluginSlotService)
    {
        _memberFeatureService = memberFeatureService;
        _memberPluginSlotService = memberPluginSlotService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var members = await _memberFeatureService.GetListAsync(search, cancellationToken);
        var normalizedStatus = MemberSearchFilterViewModel.NormalizeStatus(status);
        var filteredMembers = normalizedStatus switch
        {
            "active" => members.Where(m => m.IsActive).ToList(),
            "inactive" => members.Where(m => !m.IsActive).ToList(),
            _ => members
        };

        return Ok(filteredMembers);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var member = await _memberFeatureService.GetByIdAsync(id, cancellationToken);
        return member is null ? NotFound() : Ok(member);
    }

    [HttpGet("reference-search")]
    public async Task<IActionResult> SearchForReference([FromQuery] string? q, CancellationToken cancellationToken = default)
    {
        var options = await _memberFeatureService.SearchForReferenceAsync(q, limit: 10, cancellationToken);
        return Ok(options);
    }

    [HttpPost("plugin-actions")]
    public async Task<IActionResult> ExecutePluginAction(
        [FromBody] PluginMemberActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _memberPluginSlotService.ExecuteActionAsync(request, User, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.Status switch
        {
            "invalid" => BadRequest(result),
            "plugin-not-active" => NotFound(result),
            "member-not-found" => NotFound(result),
            "action-not-found" => NotFound(result),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    private sealed class NullMemberPluginSlotService : IMemberPluginSlotService
    {
        public static IMemberPluginSlotService Instance { get; } = new NullMemberPluginSlotService();

        public Task<MemberPluginSlotSnapshot> GetSlotsAsync(Models.Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberPluginSlotSnapshot.Empty);

        public Task<Plugin.Contracts.PluginMemberActionResult> ExecuteActionAsync(
            PluginMemberActionRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new Plugin.Contracts.PluginMemberActionResult(false, "not-supported", "Plugin-Mitgliedsaktionen sind nicht verfuegbar."));
    }
}

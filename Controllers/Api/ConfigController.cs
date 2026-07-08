using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/config")]
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class ConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public ConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(string name, [FromQuery] string? section = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name ist erforderlich." });
        }

        var entries = await _systemConfigService.GetAllAsync(cancellationToken);
        var match = entries.FirstOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(section) || string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase)));

        if (match is null)
        {
            return Ok(new { name, section = section ?? string.Empty, value = string.Empty, description = string.Empty });
        }

        return Ok(new
        {
            name = match.Name,
            section = match.Section,
            value = match.Value,
            description = match.Description
        });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] ConfigUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Name ist erforderlich." });
        }

        await _systemConfigService.UpsertAsync(
            request.Section ?? string.Empty,
            request.Name,
            request.Value ?? string.Empty,
            request.Description ?? string.Empty,
            cancellationToken);

        return Ok(new { success = true, message = "Gespeichert." });
    }

    public sealed class ConfigUpsertRequest
    {
        public string Name { get; init; } = string.Empty;
        public string? Section { get; init; }
        public string? Value { get; init; }
        public string? Description { get; init; }
    }
}

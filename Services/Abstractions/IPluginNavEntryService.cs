using System.Security.Claims;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginNavEntryService
{
    Task<IReadOnlyList<PluginNavEntry>> GetVisibleNavEntriesAsync(ClaimsPrincipal user, CancellationToken ct);
}

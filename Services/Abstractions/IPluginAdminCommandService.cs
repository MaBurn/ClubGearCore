using System.Security.Claims;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginAdminCommandService
{
    Task<IReadOnlyList<PluginAdminModulePanels>> GetPanelsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<PluginCommandResult> ExecuteCommandAsync(
        string moduleId,
        PluginAdminCommandRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
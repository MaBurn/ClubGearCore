using ClubGear.Models.PluginAdmin;

namespace ClubGear.Services.Abstractions;

public interface IPluginAdminQueryService
{
    IReadOnlyList<PluginAdminStatusViewModel> GetPluginStatuses();

    PluginAdminStatusViewModel? GetPluginStatus(string moduleId);
}
using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

public interface ISystemConfigService
{
    Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken cancellationToken = default);
    Task<string?> GetValueAsync(string section, string name, CancellationToken cancellationToken = default);
    Task UpsertAsync(string section, string name, string value, string description, CancellationToken cancellationToken = default);
    Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default);
}

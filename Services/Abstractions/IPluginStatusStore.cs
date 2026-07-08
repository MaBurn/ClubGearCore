using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

public interface IPluginStatusStore
{
    PluginStatusRecord? GetByKey(string key);

    IReadOnlyList<PluginStatusRecord> List();

    Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
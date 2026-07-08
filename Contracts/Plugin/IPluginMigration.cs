namespace ClubGear.Plugin.Contracts;

public interface IPluginDataStore
{
    string ModuleId { get; }

    string TablePrefix { get; }

    string GetTableName(string localName);

    Task ExecuteAsync(
        string sql,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginDataRow>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default);
}

public interface IPluginMigration
{
    string MigrationId { get; }

    Task ApplyAsync(IPluginMigrationContext context, CancellationToken cancellationToken = default);
}

public interface IPluginMigrationContext : IPluginDataStore
{
}

public sealed record PluginDataRow(IReadOnlyDictionary<string, string?> Values);
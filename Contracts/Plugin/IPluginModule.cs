namespace ClubGear.Plugin.Contracts;

public interface IPluginModule
{
    PluginManifest Manifest { get; }

    void RegisterContributions(IPluginContributionSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
    }

    IReadOnlyList<IPluginMigration> GetMigrations()
    {
        return Array.Empty<IPluginMigration>();
    }
}
using ClubGear.Plugin.Contracts;

// CORE-16. Deliberately violating fixture: the module entry point itself lives in a core
// namespace, so the load-time isolation scan must refuse it *before* it is constructed and
// before RegisterContributions runs. It stays in its own file so no allowed fixture is
// accidentally tainted by it.
namespace ClubGear.Services.ForbiddenPlugin;

public sealed class ForbiddenEntryPointPluginModule : IPluginModule
{
    /// <summary>
    /// Marker the module writes from <see cref="RegisterContributions"/>. The module is loaded
    /// into its own AssemblyLoadContext, so a static field would not be observable from the test;
    /// a file is. If this file exists after a load attempt, plugin code ran that must not have run.
    /// </summary>
    public static readonly string RegisterContributionsMarkerPath =
        Path.Combine(Path.GetTempPath(), "clubgear-forbidden-entrypoint-registered.marker");

    public ForbiddenEntryPointPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.forbidden.entrypoint",
            "Forbidden Entry Point Plugin",
            new Version(1, 0, 0),
            "Tests",
            "MIT",
            typeof(ForbiddenEntryPointPluginModule).FullName!,
            ">=1.13.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        File.WriteAllText(RegisterContributionsMarkerPath, "registered");
    }
}

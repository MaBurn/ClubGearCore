using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.ArchitectureTests.ForbiddenPlugin;

public sealed class ForbiddenDbContextCapability
{
    private readonly ApplicationDbContext _dbContext;

    public ForbiddenDbContextCapability(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// CORE-16. The same violation, but reachable only through the constructor signature: the core
/// type is never stored in a field, so the field-only scan the host used before this iteration
/// would have waved it through. Since contributions are host-constructed, that parameter is
/// exactly the surface an attacker would use.
/// </summary>
public sealed class ForbiddenDbContextConstructorCapability
{
    // CORE-18. The canonical constructor is present, so the type passes the constructor-shape
    // gate and the load-time rejection is attributable to the isolation scan specifically —
    // which is the point of this fixture.
    public ForbiddenDbContextConstructorCapability(IPluginHostContext hostContext)
    {
        ArgumentNullException.ThrowIfNull(hostContext);
    }

    public ForbiddenDbContextConstructorCapability(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
    }

    public Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// A module whose own entry-point type is clean, but which declares
/// <see cref="ForbiddenDbContextConstructorCapability"/> as a contribution. The load-time gate has
/// to reject it on the declared contribution type, not on the entry point.
/// </summary>
public sealed class ForbiddenContributionPluginModule : IPluginModule
{
    public ForbiddenContributionPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.forbidden.contribution",
            "Forbidden Contribution Plugin",
            new Version(1, 0, 0),
            "Tests",
            "MIT",
            typeof(ForbiddenContributionPluginModule).FullName!,
            ">=1.13.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddMemberProvider(new PluginMemberProviderContribution(
            PluginMemberSlotKind.DetailCard,
            typeof(ForbiddenDbContextConstructorCapability).FullName!));
    }
}

/// <summary>
/// A module that declares a contribution type which does not exist in its own assembly. Before
/// CORE-16 this was collected and silently never used; now it is a load-time rejection.
/// </summary>
public sealed class MissingContributionTypePluginModule : IPluginModule
{
    public MissingContributionTypePluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.missing.contribution",
            "Missing Contribution Type Plugin",
            new Version(1, 0, 0),
            "Tests",
            "MIT",
            typeof(MissingContributionTypePluginModule).FullName!,
            ">=1.13.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddMemberProvider(new PluginMemberProviderContribution(
            PluginMemberSlotKind.DetailCard,
            "ClubGear.ArchitectureTests.ForbiddenPlugin.TypeThatDoesNotExist"));
    }
}
/// <summary>
/// CORE-18. A contribution type with only a parameterless constructor — the shape that 1.13.0
/// removed. The loader has to name the module and the type, so an administrator can tell this
/// apart from a version mismatch.
/// </summary>
public sealed class ParameterlessContributionCapability
{
    public Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class ParameterlessContributionPluginModule : IPluginModule
{
    public ParameterlessContributionPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.parameterless.contribution",
            "Parameterless Contribution Plugin",
            new Version(1, 0, 0),
            "Tests",
            "MIT",
            typeof(ParameterlessContributionPluginModule).FullName!,
            ">=1.13.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddMemberProvider(new PluginMemberProviderContribution(
            PluginMemberSlotKind.DetailCard,
            typeof(ParameterlessContributionCapability).FullName!));
    }
}

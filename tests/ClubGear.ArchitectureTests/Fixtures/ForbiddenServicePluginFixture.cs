using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Services.ForbiddenPlugin;

public sealed class ForbiddenDirectCoreAccessCapability
{
    public Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

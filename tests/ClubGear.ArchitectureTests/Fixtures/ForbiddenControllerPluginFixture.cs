using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Controllers.ForbiddenPlugin;

public sealed class ForbiddenDirectCoreEndpoint
{
    public Task<PluginEndpointResult> HandleAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.FromResult(new PluginEndpointResult(200, "should-not-register"));
}

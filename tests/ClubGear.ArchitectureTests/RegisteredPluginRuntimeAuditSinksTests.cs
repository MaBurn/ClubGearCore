using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class RegisteredPluginRuntimeAuditSinksTests
{
    [Fact]
    public void AuditSinks_IsNotNull_WhenConstructedWithEmptyList()
    {
        var runtime = new RegisteredPluginRuntime(
            "test.plugin",
            "Test Plugin",
            new Version(1, 0, 0),
            "test:context",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

        Assert.NotNull(runtime.AuditSinks);
        Assert.Empty(runtime.AuditSinks);
    }
}

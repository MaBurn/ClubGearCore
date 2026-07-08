using ClubGear.Plugin.Contracts;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class Slice1IdentityProviderContractTests
{
    [Fact]
    public void ContractVersion_Current_Is_1_7_0()
    {
        Assert.Equal(new Version(1, 7, 0), ContractVersion.Current);
    }

    [Fact]
    public void IIdentityProviderPlugin_Interface_Exists_In_ContractsAssembly()
    {
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IIdentityProviderPlugin).Assembly);
    }

    [Fact]
    public void PluginExternalLoginContracts_Types_Exist_In_ContractsAssembly()
    {
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginClaimEntry).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginExternalLoginContext).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginExternalLoginTestResult).Assembly);
    }

    [Fact]
    public void PluginIdentityProviderContribution_Exists_In_ContractsAssembly()
    {
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginIdentityProviderContribution).Assembly);
    }

    [Fact]
    public void PluginExtensionPoints_Contains_IdentityProvider()
    {
        Assert.Equal("identity.provider", PluginExtensionPoints.IdentityProvider);
        Assert.True(PluginExtensionPoints.IsKnown("identity.provider"));
    }

    [Fact]
    public void PluginSchemaFieldType_Contains_Secret()
    {
        Assert.True(Enum.IsDefined(typeof(PluginSchemaFieldType), PluginSchemaFieldType.Secret));
    }

    [Fact]
    public void IPluginContributionSink_AddIdentityProvider_DefaultNoOp_DoesNotThrow()
    {
        var sink = new NoOpContributionSink();
        var contribution = new PluginIdentityProviderContribution("test.provider", "TestProviderType");

        // default no-op should not throw
        var exception = Record.Exception(() => ((IPluginContributionSink)sink).AddIdentityProvider(contribution));

        Assert.Null(exception);
    }

    private sealed class NoOpContributionSink : IPluginContributionSink
    {
        public void AddRoute(PluginRouteContribution contribution) { }
        public void AddService(PluginServiceContribution contribution) { }
        public void AddMemberProvider(PluginMemberProviderContribution contribution) { }
        public void AddBackgroundJob(PluginBackgroundJobContribution contribution) { }
    }
}

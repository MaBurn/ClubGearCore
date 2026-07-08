using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class ContractCompatibilityServiceTests
{
    private readonly ContractCompatibilityService _sut = new();

    [Fact]
    public void Validate_ReturnsCompatible_ForCurrentVersion()
    {
        var result = _sut.Validate(ContractVersion.Current);

        Assert.True(result.IsCompatible);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_ReturnsIncompatible_ForMajorMismatch()
    {
        var pluginVersion = new Version(ContractVersion.Current.Major + 1, 0, 0);

        var result = _sut.Validate(pluginVersion);

        Assert.False(result.IsCompatible);
        Assert.Contains("Incompatible major version", result.Reason);
    }

    [Fact]
    public void Validate_ReturnsIncompatible_ForBelowMinimumSupported()
    {
        var pluginVersion = new Version(
            ContractVersion.MinimumSupported.Major,
            ContractVersion.MinimumSupported.Minor);

        var result = _sut.Validate(pluginVersion);

        Assert.False(result.IsCompatible);
        Assert.Contains("below minimum supported version", result.Reason);
    }
}
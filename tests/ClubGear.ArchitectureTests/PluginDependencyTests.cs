using ClubGear.Plugin.Contracts;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginDependencyTests
{
    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndCorrectValues()
    {
        var ok = PluginDependency.TryParse("clubgear.plugin.carinfo@>=1.0.5", out var dep);

        Assert.True(ok);
        Assert.NotNull(dep);
        Assert.Equal("clubgear.plugin.carinfo", dep!.ModuleId);
        Assert.Equal(new Version(1, 0, 5), dep.MinVersion);
    }

    [Fact]
    public void TryParse_ValidInput_ToStringRoundTrips()
    {
        PluginDependency.TryParse("clubgear.plugin.carinfo@>=1.0.5", out var dep);

        Assert.Equal("clubgear.plugin.carinfo@>=1.0.5", dep!.ToString());
    }

    [Fact]
    public void TryParse_MissingAt_ReturnsFalse()
    {
        var ok = PluginDependency.TryParse("clubgear.plugin.carinfo", out var dep);

        Assert.False(ok);
        Assert.Null(dep);
    }

    [Fact]
    public void TryParse_BadVersionNumber_ReturnsFalse()
    {
        var ok = PluginDependency.TryParse("clubgear.plugin.carinfo@>=abc", out var dep);

        Assert.False(ok);
        Assert.Null(dep);
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(PluginDependency.TryParse(null!, out _));
        Assert.False(PluginDependency.TryParse("", out _));
        Assert.False(PluginDependency.TryParse("   ", out _));
    }

    [Fact]
    public void TryParse_VersionWithoutGePrefix_StillParses()
    {
        var ok = PluginDependency.TryParse("clubgear.plugin.carinfo@1.0.5", out var dep);

        Assert.True(ok);
        Assert.NotNull(dep);
        Assert.Equal(new Version(1, 0, 5), dep!.MinVersion);
    }
}

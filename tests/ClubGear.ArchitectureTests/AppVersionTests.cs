using ClubGear.Plugin.Contracts;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

public class AppVersionTests
{
    [Fact]
    public void Current_Follows_CalVer_Year_Month_Counter()
    {
        var parts = AppVersion.Current.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.InRange(int.Parse(parts[0]), 2024, 2100);
        Assert.InRange(int.Parse(parts[1]), 1, 12);
        Assert.True(int.Parse(parts[2]) >= 0);
    }

    [Fact]
    public void Current_Has_No_Build_Metadata_Suffix()
    {
        Assert.DoesNotContain('+', AppVersion.Current);
    }

    [Fact]
    public void Core_Version_Does_Not_Leak_Into_Plugin_Contracts()
    {
        // Contracts/Plugin follows SemVer and must stay independent of the core's CalVer.
        // A root Directory.Build.props would silently stamp the core version onto it.
        var contractsVersion = typeof(ContractVersion).Assembly.GetName().Version;

        Assert.NotNull(contractsVersion);
        Assert.NotEqual(2026, contractsVersion!.Major);
    }

    [Theory]
    [InlineData("2026.7.0+abc1234", "2026.7.0")]
    [InlineData("2026.7.3", "2026.7.3")]
    public void Normalize_Strips_Build_Metadata(string informational, string expected)
    {
        Assert.Equal(expected, AppVersion.Normalize(informational, assemblyVersion: null));
    }

    [Fact]
    public void Normalize_Falls_Back_To_Assembly_Version()
    {
        Assert.Equal("2026.7.1", AppVersion.Normalize(null, new Version(2026, 7, 1, 0)));
    }

    [Fact]
    public void Normalize_Reports_Unknown_When_Nothing_Is_Available()
    {
        Assert.Equal("unbekannt", AppVersion.Normalize(null, null));
    }
}

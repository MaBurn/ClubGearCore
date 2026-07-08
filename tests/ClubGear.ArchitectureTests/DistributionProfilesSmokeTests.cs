using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class DistributionProfilesSmokeTests
{
    [Theory]
    [InlineData("local", true)]
    [InlineData("staging", true)]
    [InlineData("prod", false)]
    public void EnvironmentProfiles_ExposeExpectedPluginToggles(string profile, bool expectedPluginsEnabled)
    {
        var root = ResolveRepoRoot();
        var config = new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var value = config[$"EnvironmentProfiles:{NormalizeProfile(profile)}:PluginsEnabled"];

        Assert.Equal(expectedPluginsEnabled.ToString(), value, ignoreCase: true);
    }

    [Fact]
    public void DockerCompose_DefinesLocalStagingProdProfiles()
    {
        var composePath = Path.Combine(ResolveRepoRoot(), "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        Assert.Contains("profiles: [\"local\"]", content, StringComparison.Ordinal);
        Assert.Contains("profiles: [\"staging\"]", content, StringComparison.Ordinal);
        Assert.Contains("profiles: [\"prod\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Dockerfile_DefinesWebRuntimeEntrypoint()
    {
        var dockerfilePath = Path.Combine(ResolveRepoRoot(), "Dockerfile");
        var content = File.ReadAllText(dockerfilePath);

        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:8.0", content, StringComparison.Ordinal);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"ClubGear.dll\"]", content, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var projectFile = Path.Combine(directory.FullName, "ClubGear.csproj");
            if (File.Exists(projectFile))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root from test execution path.");
    }

    private static string NormalizeProfile(string profile)
        => profile switch
        {
            "local" => "Local",
            "staging" => "Staging",
            "prod" => "Prod",
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unbekanntes Profil")
        };
}

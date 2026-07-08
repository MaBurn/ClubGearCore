using System.Reflection;
using ClubGear.Plugin.Contracts;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginBoundaryTests
{
    [Fact]
    public void ContractsAssembly_ShouldNotReference_CoreAssembly()
    {
        var referencedAssemblies = typeof(IPluginModule)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.DoesNotContain("ClubGear", referencedAssemblies, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractsAssembly_ShouldExpose_HostContext_AndMemberReadModels()
    {
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginHostContext).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginMemberSummary).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginMemberDetail).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberDetailCardProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberEditTabProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberStatusBadgeProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberActionProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginMigration).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginDataStore).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginMigrationContext).Assembly);

        var propertyTypes = typeof(IPluginHostContext)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.Contains(typeof(IPluginMetadataFacade), propertyTypes);
        Assert.Contains(typeof(IPluginMemberReader), propertyTypes);
        Assert.Contains(typeof(IPluginMemberActionFacade), propertyTypes);
        Assert.Contains(typeof(IPluginDataStore), propertyTypes);
    }

    [Fact]
    public void ContractsProject_ShouldNotReference_CoreProject()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var contractsProject = Path.Combine(repositoryRoot, "Contracts", "Plugin", "ClubGear.Plugin.Contracts.csproj");
        var projectContent = File.ReadAllText(contractsProject);

        Assert.DoesNotContain("ClubGear.csproj", projectContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractsSource_ShouldNotUse_ForbiddenCoreNamespaces()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var contractsFolder = Path.Combine(repositoryRoot, "Contracts", "Plugin");
        var sourceFiles = Directory
            .EnumerateFiles(contractsFolder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        var forbiddenNamespaces = new[]
        {
            "ClubGear.Services",
            "ClubGear.Data",
            "ClubGear.Controllers",
            "ClubGear.Models"
        };

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);

            foreach (var forbiddenNamespace in forbiddenNamespaces)
            {
                Assert.DoesNotContain(forbiddenNamespace, content, StringComparison.Ordinal);
            }
        }
    }

    private static string ResolveRepositoryRoot()
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
}
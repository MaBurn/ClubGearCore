using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class LayoutConsistencySmokeTests
{
    [Fact]
    public void SharedLayout_DefinesModuleWideShellConventions()
    {
        var layoutContent = File.ReadAllText(GetProjectFilePath("Views", "Shared", "_Layout.cshtml"));
        var layoutCssContent = File.ReadAllText(GetProjectFilePath("Views", "Shared", "_Layout.cshtml.css"));
        var siteCssContent = File.ReadAllText(GetProjectFilePath("wwwroot", "css", "site.css"));

        Assert.Contains("cg-main-shell", layoutContent, StringComparison.Ordinal);
        Assert.Contains("--cg-page-gap", siteCssContent, StringComparison.Ordinal);
        Assert.Contains(".cg-page", layoutCssContent, StringComparison.Ordinal);
        Assert.Contains(".cg-action-bar", layoutCssContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Views/Members/Index.cshtml")]
    [InlineData("Views/PluginAdmin/Index.cshtml")]
    [InlineData("Views/SelfService/Index.cshtml")]
    [InlineData("Views/SelfService/Profile.cshtml")]
    [InlineData("Views/Account/Login.cshtml")]
    [InlineData("Views/Account/Register.cshtml")]
    [InlineData("Views/Account/AccessDenied.cshtml")]
    public void TargetViews_AdoptSharedPageConventions(string relativePath)
    {
        var content = File.ReadAllText(GetProjectFilePath(relativePath.Split('/')));

        Assert.Contains("cg-page", content, StringComparison.Ordinal);
        Assert.Contains("cg-page-section", content, StringComparison.Ordinal);
    }

    private static string GetProjectFilePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var csprojPath = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(csprojPath))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }
}
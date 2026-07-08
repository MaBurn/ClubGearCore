using Xunit;

namespace ClubGear.ArchitectureTests.Layout;

public sealed class LayoutNavigationTests
{
    [Fact]
    public void SharedLayout_ContainsBerechtigungenLink_WithRolePermissionsHref()
    {
        var layoutPath = GetProjectFilePath("Views", "Shared", "_Layout.cshtml");
        var layoutContent = File.ReadAllText(layoutPath);

        // The link text must contain "Berechtigungen"
        Assert.Contains("Berechtigungen", layoutContent, StringComparison.Ordinal);

        // The href must point to /Admin/RolePermissions via tag-helper convention
        // asp-controller="RolePermissions" asp-action="Index" resolves to /Admin/RolePermissions
        // because the controller has [Route("Admin/RolePermissions")]
        // We verify both the controller attribute and action attribute are present together
        Assert.Contains("asp-controller=\"RolePermissions\"", layoutContent, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Index\"", layoutContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedLayout_BerechtigungenLink_IsInsideAdminDropdown()
    {
        var layoutPath = GetProjectFilePath("Views", "Shared", "_Layout.cshtml");
        var layoutContent = File.ReadAllText(layoutPath);

        // The "Berechtigungen" link must appear within the admin dropdown block
        // (i.e. after "cg-admin-menu" and before its closing ul tag)
        var adminMenuStart = layoutContent.IndexOf("cg-admin-menu", StringComparison.Ordinal);
        Assert.True(adminMenuStart >= 0, "Admin dropdown menu not found in layout.");

        var berechtigungenIndex = layoutContent.IndexOf("Berechtigungen", StringComparison.Ordinal);
        Assert.True(berechtigungenIndex > adminMenuStart,
            "\"Berechtigungen\" link must appear inside the admin dropdown menu.");
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

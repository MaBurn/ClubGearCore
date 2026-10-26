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

    [Fact]
    public void PluginPageActionModal_ResolvesBootstrapInstanceAtClickTime()
    {
        var modalContent = File.ReadAllText(GetProjectFilePath("Views", "PluginPage", "_PluginPageActionModal.cshtml"));

        Assert.Contains("const getModalInstance = () =>", modalContent, StringComparison.Ordinal);
        Assert.Contains("getModalInstance()?.show()", modalContent, StringComparison.Ordinal);
        Assert.DoesNotContain("const modalInstance =", modalContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedLayout_RendersGroupedPluginNavigationAsDropdown()
    {
        var layoutContent = File.ReadAllText(GetProjectFilePath("Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("entry.GroupKey", layoutContent, StringComparison.Ordinal);
        Assert.Contains("firstEntry.GroupLabel", layoutContent, StringComparison.Ordinal);
        Assert.Contains("dropdown-menu", layoutContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginPageIndex_RendersIconColumnAsBootstrapIcon()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "PluginPage", "Index.cshtml"));

        Assert.Contains("col.IsIconColumn", content, StringComparison.Ordinal);
        Assert.Contains("<i class=\"bi @cellVal\" aria-hidden=\"true\"></i>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginPageDetail_RendersIconColumnAsBootstrapIcon()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "PluginPage", "Detail.cshtml"));

        Assert.Contains("col.IsIconColumn", content, StringComparison.Ordinal);
        Assert.Contains("<i class=\"bi @value\" aria-hidden=\"true\"></i>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfServiceProfile_EditFormClosesBeforePluginContent_AndSaveButtonReferencesItByFormAttribute()
    {
        // Regression guard: editProfileForm must not structurally contain the EditTabs partial
        // or the self-service sections loop, because both can render plugin-supplied raw HTML
        // that may itself contain a <form> (e.g. Finance's "finance.account.selfservice.replace"
        // form). A <form> nested inside another <form> is invalid HTML — the browser's parser
        // closes the outer form at the inner form's closing tag, silently detaching every field
        // and the save button that follows from any form at all. Reported 2026-10-08: clicking
        // "Profil speichern" did nothing.
        var content = File.ReadAllText(GetProjectFilePath("Views", "SelfService", "Profile.cshtml"));

        var formOpenIndex = content.IndexOf("id=\"editProfileForm\"", StringComparison.Ordinal);
        var formCloseIndex = content.IndexOf("</form>", formOpenIndex, StringComparison.Ordinal);
        var pluginSlotsIndex = content.IndexOf("_PluginSlots.cshtml", StringComparison.Ordinal);
        var selfServiceLoopIndex = content.IndexOf("selfServiceSections.OrderBy", StringComparison.Ordinal);

        Assert.True(formOpenIndex >= 0, "editProfileForm not found.");
        Assert.True(formCloseIndex >= 0, "editProfileForm has no closing </form>.");
        Assert.True(pluginSlotsIndex > formCloseIndex,
            "_PluginSlots partial (plugin EditTab raw HTML) must render after editProfileForm closes.");
        Assert.True(selfServiceLoopIndex > formCloseIndex,
            "The self-service sections loop (plugin raw HTML) must render after editProfileForm closes.");

        // The save button now lives outside the form's DOM subtree by design (see above), so it
        // must bind to the form explicitly via the HTML5 form="" attribute instead of relying on
        // ancestry.
        Assert.Contains("form=\"editProfileForm\"", content, StringComparison.Ordinal);
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

using System.Text.RegularExpressions;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-31. Plugin-supplied HTML is sanitised by the host (strict allowlist) before any view renders
/// it. Scripts, styles and event handlers go; host-handled forms and ordinary markup stay.
/// </summary>
public sealed class PluginHtmlSanitisationTests
{
    [Theory]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<script>alert(1)</script>", "<script")]
    [InlineData("<script type=\"application/json\">{}</script>", "<script")]
    [InlineData("<style>body{display:none}</style>", "<style")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<a href=\"data:text/html,<script>alert(1)</script>\">x</a>", "data:")]
    [InlineData("<iframe src=\"https://example.org\"></iframe>", "<iframe")]
    [InlineData("<div style=\"background:url(javascript:alert(1))\">x</div>", "style=")]
    [InlineData("<button formaction=\"https://evil.example\">x</button>", "formaction")]
    [InlineData("<svg onload=alert(1)></svg>", "onload")]
    public void BothProfiles_RemoveActiveContent(string html, string forbidden)
    {
        Assert.DoesNotContain(forbidden, PluginHtml.SanitizeSection(html), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, PluginHtml.SanitizeEditTab(html), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SectionProfile_KeepsHostHandledFormsAndOrdinaryMarkup()
    {
        const string html =
            "<form class=\"selfservice-plugin-action-form\" data-action-key=\"finance.account.selfservice.replace\">"
            + "<label class=\"form-label\" for=\"iban\">IBAN *</label>"
            + "<input type=\"text\" class=\"form-control\" id=\"iban\" name=\"iban\" required aria-describedby=\"h\" />"
            + "<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"t\" />"
            + "<button type=\"submit\" class=\"btn btn-primary\">Speichern</button>"
            + "</form>"
            + "<table class=\"table\"><tr><td>1</td></tr></table>"
            + "<a href=\"/plugin-pages/x/y\">Seite</a>";

        var sanitised = PluginHtml.SanitizeSection(html);

        Assert.Contains("data-action-key=\"finance.account.selfservice.replace\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("name=\"iban\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("required", sanitised, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"plugin-content-h\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("type=\"hidden\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("<button type=\"submit\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("<table class=\"table\">", sanitised, StringComparison.Ordinal);
        Assert.Contains("href=\"/plugin-pages/x/y\"", sanitised, StringComparison.Ordinal);
    }

    /// <summary>
    /// Edit tabs render inside the host's member edit form (Views/Members/Edit.cshtml), so plugin
    /// markup must never be able to add fields to that form or submit it.
    /// </summary>
    [Theory]
    [InlineData("<input name=\"OauthID\" value=\"attacker\">", "<input")]
    [InlineData("<select name=\"IsActive\"><option>true</option></select>", "<select")]
    [InlineData("<textarea name=\"Email\">x</textarea>", "<textarea")]
    [InlineData("<form><input name=\"x\"></form>", "<form")]
    [InlineData("<button name=\"KeycloakUsername\" value=\"attacker\">x</button>", "name=")]
    public void EditTabProfile_CannotInjectFieldsIntoTheHostForm(string html, string forbidden)
    {
        Assert.DoesNotContain(forbidden, PluginHtml.SanitizeEditTab(html), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditTabProfile_KeepsHostActionButtons_ButNeverAsSubmitButtons()
    {
        var sanitised = PluginHtml.SanitizeEditTab(
            "<button type=\"submit\" class=\"btn btn-sm\" data-plugin-member-action data-plugin-action-key=\"carinfo.edit\">Bearbeiten</button>");

        Assert.Contains("type=\"button\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-key=\"carinfo.edit\"", sanitised, StringComparison.Ordinal);
        Assert.DoesNotContain("submit", sanitised, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<div data-selfservice-section data-selfservice-action-endpoint=\"https://evil.example\">x</div>", "data-selfservice")]
    [InlineData("<div data-module-id=\"other.plugin\">x</div>", "data-module-id")]
    [InlineData("<div data-plugin-content-module=\"other.plugin\">x</div>", "data-plugin-content-module")]
    [InlineData("<div data-host-form-message>x</div>", "data-host-")]
    [InlineData("<button data-plugin-member-action data-plugin-action-endpoint=\"https://evil.example\">x</button>", "data-plugin-action-endpoint")]
    [InlineData("<button data-plugin-member-action data-plugin-module-id=\"other.plugin\">x</button>", "data-plugin-module-id")]
    [InlineData("<button data-plugin-member-action data-plugin-member-id=\"999\">x</button>", "data-plugin-member-id")]
    [InlineData("<div data-plugin-content-endpoint=\"https://evil.example\">x</div>", "data-plugin-content-")]
    public void BothProfiles_StripHostReservedAttributes(string html, string forbidden)
    {
        Assert.DoesNotContain(forbidden, PluginHtml.SanitizeSection(html), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, PluginHtml.SanitizeEditTab(html), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BothProfiles_PrefixIds_AndTheAttributesThatReferenceThem()
    {
        var sanitised = PluginHtml.SanitizeSection(
            "<label for=\"iban\">IBAN</label><input id=\"iban\" aria-describedby=\"help other\">"
            + "<a href=\"#savingOverlay\">x</a><div id=\"editMemberForm\"></div>");

        Assert.Contains("for=\"plugin-content-iban\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("id=\"plugin-content-iban\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"plugin-content-help plugin-content-other\"", sanitised, StringComparison.Ordinal);
        Assert.Contains("href=\"#plugin-content-savingOverlay\"", sanitised, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"editMemberForm\"", sanitised, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/finance", true)]
    [InlineData("/", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//evil.example/x", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("", false)]
    public void NavRoutes_MustBeAppRelative(string route, bool allowed)
    {
        Assert.Equal(allowed, PluginNavEntryService.IsLocalRoute(route));
    }

    /// <summary>
    /// Views may only use Html.Raw on the reviewed expressions below. Plugin HTML on that list is
    /// sanitised in its collecting service; any new Html.Raw must be reviewed and added here.
    /// </summary>
    [Fact]
    public void Views_UseHtmlRaw_OnlyOnReviewedExpressions()
    {
        string[] reviewed =
        [
            "sv.Section.HtmlBody",                                                // PluginHtml.SanitizeSection in SelfServiceSectionService
            "tab.Tab.Content",                                                    // PluginHtml.SanitizeEditTab in MemberPluginSlotService
            "ClubGear.Services.Core.MemberEmailBadge.Html(member)",              // host-built markup
            "ClubGear.Services.Core.MemberEmailBadge.Html(Model)",
            "ClubGear.Services.Core.NotificationStatusBadge.Html(record.Status)",
        ];

        var viewsRoot = Path.Combine(FindRepositoryRoot(), "Views");
        var unreviewed = Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"Html\.Raw\(\s*(?<expr>[^()]*(?:\([^()]*\))?)\s*\)")
                .Select(match => (File: Path.GetRelativePath(viewsRoot, file), Expr: match.Groups["expr"].Value.Trim())))
            .Where(hit => !reviewed.Contains(hit.Expr))
            .Select(hit => $"{hit.File}: Html.Raw({hit.Expr})")
            .ToArray();

        Assert.True(unreviewed.Length == 0, "Unreviewed Html.Raw in views:\n" + string.Join("\n", unreviewed));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ClubGear.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ClubGear.csproj not found.");
    }
}

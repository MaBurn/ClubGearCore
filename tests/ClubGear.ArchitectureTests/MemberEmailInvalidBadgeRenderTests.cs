using ClubGear.Models;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2, checkbox 2.4 DoD: the "ungültig" badge appears for a member whose address is flagged
/// invalid and is absent for an unflagged one, on both the members list and the member edit view.
/// The badge markup itself lives in <see cref="MemberEmailBadge"/> so the flagged/unflagged
/// decision is asserted directly; the view tests assert that both views render exactly that badge
/// next to the address (and, for Edit, the invalidation context).
/// </summary>
public sealed class MemberEmailInvalidBadgeRenderTests
{
    [Fact]
    public void Badge_ForFlaggedMember_RendersUngueltigBadge()
    {
        var html = MemberEmailBadge.Html(FlaggedMember());

        Assert.Contains("ungültig", html, StringComparison.Ordinal);
        Assert.Contains("badge", html, StringComparison.Ordinal);
        Assert.Contains("member-email-invalid-badge", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Badge_ForUnflaggedMember_RendersNothing()
    {
        var member = FlaggedMember();
        member.EmailInvalid = false;
        member.EmailInvalidatedAtUtc = null;
        member.EmailInvalidReason = null;

        Assert.Equal(string.Empty, MemberEmailBadge.Html(member));
        Assert.Equal(string.Empty, MemberEmailBadge.DescribeInvalidState(member));
    }

    [Fact]
    public void Badge_ForNullMember_RendersNothing()
    {
        Assert.Equal(string.Empty, MemberEmailBadge.Html(null));
    }

    [Fact]
    public void Badge_EncodesTheDiagnosticInTheTooltip()
    {
        var member = FlaggedMember();
        member.EmailInvalidReason = "550 <script>alert(1)</script>";

        var html = MemberEmailBadge.Html(member);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeInvalidState_MentionsTimestampAndReason()
    {
        var description = MemberEmailBadge.DescribeInvalidState(FlaggedMember());

        Assert.Contains("24.07.2026", description, StringComparison.Ordinal);
        Assert.Contains("550 5.1.1 unknown mailbox", description, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_RendersTheBadgeNextToEveryEmailCell()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        var badgeCalls = CountOccurrences(indexContent, "MemberEmailBadge.Html(member)");
        var emailCells = CountOccurrences(indexContent, "member.Email");

        Assert.Equal(emailCells, badgeCalls);
        Assert.True(badgeCalls > 0, "Die Mitgliederliste rendert kein Ungueltig-Badge.");
    }

    [Fact]
    public void MembersEdit_RendersTheBadgeAndTheInvalidationContext()
    {
        var editContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Edit.cshtml"));

        Assert.Contains("MemberEmailBadge.Html(Model)", editContent, StringComparison.Ordinal);
        Assert.Contains("Model.EmailInvalid", editContent, StringComparison.Ordinal);
        Assert.Contains("MemberEmailBadge.DescribeInvalidState(Model)", editContent, StringComparison.Ordinal);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static Member FlaggedMember() => new()
    {
        MemberNumber = "M-1",
        FirstName = "Erika",
        LastName = "Muster",
        Email = "erika@example.org",
        EmailInvalid = true,
        EmailInvalidatedAtUtc = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc),
        EmailInvalidReason = "550 5.1.1 unknown mailbox"
    };

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
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

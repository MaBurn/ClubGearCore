using ClubGear.Models;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 6, checkboxes 6.2 and 6.3 DoD: Admin › Funktionen shows a distinct badge for
/// <c>Sent</c>, <c>Bounced</c>, <c>Suppressed</c> and <c>Failed</c> in both notification tables —
/// the five-row summary on the "Wartung" card and the full log table — instead of the previous
/// binary green/red expression.
/// </summary>
public sealed class AdminFunctionsStatusRenderTests
{
    private const string SummaryLoopMarker = "Model.NotificationRecords.Take(5)";
    private const string FullTableLoopMarker = "@foreach (var record in Model.NotificationRecords)";
    private const string BadgeCall = "NotificationStatusBadge.Html(record.Status)";

    [Theory]
    [InlineData("Sent", "bg-success")]
    [InlineData("Bounced", "bg-warning text-dark")]
    [InlineData("Suppressed", "bg-secondary")]
    [InlineData("Failed", "bg-danger")]
    public void SummaryTable_RendersTheExpectedBadgeForEveryStatus(string status, string expectedCssClass)
    {
        Assert.Contains(BadgeCall, SummaryTableBlock(), StringComparison.Ordinal);

        var rendered = NotificationStatusBadge.Html(RecordWith(status).Status);

        Assert.Contains($"badge {expectedCssClass}", rendered, StringComparison.Ordinal);
        Assert.Contains($">{status}<", rendered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Sent", "bg-success")]
    [InlineData("Bounced", "bg-warning text-dark")]
    [InlineData("Suppressed", "bg-secondary")]
    [InlineData("Failed", "bg-danger")]
    public void FullTable_RendersTheExpectedBadgeForEveryStatus(string status, string expectedCssClass)
    {
        Assert.Contains(BadgeCall, FullTableBlock(), StringComparison.Ordinal);

        var rendered = NotificationStatusBadge.Html(RecordWith(status).Status);

        Assert.Contains($"badge {expectedCssClass}", rendered, StringComparison.Ordinal);
        Assert.Contains($">{status}<", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void BothStatusColumns_RouteThroughTheSharedBadgeHelper()
    {
        var content = ViewContent();

        Assert.Equal(2, CountOccurrences(content, BadgeCall));
    }

    [Fact]
    public void NoBinaryGreenRedExpressionRemains()
    {
        var content = ViewContent();

        Assert.DoesNotContain("? \"bg-success\" : \"bg-danger\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("record.Status == \"Sent\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFourRenderedBadgeClassesAreAllDifferent()
    {
        var classes = new[] { "Sent", "Bounced", "Suppressed", "Failed" }
            .Select(NotificationStatusBadge.CssClass)
            .ToList();

        Assert.Equal(classes.Count, classes.Distinct(StringComparer.Ordinal).Count());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static NotificationRecord RecordWith(string status) => new()
    {
        Channel = "email",
        Recipient = "erika@example.org",
        Subject = "Testnachricht",
        Status = status,
        CreatedAtUtc = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc)
    };

    private static string SummaryTableBlock() => LoopBlock(SummaryLoopMarker);

    private static string FullTableBlock()
    {
        var content = ViewContent();
        var start = content.IndexOf(FullTableLoopMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Die vollstaendige Protokolltabelle wurde in Functions.cshtml nicht gefunden.");

        var end = content.IndexOf("</tbody>", start, StringComparison.Ordinal);
        Assert.True(end > start, "Zur vollstaendigen Protokolltabelle fehlt das schliessende </tbody>.");

        return content[start..end];
    }

    private static string LoopBlock(string marker)
    {
        var content = ViewContent();
        var start = content.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{marker}' wurde in Functions.cshtml nicht gefunden.");

        var end = content.IndexOf("</tbody>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Zum Marker '{marker}' fehlt das schliessende </tbody>.");

        return content[start..end];
    }

    private static string ViewContent() =>
        File.ReadAllText(GetProjectFilePath("Views", "Admin", "Functions.cshtml"));

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

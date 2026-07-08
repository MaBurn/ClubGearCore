using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MembersReleaseGateTests
{
    private static readonly string[] RequiredChecklistItems =
    {
        "UX-01",
        "UX-02",
        "UX-03",
        "PERF-01",
        "PERF-02",
        "PERF-03",
        "A11Y-01",
        "A11Y-02",
        "A11Y-03",
        "SEC-01",
        "SEC-02",
        "SEC-03"
    };

    [Fact]
    public void Checklist_ContainsAllRequiredReleaseGatesMarkedAsFulfilled()
    {
        var checklist = File.ReadAllText(GetProjectFilePath(".crispy", "release-gates", "members-kpi-a11y-checklist.md"));

        foreach (var item in RequiredChecklistItems)
        {
            Assert.Contains($"- [x] {item}:", checklist, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("- [ ]", checklist, StringComparison.Ordinal);
    }

    [Fact]
    public void SignOff_RecordsPassStatusAndReleaseDecision()
    {
        var signOff = File.ReadAllText(GetProjectFilePath(".crispy", "release-gates", "members-rollout-signoff.md"));

        Assert.Contains("Release gate checklist: PASS", signOff, StringComparison.Ordinal);
        Assert.Contains("Decision: FREIGABE ERTEILT", signOff, StringComparison.Ordinal);
        Assert.Contains("MembersReleaseGateTests", signOff, StringComparison.Ordinal);
        Assert.Contains("MemberAuthorizationRegressionTests", signOff, StringComparison.Ordinal);
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
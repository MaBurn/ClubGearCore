using System.Diagnostics;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 4 — the Members Index search and type-filter operate group-aware.
///
/// 4.1 guards that every rendered row/card carries the four group data attributes.
/// 4.2 verifies the rewritten, group-aware filter: its DOM entry point lives in the external
///     <c>member-index-filter.js</c> module and its pure decision core (computeGroupVisibility)
///     is exercised for the three DoD scenarios by executing the module under the Node runtime.
/// </summary>
public sealed class MemberIndexGroupFilterTests
{
    // --- 4.1: group data attributes on every rendered row/card ---------------------------------

    [Fact]
    public void MembersIndex_TableRows_ExposeAllFourGroupDataAttributes()
    {
        var indexContent = ReadIndexView();

        Assert.Contains("<tr data-group-id=\"@row.GroupId\" data-group-type=\"@row.GroupType\" data-member-type=\"@(member.MembershipType?.Key ?? \"unassigned\")\" data-member-id=\"@member.Id\" data-depth=\"@row.Depth\"", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_MobileCards_ExposeAllFourGroupDataAttributes()
    {
        var indexContent = ReadIndexView();

        Assert.Contains("data-group-id=\"@row.GroupId\" data-group-type=\"@row.GroupType\" data-member-type=\"@(member.MembershipType?.Key ?? \"unassigned\")\" data-depth=\"@row.Depth\"", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_LoadsTheGroupAwareFilterModule()
    {
        var indexContent = ReadIndexView();

        Assert.Contains("src=\"~/js/member-index-filter.js\"", indexContent, StringComparison.Ordinal);
        // The old per-row filter must be gone (no inline redefinition shadowing the module).
        Assert.DoesNotContain(".card[data-member-type]", indexContent, StringComparison.Ordinal);
    }

    // --- 4.2: the module is group-aware --------------------------------------------------------

    [Fact]
    public void FilterModule_IsGroupAware()
    {
        var moduleContent = ReadFilterModule();

        Assert.Contains("computeGroupVisibility", moduleContent, StringComparison.Ordinal);
        // Groups rows by data-group-id and decides visibility per whole group.
        Assert.Contains("data-group-id", moduleContent, StringComparison.Ordinal);
        Assert.Contains("data-group-type", moduleContent, StringComparison.Ordinal);
        // typeMatch: all / group container type / any own type in the group.
        Assert.Contains("filter === g.groupType", moduleContent, StringComparison.Ordinal);
        Assert.Contains("g.memberTypes.indexOf(filter)", moduleContent, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupAwareFilter_BehaviorScenarios_PassUnderNode()
    {
        var nodePath = ResolveNodeExecutable();
        if (nodePath is null)
        {
            // Node is not available on this machine; the file-content guards above still hold.
            return;
        }

        var harness = GetProjectFilePath("tests", "ClubGear.ArchitectureTests", "member-index-filter.behavior.cjs");
        var module = GetProjectFilePath("wwwroot", "js", "member-index-filter.js");

        var psi = new ProcessStartInfo
        {
            FileName = nodePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(harness);
        psi.ArgumentList.Add(module);

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdout = process!.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);

        Assert.True(
            process.ExitCode == 0,
            $"Group-aware filter behavior scenarios failed under Node.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.Contains("ALL PASS", stdout, StringComparison.Ordinal);
    }

    private static string? ResolveNodeExecutable()
    {
        var candidates = new[]
        {
            "/usr/local/bin/node",
            "/opt/homebrew/bin/node",
            "/usr/bin/node",
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVar))
        {
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                var probe = Path.Combine(dir, "node");
                if (File.Exists(probe))
                {
                    return probe;
                }
            }
        }

        return null;
    }

    private static string ReadIndexView()
        => File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

    private static string ReadFilterModule()
        => File.ReadAllText(GetProjectFilePath("wwwroot", "js", "member-index-filter.js"));

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

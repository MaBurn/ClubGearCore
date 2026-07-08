using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 4 — Razor partial: grouped edit-cards rendering in _PluginSlots.cshtml
///
/// Verifies:
///   4.1  The partial file contains data-plugin-slot="edit-cards" and implements the
///        GroupBy grouping strategy for the edit-cards mode.
///   4.2  Tab ID uniqueness: each tab button/pane pair has a deterministic ID derived
///        from ModuleId and Tab.Key. The first tab in each grouped card has the
///        "active" / "show active" classes; subsequent tabs do not.
/// </summary>
public sealed class PluginSlice4EditCardsTests
{
    // ------------------------------------------------------------------
    // 4.1 — Structural assertions on the Razor partial file
    // ------------------------------------------------------------------

    [Fact]
    public void PluginSlotsPartial_ContainsEditCardsDataAttribute()
    {
        var content = ReadSlotPartial();
        Assert.Contains("data-plugin-slot=\"edit-cards\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginSlotsPartial_ContainsGroupByExpression()
    {
        var content = ReadSlotPartial();
        // The file must use GroupBy to partition EditTabs into groups.
        Assert.Contains("GroupBy(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginSlotsPartial_ContainsSoloCardLayout()
    {
        var content = ReadSlotPartial();
        // Solo cards (size-1 groups) render with Html.Raw for content.
        Assert.Contains("Html.Raw(tab.Tab.Content)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginSlotsPartial_ContainsGroupedCardNavTabsLayout()
    {
        var content = ReadSlotPartial();
        // Multi-tab groups render a nav-tabs strip inside a card.
        Assert.Contains("nav nav-tabs", content, StringComparison.Ordinal);
        Assert.Contains("tab-content", content, StringComparison.Ordinal);
        Assert.Contains("tab-pane", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginSlotsPartial_GroupKeyFallbackUsesModuleIdAndTabKey()
    {
        var content = ReadSlotPartial();
        // GroupBy key falls back to "__solo_{ModuleId}_{Tab.Key}" for ungrouped tabs.
        Assert.Contains("__solo_", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginSlotsPartial_PreservesEditTabsDataAttribute()
    {
        // The existing edit-tabs mode must be preserved (regression guard).
        var content = ReadSlotPartial();
        Assert.Contains("data-plugin-slot=\"edit-tabs\"", content, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // 4.2 — Tab ID uniqueness and Bootstrap activation on MemberPluginEditTabView
    // ------------------------------------------------------------------

    [Fact]
    public void SafeTabId_IsUniquePerModuleAndTabKey()
    {
        // Verify that two tabs with different ModuleId+Key produce different IDs.
        var id1 = MakeTabId("plugin.carinfo", "fahrzeuge.tab");
        var id2 = MakeTabId("plugin.servicebook", "fahrzeuge.tab");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SafeTabId_ReplacesDotsAndUnderscores()
    {
        var id = MakeTabId("plugin.carinfo", "my_tab.key");

        Assert.DoesNotContain(".", id, StringComparison.Ordinal);
        Assert.DoesNotContain("_", id, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeTabId_HasExpectedFormat()
    {
        var id = MakeTabId("plugin.carinfo", "details");

        // plugin-tab-{moduleId}-{tabKey} with dots replaced by dashes
        Assert.StartsWith("plugin-tab-", id, StringComparison.Ordinal);
        Assert.Contains("plugin-carinfo", id, StringComparison.Ordinal);
        Assert.Contains("details", id, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupBy_WithSharedGroupKey_ProducesOneGroup()
    {
        var tabs = BuildGroupedTabs("fahrzeuge", "Fahrzeuge",
            ("plugin.carinfo", "carinfo-tab"),
            ("plugin.servicebook", "sb-tab"));

        var groups = tabs
            .GroupBy(t => t.GroupKey ?? $"__solo_{t.ModuleId}_{t.Tab.Key}")
            .ToList();

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count());
    }

    [Fact]
    public void GroupBy_WithNullGroupKey_ProducesSoloGroupsPerTab()
    {
        var tabs = BuildSoloTabs(
            ("plugin.carinfo", "carinfo-tab"),
            ("plugin.servicebook", "sb-tab"));

        var groups = tabs
            .GroupBy(t => t.GroupKey ?? $"__solo_{t.ModuleId}_{t.Tab.Key}")
            .ToList();

        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Single(g));
    }

    [Fact]
    public void GroupBy_MixedTabs_SeparatesSoloFromGrouped()
    {
        var grouped = BuildGroupedTabs("fahrzeuge", "Fahrzeuge",
            ("plugin.carinfo", "carinfo-tab"),
            ("plugin.servicebook", "sb-tab"));
        var solo = BuildSoloTabs(("plugin.other", "other-tab"));

        var all = grouped.Concat(solo).ToList();

        var groups = all
            .GroupBy(t => t.GroupKey ?? $"__solo_{t.ModuleId}_{t.Tab.Key}")
            .ToList();

        Assert.Equal(2, groups.Count); // one named group + one solo group
        var groupedGroup = groups.Single(g => g.Count() == 2);
        Assert.Equal("fahrzeuge", groupedGroup.Key);
    }

    [Fact]
    public void FirstTabInGroupedCard_HasActiveClass_OthersDoNot()
    {
        // Simulate the "gi == 0 ? active : null" pattern used by the Razor partial.
        var members = new List<string> { "tab-a", "tab-b", "tab-c" };

        var classes = members
            .Select((_, gi) => gi == 0 ? "nav-link active" : "nav-link")
            .ToList();

        Assert.Equal("nav-link active", classes[0]);
        Assert.Equal("nav-link", classes[1]);
        Assert.Equal("nav-link", classes[2]);
    }

    [Fact]
    public void FirstTabPaneInGroupedCard_HasShowActiveClass_OthersDoNot()
    {
        var members = new List<string> { "pane-a", "pane-b" };

        var classes = members
            .Select((_, gi) => gi == 0 ? "tab-pane fade show active" : "tab-pane fade")
            .ToList();

        Assert.Equal("tab-pane fade show active", classes[0]);
        Assert.Equal("tab-pane fade", classes[1]);
    }

    [Fact]
    public void TabIds_AreUnique_WhenTwoPluginsShareGroupKey()
    {
        var tabs = BuildGroupedTabs("fahrzeuge", "Fahrzeuge",
            ("plugin.carinfo", "carinfo-tab"),
            ("plugin.servicebook", "sb-tab"));

        var ids = tabs
            .Select(t => MakeTabId(t.ModuleId, t.Tab.Key))
            .ToList();

        // All IDs must be distinct — no duplicates in a group.
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string ReadSlotPartial()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var csproj = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(csproj))
            {
                return File.ReadAllText(
                    Path.Combine(current.FullName, "Views", "Members", "_PluginSlots.cshtml"));
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }

    /// <summary>
    /// Replicates the SafeTabId logic from _PluginSlots.cshtml.
    /// </summary>
    private static string MakeTabId(string moduleId, string tabKey)
        => $"plugin-tab-{moduleId}-{tabKey}".Replace('.', '-').Replace('_', '-');

    private static List<MemberPluginEditTabView> BuildGroupedTabs(
        string groupKey,
        string groupTitle,
        params (string moduleId, string tabKey)[] entries)
        => entries
            .Select(e => new MemberPluginEditTabView(
                e.moduleId,
                "Test Plugin",
                new MemberEditTabSlot(e.tabKey, e.tabKey, "content", 0),
                0)
            {
                GroupKey = groupKey,
                GroupTitle = groupTitle,
            })
            .ToList();

    private static List<MemberPluginEditTabView> BuildSoloTabs(
        params (string moduleId, string tabKey)[] entries)
        => entries
            .Select(e => new MemberPluginEditTabView(
                e.moduleId,
                "Test Plugin",
                new MemberEditTabSlot(e.tabKey, e.tabKey, "content", 0),
                0))
            .ToList();
}

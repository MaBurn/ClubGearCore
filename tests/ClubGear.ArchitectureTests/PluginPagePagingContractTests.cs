using ClubGear.Plugin.Contracts;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-03 increment 1 (contract 1.11.0): the page contract gained sorting and paging.
///
/// The compatibility requirement for this increment is that a plugin compiled against 1.10.0 —
/// one that implements only the three original members — keeps working and picks up sorting and
/// paging through the default interface implementation, without being recompiled.
/// </summary>
public sealed class PluginPagePagingContractTests
{
    [Fact]
    public void GroupingMetadata_IsAdditive_AndPreservesExistingConstructor()
    {
        var navEntry = new PluginNavEntry("Eintrag", "bi-box", "/entry", null, 10)
        {
            GroupKey = "group",
            GroupLabel = "Gruppe",
            GroupIcon = "bi-collection"
        };

        Assert.Equal("group", navEntry.GroupKey);
        Assert.Equal("Gruppe", navEntry.GroupLabel);
        Assert.Equal("bi-collection", navEntry.GroupIcon);
    }

    [Fact]
    public void IsIconColumn_IsAdditive_AndDefaultsToFalse()
    {
        var textColumn = new PluginPageColumn("Status", "Status");
        var iconColumn = new PluginPageColumn("Icon", "Symbol") { IsIconColumn = true };

        Assert.False(textColumn.IsIconColumn);
        Assert.True(iconColumn.IsIconColumn);
    }

    // ---------------------------------------------------------------------------------------
    // Compatibility: a provider that knows nothing about 1.11.0
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Implements exactly the members that existed in contract 1.10.0 and nothing else. If this
    /// class stops compiling, the contract change was not additive.
    /// </summary>
    private sealed class LegacyProvider : IPluginPageProvider
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string?>> _rows;

        public int GetRowsCallCount { get; private set; }

        public LegacyProvider(params string[] names)
        {
            _rows = names
                .Select(IReadOnlyDictionary<string, string?> (n) => new Dictionary<string, string?>
                {
                    ["id"] = n,
                    ["name"] = n
                })
                .ToList();
        }

        public Task<PluginPageDefinition> GetPageDefinitionAsync(IPluginHostContext context, CancellationToken ct = default)
            => Task.FromResult(new PluginPageDefinition(
                "legacy", "Legacy", "id",
                [new PluginPageColumn("name", "Name")],
                [],
                null,
                null));

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetRowsAsync(
            IPluginHostContext context, string? filterValue, string? entityKey, CancellationToken ct = default)
        {
            GetRowsCallCount++;
            return Task.FromResult(_rows);
        }

        public Task<PluginCommandResult> ExecuteCommandAsync(
            IPluginHostContext context, string commandKey, string? entityKey,
            IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
            => Task.FromResult(new PluginCommandResult(true, "ok"));
    }

    /// <summary>A provider that does its own paging, as a large plugin would.</summary>
    private sealed class PagingAwareProvider : IPluginPageProvider
    {
        public PluginPageQuery? ReceivedQuery { get; private set; }

        public Task<PluginPageDefinition> GetPageDefinitionAsync(IPluginHostContext context, CancellationToken ct = default)
            => Task.FromResult(new PluginPageDefinition(
                "aware", "Aware", "id", [], [], null, null));

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetRowsAsync(
            IPluginHostContext context, string? filterValue, string? entityKey, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The host must not fall back to the unpaged method when the provider overrides GetRowsPageAsync.");

        public Task<PluginCommandResult> ExecuteCommandAsync(
            IPluginHostContext context, string commandKey, string? entityKey,
            IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
            => Task.FromResult(new PluginCommandResult(true, "ok"));

        public Task<PluginPageRows> GetRowsPageAsync(
            IPluginHostContext context, PluginPageQuery query, CancellationToken ct = default)
        {
            ReceivedQuery = query;
            return Task.FromResult(new PluginPageRows(
                [new Dictionary<string, string?> { ["id"] = "served-by-plugin" }],
                TotalCount: 4711));
        }
    }

    [Fact]
    public async Task LegacyProvider_GainsPaging_ThroughTheDefaultImplementation()
    {
        // Through the interface: a default interface method is only reachable that way, which
        // is exactly how the host calls it.
        IPluginPageProvider provider = new LegacyProvider("a", "b", "c", "d", "e");

        var page = await provider.GetRowsPageAsync(
            context: null!,
            new PluginPageQuery { SortKey = "name", Page = 2, PageSize = 2 });

        Assert.Equal(["c", "d"], page.Rows.Select(r => r["name"]));
    }

    [Fact]
    public async Task LegacyProvider_TotalCount_Counts_All_Matches_Not_The_Page()
    {
        // Through the interface: a default interface method is only reachable that way, which
        // is exactly how the host calls it.
        IPluginPageProvider provider = new LegacyProvider("a", "b", "c", "d", "e");

        var page = await provider.GetRowsPageAsync(
            context: null!,
            new PluginPageQuery { SortKey = "name", Page = 1, PageSize = 2 });

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task LegacyProvider_WithoutPageSize_Returns_Every_Row_As_Before_1_11()
    {
        IPluginPageProvider provider = new LegacyProvider("a", "b", "c");

        var page = await provider.GetRowsPageAsync(context: null!, new PluginPageQuery());

        Assert.Equal(3, page.Rows.Count);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Overriding_Provider_Is_Used_Instead_Of_The_Fallback()
    {
        var concrete = new PagingAwareProvider();
        IPluginPageProvider provider = concrete;

        var page = await provider.GetRowsPageAsync(
            context: null!,
            new PluginPageQuery { PageSize = 10 });

        // GetRowsAsync throws, so reaching this line at all proves the override was taken.
        Assert.Equal(4711, page.TotalCount);
        Assert.Equal(10, concrete.ReceivedQuery!.PageSize);
    }

    // ---------------------------------------------------------------------------------------
    // Ordering
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PluginSortDirection.Ascending, new[] { "alpha", "Beta", "gamma" })]
    [InlineData(PluginSortDirection.Descending, new[] { "gamma", "Beta", "alpha" })]
    public void SortAndPageInMemory_Orders_Case_Insensitively(PluginSortDirection direction, string[] expected)
    {
        var rows = Rows("gamma", "alpha", "Beta");

        var page = PluginPageRows.SortAndPageInMemory(
            rows, new PluginPageQuery { SortKey = "name", SortDirection = direction });

        Assert.Equal(expected, page.Rows.Select(r => r["name"]));
    }

    [Theory]
    [InlineData(PluginSortDirection.Ascending)]
    [InlineData(PluginSortDirection.Descending)]
    public void SortAndPageInMemory_Sorts_Blanks_Last_In_Both_Directions(PluginSortDirection direction)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
        [
            new Dictionary<string, string?> { ["name"] = "b" },
            new Dictionary<string, string?> { ["name"] = null },
            new Dictionary<string, string?> { ["name"] = "a" },
            new Dictionary<string, string?> { ["name"] = "" },
        ];

        var page = PluginPageRows.SortAndPageInMemory(
            rows, new PluginPageQuery { SortKey = "name", SortDirection = direction });

        var trailing = page.Rows.TakeLast(2).Select(r => r["name"]).ToList();
        Assert.All(trailing, v => Assert.True(string.IsNullOrEmpty(v)));
    }

    [Fact]
    public void SortAndPageInMemory_Without_SortKey_Preserves_The_Plugin_Order()
    {
        var rows = Rows("zulu", "alpha", "mike");

        var page = PluginPageRows.SortAndPageInMemory(rows, new PluginPageQuery());

        Assert.Equal(["zulu", "alpha", "mike"], page.Rows.Select(r => r["name"]));
    }

    // ---------------------------------------------------------------------------------------
    // Host-side clamping
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Clamp_Caps_The_Page_Size_A_Query_String_Can_Ask_For()
    {
        var clamped = PluginPageService.Clamp(
            new PluginPageQuery { PageSize = 100_000 }, Definition());

        Assert.Equal(PluginPageService.MaxPageSize, clamped.PageSize);
    }

    [Fact]
    public void Clamp_Falls_Back_To_The_Definitions_DefaultPageSize()
    {
        var clamped = PluginPageService.Clamp(
            new PluginPageQuery { PageSize = null },
            Definition() with { DefaultPageSize = 25 });

        Assert.Equal(25, clamped.PageSize);
    }

    [Fact]
    public void Clamp_Leaves_Paging_Off_When_Neither_Side_Asks_For_It()
    {
        var clamped = PluginPageService.Clamp(new PluginPageQuery(), Definition());

        Assert.Null(clamped.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Clamp_Normalises_A_Page_Below_One(int page)
    {
        var clamped = PluginPageService.Clamp(new PluginPageQuery { Page = page }, Definition());

        Assert.Equal(1, clamped.Page);
    }

    [Fact]
    public void Clamp_Drops_A_Sort_Key_The_Definition_Did_Not_Mark_Sortable()
    {
        var clamped = PluginPageService.Clamp(
            new PluginPageQuery { SortKey = "secret" }, Definition());

        Assert.Null(clamped.SortKey);
    }

    [Fact]
    public void Clamp_Keeps_A_Sort_Key_The_Definition_Marked_Sortable()
    {
        var clamped = PluginPageService.Clamp(
            new PluginPageQuery { SortKey = "name" }, Definition());

        Assert.Equal("name", clamped.SortKey);
    }

    // ---------------------------------------------------------------------------------------
    // Binary compatibility of the records that gained properties
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void PluginPageDefinition_Still_Has_Its_Seven_Argument_Constructor()
    {
        // A plugin compiled against 1.10.0 calls this constructor by signature. Turning
        // DefaultPageSize into a constructor parameter would have changed the arity and broken
        // every already-packaged plugin at load time.
        var ctor = typeof(PluginPageDefinition).GetConstructors()
            .SingleOrDefault(c => c.GetParameters().Length == 7);

        Assert.NotNull(ctor);
    }

    [Fact]
    public void PluginPageColumn_Still_Has_Its_Two_Argument_Constructor()
    {
        var ctor = typeof(PluginPageColumn).GetConstructors()
            .SingleOrDefault(c => c.GetParameters().Length == 2);

        Assert.NotNull(ctor);
    }

    [Fact]
    public void New_Properties_Default_To_The_Pre_1_11_Behaviour()
    {
        var definition = new PluginPageDefinition("k", "T", "id", [], [], null, null);
        var column = new PluginPageColumn("name", "Name");

        Assert.Null(definition.DefaultPageSize);
        Assert.False(column.Sortable);
    }

    // ---------------------------------------------------------------------------------------

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows(params string[] names)
        => names
            .Select(IReadOnlyDictionary<string, string?> (n) => new Dictionary<string, string?> { ["name"] = n })
            .ToList();

    private static PluginPageDefinition Definition()
        => new(
            "page", "Page", "id",
            [new PluginPageColumn("name", "Name") { Sortable = true }, new PluginPageColumn("secret", "Secret")],
            [],
            null,
            null);
}

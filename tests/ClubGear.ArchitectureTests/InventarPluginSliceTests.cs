using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Inventar;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class InventarPluginSliceTests
{
    // ----------------------------------------------------------------
    // Test 1: Manifest exists and is valid
    // ----------------------------------------------------------------

    [Fact]
    public void InventarPlugin_ManifestExists_AndIsValid()
    {
        var module = new InventarPluginModule();
        var manifest = module.Manifest;

        Assert.Equal("clubgear.plugin.inventory", manifest.Key);
        Assert.Equal("Inventar", manifest.Name);
        Assert.Equal(new Version(1, 0, 0), manifest.Version);
        Assert.Contains("inventar.items.read", manifest.Permissions);
        Assert.Contains("inventar.items.manage", manifest.Permissions);
        Assert.Contains("page.generic", manifest.ExtensionPoints);
        Assert.Contains("nav.main", manifest.ExtensionPoints);
    }

    // ----------------------------------------------------------------
    // Test 2: Module registers a page provider
    // ----------------------------------------------------------------

    [Fact]
    public void InventarPlugin_RegistersPageProvider()
    {
        var module = new InventarPluginModule();
        var sink = new RecordingContributionSink();

        module.RegisterContributions(sink);

        Assert.Single(sink.PageProviders);
        Assert.Contains(nameof(InventarPageProvider), sink.PageProviders[0].ProviderType, StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------------
    // Test 3: Module registers a nav entry
    // ----------------------------------------------------------------

    [Fact]
    public void InventarPlugin_RegistersNavEntry()
    {
        var module = new InventarPluginModule();
        var sink = new RecordingContributionSink();

        module.RegisterContributions(sink);

        Assert.NotEmpty(sink.NavEntries);
        var entry = sink.NavEntries[0];
        Assert.Equal("inventar.items.read", entry.RequiredPermission);
        Assert.Equal("Inventar", entry.Label);
    }

    // ----------------------------------------------------------------
    // Test 4: Page provider returns correct definition
    // ----------------------------------------------------------------

    [Fact]
    public async Task InventarPageProvider_ReturnsCorrectDefinition()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new InventarPageProvider();

        var definition = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("inventar.items", definition.PageKey);
        Assert.Equal("Inventar", definition.Title);
        Assert.Equal("Nummer", definition.EntityKeyColumn);
        Assert.Equal("inventar.items.read", definition.ListPermission);
        Assert.NotNull(definition.FilterPlaceholder);
        Assert.NotEmpty(definition.Columns);
        Assert.NotEmpty(definition.Commands);

        Assert.Contains(definition.Commands, c => c.Key == "create" && !c.RequiresEntityKey);
        Assert.Contains(definition.Commands, c => c.Key == "edit" && c.RequiresEntityKey);
        Assert.Contains(definition.Commands, c => c.Key == "delete" && c.RequiresEntityKey);
    }

    // ----------------------------------------------------------------
    // Test 5: GetRows returns empty on fresh database
    // ----------------------------------------------------------------

    [Fact]
    public async Task InventarPageProvider_GetRows_ReturnsEmpty_OnFreshDb()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new InventarPageProvider();

        var rows = await provider.GetRowsAsync(fixture.Host, filterValue: null, entityKey: null);

        Assert.Empty(rows);
    }

    // ----------------------------------------------------------------
    // Test 6: Create → GetRows round-trip
    // ----------------------------------------------------------------

    [Fact]
    public async Task InventarPageProvider_ExecuteCreate_ThenGetRows_ReturnsOneRow()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new InventarPageProvider();

        var createResult = await provider.ExecuteCommandAsync(
            fixture.Host,
            "create",
            entityKey: null,
            new Dictionary<string, string>
            {
                ["Nummer"] = "INV-001",
                ["Name"] = "Bohrmaschine",
                ["Kategorie"] = "Werkzeug",
                ["Status"] = "Verfuegbar",
                ["Lagerort"] = "Keller A"
            });

        Assert.True(createResult.Success, $"Create failed: {createResult.Message}");
        Assert.Equal("created", createResult.Status);

        var rows = await provider.GetRowsAsync(fixture.Host, filterValue: null, entityKey: null);

        Assert.Single(rows);
        Assert.Equal("INV-001", rows[0].GetValueOrDefault("Nummer"));
        Assert.Equal("Bohrmaschine", rows[0].GetValueOrDefault("Name"));
        Assert.Equal("Werkzeug", rows[0].GetValueOrDefault("Kategorie"));
        Assert.Equal("Verfuegbar", rows[0].GetValueOrDefault("Status"));
        Assert.Equal("Keller A", rows[0].GetValueOrDefault("Lagerort"));
    }

    // ----------------------------------------------------------------
    // Test 7: CRUD round-trip (create, read by key, update, delete)
    // ----------------------------------------------------------------

    [Fact]
    public async Task InventarPageProvider_CrudRoundTrip_WorksCorrectly()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new InventarPageProvider();

        // Create
        var createResult = await provider.ExecuteCommandAsync(
            fixture.Host,
            "create",
            entityKey: null,
            new Dictionary<string, string>
            {
                ["Nummer"] = "INV-100",
                ["Name"] = "Hammer",
                ["Kategorie"] = "Werkzeug",
                ["Status"] = "Verfuegbar",
                ["Lagerort"] = "Regal 1"
            });
        Assert.True(createResult.Success, $"Create failed: {createResult.Message}");

        // Read by filter
        var filteredRows = await provider.GetRowsAsync(fixture.Host, filterValue: "Hammer", entityKey: null);
        Assert.Single(filteredRows);
        Assert.Equal("INV-100", filteredRows[0].GetValueOrDefault("Nummer"));

        // Read by entity key
        var singleRows = await provider.GetRowsAsync(fixture.Host, filterValue: null, entityKey: "INV-100");
        Assert.Single(singleRows);
        Assert.Equal("Hammer", singleRows[0].GetValueOrDefault("Name"));

        // Update
        var updateResult = await provider.ExecuteCommandAsync(
            fixture.Host,
            "edit",
            entityKey: "INV-100",
            new Dictionary<string, string>
            {
                ["Name"] = "Schwerer Hammer",
                ["Kategorie"] = "Werkzeug",
                ["Status"] = "Ausgeliehen",
                ["Lagerort"] = "Regal 2"
            });
        Assert.True(updateResult.Success, $"Update failed: {updateResult.Message}");
        Assert.Equal("updated", updateResult.Status);

        // Verify update
        var updatedRows = await provider.GetRowsAsync(fixture.Host, filterValue: null, entityKey: "INV-100");
        Assert.Single(updatedRows);
        Assert.Equal("Schwerer Hammer", updatedRows[0].GetValueOrDefault("Name"));
        Assert.Equal("Ausgeliehen", updatedRows[0].GetValueOrDefault("Status"));

        // Delete
        var deleteResult = await provider.ExecuteCommandAsync(
            fixture.Host,
            "delete",
            entityKey: "INV-100",
            new Dictionary<string, string>());
        Assert.True(deleteResult.Success, $"Delete failed: {deleteResult.Message}");
        Assert.Equal("deleted", deleteResult.Status);

        // Verify deletion
        var afterDeleteRows = await provider.GetRowsAsync(fixture.Host, filterValue: null, entityKey: null);
        Assert.Empty(afterDeleteRows);
    }

    // ----------------------------------------------------------------
    // Test 8: Manifest file on disk is valid JSON with correct key
    // ----------------------------------------------------------------

    [Fact]
    public void InventarPlugin_ManifestJsonFile_IsValidAndHasCorrectKey()
    {
        var projectRoot = FindProjectRoot();
        var manifestPath = Path.Combine(projectRoot, "plugins", "Inventar", "plugin.json");

        Assert.True(File.Exists(manifestPath), $"plugin.json not found at {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        Assert.Contains("\"clubgear.plugin.inventory\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Inventar\"", json, StringComparison.Ordinal);
        Assert.Contains("\"nav.main\"", json, StringComparison.Ordinal);
        Assert.Contains("\"page.generic\"", json, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClubGear.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new SqlitePluginStore(connection, "clubgear.plugin.inventory");
        await new InventarSchemaMigration().ApplyAsync(store);

        return new TestFixture(connection, store, new TestPluginHostContext(store));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(SqliteConnection connection, SqlitePluginStore store, TestPluginHostContext host)
        {
            Connection = connection;
            Store = store;
            Host = host;
        }

        public SqliteConnection Connection { get; }

        public SqlitePluginStore Store { get; }

        public TestPluginHostContext Host { get; }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestPluginHostContext : IPluginHostContext
    {
        public TestPluginHostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new TestMetadataFacade(persistence.ModuleId);
            Members = new NoOpMemberReader();
            MemberActions = new NoOpMemberActions();
        }

        public IPluginMetadataFacade Metadata { get; }

        public IPluginMemberReader Members { get; }

        public IPluginMemberActionFacade MemberActions { get; }

        public IPluginDataStore Persistence { get; }

        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class TestMetadataFacade : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public TestMetadataFacade(string moduleId)
        {
            _moduleId = moduleId;
        }

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "Inventar", "Proprietary", ">=1.0.0",
                ["inventar.items.read", "inventar.items.manage"],
                ["nav.main", "page.generic", "admin.functions"]);
    }

    private sealed class NoOpMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class NoOpMemberActions : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    private sealed class SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public SqlitePluginStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId = moduleId;
            TablePrefix = "plg_inventar_";
        }

        public string ModuleId { get; }

        public string TablePrefix { get; }

        public string GetTableName(string localName)
            => $"{TablePrefix}{localName}";

        public async Task ExecuteAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command, parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PluginDataRow>> QueryAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command, parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<PluginDataRow>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var raw = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    values[reader.GetName(i)] = raw?.ToString();
                }

                rows.Add(new PluginDataRow(values));
            }

            return rows;
        }

        private static void BindParameters(SqliteCommand command, IReadOnlyDictionary<string, string?>? parameters)
        {
            if (parameters is null)
            {
                return;
            }

            foreach (var pair in parameters)
            {
                var parameterName = pair.Key.StartsWith("@", StringComparison.Ordinal) ? pair.Key : $"@{pair.Key}";
                command.Parameters.AddWithValue(parameterName, pair.Value ?? (object)DBNull.Value);
            }
        }
    }

    private sealed class RecordingContributionSink : IPluginContributionSink
    {
        private readonly List<PluginPageProviderContribution> _pageProviders = new();
        private readonly List<PluginNavEntry> _navEntries = new();

        public IReadOnlyList<PluginPageProviderContribution> PageProviders => _pageProviders;

        public IReadOnlyList<PluginNavEntry> NavEntries => _navEntries;

        public void AddRoute(PluginRouteContribution contribution) { }

        public void AddService(PluginServiceContribution contribution) { }

        public void AddMemberProvider(PluginMemberProviderContribution contribution) { }

        public void AddBackgroundJob(PluginBackgroundJobContribution contribution) { }

        public void AddAdminPanelProvider(PluginAdminPanelProviderContribution contribution) { }

        public void AddNavEntries(IReadOnlyList<PluginNavEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            _navEntries.AddRange(entries);
        }

        public void AddPageProvider(PluginPageProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _pageProviders.Add(contribution);
        }
    }
}

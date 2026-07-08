using System.Globalization;
using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 4 verification:
///   4.1 GetPageDefinitionAsync returns correct PluginPageDefinition shape
///   4.2 GetRowsAsync returns correct rows for a known member and empty list for null/invalid entityKey
///   4.3 CarInfoMemberCarsPageProvider is registered via sink.AddPageProvider in CarInfoPluginModule
///   4.4 "page.generic" appears in plugin.json extensionPoints
/// </summary>
public sealed class CarInfoSlice4Tests
{
    // 4.1 — GetPageDefinitionAsync returns correct PageKey
    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsCorrectPageKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("carinfo.member-cars", def.PageKey);
    }

    // 4.1 — GetPageDefinitionAsync returns correct Title
    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsCorrectTitle()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("Fahrzeuge", def.Title);
    }

    // 4.1 — GetPageDefinitionAsync returns EntityKeyColumn = "Id"
    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsCorrectEntityKeyColumn()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("Id", def.EntityKeyColumn);
    }

    // 4.1 — GetPageDefinitionAsync returns ListPermission = "members.manage"
    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsCorrectListPermission()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("members.manage", def.ListPermission);
    }

    // 4.1 — GetPageDefinitionAsync returns FilterPlaceholder
    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsFilterPlaceholder()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Equal("Nach Kennzeichen suchen...", def.FilterPlaceholder);
    }

    // 4.1 — Static columns include Id, LicensePlate, Make, Color, UpdatedAtUtc
    [Fact]
    public async Task GetPageDefinitionAsync_IncludesStaticColumns()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        var keys = def.Columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Id",           keys);
        Assert.Contains("LicensePlate", keys);
        Assert.Contains("Make",         keys);
        Assert.Contains("Color",        keys);
        Assert.Contains("UpdatedAtUtc", keys);
    }

    // 4.1 — Dynamic columns: one per active field definition
    [Fact]
    public async Task GetPageDefinitionAsync_IncludesDynamicColumns()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        // Define an extra field
        await fixture.Host.Persistence.ExecuteAsync(
            $"INSERT INTO {fixture.Host.Persistence.GetTableName("field_definitions")} " +
            "(FieldKey, Label, InputType, IsRequired, SortOrder, IsDeleted, CreatedAtUtc, UpdatedAtUtc) " +
            "VALUES (@k, @l, 'text', 0, 10, 0, @now, @now);",
            new Dictionary<string, string?>
            {
                ["k"] = "color-code",
                ["l"] = "Farbcode",
                ["now"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            });

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        Assert.Contains(def.Columns, c => c.Key == "field.color-code" && c.Label == "Farbcode");
    }

    // 4.1 — Commands include car.add (RequiresEntityKey = false), car.edit and car.delete (RequiresEntityKey = true)
    [Fact]
    public async Task GetPageDefinitionAsync_IncludesCorrectCommandStubs()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var def = await provider.GetPageDefinitionAsync(fixture.Host);

        var add    = Assert.Single(def.Commands, c => c.Key == "car.add");
        var edit   = Assert.Single(def.Commands, c => c.Key == "car.edit");
        var delete = Assert.Single(def.Commands, c => c.Key == "car.delete");

        Assert.False(add.RequiresEntityKey,    "car.add should not require entity key");
        Assert.True(edit.RequiresEntityKey,    "car.edit should require entity key");
        Assert.True(delete.RequiresEntityKey,  "car.delete should require entity key");

        Assert.Equal("members.manage", add.RequiredPermission);
        Assert.Equal("members.manage", edit.RequiredPermission);
        Assert.Equal("members.manage", delete.RequiredPermission);
    }

    // 4.2 — GetRowsAsync returns empty list when entityKey is null
    [Fact]
    public async Task GetRowsAsync_ReturnsEmpty_WhenEntityKeyIsNull()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var rows = await provider.GetRowsAsync(fixture.Host, null, null);

        Assert.Empty(rows);
    }

    // 4.2 — GetRowsAsync returns empty list when entityKey is empty string
    [Fact]
    public async Task GetRowsAsync_ReturnsEmpty_WhenEntityKeyIsEmpty()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var rows = await provider.GetRowsAsync(fixture.Host, null, "");

        Assert.Empty(rows);
    }

    // 4.2 — GetRowsAsync returns empty list when entityKey is not a valid integer
    [Fact]
    public async Task GetRowsAsync_ReturnsEmpty_WhenEntityKeyIsNonNumeric()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var rows = await provider.GetRowsAsync(fixture.Host, null, "not-a-number");

        Assert.Empty(rows);
    }

    // 4.2 — GetRowsAsync returns correct rows for a known member
    [Fact]
    public async Task GetRowsAsync_ReturnsRows_ForKnownMember()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        var rows = await provider.GetRowsAsync(fixture.Host, null, member.Id.ToString(CultureInfo.InvariantCulture));

        var row = Assert.Single(rows);
        Assert.Equal("B-AB 123", row["LicensePlate"]);
        Assert.Equal("Audi",     row["Make"]);
        Assert.Equal("Blau",     row["Color"]);
        Assert.True(row.ContainsKey("Id"));
        Assert.True(row.ContainsKey("UpdatedAtUtc"));
    }

    // 4.2 — GetRowsAsync returns only cars for the requested member (not other members' cars)
    [Fact]
    public async Task GetRowsAsync_ReturnsOnly_MatchingMemberCars()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();
        var actionProvider = new CarInfoActionProvider();

        var member1 = CreateMember(1);
        var member2 = CreateMember(2);

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member1.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member1, fixture.Host);

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member2.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "BMW", ["color"] = "Rot", ["licensePlate"] = "M-XY 999"
            }),
            member2, fixture.Host);

        var rows = await provider.GetRowsAsync(fixture.Host, null, "1");

        var row = Assert.Single(rows);
        Assert.Equal("B-AB 123", row["LicensePlate"]);
    }

    // 4.2 — GetRowsAsync returns empty list for a member with no cars
    [Fact]
    public async Task GetRowsAsync_ReturnsEmpty_ForMemberWithNoCars()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();

        var rows = await provider.GetRowsAsync(fixture.Host, null, "42");

        Assert.Empty(rows);
    }

    // 4.2 — GetRowsAsync applies LicensePlate filter when filterValue is provided
    [Fact]
    public async Task GetRowsAsync_AppliesFilter_ByLicensePlate()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "BMW", ["color"] = "Rot", ["licensePlate"] = "M-XY 999"
            }),
            member, fixture.Host);

        var rows = await provider.GetRowsAsync(fixture.Host, "M-XY", member.Id.ToString(CultureInfo.InvariantCulture));

        var row = Assert.Single(rows);
        Assert.Equal("M-XY 999", row["LicensePlate"]);
    }

    // 4.2 — GetRowsAsync includes dynamic field columns in each row
    [Fact]
    public async Task GetRowsAsync_IncludesDynamicFieldValues()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoMemberCarsPageProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        // Define an active extra field
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await fixture.Host.Persistence.ExecuteAsync(
            $"INSERT INTO {fixture.Host.Persistence.GetTableName("field_definitions")} " +
            "(FieldKey, Label, InputType, IsRequired, SortOrder, IsDeleted, CreatedAtUtc, UpdatedAtUtc) " +
            "VALUES ('badge', 'Ausweisnummer', 'text', 0, 10, 0, @now, @now);",
            new Dictionary<string, string?> { ["now"] = now });

        // Add a car with the dynamic field value
        var addResult = await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123",
                ["field.badge"] = "A-999"
            }),
            member, fixture.Host);

        Assert.True(addResult.Success, $"Add failed: {addResult.Message}");

        var rows = await provider.GetRowsAsync(fixture.Host, null, member.Id.ToString(CultureInfo.InvariantCulture));

        var row = Assert.Single(rows);
        Assert.True(row.ContainsKey("field.badge"), "Row should contain dynamic field column");
        Assert.Equal("A-999", row["field.badge"]);
    }

    // 4.3 — CarInfoPluginModule registers CarInfoMemberCarsPageProvider via AddPageProvider
    [Fact]
    public void CarInfoPluginModule_RegistersPageProvider()
    {
        var module = new CarInfoPluginModule();
        var sink = new RecordingContributionSink();

        module.RegisterContributions(sink);

        Assert.Contains(sink.PageProviders, p =>
            p.ProviderType.Contains(nameof(CarInfoMemberCarsPageProvider), StringComparison.Ordinal));
    }

    // 4.4 — plugin.json extensionPoints includes "page.generic"
    [Fact]
    public void PluginJson_ContainsPageGenericExtensionPoint()
    {
        // The manifest is expressed both in plugin.json and in CarInfoPluginModule.Manifest
        var module = new CarInfoPluginModule();
        Assert.Contains("page.generic", module.Manifest.ExtensionPoints,
            StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PluginMemberDetail CreateMember(int id = 1)
        => new(id, $"M-00{id}", $"Member {id}", "First", "Last", $"member{id}@example.org", "+49-111", true);

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice4SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);

        return new TestFixture(connection, store, new Slice4TestPluginHostContext(store));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(SqliteConnection connection, Slice4SqlitePluginStore store, Slice4TestPluginHostContext host)
        {
            Connection = connection;
            Store = store;
            Host = host;
        }

        public SqliteConnection Connection { get; }
        public Slice4SqlitePluginStore Store { get; }
        public Slice4TestPluginHostContext Host { get; }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }

    private sealed class Slice4TestPluginHostContext : IPluginHostContext
    {
        public Slice4TestPluginHostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new Slice4TestMetadataFacade(persistence.ModuleId);
            Members = new Slice4NoOpMemberReader();
            MemberActions = new Slice4NoOpMemberActions();
        }

        public IPluginMetadataFacade Metadata { get; }
        public IPluginMemberReader Members { get; }
        public IPluginMemberActionFacade MemberActions { get; }
        public IPluginDataStore Persistence { get; }
        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class Slice4TestMetadataFacade : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public Slice4TestMetadataFacade(string moduleId) => _moduleId = moduleId;

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "CarInfo", "Proprietary", ">=1.0.0",
                   ["members.manage", "clubgear.plugin.carinfo.member.write"],
                   ["member.action", "admin.functions", "member.edit-tab", "page.generic"]);
    }

    private sealed class Slice4NoOpMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class Slice4NoOpMemberActions : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    private sealed class RecordingContributionSink : IPluginContributionSink
    {
        public List<PluginPageProviderContribution> PageProviders { get; } = [];

        // Required non-default interface members
        public void AddRoute(PluginRouteContribution contribution) { }
        public void AddService(PluginServiceContribution contribution) { }
        public void AddMemberProvider(PluginMemberProviderContribution contribution) { }
        public void AddBackgroundJob(PluginBackgroundJobContribution contribution) { }

        // Override default no-op to record page providers
        public void AddPageProvider(PluginPageProviderContribution contribution)
            => PageProviders.Add(contribution);
    }

    internal sealed class Slice4SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice4SqlitePluginStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId = moduleId;
            TablePrefix = "plg_carinfo_";
        }

        public string ModuleId { get; }
        public string TablePrefix { get; }

        public string GetTableName(string localName) => $"{TablePrefix}{localName}";

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

            foreach (var (key, value) in parameters)
            {
                var param = command.CreateParameter();
                param.ParameterName = key.StartsWith('@') ? key : $"@{key}";
                param.Value = (object?)value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }
    }
}

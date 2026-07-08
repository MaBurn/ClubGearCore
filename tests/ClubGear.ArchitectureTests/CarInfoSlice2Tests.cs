using System.Globalization;
using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2 verification:
///   2.1 DeleteCarByIdAsync compiles and performs ownership check
///   2.2 carinfo.delete slot uses carId + clubgear.plugin.carinfo.member.write
///   2.3 ExecuteAsync routes carinfo.delete to DeleteCarByIdAsync
///   2.4 CarInfoEditTabProvider emits [Id] prefix on each car line
/// </summary>
public sealed class CarInfoSlice2Tests
{
    // 2.2 — carinfo.delete slot has carId argument (not licensePlate)
    [Fact]
    public async Task GetActionsAsync_CarinfoDelete_HasCarIdArgument()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        var delete = Assert.Single(actions, slot => slot.Key == "carinfo.delete");
        Assert.NotNull(delete.ArgumentSchema);
        Assert.Contains(delete.ArgumentSchema!, field => field.Key == "carId" && field.Required);
        Assert.DoesNotContain(delete.ArgumentSchema!, field => field.Key == "licensePlate");
    }

    // 2.2 — carinfo.delete slot uses clubgear.plugin.carinfo.member.write permission key
    [Fact]
    public async Task GetActionsAsync_CarinfoDelete_HasMemberWritePermissionKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        var delete = Assert.Single(actions, slot => slot.Key == "carinfo.delete");
        Assert.Equal("clubgear.plugin.carinfo.member.write", delete.PermissionKey);
    }

    // 2.1 + 2.3 — carinfo.delete routes to DeleteCarByIdAsync; successfully deletes own car
    [Fact]
    public async Task ExecuteDelete_ByCarId_DeletesOwnCar()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        // Add a car via the action provider
        var addResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);
        Assert.True(addResult.Success, $"Add failed: {addResult.Message}");

        // Find the inserted car's Id from the cars table
        var carRows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {fixture.Host.Persistence.GetTableName("cars")} WHERE MemberId = @memberId;",
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture) });
        Assert.Single(carRows);
        var carId = carRows[0].Values.GetValueOrDefault("Id")!;

        // Delete by Id
        var deleteResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.delete", new Dictionary<string, string>
            {
                ["carId"] = carId
            }),
            member, fixture.Host);

        Assert.True(deleteResult.Success, $"Delete failed: {deleteResult.Message}");
        Assert.Equal("deleted", deleteResult.Status);

        // Car no longer exists
        var remaining = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {fixture.Host.Persistence.GetTableName("cars")} WHERE MemberId = @memberId;",
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture) });
        Assert.Empty(remaining);
    }

    // 2.1 — ownership check: delete with wrong memberId returns not-found
    [Fact]
    public async Task ExecuteDelete_WithCarIdBelongingToOtherMember_ReturnsNotFound()
    {
        await using var fixture = await CreateFixtureAsync();

        // Add a car for member 1 directly via persistence
        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await fixture.Host.Persistence.ExecuteAsync(
            $"INSERT INTO {carsTable} (MemberId, Make, Color, LicensePlate, CreatedAtUtc, UpdatedAtUtc) VALUES (@memberId, @make, @color, @plate, @now, @now);",
            new Dictionary<string, string?>
            {
                ["memberId"] = "1",
                ["make"] = "BMW",
                ["color"] = "Rot",
                ["plate"] = "M-XY 999",
                ["now"] = now
            });

        var rows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = 1;");
        var carId = rows[0].Values.GetValueOrDefault("Id")!;

        // Try to delete it as member 2
        var memberTwo = new PluginMemberDetail(2, "M-002", "Bob Builder", "Bob", "Builder", "bob@example.org", "+49-222", true);
        var provider = new CarInfoActionProvider();
        var deleteResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(memberTwo.Id, "carinfo.delete", new Dictionary<string, string>
            {
                ["carId"] = carId
            }),
            memberTwo, fixture.Host);

        Assert.False(deleteResult.Success);
        Assert.Equal("not-found", deleteResult.Status);

        // Car is still present for member 1
        var still = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = 1;");
        Assert.Single(still);
    }

    // 2.3 — invalid carId (non-numeric) is rejected with a validation error
    [Fact]
    public async Task ExecuteDelete_WithInvalidCarId_ReturnsValidationError()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.delete", new Dictionary<string, string>
            {
                ["carId"] = "not-a-number"
            }),
            member, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains(result.FieldErrors!, e => e.FieldKey == "carId");
    }

    // 2.3 — missing carId is rejected with a validation error
    [Fact]
    public async Task ExecuteDelete_WithMissingCarId_ReturnsValidationError()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.delete", new Dictionary<string, string>()),
            member, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
    }

    // 2.4 — CarInfoEditTabProvider emits [Id] prefix on each car line
    [Fact]
    public async Task GetTabsAsync_CarLines_IncludeIdPrefix()
    {
        await using var fixture = await CreateFixtureAsync();
        var editTabProvider = new CarInfoEditTabProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        // Add two cars via the action provider
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

        // Get their Ids from the DB
        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var carRows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = @memberId;",
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal(2, carRows.Count);

        var tabs = await editTabProvider.GetTabsAsync(member, fixture.Host);
        var tab = Assert.Single(tabs);
        var text = tab.Content;

        foreach (var row in carRows)
        {
            var id = row.Values.GetValueOrDefault("Id")!;
            Assert.Contains($"[{id}]", text);
        }
    }

    // 2.4 — the [Id] prefix appears before the license plate on the same car line
    [Fact]
    public async Task GetTabsAsync_CarLine_HasIdBeforeLicensePlate()
    {
        await using var fixture = await CreateFixtureAsync();
        var editTabProvider = new CarInfoEditTabProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var carRows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = @memberId;",
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture) });
        var carId = carRows[0].Values.GetValueOrDefault("Id")!;

        var tabs = await editTabProvider.GetTabsAsync(member, fixture.Host);
        var text = Assert.Single(tabs).Content;

        var idIndex = text.IndexOf($"[{carId}]", StringComparison.Ordinal);
        var plateIndex = text.IndexOf("B-AB 123", StringComparison.Ordinal);

        Assert.True(idIndex >= 0, $"[{carId}] not found in tab body");
        Assert.True(plateIndex > idIndex, $"LicensePlate should appear after [{carId}]");
    }

    [Fact]
    public async Task GetTabsAsync_RendersInteractiveVehicleButtonsInsideGroupedTab()
    {
        await using var fixture = await CreateFixtureAsync();
        var editTabProvider = new CarInfoEditTabProvider();
        var actionProvider = new CarInfoActionProvider();
        var member = CreateMember();

        await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "VW", ["color"] = "Weiss", ["licensePlate"] = "K-VW-74H"
            }),
            member, fixture.Host);

        var tab = Assert.Single(await editTabProvider.GetTabsAsync(member, fixture.Host));

        Assert.Equal("fahrzeuge", tab.GroupKey);
        Assert.Equal("Fahrzeuge", tab.GroupTitle);
        Assert.Contains("data-plugin-member-action", tab.Content, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-key=\"carinfo.add\"", tab.Content, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-key=\"carinfo.update\"", tab.Content, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-key=\"carinfo.delete\"", tab.Content, StringComparison.Ordinal);
        Assert.Contains("Bearbeiten", tab.Content, StringComparison.Ordinal);
        Assert.Contains("Fahrzeug hinzuf", tab.Content, StringComparison.Ordinal);
        Assert.Contains("schen", tab.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis: Fuer Write-Operationen", tab.Content, StringComparison.Ordinal);
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice2SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);

        return new TestFixture(connection, store, new Slice2TestPluginHostContext(store));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(SqliteConnection connection, Slice2SqlitePluginStore store, Slice2TestPluginHostContext host)
        {
            Connection = connection;
            Store = store;
            Host = host;
        }

        public SqliteConnection Connection { get; }
        public Slice2SqlitePluginStore Store { get; }
        public Slice2TestPluginHostContext Host { get; }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }

    private sealed class Slice2TestPluginHostContext : IPluginHostContext
    {
        public Slice2TestPluginHostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new Slice2TestMetadataFacade(persistence.ModuleId);
            Members = new Slice2NoOpMemberReader();
            MemberActions = new Slice2NoOpMemberActions();
        }

        public IPluginMetadataFacade Metadata { get; }
        public IPluginMemberReader Members { get; }
        public IPluginMemberActionFacade MemberActions { get; }
        public IPluginDataStore Persistence { get; }
        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class Slice2TestMetadataFacade : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public Slice2TestMetadataFacade(string moduleId) => _moduleId = moduleId;

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "CarInfo", "Proprietary", ">=1.0.0",
                   ["members.manage", "clubgear.plugin.carinfo.member.write"],
                   ["member.action", "admin.functions", "member.edit-tab"]);
    }

    private sealed class Slice2NoOpMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class Slice2NoOpMemberActions : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    internal sealed class Slice2SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice2SqlitePluginStore(SqliteConnection connection, string moduleId)
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

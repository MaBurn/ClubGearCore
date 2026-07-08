using System.Globalization;
using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 3 verification:
///   3.1 UpdateCarAsync compiles, performs ownership check, duplicate-plate guard, and updates fields
///   3.2 carinfo.update slot appears in GetActionsAsync with correct permission key and schema
///   3.3 ExecuteAsync routes carinfo.update to UpdateCarAsync
/// </summary>
public sealed class CarInfoSlice3Tests
{
    // 3.2 — carinfo.update slot is emitted by GetActionsAsync
    [Fact]
    public async Task GetActionsAsync_EmitsCarinfoUpdateSlot()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        Assert.Contains(actions, slot => slot.Key == "carinfo.update");
    }

    // 3.2 — carinfo.update uses clubgear.plugin.carinfo.member.write
    [Fact]
    public async Task GetActionsAsync_CarinfoUpdate_HasMemberWritePermissionKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        var update = Assert.Single(actions, slot => slot.Key == "carinfo.update");
        Assert.Equal("clubgear.plugin.carinfo.member.write", update.PermissionKey);
    }

    // 3.2 — carinfo.update schema includes carId (required), make, color, licensePlate
    [Fact]
    public async Task GetActionsAsync_CarinfoUpdate_HasRequiredArguments()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        var update = Assert.Single(actions, slot => slot.Key == "carinfo.update");
        Assert.NotNull(update.ArgumentSchema);
        var schema = update.ArgumentSchema!;

        Assert.Contains(schema, f => f.Key == "carId" && f.Required);
        Assert.Contains(schema, f => f.Key == "make" && f.Required);
        Assert.Contains(schema, f => f.Key == "color" && f.Required);
        Assert.Contains(schema, f => f.Key == "licensePlate" && f.Required);
    }

    // 3.1 + 3.3 — happy path: update changes make, color, and plate
    [Fact]
    public async Task ExecuteUpdate_ChangesCarFields()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        // Add a car first
        var addResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);
        Assert.True(addResult.Success, $"Add failed: {addResult.Message}");

        var carId = await GetFirstCarIdAsync(fixture, member.Id);

        // Update the car
        var updateResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = carId,
                ["make"] = "BMW",
                ["color"] = "Rot",
                ["licensePlate"] = "M-XY 999"
            }),
            member, fixture.Host);

        Assert.True(updateResult.Success, $"Update failed: {updateResult.Message}");
        Assert.Equal("updated", updateResult.Status);

        // Verify the DB state
        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var rows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Make, Color, LicensePlate FROM {carsTable} WHERE Id = @carId;",
            new Dictionary<string, string?> { ["carId"] = carId });
        var row = Assert.Single(rows);
        Assert.Equal("BMW", row.Values.GetValueOrDefault("Make"));
        Assert.Equal("Rot", row.Values.GetValueOrDefault("Color"));
        Assert.Equal("M-XY 999", row.Values.GetValueOrDefault("LicensePlate"));
    }

    // 3.1 — ownership check: update with wrong memberId returns not-found
    [Fact]
    public async Task ExecuteUpdate_WithCarBelongingToOtherMember_ReturnsNotFound()
    {
        await using var fixture = await CreateFixtureAsync();

        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await fixture.Host.Persistence.ExecuteAsync(
            $"INSERT INTO {carsTable} (MemberId, Make, Color, LicensePlate, CreatedAtUtc, UpdatedAtUtc) VALUES (@memberId, @make, @color, @plate, @now, @now);",
            new Dictionary<string, string?>
            {
                ["memberId"] = "1",
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["plate"] = "B-AB 123",
                ["now"] = now
            });

        var rows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = 1;");
        var carId = rows[0].Values.GetValueOrDefault("Id")!;

        var memberTwo = new PluginMemberDetail(2, "M-002", "Bob Builder", "Bob", "Builder", "bob@example.org", "+49-222", true);
        var provider = new CarInfoActionProvider();

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(memberTwo.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = carId,
                ["make"] = "Hacked",
                ["color"] = "Schwarz",
                ["licensePlate"] = "XX-00 000"
            }),
            memberTwo, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("not-found", result.Status);

        // Original car unchanged
        var unchanged = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Make FROM {carsTable} WHERE Id = @carId;",
            new Dictionary<string, string?> { ["carId"] = carId });
        Assert.Equal("Audi", unchanged[0].Values.GetValueOrDefault("Make"));
    }

    // 3.1 — duplicate plate guard: update rejected when another car of same member has that plate
    [Fact]
    public async Task ExecuteUpdate_WithDuplicatePlateForSameMember_ReturnsDuplicate()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "BMW", ["color"] = "Rot", ["licensePlate"] = "M-XY 999"
            }),
            member, fixture.Host);

        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var carRows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id, LicensePlate FROM {carsTable} WHERE MemberId = @memberId ORDER BY Id;",
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal(2, carRows.Count);

        var firstCarId = carRows[0].Values.GetValueOrDefault("Id")!;

        // Try to change first car's plate to the second car's plate
        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = firstCarId,
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "M-XY 999"   // already used by second car
            }),
            member, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("duplicate", result.Status);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains(result.FieldErrors!, e => e.FieldKey == "licensePlate");
    }

    // 3.1 — same plate as current car is allowed (no false duplicate)
    [Fact]
    public async Task ExecuteUpdate_WithSamePlateAsCurrentCar_Succeeds()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        var carId = await GetFirstCarIdAsync(fixture, member.Id);

        // Update make only, keep same plate
        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = carId,
                ["make"] = "VW",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        Assert.True(result.Success, $"Update failed: {result.Message}");
    }

    // 3.3 — missing carId (non-numeric) returns validation error
    [Fact]
    public async Task ExecuteUpdate_WithInvalidCarId_ReturnsValidationError()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = "not-a-number",
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains(result.FieldErrors!, e => e.FieldKey == "carId");
    }

    // 3.3 — missing carId returns validation error
    [Fact]
    public async Task ExecuteUpdate_WithMissingCarId_ReturnsValidationError()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123"
            }),
            member, fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
    }

    // 3.1 — UpsertFieldValuesAsync is called: extra field value is updated
    [Fact]
    public async Task ExecuteUpdate_WithExtraField_UpsertsSavedValue()
    {
        await using var fixture = await CreateFixtureAsync();
        var adminProvider = new CarInfoAdminPanelProvider();
        var provider = new CarInfoActionProvider();
        var member = CreateMember();

        // Define a field via admin panel
        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest("carinfo.fields", "field.upsert", new Dictionary<string, string>
            {
                ["fieldKey"] = "color-code",
                ["label"] = "Farbcode",
                ["inputType"] = "text"
            }),
            fixture.Host);

        // Add car with the field
        await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.add", new Dictionary<string, string>
            {
                ["make"] = "Audi", ["color"] = "Blau", ["licensePlate"] = "B-AB 123",
                ["field.color-code"] = "001"
            }),
            member, fixture.Host);

        var carId = await GetFirstCarIdAsync(fixture, member.Id);

        // Update the field value
        var updateResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(member.Id, "carinfo.update", new Dictionary<string, string>
            {
                ["carId"] = carId,
                ["make"] = "Audi",
                ["color"] = "Blau",
                ["licensePlate"] = "B-AB 123",
                ["field.color-code"] = "999"
            }),
            member, fixture.Host);

        Assert.True(updateResult.Success, $"Update failed: {updateResult.Message}");

        var valuesTable = fixture.Host.Persistence.GetTableName("field_values");
        var valueRows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Value FROM {valuesTable} WHERE CarId = @carId AND FieldKey = 'color-code';",
            new Dictionary<string, string?> { ["carId"] = carId });
        var row = Assert.Single(valueRows);
        Assert.Equal("999", row.Values.GetValueOrDefault("Value"));
    }

    // helper
    private static async Task<string> GetFirstCarIdAsync(Slice3TestFixture fixture, int memberId)
    {
        var carsTable = fixture.Host.Persistence.GetTableName("cars");
        var rows = await fixture.Host.Persistence.QueryAsync(
            $"SELECT Id FROM {carsTable} WHERE MemberId = @memberId ORDER BY Id LIMIT 1;",
            new Dictionary<string, string?> { ["memberId"] = memberId.ToString(CultureInfo.InvariantCulture) });
        Assert.NotEmpty(rows);
        return rows[0].Values.GetValueOrDefault("Id")!;
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    private static async Task<Slice3TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice3SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);

        return new Slice3TestFixture(connection, store, new Slice3TestPluginHostContext(store));
    }

    private sealed class Slice3TestFixture : IAsyncDisposable
    {
        public Slice3TestFixture(SqliteConnection connection, Slice3SqlitePluginStore store, Slice3TestPluginHostContext host)
        {
            Connection = connection;
            Store = store;
            Host = host;
        }

        public SqliteConnection Connection { get; }
        public Slice3SqlitePluginStore Store { get; }
        public Slice3TestPluginHostContext Host { get; }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }

    private sealed class Slice3TestPluginHostContext : IPluginHostContext
    {
        public Slice3TestPluginHostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new Slice3TestMetadataFacade(persistence.ModuleId);
            Members = new Slice3NoOpMemberReader();
            MemberActions = new Slice3NoOpMemberActions();
        }

        public IPluginMetadataFacade Metadata { get; }
        public IPluginMemberReader Members { get; }
        public IPluginMemberActionFacade MemberActions { get; }
        public IPluginDataStore Persistence { get; }
        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class Slice3TestMetadataFacade : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public Slice3TestMetadataFacade(string moduleId) => _moduleId = moduleId;

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "CarInfo", "Proprietary", ">=1.0.0",
                   ["members.manage", "clubgear.plugin.carinfo.member.write"],
                   ["member.action", "admin.functions", "member.edit-tab"]);
    }

    private sealed class Slice3NoOpMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class Slice3NoOpMemberActions : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    internal sealed class Slice3SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice3SqlitePluginStore(SqliteConnection connection, string moduleId)
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

using System.Globalization;
using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class CarInfoPluginSlice3Tests
{
    [Fact]
    public async Task GetActionsAsync_PublishesSchemaMetadata_AndCorrectPermissionKeys()
    {
        await using var fixture = await CreateFixtureAsync();

        var adminProvider = new CarInfoAdminPanelProvider();
        var actionProvider = new CarInfoActionProvider();

        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "battery_kwh",
                    ["label"] = "Batterie (kWh)",
                    ["inputType"] = "number",
                    ["required"] = "true",
                    ["sortOrder"] = "5",
                    ["min"] = "0",
                    ["max"] = "250"
                }),
            fixture.Host);

        var actions = await actionProvider.GetActionsAsync(CreateMember(), fixture.Host);

        var add = Assert.Single(actions.Where(slot => slot.Key == "carinfo.add"));
        var delete = Assert.Single(actions.Where(slot => slot.Key == "carinfo.delete"));

        Assert.Equal("clubgear.plugin.carinfo.member.write", add.PermissionKey);
        Assert.Equal("clubgear.plugin.carinfo.member.write", delete.PermissionKey);

        // carinfo.field.define is removed; only add and delete slots are emitted
        Assert.DoesNotContain(actions, slot => slot.Key == "carinfo.field.define");

        Assert.NotNull(add.ArgumentSchema);
        var addSchema = add.ArgumentSchema!;
        Assert.Contains(addSchema, field => field.Key == "make" && field.Required);
        Assert.Contains(addSchema, field => field.Key == "color" && field.Required);
        Assert.Contains(addSchema, field => field.Key == "licensePlate" && field.Required);
        Assert.Contains(addSchema, field => field.Key == "field.battery_kwh" && field.InputType == PluginSchemaFieldType.Number);

        Assert.NotNull(delete.ArgumentSchema);
        var deleteSchema = delete.ArgumentSchema!;
        Assert.Single(deleteSchema, field => field.Key == "carId" && field.Required);
    }

    [Fact]
    public async Task ExecuteAdd_ReturnsFieldErrors_ForTypeAndFormatValidationFailures()
    {
        await using var fixture = await CreateFixtureAsync();
        var adminProvider = new CarInfoAdminPanelProvider();
        var actionProvider = new CarInfoActionProvider();

        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "battery_kwh",
                    ["label"] = "Batterie (kWh)",
                    ["inputType"] = "number",
                    ["required"] = "true",
                    ["sortOrder"] = "10",
                    ["min"] = "10",
                    ["max"] = "120"
                }),
            fixture.Host);

        var result = await actionProvider.ExecuteAsync(
            new PluginMemberActionRequest(
                1,
                "carinfo.add",
                new Dictionary<string, string>
                {
                    ["make"] = " ",
                    ["color"] = "",
                    ["licensePlate"] = "@@",
                    ["field.battery_kwh"] = "abc"
                }),
            CreateMember(),
            fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);

        Assert.NotNull(result.FieldErrors);
        var errors = result.FieldErrors!;
        Assert.Contains(errors, error => error.FieldKey == "make" && error.Code == "required");
        Assert.Contains(errors, error => error.FieldKey == "color" && error.Code == "required");
        Assert.Contains(errors, error => error.FieldKey == "licensePlate" && error.Code == "pattern");
        Assert.Contains(errors, error => error.FieldKey == "field.battery_kwh" && error.Code == "invalid-number");
    }

    [Fact]
    public async Task AdminPanelLifecycleCommands_SoftDeleteAndRestore_FieldVisibilityInActionSchema()
    {
        await using var fixture = await CreateFixtureAsync();
        var adminProvider = new CarInfoAdminPanelProvider();
        var actionProvider = new CarInfoActionProvider();

        var panel = Assert.Single(await adminProvider.GetPanelsAsync(fixture.Host));
        Assert.Equal("carinfo.fields", panel.Key);
        Assert.Contains(panel.Commands!, command => command.Key == "field.upsert");
        Assert.Contains(panel.Commands!, command => command.Key == "field.reorder");
        Assert.Contains(panel.Commands!, command => command.Key == "field.delete");
        Assert.Contains(panel.Commands!, command => command.Key == "field.restore");

        var upsertPrimary = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "primary",
                    ["label"] = "Primaer",
                    ["inputType"] = "text",
                    ["sortOrder"] = "20"
                }),
            fixture.Host);
        Assert.True(upsertPrimary.Success);

        var upsertSecondary = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "secondary",
                    ["label"] = "Sekundaer",
                    ["inputType"] = "text",
                    ["sortOrder"] = "10"
                }),
            fixture.Host);
        Assert.True(upsertSecondary.Success);

        var beforeDeleteSchema = Assert.Single((await actionProvider.GetActionsAsync(CreateMember(), fixture.Host)).Where(slot => slot.Key == "carinfo.add")).ArgumentSchema!;
        var secondaryIndex = IndexOf(beforeDeleteSchema, "field.secondary");
        var primaryIndex = IndexOf(beforeDeleteSchema, "field.primary");
        Assert.True(secondaryIndex >= 0);
        Assert.True(primaryIndex > secondaryIndex);

        var deleteResult = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest("carinfo.fields", "field.delete", new Dictionary<string, string> { ["fieldKey"] = "secondary" }),
            fixture.Host);
        Assert.True(deleteResult.Success);

        var afterDeleteSchema = Assert.Single((await actionProvider.GetActionsAsync(CreateMember(), fixture.Host)).Where(slot => slot.Key == "carinfo.add")).ArgumentSchema!;
        Assert.DoesNotContain(afterDeleteSchema, field => field.Key == "field.secondary");

        var restoreResult = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest("carinfo.fields", "field.restore", new Dictionary<string, string> { ["fieldKey"] = "secondary" }),
            fixture.Host);
        Assert.True(restoreResult.Success);

        var reorderResult = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.reorder",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "primary",
                    ["sortOrder"] = "0"
                }),
            fixture.Host);
        Assert.True(reorderResult.Success);

        var afterRestoreSchema = Assert.Single((await actionProvider.GetActionsAsync(CreateMember(), fixture.Host)).Where(slot => slot.Key == "carinfo.add")).ArgumentSchema!;
        Assert.Contains(afterRestoreSchema, field => field.Key == "field.secondary");
        Assert.True(IndexOf(afterRestoreSchema, "field.primary") < IndexOf(afterRestoreSchema, "field.secondary"));
    }

    [Fact]
    public async Task AdminPanel_GetPanelsAsync_ExposesFieldItems_WithActiveAndDeletedState()
    {
        await using var fixture = await CreateFixtureAsync();
        var adminProvider = new CarInfoAdminPanelProvider();

        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "battery_kwh",
                    ["label"] = "Batterie (kWh)",
                    ["inputType"] = "number",
                    ["required"] = "true",
                    ["sortOrder"] = "5",
                    ["min"] = "0",
                    ["max"] = "250"
                }),
            fixture.Host);

        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.upsert",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "inspection_date",
                    ["label"] = "Pruefdatum",
                    ["inputType"] = "date",
                    ["sortOrder"] = "10"
                }),
            fixture.Host);

        await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest(
                "carinfo.fields",
                "field.delete",
                new Dictionary<string, string>
                {
                    ["fieldKey"] = "inspection_date"
                }),
            fixture.Host);

        var panel = Assert.Single(await adminProvider.GetPanelsAsync(fixture.Host));
        Assert.NotNull(panel.Items);
        var items = panel.Items!;
        Assert.Equal(2, items.Count);

        var battery = Assert.Single(items.Where(item => item.Key == "battery_kwh"));
        Assert.Equal("active", battery.State);
        Assert.Equal("number", battery.Values!["inputType"]);
        Assert.Equal("true", battery.Values["required"]);

        var inspection = Assert.Single(items.Where(item => item.Key == "inspection_date"));
        Assert.Equal("deleted", inspection.State);
        Assert.Equal("date", inspection.Values!["inputType"]);
    }

    [Fact]
    public async Task LegacyMigrationAndSoftDelete_PreserveHistoricalFieldValues()
    {
        await using var fixture = await CreateFixtureWithLegacySchemaAsync();

        var fieldTable = fixture.Store.GetTableName("field_definitions");
        var carsTable = fixture.Store.GetTableName("cars");
        var valuesTable = fixture.Store.GetTableName("field_values");

        await fixture.Store.ExecuteAsync(
            $@"INSERT INTO {fieldTable} (FieldKey, Label, InputType, IsRequired, SortOrder, CreatedAtUtc, UpdatedAtUtc)
               VALUES ('legacy_range', 'Legacy Range', 'number', 0, 10, @now, @now);",
            new Dictionary<string, string?> { ["now"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) });

        await fixture.Store.ExecuteAsync(
            $@"INSERT INTO {carsTable} (MemberId, Make, Color, LicensePlate, CreatedAtUtc, UpdatedAtUtc)
               VALUES (1, 'VW', 'Blau', 'B-LEG 1', @now, @now);",
            new Dictionary<string, string?> { ["now"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) });

        await fixture.Store.ExecuteAsync(
            $@"INSERT INTO {valuesTable} (CarId, FieldKey, Value, CreatedAtUtc, UpdatedAtUtc)
               VALUES (1, 'legacy_range', '300', @now, @now);",
            new Dictionary<string, string?> { ["now"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) });

        await new CarInfoFieldLifecycleMigration().ApplyAsync(fixture.Store);

        var adminProvider = new CarInfoAdminPanelProvider();
        var actionProvider = new CarInfoActionProvider();

        var deleteResult = await adminProvider.ExecuteCommandAsync(
            new PluginAdminCommandRequest("carinfo.fields", "field.delete", new Dictionary<string, string> { ["fieldKey"] = "legacy_range" }),
            fixture.Host);
        Assert.True(deleteResult.Success);

        var addSchema = Assert.Single((await actionProvider.GetActionsAsync(CreateMember(), fixture.Host)).Where(slot => slot.Key == "carinfo.add")).ArgumentSchema!;
        Assert.DoesNotContain(addSchema, field => field.Key == "field.legacy_range");

        var historicalRows = await fixture.Store.QueryAsync($"SELECT Value FROM {valuesTable} WHERE FieldKey = 'legacy_range';");
        Assert.Single(historicalRows);
        Assert.Equal("300", historicalRows[0].Values["Value"]);
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    private static int IndexOf(IReadOnlyList<PluginFieldSchema> fields, string key)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);

        return new TestFixture(connection, store, new TestPluginHostContext(store));
    }

    private static async Task<TestFixture> CreateFixtureWithLegacySchemaAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        var carsTable = store.GetTableName("cars");
        var fieldTable = store.GetTableName("field_definitions");
        var valuesTable = store.GetTableName("field_values");

        await store.ExecuteAsync(
            $@"CREATE TABLE {carsTable} (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                MemberId INTEGER NOT NULL,
                Make TEXT NOT NULL,
                Color TEXT NOT NULL,
                LicensePlate TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );");

        await store.ExecuteAsync(
            $@"CREATE TABLE {fieldTable} (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                FieldKey TEXT NOT NULL,
                Label TEXT NOT NULL,
                InputType TEXT NOT NULL,
                IsRequired INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );");

        await store.ExecuteAsync(
            $@"CREATE TABLE {valuesTable} (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                CarId INTEGER NOT NULL,
                FieldKey TEXT NOT NULL,
                Value TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );");

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
            => new(_moduleId, "CarInfo", "Proprietary", ">=1.0.0", ["members.manage"], ["member.action", "admin.functions"]);
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
            TablePrefix = "plg_carinfo_";
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
}

using System.Globalization;
using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.ServiceBook;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class ServiceBookPluginTests
{
    [Fact]
    public void Manifest_AndContributions_AreMemberProfileScoped()
    {
        var module = new ServiceBookPluginModule();
        var sink = new RecordingContributionSink();

        module.RegisterContributions(sink);

        Assert.Equal("clubgear.plugin.servicebook", module.Manifest.ModuleId);
        Assert.Equal(new Version(1, 0, 0), module.Manifest.PluginVersion);
        Assert.Contains("clubgear.plugin.servicebook.member.write", module.Manifest.Permissions);
        Assert.Contains("member.edit", module.Manifest.ExtensionPoints);
        Assert.Contains("selfservice.profile", module.Manifest.ExtensionPoints);
        Assert.Equal(3, sink.MemberProviders.Count);
        Assert.Contains(sink.MemberProviders, provider => provider.SlotKind == PluginMemberSlotKind.EditTab);
        Assert.Contains(sink.MemberProviders, provider => provider.SlotKind == PluginMemberSlotKind.Action);
        Assert.Contains(sink.MemberProviders, provider => provider.SlotKind == PluginMemberSlotKind.DetailCard);
    }

    [Fact]
    public async Task Actions_CreateUpdateAndDelete_RecordWithParts()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new ServiceBookActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);
        Assert.Equal(3, actions.Count);
        Assert.All(actions, action =>
            Assert.Equal("clubgear.plugin.servicebook.member.write", action.PermissionKey));

        var createResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(
                1,
                "servicebook.record.add",
                BuildArguments(
                    title: "Ölwechsel",
                    partsJson: """[{"name":"Ölfilter","quantity":1,"unit":"Stk","unitPrice":12.5}]""")),
            CreateMember(),
            fixture.Host);

        Assert.True(createResult.Success);

        var recordsTable = fixture.Store.GetTableName("service_records");
        var partsTable = fixture.Store.GetTableName("service_parts");
        var recordRows = await fixture.Store.QueryAsync($"SELECT Id, Title, MemberId, CarId FROM {recordsTable};");
        var record = Assert.Single(recordRows);
        Assert.Equal("Ölwechsel", record.Values["Title"]);
        Assert.Equal("1", record.Values["MemberId"]);
        Assert.Equal("42", record.Values["CarId"]);

        var partRows = await fixture.Store.QueryAsync($"SELECT Name, Quantity, UnitPrice FROM {partsTable};");
        Assert.Equal("Ölfilter", Assert.Single(partRows).Values["Name"]);

        var recordId = record.Values["Id"]!;
        var updateArguments = BuildArguments(
            title: "Große Inspektion",
            partsJson: """[{"name":"Motoröl","quantity":5,"unit":"Liter","unitPrice":9.9}]""");
        updateArguments["recordId"] = recordId;

        var updateResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(1, "servicebook.record.update", updateArguments),
            CreateMember(),
            fixture.Host);

        Assert.True(updateResult.Success);
        var updatedRows = await fixture.Store.QueryAsync($"SELECT Title FROM {recordsTable} WHERE Id = @id;", new Dictionary<string, string?> { ["id"] = recordId });
        Assert.Equal("Große Inspektion", Assert.Single(updatedRows).Values["Title"]);
        var updatedParts = await fixture.Store.QueryAsync($"SELECT Name FROM {partsTable};");
        Assert.Equal("Motoröl", Assert.Single(updatedParts).Values["Name"]);

        var tabs = await new ServiceBookEditTabProvider().GetTabsAsync(CreateMember(), fixture.Host);
        var tab = Assert.Single(tabs);
        Assert.Contains("data-servicebook-root", tab.Content, StringComparison.Ordinal);
        Assert.Contains("Inspektion", tab.Content, StringComparison.Ordinal);
        Assert.Contains("data-carinfo-vehicles", tab.Content, StringComparison.Ordinal);

        var deleteResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(
                1,
                "servicebook.record.delete",
                new Dictionary<string, string> { ["recordId"] = recordId }),
            CreateMember(),
            fixture.Host);

        Assert.True(deleteResult.Success);
        Assert.Empty(await fixture.Store.QueryAsync($"SELECT Id FROM {recordsTable};"));
        Assert.Empty(await fixture.Store.QueryAsync($"SELECT Id FROM {partsTable};"));
    }

    [Fact]
    public async Task Delete_RejectsRecordOwnedByAnotherMember()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new ServiceBookActionProvider();

        var createResult = await provider.ExecuteAsync(
            new PluginMemberActionRequest(1, "servicebook.record.add", BuildArguments("Wartung", "[]")),
            CreateMember(),
            fixture.Host);
        Assert.True(createResult.Success);

        var recordId = Assert.Single(await fixture.Store.QueryAsync(
            $"SELECT Id FROM {fixture.Store.GetTableName("service_records")};")).Values["Id"]!;

        var result = await provider.ExecuteAsync(
            new PluginMemberActionRequest(
                2,
                "servicebook.record.delete",
                new Dictionary<string, string> { ["recordId"] = recordId }),
            new PluginMemberDetail(2, "M-002", "Other Member", "Other", "Member", null, null, true),
            fixture.Host);

        Assert.False(result.Success);
        Assert.Equal("not-found", result.Status);
    }

    [Fact]
    public void CarInfo_ExposesMachineReadableVehicleData_Island()
    {
        var source = File.ReadAllText(GetProjectFilePath("plugins", "CarInfo", "CarInfoProviders.cgcs"));
        var manifest = File.ReadAllText(GetProjectFilePath("plugins", "CarInfo", "plugin.json"));

        Assert.Contains("data-carinfo-vehicles", source, StringComparison.Ordinal);
        Assert.Contains("SerializeVehiclesForHtmlAttribute", source, StringComparison.Ordinal);
        Assert.Contains("\"version\": \"1.0.5\"", manifest, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> BuildArguments(string title, string partsJson)
    {
        return new Dictionary<string, string>
        {
            ["carId"] = "42",
            ["carMake"] = "Audi",
            ["carColor"] = "Blau",
            ["licensePlate"] = "B-AB 123",
            ["type"] = "Wartung",
            ["serviceDate"] = "2026-06-18",
            ["mileage"] = "125000",
            ["title"] = title,
            ["description"] = "Regelmäßiger Service",
            ["workshop"] = "Werkstatt Mustermann",
            ["laborCost"] = "89.50",
            ["nextServiceDate"] = "2027-06-18",
            ["nextServiceMileage"] = "140000",
            ["partsJson"] = partsJson
        };
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var store = new SqlitePluginStore(connection);
        await new ServiceBookSchemaMigration().ApplyAsync(store);
        return new TestFixture(connection, store, new TestPluginHostContext(store));
    }

    private static string GetProjectFilePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var projectPath = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(projectPath))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel wurde nicht gefunden.");
    }

    private sealed class RecordingContributionSink : IPluginContributionSink
    {
        public List<PluginMemberProviderContribution> MemberProviders { get; } = [];

        public void AddRoute(PluginRouteContribution contribution) { }

        public void AddService(PluginServiceContribution contribution) { }

        public void AddMemberProvider(PluginMemberProviderContribution contribution)
            => MemberProviders.Add(contribution);

        public void AddBackgroundJob(PluginBackgroundJobContribution contribution) { }
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
            => await Connection.DisposeAsync();
    }

    private sealed class TestPluginHostContext : IPluginHostContext
    {
        public TestPluginHostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new TestMetadataFacade();
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
        public PluginHostMetadata GetCurrent()
            => new(
                "clubgear.plugin.servicebook",
                "Serviceheft",
                "Proprietary",
                ">=1.0.0",
                ["clubgear.plugin.servicebook.member.write"],
                ["member.detail", "member.edit", "member.action", "selfservice.profile"]);
    }

    private sealed class NoOpMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(
            string? search = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(
            int memberId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class NoOpMemberActions : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(
            PluginMemberActionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    private sealed class SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public SqlitePluginStore(SqliteConnection connection)
        {
            _connection = connection;
        }

        public string ModuleId => "clubgear.plugin.servicebook";

        public string TablePrefix => "plg_servicebook_";

        public string GetTableName(string localName)
            => $"{TablePrefix}{localName}";

        public async Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command, parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PluginDataRow>> QueryAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command, parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<PluginDataRow>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var value = reader.IsDBNull(index) ? null : reader.GetValue(index);
                    values[reader.GetName(index)] = value?.ToString();
                }

                rows.Add(new PluginDataRow(values));
            }

            return rows;
        }

        private static void BindParameters(
            SqliteCommand command,
            IReadOnlyDictionary<string, string?>? parameters)
        {
            if (parameters is null)
            {
                return;
            }

            foreach (var pair in parameters)
            {
                var name = pair.Key.StartsWith("@", StringComparison.Ordinal)
                    ? pair.Key
                    : $"@{pair.Key}";
                command.Parameters.AddWithValue(name, pair.Value ?? (object)DBNull.Value);
            }
        }
    }
}

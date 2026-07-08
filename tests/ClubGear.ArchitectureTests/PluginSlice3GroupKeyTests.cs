using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.ServiceBook;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 3 verification:
///   3.1 CarInfoEditTabProvider.GetTabsAsync returns a slot with GroupKey == "fahrzeuge" and GroupTitle == "Fahrzeuge"
///   3.2 ServiceBookEditTabProvider.GetTabsAsync returns a slot with GroupKey == "fahrzeuge" and GroupTitle == "Fahrzeuge"
/// </summary>
public sealed class PluginSlice3GroupKeyTests
{
    // 3.1 — CarInfoEditTabProvider returns GroupKey == "fahrzeuge"
    [Fact]
    public async Task CarInfoEditTab_GetTabsAsync_ReturnsSlotWithCorrectGroupKey()
    {
        await using var fixture = await CreateCarInfoFixtureAsync();
        var provider = new CarInfoEditTabProvider();
        var member = CreateMember();

        var tabs = await provider.GetTabsAsync(member, fixture.Host);

        var slot = Assert.Single(tabs);
        Assert.Equal("fahrzeuge", slot.GroupKey);
    }

    // 3.1 — CarInfoEditTabProvider returns GroupTitle == "Fahrzeuge"
    [Fact]
    public async Task CarInfoEditTab_GetTabsAsync_ReturnsSlotWithCorrectGroupTitle()
    {
        await using var fixture = await CreateCarInfoFixtureAsync();
        var provider = new CarInfoEditTabProvider();
        var member = CreateMember();

        var tabs = await provider.GetTabsAsync(member, fixture.Host);

        var slot = Assert.Single(tabs);
        Assert.Equal("Fahrzeuge", slot.GroupTitle);
    }

    // 3.2 — ServiceBookEditTabProvider returns GroupKey == "fahrzeuge"
    [Fact]
    public async Task ServiceBookEditTab_GetTabsAsync_ReturnsSlotWithCorrectGroupKey()
    {
        await using var fixture = await CreateServiceBookFixtureAsync();
        var provider = new ServiceBookEditTabProvider();
        var member = CreateMember();

        var tabs = await provider.GetTabsAsync(member, fixture.Host);

        var slot = Assert.Single(tabs);
        Assert.Equal("fahrzeuge", slot.GroupKey);
    }

    // 3.2 — ServiceBookEditTabProvider returns GroupTitle == "Fahrzeuge"
    [Fact]
    public async Task ServiceBookEditTab_GetTabsAsync_ReturnsSlotWithCorrectGroupTitle()
    {
        await using var fixture = await CreateServiceBookFixtureAsync();
        var provider = new ServiceBookEditTabProvider();
        var member = CreateMember();

        var tabs = await provider.GetTabsAsync(member, fixture.Host);

        var slot = Assert.Single(tabs);
        Assert.Equal("Fahrzeuge", slot.GroupTitle);
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    // -----------------------------------------------------------------------
    // CarInfo fixture
    // -----------------------------------------------------------------------

    private static async Task<CarInfoTestFixture> CreateCarInfoFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var store = new Slice3CarInfoStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);
        return new CarInfoTestFixture(connection, new Slice3HostContext(store));
    }

    private sealed class CarInfoTestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public CarInfoTestFixture(SqliteConnection connection, Slice3HostContext host)
        {
            _connection = connection;
            Host = host;
        }

        public Slice3HostContext Host { get; }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // ServiceBook fixture
    // -----------------------------------------------------------------------

    private static async Task<ServiceBookTestFixture> CreateServiceBookFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var store = new Slice3ServiceBookStore(connection, "clubgear.plugin.servicebook");
        await new ServiceBookSchemaMigration().ApplyAsync(store);
        return new ServiceBookTestFixture(connection, new Slice3HostContext(store));
    }

    private sealed class ServiceBookTestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public ServiceBookTestFixture(SqliteConnection connection, Slice3HostContext host)
        {
            _connection = connection;
            Host = host;
        }

        public Slice3HostContext Host { get; }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Shared host context
    // -----------------------------------------------------------------------

    internal sealed class Slice3HostContext : IPluginHostContext
    {
        public Slice3HostContext(IPluginDataStore persistence)
        {
            Persistence = persistence;
            Metadata = new Slice3MetadataFacade(persistence.ModuleId);
            Members = new Slice3NoOpMemberReader();
            MemberActions = new Slice3NoOpMemberActions();
        }

        public IPluginMetadataFacade Metadata { get; }
        public IPluginMemberReader Members { get; }
        public IPluginMemberActionFacade MemberActions { get; }
        public IPluginDataStore Persistence { get; }
        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class Slice3MetadataFacade : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public Slice3MetadataFacade(string moduleId) => _moduleId = moduleId;

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "Test", "Proprietary", ">=1.0.0", [], []);
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

    // -----------------------------------------------------------------------
    // SQLite stores
    // -----------------------------------------------------------------------

    internal sealed class Slice3CarInfoStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice3CarInfoStore(SqliteConnection connection, string moduleId)
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
            if (parameters is null) return;
            foreach (var (key, value) in parameters)
            {
                var param = command.CreateParameter();
                param.ParameterName = key.StartsWith('@') ? key : $"@{key}";
                param.Value = (object?)value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }
    }

    internal sealed class Slice3ServiceBookStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice3ServiceBookStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId = moduleId;
            TablePrefix = "plg_servicebook_";
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
            if (parameters is null) return;
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

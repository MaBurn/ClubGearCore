using System.Security.Claims;
using ClubGear.Plugin.CarInfo;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 1 verification: permission key migration for carinfo.add, derived permission grant,
/// and removal of carinfo.field.define action slot.
/// </summary>
public sealed class CarInfoSlice1Tests
{
    // 1.1 — carinfo.add uses "clubgear.plugin.carinfo.member.write"
    [Fact]
    public async Task GetActionsAsync_CarinfoAdd_HasMemberWritePermissionKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        var add = Assert.Single(actions, slot => slot.Key == "carinfo.add");
        Assert.Equal("clubgear.plugin.carinfo.member.write", add.PermissionKey);
    }

    // 1.3 — carinfo.field.define slot is not emitted by GetActionsAsync
    [Fact]
    public async Task GetActionsAsync_DoesNotEmit_CarinfoFieldDefineSlot()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = new CarInfoActionProvider();

        var actions = await provider.GetActionsAsync(CreateMember(), fixture.Host);

        Assert.DoesNotContain(actions, slot => slot.Key == "carinfo.field.define");
    }

    // 1.2 — derived grant: holders of selfservice.profile.edit are granted *.member.write
    [Fact]
    public async Task ExtensionPermissionFacade_GrantsMemberWrite_ToSelfServiceProfileEditHolder()
    {
        var user = CreateUserWithPermission(PermissionKeys.SelfServiceProfileEdit);
        var facade = CreateFacade();
        var declaredPermissions = new[] { "clubgear.plugin.carinfo.member.write" };

        var result = await facade.HasPermissionAsync(user, "clubgear.plugin.carinfo.member.write", declaredPermissions);

        Assert.True(result, "User holding selfservice.profile.edit should be granted clubgear.plugin.carinfo.member.write via derived grant.");
    }

    // 1.2 — derived grant: holders of members.manage are also granted *.member.write
    [Fact]
    public async Task ExtensionPermissionFacade_GrantsMemberWrite_ToMembersManageHolder()
    {
        var user = CreateUserWithPermission(PermissionKeys.MembersManage);
        var facade = CreateFacade();
        var declaredPermissions = new[] { "clubgear.plugin.carinfo.member.write" };

        var result = await facade.HasPermissionAsync(user, "clubgear.plugin.carinfo.member.write", declaredPermissions);

        Assert.True(result, "User holding members.manage should be granted clubgear.plugin.carinfo.member.write via derived grant.");
    }

    // 1.2 — no grant for users without any qualifying permission
    [Fact]
    public async Task ExtensionPermissionFacade_DeniesAccess_WhenNoQualifyingPermission()
    {
        var user = CreateUserWithPermission("some.other.permission");
        var facade = CreateFacade();
        var declaredPermissions = new[] { "clubgear.plugin.carinfo.member.write" };

        var result = await facade.HasPermissionAsync(user, "clubgear.plugin.carinfo.member.write", declaredPermissions);

        Assert.False(result);
    }

    // 1.2 — undeclared permission in manifest is rejected regardless of grant
    [Fact]
    public async Task ExtensionPermissionFacade_DeniesAccess_WhenPermissionNotInManifest()
    {
        var user = CreateUserWithPermission(PermissionKeys.SelfServiceProfileEdit);
        var facade = CreateFacade();
        var declaredPermissions = Array.Empty<string>(); // not declared

        var result = await facade.HasPermissionAsync(user, "clubgear.plugin.carinfo.member.write", declaredPermissions);

        Assert.False(result);
    }

    private static ClaimsPrincipal CreateUserWithPermission(string permissionKey)
    {
        var identity = new ClaimsIdentity([new Claim("permission", permissionKey)], authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ExtensionPermissionFacade CreateFacade()
    {
        return new ExtensionPermissionFacade(
            new ClaimsPermissionService(),
            NullLogger<ExtensionPermissionFacade>.Instance);
    }

    private static PluginMemberDetail CreateMember()
        => new(1, "M-001", "Ada Lovelace", "Ada", "Lovelace", "ada@example.org", "+49-111", true);

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new SqlitePluginStore(connection, "clubgear.plugin.carinfo");
        await new CarInfoSchemaMigration().ApplyAsync(store);
        await new CarInfoFieldLifecycleMigration().ApplyAsync(store);

        return new TestFixture(connection, store, new TestPluginHostContext(store));
    }

    /// <summary>
    /// Simple in-memory permission service that reads permissions from "permission" claims.
    /// Used so tests do not need a database or real DatabasePermissionService.
    /// </summary>
    private sealed class ClaimsPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
        {
            var result = user.Claims.Any(c =>
                string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(c.Value, permissionKey, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, PermissionKeys.Wildcard, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(result);
        }
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

        public TestMetadataFacade(string moduleId) => _moduleId = moduleId;

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "CarInfo", "Proprietary", ">=1.0.0",
                   ["members.manage", "clubgear.plugin.carinfo.member.write"],
                   ["member.action", "admin.functions"]);
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

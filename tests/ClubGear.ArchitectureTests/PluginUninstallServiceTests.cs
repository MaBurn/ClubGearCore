using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Uninstall;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginUninstallServiceTests
{
    // ── 7.1a — Delete without data ────────────────────────────────────────────

    [Fact]
    public async Task UninstallAsync_WithoutRemoveData_CallsDeactivateStatusAndPackageDelete_ButLeavesDataUntouched()
    {
        await using var fixture = await CreateFixtureAsync();

        // Seed an inactive plugin record
        await fixture.StatusStore.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.nodelete",
            DisplayName = "No Delete Plugin",
            Version = "1.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.NoDelete",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "AABBCC",
            PackagePath = "/tmp/plugin.nodelete.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]"
        });

        // Seed a migration row so we can confirm it is NOT removed
        fixture.DbContext.PluginMigrationStates.Add(new PluginMigrationState
        {
            PluginKey = "plugin.nodelete",
            MigrationId = "m001",
            TablePrefix = "nd_"
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Sut.UninstallAsync("plugin.nodelete", removeData: false);

        // Success
        Assert.True(result.Success);
        Assert.Equal("uninstalled", result.Status);

        // DeactivateAsync called exactly once
        Assert.Equal(1, fixture.LifecycleService.DeactivateCalls.Count);
        Assert.Equal("plugin.nodelete", fixture.LifecycleService.DeactivateCalls[0]);

        // Status record removed from DB
        var statusRecord = await fixture.DbContext.PluginStatusRecords
            .FirstOrDefaultAsync(r => r.Key == "plugin.nodelete");
        Assert.Null(statusRecord);

        // Package store DeleteAsync called exactly once
        Assert.Equal(1, fixture.PackageStore.DeleteCalls.Count);
        Assert.Equal("plugin.nodelete", fixture.PackageStore.DeleteCalls[0]);

        // Migration row is NOT removed
        var migrationRows = await fixture.DbContext.PluginMigrationStates
            .Where(s => s.PluginKey == "plugin.nodelete")
            .CountAsync();
        Assert.Equal(1, migrationRows);
    }

    // ── 7.1b — Delete with data ───────────────────────────────────────────────

    [Fact]
    public async Task UninstallAsync_WithRemoveData_DropsTablesAndRemovesMigrationRows()
    {
        await using var fixture = await CreateFixtureAsync();

        // Seed an inactive plugin record
        await fixture.StatusStore.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.withdata",
            DisplayName = "Data Plugin",
            Version = "2.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.WithData",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "DDDDDD",
            PackagePath = "/tmp/plugin.withdata.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]"
        });

        // Create the plugin's table in the SQLite database and seed migration rows
        await fixture.DbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"wd_notes\" (Id INTEGER PRIMARY KEY)");

        fixture.DbContext.PluginMigrationStates.AddRange(
            new PluginMigrationState { PluginKey = "plugin.withdata", MigrationId = "m001", TablePrefix = "wd_" },
            new PluginMigrationState { PluginKey = "plugin.withdata", MigrationId = "m002", TablePrefix = "wd_" });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Sut.UninstallAsync("plugin.withdata", removeData: true);

        // Success
        Assert.True(result.Success);
        Assert.Equal("uninstalled", result.Status);

        // DeactivateAsync called exactly once
        Assert.Equal(1, fixture.LifecycleService.DeactivateCalls.Count);
        Assert.Equal("plugin.withdata", fixture.LifecycleService.DeactivateCalls[0]);

        // Status record removed from DB
        var statusRecord = await fixture.DbContext.PluginStatusRecords
            .FirstOrDefaultAsync(r => r.Key == "plugin.withdata");
        Assert.Null(statusRecord);

        // Package store DeleteAsync called exactly once
        Assert.Equal(1, fixture.PackageStore.DeleteCalls.Count);
        Assert.Equal("plugin.withdata", fixture.PackageStore.DeleteCalls[0]);

        // Migration rows removed
        var migrationRows = await fixture.DbContext.PluginMigrationStates
            .Where(s => s.PluginKey == "plugin.withdata")
            .CountAsync();
        Assert.Equal(0, migrationRows);

        // Table dropped via sqlite_master
        var tableExists = await fixture.DbContext.Database
            .SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='wd_notes'")
            .ToListAsync();
        Assert.Empty(tableExists);
    }

    // ── 7.1c — Guard: active plugin ──────────────────────────────────────────

    [Fact]
    public async Task UninstallAsync_ForActivePlugin_ReturnsFailureWithoutCallingDeleteMethods()
    {
        await using var fixture = await CreateFixtureAsync();

        // Seed an ACTIVE plugin record
        await fixture.StatusStore.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.active",
            DisplayName = "Active Plugin",
            Version = "1.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.Active",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "EEEEEE",
            PackagePath = "/tmp/plugin.active.zip",
            IsActive = true,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]"
        });

        var result = await fixture.Sut.UninstallAsync("plugin.active", removeData: false);

        // Failure result
        Assert.False(result.Success);
        Assert.Equal("still-active", result.Status);

        // No delete calls made
        Assert.Empty(fixture.StatusStore.DeleteCalls);
        Assert.Empty(fixture.PackageStore.DeleteCalls);

        // DeactivateAsync not called
        Assert.Empty(fixture.LifecycleService.DeactivateCalls);
    }

    // ── 3.2a — Plugin permission rows are removed on uninstall ───────────────

    [Fact]
    public async Task UninstallAsync_RemovesAppPermissionAndRolePermissionRows_ForPluginKeys()
    {
        await using var fixture = await CreateFixtureAsync();

        const string pluginKey = "plugin.withperms";
        const string permKey = "plugin.withperms.view";
        const string coreKey = "admin.access";

        // Seed the plugin record with one non-core permission
        await fixture.StatusStore.UpsertAsync(new PluginStatusRecord
        {
            Key = pluginKey,
            DisplayName = "Perms Plugin",
            Version = "1.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.WithPerms",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "FFFFFF",
            PackagePath = "/tmp/plugin.withperms.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = $"[\"{permKey}\"]",
            ExtensionPointsJson = "[]"
        });

        // Seed AppPermission rows: one plugin permission + one core permission
        fixture.DbContext.Permissions.Add(new AppPermission { Key = permKey, Description = "View", Category = "General" });
        fixture.DbContext.Permissions.Add(new AppPermission { Key = coreKey, Description = "Core", Category = "Administration" });

        // Seed AppRolePermission rows: one for plugin perm + one for core perm
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "member", PermissionKey = permKey });
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "admin", PermissionKey = coreKey });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Sut.UninstallAsync(pluginKey, removeData: false);

        Assert.True(result.Success);

        // Plugin permission row removed
        var pluginPerm = await fixture.DbContext.Permissions
            .FirstOrDefaultAsync(p => p.Key == permKey);
        Assert.Null(pluginPerm);

        // Role-permission row for plugin key removed
        var pluginRolePerm = await fixture.DbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.PermissionKey == permKey);
        Assert.Null(pluginRolePerm);

        // Core permission row untouched
        var corePerm = await fixture.DbContext.Permissions
            .FirstOrDefaultAsync(p => p.Key == coreKey);
        Assert.NotNull(corePerm);

        // Core role-permission row untouched
        var coreRolePerm = await fixture.DbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.PermissionKey == coreKey);
        Assert.NotNull(coreRolePerm);
    }

    // ── 3.2b — Core permission rows are untouched ────────────────────────────

    [Fact]
    public async Task UninstallAsync_DoesNotRemove_CorePermissionRows()
    {
        await using var fixture = await CreateFixtureAsync();

        const string pluginKey = "plugin.coreonly";
        // Plugin declares only a core permission key — should be excluded from removal
        const string corePermKey = "members.read";

        await fixture.StatusStore.UpsertAsync(new PluginStatusRecord
        {
            Key = pluginKey,
            DisplayName = "Core-Only Plugin",
            Version = "1.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.CoreOnly",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "CCCCCC",
            PackagePath = "/tmp/plugin.coreonly.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = $"[\"{corePermKey}\"]",
            ExtensionPointsJson = "[]"
        });

        fixture.DbContext.Permissions.Add(new AppPermission { Key = corePermKey, Description = "Members Read", Category = "Members" });
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "viewer", PermissionKey = corePermKey });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Sut.UninstallAsync(pluginKey, removeData: false);

        Assert.True(result.Success);

        // Core permission row must remain intact
        var corePerm = await fixture.DbContext.Permissions
            .FirstOrDefaultAsync(p => p.Key == corePermKey);
        Assert.NotNull(corePerm);

        // Core role-permission row must remain intact
        var coreRolePerm = await fixture.DbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.PermissionKey == corePermKey);
        Assert.NotNull(coreRolePerm);
    }

    // ── 3.2c — SaveChangesAsync failure rolls back the whole operation ────────

    [Fact]
    public async Task UninstallAsync_WhenSaveChangesThrows_RollsBackEntireOperation()
    {
        // Arrange: seed data via a normal DbContext, then build the SUT with a failing one
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var seedCtx = new ApplicationDbContext(options);
        await seedCtx.Database.EnsureCreatedAsync();

        const string pluginKey = "plugin.rollback";
        const string permKey = "plugin.rollback.edit";

        // Seed status record
        seedCtx.PluginStatusRecords.Add(new PluginStatusRecord
        {
            Key = pluginKey,
            DisplayName = "Rollback Plugin",
            Version = "1.0.0",
            Author = "Tests",
            License = "MIT",
            EntryPoint = "Plugin.Rollback",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "BBBBBB",
            PackagePath = "/tmp/plugin.rollback.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = $"[\"{permKey}\"]",
            ExtensionPointsJson = "[]"
        });
        seedCtx.Permissions.Add(new AppPermission { Key = permKey, Description = "Edit", Category = "General" });
        seedCtx.RolePermissions.Add(new AppRolePermission { RoleName = "editor", PermissionKey = permKey });
        await seedCtx.SaveChangesAsync();

        // Build the failing DbContext on the same connection
        await using var failCtx = new FailOnSaveDbContext(options);
        var statusStore = new DirectReadPluginStatusStore(failCtx);
        var lifecycleService = new TrackingPluginLifecycleService();
        var packageStore = new TrackingPluginPackageStore();

        var sut = new PluginUninstallService(
            statusStore,
            lifecycleService,
            packageStore,
            failCtx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginUninstallService>.Instance);

        // Act: the save will throw inside the transaction
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UninstallAsync(pluginKey, removeData: false));

        // Assert: status record still present (rollback succeeded)
        await using var verifyCtx = new ApplicationDbContext(options);
        var statusRow = await verifyCtx.PluginStatusRecords
            .FirstOrDefaultAsync(r => r.Key == pluginKey);
        Assert.NotNull(statusRow);

        // Permission row still present
        var permRow = await verifyCtx.Permissions
            .FirstOrDefaultAsync(p => p.Key == permKey);
        Assert.NotNull(permRow);

        // Role-permission row still present
        var rolePermRow = await verifyCtx.RolePermissions
            .FirstOrDefaultAsync(rp => rp.PermissionKey == permKey);
        Assert.NotNull(rolePermRow);

        await connection.DisposeAsync();
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var realStatusStore = new TrackingPluginStatusStore(dbContext);
        var lifecycleService = new TrackingPluginLifecycleService();
        var packageStore = new TrackingPluginPackageStore();

        var sut = new PluginUninstallService(
            realStatusStore,
            lifecycleService,
            packageStore,
            dbContext,
            NullLogger<PluginUninstallService>.Instance);

        return new Fixture(connection, dbContext, realStatusStore, lifecycleService, packageStore, sut);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            TrackingPluginStatusStore statusStore,
            TrackingPluginLifecycleService lifecycleService,
            TrackingPluginPackageStore packageStore,
            PluginUninstallService sut)
        {
            Connection = connection;
            DbContext = dbContext;
            StatusStore = statusStore;
            LifecycleService = lifecycleService;
            PackageStore = packageStore;
            Sut = sut;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }
        public TrackingPluginStatusStore StatusStore { get; }
        public TrackingPluginLifecycleService LifecycleService { get; }
        public TrackingPluginPackageStore PackageStore { get; }
        public PluginUninstallService Sut { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    // ── Tracking fakes ────────────────────────────────────────────────────────

    /// <summary>Wraps DbPluginStatusStore to track DeleteAsync calls.</summary>
    private sealed class TrackingPluginStatusStore : IPluginStatusStore
    {
        private readonly ClubGear.Services.Plugins.Status.DbPluginStatusStore _inner;

        public TrackingPluginStatusStore(ApplicationDbContext dbContext)
        {
            _inner = new ClubGear.Services.Plugins.Status.DbPluginStatusStore(dbContext);
        }

        public List<string> DeleteCalls { get; } = new();

        public PluginStatusRecord? GetByKey(string key) => _inner.GetByKey(key);

        public IReadOnlyList<PluginStatusRecord> List() => _inner.List();

        public Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken ct = default)
            => _inner.UpsertAsync(record, ct);

        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            DeleteCalls.Add(key);
            await _inner.DeleteAsync(key, ct);
        }
    }

    private sealed class TrackingPluginLifecycleService : IPluginLifecycleService
    {
        public List<string> DeactivateCalls { get; } = new();

        public Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PluginLifecycleOperationResult>>(Array.Empty<PluginLifecycleOperationResult>());

        public Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken ct = default)
            => Task.FromResult(new PluginLifecycleOperationResult(true, "activated", "ok"));

        public Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken ct = default)
        {
            DeactivateCalls.Add(moduleId);
            return Task.FromResult(new PluginLifecycleOperationResult(true, "deactivated", "ok"));
        }
    }

    private sealed class TrackingPluginPackageStore : IPluginPackageStore
    {
        public List<string> DeleteCalls { get; } = new();

        public Task<string> SaveAsync(string pluginKey, string packageHash, byte[] packageBytes, CancellationToken ct = default)
            => Task.FromResult(Path.Combine(Path.GetTempPath(), $"{pluginKey}.zip"));

        public Task<string> EnsureExtractedAsync(string pluginKey, string packageHash, string packagePath, CancellationToken ct = default)
            => Task.FromResult(Path.Combine(Path.GetTempPath(), pluginKey));

        public Task DeleteAsync(string pluginKey, CancellationToken ct = default)
        {
            DeleteCalls.Add(pluginKey);
            return Task.CompletedTask;
        }
    }

    /// <summary>ApplicationDbContext that always throws on SaveChangesAsync to simulate a DB failure.</summary>
    private sealed class FailOnSaveDbContext : ApplicationDbContext
    {
        public FailOnSaveDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated DB failure");
    }

    /// <summary>Reads the status record directly from the given DbContext without wrapping a DbPluginStatusStore.</summary>
    private sealed class DirectReadPluginStatusStore : IPluginStatusStore
    {
        private readonly ApplicationDbContext _ctx;

        public DirectReadPluginStatusStore(ApplicationDbContext ctx) => _ctx = ctx;

        public PluginStatusRecord? GetByKey(string key)
            => _ctx.PluginStatusRecords.AsNoTracking().SingleOrDefault(r => r.Key == key);

        public IReadOnlyList<PluginStatusRecord> List()
            => _ctx.PluginStatusRecords.AsNoTracking().OrderBy(r => r.Key).ToArray();

        public Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}

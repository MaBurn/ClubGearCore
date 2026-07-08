using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core.SeedTasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests.SeedTasks;

public sealed class PermissionSeedTaskTests
{
    // ─── 2.2a — Pre-existing row with stale description/category is updated ─────

    [Fact]
    public async Task SeedAsync_UpdatesStaleRow_WhenDescriptionOrCategoryDiffers()
    {
        await using var fixture = await CreateFixtureAsync();

        // Pre-seed a row with outdated description and category
        fixture.DbContext.Permissions.Add(new AppPermission
        {
            Key = "finance.view",
            Description = "Old description",
            Category = "OldCategory"
        });
        await fixture.DbContext.SaveChangesAsync();

        // Provider now returns updated description and category
        var provider = new FakePermissionDefinitionProvider(
            new PermissionDefinition("finance.view", "Finance Plugin: finance.view", "Finance"));

        var task = new PermissionSeedTask();
        await task.SeedAsync(fixture.DbContext, BuildServiceProvider(provider), CancellationToken.None);

        var row = await fixture.DbContext.Permissions
            .SingleAsync(p => p.Key == "finance.view");

        Assert.Equal("Finance Plugin: finance.view", row.Description);
        Assert.Equal("Finance", row.Category);
    }

    // ─── 2.2b — Missing row is inserted ─────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_InsertsNewRow_WhenKeyIsAbsent()
    {
        await using var fixture = await CreateFixtureAsync();

        // No pre-existing rows
        var provider = new FakePermissionDefinitionProvider(
            new PermissionDefinition("billing.manage", "Billing Plugin: billing.manage", "Billing"));

        var task = new PermissionSeedTask();
        await task.SeedAsync(fixture.DbContext, BuildServiceProvider(provider), CancellationToken.None);

        var row = await fixture.DbContext.Permissions
            .SingleAsync(p => p.Key == "billing.manage");

        Assert.Equal("Billing Plugin: billing.manage", row.Description);
        Assert.Equal("Billing", row.Category);
    }

    // ─── 2.2c — Already-correct row is left untouched ────────────────────────────

    [Fact]
    public async Task SeedAsync_LeavesCorrectRow_Unchanged()
    {
        await using var fixture = await CreateFixtureAsync();

        // Pre-seed a row that already matches the provider definition
        fixture.DbContext.Permissions.Add(new AppPermission
        {
            Key = "members.read",
            Description = "Core Plugin: members.read",
            Category = "Core"
        });
        await fixture.DbContext.SaveChangesAsync();

        var rowBefore = await fixture.DbContext.Permissions
            .AsNoTracking()
            .SingleAsync(p => p.Key == "members.read");

        var provider = new FakePermissionDefinitionProvider(
            new PermissionDefinition("members.read", "Core Plugin: members.read", "Core"));

        var task = new PermissionSeedTask();
        await task.SeedAsync(fixture.DbContext, BuildServiceProvider(provider), CancellationToken.None);

        // Row count unchanged
        var count = await fixture.DbContext.Permissions.CountAsync();
        Assert.Equal(1, count);

        var rowAfter = await fixture.DbContext.Permissions
            .AsNoTracking()
            .SingleAsync(p => p.Key == "members.read");

        Assert.Equal(rowBefore.Id, rowAfter.Id);
        Assert.Equal("Core Plugin: members.read", rowAfter.Description);
        Assert.Equal("Core", rowAfter.Category);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static IServiceProvider BuildServiceProvider(params IPermissionDefinitionProvider[] providers)
    {
        var services = new ServiceCollection();
        foreach (var p in providers)
        {
            services.AddSingleton<IPermissionDefinitionProvider>(p);
        }
        return services.BuildServiceProvider();
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return new Fixture(connection, dbContext);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(SqliteConnection connection, ApplicationDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FakePermissionDefinitionProvider : IPermissionDefinitionProvider
    {
        private readonly IEnumerable<PermissionDefinition> _definitions;

        public FakePermissionDefinitionProvider(params PermissionDefinition[] definitions)
        {
            _definitions = definitions;
        }

        public IEnumerable<PermissionDefinition> GetPermissions() => _definitions;
    }
}

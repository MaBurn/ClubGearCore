using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core.SeedTasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests.SeedTasks;

/// <summary>
/// Slice 2, checkbox 1 DoD: the members.types.manage permission is granted to the
/// ClubGear.Admin role after a fresh seed run, and re-running the seed task is a
/// no-op (idempotent).
/// </summary>
public sealed class MembershipTypePermissionSeedTaskTests
{
    [Fact]
    public async Task SeedAsync_GrantsPermission_ToAdminRole()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new MembershipTypePermissionSeedTask();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);

        var row = await fixture.DbContext.RolePermissions
            .SingleAsync(rp => rp.RoleName == RoleNames.Admin && rp.PermissionKey == PermissionKeys.MembersTypesManage);
        Assert.NotNull(row);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateRow()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new MembershipTypePermissionSeedTask();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);
        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);

        var count = await fixture.DbContext.RolePermissions
            .CountAsync(rp => rp.RoleName == RoleNames.Admin && rp.PermissionKey == PermissionKeys.MembersTypesManage);
        Assert.Equal(1, count);
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
}

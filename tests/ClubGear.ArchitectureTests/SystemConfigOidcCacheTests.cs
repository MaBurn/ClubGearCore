using ClubGear.Data;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class SystemConfigOidcCacheTests
{
    [Fact]
    public async Task ExternalLoginWritesAndDeletes_InvalidateNamedOidcOptions()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        var cache = new TrackingOptionsCache();
        var service = new SystemConfigService(db, cache);

        await service.UpsertAsync(
            "externallogin.keycloak",
            "authority",
            "https://id.example.test",
            string.Empty);

        Assert.Equal(1, cache.ClearCalls);
        var entry = await db.SystemConfigEntries.SingleAsync();

        await service.DeleteByIdAsync(entry.Id);

        Assert.Equal(2, cache.ClearCalls);
    }

    [Fact]
    public async Task ExternalLoginWrite_RebuildsRealNamedOptionsFromUpdatedDatabaseValues()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupDb = new ApplicationDbContext(dbOptions))
        {
            await setupDb.Database.EnsureCreatedAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddScoped(_ => new ApplicationDbContext(dbOptions));
        services.AddClubGearCoreServices();
        services.AddAuthentication()
            .AddOpenIdConnect("oidc.generic", options =>
            {
                options.Authority = "https://unconfigured.invalid";
                options.ClientId = "unconfigured";
            });

        await using var provider = services.BuildServiceProvider();
        await using (var writeScope = provider.CreateAsyncScope())
        {
            var config = writeScope.ServiceProvider.GetRequiredService<ISystemConfigService>();
            await config.UpsertAsync("externallogin.keycloak", "enabled", "true", string.Empty);
            await config.UpsertAsync(
                "externallogin.keycloak",
                "authority",
                "https://first.example.test",
                string.Empty);
            await config.UpsertAsync("externallogin.keycloak", "clientid", "clubgear", string.Empty);
            await config.UpsertAsync("externallogin.keycloak", "clientsecret", "secret", string.Empty);
        }

        var monitor = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        var first = monitor.Get("oidc.keycloak");
        Assert.Equal("https://first.example.test", first.Authority);

        await using (var writeScope = provider.CreateAsyncScope())
        {
            var config = writeScope.ServiceProvider.GetRequiredService<ISystemConfigService>();
            await config.UpsertAsync(
                "externallogin.keycloak",
                "authority",
                "https://second.example.test",
                string.Empty);
        }

        var second = monitor.Get("oidc.keycloak");
        Assert.NotSame(first, second);
        Assert.Equal("https://second.example.test", second.Authority);
    }

    private sealed class TrackingOptionsCache : IOptionsMonitorCache<OpenIdConnectOptions>
    {
        public int ClearCalls { get; private set; }

        public void Clear() => ClearCalls++;

        public OpenIdConnectOptions GetOrAdd(
            string? name,
            Func<OpenIdConnectOptions> createOptions)
            => createOptions();

        public bool TryAdd(string? name, OpenIdConnectOptions options) => true;

        public bool TryRemove(string? name)
        {
            return true;
        }
    }
}

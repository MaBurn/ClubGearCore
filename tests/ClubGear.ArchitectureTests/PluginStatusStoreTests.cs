using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginStatusStoreTests
{
    [Fact]
    public async Task UpsertAndRead_RoundTripsPersistedPluginStatus()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var store = new DbPluginStatusStore(dbContext);
        var installedAt = DateTime.UtcNow.AddMinutes(-5);
        var updatedAt = DateTime.UtcNow;

        await store.UpsertAsync(new PluginStatusRecord
        {
            Key = "clubgear.finance",
            DisplayName = "Finance",
            Version = "1.0.0",
            Author = "Plugin Author",
            License = "Commercial",
            EntryPoint = "Finance.PluginModule",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "ABC123",
            PackagePath = "/tmp/plugins/clubgear.finance/package.zip",
            IsActive = false,
            LastError = "Initial validation failed",
            PermissionsJson = "[\"Plugin_Finance_View\"]",
            ExtensionPointsJson = "[\"member.detail\"]",
            InstalledAtUtc = installedAt,
            UpdatedAtUtc = updatedAt
        });

        var persisted = store.GetByKey("clubgear.finance");

        Assert.NotNull(persisted);
        Assert.Equal("Finance", persisted!.DisplayName);
        Assert.Equal("Commercial", persisted.License);
        Assert.Equal("ABC123", persisted.PackageHash);
        Assert.Equal("/tmp/plugins/clubgear.finance/package.zip", persisted.PackagePath);
        Assert.False(persisted.IsActive);
        Assert.Equal("Initial validation failed", persisted.LastError);
        Assert.Equal(installedAt, persisted.InstalledAtUtc, TimeSpan.FromSeconds(1));

        var listed = store.List();
        Assert.Single(listed);
        Assert.Equal("clubgear.finance", listed[0].Key);

        persisted.PackageHash = "DEF456";
        persisted.PackagePath = "/tmp/plugins/clubgear.finance/v2/package.zip";
        persisted.UpdatedAtUtc = DateTime.UtcNow;
        await store.UpsertAsync(persisted);

        var updated = store.GetByKey("clubgear.finance");
        Assert.NotNull(updated);
        Assert.Equal("DEF456", updated!.PackageHash);
        Assert.Equal("/tmp/plugins/clubgear.finance/v2/package.zip", updated.PackagePath);
    }
}
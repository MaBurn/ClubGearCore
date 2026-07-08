using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services;
using ClubGear.Services.Plugins.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginMigrationStateTests
{
    [Fact]
    public async Task DbContext_RoundTripsPluginMigrationState_WithSeparatePerPluginHistory()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.PluginMigrationStates.AddRange(
            new PluginMigrationState
            {
                PluginKey = "plugin.alpha",
                MigrationId = "001_create_notes",
                TablePrefix = "plugin_plugin_alpha_",
                AppliedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new PluginMigrationState
            {
                PluginKey = "plugin.beta",
                MigrationId = "001_create_notes",
                TablePrefix = "plugin_plugin_beta_",
                AppliedAtUtc = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();

        var persisted = await dbContext.PluginMigrationStates
            .AsNoTracking()
            .OrderBy(state => state.PluginKey)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal("plugin.alpha", persisted[0].PluginKey);
        Assert.Equal("plugin.beta", persisted[1].PluginKey);
        Assert.NotEqual(persisted[0].TablePrefix, persisted[1].TablePrefix);
    }

    [Fact]
    public void SchemaNamePolicy_CreatesStablePrefixes_AndRejectsForeignTables()
    {
        var policy = new PluginSchemaNamePolicy();

        var prefix = policy.GetTablePrefix("Plugin.Runtime.A");
        var tableName = policy.GetTableName("Plugin.Runtime.A", "notes");

        Assert.Equal("plugin_plugin_runtime_a_", prefix);
        Assert.Equal("plugin_plugin_runtime_a_notes", tableName);

        var ex = Assert.Throws<UserFriendlyException>(() => policy.ValidateSql("plugin.runtime.a", "CREATE TABLE Members (Id INTEGER NOT NULL);"));
        Assert.Contains("Praefix", ex.Message, StringComparison.Ordinal);
    }
}
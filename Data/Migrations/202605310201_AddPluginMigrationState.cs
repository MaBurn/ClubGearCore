using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

public static class AddPluginMigrationState_202605310201
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS PluginMigrationStates (
                Id INTEGER NOT NULL CONSTRAINT PK_PluginMigrationStates PRIMARY KEY AUTOINCREMENT,
                PluginKey TEXT NOT NULL,
                MigrationId TEXT NOT NULL,
                TablePrefix TEXT NOT NULL,
                AppliedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PluginMigrationStates_PluginKey_MigrationId ON PluginMigrationStates (PluginKey, MigrationId);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PluginMigrationStates_PluginKey ON PluginMigrationStates (PluginKey);",
            cancellationToken);
    }
}
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

public static class AddPluginStatusStore_202605310101
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS PluginStatusRecords (
                Id INTEGER NOT NULL CONSTRAINT PK_PluginStatusRecords PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Version TEXT NOT NULL,
                Author TEXT NOT NULL,
                License TEXT NOT NULL,
                EntryPoint TEXT NOT NULL,
                RequiredCoreVersion TEXT NOT NULL,
                InstallSource TEXT NOT NULL,
                PackageHash TEXT NOT NULL,
                PackagePath TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 0,
                LastError TEXT NULL,
                PermissionsJson TEXT NOT NULL DEFAULT '[]',
                ExtensionPointsJson TEXT NOT NULL DEFAULT '[]',
                InstalledAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PluginStatusRecords_Key ON PluginStatusRecords (Key);",
            cancellationToken);
    }
}
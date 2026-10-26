using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

/// <summary>
/// CORE-09. Creates the ApiKeys table plus the index the authentication path needs. Idempotent
/// raw SQL, matching the project's no-EF-Migrations philosophy. Nothing is dropped and nothing is
/// backfilled: an installation without API keys behaves exactly as before.
/// </summary>
public static class AddApiKeys_202608310101
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS ApiKeys (
                Id INTEGER NOT NULL CONSTRAINT PK_ApiKeys PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                KeyPrefix TEXT NOT NULL,
                KeyHash TEXT NOT NULL,
                ScopesCsv TEXT NOT NULL DEFAULT '',
                CreatedAtUtc TEXT NOT NULL,
                CreatedBy TEXT NULL,
                ExpiresAtUtc TEXT NULL,
                LastUsedAtUtc TEXT NULL,
                RevokedAtUtc TEXT NULL,
                RevokedBy TEXT NULL
            );",
            cancellationToken);

        // Every authenticated API request looks a key up by prefix, so this index is on the hot
        // path. Not unique: two keys may share a prefix, and the hash comparison decides.
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ApiKeys_KeyPrefix ON ApiKeys (KeyPrefix);",
            cancellationToken);
    }
}

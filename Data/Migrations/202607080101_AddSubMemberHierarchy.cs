using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

/// <summary>
/// Adds the sub-member container configuration to MembershipTypes: an
/// AllowsSubMembers flag (marks the type as a parent/container type) and a
/// SubMemberLabel (the per-type label used to group its sub-members, e.g.
/// Firma -> "Mitarbeiter"). Both columns are added idempotently via column-exists
/// guards, matching the project's no-EF-Migrations philosophy. Nothing is dropped.
///
/// A one-off idempotent backfill marks the seeded system container types
/// (Familie -> "Familienmitglied", Firma -> "Mitarbeiter", Verein -> "Mitglied")
/// so the feature is usable immediately; "Standard" stays a non-container (0).
/// The backfill only touches rows that still carry the default (AllowsSubMembers = 0
/// AND SubMemberLabel IS NULL), so re-running never overrides admin edits.
/// </summary>
public static class AddSubMemberHierarchy_202607080101
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await AddColumnsAsync(dbContext, cancellationToken);
        await BackfillSystemContainerTypesAsync(dbContext, cancellationToken);
    }

    private static async Task AddColumnsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "MembershipTypes", cancellationToken);
        if (existingColumns.Count == 0)
        {
            return;
        }

        if (!existingColumns.Contains("AllowsSubMembers"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE MembershipTypes ADD COLUMN AllowsSubMembers INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }

        if (!existingColumns.Contains("SubMemberLabel"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE MembershipTypes ADD COLUMN SubMemberLabel TEXT NULL;",
                cancellationToken);
        }
    }

    private static async Task BackfillSystemContainerTypesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var defaults = new (string Key, string Label)[]
        {
            ("Familie", "Familienmitglied"),
            ("Firma", "Mitarbeiter"),
            ("Verein", "Mitglied"),
        };

        foreach (var (key, label) in defaults)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"UPDATE MembershipTypes
                  SET AllowsSubMembers = 1, SubMemberLabel = {1}
                  WHERE Key = {0} AND AllowsSubMembers = 0 AND SubMemberLabel IS NULL;",
                new object[] { key, label },
                cancellationToken);
        }
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(ApplicationDbContext dbContext, string tableName, CancellationToken cancellationToken)
    {
        // Do not wrap the DbContext's connection in `await using` here: it is owned and
        // reused by the DbContext, and disposing it prematurely destroys in-memory SQLite
        // databases before the caller's follow-up SQL can run. Only close it if we
        // were the ones who opened it.
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(1))
                {
                    result.Add(reader.GetString(1));
                }
            }

            return result;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

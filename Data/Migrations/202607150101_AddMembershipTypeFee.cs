using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

/// <summary>
/// Adds the per-membership-type fee configuration to MembershipTypes: a nullable
/// FeeAmount (the recurring fee) and a nullable BillingInterval (Yearly / Quarterly).
/// Both columns are added idempotently via column-exists guards, matching the project's
/// no-EF-Migrations philosophy. Nothing is dropped and nothing is backfilled: pre-existing
/// types simply keep a NULL fee/interval and remain valid.
/// </summary>
public static class AddMembershipTypeFee_202607150101
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "MembershipTypes", cancellationToken);
        if (existingColumns.Count == 0)
        {
            return;
        }

        if (!existingColumns.Contains("FeeAmount"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE MembershipTypes ADD COLUMN FeeAmount TEXT NULL;",
                cancellationToken);
        }

        if (!existingColumns.Contains("BillingInterval"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE MembershipTypes ADD COLUMN BillingInterval TEXT NULL;",
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

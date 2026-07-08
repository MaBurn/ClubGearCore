using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

public static class AddPluginPackagePath_202605310151
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "PluginStatusRecords", cancellationToken);
        if (existingColumns.Count == 0 || existingColumns.Contains("PackagePath"))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE PluginStatusRecords ADD COLUMN PackagePath TEXT NOT NULL DEFAULT '';",
            cancellationToken);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(ApplicationDbContext dbContext, string tableName, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
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
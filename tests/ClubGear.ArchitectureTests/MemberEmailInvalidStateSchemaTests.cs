using ClubGear.Data;
using ClubGear.Data.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2, checkbox 2.1 DoD: the AddMemberEmailInvalidState migration adds all three
/// email-invalid columns to a pre-feature Members table, is a no-op on re-run, and a fresh
/// EnsureCreatedAsync database already carries the columns from the EF model.
/// </summary>
public sealed class MemberEmailInvalidStateSchemaTests
{
    [Fact]
    public async Task EnsureCreatedAsync_OnNewDatabase_MembersHasEmailInvalidColumns()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var columns = await GetTableColumnsAsync(connection, "Members");
        Assert.Contains("EmailInvalid", columns);
        Assert.Contains("EmailInvalidatedAtUtc", columns);
        Assert.Contains("EmailInvalidReason", columns);
    }

    [Fact]
    public async Task ApplyAsync_AgainstPreFeatureDb_AddsAllThreeColumns_AndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        // Pre-feature Members shape: no email-invalid columns yet.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE Members (
                    Id INTEGER NOT NULL CONSTRAINT PK_Members PRIMARY KEY AUTOINCREMENT,
                    MemberNumber TEXT NOT NULL DEFAULT '',
                    FirstName TEXT NOT NULL DEFAULT '',
                    LastName TEXT NOT NULL DEFAULT '',
                    Email TEXT NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Members (MemberNumber, FirstName, LastName, Email) VALUES ('M-1', 'Erika', 'Muster', 'erika@example.org');";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        // First run adds all three columns.
        await AddMemberEmailInvalidState_202607240101.ApplyAsync(dbContext);

        var columnsAfterFirstRun = await GetTableColumnsAsync(connection, "Members");
        Assert.Contains("EmailInvalid", columnsAfterFirstRun);
        Assert.Contains("EmailInvalidatedAtUtc", columnsAfterFirstRun);
        Assert.Contains("EmailInvalidReason", columnsAfterFirstRun);

        // Pre-existing members default to "not invalid" with no timestamp/reason.
        var (invalid, invalidatedAt, reason) = await GetEmailInvalidStateAsync(connection, memberId: 1);
        Assert.False(invalid);
        Assert.Null(invalidatedAt);
        Assert.Null(reason);

        // Second run is a no-op (idempotent): must not throw and must not duplicate columns.
        await AddMemberEmailInvalidState_202607240101.ApplyAsync(dbContext);

        var columnsAfterSecondRun = await GetTableColumnsAsync(connection, "Members");
        Assert.Equal(columnsAfterFirstRun.Count, columnsAfterSecondRun.Count);

        // Values written after the migration read back intact.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Members SET EmailInvalid = 1, EmailInvalidatedAtUtc = '2026-07-24T10:00:00Z', EmailInvalidReason = '550 5.1.1 unknown' WHERE Id = 1;";
            await cmd.ExecuteNonQueryAsync();
        }

        var (updatedInvalid, updatedAt, updatedReason) = await GetEmailInvalidStateAsync(connection, memberId: 1);
        Assert.True(updatedInvalid);
        Assert.Equal("2026-07-24T10:00:00Z", updatedAt);
        Assert.Equal("550 5.1.1 unknown", updatedReason);
    }

    [Fact]
    public async Task ApplyAsync_WithoutMembersTable_IsANoOp()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        // No Members table at all: the guard must return without throwing.
        await AddMemberEmailInvalidState_202607240101.ApplyAsync(dbContext);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static async Task<(bool Invalid, string? InvalidatedAtUtc, string? Reason)> GetEmailInvalidStateAsync(SqliteConnection connection, int memberId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EmailInvalid, EmailInvalidatedAtUtc, EmailInvalidReason FROM Members WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", memberId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (false, null, null);
        }

        var invalid = reader.GetInt64(0) != 0;
        var invalidatedAt = reader.IsDBNull(1) ? null : reader.GetString(1);
        var reason = reader.IsDBNull(2) ? null : reader.GetString(2);
        return (invalid, invalidatedAt, reason);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(1));
        }

        return result;
    }
}

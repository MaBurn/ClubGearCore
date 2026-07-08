using ClubGear.Data;
using ClubGear.Data.Migrations;
using ClubGear.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MembershipTypeSchemaTests
{
    // ── Slice 1, checkbox 2 DoD: brand-new DB via EnsureCreatedAsync() contains all 3
    //    new tables with the configured indexes/FKs. ──────────────────────────────────

    [Fact]
    public async Task EnsureCreatedAsync_OnNewDatabase_CreatesMembershipTypeTablesWithIndexesAndForeignKeys()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var tables = await GetTableNamesAsync(connection);
        Assert.Contains("MembershipTypes", tables);
        Assert.Contains("MembershipTypeFields", tables);
        Assert.Contains("MemberMetadataValues", tables);

        var membersColumns = await GetTableColumnsAsync(connection, "Members");
        Assert.Contains("MembershipTypeId", membersColumns);

        var membershipTypeIndexColumns = await GetUniqueIndexColumnSetsAsync(connection, "MembershipTypes");
        Assert.Contains(membershipTypeIndexColumns, cols => cols.SequenceEqual(new[] { "Key" }));

        var fieldIndexColumns = await GetUniqueIndexColumnSetsAsync(connection, "MembershipTypeFields");
        Assert.Contains(fieldIndexColumns, cols => cols.SequenceEqual(new[] { "MembershipTypeId", "Key" }));

        var valueIndexColumns = await GetUniqueIndexColumnSetsAsync(connection, "MemberMetadataValues");
        Assert.Contains(valueIndexColumns, cols => cols.SequenceEqual(new[] { "MemberId", "FieldId" }));

        var fieldForeignKeys = await GetForeignKeyTargetTablesAsync(connection, "MembershipTypeFields");
        Assert.Contains("MembershipTypes", fieldForeignKeys);

        var valueForeignKeys = await GetForeignKeyTargetTablesAsync(connection, "MemberMetadataValues");
        Assert.Contains("Members", valueForeignKeys);
        Assert.Contains("MembershipTypeFields", valueForeignKeys);

        var memberForeignKeys = await GetForeignKeyTargetTablesAsync(connection, "Members");
        Assert.Contains("MembershipTypes", memberForeignKeys);
    }

    // ── Slice 1, checkbox 3 DoD: running the migration twice against a pre-existing
    //    SQLite DB seeded with rows exercising every legacy-flag combination produces
    //    correct MembershipTypeId/MemberMetadataValue rows on the first run and is a
    //    no-op (no duplicate rows/errors) on the second run. ─────────────────────────

    [Fact]
    public async Task ApplyAsync_AgainstPreExistingDbWithLegacyFlagCombinations_BackfillsCorrectlyAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        // Minimal legacy Members schema (no MembershipTypeId column yet, no new tables),
        // mirroring the pre-migration on-disk shape.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE Members (
                    Id INTEGER NOT NULL CONSTRAINT PK_Members PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL DEFAULT '',
                    LastName TEXT NOT NULL DEFAULT '',
                    IsCompany INTEGER NOT NULL,
                    CompanyName TEXT NULL,
                    IsClub INTEGER NOT NULL,
                    ClubName TEXT NULL,
                    FamilyMembership INTEGER NOT NULL,
                    MainMemberId INTEGER NULL,
                    MembershipDiscount INTEGER NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        // Row 1: Verein, no discount override.
        // Row 2: Firma, with discount override.
        // Row 3: Familie (references row 1 as main member), no discount override.
        // Row 4: plain (no flags), no discount override.
        // Row 5: plain (no flags), with discount override.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"INSERT INTO Members (FirstName, LastName, IsCompany, CompanyName, IsClub, ClubName, FamilyMembership, MainMemberId, MembershipDiscount) VALUES
                    ('Verein', 'Mitglied', 0, NULL, 1, 'Musterverein e.V.', 0, NULL, NULL),
                    ('Firma', 'Mitglied', 1, 'Musterfirma GmbH', 0, NULL, 0, NULL, 10),
                    ('Familie', 'Mitglied', 0, NULL, 0, NULL, 1, 1, NULL),
                    ('Plain', 'Mitglied', 0, NULL, 0, NULL, 0, NULL, NULL),
                    ('PlainWithDiscount', 'Mitglied', 0, NULL, 0, NULL, 0, NULL, 5);";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        // First run: performs the backfill.
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext);

        var typeIdByKey = await GetMembershipTypeIdsByKeyAsync(connection);
        var memberTypeIds = await GetMemberMembershipTypeIdsAsync(connection);

        Assert.Equal(typeIdByKey["Verein"], memberTypeIds[1]);
        Assert.Equal(typeIdByKey["Firma"], memberTypeIds[2]);
        Assert.Equal(typeIdByKey["Familie"], memberTypeIds[3]);
        Assert.Equal(typeIdByKey["Standard"], memberTypeIds[4]);
        Assert.Equal(typeIdByKey["Standard"], memberTypeIds[5]);

        var clubNameValue = await GetMetadataValueAsync(connection, memberId: 1, fieldKey: "club_name");
        Assert.Equal("Musterverein e.V.", clubNameValue);

        var companyNameValue = await GetMetadataValueAsync(connection, memberId: 2, fieldKey: "company_name");
        Assert.Equal("Musterfirma GmbH", companyNameValue);

        var firmaDiscountOverride = await GetMetadataValueAsync(connection, memberId: 2, fieldKey: "membership_discount_override");
        Assert.Equal("10", firmaDiscountOverride);

        var mainMemberValue = await GetMetadataValueAsync(connection, memberId: 3, fieldKey: "main_member");
        Assert.Equal("1", mainMemberValue);

        var plainDiscountOverride = await GetMetadataValueAsync(connection, memberId: 4, fieldKey: "membership_discount_override");
        Assert.Null(plainDiscountOverride);

        var plainWithDiscountOverride = await GetMetadataValueAsync(connection, memberId: 5, fieldKey: "membership_discount_override");
        Assert.Equal("5", plainWithDiscountOverride);

        var totalMetadataRowsAfterFirstRun = await CountMemberMetadataValuesAsync(connection);
        Assert.Equal(5, totalMetadataRowsAfterFirstRun);

        // Second run: must be a no-op (no duplicate rows, no errors).
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext);

        var memberTypeIdsAfterSecondRun = await GetMemberMembershipTypeIdsAsync(connection);
        Assert.Equal(memberTypeIds, memberTypeIdsAfterSecondRun);

        var totalMetadataRowsAfterSecondRun = await CountMemberMetadataValuesAsync(connection);
        Assert.Equal(totalMetadataRowsAfterFirstRun, totalMetadataRowsAfterSecondRun);

        var totalMembershipTypeRows = await CountRowsAsync(connection, "MembershipTypes");
        Assert.Equal(4, totalMembershipTypeRows);
    }

    // ── Regression: on a database that pre-dates this feature, IsCompany/IsClub/FamilyMembership
    //    are physically INTEGER NOT NULL with no SQL-level DEFAULT (confirmed against a real
    //    ClubGear dev database - unlike the CREATE TABLE above before this fix, production
    //    databases were never given a DEFAULT for these columns). Since Member.cs no longer
    //    exposes them, EF must still supply an explicit value for them via shadow-property
    //    mapping (ApplicationDbContext.OnModelCreating), or every new-Member INSERT fails with
    //    "NOT NULL constraint failed: Members.IsCompany". ─────────────────────────────────────

    [Fact]
    public async Task CreateMember_AfterMigrationOnPreExistingDbWithoutLegacyColumnDefaults_Succeeds()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        // Full pre-migration legacy schema (mirrors a real ClubGear production/dev database,
        // i.e. every column Member.cs and the rest of the app need, exactly as EF originally
        // created it - no MembershipTypeId column yet, and no DEFAULT on the NOT NULL booleans).
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE Members (
                    Id INTEGER NOT NULL CONSTRAINT PK_Members PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NULL,
                    OauthID TEXT NULL,
                    OAuthUserName TEXT NULL,
                    IsVerified INTEGER NOT NULL,
                    MemberNumber TEXT NOT NULL,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NULL,
                    PhoneNumber TEXT NULL,
                    DateOfBirth TEXT NULL,
                    Gender TEXT NULL,
                    IsCompany INTEGER NOT NULL,
                    CompanyName TEXT NULL,
                    IsClub INTEGER NOT NULL,
                    ClubName TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    JoinedAt TEXT NOT NULL,
                    Joined TEXT NULL,
                    Leaved TEXT NULL,
                    LastUpdated TEXT NULL,
                    IsDeceased INTEGER NOT NULL,
                    MembershipDiscount INTEGER NULL,
                    FamilyMembership INTEGER NOT NULL,
                    MainMemberId INTEGER NULL,
                    NotifyViaEmail INTEGER NOT NULL,
                    NotifyViaMatrix INTEGER NOT NULL,
                    DataprivacyAccepted INTEGER NOT NULL,
                    NewsletterConsent INTEGER NOT NULL,
                    ProfileImagePath TEXT NULL,
                    PendingEmail TEXT NULL,
                    EmailVerificationToken TEXT NULL,
                    EmailVerificationTokenExpiry TEXT NULL,
                    RentalPayoutOptions TEXT NULL,
                    InitPassword TEXT NULL,
                    KeycloakUsername TEXT NULL,
                    ApplicationUserId TEXT NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext);

        var standardTypeId = (await GetMembershipTypeIdsByKeyAsync(connection))["Standard"];

        dbContext.Members.Add(new Member
        {
            MemberNumber = "TEST-001",
            FirstName = "New",
            LastName = "Member",
            MembershipTypeId = standardTypeId,
        });

        // Must not throw SqliteException "NOT NULL constraint failed: Members.IsCompany".
        await dbContext.SaveChangesAsync();

        var insertedCount = await CountRowsAsync(connection, "Members");
        Assert.True(insertedCount > 0);
    }

    // ── Slice 1 (sub-member hierarchy) DoD: the AddSubMemberHierarchy migration adds the
    //    two container columns idempotently and backfills the seeded system container types
    //    (Familie/Firma/Verein) without touching "Standard". ────────────────────────────

    [Fact]
    public async Task AddSubMemberHierarchy_AddsColumnsAndBackfillsSystemContainerTypes_Idempotently()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        // Minimal legacy Members schema so the prerequisite MembershipType migration
        // (which seeds Standard/Verein/Firma/Familie) can run.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE Members (
                    Id INTEGER NOT NULL CONSTRAINT PK_Members PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL DEFAULT '',
                    LastName TEXT NOT NULL DEFAULT '',
                    IsCompany INTEGER NOT NULL DEFAULT 0,
                    CompanyName TEXT NULL,
                    IsClub INTEGER NOT NULL DEFAULT 0,
                    ClubName TEXT NULL,
                    FamilyMembership INTEGER NOT NULL DEFAULT 0,
                    MainMemberId INTEGER NULL,
                    MembershipDiscount INTEGER NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        // Prerequisite: create + seed MembershipTypes (Standard/Verein/Firma/Familie).
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext);

        // Sub-member migration under test — first run adds columns + backfills.
        await AddSubMemberHierarchy_202607080101.ApplyAsync(dbContext);

        var columns = await GetTableColumnsAsync(connection, "MembershipTypes");
        Assert.Contains("AllowsSubMembers", columns);
        Assert.Contains("SubMemberLabel", columns);

        var configAfterFirstRun = await GetSubMemberConfigByKeyAsync(connection);
        Assert.Equal((true, "Familienmitglied"), configAfterFirstRun["Familie"]);
        Assert.Equal((true, "Mitarbeiter"), configAfterFirstRun["Firma"]);
        Assert.Equal((true, "Mitglied"), configAfterFirstRun["Verein"]);
        Assert.Equal((false, null), configAfterFirstRun["Standard"]);

        // Second run: must be a no-op (idempotent), leaving the values unchanged.
        await AddSubMemberHierarchy_202607080101.ApplyAsync(dbContext);

        var configAfterSecondRun = await GetSubMemberConfigByKeyAsync(connection);
        Assert.Equal(configAfterFirstRun, configAfterSecondRun);
    }

    [Fact]
    public async Task AddSubMemberHierarchy_DoesNotOverrideAdminEditedContainerConfig_OnReRun()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE Members (
                    Id INTEGER NOT NULL CONSTRAINT PK_Members PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL DEFAULT '',
                    LastName TEXT NOT NULL DEFAULT '',
                    IsCompany INTEGER NOT NULL DEFAULT 0,
                    CompanyName TEXT NULL,
                    IsClub INTEGER NOT NULL DEFAULT 0,
                    ClubName TEXT NULL,
                    FamilyMembership INTEGER NOT NULL DEFAULT 0,
                    MainMemberId INTEGER NULL,
                    MembershipDiscount INTEGER NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext);
        await AddSubMemberHierarchy_202607080101.ApplyAsync(dbContext);

        // Admin changes the Firma label after the initial backfill.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE MembershipTypes SET SubMemberLabel = 'Angestellte' WHERE Key = 'Firma';";
            await cmd.ExecuteNonQueryAsync();
        }

        // Re-running the migration must not clobber the admin edit.
        await AddSubMemberHierarchy_202607080101.ApplyAsync(dbContext);

        var config = await GetSubMemberConfigByKeyAsync(connection);
        Assert.Equal((true, "Angestellte"), config["Firma"]);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, (bool AllowsSubMembers, string? Label)>> GetSubMemberConfigByKeyAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, AllowsSubMembers, SubMemberLabel FROM MembershipTypes;";

        var result = new Dictionary<string, (bool, string?)>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var allows = reader.GetInt64(1) != 0;
            var label = reader.IsDBNull(2) ? null : reader.GetString(2);
            result[key] = (allows, label);
        }

        return result;
    }


    private static async Task<HashSet<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
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

    private static async Task<List<string[]>> GetUniqueIndexColumnSetsAsync(SqliteConnection connection, string tableName)
    {
        var result = new List<string[]>();

        var indexNames = new List<string>();
        await using (var listCommand = connection.CreateCommand())
        {
            listCommand.CommandText = $"PRAGMA index_list({tableName});";
            await using var reader = await listCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var isUnique = reader.GetInt32(2) == 1;
                if (isUnique)
                {
                    indexNames.Add(reader.GetString(1));
                }
            }
        }

        foreach (var indexName in indexNames)
        {
            await using var infoCommand = connection.CreateCommand();
            infoCommand.CommandText = $"PRAGMA index_info({indexName});";
            var columns = new List<string>();
            await using var reader = await infoCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(2));
            }

            result.Add(columns.ToArray());
        }

        return result;
    }

    private static async Task<HashSet<string>> GetForeignKeyTargetTablesAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName});";

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(2));
        }

        return result;
    }

    private static async Task<Dictionary<string, int>> GetMembershipTypeIdsByKeyAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Id FROM MembershipTypes;";

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetInt32(1);
        }

        return result;
    }

    private static async Task<Dictionary<int, int>> GetMemberMembershipTypeIdsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, MembershipTypeId FROM Members ORDER BY Id;";

        var result = new Dictionary<int, int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetInt32(0)] = reader.GetInt32(1);
        }

        return result;
    }

    private static async Task<string?> GetMetadataValueAsync(SqliteConnection connection, int memberId, string fieldKey)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            @"SELECT v.Value
              FROM MemberMetadataValues v
              JOIN MembershipTypeFields f ON f.Id = v.FieldId
              WHERE v.MemberId = $memberId AND f.Key = $fieldKey;";
        command.Parameters.AddWithValue("$memberId", memberId);
        command.Parameters.AddWithValue("$fieldKey", fieldKey);

        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    private static async Task<int> CountMemberMetadataValuesAsync(SqliteConnection connection)
        => await CountRowsAsync(connection, "MemberMetadataValues");

    private static async Task<int> CountRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}

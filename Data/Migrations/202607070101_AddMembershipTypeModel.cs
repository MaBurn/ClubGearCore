using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

/// <summary>
/// Introduces the dynamic Mitgliedsart (MembershipType) model: creates the
/// MembershipTypes / MembershipTypeFields / MemberMetadataValues tables, seeds the
/// 4 system-defined types (Standard, Verein, Firma, Familie) and their field definitions,
/// adds the nullable Members.MembershipTypeId column, and idempotently backfills it
/// (plus the corresponding MemberMetadataValue rows) from the 7 legacy Members columns.
/// Legacy columns are left physically in place; nothing is dropped.
/// </summary>
public static class AddMembershipTypeModel_202607070101
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await CreateTablesAsync(dbContext, cancellationToken);
        await SeedSystemTypesAndFieldsAsync(dbContext, cancellationToken);
        await AddMembersColumnAsync(dbContext, cancellationToken);
        await BackfillMembershipTypeAsync(dbContext, cancellationToken);
        await BackfillMetadataValuesAsync(dbContext, cancellationToken);
    }

    private static async Task CreateTablesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MembershipTypes (
                Id INTEGER NOT NULL CONSTRAINT PK_MembershipTypes PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                DefaultDiscountPercent INTEGER NULL,
                IsSystemDefined INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_MembershipTypes_Key ON MembershipTypes (Key);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MembershipTypeFields (
                Id INTEGER NOT NULL CONSTRAINT PK_MembershipTypeFields PRIMARY KEY AUTOINCREMENT,
                MembershipTypeId INTEGER NOT NULL,
                Key TEXT NOT NULL,
                Label TEXT NOT NULL,
                FieldType INTEGER NOT NULL,
                IsRequired INTEGER NOT NULL DEFAULT 0,
                HelpText TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsSystemDefined INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT FK_MembershipTypeFields_MembershipTypes_MembershipTypeId FOREIGN KEY (MembershipTypeId) REFERENCES MembershipTypes (Id) ON DELETE CASCADE
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_MembershipTypeFields_MembershipTypeId_Key ON MembershipTypeFields (MembershipTypeId, Key);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MemberMetadataValues (
                Id INTEGER NOT NULL CONSTRAINT PK_MemberMetadataValues PRIMARY KEY AUTOINCREMENT,
                MemberId INTEGER NOT NULL,
                FieldId INTEGER NOT NULL,
                Value TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                CONSTRAINT FK_MemberMetadataValues_Members_MemberId FOREIGN KEY (MemberId) REFERENCES Members (Id) ON DELETE CASCADE,
                CONSTRAINT FK_MemberMetadataValues_MembershipTypeFields_FieldId FOREIGN KEY (FieldId) REFERENCES MembershipTypeFields (Id) ON DELETE CASCADE
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_MemberMetadataValues_MemberId_FieldId ON MemberMetadataValues (MemberId, FieldId);",
            cancellationToken);
    }

    private static async Task SeedSystemTypesAndFieldsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow.ToString("O");

        var types = new (string Key, string Name, int SortOrder)[]
        {
            ("Standard", "Standard", 0),
            ("Verein", "Verein", 1),
            ("Firma", "Firma", 2),
            ("Familie", "Familie", 3),
        };

        foreach (var (key, name, sortOrder) in types)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"INSERT INTO MembershipTypes (Key, Name, Description, DefaultDiscountPercent, IsSystemDefined, SortOrder, IsActive, CreatedAtUtc, UpdatedAtUtc)
                  SELECT {0}, {1}, NULL, NULL, 1, {2}, 1, {3}, {3}
                  WHERE NOT EXISTS (SELECT 1 FROM MembershipTypes WHERE Key = {0});",
                new object[] { key, name, sortOrder, nowUtc },
                cancellationToken);
        }

        // (TypeKey, FieldKey, Label, FieldType [0=Text,1=Number,2=Boolean,3=Date,4=MemberReference], IsRequired, SortOrder)
        var fields = new (string TypeKey, string FieldKey, string Label, int FieldType, int IsRequired, int SortOrder)[]
        {
            ("Verein", "club_name", "Vereinsname", 0, 1, 0),
            ("Verein", "club_magazine", "Vereinszeitschrift", 2, 0, 1),
            ("Verein", "membership_discount_override", "Mitgliedsrabatt (Override)", 1, 0, 2),
            ("Firma", "company_name", "Firmenname", 0, 1, 0),
            ("Firma", "membership_discount_override", "Mitgliedsrabatt (Override)", 1, 0, 1),
            ("Familie", "main_member", "Hauptmitglied", 4, 0, 0),
            ("Familie", "membership_fee", "Mitgliedsbeitrag", 1, 0, 1),
            ("Familie", "membership_discount_override", "Mitgliedsrabatt (Override)", 1, 0, 2),
            ("Standard", "membership_discount_override", "Mitgliedsrabatt (Override)", 1, 0, 0),
        };

        foreach (var (typeKey, fieldKey, label, fieldType, isRequired, sortOrder) in fields)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"INSERT INTO MembershipTypeFields (MembershipTypeId, Key, Label, FieldType, IsRequired, HelpText, SortOrder, IsSystemDefined)
                  SELECT t.Id, {1}, {2}, {3}, {4}, NULL, {5}, 1
                  FROM MembershipTypes t
                  WHERE t.Key = {0}
                    AND NOT EXISTS (SELECT 1 FROM MembershipTypeFields f WHERE f.MembershipTypeId = t.Id AND f.Key = {1});",
                new object[] { typeKey, fieldKey, label, fieldType, isRequired, sortOrder },
                cancellationToken);
        }
    }

    private static async Task AddMembersColumnAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "Members", cancellationToken);
        if (existingColumns.Count == 0 || existingColumns.Contains("MembershipTypeId"))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Members ADD COLUMN MembershipTypeId INTEGER NULL;",
            cancellationToken);
    }

    private static async Task BackfillMembershipTypeAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Precedence, matching the historical FullName computation: Firma > Verein > Familie > Standard.
        await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE Members
              SET MembershipTypeId = (SELECT Id FROM MembershipTypes WHERE Key = 'Firma')
              WHERE MembershipTypeId IS NULL AND IsCompany = 1;",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE Members
              SET MembershipTypeId = (SELECT Id FROM MembershipTypes WHERE Key = 'Verein')
              WHERE MembershipTypeId IS NULL AND IsClub = 1;",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE Members
              SET MembershipTypeId = (SELECT Id FROM MembershipTypes WHERE Key = 'Familie')
              WHERE MembershipTypeId IS NULL AND FamilyMembership = 1;",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE Members
              SET MembershipTypeId = (SELECT Id FROM MembershipTypes WHERE Key = 'Standard')
              WHERE MembershipTypeId IS NULL;",
            cancellationToken);
    }

    private static async Task BackfillMetadataValuesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow.ToString("O");

        await dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO MemberMetadataValues (MemberId, FieldId, Value, UpdatedAtUtc)
              SELECT m.Id, f.Id, m.CompanyName, {0}
              FROM Members m
              JOIN MembershipTypeFields f ON f.Key = 'company_name' AND f.MembershipTypeId = m.MembershipTypeId
              WHERE m.CompanyName IS NOT NULL AND TRIM(m.CompanyName) <> ''
                AND NOT EXISTS (SELECT 1 FROM MemberMetadataValues v WHERE v.MemberId = m.Id AND v.FieldId = f.Id);",
            new object[] { nowUtc },
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO MemberMetadataValues (MemberId, FieldId, Value, UpdatedAtUtc)
              SELECT m.Id, f.Id, m.ClubName, {0}
              FROM Members m
              JOIN MembershipTypeFields f ON f.Key = 'club_name' AND f.MembershipTypeId = m.MembershipTypeId
              WHERE m.ClubName IS NOT NULL AND TRIM(m.ClubName) <> ''
                AND NOT EXISTS (SELECT 1 FROM MemberMetadataValues v WHERE v.MemberId = m.Id AND v.FieldId = f.Id);",
            new object[] { nowUtc },
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO MemberMetadataValues (MemberId, FieldId, Value, UpdatedAtUtc)
              SELECT m.Id, f.Id, CAST(m.MainMemberId AS TEXT), {0}
              FROM Members m
              JOIN MembershipTypeFields f ON f.Key = 'main_member' AND f.MembershipTypeId = m.MembershipTypeId
              WHERE m.MainMemberId IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM MemberMetadataValues v WHERE v.MemberId = m.Id AND v.FieldId = f.Id);",
            new object[] { nowUtc },
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO MemberMetadataValues (MemberId, FieldId, Value, UpdatedAtUtc)
              SELECT m.Id, f.Id, CAST(m.MembershipDiscount AS TEXT), {0}
              FROM Members m
              JOIN MembershipTypeFields f ON f.Key = 'membership_discount_override' AND f.MembershipTypeId = m.MembershipTypeId
              WHERE m.MembershipDiscount IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM MemberMetadataValues v WHERE v.MemberId = m.Id AND v.FieldId = f.Id);",
            new object[] { nowUtc },
            cancellationToken);
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

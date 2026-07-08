using ClubGear.Data;
using ClubGear.Data.Migrations;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ClubGear.Services.Core;

public class ApplicationSeeder : IApplicationSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApplicationSeeder> _logger;

    public ApplicationSeeder(IServiceProvider serviceProvider, ILogger<ApplicationSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seedTasks = scope.ServiceProvider.GetServices<ISeedTask>().OrderBy(t => t.Order).ToList();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSqliteSchemaCompatibilityAsync(dbContext, cancellationToken);

        foreach (var task in seedTasks)
        {
            _logger.LogInformation("Fuehre Seed-Task {TaskType} aus", task.GetType().Name);
            await task.SeedAsync(dbContext, scope.ServiceProvider, cancellationToken);
        }
    }

    private static async Task EnsureSqliteSchemaCompatibilityAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        await EnsureMembersColumnsAsync(dbContext, cancellationToken);
        await EnsureMemberAddressesTableAsync(dbContext, cancellationToken);
        await EnsureSystemConfigTableAsync(dbContext, cancellationToken);
        await EnsureLegacyDataMappingAsync(dbContext, cancellationToken);
        await AddPluginStatusStore_202605310101.ApplyAsync(dbContext, cancellationToken);
        await AddPluginPackagePath_202605310151.ApplyAsync(dbContext, cancellationToken);
        await AddPluginMigrationState_202605310201.ApplyAsync(dbContext, cancellationToken);
        await AddPluginCategory_202606090101.ApplyAsync(dbContext, cancellationToken);
        await AddPluginDependencies_202606300101.ApplyAsync(dbContext, cancellationToken);
        await AddMembershipTypeModel_202607070101.ApplyAsync(dbContext, cancellationToken);
        await AddSubMemberHierarchy_202607080101.ApplyAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureSystemConfigTableAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS SystemConfigEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_SystemConfigEntries PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Section TEXT NOT NULL DEFAULT '',
                Value TEXT NOT NULL DEFAULT '',
                Description TEXT NOT NULL DEFAULT '',
                UpdatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_SystemConfigEntries_Section_Name ON SystemConfigEntries (Section, Name);",
            cancellationToken);
    }

    private static async Task EnsureMembersColumnsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "Members", cancellationToken);
        if (existingColumns.Count == 0)
        {
            return;
        }

        var alterStatements = new List<string>();

        if (!existingColumns.Contains("Title"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN Title TEXT NULL;");
        }

        if (!existingColumns.Contains("OauthID"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN OauthID TEXT NULL;");
        }

        if (!existingColumns.Contains("OAuthUserName"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN OAuthUserName TEXT NULL;");
        }

        if (!existingColumns.Contains("DateOfBirth"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN DateOfBirth TEXT NULL;");
        }

        if (!existingColumns.Contains("Gender"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN Gender TEXT NULL;");
        }

        if (!existingColumns.Contains("IsCompany"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN IsCompany INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("CompanyName"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN CompanyName TEXT NULL;");
        }

        if (!existingColumns.Contains("IsClub"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN IsClub INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("ClubName"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN ClubName TEXT NULL;");
        }

        if (!existingColumns.Contains("IsVerified"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN IsVerified INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("MemberNumber"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN MemberNumber TEXT NULL;");
        }

        if (!existingColumns.Contains("PhoneNumber"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN PhoneNumber TEXT NULL;");
        }

        if (!existingColumns.Contains("IsActive"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;");
        }

        if (!existingColumns.Contains("JoinedAt"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN JoinedAt TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z';");
        }

        if (!existingColumns.Contains("Joined"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN Joined TEXT NULL;");
        }

        if (!existingColumns.Contains("Leaved"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN Leaved TEXT NULL;");
        }

        if (!existingColumns.Contains("LastUpdated"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN LastUpdated TEXT NULL;");
        }

        if (existingColumns.Contains("RIP") && !existingColumns.Contains("IsDeceased"))
        {
            alterStatements.Add("ALTER TABLE Members RENAME COLUMN RIP TO IsDeceased;");
        }
        else if (!existingColumns.Contains("IsDeceased"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN IsDeceased INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("MembershipDiscount"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN MembershipDiscount INTEGER NULL;");
        }

        if (!existingColumns.Contains("FamilyMembership"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN FamilyMembership INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("MainMemberId"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN MainMemberId INTEGER NULL;");
        }

        if (!existingColumns.Contains("NotifyViaEmail"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN NotifyViaEmail INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("NotifyViaMatrix"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN NotifyViaMatrix INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("DataprivacyAccepted"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN DataprivacyAccepted INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("NewsletterConsent"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN NewsletterConsent INTEGER NOT NULL DEFAULT 0;");
        }

        if (!existingColumns.Contains("ProfileImagePath"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN ProfileImagePath TEXT NULL;");
        }

        if (!existingColumns.Contains("PendingEmail"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN PendingEmail TEXT NULL;");
        }

        if (!existingColumns.Contains("EmailVerificationToken"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN EmailVerificationToken TEXT NULL;");
        }

        if (!existingColumns.Contains("EmailVerificationTokenExpiry"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN EmailVerificationTokenExpiry TEXT NULL;");
        }

        if (!existingColumns.Contains("RentalPayoutOptions"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN RentalPayoutOptions TEXT NULL;");
        }

        if (!existingColumns.Contains("InitPassword"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN InitPassword TEXT NULL;");
        }

        if (!existingColumns.Contains("KeycloakUsername"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN KeycloakUsername TEXT NULL;");
        }

        if (!existingColumns.Contains("ApplicationUserId"))
        {
            alterStatements.Add("ALTER TABLE Members ADD COLUMN ApplicationUserId TEXT NULL;");
        }

        foreach (var sql in alterStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private static async Task EnsureLegacyDataMappingAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await EnsureMembersLegacyMappingAsync(dbContext, cancellationToken);
        await EnsureAddressesLegacyMappingAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureMembersLegacyMappingAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await GetTableColumnsAsync(dbContext, "Members", cancellationToken);
        if (existingColumns.Count == 0)
        {
            return;
        }

        if (existingColumns.Contains("IsNotVerifyed") && existingColumns.Contains("IsVerified"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE Members SET IsVerified = CASE WHEN COALESCE(IsNotVerifyed, 1) = 1 THEN 0 ELSE 1 END;",
                cancellationToken);
        }

        if (existingColumns.Contains("MembershipNumber") && existingColumns.Contains("MemberNumber"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE Members SET MemberNumber = MembershipNumber WHERE (MemberNumber IS NULL OR TRIM(MemberNumber) = '') AND MembershipNumber IS NOT NULL AND TRIM(MembershipNumber) <> '';",
                cancellationToken);
        }

        if (existingColumns.Contains("MemberNumber"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE Members SET MemberNumber = 'M-' || printf('%04d', Id) WHERE MemberNumber IS NULL OR TRIM(MemberNumber) = '';",
                cancellationToken);
        }

        if (existingColumns.Contains("PhoneNumber"))
        {
            var tables = await GetTablesAsync(dbContext, cancellationToken);
            if (tables.Contains("PhoneNumbers"))
            {
                var phoneNumberColumns = await GetTableColumnsAsync(dbContext, "PhoneNumbers", cancellationToken);
                var memberIdColumn = ResolveColumnName(phoneNumberColumns, "MemberId", "MemberID");
                var numberColumn = ResolveColumnName(phoneNumberColumns, "Number", "PhoneNumber", "Value");

                if (memberIdColumn is not null && numberColumn is not null)
                {
                    var sql = $@"
UPDATE Members
SET PhoneNumber = (
    SELECT p.{numberColumn}
    FROM PhoneNumbers p
    WHERE p.{memberIdColumn} = Members.Id
      AND p.{numberColumn} IS NOT NULL
      AND TRIM(p.{numberColumn}) <> ''
    ORDER BY p.Id
    LIMIT 1
)
WHERE (PhoneNumber IS NULL OR TRIM(PhoneNumber) = '')
  AND EXISTS (
    SELECT 1
    FROM PhoneNumbers p
    WHERE p.{memberIdColumn} = Members.Id
      AND p.{numberColumn} IS NOT NULL
      AND TRIM(p.{numberColumn}) <> ''
  );";

                    await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                }
            }
        }

        if (existingColumns.Contains("FirstName"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"UPDATE Members
SET FirstName = CASE
    WHEN CompanyName IS NOT NULL AND TRIM(CompanyName) <> '' THEN CompanyName
    WHEN ClubName IS NOT NULL AND TRIM(ClubName) <> '' THEN ClubName
    ELSE 'Unbekannt'
END
WHERE FirstName IS NULL OR TRIM(FirstName) = '';",
                cancellationToken);
        }

        if (existingColumns.Contains("LastName"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE Members SET LastName = '-' WHERE LastName IS NULL OR TRIM(LastName) = '';",
                cancellationToken);
        }

        if (existingColumns.Contains("JoinedAt"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE Members SET JoinedAt = COALESCE(Joined, LastUpdated, '1970-01-01T00:00:00Z') WHERE JoinedAt IS NULL OR TRIM(JoinedAt) = '';",
                cancellationToken);
        }
    }

    private static async Task EnsureMemberAddressesTableAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingTables = await GetTablesAsync(dbContext, cancellationToken);
        if (existingTables.Contains("MemberAddresses"))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MemberAddresses (
                Id INTEGER NOT NULL CONSTRAINT PK_MemberAddresses PRIMARY KEY AUTOINCREMENT,
                MemberId INTEGER NOT NULL,
                Street TEXT NULL,
                PostalCode TEXT NULL,
                City TEXT NULL,
                Country TEXT NULL,
                IsDefault INTEGER NOT NULL,
                CONSTRAINT FK_MemberAddresses_Members_MemberId FOREIGN KEY (MemberId) REFERENCES Members (Id) ON DELETE CASCADE
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_MemberAddresses_MemberId ON MemberAddresses (MemberId);",
            cancellationToken);
    }

    private static async Task EnsureAddressesLegacyMappingAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingTables = await GetTablesAsync(dbContext, cancellationToken);
        if (!existingTables.Contains("Addresses") || !existingTables.Contains("MemberAddresses"))
        {
            return;
        }

        var addressColumns = await GetTableColumnsAsync(dbContext, "Addresses", cancellationToken);
        if (addressColumns.Count == 0)
        {
            return;
        }

        var idColumn = ResolveColumnName(addressColumns, "Id");
        var memberIdColumn = ResolveColumnName(addressColumns, "MemberId", "MemberID");
        var streetColumn = ResolveColumnName(addressColumns, "Street");
        var postalCodeColumn = ResolveColumnName(addressColumns, "PostalCode", "ZIP", "Zip");
        var cityColumn = ResolveColumnName(addressColumns, "City");
        var countryColumn = ResolveColumnName(addressColumns, "Country");
        var isDefaultColumn = ResolveColumnName(addressColumns, "IsDefault");

        if (idColumn is null || memberIdColumn is null)
        {
            return;
        }

        var sourceStreet = streetColumn ?? "NULL";
        var sourcePostalCode = postalCodeColumn ?? "NULL";
        var sourceCity = cityColumn ?? "NULL";
        var sourceCountry = countryColumn ?? "NULL";
        var sourceIsDefault = isDefaultColumn is null
            ? "0"
            : $"CASE WHEN COALESCE(a.{isDefaultColumn}, 0) = 1 THEN 1 ELSE 0 END";

        var sql = $@"
INSERT INTO MemberAddresses (Id, MemberId, Street, PostalCode, City, Country, IsDefault)
SELECT
    a.{idColumn},
    a.{memberIdColumn},
    a.{sourceStreet},
    a.{sourcePostalCode},
    a.{sourceCity},
    a.{sourceCountry},
    {sourceIsDefault}
FROM Addresses a
WHERE a.{memberIdColumn} IS NOT NULL
  AND EXISTS (SELECT 1 FROM Members m WHERE m.Id = a.{memberIdColumn})
  AND NOT EXISTS (SELECT 1 FROM MemberAddresses ma WHERE ma.Id = a.{idColumn});";

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static string? ResolveColumnName(HashSet<string> columns, params string[] preferredNames)
    {
        foreach (var candidate in preferredNames)
        {
            if (columns.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<HashSet<string>> GetTablesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
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
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    result.Add(reader.GetString(0));
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

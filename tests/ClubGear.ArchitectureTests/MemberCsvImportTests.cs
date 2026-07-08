using System.Text;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 6 — CSV-Import Rückwärtskompatibilität für Mitgliedsart/Metadaten.
/// Verifies that <see cref="MemberFeatureService.ImportCsvAsync"/> keeps reading the exact same
/// fixed legacy column positions ([9]=IsClub,[10]=ClubName,[11]=IsCompany,[12]=CompanyName) but
/// resolves the "Verein"/"Firma"/"Standard" MembershipType (Firma taking precedence when both
/// flags are set) and upserts the corresponding club_name/company_name MemberMetadataValue,
/// instead of writing to the removed legacy scalar Member columns.
/// </summary>
public sealed class MemberCsvImportTests
{
    private static async Task SeedSystemMembershipTypesAsync(ApplicationDbContext db)
    {
        var standard = new MembershipType { Key = "Standard", Name = "Standard", IsActive = true, IsSystemDefined = true };
        var verein = new MembershipType { Key = "Verein", Name = "Verein", IsActive = true, IsSystemDefined = true };
        var firma = new MembershipType { Key = "Firma", Name = "Firma", IsActive = true, IsSystemDefined = true };
        db.MembershipTypes.AddRange(standard, verein, firma);
        await db.SaveChangesAsync();

        db.MembershipTypeFields.AddRange(
            new MembershipTypeField
            {
                MembershipTypeId = verein.Id,
                Key = "club_name",
                Label = "Vereinsname",
                FieldType = MemberMetadataFieldType.Text,
                IsSystemDefined = true
            },
            new MembershipTypeField
            {
                MembershipTypeId = firma.Id,
                Key = "company_name",
                Label = "Firmenname",
                FieldType = MemberMetadataFieldType.Text,
                IsSystemDefined = true
            });
        await db.SaveChangesAsync();
    }

    private static Stream BuildCsvStream(params string[] rows)
    {
        var content = string.Join("\n", rows);
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// Builds a legacy-format CSV row with the exact fixed column layout that ImportCsvAsync
    /// expects: [0]=MemberNumber,[1]=FirstName,[2]=LastName,[3]=Email,[4]=Phone,[5]=IsActive,
    /// [6]=JoinedAt,[7]=Title,[8]=Gender,[9]=IsClub,[10]=ClubName,[11]=IsCompany,[12]=CompanyName.
    /// No column is added, removed, or reordered relative to the pre-Slice-6 format.
    /// </summary>
    private static string BuildRow(
        string memberNumber,
        string firstName,
        string lastName,
        bool isClub,
        string clubName,
        bool isCompany,
        string companyName)
    {
        var columns = new[]
        {
            memberNumber,
            firstName,
            lastName,
            $"{memberNumber.ToLowerInvariant()}@example.org",
            "0170-0000000",
            "true",
            "2020-01-01",
            string.Empty,
            string.Empty,
            isClub ? "true" : "false",
            clubName,
            isCompany ? "true" : "false",
            companyName
        };

        return string.Join(";", columns);
    }

    [Fact]
    public async Task ImportCsvAsync_IsClubOnlyRow_ResolvesVereinTypeAndPersistsClubNameMetadata()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SeedSystemMembershipTypesAsync(db);

        var vereinTypeId = await db.MembershipTypes.Where(t => t.Key == "Verein").Select(t => t.Id).SingleAsync();
        var clubNameFieldId = await db.MembershipTypeFields.Where(f => f.Key == "club_name").Select(f => f.Id).SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService(), new MemberMetadataService());

        var row = BuildRow("M-6001", "Klaus", "Schmidt", isClub: true, clubName: "Modellbauverein e.V.", isCompany: false, companyName: string.Empty);
        var result = await service.ImportCsvAsync(BuildCsvStream(row), "tester");

        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        var member = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6001");
        Assert.Equal(vereinTypeId, member.MembershipTypeId);

        var metadata = await db.MemberMetadataValues.AsNoTracking().SingleAsync(v => v.MemberId == member.Id);
        Assert.Equal(clubNameFieldId, metadata.FieldId);
        Assert.Equal("Modellbauverein e.V.", metadata.Value);
    }

    [Fact]
    public async Task ImportCsvAsync_IsCompanyOnlyRow_ResolvesFirmaTypeAndPersistsCompanyNameMetadata()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SeedSystemMembershipTypesAsync(db);

        var firmaTypeId = await db.MembershipTypes.Where(t => t.Key == "Firma").Select(t => t.Id).SingleAsync();
        var companyNameFieldId = await db.MembershipTypeFields.Where(f => f.Key == "company_name").Select(f => f.Id).SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService(), new MemberMetadataService());

        var row = BuildRow("M-6002", "Erika", "Muster", isClub: false, clubName: string.Empty, isCompany: true, companyName: "Musterfirma GmbH");
        var result = await service.ImportCsvAsync(BuildCsvStream(row), "tester");

        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        var member = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6002");
        Assert.Equal(firmaTypeId, member.MembershipTypeId);

        var metadata = await db.MemberMetadataValues.AsNoTracking().SingleAsync(v => v.MemberId == member.Id);
        Assert.Equal(companyNameFieldId, metadata.FieldId);
        Assert.Equal("Musterfirma GmbH", metadata.Value);
    }

    [Fact]
    public async Task ImportCsvAsync_BothFlagsSetRow_FirmaTakesPrecedenceOverVerein()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SeedSystemMembershipTypesAsync(db);

        var firmaTypeId = await db.MembershipTypes.Where(t => t.Key == "Firma").Select(t => t.Id).SingleAsync();
        var companyNameFieldId = await db.MembershipTypeFields.Where(f => f.Key == "company_name").Select(f => f.Id).SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService(), new MemberMetadataService());

        var row = BuildRow("M-6003", "Doppel", "Flagge", isClub: true, clubName: "Sollte ignoriert werden e.V.", isCompany: true, companyName: "Vorrang GmbH");
        var result = await service.ImportCsvAsync(BuildCsvStream(row), "tester");

        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        var member = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6003");
        Assert.Equal(firmaTypeId, member.MembershipTypeId);

        // Only the company_name metadata value is written; club_name is not persisted for this row,
        // preserving the historical FullName precedence order (Firma > Verein).
        var metadataValues = await db.MemberMetadataValues.AsNoTracking().Where(v => v.MemberId == member.Id).ToListAsync();
        var metadata = Assert.Single(metadataValues);
        Assert.Equal(companyNameFieldId, metadata.FieldId);
        Assert.Equal("Vorrang GmbH", metadata.Value);
    }

    [Fact]
    public async Task ImportCsvAsync_NeitherFlagRow_ResolvesStandardTypeAndPersistsNoMetadata()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SeedSystemMembershipTypesAsync(db);

        var standardTypeId = await db.MembershipTypes.Where(t => t.Key == "Standard").Select(t => t.Id).SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService(), new MemberMetadataService());

        var row = BuildRow("M-6004", "Otto", "Normal", isClub: false, clubName: string.Empty, isCompany: false, companyName: string.Empty);
        var result = await service.ImportCsvAsync(BuildCsvStream(row), "tester");

        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        var member = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6004");
        Assert.Equal(standardTypeId, member.MembershipTypeId);

        Assert.Empty(await db.MemberMetadataValues.AsNoTracking().Where(v => v.MemberId == member.Id).ToListAsync());
    }

    [Fact]
    public async Task ImportCsvAsync_UpdatingExistingMember_ChangesMembershipTypeAndUpsertsMetadataValue()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SeedSystemMembershipTypesAsync(db);

        var vereinTypeId = await db.MembershipTypes.Where(t => t.Key == "Verein").Select(t => t.Id).SingleAsync();
        var firmaTypeId = await db.MembershipTypes.Where(t => t.Key == "Firma").Select(t => t.Id).SingleAsync();
        var companyNameFieldId = await db.MembershipTypeFields.Where(f => f.Key == "company_name").Select(f => f.Id).SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService(), new MemberMetadataService());

        // First import: Verein row creates the member with club_name metadata.
        var firstRow = BuildRow("M-6005", "Update", "Fall", isClub: true, clubName: "Erst-Verein e.V.", isCompany: false, companyName: string.Empty);
        var firstResult = await service.ImportCsvAsync(BuildCsvStream(firstRow), "tester");
        Assert.Equal(1, firstResult.Created);

        var member = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6005");
        Assert.Equal(vereinTypeId, member.MembershipTypeId);

        // Second import (same MemberNumber): flags switch to Firma-only, so the member's
        // MembershipTypeId must change to Firma and the metadata value must reflect company_name.
        var secondRow = BuildRow("M-6005", "Update", "Fall", isClub: false, clubName: string.Empty, isCompany: true, companyName: "Zweite Firma GmbH");
        var secondResult = await service.ImportCsvAsync(BuildCsvStream(secondRow), "tester");
        Assert.Equal(1, secondResult.Updated);

        var updatedMember = await db.Members.AsNoTracking().SingleAsync(m => m.MemberNumber == "M-6005");
        Assert.Equal(firmaTypeId, updatedMember.MembershipTypeId);

        // Unlike the form-based MemberFeatureService.UpdateAsync (Slice 3), ImportCsvAsync only
        // upserts the metadata value for the newly resolved MembershipType's field; it does not
        // remove metadata rows left over from a previous type (no such stale-row cleanup is part
        // of the CSV-import design/DoD for this slice). The old club_name row therefore remains
        // alongside the new company_name row.
        var metadataValues = await db.MemberMetadataValues.AsNoTracking().Where(v => v.MemberId == updatedMember.Id).ToListAsync();
        Assert.Equal(2, metadataValues.Count);
        var companyNameMetadata = Assert.Single(metadataValues, v => v.FieldId == companyNameFieldId);
        Assert.Equal("Zweite Firma GmbH", companyNameMetadata.Value);
    }

    [Fact]
    public void BuildRow_ProducesExactlyThirteenFixedLegacyColumns_NoneAddedRemovedOrReordered()
    {
        var row = BuildRow("M-6006", "Spalten", "Test", isClub: true, clubName: "Verein", isCompany: false, companyName: string.Empty);
        var columns = row.Split(';');

        Assert.Equal(13, columns.Length);
        Assert.Equal("M-6006", columns[0]);
        Assert.Equal("Spalten", columns[1]);
        Assert.Equal("Test", columns[2]);
        Assert.Equal("true", columns[9]);
        Assert.Equal("Verein", columns[10]);
        Assert.Equal("false", columns[11]);
        Assert.Equal(string.Empty, columns[12]);
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

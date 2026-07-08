using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2, checkbox 2 DoD: create/edit/delete a custom Mitgliedsart and its field
/// definitions directly through <see cref="MembershipTypeService"/> (no controller);
/// delete-blocking rules for system-defined types/fields and types referenced by
/// members are verified.
/// </summary>
public sealed class MembershipTypeServiceTests
{
    [Fact]
    public async Task CreateTypeAsync_PersistsNewCustomMembershipType()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var result = await sut.CreateTypeAsync(new MembershipType
        {
            Key = "Ehrenmitglied",
            Name = "Ehrenmitglied",
            Description = "Ehrenhalber ernannte Mitglieder",
            DefaultDiscountPercent = 100,
            SortOrder = 10,
            IsActive = true
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Type);
        Assert.False(result.Type!.IsSystemDefined);

        var persisted = await fixture.DbContext.MembershipTypes.SingleAsync(t => t.Key == "Ehrenmitglied");
        Assert.Equal("Ehrenmitglied", persisted.Name);
        Assert.Equal(100, persisted.DefaultDiscountPercent);
    }

    [Fact]
    public async Task CreateTypeAsync_DuplicateKey_ReturnsDuplicateResult_AndDoesNotPersist()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        await sut.CreateTypeAsync(new MembershipType { Key = "Foerderer", Name = "Foerderer" });
        var second = await sut.CreateTypeAsync(new MembershipType { Key = "Foerderer", Name = "Foerderer (2)" });

        Assert.False(second.Success);
        Assert.Equal(MembershipTypeOperationStatus.DuplicateKey, second.Status);

        var count = await fixture.DbContext.MembershipTypes.CountAsync(t => t.Key == "Foerderer");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateTypeAsync_ChangesEditableFields_ButNotKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var created = await sut.CreateTypeAsync(new MembershipType { Key = "Gast", Name = "Gast", SortOrder = 5 });
        var result = await sut.UpdateTypeAsync(created.Type!.Id, new MembershipType
        {
            Name = "Gastmitglied",
            Description = "Update",
            DefaultDiscountPercent = 20,
            SortOrder = 9,
            IsActive = false
        });

        Assert.True(result.Success);
        Assert.Equal("Gastmitglied", result.Type!.Name);
        Assert.Equal(20, result.Type.DefaultDiscountPercent);
        Assert.False(result.Type.IsActive);
        Assert.Equal("Gast", result.Type.Key);
    }

    [Fact]
    public async Task CreateTypeAsync_PersistsAllowsSubMembersAndSubMemberLabel_RoundTrips()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var created = await sut.CreateTypeAsync(new MembershipType
        {
            Key = "Firma",
            Name = "Firma",
            AllowsSubMembers = true,
            SubMemberLabel = "Mitarbeiter"
        });

        Assert.True(created.Success);

        // Reload from the store (fresh entity) to prove the two columns persisted.
        var reloaded = await sut.GetByIdAsync(created.Type!.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.AllowsSubMembers);
        Assert.Equal("Mitarbeiter", reloaded.SubMemberLabel);
    }

    [Fact]
    public async Task UpdateTypeAsync_TogglesAllowsSubMembersAndLabel_RoundTrips()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var created = await sut.CreateTypeAsync(new MembershipType { Key = "Familie", Name = "Familie" });
        Assert.False((await sut.GetByIdAsync(created.Type!.Id))!.AllowsSubMembers);

        var result = await sut.UpdateTypeAsync(created.Type.Id, new MembershipType
        {
            Name = "Familie",
            AllowsSubMembers = true,
            SubMemberLabel = "Familienmitglied"
        });

        Assert.True(result.Success);

        var reloaded = await sut.GetByIdAsync(created.Type.Id);
        Assert.True(reloaded!.AllowsSubMembers);
        Assert.Equal("Familienmitglied", reloaded.SubMemberLabel);

        // Blank label is normalized back to null.
        await sut.UpdateTypeAsync(created.Type.Id, new MembershipType
        {
            Name = "Familie",
            AllowsSubMembers = false,
            SubMemberLabel = "   "
        });

        var cleared = await sut.GetByIdAsync(created.Type.Id);
        Assert.False(cleared!.AllowsSubMembers);
        Assert.Null(cleared.SubMemberLabel);
    }

    [Fact]
    public async Task UpdateTypeAsync_UnknownId_ReturnsNotFound()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var result = await sut.UpdateTypeAsync(9999, new MembershipType { Name = "X" });

        Assert.False(result.Success);
        Assert.Equal(MembershipTypeOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteTypeAsync_CustomTypeWithoutMembers_Succeeds()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var created = await sut.CreateTypeAsync(new MembershipType { Key = "Temp", Name = "Temp" });
        var result = await sut.DeleteTypeAsync(created.Type!.Id);

        Assert.True(result.Success);
        Assert.False(await fixture.DbContext.MembershipTypes.AnyAsync(t => t.Key == "Temp"));
    }

    [Fact]
    public async Task DeleteTypeAsync_CustomTypeReferencedByMember_IsBlocked()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var created = await sut.CreateTypeAsync(new MembershipType { Key = "Foerderer", Name = "Foerderer" });
        fixture.DbContext.Members.Add(new Member
        {
            MemberNumber = "M-0001",
            FirstName = "Erika",
            LastName = "Musterfrau",
            MembershipTypeId = created.Type!.Id
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await sut.DeleteTypeAsync(created.Type.Id);

        Assert.False(result.Success);
        Assert.Equal(MembershipTypeOperationStatus.Blocked, result.Status);
        Assert.True(await fixture.DbContext.MembershipTypes.AnyAsync(t => t.Key == "Foerderer"));
    }

    [Fact]
    public async Task DeleteTypeAsync_SystemDefinedTypeReferencedByMember_IsBlocked()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var standardType = new MembershipType { Key = "Standard", Name = "Standard", IsSystemDefined = true };
        fixture.DbContext.MembershipTypes.Add(standardType);
        await fixture.DbContext.SaveChangesAsync();

        fixture.DbContext.Members.Add(new Member
        {
            MemberNumber = "M-0002",
            FirstName = "Max",
            LastName = "Mustermann",
            MembershipTypeId = standardType.Id
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await sut.DeleteTypeAsync(standardType.Id);

        Assert.False(result.Success);
        Assert.Equal(MembershipTypeOperationStatus.Blocked, result.Status);
    }

    [Fact]
    public async Task AddFieldAsync_PersistsCustomField_ForExistingType()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var type = await sut.CreateTypeAsync(new MembershipType { Key = "Verein", Name = "Verein" });
        var result = await sut.AddFieldAsync(type.Type!.Id, new MembershipTypeField
        {
            Key = "club_name",
            Label = "Vereinsname",
            FieldType = MemberMetadataFieldType.Text,
            IsRequired = true,
            SortOrder = 0
        });

        Assert.True(result.Success);
        Assert.False(result.Field!.IsSystemDefined);

        var persisted = await fixture.DbContext.MembershipTypeFields
            .SingleAsync(f => f.MembershipTypeId == type.Type.Id && f.Key == "club_name");
        Assert.Equal("Vereinsname", persisted.Label);
        Assert.True(persisted.IsRequired);
    }

    [Fact]
    public async Task AddFieldAsync_DuplicateKeyWithinType_ReturnsDuplicateResult()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var type = await sut.CreateTypeAsync(new MembershipType { Key = "Verein", Name = "Verein" });
        await sut.AddFieldAsync(type.Type!.Id, new MembershipTypeField { Key = "club_name", Label = "Vereinsname" });
        var second = await sut.AddFieldAsync(type.Type.Id, new MembershipTypeField { Key = "club_name", Label = "Vereinsname (2)" });

        Assert.False(second.Success);
        Assert.Equal(MembershipTypeOperationStatus.DuplicateKey, second.Status);
    }

    [Fact]
    public async Task AddFieldAsync_UnknownMembershipType_ReturnsNotFound()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var result = await sut.AddFieldAsync(9999, new MembershipTypeField { Key = "x", Label = "X" });

        Assert.False(result.Success);
        Assert.Equal(MembershipTypeOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateFieldAsync_ChangesLabelAndFieldType()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var type = await sut.CreateTypeAsync(new MembershipType { Key = "Verein", Name = "Verein" });
        var field = await sut.AddFieldAsync(type.Type!.Id, new MembershipTypeField { Key = "club_name", Label = "Vereinsname" });

        var result = await sut.UpdateFieldAsync(field.Field!.Id, new MembershipTypeField
        {
            Key = "club_name",
            Label = "Vereinsname (offiziell)",
            FieldType = MemberMetadataFieldType.Text,
            IsRequired = true,
            SortOrder = 3
        });

        Assert.True(result.Success);
        Assert.Equal("Vereinsname (offiziell)", result.Field!.Label);
        Assert.True(result.Field.IsRequired);
        Assert.Equal(3, result.Field.SortOrder);
    }

    [Fact]
    public async Task RemoveFieldAsync_CustomField_AlwaysAllowed_EvenIfReferencedByData()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        var type = await sut.CreateTypeAsync(new MembershipType { Key = "Verein", Name = "Verein" });
        var field = await sut.AddFieldAsync(type.Type!.Id, new MembershipTypeField { Key = "club_name", Label = "Vereinsname" });

        fixture.DbContext.Members.Add(new Member { MemberNumber = "M-0003", FirstName = "A", LastName = "B" });
        await fixture.DbContext.SaveChangesAsync();
        var member = await fixture.DbContext.Members.FirstAsync();

        fixture.DbContext.MemberMetadataValues.Add(new MemberMetadataValue
        {
            MemberId = member.Id,
            FieldId = field.Field!.Id,
            Value = "Testverein"
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await sut.RemoveFieldAsync(field.Field.Id);

        Assert.True(result.Success);
        Assert.False(await fixture.DbContext.MembershipTypeFields.AnyAsync(f => f.Id == field.Field.Id));
    }

    [Fact]
    public async Task RemoveFieldAsync_SystemDefinedFieldReferencedByData_IsBlocked()
    {
        await using var fixture = await CreateFixtureAsync();

        var type = new MembershipType { Key = "Verein", Name = "Verein", IsSystemDefined = true };
        fixture.DbContext.MembershipTypes.Add(type);
        await fixture.DbContext.SaveChangesAsync();

        var field = new MembershipTypeField
        {
            MembershipTypeId = type.Id,
            Key = "club_name",
            Label = "Vereinsname",
            IsSystemDefined = true
        };
        fixture.DbContext.MembershipTypeFields.Add(field);
        await fixture.DbContext.SaveChangesAsync();

        fixture.DbContext.Members.Add(new Member { MemberNumber = "M-0004", FirstName = "A", LastName = "B", MembershipTypeId = type.Id });
        await fixture.DbContext.SaveChangesAsync();
        var member = await fixture.DbContext.Members.FirstAsync();

        fixture.DbContext.MemberMetadataValues.Add(new MemberMetadataValue
        {
            MemberId = member.Id,
            FieldId = field.Id,
            Value = "Testverein"
        });
        await fixture.DbContext.SaveChangesAsync();

        var sut = new MembershipTypeService(fixture.DbContext);
        var result = await sut.RemoveFieldAsync(field.Id);

        Assert.False(result.Success);
        Assert.Equal(MembershipTypeOperationStatus.Blocked, result.Status);
        Assert.True(await fixture.DbContext.MembershipTypeFields.AnyAsync(f => f.Id == field.Id));
    }

    [Fact]
    public async Task RemoveFieldAsync_SystemDefinedFieldNotReferencedByData_IsAllowed()
    {
        await using var fixture = await CreateFixtureAsync();

        var type = new MembershipType { Key = "Verein", Name = "Verein", IsSystemDefined = true };
        fixture.DbContext.MembershipTypes.Add(type);
        await fixture.DbContext.SaveChangesAsync();

        var field = new MembershipTypeField
        {
            MembershipTypeId = type.Id,
            Key = "club_magazine",
            Label = "Vereinszeitschrift",
            IsSystemDefined = true
        };
        fixture.DbContext.MembershipTypeFields.Add(field);
        await fixture.DbContext.SaveChangesAsync();

        var sut = new MembershipTypeService(fixture.DbContext);
        var result = await sut.RemoveFieldAsync(field.Id);

        Assert.True(result.Success);
        Assert.False(await fixture.DbContext.MembershipTypeFields.AnyAsync(f => f.Id == field.Id));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTypesOrderedBySortOrder_WithFieldsOrderedBySortOrder()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new MembershipTypeService(fixture.DbContext);

        await sut.CreateTypeAsync(new MembershipType { Key = "B", Name = "B", SortOrder = 2 });
        var a = await sut.CreateTypeAsync(new MembershipType { Key = "A", Name = "A", SortOrder = 1 });

        await sut.AddFieldAsync(a.Type!.Id, new MembershipTypeField { Key = "second", Label = "Second", SortOrder = 2 });
        await sut.AddFieldAsync(a.Type.Id, new MembershipTypeField { Key = "first", Label = "First", SortOrder = 1 });

        var all = await sut.GetAllAsync();

        Assert.Equal(new[] { "A", "B" }, all.Select(t => t.Key));
        var aType = all.Single(t => t.Key == "A");
        Assert.Equal(new[] { "first", "second" }, aType.Fields.Select(f => f.Key));
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return new Fixture(connection, dbContext);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(SqliteConnection connection, ApplicationDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

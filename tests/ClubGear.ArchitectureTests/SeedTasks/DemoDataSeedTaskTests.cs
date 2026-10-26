using System.Globalization;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Core.SeedTasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests.SeedTasks;

/// <summary>
/// The demo dataset is opt-in, idempotent, and reuses the system membership types created by
/// the migrations instead of adding parallel ones. The last part matters: an earlier draft
/// introduced its own "familie" type next to the system "Familie", which would have shown two
/// near-identical entries in the admin UI.
/// </summary>
public sealed class DemoDataSeedTaskTests
{
    [Fact]
    public async Task SeedAsync_WithoutFlag_SeedsNothing()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new DemoDataSeedTask();

        await task.SeedAsync(fixture.DbContext, BuildServices(enabled: null), CancellationToken.None);

        Assert.Empty(await fixture.DbContext.Members.ToListAsync());
        Assert.Empty(await fixture.DbContext.MembershipTypes.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_WithFlagFalse_SeedsNothing()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new DemoDataSeedTask();

        await task.SeedAsync(fixture.DbContext, BuildServices(enabled: false), CancellationToken.None);

        Assert.Empty(await fixture.DbContext.Members.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_WithFlag_SeedsRosterAcrossTypes()
    {
        await using var fixture = await CreateFixtureAsync();
        await SeedSystemTypesAsync(fixture.DbContext);

        await new DemoDataSeedTask().SeedAsync(fixture.DbContext, BuildServices(enabled: true), CancellationToken.None);

        var members = await fixture.DbContext.Members.ToListAsync();
        Assert.Equal(30, members.Count);
        Assert.Contains(members, m => m.MemberNumber == "M-1001");

        // Departed and deceased members are present so the list filters have something to hide.
        Assert.Contains(members, m => !m.IsActive && m.Leaved is not null);
        Assert.Contains(members, m => m.IsDeceased);
    }

    [Fact]
    public async Task SeedAsync_ReusesSystemTypes_AndAddsOnlyClubTypes()
    {
        await using var fixture = await CreateFixtureAsync();
        await SeedSystemTypesAsync(fixture.DbContext);

        await new DemoDataSeedTask().SeedAsync(fixture.DbContext, BuildServices(enabled: true), CancellationToken.None);

        var keys = await fixture.DbContext.MembershipTypes.Select(t => t.Key).ToListAsync();

        Assert.Equal(new[] { "Standard", "Familie", "Firma", "Verein", "jugend", "passiv", "ehrenmitglied" }.Order(),
                     keys.Order());

        // No second household type alongside the system "Familie".
        Assert.Single(keys, k => string.Equals(k, "Familie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SeedAsync_LinksSubMembers_ThroughSystemMainMemberField()
    {
        await using var fixture = await CreateFixtureAsync();
        await SeedSystemTypesAsync(fixture.DbContext);

        await new DemoDataSeedTask().SeedAsync(fixture.DbContext, BuildServices(enabled: true), CancellationToken.None);

        var links = await fixture.DbContext.MemberMetadataValues
            .Include(v => v.Field)
            .Where(v => v.Field!.Key == "main_member")
            .ToListAsync();

        Assert.Equal(6, links.Count);

        // Every reference resolves to a real member, and no sub-member points at itself.
        var memberIds = await fixture.DbContext.Members.Select(m => m.Id).ToListAsync();
        foreach (var link in links)
        {
            Assert.True(int.TryParse(link.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId));
            Assert.Contains(parentId, memberIds);
            Assert.NotEqual(link.MemberId, parentId);
        }

        // Single-level hierarchy: no parent is itself a sub-member.
        var subMemberIds = links.Select(l => l.MemberId).ToHashSet();
        var parentIds = links.Select(l => int.Parse(l.Value!, CultureInfo.InvariantCulture)).ToHashSet();
        Assert.Empty(parentIds.Intersect(subMemberIds));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicate()
    {
        await using var fixture = await CreateFixtureAsync();
        await SeedSystemTypesAsync(fixture.DbContext);
        var task = new DemoDataSeedTask();
        var services = BuildServices(enabled: true);

        await task.SeedAsync(fixture.DbContext, services, CancellationToken.None);
        var afterFirst = (
            Members: await fixture.DbContext.Members.CountAsync(),
            Types: await fixture.DbContext.MembershipTypes.CountAsync(),
            Values: await fixture.DbContext.MemberMetadataValues.CountAsync());

        await task.SeedAsync(fixture.DbContext, services, CancellationToken.None);

        Assert.Equal(afterFirst.Members, await fixture.DbContext.Members.CountAsync());
        Assert.Equal(afterFirst.Types, await fixture.DbContext.MembershipTypes.CountAsync());
        Assert.Equal(afterFirst.Values, await fixture.DbContext.MemberMetadataValues.CountAsync());
    }

    private static IServiceProvider BuildServices(bool? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is { } flag)
        {
            values[DemoDataSeedTask.EnabledConfigKey] = flag ? "true" : "false";
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
    }

    /// <summary>
    /// Mirrors the subset of migration 202607070101 the demo data depends on: the four system
    /// types and the metadata fields it fills in.
    /// </summary>
    private static async Task SeedSystemTypesAsync(ApplicationDbContext dbContext)
    {
        var standard = new MembershipType { Key = "Standard", Name = "Standard", IsSystemDefined = true, SortOrder = 0 };
        var verein = new MembershipType { Key = "Verein", Name = "Verein", IsSystemDefined = true, SortOrder = 1, AllowsSubMembers = true };
        var firma = new MembershipType { Key = "Firma", Name = "Firma", IsSystemDefined = true, SortOrder = 2, AllowsSubMembers = true };
        var familie = new MembershipType { Key = "Familie", Name = "Familie", IsSystemDefined = true, SortOrder = 3, AllowsSubMembers = true, SubMemberLabel = "Familienmitglied" };

        standard.Fields.Add(Field("membership_discount_override", "Mitgliedsrabatt (Override)", MemberMetadataFieldType.Number, 0));
        verein.Fields.Add(Field("club_name", "Vereinsname", MemberMetadataFieldType.Text, 0, required: true));
        firma.Fields.Add(Field("company_name", "Firmenname", MemberMetadataFieldType.Text, 0, required: true));
        familie.Fields.Add(Field("main_member", "Hauptmitglied", MemberMetadataFieldType.MemberReference, 0));
        familie.Fields.Add(Field("membership_fee", "Mitgliedsbeitrag", MemberMetadataFieldType.Number, 1));

        dbContext.MembershipTypes.AddRange(standard, verein, firma, familie);
        await dbContext.SaveChangesAsync();
    }

    private static MembershipTypeField Field(string key, string label, MemberMetadataFieldType type, int sortOrder, bool required = false)
        => new()
        {
            Key = key,
            Label = label,
            FieldType = type,
            SortOrder = sortOrder,
            IsRequired = required,
            IsSystemDefined = true
        };

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

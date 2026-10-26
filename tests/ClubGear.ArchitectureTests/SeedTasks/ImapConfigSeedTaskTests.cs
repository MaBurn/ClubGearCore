using System.Reflection;
using ClubGear.Controllers;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Services.Core.SeedTasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests.SeedTasks;

/// <summary>
/// Slice 1, checkbox 1.2 and Slice 3, checkbox 3.1 DoD: the ten IMAP/Sent/bounce keys are seeded in
/// section "Email" with the documented defaults, re-running the seed task leaves existing values
/// untouched, and the IMAP config card is exposed by Admin_Controller.BuildConfigCards().
/// </summary>
public sealed class ImapConfigSeedTaskTests
{
    private static readonly (string Name, string Value)[] ExpectedDefaults =
    {
        (nameof(EmailSettings.ImapHost), ""),
        (nameof(EmailSettings.ImapPort), "993"),
        (nameof(EmailSettings.ImapUseSsl), "true"),
        (nameof(EmailSettings.ImapUsername), ""),
        (nameof(EmailSettings.ImapPassword), ""),
        (nameof(EmailSettings.SentFolder), "Sent"),
        (nameof(EmailSettings.AppendSentEnabled), "false"),
        (nameof(EmailSettings.BounceFolder), "INBOX"),
        (nameof(EmailSettings.BounceCheckEnabled), "false"),
        (nameof(EmailSettings.BouncePollIntervalMinutes), "5")
    };

    [Fact]
    public async Task SeedAsync_SeedsTenImapKeys_InEmailSection_WithDocumentedDefaults()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new SystemConfigSeedTask();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);

        foreach (var (name, value) in ExpectedDefaults)
        {
            var entry = await fixture.DbContext.SystemConfigEntries
                .SingleAsync(e => e.Section == "Email" && e.Name == name);
            Assert.Equal(value, entry.Value);
        }
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent_AndKeepsExistingValues()
    {
        await using var fixture = await CreateFixtureAsync();
        var task = new SystemConfigSeedTask();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);

        var host = await fixture.DbContext.SystemConfigEntries
            .SingleAsync(e => e.Section == "Email" && e.Name == nameof(EmailSettings.ImapHost));
        host.Value = "imap.example.org";
        await fixture.DbContext.SaveChangesAsync();

        await task.SeedAsync(fixture.DbContext, serviceProvider, CancellationToken.None);

        foreach (var (name, _) in ExpectedDefaults)
        {
            var count = await fixture.DbContext.SystemConfigEntries
                .CountAsync(e => e.Section == "Email" && e.Name == name);
            Assert.Equal(1, count);
        }

        var reloaded = await fixture.DbContext.SystemConfigEntries
            .AsNoTracking()
            .SingleAsync(e => e.Section == "Email" && e.Name == nameof(EmailSettings.ImapHost));
        Assert.Equal("imap.example.org", reloaded.Value);
    }

    [Fact]
    public void BuildConfigCards_ExposesImapCard_WithAllTenKeys()
    {
        var method = typeof(AdminController)
            .GetMethod("BuildConfigCards", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var cards = (IReadOnlyList<AdminConfigCardViewModel>)method!.Invoke(null, null)!;
        var card = Assert.Single(cards, c => c.ModalId == "cfgModal-imap");

        Assert.All(card.Fields, f => Assert.Equal("Email", f.Section));
        foreach (var (name, _) in ExpectedDefaults)
        {
            Assert.Contains(card.Fields, f => f.Key == name);
        }

        Assert.Equal("password", card.Fields.Single(f => f.Key == nameof(EmailSettings.ImapPassword)).InputType);
        Assert.Equal("number", card.Fields.Single(f => f.Key == nameof(EmailSettings.ImapPort)).InputType);
        Assert.Equal("checkbox", card.Fields.Single(f => f.Key == nameof(EmailSettings.ImapUseSsl)).InputType);
        Assert.Equal("checkbox", card.Fields.Single(f => f.Key == nameof(EmailSettings.AppendSentEnabled)).InputType);
        Assert.Equal("checkbox", card.Fields.Single(f => f.Key == nameof(EmailSettings.BounceCheckEnabled)).InputType);
        Assert.Equal("number", card.Fields.Single(f => f.Key == nameof(EmailSettings.BouncePollIntervalMinutes)).InputType);
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

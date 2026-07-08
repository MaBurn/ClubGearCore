using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Finance;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 6 verification (updated for Finance iteration 7 audit display):
///   6.1 GetAuditLogAsync returns seeded audit rows ordered newest-first.
///   6.2 GetTabsAsync HTML contains the "Änderungsprotokoll" heading and six column headers
///       (MemberId, Aktion, Alter Status, Neuer Status, Zeitstempel, Ausgeführt von).
///   6.3 GetTabsAsync HTML contains each seeded Action value and PerformedByCategory value.
///   6.4 GetTabsAsync HTML contains the Zeitstempel formatted as dd.MM.yyyy HH:mm.
/// </summary>
public sealed class FinanceSlice6AuditLogDisplayTests
{
    // ---------------------------------------------------------------------------
    // Shared setup
    // ---------------------------------------------------------------------------

    private static async Task<(FinanceDataService data, Slice6SqlitePluginStore store, Slice6PluginHostContext host)>
        CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice6SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host  = new Slice6PluginHostContext(store);

        foreach (var migration in new FinancePluginModule().GetMigrations())
            await migration.ApplyAsync(store);

        return (new FinanceDataService(), store, host);
    }

    /// <summary>Insert rows directly into bank_account_audit, bypassing business logic.</summary>
    private static async Task SeedAuditRowAsync(
        Slice6SqlitePluginStore store,
        int memberId,
        int? bankAccountId,
        string action,
        string performedBy,
        DateTimeOffset createdAtUtc,
        string performedByCategory = "Verwaltung")
    {
        var table = store.GetTableName("bank_account_audit");
        await store.ExecuteAsync(
            $@"INSERT INTO {table} (MemberId, BankAccountId, Action, BeforeJson, AfterJson, PerformedBy, PerformedByCategory, CreatedAtUtc)
               VALUES (@memberId, @bankAccountId, @action, NULL, NULL, @performedBy, @performedByCategory, @createdAtUtc);",
            new Dictionary<string, string?>
            {
                ["memberId"]             = memberId.ToString(CultureInfo.InvariantCulture),
                ["bankAccountId"]        = bankAccountId?.ToString(CultureInfo.InvariantCulture),
                ["action"]               = action,
                ["performedBy"]          = performedBy,
                ["performedByCategory"]  = performedByCategory,
                ["createdAtUtc"]         = createdAtUtc.ToString("O", CultureInfo.InvariantCulture)
            });
    }

    // ---------------------------------------------------------------------------
    // 6.1 — GetAuditLogAsync returns rows ordered newest-first
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetAuditLogAsync_ReturnsSeedRowsNewestFirst()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 100;

        var t1 = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 1, 2, 11, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 1, 3, 12, 0, 0, TimeSpan.Zero);

        await SeedAuditRowAsync(store, memberId, 1, "ActionA", "user1@example.com", t1);
        await SeedAuditRowAsync(store, memberId, 2, "ActionB", "user2@example.com", t2);
        await SeedAuditRowAsync(store, memberId, 3, "ActionC", "user3@example.com", t3);

        var log = await data.GetAuditLogAsync(host, memberId, CancellationToken.None);

        Assert.Equal(3, log.Count);
        // Newest first
        Assert.Equal("ActionC", log[0].Action);
        Assert.Equal("ActionB", log[1].Action);
        Assert.Equal("ActionA", log[2].Action);

        Assert.Equal("user3@example.com", log[0].PerformedBy);
        Assert.Equal("user2@example.com", log[1].PerformedBy);
        Assert.Equal("user1@example.com", log[2].PerformedBy);
    }

    // ---------------------------------------------------------------------------
    // 6.2 — GetTabsAsync HTML contains "Änderungsprotokoll" heading and six column headers
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetTabsAsync_ContainsAuditLogHeading_WhenRowsExist()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 200;

        await SeedAuditRowAsync(store, memberId, null, "BankAccountReplaced", "kw@club.de",
            new DateTimeOffset(2025, 6, 15, 9, 30, 0, TimeSpan.Zero));

        var provider = new FinanceEditTabProvider();
        var member   = MakeMember(memberId);
        var tabs     = await provider.GetTabsAsync(member, host, CancellationToken.None);

        Assert.Single(tabs);
        var html = tabs[0].Content;
        Assert.Contains("<h6 class=\"mt-4\">Änderungsprotokoll</h6>", html, StringComparison.Ordinal);
        Assert.Contains("<th>MemberId</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Aktion</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Alter Status</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Neuer Status</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Zeitstempel</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Ausgeführt von</th>", html, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // 6.3 — GetTabsAsync HTML contains Action values and PerformedByCategory values
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetTabsAsync_ContainsActionAndPerformedBy_ForEachSeedRow()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 300;

        var baseTime = new DateTimeOffset(2025, 3, 1, 8, 0, 0, TimeSpan.Zero);
        await SeedAuditRowAsync(store, memberId, 10, "BankAccountReplaced",    "admin@club.de",    baseTime,             "Verwaltung");
        await SeedAuditRowAsync(store, memberId, 10, "BankAccountVerified",    "kassenwart@test",  baseTime.AddHours(1), "Verwaltung");
        await SeedAuditRowAsync(store, memberId, 10, "BankAccountInvalidated", "system",           baseTime.AddHours(2), "System");

        var provider = new FinanceEditTabProvider();
        var member   = MakeMember(memberId);
        var tabs     = await provider.GetTabsAsync(member, host, CancellationToken.None);
        var html     = tabs[0].Content;

        // All three Action values must appear
        Assert.Contains("BankAccountReplaced",    html, StringComparison.Ordinal);
        Assert.Contains("BankAccountVerified",    html, StringComparison.Ordinal);
        Assert.Contains("BankAccountInvalidated", html, StringComparison.Ordinal);

        // PerformedByCategory values (not raw email addresses) appear in the last column
        Assert.Contains("Verwaltung", html, StringComparison.Ordinal);
        Assert.Contains("System",     html, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // 6.4 — GetTabsAsync HTML contains Datum formatted as dd.MM.yyyy HH:mm
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetTabsAsync_ContainsDatumFormattedAsDdMmYyyyHhMm()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 400;

        // Use UTC time — ToLocalTime() in the provider will convert it; since we're running
        // in the test environment we compare the UTC representation rendered as local.
        // Store a UTC timestamp and compute what the formatted string should be after ToLocalTime().
        var utcTime = new DateTimeOffset(2025, 11, 5, 14, 25, 0, TimeSpan.Zero);
        await SeedAuditRowAsync(store, memberId, 5, "SepaDirectDebitIdChanged", "system", utcTime);

        var provider = new FinanceEditTabProvider();
        var member   = MakeMember(memberId);
        var tabs     = await provider.GetTabsAsync(member, host, CancellationToken.None);
        var html     = tabs[0].Content;

        // Compute the expected formatted string using the same conversion as the provider
        var expectedDate = utcTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        Assert.Contains(expectedDate, html, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------

    internal sealed class Slice6SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice6SqlitePluginStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId    = moduleId;
            TablePrefix = "plg_finance_";
        }

        public string ModuleId    { get; }
        public string TablePrefix { get; }

        public string GetTableName(string localName) => $"{TablePrefix}{localName}";

        public async Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                    command.Parameters.AddWithValue($"@{key.TrimStart('@')}", value ?? (object)DBNull.Value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PluginDataRow>> QueryAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                    command.Parameters.AddWithValue($"@{key.TrimStart('@')}", value ?? (object)DBNull.Value);
            }

            var rows = new List<PluginDataRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    values[reader.GetName(i)] = reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i)?.ToString();
                }
                rows.Add(new PluginDataRow(values));
            }

            return rows;
        }
    }

    internal sealed class Slice6PluginHostContext : IPluginHostContext
    {
        public Slice6PluginHostContext(Slice6SqlitePluginStore store) => Persistence = store;

        public IPluginMetadataFacade     Metadata      => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberReader       Members       => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException("Not needed in tests.");
        public IPluginDataStore          Persistence   { get; }
        public IPluginPermissionFacade   Permissions   => throw new NotSupportedException("Not needed in tests.");
    }

    private static PluginMemberDetail MakeMember(int id) =>
        new(id, "TM-001", "Test Member", "Test", "Member", "test@example.com", null, true);
}

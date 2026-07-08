using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Finance;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 4 (Finance iteration 3) verification:
///   4.1 Calling ReplaceAccountAsync twice creates account B in PendingVerification
///       and transitions account A to Invalid.
///   4.2 GetAccountsAsync after two replacements returns A=Invalid, B=PendingVerification.
///   4.3 Audit table contains a BankAccountReplaced entry with non-empty PerformedBy.
///   4.4 For a DE IBAN with a known BLZ, the stored record has non-null Bic and BankName
///       even when not provided in the input.
/// </summary>
public sealed class FinanceSlice4ReplaceAccountTests
{
    // 4.1 + 4.2 — Second ReplaceAccountAsync call leaves account A=Invalid, account B=PendingVerification
    [Fact]
    public async Task ReplaceAccountTwice_FirstAccountBecomesInvalid_SecondIsPending()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice4SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host = new Slice4PluginHostContext(store);
        var migrations = new FinancePluginModule().GetMigrations();

        foreach (var migration in migrations)
        {
            await migration.ApplyAsync(store);
        }

        var data = new FinanceDataService();
        const int memberId = 42;

        // First call: creates account A (PendingVerification)
        var inputA = new BankAccountInput(
            "Max Mustermann",
            null, null,
            "DE89370400440532013000",
            "COBADEFFXXX",
            "Commerzbank",
            null);
        var result1 = await data.ReplaceAccountAsync(host, memberId, inputA, "user1", CancellationToken.None);
        Assert.True(result1.Success, $"First replace failed: {result1.Message}");

        // Second call: creates account B (PendingVerification), account A should become Invalid
        // DE87200100200000044242 is a valid DE IBAN (BLZ 20010020, passes mod-97)
        var inputB = new BankAccountInput(
            "Max Mustermann",
            null, null,
            "DE87200100200000044242",
            "PBNKDEFFXXX",
            "Postbank",
            null);
        var result2 = await data.ReplaceAccountAsync(host, memberId, inputB, "user2", CancellationToken.None);
        Assert.True(result2.Success, $"Second replace failed: {result2.Message}");

        // Verify accounts state
        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);

        var accountA = accounts.FirstOrDefault(a => a.Iban == "DE89370400440532013000");
        var accountB = accounts.FirstOrDefault(a => a.Iban == "DE87200100200000044242");

        Assert.NotNull(accountA);
        Assert.NotNull(accountB);
        Assert.Equal(BankAccountStatus.Invalid, accountA!.Status);
        Assert.Equal(BankAccountStatus.PendingVerification, accountB!.Status);
    }

    // 4.3 — Audit entry contains BankAccountReplaced with non-empty PerformedBy
    [Fact]
    public async Task ReplaceAccount_WritesAuditEntry_WithPerformedBy()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice4SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host = new Slice4PluginHostContext(store);
        var migrations = new FinancePluginModule().GetMigrations();

        foreach (var migration in migrations)
        {
            await migration.ApplyAsync(store);
        }

        var data = new FinanceDataService();
        const int memberId = 99;
        const string performedBy = "testuser@example.com";

        var input = new BankAccountInput(
            "Erika Musterfrau",
            null, null,
            "DE89370400440532013000",
            "COBADEFFXXX",
            "Commerzbank",
            null);

        var result = await data.ReplaceAccountAsync(host, memberId, input, performedBy, CancellationToken.None);
        Assert.True(result.Success, $"Replace failed: {result.Message}");

        // Query audit table
        var auditTable = store.GetTableName("bank_account_audit");
        var rows = await store.QueryAsync(
            $"SELECT Action, PerformedBy FROM {auditTable} WHERE MemberId = @memberId AND Action = @action;",
            new Dictionary<string, string?>
            {
                ["memberId"] = memberId.ToString(CultureInfo.InvariantCulture),
                ["action"] = "BankAccountReplaced"
            });

        Assert.NotEmpty(rows);
        var auditRow = rows[0];
        Assert.Equal("BankAccountReplaced", auditRow.Values.GetValueOrDefault("Action"));
        Assert.Equal(performedBy, auditRow.Values.GetValueOrDefault("PerformedBy"));
    }

    // 4.4 — DE IBAN with known BLZ auto-fills BIC and BankName when not provided
    [Fact]
    public async Task ReplaceAccount_WithDeIbanAndNoBic_AutoFillsBicAndBankName()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice4SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host = new Slice4PluginHostContext(store);
        var migrations = new FinancePluginModule().GetMigrations();

        foreach (var migration in migrations)
        {
            await migration.ApplyAsync(store);
        }

        var data = new FinanceDataService();
        const int memberId = 77;

        // Use the Deutsche Bank DE IBAN with BLZ 10070000 — known in Bundesbank data
        // IBAN DE89 3704 0044 0532 0130 00 => BLZ 37040044 (Commerzbank Köln)
        // Use a well-known BLZ: 10070000 = Deutsche Bank Berlin
        // IBAN format: DE + check + BLZ(8) + account(10)
        // DE10100700000000123456 is a syntactically valid IBAN for BLZ 10070000
        // Let's use the standard test IBAN for Postbank: DE89370400440532013000 (BLZ 37040044, Commerzbank)
        // Actually use BLZ 10020000 (Berliner Bank/Deutsche Bank), or 20010020 (Postbank Hamburg)
        // Use DE IBAN with BLZ 10070000: DE71100700000000029200 — may not pass modulo check
        // Use the simplest approach: use an IBAN that passes modulo-97 AND whose BLZ is in the CSV
        // BLZ 37040044 = Commerzbank AG Köln (in bundesbank data)
        // The test IBAN DE89370400440532013000 has BLZ 37040044, passes modulo-97
        var input = new BankAccountInput(
            "Hans Test",
            null, null,
            "DE89370400440532013000",
            null,  // No BIC provided
            null,  // No BankName provided
            null);

        var result = await data.ReplaceAccountAsync(host, memberId, input, "system", CancellationToken.None);
        Assert.True(result.Success, $"Replace failed: {result.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        Assert.Single(accounts);

        var stored = accounts[0];
        // BLZ 37040044 should be in the Bundesbank data and return a BIC and bank name
        // If BLZ lookup returns null (e.g. BLZ not in CSV), the field stays null — but
        // we assert it's non-null because 37040044 is a major German bank BLZ
        Assert.NotNull(stored.Bic);
        Assert.NotNull(stored.BankName);
        Assert.NotEmpty(stored.Bic);
        Assert.NotEmpty(stored.BankName);
    }

    // Additional: BankAccountAuditRecord type is accessible
    [Fact]
    public void BankAccountAuditRecord_TypeExists_AndHasExpectedProperties()
    {
        var record = new BankAccountAuditRecord(
            Id: 1,
            MemberId: 42,
            BankAccountId: 10,
            Action: "BankAccountReplaced",
            BeforeJson: null,
            AfterJson: "{}",
            PerformedBy: "admin",
            CreatedAtUtc: DateTimeOffset.UtcNow);

        Assert.Equal(1, record.Id);
        Assert.Equal(42, record.MemberId);
        Assert.Equal("BankAccountReplaced", record.Action);
        Assert.Equal("admin", record.PerformedBy);
    }

    /// <summary>
    /// SQLite-backed IPluginDataStore for Slice 4 tests.
    /// Reuses the same pattern as Slice3SqlitePluginStore.
    /// </summary>
    internal sealed class Slice4SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice4SqlitePluginStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId = moduleId;
            TablePrefix = "plg_finance_";
        }

        public string ModuleId { get; }
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
                {
                    command.Parameters.AddWithValue($"@{key.TrimStart('@')}", value ?? (object)DBNull.Value);
                }
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
                {
                    command.Parameters.AddWithValue($"@{key.TrimStart('@')}", value ?? (object)DBNull.Value);
                }
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

    /// <summary>
    /// Minimal IPluginHostContext that wraps a Slice4SqlitePluginStore.
    /// </summary>
    internal sealed class Slice4PluginHostContext : IPluginHostContext
    {
        public Slice4PluginHostContext(Slice4SqlitePluginStore store)
        {
            Persistence = store;
        }

        public IPluginMetadataFacade Metadata => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberReader Members => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException("Not needed in tests.");
        public IPluginDataStore Persistence { get; }
        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }
}

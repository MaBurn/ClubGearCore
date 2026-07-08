using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Finance;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 5 (Finance iteration 3) verification:
///   5.1 VerifyAccountAsync transitions a PendingVerification account to Verified;
///       pre-existing MarkedForDeletion rows for the same member become Invalid;
///       an audit row "BankAccountVerified" is written with the correct PerformedBy.
///   5.2 InvalidateAccountAsync (PendingVerification path) transitions to Invalid;
///       audit row "BankAccountInvalidated" written.
///   5.3 InvalidateAccountAsync (MarkedForDeletion path) transitions to Invalid.
///   5.4 VerifyAccountAsync on a Verified account returns (false, "invalid-status", _).
/// </summary>
public sealed class FinanceSlice5VerifyInvalidateTests
{
    // ---------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------

    private static async Task<(FinanceDataService data, Slice5SqlitePluginStore store, Slice5PluginHostContext host)>
        CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice5SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host  = new Slice5PluginHostContext(store);

        foreach (var migration in new FinancePluginModule().GetMigrations())
            await migration.ApplyAsync(store);

        return (new FinanceDataService(), store, host);
    }

    private static BankAccountInput MakeInput(string iban) => new(
        AccountHolder:     "Test Inhaber",
        AccountNumber:     null,
        BankCode:          null,
        Iban:              iban,
        Bic:               "COBADEFFXXX",
        BankName:          "Commerzbank",
        SepaDirectDebitId: null);

    // ---------------------------------------------------------------------------
    // 5.1 — VerifyAccountAsync happy path
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task VerifyAccount_TransitionsToVerified_AndMarkedForDeletionToInvalid_AndWritesAudit()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 10;
        const string performedBy = "kassenwart@example.com";

        // Create account A (PendingVerification)
        var resultA = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(resultA.Success, $"ReplaceAccountAsync A failed: {resultA.Message}");

        var accountsAfterA = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var accountA = accountsAfterA.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.PendingVerification, accountA.Status);

        // Manually put a MarkedForDeletion row in place by replacing again
        var resultB = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE87200100200000044242"), "system", CancellationToken.None);
        Assert.True(resultB.Success, $"ReplaceAccountAsync B failed: {resultB.Message}");

        // At this point: A=Invalid (was orphan Pending), B=PendingVerification
        // Re-check: we need a MarkedForDeletion row. Let's use a different path:
        // Verify B first (so B becomes Verified), then replace with C (so B becomes MarkedForDeletion, C is Pending)
        var accountsAfterB = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var accountB = accountsAfterB.Single(a => a.Iban == "DE87200100200000044242");
        Assert.Equal(BankAccountStatus.PendingVerification, accountB.Status);

        // Verify B → B becomes Verified
        var verifyResult = await data.VerifyAccountAsync(host, accountB.Id, memberId, performedBy, CancellationToken.None);
        Assert.True(verifyResult.Success, $"VerifyAccountAsync B failed: {verifyResult.Message}");
        Assert.Equal("verified", verifyResult.Status);

        var accountsAfterVerifyB = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var verifiedB = accountsAfterVerifyB.Single(a => a.Iban == "DE87200100200000044242");
        Assert.Equal(BankAccountStatus.Verified, verifiedB.Status);

        // Replace with C → B becomes MarkedForDeletion, C is PendingVerification
        var resultC = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE02500105170137075030"), "system", CancellationToken.None);
        Assert.True(resultC.Success, $"ReplaceAccountAsync C failed: {resultC.Message}");

        var accountsAfterC = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var markedB = accountsAfterC.Single(a => a.Iban == "DE87200100200000044242");
        var pendingC = accountsAfterC.Single(a => a.Iban == "DE02500105170137075030");
        Assert.Equal(BankAccountStatus.MarkedForDeletion, markedB.Status);
        Assert.Equal(BankAccountStatus.PendingVerification, pendingC.Status);

        // Verify C → C becomes Verified, markedB (MarkedForDeletion) → Invalid
        var verifyC = await data.VerifyAccountAsync(host, pendingC.Id, memberId, performedBy, CancellationToken.None);
        Assert.True(verifyC.Success, $"VerifyAccountAsync C failed: {verifyC.Message}");

        var accountsFinal = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var verifiedC = accountsFinal.Single(a => a.Iban == "DE02500105170137075030");
        var nowInvalidB = accountsFinal.Single(a => a.Iban == "DE87200100200000044242");

        Assert.Equal(BankAccountStatus.Verified, verifiedC.Status);
        Assert.Equal(BankAccountStatus.Invalid, nowInvalidB.Status);

        // Check audit
        var auditTable = store.GetTableName("bank_account_audit");
        var auditRows = await store.QueryAsync(
            $"SELECT Action, PerformedBy FROM {auditTable} WHERE MemberId = @memberId AND Action = @action;",
            new Dictionary<string, string?>
            {
                ["memberId"] = memberId.ToString(CultureInfo.InvariantCulture),
                ["action"]   = "BankAccountVerified"
            });

        Assert.NotEmpty(auditRows);
        // The last BankAccountVerified row should have the performedBy set
        Assert.Equal(performedBy, auditRows.Last().Values.GetValueOrDefault("PerformedBy"));
    }

    // ---------------------------------------------------------------------------
    // 5.2 — InvalidateAccountAsync on PendingVerification
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task InvalidateAccount_PendingVerification_TransitionsToInvalid_AndWritesAudit()
    {
        var (data, store, host) = await CreateAsync();
        const int memberId = 20;
        const string performedBy = "kw@example.com";

        var replaceResult = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replaceResult.Success, $"Replace failed: {replaceResult.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var invalidateResult = await data.InvalidateAccountAsync(host, pending.Id, memberId, performedBy, CancellationToken.None);
        Assert.True(invalidateResult.Success, $"InvalidateAccountAsync failed: {invalidateResult.Message}");
        Assert.Equal("invalidated", invalidateResult.Status);

        var accountsAfter = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var invalidated = accountsAfter.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.Invalid, invalidated.Status);

        var auditTable = store.GetTableName("bank_account_audit");
        var auditRows = await store.QueryAsync(
            $"SELECT Action, PerformedBy FROM {auditTable} WHERE MemberId = @memberId AND Action = @action;",
            new Dictionary<string, string?>
            {
                ["memberId"] = memberId.ToString(CultureInfo.InvariantCulture),
                ["action"]   = "BankAccountInvalidated"
            });

        Assert.NotEmpty(auditRows);
        Assert.Equal(performedBy, auditRows[0].Values.GetValueOrDefault("PerformedBy"));
    }

    // ---------------------------------------------------------------------------
    // 5.3 — InvalidateAccountAsync on MarkedForDeletion
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task InvalidateAccount_MarkedForDeletion_TransitionsToInvalid()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 30;

        // Create A (Pending), verify it (Verified), replace with B (A becomes MarkedForDeletion, B is Pending)
        var r1 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(r1.Success, $"Replace A failed: {r1.Message}");

        var accountsA = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pendingA = accountsA.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var vr = await data.VerifyAccountAsync(host, pendingA.Id, memberId, "system", CancellationToken.None);
        Assert.True(vr.Success, $"VerifyA failed: {vr.Message}");

        var r2 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE87200100200000044242"), "system", CancellationToken.None);
        Assert.True(r2.Success, $"Replace B failed: {r2.Message}");

        var accountsB = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var markedA = accountsB.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.MarkedForDeletion, markedA.Status);

        // Invalidate the MarkedForDeletion row
        var ir = await data.InvalidateAccountAsync(host, markedA.Id, memberId, "kw", CancellationToken.None);
        Assert.True(ir.Success, $"InvalidateMarked failed: {ir.Message}");

        var accountsFinal = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var nowInvalid = accountsFinal.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.Invalid, nowInvalid.Status);
    }

    // ---------------------------------------------------------------------------
    // 5.4 — Guard: VerifyAccountAsync on already-Verified account returns invalid-status
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task VerifyAccount_OnVerifiedAccount_ReturnsInvalidStatus()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 40;

        // Create and verify an account
        var r1 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(r1.Success);

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var vr = await data.VerifyAccountAsync(host, pending.Id, memberId, "kw", CancellationToken.None);
        Assert.True(vr.Success);

        // Now try to verify it again — should fail
        var vr2 = await data.VerifyAccountAsync(host, pending.Id, memberId, "kw", CancellationToken.None);
        Assert.False(vr2.Success);
        Assert.Equal("invalid-status", vr2.Status);
    }

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------

    internal sealed class Slice5SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice5SqlitePluginStore(SqliteConnection connection, string moduleId)
        {
            _connection = connection;
            ModuleId    = moduleId;
            TablePrefix = "plg_finance_";
        }

        public string ModuleId   { get; }
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

    internal sealed class Slice5PluginHostContext : IPluginHostContext
    {
        public Slice5PluginHostContext(Slice5SqlitePluginStore store) => Persistence = store;

        public IPluginMetadataFacade    Metadata      => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberReader      Members       => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException("Not needed in tests.");
        public IPluginDataStore         Persistence   { get; }
        public IPluginPermissionFacade  Permissions   => throw new NotSupportedException("Not needed in tests.");
    }
}

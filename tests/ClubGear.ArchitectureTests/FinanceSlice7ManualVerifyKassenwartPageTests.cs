using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Finance;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 7 (Finance iteration 4) verification:
///   7.1 ExecuteCommandAsync("finance.kassenwart.verify", entityKey=memberId, args={bankAccountId=X})
///       on a PendingVerification account returns success; account transitions to Verified;
///       all MarkedForDeletion rows for the same member become Invalid.
///   7.2 Same command with a mismatched memberId (account belongs to a different member)
///       returns (false, "not-found", _).
///   7.3 Same command with a non-integer bankAccountId argument
///       returns (false, "invalid-account", _).
///   7.4 ExecuteCommandAsync("finance.kassenwart.invalidate", ...) on a PendingVerification account
///       returns success.
///   7.5 GetPageDefinitionAsync command schemas include a bankAccountId field and do NOT include
///       a memberId field.
/// </summary>
public sealed class FinanceSlice7ManualVerifyKassenwartPageTests
{
    // ---------------------------------------------------------------------------
    // Shared setup
    // ---------------------------------------------------------------------------

    private static async Task<(FinanceDataService data, Slice7SqlitePluginStore store, Slice7PluginHostContext host)>
        CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice7SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host  = new Slice7PluginHostContext(store);

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
    // 7.1 — verify command: happy path (PendingVerification → Verified, MarkedForDeletion → Invalid)
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteCommand_Verify_PendingVerification_Succeeds_AndTransitionsMarkedForDeletion()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 101;

        // Create account A and verify it so it becomes Verified.
        var r1 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(r1.Success, $"Replace A failed: {r1.Message}");

        var accountsA = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pendingA = accountsA.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var verifyA = await data.VerifyAccountAsync(host, pendingA.Id, memberId, "system", CancellationToken.None);
        Assert.True(verifyA.Success);

        // Replace with B → A becomes MarkedForDeletion, B is PendingVerification.
        var r2 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE87200100200000044242"), "system", CancellationToken.None);
        Assert.True(r2.Success, $"Replace B failed: {r2.Message}");

        var accountsB = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pendingB = accountsB.Single(a => a.Status == BankAccountStatus.PendingVerification);
        var markedA  = accountsB.Single(a => a.Status == BankAccountStatus.MarkedForDeletion);
        Assert.Equal(BankAccountStatus.MarkedForDeletion, markedA.Status);

        // Execute the page command: entityKey = memberId, args = { bankAccountId = pendingB.Id }
        var provider = new FinanceKassenwartPageProvider();
        var args = new Dictionary<string, string>
        {
            ["bankAccountId"] = pendingB.Id.ToString(CultureInfo.InvariantCulture)
        };

        var result = await provider.ExecuteCommandAsync(
            host,
            "finance.kassenwart.verify",
            memberId.ToString(CultureInfo.InvariantCulture),
            args,
            CancellationToken.None);

        Assert.True(result.Success, $"Command failed: {result.Status} — {result.Message}");

        // B is now Verified.
        var accountsFinal = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var verifiedB = accountsFinal.Single(a => a.Iban == "DE87200100200000044242");
        Assert.Equal(BankAccountStatus.Verified, verifiedB.Status);

        // A (previously MarkedForDeletion) is now Invalid.
        var nowInvalidA = accountsFinal.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.Invalid, nowInvalidA.Status);
    }

    // ---------------------------------------------------------------------------
    // 7.2 — verify command: mismatched memberId → not-found
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteCommand_Verify_MismatchedMemberId_ReturnsNotFound()
    {
        var (data, _, host) = await CreateAsync();
        const int realMemberId  = 201;
        const int wrongMemberId = 202;

        // Create an account for member 201.
        var r1 = await data.ReplaceAccountAsync(host, realMemberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(r1.Success, $"Replace failed: {r1.Message}");

        var accounts = await data.GetAccountsAsync(host, realMemberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        // Use the correct bankAccountId but a wrong memberId as entityKey.
        var provider = new FinanceKassenwartPageProvider();
        var args = new Dictionary<string, string>
        {
            ["bankAccountId"] = pending.Id.ToString(CultureInfo.InvariantCulture)
        };

        var result = await provider.ExecuteCommandAsync(
            host,
            "finance.kassenwart.verify",
            wrongMemberId.ToString(CultureInfo.InvariantCulture),
            args,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("not-found", result.Status);
    }

    // ---------------------------------------------------------------------------
    // 7.3 — verify command: non-integer bankAccountId → invalid-account
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteCommand_Verify_NonIntegerBankAccountId_ReturnsInvalidAccount()
    {
        var (_, _, host) = await CreateAsync();

        var provider = new FinanceKassenwartPageProvider();
        var args = new Dictionary<string, string>
        {
            ["bankAccountId"] = "not-a-number"
        };

        var result = await provider.ExecuteCommandAsync(
            host,
            "finance.kassenwart.verify",
            "999",
            args,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid-account", result.Status);
    }

    // ---------------------------------------------------------------------------
    // 7.4 — invalidate command: happy path (PendingVerification → Invalid)
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteCommand_Invalidate_PendingVerification_Succeeds()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 301;

        var r1 = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(r1.Success, $"Replace failed: {r1.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var provider = new FinanceKassenwartPageProvider();
        var args = new Dictionary<string, string>
        {
            ["bankAccountId"] = pending.Id.ToString(CultureInfo.InvariantCulture)
        };

        var result = await provider.ExecuteCommandAsync(
            host,
            "finance.kassenwart.invalidate",
            memberId.ToString(CultureInfo.InvariantCulture),
            args,
            CancellationToken.None);

        Assert.True(result.Success, $"Invalidate command failed: {result.Status} — {result.Message}");

        var accountsAfter = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var invalidated = accountsAfter.Single(a => a.Iban == "DE89370400440532013000");
        Assert.Equal(BankAccountStatus.Invalid, invalidated.Status);
    }

    // ---------------------------------------------------------------------------
    // 7.5 — GetPageDefinitionAsync: command schemas have bankAccountId, not memberId
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetPageDefinition_CommandSchemas_ContainBankAccountId_NotMemberId()
    {
        var (_, _, host) = await CreateAsync();

        var provider = new FinanceKassenwartPageProvider();
        var definition = await provider.GetPageDefinitionAsync(host, CancellationToken.None);

        Assert.NotNull(definition.Commands);

        foreach (var command in definition.Commands)
        {
            Assert.NotNull(command.Schema);

            var fieldKeys = command.Schema.Select(f => f.Key).ToList();

            Assert.Contains("bankAccountId", fieldKeys);
            Assert.DoesNotContain("memberId", fieldKeys);
        }
    }

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------

    internal sealed class Slice7SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice7SqlitePluginStore(SqliteConnection connection, string moduleId)
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

    internal sealed class Slice7PluginHostContext : IPluginHostContext
    {
        public Slice7PluginHostContext(Slice7SqlitePluginStore store) => Persistence = store;

        public IPluginMetadataFacade     Metadata      => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberReader       Members       => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException("Not needed in tests.");
        public IPluginDataStore          Persistence   { get; }
        public IPluginPermissionFacade   Permissions   => throw new NotSupportedException("Not needed in tests.");
    }
}

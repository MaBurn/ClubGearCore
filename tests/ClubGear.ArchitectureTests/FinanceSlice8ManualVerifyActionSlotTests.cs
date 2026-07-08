using ClubGear.Plugin.Contracts;
using ClubGear.Plugin.Finance;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 8+9 (Finance iteration 4) verification:
///   8.1 GetActionsAsync includes "finance.account.verify" when a PendingVerification account exists.
///   8.2 GetActionsAsync does NOT include "finance.account.verify" when no pending accounts exist.
///   8.3 GetActionsAsync includes "finance.account.invalidate" when a PendingVerification
///       or MarkedForDeletion account exists.
///   8.4 ExecuteAsync("finance.account.verify") on a valid pending account returns (true, "verified").
///   8.5 ExecuteAsync("finance.account.invalidate") on a valid pending account returns (true, "invalidated").
///   8.6 ExecuteAsync("finance.account.verify") with bankAccountId="not-a-number"
///       returns (false, _) with a bankAccountId field error.
///   9.1 GetTabsAsync HTML contains &lt;th&gt;ID&lt;/th&gt; as the first column header AND
///       each account row's integer Id as the first &lt;td&gt;.
/// </summary>
public sealed class FinanceSlice8ManualVerifyActionSlotTests
{
    // ---------------------------------------------------------------------------
    // Shared setup
    // ---------------------------------------------------------------------------

    private static async Task<(FinanceDataService data, Slice8SqlitePluginStore store, Slice8PluginHostContext host)>
        CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var store = new Slice8SqlitePluginStore(connection, "clubgear.plugin.finance");
        var host  = new Slice8PluginHostContext(store);

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

    private static PluginMemberDetail MakeMember(int id)
        => new(id, $"M-00{id}", $"Member {id}", "First", "Last", $"member{id}@example.org", "+49-111", true);

    // ---------------------------------------------------------------------------
    // 8.1 — GetActionsAsync includes finance.account.verify when pending account exists
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetActionsAsync_IncludesVerifySlot_WhenPendingAccountExists()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 10;

        var replace = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replace.Success, $"ReplaceAccountAsync failed: {replace.Message}");

        var provider = new FinanceActionProvider();
        var member   = MakeMember(memberId);

        var actions = await provider.GetActionsAsync(member, host, CancellationToken.None);

        Assert.Contains(actions, a => a.Key == "finance.account.verify");
    }

    // ---------------------------------------------------------------------------
    // 8.2 — GetActionsAsync does NOT include finance.account.verify when no pending accounts
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetActionsAsync_ExcludesVerifySlot_WhenNoPendingAccounts()
    {
        var (_, _, host) = await CreateAsync();
        // Member has no accounts at all
        var provider = new FinanceActionProvider();
        var member   = MakeMember(99);

        var actions = await provider.GetActionsAsync(member, host, CancellationToken.None);

        Assert.DoesNotContain(actions, a => a.Key == "finance.account.verify");
    }

    // ---------------------------------------------------------------------------
    // 8.3 — GetActionsAsync includes finance.account.invalidate when pending or marked account exists
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetActionsAsync_IncludesInvalidateSlot_WhenPendingOrMarkedAccountExists()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 20;

        // Create a pending account
        var replace = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replace.Success, $"ReplaceAccountAsync failed: {replace.Message}");

        var provider = new FinanceActionProvider();
        var member   = MakeMember(memberId);

        var actions = await provider.GetActionsAsync(member, host, CancellationToken.None);

        Assert.Contains(actions, a => a.Key == "finance.account.invalidate");
    }

    // ---------------------------------------------------------------------------
    // 8.4 — ExecuteAsync("finance.account.verify") on a valid pending account → (true, "verified")
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteAsync_Verify_ValidPendingAccount_ReturnsVerified()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 30;

        var replace = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replace.Success, $"ReplaceAccountAsync failed: {replace.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var provider = new FinanceActionProvider();
        var member   = MakeMember(memberId);
        var request  = new PluginMemberActionRequest(memberId, "finance.account.verify", new Dictionary<string, string>
        {
            ["bankAccountId"] = pending.Id.ToString(CultureInfo.InvariantCulture),
            ["performedBy"]   = "Tester"
        });

        var result = await provider.ExecuteAsync(request, member, host, CancellationToken.None);

        Assert.True(result.Success, $"ExecuteAsync verify failed: {result.Message}");
        Assert.Equal("verified", result.Status);
    }

    // ---------------------------------------------------------------------------
    // 8.5 — ExecuteAsync("finance.account.invalidate") on a valid pending account → (true, "invalidated")
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteAsync_Invalidate_ValidPendingAccount_ReturnsInvalidated()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 40;

        var replace = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replace.Success, $"ReplaceAccountAsync failed: {replace.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var pending = accounts.Single(a => a.Status == BankAccountStatus.PendingVerification);

        var provider = new FinanceActionProvider();
        var member   = MakeMember(memberId);
        var request  = new PluginMemberActionRequest(memberId, "finance.account.invalidate", new Dictionary<string, string>
        {
            ["bankAccountId"] = pending.Id.ToString(CultureInfo.InvariantCulture)
        });

        var result = await provider.ExecuteAsync(request, member, host, CancellationToken.None);

        Assert.True(result.Success, $"ExecuteAsync invalidate failed: {result.Message}");
        Assert.Equal("invalidated", result.Status);
    }

    // ---------------------------------------------------------------------------
    // 8.6 — ExecuteAsync("finance.account.verify") with non-integer bankAccountId
    //        → (false, _) with bankAccountId field error
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ExecuteAsync_Verify_NonIntegerBankAccountId_ReturnsFieldError()
    {
        var (_, _, host) = await CreateAsync();
        const int memberId = 50;

        var provider = new FinanceActionProvider();
        var member   = MakeMember(memberId);
        var request  = new PluginMemberActionRequest(memberId, "finance.account.verify", new Dictionary<string, string>
        {
            ["bankAccountId"] = "not-a-number"
        });

        var result = await provider.ExecuteAsync(request, member, host, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains(result.FieldErrors, e => e.FieldKey == "bankAccountId");
    }

    // ---------------------------------------------------------------------------
    // 9.1 — GetTabsAsync HTML contains <th>ID</th> as first column header
    //        AND each account's integer Id as first <td> in each row
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task GetTabsAsync_ContainsIdColumnHeader_AndAccountIdAsFirstCell()
    {
        var (data, _, host) = await CreateAsync();
        const int memberId = 60;

        var replace = await data.ReplaceAccountAsync(host, memberId, MakeInput("DE89370400440532013000"), "system", CancellationToken.None);
        Assert.True(replace.Success, $"ReplaceAccountAsync failed: {replace.Message}");

        var accounts = await data.GetAccountsAsync(host, memberId, CancellationToken.None);
        var account = accounts.Single();

        var tabProvider = new FinanceEditTabProvider();
        var member      = MakeMember(memberId);

        var tabs = await tabProvider.GetTabsAsync(member, host, CancellationToken.None);
        var html = tabs.Single().Content;

        // Must have the ID column header
        Assert.Contains("<th>ID</th>", html, StringComparison.Ordinal);

        // The account's integer Id must appear as the first <td> in its row
        var expectedCell = $"<td>{account.Id.ToString(CultureInfo.InvariantCulture)}</td>";
        Assert.Contains(expectedCell, html, StringComparison.Ordinal);

        // Verify the ID column is the *first* <td> in the row (i.e., appears before the status badge)
        var idCellIndex     = html.IndexOf(expectedCell, StringComparison.Ordinal);
        var statusBadgeIndex = html.IndexOf("<td><span class=\"badge", StringComparison.Ordinal);
        Assert.True(idCellIndex < statusBadgeIndex,
            $"Expected <td>{account.Id}</td> to appear before the status badge <td>, but id was at {idCellIndex} and badge at {statusBadgeIndex}.");
    }

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------

    internal sealed class Slice8SqlitePluginStore : IPluginMigrationContext
    {
        private readonly SqliteConnection _connection;

        public Slice8SqlitePluginStore(SqliteConnection connection, string moduleId)
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

    internal sealed class Slice8PluginHostContext : IPluginHostContext
    {
        public Slice8PluginHostContext(Slice8SqlitePluginStore store) => Persistence = store;

        public IPluginMetadataFacade     Metadata      => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberReader       Members       => throw new NotSupportedException("Not needed in tests.");
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException("Not needed in tests.");
        public IPluginDataStore          Persistence   { get; }
        public IPluginPermissionFacade   Permissions   { get; } = new Slice8StubPermissionFacade(hasPermission: false);
    }

    internal sealed class Slice8StubPermissionFacade : IPluginPermissionFacade
    {
        private readonly bool _hasPermission;

        public Slice8StubPermissionFacade(bool hasPermission)
        {
            _hasPermission = hasPermission;
        }

        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_hasPermission);
    }
}

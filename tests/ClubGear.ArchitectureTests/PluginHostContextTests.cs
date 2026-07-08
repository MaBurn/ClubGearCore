using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginHostContextTests
{
    [Fact]
    public async Task MemberActionsFacade_ForwardsRequestToConfiguredExecutor()
    {
        PluginMemberActionRequest? capturedRequest = null;

        var hostContext = new PluginHostContext(
            new PluginManifest(
                "plugin.host.actions",
                "Host Actions",
                new Version(1, 0, 0),
                "Plugin Tests",
                "MIT",
                "Plugin.EntryPoint",
                ">=1.0.0",
                ["members.manage"],
                Array.Empty<string>()),
            new FakeMemberFeatureService(),
            new FakePluginDataStore(),
            (request, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(new PluginMemberActionResult(true, "executed", "ok"));
            });

        var result = await hostContext.MemberActions.ExecuteAsync(new PluginMemberActionRequest(42, "sync-member", new Dictionary<string, string>
        {
            ["dryRun"] = "false"
        }));

        Assert.True(result.Success);
        Assert.Equal("executed", result.Status);
        Assert.NotNull(capturedRequest);
        Assert.Equal(42, capturedRequest!.MemberId);
        Assert.Equal("sync-member", capturedRequest.ActionKey);
        Assert.Equal("false", capturedRequest.Arguments!["dryRun"]);
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Member?>(null);

        public Task CreateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakePluginDataStore : IPluginDataStore
    {
        public string ModuleId => "plugin.host.actions";

        public string TablePrefix => "plg_test_";

        public string GetTableName(string localName)
            => TablePrefix + localName;

        public Task ExecuteAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PluginDataRow>> QueryAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginDataRow>>(Array.Empty<PluginDataRow>());
    }
}

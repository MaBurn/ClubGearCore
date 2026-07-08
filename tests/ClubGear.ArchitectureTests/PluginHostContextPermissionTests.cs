using ClubGear.Models.MemberFilters;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Runtime;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2.1 DoD: a unit test constructing PluginHostContext with a stub delegate
/// can call context.Permissions.HasPermissionAsync("key") and get back the stub's return value.
/// </summary>
public sealed class PluginHostContextPermissionTests
{
    private static PluginManifest CreateManifest() => new(
        Key: "plugin.test.permissions",
        Name: "Permission Test Plugin",
        Version: new Version(1, 0, 0),
        Author: "Test",
        License: "MIT",
        EntryPoint: "Test.Module",
        RequiredCoreVersion: ">=1.8.0",
        Permissions: Array.Empty<string>(),
        ExtensionPoints: Array.Empty<string>());

    [Fact]
    public async Task Permissions_DelegatesToResolver_WhenResolverReturnsTrue()
    {
        // Arrange
        var manifest = CreateManifest();
        var memberService = new StubMemberFeatureService();
        var dataStore = new StubPluginDataStore();

        var context = new PluginHostContext(
            manifest,
            memberService,
            dataStore,
            executeMemberActionAsync: null,
            permissionResolver: (_, _) => Task.FromResult(true));

        // Act
        var result = await context.Permissions.HasPermissionAsync("kassenwart.access");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Permissions_DelegatesToResolver_WhenResolverReturnsFalse()
    {
        // Arrange
        var manifest = CreateManifest();
        var memberService = new StubMemberFeatureService();
        var dataStore = new StubPluginDataStore();

        var context = new PluginHostContext(
            manifest,
            memberService,
            dataStore,
            executeMemberActionAsync: null,
            permissionResolver: (_, _) => Task.FromResult(false));

        // Act
        var result = await context.Permissions.HasPermissionAsync("kassenwart.access");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Permissions_ForwardsPermissionKey_ToResolver()
    {
        // Arrange
        string? capturedKey = null;

        var context = new PluginHostContext(
            CreateManifest(),
            new StubMemberFeatureService(),
            new StubPluginDataStore(),
            executeMemberActionAsync: null,
            permissionResolver: (key, _) =>
            {
                capturedKey = key;
                return Task.FromResult(true);
            });

        // Act
        await context.Permissions.HasPermissionAsync("finance.kassenwart.access");

        // Assert
        Assert.Equal("finance.kassenwart.access", capturedKey);
    }

    [Fact]
    public async Task Permissions_UsesNullFacade_WhenNoResolverProvided()
    {
        // Arrange — no permissionResolver supplied (default)
        var context = new PluginHostContext(
            CreateManifest(),
            new StubMemberFeatureService(),
            new StubPluginDataStore());

        // Act
        var result = await context.Permissions.HasPermissionAsync("any.permission");

        // Assert: null facade always returns false
        Assert.False(result);
    }

    // ── Minimal stubs ─────────────────────────────────────────────────────────

    private sealed class StubMemberFeatureService : IMemberFeatureService
    {
        public Task<IReadOnlyList<ClubGear.Models.Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClubGear.Models.Member>>(Array.Empty<ClubGear.Models.Member>());

        public Task<IReadOnlyList<ClubGear.Models.Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClubGear.Models.Member>>(Array.Empty<ClubGear.Models.Member>());

        public Task<ClubGear.Models.Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<ClubGear.Models.Member?>(null);

        public Task CreateAsync(ClubGear.Models.Member member, string? actor, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MemberMutationStatus> UpdateAsync(ClubGear.Models.Member member, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(new MembersImportResult(0, 0, 0, Array.Empty<string>()));
    }

    private sealed class StubPluginDataStore : IPluginDataStore
    {
        public string ModuleId => "plugin.test.permissions";
        public string TablePrefix => "plugin_test_permissions_";

        public string GetTableName(string localName) => TablePrefix + localName;

        public Task ExecuteAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PluginDataRow>> QueryAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginDataRow>>(Array.Empty<PluginDataRow>());
    }
}

using System.Text.Json;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Xunit;

namespace ClubGear.ArchitectureTests.Authorization;

public sealed class CorePermissionDefinitionProviderTests
{
    private static PluginStatusRecord MakeRecord(
        string key,
        string displayName,
        string category,
        params string[] permissions)
    {
        return new PluginStatusRecord
        {
            Key = key,
            DisplayName = displayName,
            Category = category,
            Version = "1.0.0",
            Author = "Test",
            License = "MIT",
            EntryPoint = "EntryPoint",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "HASH",
            PackagePath = "/tmp/plugin.zip",
            PermissionsJson = JsonSerializer.Serialize(permissions),
            ExtensionPointsJson = "[]"
        };
    }

    [Fact]
    public void GetPermissions_SetsDescriptionAndCategoryFromRecord()
    {
        var recordA = MakeRecord("plugin.a", "Finance Plugin", "Finance", "finance.view", "finance.edit");
        var store = new FakePluginStatusStore([recordA]);
        var provider = new CorePermissionDefinitionProvider(store);

        var pluginDefs = provider.GetPermissions()
            .Where(d => !PermissionKeys.IsCorePermission(d.Key))
            .ToList();

        Assert.Equal(2, pluginDefs.Count);

        var viewDef = pluginDefs.Single(d => d.Key == "finance.view");
        Assert.Equal("Finance Plugin: finance.view", viewDef.Description);
        Assert.Equal("Finance", viewDef.Category);

        var editDef = pluginDefs.Single(d => d.Key == "finance.edit");
        Assert.Equal("Finance Plugin: finance.edit", editDef.Description);
        Assert.Equal("Finance", editDef.Category);
    }

    [Fact]
    public void GetPermissions_DeduplicatesKeys_FirstRecordWins()
    {
        var recordA = MakeRecord("plugin.a", "Plugin A", "CategoryA", "shared.permission");
        var recordB = MakeRecord("plugin.b", "Plugin B", "CategoryB", "shared.permission");
        var store = new FakePluginStatusStore([recordA, recordB]);
        var provider = new CorePermissionDefinitionProvider(store);

        var pluginDefs = provider.GetPermissions()
            .Where(d => !PermissionKeys.IsCorePermission(d.Key))
            .ToList();

        Assert.Single(pluginDefs);
        Assert.Equal("Plugin A: shared.permission", pluginDefs[0].Description);
        Assert.Equal("CategoryA", pluginDefs[0].Category);
    }

    [Fact]
    public void GetPermissions_OrdersPluginPermissionsByKeyAscending()
    {
        var recordA = MakeRecord("plugin.a", "Plugin A", "CatA", "zz.permission", "aa.permission", "mm.permission");
        var store = new FakePluginStatusStore([recordA]);
        var provider = new CorePermissionDefinitionProvider(store);

        var pluginKeys = provider.GetPermissions()
            .Where(d => !PermissionKeys.IsCorePermission(d.Key))
            .Select(d => d.Key)
            .ToList();

        Assert.Equal(["aa.permission", "mm.permission", "zz.permission"], pluginKeys);
    }

    [Fact]
    public void GetPermissions_ExcludesCorePermissions()
    {
        var recordA = MakeRecord("plugin.a", "Plugin A", "CatA",
            "plugin.only", PermissionKeys.AdminAccess, PermissionKeys.MembersRead);
        var store = new FakePluginStatusStore([recordA]);
        var provider = new CorePermissionDefinitionProvider(store);

        var pluginDefs = provider.GetPermissions()
            .Where(d => !PermissionKeys.IsCorePermission(d.Key))
            .ToList();

        Assert.Single(pluginDefs);
        Assert.Equal("plugin.only", pluginDefs[0].Key);
    }

    [Fact]
    public void GetPermissions_AlwaysIncludesCorePermissions()
    {
        var store = new FakePluginStatusStore([]);
        var provider = new CorePermissionDefinitionProvider(store);

        var allKeys = provider.GetPermissions().Select(d => d.Key).ToList();

        Assert.Contains(PermissionKeys.AdminAccess, allKeys);
        Assert.Contains(PermissionKeys.MembersRead, allKeys);
        Assert.Contains(PermissionKeys.MembersManage, allKeys);
    }

    private sealed class FakePluginStatusStore : IPluginStatusStore
    {
        private readonly IReadOnlyList<PluginStatusRecord> _records;

        public FakePluginStatusStore(IReadOnlyList<PluginStatusRecord> records)
        {
            _records = records;
        }

        public PluginStatusRecord? GetByKey(string key)
            => _records.FirstOrDefault(r => r.Key == key);

        public IReadOnlyList<PluginStatusRecord> List()
            => _records;

        public Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}

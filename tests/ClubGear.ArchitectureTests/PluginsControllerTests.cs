using ClubGear.Controllers.Api;
using ClubGear.Models.PluginAdmin;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginsControllerTests
{
    [Fact]
    public void GetInstalled_ReturnsAdminStatusView()
    {
        var installed = new PluginAdminStatusViewModel(
            "clubgear.marketplace.members",
            "Members Marketplace",
            new Version(1, 0, 0),
            "marketplace",
            DateTimeOffset.UtcNow,
            "Plugin Author",
            "Proprietary",
            ">=1.0.0",
            true,
            false,
            false,
            true,
            null,
            null,
            ["Plugin_Marketplace_View"],
            ["member.detail"],
            0,
            0,
            0,
            0,
            null,
            "General",
            string.Empty,
            0,
            0,
            0,
            0);
        var queryService = new FakePluginAdminQueryService
        {
            Statuses = [installed]
        };
        var sut = new PluginsController(new FakePluginInstallerService(), new FakePluginLifecycleService(), queryService);

        var response = sut.GetInstalled();

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(ok.Value);
        var record = Assert.Single(payload);
        Assert.Equal("Plugin Author", record.Author);
        Assert.Equal("Proprietary", record.License);
        Assert.False(record.IsActive);
        Assert.True(record.IsInstalled);
    }

    [Fact]
    public async Task InstallFromMarketplace_ReturnsOk_WhenInstallSucceeds()
    {
        var service = new FakePluginInstallerService
        {
            OnMarketplaceInstall = (_, _) => Task.FromResult(new PluginInstallOperationResult(true, "installed", "ok"))
        };
        var sut = new PluginsController(service, new FakePluginLifecycleService(), new FakePluginAdminQueryService());

        var response = await sut.InstallFromMarketplace(new MarketplaceInstallRequest("clubgear.marketplace.members"));

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<PluginInstallOperationResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("installed", payload.Status);
    }

    [Fact]
    public async Task InstallFromZip_ReturnsBadRequest_ForInvalidBase64()
    {
        var sut = new PluginsController(new FakePluginInstallerService(), new FakePluginLifecycleService(), new FakePluginAdminQueryService());

        var response = await sut.InstallFromZip(new ZipInstallRequest(
            "plugin.zip",
            "invalid-base64",
            "ABCD",
            "AAAA",
            "PUBLICKEY"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var payload = Assert.IsType<PluginInstallOperationResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Equal("invalid", payload.Status);
    }

    [Fact]
    public async Task Activate_ReturnsOk_WhenLifecycleSucceeds()
    {
        var lifecycle = new FakePluginLifecycleService
        {
            OnActivate = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(
                true,
                "activated",
                "ok",
                CreateInstalledPluginRecord(isActive: true)))
        };

        var sut = new PluginsController(new FakePluginInstallerService(), lifecycle, new FakePluginAdminQueryService());

        var response = await sut.Activate("clubgear.marketplace.members");

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<PluginLifecycleOperationResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("activated", payload.Status);
        Assert.True(payload.Plugin!.IsActive);
    }

    [Fact]
    public async Task Deactivate_ReturnsNotFound_WhenPluginIsMissing()
    {
        var lifecycle = new FakePluginLifecycleService
        {
            OnDeactivate = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(false, "not-found", "missing"))
        };

        var sut = new PluginsController(new FakePluginInstallerService(), lifecycle, new FakePluginAdminQueryService());

        var response = await sut.Deactivate("clubgear.missing");

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        var payload = Assert.IsType<PluginLifecycleOperationResult>(notFound.Value);
        Assert.False(payload.Success);
        Assert.Equal("not-found", payload.Status);
    }

    private static InstalledPluginRecord CreateInstalledPluginRecord(bool isActive)
        => new(
            "clubgear.marketplace.members",
            "Members Marketplace",
            new Version(1, 0, 0),
            "marketplace",
            DateTimeOffset.UtcNow,
            "Plugin Author",
            "Proprietary",
            ">=1.0.0",
            ["Plugin_Marketplace_View"],
            ["member.detail"],
            isActive,
            null,
            "ABC123");

    private sealed class FakePluginInstallerService : IPluginInstallerService
    {
        public IReadOnlyList<InstalledPluginRecord> Installed { get; set; } = Array.Empty<InstalledPluginRecord>();

        public Func<string, CancellationToken, Task<PluginInstallOperationResult>> OnMarketplaceInstall { get; set; }
            = (_, _) => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public Func<string, byte[], string, string, string, CancellationToken, Task<PluginInstallOperationResult>> OnZipInstall { get; set; }
            = (_, _, _, _, _, _) => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(string moduleId, CancellationToken cancellationToken = default)
            => OnMarketplaceInstall(moduleId, cancellationToken);

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(
            string fileName,
            byte[] zipBytes,
            string expectedSha256Hex,
            string signatureBase64,
            string signerPublicKeyPem,
            CancellationToken cancellationToken = default)
            => OnZipInstall(fileName, zipBytes, expectedSha256Hex, signatureBase64, signerPublicKeyPem, cancellationToken);

        public IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins()
            => Installed;
    }

    private sealed class FakePluginAdminQueryService : IPluginAdminQueryService
    {
        public IReadOnlyList<PluginAdminStatusViewModel> Statuses { get; set; } = Array.Empty<PluginAdminStatusViewModel>();

        public IReadOnlyList<PluginAdminStatusViewModel> GetPluginStatuses()
            => Statuses;

        public PluginAdminStatusViewModel? GetPluginStatus(string moduleId)
            => Statuses.SingleOrDefault(status => string.Equals(status.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakePluginLifecycleService : IPluginLifecycleService
    {
        public Func<CancellationToken, Task<IReadOnlyList<PluginLifecycleOperationResult>>> OnLoadActivated { get; set; }
            = _ => Task.FromResult<IReadOnlyList<PluginLifecycleOperationResult>>(Array.Empty<PluginLifecycleOperationResult>());

        public Func<string, CancellationToken, Task<PluginLifecycleOperationResult>> OnActivate { get; set; }
            = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(false, "not-configured", "not-configured"));

        public Func<string, CancellationToken, Task<PluginLifecycleOperationResult>> OnDeactivate { get; set; }
            = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(false, "not-configured", "not-configured"));

        public Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken cancellationToken = default)
            => OnLoadActivated(cancellationToken);

        public Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => OnActivate(moduleId, cancellationToken);

        public Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => OnDeactivate(moduleId, cancellationToken);
    }
}

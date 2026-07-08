using ClubGear.Controllers;
using ClubGear.Models.Feedback;
using ClubGear.Models.PluginAdmin;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAdminDeleteMvcTests
{
    // ── helper ──────────────────────────────────────────────────────────────

    private static PluginAdminController BuildSut(
        FakePluginAdminQueryService? queryService = null,
        FakePluginUninstallService? uninstallService = null)
    {
        var httpContext = new DefaultHttpContext();
        var sut = new PluginAdminController(
            new FakePluginInstallerService(),
            new FakePluginLifecycleService(),
            queryService ?? new FakePluginAdminQueryService(),
            uninstallService ?? new FakePluginUninstallService());
        sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        sut.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return sut;
    }

    private static PluginAdminStatusViewModel BuildViewModel(string moduleId = "plugin.test") =>
        new PluginAdminStatusViewModel(
            moduleId,
            "Test Plugin",
            new Version(1, 0, 0),
            "zip",
            DateTimeOffset.UtcNow,
            "Author",
            "MIT",
            ">=1.0.0",
            true,
            false,
            false,
            true,
            null,
            null,
            ["members.read"],
            ["member.detail"],
            0, 0, 0, 0,
            null,
            "General",
            "ABC123",
            0,
            0,
            0,
            0);

    // ── 4.1 Detail GET ───────────────────────────────────────────────────────

    [Fact]
    public void Detail_WithUnknownModuleId_RedirectsToIndex()
    {
        using var sut = BuildSut();

        var result = sut.Detail("plugin.unknown");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
    }

    [Fact]
    public void Detail_WithKnownModuleId_ReturnsDetailView()
    {
        var vm = BuildViewModel("plugin.known");
        var queryService = new FakePluginAdminQueryService { Statuses = [vm] };
        using var sut = BuildSut(queryService: queryService);

        var result = sut.Detail("plugin.known");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Detail", view.ViewName);
        var model = Assert.IsType<PluginAdminStatusViewModel>(view.Model);
        Assert.Equal("plugin.known", model.ModuleId);
    }

    // ── 4.2 Delete POST ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithMissingModuleId_SetsFeedbackAndRedirects()
    {
        var uninstallService = new FakePluginUninstallService
        {
            OnUninstall = (moduleId, _, _) =>
            {
                // Empty moduleId is forwarded; service returns failure
                return Task.FromResult(new PluginUninstallResult(false, "not-found", "Plugin nicht gefunden."));
            }
        };
        using var sut = BuildSut(uninstallService: uninstallService);

        var result = await sut.Delete(string.Empty, false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Plugin nicht gefunden.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task Delete_OnSuccess_SetsSuccessFeedbackAndRedirectsToIndex()
    {
        var uninstallService = new FakePluginUninstallService
        {
            OnUninstall = (_, _, _) =>
                Task.FromResult(new PluginUninstallResult(true, "uninstalled", "Plugin wurde deinstalliert."))
        };
        using var sut = BuildSut(uninstallService: uninstallService);

        var result = await sut.Delete("plugin.test", false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Plugin wurde deinstalliert.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task Delete_ForwardsRemoveDataFlag_ToUninstallService()
    {
        bool? capturedRemoveData = null;
        var uninstallService = new FakePluginUninstallService
        {
            OnUninstall = (_, removeData, _) =>
            {
                capturedRemoveData = removeData;
                return Task.FromResult(new PluginUninstallResult(true, "uninstalled", "ok"));
            }
        };
        using var sut = BuildSut(uninstallService: uninstallService);

        await sut.Delete("plugin.test", removeData: true);

        Assert.True(capturedRemoveData);
    }

    // ── fakes ────────────────────────────────────────────────────────────────

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class FakePluginInstallerService : IPluginInstallerService
    {
        public Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(string fileName, byte[] zipBytes, string expectedSha256Hex, string signatureBase64, string signerPublicKeyPem, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins()
            => Array.Empty<InstalledPluginRecord>();
    }

    private sealed class FakePluginLifecycleService : IPluginLifecycleService
    {
        public Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginLifecycleOperationResult>>(Array.Empty<PluginLifecycleOperationResult>());

        public Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginLifecycleOperationResult(false, "not-configured", "not-configured"));

        public Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginLifecycleOperationResult(true, "deactivated", "ok"));
    }

    private sealed class FakePluginAdminQueryService : IPluginAdminQueryService
    {
        public IReadOnlyList<PluginAdminStatusViewModel> Statuses { get; set; } = Array.Empty<PluginAdminStatusViewModel>();

        public IReadOnlyList<PluginAdminStatusViewModel> GetPluginStatuses() => Statuses;

        public PluginAdminStatusViewModel? GetPluginStatus(string moduleId)
            => Statuses.SingleOrDefault(s => string.Equals(s.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakePluginUninstallService : IPluginUninstallService
    {
        public Func<string, bool, CancellationToken, Task<PluginUninstallResult>> OnUninstall { get; set; }
            = (_, _, _) => Task.FromResult(new PluginUninstallResult(true, "uninstalled", "ok"));

        public Task<PluginUninstallResult> UninstallAsync(string moduleId, bool removeData, CancellationToken ct = default)
            => OnUninstall(moduleId, removeData, ct);
    }
}

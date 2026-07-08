using ClubGear.Controllers;
using ClubGear.Models.Feedback;
using ClubGear.Models.PluginAdmin;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAdminMvcSmokeTests
{
    [Fact]
    public void Index_RendersStatusesFromQueryService()
    {
        var statuses = new[]
        {
            new PluginAdminStatusViewModel(
                "plugin.runtime.a",
                "Runtime Plugin A",
                new Version(1, 0, 0),
                "zip",
                DateTimeOffset.UtcNow,
                "Plugin Tests",
                "Commercial",
                ">=1.0.0",
                true,
                false,
                false,
                true,
                null,
                null,
                ["members.read"],
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
                0)
        };

        using var sut = new PluginAdminController(new FakePluginInstallerService(), new FakePluginLifecycleService(), new FakePluginAdminQueryService { Statuses = statuses }, new FakePluginUninstallService());

        var result = sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(view.Model);
        Assert.Single(model);
        Assert.Equal("plugin.runtime.a", model[0].ModuleId);
    }

    [Fact]
    public async Task Activate_SetsTempDataFeedbackAndRedirects()
    {
        using var sut = new PluginAdminController(
            new FakePluginInstallerService(),
            new FakePluginLifecycleService
            {
                OnActivate = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(true, "activated", "Plugin wurde aktiviert."))
            },
            new FakePluginAdminQueryService(),
            new FakePluginUninstallService());
        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.Activate("plugin.runtime.a");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Plugin wurde aktiviert.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task InstallFromMarketplace_WithMissingModuleId_SetsErrorFeedback()
    {
        using var sut = new PluginAdminController(new FakePluginInstallerService(), new FakePluginLifecycleService(), new FakePluginAdminQueryService(), new FakePluginUninstallService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.InstallFromMarketplace(string.Empty);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    [Fact]
    public async Task InstallFromZip_WithMissingFile_SetsErrorFeedback()
    {
        using var sut = new PluginAdminController(new FakePluginInstallerService(), new FakePluginLifecycleService(), new FakePluginAdminQueryService(), new FakePluginUninstallService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.InstallFromZip(null, "sha", "sig", "pem");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Bitte eine gueltige ZIP-Datei auswaehlen.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task InstallFromZip_ForwardsFileAndSignatureDataToInstaller()
    {
        var installer = new FakePluginInstallerService
        {
            OnZipInstall = (fileName, zipBytes, expectedSha256Hex, signatureBase64, signerPublicKeyPem, _) =>
            {
                Assert.Equal("plugin.zip", fileName);
                Assert.Equal("ABC123", expectedSha256Hex);
                Assert.Equal("SIG", signatureBase64);
                Assert.Equal("PEM", signerPublicKeyPem);
                Assert.Equal("zip-payload", Encoding.UTF8.GetString(zipBytes));
                return Task.FromResult(new PluginInstallOperationResult(true, "installed", "ZIP installiert."));
            }
        };

        using var sut = new PluginAdminController(installer, new FakePluginLifecycleService(), new FakePluginAdminQueryService(), new FakePluginUninstallService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("zip-payload"));
        IFormFile file = new FormFile(stream, 0, stream.Length, "zipFile", "plugin.zip");

        var result = await sut.InstallFromZip(file, "ABC123", "SIG", "PEM");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PluginAdminController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("ZIP installiert.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public void PluginAdminViews_RenderStatusPartialAndScriptHook()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "PluginAdmin", "Index.cshtml"));
        var partialContent = File.ReadAllText(GetProjectFilePath("Views", "PluginAdmin", "_PluginStatusTable.cshtml"));
        var scriptContent = File.ReadAllText(GetProjectFilePath("wwwroot", "js", "plugin-admin.js"));

        Assert.Contains("<partial name=\"_PluginStatusTable\" model=\"Model\" />", indexContent, StringComparison.Ordinal);
        Assert.Contains("plugin-admin.js", indexContent, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"InstallFromZip\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-zip-file", indexContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-admin-table", partialContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-admin-action", partialContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-admin-action", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-zip-file-label", scriptContent, StringComparison.Ordinal);
        Assert.Contains("Wird ausgefuehrt...", scriptContent, StringComparison.Ordinal);
    }

    private static ITempDataDictionary BuildTempData(HttpContext httpContext)
        => new TempDataDictionary(httpContext, new TestTempDataProvider());

    private static string GetProjectFilePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var csprojPath = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(csprojPath))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }

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
        public Func<string, byte[], string, string, string, CancellationToken, Task<PluginInstallOperationResult>> OnZipInstall { get; set; }
            = (_, _, _, _, _, _) => Task.FromResult(new PluginInstallOperationResult(true, "installed", "ok"));

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginInstallOperationResult(true, "installed", "ok"));

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(string fileName, byte[] zipBytes, string expectedSha256Hex, string signatureBase64, string signerPublicKeyPem, CancellationToken cancellationToken = default)
            => OnZipInstall(fileName, zipBytes, expectedSha256Hex, signatureBase64, signerPublicKeyPem, cancellationToken);

        public IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins()
            => Array.Empty<InstalledPluginRecord>();
    }

    private sealed class FakePluginLifecycleService : IPluginLifecycleService
    {
        public Func<string, CancellationToken, Task<PluginLifecycleOperationResult>> OnActivate { get; set; }
            = (_, _) => Task.FromResult(new PluginLifecycleOperationResult(false, "not-configured", "not-configured"));

        public Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginLifecycleOperationResult>>(Array.Empty<PluginLifecycleOperationResult>());

        public Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => OnActivate(moduleId, cancellationToken);

        public Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginLifecycleOperationResult(true, "deactivated", "Plugin wurde deaktiviert."));
    }

    private sealed class FakePluginAdminQueryService : IPluginAdminQueryService
    {
        public IReadOnlyList<PluginAdminStatusViewModel> Statuses { get; set; } = Array.Empty<PluginAdminStatusViewModel>();

        public IReadOnlyList<PluginAdminStatusViewModel> GetPluginStatuses()
            => Statuses;

        public PluginAdminStatusViewModel? GetPluginStatus(string moduleId)
            => Statuses.SingleOrDefault(status => string.Equals(status.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakePluginUninstallService : IPluginUninstallService
    {
        public Func<string, bool, CancellationToken, Task<PluginUninstallResult>> OnUninstall { get; set; }
            = (_, _, _) => Task.FromResult(new PluginUninstallResult(true, "uninstalled", "Plugin wurde deinstalliert."));

        public Task<PluginUninstallResult> UninstallAsync(string moduleId, bool removeData, CancellationToken ct = default)
            => OnUninstall(moduleId, removeData, ct);
    }
}
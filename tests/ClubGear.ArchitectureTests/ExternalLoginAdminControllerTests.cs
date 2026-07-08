using ClubGear.Controllers.Admin;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Unit tests for <see cref="ExternalLoginAdminController"/>.
/// All tests use a stub <see cref="IExternalLoginConfigService"/> — no DI, no database.
/// </summary>
public sealed class ExternalLoginAdminControllerTests
{
    // ------------------------------------------------------------------
    // Stub IExternalLoginConfigService
    // ------------------------------------------------------------------

    private sealed class StubExternalLoginConfigService : IExternalLoginConfigService
    {
        public List<ExternalProviderInfo> DeclaredProviders { get; set; } = new();
        public List<(string ProviderKey, IReadOnlyDictionary<string, string> Values)> SaveCalls { get; } = new();
        public Dictionary<string, IReadOnlyDictionary<string, string>> StoredConfig { get; } = new(StringComparer.OrdinalIgnoreCase);
        public PluginExternalLoginTestResult TestResult { get; set; } = new PluginExternalLoginTestResult(true, "OK");

        public Task<IReadOnlyList<ExternalProviderInfo>> GetAllDeclaredProvidersAsync(
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ExternalProviderInfo>>(DeclaredProviders);

        public Task<IReadOnlyDictionary<string, string>> GetConfigAsync(
            string providerKey,
            CancellationToken ct = default)
        {
            StoredConfig.TryGetValue(providerKey, out var values);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                values ?? new Dictionary<string, string>());
        }

        public Task SaveConfigAsync(
            string providerKey,
            IReadOnlyDictionary<string, string> configValues,
            CancellationToken ct = default)
        {
            SaveCalls.Add((providerKey, configValues));
            StoredConfig[providerKey] = configValues;
            return Task.CompletedTask;
        }

        public Task<PluginExternalLoginTestResult> TestConnectionAsync(
            string providerKey,
            CancellationToken ct = default)
            => Task.FromResult(TestResult);
    }

    // ------------------------------------------------------------------
    // Helper: create controller with minimal MVC plumbing
    // ------------------------------------------------------------------

    private static ExternalLoginAdminController CreateController(
        StubExternalLoginConfigService svc)
    {
        var ctrl = new ExternalLoginAdminController(svc);

        // Provide a minimal ControllerContext so Url.Action and TempData work.
        var httpCtx = new DefaultHttpContext();
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = httpCtx
        };
        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
    }

    // Minimal ITempDataProvider so TempDataDictionary can be instantiated without the full MVC pipeline.
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    // ------------------------------------------------------------------
    // Index GET — returns view with all declared providers
    // ------------------------------------------------------------------

    [Fact]
    public async Task Index_ReturnsViewResult_WithDeclaredProviders()
    {
        var svc = new StubExternalLoginConfigService
        {
            DeclaredProviders = new List<ExternalProviderInfo>
            {
                new ExternalProviderInfo("provider-a", "Provider A", "module.a", IsEnabled: false),
                new ExternalProviderInfo("provider-b", "Provider B", "module.b", IsEnabled: true),
            }
        };
        using var ctrl = CreateController(svc);

        var result = await ctrl.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<ExternalProviderInfo>>(viewResult.Model);
        Assert.Equal(2, model.Count);
        Assert.Contains(model, p => p.ProviderKey == "provider-a");
        Assert.Contains(model, p => p.ProviderKey == "provider-b");
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WhenNoProvidersLoaded()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<ExternalProviderInfo>>(viewResult.Model);
        Assert.Empty(model);
    }

    // ------------------------------------------------------------------
    // Configure GET — returns view with provider info and config values
    // ------------------------------------------------------------------

    [Fact]
    public async Task Configure_GET_ReturnsViewResult_WithProviderInfo()
    {
        var svc = new StubExternalLoginConfigService
        {
            DeclaredProviders = new List<ExternalProviderInfo>
            {
                new ExternalProviderInfo("myprovider", "My Provider", "module.a", IsEnabled: false),
            }
        };
        svc.StoredConfig["myprovider"] = new Dictionary<string, string>
        {
            ["authority"] = "https://idp.example.com",
            ["clientid"]  = "my-client",
        };
        using var ctrl = CreateController(svc);

        var result = await ctrl.Configure("myprovider");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalLoginConfigureViewModel>(viewResult.Model);
        Assert.Equal("myprovider", model.ProviderInfo.ProviderKey);
        Assert.Equal("https://idp.example.com", model.ConfigValues["authority"]);
    }

    [Fact]
    public async Task Configure_GET_ReturnsNotFound_WhenProviderKeyUnknown()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Configure("unknown-provider");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Configure_GET_RedirectsToIndex_WhenProviderParamNull()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Configure(provider: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExternalLoginAdminController.Index), redirect.ActionName);
    }

    // ------------------------------------------------------------------
    // Configure POST — calls SaveConfigAsync with correct args
    // ------------------------------------------------------------------

    [Fact]
    public async Task Configure_POST_CallsSaveConfigAsync_WithCorrectProviderKeyAndValues()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var configValues = new Dictionary<string, string>
        {
            ["authority"] = "https://idp.example.com",
            ["clientid"]  = "my-client",
            ["clientsecret"] = "my-secret",
        };

        var result = await ctrl.Configure("myprovider", configValues);

        Assert.Single(svc.SaveCalls);
        var (savedKey, savedValues) = svc.SaveCalls[0];
        Assert.Equal("myprovider", savedKey);
        Assert.Equal("https://idp.example.com", savedValues["authority"]);
        Assert.Equal("my-client",               savedValues["clientid"]);
        Assert.Equal("my-secret",               savedValues["clientsecret"]);
    }

    [Fact]
    public async Task Configure_POST_RedirectsToIndex_AfterSave()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Configure("myprovider", new Dictionary<string, string>
        {
            ["authority"] = "https://idp.example.com",
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExternalLoginAdminController.Index), redirect.ActionName);
    }

    [Fact]
    public async Task Configure_POST_RedirectsToIndex_WhenProviderParamNull()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Configure(provider: null, configValues: new Dictionary<string, string>());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExternalLoginAdminController.Index), redirect.ActionName);
        Assert.Empty(svc.SaveCalls);
    }

    // ------------------------------------------------------------------
    // Activate POST — writes enabled flag via SaveConfigAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task Activate_POST_WritesEnabledTrue_WhenEnabledIsTrue()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Activate("myprovider", enabled: true);

        Assert.Single(svc.SaveCalls);
        var (savedKey, savedValues) = svc.SaveCalls[0];
        Assert.Equal("myprovider", savedKey);
        Assert.Equal("true", savedValues["enabled"]);
    }

    [Fact]
    public async Task Activate_POST_WritesEnabledFalse_WhenEnabledIsFalse()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Activate("myprovider", enabled: false);

        Assert.Single(svc.SaveCalls);
        var (_, savedValues) = svc.SaveCalls[0];
        Assert.Equal("false", savedValues["enabled"]);
    }

    [Fact]
    public async Task Activate_POST_RedirectsToIndex_AfterWritingFlag()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Activate("myprovider", enabled: true);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExternalLoginAdminController.Index), redirect.ActionName);
    }

    [Fact]
    public async Task Activate_POST_RedirectsToIndex_WhenProviderParamNull()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Activate(provider: null, enabled: true);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExternalLoginAdminController.Index), redirect.ActionName);
        Assert.Empty(svc.SaveCalls);
    }

    // ------------------------------------------------------------------
    // Test POST — returns JSON with success and message fields
    // ------------------------------------------------------------------

    [Fact]
    public async Task Test_POST_ReturnsJsonWithSuccess_WhenPluginReturnsSuccess()
    {
        var svc = new StubExternalLoginConfigService
        {
            TestResult = new PluginExternalLoginTestResult(Success: true, Message: "Connection OK")
        };
        using var ctrl = CreateController(svc);

        var result = await ctrl.Test("myprovider");

        var jsonResult = Assert.IsType<JsonResult>(result);
        var json = jsonResult.Value!;
        var successProp = json.GetType().GetProperty("success")!.GetValue(json);
        var messageProp = json.GetType().GetProperty("message")!.GetValue(json);
        Assert.True((bool)successProp!);
        Assert.Equal("Connection OK", (string)messageProp!);
    }

    [Fact]
    public async Task Test_POST_ReturnsJsonWithFailure_WhenPluginReturnsFailure()
    {
        var svc = new StubExternalLoginConfigService
        {
            TestResult = new PluginExternalLoginTestResult(Success: false, Message: "Could not connect")
        };
        using var ctrl = CreateController(svc);

        var result = await ctrl.Test("myprovider");

        var jsonResult = Assert.IsType<JsonResult>(result);
        var json = jsonResult.Value!;
        var successProp = json.GetType().GetProperty("success")!.GetValue(json);
        var messageProp = json.GetType().GetProperty("message")!.GetValue(json);
        Assert.False((bool)successProp!);
        Assert.Equal("Could not connect", (string)messageProp!);
    }

    [Fact]
    public async Task Test_POST_ReturnsBadRequest_WhenProviderParamNull()
    {
        var svc  = new StubExternalLoginConfigService();
        using var ctrl = CreateController(svc);

        var result = await ctrl.Test(provider: null);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}

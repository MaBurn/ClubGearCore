using ClubGear.Controllers;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Tests for slice 10: Login GET populates active providers in ViewData,
/// and the login page renders no provider buttons when the list is empty.
/// </summary>
public sealed class LoginPageProviderButtonsTests
{
    // ------------------------------------------------------------------
    // Stubs
    // ------------------------------------------------------------------

    private sealed class StubAccountFeatureService : IAccountFeatureService
    {
        public Task<AccountLoginOutcome> LoginAsync(string email, string password,
            CancellationToken ct = default)
            => Task.FromResult(new AccountLoginOutcome(AccountLoginStatus.Success));

        public Task<AccountRegistrationOutcome> RegisterAsync(string fullName, string email,
            string password, CancellationToken ct = default)
            => Task.FromResult(new AccountRegistrationOutcome(true, Array.Empty<string>()));

        public Task LogoutAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubExternalLoginService : IExternalLoginService
    {
        public IReadOnlyList<ExternalProviderInfo> ActiveProviders { get; set; }
            = Array.Empty<ExternalProviderInfo>();

        public Task<IReadOnlyList<ExternalProviderInfo>> GetActiveProvidersAsync(
            CancellationToken ct = default)
            => Task.FromResult(ActiveProviders);

        public Task<AuthenticationProperties?> BuildChallengeAsync(
            string providerKey, string redirectUrl, CancellationToken ct = default)
            => Task.FromResult<AuthenticationProperties?>(null);

        public Task<ExternalLoginOutcome> HandleCallbackAsync(
            CancellationToken ct = default)
            => Task.FromResult(new ExternalLoginOutcome(ExternalLoginStatus.Success));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new ActionContext
        {
            HttpContext      = new DefaultHttpContext(),
            RouteData        = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        };

        public string? Action(UrlActionContext actionContext) => "/Account/ExternalLoginCallback";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url)
            => !string.IsNullOrEmpty(url) && url.StartsWith("/", StringComparison.Ordinal);
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }

    private static AccountController CreateController(StubExternalLoginService loginSvc)
    {
        var ctrl = new AccountController(
            new StubAccountFeatureService(),
            loginSvc);

        var httpCtx = new DefaultHttpContext();
        ctrl.ControllerContext = new ControllerContext { HttpContext = httpCtx };
        ctrl.Url      = new StubUrlHelper();
        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
    }

    // ------------------------------------------------------------------
    // 10.1 — controller unit tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task Login_GET_ViewData_ContainsActiveProvider_WhenOneProviderActive()
    {
        var provider = new ExternalProviderInfo(
            ProviderKey: "myprovider",
            DisplayName: "My Provider",
            ModuleId:    "plugin.myprovider",
            IsEnabled:   true);

        var loginSvc = new StubExternalLoginService
        {
            ActiveProviders = new[] { provider }
        };

        using var ctrl = CreateController(loginSvc);

        var result = await ctrl.Login(returnUrl: null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<ExternalProviderInfo>>(
            viewResult.ViewData["ActiveProviders"]);
        Assert.Single(providers);
        Assert.Equal("myprovider", providers[0].ProviderKey);
        Assert.Equal("My Provider", providers[0].DisplayName);
    }

    [Fact]
    public async Task Login_GET_ViewData_ActiveProviders_IsEmpty_WhenNoProvidersActive()
    {
        var loginSvc = new StubExternalLoginService
        {
            ActiveProviders = Array.Empty<ExternalProviderInfo>()
        };

        using var ctrl = CreateController(loginSvc);

        var result = await ctrl.Login(returnUrl: null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<ExternalProviderInfo>>(
            viewResult.ViewData["ActiveProviders"]);
        Assert.Empty(providers);
    }

    [Fact]
    public async Task Login_GET_ViewData_ReturnUrl_IsPreserved()
    {
        var loginSvc = new StubExternalLoginService();
        using var ctrl = CreateController(loginSvc);

        var result = await ctrl.Login(returnUrl: "/dashboard");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("/dashboard", viewResult.ViewData["ReturnUrl"]);
    }

    // ------------------------------------------------------------------
    // 10.3 — WebApplicationFactory integration test
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoginPage_DoesNotRenderProviderButtons_WhenNoProvidersActive()
    {
        // Arrange: use a unique temp DB file so the seeder can run cleanly.
        var dbFile = Path.Combine(Path.GetTempPath(), $"logintest_{Guid.NewGuid():N}.db");
        try
        {
            await using var baseFactory = new WebApplicationFactory<Program>();
            await using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
                builder.ConfigureServices(services =>
                {
                    // Replace the real IExternalLoginService with a stub that returns empty.
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IExternalLoginService));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    services.AddScoped<IExternalLoginService>(_ =>
                        new StubExternalLoginService
                        {
                            ActiveProviders = Array.Empty<ExternalProviderInfo>()
                        });

                    // Remove the OIDC handler to avoid ClientId validation errors
                    // when no provider is configured in the test DB.
                    var oidcDescriptors = services
                        .Where(d => d.ServiceType.FullName != null
                                    && d.ServiceType.FullName.Contains("OpenIdConnect"))
                        .ToList();
                    foreach (var d in oidcDescriptors)
                        services.Remove(d);
                });
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    $"Data Source={dbFile}");
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/Account/Login");
            var html     = await response.Content.ReadAsStringAsync();

            // Assert: page loads and contains no external-provider button markup
            Assert.True(
                (int)response.StatusCode < 500,
                $"Login page returned server error {response.StatusCode}");
            Assert.DoesNotContain("ExternalLoginChallenge", html,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Clean up temp DB files.
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(dbFile) + "*"))
            {
                try { File.Delete(f); } catch { /* best-effort */ }
            }
        }
    }
}

using ClubGear.Controllers;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Unit tests for the external-login actions added to <see cref="AccountController"/>
/// in slice 8: ExternalLoginChallenge and ExternalLoginCallback.
/// All tests use stubs — no DI, no database.
/// </summary>
public sealed class AccountControllerExternalLoginTests
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
        public AuthenticationProperties? ChallengeProperties { get; set; }
        public ExternalLoginOutcome CallbackOutcome { get; set; } =
            new ExternalLoginOutcome(ExternalLoginStatus.Success);

        public Task<IReadOnlyList<ExternalProviderInfo>> GetActiveProvidersAsync(
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ExternalProviderInfo>>(
                Array.Empty<ExternalProviderInfo>());

        public Task<AuthenticationProperties?> BuildChallengeAsync(
            string providerKey,
            string redirectUrl,
            CancellationToken ct = default)
            => Task.FromResult(ChallengeProperties);

        public Task<ExternalLoginOutcome> HandleCallbackAsync(
            CancellationToken ct = default)
            => Task.FromResult(CallbackOutcome);
    }

    // ------------------------------------------------------------------
    // Helper: minimal controller context
    // ------------------------------------------------------------------

    private static AccountController CreateController(
        StubAccountFeatureService? accountSvc = null,
        StubExternalLoginService? loginSvc = null)
    {
        var ctrl = new AccountController(
            accountSvc ?? new StubAccountFeatureService(),
            loginSvc  ?? new StubExternalLoginService());

        var httpCtx = new DefaultHttpContext();
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = httpCtx
        };

        // Provide a stub IUrlHelper so Url.Action does not throw.
        ctrl.Url = new StubUrlHelper();

        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
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
            HttpContext     = new DefaultHttpContext(),
            RouteData       = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        };

        public string? Action(UrlActionContext actionContext) => "/Account/ExternalLoginCallback";

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url)
            => !string.IsNullOrEmpty(url) && url.StartsWith("/", StringComparison.Ordinal);

        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }

    // ------------------------------------------------------------------
    // ExternalLoginChallenge GET — returns ChallengeResult with correct scheme
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExternalLoginChallenge_ReturnsChallengeResult_WithCorrectScheme()
    {
        var loginSvc = new StubExternalLoginService
        {
            ChallengeProperties = new AuthenticationProperties()
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginChallenge("myprovider");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains("oidc.myprovider", challenge.AuthenticationSchemes);
    }

    [Fact]
    public async Task ExternalLoginChallenge_ReturnsChallengeResult_PropertiesFromService()
    {
        var props = new AuthenticationProperties { RedirectUri = "/callback" };
        var loginSvc = new StubExternalLoginService
        {
            ChallengeProperties = props
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginChallenge("myprovider");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/callback", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public async Task ExternalLoginChallenge_ReturnsBadRequest_WhenServiceReturnsNull()
    {
        var loginSvc = new StubExternalLoginService
        {
            ChallengeProperties = null
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginChallenge("unknown-provider");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ------------------------------------------------------------------
    // ExternalLoginCallback GET — redirects on Success
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExternalLoginCallback_RedirectsToSelfService_OnSuccess_WhenNoReturnUrl()
    {
        var loginSvc = new StubExternalLoginService
        {
            CallbackOutcome = new ExternalLoginOutcome(ExternalLoginStatus.Success)
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginCallback(returnUrl: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index",       redirect.ActionName);
        Assert.Equal("SelfService", redirect.ControllerName);
    }

    [Fact]
    public async Task ExternalLoginCallback_RedirectsToReturnUrl_OnSuccess_WhenLocalUrl()
    {
        var loginSvc = new StubExternalLoginService
        {
            CallbackOutcome = new ExternalLoginOutcome(ExternalLoginStatus.Success)
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginCallback(returnUrl: "/members");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/members", redirect.Url);
    }

    // ------------------------------------------------------------------
    // ExternalLoginCallback GET — returns view on NoLinkedMember
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExternalLoginCallback_ReturnsViewResult_OnNoLinkedMember()
    {
        var loginSvc = new StubExternalLoginService
        {
            CallbackOutcome = new ExternalLoginOutcome(
                ExternalLoginStatus.NoLinkedMember,
                ExternalUserId: "ext-user-123",
                ProviderKey:    "myprovider")
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginCallback();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalLoginOutcome>(viewResult.Model);
        Assert.Equal(ExternalLoginStatus.NoLinkedMember, model.Status);
        Assert.Equal("ext-user-123", model.ExternalUserId);
    }

    [Fact]
    public async Task ExternalLoginCallback_ViewData_ContainsExternalUserId_OnNoLinkedMember()
    {
        var loginSvc = new StubExternalLoginService
        {
            CallbackOutcome = new ExternalLoginOutcome(
                ExternalLoginStatus.NoLinkedMember,
                ExternalUserId: "ext-user-456",
                ProviderKey:    "otherprovider")
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        await ctrl.ExternalLoginCallback();

        Assert.Equal("ext-user-456", ctrl.ViewData["ExternalUserId"]);
        Assert.Equal("otherprovider", ctrl.ViewData["ProviderKey"]);
    }

    // ------------------------------------------------------------------
    // ExternalLoginCallback GET — returns view on ProviderError
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExternalLoginCallback_ReturnsViewResult_OnProviderError()
    {
        var loginSvc = new StubExternalLoginService
        {
            CallbackOutcome = new ExternalLoginOutcome(
                ExternalLoginStatus.ProviderError,
                ErrorMessage: "Invalid state.")
        };
        using var ctrl = CreateController(loginSvc: loginSvc);

        var result = await ctrl.ExternalLoginCallback();

        Assert.IsType<ViewResult>(result);
    }
}

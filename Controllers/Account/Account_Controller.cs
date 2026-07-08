using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers;

public class AccountController : Controller
{
    private readonly IAccountFeatureService _accountFeatureService;
    private readonly IExternalLoginService _externalLoginService;

    public AccountController(
        IAccountFeatureService accountFeatureService,
        IExternalLoginService externalLoginService)
    {
        _accountFeatureService = accountFeatureService;
        _externalLoginService  = externalLoginService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        var activeProviders = await _externalLoginService.GetActiveProvidersAsync(cancellationToken);
        ViewData["ActiveProviders"] = activeProviders;

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        var outcome = await _accountFeatureService.LoginAsync(email, password, cancellationToken);
        if (outcome.Status == AccountLoginStatus.MissingCredentials)
        {
            ModelState.AddModelError(string.Empty, "Bitte E-Mail und Passwort eingeben.");
            return View();
        }

        if (outcome.Status == AccountLoginStatus.InvalidCredentials)
        {
            ModelState.AddModelError(string.Empty, "Ungueltige Anmeldedaten.");
            return View();
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "SelfService");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        var outcome = await _accountFeatureService.RegisterAsync(fullName, email, password, cancellationToken);
        if (!outcome.Succeeded)
        {
            foreach (var error in outcome.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View();
        }

        return RedirectToAction("Index", "SelfService");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        await _accountFeatureService.LogoutAsync(cancellationToken);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ------------------------------------------------------------------
    // External login: challenge
    // ------------------------------------------------------------------

    /// <summary>
    /// Initiates the OIDC challenge for the given provider key.
    /// Returns <c>BadRequest</c> when the provider is unknown or config is incomplete.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginChallenge(
        string provider,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var callbackUrl = Url.Action(
            nameof(ExternalLoginCallback),
            "Account",
            new { returnUrl },
            Request.Scheme) ?? "/Account/ExternalLoginCallback";

        var properties = await _externalLoginService.BuildChallengeAsync(
            provider, callbackUrl, cancellationToken);

        if (properties is null)
        {
            return BadRequest("Unknown provider or incomplete configuration.");
        }

        var scheme = $"oidc.{provider}";
        return Challenge(properties, scheme);
    }

    // ------------------------------------------------------------------
    // External login: callback
    // ------------------------------------------------------------------

    /// <summary>
    /// Handles the OIDC callback. Redirects on success; renders the link-account
    /// fallback view when no linked member is found.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var outcome = await _externalLoginService.HandleCallbackAsync(cancellationToken);

        if (outcome.Status == ExternalLoginStatus.Success)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "SelfService");
        }

        if (outcome.Status == ExternalLoginStatus.NoLinkedMember)
        {
            ViewData["ExternalUserId"] = outcome.ExternalUserId;
            ViewData["ProviderKey"]    = outcome.ProviderKey;
            return View(outcome);
        }

        // ProviderError / InternalError
        ModelState.AddModelError(
            string.Empty,
            outcome.ErrorMessage ?? "External login failed.");
        return View(outcome);
    }
}

using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// High-level facade that orchestrates the external-login challenge and callback.
/// </summary>
internal sealed class ExternalLoginService : IExternalLoginService
{
    private static readonly string[] RequiredConfigKeys = ["authority", "clientid", "clientsecret"];

    private readonly IExternalLoginConfigService  _configService;
    private readonly IIdpClaimsEnricher           _claimsEnricher;
    private readonly IExternalLoginInfoProvider   _loginInfoProvider;
    private readonly ApplicationDbContext         _db;
    private readonly ILogger<ExternalLoginService> _logger;

    public ExternalLoginService(
        IExternalLoginConfigService   configService,
        IIdpClaimsEnricher            claimsEnricher,
        IExternalLoginInfoProvider    loginInfoProvider,
        ApplicationDbContext          db,
        ILogger<ExternalLoginService> logger)
    {
        _configService     = configService;
        _claimsEnricher    = claimsEnricher;
        _loginInfoProvider = loginInfoProvider;
        _db                = db;
        _logger            = logger;
    }

    // ------------------------------------------------------------------
    // GetActiveProvidersAsync
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<ExternalProviderInfo>> GetActiveProvidersAsync(
        CancellationToken ct = default)
    {
        var all = await _configService.GetAllDeclaredProvidersAsync(ct);
        return all.Where(p => p.IsEnabled).ToList();
    }

    // ------------------------------------------------------------------
    // BuildChallengeAsync
    // ------------------------------------------------------------------

    public async Task<AuthenticationProperties?> BuildChallengeAsync(
        string providerKey,
        string redirectUrl,
        CancellationToken ct = default)
    {
        var providers = await _configService.GetAllDeclaredProvidersAsync(ct);
        var provider  = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            _logger.LogWarning(
                "BuildChallengeAsync: unknown provider key '{ProviderKey}'.", providerKey);
            return null;
        }

        var config = await _configService.GetConfigAsync(providerKey, ct);

        foreach (var requiredKey in RequiredConfigKeys)
        {
            if (!config.TryGetValue(requiredKey, out var value) || string.IsNullOrWhiteSpace(value))
            {
                _logger.LogWarning(
                    "BuildChallengeAsync: provider '{ProviderKey}' is missing config key '{Key}'.",
                    providerKey, requiredKey);
                return null;
            }
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl,
            Items =
            {
                ["providerKey"] = providerKey,
                ["LoginProvider"] = $"oidc.{providerKey}"
            }
        };

        return properties;
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync
    // ------------------------------------------------------------------

    public async Task<ExternalLoginOutcome> HandleCallbackAsync(
        CancellationToken ct = default)
    {
        ExternalLoginInfo? info;
        try
        {
            info = await _loginInfoProvider.GetExternalLoginInfoAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleCallbackAsync: failed to retrieve external login info.");
            return new ExternalLoginOutcome(ExternalLoginStatus.ProviderError,
                ErrorMessage: "Failed to retrieve external login info.");
        }

        if (info is null)
        {
            _logger.LogWarning("HandleCallbackAsync: no external login info available.");
            return new ExternalLoginOutcome(ExternalLoginStatus.ProviderError,
                ErrorMessage: "No external login info available.");
        }

        // Determine the provider key from the login provider name (strip "oidc." prefix).
        var loginProvider = info.LoginProvider ?? string.Empty;
        var providerKey   = loginProvider.StartsWith("oidc.", StringComparison.OrdinalIgnoreCase)
            ? loginProvider["oidc.".Length..]
            : loginProvider;

        // Collect subject claim from the principal.
        var subject = info.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? info.ProviderKey;

        // Build context for plugin claim enrichment.
        var rawClaims = info.Principal?.Claims
            .ToDictionary(c => c.Type, c => c.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var config = await _configService.GetConfigAsync(providerKey, ct);

        var context = new PluginExternalLoginContext(
            ProviderKey: providerKey,
            Subject:     subject,
            RawClaims:   rawClaims,
            Config:      config);

        // Dispatch to plugin for claim mapping.
        IReadOnlyList<PluginClaimEntry> enrichedClaims;
        try
        {
            enrichedClaims = await _claimsEnricher.EnrichAsync(providerKey, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "HandleCallbackAsync: claim enrichment failed for provider '{ProviderKey}'.",
                providerKey);
            enrichedClaims = Array.Empty<PluginClaimEntry>();
        }

        // Look up the member by subject (OauthID).
        var effectiveSubject = enrichedClaims
            .FirstOrDefault(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? subject;

        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OauthID == effectiveSubject, ct);

        if (member is null)
        {
            _logger.LogInformation(
                "HandleCallbackAsync: no member linked to subject '{Subject}' for provider '{ProviderKey}'.",
                effectiveSubject, providerKey);

            return new ExternalLoginOutcome(
                Status:         ExternalLoginStatus.NoLinkedMember,
                ExternalUserId: effectiveSubject,
                ProviderKey:    providerKey);
        }

        return new ExternalLoginOutcome(
            Status:         ExternalLoginStatus.Success,
            ExternalUserId: effectiveSubject,
            ProviderKey:    providerKey);
    }
}

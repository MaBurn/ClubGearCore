using System.Security.Claims;
using System.Text.Json;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// High-level facade that orchestrates the external-login challenge and callback.
/// </summary>
internal sealed class ExternalLoginService : IExternalLoginService
{
    private readonly IExternalLoginConfigService  _configService;
    private readonly IIdpClaimsEnricher           _claimsEnricher;
    private readonly IExternalLoginInfoProvider   _loginInfoProvider;
    private readonly IExternalLoginSessionService _sessionService;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly ApplicationDbContext         _db;
    private readonly ILogger<ExternalLoginService> _logger;

    public ExternalLoginService(
        IExternalLoginConfigService   configService,
        IIdpClaimsEnricher            claimsEnricher,
        IExternalLoginInfoProvider    loginInfoProvider,
        IExternalLoginSessionService  sessionService,
        IAuthenticationSchemeProvider schemeProvider,
        ApplicationDbContext          db,
        ILogger<ExternalLoginService> logger)
    {
        _configService     = configService;
        _claimsEnricher    = claimsEnricher;
        _loginInfoProvider = loginInfoProvider;
        _sessionService    = sessionService;
        _schemeProvider    = schemeProvider;
        _db                = db;
        _logger            = logger;
    }

    // ------------------------------------------------------------------
    // GetActiveProvidersAsync
    // ------------------------------------------------------------------

    public async Task RegisterDeclaredSchemesAsync(CancellationToken ct = default)
    {
        var providers = await _configService.GetAllDeclaredProvidersAsync(ct);
        foreach (var group in providers
            .Where(provider => provider.IsEnabled && provider.IsAvailable)
            .GroupBy(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() != 1)
            {
                _logger.LogWarning(
                    "Identity provider key '{ProviderKey}' is declared more than once and was not registered.",
                    group.Key);
                continue;
            }

            var provider = group.Single();
            if (!ExternalLoginServiceProviderKey.IsValid(provider.ProviderKey))
            {
                _logger.LogWarning(
                    "Identity provider '{ProviderKey}' has an invalid provider key and was not registered.",
                    provider.ProviderKey);
                continue;
            }

            var config = await _configService.GetConfigAsync(provider.ProviderKey, ct);
            if (!OidcProviderConfiguration.TryCreate(config, out _, out var configError))
            {
                _logger.LogWarning(
                    "Identity provider '{ProviderKey}' was not registered: {ConfigError}",
                    provider.ProviderKey,
                    configError);
                continue;
            }

            await EnsureSchemeAsync(provider);
        }
    }

    public async Task<IReadOnlyList<ExternalProviderInfo>> GetActiveProvidersAsync(
        CancellationToken ct = default)
    {
        var all = await _configService.GetAllDeclaredProvidersAsync(ct);
        var active = new List<ExternalProviderInfo>();
        foreach (var group in all
            .Where(provider => provider.IsEnabled && provider.IsAvailable)
            .GroupBy(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() != 1)
            {
                continue;
            }

            var provider = group.Single();
            if (!ExternalLoginServiceProviderKey.IsValid(provider.ProviderKey))
            {
                continue;
            }

            var config = await _configService.GetConfigAsync(provider.ProviderKey, ct);
            if (OidcProviderConfiguration.TryCreate(config, out _, out _))
            {
                active.Add(provider);
            }
        }

        return active;
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
        var provider = FindUniqueProvider(providers, providerKey);

        if (provider is null
            || !provider.IsEnabled
            || !provider.IsAvailable
            || !ExternalLoginServiceProviderKey.IsValid(provider.ProviderKey))
        {
            _logger.LogWarning(
                "BuildChallengeAsync: unknown provider key '{ProviderKey}'.", providerKey);
            return null;
        }

        var config = await _configService.GetConfigAsync(provider.ProviderKey, ct);

        if (!OidcProviderConfiguration.TryCreate(config, out _, out var configError))
        {
            _logger.LogWarning(
                "BuildChallengeAsync: provider '{ProviderKey}' has unusable configuration: {ConfigError}",
                provider.ProviderKey,
                configError);
            return null;
        }

        var schemeName = await EnsureSchemeAsync(provider);
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl,
            Items =
            {
                ["providerKey"] = provider.ProviderKey,
                ["LoginProvider"] = schemeName
            }
        };

        return properties;
    }

    private async Task<string> EnsureSchemeAsync(ExternalProviderInfo provider)
    {
        var schemeName = $"oidc.{provider.ProviderKey}";
        if (await _schemeProvider.GetSchemeAsync(schemeName) is null)
        {
            try
            {
                _schemeProvider.AddScheme(new AuthenticationScheme(
                    schemeName,
                    provider.DisplayName,
                    typeof(OpenIdConnectHandler)));
            }
            catch (InvalidOperationException)
            {
                if (await _schemeProvider.GetSchemeAsync(schemeName) is null)
                {
                    throw;
                }

                // Another concurrent challenge registered the same provider first.
            }
        }

        return schemeName;
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await ClearExternalBestEffortAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleCallbackAsync: failed to retrieve external login info.");
            await ClearExternalBestEffortAsync();
            return new ExternalLoginOutcome(ExternalLoginStatus.ProviderError,
                ErrorMessage: "Failed to retrieve external login info.");
        }

        if (!await ClearExternalAsync(ct))
        {
            return new ExternalLoginOutcome(
                ExternalLoginStatus.InternalError,
                ErrorMessage: "Could not clear the temporary external-login session.");
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

        var declaredProviders = await _configService.GetAllDeclaredProvidersAsync(ct);
        var declaredProvider = FindUniqueProvider(declaredProviders, providerKey);
        if (declaredProvider is null
            || !declaredProvider.IsEnabled
            || !declaredProvider.IsAvailable
            || !ExternalLoginServiceProviderKey.IsValid(declaredProvider.ProviderKey))
        {
            _logger.LogWarning(
                "HandleCallbackAsync: provider '{ProviderKey}' is no longer declared and enabled.",
                providerKey);
            return new ExternalLoginOutcome(
                ExternalLoginStatus.ProviderError,
                ErrorMessage: "Identity provider is not enabled.",
                ProviderKey: providerKey);
        }

        // Use the declaration's exact casing for config sections, mapper dispatch,
        // and outcomes even if an authentication implementation returned a
        // differently-cased scheme name.
        providerKey = declaredProvider.ProviderKey;

        // Collect subject claim from the principal.
        var subject = info.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? info.ProviderKey;

        // Build context for plugin claim enrichment.
        var rawClaims = info.Principal?.Claims
            .GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => SerializeClaimValues(group.Select(claim => claim.Value)),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var config = await _configService.GetConfigAsync(providerKey, ct);
        if (!OidcProviderConfiguration.TryCreate(config, out _, out _))
        {
            _logger.LogWarning(
                "HandleCallbackAsync: provider '{ProviderKey}' is no longer fully configured.",
                providerKey);
            return new ExternalLoginOutcome(
                ExternalLoginStatus.ProviderError,
                ErrorMessage: "Identity provider configuration is incomplete.",
                ProviderKey: providerKey);
        }

        var context = new PluginExternalLoginContext(
            ProviderKey: providerKey,
            Subject:     subject,
            RawClaims:   rawClaims,
            Config:      config);

        // Dispatch to plugin for claim mapping.
        IReadOnlyList<PluginClaimEntry> enrichedClaims;
        try
        {
            enrichedClaims = await _claimsEnricher.EnrichAsync(providerKey, context, ct)
                ?? throw new InvalidOperationException(
                    "The identity-provider claim mapper returned null.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "HandleCallbackAsync: claim enrichment failed for provider '{ProviderKey}'.",
                providerKey);
            return new ExternalLoginOutcome(
                ExternalLoginStatus.ProviderError,
                ErrorMessage: "Identity-provider claim mapping failed.",
                ProviderKey: providerKey);
        }

        // Look up the member by subject (OauthID).
        var effectiveSubject = enrichedClaims
            .FirstOrDefault(c =>
                string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(c.Value))
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

        bool signedIn;
        try
        {
            signedIn = !string.IsNullOrWhiteSpace(member.ApplicationUserId)
                && await _sessionService.SignInAsync(member.ApplicationUserId, enrichedClaims, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "HandleCallbackAsync: failed to create the local session for member {MemberId}.",
                member.Id);
            return new ExternalLoginOutcome(
                Status: ExternalLoginStatus.InternalError,
                ExternalUserId: effectiveSubject,
                ProviderKey: providerKey,
                ErrorMessage: "Could not create the local application session.");
        }

        if (!signedIn)
        {
            _logger.LogWarning(
                "HandleCallbackAsync: member {MemberId} is linked to subject '{Subject}', but has no valid application user.",
                member.Id, effectiveSubject);

            return new ExternalLoginOutcome(
                Status: ExternalLoginStatus.NoLinkedMember,
                ExternalUserId: effectiveSubject,
                ProviderKey: providerKey,
                ErrorMessage: "The linked member has no ClubGear application account.");
        }

        return new ExternalLoginOutcome(
            Status:         ExternalLoginStatus.Success,
            ExternalUserId: effectiveSubject,
            ProviderKey:    providerKey);
    }

    private async Task<bool> ClearExternalAsync(CancellationToken ct)
    {
        try
        {
            await _sessionService.ClearExternalAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleCallbackAsync: failed to clear the external-login session.");
            return false;
        }
    }

    private async Task ClearExternalBestEffortAsync()
    {
        try
        {
            await _sessionService.ClearExternalAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "HandleCallbackAsync: could not clear the external-login session after an earlier failure.");
        }
    }

    private static ExternalProviderInfo? FindUniqueProvider(
        IReadOnlyList<ExternalProviderInfo> providers,
        string providerKey)
    {
        var matches = providers
            .Where(provider => string.Equals(
                provider.ProviderKey,
                providerKey,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string SerializeClaimValues(IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 1
            ? materialized[0]
            : JsonSerializer.Serialize(materialized);
    }
}

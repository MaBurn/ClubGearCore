using Microsoft.AspNetCore.Authentication;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// High-level facade for the external-login flow (challenge + callback).
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Ensures that every currently declared, enabled, available, and validly configured
    /// provider has a named OIDC authentication scheme. Called during startup after
    /// activated plugins are loaded.
    /// </summary>
    Task RegisterDeclaredSchemesAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Returns every identity provider that is currently declared by a loaded plugin
    /// AND has the <c>enabled</c> config flag set to <c>true</c>, with a complete and
    /// valid OIDC configuration.
    /// </summary>
    Task<IReadOnlyList<ExternalProviderInfo>> GetActiveProvidersAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Validates that the provider referenced by <paramref name="providerKey"/> is
    /// enabled, configured (authority, clientId, clientSecret are present and valid), ensures
    /// its provider-specific OIDC scheme is registered, and returns an
    /// <see cref="AuthenticationProperties"/> instance that should be passed to a
    /// <see cref="Microsoft.AspNetCore.Mvc.ChallengeResult"/>.
    /// Returns <c>null</c> when the provider is unknown or config is incomplete.
    /// </summary>
    Task<AuthenticationProperties?> BuildChallengeAsync(
        string providerKey,
        string redirectUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Processes the OIDC callback: retrieves the external login info, enriches claims
    /// via <see cref="IIdpClaimsEnricher"/>, looks up the linked <c>Member</c> by the
    /// subject claim stored in <c>Member.OauthID</c>, signs in its linked application
    /// user, and returns an outcome record.
    /// </summary>
    Task<ExternalLoginOutcome> HandleCallbackAsync(
        CancellationToken ct = default);
}

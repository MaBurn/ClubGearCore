using System.Security.Claims;
using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// CORE-09. Issues, lists, revokes and authenticates API keys.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Creates a key and returns the secret. This is the only moment the secret exists outside the
    /// caller's hands — it is hashed before storage and cannot be recovered afterwards.
    /// </summary>
    Task<ApiKeyIssueResult> CreateAsync(
        string name,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc,
        string? createdBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListAsync(bool includeRevoked = false, CancellationToken ct = default);

    /// <summary>Revoking is permanent; a revoked key is never reactivated, a new one is issued.</summary>
    Task<bool> RevokeAsync(int id, string? revokedBy, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented secret. Returns a principal carrying the key's scopes as
    /// <c>permission</c> claims, or <c>null</c> when the key is unknown, revoked or expired.
    /// </summary>
    Task<ClaimsPrincipal?> AuthenticateAsync(string presentedKey, string authenticationScheme, CancellationToken ct = default);
}

/// <summary>The stored record plus the one-time plaintext secret.</summary>
public sealed record ApiKeyIssueResult(ApiKey Key, string Secret);

public sealed class ApiKeyValidationException : Exception
{
    public ApiKeyValidationException(string message) : base(message)
    {
    }
}

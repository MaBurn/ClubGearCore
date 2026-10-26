using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

/// <summary>
/// CORE-09. See <see cref="IApiKeyService"/>.
/// </summary>
public sealed class ApiKeyService : IApiKeyService
{
    /// <summary>Marks a principal as a service account rather than a signed-in person.</summary>
    public const string ServiceAccountClaimType = "clubgear.service-account";

    /// <summary>Carries the database id of the key that authenticated the request.</summary>
    public const string ApiKeyIdClaimType = "clubgear.api-key-id";

    /// <summary>Distinguishes a ClubGear key from any other bearer token at a glance in logs.</summary>
    public const string KeyPrefixMarker = "cgk_";

    /// <summary>Characters of the secret kept in clear for identification and lookup.</summary>
    internal const int PrefixLength = 12;

    /// <summary>
    /// How stale LastUsedAtUtc may get. Writing it on every request would turn a read-only
    /// authentication into a write on the hot path.
    /// </summary>
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLog,
        TimeProvider timeProvider,
        ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApiKeyIssueResult> CreateAsync(
        string name,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc,
        string? createdBy,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scopes);

        var normalisedScopes = NormaliseScopes(scopes);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (expiresAtUtc is not null && expiresAtUtc <= now)
        {
            throw new ApiKeyValidationException("Das Ablaufdatum muss in der Zukunft liegen.");
        }

        var secret = GenerateSecret();

        var key = new ApiKey
        {
            Name = name.Trim(),
            KeyPrefix = secret[..PrefixLength],
            KeyHash = Hash(secret),
            ScopesCsv = string.Join(',', normalisedScopes),
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            ExpiresAtUtc = expiresAtUtc
        };

        _dbContext.ApiKeys.Add(key);
        await _dbContext.SaveChangesAsync(ct);

        // The secret is deliberately absent from the audit record.
        await _auditLog.LogAsync(
            new AuditLogRecord(
                Action: "apikey.created",
                Actor: createdBy,
                Source: "core",
                TargetType: nameof(ApiKey),
                TargetId: key.Id.ToString(),
                After: new { key.Name, key.KeyPrefix, Scopes = normalisedScopes, key.ExpiresAtUtc },
                OccurredAtUtc: now),
            ct);

        return new ApiKeyIssueResult(key, secret);
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(bool includeRevoked = false, CancellationToken ct = default)
    {
        var query = _dbContext.ApiKeys.AsNoTracking();

        if (!includeRevoked)
        {
            query = query.Where(k => k.RevokedAtUtc == null);
        }

        return await query
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeAsync(int id, string? revokedBy, CancellationToken ct = default)
    {
        var key = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        if (key is null || key.IsRevoked)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        key.RevokedAtUtc = now;
        key.RevokedBy = revokedBy;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            new AuditLogRecord(
                Action: "apikey.revoked",
                Actor: revokedBy,
                Source: "core",
                TargetType: nameof(ApiKey),
                TargetId: key.Id.ToString(),
                After: new { key.Name, key.KeyPrefix, key.RevokedAtUtc },
                OccurredAtUtc: now),
            ct);

        return true;
    }

    public async Task<ClaimsPrincipal?> AuthenticateAsync(
        string presentedKey,
        string authenticationScheme,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) || presentedKey.Length <= PrefixLength)
        {
            return null;
        }

        var prefix = presentedKey[..PrefixLength];

        // The prefix narrows the candidates; it is not itself a credential. The decision is made
        // by the fixed-time hash comparison below.
        var candidates = await _dbContext.ApiKeys
            .Where(k => k.KeyPrefix == prefix)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        var presentedHash = Hash(presentedKey);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        ApiKey? matched = null;
        foreach (var candidate in candidates)
        {
            if (FixedTimeEquals(candidate.KeyHash, presentedHash))
            {
                matched = candidate;
                break;
            }
        }

        if (matched is null)
        {
            return null;
        }

        if (!matched.IsUsableAt(now))
        {
            _logger.LogInformation(
                "API-Key {KeyPrefix} abgelehnt: {Reason}.",
                matched.KeyPrefix,
                matched.IsRevoked ? "widerrufen" : "abgelaufen");
            return null;
        }

        await TouchLastUsedAsync(matched, now, ct);

        return BuildPrincipal(matched, authenticationScheme);
    }

    /// <summary>
    /// Builds the principal. Deliberately narrow: the key's scopes become <c>permission</c> claims,
    /// which <see cref="Authorization.DatabasePermissionService"/> already honours, and nothing
    /// else. No roles, and never the master-admin claim — an API key cannot be an administrator.
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(ApiKey key, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"apikey:{key.Id}"),
            new(ClaimTypes.Name, key.Name),
            new(ServiceAccountClaimType, "true"),
            new(ApiKeyIdClaimType, key.Id.ToString())
        };

        claims.AddRange(key.Scopes.Select(scope => new Claim("permission", scope)));

        var identity = new ClaimsIdentity(claims, authenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private async Task TouchLastUsedAsync(ApiKey key, DateTime now, CancellationToken ct)
    {
        if (key.LastUsedAtUtc is not null && now - key.LastUsedAtUtc < LastUsedResolution)
        {
            return;
        }

        key.LastUsedAtUtc = now;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Rejects the wildcard and unknown-shaped entries, de-duplicates, and requires at least one
    /// scope. A key with no scope would authenticate but authorise nothing, which is a
    /// configuration mistake worth failing loudly rather than shipping.
    /// </summary>
    internal static IReadOnlyList<string> NormaliseScopes(IReadOnlyCollection<string> scopes)
    {
        var cleaned = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            throw new ApiKeyValidationException("Ein API-Key braucht mindestens eine Berechtigung.");
        }

        if (cleaned.Any(s => s == PermissionKeys.Wildcard))
        {
            throw new ApiKeyValidationException(
                "Der Platzhalter \"*\" ist für API-Keys nicht zulässig. Berechtigungen müssen einzeln vergeben werden.");
        }

        if (cleaned.Any(s => s.Contains(',', StringComparison.Ordinal)))
        {
            throw new ApiKeyValidationException("Berechtigungsschlüssel dürfen kein Komma enthalten.");
        }

        return cleaned;
    }

    /// <summary>
    /// 32 bytes of cryptographic randomness, base64url-encoded and marker-prefixed. Base64url
    /// avoids '+', '/' and '=' so the value survives headers, query strings and .env files intact.
    /// </summary>
    internal static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return KeyPrefixMarker + Base64Url(bytes);
    }

    internal static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}

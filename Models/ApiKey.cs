namespace ClubGear.Models;

/// <summary>
/// CORE-09. A credential that authenticates a non-browser caller — a service account rather than
/// a person. The secret itself is never stored: only its hash, plus a short prefix so a key can be
/// identified in the UI and looked up without scanning every row.
/// </summary>
public class ApiKey
{
    public int Id { get; set; }

    /// <summary>Human label, e.g. "Mobile Inventur" or "Traccar-Import".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The leading, non-secret characters of the key. Shown in the UI so an operator can tell two
    /// keys apart, and used to narrow the lookup before the hash comparison.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>Hex-encoded SHA-256 of the full presented key.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated permission keys this key may exercise. Never contains the wildcard: an API
    /// key is always scoped to an explicit list.
    /// </summary>
    public string ScopesCsv { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    /// <summary><c>null</c> never expires.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Coarse last-use marker; written at most once a minute, not on every request.</summary>
    public DateTime? LastUsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedBy { get; set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpiredAt(DateTime utcNow) => ExpiresAtUtc is not null && ExpiresAtUtc <= utcNow;

    public bool IsUsableAt(DateTime utcNow) => !IsRevoked && !IsExpiredAt(utcNow);

    public IReadOnlyList<string> Scopes =>
        string.IsNullOrWhiteSpace(ScopesCsv)
            ? Array.Empty<string>()
            : ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

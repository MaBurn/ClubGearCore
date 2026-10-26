namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Validates the core OIDC settings shared by scheme discovery, challenges, and
/// named-options materialization. Keeping this validation in one place prevents a
/// provider from being shown as usable while its authentication handler cannot start.
/// </summary>
internal static class OidcProviderConfiguration
{
    private static readonly string[] RequiredKeys = ["authority", "clientid", "clientsecret"];

    public static bool IsEnabled(IReadOnlyDictionary<string, string> config)
        => TryGetValue(config, "enabled", out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static bool TryCreate(
        IReadOnlyDictionary<string, string> config,
        out OidcProviderSettings settings,
        out string error)
    {
        settings = default!;

        foreach (var key in RequiredKeys)
        {
            if (!TryGetValue(config, key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                error = $"Required setting '{key}' is missing.";
                return false;
            }
        }

        TryGetValue(config, "authority", out var rawAuthority);
        TryGetValue(config, "clientid", out var rawClientId);
        TryGetValue(config, "clientsecret", out var clientSecret);

        var authority = rawAuthority!.Trim();
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
            || string.IsNullOrWhiteSpace(authorityUri.Host)
            || !string.IsNullOrEmpty(authorityUri.UserInfo)
            || !string.IsNullOrEmpty(authorityUri.Query)
            || !string.IsNullOrEmpty(authorityUri.Fragment))
        {
            error = "The authority must be an absolute OIDC issuer URI without credentials, query, or fragment.";
            return false;
        }

        var usesHttps = string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var usesLoopbackHttp = string.Equals(
                authorityUri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase)
            && authorityUri.IsLoopback;
        if (!usesHttps && !usesLoopbackHttp)
        {
            error = "The authority must use HTTPS; HTTP is allowed only for a loopback development issuer.";
            return false;
        }

        settings = new OidcProviderSettings(
            Authority: authority,
            ClientId: rawClientId!.Trim(),
            ClientSecret: clientSecret!,
            RequireHttpsMetadata: usesHttps);
        error = string.Empty;
        return true;
    }

    private static bool TryGetValue(
        IReadOnlyDictionary<string, string> config,
        string key,
        out string? value)
    {
        if (config.TryGetValue(key, out var directValue))
        {
            value = directValue;
            return true;
        }

        foreach (var entry in config)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}

internal sealed record OidcProviderSettings(
    string Authority,
    string ClientId,
    string ClientSecret,
    bool RequireHttpsMetadata);

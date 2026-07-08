namespace ClubGear.Plugin.Contracts;

public sealed record PluginClaimEntry(string Type, string Value);

public sealed record PluginExternalLoginContext(
    string ProviderKey,
    string Subject,
    IReadOnlyDictionary<string, string> RawClaims,
    IReadOnlyDictionary<string, string> Config);

public sealed record PluginExternalLoginTestResult(
    bool Success,
    string? Message = null);

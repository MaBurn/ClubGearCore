using System.Text.RegularExpressions;

namespace ClubGear.Services.ExternalLogin;

internal static class ExternalLoginServiceProviderKey
{
    private static readonly Regex Pattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool IsValid(string providerKey)
        => !string.IsNullOrWhiteSpace(providerKey) && Pattern.IsMatch(providerKey);
}

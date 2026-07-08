namespace ClubGear.Plugin.Contracts;

public sealed record PluginManifest(
    string Key,
    string Name,
    Version Version,
    string Author,
    string License,
    string EntryPoint,
    string RequiredCoreVersion,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> ExtensionPoints)
{
    public string ModuleId => Key;

    public string DisplayName => Name;

    public Version PluginVersion => Version;

    public string SuggestedDataPrefix => CreateSuggestedDataPrefix(Key);

    public string EntryPointType => EntryPoint;

    public Version RequiredContractVersion => ParseMinimumRequiredVersion(RequiredCoreVersion);

    public string Category { get; init; } = "General";

    public IReadOnlyList<PluginDependency> Dependencies { get; init; } = Array.Empty<PluginDependency>();

    private static string CreateSuggestedDataPrefix(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        builder.Append("plugin_");

        var previousWasUnderscore = false;
        foreach (var character in value)
        {
            var normalized = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '_';

            if (normalized == '_' && previousWasUnderscore)
            {
                continue;
            }

            builder.Append(normalized);
            previousWasUnderscore = normalized == '_';
        }

        if (!previousWasUnderscore)
        {
            builder.Append('_');
        }

        return builder.ToString();
    }

    private static Version ParseMinimumRequiredVersion(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith(">=", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }

        return Version.TryParse(normalized, out var parsed)
            ? parsed
            : ContractVersion.Current;
    }
}
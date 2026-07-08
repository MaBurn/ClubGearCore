using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Plugins.Manifest;

public sealed record PluginManifestValidationResult(
    bool IsValid,
    PluginManifest? Manifest,
    IReadOnlyList<string> Errors)
{
    public static PluginManifestValidationResult Success(PluginManifest manifest)
        => new(true, manifest, Array.Empty<string>());

    public static PluginManifestValidationResult Failure(params string[] errors)
        => new(false, null, errors);
}
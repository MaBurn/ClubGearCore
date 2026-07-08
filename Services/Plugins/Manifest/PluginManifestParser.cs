using System.Text.Json;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Plugins.Manifest;

public sealed class PluginManifestParser
{
    public PluginManifestValidationResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return PluginManifestValidationResult.Failure("Manifest content is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PluginManifestValidationResult.Failure("Manifest root must be a JSON object.");
            }

            var errors = new List<string>();

            var key = ReadRequiredString(root, "key", errors, "moduleId");
            var name = ReadRequiredString(root, "name", errors, "displayName");
            var versionRaw = ReadRequiredString(root, "version", errors, "pluginVersion");
            var requiredCoreVersion = ReadRequiredString(root, "requiredCoreVersion", errors, "requiredContractVersion");
            var entryPoint = ReadRequiredString(root, "entryPoint", errors, "entryPointType");
            var author = ReadOptionalString(root, "author") ?? "Unknown";
            var license = ReadOptionalString(root, "license") ?? "Unspecified";
            var category = ReadOptionalString(root, "category") ?? "General";
            var permissions = ReadStringArray(root, "permissions", errors);
            var extensionPoints = ReadStringArray(root, "extensionPoints", errors);
            ValidateExtensionPoints(extensionPoints, errors);

            var rawDependencies = ReadStringArray(root, "dependencies", errors);
            var dependencies = new List<PluginDependency>(rawDependencies.Count);
            foreach (var raw in rawDependencies)
            {
                if (PluginDependency.TryParse(raw, out var dep))
                {
                    dependencies.Add(dep!);
                }
                else
                {
                    errors.Add($"Ungueltige Abhaengigkeitsangabe: '{raw}'.");
                }
            }

            var version = ParseVersion(versionRaw, "version", errors);
            ValidateRequiredCoreVersion(requiredCoreVersion, errors);

            if (errors.Count > 0 || version is null || requiredCoreVersion is null)
            {
                return new PluginManifestValidationResult(false, null, errors);
            }

            var manifest = new PluginManifest(
                key!,
                name!,
                version,
                author,
                license,
                entryPoint!,
                requiredCoreVersion,
                permissions,
                extensionPoints) with { Category = category, Dependencies = dependencies };

            return PluginManifestValidationResult.Success(manifest);
        }
        catch (JsonException ex)
        {
            return PluginManifestValidationResult.Failure($"Manifest JSON is invalid: {ex.Message}");
        }
    }

    private static string? ReadRequiredString(
        JsonElement root,
        string propertyName,
        List<string> errors,
        string? legacyPropertyName = null)
    {
        if (TryReadString(root, propertyName, out var value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(legacyPropertyName)
            && TryReadString(root, legacyPropertyName, out value))
        {
            return value;
        }

        errors.Add($"Missing required property '{propertyName}'.");
        return null;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        return TryReadString(root, propertyName, out var value)
            ? value
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return Array.Empty<string>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Property '{propertyName}' must be an array of strings.");
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add($"Property '{propertyName}' must contain only strings.");
                return Array.Empty<string>();
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Property '{propertyName}' must not contain empty values.");
                return Array.Empty<string>();
            }

            values.Add(value);
        }

        return values;
    }

    private static Version? ParseVersion(string? value, string propertyName, List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        if (Version.TryParse(value, out var parsed))
        {
            return parsed;
        }

        errors.Add($"Property '{propertyName}' must be a valid version string.");
        return null;
    }

    private static void ValidateRequiredCoreVersion(string? value, List<string> errors)
    {
        if (value is null)
        {
            return;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith(">=", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }

        if (!Version.TryParse(normalized, out _))
        {
            errors.Add("Property 'requiredCoreVersion' must be an exact version or a minimum version in the form '>=x.y.z'.");
        }
    }

    private static void ValidateExtensionPoints(IReadOnlyList<string> values, List<string> errors)
    {
        foreach (var value in values)
        {
            if (!PluginExtensionPoints.IsKnown(value))
            {
                errors.Add($"Extension point '{value}' is not a recognized value.");
            }
        }
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
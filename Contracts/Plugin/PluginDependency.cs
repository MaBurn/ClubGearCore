namespace ClubGear.Plugin.Contracts;

public sealed record PluginDependency(string ModuleId, Version MinVersion)
{
    public static bool TryParse(string value, out PluginDependency? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var atIndex = value.IndexOf('@');
        if (atIndex < 0)
        {
            return false;
        }

        var moduleId = value[..atIndex];
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        var versionPart = value[(atIndex + 1)..].Trim();
        if (versionPart.StartsWith(">=", StringComparison.Ordinal))
        {
            versionPart = versionPart[2..].Trim();
        }

        if (!Version.TryParse(versionPart, out var minVersion))
        {
            return false;
        }

        result = new PluginDependency(moduleId, minVersion);
        return true;
    }

    public override string ToString() => $"{ModuleId}@>={MinVersion}";
}

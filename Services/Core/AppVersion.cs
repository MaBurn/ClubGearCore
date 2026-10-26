using System.Reflection;

namespace ClubGear.Services.Core;

/// <summary>
/// Exposes the ClubGear core version (CalVer, see ClubGear.csproj) for display.
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// The version as declared in ClubGear.csproj, for example <c>2026.7.0</c>.
    /// Any build metadata the SDK appends (<c>+&lt;commit-sha&gt;</c>) is stripped.
    /// </summary>
    public static string Current { get; } = Normalize(
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion,
        typeof(AppVersion).Assembly.GetName().Version);

    internal static string Normalize(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        return assemblyVersion?.ToString(3) ?? "unbekannt";
    }
}

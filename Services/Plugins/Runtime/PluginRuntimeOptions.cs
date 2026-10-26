namespace ClubGear.Services.Plugins.Runtime;

/// <summary>
/// Runtime knobs for the plugin host, bound from the <c>Plugins</c> configuration section.
/// </summary>
public sealed class PluginRuntimeOptions
{
    /// <summary>
    /// Preserves the previously hard-coded background-job stop budget, so an operator who changes
    /// nothing sees no timing change.
    /// </summary>
    public const double DefaultDrainTimeoutSeconds = 5d;

    /// <summary>Bound from <c>Plugins:Drain</c>.</summary>
    public PluginDrainOptions Drain { get; set; } = new();

    /// <summary>
    /// How long deactivation waits for in-flight plugin work before it unloads anyway.
    /// Non-positive or absent configuration falls back to <see cref="DefaultDrainTimeoutSeconds"/>.
    /// </summary>
    public TimeSpan DrainTimeout
        => TimeSpan.FromSeconds(Drain.TimeoutSeconds > 0 ? Drain.TimeoutSeconds : DefaultDrainTimeoutSeconds);
}

/// <summary>Bound from <c>Plugins:Drain</c>.</summary>
public sealed class PluginDrainOptions
{
    /// <summary>Drain budget in seconds. Defaults to 5.</summary>
    public double TimeoutSeconds { get; set; } = PluginRuntimeOptions.DefaultDrainTimeoutSeconds;
}

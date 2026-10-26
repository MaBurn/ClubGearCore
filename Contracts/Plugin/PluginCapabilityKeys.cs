namespace ClubGear.Plugin.Contracts;

/// <summary>
/// How many providers a capability may have active at once.
/// </summary>
public enum PluginCapabilityArity
{
    /// <summary>
    /// Exactly one provider serves the capability. Several plugins may register, but only the
    /// selected one is used — see <c>IPluginProviderRegistry.Resolve</c>.
    /// </summary>
    SingleActive,

    /// <summary>
    /// Every registered provider serves the capability, and the host calls all of them —
    /// see <c>IPluginProviderRegistry.ResolveAll</c>.
    /// </summary>
    FanOut
}

/// <summary>
/// CORE-13. The host-validated allowlist of capability keys a plugin may claim in a
/// <see cref="PluginServiceContribution"/>.
/// </summary>
/// <remarks>
/// Capability keys are not free text: an unknown key is rejected when the plugin loads, rather
/// than being silently ignored and leaving a plugin that appears installed but never runs. This
/// mirrors <see cref="PluginExtensionPoints"/>, which does the same for manifest extension points.
/// A capability key answers "what does this provider do"; an extension point answers "where does
/// this plugin show up". They are separate namespaces on purpose.
/// </remarks>
public static class PluginCapabilityKeys
{
    /// <summary>Outbound message transport — Matrix, Telegram, WhatsApp, SMS.</summary>
    public const string NotificationChannel = "notification.channel";

    /// <summary>Binary/blob storage that supersedes the core's local file storage — S3, Nextcloud.</summary>
    public const string StorageBlob = "storage.blob";

    /// <summary>Document storage and retrieval, distinct from raw blobs — Nextcloud.</summary>
    public const string DocumentStore = "document.store";

    /// <summary>An additional authentication scheme, such as the extracted API-key scheme.</summary>
    public const string AuthScheme = "auth.scheme";

    private static readonly Dictionary<string, PluginCapabilityArity> KnownValuesInternal = new(StringComparer.Ordinal)
    {
        [NotificationChannel] = PluginCapabilityArity.FanOut,
        [StorageBlob]         = PluginCapabilityArity.SingleActive,
        [DocumentStore]       = PluginCapabilityArity.SingleActive,
        [AuthScheme]          = PluginCapabilityArity.FanOut
    };

    public static IReadOnlySet<string> All => KnownValuesInternal.Keys.ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string value) => value is not null && KnownValuesInternal.ContainsKey(value);

    /// <summary>
    /// The arity declared for <paramref name="capabilityKey"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The key is not on the allowlist.</exception>
    public static PluginCapabilityArity GetArity(string capabilityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        return KnownValuesInternal.TryGetValue(capabilityKey, out var arity)
            ? arity
            : throw new ArgumentException($"Capability key '{capabilityKey}' is not a recognized value.", nameof(capabilityKey));
    }
}

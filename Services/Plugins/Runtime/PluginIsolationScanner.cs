using System.Reflection;
using ClubGear.Data;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>
/// CORE-16. The isolation gate a plugin type has to pass before the host lets it run.
/// Extracted from <see cref="PluginRuntimeAdapter"/> so that the same rule can be applied at
/// load time (<see cref="PluginLoader"/>, before any plugin code executes) and at invocation
/// time (<see cref="PluginRuntimeAdapter.EnsureIsolated"/>, for route and capability delegates).
///
/// Accessibility decision (CORE-16): the scanner stays <c>internal</c> — it is a host
/// implementation detail, not part of any published surface — and the architecture tests read
/// <see cref="ForbiddenNamespaces"/> through the <c>InternalsVisibleTo("ClubGear.ArchitectureTests")</c>
/// entry that <c>ClubGear.csproj</c> already declares. The source guardrail over <c>plugins/**</c>
/// therefore references this list instead of re-declaring it, so the runtime gate and the source
/// gate cannot drift apart.
/// </summary>
internal static class PluginIsolationScanner
{
    /// <summary>
    /// Core namespaces a plugin may never reference directly. A plugin talks to the host through
    /// <c>ClubGear.Plugin.Contracts</c> only.
    /// </summary>
    internal static readonly IReadOnlyList<string> ForbiddenNamespaces = new[]
    {
        "ClubGear.Services",
        "ClubGear.Data",
        "ClubGear.Controllers",
        "ClubGear.Models"
    };

    /// <summary>
    /// Reports the first forbidden reference reachable from <paramref name="pluginType"/>, or
    /// <c>false</c> when the type is clean.
    /// </summary>
    internal static bool TryFindViolation(Type pluginType, out string? violation)
    {
        ArgumentNullException.ThrowIfNull(pluginType);

        return TryFindForbiddenReference(pluginType, out violation);
    }

    private static bool TryFindForbiddenReference(Type pluginType, out string? violation)
    {
        foreach (var referencedType in EnumerateReferencedTypes(pluginType))
        {
            if (!IsForbiddenType(referencedType, out violation))
            {
                continue;
            }

            return true;
        }

        violation = null;
        return false;
    }

    private static IEnumerable<Type> EnumerateReferencedTypes(Type pluginType)
    {
        yield return pluginType;

        foreach (var field in pluginType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            yield return field.FieldType;
        }

        // CORE-16 / CORE-18. Constructor parameters are the surface host-side construction
        // introduces: a contribution the host builds as T(IPluginHostContext) could otherwise
        // declare a second, core-typed constructor parameter and slip past a field-only scan.
        // Deliberately nothing else is inspected here — base types, interfaces, properties,
        // method signatures, static fields, nested types and the assembly reference list stay
        // out of scope, because widening further is a separate decision with its own
        // false-positive risk.
        foreach (var constructor in pluginType.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static bool IsForbiddenType(Type candidateType, out string? violation)
    {
        foreach (var type in FlattenTypes(candidateType))
        {
            if (typeof(ApplicationDbContext).IsAssignableFrom(type))
            {
                violation = type.FullName;
                return true;
            }

            var namespaceName = type.Namespace;
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                continue;
            }

            if (ForbiddenNamespaces.Any(forbidden => namespaceName.StartsWith(forbidden, StringComparison.Ordinal)))
            {
                violation = type.FullName;
                return true;
            }
        }

        violation = null;
        return false;
    }

    private static IEnumerable<Type> FlattenTypes(Type type)
    {
        var pending = new Queue<Type>();
        pending.Enqueue(type);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var actual = Nullable.GetUnderlyingType(current) ?? current;

            if (actual.HasElementType && actual.GetElementType() is { } elementType)
            {
                pending.Enqueue(elementType);
            }

            foreach (var genericArgument in actual.GetGenericArguments())
            {
                pending.Enqueue(genericArgument);
            }

            yield return actual;
        }
    }
}

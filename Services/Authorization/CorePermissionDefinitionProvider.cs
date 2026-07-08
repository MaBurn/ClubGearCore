using System.Text.Json;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Authorization;

public class CorePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    private readonly IPluginStatusStore _pluginStatusStore;

    public CorePermissionDefinitionProvider(IPluginStatusStore pluginStatusStore)
    {
        _pluginStatusStore = pluginStatusStore;
    }

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(PermissionKeys.Wildcard, "Voller Zugriff auf alle Berechtigungen", "System");
        yield return new PermissionDefinition(PermissionKeys.AdminAccess, "Zugriff auf Administrationsfunktionen", "Administration");
        yield return new PermissionDefinition(PermissionKeys.MembersRead, "Mitglieder lesen", "Members");
        yield return new PermissionDefinition(PermissionKeys.MembersManage, "Mitglieder erstellen/bearbeiten/loeschen", "Members");
        yield return new PermissionDefinition(PermissionKeys.MembersTypesManage, "Mitgliedsarten und Metadatendefinitionen verwalten", "Members");
        yield return new PermissionDefinition(PermissionKeys.SelfServiceAccess, "Selfservice aufrufen", "SelfService");
        yield return new PermissionDefinition(PermissionKeys.SelfServiceProfileEdit, "Selfservice-Profil bearbeiten", "SelfService");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pluginDefinitions = new List<PermissionDefinition>();

        foreach (var record in _pluginStatusStore.List())
        {
            foreach (var permissionKey in DeserializePermissions(record.PermissionsJson))
            {
                if (PermissionKeys.IsCorePermission(permissionKey))
                    continue;
                if (!seen.Add(permissionKey))
                    continue;

                pluginDefinitions.Add(new PermissionDefinition(
                    permissionKey,
                    $"{record.DisplayName}: {permissionKey}",
                    record.Category));
            }
        }

        foreach (var definition in pluginDefinitions.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return definition;
        }
    }

    private static IReadOnlyList<string> DeserializePermissions(string? permissionsJson)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(permissionsJson) ?? Array.Empty<string>();
    }
}

namespace ClubGear.Services.Authorization;

public static class PermissionKeys
{
    private static readonly HashSet<string> CorePermissionsInternal = new(StringComparer.OrdinalIgnoreCase)
    {
        Wildcard,
        AdminAccess,
        MembersRead,
        MembersManage,
        MembersTypesManage,
        SelfServiceAccess,
        SelfServiceProfileEdit,
        ApiKeysManage
    };

    public const string Wildcard = "*";

    public const string AdminAccess = "admin.access";

    public const string MembersRead = "members.read";
    public const string MembersManage = "members.manage";
    public const string MembersTypesManage = "members.types.manage";

    public const string SelfServiceAccess = "selfservice.access";
    public const string SelfServiceProfileEdit = "selfservice.profile.edit";

    /// <summary>CORE-09. Issuing and revoking API keys is an administrative act of its own.</summary>
    public const string ApiKeysManage = "apikeys.manage";

    public const string PluginCategory = "Plugins";

    public static IReadOnlyCollection<string> CorePermissions => CorePermissionsInternal;

    public static bool IsCorePermission(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        return CorePermissionsInternal.Contains(permissionKey);
    }
}

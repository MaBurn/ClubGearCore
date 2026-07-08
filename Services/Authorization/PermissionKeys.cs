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
        SelfServiceProfileEdit
    };

    public const string Wildcard = "*";

    public const string AdminAccess = "admin.access";

    public const string MembersRead = "members.read";
    public const string MembersManage = "members.manage";
    public const string MembersTypesManage = "members.types.manage";

    public const string SelfServiceAccess = "selfservice.access";
    public const string SelfServiceProfileEdit = "selfservice.profile.edit";

    public const string PluginCategory = "Plugins";

    public static IReadOnlyCollection<string> CorePermissions => CorePermissionsInternal;

    public static bool IsCorePermission(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        return CorePermissionsInternal.Contains(permissionKey);
    }
}

namespace ClubGear.Models;

public class AppRolePermission
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
}

using ClubGear.Models;
using ClubGear.Models.Feedback;

namespace ClubGear.Models.Admin;

public sealed class RolePermissionsViewModel
{
    public List<RolePermissionRowViewModel> Roles { get; init; } = new();
    public List<PermissionGroupViewModel> Permissions { get; init; } = new();
    public ActionFeedbackViewModel? Feedback { get; init; }
}

public sealed class RolePermissionRowViewModel
{
    public string RoleName { get; init; } = string.Empty;
    public HashSet<string> GrantedKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PermissionGroupViewModel
{
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<AppPermission> Permissions { get; init; } = Array.Empty<AppPermission>();
}

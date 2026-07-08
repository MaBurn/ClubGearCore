using System.Security.Claims;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;

namespace ClubGear.Services.Core;

public class ExtensionPermissionFacade : IExtensionPermissionFacade
{
    private static readonly string[] MemberWriteDerivedSources =
    [
        PermissionKeys.MembersManage,
        PermissionKeys.SelfServiceProfileEdit
    ];

    private readonly IPermissionService _permissionService;
    private readonly ILogger<ExtensionPermissionFacade> _logger;

    public ExtensionPermissionFacade(IPermissionService permissionService, ILogger<ExtensionPermissionFacade> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionKey,
        IReadOnlyCollection<string> declaredPermissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        ArgumentNullException.ThrowIfNull(declaredPermissions);

        if (!declaredPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Plugin-Berechtigung {PermissionKey} wurde verweigert, weil sie nicht im Manifest deklariert ist.",
                permissionKey);
            return false;
        }

        try
        {
            if (await _permissionService.HasPermissionAsync(user, permissionKey, cancellationToken))
            {
                return true;
            }

            if (permissionKey.EndsWith(".member.write", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var sourceKey in MemberWriteDerivedSources)
                {
                    if (await _permissionService.HasPermissionAsync(user, sourceKey, cancellationToken))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler in der Erweiterungs-Berechtigungspruefung fuer {PermissionKey}", permissionKey);
            throw new UserFriendlyException("Die Berechtigungspruefung der Erweiterung konnte nicht ausgefuehrt werden.", ex);
        }
    }
}

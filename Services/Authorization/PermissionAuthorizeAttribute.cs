using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClubGear.Services.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    public PermissionAuthorizeAttribute(string permissionKey)
        : base(typeof(PermissionAuthorizeFilter))
    {
        Arguments = new object[] { permissionKey };
    }
}

public sealed class PermissionAuthorizeFilter : IAsyncAuthorizationFilter
{
    private readonly string _permissionKey;
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizeFilter(string permissionKey, IPermissionService permissionService)
    {
        _permissionKey = permissionKey;
        _permissionService = permissionService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var allowed = await _permissionService.HasPermissionAsync(context.HttpContext.User, _permissionKey);
        if (!allowed)
        {
            context.Result = new ForbidResult();
        }
    }
}

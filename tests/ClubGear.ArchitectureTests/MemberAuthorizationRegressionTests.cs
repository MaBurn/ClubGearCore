using System.Reflection;
using ClubGear.Controllers;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberAuthorizationRegressionTests
{
    [Fact]
    public void MembersController_KeepsAuthenticatedReadBaseline()
    {
        var authorizeAttribute = Assert.Single(typeof(MembersController).GetCustomAttributes<AuthorizeAttribute>());
        var permissionAttribute = Assert.Single(typeof(MembersController).GetCustomAttributes<PermissionAuthorizeAttribute>());

        Assert.Null(authorizeAttribute.Policy);
        Assert.Equal(PermissionKeys.MembersRead, GetPermissionKey(permissionAttribute));
    }

    [Theory]
    [InlineData(nameof(MembersController.Create), new[] { PermissionKeys.MembersManage, PermissionKeys.MembersManage })]
    [InlineData(nameof(MembersController.Edit), new[] { PermissionKeys.MembersManage, PermissionKeys.MembersManage })]
    [InlineData(nameof(MembersController.Delete), new[] { PermissionKeys.MembersManage })]
    [InlineData(nameof(MembersController.Import), new[] { PermissionKeys.MembersManage, PermissionKeys.MembersManage })]
    [InlineData(nameof(MembersController.BulkDeleteTerminatedMembers), new[] { PermissionKeys.MembersManage, PermissionKeys.MembersManage })]
    public void MutatingOrPrivilegedEndpoints_KeepMembersManageRequirement(string methodName, string[] expectedPermissions)
    {
        var methods = typeof(MembersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .OrderBy(method => method.MetadataToken)
            .ToArray();

        Assert.Equal(expectedPermissions.Length, methods.Length);

        for (var index = 0; index < methods.Length; index++)
        {
            var permissionAttribute = Assert.Single(methods[index].GetCustomAttributes<PermissionAuthorizeAttribute>());
            Assert.Equal(expectedPermissions[index], GetPermissionKey(permissionAttribute));
        }
    }

    [Theory]
    [InlineData(nameof(MembersController.Index))]
    [InlineData(nameof(MembersController.Details))]
    public void ReadEndpoints_DoNotEscalateToMembersManage(string methodName)
    {
        var methods = typeof(MembersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            var permissionAttributes = method.GetCustomAttributes<PermissionAuthorizeAttribute>().ToArray();
            Assert.DoesNotContain(permissionAttributes, attribute =>
                string.Equals(GetPermissionKey(attribute), PermissionKeys.MembersManage, StringComparison.Ordinal));
        }
    }

    private static string GetPermissionKey(PermissionAuthorizeAttribute attribute)
    {
        var arguments = Assert.IsAssignableFrom<object[]>(attribute.Arguments ?? Array.Empty<object>());
        return Assert.IsType<string>(Assert.Single(arguments));
    }
}
using System.Reflection;
using ClubGear.Controllers;
using ClubGear.Controllers.Api;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class ControllerThinnessTests
{
    [Theory]
    [MemberData(nameof(ExpectedControllerDependencies))]
    public void FeatureControllers_DependenOnlyOnFeatureServices(Type controllerType, Type[] expectedDependencies)
    {
        var ctor = controllerType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        var parameterTypes = ctor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(expectedDependencies, parameterTypes);
    }

    [Theory]
    [MemberData(nameof(FeatureControllerTypes))]
    public void FeatureControllers_DoNotDependOnDataLayer(Type controllerType)
    {
        var fieldTypes = controllerType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(f => f.FieldType)
            .ToArray();

        Assert.DoesNotContain(fieldTypes, t => t.Namespace is not null && t.Namespace.StartsWith("ClubGear.Data", StringComparison.Ordinal));
    }

    [Fact]
    public void MembersController_DoesNotDependOnAuthorizationServices()
    {
        var ctor = typeof(MembersController)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        var dependencyTypes = ctor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.DoesNotContain(dependencyTypes, type => type == typeof(IPermissionService));
        Assert.DoesNotContain(dependencyTypes, type => type.Namespace is not null && type.Namespace.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> FeatureControllerTypes()
    {
        yield return new object[] { typeof(MembersController) };
        yield return new object[] { typeof(AccountController) };
        yield return new object[] { typeof(PluginAdminController) };
        yield return new object[] { typeof(SelfServiceController) };
        yield return new object[] { typeof(MemberApiController) };
        yield return new object[] { typeof(AccountApiController) };
        yield return new object[] { typeof(SelfServiceApiController) };
        yield return new object[] { typeof(PluginsController) };
    }

    public static IEnumerable<object[]> ExpectedControllerDependencies()
    {
        yield return new object[] { typeof(MembersController), new[] { typeof(IMemberFeatureService), typeof(IMemberPluginSlotService), typeof(IMembershipTypeService) } };
        yield return new object[] { typeof(AccountController), new[] { typeof(IAccountFeatureService), typeof(IExternalLoginService) } };
        yield return new object[] { typeof(PluginAdminController), new[] { typeof(IPluginInstallerService), typeof(IPluginLifecycleService), typeof(IPluginAdminQueryService), typeof(IPluginUninstallService) } };
        yield return new object[] { typeof(SelfServiceController), new[] { typeof(ISelfServiceFeatureService), typeof(IMemberPluginSlotService), typeof(ISelfServiceSectionService) } };
        yield return new object[] { typeof(MemberApiController), new[] { typeof(IMemberFeatureService), typeof(IMemberPluginSlotService) } };
        yield return new object[] { typeof(AccountApiController), new[] { typeof(IAccountFeatureService) } };
        yield return new object[] { typeof(SelfServiceApiController), new[] { typeof(ISelfServiceFeatureService), typeof(IMemberPluginSlotService), typeof(ISelfServiceSectionService) } };
        yield return new object[] { typeof(PluginsController), new[] { typeof(IPluginInstallerService), typeof(IPluginLifecycleService), typeof(IPluginAdminQueryService) } };
    }
}

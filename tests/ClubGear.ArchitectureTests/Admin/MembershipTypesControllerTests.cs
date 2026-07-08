using System.Security.Claims;
using ClubGear.Controllers.Admin;
using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Models.Feedback;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests.Admin;

/// <summary>
/// Slice 2, checkbox 3 DoD: unauthorized users (missing members.types.manage) receive
/// a 403/redirect; authorized users can POST create/update/delete actions and see
/// redirect-after-post feedback.
/// </summary>
public sealed class MembershipTypesControllerTests
{
    // ── Authorization: the shared PermissionAuthorizeFilter (attached to the
    //    controller via [PermissionAuthorize(PermissionKeys.MembersTypesManage)])
    //    is what actually enforces the 403/challenge behavior for this route. ──────

    [Fact]
    public async Task PermissionAuthorizeFilter_UnauthenticatedUser_ChallengesForMembersTypesManage()
    {
        var filter = new PermissionAuthorizeFilter(PermissionKeys.MembersTypesManage, new FakePermissionService(false));
        var context = BuildAuthorizationContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<ChallengeResult>(context.Result);
    }

    [Fact]
    public async Task PermissionAuthorizeFilter_AuthenticatedWithoutPermission_ReturnsForbid()
    {
        var filter = new PermissionAuthorizeFilter(PermissionKeys.MembersTypesManage, new FakePermissionService(false));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester") }, "TestAuth"));
        var context = BuildAuthorizationContext(principal);

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task PermissionAuthorizeFilter_AuthenticatedWithPermission_AllowsRequest()
    {
        var filter = new PermissionAuthorizeFilter(PermissionKeys.MembersTypesManage, new FakePermissionService(true));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester") }, "TestAuth"));
        var context = BuildAuthorizationContext(principal);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    // ── Controller action behavior (authorized caller, direct instantiation) ──────

    [Fact]
    public async Task Index_ReturnsViewModel_WithTypesAndFeedback()
    {
        var service = new FakeMembershipTypeService
        {
            Types = new List<MembershipType> { new() { Id = 1, Key = "Standard", Name = "Standard" } }
        };
        using var sut = BuildController(service);

        var result = await sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MembershipTypesViewModel>(viewResult.Model);
        Assert.Single(model.Types);
        Assert.Equal("Standard", model.Types[0].Name);
    }

    [Fact]
    public async Task CreateType_HappyPath_SetsSuccessFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            CreateResult = MembershipTypeOperationResult.Ok(new MembershipType { Id = 5, Key = "Foerderer", Name = "Foerderer" })
        };
        using var sut = BuildController(service);

        var result = await sut.CreateType(new CreateMembershipTypeInputModel { Key = "Foerderer", Name = "Foerderer" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Contains("Foerderer", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
    }

    [Fact]
    public async Task CreateType_PassesAllowsSubMembersAndLabel_ToService()
    {
        var service = new FakeMembershipTypeService
        {
            CreateResult = MembershipTypeOperationResult.Ok(new MembershipType { Id = 5, Key = "Firma", Name = "Firma" })
        };
        using var sut = BuildController(service);

        await sut.CreateType(new CreateMembershipTypeInputModel
        {
            Key = "Firma",
            Name = "Firma",
            AllowsSubMembers = true,
            SubMemberLabel = "Mitarbeiter"
        });

        Assert.NotNull(service.LastCreatedType);
        Assert.True(service.LastCreatedType!.AllowsSubMembers);
        Assert.Equal("Mitarbeiter", service.LastCreatedType.SubMemberLabel);
    }

    [Fact]
    public async Task UpdateType_PassesAllowsSubMembersAndLabel_ToService()
    {
        var service = new FakeMembershipTypeService
        {
            UpdateResult = MembershipTypeOperationResult.Ok(new MembershipType { Id = 3, Key = "Familie", Name = "Familie" })
        };
        using var sut = BuildController(service);

        await sut.UpdateType(new UpdateMembershipTypeInputModel
        {
            Id = 3,
            Name = "Familie",
            AllowsSubMembers = true,
            SubMemberLabel = "Familienmitglied"
        });

        Assert.NotNull(service.LastUpdatedType);
        Assert.True(service.LastUpdatedType!.AllowsSubMembers);
        Assert.Equal("Familienmitglied", service.LastUpdatedType.SubMemberLabel);
    }

    [Fact]
    public async Task CreateType_ServiceReportsDuplicate_SetsErrorFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            CreateResult = MembershipTypeOperationResult.Duplicate("Schluessel existiert bereits.")
        };
        using var sut = BuildController(service);

        var result = await sut.CreateType(new CreateMembershipTypeInputModel { Key = "Standard", Name = "Standard" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    [Fact]
    public async Task UpdateType_HappyPath_SetsSuccessFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            UpdateResult = MembershipTypeOperationResult.Ok(new MembershipType { Id = 3, Key = "Gast", Name = "Gastmitglied" })
        };
        using var sut = BuildController(service);

        var result = await sut.UpdateType(new UpdateMembershipTypeInputModel { Id = 3, Name = "Gastmitglied" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal(3, service.LastUpdateTypeId);
    }

    [Fact]
    public async Task DeleteType_Blocked_SetsErrorFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            DeleteResult = MembershipTypeOperationResult.BlockedResult("Wird noch verwendet.")
        };
        using var sut = BuildController(service);

        var result = await sut.DeleteType(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Contains("Wird noch verwendet.", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
    }

    [Fact]
    public async Task DeleteType_HappyPath_SetsWarningFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            DeleteResult = MembershipTypeOperationResult.Ok(new MembershipType { Id = 7, Key = "Temp", Name = "Temp" })
        };
        using var sut = BuildController(service);

        var result = await sut.DeleteType(7);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("warning", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal(7, service.LastDeleteTypeId);
    }

    [Fact]
    public async Task AddField_HappyPath_SetsSuccessFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            AddFieldResult = MembershipTypeFieldOperationResult.Ok(new MembershipTypeField { Id = 11, Key = "club_name", Label = "Vereinsname" })
        };
        using var sut = BuildController(service);

        var result = await sut.AddField(new CreateMembershipTypeFieldInputModel { MembershipTypeId = 2, Key = "club_name", Label = "Vereinsname" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal(2, service.LastAddFieldTypeId);
    }

    [Fact]
    public async Task RemoveField_Blocked_SetsErrorFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            RemoveFieldResult = MembershipTypeFieldOperationResult.BlockedResult("Systemfeld wird noch verwendet.")
        };
        using var sut = BuildController(service);

        var result = await sut.RemoveField(11);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    [Fact]
    public async Task RemoveField_HappyPath_SetsWarningFeedback_AndRedirects()
    {
        var service = new FakeMembershipTypeService
        {
            RemoveFieldResult = MembershipTypeFieldOperationResult.Ok(new MembershipTypeField { Id = 12, Key = "club_magazine", Label = "Vereinszeitschrift" })
        };
        using var sut = BuildController(service);

        var result = await sut.RemoveField(12);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembershipTypesController.Index), redirect.ActionName);
        Assert.Equal("warning", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal(12, service.LastRemoveFieldId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuthorizationFilterContext BuildAuthorizationContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        };
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static MembershipTypesController BuildController(IMembershipTypeService service)
    {
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return new MembershipTypesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class FakePermissionService : IPermissionService
    {
        private readonly bool _hasPermission;

        public FakePermissionService(bool hasPermission)
        {
            _hasPermission = hasPermission;
        }

        public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(user.Identity?.IsAuthenticated == true && _hasPermission);
    }

    private sealed class FakeMembershipTypeService : IMembershipTypeService
    {
        public List<MembershipType> Types { get; set; } = new();
        public MembershipTypeOperationResult CreateResult { get; set; } = MembershipTypeOperationResult.NotFoundResult();
        public MembershipTypeOperationResult UpdateResult { get; set; } = MembershipTypeOperationResult.NotFoundResult();
        public MembershipTypeOperationResult DeleteResult { get; set; } = MembershipTypeOperationResult.NotFoundResult();
        public MembershipTypeFieldOperationResult AddFieldResult { get; set; } = MembershipTypeFieldOperationResult.NotFoundResult();
        public MembershipTypeFieldOperationResult UpdateFieldResult { get; set; } = MembershipTypeFieldOperationResult.NotFoundResult();
        public MembershipTypeFieldOperationResult RemoveFieldResult { get; set; } = MembershipTypeFieldOperationResult.NotFoundResult();

        public int? LastUpdateTypeId { get; private set; }
        public int? LastDeleteTypeId { get; private set; }
        public int? LastAddFieldTypeId { get; private set; }
        public int? LastRemoveFieldId { get; private set; }
        public MembershipType? LastCreatedType { get; private set; }
        public MembershipType? LastUpdatedType { get; private set; }

        public Task<IReadOnlyList<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MembershipType>>(Types);

        public Task<MembershipType?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Types.FirstOrDefault(t => t.Id == id));

        public Task<MembershipTypeOperationResult> CreateTypeAsync(MembershipType type, CancellationToken cancellationToken = default)
        {
            LastCreatedType = type;
            return Task.FromResult(CreateResult);
        }

        public Task<MembershipTypeOperationResult> UpdateTypeAsync(int id, MembershipType updated, CancellationToken cancellationToken = default)
        {
            LastUpdateTypeId = id;
            LastUpdatedType = updated;
            return Task.FromResult(UpdateResult);
        }

        public Task<MembershipTypeOperationResult> DeleteTypeAsync(int id, CancellationToken cancellationToken = default)
        {
            LastDeleteTypeId = id;
            return Task.FromResult(DeleteResult);
        }

        public Task<MembershipTypeFieldOperationResult> AddFieldAsync(int membershipTypeId, MembershipTypeField field, CancellationToken cancellationToken = default)
        {
            LastAddFieldTypeId = membershipTypeId;
            return Task.FromResult(AddFieldResult);
        }

        public Task<MembershipTypeFieldOperationResult> UpdateFieldAsync(int fieldId, MembershipTypeField updated, CancellationToken cancellationToken = default)
            => Task.FromResult(UpdateFieldResult);

        public Task<MembershipTypeFieldOperationResult> RemoveFieldAsync(int fieldId, CancellationToken cancellationToken = default)
        {
            LastRemoveFieldId = fieldId;
            return Task.FromResult(RemoveFieldResult);
        }
    }
}

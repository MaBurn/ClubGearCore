using ClubGear.Controllers.Admin;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Models.Feedback;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests.Admin;

public sealed class RolePermissionsControllerTests
{
    // ── 4.4a — Correct number of roles in view model ──────────────────────────

    [Fact]
    public async Task Index_ReturnsCorrectNumberOfRoles()
    {
        await using var fixture = await CreateFixtureAsync();

        // Seed two roles
        fixture.DbContext.Roles.AddRange(
            new IdentityRole("Admin") { NormalizedName = "ADMIN" },
            new IdentityRole("Member") { NormalizedName = "MEMBER" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RolePermissionsViewModel>(viewResult.Model);
        Assert.Equal(2, model.Roles.Count);
        Assert.Contains(model.Roles, r => r.RoleName == "Admin");
        Assert.Contains(model.Roles, r => r.RoleName == "Member");
    }

    // ── 4.4b — GrantedKeys correctly populated per role ───────────────────────

    [Fact]
    public async Task Index_PopulatesGrantedKeys_PerRole()
    {
        await using var fixture = await CreateFixtureAsync();

        // Seed roles
        fixture.DbContext.Roles.AddRange(
            new IdentityRole("Admin") { NormalizedName = "ADMIN" },
            new IdentityRole("Member") { NormalizedName = "MEMBER" });

        // Seed permissions
        fixture.DbContext.Permissions.AddRange(
            new AppPermission { Key = "admin.access", Description = "Admin access", Category = "Administration" },
            new AppPermission { Key = "members.read", Description = "Read members", Category = "Members" });

        // Admin role has both permissions; Member role has only members.read
        fixture.DbContext.RolePermissions.AddRange(
            new AppRolePermission { RoleName = "Admin", PermissionKey = "admin.access" },
            new AppRolePermission { RoleName = "Admin", PermissionKey = "members.read" },
            new AppRolePermission { RoleName = "Member", PermissionKey = "members.read" });

        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RolePermissionsViewModel>(viewResult.Model);

        var adminRow = Assert.Single(model.Roles, r => r.RoleName == "Admin");
        Assert.Contains("admin.access", adminRow.GrantedKeys);
        Assert.Contains("members.read", adminRow.GrantedKeys);

        var memberRow = Assert.Single(model.Roles, r => r.RoleName == "Member");
        Assert.DoesNotContain("admin.access", memberRow.GrantedKeys);
        Assert.Contains("members.read", memberRow.GrantedKeys);
    }

    // ── 4.4c — Permissions grouped by category ────────────────────────────────

    [Fact]
    public async Task Index_GroupsPermissions_ByCategory()
    {
        await using var fixture = await CreateFixtureAsync();

        // No roles needed; just seed permissions in two categories
        fixture.DbContext.Permissions.AddRange(
            new AppPermission { Key = "admin.access", Description = "Admin access", Category = "Administration" },
            new AppPermission { Key = "admin.config", Description = "Admin config", Category = "Administration" },
            new AppPermission { Key = "members.read", Description = "Read members", Category = "Members" });

        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RolePermissionsViewModel>(viewResult.Model);

        Assert.Equal(2, model.Permissions.Count);

        var adminGroup = Assert.Single(model.Permissions, g => g.Category == "Administration");
        Assert.Equal(2, adminGroup.Permissions.Count);

        var memberGroup = Assert.Single(model.Permissions, g => g.Category == "Members");
        Assert.Single(memberGroup.Permissions);
        Assert.Equal("members.read", memberGroup.Permissions[0].Key);
    }

    // ── 4.4d — Roles ordered by name ─────────────────────────────────────────

    [Fact]
    public async Task Index_OrdersRoles_ByName()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.AddRange(
            new IdentityRole("Zebra") { NormalizedName = "ZEBRA" },
            new IdentityRole("Alpha") { NormalizedName = "ALPHA" },
            new IdentityRole("Member") { NormalizedName = "MEMBER" });

        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RolePermissionsViewModel>(viewResult.Model);

        Assert.Equal(3, model.Roles.Count);
        Assert.Equal("Alpha", model.Roles[0].RoleName);
        Assert.Equal("Member", model.Roles[1].RoleName);
        Assert.Equal("Zebra", model.Roles[2].RoleName);
    }

    // ── 5.4 — Grant happy-path ────────────────────────────────────────────────

    [Fact]
    public async Task Grant_HappyPath_AddsRolePermissionRow_AndSetsFeedback()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("Editor") { NormalizedName = "EDITOR" });
        fixture.DbContext.Permissions.Add(new AppPermission { Key = "members.read", Description = "Read", Category = "Members" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Grant("Editor", "members.read");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        var row = await fixture.DbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleName == "Editor" && rp.PermissionKey == "members.read");
        Assert.NotNull(row);

        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Contains("members.read", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
        Assert.Contains("Editor", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
    }

    // ── 5.4 — Grant: unknown-key guard ───────────────────────────────────────

    [Fact]
    public async Task Grant_UnknownPermissionKey_SetsErrorFeedback_AndRedirects()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("Editor") { NormalizedName = "EDITOR" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Grant("Editor", "nonexistent.key");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        var rowCount = await fixture.DbContext.RolePermissions.CountAsync();
        Assert.Equal(0, rowCount);

        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    // ── 5.4 — Grant: duplicate guard ─────────────────────────────────────────

    [Fact]
    public async Task Grant_DuplicateGrant_SetsErrorFeedback_AndDoesNotAddRow()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("Editor") { NormalizedName = "EDITOR" });
        fixture.DbContext.Permissions.Add(new AppPermission { Key = "members.read", Description = "Read", Category = "Members" });
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "Editor", PermissionKey = "members.read" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Grant("Editor", "members.read");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        var rowCount = await fixture.DbContext.RolePermissions.CountAsync();
        Assert.Equal(1, rowCount); // still only one row

        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    // ── 5.4 — Revoke happy-path ───────────────────────────────────────────────

    [Fact]
    public async Task Revoke_HappyPath_RemovesRolePermissionRow_AndSetsFeedback()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("Editor") { NormalizedName = "EDITOR" });
        fixture.DbContext.Permissions.Add(new AppPermission { Key = "members.read", Description = "Read", Category = "Members" });
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "Editor", PermissionKey = "members.read" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Revoke("Editor", "members.read");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        var rowCount = await fixture.DbContext.RolePermissions.CountAsync();
        Assert.Equal(0, rowCount);

        Assert.Equal("warning", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Contains("members.read", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
        Assert.Contains("Editor", (string)sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]!);
    }

    // ── 5.4 — Revoke: absent row redirects with warning ───────────────────────

    [Fact]
    public async Task Revoke_AbsentRow_SetsWarningFeedback_AndRedirects()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("Editor") { NormalizedName = "EDITOR" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Revoke("Editor", "members.read");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        Assert.Equal("warning", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    // ── 5.4 — Revoke: admin-wildcard guard ───────────────────────────────────

    [Fact]
    public async Task Revoke_AdminWildcard_SetsErrorFeedback_AndDoesNotRemoveRow()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.DbContext.Roles.Add(new IdentityRole("ClubGear.Admin") { NormalizedName = "CLUBGEAR.ADMIN" });
        fixture.DbContext.Permissions.Add(new AppPermission { Key = "admin.access", Description = "Admin access", Category = "Administration" });
        fixture.DbContext.RolePermissions.Add(new AppRolePermission { RoleName = "ClubGear.Admin", PermissionKey = "admin.access" });
        await fixture.DbContext.SaveChangesAsync();

        using var sut = BuildController(fixture.DbContext);
        var result = await sut.Revoke("ClubGear.Admin", "admin.access");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(RolePermissionsController.Index), redirect.ActionName);

        var rowCount = await fixture.DbContext.RolePermissions.CountAsync();
        Assert.Equal(1, rowCount); // row is still there

        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RolePermissionsController BuildController(ApplicationDbContext dbContext)
    {
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        var controller = new RolePermissionsController(dbContext)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
        return controller;
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return new Fixture(connection, dbContext);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(SqliteConnection connection, ApplicationDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}

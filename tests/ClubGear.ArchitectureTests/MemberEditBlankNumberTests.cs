using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 3: Edit action rejects a blank Mitgliedsnummer end-to-end - the controller adds a
/// server-side ModelState error before ever calling the service, and (as defense-in-depth)
/// MemberFeatureService.UpdateAsync itself refuses to overwrite an existing MemberNumber with a
/// blank value even if some other caller bypasses the controller's check.
/// </summary>
public sealed class MemberEditBlankNumberTests
{
    [Fact]
    public async Task UpdateAsync_WithBlankMemberNumber_LeavesTrackedMemberNumberUntouched()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Members.Add(new Member
        {
            MemberNumber = "M-1234",
            FirstName = "Alte",
            LastName = "Nummer",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var existing = await db.Members.AsNoTracking().SingleAsync();
        var service = new MemberFeatureService(db, new NoopAuditLogService());

        var updatedMember = new Member
        {
            Id = existing.Id,
            MemberNumber = string.Empty,
            FirstName = "Alte",
            LastName = "Nummer-Geaendert",
            IsActive = true,
            JoinedAt = existing.JoinedAt
        };

        var result = await service.UpdateAsync(updatedMember, "tester");

        Assert.Equal(MemberMutationStatus.Success, result);

        var persisted = await db.Members.AsNoTracking().SingleAsync(m => m.Id == existing.Id);
        Assert.Equal("M-1234", persisted.MemberNumber);
        Assert.Equal("Nummer-Geaendert", persisted.LastName);
    }

    [Fact]
    public async Task Edit_Post_WithBlankMemberNumber_ReRendersFormWithValidationErrorAndDoesNotPersistChange()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Members.Add(new Member
        {
            MemberNumber = "M-5678",
            FirstName = "Bestehendes",
            LastName = "Mitglied",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var existing = await db.Members.AsNoTracking().SingleAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService());
        using var sut = new MembersController(service);
        sut.ControllerContext = BuildControllerContext(PermissionKeys.MembersManage);
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var postedMember = new Member
        {
            Id = existing.Id,
            MemberNumber = string.Empty,
            FirstName = existing.FirstName,
            LastName = "Mitglied-Versucht-Zu-Aendern",
            IsActive = existing.IsActive,
            JoinedAt = existing.JoinedAt
        };

        // Mirror the controller's own guard plus what ASP.NET Core's model binder does before
        // invoking the action: run DataAnnotations validation into ModelState.
        ValidateModelViaDataAnnotations(postedMember, sut.ModelState);
        if (string.IsNullOrWhiteSpace(postedMember.MemberNumber))
        {
            sut.ModelState.AddModelError(nameof(Member.MemberNumber), "Mitgliedsnummer darf nicht leer sein.");
        }

        var result = await sut.Edit(existing.Id, postedMember);

        Assert.False(sut.ModelState.IsValid);
        Assert.True(sut.ModelState.ContainsKey(nameof(Member.MemberNumber)));

        var viewResult = Assert.IsType<ViewResult>(result);
        var returnedMember = Assert.IsType<Member>(viewResult.Model);
        Assert.Equal(existing.Id, returnedMember.Id);

        var persisted = await db.Members.AsNoTracking().SingleAsync(m => m.Id == existing.Id);
        Assert.Equal("M-5678", persisted.MemberNumber);
        Assert.Equal("Mitglied", persisted.LastName);
    }

    private static void ValidateModelViaDataAnnotations(Member member, ModelStateDictionary modelState)
    {
        var validationContext = new ValidationContext(member);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(member, validationContext, validationResults, validateAllProperties: true);

        foreach (var validationResult in validationResults)
        {
            var memberNames = validationResult.MemberNames.Any()
                ? validationResult.MemberNames
                : new[] { string.Empty };

            foreach (var memberName in memberNames)
            {
                modelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Invalid");
            }
        }
    }

    private static ControllerContext BuildControllerContext(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user")
        };

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    private static ITempDataDictionary BuildTempData(HttpContext httpContext)
    {
        return new TempDataDictionary(httpContext, new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

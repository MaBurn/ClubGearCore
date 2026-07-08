using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
/// Slice 1: Create action allows a blank Mitgliedsnummer end-to-end - the model no longer
/// requires it, the controller no longer strips a ModelState error for it, and the service
/// fills it in from the configured prefix/suffix/padding/next-number before persisting.
/// </summary>
public sealed class MemberCreateBlankNumberTests
{
    [Fact]
    public void MemberNumber_IsNotDecoratedWithRequiredAttribute()
    {
        var property = typeof(Member).GetProperty(nameof(Member.MemberNumber));
        Assert.NotNull(property);

        var requiredAttribute = property!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false);
        Assert.Empty(requiredAttribute);

        var stringLengthAttribute = Assert.Single(property.GetCustomAttributes(typeof(StringLengthAttribute), inherit: false));
        Assert.Equal(30, ((StringLengthAttribute)stringLengthAttribute).MaximumLength);
    }

    /// <summary>
    /// Regression guard for a non-obvious ASP.NET Core MVC behavior: with <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>
    /// and <c>SuppressImplicitRequiredAttributeForNonNullableReferenceTypes</c> left at its default (false),
    /// MVC synthesizes an implicit "required" validation for any non-nullable reference-type property -
    /// independent of whether <see cref="RequiredAttribute"/> is present on the source. Removing
    /// <see cref="RequiredAttribute"/> alone therefore does NOT make a blank Mitgliedsnummer submittable;
    /// the property must also be declared nullable (<c>string?</c>) so MVC's DefaultModelMetadataProvider
    /// does not mark it as implicitly required (which would otherwise still emit data-val-required and
    /// block both client-side and server-side submission of a blank value).
    /// </summary>
    [Fact]
    public void MemberNumber_IsDeclaredNullable_SoMvcDoesNotImplicitlyTreatItAsRequired()
    {
        var property = typeof(Member).GetProperty(nameof(Member.MemberNumber))!;

        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);

        Assert.Equal(NullabilityState.Nullable, nullabilityInfo.WriteState);
        Assert.Equal(NullabilityState.Nullable, nullabilityInfo.ReadState);
    }

    [Fact]
    public async Task Create_WithBlankMemberNumber_PassesModelValidationAndPersistsConfigDerivedNumber()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.SystemConfigEntries.AddRange(
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberPrefix", Value = "MB-", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberSuffix", Value = "-A", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberNextNumber", Value = "42", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberPadding", Value = "5", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new MemberFeatureService(db, new NoopAuditLogService());
        using var sut = new MembersController(service);
        sut.ControllerContext = BuildControllerContext(PermissionKeys.MembersManage);
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var member = new Member
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            MemberNumber = string.Empty
        };

        // Mirror what ASP.NET Core's model binder does before invoking the action: run
        // DataAnnotations validation and push any failures into ModelState.
        ValidateModelViaDataAnnotations(member, sut.ModelState);

        Assert.True(sut.ModelState.IsValid);
        Assert.False(sut.ModelState.ContainsKey(nameof(Member.MemberNumber)));

        var result = await sut.Create(member);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembersController.Index), redirect.ActionName);

        var persisted = await db.Members.AsNoTracking().SingleAsync();
        Assert.Equal("MB-00042-A", persisted.MemberNumber);
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

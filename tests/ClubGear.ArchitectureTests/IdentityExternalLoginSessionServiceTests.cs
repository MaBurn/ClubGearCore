using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class IdentityExternalLoginSessionServiceTests
{
    [Fact]
    public async Task SignInAsync_WritesAuthenticApplicationCookie_AndFiltersReservedClaims()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IExternalLoginSessionService, IdentityExternalLoginSessionService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "oidc-user@example.test",
            Email = "oidc-user@example.test",
        };
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var sut = scope.ServiceProvider.GetRequiredService<IExternalLoginSessionService>();

        await sut.ClearExternalAsync();
        var signedIn = await sut.SignInAsync(
            user.Id,
            [
                new PluginClaimEntry("custom.profile-role", "treasurer"),
                new PluginClaimEntry("permission", "*"),
                new PluginClaimEntry("clubgear.system-role", "master-admin"),
                new PluginClaimEntry(ClaimTypes.Role, "Admin"),
                new PluginClaimEntry(ClaimTypes.NameIdentifier, "attacker-controlled-id")
            ]);

        Assert.True(signedIn);
        var setCookies = httpContext.Response.Headers.SetCookie.ToArray();
        var applicationCookie = Assert.Single(setCookies.Where(value =>
            value?.Contains(".AspNetCore.Identity.Application=", StringComparison.Ordinal) == true));
        Assert.Contains(setCookies, value =>
            value?.Contains("Identity.External=", StringComparison.Ordinal) == true
            && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        await using var followupScope = provider.CreateAsyncScope();
        var followupContext = new DefaultHttpContext
        {
            RequestServices = followupScope.ServiceProvider,
        };
        followupContext.Request.Headers.Cookie = applicationCookie!.Split(';', 2)[0];
        followupScope.ServiceProvider
            .GetRequiredService<IHttpContextAccessor>()
            .HttpContext = followupContext;

        var authentication = await followupContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);

        Assert.True(authentication.Succeeded);
        Assert.NotNull(authentication.Principal);
        Assert.Equal(
            user.Id,
            authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(authentication.Principal.Claims, claim =>
            claim.Type == "custom.profile-role" && claim.Value == "treasurer");
        Assert.DoesNotContain(authentication.Principal.Claims, claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(authentication.Principal.Claims, claim =>
            string.Equals(claim.Type, "clubgear.system-role", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(authentication.Principal.FindAll(ClaimTypes.Role), claim =>
            claim.Value == "Admin");
        Assert.DoesNotContain(authentication.Principal.FindAll(ClaimTypes.NameIdentifier), claim =>
            claim.Value == "attacker-controlled-id");
    }

    [Fact]
    public async Task SignInAsync_ReturnsFalse_WhenApplicationUserIdMissing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite("Filename=:memory:"));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IExternalLoginSessionService, IdentityExternalLoginSessionService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IExternalLoginSessionService>();

        Assert.False(await sut.SignInAsync(null, []));
    }

    [Fact]
    public async Task SignInAsync_ReturnsFalse_WhenLocalUserIsLockedOut()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IExternalLoginSessionService, IdentityExternalLoginSessionService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "locked@example.test",
            Email = "locked@example.test"
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        Assert.True((await userManager.SetLockoutEnabledAsync(user, true)).Succeeded);
        Assert.True((await userManager.SetLockoutEndDateAsync(
            user,
            DateTimeOffset.UtcNow.AddHours(1))).Succeeded);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var sut = scope.ServiceProvider.GetRequiredService<IExternalLoginSessionService>();

        Assert.False(await sut.SignInAsync(user.Id, []));
        Assert.DoesNotContain(httpContext.Response.Headers.SetCookie, value =>
            value?.Contains(".AspNetCore.Identity.Application=", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task SignInAsync_ReturnsFalse_WhenLocalTwoFactorIsEnabled()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IExternalLoginSessionService, IdentityExternalLoginSessionService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "two-factor@example.test",
            Email = "two-factor@example.test"
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        Assert.True((await userManager.SetTwoFactorEnabledAsync(user, true)).Succeeded);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var sut = scope.ServiceProvider.GetRequiredService<IExternalLoginSessionService>();

        Assert.False(await sut.SignInAsync(user.Id, []));
        Assert.DoesNotContain(httpContext.Response.Headers.SetCookie, value =>
            value?.Contains(".AspNetCore.Identity.Application=", StringComparison.Ordinal) == true);
    }
}

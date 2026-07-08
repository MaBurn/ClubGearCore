using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClubGear.Services.Core;

public sealed class AccountFeatureService : IAccountFeatureService
{
    private const string MasterAdminClaimType = "clubgear.system-role";
    private const string MasterAdminClaimValue = "master-admin";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly INotificationService _notificationService;
    private readonly IMessageComposer _messageComposer;

    public AccountFeatureService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        INotificationService notificationService,
        IMessageComposer messageComposer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _notificationService = notificationService;
        _messageComposer = messageComposer;
    }

    public async Task<AccountLoginOutcome> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AccountLoginOutcome(AccountLoginStatus.MissingCredentials);
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new AccountLoginOutcome(AccountLoginStatus.InvalidCredentials);
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, true, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return new AccountLoginOutcome(AccountLoginStatus.InvalidCredentials);
        }

        await EnsureMasterAdminForFirstUserAsync(user, cancellationToken);
        await EnsureBootstrapAdminAsync(user);

        return new AccountLoginOutcome(AccountLoginStatus.Success);
    }

    public async Task<AccountRegistrationOutcome> RegisterAsync(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AccountRegistrationOutcome(false, new[] { "Bitte alle Felder ausfuellen." });
        }

        var isFirstUser = !await _userManager.Users.AsNoTracking().AnyAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new AccountRegistrationOutcome(false, result.Errors.Select(e => e.Description).ToArray());
        }

        if (await _roleManager.RoleExistsAsync(RoleNames.MemberSelfService))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.MemberSelfService);
        }

        if (isFirstUser)
        {
            if (await _roleManager.RoleExistsAsync(RoleNames.Admin))
            {
                await _userManager.AddToRoleAsync(user, RoleNames.Admin);
            }

            if (await _roleManager.RoleExistsAsync(RoleNames.MemberManager))
            {
                await _userManager.AddToRoleAsync(user, RoleNames.MemberManager);
            }

            await EnsureMasterAdminForFirstUserAsync(user, cancellationToken);
        }

        var rendered = _messageComposer.Compose(
            subjectTemplate: "Willkommen bei ClubGear, {{FullName}}",
            bodyTemplate: "<p>Hallo {{FullName}},</p><p>dein Konto wurde erfolgreich erstellt.</p>",
            values: new Dictionary<string, string>
            {
                ["FullName"] = fullName,
                ["Email"] = email
            });

        await _notificationService.NotifyAsync(new NotificationMessage(
            Recipient: email,
            Subject: rendered.Subject,
            Body: rendered.Body,
            Channel: "email",
            CorrelationId: $"register:{user.Id}"), cancellationToken);

        await _signInManager.SignInAsync(user, isPersistent: true);
        return new AccountRegistrationOutcome(true, Array.Empty<string>());
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return _signInManager.SignOutAsync();
    }

    private async Task EnsureMasterAdminForFirstUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var userCount = await _userManager.Users.AsNoTracking().CountAsync(cancellationToken);
        if (userCount != 1)
        {
            return;
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var hasMasterAdminClaim = claims.Any(c =>
            string.Equals(c.Type, MasterAdminClaimType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value, MasterAdminClaimValue, StringComparison.OrdinalIgnoreCase));

        if (!hasMasterAdminClaim)
        {
            await _userManager.AddClaimAsync(user, new Claim(MasterAdminClaimType, MasterAdminClaimValue));
        }

        if (await _roleManager.RoleExistsAsync(RoleNames.Admin) && !await _userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.Admin);
        }

        if (await _roleManager.RoleExistsAsync(RoleNames.MemberManager) && !await _userManager.IsInRoleAsync(user, RoleNames.MemberManager))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.MemberManager);
        }
    }

    private async Task EnsureBootstrapAdminAsync(ApplicationUser user)
    {
        if (!await _roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            await _roleManager.CreateAsync(new IdentityRole(RoleNames.Admin));
        }

        var adminUsers = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
        if (adminUsers.Count > 0)
        {
            return;
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.Admin);
        }

        if (await _roleManager.RoleExistsAsync(RoleNames.MemberManager) && !await _userManager.IsInRoleAsync(user, RoleNames.MemberManager))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.MemberManager);
        }
    }
}

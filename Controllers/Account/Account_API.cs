using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/account")]
public class AccountApiController : ControllerBase
{
    private readonly IAccountFeatureService _accountFeatureService;

    public AccountApiController(IAccountFeatureService accountFeatureService)
    {
        _accountFeatureService = accountFeatureService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AccountLoginRequest request, CancellationToken cancellationToken = default)
    {
        var outcome = await _accountFeatureService.LoginAsync(request.Email, request.Password, cancellationToken);
        return outcome.Status switch
        {
            AccountLoginStatus.Success => Ok(new { success = true }),
            AccountLoginStatus.MissingCredentials => BadRequest(new { success = false, error = "Bitte E-Mail und Passwort eingeben." }),
            _ => Unauthorized(new { success = false, error = "Ungueltige Anmeldedaten." })
        };
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AccountRegisterRequest request, CancellationToken cancellationToken = default)
    {
        var outcome = await _accountFeatureService.RegisterAsync(request.FullName, request.Email, request.Password, cancellationToken);
        if (outcome.Succeeded)
        {
            return Ok(new { success = true });
        }

        return BadRequest(new { success = false, errors = outcome.Errors });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        await _accountFeatureService.LogoutAsync(cancellationToken);
        return Ok(new { success = true });
    }
}

public sealed record AccountLoginRequest(string Email, string Password);

public sealed record AccountRegisterRequest(string FullName, string Email, string Password);

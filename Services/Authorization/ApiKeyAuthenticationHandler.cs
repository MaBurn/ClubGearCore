using System.Text.Encodings.Web;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ClubGear.Services.Authorization;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    /// <summary>Header carrying the raw key, as an alternative to <c>Authorization: Bearer</c>.</summary>
    public string HeaderName { get; set; } = "X-API-Key";
}

/// <summary>
/// CORE-09. Authenticates a request from <c>Authorization: Bearer &lt;key&gt;</c> or
/// <c>X-API-Key: &lt;key&gt;</c>.
/// </summary>
/// <remarks>
/// A missing header is <see cref="AuthenticateResult.NoResult"/> rather than a failure, so a
/// request carrying an Identity cookie still authenticates through the cookie scheme. Only a
/// header that is present and wrong fails.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = ReadKey();

        if (string.IsNullOrEmpty(presented))
        {
            return AuthenticateResult.NoResult();
        }

        var principal = await _apiKeyService.AuthenticateAsync(presented, Scheme.Name, Context.RequestAborted);

        if (principal is null)
        {
            // One message for unknown, revoked and expired alike: distinguishing them would tell
            // an attacker which of their guesses named a real key.
            return AuthenticateResult.Fail("Ungültiger API-Key.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private string? ReadKey()
    {
        if (Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            var direct = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct.Trim();
            }
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }

        return null;
    }
}

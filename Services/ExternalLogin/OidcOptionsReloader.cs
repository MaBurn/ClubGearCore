using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Reloads OIDC handler options from <see cref="ISystemConfigService"/> at request time.
/// Config keys live under section <c>externallogin.{providerKey}</c> with names
/// <c>authority</c>, <c>clientid</c>, and <c>clientsecret</c>.
/// </summary>
internal sealed class OidcOptionsReloader : IConfigureNamedOptions<OpenIdConnectOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OidcOptionsReloader(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        // name is the OIDC scheme name: "oidc.<providerKey>" by convention.
        // Strip the "oidc." prefix to derive the provider key; fall back to full name.
        var providerKey = name.StartsWith("oidc.", StringComparison.OrdinalIgnoreCase)
            ? name["oidc.".Length..]
            : name;

        var section = $"externallogin.{providerKey}";

        using var scope = _scopeFactory.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

        var authority = configService
            .GetValueAsync(section, "authority")
            .GetAwaiter().GetResult();
        var clientId = configService
            .GetValueAsync(section, "clientid")
            .GetAwaiter().GetResult();
        var clientSecret = configService
            .GetValueAsync(section, "clientsecret")
            .GetAwaiter().GetResult();

        if (!string.IsNullOrWhiteSpace(authority))
            options.Authority = authority;

        if (!string.IsNullOrWhiteSpace(clientId))
            options.ClientId = clientId;

        if (!string.IsNullOrWhiteSpace(clientSecret))
            options.ClientSecret = clientSecret;
    }

    public void Configure(OpenIdConnectOptions options)
        => Configure(Options.DefaultName, options);
}

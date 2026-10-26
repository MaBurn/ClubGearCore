using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Post-configures provider-specific OIDC handler options from
/// <see cref="ISystemConfigService"/>. The named options cache is invalidated by
/// <see cref="ExternalLoginConfigService"/> when provider configuration changes.
/// Config keys live under section <c>externallogin.{providerKey}</c> with names
/// <c>authority</c>, <c>clientid</c>, and <c>clientsecret</c>.
/// </summary>
internal sealed class OidcOptionsReloader : IPostConfigureOptions<OpenIdConnectOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OidcOptionsReloader> _logger;

    public OidcOptionsReloader(
        IServiceScopeFactory scopeFactory,
        ILogger<OidcOptionsReloader>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<OidcOptionsReloader>.Instance;
    }

    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !name.StartsWith("oidc.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // name is the OIDC scheme name: "oidc.<providerKey>" by convention.
        // Strip the "oidc." prefix to derive the provider key; fall back to full name.
        var providerKey = name["oidc.".Length..];
        var hasSafeProviderKey = ExternalLoginServiceProviderKey.IsValid(providerKey);

        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.CallbackPath = hasSafeProviderKey
            ? new PathString($"/signin-oidc-{providerKey}")
            : new PathString("/_clubgear/oidc-disabled");
        options.SaveTokens = false;

        if (!hasSafeProviderKey)
        {
            ConfigureQuarantined(options);
            return;
        }

        var section = $"externallogin.{providerKey}";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var entries = configService
                .GetBySectionAsync(section)
                .GetAwaiter().GetResult();
            var config = entries
                .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Value,
                    StringComparer.OrdinalIgnoreCase);

            if (OidcProviderConfiguration.IsEnabled(config)
                && OidcProviderConfiguration.TryCreate(config, out var settings, out _))
            {
                options.Authority = settings.Authority;
                options.MetadataAddress = null;
                options.Configuration = null;
                options.ConfigurationManager = null;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                // ASP.NET Core deletes `iss` by default after token validation. The
                // plugin contract needs the validated issuer to namespace globally
                // unique Member.OauthID values safely across OIDC issuers.
                options.ClaimActions.Remove("iss");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not materialize OIDC options for provider '{ProviderKey}'; the scheme was quarantined.",
                providerKey);
        }

        ConfigureQuarantined(options);
    }

    private static void ConfigureQuarantined(OpenIdConnectOptions options)
    {
        const string quarantineAuthority = "https://oidc-disabled.invalid";

        options.Authority = null;
        options.MetadataAddress = null;
        options.ClientId = "clubgear-disabled-provider";
        options.ClientSecret = "clubgear-disabled-provider";
        options.RequireHttpsMetadata = true;
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = quarantineAuthority,
            AuthorizationEndpoint = $"{quarantineAuthority}/authorize",
            TokenEndpoint = $"{quarantineAuthority}/token",
            JwksUri = $"{quarantineAuthority}/jwks"
        };
        options.Configuration = configuration;
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
            configuration);
        options.Events.OnMessageReceived = context =>
        {
            context.SkipHandler();
            return Task.CompletedTask;
        };
    }
}

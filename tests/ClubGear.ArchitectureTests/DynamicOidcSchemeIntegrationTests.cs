using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class DynamicOidcSchemeIntegrationTests
{
    [Fact]
    public async Task ProviderSpecificHandler_ProducesOidcChallenge_WithItsOwnCallback()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>>(
            new StaticOidcOptionsPostConfigurer());
        services
            .AddAuthentication()
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddOpenIdConnect("oidc.generic", options =>
            {
                options.Authority = "https://unconfigured.invalid";
                options.ClientId = "unconfigured";
            });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var schemeProvider = scope.ServiceProvider
            .GetRequiredService<IAuthenticationSchemeProvider>();
        schemeProvider.AddScheme(new AuthenticationScheme(
            "oidc.keycloak",
            "Keycloak",
            typeof(OpenIdConnectHandler)));

        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("club.example");

        await context.ChallengeAsync(
            "oidc.keycloak",
            new AuthenticationProperties { RedirectUri = "/after-login" });

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        var location = context.Response.Headers.Location.ToString();
        Assert.StartsWith("https://id.example.test/authorize?", location, StringComparison.Ordinal);
        Assert.Contains("client_id=clubgear", location, StringComparison.Ordinal);
        Assert.Contains(
            "redirect_uri=https%3A%2F%2Fclub.example%2Fsignin-oidc-keycloak",
            location,
            StringComparison.Ordinal);
        Assert.Contains("state=", location, StringComparison.Ordinal);
        Assert.Contains(context.Response.Headers.SetCookie, value =>
            value?.Contains("Correlation", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(context.Response.Headers.SetCookie, value =>
            value?.Contains("Nonce", StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed class StaticOidcOptionsPostConfigurer
        : IPostConfigureOptions<OpenIdConnectOptions>
    {
        public void PostConfigure(string? name, OpenIdConnectOptions options)
        {
            if (!string.Equals(name, "oidc.keycloak", StringComparison.Ordinal))
            {
                return;
            }

            options.ClientId = "clubgear";
            options.ClientSecret = "secret";
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.CallbackPath = "/signin-oidc-keycloak";
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = "https://id.example.test",
                AuthorizationEndpoint = "https://id.example.test/authorize",
                TokenEndpoint = "https://id.example.test/token"
            };
        }
    }
}

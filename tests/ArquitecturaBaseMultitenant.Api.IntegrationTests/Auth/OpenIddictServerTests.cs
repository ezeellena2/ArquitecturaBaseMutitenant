using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class OpenIddictServerTests(ApiFactory factory)
{
    [Fact]
    public void Api_registers_the_local_authorization_server()
    {
        Assert.NotNull(factory.Services.GetService<IOpenIddictApplicationManager>());
    }

    [Fact]
    public void Server_requires_pkce_and_rotating_refresh_tokens_without_password_flow()
    {
        var server = factory.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var validation = factory.Services.GetRequiredService<IOptionsMonitor<OpenIddictValidationOptions>>().CurrentValue;

        Assert.Contains(GrantTypes.AuthorizationCode, server.GrantTypes);
        Assert.Contains(GrantTypes.RefreshToken, server.GrantTypes);
        Assert.DoesNotContain(GrantTypes.Password, server.GrantTypes);
        Assert.DoesNotContain(GrantTypes.ClientCredentials, server.GrantTypes);
        Assert.Contains(CodeChallengeMethods.Sha256, server.CodeChallengeMethods);
        Assert.Contains("api", server.Scopes);
        Assert.DoesNotContain(Scopes.Roles, server.Scopes);
        Assert.Equal(TimeSpan.FromMinutes(5), server.AuthorizationCodeLifetime);
        Assert.Equal(TimeSpan.FromMinutes(15), server.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), server.RefreshTokenLifetime);
        Assert.Null(server.RefreshTokenReuseLeeway);
        Assert.Equal(new Uri("https://localhost:5174/"), server.Issuer);
        Assert.Contains(new Uri("connect/authorize", UriKind.Relative), server.AuthorizationEndpointUris);
        Assert.Contains(new Uri("connect/token", UriKind.Relative), server.TokenEndpointUris);
        Assert.True(validation.EnableTokenEntryValidation);
        var web = factory.Services.GetRequiredService<IOptions<WebClientOptions>>().Value;
        Assert.Equal(new Uri("https://localhost:5174/auth/callback"), Assert.Single(web.RedirectUris));
        Assert.Equal(new Uri("https://localhost:5174/"), Assert.Single(web.PostLogoutRedirectUris));
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<ITokenRevoker>());
    }
}

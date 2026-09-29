using ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Siembra o alinea el cliente público web y el scope API dentro de la transacción del seed.</summary>
internal sealed class OpenIddictSeeder(
    IOpenIddictApplicationManager applications,
    IOpenIddictScopeManager scopes,
    IOptions<WebClientOptions> webOptions)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var scopeDescriptor = new OpenIddictScopeDescriptor
        {
            Name = AuthServerDefaults.ApiScope,
            DisplayName = "ArquitecturaBaseMultitenant API",
        };
        scopeDescriptor.Resources.Add(AuthServerDefaults.ApiResource);

        var scope = await scopes.FindByNameAsync(AuthServerDefaults.ApiScope, cancellationToken);
        if (scope is null)
        {
            await scopes.CreateAsync(scopeDescriptor, cancellationToken);
        }
        else
        {
            await scopes.UpdateAsync(scope, scopeDescriptor, cancellationToken);
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AuthServerDefaults.WebClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "ArquitecturaBaseMultitenant Web",
        };
        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Email,
            Permissions.Scopes.Profile,
            Permissions.Prefixes.Scope + AuthServerDefaults.ApiScope,
        ]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        descriptor.RedirectUris.UnionWith(webOptions.Value.RedirectUris);
        descriptor.PostLogoutRedirectUris.UnionWith(webOptions.Value.PostLogoutRedirectUris);

        var application = await applications.FindByClientIdAsync(AuthServerDefaults.WebClientId, cancellationToken);
        if (application is null)
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applications.UpdateAsync(application, descriptor, cancellationToken);
        }
    }
}

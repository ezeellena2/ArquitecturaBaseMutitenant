using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;

/// <summary>Conserva las URI fijas de OpenIddict y amplía sólo el cliente web con subdominios publicados.</summary>
internal sealed class SubdomainApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<TApplication> store,
    SubdomainRedirectUriValidator subdomains) : OpenIddictApplicationManager<TApplication>(cache, logger, options, store)
    where TApplication : class
{
    public override async ValueTask<bool> ValidateRedirectUriAsync(TApplication application, string uri,
        CancellationToken cancellationToken = default) =>
        await base.ValidateRedirectUriAsync(application, uri, cancellationToken)
        || (await GetClientIdAsync(application, cancellationToken) == AuthServerDefaults.WebClientId
            && await subdomains.ValidateRedirectUriAsync(uri, cancellationToken));

    public override async ValueTask<bool> ValidatePostLogoutRedirectUriAsync(TApplication application, string uri,
        CancellationToken cancellationToken = default) =>
        await base.ValidatePostLogoutRedirectUriAsync(application, uri, cancellationToken)
        || (await GetClientIdAsync(application, cancellationToken) == AuthServerDefaults.WebClientId
            && await subdomains.ValidatePostLogoutRedirectUriAsync(uri, cancellationToken));

    public override async IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string uri,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var foundWeb = false;
        await foreach (var application in base.FindByPostLogoutRedirectUriAsync(uri, cancellationToken))
        {
            foundWeb |= await GetClientIdAsync(application, cancellationToken) == AuthServerDefaults.WebClientId;
            yield return application;
        }

        // Sin client_id, OpenIddict busca primero por la URI: la validación por aplicación no alcanza.
        if (!foundWeb && await subdomains.ValidatePostLogoutRedirectUriAsync(uri, cancellationToken)
            && await FindByClientIdAsync(AuthServerDefaults.WebClientId, cancellationToken) is { } web)
        {
            yield return web;
        }
    }
}

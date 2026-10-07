using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;

/// <summary>Acepta sólo retornos exactos del origen de una organización publicada y activa, sobre el issuer fijo.</summary>
internal sealed class SubdomainRedirectUriValidator(IPublicOrigin publicOrigin, IPublicSiteReader sites)
{
    public Task<bool> ValidateRedirectUriAsync(string uri, CancellationToken cancellationToken) =>
        ValidateAsync(uri, postLogout: false, cancellationToken);

    public Task<bool> ValidatePostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken) =>
        ValidateAsync(uri, postLogout: true, cancellationToken);

    private async Task<bool> ValidateAsync(string value, bool postLogout, CancellationToken cancellationToken)
    {
        var origin = publicOrigin.Value;
        if (origin is null || origin.HostNameType != UriHostNameType.Dns
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.HostNameType != UriHostNameType.Dns
            || !string.Equals(uri.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
            || uri.Port != origin.Port || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || !value.Equals(value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        // Uri normaliza escapes y segmentos: las rutas permitidas se comparan también en el valor recibido.
        var authorityStart = value.IndexOf("://", StringComparison.Ordinal);
        if (authorityStart < 0) return false;
        var pathStart = value.IndexOf('/', authorityStart + 3);
        var path = pathStart < 0 ? "/" : value[pathStart..];
        if (postLogout ? path != "/"
            : path is not ("/auth/callback" or "/silent-renew.html"))
        {
            return false;
        }

        var suffix = "." + origin.Host;
        if (!uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;

        var label = uri.Host[..^suffix.Length];
        if (label.Contains('.')) return false;

        var slug = TenantSlug.Create(label);
        if (slug.IsFailure || !label.Equals(slug.Value.Value, StringComparison.OrdinalIgnoreCase)) return false;

        return await sites.FindBySlugAsync(slug.Value.Value, cancellationToken) is not null;
    }
}

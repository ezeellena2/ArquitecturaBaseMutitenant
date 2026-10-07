using System.Net;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Resuelve solo rutas públicas por un único slug sobre el host y puerto del issuer.</summary>
public sealed class PublicSiteResolutionMiddleware(RequestDelegate next, IPublicOrigin publicOrigin)
{
    public async Task InvokeAsync(HttpContext context, PublicSiteContext publicSite, IPublicSiteReader reader)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint()?.Metadata.GetMetadata<PublicSiteAttribute>() is not null
            && FindSlug(context.Request) is { } slug)
        {
            var site = await reader.FindBySlugAsync(slug, context.RequestAborted);
            if (site is not null) publicSite.Set(site);
        }

        await next(context);
    }

    private string? FindSlug(HttpRequest request)
    {
        var origin = publicOrigin.Value;
        if (origin is null || origin.HostNameType != UriHostNameType.Dns) return null;

        var requestHost = request.Host.Host;
        var requestPort = request.Host.Port ?? (request.IsHttps ? 443 : 80);
        if (requestPort != origin.Port || IPAddress.TryParse(requestHost, out _)) return null;

        var suffix = "." + origin.Host;
        if (!requestHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;

        var label = requestHost[..^suffix.Length];
        if (label.Contains('.')) return null;

        var slug = TenantSlug.Create(label);
        return slug.IsSuccess && label.Equals(slug.Value.Value, StringComparison.OrdinalIgnoreCase)
            ? slug.Value.Value : null;
    }
}

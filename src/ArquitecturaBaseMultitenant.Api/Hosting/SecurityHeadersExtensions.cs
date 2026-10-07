using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Hosting;

/// <summary>Agrega cabeceras de seguridad a toda respuesta, incluso las que produce el manejador de excepciones.</summary>
internal static class SecurityHeadersExtensions
{
    /// <summary>
    /// Encabezados de seguridad de la sección 6.9 del spec. La CSP permite exactamente lo que hace el SPA: sus
    /// propios archivos (scripts, estilos y fuentes con hash, servidos desde este mismo origen), los estilos en
    /// línea que escriben los componentes accesibles, sus llamadas a esta misma Api, el iframe de renovación
    /// silenciosa (/silent-renew.html, del mismo origen) y el formulario que puede terminar en Google.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-src 'self'; " +
        "frame-ancestors 'self'; " +
        "form-action 'self' https://accounts.google.com; " +
        "base-uri 'self'";

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var origin = app.Services.GetRequiredService<IPublicOrigin>().Value;
        app.Use(async (context, next) =>
        {
            // Los encabezados se escriben recién cuando arranca la respuesta, no acá: UseExceptionHandler limpia
            // la respuesta antes de armar el 500 y se llevaría puesto todo lo que hubiéramos puesto antes.
            context.Response.OnStarting(static state =>
            {
                var (request, issuer) = ((HttpContext, Uri?))state;
                var headers = request.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.ContentSecurityPolicy = PolicyFor(request, issuer);

                return Task.CompletedTask;
            }, (context, origin));

            await next(context);
        });

        return app;
    }

    private static string PolicyFor(HttpContext context, Uri? origin)
    {
        if (origin is null || origin.HostNameType != UriHostNameType.Dns) return ContentSecurityPolicy;

        if (context.Request.Path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase))
        {
            var ancestors = $"frame-ancestors 'self' {origin.Scheme}://*.{origin.Authority};";
            return ContentSecurityPolicy.Replace("frame-ancestors 'self';", ancestors, StringComparison.Ordinal);
        }

        if (context.Response.ContentType?.Split(';', 2)[0].Equals("text/html", StringComparison.OrdinalIgnoreCase) == true
            && IsOrganizationOrigin(context.Request, origin))
        {
            return ContentSecurityPolicy.Replace("frame-src 'self';",
                "frame-src 'self' " + origin.GetLeftPart(UriPartial.Authority) + ";", StringComparison.Ordinal);
        }

        return ContentSecurityPolicy;
    }

    private static bool IsOrganizationOrigin(HttpRequest request, Uri origin)
    {
        if (!request.Scheme.Equals(origin.Scheme, StringComparison.OrdinalIgnoreCase)
            || (request.Host.Port ?? (request.IsHttps ? 443 : 80)) != origin.Port) return false;

        var suffix = "." + origin.Host;
        var host = request.Host.Host;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;

        var label = host[..^suffix.Length];
        if (label.Contains('.')) return false;

        var slug = TenantSlug.Create(label);
        return slug.IsSuccess && label.Equals(slug.Value.Value, StringComparison.OrdinalIgnoreCase);
    }
}

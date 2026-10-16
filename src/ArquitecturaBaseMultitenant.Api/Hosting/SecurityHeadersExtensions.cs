using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Hosting;

/// <summary>Agrega cabeceras de seguridad a toda respuesta, incluso las que produce el manejador de excepciones.</summary>
internal static partial class SecurityHeadersExtensions
{
    /// <summary>
    /// Encabezados de seguridad de la sección 6.9 del spec. La CSP permite exactamente lo que hace el SPA: sus
    /// propios archivos (scripts, estilos y fuentes con hash, servidos desde este mismo origen), los estilos en
    /// línea que escriben los componentes accesibles, sus llamadas a esta misma Api, el iframe de renovación
    /// silenciosa (/silent-renew.html, del mismo origen) y el formulario que puede terminar en Google. Los estilos en
    /// línea son la única excepción y se acotan a estilos (E9): ningún script en línea ni eval, tampoco de un módulo.
    /// </summary>
    private static readonly (string Name, string[] Sources)[] Directives =
    [
        ("default-src", ["'self'"]),
        ("script-src", ["'self'"]),
        ("style-src", ["'self'", "'unsafe-inline'"]),
        ("img-src", ["'self'", "data:", "https:"]),
        ("font-src", ["'self'", "data:"]),
        ("connect-src", ["'self'"]),
        ("frame-src", ["'self'"]),
        ("frame-ancestors", ["'self'"]),
        ("form-action", ["'self'", "https://accounts.google.com"]),
        ("base-uri", ["'self'"]),
        ("object-src", ["'none'"]),
    ];

    /// <summary>Las únicas directivas que un módulo puede ampliar.</summary>
    internal static readonly string[] ModuleDirectives = ["script-src", "frame-src", "connect-src"];

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var policies = new Policies(app.Services.GetRequiredService<IPublicOrigin>().Value,
            app.Services.GetServices<IContentSecurityPolicySource>());
        app.Use(async (context, next) =>
        {
            // Los encabezados se escriben recién cuando arranca la respuesta, no acá: UseExceptionHandler limpia
            // la respuesta antes de armar el 500 y se llevaría puesto todo lo que hubiéramos puesto antes.
            context.Response.OnStarting(static state =>
            {
                var (request, policies) = ((HttpContext, Policies))state;
                var headers = request.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.ContentSecurityPolicy = policies.For(request);

                return Task.CompletedTask;
            }, (context, policies));

            await next(context);
        });

        return app;
    }

    [GeneratedRegex(@"^https://(\*\.)?[a-z0-9-]+(\.[a-z0-9-]+)+(:[0-9]{1,5})?$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleOrigin();

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

    /// <summary>
    /// Las tres CSP que puede llevar una respuesta, armadas al arrancar: la general, la de /connect/authorize (que pueden
    /// enmarcar los subdominios de las organizaciones) y la de un documento de un subdominio (que enmarca el puente del
    /// dominio principal). Los orígenes de los módulos se validan antes de entrar.
    /// </summary>
    private sealed class Policies
    {
        private readonly Uri? _origin;
        private readonly string _general;
        private readonly string _authorize;
        private readonly string _organizationDocument;

        public Policies(Uri? origin, IEnumerable<IContentSecurityPolicySource> modules)
        {
            var directives = Directives.ToDictionary(directive => directive.Name,
                directive => new List<string>(directive.Sources), StringComparer.Ordinal);
            foreach (var (name, sources) in modules.SelectMany(module => module.Sources))
            {
                if (!ModuleDirectives.Contains(name, StringComparer.Ordinal))
                    throw new InvalidOperationException("A module cannot extend the CSP directive '" + name + "'.");
                foreach (var source in sources)
                {
                    if (!ModuleOrigin().IsMatch(source))
                        throw new InvalidOperationException("A module CSP source must be an https origin: '" + source + "'.");
                    if (!directives[name].Contains(source, StringComparer.Ordinal)) directives[name].Add(source);
                }
            }

            _origin = origin is { HostNameType: UriHostNameType.Dns } ? origin : null;
            _general = Render(directives, null, null);
            _authorize = _origin is null ? _general
                : Render(directives, null, _origin.Scheme + "://*." + _origin.Authority);
            _organizationDocument = _origin is null ? _general
                : Render(directives, _origin.GetLeftPart(UriPartial.Authority), null);
        }

        public string For(HttpContext context)
        {
            if (_origin is null) return _general;
            if (context.Request.Path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase)) return _authorize;
            return context.Response.ContentType?.Split(';', 2)[0].Equals("text/html", StringComparison.OrdinalIgnoreCase) == true
                && IsOrganizationOrigin(context.Request, _origin) ? _organizationDocument : _general;
        }

        private static string Render(Dictionary<string, List<string>> directives, string? frame, string? ancestor) =>
            string.Join("; ", Directives.Select(directive =>
            {
                var sources = directives[directive.Name].AsEnumerable();
                if (directive.Name == "frame-src" && frame is not null) sources = sources.Append(frame);
                if (directive.Name == "frame-ancestors" && ancestor is not null) sources = sources.Append(ancestor);
                return directive.Name + " " + string.Join(' ', sources);
            }));
    }
}

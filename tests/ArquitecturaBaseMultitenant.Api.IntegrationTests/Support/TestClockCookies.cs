using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Las cookies de un cliente de prueba, vencidas con el reloj del host. Un host con el reloj fijo emite la cookie de sesión
/// (30 días) con ese reloj, y el <c>CookieContainer</c> de <c>CreateClient</c> la vence con el reloj real: pasados 30 días
/// de la fecha fija la descarta y el pedido siguiente llega sin sesión. Este manejador guarda las cookies como un navegador
/// (nombre, ruta y <c>Secure</c>), pero mira su vencimiento con el reloj de la prueba; la que el servidor borra, sale.
/// </summary>
internal sealed class TestClockCookies(TimeProvider clock) : DelegatingHandler
{
    private readonly Dictionary<(string Name, string Path), Cookie> cookies = [];

    /// <summary>Como <c>host.CreateClient(options)</c>, con las cookies vencidas por el <see cref="TimeProvider"/> del host.</summary>
    internal static HttpClient Client(WebApplicationFactory<Program> host, WebApplicationFactoryClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        options ??= new WebApplicationFactoryClientOptions();
        var jar = new TestClockCookies(host.Services.GetRequiredService<TimeProvider>());
        DelegatingHandler[] handlers = options.AllowAutoRedirect ? [new RedirectHandler(options.MaxAutomaticRedirections), jar] : [jar];
        return host.CreateDefaultClient(options.BaseAddress, handlers);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");
        var header = Header(uri);
        if (header.Length > 0) request.Headers.Add("Cookie", header);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
            foreach (var value in values) Store(uri, value);
        return response;
    }

    private string Header(Uri uri)
    {
        var now = clock.GetUtcNow();
        lock (cookies)
            return string.Join("; ", cookies.Where(pair => (pair.Value.ExpiresUtc is null || pair.Value.ExpiresUtc > now)
                    && (!pair.Value.Secure || uri.Scheme == Uri.UriSchemeHttps) && PathMatches(uri.AbsolutePath, pair.Key.Path))
                .Select(pair => pair.Key.Name + "=" + pair.Value.Value));
    }

    // RFC 6265: Max-Age manda sobre Expires; una cookie vencida para el reloj de la prueba es una baja.
    private void Store(Uri uri, string setCookie)
    {
        var parts = setCookie.Split(';', StringSplitOptions.TrimEntries);
        var separator = parts[0].IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0) return;
        var now = clock.GetUtcNow();
        var path = DefaultPath(uri.AbsolutePath);
        var secure = false;
        DateTimeOffset? expires = null;
        DateTimeOffset? maxAge = null;
        foreach (var attribute in parts.Skip(1))
        {
            var (name, value) = attribute.IndexOf('=', StringComparison.Ordinal) is var equals and >= 0
                ? (attribute[..equals], attribute[(equals + 1)..]) : (attribute, string.Empty);
            if (name.Equals("path", StringComparison.OrdinalIgnoreCase) && value.StartsWith('/')) path = value;
            else if (name.Equals("secure", StringComparison.OrdinalIgnoreCase)) secure = true;
            else if (name.Equals("max-age", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                maxAge = seconds <= 0 ? DateTimeOffset.MinValue : now.AddSeconds(seconds);
            else if (name.Equals("expires", StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                expires = date;
        }

        var key = (parts[0][..separator], path);
        var expiresUtc = maxAge ?? expires;
        lock (cookies)
        {
            if (expiresUtc <= now) cookies.Remove(key);
            else cookies[key] = new Cookie(parts[0][(separator + 1)..], expiresUtc, secure);
        }
    }

    private static bool PathMatches(string requestPath, string cookiePath) =>
        requestPath == cookiePath || requestPath.StartsWith(cookiePath, StringComparison.Ordinal)
            && (cookiePath.EndsWith('/') || requestPath[cookiePath.Length] == '/');

    private static string DefaultPath(string requestPath)
    {
        var slash = requestPath.LastIndexOf('/');
        return slash <= 0 ? "/" : requestPath[..slash];
    }

    private sealed record Cookie(string Value, DateTimeOffset? ExpiresUtc, bool Secure);
}

using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Las cookies de <see cref="TestClockCookies"/> vencen con el reloj de la prueba: se manda la cookie que el reloj real ya
/// daría por vencida, y deja de mandarse cuando vence para el reloj de la prueba o el servidor la borra.
/// </summary>
public sealed class TestClockCookiesTests
{
    private static readonly Uri Https = new("https://localhost");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_session_cookie_of_a_past_test_clock_is_sent_until_it_expires_for_that_clock()
    {
        // La emite un reloj fijo en 2001: vence a los 30 días de esa fecha, ya pasados para el reloj real.
        var clock = new FakeTimeProvider(new DateTimeOffset(2001, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var withContainer = new Server(clock);
        using (var container = new HttpClient(new CookieContainerHandler { InnerHandler = withContainer }) { BaseAddress = Https })
        {
            await SendAsync(container, "/login");
            await SendAsync(container, "/me");
        }
        Assert.Null(withContainer.Received[^1]);

        var server = new Server(clock);
        using var client = new HttpClient(new TestClockCookies(clock) { InnerHandler = server }) { BaseAddress = Https };
        await SendAsync(client, "/login");
        await SendAsync(client, "/me");
        Assert.Equal("session=abc", server.Received[^1]);
        clock.Advance(TimeSpan.FromDays(30));
        await SendAsync(client, "/me");
        Assert.Null(server.Received[^1]);
    }

    [Fact]
    public async Task A_secure_cookie_goes_only_over_https_and_a_deleted_one_is_no_longer_sent()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var server = new Server(clock);
        using var client = new HttpClient(new TestClockCookies(clock) { InnerHandler = server });
        await SendAsync(client, "https://localhost/login");
        await SendAsync(client, "http://localhost/me");
        Assert.Null(server.Received[^1]);
        await SendAsync(client, "https://localhost/me");
        Assert.Equal("session=abc", server.Received[^1]);
        await SendAsync(client, "https://localhost/logout");
        await SendAsync(client, "https://localhost/me");
        Assert.Null(server.Received[^1]);
    }

    private static async Task SendAsync(HttpClient client, string uri)
    {
        using var response = await client.GetAsync(new Uri(uri, UriKind.RelativeOrAbsolute), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Emite la cookie de sesión de 30 días con el reloj de la prueba, la borra al salir y anota lo que recibe.</summary>
    private sealed class Server(TimeProvider clock) : HttpMessageHandler
    {
        public List<string?> Received { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Received.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
            if (request.RequestUri!.AbsolutePath == "/login")
                response.Headers.Add("Set-Cookie", "session=abc; expires="
                    + clock.GetUtcNow().AddDays(30).ToString("r", CultureInfo.InvariantCulture) + "; path=/; secure; httponly");
            else if (request.RequestUri.AbsolutePath == "/logout")
                response.Headers.Add("Set-Cookie", "session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/");
            return Task.FromResult(response);
        }
    }
}

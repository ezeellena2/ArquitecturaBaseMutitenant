using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La organización en la telemetría (E9): el span de un pedido con acceso de organización lleva <c>tenant.id</c> y
/// <c>tenant.kind</c> del contexto validado; Plataforma, un pedido anónimo y el sitio público de un subdominio quedan
/// sin organización, y la métrica de duración de los pedidos nunca lleva el id como dimensión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class TenantTelemetryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_organization_request_tags_its_span_with_the_validated_organization()
    {
        using var client = Browser(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Telemetría " + Guid.NewGuid().ToString("N")[..8], Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        using var spans = new SpanRecorder();
        using var duration = new MetricCollector<double>(factory.Services.GetRequiredService<IMeterFactory>(),
            "Microsoft.AspNetCore.Hosting", "http.server.request.duration");

        var span = await spans.SendAsync(client, "/api/me");

        Assert.Equal(tenantId.ToString("D"), span.GetTagItem("tenant.id"));
        Assert.Equal("Business", span.GetTagItem("tenant.kind"));
        await duration.WaitForMeasurementsAsync(1, TimeSpan.FromSeconds(10));
        Assert.All(duration.GetMeasurementSnapshot(), measurement =>
            Assert.False(measurement.Tags.ContainsKey("tenant.id")));
    }

    [Fact]
    public async Task Platform_anonymous_and_public_site_requests_carry_no_organization()
    {
        using var operatorClient = Browser(factory);
        var account = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using var anonymous = Browser(factory);
        using var subdomain = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://empresa.localhost:5174"), AllowAutoRedirect = false });
        using var spans = new SpanRecorder();

        var platform = await spans.SendAsync(operatorClient, "/api/platform/settings");
        var unauthenticated = await spans.SendAsync(anonymous, "/api/me");
        var publicSite = await spans.SendAsync(subdomain, "/api/public-site");

        Assert.All([platform, unauthenticated, publicSite], span =>
        {
            Assert.Null(span.GetTagItem("tenant.id"));
            Assert.Null(span.GetTagItem("tenant.kind"));
        });
    }

    private static HttpClient Browser(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Los spans de pedidos de la Api, encontrados por el traceparent que manda cada prueba.</summary>
    private sealed class SpanRecorder : IDisposable
    {
        private readonly ConcurrentDictionary<ActivityTraceId, Activity> _stopped = new();
        private readonly ActivityListener _listener;

        public SpanRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _stopped.TryAdd(activity.TraceId, activity),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public async Task<Activity> SendAsync(HttpClient client, string path)
        {
            var traceId = ActivityTraceId.CreateRandom();
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("traceparent", "00-" + traceId.ToHexString() + "-" + ActivitySpanId.CreateRandom().ToHexString() + "-01");
            using var response = await client.SendAsync(request, Ct);
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            for (var attempt = 0; attempt < 50 && !_stopped.ContainsKey(traceId); attempt++)
                await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
            return Assert.Contains(traceId, (IReadOnlyDictionary<ActivityTraceId, Activity>)_stopped);
        }

        public void Dispose() => _listener.Dispose();
    }
}

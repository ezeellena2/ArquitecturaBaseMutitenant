using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Las métricas no llevan a quién ni qué (B9): ninguna etiqueta nombra una organización, una empresa, una persona, un
/// teléfono, un correo o un contenido, y ningún valor es el id de una de ellas. Una etiqueta así sería una dimensión sin
/// tope y un dato personal en el tablero; la organización va solo en el span (<see cref="TenantTelemetryTests"/>).
/// Escucha todos los instrumentos de la Api, de ASP.NET Core y de los clientes HTTP mientras una persona crea su
/// organización, entra como empresa y usa una empresa, como el que se agregue mañana.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class MetricTagsTests(ApiFactory factory)
{
    private static readonly string[] WatchedMeters = ["ArquitecturaBaseMultitenant", "Microsoft.AspNetCore", "System.Net.Http"];

    /// <summary>
    /// Lo que una etiqueta no puede nombrar. La persona se controla por su id y su correo en los valores: ASP.NET Core
    /// etiqueta con «user» si el pedido tiene sesión, que no dice quién es.
    /// </summary>
    private static readonly string[] ForbiddenKeyParts =
        ["tenant", "organization", "organisation", "company", "empresa", "phone", "telefono", "email", "correo",
            "content", "body", "message.text"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_metric_tag_names_or_carries_an_organization_a_company_a_person_or_a_content()
    {
        using var recorder = new TagRecorder();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Métricas " + Guid.NewGuid().ToString("N")[..8], Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Empresa " + Guid.NewGuid().ToString("N")[..8], Ct);

        foreach (var path in new[] { "/api/me", "/api/companies/" + companyId.ToString("D"), "/api/users/paged" })
            (await client.GetAsync(path, Ct)).Dispose();

        var tags = recorder.Snapshot();
        Assert.Contains(tags, tag => tag.Instrument == "http.server.request.duration");
        string[] identities = [tenantId.ToString("D"), tenantId.ToString("N"), companyId.ToString("D"), companyId.ToString("N"),
            owner.UserId.ToString("D"), owner.UserId.ToString("N"), owner.Email.Value];
        Assert.All(tags, tag =>
        {
            Assert.False(ForbiddenKeyParts.Any(part => tag.Key.Contains(part, StringComparison.OrdinalIgnoreCase)),
                $"The metric {tag.Instrument} has the tag {tag.Key}.");
            Assert.False(identities.Any(identity => tag.Value.Contains(identity, StringComparison.OrdinalIgnoreCase)),
                $"The tag {tag.Key} of {tag.Instrument} carries an id or an email.");
        });
    }

    private sealed record RecordedTag(string Instrument, string Key, string Value);

    /// <summary>Cada etiqueta de cada medición de los medidores vigilados, mientras dura la prueba.</summary>
    private sealed class TagRecorder : IDisposable
    {
        private readonly ConcurrentBag<RecordedTag> _tags = [];
        private readonly MeterListener _listener = new();

        public TagRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (WatchedMeters.Any(prefix => instrument.Meter.Name.StartsWith(prefix, StringComparison.Ordinal)))
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Record(instrument, tags));
            _listener.SetMeasurementEventCallback<int>((instrument, _, tags, _) => Record(instrument, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Record(instrument, tags));
            _listener.Start();
        }

        public RecordedTag[] Snapshot() => [.. _tags];

        public void Dispose() => _listener.Dispose();

        private void Record(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var (key, value) in tags)
                _tags.Add(new RecordedTag(instrument.Name, key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
        }
    }
}

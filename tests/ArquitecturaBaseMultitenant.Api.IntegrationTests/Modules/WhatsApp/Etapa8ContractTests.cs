using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

public sealed partial class Etapa8ContractTests : IAsyncLifetime
{
    // «Sin canal» es sin número de Plataforma conectado en la base que comparte la colección (decisión 3 de la E11): se
    // asegura antes de cada caso.
    public async ValueTask InitializeAsync() => await PlatformWhatsAppNumberReset.DisconnectAsync(factory, Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Technical_webhook_requires_its_registered_global_availability(bool installed, bool enabled)
    {
        await using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInstalledModuleRouteAvailability>();
            if (installed) services.AddSingleton<IInstalledModuleRouteAvailability>(new Availability(enabled));
        }));
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=example&hub.challenge=example-challenge", Ct);
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(enabled ? "WhatsApp.Webhook.InvalidSignature" : "Http.NotFound", problem?["code"]?.GetValue<string>());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }


    static partial void AssertWhatsAppContract(JsonObject document)
    {
        Assert.NotNull(document["paths"]!["/webhooks/whatsapp"]!["post"]!["responses"]!["413"]);
        Assert.DoesNotContain(document["paths"]!["/webhooks/whatsapp"]!["post"]!["parameters"]!.AsArray(), value => value?["name"]?.GetValue<string>() == "Idempotency-Key");
        foreach (var method in new[] { "get", "post" })
        {
            var operation = document["paths"]!["/webhooks/whatsapp"]![method]!;
            Assert.Null(operation["responses"]?["501"]);
            Assert.DoesNotContain("T01", operation["description"]?.GetValue<string>() ?? "");
            Assert.DoesNotContain(operation["x-error-codes"]?.AsArray() ?? [], value => value?["code"]?.GetValue<string>() == "General.Contract.NotImplemented");
        }
    }

    private sealed class Availability(bool available) : IInstalledModuleRouteAvailability
    {
        public string Key => "WhatsApp.Webhook";
        public bool IsAvailable => available;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B1 de la E11, número de Plataforma (decisión 3): la configuración lo crea al arrancar, la pantalla lo reemplaza, y sin
/// número conectado (o sin medio de pago) el ingreso ofrece solo correo y Google. Base propia: el número es global.
/// </summary>
public sealed class WhatsAppPlatformNumberTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_screen_replaces_the_configured_number_and_payment_decides_whether_codes_use_whatsapp()
    {
        var graph = new FakeGraph();
        await using (var empty = WhatsAppAttentionJourney.Host(factory, graph))
        {
            using var operatorClient = await OperatorAsync(empty);
            Assert.False(await OffersWhatsAppAsync(operatorClient));
            using var none = await operatorClient.GetAsync("/api/platform/whatsapp/number", Ct);
            var body = await WhatsAppAttentionJourney.JsonAsync(none, Ct);
            Assert.Equal(JsonValueKind.Null, body.GetProperty("number").ValueKind);
            Assert.Equal("app-fixture", body.GetProperty("signup").GetProperty("appId").GetString());
            using var update = await operatorClient.PutAsJsonAsync("/api/platform/whatsapp/number", new { consultationsEnabled = false, version = 0 }, Ct);
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        }
        var configured = WhatsAppAttentionJourney.NewNumberId();
        var settings = new Dictionary<string, string> { ["WhatsApp:PhoneNumberId"] = configured, ["WhatsApp:AccessToken"] = "configured-token" };
        await using (var boot = WhatsAppAttentionJourney.Host(factory, graph, settings: settings))
        {
            using var anonymous = WhatsAppAttentionJourney.Client(boot);
            Assert.True(await OffersWhatsAppAsync(anonymous));
            await using var scope = boot.Services.CreateAsyncScope();
            var channel = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChannel>().AsNoTracking()
                .SingleAsync(value => value.OrganizationId == null && value.Status != WhatsAppNumberStatus.Disconnected, Ct);
            Assert.Equal((configured, WhatsAppChannelSource.Configuration), (channel.PhoneNumberId, channel.Source));
        }

        await using var host = WhatsAppAttentionJourney.Host(factory, graph, settings: settings);
        using var client = await OperatorAsync(host);
        var replacement = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.PostAsync(client, "/api/platform/whatsapp/number",
            new { code = "signup-code", phoneNumberId = replacement, wabaId = "waba-platform", coexistence = false }, Ct);
        Assert.True(connected.StatusCode == HttpStatusCode.Created, await connected.Content.ReadAsStringAsync(Ct));
        var number = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("number");
        Assert.True(await OffersWhatsAppAsync(client));

        graph.HasPayment = false;
        using var checkedNumber = await WhatsAppAttentionJourney.PostAsync(client, "/api/platform/whatsapp/number/check",
            new { version = number.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal("PaymentMissing", (await WhatsAppAttentionJourney.JsonAsync(checkedNumber, Ct)).GetProperty("number").GetProperty("status").GetString());
        Assert.False(await OffersWhatsAppAsync(client));

        // Con un número conectado por la pantalla, la configuración ya no manda al volver a arrancar.
        await using var restarted = WhatsAppAttentionJourney.Host(factory, graph, settings: settings);
        await using var db = restarted.Services.CreateAsyncScope();
        var active = await db.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChannel>().AsNoTracking()
            .Where(value => value.OrganizationId == null && value.Status != WhatsAppNumberStatus.Disconnected).ToArrayAsync(Ct);
        Assert.Equal(replacement, Assert.Single(active).PhoneNumberId);
    }


    private static async Task<HttpClient> OperatorAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.WhatsApp.Manage], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static async Task<bool> OffersWhatsAppAsync(HttpClient client)
    {
        using var methods = await client.GetFromJsonAsync<JsonDocument>("/api/auth/methods", Ct);
        return methods!.RootElement.GetProperty("channels").EnumerateArray().Any(channel => channel.GetProperty("key").GetString() == "whatsapp");
    }
}

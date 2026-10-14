using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// D6 de la E11: un correo por equipo cuando la conversación sin tomar más vieja pasa el tiempo elegido, solo con el
/// equipo, la cantidad y el enlace (sin teléfonos ni textos); no se repite hasta que la bandeja se vacía y vuelve a llenarse.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppWaitingNoticeTests(ApiFactory factory)
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_team_gets_one_summary_email_until_its_inbox_empties_and_fills_again()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (account, _) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)).Dispose();
        var member = (await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/teams/member-options", Ct)))
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        var teamName = "Ventas " + Guid.NewGuid().ToString("N")[..6];
        var team = await JsonAsync(await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/teams",
            new { name = teamName, inbox = "Customers", memberIds = new[] { member }, notifyAfterMinutes = 10 }, Ct));
        var silent = await JsonAsync(await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/teams",
            new { name = "No avisar " + Guid.NewGuid().ToString("N")[..6], inbox = "Customers", memberIds = new[] { member } }, Ct));
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = WhatsAppAttentionJourney.NewPhone();
        var quiet = WhatsAppAttentionJourney.NewPhone();
        var longAgo = TimeProvider.System.GetUtcNow().AddMinutes(-15);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Texto privado del cliente", Ct, longAgo);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, quiet, "Hola", Ct, longAgo);
        var chat = await TransferAsync(owner, customer, Id(team));
        await TransferAsync(owner, quiet, Id(silent));

        await RunAsync(host);
        var email = Assert.Single(await EmailsAsync(host, account.Email.Value));
        Assert.Contains(teamName, email.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(customer.TrimStart('+'), email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Texto privado", email.HtmlBody, StringComparison.Ordinal);
        await RunAsync(host);
        Assert.Single(await EmailsAsync(host, account.Email.Value));

        // Se vacía (alguien la toma) y vuelve a llenarse: avisa otra vez.
        using (var taken = await owner.PostAsync($"{Customers}/conversations/{chat}/take", null, Ct)) Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        await RunAsync(host);
        var another = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, another, "Hola", Ct, longAgo);
        await TransferAsync(owner, another, Id(team));
        await RunAsync(host);
        Assert.Equal(2, (await EmailsAsync(host, account.Email.Value)).Count);
    }

    private static async Task<Guid> TransferAsync(HttpClient client, string waId, Guid teamId)
    {
        var row = (await JsonAsync(await client.GetAsync($"{Customers}/conversations", Ct))).GetProperty("conversations").GetProperty("items")
            .EnumerateArray().Single(value => value.GetProperty("displayNumber").GetString() == waId);
        using var transferred = await WhatsAppAttentionJourney.PostAsync(client, $"{Customers}/conversations/{Id(row)}/transfer",
            new { teamId, version = row.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
        return Id(row);
    }

    private static Task RunAsync(WebApplicationFactory<Program> host) =>
        host.Services.GetServices<IHostedService>().OfType<WhatsAppWaitingNoticeWorker>().Single().RunOnceAsync(Ct);

    /// <summary>Los correos encolados para esa dirección; los que cifró otro host no se pueden leer y no son de esta prueba.</summary>
    private static async Task<IReadOnlyList<EmailMessage>> EmailsAsync(WebApplicationFactory<Program> host, string address)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "email").ToArrayAsync(Ct);
        var messages = new List<EmailMessage>();
        foreach (var row in rows)
        {
            try { messages.Add(JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(row.EncryptedPayload))!); }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        return [.. messages.Where(message => message.To == address && message.Subject.Contains("esperando", StringComparison.Ordinal))];
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();
}

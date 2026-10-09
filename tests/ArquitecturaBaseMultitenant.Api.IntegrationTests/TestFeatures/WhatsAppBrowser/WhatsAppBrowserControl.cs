using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Control por stdin/stdout privado del runner, siempre sobre ApiFactory Testing y su base propia.</summary>
public sealed class WhatsAppBrowserControl
{
    public const string OperatorEmail = "e8-operator@example.test";
    public const string OperatorPhone = "+5491134567899";
    public const string AppSecret = "browser-fixture-secret";
    public const string NumberId = "browser-fixture-number";
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, (string Phone, DateTime SinceUtc)> _scenarios = new(StringComparer.Ordinal);

    public WhatsAppBrowserControl(IServiceProvider services)
    {
        if (!services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
            throw new InvalidOperationException("Fixture requires Testing.");
        _services = services;
    }

    public async Task<object> ExecuteAsync(WhatsAppBrowserRequest request, HttpClient client, CancellationToken ct)
    {
        if (!request.IsValid()) throw new InvalidOperationException("Invalid fixture request.");
        var nowUtc = _services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (request.Kind == "begin")
        {
            if (!_scenarios.TryAdd(request.Scenario, (request.Phone, nowUtc)))
                throw new InvalidOperationException("Fixture scenario already exists.");
            return new { ok = true };
        }
        if (!_scenarios.TryGetValue(request.Scenario, out var scenario) || scenario.Phone != request.Phone)
            throw new InvalidOperationException("Fixture scenario does not own this destination.");
        if (request.Kind == "inbound")
        {
            var payload = JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account", entry = new[] { new { id = "fixture-waba", changes = new[] { new
                {
                    field = "messages", value = new { messaging_product = "whatsapp", metadata = new { phone_number_id = NumberId },
                        messages = new[] { new { from = request.Phone[1..], id = "wamid.fixture." + request.Scenario,
                            timestamp = new DateTimeOffset(nowUtc).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                            type = "text", text = new { body = "Hola" } } } }
                } } } }
            });
            using var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            message.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(payload))));
            using var response = await client.SendAsync(message, ct);
            return new { status = (int)response.StatusCode };
        }
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (request.Kind == "expireTicket")
        {
            var hash = services.GetRequiredService<ISecureTokenGenerator>().Hash(request.Ticket!);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                var ids = db.LoginMethods.Where(method => method.Type == LoginMethodType.Phone && method.Value == scenario.Phone).Select(method => method.UserId);
                var count = await db.AccountDeletionCancelTickets.Where(ticket => ticket.TokenHash == hash
                        && ids.Contains(ticket.UserId) && ticket.IssuedAtUtc >= scenario.SinceUtc && ticket.ConsumedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(ticket => ticket.ExpiresAtUtc, nowUtc.AddSeconds(-1)), token);
                return count == 1 ? Result.Success() : Result.Failure(Error.Validation("Fixture.Ticket.NotOwned", "Fixture ticket is not owned by this scenario."));
            }, CommitPolicy.OnSuccess, ct);
            if (result.IsFailure) throw new InvalidOperationException("Fixture ticket is not owned by this scenario.");
            return new { ok = true };
        }
        if (request.Kind == "events")
        {
            var contacts = db.Set<WhatsAppContact>().Where(contact => contact.WaId == scenario.Phone).Select(contact => contact.Id);
            var events = await db.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.ContactId != null
                    && contacts.Contains(value.ContactId.Value) && value.ReceivedAtUtc >= scenario.SinceUtc)
                .OrderBy(value => value.ReceivedAtUtc).Select(value => new { kind = value.Kind.ToString(), direction = value.Direction.ToString(), status = value.Status.ToString(), processed = value.ProcessedAtUtc != null }).ToArrayAsync(ct);
            return new { events };
        }
        if (request.Kind == "bot")
        {
            var inbound = services.GetRequiredService<IWhatsAppInboundService>();
            for (var i = 0; i < 100; i++)
            {
                var claim = await inbound.ClaimAsync(ct);
                if (claim.IsFailure) throw new InvalidOperationException("Fixture claim failed.");
                if (claim.Value is null) break;
                if ((await inbound.ProcessAsync(claim.Value, ct)).IsFailure) throw new InvalidOperationException("Fixture bot failed.");
            }
        }
        var channel = request.Kind == "operatorCode" ? "email" : "whatsapp";
        var destination = request.Kind == "operatorCode" ? OperatorEmail : scenario.Phone;
        var protector = services.GetRequiredService<IPayloadProtector>();
        var rows = await db.OutboxMessages.AsNoTracking().Where(row => row.Channel == channel && row.EncryptedPayload != "" && row.CreatedAtUtc >= scenario.SinceUtc)
            .OrderByDescending(row => row.CreatedAtUtc).ToArrayAsync(ct);
        foreach (var row in rows)
        {
            using var payload = JsonDocument.Parse(protector.Unprotect(row.EncryptedPayload));
            var value = payload.RootElement;
            if (value.GetProperty("To").GetString() != destination) continue;
            string? proof = null;
            if (request.Kind == "bot" && value.TryGetProperty("LinkUrl", out var url) && url.ValueKind == JsonValueKind.String) proof = url.GetString();
            if (request.Kind == "code" && value.TryGetProperty("TemplateName", out var template) && template.GetString() == "codigo_ingreso") proof = value.GetProperty("BodyParameters")[0].GetString();
            if (request.Kind == "operatorCode")
            {
                var match = Regex.Match(value.GetProperty("TextBody").GetString() ?? "", @"(?m)^(?<code>\d{6})\r?$", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (match.Success) proof = match.Groups["code"].Value;
            }
            if (proof is not null) return new { proof };
        }
        throw new InvalidOperationException("Fixture proof not found.");
    }
}

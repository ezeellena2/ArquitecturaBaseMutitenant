using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>
/// Controles del recorrido de la E11: un número de organización sin Meta, lo que escribe la persona, el turno del bot con el
/// worker real y lo que salió de la conversación, sin textos. Cada escenario solo ve su número y su teléfono.
/// </summary>
public sealed partial class WhatsAppBrowserControl
{
    public const string OrganizationNumberPrefix = "browser-fixture-org-";
    public const string OrganizationWabaId = "browser-fixture-waba";
    public const string OrganizationDisplayNumber = "+5491134567700";
    private const string OrganizationToken = "browser-fixture-org-token";
    private readonly Dictionary<string, OrganizationNumber> _organizationNumbers = new(StringComparer.Ordinal);

    private async Task<object> OrganizationAsync(WhatsAppBrowserRequest request, ScenarioCursor scenario, HttpClient client,
        DateTime nowUtc, CancellationToken ct)
    {
        if (request.Kind == "orgNumber") return new { numberId = await OrganizationNumberAsync(request, ct) };
        if (!_organizationNumbers.TryGetValue(request.Scenario, out var number))
            throw new InvalidOperationException("Fixture scenario has no organization number.");
        return request.Kind switch
        {
            "orgInbound" => new { status = await OrganizationInboundAsync(request, number, scenario, client, nowUtc, ct) },
            "orgProcess" => await OrganizationProcessAsync(number, scenario, ct),
            _ => await OrganizationOutboundAsync(number, scenario, ct),
        };
    }

    /// <summary>
    /// Un número Connected con el creador real (token cifrado y su evento), sin pasar por Meta. Su bot atiende todos los
    /// días de 00:00 a 23:59 en la zona de la organización, ofrece vincular la cuenta y no tiene menú. El módulo no se toca.
    /// </summary>
    private async Task<Guid> OrganizationNumberAsync(WhatsAppBrowserRequest request, CancellationToken ct)
    {
        var tenantId = request.TenantId!.Value;
        if (_organizationNumbers.TryGetValue(request.Scenario, out var known))
            return known.TenantId == tenantId ? known.ChannelId : throw new InvalidOperationException("Fixture scenario owns another organization.");
        var phoneNumberId = OrganizationNumberPrefix + request.Scenario;
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenant = await services.GetRequiredService<ApplicationDbContext>().Tenants.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == tenantId, ct);
        if (tenant is not { Kind: TenantKind.Business }) throw new InvalidOperationException("Fixture organization not found.");
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
        {
            var channels = services.GetRequiredService<IWhatsAppChannelRepository>();
            await channels.LockNumberAsync(phoneNumberId, work);
            if (await channels.GetActiveAsync(phoneNumberId, work) is { } existing)
                return existing.OrganizationId == tenantId ? Result.Success(existing.Id) : Result.Failure<Guid>(WhatsAppErrors.NumberAlreadyConnected);
            var channel = await services.GetRequiredService<WhatsAppChannelCreator>().ConnectOrganizationAsync(null,
                new WhatsAppSignupInput("browser-fixture-code", phoneNumberId, OrganizationWabaId, Coexistence: false),
                new WhatsAppSignupResult(phoneNumberId, OrganizationToken, null, PhoneNumber.Create(OrganizationDisplayNumber).Value,
                    "Fixture", HasPayment: true), work);
            if (channel.IsFailure) return Result.Failure<Guid>(channel.Error);
            var bot = await services.GetRequiredService<WhatsAppBotResolver>().OrganizationAsync(channel.Value.Id, work);
            var configured = bot.Update(bot.Welcome, bot.OutOfHours, bot.TimeZoneId,
                Enum.GetValues<DayOfWeek>().Select(day => new WhatsAppBotHours(day, new TimeOnly(0, 0), new TimeOnly(23, 59))), [],
                askPromotions: false, offerAccountLink: true, employeesTeamId: null);
            if (configured.IsFailure) return Result.Failure<Guid>(configured.Error);
            services.GetRequiredService<IWhatsAppBotRepository>().Add(bot);
            return Result.Success(channel.Value.Id);
        }, CommitPolicy.OnSuccess, ct);
        if (created.IsFailure) throw new InvalidOperationException("Fixture organization number was not created.");
        _organizationNumbers.Add(request.Scenario, new OrganizationNumber(tenantId, created.Value, phoneNumberId));
        return created.Value;
    }

    /// <summary>Lo que escribe o toca la persona, firmado como Meta al webhook real del número de la organización.</summary>
    private async Task<int> OrganizationInboundAsync(WhatsAppBrowserRequest request, OrganizationNumber number, ScenarioCursor scenario,
        HttpClient client, DateTime nowUtc, CancellationToken ct)
    {
        var message = new Dictionary<string, object>
        {
            ["from"] = scenario.Phone[1..], ["id"] = "wamid.fixture.org." + Guid.NewGuid().ToString("N"),
            ["timestamp"] = new DateTimeOffset(nowUtc).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (request.Text is { } text) { message["type"] = "text"; message["text"] = new { body = text }; }
        else
        {
            var reply = request.Reply!;
            // Lo que eligió llega con el texto que vio, como en WhatsApp.
            var title = (await BotTurnsAsync(number, scenario, ct)).SelectMany(Choices).LastOrDefault(choice => choice.Id == reply.Id)?.Title ?? reply.Id;
            if (reply.Id == BotButtons.Unsubscribe) { message["type"] = "button"; message["button"] = new { payload = reply.Id, text = title }; }
            else
            {
                message["type"] = "interactive";
                message["interactive"] = reply.Type == "list"
                    ? new Dictionary<string, object> { ["type"] = "list_reply", ["list_reply"] = new { id = reply.Id, title } }
                    : new Dictionary<string, object> { ["type"] = "button_reply", ["button_reply"] = new { id = reply.Id, title } };
            }
        }
        var payload = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account", entry = new[] { new { id = OrganizationWabaId, changes = new[] { new
            {
                field = "messages", value = new { messaging_product = "whatsapp",
                    metadata = new { display_phone_number = OrganizationDisplayNumber[1..], phone_number_id = number.PhoneNumberId },
                    messages = new[] { message } },
            } } } },
        });
        using var webhook = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        webhook.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(payload))));
        using var response = await client.SendAsync(webhook, ct);
        return (int)response.StatusCode;
    }

    /// <summary>
    /// Procesa la cola con el worker real (cada número de organización en su scope, con SetFromAccess) y devuelve el último
    /// turno del bot a ese teléfono: si en un mismo turno mandó varios (la bienvenida y la oferta de vincular), van juntos en
    /// ese orden.
    /// </summary>
    private async Task<object> OrganizationProcessAsync(OrganizationNumber number, ScenarioCursor scenario, CancellationToken ct)
    {
        var processor = _services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().SingleOrDefault()
            ?? throw new InvalidOperationException("Fixture inbound worker is not registered.");
        await processor.ProcessPendingAsync(ct);
        var turns = await BotTurnsAsync(number, scenario, ct);
        var last = turns.Count == 0 ? [] : turns[^1];
        return new
        {
            text = last.Count == 0 ? null : string.Join("\n\n", last.Select(message => message.Body)),
            list = last.SelectMany(message => message.ListRows ?? []).Select(row => new { id = row.Id, title = row.Title }).ToArray(),
            buttons = last.SelectMany(message => message.ReplyButtons ?? []).Select(button => new { id = button.Id, title = button.Title }).ToArray(),
        };
    }

    /// <summary>Lo que salió de la conversación (tiene envío en el outbox), sin textos: una nota nunca aparece.</summary>
    private async Task<object> OrganizationOutboundAsync(OrganizationNumber number, ScenarioCursor scenario, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(number.TenantId);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var rows = await (from message in Messages(db, number, scenario)
                          where message.OutboxMessageId != null
                          from outbox in db.OutboxMessages.AsNoTracking().Where(outbox => outbox.Id == message.OutboxMessageId).DefaultIfEmpty()
                          orderby message.CreatedAtUtc, message.Id
                          select new { message.Kind, message.Status, Outbox = outbox == null ? (OutboxStatus?)null : outbox.Status }).ToArrayAsync(ct);
        return rows.Select(row => new
        {
            kind = row.Kind.ToString(),
            status = (row.Outbox is OutboxStatus.Failed or OutboxStatus.Cancelled ? WhatsAppChatMessageStatus.Failed
                : row.Outbox == OutboxStatus.Sent ? WhatsAppChatMessageStatus.Sent : row.Status).ToString(),
        }).ToArray();
    }

    // Los turnos del bot (lo que salió sin autor) desde el comienzo del escenario, del más viejo al más nuevo. Un turno comparte
    // su instante; dentro de él, la respuesta va antes que las preguntas con botones y la oferta de vincular, al final.
    private async Task<IReadOnlyList<IReadOnlyList<WhatsAppOutboundMessage>>> BotTurnsAsync(OrganizationNumber number, ScenarioCursor scenario,
        CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(number.TenantId);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var sent = await (from message in Messages(db, number, scenario)
                          where message.AuthorUserId == null && message.OutboxMessageId != null
                          join outbox in db.OutboxMessages.AsNoTracking() on message.OutboxMessageId equals outbox.Id
                          select new { message.CreatedAtUtc, outbox.EncryptedPayload }).ToArrayAsync(ct);
        var protector = services.GetRequiredService<IPayloadProtector>();
        return [.. sent.GroupBy(row => row.CreatedAtUtc).OrderBy(turn => turn.Key).Select(turn => (IReadOnlyList<WhatsAppOutboundMessage>)[.. turn
            .Select(row => JsonSerializer.Deserialize<WhatsAppOutboundMessage>(protector.Unprotect(row.EncryptedPayload))!)
            .Where(message => message.PhoneNumberId == number.PhoneNumberId && message.To == scenario.Phone)
            .OrderBy(message => message.ReplyButtons is { Count: > 0 })
            .ThenBy(message => message.ReplyButtons?.Any(button => button.Id == BotButtons.LinkAccount) == true)])
            .Where(turn => turn.Count > 0)];
    }

    private static IQueryable<WhatsAppChatMessage> Messages(ApplicationDbContext db, OrganizationNumber number, ScenarioCursor scenario)
    {
        var chats = db.Set<WhatsAppChat>().AsNoTracking().Where(chat => chat.ChannelId == number.ChannelId && chat.WaId == scenario.Phone)
            .Select(chat => chat.Id);
        return db.Set<WhatsAppChatMessage>().AsNoTracking().Where(message => chats.Contains(message.ChatId)
            && message.Direction == WhatsAppMessageDirection.Outbound && message.CreatedAtUtc >= scenario.SinceUtc);
    }

    private static IEnumerable<WhatsAppReplyButton> Choices(IReadOnlyList<WhatsAppOutboundMessage> turn) =>
        turn.SelectMany(message => (message.ListRows ?? []).Concat(message.ReplyButtons ?? []));

    private sealed record OrganizationNumber(Guid TenantId, Guid ChannelId, string PhoneNumberId)
    {
        public override string ToString() => nameof(OrganizationNumber);
    }
}

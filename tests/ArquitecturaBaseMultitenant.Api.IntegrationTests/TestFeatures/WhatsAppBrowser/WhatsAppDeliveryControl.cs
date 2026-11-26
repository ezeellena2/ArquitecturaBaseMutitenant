using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>
/// Orden privada <c>delivery</c> del recorrido para un mensaje del chat (I3 de la base 2, B-2): deja «Sin confirmar» el
/// último mensaje que salió de la conversación de un teléfono de prueba, en una organización o en las Consultas de
/// Plataforma, y le borra la revisión para que se pueda volver a revisar. En el entorno aislado WhatsApp no sale nunca.
/// Plataforma no tiene en el recorrido una consulta propia (su número atiende el ingreso de la E8, con las consultas
/// apagadas): si el teléfono no tiene una, la orden arma la mínima en el número de Plataforma, con lo que escribió la persona
/// y la respuesta, sin prender las consultas.
/// </summary>
public static class WhatsAppDeliveryControl
{
    public const string ConsultationText = "Hola, no puedo entrar a mi cuenta.";
    public const string ConsultationReply = "Ya lo revisamos: probá de nuevo en unos minutos.";

    /// <summary><c>{ kind: "chatMessage", scope: "organization" | "platform", phone, outcome: "Uncertain", tenantId? }</c>.</summary>
    public static async Task<object> ExecuteAsync(IServiceProvider services, JsonElement command, Guid businessId, CancellationToken ct)
    {
        RecorridoCommandArguments.Allow(command, "kind", "scope", "phone", "outcome", "tenantId");
        if (RecorridoCommandArguments.Text(command, "kind") != "chatMessage" || RecorridoCommandArguments.Text(command, "outcome") != "Uncertain")
            throw new InvalidOperationException("Unsupported isolated chat delivery command.");
        var phone = RecorridoCommandArguments.SyntheticPhone(RecorridoCommandArguments.Text(command, "phone"));
        var platform = RecorridoCommandArguments.Text(command, "scope") switch
        {
            "organization" => false,
            "platform" => true,
            _ => throw new InvalidOperationException("Unsupported isolated chat scope."),
        };
        var tenantId = RecorridoCommandArguments.OptionalGuid(command, "tenantId");
        if (platform && tenantId is not null) throw new InvalidOperationException("Platform consultations have no organization.");

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        RecorridoCommandArguments.RequireTesting(provider);
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var (chatId, messageId, outboxId) = platform
            ? await PlatformAsync(provider, db, phone, nowUtc, ct)
            : await OrganizationAsync(provider, db, tenantId ?? businessId, phone, ct);
        // Meta manda: lo que ya aceptó no se lee «Sin confirmar».
        if (await db.Set<WhatsAppMessage>().AsNoTracking().AnyAsync(row => row.EventType == "accepted" && row.OutboxMessageId == outboxId, ct))
            throw new InvalidOperationException("The isolated message was already accepted.");
        if (await RecorridoInvitationDelivery.SetAsync(db, outboxId, OutboxStatus.Uncertain, OutboxMessage.InterruptedCode, nowUtc, ct) != 1)
            throw new InvalidOperationException("The isolated message delivery could not be changed.");
        return new { chatId, messageId };
    }

    private static async Task<(Guid Chat, Guid Message, Guid Outbox)> OrganizationAsync(IServiceProvider provider, ApplicationDbContext db,
        Guid tenantId, PhoneNumber phone, CancellationToken ct)
    {
        using var lease = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var chatId = await db.Set<WhatsAppChat>().AsNoTracking().Where(chat => chat.TenantId == tenantId && chat.WaId == phone.Value)
            .OrderByDescending(chat => chat.LastMessageAtUtc).Select(chat => (Guid?)chat.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The isolated organization has no conversation with this phone.");
        var message = await LastOutbound(db.Set<WhatsAppChatMessage>().Where(row => row.TenantId == tenantId && row.ChatId == chatId))
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("The isolated conversation has nothing sent.");
        await db.Set<WhatsAppChatMessage>().Where(row => row.TenantId == tenantId && row.Id == message.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.ReviewedByUserId, (Guid?)null).SetProperty(row => row.ReviewedByName, (string?)null)
            .SetProperty(row => row.ReviewedAtUtc, (DateTime?)null), ct);
        return (chatId, message.Id, message.OutboxId);
    }

    private static async Task<(Guid Chat, Guid Message, Guid Outbox)> PlatformAsync(IServiceProvider provider, ApplicationDbContext db,
        PhoneNumber phone, DateTime nowUtc, CancellationToken ct)
    {
        var channelId = await db.Set<WhatsAppChannel>().AsNoTracking()
            .Where(channel => channel.OrganizationId == null && channel.Status != WhatsAppNumberStatus.Disconnected)
            .OrderByDescending(channel => channel.ConnectedAtUtc).Select(channel => (Guid?)channel.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The isolated platform number is not connected.");
        var chatId = await db.Set<PlatformWhatsAppChat>().AsNoTracking().Where(chat => chat.ChannelId == channelId && chat.WaId == phone.Value)
            .Select(chat => (Guid?)chat.Id).SingleOrDefaultAsync(ct);
        var sent = chatId is { } existing
            ? await LastOutbound(db.Set<PlatformWhatsAppChatMessage>().Where(row => row.ChatId == existing)).FirstOrDefaultAsync(ct) : null;
        if (sent is null)
        {
            chatId = await StartConsultationAsync(provider, db, channelId, phone, nowUtc, ct);
            sent = await LastOutbound(db.Set<PlatformWhatsAppChatMessage>().Where(row => row.ChatId == chatId)).SingleAsync(ct);
        }
        await db.Set<PlatformWhatsAppChatMessage>().Where(row => row.Id == sent.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.ReviewedByUserId, (Guid?)null).SetProperty(row => row.ReviewedByName, (string?)null)
            .SetProperty(row => row.ReviewedAtUtc, (DateTime?)null), ct);
        return (chatId!.Value, sent.Id, sent.OutboxId);
    }

    /// <summary>
    /// La consulta mínima, con las piezas reales: la conversación, lo que escribió la persona y la respuesta en su mensaje de
    /// la cola, que nadie despacha. Como la entrada de <c>PlatformWhatsAppInboundWriter</c>, sin el bot ni las consultas.
    /// </summary>
    private static async Task<Guid> StartConsultationAsync(IServiceProvider provider, ApplicationDbContext db, Guid channelId,
        PhoneNumber phone, DateTime nowUtc, CancellationToken ct)
    {
        var started = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
        {
            var chats = provider.GetRequiredService<IPlatformWhatsAppChatRepository>();
            await chats.LockAsync(channelId, phone.Value, token);
            var chat = await chats.GetAsync(channelId, phone.Value, token);
            if (chat is null)
            {
                chat = PlatformWhatsAppChat.Create(channelId, phone, nowUtc);
                chats.Add(chat);
            }
            chats.Add(PlatformWhatsAppChatMessage.Inbound(chat.Id, $"wamid.recorrido.{Guid.NewGuid():N}", ConsultationText, null, nowUtc));
            chat.RecordInbound(nowUtc, ConsultationText);
            var outbox = OutboxMessage.Enqueue("whatsapp", provider.GetRequiredService<IPayloadProtector>().Protect("{}"), nowUtc);
            db.OutboxMessages.Add(outbox);
            var repliedAtUtc = nowUtc.AddSeconds(1);
            chats.Add(PlatformWhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Text, null, null, ConsultationReply,
                outbox.Id, null, repliedAtUtc));
            chat.RecordOutbound(repliedAtUtc, ConsultationReply);
            return Result.Success(chat.Id);
        }, CommitPolicy.OnSuccess, ct);
        return started.IsSuccess ? started.Value : throw new InvalidOperationException("The isolated consultation could not start.");
    }

    private static IQueryable<Sent> LastOutbound<T>(IQueryable<T> messages) where T : WhatsAppChatMessageBase =>
        messages.AsNoTracking()
            .Where(row => row.Direction == WhatsAppMessageDirection.Outbound && row.Kind != WhatsAppChatMessageKind.Note && row.OutboxMessageId != null)
            .OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id)
            .Select(row => new Sent(row.Id, row.OutboxMessageId!.Value));

    private sealed record Sent(Guid Id, Guid OutboxId);
}

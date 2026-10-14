using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppWebhookWriter(IWhatsAppContactRepository contacts, IWhatsAppMessageRepository messages,
    IUserLookup lookup, IExternalLoginLock identities, ILoginMethodRepository methods, IPayloadProtector protector,
    WhatsAppChannelUsageWriter usage, TimeProvider clock)
{
    internal async Task<Result> WriteAsync(WhatsAppWebhookBatch received, CancellationToken ct)
    {
        // Solo la entrada de un número vigente (de Plataforma o de una organización); los estados se filtran bajo su lock.
        var routes = await usage.FindActiveAsync(received.Messages.Select(value => value.PhoneNumberId), ct);
        var batch = received with { Messages = [.. received.Messages.Where(value => routes.ContainsKey(value.PhoneNumberId))] };
        var senders = batch.Messages.Select(value => value.Sender).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var sender in senders) await identities.LockPhoneAsync(PhoneNumber.Create(sender).Value, ct);
        var owners = new Dictionary<string, LoginMethodLookup?>(StringComparer.Ordinal);
        foreach (var sender in senders)
        {
            var owner = await lookup.FindMethodAsync(LoginMethodType.Phone, sender, ct);
            owners[sender] = owner is { VerifiedAtUtc: not null } ? owner : null;
        }
        foreach (var userId in owners.Values.OfType<LoginMethodLookup>().Select(value => value.UserId).Distinct().Order()) await methods.LockUserAsync(userId, ct);
        foreach (var sender in senders) await contacts.LockAsync(sender, ct);
        var streams = batch.Messages.Select(value => (value.PhoneNumberId, value.Wamid))
            .Concat(batch.Statuses.Select(value => (value.PhoneNumberId, value.Wamid))).Distinct()
            .OrderBy(value => value.PhoneNumberId, StringComparer.Ordinal).ThenBy(value => value.Wamid, StringComparer.Ordinal);
        foreach (var (number, id) in streams) await messages.LockStreamAsync(number, id, ct);
        var activeNumbers = await usage.LockActiveAsync(batch.Statuses, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var resolved = new Dictionary<string, WhatsAppContact>(StringComparer.Ordinal);
        var seen = new HashSet<(string, string, string)>();
        foreach (var inbound in batch.Messages.OrderBy(value => value.OccurredAtUtc))
        {
            if (!seen.Add((inbound.PhoneNumberId, inbound.Wamid, "inbound"))
                || await messages.GetEventAsync(inbound.PhoneNumberId, inbound.Wamid, "inbound", ct) is not null) continue;
            if (!resolved.TryGetValue(inbound.Sender, out var contact))
            {
                contact = await contacts.GetAsync(inbound.Sender, ct);
                if (contact is null) { contact = WhatsAppContact.Create(PhoneNumber.Create(inbound.Sender).Value, nowUtc); contacts.Add(contact); }
                var owner = await lookup.FindMethodAsync(LoginMethodType.Phone, inbound.Sender, ct);
                owners[inbound.Sender] = owner is { VerifiedAtUtc: not null } ? owner : null;
                if (owners[inbound.Sender] is { } verified) contact.Link(verified.MethodId, verified.UserId);
                else contact.Unlink();
                resolved.Add(inbound.Sender, contact);
            }
            contact.RecordInbound(inbound.OccurredAtUtc > nowUtc ? nowUtc : inbound.OccurredAtUtc);
            var route = routes[inbound.PhoneNumberId];
            // El número de Plataforma no conserva códigos ni enlaces; el de una organización guarda el texto del cliente.
            var text = route.OrganizationId is null ? WhatsAppSafeTextPolicy.Retain(inbound.Text) : inbound.Text;
            var original = owners[inbound.Sender];
            var kind = inbound.Text is not null && text is null ? WhatsAppMessageKind.Authentication : inbound.Kind;
            messages.Add(WhatsAppMessage.Create(inbound.PhoneNumberId, inbound.Wamid, "inbound", WhatsAppMessageDirection.Inbound,
                kind, WhatsAppMessageStatus.Pending, inbound.OccurredAtUtc, nowUtc, contact.Id,
                text is null ? null : protector.Protect(text), originalUserId: original?.UserId,
                originalMethodId: original?.MethodId, replyId: inbound.ReplyId, channelId: route.Id, contentType: inbound.ContentType));
        }
        var delivered = new HashSet<(string, string)>();
        foreach (var status in batch.Statuses.Where(value => activeNumbers.Contains(value.PhoneNumberId)))
        {
            var eventType = status.Status.ToString().ToLowerInvariant();
            if (!seen.Add((status.PhoneNumberId, status.Wamid, eventType))
                || await messages.GetEventAsync(status.PhoneNumberId, status.Wamid, eventType, ct) is not null) continue;
            var accepted = await messages.GetEventAsync(status.PhoneNumberId, status.Wamid, "accepted", ct);
            var callback = WhatsAppMessage.Create(status.PhoneNumberId, status.Wamid, eventType, WhatsAppMessageDirection.Outbound,
                WhatsAppMessageKind.Delivery, status.Status, status.OccurredAtUtc, nowUtc,
                outboxMessageId: accepted?.OutboxMessageId, originalUserId: accepted?.OriginalUserId);
            callback.MarkOutboundAccepted(nowUtc); messages.Add(callback);
            accepted?.ApplyDeliveryStatus(status.Status, status.OccurredAtUtc);
            // Leído implica entregado: se cuenta una sola vez por mensaje, sea cual sea el primero que llegue.
            var firstDelivery = status.Status is WhatsAppMessageStatus.Delivered or WhatsAppMessageStatus.Read
                && delivered.Add((status.PhoneNumberId, status.Wamid))
                && await messages.GetEventAsync(status.PhoneNumberId, status.Wamid,
                    status.Status == WhatsAppMessageStatus.Delivered ? "read" : "delivered", ct) is null;
            await usage.RecordAsync(status, firstDelivery, ct);
        }
        return Result.Success();
    }
}

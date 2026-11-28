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
    internal async Task<Result<IReadOnlyList<WhatsAppDeliveryFailure>>> WriteAsync(WhatsAppWebhookBatch received, CancellationToken ct)
    {
        // Solo la entrada de un número vigente (de Plataforma o de una organización); los ecos solo de una organización; los estados bajo su lock.
        var routes = await usage.FindActiveAsync(
            received.Messages.Select(value => value.PhoneNumberId)
                .Concat(received.MessageEchoes.Select(value => value.PhoneNumberId)), ct);
        var batch = received with
        {
            Messages = [.. received.Messages.Where(value => routes.ContainsKey(value.PhoneNumberId))],
            Echoes = [.. received.MessageEchoes.Where(value => routes.TryGetValue(value.PhoneNumberId, out var route) && route.OrganizationId is not null)],
        };
        var phones = batch.Messages.Select(value => value.Sender)
            .Concat(batch.MessageEchoes.Select(value => value.Recipient))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var phone in phones) await identities.LockPhoneAsync(PhoneNumber.Create(phone).Value, ct);
        var owners = new Dictionary<string, LoginMethodLookup?>(StringComparer.Ordinal);
        foreach (var phone in phones)
        {
            var owner = await lookup.FindMethodAsync(LoginMethodType.Phone, phone, ct);
            owners[phone] = owner is { VerifiedAtUtc: not null } ? owner : null;
        }
        foreach (var userId in owners.Values.OfType<LoginMethodLookup>().Select(value => value.UserId).Distinct().Order()) await methods.LockUserAsync(userId, ct);
        foreach (var phone in phones) await contacts.LockAsync(phone, ct);
        var streams = batch.Messages.Select(value => (value.PhoneNumberId, value.Wamid))
            .Concat(batch.MessageEchoes.Select(value => (value.PhoneNumberId, value.Wamid)))
            .Concat(batch.Statuses.Select(value => (value.PhoneNumberId, value.Wamid))).Distinct()
            .OrderBy(value => value.PhoneNumberId, StringComparer.Ordinal).ThenBy(value => value.Wamid, StringComparer.Ordinal);
        foreach (var (number, id) in streams) await messages.LockStreamAsync(number, id, ct);
        var activeChannels = await usage.LockActiveAsync(batch.Statuses, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var resolved = new Dictionary<string, WhatsAppContact>(StringComparer.Ordinal);
        var seen = new HashSet<(string, string, string)>();
        await WriteInboundAsync(batch.Messages, routes, resolved, owners, seen, nowUtc, ct);
        await WriteEchoesAsync(batch.MessageEchoes, routes, resolved, owners, seen, nowUtc, ct);
        var delivered = new HashSet<(string, string)>();
        var failures = new List<WhatsAppDeliveryFailure>();
        foreach (var status in batch.Statuses)
        {
            if (!activeChannels.TryGetValue(status.PhoneNumberId, out var channel)) continue;
            var eventType = status.Status.ToString().ToLowerInvariant();
            if (!seen.Add((status.PhoneNumberId, status.Wamid, eventType))
                || await messages.GetEventAsync(status.PhoneNumberId, status.Wamid, eventType, ct) is not null) continue;
            var accepted = await messages.GetEventAsync(status.PhoneNumberId, status.Wamid, "accepted", ct);
            var callback = WhatsAppMessage.Create(status.PhoneNumberId, status.Wamid, eventType, WhatsAppMessageDirection.Outbound,
                WhatsAppMessageKind.Delivery, status.Status, status.OccurredAtUtc, nowUtc,
                outboxMessageId: accepted?.OutboxMessageId, originalUserId: accepted?.OriginalUserId);
            callback.MarkOutboundAccepted(nowUtc); messages.Add(callback);
            if (accepted?.ApplyDeliveryStatus(status.Status, status.OccurredAtUtc) == true
                && status.Status == WhatsAppMessageStatus.Failed && accepted.OutboxMessageId is { } outboxMessageId)
                await RecordFailureAsync(channel, status, outboxMessageId, failures, ct);
            // Leído implica entregado: se cuenta una sola vez por mensaje, sea cual sea el primero que llegue.
            var firstDelivery = status.Status is WhatsAppMessageStatus.Delivered or WhatsAppMessageStatus.Read
                && delivered.Add((status.PhoneNumberId, status.Wamid))
                && await messages.GetEventAsync(status.PhoneNumberId, status.Wamid,
                    status.Status == WhatsAppMessageStatus.Delivered ? "read" : "delivered", ct) is null;
            await usage.RecordAsync(status, firstDelivery, ct);
        }
        return Result.Success<IReadOnlyList<WhatsAppDeliveryFailure>>(failures);
    }

    private async Task WriteInboundAsync(IReadOnlyList<WhatsAppWebhookMessage> inboundMessages,
        IReadOnlyDictionary<string, WhatsAppChannel> routes, Dictionary<string, WhatsAppContact> resolved,
        Dictionary<string, LoginMethodLookup?> owners, HashSet<(string, string, string)> seen, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var inbound in inboundMessages.OrderBy(value => value.OccurredAtUtc))
        {
            if (!seen.Add((inbound.PhoneNumberId, inbound.Wamid, WhatsAppMessage.InboundEventType))
                || await messages.GetEventAsync(inbound.PhoneNumberId, inbound.Wamid, WhatsAppMessage.InboundEventType, ct) is not null) continue;
            var contact = await ResolveContactAsync(inbound.Sender, resolved, owners, nowUtc, ct);
            contact.RecordInbound(inbound.OccurredAtUtc > nowUtc ? nowUtc : inbound.OccurredAtUtc);
            var route = routes[inbound.PhoneNumberId];
            // El número de Plataforma no conserva códigos ni enlaces; el de una organización guarda el texto del cliente.
            var text = route.OrganizationId is null ? WhatsAppSafeTextPolicy.Retain(inbound.Text) : inbound.Text;
            // El nombre del perfil solo completa un contacto de la organización (C14 de la E12): el de Plataforma no lo guarda.
            var profileName = route.OrganizationId is null ? null : inbound.ProfileName;
            var original = owners[inbound.Sender];
            var kind = inbound.Text is not null && text is null ? WhatsAppMessageKind.Authentication : inbound.Kind;
            messages.Add(WhatsAppMessage.Create(inbound.PhoneNumberId, inbound.Wamid, WhatsAppMessage.InboundEventType, WhatsAppMessageDirection.Inbound,
                kind, WhatsAppMessageStatus.Pending, inbound.OccurredAtUtc, nowUtc, contact.Id,
                text is null ? null : protector.Protect(text), originalUserId: original?.UserId,
                originalMethodId: original?.MethodId, replyId: inbound.ReplyId, channelId: route.Id, contentType: inbound.ContentType,
                encryptedProfileName: profileName is null ? null : protector.Protect(profileName)));
        }
    }

    private async Task WriteEchoesAsync(IReadOnlyList<WhatsAppWebhookEcho> echoes,
        IReadOnlyDictionary<string, WhatsAppChannel> routes, Dictionary<string, WhatsAppContact> resolved,
        Dictionary<string, LoginMethodLookup?> owners, HashSet<(string, string, string)> seen, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var echo in echoes.OrderBy(value => value.OccurredAtUtc))
        {
            if (!seen.Add((echo.PhoneNumberId, echo.Wamid, WhatsAppMessage.EchoEventType))
                || await messages.GetEventAsync(echo.PhoneNumberId, echo.Wamid, WhatsAppMessage.EchoEventType, ct) is not null) continue;
            var contact = await ResolveContactAsync(echo.Recipient, resolved, owners, nowUtc, ct);
            var route = routes[echo.PhoneNumberId];
            messages.Add(WhatsAppMessage.Create(echo.PhoneNumberId, echo.Wamid, WhatsAppMessage.EchoEventType, WhatsAppMessageDirection.Outbound,
                echo.Kind, WhatsAppMessageStatus.Pending, echo.OccurredAtUtc, nowUtc, contact.Id,
                echo.Text is null ? null : protector.Protect(echo.Text), channelId: route.Id, contentType: echo.ContentType));
        }
    }

    private async Task<WhatsAppContact> ResolveContactAsync(string phone, Dictionary<string, WhatsAppContact> resolved,
        Dictionary<string, LoginMethodLookup?> owners, DateTime nowUtc, CancellationToken ct)
    {
        if (resolved.TryGetValue(phone, out var contact)) return contact;
        contact = await contacts.GetAsync(phone, ct);
        if (contact is null) { contact = WhatsAppContact.Create(PhoneNumber.Create(phone).Value, nowUtc); contacts.Add(contact); }
        var owner = await lookup.FindMethodAsync(LoginMethodType.Phone, phone, ct);
        owners[phone] = owner is { VerifiedAtUtc: not null } ? owner : null;
        if (owners[phone] is { } verified) contact.Link(verified.MethodId, verified.UserId);
        else contact.Unlink();
        resolved.Add(phone, contact);
        return contact;
    }

    internal Task RecordDeliveryFailuresAsync(IReadOnlyList<WhatsAppDeliveryFailure> failures, CancellationToken ct) =>
        usage.RecordOrganizationDeliveryFailuresAsync(failures, ct);

    private Task RecordFailureAsync(WhatsAppChannel channel, WhatsAppWebhookStatus status, Guid outboxMessageId,
        List<WhatsAppDeliveryFailure> failures, CancellationToken ct)
    {
        var failureReason = WhatsAppSendFailures.ForWebhookError(status.ErrorCode);
        if (channel.OrganizationId is { } organizationId)
        {
            failures.Add(new(organizationId, outboxMessageId, status.Wamid, failureReason, status.OccurredAtUtc));
            return Task.CompletedTask;
        }
        return usage.RecordPlatformDeliveryFailureAsync(outboxMessageId, status.Wamid, failureReason, ct);
    }
}

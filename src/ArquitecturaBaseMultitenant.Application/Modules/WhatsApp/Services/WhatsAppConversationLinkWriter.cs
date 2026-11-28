using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Vincula una conversación de clientes con la cuenta de quien escribe y le copia los mensajes (D2, D5), dentro de la
/// transacción del lado de la organización. Solo se ofrece y se crea si el número es un teléfono verificado de una cuenta
/// activa con espacio personal; lo que entra y lo que sale solo se copian mientras el número siga verificado en esa cuenta.
/// </summary>
internal sealed class WhatsAppConversationLinkWriter(IWhatsAppConversationLinkRepository links, IUserTenantAccessReader accesses,
    ITenantSettingsReader settings, IWhatsAppChannelRepository channels, IUserLookup accounts)
{
    /// <summary>Si a quien escribe se le puede ofrecer vincular esta conversación: es la dueña verificada y no está vinculada.</summary>
    internal async Task<bool> CanLinkAsync(WhatsAppChat chat, WhatsAppInboundContext context, CancellationToken ct) =>
        chat.Inbox == WhatsAppInbox.Customers && Owner(context) is { } account && await PersonalSpaceAsync(account, ct) is not null
        && await links.GetByChatAsync(chat.Id, ct) is null;

    internal async Task<bool> LinkAsync(WhatsAppChat chat, WhatsAppInboundContext context, DateTime nowUtc, CancellationToken ct)
    {
        if (chat.Inbox != WhatsAppInbox.Customers || Owner(context) is not { } account || await links.GetByChatAsync(chat.Id, ct) is not null
            || await PersonalSpaceAsync(account, ct) is not { } personal) return false;
        var organization = (await settings.FindCurrentAsync(ct))?.TenantName;
        var channel = await channels.GetAsync(chat.ChannelId, ct);
        if (string.IsNullOrWhiteSpace(organization) || channel?.DisplayNumber is not { } number) return false;
        links.Add(WhatsAppConversationLink.Create(personal, chat.Id, account, context.OriginalAccount!.DisplayName, organization, number, nowUtc));
        return true;
    }

    /// <summary>Copia un mensaje que se dijo con la persona solo mientras el número del chat siga verificado en la cuenta vinculada.</summary>
    internal async Task CopyAsync(Guid chatId, WhatsAppMessageDirection direction, string? text, WhatsAppMediaKind? mediaKind, DateTime atUtc,
        Guid? senderAccountId, CancellationToken ct)
    {
        if (await links.GetByChatAsync(chatId, ct) is not { } link) return;
        if (direction == WhatsAppMessageDirection.Inbound && senderAccountId != link.AccountId) return;
        if (direction == WhatsAppMessageDirection.Outbound
            && (await links.FindChatWaIdAsync(chatId, ct) is not { } waId
                || await accounts.FindVerifiedUserIdAsync(LoginMethodType.Phone, waId, ct) != link.AccountId))
            return;
        links.Add(WhatsAppLinkedMessage.Copy(link, direction, text, mediaKind, atUtc));
        link.RecordMessage(atUtc, text);
    }

    /// <summary>Al quitar un teléfono, borra los vínculos de esa cuenta para ese número en su espacio personal.</summary>
    internal async Task RemoveForPhoneAsync(Guid accountId, PhoneNumber phone, CancellationToken ct)
    {
        var personal = (await accesses.ListForUserAsync(accountId, ct))
            .FirstOrDefault(access => access.Kind == TenantKind.Personal)?.TenantId;
        if (personal is { } consumerTenantId)
            await links.RemoveByAccountAndPhoneAsync(consumerTenantId, accountId, phone.Value, ct);
    }

    private static Guid? Owner(WhatsAppInboundContext context) =>
        context.Message.OriginalUserId is { } userId && context.OriginalMethodOwned
        && context.OriginalAccount is { Status: UserStatus.Active, IsPlatformOperator: false } account && account.Id == userId ? userId : null;

    private async Task<Guid?> PersonalSpaceAsync(Guid accountId, CancellationToken ct) =>
        (await accesses.ListForUserAsync(accountId, ct)).FirstOrDefault(access => access.Kind == TenantKind.Personal
            && access.TenantStatus == TenantStatus.Active && access.MemberStatus == MemberStatus.Active)?.TenantId;
}

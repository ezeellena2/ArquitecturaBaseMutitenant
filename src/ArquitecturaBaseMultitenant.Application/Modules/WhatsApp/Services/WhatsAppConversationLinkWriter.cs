using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Vincula una conversación de clientes con la cuenta de quien escribe y le copia los mensajes (D2, D5), dentro de la
/// transacción del lado de la organización. Solo se ofrece y se crea si el número es un teléfono verificado de una cuenta
/// activa con espacio personal; lo que entra después solo se copia mientras el número siga siendo de esa cuenta.
/// </summary>
internal sealed class WhatsAppConversationLinkWriter(IWhatsAppConversationLinkRepository links, IUserTenantAccessReader accesses,
    ITenantSettingsReader settings, IWhatsAppChannelRepository channels)
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

    /// <summary>Copia un mensaje que se dijo con la persona; lo que entra, solo si lo mandó la cuenta vinculada.</summary>
    internal async Task CopyAsync(Guid chatId, WhatsAppMessageDirection direction, string? text, WhatsAppMediaKind? mediaKind, DateTime atUtc,
        Guid? senderAccountId, CancellationToken ct)
    {
        if (await links.GetByChatAsync(chatId, ct) is not { } link) return;
        if (direction == WhatsAppMessageDirection.Inbound && senderAccountId != link.AccountId) return;
        links.Add(WhatsAppLinkedMessage.Copy(link, direction, text, mediaKind, atUtc));
        link.RecordMessage(atUtc, text);
    }

    private static Guid? Owner(WhatsAppInboundContext context) =>
        context.Message.OriginalUserId is { } userId && context.OriginalMethodOwned
        && context.OriginalAccount is { Status: UserStatus.Active, IsPlatformOperator: false } account && account.Id == userId ? userId : null;

    private async Task<Guid?> PersonalSpaceAsync(Guid accountId, CancellationToken ct) =>
        (await accesses.ListForUserAsync(accountId, ct)).FirstOrDefault(access => access.Kind == TenantKind.Personal
            && access.TenantStatus == TenantStatus.Active && access.MemberStatus == MemberStatus.Active)?.TenantId;
}

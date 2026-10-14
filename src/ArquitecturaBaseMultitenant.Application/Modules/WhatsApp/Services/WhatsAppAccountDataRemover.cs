using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Quita de WhatsApp lo que es de una cuenta eliminada, en la UoW y el alcance de cada paso de la baja: su nombre en lo que
/// escribió (respuestas y notas), las conversaciones que tenía tomadas y sus vínculos con las copias de los mensajes.
/// </summary>
internal sealed class WhatsAppAccountDataRemover(IWhatsAppChatRepository chats, IPlatformWhatsAppChatRepository platformChats,
    IWhatsAppConversationLinkRepository links)
{
    internal async Task ForgetOrganizationAuthorAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var chat in await chats.ListTakenByAsync(userId, ct)) chat.Transfer(null, null, nowUtc);
        foreach (var message in await chats.ListAuthoredByAsync(userId, ct)) message.ForgetAuthor();
    }

    internal async Task ForgetPlatformAuthorAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var chat in await platformChats.ListTakenByAsync(userId, ct)) chat.Transfer(null, null, nowUtc);
        foreach (var message in await platformChats.ListAuthoredByAsync(userId, ct)) message.ForgetAuthor();
    }

    /// <summary>Sus vínculos desaparecen con sus copias: la organización ve otra vez la conversación sin vincular.</summary>
    internal async Task RemoveLinksAsync(Guid consumerTenantId, CancellationToken ct)
    {
        foreach (var link in await links.ListForConsumerAsync(consumerTenantId, ct)) links.Remove(link);
    }
}

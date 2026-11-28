using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Limpieza en la UoW llamadora con cuenta bloqueada: contacto antes de mensajes, sin tomar Phone después.</summary>
internal sealed class WhatsAppContactRemover(IWhatsAppContactRepository contacts, IWhatsAppMessageRepository messages,
    WhatsAppConversationLinkWriter links)
{
    internal async Task RemoveMethodAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct)
    {
        await contacts.LockAsync(phone.Value, ct);
        var contact = await contacts.GetAsync(phone.Value, ct);
        if (contact?.UserId == userId && contact.MethodId == methodId) contacts.Remove(contact);
        foreach (var message in await messages.ListOriginalForUpdateAsync(userId, methodId, ct)) message.ClearPersonalData();
        await links.RemoveForPhoneAsync(userId, phone, ct);
    }

    internal async Task RemoveAccountAsync(Guid userId, CancellationToken ct)
    {
        var numbers = await contacts.ListNumbersByUserAsync(userId, ct);
        foreach (var number in numbers) await contacts.LockAsync(number, ct);
        foreach (var number in numbers)
        {
            var contact = await contacts.GetAsync(number, ct);
            if (contact?.UserId == userId) contacts.Remove(contact);
        }
        foreach (var message in await messages.ListOriginalForUpdateAsync(userId, null, ct)) message.ClearPersonalData();
    }
}

using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Recibe posesión ya verificada por el núcleo, incluso antes de agregar el método de un alta nueva.</summary>
internal sealed class WhatsAppPhoneLinkObserver(IWhatsAppContactRepository contacts, WhatsAppContactRemover remover,
    TimeProvider clock) : IPhoneLinkObserver
{
    public async Task VerifiedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct)
    {
        await contacts.LockAsync(phone.Value, ct);
        var contact = await contacts.GetAsync(phone.Value, ct);
        if (contact is null) { contact = WhatsAppContact.Create(phone, clock.GetUtcNow().UtcDateTime); contacts.Add(contact); }
        if (contact.UserId is { } owner && (owner != userId || contact.MethodId != methodId)) return;
        contact.Link(methodId, userId);
    }
    public Task RemovedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct) =>
        remover.RemoveMethodAsync(userId, methodId, phone, ct);
    public Task AccountDeletedAsync(Guid userId, CancellationToken ct) => remover.RemoveAccountAsync(userId, ct);
}

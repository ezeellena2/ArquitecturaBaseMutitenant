using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Guarda, dentro de la UoW del alta, el contacto del Dueño invitado para que Plataforma lo muestre desde el
/// alta. La base lo borra sola cuando la invitación deja de estar pendiente.
/// </summary>
public interface IPendingTenantOwnerRepository
{
    void Add(Guid tenantId, Guid invitationId, Email email);
}

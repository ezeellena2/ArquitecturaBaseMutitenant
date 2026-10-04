using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Agrega la copia técnica dentro de la transacción del alta; runtime no puede borrarla ni cambiarla.</summary>
internal sealed class PendingTenantOwnerRepository(ApplicationDbContext context) : IPendingTenantOwnerRepository
{
    public void Add(Guid tenantId, Guid invitationId, Email email)
    {
        ArgumentNullException.ThrowIfNull(email);
        context.RequireTransaction();
        context.PendingTenantOwners.Add(new PendingTenantOwner(invitationId, tenantId, email.Value));
    }
}

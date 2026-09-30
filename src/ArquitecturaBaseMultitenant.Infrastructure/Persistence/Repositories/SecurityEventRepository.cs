using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Agrega eventos globales de seguridad al ChangeTracker de una transacción. El UnitOfWork confirma el evento junto con la acción que lo originó.</summary>
internal sealed class SecurityEventRepository(ApplicationDbContext context) : ISecurityEventRepository
{
    public void Add(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        context.RequireTransaction();
        context.SecurityEvents.Add(securityEvent);
    }
}

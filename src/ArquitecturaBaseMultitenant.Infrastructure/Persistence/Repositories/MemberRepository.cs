using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara las membresías privadas del tenant activo para los casos de uso de alta o cambio. Exige alcance de tenant y transacción antes de leer o agregar.</summary>
internal sealed class MemberRepository(ApplicationDbContext context, ITenantContext tenantContext) : IMemberRepository
{
    public Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return context.Members.SingleOrDefaultAsync(
            member => member.TenantId == tenantId && member.UserId == userId, cancellationToken);
    }

    public void Add(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Members.Add(member);
    }
}

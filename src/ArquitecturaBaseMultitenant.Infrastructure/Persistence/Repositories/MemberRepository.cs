using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara las membresías privadas del tenant activo para los casos de uso de alta o cambio. Exige alcance de tenant y transacción antes de leer o agregar.</summary>
internal sealed class MemberRepository(ApplicationDbContext context, ITenantContext tenantContext) : IMemberRepository
{
    public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(tenantContext.RequiredTenantId, "member-user", userId)], cancellationToken);
    public Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Members.SingleOrDefaultAsync(member => member.Id == id, cancellationToken);
    }

    public async Task<Member?> GetByIdAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        var member = await context.Members.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (member is not null)
            context.Entry(member).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return member;
    }

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

    public void MarkUpdated(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(member).State = EntityState.Modified;
    }
}

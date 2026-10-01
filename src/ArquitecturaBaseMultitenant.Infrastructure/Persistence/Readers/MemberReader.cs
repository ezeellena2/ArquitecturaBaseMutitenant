using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>La consulta parte siempre de Members y une la identidad solo para ese miembro.</summary>
internal sealed class MemberReader(ApplicationDbContext context, ITenantContext tenantContext) : IMemberReader
{
    public Task<MemberRow?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return Members(tenantId, userId).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberRow>> ListCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return await Members(tenantId).ToArrayAsync(cancellationToken);
    }

    private IQueryable<MemberRow> Members(Guid tenantId, Guid? userId = null)
    {
        var members = context.Members.AsNoTracking().Where(member => member.TenantId == tenantId);
        if (userId is { } id)
        {
            members = members.Where(member => member.UserId == id);
        }

        return from member in members.OrderBy(member => member.UserId)
            join user in context.Users.AsNoTracking() on member.UserId equals user.Id
            select new MemberRow(member.TenantId, user.Id, member.Status,
                user.Status, user.DisplayName);
    }
}

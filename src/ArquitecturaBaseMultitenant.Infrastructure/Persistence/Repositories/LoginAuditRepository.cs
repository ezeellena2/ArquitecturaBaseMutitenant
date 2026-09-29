using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class LoginAuditRepository(ApplicationDbContext context) : ILoginAuditRepository
{
    public void Add(LoginAudit audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        context.RequireTransaction();
        context.LoginAudits.Add(audit);
    }

    public Task<DateTime?> FindLastSuccessAtUtcAsync(Guid userId, CancellationToken cancellationToken) =>
        context.LoginAudits.AsNoTracking()
            .Where(audit => audit.UserId == userId && audit.Succeeded)
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .Select(audit => (DateTime?)audit.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
